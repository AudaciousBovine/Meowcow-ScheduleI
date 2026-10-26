using MelonLoader;
using MeowcowCore.Menu;
using MeowcowThemes.Menu;
using MeowcowThemes.Settings;

[assembly: MelonInfo(typeof(MeowcowThemes.ThemesMod), "MeowcowThemes", "0.0.1", "Evelyna")]
[assembly: MelonGame("TVGS", "Schedule I")]
[assembly: MelonAdditionalDependencies("MeowcowCore")]

namespace MeowcowThemes
{
    /// <summary>
    /// Themes for the Meowcow suite: main menu layout, colours, hover style, the label and lobby styling.
    /// Builds on MeowcowCore's menu hooks; Core works the same without it, just with vanilla looks.
    /// </summary>
    public class ThemesMod : MelonMod
    {
        internal static MelonLogger.Instance Log;

        public override void OnInitializeMelon()
        {
            Log = LoggerInstance;
            ThemeSettings.Load();

            MainMenu.Ready += MainMenuLayout.OnMenuReady;
            LobbyToggle.Ready += LobbyStyle.OnLobbyReady;
            LobbyToggle.Changed += LobbyStyle.Refresh;
            ThemeSettings.Changed += MainMenuLayout.Refresh;
            ThemeSettings.Changed += LobbyStyle.Refresh;

            Log.Msg("MeowcowThemes loaded");
        }

        public override void OnUpdate()
        {
            ThemeSettings.Tick();
            MainMenuLayout.Tick();
            LobbyStyle.Tick();
        }
    }
}
