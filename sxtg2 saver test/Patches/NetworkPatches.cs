using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using RhythmGame;
using RhythmGame.SixtarServer;
using sxtg2_saver_test.Helpers;

namespace sxtg2_saver_test.Patches
{
    /// <summary>
    /// ManagerWebRequest 의 서버 통신 메서드를 막고 "성공/빈 응답" 으로 콜백을 호출한다.
    ///
    /// 규칙
    ///  - 각 패치는 Prepare() 에서 대상을 찾고, 못 찾으면 false 를 돌려 건너뛴다 (게임 버전별 차이 흡수).
    ///  - Prefix 의 파라미터 이름은 원본 메서드의 파라미터 이름과 같아야 Harmony 가 바인딩한다.
    ///    이름이 바뀌면 그 패치만 Failed 로 보고된다 (PatchRegistry 가 격리).
    ///  - [OptionalPatch] 는 신버전에만 있는 메서드다. 구버전에서 없는 것은 정상이다.
    /// </summary>
    public static class NetworkPatches
    {
        private static MethodBase Find(params string[] names)
        {
            return ReflectionUtils.FindMethod(new[] { typeof(ManagerWebRequest) }, names);
        }

        // 0. WebRequest: CommonErrorAction Suppressor
        [OptionalPatch("신버전 전용 메서드")]
        [HarmonyPatch]
        public static class CommonErrorActionPatch
        {
            private static MethodBase _target;

            public static bool Prepare() => (_target = Find("CommonErrorAction")) != null;
            public static MethodBase TargetMethod() => _target;

            public static bool Prefix(ref IEnumerator __result)
            {
                MelonLogger.Msg("[Bypass] Suppressed CommonErrorAction! Hiding loading overlay.");
                if (ManagerPopup.Instance != null)
                {
                    ReflectionUtils.InvokeMethod(ManagerPopup.Instance, typeof(ManagerPopup), "HidePopupOverlay");
                    ReflectionUtils.InvokeMethod(ManagerPopup.Instance, typeof(ManagerPopup), "OnCloseAllPopups");
                }
                Type inputType = ReflectionUtils.FindType("RG_InputManager", "InputListener");
                if (inputType != null)
                {
                    object inputInst = ReflectionUtils.GetFieldValue<object>(null, inputType, "Instance", "instance");
                    if (inputInst != null)
                    {
                        ReflectionUtils.SetFieldValue(inputInst, inputType, true, "inputEnabled", "InputEnabled");
                    }
                }
                __result = EmptyRoutine();
                return false;
            }

            private static IEnumerator EmptyRoutine()
            {
                yield break;
            }
        }

        // 1. WebRequest: FetchServer
        [HarmonyPatch]
        public static class FetchServerPatch
        {
            private static MethodBase _target;

            public static bool Prepare() => (_target = Find("FetchServer")) != null;
            public static MethodBase TargetMethod() => _target;

            public static bool Prefix(Action<bool> onComplete)
            {
                MelonLogger.Msg("[Bypass] FetchServer intercepted! Returning true.");
                onComplete?.Invoke(true);
                return false;
            }
        }

        // 2. WebRequest: GetUnlockData
        [HarmonyPatch]
        public static class GetUnlockDataPatch
        {
            private static MethodBase _target;

            public static bool Prepare() => (_target = Find("GetUnlockData")) != null;
            public static MethodBase TargetMethod() => _target;

