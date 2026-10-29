using System;
using System.Collections.Generic;
using MeowcowCore.Foundation;
using Il2CppTMPro;
using MeowcowCore.SuiteUI;
using UnityEngine;
using UIContentPanel = Il2CppScheduleOne.UIContentPanel;
using UISelectable = Il2CppScheduleOne.UISelectable;

namespace MeowcowCore.Menus;

/// <summary>
/// Vanilla's Settings menu, reorganised (design §9): vanilla's real screen becomes a Meowcow window, and
/// its own rows are moved into the suite's tabs and subtabs. Moved, not copied: each row keeps its own
/// script, which keeps applying vanilla's setting exactly as before.
/// </summary>
/// <remarks>
/// [CHANGED] New (vanilla Settings rework, slice 1: main menu, vanilla's rows only). The Game tab (in a
/// save), Core's new settings and the keybind rework come in later slices.
/// A row that isn't found (e.g. after a game update) is skipped with a warning; the rest still move.
/// [PERFORMANCE] About 40 reparents once per menu load; nothing per frame of ours.
/// </remarks>
internal static class SettingsMenu
{
    private static readonly MeowcowLogger Log = CoreMod.Log.Tagged("Settings");

    /// <summary>All of a vanilla page's rows not placed elsewhere, in their order.</summary>
    private const string RestOfPage = "*";

    /// <summary>The vanilla page itself, moved whole as a subtab (for pages run by a script on the page).</summary>
    private const string WholePage = "#page";

    /// <summary>
    /// Where each vanilla row goes: vanilla page, row name, our tab, our subtab (null for a tab with one
    /// page), and an optional new label. Tabs and subtabs appear in the order they first appear here.
    /// </summary>
    /// <remarks>
    /// The one place that knows vanilla's row names (instead of UIPathIndex), since the names only mean
    /// something next to where each row goes. A renamed row after a game update is a one-line fix here.
    /// Names come from the hierarchy dump of the Settings screen.
    /// </remarks>
    private static readonly Placement[] Layout =
    {
        // General
        new("Display", "PauseOnLoseFocus", "General", "Main"),
        new("Other", "AutoBackupSaves", "General", "Main"),
        // [CHANGED] The current save's settings (in a save only): vanilla's Game page, moved whole, since a
        // script on the page runs its rows and shows its host-only Blocker.
        new("Game", WholePage, "General", "Current Game", InSaveOnly: true),
        new("Other", "Export Player Log", "General", "Misc"),
        new("Other", "Export Player Log (1)", "General", "Misc"),

        // Gameplay
        new("Display", "InterfaceScaleSlider", "Gameplay", "Interface"),
        new("Display", "Units", "Gameplay", "Interface"),
        new("Gamepad", "PointerSensitivity", "Gameplay", "Interface"),
        new("Display", "CameraBobbingSlider", "Gameplay", "Camera"),
        new("Graphics", "FOV", "Gameplay", "Camera"),
        new("Controls", "SettingsSlider", "Gameplay", "Camera", "Mouse Camera Sensitivity"),
        new("Controls", "InvertYToggle", "Gameplay", "Camera", "Mouse Invert Y"),
        new("Gamepad", "CameraSensitivity", "Gameplay", "Camera", "Gamepad Camera Sensitivity"),

        // Graphics
        new("Display", "Resolution", "Graphics", null),
        new("Display", "Display", "Graphics", null),
        new("Display", "Display Mode", "Graphics", null),
        new("Display", "VSync", "Graphics", null),
        new("Display", "TargetFramerateSlider", "Graphics", null),
        new("Graphics", "QualityPopupSelector", "Graphics", null),
        new("Graphics", "AntiAliasingPopupSelector", "Graphics", null),
        new("Graphics", "SSAOToggle", "Graphics", null),
        new("Graphics", "GodRaysToggle", "Graphics", null),
        new("Display", "Apply", "Graphics", null), // appears after a display change; applies it

        // Audio
        new("Audio", RestOfPage, "Audio", null),

        // Keybinds (vanilla's binding sections, until the keybind rework). [CHANGED] The "Bindings" header
        // opens the bindings, after Sprint Mode; its Restore Default button moves to the top of the page
        // (see TouchUpKeybinds).
        new("Controls", "SprintModePopupSelector", "Keybinds", "Mouse & Keyboard"),
        new("Controls", "BindingLabel", "Keybinds", "Mouse & Keyboard"),
        new("Controls", RestOfPage, "Keybinds", "Mouse & Keyboard"),
        new("Gamepad", "BindingLabel", "Keybinds", "Gamepad"),
        new("Gamepad", RestOfPage, "Keybinds", "Gamepad"),
    };

