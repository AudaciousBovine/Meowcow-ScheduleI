using System;
using System.Collections.Generic;
using MeowcowCore.Foundation;
using UnityEngine;
using UnityEngine.UI;
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
    public WindowPage AddPage(string label)
    {
        WindowPage page = WindowPage.Create(_window, View.transform, label);
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

    internal void SetVisible(bool visible) => View.SetActive(visible);

    /// <summary>Called when the tab is shown or the window opens: refreshes scrolling and the strip's focus.</summary>
    internal void OnShown()
    {
        if (SelectedPage >= 0) ShowPage(SelectedPage);
    }

    private void ShowPage(int index)
    {
        SelectedPage = index;
        for (int i = 0; i < _subtabs.Count; i++)
        {
            _subtabs[i].Page.Root.SetActive(i == index);
            if (_subtabs[i].Toggle != null) _subtabs[i].Toggle!.interactable = i != index;
        }

        WindowPage page = _subtabs[index].Page;
        if (page.Scroll != null) _window.Screen.uiScreen?.ChangeActiveScrollRect(page.Scroll);

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
            subtab.Page.Label, SubtabWidth, on =>
            {
                if (on) ShowPage(index);
            });
        subtab.Toggle = toggle;
        subtab.Selectable = selectable;
        if (index == SelectedPage) toggle.isOn = true;
    }

    /// <summary>Pages fill the tab, below the strip when there is one.</summary>
    private void LayOutPages()
    {
        float top = PagePadding + (_strip != null ? StripHeight + PagePadding : 0f);
        foreach (Subtab subtab in _subtabs)
        {
            MeowcowWindow.SetRect(subtab.Page.Root.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
                new Vector2(PagePadding, PagePadding), new Vector2(-PagePadding, -top));
        }
    }
}