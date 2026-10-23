using MelonLoader;

[assembly: MelonInfo(typeof(MeowcowCore.CoreMod), "MeowcowCore", "0.0.1", "Evelyna")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace MeowcowCore
{
    public class CoreMod : MelonMod
    {
        internal static MelonLogger.Instance Log;

        public override void OnInitializeMelon()
        {
            Log = LoggerInstance;
            Log.Msg("MeowcowCore loaded");
        }
    }
}
