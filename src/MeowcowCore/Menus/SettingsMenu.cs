using System;
using System.Collections.Generic;
using MeowcowCore.Foundation;
using MeowcowCore.SuiteSettingsManager;
using MeowcowCore.SuiteUI;
using UnityEngine;
using UnityEngine.UI;
using MenuScreen = Il2CppScheduleOne.UI.MainMenu.MenuScreen;
using SettingsScreen = Il2CppScheduleOne.UI.MainMenu.SettingsScreen;
using UObject = UnityEngine.Object;

namespace MeowcowCore.Menus;

/// <summary>
/// The improved Settings menu (design §9, "Rebuilt with adapters"): a Core window with Core's own rows, in
/// the suite's layout. Vanilla's own Settings screen is left untouched, and a remembered toggle switches
/// between the two.
/// </summary>
/// <remarks>
/// [CHANGED] Rewritten for the adapter rebuild (R1). Was: vanilla's real screen reorganised in place, with
/// vanilla's rows moved into it, which kept inheriting vanilla state (shared screen, hidden-page navigation).
/// R1 builds the window, routes the main menu's Settings button and adds the toggle; vanilla's settings
/// get adapters in R2, Graphics and Display in R3, the pause menu in R4.
/// [PERFORMANCE] At menu load: one button rewire and one small button added to vanilla's screen. The window
/// is built on first open per scene (one clone, like Meowcow Settings). Nothing per frame.
/// </remarks>
internal static class SettingsMenu
{
    private static readonly MeowcowLogger Log = CoreMod.Log.Tagged("Settings");

    private const string WindowName = "MeowcowImprovedSettings";
    private const string Title = "Settings";
    private const string ShowVanillaLabel = "Show Vanilla Settings Menu";
    private const string ShowImprovedLabel = "Show Improved Settings Menu";
    private const string ShowImprovedName = MeowcowWindow.CoreAdditionPrefix + "ShowImprovedSettings";
    private const float OutsideGap = 10f;      // between the frame's bottom edge and the buttons below it
    private const float SmallButtonHeight = 40f;
    private const float BackWidth = 140f;      // as the improved window's Back button
    private const string BackName = "Back";

    // [TEMPORARY] R1 only: shown on pages whose vanilla settings arrive in R2/R3.
    private const string PendingText = "Vanilla settings arrive in the next update";

    /// <summary>All of a vanilla page's rows not placed elsewhere, in their order.</summary>
    private const string RestOfPage = "*";

    /// <summary>The vanilla page itself (Current Game, in a save; R4).</summary>
    private const string WholePage = "#page";

    /// <summary>As a page name: a Core setting (by key) rather than a vanilla row.</summary>
    private const string CoreSetting = "@core";

    /// <summary>Key of the remembered choice: true opens vanilla's own Settings screen.</summary>
    internal const string UseVanillaMenuKey = "UseVanillaSettingsMenu";

    private static ToggleSetting? _useVanillaMenu;
    private static MeowcowWindow? _window;
    private static SettingsScreen? _vanilla;
    private static Transform? _vanillaExtras;       // the holder of Core's buttons on vanilla's screen
    private static IntPtr _hookedScreen;            // the vanilla screen whose open/close is followed
    private static Il2CppSystem.Action? _onVanillaOpened; // kept referenced (IL2CPP rule)
    private static Il2CppSystem.Action? _onVanillaClosed;
    private static GameObject? _cornerPrompt;
    private static bool _cornerPromptWasActive;

