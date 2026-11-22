using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using MelonLoader;
using MelonLoader.Utils;

namespace sxtg2_saver_test.Helpers
{
    /// <summary>
    /// 이 패치는 일부 게임 버전에만 대상이 존재한다는 표시.
    /// 대상이 없거나(Skip) 대상 타입이 없어 로드에 실패해도 "실패"가 아니라 "해당 없음"으로 보고한다.
    /// 예) 신버전에만 있는 메서드를 막는 패치, 구/신버전 중 한쪽 타입만 쓰는 패치.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class OptionalPatchAttribute : Attribute
    {
        public string Reason { get; private set; }

        public OptionalPatchAttribute(string reason)
        {
            Reason = reason;
        }
    }

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
            /// <summary>이 게임 버전에는 대상이 없는 것이 정상인 선택 패치 ([OptionalPatch]).</summary>
            NotApplicable,
            /// <summary>대상을 못 찾아 건너뜀. 필수 패치라면 게임 업데이트를 의심해야 한다.</summary>
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

        private const string FingerprintFileName = "SixtarBypass.fingerprint.txt";
        private const string ReportFileName = "SixtarBypass.report.txt";

        /// <summary>패치 대상 게임인지 확인한다. [MelonGame(null, null)] 이라 아무 게임에나 로드될 수 있다.</summary>
        public static bool IsTargetGameLoaded()
        {
            return ReflectionUtils.FindType("ManagerWebRequest") != null;
        }

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
                    Add(NameOf(nested), IsOptional(nested) ? Result.NotApplicable : Result.Failed,
                        "어트리뷰트 해석 실패: " + Unwrap(ex).Message);
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
            string optionalReason;
            bool optional = TryGetOptionalReason(patchClass, out optionalReason);

            try
            {
                // Prepare() 가 false 를 반환하면 Patch() 는 null 을 돌려준다.
                List<MethodInfo> patched = harmony.CreateClassProcessor(patchClass).Patch();

                if (patched == null || patched.Count == 0)
                {
                    if (optional)
                    {
                        Add(name, Result.NotApplicable, "이 게임 버전에는 대상 없음 (" + optionalReason + ")");
                    }
                    else
                    {
                        Add(name, Result.Skipped, "대상 메서드를 찾지 못함");
                    }
                }
                else
                {
                    Add(name, Result.Applied, ResolveTargetName(patchClass, patched.Count));
                }
            }
            catch (Exception ex)
            {
                Exception root = Unwrap(ex);
                string detail = root.GetType().Name + ": " + root.Message;

                if (optional)
                {
                    // 사라진 타입을 시그니처에 쓰는 패치는 Prepare 를 JIT 하다가 여기로 온다.
                    Add(name, Result.NotApplicable, optionalReason + " [" + root.GetType().Name + "]");
                }
                else
                {
                    Add(name, Result.Failed, detail);
                }
            }
        }

        public static void PrintReport()
        {
            int applied = Entries.Count(e => e.Result == Result.Applied);
            int na = Entries.Count(e => e.Result == Result.NotApplicable);
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
            MelonLogger.Msg($"  적용 {applied} / 해당 없음 {na} / 건너뜀 {skipped} / 실패 {failed}");

            if (failed > 0 || skipped > 0)
            {
                MelonLogger.Warning("  필수 패치 일부가 적용되지 않았습니다. 게임이 업데이트되어 대상 메서드가");
                MelonLogger.Warning("  바뀌었을 수 있습니다. 위 SKIP/FAIL 항목의 대상 이름을 확인하세요.");
            }
            MelonLogger.Msg("----------------------------------------------");

            WriteReportFile(applied, na, skipped, failed);
        }

        /// <summary>
        /// 게임 어셈블리 지문. 게임이 갱신되면 MVID 가 바뀐다.
        /// 이전 실행의 MVID 를 UserData 에 기록해 두었다가, 달라졌으면 경고한다
        /// (게임이 업데이트되었거나 다른 사본으로 실행했다는 뜻 → 패치 리포트를 꼼꼼히 볼 것).
        /// </summary>
        public static void LogGameAssemblyFingerprint()
        {
            Assembly game = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

            if (game == null)
            {
                MelonLogger.Warning("[Env] Assembly-CSharp 을 찾지 못했습니다.");
                return;
            }

            string mvid = game.ManifestModule.ModuleVersionId.ToString();
            MelonLogger.Msg($"[Env] Assembly-CSharp version : {game.GetName().Version}");
            MelonLogger.Msg($"[Env] Assembly-CSharp MVID    : {mvid}");

            try
            {
                string path = Path.Combine(MelonEnvironment.UserDataDirectory, FingerprintFileName);
                string previous = File.Exists(path) ? File.ReadAllText(path).Trim() : null;

                if (previous == null)
                {
                    MelonLogger.Msg("[Env] 첫 실행으로 보고 MVID 를 기록합니다.");
                }
                else if (!string.Equals(previous, mvid, StringComparison.OrdinalIgnoreCase))
                {
                    MelonLogger.Warning($"[Env] 게임 어셈블리가 이전 실행과 다릅니다 (이전 MVID {previous}).");
                    MelonLogger.Warning("[Env] 게임이 업데이트되었을 수 있습니다. 아래 Patch Report 의 SKIP/FAIL 을 확인하세요.");
                }

                if (previous != mvid)
                {
                    File.WriteAllText(path, mvid);
                }
            }
            catch (Exception ex)
            {
                // 기록 실패는 모드 동작과 무관하다.
                MelonLogger.Warning("[Env] MVID 기록 건너뜀: " + ex.GetType().Name);
            }
        }

        /// <summary>리포트를 UserData 에 파일로도 남긴다. 버그 제보할 때 이 파일만 첨부하면 된다.</summary>
        private static void WriteReportFile(int applied, int na, int skipped, int failed)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Sixtar Bypass Patch Report  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine("version: " + ModInfo.Version);
                foreach (Entry e in Entries)
                {
                    sb.AppendLine($"[{Mark(e.Result)}] {e.Name}  {e.Detail}");
                }
                sb.AppendLine($"applied {applied} / n/a {na} / skipped {skipped} / failed {failed}");

                File.WriteAllText(Path.Combine(MelonEnvironment.UserDataDirectory, ReportFileName), sb.ToString());
            }
            catch
            {
                // 파일을 못 써도 콘솔 리포트가 있으므로 무시한다.
            }
        }

        private static string Mark(Result r)
        {
            switch (r)
            {
                case Result.Applied: return "OK  ";
                case Result.NotApplicable: return "N/A ";
                case Result.Skipped: return "SKIP";
                default: return "FAIL";
            }
        }

        private static bool IsOptional(Type t)
        {
            string ignored;
            return TryGetOptionalReason(t, out ignored);
        }

        private static bool TryGetOptionalReason(Type t, out string reason)
        {
            reason = null;
            try
            {
                var attr = (OptionalPatchAttribute)Attribute.GetCustomAttribute(t, typeof(OptionalPatchAttribute), false);
                if (attr == null) return false;
                reason = attr.Reason;
                return true;
            }
            catch
            {
                return false;
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
