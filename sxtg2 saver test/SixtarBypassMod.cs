using MelonLoader;
using sxtg2_saver_test.Helpers;
using sxtg2_saver_test.Patches;

[assembly: MelonInfo(typeof(sxtg2_saver_test.SixtarBypassMod), "Sixtar Gate Version & Network Bypass", "1.9.2", "Hwa-young-wang")]
[assembly: MelonGame(null, null)]

// Harmony 의 자동 PatchAll 을 끈다. 패치 하나가 실패하면 그 뒤 패치가 전부 적용되지 않기 때문에,
// PatchRegistry 에서 하나씩 격리해서 적용한다. (Helpers/PatchRegistry.cs 참고)
[assembly: HarmonyDontPatchAll]

namespace sxtg2_saver_test
{
    public class SixtarBypassMod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("==================================================");
            LoggerInstance.Msg(" Sixtar Gate: STARTRAIL Full Bypass Mod Loaded (v1.9.2)");
            LoggerInstance.Msg(" Architecture: Modularized & Resilience-Enhanced");
            LoggerInstance.Msg("==================================================");

            PatchRegistry.LogGameAssemblyFingerprint();

            PatchRegistry.ApplyContainer(HarmonyInstance, typeof(NetworkPatches));
            PatchRegistry.ApplyContainer(HarmonyInstance, typeof(DataPatches));
            PatchRegistry.ApplyContainer(HarmonyInstance, typeof(UIPatches));

            PatchRegistry.PrintReport();
        }
    }
}
