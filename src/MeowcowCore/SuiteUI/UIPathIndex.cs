using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;
using MenuScreen = Il2CppScheduleOne.UI.MainMenu.MenuScreen;
using SettingsScreen = Il2CppScheduleOne.UI.MainMenu.SettingsScreen;
using UObject = UnityEngine.Object;

namespace MeowcowCore.SuiteUI;

/// <summary>
/// The only place that knows where vanilla UI lives. If a game update moves or renames something,
/// it's fixed here and nowhere else.
/// </summary>
/// <remarks>
/// [CHANGED] New (design §7). Only the main menu paths for now; other menus add theirs when they're built.
/// [SOURCE] Paths from the pre-rebuild <c>MenuPaths</c> (found with SceneDump); single-place rule from
/// Mirror's <c>VanillaOptions</c>.
/// </remarks>
public static class UIPathIndex
{
    // ---------- Main menu ----------

    /// <summary>The main menu's home screen object.</summary>
    public const string MainMenuHome = "Home";

    /// <summary>Under Home: the button list (Continue, New Game, Settings, ...).</summary>
    public const string MainMenuList = "Bank/Panel";

    /// <summary>Under Home: the version text under the logo.</summary>
    public const string MainMenuVersion = "Bank/Title/Text";

    /// <summary>In the list: the Settings button.</summary>
    public const string MainMenuSettingsButton = "Settings";

    // ---------- List buttons (every main menu list button has these children) ----------

    /// <summary>The label shown on the button.</summary>
    public const string ListButtonText = "ButtonText";

    /// <summary>Where the label slides to on hover; its X offset is the slide distance.</summary>
    public const string ListButtonTextHighlighted = "ButtonText Highlighted Position";

    // ---------- Framed screens (Settings, Load Game, New Game) ----------
    // [CHANGED] Added for MeowcowWindow, which uses vanilla's Settings screen as its shell.

    /// <summary>Under a framed screen: the title text.</summary>
    public const string WindowTitle = "Title";

    /// <summary>Under a framed screen: the content area (each category's panel lives here in vanilla).</summary>
    public const string WindowContent = "Content";

    /// <summary>Under a framed screen: the tab strip (holds vanilla's UITab, which cycles tabs on LB/RB).</summary>
    public const string WindowTabsPanel = "TabsPanel";

    /// <summary>Under a framed screen: the tab list (layout group and toggle group).</summary>
    public const string WindowTabsLayout = "TabsPanel/TabsLayout";

    /// <summary>Under a tab: its label.</summary>
    public const string WindowTabLabel = "Label";

    /// <summary>Under a framed screen: the LB and RB prompts shown with a gamepad.</summary>
    public const string WindowLeftBumper = "Left bumper prompt";
    public const string WindowRightBumper = "Right bumper prompt";

    /// <summary>Under the Settings screen: its version text (not wanted in a Meowcow window).</summary>
    public const string SettingsVersion = "Version";

    /// <summary>Under the Settings screen: the display-settings confirmation, a nested screen of its own.</summary>
    public const string SettingsConfirmDisplay = "ConfirmDisplaySettings";

    /// <summary>Under a framed screen: the background, which masks the pattern behind it.</summary>
    public const string WindowBackground = "Background";

    /// <summary>Under a framed screen: the pattern behind the window (1000×842 in vanilla).</summary>
    public const string WindowPattern = "Background/Image";

    // ---------- Other pieces ----------

    /// <summary>The save slots' small dark button: the style source for Back buttons.</summary>
    public const string ImportButton = "Import";

    /// <summary>The loose "Esc Back" prompt in the main menu's corner.</summary>
    public const string MenuCornerPrompt = "BottomLeftMenuPivot";

    /// <summary>
    /// Finds the save slots' Import button (main menu only). Null where there are no save slots.
    /// </summary>
    /// <remarks>[PERFORMANCE] Walks every Button in the scene; once per window build.</remarks>
    public static Button? FindImportButton()
    {
        foreach (Button button in UObject.FindObjectsOfType<Button>(true))
        {
            if (button.name == ImportButton) return button;
        }
        return null;
    }

    /// <summary>
    /// Finds vanilla's Settings screen (the main menu's, or the pause menu's in-game). Null if missing.
    /// </summary>
    /// <remarks>[PERFORMANCE] One object search; called once per window build, not per frame.</remarks>
    public static SettingsScreen? FindSettingsScreen() => UObject.FindObjectOfType<SettingsScreen>(true);

    /// <summary>
    /// Finds the main menu's home screen: the <c>MenuScreen</c> named Home that opens on start and has
    /// no previous screen. Null when not on the main menu.
    /// </summary>
    /// <remarks>[PERFORMANCE] Walks every MenuScreen in the scene (a handful), once per menu load.</remarks>
    public static MenuScreen? FindMainMenuHome()
    {
        Il2CppArrayBase<MenuScreen> screens = UObject.FindObjectsOfType<MenuScreen>(true);
        foreach (MenuScreen screen in screens)
        {
            if (screen.OpenOnStart && screen.PreviousScreen == null && screen.name == MainMenuHome)
                return screen;
        }
        return null;
    }
}