    /// <summary>Vanilla rows that aren't moved anywhere: the "Settings" headers above the moved settings.</summary>
    private static readonly HashSet<string> LeftBehind = new(StringComparer.Ordinal) { "SettingsLabel" };

    /// <param name="InSaveOnly">The vanilla page only exists in a save; its absence elsewhere is expected.</param>
    private sealed record Placement(string Page, string Row, string Tab, string? Subtab, string? Label = null,
        bool InSaveOnly = false);

    /// <summary>
    /// Reorganises this scene's Settings screen. Called once per menu load, before the player can open it.
    /// Keeps an untouched copy first, which Meowcow Settings is cloned from.
    /// </summary>
    /// <returns>The window, or null if the screen was left vanilla.</returns>
    internal static MeowcowWindow? Setup()
    {
        var screen = UIPathIndex.FindSettingsScreen();
        if (screen == null)
        {
            Log.Warning("Settings screen not found; left vanilla");
            return null;
        }

        MeowcowWindow.CapturePristineSettings(); // before anything changes
        MeowcowWindow? window = MeowcowWindow.AdoptSettings(screen);
        if (window?.VanillaPages == null) return null;

        List<UISelectable> gamepadControls = CollectGamepadControls(window.VanillaPages);
        var tabs = new Dictionary<string, WindowTab>(StringComparer.Ordinal);
        var pages = new Dictionary<string, WindowPage>(StringComparer.Ordinal);
        var moved = new HashSet<IntPtr>();
        var movedRows = new List<(WindowPage page, Transform row)>();
        int count = 0;

        foreach (Placement placement in Layout)
        {
            try
            {
                Transform? pageRoot = window.VanillaPages.Find(placement.Page);
                if (pageRoot == null)
                {
                    if (!placement.InSaveOnly) Log.Warning($"Vanilla page '{placement.Page}' not found; its rows aren't shown");
                    continue;
                }

                if (placement.Row == WholePage)
                {
                    AdoptWholePage(window, tabs, pages, placement, pageRoot);
                    continue;
                }

                WindowPage page = PageFor(window, tabs, pages, placement);
                foreach (Transform row in RowsFor(pageRoot, placement, moved))
                {
                    page.AddExistingRow(row, gamepadControls, placement.Label);
                    moved.Add(row.Pointer);
                    movedRows.Add((page, row));
                    count++;
                }
            }
            catch (Exception e)
            {
                Log.Error($"Couldn't move '{placement.Row}' from '{placement.Page}'", e);
            }
        }

        TouchUpKeybinds(movedRows);
        Log.Debug($"Settings reorganised: {count} rows moved");
        return window;
    }

    // ---------- Keybinds touch-ups ----------
    // [CHANGED] New (Evelyna's Keybinds layout). Each step is independent and skipped if its row is missing.

    private const string RestoreDefaultText = "Restore Default Bindings";
    private const string CannotBeRebound = "(Cannot be rebound)";
    private const float ButtonTextPadding = 40f; // room either side of the Restore Default text
    private const float ButtonSpaceAbove = 20f;  // [CHANGED] between the subtab strip and Restore Default
    private const float ButtonSpaceBelow = 8f;

    /// <summary>
    /// The bindings header keeps its "Bindings" title where the bindings start, and its button, renamed
    /// "Restore Default Bindings", moves to its own row at the top of the page, at the label column's start;
    /// the mouse-button row is split into one row per button, laid out like binding rows; and labels marked
    /// "(Cannot be rebound)" put that note on its own line.
    /// </summary>
    private static void TouchUpKeybinds(List<(WindowPage page, Transform row)> rows)
    {
        foreach ((WindowPage page, Transform row) in rows)
        {
            try
            {
                if (row.name == UIPathIndex.BindingHeader) TouchUpBindingHeader(row);
                else if (row.name == UIPathIndex.MouseInfoRow) SplitMouseRow(page, row);
                else BreakCannotBeRebound(row);
            }
            catch (Exception e)
            {
                Log.Error($"Couldn't adjust '{row.name}'", e);
            }
        }
    }

