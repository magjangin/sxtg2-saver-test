using MelonLoader;
using sxtg2_saver_test.Helpers;
using sxtg2_saver_test.Patches;

[assembly: MelonInfo(typeof(sxtg2_saver_test.SixtarBypassMod), sxtg2_saver_test.ModInfo.Name, sxtg2_saver_test.ModInfo.Version, sxtg2_saver_test.ModInfo.Author)]
[assembly: MelonGame(null, null)]

// Harmony 의 자동 PatchAll 을 끈다. 패치 하나가 실패하면 그 뒤 패치가 전부 적용되지 않기 때문에,
// PatchRegistry 에서 하나씩 격리해서 적용한다. (Helpers/PatchRegistry.cs 참고)
[assembly: HarmonyDontPatchAll]

namespace sxtg2_saver_test
{
    /// <summary>모드 이름/버전의 단일 출처. MelonInfo, AssemblyInfo, 로그가 모두 여기를 참조한다.</summary>
    public static class ModInfo
    {
        public const string Name = "Sixtar Gate Version & Network Bypass";
        public const string Version = "1.9.3";
        public const string Author = "Hwa-young-wang";
    }

    public class SixtarBypassMod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("==================================================");
            LoggerInstance.Msg($" Sixtar Gate: STARTRAIL Full Bypass Mod Loaded (v{ModInfo.Version})");
            LoggerInstance.Msg(" Architecture: Modularized & Resilience-Enhanced");
            LoggerInstance.Msg("==================================================");

            PatchRegistry.LogGameAssemblyFingerprint();

            // [MelonGame(null, null)] 이라 다른 Unity 게임에도 로드될 수 있다.
            // 대상 게임이 아니면 패치를 시도하지 않는다 (엉뚱한 게임의 동작을 건드리지 않기 위해).
            if (!PatchRegistry.IsTargetGameLoaded())
            {
                LoggerInstance.Warning("ManagerWebRequest 타입이 없습니다. Sixtar Gate 가 아닌 것 같아 패치를 적용하지 않습니다.");
                return;
            }

            PatchRegistry.ApplyContainer(HarmonyInstance, typeof(NetworkPatches));
            PatchRegistry.ApplyContainer(HarmonyInstance, typeof(DataPatches));
            PatchRegistry.ApplyContainer(HarmonyInstance, typeof(UIPatches));

            PatchRegistry.PrintReport();
        }
    }
}
