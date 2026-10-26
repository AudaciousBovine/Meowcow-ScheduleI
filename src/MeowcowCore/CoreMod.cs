using MelonLoader;
using UnityEngine;
using MeowcowCore.Diagnostics;
using MeowcowCore.Settings;
using MeowcowCore.Menu;

[assembly: MelonInfo(typeof(MeowcowCore.CoreMod), "MeowcowCore", "0.0.1", "Evelyna")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace MeowcowCore
{
    public class CoreMod : MelonMod
    {
        internal static MelonLogger.Instance Log;

        // Debug settings stay in MelonPreferences.
        private MelonPreferences_Entry<KeyCode> _dumpKey;
        private MelonPreferences_Entry<bool> _dumpNeedsCtrl;

        public override void OnInitializeMelon()
        {
            Log = LoggerInstance;

            var cat = MelonPreferences.CreateCategory("MeowcowCore");
            _dumpKey       = cat.CreateEntry("SceneDumpKey", KeyCode.F11, "Write every UI canvas to UserData/MeowcowCore/Dumps (debug)");
            _dumpNeedsCtrl = cat.CreateEntry("SceneDumpRequireCtrl", true, "Only dump while Ctrl is held");

            CoreSettings.Load();
            CoreSettings.Changed += PlayMode.OnSettingsChanged;
            Log.Msg("MeowcowCore loaded");
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (sceneName == "Menu") MainMenuLayout.OnMenuLoaded();
            LobbyTweaks.OnSceneLoaded(); // main menu and game scenes both have a lobby panel
        }

        public override void OnUpdate()
        {
            CoreSettings.Tick();
            MainMenuLayout.Tick();
            LobbyTweaks.Tick();

            if (Input.GetKeyDown(_dumpKey.Value) && (!_dumpNeedsCtrl.Value || CtrlHeld()))
                SceneDump.Run();
        }

        private static bool CtrlHeld() =>
            Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
    }
}