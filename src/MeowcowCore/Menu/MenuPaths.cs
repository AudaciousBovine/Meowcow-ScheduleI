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

        // Window frame: children of a framed MenuScreen (Settings, and later Load Game / New Game)
        public const string FrameBackground = "Background";
        public const string FramePattern = "Background/Image";   // the pattern behind, 1000x842 in vanilla
        public const string FrameTitle = "Title";
        public const string FrameTabs = "TabsPanel";
        public const string FrameTabsLayout = "TabsPanel/TabsLayout";
        public const string FrameContent = "Content";
        public const string FrameLeftBumper = "Left bumper prompt";
        public const string FrameRightBumper = "Right bumper prompt";
        public const string TabBackground = "Background";          // under each tab
        public const string TabCheckmark = "Checkmark";            // under each tab: shown when selected
        public const string TabLabel = "Label";                    // under each tab

        // The dark small button style (the save slots' Import button), cloned for Back
        public const string DarkButtonName = "Import";

        // The loose "Esc Back" prompt in the screen corner, on the menu's prompt canvas
        public const string MenuPromptPivot = "BottomLeftMenuPivot";

        // Core's own additions
        public const string OptionsButtonName = "MeowcowOptions";
        public const string LabelName = "MeowcowLabel";
    }
}