    /// <summary>
    /// Where each setting goes: vanilla page and row name (or a Core setting), our tab, our subtab (null for a
    /// tab with one page), and an optional new label. Tabs and subtabs appear in the order they first appear
    /// here. [CHANGED] R1: only Core settings get rows; vanilla entries get adapters in R2 (their page and row
    /// names stay: R2's detection checks vanilla's rows against them).
    /// </summary>
    /// <remarks>
    /// The one place that knows vanilla's row names (instead of UIPathIndex), since the names only mean
    /// something next to where each row goes. A renamed row after a game update is a one-line fix here.
    /// Names come from the hierarchy dump of the Settings screen.
    /// </remarks>
    private static readonly Placement[] Layout =
    {
        // General. [CHANGED] Core's switches for vanilla behaviour come first (see SuiteSettings/VanillaSkips).
        new(CoreSetting, SuiteSettings.VanillaSkips.SkipDisclaimerKey, "General", "Main"),
        new(CoreSetting, SuiteSettings.VanillaSkips.AlwaysSkipTutorialKey, "General", "Main"),
        new(CoreSetting, SuiteSettings.VanillaSkips.AlwaysSkipIntroKey, "General", "Main"),
        new("Display", "PauseOnLoseFocus", "General", "Main", Description: "Pauses the game when its window loses focus, such as when you switch to another program."),
        new("Other", "AutoBackupSaves", "General", "Main", Description: "When loading an existing save on a new version of the game, a backup is made first."),
        // [CHANGED] The current save's settings (in a save only): vanilla's Game page, moved whole, since a
        // script on the page runs its rows and shows its host-only Blocker.
        new("Game", WholePage, "General", "Current Game", InSaveOnly: true),
        new("Other", "Export Player Log", "General", "Misc"),
        new("Other", "Export Player Log (1)", "General", "Misc"),

        // Gameplay
        new("Display", "InterfaceScaleSlider", "Gameplay", "Interface", Description: "The size of the game's interface."),
        new("Display", "Units", "Gameplay", "Interface", Description: "Metric or imperial units, for weights and distances."),
        new("Gamepad", "PointerSensitivity", "Gameplay", "Interface", Description: "How fast the on-screen pointer moves with a gamepad."),
        new("Display", "CameraBobbingSlider", "Gameplay", "Camera", Description: "How much the camera bobs while you move."),
        new("Graphics", "FOV", "Gameplay", "Camera", Description: "Field of view: how wide the camera sees."),
        new("Controls", "SettingsSlider", "Gameplay", "Camera", "Mouse Camera Sensitivity", Description: "How fast the camera turns with the mouse."),
        new("Controls", "InvertYToggle", "Gameplay", "Camera", "Mouse Invert Y", Description: "Inverts looking up and down with the mouse."),
        new("Gamepad", "CameraSensitivity", "Gameplay", "Camera", "Gamepad Camera Sensitivity", Description: "How fast the camera turns with a gamepad."),
        new(CoreSetting, SuiteSettings.InputSplit.GamepadInvertYKey, "Gameplay", "Camera"),

        // Graphics
        new("Display", "Resolution", "Graphics", null),
        new("Display", "Display", "Graphics", null),
        new("Display", "Display Mode", "Graphics", null),
        new("Display", "VSync", "Graphics", null, Description: "Matches frames to your monitor's refresh rate to prevent tearing."),
        new("Display", "TargetFramerateSlider", "Graphics", null, Description: "The frame rate limit, when V-Sync is off."),
        new("Graphics", "QualityPopupSelector", "Graphics", null),
        new("Graphics", "AntiAliasingPopupSelector", "Graphics", null, Description: "Smooths jagged edges."),
        new("Graphics", "SSAOToggle", "Graphics", null, Description: "Soft shadows where surfaces meet. Costs some performance."),
        new("Graphics", "GodRaysToggle", "Graphics", null, Description: "Visible shafts of sunlight. Costs some performance."),
        new("Display", "Apply", "Graphics", null), // appears after a display change; applies it

        // Audio
        new("Audio", RestOfPage, "Audio", null),

        // Keybinds (vanilla's binding sections, until the keybind rework). [CHANGED] The "Bindings" header
        // opens the bindings, after Sprint Mode; its Restore Default button moves to the top of the page
        // (see TouchUpKeybinds).
        new("Controls", "SprintModePopupSelector", "Keybinds", "Mouse & Keyboard", Description: "How the sprint key works: press to toggle, or hold."),
        new(CoreSetting, SuiteSettings.InputSplit.KeyboardSneakModeKey, "Keybinds", "Mouse & Keyboard"),
        new("Controls", "BindingLabel", "Keybinds", "Mouse & Keyboard"),
        new("Controls", RestOfPage, "Keybinds", "Mouse & Keyboard"),
        new(CoreSetting, SuiteSettings.InputSplit.GamepadSprintModeKey, "Keybinds", "Gamepad"),
        new(CoreSetting, SuiteSettings.InputSplit.GamepadSneakModeKey, "Keybinds", "Gamepad"),
        new("Gamepad", "BindingLabel", "Keybinds", "Gamepad"),
        new("Gamepad", RestOfPage, "Keybinds", "Gamepad"),
    };

