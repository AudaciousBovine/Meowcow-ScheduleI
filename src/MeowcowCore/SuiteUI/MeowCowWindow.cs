using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Il2CppTMPro;
using MeowcowCore.Foundation;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using MenuScreen = Il2CppScheduleOne.UI.MainMenu.MenuScreen;
using SettingsScreen = Il2CppScheduleOne.UI.MainMenu.SettingsScreen;
using UIPanel = Il2CppScheduleOne.UIPanel;
using UIScreen = Il2CppScheduleOne.UIScreen;
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
/// [CHANGED] Slice 2b: vertical tabs and pages, vanilla leftovers removed, the tab strip selected on open.
/// Back button and dragging come in 2c.
/// How the clone stays safe: it's made inside an inactive parent, so no vanilla code runs on it until
/// the settings script has been swapped for a plain <c>MenuScreen</c>.
/// [SOURCE] Vanilla <c>MenuScreen</c>, <c>SettingsScreen</c>, <c>MonoState</c>, <c>UIScreen</c>, <c>UITab</c>;
/// layout metrics from the pre-rebuild window frame (findings doc §3).
/// </remarks>
public sealed class MeowcowWindow
{
    // Layout, in canvas units (1920×1080 reference). [SOURCE] findings doc §3.
    private const float Margin = 20f;        // inner padding
    private const float TitleHeight = 70f;   // title bar (the drag handle in 2c)
    private const float FooterHeight = 50f;  // room for the Back button (2c)
    private const float TabWidth = 170f;
    private const float TabHeight = 34f;
    private const float TabSpacing = 4f;
    private const float BumperRow = 30f;     // above the tabs, for the LB/RB prompts (gamepad only)
    private const float PagePadding = 10f;   // between the content area's edge and a page

    private static readonly MeowcowLogger Log = CoreMod.Log.Tagged("Window");

    /// <summary>One tab and its page.</summary>
    private sealed class Tab
    {
        public Tab(Toggle toggle, UISelectable? selectable, GameObject page, ScrollRect? scroll, UnityAction<bool> onToggle)
        {
            Toggle = toggle;
            Selectable = selectable;
            Page = page;
            Scroll = scroll;
            OnToggle = onToggle;
        }

        public Toggle Toggle { get; }
        public UISelectable? Selectable { get; }
        public GameObject Page { get; }
        public ScrollRect? Scroll { get; }

        /// <summary>Kept referenced: IL2CPP rule for converted delegates.</summary>
        public UnityAction<bool> OnToggle { get; }
    }

    private readonly List<Tab> _tabs = new();
    private readonly UITab _tabStrip;
    private readonly RectTransform _tabsLayout;
    private readonly GameObject _tabTemplate;
    private readonly GameObject _pageTemplate;

    private MeowcowWindow(MenuScreen screen, RectTransform content, TMP_Text? title, UITab tabStrip,
        RectTransform tabsLayout, GameObject tabTemplate, GameObject pageTemplate)
    {
        Screen = screen;
        Content = content;
        Title = title;
        _tabStrip = tabStrip;
        _tabsLayout = tabsLayout;
        _tabTemplate = tabTemplate;
        _pageTemplate = pageTemplate;
    }

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
    /// Adds a tab at the bottom of the list and returns its page's content, where the tab's UI goes
    /// (it scrolls when it outgrows the page). The first tab added is selected.
    /// </summary>
    /// <remarks>[PERFORMANCE] Two clones (tab and page) per tab, once per window build.</remarks>
    public RectTransform AddTab(string label)
    {
        int index = _tabs.Count;

        GameObject tabObject = UObject.Instantiate(_tabTemplate, _tabsLayout);
        tabObject.name = "Tab " + label;
        tabObject.SetActive(true);

        TMP_Text? text = tabObject.transform.Find(UIPathIndex.WindowTabLabel)?.GetComponent<TMP_Text>();
        if (text != null) text.text = label;

        LayoutElement layout = tabObject.GetComponent<LayoutElement>() ?? tabObject.AddComponent<LayoutElement>();
        layout.minHeight = TabHeight;
        layout.preferredHeight = TabHeight;

        // A fresh event: no vanilla wiring, only ours. Converted once per tab and kept with the tab.
        Toggle toggle = tabObject.GetComponent<Toggle>();
        toggle.onValueChanged = new Toggle.ToggleEvent();
        toggle.isOn = false;
        UnityAction<bool> onToggle = DelegateSupport.ConvertDelegate<UnityAction<bool>>(
                new Action<bool>(on => OnTabToggled(index, on)))
            ?? throw new InvalidOperationException("Couldn't convert the tab handler for IL2CPP");
        toggle.onValueChanged.AddListener(onToggle);

        // Into the tab strip's gamepad list, so LB/RB reach it. Vanilla's UITab.Start does the
        // "ignore when navigating" part, but only for tabs present at Start; doing it here covers both.
        UISelectable? selectable = tabObject.GetComponent<UISelectable>();
        if (selectable != null)
        {
            _tabStrip.AddSelectable(selectable);
            selectable.SetIgnoreWhenNavigating(true);
        }

        GameObject page = UObject.Instantiate(_pageTemplate, Content);
        page.name = "Page " + label;
        page.SetActive(false);
        RectTransform pageRect = page.GetComponent<RectTransform>();
        SetRect(pageRect, Vector2.zero, Vector2.one,
            new Vector2(PagePadding, PagePadding), new Vector2(-PagePadding, -PagePadding));
        ScrollRect? scroll = page.GetComponent<ScrollRect>();

        _tabs.Add(new Tab(toggle, selectable, page, scroll, onToggle));
        if (index == 0) toggle.isOn = true; // shows the first page

        return scroll != null && scroll.content != null ? scroll.content : pageRect;
    }

