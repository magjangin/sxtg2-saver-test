using System;
using System.Reflection;
using HarmonyLib;
using Inventory;
using MelonLoader;
using RhythmGame;
using RhythmGame.ModeSelect;
using RhythmGame.MusicSelect;
using UnityEngine;
using sxtg2_saver_test.Helpers;

namespace sxtg2_saver_test.Patches
{
    public static class UIPatches
    {
        // 1. UpdateChecker UI 비활성화 & 최신 버전 완벽 콜백
        [HarmonyPatch]
        public static class UpdateCheckerPatch
        {
            private static MethodBase _target;

            public static bool Prepare()
            {
                _target = ReflectionUtils.FindMethod(
                    new[] { typeof(UpdateChecker) },
                    new[] { "Check" }
                );
                return _target != null;
            }

            public static MethodBase TargetMethod() => _target;

            public static bool Prefix(UpdateChecker __instance, Action onLatestVersion, Action onRequireUpdate)
            {
                MelonLogger.Msg("[Bypass] UpdateChecker.Check intercepted! Hiding UpdateChecker GameObject.");
                if (__instance != null && __instance.gameObject != null)
                {
                    __instance.gameObject.SetActive(false);
                }
                if (UserAccountModule.Instance != null)
                {
                    UserAccountModule.Instance.runtimeCheck.isUpdateChecked = true;
                }
                onLatestVersion?.Invoke();
                return false;
            }
        }

        // 2. ManagerPopup: ShowCriticalErrorPopup 차단
        [HarmonyPatch]
        public static class ShowCriticalErrorPopupPatch
        {
            private static MethodBase _target;

            public static bool Prepare()
            {
                _target = ReflectionUtils.FindMethod(
                    new[] { typeof(ManagerPopup) },
                    new[] { "ShowCriticalErrorPopup", "ShowErrorPopup", "ShowCriticalError" }
                );
                return _target != null;
            }

            public static MethodBase TargetMethod() => _target;

            public static bool Prefix()
            {
                MelonLogger.Msg($"[Bypass] Suppressed Critical Error Popup");
                return false;
            }
        }

        // 3. UserAccountModule: CheckTutorial 스킵
        [HarmonyPatch]
        public static class CheckTutorialBypass
        {
            private static MethodBase _target;

            public static bool Prepare()
            {
                _target = ReflectionUtils.FindMethod(
                    new[] { typeof(UserAccountModule) },
                    new[] { "CheckTutorial" }
                );
                return _target != null;
            }

            public static MethodBase TargetMethod() => _target;

            public static bool Prefix(ref bool __result)
            {
                __result = false;
                return false;
            }
        }

        // 4. ManagerMusicSelect.Start 완전 오버라이드
        [HarmonyPatch]
        public static class ManagerMusicSelectStartOverride
        {
            private static MethodBase _target;

            public static bool Prepare()
            {
                _target = ReflectionUtils.FindMethod(
                    new[] { typeof(ManagerMusicSelect) },
                    new[] { "Start" }
                );
                return _target != null;
            }

            public static MethodBase TargetMethod() => _target;

