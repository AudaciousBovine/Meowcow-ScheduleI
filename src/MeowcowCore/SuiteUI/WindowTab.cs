using System;
using System.Collections.Generic;
using MeowcowCore.Foundation;
using UnityEngine;
using UnityEngine.UI;
using UIPanel = Il2CppScheduleOne.UIPanel;
using UISelectable = Il2CppScheduleOne.UISelectable;
using UITab = Il2CppScheduleOne.UITab;
using UObject = UnityEngine.Object;

namespace MeowcowCore.SuiteUI;

/// <summary>
/// One tab down the window's left side, holding one or more pages. With two or more pages, a subtab strip
/// runs along the top; with one, the page fills the tab.
/// </summary>
/// <remarks>
/// [CHANGED] New. Subtabs cycle with vanilla's second tab input (<c>Secondary</c>), so LB/RB keep switching
/// tabs. The strip is a clone of vanilla's horizontal tab strip, taken before the main strip turned vertical.
/// [SOURCE] Vanilla <c>UITab</c> (input types, direction-based cycling).
/// [CHANGED] Only the page showing is a gamepad panel of the window's screen; hidden pages are taken off it.
/// Vanilla's content panels pick navigation targets from every panel on the screen
/// (<c>UIContentPanel.Navigate</c> → <c>CustomUIUtils.GetSelectables</c>), so at the end of a list the
/// gamepad jumped into a hidden page (log: Page Main → Page Misc, visible False) and selection vanished.
/// [SOURCE] Vanilla <c>UIScreen.AddPanel</c>/<c>RemovePanel</c>/<c>SetCurrentSelectedPanel</c>.
/// </remarks>
public sealed class WindowTab
{
    private const float PagePadding = 10f;
    private const float StripHeight = 34f;    // same height as a tab
    private const float SubtabWidth = 150f;
    private const float SubtabSpacing = 4f;

    private static readonly MeowcowLogger Log = CoreMod.Log.Tagged("Window");

    private sealed class Subtab
    {
        public Subtab(WindowPage page) => Page = page;
        public WindowPage Page { get; }
        public Toggle? Toggle { get; set; }
        public UISelectable? Selectable { get; set; }
    }

    private readonly MeowcowWindow _window;
    private readonly List<Subtab> _subtabs = new();
    private UITab? _strip;
    private Transform? _stripLayout;
    private ToggleGroup? _stripGroup;

    internal WindowTab(MeowcowWindow window, string label, GameObject view, Toggle toggle, UISelectable? selectable)
    {
        _window = window;
        Label = label;
        View = view;
        Toggle = toggle;
        Selectable = selectable;
    }

    public string Label { get; }

    /// <summary>The selected page's index, or -1 when there are none.</summary>
    public int SelectedPage { get; private set; } = -1;

    /// <summary>Everything this tab shows, inside the window's content area.</summary>
    internal GameObject View { get; }
    internal Toggle Toggle { get; }
    internal UISelectable? Selectable { get; }

    /// <summary>
    /// Adds a page (a subtab named <paramref name="label"/>) and returns it to fill with rows. The first page
    /// is selected; the subtab strip appears when the second is added.
    /// </summary>
    /// <remarks>[PERFORMANCE] One page clone; the strip is built once, when it's first needed.</remarks>
    public WindowPage AddPage(string label) => AddPageInternal(WindowPage.Create(_window, View.transform, label));

    /// <summary>
    /// Adds an existing page (one of vanilla's, with its own scripts) as a subtab, instead of a new one: the
    /// page moves here whole, so its scripts run whenever it's shown, and its rows are aligned like any other.
    /// </summary>
    /// <remarks>
    /// [CHANGED] New for General › Current Game: vanilla's Game page is driven by a script on the page itself,
    /// which only runs while the page is active, so the page moves rather than its rows.
    /// </remarks>
    /// <param name="describe">[CHANGED] A row's description by its name, or null for none.</param>
    public WindowPage AddExistingPage(string label, GameObject pageRoot, Func<string, string?>? describe = null) =>
        AddPageInternal(WindowPage.Adopt(_window, View.transform, label, pageRoot, describe));

    private WindowPage AddPageInternal(WindowPage page)
    {
        var subtab = new Subtab(page);
        _subtabs.Add(subtab);

        if (_subtabs.Count == 2) BuildStrip();          // adds buttons for both pages
        else if (_strip != null) AddButton(subtab);

        LayOutPages();
        if (_subtabs.Count == 1) ShowPage(0);
        return page;
    }

    /// <summary>Selects a page, as if its subtab were clicked.</summary>
    public void SelectPage(int index)
    {
        if (index < 0 || index >= _subtabs.Count) return;
        Toggle? toggle = _subtabs[index].Toggle;
        if (toggle != null) toggle.isOn = true; // the toggle group turns the others off; the handler shows it
        else ShowPage(index);
    }

    internal void SetVisible(bool visible)
    {
        View.SetActive(visible);
        if (visible) return; // OnShown registers the showing page
        foreach (Subtab subtab in _subtabs)
        {
            subtab.Page.ClearDescription(); // [CHANGED] no stale description on return
            Unregister(subtab.Page); // [CHANGED] a hidden tab's pages leave the screen
        }
    }

    /// <summary>Called when the tab is shown or the window opens: refreshes scrolling and the strip's focus.</summary>
    internal void OnShown()
    {
        if (SelectedPage >= 0) ShowPage(SelectedPage);
    }

