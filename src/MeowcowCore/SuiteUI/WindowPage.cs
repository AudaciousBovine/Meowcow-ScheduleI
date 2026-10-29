using System;
using System.Collections.Generic;
using Il2CppTMPro;
using MeowcowCore.Foundation;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UIContentPanel = Il2CppScheduleOne.UIContentPanel;
using UISelectable = Il2CppScheduleOne.UISelectable;
using UISlider = Il2CppScheduleOne.UISlider;
using UIToggle = Il2CppScheduleOne.UIToggle;
using UObject = UnityEngine.Object;

namespace MeowcowCore.SuiteUI;

/// <summary>
/// One scrolling page of a window tab (one subtab), holding rows. Built from a clone of a vanilla Settings
/// page, so it's also a gamepad panel: the gamepad moves between rows and the page scrolls to follow.
/// </summary>
/// <remarks>
/// [CHANGED] New. Rows are clones of vanilla's own rows with their setting scripts removed; vanilla's
/// generic controls (UIToggle, UISlider, TMP_Dropdown) stay and do the work, gamepad included.
/// [SOURCE] Vanilla <c>UIOption</c>, <c>UIToggle</c>, <c>UISlider</c>, <c>UIPanel</c>, <c>UIContentPanel</c>.
/// [PERFORMANCE] One clone per row, once per window build. Vanilla's controls check "am I selected?" each
/// frame while their page is shown; that's the game's own cost per row.
/// </remarks>
public sealed class WindowPage
{
    private const float TextHeight = 60f;
    private const float TextWidth = 600f;

    private static readonly MeowcowLogger Log = CoreMod.Log.Tagged("Window");

    private readonly MeowcowWindow _window;

    private WindowPage(MeowcowWindow window, string label, GameObject root, UIContentPanel panel, ScrollRect? scroll, RectTransform rows)
    {
        _window = window;
        Label = label;
        Root = root;
        Panel = panel;
        Scroll = scroll;
        Rows = rows;
    }

    public string Label { get; }

    /// <summary>False once the window is gone.</summary>
    public bool IsAlive => Root != null;

    internal GameObject Root { get; }
    internal UIContentPanel Panel { get; }
    internal ScrollRect? Scroll { get; }

    /// <summary>Where rows go: the scroll view's content (a vertical layout that grows with its rows).</summary>
    internal RectTransform Rows { get; }

    /// <summary>A new, hidden page under <paramref name="parent"/>, registered as a gamepad panel.</summary>
    internal static WindowPage Create(MeowcowWindow window, Transform parent, string label)
    {
        GameObject root = UObject.Instantiate(window.Templates.Page, parent); // inactive, like its template
        root.name = "Page " + label;

        UIContentPanel panel = root.GetComponent<UIContentPanel>();
        var scrolls = root.GetComponentsInChildren<ScrollRect>(true);
        ScrollRect? scroll = scrolls.Length > 0 ? scrolls[0] : null;
        RectTransform rows = scroll != null && scroll.content != null ? scroll.content : root.GetComponent<RectTransform>();

        // A gamepad panel of the window's screen: the screen picks the visible page when it opens.
        window.Screen.uiScreen?.AddPanel(panel);
        return new WindowPage(window, label, root, panel, scroll, rows);
    }

    // ---------- Rows ----------

    /// <summary>An on/off row. <paramref name="onChanged"/> runs when the player flips it.</summary>
    public ToggleRow? AddToggle(string label, bool value, Action<bool> onChanged)
    {
        GameObject? row = NewRow(_window.Templates.ToggleRow, label, "on/off");
        if (row == null) return null;

        UIToggle toggle = row.GetComponent<UIToggle>();
        SetOptionLabel(toggle, label);
        toggle.SetStateWithoutNotify(value);

        // A fresh event: the clone's saved listeners pointed at the removed setting script.
        toggle.OnChanged = new UnityEvent<bool>();
        toggle.OnChanged.AddListener(_window.Keep(UIBuilder.ToIl2Cpp<UnityAction<bool>>(
            new Action<bool>(v => UIBuilder.RunSafely(() => onChanged(v), $"'{label}' change")))));

        Register(toggle.selectable);
        row.SetActive(true);
        return new ToggleRow(toggle);
    }