    /// <summary>
    /// [CHANGED] Descriptions for the rows of whole pages (Current Game), by row name. Seeded Mixing's is
    /// vanilla's own tooltip text.
    /// </summary>
    private static readonly Dictionary<string, string> WholePageDescriptions = new(StringComparer.Ordinal)
    {
        ["Seeded Mixing"] = "If enabled, the product mixing algorithm will be randomized using this save file's seed. " +
                            "This will not affect your existing mixes.",
        ["Console"] = "Enables the developer console in this save.",
    };

    /// <param name="InSaveOnly">The vanilla page only exists in a save; its absence elsewhere is expected.</param>
    /// <param name="Description">[CHANGED] Shown in the page's description panel (with an info icon).</param>
    private sealed record Placement(string Page, string Row, string Tab, string? Subtab, string? Label = null,
        bool InSaveOnly = false, string? Description = null);


    // ---------- Setup ----------

    /// <summary>
    /// Declares the remembered choice on Core. Not shown in either menu: the toggle buttons are its control.
    /// </summary>
    /// <remarks>
    /// [CHANGED] New (R1). Marked as a vanilla-Settings setting so Meowcow Settings skips it, and not in the
    /// layout table, so the improved menu doesn't show it as a row either.
    /// </remarks>
    internal static void Declare(ModuleSettings core)
    {
        _useVanillaMenu = core.Subtab("Vanilla").Toggle(UseVanillaMenuKey, ShowVanillaLabel, false,
            "Opens the game's own Settings menu instead of the improved one.").ShownInVanillaSettings();
    }

    /// <summary>
    /// Called once per main menu load: the main menu's Settings button opens the chosen menu, and vanilla's
    /// screen gets its way back to the improved one. The window itself is built on first open.
    /// </summary>
    /// <param name="settingsButton">The main menu's Settings button.</param>
    internal static void Setup(Button? settingsButton)
    {
        _window = null; // the previous scene's window went with it
        _vanilla = UIPathIndex.FindSettingsScreen();
        if (_vanilla == null)
        {
            Log.Warning("Settings screen not found; Settings left vanilla");
            return;
        }

        AddVanillaExtras(_vanilla);
        FollowVanillaOpenClose(_vanilla);

        // [CHANGED] Replaces vanilla's wiring (Settings → SettingsScreen.Open) with the chooser.
        if (settingsButton != null) UIBuilder.SetClick(settingsButton, Open);
        else Log.Warning("Main menu Settings button not given; it opens vanilla's Settings");
    }

    /// <summary>
    /// [TEMPORARY] In a save, vanilla's Settings stays as it is until R4 (the pause menu's window). Called by
    /// CoreLifecycle at session start.
    /// </summary>
    internal static void SetupInSave() { }

    // ---------- Opening ----------

    /// <summary>
    /// Opens the chosen menu. If the improved window can't be built, vanilla's opens instead (design §14:
    /// a menu Core can't build falls back to vanilla).
    /// </summary>
    internal static void Open()
    {
        if (_useVanillaMenu?.Value == true)
        {
            OpenVanilla();
            return;
        }

        if (_window == null || !_window.IsAlive)
        {
            _window = Build();
            if (_window != null) _window.Closed += MeowcowSettings.FlushAll; // Core rows save pending changes
        }

        if (_window != null) _window.Open();
        else OpenVanilla();
    }

    private static void OpenVanilla()
    {
        if (_vanilla != null) _vanilla.Open();
        else Log.Warning("Vanilla Settings screen is gone; nothing to open");
    }

    /// <summary>
    /// Switches menus and remembers the choice. The open one closes first, so Escape from the other still
    /// returns to where Settings was opened from.
    /// </summary>
    private static void SwitchTo(bool vanilla)
    {
        if (_useVanillaMenu != null) _useVanillaMenu.Value = vanilla;
        MenuScreen? current = MenuScreen.Current;
        if (current != null) current.Close();
        Open();
    }

    // Static handlers (not lambdas): UIBuilder.SetClick keeps one converted handler per method.
    private static void ShowVanilla() => SwitchTo(vanilla: true);
    private static void ShowImproved() => SwitchTo(vanilla: false);