    private void ShowPage(int index)
    {
        SelectedPage = index;

        // [CHANGED] The new page is shown and registered before the others leave, so when the screen loses
        // its selected panel (a page being hidden) its fallback lands on this one.
        WindowPage shown = _subtabs[index].Page;
        shown.Root.SetActive(true);
        _window.Screen.uiScreen?.AddPanel(shown.Panel);

        for (int i = 0; i < _subtabs.Count; i++)
        {
            if (i != index)
            {
                _subtabs[i].Page.Root.SetActive(false);
                _subtabs[i].Page.ClearDescription(); // [CHANGED] no stale description on return
                Unregister(_subtabs[i].Page);
            }
            if (_subtabs[i].Toggle != null)
            {
                _subtabs[i].Toggle!.interactable = i != index;
                MeowcowWindow.SetSubtabSelected(_subtabs[i].Toggle!, i == index); // [CHANGED] reversed shading
            }
        }

        WindowPage page = shown;
        if (page.Scroll != null) _window.Screen.uiScreen?.ChangeActiveScrollRect(page.Scroll);
        FocusIfLost(page);

        // The strip needs a selected subtab before its input cycles (as with the main strip).
        if (_strip != null && _subtabs[index].Selectable != null)
        {
            try
            {
                _strip.Select(_subtabs[index].Selectable, false);
            }
            catch (Exception e)
            {
                Log.Warning($"Couldn't select the subtab strip; subtabs may not cycle by gamepad: {e.Message}");
            }
        }
    }

    /// <summary>Takes a page off the window's screen (no-op if it isn't on it). [CHANGED] New.</summary>
    /// <remarks>
    /// If it was the selected panel, vanilla's RemovePanel falls back to the best visible panel itself.
    /// [PERFORMANCE] A list search and removal; only on page and tab switches.
    /// </remarks>
    private void Unregister(WindowPage page)
    {
        var screen = _window.Screen.uiScreen;
        if (screen != null && page.Panel != null) screen.RemovePanel(page.Panel);
    }

    /// <summary>
    /// Selects the shown page's panel if the screen has nothing usable selected (e.g. the previous tab's page
    /// was hidden before this one was registered). Anything else validly selected is left alone.
    /// </summary>
    /// <remarks>[CHANGED] New. Only while the screen is selected (open and taking input).</remarks>
    private void FocusIfLost(WindowPage page)
    {
        var screen = _window.Screen.uiScreen;
        if (screen == null || !screen.IsSelected) return;
        UIPanel? current = screen.CurrentSelectedPanel;
        if (current == null || !current.IsPanelVisible() || !screen.panels.Contains(current))
        {
            try
            {
                screen.SetCurrentSelectedPanel(page.Panel, null, true, false);
            }
            catch (Exception e)
            {
                Log.Warning($"Couldn't select page '{page.Label}' for gamepad: {e.Message}");
            }
        }
    }

    /// <summary>
    /// The subtab strip: a clone of vanilla's horizontal tab strip, set to vanilla's second tab input so it
    /// doesn't fight LB/RB, with buttons for the pages so far.
    /// </summary>
    private void BuildStrip()
    {
        GameObject strip = UObject.Instantiate(_window.Templates.Strip, View.transform); // inactive, like its template
        strip.name = "SubtabStrip";
        RectTransform rect = strip.GetComponent<RectTransform>();
        MeowcowWindow.SetRect(rect, new Vector2(0f, 1f), Vector2.one,
            new Vector2(PagePadding, -(PagePadding + StripHeight)), new Vector2(-PagePadding, -PagePadding));

        _strip = strip.GetComponent<UITab>();

        // [CHANGED] No strip background: the selected subtab must sit on what the page sits on.
        Image? stripBackground = strip.GetComponent<Image>();
        if (stripBackground != null) stripBackground.color = Color.clear;
        _strip.cycleInputActionType = UITab.CycleInputActionType.Secondary;
        _strip.cycleDirection = UITab.CycleDirection.Horizontal;
        _strip.reverseCycleDirection = false;
        // The template still points at the window's LB/RB prompts; the strip mustn't switch those.
        _strip.cycleLeftVisual = null;
        _strip.cycleRightVisual = null;

        _stripLayout = strip.transform.Find(UIPathIndex.TabStripLayout);
        if (_stripLayout != null)
        {
            _stripGroup = _stripLayout.GetComponent<ToggleGroup>();
            HorizontalLayoutGroup? layout = _stripLayout.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.spacing = SubtabSpacing;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = true;
            }
        }

        strip.SetActive(true);
        foreach (Subtab subtab in _subtabs) AddButton(subtab);
    }

    private void AddButton(Subtab subtab)
    {
        if (_strip == null || _stripLayout == null) return;
        int index = _subtabs.IndexOf(subtab);
        (Toggle toggle, UISelectable? selectable) = _window.CreateTabButton(_stripLayout, _strip, _stripGroup,
            subtab.Page.Label, SubtabWidth, sideTab: false, on =>
            {
                if (on) ShowPage(index);
            });
        subtab.Toggle = toggle;
        subtab.Selectable = selectable;
        if (index == SelectedPage) toggle.isOn = true;
    }

    /// <summary>
    /// Pages fill the tab, below the strip when there is one. [CHANGED] Flush under the strip (no gap), so
    /// the selected subtab joins its page.
    /// </summary>
    private void LayOutPages()
    {
        float top = PagePadding + (_strip != null ? StripHeight : 0f);
        foreach (Subtab subtab in _subtabs)
        {
            MeowcowWindow.SetRect(subtab.Page.Root.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
                new Vector2(PagePadding, PagePadding), new Vector2(-PagePadding, -top));
        }
    }
}