using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using sxtg2_saver_test.Helpers;
using UnityEngine;

namespace sxtg2_saver_test.Patches
{
    public static class UIPatches
    {
        // 1. UpdateChecker UI 비활성화 & 최신 버전 완벽 콜백
        //    UpdateChecker.Check 는 버전에 따라 (onLatest, onRequireUpdate) 또는
        //    (onLatest, onRequireUpdate, onDisconnected) 이다. 앞의 두 파라미터만 바인딩하면 둘 다 처리된다.
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
                MarkUpdateChecked();
                onLatestVersion?.Invoke();
                return false;
            }

            /// <summary>
            /// UserAccountModule.runtimeCheck.isUpdateChecked = true.
            /// runtimeCheck 는 struct 라서 복사본을 고쳐 다시 대입해야 한다.
            /// 필드/구조체가 바뀐 버전에서도 패치가 깨지지 않도록 리플렉션(박싱된 값 수정)으로 접근한다.
            /// </summary>
            private static void MarkUpdateChecked()
            {
                UserAccountModule account = UserAccountModule.Instance;
                if (account == null) return;

                object runtimeCheck = ReflectionUtils.GetFieldValue<object>(account, typeof(UserAccountModule), "runtimeCheck");
                if (runtimeCheck == null) return;

                if (ReflectionUtils.SetFieldValue(runtimeCheck, runtimeCheck.GetType(), true, "isUpdateChecked"))
                {
                    ReflectionUtils.SetFieldValue(account, typeof(UserAccountModule), runtimeCheck, "runtimeCheck");
                }
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
                MelonLogger.Msg("[Bypass] Suppressed Critical Error Popup");
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