            public static bool Prefix(ManagerMusicSelect __instance)
            {
                MelonLogger.Msg("[Bypass] Overriding ManagerMusicSelect.Start cleanly!");
                try
                {
                    Util.DeserializeInventoryItemFromCSV();
                    if (RhythmGameUtils.trackDatas == null || RhythmGameUtils.trackDatas.Count == 0)
                    {
                        RhythmGameUtils.trackDatas = RhythmGameUtils.GetTrackDataListFromCSV();
                    }

                    var us = UserAccountModule.Instance?.userSettingCache ?? new SXGT_UserSettingCache();
                    if (UserAccountModule.Instance != null)
                    {
                        UserAccountModule.Instance.userSettingCache = us;
                    }
                    if (UserAccountModule.Instance?.userData != null)
                    {
                        UserAccountModule.Instance.userData.tutorialProgress = 99;
                    }
                    us.lastSelectedCategoryID = "ALL TRACKS";

                    // 원본은 InitializeMusicSelect 에서 인스턴스 필드 us 를 채운다. 여기서 안 채우면
                    // Unity 가 만들어 둔 빈 SXGT_UserSettingCache 에 마지막 선택 곡/레벨/카테고리가 기록되고
                    // 아무도 저장하지 않아서 다음 진입 때 전부 초기화된다.
                    ReflectionUtils.SetFieldValue(__instance, typeof(ManagerMusicSelect), us, "us");

                    // Resilient field retrieval
                    Animator anim = ReflectionUtils.GetFieldValue<Animator>(__instance, typeof(ManagerMusicSelect), "animator");
                    if (anim == null) anim = __instance.GetComponent<Animator>();
                    ReflectionUtils.SetFieldValue(__instance, typeof(ManagerMusicSelect), anim, "animator");

                    SFXModule sfx = ReflectionUtils.GetFieldValue<SFXModule>(__instance, typeof(ManagerMusicSelect), "sfxModule");
                    sfx?.InitializeAllMemberInDic();

                    // 원본 InitializeMusicSelect 의 볼륨 초기화. 이게 빠지면 targetPreviewVolume /
                    // targetBGMVolume 이 0f 로 남고, PlayTrackPreviewRoutine 이 previewSource.volume 에
                    // 그 0 을 그대로 넣어서 프리뷰도 셀렉 BGM 도 무음이 된다.
                    try
                    {
                        __instance.SetBGMSourceVolume(-1f);
                    }
                    catch (Exception volEx)
                    {
                        MelonLogger.Warning($"[Bypass] SetBGMSourceVolume 실패 -> 볼륨 필드 직접 주입 ({volEx.GetType().Name})");
                        ReflectionUtils.SetFieldValue(__instance, typeof(ManagerMusicSelect), Util.GetBGMVolume(), "targetBGMVolume");
                        ReflectionUtils.SetFieldValue(__instance, typeof(ManagerMusicSelect), Util.GetGameplayVolume(), "targetPreviewVolume");
                    }

                    GameObject opLayer = ReflectionUtils.GetFieldValue<GameObject>(__instance, typeof(ManagerMusicSelect), "operatorLayer");
                    if (opLayer != null) opLayer.SetActive(false);

                    ReflectionUtils.InvokeMethod(__instance, typeof(ManagerMusicSelect), "DisableWindows");

                    ReflectionUtils.SetFieldValue(__instance, typeof(ManagerMusicSelect), new SXGT_Favorites(), "favorites");

                    IndicatorTrackPlayResult infoViewer = ReflectionUtils.GetFieldValue<IndicatorTrackPlayResult>(__instance, typeof(ManagerMusicSelect), "infoViewer");
                    infoViewer?.InitializeIndicator(__instance);

                    TrackSortListViewer trackSortListViewer = ReflectionUtils.GetFieldValue<TrackSortListViewer>(__instance, typeof(ManagerMusicSelect), "trackSortListViewer");
                    trackSortListViewer?.InitializeTrackSorter(__instance);

                    RG_RankingView rankingView = ReflectionUtils.GetFieldValue<RG_RankingView>(__instance, typeof(ManagerMusicSelect), "rankingView");
                    rankingView?.InitializeRankingView(__instance);

                    TrackCategoryViewer categoryViewer = ReflectionUtils.GetFieldValue<TrackCategoryViewer>(__instance, typeof(ManagerMusicSelect), "categoryViewer");
                    if (categoryViewer != null)
                    {
                        categoryViewer.InitializeCategoryViewer(0);
                    }

                    var allTracks = RhythmGameUtils.GetTrackDataListByVisibility();
                    ReflectionUtils.SetFieldValue(__instance, typeof(ManagerMusicSelect), allTracks, "trackList", "trackDatas");

                    __instance.isOnInitializing = false;

                    ReflectionUtils.InvokeMethod(__instance, typeof(ManagerMusicSelect), "ChangeCategoryDelta", new object[] { 0 });
                    ReflectionUtils.InvokeMethod(__instance, typeof(ManagerMusicSelect), "ChangePlayStyleDelta", new object[] { 0, false });
                    ReflectionUtils.InvokeMethod(__instance, typeof(ManagerMusicSelect), "ChangeLevelDelta", new object[] { 0 });

                    try
                    {
                        ReflectionUtils.InvokeMethod(__instance, typeof(ManagerMusicSelect), "ListenMusicSelectManager");
                    }
                    catch { }

                    if (InputListener.Instance != null)
                    {
                        InputListener.Instance.ListenUI(__instance);
                        InputListener.Instance.InputEnabled = true;
                    }

                    MelonLogger.Msg($"[Bypass] Clean ManagerMusicSelect Start complete with PlayStyle active & Input listening enabled!");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[Bypass] Error in ManagerMusicSelect Start Override: {ex}");
                }

                return false;
            }
        }
    }
}
