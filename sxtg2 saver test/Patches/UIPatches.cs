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
                Type ucType = ReflectionUtils.FindType("UpdateChecker");
                if (ucType == null) return false;

                _target = ReflectionUtils.FindMethod(
                    new[] { ucType },
                    new[] { "Check" }
                );
                return _target != null;
            }

            public static MethodBase TargetMethod() => _target;

            public static bool Prefix(object __instance, Action onLatestVersion, Action onRequireUpdate)
            {
                MelonLogger.Msg("[Bypass] UpdateChecker.Check intercepted! Hiding UpdateChecker GameObject.");
                if (__instance is Component comp && comp.gameObject != null)
                {
                    comp.gameObject.SetActive(false);
                }
                if (UserAccountModule.Instance != null)
                {
                    var rc = UserAccountModule.Instance.runtimeCheck;
                    rc.isUpdateChecked = true;
                    UserAccountModule.Instance.runtimeCheck = rc;
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


    }
}