            public static bool Prefix()
            {
                MelonLogger.Msg("[Bypass] GetUnlockData intercepted!");
                try
                {
                    Type unlockType = ReflectionUtils.FindType("ManagerUnlock", "ItemHelper");
                    if (unlockType != null)
                    {
                        object inst = ReflectionUtils.GetFieldValue<object>(null, unlockType, "Instance", "instance");
                        if (inst != null)
                        {
                            ReflectionUtils.InvokeMethod(inst, unlockType, "InitializeUnlockData", new object[] { new List<string>() });
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[Bypass] Exception in GetUnlockDataPatch: {ex}");
                }
                return false;
            }
        }

        // 3. WebRequest: RefreshRequest 차단
        [HarmonyPatch]
        public static class RefreshRequestPatch
        {
            private static MethodBase _target;

            public static bool Prepare() => (_target = Find("RefreshRequest", "RefreshUserData")) != null;
            public static MethodBase TargetMethod() => _target;

            public static bool Prefix() => false;
        }

        // 4. WebRequest: RequestItemUnlock
        [HarmonyPatch]
        public static class RequestItemUnlockPatch
        {
            private static MethodBase _target;

            public static bool Prepare() => (_target = Find("RequestItemUnlock")) != null;
            public static MethodBase TargetMethod() => _target;

            public static bool Prefix(string _itemID, Action<bool> onComplete)
            {
                onComplete?.Invoke(true);
                return false;
            }
        }

        // 5. WebRequest: GetUserArkk
        [HarmonyPatch]
        public static class GetUserArkkPatch
        {
            private static MethodBase _target;

            public static bool Prepare() => (_target = Find("GetUserArkk")) != null;
            public static MethodBase TargetMethod() => _target;

            public static bool Prefix(Action<int> onComplete)
            {
                int arkk = 0;
                if (UserAccountModule.Instance != null)
                {
                    if (UserAccountModule.Instance.userData != null)
                    {
                        arkk = UserAccountModule.Instance.userData.arkk;
                    }
                    else
                    {
                        arkk = ReflectionUtils.GetFieldValue<int>(UserAccountModule.Instance, typeof(UserAccountModule), "CurrentUserArkk");
                    }
                }
                onComplete?.Invoke(arkk);
                return false;
            }
        }

        // 6. WebRequest: CheckServerConnection
        [HarmonyPatch]
        public static class CheckServerConnectionPatch
        {
            private static MethodBase _target;

            public static bool Prepare() => (_target = Find("CheckServerConnection")) != null;
            public static MethodBase TargetMethod() => _target;

            public static bool Prefix(string httpURL, Action<bool> onFinish)
            {
                onFinish?.Invoke(true);
                return false;
            }
        }

        // 7. WebRequest: GetRemoteVersion
        [HarmonyPatch]
        public static class GetRemoteVersionPatch
        {
            /// <summary>
            /// 서버가 알려주는 "원격 에셋번들 버전" 대신 돌려줄 고정값.
            /// 게임이 이 값과 로컬 번들 버전을 비교해 다운로드/업데이트를 결정하므로,
            /// 로컬 번들을 "최신"으로 취급시키려는 값이다. 게임 갱신 후 번들 업데이트 팝업이
            /// 다시 뜨면 이 값부터 의심할 것 (값의 정확한 기준은 인게임 미확인).
            /// </summary>
            private const int FixedRemoteVersion = 207;

            private static MethodBase _target;

            public static bool Prepare() => (_target = Find("GetRemoteVersion")) != null;
            public static MethodBase TargetMethod() => _target;

            public static bool Prefix(string abName, Action<int> onComplete)
            {
                onComplete?.Invoke(FixedRemoteVersion);
                return false;
            }
        }

        // 8. WebRequest: PostHighScoreData
        [HarmonyPatch]
        public static class PostHighScoreDataPatch
        {
            private static MethodBase _target;

            public static bool Prepare() => (_target = Find("PostHighScoreData")) != null;
            public static MethodBase TargetMethod() => _target;

            public static bool Prefix() => false;
        }

        // 9. WebRequest: GetHighScoreList
        [HarmonyPatch]
        public static class GetHighScoreListPatch
        {
            private static MethodBase _target;

            public static bool Prepare() => (_target = Find("GetHighScoreList")) != null;
            public static MethodBase TargetMethod() => _target;

            public static bool Prefix(Action<ScoreList> callback)
            {
                callback?.Invoke(new ScoreList());
                return false;
            }
        }

        // 10. WebRequest: GetAttendanceEventTable
        [OptionalPatch("신버전 전용 메서드")]
        [HarmonyPatch]
        public static class GetAttendanceEventTablePatch
        {
            private static MethodBase _target;

            public static bool Prepare() => (_target = Find("GetAttendanceEventTable")) != null;
            public static MethodBase TargetMethod() => _target;

            public static bool Prefix(Action _onComplete)
            {
                MelonLogger.Msg("[Bypass] GetAttendanceEventTable intercepted!");
                _onComplete?.Invoke();
                return false;
            }
        }

        // 11. WebRequest: GetAttendanceReward
        [OptionalPatch("신버전 전용 메서드")]
        [HarmonyPatch]
        public static class GetAttendanceRewardPatch
        {
            private static MethodBase _target;

            public static bool Prepare() => (_target = Find("GetAttendanceReward")) != null;
            public static MethodBase TargetMethod() => _target;

            public static bool Prefix(Delegate onComplete)
            {
                MelonLogger.Msg("[Bypass] GetAttendanceReward intercepted!");
                PatchHelpers.InvokeWithDefault(onComplete, "GetAttendanceReward",
                    (inst, type) => ReflectionUtils.SetFieldValue(inst, type, false, "gotReward"));
                return false;
            }
        }

        // 12. WebRequest: GetNewsList
        [OptionalPatch("신버전 전용 메서드")]
        [HarmonyPatch]
        public static class GetNewsListPatch
        {
            private static MethodBase _target;

            public static bool Prepare() => (_target = Find("GetNewsList")) != null;
            public static MethodBase TargetMethod() => _target;

            public static bool Prefix(Delegate onComplete, Action onFailed)
            {
                MelonLogger.Msg("[Bypass] GetNewsList intercepted!");
                PatchHelpers.InvokeWithDefault(onComplete, "GetNewsList");
                return false;
            }
        }

        // 13. WebRequest: GetUserLicenseLevel
        [OptionalPatch("신버전 전용 메서드")]
        [HarmonyPatch]
        public static class GetUserLicenseLevelPatch
        {
            private static MethodBase _target;

            public static bool Prepare() => (_target = Find("GetUserLicenseLevel")) != null;
            public static MethodBase TargetMethod() => _target;

            public static bool Prefix(Delegate onComplete)
            {
                MelonLogger.Msg("[Bypass] GetUserLicenseLevel intercepted!");
                PatchHelpers.InvokeWithDefault(onComplete, "GetUserLicenseLevel");
                return false;
            }
        }

        // 14. WebRequest: GetUnityQuestProcess
        [OptionalPatch("신버전 전용 메서드")]
        [HarmonyPatch]
        public static class GetUnityQuestProcessPatch
        {
            private static MethodBase _target;

            public static bool Prepare() => (_target = Find("GetUnityQuestProcess")) != null;
            public static MethodBase TargetMethod() => _target;

            public static bool Prefix(Delegate onComplete)
            {
                MelonLogger.Msg("[Bypass] GetUnityQuestProcess intercepted!");
                PatchHelpers.InvokeWithDefault(onComplete, "GetUnityQuestProcess");
                return false;
            }
        }

        // 15. WebRequest: GetUnityQuestServer (오버로드가 2개라 Action<URLPort> 시그니처를 명시)
        [HarmonyPatch]
        public static class GetUnityQuestServerPatch
        {
            private static MethodBase _target;

            public static bool Prepare()
            {
                _target = ReflectionUtils.FindMethod(
                    new[] { typeof(ManagerWebRequest) },
                    new[] { "GetUnityQuestServer" },
                    new[] { typeof(Action<URLPort>) });
                return _target != null;
            }

            public static MethodBase TargetMethod() => _target;

            public static bool Prefix(Action<URLPort> onComplete)
            {
                MelonLogger.Msg("[Bypass] GetUnityQuestServer intercepted!");
                onComplete?.Invoke(new URLPort());
                return false;
            }
        }
    }
}
