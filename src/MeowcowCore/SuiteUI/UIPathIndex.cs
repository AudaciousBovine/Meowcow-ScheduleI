using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
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

    /// <summary>Under a framed screen: the tab list.</summary>
    public const string WindowTabsLayout = "TabsPanel/TabsLayout";

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