using MelonLoader;

[assembly: MelonInfo(typeof(sxtg2_saver_test.SixtarBypassMod), "Sixtar Gate Version & Network Bypass", "1.9.2", "Hwa-young-wang")]
[assembly: MelonGame(null, null)]

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
        }
    }
}