    // ---------- The window ----------

    /// <summary>The improved window: its tabs and pages from the layout table, Core's rows, the toggle.</summary>
    private static MeowcowWindow? Build()
    {
        MeowcowWindow? window = MeowcowWindow.CreateFromSettings(WindowName, Title);
        if (window == null) return null;

        var tabs = new Dictionary<string, WindowTab>(StringComparer.Ordinal);
        var pages = new Dictionary<string, WindowPage>(StringComparer.Ordinal);
        var pending = new HashSet<WindowPage>();

        foreach (Placement placement in Layout)
        {
            try
            {
                if (placement.InSaveOnly) continue; // R4
                WindowPage page = PageFor(window, tabs, pages, placement);
                if (placement.Page == CoreSetting) AddCoreSetting(page, placement);
                else pending.Add(page); // [TEMPORARY] R1: vanilla settings get adapters in R2/R3
            }
            catch (Exception e)
            {
                Log.Error($"Couldn't place '{placement.Row}'", e);
            }
        }

        foreach (WindowPage page in pending) page.AddText(PendingText); // [TEMPORARY]

        window.AddFooterButton("ShowVanilla", ShowVanillaLabel, ShowVanilla);
        return window;
    }

    private static void AddCoreSetting(WindowPage page, Placement placement)
    {
        Setting? setting = MeowcowSettings.Core?.Find(placement.Row);
        if (setting == null)
        {
            Log.Warning($"Core setting '{placement.Row}' not found; not shown");
            return;
        }
        SettingRows.Add(page, setting);
    }

    private static WindowPage PageFor(MeowcowWindow window, Dictionary<string, WindowTab> tabs,
        Dictionary<string, WindowPage> pages, Placement placement)
    {
        WindowTab tab = TabFor(window, tabs, placement);
        string subtab = placement.Subtab ?? placement.Tab;
        string key = placement.Tab + "/" + subtab;
        if (!pages.TryGetValue(key, out WindowPage? page))
        {
            page = tab.AddPage(subtab);
            pages.Add(key, page);
        }
        return page;
    }

    private static WindowTab TabFor(MeowcowWindow window, Dictionary<string, WindowTab> tabs, Placement placement)
    {
        if (!tabs.TryGetValue(placement.Tab, out WindowTab? tab))
        {
            tab = window.AddTab(placement.Tab);
            tabs.Add(placement.Tab, tab);
        }
        return tab;
    }

    // ---------- Vanilla's screen ----------

