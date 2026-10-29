using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Il2CppTMPro;
using MeowcowCore.Foundation;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MenuScreen = Il2CppScheduleOne.UI.MainMenu.MenuScreen;
using SettingsScreen = Il2CppScheduleOne.UI.MainMenu.SettingsScreen;
using UIPanel = Il2CppScheduleOne.UIPanel;
using UIScreen = Il2CppScheduleOne.UIScreen;
using UIContentPanel = Il2CppScheduleOne.UIContentPanel;
using UISelectable = Il2CppScheduleOne.UISelectable;
using UITab = Il2CppScheduleOne.UITab;
using UObject = UnityEngine.Object;

namespace MeowcowCore.SuiteUI;

/// <summary>
/// The suite's shared window (design §7). Built from a clone of vanilla's Settings screen, so it has
/// vanilla's look and behaviour: open and close transitions, Escape closing it and returning to the
/// previous screen, gamepad focus, and LB/RB cycling tabs.
/// Tabs run down the left side; each has its own scrolling page.
/// </summary>
/// <remarks>
/// [CHANGED] Slice 2c: 1100×700 frame, tabs level with the content, LB/RB centred above the first tab,
/// Back button (showing vanilla's own Back prompt while open), dragging by the title (double-click recentres).
/// [CHANGED] SuiteSettings slice 1: tabs hold pages (<see cref="WindowTab"/>, with subtabs) and pages hold
/// rows (<see cref="WindowPage"/>), all cloned from vanilla's Settings pages and rows.
/// Everything is event-driven: nothing of ours runs per frame.
/// How the clone stays safe: it's made inside an inactive parent, so no vanilla code runs on it until
/// the settings script has been swapped for a plain <c>MenuScreen</c>.
/// [SOURCE] Vanilla <c>MenuScreen</c>, <c>SettingsScreen</c>, <c>MonoState</c>, <c>UIScreen</c>, <c>UITab</c>;
/// layout metrics from the pre-rebuild window frame (findings doc §3).
/// </remarks>
public sealed class MeowcowWindow
{
    // Layout, in canvas units (1920×1080 reference). [SOURCE] findings doc §3.
    private const float Margin = 20f;        // inner padding
    private const float TitleHeight = 70f;   // title bar, also the drag handle
    private const float FooterHeight = 50f;  // room for the Back button
    private const float TabWidth = 170f;
    private const float TabHeight = 34f;
    private const float TabSpacing = 4f;
    private const float PagePadding = 10f;   // between the content area's edge and a page
    private const float BumperGap = 8f;      // between the LB and RB prompts
    // [CHANGED] Was 120×30: vanilla's Back prompt (borrowed into the button) is as tall as 30, leaving no
    // margin around its key. 40 still fits the 50-unit footer, centred.
    private const float BackWidth = 140f;
    private const float BackHeight = 40f;
    private const string BackLabel = "<color=#9A9A9A>Esc</color>   Back";

    // [CHANGED] The larger frame from the pre-rebuild window, capped to the screen.
    private static readonly Vector2 DefaultSize = new(1100f, 700f);

    // Where each window was last dragged, by object name. For this run only: saving it between runs
    // needs SuiteSettings.
    private static readonly Dictionary<string, Vector2> RememberedPositions = new();

    private static readonly MeowcowLogger Log = CoreMod.Log.Tagged("Window");

    /// <summary>
    /// Pieces of vanilla's Settings screen kept (hidden, scripts stripped) to clone the window's parts from.
    /// Row templates may be missing if a game update removes them; those rows are then skipped.
    /// </summary>
    internal sealed class WindowTemplates
    {
        public WindowTemplates(GameObject tab, GameObject strip, GameObject page,
            GameObject? toggleRow, GameObject? sliderRow, GameObject? choiceRow)
        {
            Tab = tab;
            Strip = strip;
            Page = page;
            ToggleRow = toggleRow;
            SliderRow = sliderRow;
            ChoiceRow = choiceRow;
        }

        public GameObject Tab { get; }
        public GameObject Strip { get; }
        public GameObject Page { get; }
        public GameObject? ToggleRow { get; }
        public GameObject? SliderRow { get; }
        public GameObject? ChoiceRow { get; }
    }

    private readonly List<WindowTab> _tabs = new();

    // Event handlers, kept referenced (IL2CPP rule for converted delegates).
    private readonly List<Il2CppSystem.Object> _handlers = new();

    private RectTransform? _root;
    private float _canvasScale = 1f;
    // [CHANGED] The Back button borrows vanilla's corner "Back" prompt while open (see BorrowPrompt).
    private Button? _backButton;
    private TMP_Text? _backText;
    private BorrowedPrompt? _borrowed;
    private static MeowcowWindow? _borrower;     // the window holding the prompt, if any
    private static bool _promptDumped;           // the prompt's hierarchy is logged once (debug)

    /// <summary>Where vanilla's corner prompt was, so it goes back exactly as it was.</summary>
    private sealed class BorrowedPrompt
    {
        public BorrowedPrompt(RectTransform prompt)
        {
            Prompt = prompt;
            Parent = prompt.parent;
            SiblingIndex = prompt.GetSiblingIndex();
            AnchorMin = prompt.anchorMin;
            AnchorMax = prompt.anchorMax;
            Pivot = prompt.pivot;
            AnchoredPosition = prompt.anchoredPosition;
            SizeDelta = prompt.sizeDelta;
            LocalScale = prompt.localScale;
        }

