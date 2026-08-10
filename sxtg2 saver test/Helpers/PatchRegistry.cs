using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MelonLoader;

namespace sxtg2_saver_test.Helpers
{
    /// <summary>
    /// 패치를 하나씩 격리해서 적용하고, 결과를 표로 보고한다.
    ///
    /// Harmony 의 PatchAll 은 패치 클래스를 순회하다가 하나라도 예외가 나면 그 자리에서 멈춘다.
    /// 게임이 업데이트돼서 타입이 사라지거나(TypeLoadException) 파라미터 이름이 바뀌면
    /// 그 뒤에 있던 멀쩡한 패치들까지 통째로 적용되지 않는다.
    /// 그래서 PatchAll 을 끄고(assembly: HarmonyDontPatchAll) 여기서 하나씩 try/catch 로 적용한다.
    /// </summary>
    public static class PatchRegistry
    {
        public enum Result
        {
            Applied,
            Skipped,
            Failed
        }

        private struct Entry
        {
            public string Name;
            public Result Result;
            public string Detail;
        }

        private static readonly List<Entry> Entries = new List<Entry>();

        /// <summary>컨테이너 클래스 안의 [HarmonyPatch] 중첩 클래스를 전부 적용한다.</summary>
        public static void ApplyContainer(HarmonyLib.Harmony harmony, Type container)
        {
            foreach (Type nested in container.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            {
                bool isPatchClass;
                try
                {
                    // 어트리뷰트가 사라진 게임 타입을 참조하고 있으면 여기서 터질 수 있다.
                    isPatchClass = nested.GetCustomAttributes(typeof(HarmonyPatch), false).Length > 0;
                }
                catch (Exception ex)
                {
                    Add(NameOf(nested), Result.Failed, "어트리뷰트 해석 실패: " + Unwrap(ex).Message);
                    continue;
                }

                if (isPatchClass)
                {
                    Apply(harmony, nested);
                }
            }
        }

        public static void Apply(HarmonyLib.Harmony harmony, Type patchClass)
        {
            string name = NameOf(patchClass);
            try
            {
                // Prepare() 가 false 를 반환하면 Patch() 는 null 을 돌려준다.
                List<MethodInfo> patched = harmony.CreateClassProcessor(patchClass).Patch();

                if (patched == null || patched.Count == 0)
                {
                    Add(name, Result.Skipped, "대상 메서드를 찾지 못함");
                }
                else
                {
                    Add(name, Result.Applied, ResolveTargetName(patchClass, patched.Count));
                }
            }
            catch (Exception ex)
            {
                Exception root = Unwrap(ex);
                Add(name, Result.Failed, root.GetType().Name + ": " + root.Message);
            }
        }

        public static void PrintReport()
        {
            int applied = Entries.Count(e => e.Result == Result.Applied);
            int skipped = Entries.Count(e => e.Result == Result.Skipped);
            int failed = Entries.Count(e => e.Result == Result.Failed);

            MelonLogger.Msg("---------------- Patch Report ----------------");
            foreach (Entry e in Entries)
            {
                string line = $"  [{Mark(e.Result)}] {e.Name}  {e.Detail}";
                if (e.Result == Result.Failed)
                {
                    MelonLogger.Error(line);
                }
                else if (e.Result == Result.Skipped)
                {
                    MelonLogger.Warning(line);
                }
                else
                {
                    MelonLogger.Msg(line);
                }
            }
            MelonLogger.Msg($"  적용 {applied} / 건너뜀 {skipped} / 실패 {failed}");

            if (failed > 0 || skipped > 0)
            {
                MelonLogger.Warning("  일부 패치가 적용되지 않았습니다. 게임이 업데이트되어 대상 메서드가");
                MelonLogger.Warning("  바뀌었을 수 있습니다. 위 항목의 대상 이름을 확인하세요.");
            }
            MelonLogger.Msg("----------------------------------------------");
        }

        /// <summary>게임 어셈블리 지문. 게임이 갱신되면 MVID 가 바뀐다.</summary>
        public static void LogGameAssemblyFingerprint()
        {
            Assembly game = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

            if (game == null)
            {
                MelonLogger.Warning("[Env] Assembly-CSharp 을 찾지 못했습니다.");
                return;
            }

            MelonLogger.Msg($"[Env] Assembly-CSharp version : {game.GetName().Version}");
            MelonLogger.Msg($"[Env] Assembly-CSharp MVID    : {game.ManifestModule.ModuleVersionId}");
        }

        private static string Mark(Result r)
        {
            switch (r)
            {
                case Result.Applied: return "OK  ";
                case Result.Skipped: return "SKIP";
                default: return "FAIL";
            }
        }

        private static string NameOf(Type t)
        {
            return (t.DeclaringType != null ? t.DeclaringType.Name + "." : "") + t.Name;
        }

        /// <summary>
        /// 패치된 원본 메서드 이름을 사람이 읽을 수 있게 만든다.
        /// Patch() 가 돌려주는 건 원본이 아니라 교체용 동적 메서드(DMD&lt;Foo&gt;?12345._Bar::Foo)라
        /// 그대로 찍으면 알아볼 수 없다. 패치 클래스가 알려주는 원본 정보를 우선 사용한다.
        /// </summary>
        private static string ResolveTargetName(Type patchClass, int count)
        {
            // 1) TargetMethod() 를 제공하는 패치 (Prepare 에서 이미 대상을 찾아둔 상태)
            try
            {
                MethodInfo tm = patchClass.GetMethod("TargetMethod",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (tm != null && tm.GetParameters().Length == 0)
                {
                    MethodBase target = tm.Invoke(null, null) as MethodBase;
                    if (target != null)
                    {
                        return (target.DeclaringType != null ? target.DeclaringType.Name + "." : "") + target.Name;
                    }
                }
            }
            catch
            {
                // 아래 경로로
            }

            // 2) [HarmonyPatch(typeof(X), "Method")] 어트리뷰트로 대상을 지정한 패치
            try
            {
                foreach (HarmonyPatch attr in patchClass.GetCustomAttributes(typeof(HarmonyPatch), false))
                {
                    if (attr.info != null && attr.info.declaringType != null)
                    {
                        return attr.info.declaringType.Name + "." + (attr.info.methodName ?? "?");
                    }
                }
            }
            catch
            {
                // 아래 경로로
            }

            return $"메서드 {count}개";
        }

        private static Exception Unwrap(Exception ex)
        {
            while (ex.InnerException != null)
            {
                ex = ex.InnerException;
            }
            return ex;
        }

        private static void Add(string name, Result result, string detail)
        {
            Entries.Add(new Entry { Name = name, Result = result, Detail = detail });
        }
    }
}
