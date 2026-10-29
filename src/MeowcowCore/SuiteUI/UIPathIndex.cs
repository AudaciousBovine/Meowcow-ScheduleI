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

    /// <summary>Under a tab strip: its tab list.</summary>
    public const string TabStripLayout = "TabsLayout";

    /// <summary>Under a tab: its label.</summary>
    public const string WindowTabLabel = "Label";

    /// <summary>Under a tab: the highlight shown while selected (rounded top, flat bottom).</summary>
    public const string TabCheckmark = "Checkmark";

    /// <summary>Under a tab: its background (used for unselected subtabs' shade).</summary>
    public const string TabBackground = "Background";

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

    // ---------- Setting rows (inside the Settings screen's pages) ----------
    // [CHANGED] Added for MeowcowWindow's rows. Found with UIBuilder.DumpHierarchy.

    /// <summary>A choice row's label (on/off and slider rows label themselves through UIOption).</summary>
    public const string RowOptionName = "Option Name - Text (TMP)";

    /// <summary>A binding row's (and gamepad slider row's) label.</summary>
    public const string RowLabel = "Label";

    /// <summary>The layout group inside gamepad rows, holding their label and control.</summary>
    public const string RowContainer = "Container";

    /// <summary>Under a slider: its track (what reads as the slider's left edge).</summary>
    public const string SliderTrack = "Background";

    /// <summary>Under an on/off control (at any depth): its circle.</summary>
    public const string ToggleCircle = "BG Circle";

    /// <summary>A Keybinds page's section header ("Bindings"), holding the Restore Default button.</summary>
    public const string BindingHeader = "BindingLabel";

    /// <summary>Under the bindings header: the Restore Default button.</summary>
    public const string RestoreDefaultButton = "RestoreDefault";

    /// <summary>The keyboard page's mouse-button row (two buttons with icons, side by side).</summary>
    public const string MouseInfoRow = "MouseInfo";

    /// <summary>A choice row's dropdown.</summary>
    public const string RowDropdown = "Dropdown";

    /// <summary>A dropdown's hidden item list (has its own gamepad panel; not a page).</summary>
    public const string DropdownTemplate = "Template";

    // ---------- Other pieces ----------

    /// <summary>The save slots' small dark button: the style source for Back buttons.</summary>
    public const string ImportButton = "Import";

    /// <summary>The loose "Esc Back" prompt in the main menu's corner.</summary>
    public const string MenuCornerPrompt = "BottomLeftMenuPivot";

    /// <summary>The pause menu's own "Back" prompt, beside its screens (in a save).</summary>
    public const string InGameCornerPrompt = "InputPrompt (Back)";

    /// <summary>Under the corner prompt: the text beside the key ("Back"), the size prompt text is shown at.</summary>
    public const string PromptLabel = "PromptLabel";

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
    /// <remarks>
    /// [CHANGED] Skips Core's untouched copy of the screen (see <see cref="PristineSettingsName"/>), which is
    /// also a SettingsScreen.
    /// </remarks>
    public static SettingsScreen? FindSettingsScreen()
    {
        foreach (SettingsScreen screen in UObject.FindObjectsOfType<SettingsScreen>(true))
        {
            if (screen.name != PristineSettingsName) return screen;
        }
        return null;
    }

    /// <summary>
    /// Core's hidden, untouched copy of vanilla's Settings screen, taken before Core reorganises the real
    /// one. New windows (Meowcow Settings) are cloned from it, since the real one no longer looks vanilla.
    /// </summary>
    /// <remarks>[CHANGED] New for the vanilla Settings rework.</remarks>
    public const string PristineSettingsName = "MeowcowPristineSettings";

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