        public RectTransform Prompt { get; }
        public Transform? Parent { get; }
        public int SiblingIndex { get; }
        public Vector2 AnchorMin { get; }
        public Vector2 AnchorMax { get; }
        public Vector2 Pivot { get; }
        public Vector2 AnchoredPosition { get; }
        public Vector2 SizeDelta { get; }
        public Vector3 LocalScale { get; }
    }

    // [CHANGED] Our own double-click tracking (see OnTitleClick).
    private const float DoubleClickTime = 0.25f;     // seconds between the two clicks
    private const float DoubleClickDistance = 10f;   // screen pixels between the two clicks
    private float _lastClickTime = -1f;
    private Vector2 _lastClickPosition;
    private readonly UITab _tabStrip;
    private readonly RectTransform _tabsLayout;
    private readonly ToggleGroup? _tabGroup;

    private MeowcowWindow(MenuScreen screen, RectTransform content, TMP_Text? title, UITab tabStrip,
        RectTransform tabsLayout, WindowTemplates templates)
    {
        Screen = screen;
        Content = content;
        Title = title;
        _tabStrip = tabStrip;
        _tabsLayout = tabsLayout;
        _tabGroup = tabsLayout.GetComponent<ToggleGroup>();
        Templates = templates;
    }

    internal WindowTemplates Templates { get; }

    /// <summary>Fired when the window closes, however it closed (Back, Escape, another screen opening).</summary>
    /// <remarks>[CHANGED] New: Meowcow Settings saves pending changes on close.</remarks>
    public event Action? Closed;

    /// <summary>The window's screen: vanilla's open/close and Escape handling live here.</summary>
    public MenuScreen Screen { get; }

    /// <summary>The area right of the tabs that holds the pages.</summary>
    public RectTransform Content { get; }

    /// <summary>The title text. Also a style source: cloning it gives text in the window's font.</summary>
    public TMP_Text? Title { get; }

    /// <summary>False once the window's scene is gone (it's destroyed with the scene).</summary>
    public bool IsAlive => Screen != null;

    public bool IsOpen => IsAlive && Screen.IsOpen;

    /// <summary>The selected tab's index, or -1 when there are no tabs.</summary>
    public int SelectedTab { get; private set; } = -1;

    public void Open()
    {
        if (!IsAlive) return;
        Screen.Open();
        SelectTabStrip();
    }

    public void Close()
    {
        if (IsAlive) Screen.Close();
    }

    // ---------- Tabs ----------