    /// <summary>Selects a tab, as if it were clicked.</summary>
    public void SelectTab(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;
        _tabs[index].Toggle.isOn = true; // the toggle group turns the others off
    }

    /// <summary>A tab's toggle changed. Called from native code: exceptions must never escape.</summary>
    private void OnTabToggled(int index, bool on)
    {
        try
        {
            if (on) ShowTab(index);
        }
        catch (Exception e)
        {
            Log.Error($"Switching to tab {index} failed", e);
        }
    }

    /// <summary>
    /// Shows one tab's page and hides the rest. The selected tab can't be clicked again, as in vanilla.
    /// </summary>
    private void ShowTab(int index)
    {
        SelectedTab = index;
        for (int i = 0; i < _tabs.Count; i++)
        {
            _tabs[i].Toggle.interactable = i != index;
            _tabs[i].Page.SetActive(i == index);
        }

        // The gamepad's scroll stick scrolls the visible page.
        Tab tab = _tabs[index];
        if (tab.Scroll != null && Screen.uiScreen != null) Screen.uiScreen.ChangeActiveScrollRect(tab.Scroll);

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

        // Templates first, while vanilla's pieces still exist.
        GameObject? tabTemplate = TakeTabTemplate(tabsLayout, root);
        GameObject? pageTemplate = TakePageTemplate(content, root);
        if (tabTemplate == null || pageTemplate == null)
        {
            Log.Warning("Settings screen has no tab or scroll view to copy; window not built");
            return null;
        }

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

        LayOut(root, tabsPanel, tabsLayout, content);

        // Tabs now run top to bottom, so LB/RB must cycle vertically. RB moves down the list.
        tabStrip.cycleDirection = UITab.CycleDirection.Vertical;
        tabStrip.reverseCycleDirection = true;

        return new MeowcowWindow(screen, content, titleText, tabStrip, tabsLayout, tabTemplate, pageTemplate);
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
            return tab.gameObject;
        }
        return null;
    }

    /// <summary>
    /// One of vanilla's scroll views (one with a scrollbar if there is one), emptied, moved out of the
    /// content area and switched off, to clone pages from.
    /// </summary>
    private static GameObject? TakePageTemplate(RectTransform content, Transform root)
    {
        ScrollRect? chosen = null;
        foreach (ScrollRect scroll in content.GetComponentsInChildren<ScrollRect>(true))
        {
            chosen ??= scroll;
            if (scroll.verticalScrollbar != null)
            {
                chosen = scroll;
                break;
            }
        }
        if (chosen == null) return null;

        // Vanilla's settings rows go; the scroll view keeps its look, viewport and scrollbar.
        RectTransform? rows = chosen.content;
        if (rows != null)
        {
            for (int i = rows.childCount - 1; i >= 0; i--) UObject.DestroyImmediate(rows.GetChild(i).gameObject);
        }

        GameObject template = chosen.gameObject;
        template.transform.SetParent(root, false);
        template.SetActive(false);
        template.name = "PageTemplate";
        return template;
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
    /// for the title above and the footer below. The window keeps vanilla's size for now.
    /// </summary>
    private static void LayOut(Transform root, RectTransform tabsPanel, RectTransform tabsLayout, RectTransform content)
    {
        float top = TitleHeight + BumperRow;
        SetRect(tabsPanel, new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(Margin, FooterHeight), new Vector2(Margin + TabWidth, -top));

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

        // LB and RB prompts side by side above the tabs: LB at the column's left, RB at its right.
        PlaceBumper(root, UIPathIndex.WindowLeftBumper, new Vector2(0f, 1f), Margin);
        PlaceBumper(root, UIPathIndex.WindowRightBumper, new Vector2(1f, 1f), Margin + TabWidth);
    }

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

    /// <summary>Sets anchors and edge offsets in one call.</summary>
    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
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