    /// <summary>
    /// The header keeps its "Bindings" title. Its button is renamed, widened to fit, and moved to a row of its
    /// own at the top of the page, with some space above it, at the start of the label column.
    /// </summary>
    /// <remarks>
    /// [CHANGED] The title stays (was: removed) and the button gets its own row (was: the header moved to the
    /// top). The button stays on the page's gamepad list: it was registered while still in the header.
    /// </remarks>
    private static void TouchUpBindingHeader(Transform header)
    {
        RectTransform? button = header.Find(UIPathIndex.RestoreDefaultButton)?.TryCast<RectTransform>();
        if (button == null || header.parent == null) return;

        var texts = button.GetComponentsInChildren<TMP_Text>(true);
        if (texts.Length > 0)
        {
            texts[0].text = RestoreDefaultText;
            texts[0].enableWordWrapping = false;
            float wanted = texts[0].preferredWidth + ButtonTextPadding;
            if (button.rect.width < wanted) button.sizeDelta += new Vector2(wanted - button.rect.width, 0f);
        }

        // A row of its own, first on the page: the button's height plus the space around it.
        var row = new GameObject("RestoreDefaultRow");
        RectTransform rowRect = row.AddComponent<RectTransform>();
        row.transform.SetParent(header.parent, false);
        row.transform.SetSiblingIndex(0);
        RectTransform headerRect = header.TryCast<RectTransform>()!;
        rowRect.sizeDelta = new Vector2(headerRect.rect.width, button.rect.height + ButtonSpaceAbove + ButtonSpaceBelow);

        button.SetParent(row.transform, false);
        button.anchorMin = new Vector2(button.anchorMin.x, 0.5f);
        button.anchorMax = new Vector2(button.anchorMax.x, 0.5f);
        button.pivot = new Vector2(button.pivot.x, 0.5f);
        button.anchoredPosition = new Vector2(button.anchoredPosition.x, -(ButtonSpaceAbove - ButtonSpaceBelow) / 2f);
        WindowPage.PlaceAtLabelColumnStart(row.transform, button);
    }

    /// <summary>
    /// Vanilla shows both mouse buttons side by side in one row. Each becomes its own row, the same size as
    /// the original, with its text in the label column and its icon at the control column.
    /// </summary>
    private static void SplitMouseRow(WindowPage page, Transform mouseRow)
    {
        RectTransform rowRect = mouseRow.TryCast<RectTransform>()!;
        Vector2 size = rowRect.rect.size;
        int index = mouseRow.GetSiblingIndex();

        var buttons = new List<Transform>();
        for (int i = 0; i < mouseRow.childCount; i++) buttons.Add(mouseRow.GetChild(i));

        foreach (Transform button in buttons)
        {
            var texts = button.GetComponentsInChildren<TMP_Text>(true);
            if (texts.Length == 0) continue;
            TMP_Text label = texts[0];

            // The parts that aren't the label (the icon and its shade) are what goes in the control column.
            var controls = new List<RectTransform>();
            for (int i = 0; i < button.childCount; i++)
            {
                Transform part = button.GetChild(i);
                if (label.transform == part || label.transform.IsChildOf(part)) continue;
                RectTransform? partRect = part.TryCast<RectTransform>();
                if (partRect != null) controls.Add(partRect);
            }

            RectTransform buttonRect = button.TryCast<RectTransform>()!;
            button.SetParent(mouseRow.parent, false);
            button.SetSiblingIndex(++index);
            buttonRect.sizeDelta = size;
            page.AlignCustomRow(button, label, controls);
        }

        mouseRow.gameObject.SetActive(false); // empty now; hidden rather than destroyed
    }

    /// <summary>"Select (Cannot be rebound)" → "Select" with "(Cannot be rebound)" on the next line.</summary>
    private static void BreakCannotBeRebound(Transform row)
    {
        foreach (TMP_Text text in row.GetComponentsInChildren<TMP_Text>(true))
        {
            string current = text.text;
            int at = current.IndexOf(" " + CannotBeRebound, StringComparison.Ordinal);
            if (at >= 0) text.text = current.Substring(0, at) + "\n" + CannotBeRebound;
        }
    }

    /// <summary>The page for a placement, creating its tab and subtab the first time.</summary>
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

    /// <summary>A whole vanilla page becomes a subtab: its panel (with its scripts) moves into the tab.</summary>
    private static void AdoptWholePage(MeowcowWindow window, Dictionary<string, WindowTab> tabs,
        Dictionary<string, WindowPage> pages, Placement placement, Transform pageRoot)
    {
        var panels = pageRoot.GetComponentsInChildren<UIContentPanel>(true);
        if (panels.Length == 0)
        {
            Log.Warning($"Vanilla page '{placement.Page}' has no panel to move");
            return;
        }

        WindowTab tab = TabFor(window, tabs, placement);
        string subtab = placement.Subtab ?? placement.Tab;
        pages[placement.Tab + "/" + subtab] = tab.AddExistingPage(subtab, panels[0].gameObject);
    }

    private static Il2CppSystem.Action? _firstOpen; // kept referenced (IL2CPP rule)
    private static Il2CppScheduleOne.UI.MainMenu.SettingsScreen? _waitingScreen;

