using System;
using System.Collections.Generic;
using Il2CppTMPro;
using MeowcowCore.Foundation;
using MeowcowCore.SuiteSettings;
using MeowcowCore.SuiteSettingsManager;
using MeowcowCore.SuiteUI;
using UnityEngine;
using UnityEngine.UI;
using MenuScreen = Il2CppScheduleOne.UI.MainMenu.MenuScreen;
using SettingsScreen = Il2CppScheduleOne.UI.MainMenu.SettingsScreen;
using Il2CppScheduleOne.DevUtilities;
using UObject = UnityEngine.Object;
using VanillaSettings = Il2CppScheduleOne.DevUtilities.Settings;

namespace MeowcowCore.Menus;

/// <summary>
/// The improved Settings menu (design §9, "Rebuilt with adapters"): a Core window with Core's own rows, in
/// the suite's layout. Vanilla's own Settings screen is left untouched, and a remembered toggle switches
/// between the two.
/// </summary>
/// <remarks>
/// [CHANGED] Rewritten for the adapter rebuild (R1). Was: vanilla's real screen reorganised in place, with
/// vanilla's rows moved into it, which kept inheriting vanilla state (shared screen, hidden-page navigation).
/// R1 built the window, routed the main menu's Settings button and added the toggle. R2: Other, Audio,
/// Gamepad and Input are bound to vanilla through adapters (SuiteSettings/VanillaSettingAdapters), and vanilla
/// settings without one are reported (log error, yellow footer line). [CHANGED] R3: Display and Graphics, with
/// Core's confirm dialog (SuiteUI/ConfirmDialog) for the display changes vanilla confirms. The pause menu in R4.
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

    // [TEMPORARY] Until R3/R4: shown on pages with vanilla settings still waiting for adapters.
    private const string PendingText = "Vanilla settings arrive in the next update";

    /// <summary>As a page name: a Core setting (by key) rather than a vanilla row.</summary>
    private const string CoreSetting = "@core";

    /// <summary>[CHANGED] As a page name: a vanilla setting, by its adapter's id.</summary>
    private const string Adapter = "@vanilla";

    /// <summary>[CHANGED] As a page name: a line of text (the row field holds it).</summary>
    private const string Note = "@note";

    private const string BindingsNote = "Key bindings: use Show Vanilla Settings Menu until the keybind rework";
    private const string ExportedMessage = "Exported";
    private const string ConfirmTitle = "Keep these display settings?";
    private const string ConfirmCountdown = "Reverting in {0:0.0} seconds"; // vanilla's wording
    private const float MessageSeconds = 2f;

    // [CHANGED] Yellow: nothing is broken, a setting is only reachable in vanilla's menu.
    private static readonly Color WarningColor = new(1f, 0.85f, 0.2f, 1f);

    /// <summary>
    /// [CHANGED] Re-reads each vanilla-bound row's value. Run each time the window opens: the value may have
    /// changed in vanilla's menu since. [PERFORMANCE] One read per row per open.
    /// </summary>
    private static readonly List<Action> Refreshers = new();

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
    /// here. [CHANGED] R2: a vanilla setting is placed by its adapter's id (SuiteSettings/VanillaSettingAdapters,
    /// which also holds its label and description); entries still naming a vanilla page and row are waiting for
    /// adapters (R3, R4) and show the temporary line.
    /// </summary>
    /// <remarks>
    /// The one place that knows vanilla's row names (instead of UIPathIndex), since the names only mean
    /// something next to where each row goes. A renamed row after a game update is a one-line fix here.
    /// Names come from the hierarchy dump of the Settings screen.
    /// </remarks>
    private static readonly Placement[] Layout =
    {
        // General. Core's switches for vanilla behaviour come first (see SuiteSettings/VanillaSkips).
        new(CoreSetting, SuiteSettings.VanillaSkips.SkipDisclaimerKey, "General", "Main"),
        new(CoreSetting, SuiteSettings.VanillaSkips.AlwaysSkipTutorialKey, "General", "Main"),
        new(CoreSetting, SuiteSettings.VanillaSkips.AlwaysSkipIntroKey, "General", "Main"),
        new(Adapter, "display.pauseOnFocusLost", "General", "Main"),
        new(Adapter, "other.autoBackup", "General", "Main"),
        new(Adapter, "log.export", "General", "Misc"),
        new(Adapter, "log.exportPrevious", "General", "Misc"),

        // Gameplay
        new(Adapter, "display.interfaceScale", "Gameplay", "Interface"),
        new(Adapter, "display.units", "Gameplay", "Interface"),
        new(Adapter, "gamepad.pointer", "Gameplay", "Interface"),
        new(Adapter, "display.cameraBob", "Gameplay", "Camera"),
        new(Adapter, "graphics.fov", "Gameplay", "Camera"),
        new(Adapter, "input.mouseSensitivity", "Gameplay", "Camera"),
        new(Adapter, "input.invertY", "Gameplay", "Camera"),
        new(Adapter, "gamepad.cameraSensitivity", "Gameplay", "Camera"),
        new(CoreSetting, SuiteSettings.InputSplit.GamepadInvertYKey, "Gameplay", "Camera"),

        // Graphics. [CHANGED] R3: no Apply row; display changes apply at once (some then ask to keep or revert).
        new(Adapter, "display.resolution", "Graphics", null),
        new(Adapter, "display.monitor", "Graphics", null),
        new(Adapter, "display.mode", "Graphics", null),
        new(Adapter, "display.vsync", "Graphics", null),
        new(Adapter, "display.targetFps", "Graphics", null),
        new(Adapter, "graphics.quality", "Graphics", null),
        new(Adapter, "graphics.antiAliasing", "Graphics", null),
        new(Adapter, "graphics.ssao", "Graphics", null),
        new(Adapter, "graphics.godRays", "Graphics", null),

        // Audio
        new(Adapter, "audio.master", "Audio", null),
        new(Adapter, "audio.music", "Audio", null),
        new(Adapter, "audio.sfx", "Audio", null),
        new(Adapter, "audio.ambient", "Audio", null),
        new(Adapter, "audio.weather", "Audio", null),
        new(Adapter, "audio.footsteps", "Audio", null),
        new(Adapter, "audio.dialogue", "Audio", null),
        new(Adapter, "audio.ui", "Audio", null),

        // Keybinds. Bindings come with the keybind rework (slice 4); until then, a pointer to vanilla's menu.
        new(Adapter, "input.sprintMode", "Keybinds", "Mouse & Keyboard"),
        new(CoreSetting, SuiteSettings.InputSplit.KeyboardSneakModeKey, "Keybinds", "Mouse & Keyboard"),
        new(Note, BindingsNote, "Keybinds", "Mouse & Keyboard"),
        new(CoreSetting, SuiteSettings.InputSplit.GamepadSprintModeKey, "Keybinds", "Gamepad"),
        new(CoreSetting, SuiteSettings.InputSplit.GamepadSneakModeKey, "Keybinds", "Gamepad"),
        new(Note, BindingsNote, "Keybinds", "Gamepad"),
    };

    /// <remarks>
    /// [CHANGED] Just where a setting goes: labels and descriptions live in the adapters (and Core settings).
    /// The Current Game page and its InSaveOnly flag are gone (R4): Console and Seeded Mixing become
    /// playthrough settings in Core's save system; until then they're in vanilla's menu.
    /// </remarks>
    private sealed record Placement(string Page, string Row, string Tab, string? Subtab);


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
        if (!PrepareScene()) return;

        // [CHANGED] Replaces vanilla's wiring (Settings → SettingsScreen.Open) with the chooser.
        if (settingsButton != null) UIBuilder.SetClick(settingsButton, Open);
        else Log.Warning("Main menu Settings button not given; it opens vanilla's Settings");
    }

    /// <summary>
    /// [CHANGED] R4: in a save, the pause menu's Settings works the same way: its button opens the chosen
    /// menu, vanilla's screen gets Core's two buttons, and the improved window is built on first open (the
    /// pause menu's canvas is switched off until the game is paused, and a switched-off canvas measures
    /// nothing). Called by CoreLifecycle at session start.
    /// </summary>
    internal static void SetupInSave() => MelonLoader.MelonCoroutines.Start(SetupInSaveNextFrame());

    private static System.Collections.IEnumerator SetupInSaveNextFrame()
    {
        yield return null; // the pause menu sets itself up first
        try
        {
            if (!PrepareScene()) yield break;
            RoutePauseMenuButton(_vanilla!);
        }
        catch (Exception e)
        {
            Log.Error("Couldn't set up the in-game Settings menus", e);
        }
    }

    /// <summary>The scene's vanilla Settings screen, with Core's buttons and open/close handling.</summary>
    /// <remarks>[CHANGED] Shared by the main menu and the pause menu (R4).</remarks>
    private static bool PrepareScene()
    {
        _window = null; // the previous scene's window went with it
        // [CHANGED] Always on, as a safety net: if vanilla's screen ever opens other than through Core (a button
        // that wasn't rerouted), it hands over to the improved menu when that's the chosen one.
        _redirectOpens = true;
        _vanilla = UIPathIndex.FindSettingsScreen();
        if (_vanilla == null)
        {
            Log.Warning("Settings screen not found; Settings left vanilla");
            return false;
        }

        AddVanillaExtras(_vanilla);
        FollowVanillaOpenClose(_vanilla);
        return true;
    }

    // ---------- The pause menu's button (R4) ----------

    private static UnityEngine.Events.UnityAction? _openAction; // kept referenced (IL2CPP rule)

    /// <summary>
    /// Points the button that opens vanilla's Settings at the chooser. The button is found by what it opens
    /// (its saved click listener targets the Settings screen), not by its path, so a moved or renamed button
    /// is still found. Only that one listener is switched off; anything else the button does still runs.
    /// If no such button is found, vanilla's screen is redirected to the improved one as it opens instead.
    /// </summary>
    /// <remarks>
    /// [CHANGED] New (R4). [PERFORMANCE] One pass over the scene's buttons, once per session.
    /// </remarks>
    private static void RoutePauseMenuButton(SettingsScreen screen)
    {
        IntPtr screenPtr = screen.Pointer;
        IntPtr objectPtr = screen.gameObject.Pointer;
        IntPtr statePtr = screen.State != null ? screen.State.Pointer : IntPtr.Zero;

        foreach (Button button in UObject.FindObjectsOfType<Button>(true))
        {
            // [CHANGED] Not the screen's own buttons (its Back also targets the screen, to close it), and only a
            // listener that opens it: the first version rerouted the wrong button, so the pause menu's Settings
            // kept opening vanilla's screen.
            if (button.transform.IsChildOf(screen.transform)) continue;
            Button.ButtonClickedEvent click = button.onClick;
            if (click == null) continue;
            for (int i = 0; i < click.GetPersistentEventCount(); i++)
            {
                UObject? target = click.GetPersistentTarget(i);
                if (target == null) continue;
                IntPtr ptr = target.Pointer;
                if (ptr != screenPtr && ptr != objectPtr && (statePtr == IntPtr.Zero || ptr != statePtr)) continue;
                string method = click.GetPersistentMethodName(i) ?? "";
                if (!method.StartsWith("Open", StringComparison.Ordinal) && method != "PushToDefaultParent") continue;

                click.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
                _openAction ??= UIBuilder.ToIl2Cpp<UnityEngine.Events.UnityAction>(new Action(() =>
                    UIBuilder.RunSafely(Open, "opening Settings")));
                click.AddListener(_openAction);
                Log.Debug($"Pause menu Settings button '{button.name}' ({method}) now opens the chosen menu");
                return;
            }
        }

        // Not found: vanilla opens its screen some other way (from code); the hand-over covers it.
        Log.Debug("No button opens the in-game Settings directly; vanilla's screen will hand over as it opens");
    }

    /// <summary>
    /// When true, vanilla's screen opening (with the improved menu chosen) closes again and opens the improved
    /// window instead. [CHANGED] Always on (a safety net behind the rerouted buttons), set per scene.
    /// </summary>
    private static bool _redirectOpens;

    /// <summary>Set while Core itself opens vanilla's screen (chosen, or as a fallback), so it isn't redirected.</summary>
    private static bool _openingVanillaOnPurpose;

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

        if (_window != null)
        {
            RefreshAll();
            _window.Open();
        }
        else OpenVanilla();
    }

    private static void OpenVanilla()
    {
        if (_vanilla == null)
        {
            Log.Warning("Vanilla Settings screen is gone; nothing to open");
            return;
        }
        _openingVanillaOnPurpose = true; // [CHANGED] R4: not redirected
        _vanilla.Open();
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

    /// <summary>Every vanilla-bound row re-reads its value (and dynamic choices their options).</summary>
    private static void RefreshAll()
    {
        foreach (Action refresh in Refreshers) UIBuilder.RunSafely(refresh, "refreshing a Settings row");
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

        Refreshers.Clear(); // the previous window's rows are gone

        // [CHANGED] R3: display changes vanilla confirms are confirmed in this window's own dialog; a revert
        // puts every row back.
        MeowcowWindow dialogWindow = window;
        VanillaSettingAdapters.ConfirmDisplay = (keep, revert) => ConfirmDialog.Show(dialogWindow, ConfirmTitle,
            ConfirmCountdown, Il2CppScheduleOne.UI.Settings.ConfirmDisplaySettings.RevertTime, keep, () =>
            {
                revert();
                RefreshAll();
            });
        VanillaSettings? settings = Singleton<VanillaSettings>.Instance;
        // [CHANGED] R2: vanilla's untouched screen tells us its row scripts (for slider ranges) and anything
        // without an adapter.
        VanillaSettingAdapters.ScanResult? scan = _vanilla != null ? VanillaSettingAdapters.Scan(_vanilla.transform) : null;

        var tabs = new Dictionary<string, WindowTab>(StringComparer.Ordinal);
        var pages = new Dictionary<string, WindowPage>(StringComparer.Ordinal);
        var pending = new HashSet<WindowPage>();

        foreach (Placement placement in Layout)
        {
            try
            {
                WindowPage page = PageFor(window, tabs, pages, placement);
                if (placement.Page == CoreSetting) AddCoreSetting(page, placement);
                else if (placement.Page == Adapter) AddAdapterRow(page, placement.Row, settings, scan);
                else if (placement.Page == Note) page.AddText(placement.Row);
                else pending.Add(page); // [TEMPORARY] vanilla settings whose adapters come in R3/R4
            }
            catch (Exception e)
            {
                Log.Error($"Couldn't place '{placement.Row}'", e);
            }
        }

        foreach (WindowPage page in pending) page.AddText(PendingText); // [TEMPORARY]

        Button? showVanilla = window.AddFooterButton("ShowVanilla", ShowVanillaLabel, ShowVanilla);
        // [CHANGED] The gamepad's Options button does the same, with its prompt beside the footer button.
        window.OptionsAction = ShowVanilla;
        window.OptionsAnchor = showVanilla != null ? showVanilla.GetComponent<RectTransform>() : null;

        int unmapped = scan?.Unmapped.Count ?? 0;
        if (unmapped > 0)
        {
            window.SetFooterNotice(unmapped == 1
                ? "1 vanilla setting isn't shown here — use Show Vanilla Settings Menu"
                : $"{unmapped} vanilla settings aren't shown here — use Show Vanilla Settings Menu", WarningColor);
        }
        return window;
    }

    /// <summary>
    /// A row bound to a vanilla setting through its adapter. Sliders take their range from vanilla's own
    /// row (so a range changed by an update is followed); a slider whose vanilla row isn't found is left out.
    /// </summary>
    /// <remarks>
    /// [CHANGED] New (R2). Changes go through VanillaSettingAdapters.Apply (settling ones wait for the value
    /// to stop changing). [PERFORMANCE] One row per adapter, once per window build.
    /// </remarks>
    private static void AddAdapterRow(WindowPage page, string id, VanillaSettings? settings,
        VanillaSettingAdapters.ScanResult? scan)
    {
        VanillaAdapter? adapter = VanillaSettingAdapters.Find(id);
        if (adapter == null)
        {
            Log.Warning($"No adapter '{id}'; not shown");
            return;
        }
        if (settings == null)
        {
            Log.Warning($"Vanilla settings not found; '{adapter.Label}' not shown");
            return;
        }
        if (adapter.Visible != null && !adapter.Visible()) return; // [CHANGED] R3: e.g. Monitor with one display

        // [CHANGED] R3: some changes affect other rows (or are reverted), so every row re-reads afterwards.
        void Run(Action apply) => VanillaSettingAdapters.Apply(adapter, () =>
        {
            apply();
            if (adapter.RefreshAllAfter) RefreshAll();
        });

        switch (adapter.Kind)
        {
            case AdapterKind.Toggle:
            {
                ToggleRow? row = page.AddToggle(adapter.Label, adapter.GetBool!(settings),
                    v => Run(() => adapter.SetBool!(settings, v)), adapter.Description);
                if (row != null) Refreshers.Add(() => row.SetValue(adapter.GetBool!(settings)));
                break;
            }
            case AdapterKind.Slider:
            {
                MonoBehaviour? script = null;
                scan?.Scripts.TryGetValue(adapter.Script, out script);
                // [CHANGED] An adapter's fixed range wins (vanilla sets some ranges only when the row is enabled).
                SliderRange? found = adapter.Range ?? (script != null ? VanillaSettingAdapters.RangeOf(script) : null);
                if (found == null)
                {
                    Log.Warning($"Vanilla's '{adapter.Script}' slider not found; '{adapter.Label}' not shown");
                    return;
                }
                SliderRange range = found.Value;
                SliderRow? row = page.AddSlider(adapter.Label, range.Min, range.Max, 1f, range.WholeNumbers,
                    adapter.GetFloat!(settings, range),
                    v => Run(() => adapter.SetFloat!(settings, range, v)), adapter.Description, adapter.Format);
                if (row != null) Refreshers.Add(() => row.SetValue(adapter.GetFloat!(settings, range)));
                break;
            }
            case AdapterKind.Choice:
            {
                ChoiceRow? row = page.AddChoice(adapter.Label, adapter.Choices!(), adapter.GetIndex!(settings),
                    i => Run(() => adapter.SetIndex!(settings, i)), adapter.Description);
                if (row != null)
                {
                    // [CHANGED] R3: dynamic choices (resolutions) are re-read too.
                    if (adapter.DynamicChoices) Refreshers.Add(() => row.SetChoices(adapter.Choices!(), adapter.GetIndex!(settings)));
                    else Refreshers.Add(() => row.SetValue(adapter.GetIndex!(settings)));
                }
                break;
            }
            case AdapterKind.Button:
            {
                // [CHANGED] Vanilla's own wording when its row is found ("Export Player.log"), so labels follow
                // vanilla; the adapter's label otherwise.
                MonoBehaviour? script = null;
                scan?.Scripts.TryGetValue(adapter.Script, out script);
                string label = (script != null ? VanillaText(script) : null) ?? adapter.Label;
                // [CHANGED] And vanilla's own size: its row is still laid out where vanilla put it.
                RectTransform? vanillaRect = script != null ? script.GetComponent<RectTransform>() : null;
                Vector2? size = vanillaRect != null && vanillaRect.rect.height > 0f ? vanillaRect.rect.size : null;
                ButtonRow? row = null;
                row = page.AddButton(label, ButtonHandler(adapter, () => row), size);
                if (row != null) Refreshers.Add(() => row.SetEnabled(adapter.Enabled?.Invoke() ?? true));
                break;
            }
        }
    }

    /// <summary>The first text in a vanilla row (its label or button text), or null if it has none.</summary>
    private static string? VanillaText(MonoBehaviour script)
    {
        var texts = script.GetComponentsInChildren<Il2CppTMPro.TMP_Text>(true);
        return texts.Length > 0 && !string.IsNullOrWhiteSpace(texts[0].text) ? texts[0].text : null;
    }

    /// <summary>
    /// One click handler per button adapter, made once per run (UIBuilder.SetClick keeps one converted
    /// handler per delegate). The row it updates is looked up at click time, so a rebuilt window's row is used.
    /// </summary>
    private static readonly Dictionary<string, Action> ButtonHandlers = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Func<ButtonRow?>> ButtonRows = new(StringComparer.Ordinal);

    private static Action ButtonHandler(VanillaAdapter adapter, Func<ButtonRow?> row)
    {
        ButtonRows[adapter.Id] = row;
        if (!ButtonHandlers.TryGetValue(adapter.Id, out Action? handler))
        {
            handler = () => adapter.Click?.Invoke(() =>
            {
                ButtonRow? current = ButtonRows.TryGetValue(adapter.Id, out var get) ? get() : null;
                if (current != null) MelonLoader.MelonCoroutines.Start(ShowBriefly(current, ExportedMessage));
            });
            ButtonHandlers[adapter.Id] = handler;
        }
        return handler;
    }

    /// <summary>Shows a message on a button row for a moment, then its label again.</summary>
    private static System.Collections.IEnumerator ShowBriefly(ButtonRow row, string message)
    {
        row.ShowMessage(message);
        yield return new WaitForSecondsRealtime(MessageSeconds);
        row.ShowMessage(null);
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

                // [CHANGED] The Options prompt, just left of the button; shown with a gamepad (see WatchOptions).
                TMP_Text? style = root.Find(UIPathIndex.WindowTitle)?.GetComponent<TMP_Text>();
                _vanillaOptionsGlyph = GamepadGlyphs.Create(show.transform, "GlyphOptions", MeowcowWindow.GlyphSize, style);
                RectTransform glyphRect = _vanillaOptionsGlyph.GetComponent<RectTransform>();
                glyphRect.anchorMin = glyphRect.anchorMax = new Vector2(0f, 0.5f);
                glyphRect.pivot = new Vector2(1f, 0.5f);
                glyphRect.anchoredPosition = new Vector2(-MeowcowWindow.GlyphGap, 0f);
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
    private static GameObject? _vanillaOptionsGlyph;
    private static int _optionsWatch; // bumped on each open and close, so an old watch stops

    /// <summary>
    /// While vanilla's screen is open: its Options prompt follows the device, and the gamepad's Options button
    /// switches to the improved menu (as it switches the other way in the improved window).
    /// </summary>
    /// <remarks>[CHANGED] New. [PERFORMANCE] One button read per frame while vanilla's Settings is open.</remarks>
    private static System.Collections.IEnumerator WatchOptions(int token)
    {
        bool? shown = null;
        yield return null; // vanilla marks the screen open after this handler (the navigator's lesson)
        while (token == _optionsWatch && _vanilla != null) // ends when the close handler bumps the token
        {
            bool gamepad = Il2CppScheduleOne.GameInput.GetCurrentInputDeviceIsGamepad();
            if (shown != gamepad)
            {
                shown = gamepad;
                GamepadGlyphs.Show(_vanillaOptionsGlyph, gamepad ? GamepadGlyphs.Select : null);
            }
            if (gamepad && WindowNavigator.OptionsPressed())
            {
                UIBuilder.RunSafely(ShowImproved, "Options button");
                yield break;
            }
            yield return null;
        }
    }

    private static void OnVanillaOpened() => UIBuilder.RunSafely(() =>
    {
        // [CHANGED] R4 fallback: vanilla opened its screen itself, but the improved menu is chosen. Hand over
        // next frame (not inside vanilla's own open).
        bool onPurpose = _openingVanillaOnPurpose;
        _openingVanillaOnPurpose = false;
        if (_redirectOpens && !onPurpose && _useVanillaMenu?.Value != true)
        {
            MelonLoader.MelonCoroutines.Start(HandOverNextFrame());
            return;
        }

        _cornerPrompt = GameObject.Find(UIPathIndex.MenuCornerPrompt) ?? GameObject.Find(UIPathIndex.InGameCornerPrompt);
        _cornerPromptWasActive = _cornerPrompt != null && _cornerPrompt.activeSelf;
        if (_cornerPromptWasActive) _cornerPrompt!.SetActive(false);
        InputPrompts.Shown(_vanillaExtras);
        MelonLoader.MelonCoroutines.Start(WatchOptions(++_optionsWatch)); // [CHANGED] the Options button
    }, "vanilla Settings open");

    private static System.Collections.IEnumerator HandOverNextFrame()
    {
        yield return null;
        UIBuilder.RunSafely(() =>
        {
            if (_vanilla != null && _vanilla.IsOpen) _vanilla.Close();
            Open();
        }, "handing over to the improved Settings");
    }

    private static void OnVanillaClosed() => UIBuilder.RunSafely(() =>
    {
        _optionsWatch++; // [CHANGED] stops the Options watch
        InputPrompts.Hidden(_vanillaExtras);
        // Only shown again if it was showing before, so a prompt vanilla hid stays hidden.
        if (_cornerPromptWasActive && _cornerPrompt != null) _cornerPrompt.SetActive(true);
        _cornerPromptWasActive = false;
    }, "vanilla Settings close");
}