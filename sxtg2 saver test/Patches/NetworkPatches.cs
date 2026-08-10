using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using RhythmGame;
using RhythmGame.LicenseTest;
using RhythmGame.SixtarServer;
using sxtg2_saver_test.Helpers;

namespace sxtg2_saver_test.Patches
{
    public static class NetworkPatches
    {
        // 0. WebRequest: CommonErrorAction Suppressor
        [HarmonyPatch]
        public static class CommonErrorActionPatch
        {
            private static MethodBase _target;

            public static bool Prepare()
            {
                _target = ReflectionUtils.FindMethod(
                    new[] { typeof(ManagerWebRequest) },
                    new[] { "CommonErrorAction" }
                );
                return _target != null;
            }

            public static MethodBase TargetMethod() => _target;

            public static bool Prefix(ref IEnumerator __result)
            {
                MelonLogger.Msg("[Bypass] Suppressed CommonErrorAction! Hiding loading overlay.");
                if (ManagerPopup.Instance != null)
                {
                    ManagerPopup.Instance.HidePopupOverlay();
                }
                if (InputListener.Instance != null)
                {
                    InputListener.Instance.InputEnabled = true;
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

            public static bool Prepare()
            {
                _target = ReflectionUtils.FindMethod(
                    new[] { typeof(ManagerWebRequest) },
                    new[] { "FetchServer" }
                );
                return _target != null;
            }

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

            public static bool Prepare()
            {
                _target = ReflectionUtils.FindMethod(
                    new[] { typeof(ManagerWebRequest) },
                    new[] { "GetUnlockData" }
                );
                return _target != null;
            }

            public static MethodBase TargetMethod() => _target;

            public static bool Prefix()
            {
                MelonLogger.Msg("[Bypass] GetUnlockData intercepted!");
                try
                {
                    ItemHelper.Instance?.InitializeUnlockData(new List<string>());
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

            public static bool Prepare()
            {
                _target = ReflectionUtils.FindMethod(
                    new[] { typeof(ManagerWebRequest) },
                    new[] { "RefreshRequest", "RefreshUserData" }
                );
                return _target != null;
            }

            public static MethodBase TargetMethod() => _target;

            public static bool Prefix()
            {
                return false;
            }
        }

        // 4. WebRequest: RequestItemUnlock
        [HarmonyPatch]
        public static class RequestItemUnlockPatch
        {
            private static MethodBase _target;

            public static bool Prepare()
            {
                _target = ReflectionUtils.FindMethod(
                    new[] { typeof(ManagerWebRequest) },
                    new[] { "RequestItemUnlock" }
                );
                return _target != null;
            }

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

            public static bool Prepare()
            {
                _target = ReflectionUtils.FindMethod(
                    new[] { typeof(ManagerWebRequest) },
                    new[] { "GetUserArkk" }
                );
                return _target != null;
            }

            public static MethodBase TargetMethod() => _target;

            public static bool Prefix(Action<int> onComplete)
            {
                int arkk = UserAccountModule.Instance != null ? UserAccountModule.Instance.CurrentUserArkk : 0;
                onComplete?.Invoke(arkk);
                return false;
            }
        }

        // 6. WebRequest: CheckServerConnection
        [HarmonyPatch]
        public static class CheckServerConnectionPatch
        {
            private static MethodBase _target;

            public static bool Prepare()
            {
                _target = ReflectionUtils.FindMethod(
                    new[] { typeof(ManagerWebRequest) },
                    new[] { "CheckServerConnection" }
                );
                return _target != null;
            }

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
            private static MethodBase _target;

            public static bool Prepare()
            {
                _target = ReflectionUtils.FindMethod(
                    new[] { typeof(ManagerWebRequest) },
                    new[] { "GetRemoteVersion" }
                );
                return _target != null;
            }

            public static MethodBase TargetMethod() => _target;

            public static bool Prefix(string abName, Action<int> onComplete)
            {
                onComplete?.Invoke(207);
                return false;
            }
        }

        // 8. WebRequest: PostHighScoreData
        [HarmonyPatch]
        public static class PostHighScoreDataPatch
        {
            private static MethodBase _target;

            public static bool Prepare()
            {
                _target = ReflectionUtils.FindMethod(
                    new[] { typeof(ManagerWebRequest) },
                    new[] { "PostHighScoreData" }
                );
                return _target != null;
            }

            public static MethodBase TargetMethod() => _target;

            public static bool Prefix()
            {
                return false;
            }
        }

        // 9. WebRequest: GetHighScoreList
        [HarmonyPatch]
        public static class GetHighScoreListPatch
        {
            private static MethodBase _target;

            public static bool Prepare()
            {
                _target = ReflectionUtils.FindMethod(
                    new[] { typeof(ManagerWebRequest) },
                    new[] { "GetHighScoreList" }
                );
                return _target != null;
            }

            public static MethodBase TargetMethod() => _target;

            public static bool Prefix(Action<ScoreList> callback)
            {
                callback?.Invoke(new ScoreList());
                return false;
            }
        }

        // 10. WebRequest: GetAttendanceEventTable
        [HarmonyPatch]
        public static class GetAttendanceEventTablePatch
        {
            private static MethodBase _target;

            public static bool Prepare()
            {
                _target = ReflectionUtils.FindMethod(
                    new[] { typeof(ManagerWebRequest) },
                    new[] { "GetAttendanceEventTable" }
                );
                return _target != null;
            }

            public static MethodBase TargetMethod() => _target;

            public static bool Prefix(Action _onComplete)
            {
                MelonLogger.Msg("[Bypass] GetAttendanceEventTable intercepted!");
                _onComplete?.Invoke();
                return false;
            }
        }

        // 11. WebRequest: GetAttendanceReward
        [HarmonyPatch]
        public static class GetAttendanceRewardPatch
        {
            private static MethodBase _target;

            public static bool Prepare()
            {
                _target = ReflectionUtils.FindMethod(
                    new[] { typeof(ManagerWebRequest) },
                    new[] { "GetAttendanceReward" }
                );
                return _target != null;
            }

            public static MethodBase TargetMethod() => _target;

            public static bool Prefix(Action<AttendanceReward> onComplete)
            {
                MelonLogger.Msg("[Bypass] GetAttendanceReward intercepted!");
                onComplete?.Invoke(new AttendanceReward { gotReward = false });
                return false;
            }
        }

        // 12. WebRequest: GetNewsList
        [HarmonyPatch]
        public static class GetNewsListPatch
        {
            private static MethodBase _target;

            public static bool Prepare()
            {
                _target = ReflectionUtils.FindMethod(
                    new[] { typeof(ManagerWebRequest) },
                    new[] { "GetNewsList" }
                );
                return _target != null;
            }

            public static MethodBase TargetMethod() => _target;

            public static bool Prefix(Action<List<NewsInfo>> onComplete, Action onFailed)
            {
                MelonLogger.Msg("[Bypass] GetNewsList intercepted!");
                onComplete?.Invoke(new List<NewsInfo>());
                return false;
            }
        }

        // 13. WebRequest: GetUserLicenseLevel
        [HarmonyPatch]
        public static class GetUserLicenseLevelPatch
        {
            private static MethodBase _target;

            public static bool Prepare()
            {
                _target = ReflectionUtils.FindMethod(
                    new[] { typeof(ManagerWebRequest) },
                    new[] { "GetUserLicenseLevel" }
                );
                return _target != null;
            }

            public static MethodBase TargetMethod() => _target;

            public static bool Prefix(Action<LicenseLevel> onComplete)
            {
                MelonLogger.Msg("[Bypass] GetUserLicenseLevel intercepted!");
                onComplete?.Invoke(new LicenseLevel());
                return false;
            }
        }

        // 14. WebRequest: GetUnityQuestProcess
        [HarmonyPatch]
        public static class GetUnityQuestProcessPatch
        {
            private static MethodBase _target;

            public static bool Prepare()
            {
                _target = ReflectionUtils.FindMethod(
                    new[] { typeof(ManagerWebRequest) },
                    new[] { "GetUnityQuestProcess" }
                );
                return _target != null;
            }

            public static MethodBase TargetMethod() => _target;

            public static bool Prefix(Action<List<NoticeInfo>> onComplete)
            {
                MelonLogger.Msg("[Bypass] GetUnityQuestProcess intercepted!");
                onComplete?.Invoke(new List<NoticeInfo>());
                return false;
            }
        }

        // 15. WebRequest: GetUnityQuestServer
        [HarmonyPatch]
        public static class GetUnityQuestServerPatch
        {
            private static MethodBase _target;

            public static bool Prepare()
            {
                _target = ReflectionUtils.FindMethod(
                    new[] { typeof(ManagerWebRequest) },
                    new[] { "GetUnityQuestServer" }
                );
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
