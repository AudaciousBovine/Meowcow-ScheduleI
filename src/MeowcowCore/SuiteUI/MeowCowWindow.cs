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
/// Back button, corner prompt hidden while open, dragging by the title (double-click recentres).
/// Everything is event-driven: nothing runs per frame.
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
    private const float BackWidth = 120f;
    private const float BackHeight = 30f;
    private const string BackLabel = "<color=#9A9A9A>Esc</color>   Back";

    // [CHANGED] The larger frame from the pre-rebuild window, capped to the screen.
    private static readonly Vector2 DefaultSize = new(1100f, 700f);

    // Where each window was last dragged, by object name. For this run only: saving it between runs
    // needs SuiteSettings.
    private static readonly Dictionary<string, Vector2> RememberedPositions = new();

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

    // Event handlers, kept referenced (IL2CPP rule for converted delegates).
    private readonly List<Il2CppSystem.Object> _handlers = new();

    private RectTransform? _root;
    private float _canvasScale = 1f;
    private GameObject? _cornerPrompt;
    private bool _cornerPromptWasActive;

    // [CHANGED] Our own double-click tracking (see OnTitleClick).
    private const float DoubleClickTime = 0.25f;     // seconds between the two clicks
    private const float DoubleClickDistance = 10f;   // screen pixels between the two clicks
    private float _lastClickTime = -1f;
    private Vector2 _lastClickPosition;
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

        LayOut(root, tabsPanel, tabsLayout, content, titleText);

        // Tabs now run top to bottom, so LB/RB must cycle vertically. RB moves down the list.
        tabStrip.cycleDirection = UITab.CycleDirection.Vertical;
        tabStrip.reverseCycleDirection = true;

        var window = new MeowcowWindow(screen, content, titleText, tabStrip, tabsLayout, tabTemplate, pageTemplate);
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
    /// Follows the screen's own open and close (whatever caused them, Escape included) to hide vanilla's
    /// corner "Esc Back" prompt while the window is open, so there aren't two.
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
            // Looked up on each open: the prompt belongs to the menu, which is rebuilt per scene.
            _cornerPrompt = GameObject.Find(UIPathIndex.MenuCornerPrompt);
            _cornerPromptWasActive = _cornerPrompt != null && _cornerPrompt.activeSelf;
            if (_cornerPromptWasActive) _cornerPrompt!.SetActive(false);
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
            // Only restored if it was showing before, so a prompt hidden by vanilla stays hidden.
            if (_cornerPromptWasActive && _cornerPrompt != null) _cornerPrompt.SetActive(true);
            _cornerPromptWasActive = false;
        }
        catch (Exception e)
        {
            Log.Error("Window close handling failed", e);
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