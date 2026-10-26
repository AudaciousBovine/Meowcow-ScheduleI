namespace MeowcowCore.Menu
{
    /// <summary>
    /// The vanilla main menu paths Core needs (from the scene dump). If a game update renames
    /// something, fix it here.
    /// </summary>
    public static class MenuPaths
    {
        /// <summary>The main menu's home screen: a plain MenuScreen with OpenOnStart.</summary>
        public const string HomeName = "Home";

        // Under Home
        public const string List = "Bank/Panel";           // the button list
        public const string Version = "Bank/Title/Text";   // the version text under the logo

        // Children of the list's buttons (each button has the same structure)
        public const string SettingsButton = "Settings";
        public const string ButtonText = "ButtonText";                                  // the label shown
        public const string ButtonTextHighlighted = "ButtonText Highlighted Position";  // where it slides on hover
        public const string ButtonShine = "Shine";                                      // the bar that fades in on hover

        // Under LobbyInterface.Container
        public const string LobbyEntries = "Entries";      // the invite button and player slots

        // Core's own additions
        public const string OptionsButtonName = "MeowcowOptions";
        public const string LabelName = "MeowcowLabel";
    }
}