    /// <summary>
    /// Adds a tab at the bottom of the list. Fill it with <see cref="WindowTab.AddPage"/>; with two or more
    /// pages it gets a subtab strip. The first tab added is selected.
    /// </summary>
    /// <remarks>
    /// [CHANGED] Returns a <see cref="WindowTab"/> (was the page's content) now that tabs hold subtab pages.
    /// [PERFORMANCE] One tab clone per tab, once per window build.
    /// </remarks>
    public WindowTab AddTab(string label)
    {
        int index = _tabs.Count;
        (Toggle toggle, UISelectable? selectable) = CreateTabButton(_tabsLayout, _tabStrip, _tabGroup, label, null,
            on =>
            {
                if (on) ShowTab(index);
            });

        // The tab's view: an empty object filling the content area, holding its strip and pages.
        var view = new GameObject("Tab " + label);
        view.AddComponent<RectTransform>();
        view.transform.SetParent(Content, false);
        SetRect(view.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        view.SetActive(false);

        var tab = new WindowTab(this, label, view, toggle, selectable);
        _tabs.Add(tab);
        if (index == 0) toggle.isOn = true; // shows the first tab
        return tab;
    }

    /// <summary>Selects a tab, as if it were clicked.</summary>
    public void SelectTab(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;
        _tabs[index].Toggle.isOn = true; // the toggle group turns the others off
    }

    /// <summary>
    /// A tab button cloned from vanilla's tab, in a strip's list and gamepad list. Used for the left tabs and
    /// for subtabs. <paramref name="onToggled"/> runs safely when it's switched on or off.
    /// </summary>
    /// <param name="width">Fixed width for a horizontal strip; null to let a vertical list size it.</param>
    /// <remarks>
    /// [CHANGED] Shared by tabs and subtabs (was inside AddTab). The toggle group is set explicitly: the
    /// template still belongs to the main strip's group.
    /// </remarks>
    internal (Toggle toggle, UISelectable? selectable) CreateTabButton(Transform layout, UITab strip, ToggleGroup? group,
        string label, float? width, Action<bool> onToggled)
    {
        GameObject button = UObject.Instantiate(Templates.Tab, layout);
        button.name = "Tab " + label;

        TMP_Text? text = button.transform.Find(UIPathIndex.WindowTabLabel)?.GetComponent<TMP_Text>();
        if (text != null) text.text = label;

        LayoutElement element = button.GetComponent<LayoutElement>() ?? button.AddComponent<LayoutElement>();
        element.minHeight = TabHeight;
        element.preferredHeight = TabHeight;
        if (width.HasValue)
        {
            element.minWidth = width.Value;
            element.preferredWidth = width.Value;
        }

        // A fresh event: no vanilla wiring, only ours. Converted once per button and kept by the window.
        Toggle toggle = button.GetComponent<Toggle>();
        toggle.onValueChanged = new Toggle.ToggleEvent();
        toggle.isOn = false;
        toggle.interactable = true; // every new button starts clickable; its tab or page manages it from here
        toggle.group = group;
        toggle.onValueChanged.AddListener(Keep(UIBuilder.ToIl2Cpp<UnityAction<bool>>(
            new Action<bool>(on => UIBuilder.RunSafely(() => onToggled(on), $"Tab '{label}'")))));

        // Into the strip's gamepad list, so its bumpers reach it. Vanilla's UITab.Start does the
        // "ignore when navigating" part, but only for buttons present at Start; doing it here covers both.
        UISelectable? selectable = button.GetComponent<UISelectable>();
        if (selectable != null)
        {
            strip.AddSelectable(selectable);
            selectable.SetIgnoreWhenNavigating(true);
        }

        button.SetActive(true);
        return (toggle, selectable);
    }

    /// <summary>Keeps a converted handler referenced for the window's lifetime (IL2CPP rule).</summary>
    internal T Keep<T>(T handler) where T : Il2CppSystem.Object
    {
        _handlers.Add(handler);
        return handler;
    }

    /// <summary>
    /// Shows one tab and hides the rest. The selected tab can't be clicked again, as in vanilla.
    /// </summary>
    private void ShowTab(int index)
    {
        SelectedTab = index;
        for (int i = 0; i < _tabs.Count; i++)
        {
            _tabs[i].Toggle.interactable = i != index;
            _tabs[i].SetVisible(i == index);
        }
        _tabs[index].OnShown();
        Log.Debug($"Tab {index} shown");
    }

    /// <summary>
    /// Selects the tab strip on the current tab, so LB/RB cycle tabs. Vanilla's SettingsScreen did this in
    /// OnEnable; the plain MenuScreen that replaced it doesn't.
    /// </summary>
    private void SelectTabStrip()
    {
        if (SelectedTab < 0) return;
        try
        {
            _tabStrip.Select(_tabs[SelectedTab].Selectable, true);
            _tabs[SelectedTab].OnShown(); // the subtab strip's focus and the page's scrolling
        }
        catch (Exception e)
        {
            Log.Warning($"Couldn't select the tab strip; LB/RB may not cycle tabs: {e.Message}");
        }
    }

    // ---------- Building ----------

    /// <summary>
    /// Builds a new window from vanilla's Settings screen in the current scene, or returns null (with a
    /// warning) if the screen isn't there or doesn't look as expected. The window starts closed, with no tabs.
    /// </summary>
    /// <param name="objectName">Unique object name: vanilla tracks open UI by name.</param>
    /// <remarks>[PERFORMANCE] One clone of the Settings screen; done once per scene, on first use.</remarks>
    public static MeowcowWindow? CreateFromSettings(string objectName, string title)
    {
        SettingsScreen? source = UIPathIndex.FindSettingsScreen();
        if (source == null)
        {
            Log.Warning("Vanilla Settings screen not found; window not built");
            return null;
        }

        GameObject? clone = null;
        GameObject? staging = null;
        try
        {
            // Inside an inactive parent, nothing on the clone wakes up, even if the source is open.
            staging = new GameObject("MeowcowStaging");
            staging.SetActive(false);
            clone = UObject.Instantiate(source.gameObject, staging.transform);
            clone.name = objectName;

            UIBuilder.DumpHierarchy(clone.transform, Log); // debug only: how the shell is built

            MenuScreen screen = SwapToPlainScreen(clone, source);
            MeowcowWindow? window = BelongsToClone(screen, clone) ? Rebuild(clone, screen, title) : null;
            if (window == null)
            {
                UObject.Destroy(clone);
                return null;
            }

            // Into the real UI, then one activation so Awake runs exactly as for vanilla's screen:
            // MenuScreen.Awake registers Escape, hooks the state's open/close, and switches itself off.
            clone.SetActive(false);
            clone.transform.SetParent(source.transform.parent, false);
            clone.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
            window.SizeAndPlace(clone.transform.TryCast<RectTransform>()!, objectName);
            clone.SetActive(true);

            Log.Debug($"Built '{objectName}'");
            return window;
        }
        catch (Exception e)
        {
            Log.Error($"Couldn't build window '{objectName}'", e);
            if (clone != null) UObject.Destroy(clone);
            return null;
        }
        finally
        {
            if (staging != null) UObject.Destroy(staging);
        }
    }

    /// <summary>
    /// Turns the cloned Settings screen into an empty Meowcow window: keeps one tab and one scroll view
    /// as templates, removes vanilla's tabs, pages and leftovers, and lays out the frame.
    /// Null (with a warning) if an expected piece is missing.
    /// </summary>
    /// <remarks>[CHANGED] New in 2b.</remarks>
    private static MeowcowWindow? Rebuild(GameObject clone, MenuScreen screen, string title)
    {
        Transform root = clone.transform;
        RectTransform? content = root.Find(UIPathIndex.WindowContent)?.TryCast<RectTransform>();
        RectTransform? tabsPanel = root.Find(UIPathIndex.WindowTabsPanel)?.TryCast<RectTransform>();
        RectTransform? tabsLayout = root.Find(UIPathIndex.WindowTabsLayout)?.TryCast<RectTransform>();
        UITab? tabStrip = tabsPanel != null ? tabsPanel.GetComponent<UITab>() : null;
        if (content == null || tabsPanel == null || tabsLayout == null || tabStrip == null)
        {
            Log.Warning("Settings screen doesn't look as expected (content or tabs missing); window not built");
            return null;
        }

        // Templates first, while vanilla's pieces still exist. [CHANGED] Rows before the page, since
        // taking the page empties its rows; the strip before the main tabs turn vertical.
        GameObject? toggleRow = TakeRowTemplate(screen, content, root, FindFirst<Il2CppScheduleOne.UIToggle>(content), "ToggleRowTemplate");
        GameObject? sliderRow = TakeRowTemplate(screen, content, root, FindFirst<Il2CppScheduleOne.UISlider>(content), "SliderRowTemplate");
        GameObject? choiceRow = TakeRowTemplate(screen, content, root, FindChoiceRow(content), "ChoiceRowTemplate");
        GameObject? stripTemplate = TakeStripTemplate(tabsPanel, root);
        GameObject? tabTemplate = TakeTabTemplate(tabsLayout, root);
        GameObject? pageTemplate = TakePageTemplate(screen, content, root);
        if (tabTemplate == null || pageTemplate == null || stripTemplate == null)
        {
            Log.Warning("Settings screen has no tab, strip or page to copy; window not built");
            return null;
        }
        if (toggleRow == null || sliderRow == null || choiceRow == null)
            Log.Warning("Settings screen is missing a row type; rows of that type won't be shown");

        // Vanilla's tabs: out of the strip's gamepad list, then gone.
        tabStrip.selectables.Clear();
        for (int i = tabsLayout.childCount - 1; i >= 0; i--) UObject.DestroyImmediate(tabsLayout.GetChild(i).gameObject);

        // Vanilla's pages and leftovers. Gamepad panels are detached before anything is destroyed.
        DetachPanels(screen, content);
        for (int i = content.childCount - 1; i >= 0; i--) UObject.DestroyImmediate(content.GetChild(i).gameObject);
        DestroyChild(screen, root, UIPathIndex.SettingsConfirmDisplay);
        DestroyChild(screen, root, UIPathIndex.SettingsVersion);

        TMP_Text? titleText = root.Find(UIPathIndex.WindowTitle)?.GetComponent<TMP_Text>();
        if (titleText != null) titleText.text = title;

        LayOut(root, tabsPanel, tabsLayout, content, titleText);

        // Tabs now run top to bottom, so LB/RB must cycle vertically. RB moves down the list.
        tabStrip.cycleDirection = UITab.CycleDirection.Vertical;
        tabStrip.reverseCycleDirection = true;

        var templates = new WindowTemplates(tabTemplate, stripTemplate, pageTemplate, toggleRow, sliderRow, choiceRow);
        var window = new MeowcowWindow(screen, content, titleText, tabStrip, tabsLayout, templates);
        window.AddBackButton(root);
        window.HookOpenClose();
        if (titleText != null) window.MakeDraggable(titleText);
        return window;
    }

    /// <summary>One of vanilla's tabs, moved out of the list and switched off, to clone our tabs from.</summary>
    private static GameObject? TakeTabTemplate(RectTransform tabsLayout, Transform root)
    {
        for (int i = 0; i < tabsLayout.childCount; i++)
        {
            Transform tab = tabsLayout.GetChild(i);
            if (tab.GetComponent<Toggle>() == null) continue;

            tab.SetParent(root, false);
            tab.gameObject.SetActive(false);
            tab.name = "TabTemplate";

            // [CHANGED] A clone copies vanilla's *current* state. If vanilla Settings was opened earlier,
            // this tab is the selected one: on, and made unclickable by SettingsScreen.ShowCategory. Every
            // tab cloned from it would start unclickable, so the template is reset to a plain, clickable tab.
            Toggle toggle = tab.GetComponent<Toggle>();
            toggle.isOn = false;
            toggle.interactable = true;
            return tab.gameObject;
        }
        return null;
    }

    /// <summary>
    /// One of vanilla's pages (a gamepad panel holding a scroll view, preferably with a scrollbar), emptied
    /// of rows and leftovers, stripped and switched off, to clone pages from.
    /// </summary>
    /// <remarks>
    /// [CHANGED] Was the scroll view alone; the whole page brings its gamepad panel, so rows are reachable
    /// by gamepad and the page scrolls to the selected row. Dropdown item lists also have a panel and a
    /// scroll view, so they're skipped by name.
    /// </remarks>
    private static GameObject? TakePageTemplate(MenuScreen screen, RectTransform content, Transform root)
    {
        UIContentPanel? chosen = null;
        ScrollRect? chosenScroll = null;
        foreach (UIContentPanel panel in content.GetComponentsInChildren<UIContentPanel>(true))
        {
            if (panel.name == UIPathIndex.DropdownTemplate) continue;
            var scrolls = panel.GetComponentsInChildren<ScrollRect>(true);
            if (scrolls.Length == 0) continue;

            if (chosen == null)
            {
                chosen = panel;
                chosenScroll = scrolls[0];
            }
            if (scrolls[0].verticalScrollbar != null)
            {
                chosen = panel;
                chosenScroll = scrolls[0];
                break;
            }
        }
        if (chosen == null || chosenScroll == null) return null;

        // Out of the window's gamepad screen before it's changed (see DetachPanels).
        DetachPanels(screen, chosen.transform);

        // Vanilla's rows go, and anything beside the scroll view (e.g. Display's Apply button).
        RectTransform? rows = chosenScroll.content;
        if (rows != null)
        {
            for (int i = rows.childCount - 1; i >= 0; i--) UObject.DestroyImmediate(rows.GetChild(i).gameObject);
        }
        Transform page = chosen.transform;
        for (int i = page.childCount - 1; i >= 0; i--)
        {
            Transform child = page.GetChild(i);
            if (!chosenScroll.transform.IsChildOf(child)) UObject.DestroyImmediate(child.gameObject);
        }

        // Its state reset too: no rows in its gamepad list, nothing to select by default.
        chosen.ClearAllSelectables();
        chosen.defaultSelectable = null;

        GameObject template = chosen.gameObject;
        UIBuilder.StripScripts(template);
        template.transform.SetParent(root, false);
        template.SetActive(false);
        template.name = "PageTemplate";
        return template;
    }

    /// <summary>
    /// A copy of vanilla's tab strip while it's still horizontal, emptied, to clone subtab strips from.
    /// </summary>
    /// <remarks>[CHANGED] New for subtabs.</remarks>
    private static GameObject? TakeStripTemplate(RectTransform tabsPanel, Transform root)
    {
        GameObject strip = UObject.Instantiate(tabsPanel.gameObject, root); // inside the inactive staging object
        strip.name = "StripTemplate";
        strip.SetActive(false);

        UITab? tab = strip.GetComponent<UITab>();
        Transform? layout = strip.transform.Find(UIPathIndex.TabStripLayout);
        if (tab == null || layout == null)
        {
            UObject.DestroyImmediate(strip);
            return null;
        }

        tab.selectables.Clear();
        for (int i = layout.childCount - 1; i >= 0; i--) UObject.DestroyImmediate(layout.GetChild(i).gameObject);
        return strip;
    }

    /// <summary>
    /// A vanilla setting row, moved out, stripped of its setting script (allow-list) and switched off, to
    /// clone rows from. Null if there's no such row.
    /// </summary>
    /// <remarks>[CHANGED] New for SuiteSettings' rows.</remarks>
    private static GameObject? TakeRowTemplate(MenuScreen screen, RectTransform content, Transform root, GameObject? row, string name)
    {
        if (row == null) return null;

        DetachPanels(screen, row.transform); // a dropdown's item list has a gamepad panel of its own
        UIBuilder.StripScripts(row);
        row.transform.SetParent(root, false);
        row.SetActive(false);
        row.name = name;
        return row;
    }

    /// <summary>The object of the first component of a type under the content area, or null.</summary>
    private static GameObject? FindFirst<T>(RectTransform content) where T : Component
    {
        var found = content.GetComponentsInChildren<T>(true);
        return found.Length > 0 ? found[0].gameObject : null;
    }

    /// <summary>The first dropdown row: the row holding a dropdown, with its name label beside it.</summary>
    private static GameObject? FindChoiceRow(RectTransform content)
    {
        foreach (TMP_Dropdown dropdown in content.GetComponentsInChildren<TMP_Dropdown>(true))
        {
            Transform? row = dropdown.transform.parent;
            if (row != null && row.Find(UIPathIndex.RowOptionName) != null) return row.gameObject;
        }
        return null;
    }

    /// <summary>Destroys a direct child by name, detaching its gamepad panels first. Missing is fine.</summary>
    private static void DestroyChild(MenuScreen screen, Transform root, string name)
    {
        Transform? child = root.Find(name);
        if (child == null) return;
        DetachPanels(screen, child);
        UObject.DestroyImmediate(child.gameObject);
    }

    /// <summary>
    /// The frame: tabs in a column on the left (bumper prompts above them), content to their right, room
    /// for the title above and the footer below. The window itself is sized in SizeAndPlace.
    /// </summary>
    private static void LayOut(Transform root, RectTransform tabsPanel, RectTransform tabsLayout, RectTransform content,
        TMP_Text? title)
    {
        // [CHANGED] The background fills the window, and the title spans its top: it's the drag handle.
        RectTransform? background = root.Find(UIPathIndex.WindowBackground)?.TryCast<RectTransform>();
        if (background != null) SetRect(background, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        if (title != null)
            SetRect(title.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -TitleHeight), Vector2.zero);

        // [CHANGED] Tabs start level with the content (no bumper row any more).
        SetRect(tabsPanel, new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(Margin, FooterHeight), new Vector2(Margin + TabWidth, -TitleHeight));

        SetRect(content, Vector2.zero, Vector2.one,
            new Vector2(Margin + TabWidth + Margin, FooterHeight), new Vector2(-Margin, -TitleHeight));

        // Vertical tabs. A GameObject can't hold two layout groups, so the horizontal one goes first.
        SetRect(tabsLayout, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        HorizontalLayoutGroup? horizontal = tabsLayout.GetComponent<HorizontalLayoutGroup>();
        if (horizontal != null) UObject.DestroyImmediate(horizontal);
        VerticalLayoutGroup vertical = tabsLayout.gameObject.AddComponent<VerticalLayoutGroup>();
        vertical.spacing = TabSpacing;
        vertical.childAlignment = TextAnchor.UpperCenter;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;

        // [CHANGED] LB and RB side by side, the pair centred over the tab column, sitting on top of the
        // first tab (the bottom of the title bar; the title text is centred over the whole window, so
        // they don't overlap). LB's right edge and RB's left edge meet at the column's centre, minus a gap.
        float centre = Margin + TabWidth / 2f;
        PlaceBumper(root, UIPathIndex.WindowLeftBumper, new Vector2(1f, 0f), centre - BumperGap / 2f);
        PlaceBumper(root, UIPathIndex.WindowRightBumper, new Vector2(0f, 0f), centre + BumperGap / 2f);
    }

    /// <summary>Places a bumper prompt by its pivot, with its bottom on the title bar's bottom edge.</summary>
    private static void PlaceBumper(Transform root, string name, Vector2 pivot, float x)
    {
        RectTransform? prompt = root.Find(name)?.TryCast<RectTransform>();
        if (prompt == null) return;
        Vector2 size = prompt.rect.size;
        prompt.anchorMin = new Vector2(0f, 1f);
        prompt.anchorMax = new Vector2(0f, 1f);
        prompt.pivot = pivot;
        prompt.sizeDelta = size;
        prompt.anchoredPosition = new Vector2(x, -TitleHeight);
    }

    // ---------- Size and position ----------

    /// <summary>
    /// 1100×700 (capped to the screen), centred on the screen or where it was last dragged this run.
    /// The pattern behind the window is scaled to cover it.
    /// </summary>
    /// <remarks>[CHANGED] New in 2c. Runs once the window is in the real UI, where the canvas is known.</remarks>
    private void SizeAndPlace(RectTransform root, string objectName)
    {
        _root = root;
        Canvas? canvas = UIBuilder.RootCanvas(root);
        if (canvas != null && canvas.scaleFactor > 0f) _canvasScale = canvas.scaleFactor;

        Vector2 available = canvas != null
            ? canvas.GetComponent<RectTransform>().rect.size - new Vector2(Margin * 2f, Margin * 2f)
            : DefaultSize;
        Vector2 size = Vector2.Min(DefaultSize, available);

        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = size;
        root.anchoredPosition = Clamp(RememberedPositions.TryGetValue(objectName, out Vector2 saved) ? saved : Vector2.zero);

        // The pattern keeps its shape and covers the bigger window. [SOURCE] findings doc §3.
        RectTransform? pattern = root.Find(UIPathIndex.WindowPattern)?.TryCast<RectTransform>();
        if (pattern != null)
        {
            Vector2 original = pattern.rect.size;
            if (original.x > 0f && original.y > 0f)
            {
                float scale = Mathf.Max(size.x / original.x, size.y / original.y, 1f) * 1.05f;
                pattern.sizeDelta = original * scale;
            }
        }
    }

    /// <summary>Keeps the whole window on screen: a centre position limited by half the free space.</summary>
    private Vector2 Clamp(Vector2 position)
    {
        if (_root == null) return position;
        RectTransform? parent = _root.parent?.TryCast<RectTransform>();
        if (parent == null) return position;

        Vector2 room = (parent.rect.size - _root.rect.size) / 2f;
        room = Vector2.Max(room, Vector2.zero);
        return new Vector2(Mathf.Clamp(position.x, -room.x, room.x), Mathf.Clamp(position.y, -room.y, room.y));
    }

    // ---------- Back button ----------

    /// <summary>
    /// Back in the footer's left corner, styled like the save slots' Import button. Without the style
    /// source (e.g. in-game), there's no button; Escape still closes the window.
    /// </summary>
    /// <remarks>[CHANGED] New in 2c. [SOURCE] Findings doc §3 (Back button).</remarks>
    private void AddBackButton(Transform root)
    {
        Button? style = UIPathIndex.FindImportButton();
        if (style == null)
        {
            Log.Debug("No Import button to copy; window has no Back button (Escape still works)");
            return;
        }

        Button back = UIBuilder.CloneButton(style, root, "Back", BackLabel, CloseCurrentScreen);
        _backButton = back;
        var labels = back.GetComponentsInChildren<TMP_Text>(true);
        _backText = labels.Length > 0 ? labels[0] : null;
        RectTransform rect = back.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.sizeDelta = new Vector2(BackWidth, BackHeight);
        rect.anchoredPosition = new Vector2(Margin, (FooterHeight - BackHeight) / 2f);
    }

    /// <summary>
    /// Closes whichever screen is open: a Back button can only be clicked on the open window. Static, so
    /// one click handler serves every window and every rebuild.
    /// </summary>
    private static void CloseCurrentScreen()
    {
        MenuScreen? current = MenuScreen.Current;
        if (current != null) current.Close();
    }

    // ---------- Open and close ----------

    /// <summary>
    /// Follows the screen's own open and close (whatever caused them, Escape included): on open, the Back
    /// button borrows vanilla's corner "Back" prompt; on close, it's returned and <see cref="Closed"/> fires.
    /// </summary>
    /// <remarks>
    /// [CHANGED] New in 2c. Uses the state's events rather than checking every frame.
    /// [SOURCE] Vanilla <c>MonoState</c> (OnBecomeTopSibling / OnNoLongerTopSibling drive MenuScreen's own open/close).
    /// </remarks>
    private void HookOpenClose()
    {
        Il2CppSystem.Action opened = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(new Action(OnOpened))
            ?? throw new InvalidOperationException("Couldn't convert the open handler for IL2CPP");
        Il2CppSystem.Action closed = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(new Action(OnClosed))
            ?? throw new InvalidOperationException("Couldn't convert the close handler for IL2CPP");
        _handlers.Add(opened);
        _handlers.Add(closed);
        Screen.State.add_OnBecomeTopSibling(opened);
        Screen.State.add_OnNoLongerTopSibling(closed);
    }

    /// <summary>Called from native code: exceptions must never escape.</summary>
    private void OnOpened()
    {
        try
        {
            BorrowPrompt();
        }
        catch (Exception e)
        {
            Log.Error("Window open handling failed", e);
        }
    }

    /// <summary>Called from native code: exceptions must never escape.</summary>
    private void OnClosed()
    {
        try
        {
            ReturnPrompt();
            Closed?.Invoke();
        }
        catch (Exception e)
        {
            Log.Error("Window close handling failed", e);
        }
    }

    // ---------- The Back button's prompt ----------

    /// <summary>
    /// Moves vanilla's corner "Back" prompt into our Back button, in place of its text. Vanilla keeps that
    /// prompt right for the input device, the controller type and rebinding, so the button always shows the
    /// right button (Esc, B, Circle...) with no device logic of ours. Only a prompt that's showing is
    /// borrowed; without one, the button keeps its text.
    /// </summary>
    /// <remarks>
    /// [CHANGED] New (was: hide the corner prompt while open). Idea: Evelyna.
    /// [PERFORMANCE] One lookup and one reparent per open.
    /// </remarks>
    private void BorrowPrompt()
    {
        if (_backButton == null || _borrowed != null) return;

        // Looked up on each open: the prompt belongs to the menu, which is rebuilt per scene.
        GameObject? found = GameObject.Find(UIPathIndex.MenuCornerPrompt); // only finds it if it's showing
        RectTransform? prompt = found != null ? found.GetComponent<RectTransform>() : null;
        if (prompt == null) return;

        if (!_promptDumped)
        {
            _promptDumped = true;
            UIBuilder.DumpHierarchy(prompt, Log); // debug only: what the borrowed prompt is made of
        }

        _borrowed = new BorrowedPrompt(prompt);
        _borrower = this;

        // Centred in the button. It comes from another canvas, which may scale differently, so its scale
        // is reset here and restored on return.
        prompt.SetParent(_backButton.transform, false);
        prompt.anchorMin = new Vector2(0.5f, 0.5f);
        prompt.anchorMax = new Vector2(0.5f, 0.5f);
        prompt.pivot = new Vector2(0.5f, 0.5f);
        prompt.anchoredPosition = Vector2.zero;
        prompt.localScale = Vector3.one;
        if (_backText != null) _backText.gameObject.SetActive(false);
    }

    /// <summary>Puts vanilla's prompt back exactly where it was, and the button's text back.</summary>
    private void ReturnPrompt()
    {
        BorrowedPrompt? borrowed = _borrowed;
        _borrowed = null;
        if (_borrower == this) _borrower = null;
        if (_backText != null) _backText.gameObject.SetActive(true);
        if (borrowed == null || borrowed.Prompt == null) return;

        RectTransform prompt = borrowed.Prompt;
        prompt.SetParent(borrowed.Parent, false);
        if (borrowed.Parent != null) prompt.SetSiblingIndex(borrowed.SiblingIndex);
        prompt.anchorMin = borrowed.AnchorMin;
        prompt.anchorMax = borrowed.AnchorMax;
        prompt.pivot = borrowed.Pivot;
        prompt.anchoredPosition = borrowed.AnchoredPosition;
        prompt.sizeDelta = borrowed.SizeDelta;
        prompt.localScale = borrowed.LocalScale;
    }

    /// <summary>
    /// Guard: returns the borrowed prompt before a scene change, in case a window is torn down without
    /// closing. A prompt left inside a destroyed window would be destroyed with it.
    /// </summary>
    /// <remarks>[CHANGED] New. Called by CoreLifecycle on every pre-scene-change, in or out of a session.</remarks>
    internal static void ReturnBorrowedPrompt()
    {
        try
        {
            _borrower?.ReturnPrompt();
        }
        catch (Exception e)
        {
            Log.Error("Couldn't return the borrowed Back prompt", e);
        }
    }

    // ---------- Dragging ----------

    /// <summary>
    /// Drag the window by its title; double-click the title to recentre it. Uses Unity's pointer events
    /// on the title, so nothing runs unless the title is being dragged or clicked.
    /// </summary>
    /// <remarks>
    /// [CHANGED] New in 2c (the pre-rebuild frame checked the mouse every frame).
    /// [PERFORMANCE] One small move per drag event; no per-frame cost.
    /// </remarks>
    private void MakeDraggable(TMP_Text title)
    {
        title.raycastTarget = true; // text ignores the pointer by default
        EventTrigger trigger = title.gameObject.GetComponent<EventTrigger>() ?? title.gameObject.AddComponent<EventTrigger>();
        AddTrigger(trigger, EventTriggerType.BeginDrag, _ => _lastClickTime = -1f); // a drag breaks a double-click
        AddTrigger(trigger, EventTriggerType.Drag, OnDrag);
        AddTrigger(trigger, EventTriggerType.EndDrag, _ => Remember());
        AddTrigger(trigger, EventTriggerType.PointerClick, OnTitleClick);
    }

    private void AddTrigger(EventTrigger trigger, EventTriggerType type, Action<BaseEventData> handler)
    {
        UnityAction<BaseEventData> converted = DelegateSupport.ConvertDelegate<UnityAction<BaseEventData>>(
                new Action<BaseEventData>(data => RunEvent(handler, data)))
            ?? throw new InvalidOperationException("Couldn't convert a drag handler for IL2CPP");
        _handlers.Add(converted);

        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(converted);
        trigger.triggers.Add(entry);
    }

    /// <summary>Pointer events come from native code: exceptions must never escape.</summary>
    private static void RunEvent(Action<BaseEventData> handler, BaseEventData data)
    {
        try
        {
            handler(data);
        }
        catch (Exception e)
        {
            Log.Error("Window drag handling failed", e);
        }
    }

    /// <summary>Moves the window with the pointer; screen pixels become canvas units via the canvas scale.</summary>
    private void OnDrag(BaseEventData data)
    {
        PointerEventData? pointer = data.TryCast<PointerEventData>();
        if (pointer == null || _root == null) return;
        _root.anchoredPosition = Clamp(_root.anchoredPosition + pointer.delta / _canvasScale);
    }

    /// <summary>
    /// A real double-click on the title recentres the window: two clicks, neither of them ending a drag,
    /// within 0.25 s and 10 px of each other.
    /// </summary>
    /// <remarks>
    /// [CHANGED] Was Unity's <c>clickCount</c>, which recentred the window at random while dragging quickly:
    /// the title handles both the drag and the click, so Unity counts releasing a drag as a click, and two
    /// quick drags (0.3 s apart) looked like a double-click.
    /// [PERFORMANCE] A time and distance check per click.
    /// </remarks>
    private void OnTitleClick(BaseEventData data)
    {
        PointerEventData? pointer = data.TryCast<PointerEventData>();
        if (pointer == null || _root == null) return;

        // Still flagged as dragging when the click arrives, so a drag's release is recognised and ignored.
        if (pointer.dragging)
        {
            _lastClickTime = -1f;
            return;
        }

        float now = Time.unscaledTime;
        bool isDouble = _lastClickTime >= 0f
            && now - _lastClickTime <= DoubleClickTime
            && (pointer.position - _lastClickPosition).sqrMagnitude <= DoubleClickDistance * DoubleClickDistance;

        if (!isDouble)
        {
            _lastClickTime = now;
            _lastClickPosition = pointer.position;
            return;
        }

        _lastClickTime = -1f;
        _root.anchoredPosition = Vector2.zero;
        Remember();
    }

    /// <summary>Remembers the position for this run, rounded to whole units.</summary>
    private void Remember()
    {
        if (_root == null) return;
        Vector2 position = new(Mathf.Round(_root.anchoredPosition.x), Mathf.Round(_root.anchoredPosition.y));
        _root.anchoredPosition = position;
        RememberedPositions[_root.name] = position;
    }

    /// <summary>Sets anchors and edge offsets in one call.</summary>
    internal static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    /// <summary>
    /// Replaces the clone's <c>SettingsScreen</c> with a plain <c>MenuScreen</c>, copying the fields that
    /// carry the screen behaviour. <c>SettingsScreen</c> is a <c>MenuScreen</c> subclass, so it can't
    /// simply be removed; this keeps the behaviour and drops the settings logic.
    /// </summary>
    private static MenuScreen SwapToPlainScreen(GameObject clone, SettingsScreen source)
    {
        SettingsScreen cloned = clone.GetComponent<SettingsScreen>();
        MenuScreen screen = clone.AddComponent<MenuScreen>();

        // The clone's own copies (Group, State, uiScreen, uiPanel point inside the clone after Instantiate).
        screen.ExitInputPriority = cloned.ExitInputPriority;
        screen.OpenOnStart = false;
        screen.AttachLobbyToScreen = cloned.AttachLobbyToScreen;
        screen.PreviousScreen = source.PreviousScreen; // where Escape returns to (Home on the main menu)
        screen.Group = cloned.Group;
        screen.State = cloned.State;
        screen.uiScreen = cloned.uiScreen;
        screen.uiPanel = cloned.uiPanel;

        UObject.DestroyImmediate(cloned);
        return screen;
    }

    /// <summary>
    /// Takes every gamepad panel under <paramref name="subtree"/> out of the window's gamepad screen, so
    /// it can be destroyed safely. Otherwise vanilla's <c>UIScreen</c> keeps them in its list, and opening
    /// the window crashes when it asks a destroyed panel whether it's visible.
    /// </summary>
    /// <remarks>
    /// Must run <i>before</i> destroying: <c>RemovePanel</c> compares the panel with its current selection
    /// using Unity's <c>==</c>, which treats a destroyed object as null, so removing an already-destroyed
    /// panel would itself crash.
    /// [SOURCE] Vanilla <c>UIScreen</c> (panels list, <c>RemovePanel</c>).
    /// [PERFORMANCE] One pass over the screen's panels (about a dozen), per subtree removed.
    /// </remarks>
    private static void DetachPanels(MenuScreen screen, Transform subtree)
    {
        UIScreen? uiScreen = screen.uiScreen;
        if (uiScreen != null)
        {
            // Collect first: RemovePanel changes the list being read.
            var doomed = new List<UIPanel>();
            var panels = uiScreen.panels;
            for (int i = 0; i < panels.Count; i++)
            {
                UIPanel panel = panels[i];
                if (panel != null && panel.transform.IsChildOf(subtree)) doomed.Add(panel);
            }
            foreach (UIPanel panel in doomed) uiScreen.RemovePanel(panel);
            if (doomed.Count > 0) Log.Debug($"Detached {doomed.Count} gamepad panels under '{subtree.name}'; {panels.Count} left");
        }

        // The screen's starting panel, if it was one of them.
        if (screen.uiPanel != null && screen.uiPanel.transform.IsChildOf(subtree)) screen.uiPanel = null;
    }

    /// <summary>
    /// Checks the screen's state and fade group are the clone's own. If they pointed back at vanilla's
    /// screen, opening the window would open vanilla Settings instead, so the window isn't built.
    /// </summary>
    private static bool BelongsToClone(MenuScreen screen, GameObject clone)
    {
        Transform root = clone.transform;
        if (screen.State == null || !screen.State.transform.IsChildOf(root))
        {
            Log.Warning("Settings screen's state isn't part of the screen; window not built");
            return false;
        }
        if (screen.Group == null || !screen.Group.transform.IsChildOf(root))
        {
            Log.Warning("Settings screen's fade group isn't part of the screen; window not built");
            return false;
        }
        if (screen.uiScreen != null && !screen.uiScreen.transform.IsChildOf(root))
            Log.Warning("Settings screen's gamepad screen isn't part of the screen; gamepad focus may be wrong");
        return true;
    }
}