    /// <summary>
    /// In a save, the Settings screen is reorganised the first time it opens, not when the session starts.
    /// Called by CoreLifecycle at session start.
    /// </summary>
    /// <remarks>
    /// [CHANGED] Was: one frame after the session started. The pause menu's canvas is switched off until the
    /// game is paused, and a switched-off canvas has no size, so every row was measured as zero-sized and
    /// aligned wrongly (labels gone, rows on top of each other). At first open the canvas is on.
    /// [PERFORMANCE] The reorganising (about 40 reparents) happens on that first open, once per session.
    /// </remarks>
    internal static void SetupInSave() => MelonLoader.MelonCoroutines.Start(WaitForFirstOpen());

    private static System.Collections.IEnumerator WaitForFirstOpen()
    {
        yield return null; // the pause menu sets itself up first
        try
        {
            var screen = UIPathIndex.FindSettingsScreen();
            if (screen == null)
            {
                Log.Warning("In-game Settings screen not found; left vanilla");
                yield break;
            }
            _waitingScreen = screen;
            _firstOpen ??= Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(new Action(OnFirstOpen));
            screen.State.add_OnBecomeTopSibling(_firstOpen);
        }
        catch (Exception e)
        {
            Log.Error("Couldn't prepare the in-game Settings", e);
        }
    }

    /// <summary>Called from native code (the screen opening): exceptions must never escape.</summary>
    private static void OnFirstOpen()
    {
        try
        {
            var screen = _waitingScreen;
            _waitingScreen = null;
            if (screen == null) return;
            screen.State.remove_OnBecomeTopSibling(_firstOpen);

            // The window's own open handling was added during this open, so it runs from the next one: this
            // first time, it's run here.
            Setup()?.NotifyOpened();
        }
        catch (Exception e)
        {
            Log.Error("Couldn't reorganise the in-game Settings", e);
        }
    }

    /// <summary>The rows a placement moves: one named row, or every remaining row of the page.</summary>
    private static List<Transform> RowsFor(Transform pageRoot, Placement placement, HashSet<IntPtr> moved)
    {
        var rows = new List<Transform>();
        if (placement.Row != RestOfPage)
        {
            Transform? row = FindDeep(pageRoot, placement.Row);
            if (row == null) Log.Warning($"Vanilla row '{placement.Row}' not found in '{placement.Page}'; not shown");
            else if (!moved.Contains(row.Pointer)) rows.Add(row);
            return rows;
        }

        Transform? container = RowsContainer(pageRoot);
        if (container == null)
        {
            Log.Warning($"Vanilla page '{placement.Page}' has no rows to move");
            return rows;
        }
        for (int i = 0; i < container.childCount; i++)
        {
            Transform row = container.GetChild(i);
            if (!moved.Contains(row.Pointer) && !LeftBehind.Contains(row.name)) rows.Add(row);
        }
        return rows;
    }

    /// <summary>Where a vanilla page's rows sit: its scroll view's content, or the page panel itself.</summary>
    private static Transform? RowsContainer(Transform pageRoot)
    {
        var scrolls = pageRoot.GetComponentsInChildren<UnityEngine.UI.ScrollRect>(true);
        foreach (var scroll in scrolls)
        {
            // A dropdown's item list is also a scroll view; the page's own comes first, but be sure.
            if (scroll.name != UIPathIndex.DropdownTemplate && scroll.content != null) return scroll.content;
        }
        var panels = pageRoot.GetComponentsInChildren<UIContentPanel>(true);
        return panels.Length > 0 ? panels[0].transform : null;
    }

    /// <summary>The nearest descendant with this name (breadth-first, so a row wins over anything inside rows).</summary>
    private static Transform? FindDeep(Transform root, string name)
    {
        var queue = new Queue<Transform>();
        for (int i = 0; i < root.childCount; i++) queue.Enqueue(root.GetChild(i));
        while (queue.Count > 0)
        {
            Transform t = queue.Dequeue();
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++) queue.Enqueue(t.GetChild(i));
        }
        return null;
    }

    /// <summary>
    /// Every control vanilla's pages had on their gamepad lists, in order, before the rows move. Each moved
    /// row then registers exactly the controls it had (binding buttons, Restore Default, dropdowns...).
    /// </summary>
    private static List<UISelectable> CollectGamepadControls(Transform vanillaPages)
    {
        var controls = new List<UISelectable>();
        foreach (UIContentPanel panel in vanillaPages.GetComponentsInChildren<UIContentPanel>(true))
        {
            if (panel.name == UIPathIndex.DropdownTemplate) continue;
            var list = panel.selectables;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null) controls.Add(list[i]);
            }
        }
        return controls;
    }
}