    /// <summary>
    /// Core's additions to vanilla's Settings screen, just outside its frame: Back below the bottom-left
    /// corner, "Show Improved Settings Menu" below the bottom-right one. The only changes Core makes to
    /// vanilla's screen. Named with the Core-addition prefix, so windows cloned from the screen leave them out.
    /// </summary>
    /// <remarks>
    /// [CHANGED] Back added, and both buttons moved outside the frame (were: Show Improved inside the
    /// frame's corner). A holder that's the screen's last child, with the frame's exact rect, holds them: drawn
    /// on top (the frame is the screen's first child, so its own children were covered and lost clicks) and
    /// placed relative to the frame wherever it sits.
    /// [PERFORMANCE] Two clones and one empty object, once per menu load.
    /// </remarks>
    private static void AddVanillaExtras(SettingsScreen screen)
    {
        try
        {
            Transform root = screen.transform;
            Transform? existing = root.Find(ShowImprovedName);
            if (existing != null)
            {
                _vanillaExtras = existing; // already added this scene
                return;
            }

            // The holder takes the frame's rect (same parent, so same anchors and offsets mean the same place).
            var holder = new GameObject(ShowImprovedName);
            RectTransform holderRect = holder.AddComponent<RectTransform>();
            holderRect.SetParent(root, false);
            if (root.Find(UIPathIndex.WindowBackground) is RectTransform frame)
            {
                holderRect.anchorMin = frame.anchorMin;
                holderRect.anchorMax = frame.anchorMax;
                holderRect.pivot = frame.pivot;
                holderRect.anchoredPosition = frame.anchoredPosition;
                holderRect.sizeDelta = frame.sizeDelta;
            }
            else
            {
                MeowcowWindow.SetRect(holderRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }
            holder.transform.SetAsLastSibling(); // drawn over everything else on the screen
            _vanillaExtras = holderRect;

            // Show Improved Settings Menu: below the frame, right edges lined up.
            GameObject? show = UITemplateRegistry.Create(UITemplateRegistry.SmallButton, holderRect, "ShowImproved");
            if (show != null)
            {
                float width = UITemplateRegistry.FitToLabel(show, ShowImprovedLabel);
                PlaceBelowFrame(show.GetComponent<RectTransform>(), right: true, width);
                UIBuilder.SetClick(show.GetComponent<Button>(), ShowImproved);
            }
            else
            {
                Log.Warning("No small button in the registry; vanilla Settings has no way back to the improved menu");
            }

            // Back: below the frame, left edges lined up. The registry's Back (live key prompt).
            GameObject? back = UITemplateRegistry.Create(UITemplateRegistry.BackButton, holderRect, BackName);
            if (back != null)
            {
                PlaceBelowFrame(back.GetComponent<RectTransform>(), right: false, BackWidth);
                UIBuilder.SetClick(back.GetComponent<Button>(), CloseCurrent);
            }
        }
        catch (Exception e)
        {
            Log.Error("Couldn't add Core's buttons to vanilla Settings", e);
        }
    }

    /// <summary>Just below the holder's (the frame's) bottom edge, lined up with its left or right edge.</summary>
    private static void PlaceBelowFrame(RectTransform rect, bool right, float width)
    {
        float x = right ? 1f : 0f;
        rect.anchorMin = new Vector2(x, 0f);
        rect.anchorMax = new Vector2(x, 0f);
        rect.pivot = new Vector2(x, 1f);                // hangs down from the frame's edge
        rect.sizeDelta = new Vector2(width, SmallButtonHeight);
        rect.anchoredPosition = new Vector2(0f, -OutsideGap);
    }

    /// <summary>Back: closes whichever screen is open (it can only be clicked on the open one).</summary>
    private static void CloseCurrent()
    {
        MenuScreen? current = MenuScreen.Current;
        if (current != null) current.Close();
    }

    /// <summary>
    /// Follows vanilla's screen opening and closing, as the improved window does for itself: vanilla's corner
    /// "Back" prompt is hidden while open (Core's Back replaces it), and Back's key cap follows the device.
    /// </summary>
    /// <remarks>
    /// [CHANGED] New. Hooked once per scene. Mirrors MeowcowWindow's own open/close handling; if more screens
    /// need it, it moves into a shared helper.
    /// [SOURCE] MeowcowWindow.HookOpenClose (the state's OnBecomeTopSibling / OnNoLongerTopSibling).
    /// [PERFORMANCE] Two events, no per-frame work.
    /// </remarks>
    private static void FollowVanillaOpenClose(SettingsScreen screen)
    {
        if (_hookedScreen == screen.Pointer || screen.State == null) return;
        _onVanillaOpened ??= UIBuilder.ToIl2Cpp<Il2CppSystem.Action>(new Action(OnVanillaOpened));
        _onVanillaClosed ??= UIBuilder.ToIl2Cpp<Il2CppSystem.Action>(new Action(OnVanillaClosed));
        screen.State.add_OnBecomeTopSibling(_onVanillaOpened);
        screen.State.add_OnNoLongerTopSibling(_onVanillaClosed);
        _hookedScreen = screen.Pointer;
    }

    // Called from native code: exceptions must never escape.
    private static void OnVanillaOpened() => UIBuilder.RunSafely(() =>
    {
        _cornerPrompt = GameObject.Find(UIPathIndex.MenuCornerPrompt) ?? GameObject.Find(UIPathIndex.InGameCornerPrompt);
        _cornerPromptWasActive = _cornerPrompt != null && _cornerPrompt.activeSelf;
        if (_cornerPromptWasActive) _cornerPrompt!.SetActive(false);
        InputPrompts.Shown(_vanillaExtras);
    }, "vanilla Settings open");

    private static void OnVanillaClosed() => UIBuilder.RunSafely(() =>
    {
        InputPrompts.Hidden(_vanillaExtras);
        // Only shown again if it was showing before, so a prompt vanilla hid stays hidden.
        if (_cornerPromptWasActive && _cornerPrompt != null) _cornerPrompt.SetActive(true);
        _cornerPromptWasActive = false;
    }, "vanilla Settings close");
}