    /// <summary>
    /// A slider row from <paramref name="min"/> to <paramref name="max"/>. <paramref name="step"/> is one
    /// gamepad left/right press. <paramref name="onChanged"/> runs while dragging and on each press.
    /// </summary>
    public SliderRow? AddSlider(string label, float min, float max, float step, bool wholeNumbers, float value,
        Action<float> onChanged)
    {
        GameObject? row = NewRow(_window.Templates.SliderRow, label, "slider");
        if (row == null) return null;

        UISlider option = row.GetComponent<UISlider>();
        SetOptionLabel(option, label);
        Slider slider = option.slider;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = wholeNumbers;
        slider.onValueChanged = new Slider.SliderEvent(); // vanilla's UISlider re-adds its own listener on Awake
        slider.SetValueWithoutNotify(value);
        option.stepSize = step;
        TMP_Text? valueText = option.valueText;
        if (valueText != null) valueText.text = SliderRow.Format(value);

        option.OnChanged = new UnityEvent<float>();
        option.OnChanged.AddListener(_window.Keep(UIBuilder.ToIl2Cpp<UnityAction<float>>(
            new Action<float>(v => UIBuilder.RunSafely(() => onChanged(v), $"'{label}' change")))));

        Register(option.selectable);
        row.SetActive(true);
        return new SliderRow(slider, valueText);
    }

    /// <summary>A choice row (dropdown). <paramref name="onChanged"/> gets the chosen index.</summary>
    public ChoiceRow? AddChoice(string label, IReadOnlyList<string> choices, int index, Action<int> onChanged)
    {
        GameObject? row = NewRow(_window.Templates.ChoiceRow, label, "choice");
        if (row == null) return null;

        TMP_Text? name = row.transform.Find(UIPathIndex.RowOptionName)?.GetComponent<TMP_Text>();
        if (name != null) name.text = label;

        Transform? dropdownObject = row.transform.Find(UIPathIndex.RowDropdown);
        TMP_Dropdown? dropdown = dropdownObject != null ? dropdownObject.GetComponent<TMP_Dropdown>() : null;
        if (dropdown == null)
        {
            Log.Warning($"Choice row '{label}' has no dropdown; skipped");
            UObject.Destroy(row);
            return null;
        }

        var options = new Il2CppSystem.Collections.Generic.List<string>();
        foreach (string choice in choices) options.Add(choice);
        dropdown.ClearOptions();
        dropdown.AddOptions(options);
        dropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
        dropdown.SetValueWithoutNotify(index);
        dropdown.onValueChanged.AddListener(_window.Keep(UIBuilder.ToIl2Cpp<UnityAction<int>>(
            new Action<int>(i => UIBuilder.RunSafely(() => onChanged(i), $"'{label}' change")))));

        Register(dropdownObject!.GetComponent<UISelectable>());
        row.SetActive(true);
        return new ChoiceRow(dropdown);
    }

    /// <summary>A line of text in the window's title font, e.g. "No settings yet".</summary>
    public void AddText(string text)
    {
        if (_window.Title == null) return;
        TMP_Text line = UIBuilder.CloneText(_window.Title, "Text", text);
        line.transform.SetParent(Rows, false);
        line.raycastTarget = false;

        // [CHANGED] The title stretches across the window's top; a copy keeps those anchors, which put it
        // partly outside the page (the first letters were cut off). Reset to a plain top-left box that the
        // page's layout positions like a row, with the text starting where row labels start.
        RectTransform rect = line.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(TextWidth, TextHeight);
        line.horizontalAlignment = HorizontalAlignmentOptions.Left;
        line.verticalAlignment = VerticalAlignmentOptions.Middle;
        LayoutElement layout = line.gameObject.GetComponent<LayoutElement>() ?? line.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = TextHeight;
        layout.preferredHeight = TextHeight;
        layout.preferredWidth = TextWidth;
    }

    // ---------- Internals ----------

    /// <summary>
    /// A hidden clone of a row template at the end of the page. Configured while hidden, so vanilla's
    /// Awake runs once on the finished row; the caller switches it on.
    /// </summary>
    private GameObject? NewRow(GameObject? template, string label, string kind)
    {
        if (template == null)
        {
            Log.Warning($"No vanilla {kind} row to copy; '{label}' not shown");
            return null;
        }

        GameObject row = UObject.Instantiate(template, Rows);
        row.name = label;
        return row;
    }

    /// <summary>
    /// Sets an on/off or slider row's label. UIOption's Awake copies <c>optionName</c> into the label, so
    /// both are set (Awake hasn't run yet on a hidden row).
    /// </summary>
    private static void SetOptionLabel(Il2CppScheduleOne.UIOption option, string label)
    {
        option.optionName = label;
        if (option.nameText != null) option.nameText.text = label;
    }

    /// <summary>Adds a row's control to the page's gamepad list.</summary>
    private void Register(UISelectable? selectable)
    {
        if (selectable == null) return;
        Panel.AddSelectable(selectable);
    }
}