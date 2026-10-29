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

    // [CHANGED] Two-column rows: every label sits in one column, right-aligned and wrapping onto more lines
    // when it's longer than the column; every control starts the same gap to its right. Measured from the
    // page's left edge, the same for every kind of row. Tune here.
    private const float LabelIndent = 10f;        // row's left edge → label column ([CHANGED] was 20)
    private const float LabelColumnWidth = 160f;  // [CHANGED] was 200, then 260; longer labels wrap
    private const float ColumnGap = 24f;          // label column → controls
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
        AlignRow(row.transform);
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
        AlignRow(row.transform);
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
        AlignRow(row.transform);
        row.SetActive(true);
        return new ChoiceRow(dropdown);
    }

    /// <summary>
    /// Moves an existing row (e.g. one of vanilla's, with its own setting script) to the end of this page,
    /// and registers its controls with the page's gamepad panel. Only the controls in
    /// <paramref name="gamepadControls"/> that sit inside the row are registered, so a row keeps exactly
    /// the gamepad reach it had before.
    /// </summary>
    /// <param name="label">A new label for the row, or null to keep its own.</param>
    /// <remarks>
    /// [CHANGED] New for the vanilla Settings rework: vanilla's rows are moved, not copied, so their own
    /// scripts keep applying vanilla's settings.
    /// [PERFORMANCE] One reparent, and a pass over the given controls.
    /// </remarks>
    public void AddExistingRow(Transform row, IReadOnlyList<UISelectable> gamepadControls, string? label = null)
    {
        row.SetParent(Rows, false);
        if (label != null) SetRowLabel(row, label);
        AlignRow(row);

        foreach (UISelectable control in gamepadControls)
        {
            if (control != null && control.transform.IsChildOf(row)) Panel.AddSelectable(control);
        }
    }

    /// <summary>
    /// Renames a row: through its UIOption (on/off and slider rows label themselves from it), or its name
    /// label (dropdown rows).
    /// </summary>
    private static void SetRowLabel(Transform row, string label)
    {
        Il2CppScheduleOne.UIOption? option = row.GetComponent<Il2CppScheduleOne.UIOption>();
        if (option != null)
        {
            SetOptionLabel(option, label);
            return;
        }

        TMP_Text? name = row.Find(UIPathIndex.RowOptionName)?.GetComponent<TMP_Text>();
        if (name != null) name.text = label;
        else Log.Warning($"Row '{row.name}' has no label to rename");
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

    // ---------- Two-column alignment ----------

    /// <summary>
    /// Puts a row's label in the label column (right-aligned, wrapping) and its controls just right of it.
    /// Everything is measured from the row's left edge, so every kind of row lands on the same column (the
    /// page lines its rows up on their left edges, after this runs): rows
    /// with positioned children, and rows whose layout group placed them (that layout is switched off, so
    /// only one rule positions anything). Rows without a label/control pair (headers, the mouse-icon row,
    /// plain buttons) are left as they are.
    /// </summary>
    /// <remarks>
    /// [CHANGED] One method for every row (was: separate handling for layout rows, which landed about 25
    /// units off the positioned ones). Applied to every row, ours and vanilla's.
    /// [PERFORMANCE] A few rect reads and writes per row, once per window build.
    /// </remarks>
    private void AlignRow(Transform row)
    {
        TMP_Text? label = FindRowLabel(row);
        Transform? holder = label != null ? label.transform.parent : null;
        if (label == null || holder == null) return;

        // The label's siblings are positioned by us from here; a layout group would undo it.
        HorizontalLayoutGroup? layout = holder.GetComponent<HorizontalLayoutGroup>();
        if (layout != null) layout.enabled = false;

        // Controls hold a control (slider, toggle button, dropdown...); text without one is a note under the
        // label; anything else (backgrounds, navigation markers) stays put.
        var controls = new List<RectTransform>();
        var notes = new List<RectTransform>();
        for (int i = 0; i < holder.childCount; i++)
        {
            RectTransform? child = holder.GetChild(i).TryCast<RectTransform>();
            if (child == null || child == label.rectTransform) continue;

            if (child.GetComponentsInChildren<Selectable>(true).Length > 0) controls.Add(child);
            else if (child.gameObject.activeSelf && child.GetComponent<TMP_Text>() != null) notes.Add(child);
        }

        Align(row, label, controls, notes);
    }

    /// <summary>
    /// Lines up a row whose parts Core picks itself, e.g. a mouse-button row split out of vanilla's mouse
    /// info: <paramref name="label"/> goes in the label column, <paramref name="controls"/> start at the
    /// control column. Same rule as every other row.
    /// </summary>
    /// <remarks>[CHANGED] New for the Keybinds touch-ups.</remarks>
    public void AlignCustomRow(Transform row, TMP_Text label, IReadOnlyList<RectTransform> controls) =>
        Align(row, label, controls, Array.Empty<RectTransform>());

    /// <summary>
    /// Moves a rect so its left edge is at the label column's left edge (e.g. a button that replaces a
    /// section header).
    /// </summary>
    /// <remarks>[CHANGED] New for the Keybinds touch-ups.</remarks>
    public static void PlaceAtLabelColumnStart(Transform row, RectTransform rect)
    {
        float columnLeft = row.TryCast<RectTransform>()!.rect.xMin + LabelIndent;
        AnchorLeftKeepPlace(rect);
        rect.anchoredPosition += new Vector2(columnLeft - RowLeftEdge(row, rect), 0f);
    }

    /// <summary>The shared rule: label (and notes) into the column, controls to the control column.</summary>
    private static void Align(Transform row, TMP_Text label, IReadOnlyList<RectTransform> controls,
        IReadOnlyList<RectTransform> notes)
    {
        // In the row's own space, from its left edge (its pivot is its centre, and the page only positions
        // the row afterwards, so page-space positions now wouldn't hold).
        float columnLeft = row.TryCast<RectTransform>()!.rect.xMin + LabelIndent;
        float controlsLeft = columnLeft + LabelColumnWidth + ColumnGap;

        foreach (RectTransform note in notes) PlaceInLabelColumn(row, note, note.GetComponent<TMP_Text>(), columnLeft, isNote: true);
        PlaceInLabelColumn(row, label.rectTransform, label, columnLeft);

        float firstControl = float.MaxValue;
        foreach (RectTransform control in controls)
        {
            if (control.gameObject.activeSelf) firstControl = Mathf.Min(firstControl, ControlStart(row, control));
        }
        if (firstControl == float.MaxValue) return;

        float shift = controlsLeft - firstControl;
        foreach (RectTransform control in controls)
        {
            // Pinned to the holder's left first: vanilla anchors some controls to the right (or stretches
            // them), and the row only takes its final width when the page lays out, which would move them.
            AnchorLeftKeepPlace(control);
            control.anchoredPosition += new Vector2(shift, 0f);
        }
    }

    /// <summary>
    /// Where a control starts, by what the player reads as its edge: a dropdown's box, a slider's track, a
    /// toggle's circle; anything else by its leftmost visible graphic.
    /// </summary>
    /// <remarks>
    /// [CHANGED] Per kind of control (was: leftmost visible graphic for all). Dropdowns sat a little left of
    /// sliders and toggles, whose leftmost graphics (the fill, an outline) start before what reads as their
    /// edge.
    /// </remarks>
    private static float ControlStart(Transform row, RectTransform control)
    {
        var dropdowns = control.GetComponentsInChildren<TMP_Dropdown>(true);
        if (dropdowns.Length > 0) return RowLeftEdge(row, dropdowns[0].GetComponent<RectTransform>());

        var sliders = control.GetComponentsInChildren<Slider>(true);
        if (sliders.Length > 0)
        {
            RectTransform? track = sliders[0].transform.Find(UIPathIndex.SliderTrack)?.TryCast<RectTransform>();
            return RowLeftEdge(row, track ?? sliders[0].GetComponent<RectTransform>());
        }

        RectTransform? circle = FindDescendant(control, UIPathIndex.ToggleCircle)?.TryCast<RectTransform>();
        if (circle != null) return RowLeftEdge(row, circle);

        return VisibleLeftEdge(row, control);
    }

    private static Transform? FindDescendant(Transform root, string name)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == name) return child;
            Transform? deeper = FindDescendant(child, name);
            if (deeper != null) return deeper;
        }
        return null;
    }

    /// <summary>
    /// A row's label: its UIOption's (on/off and slider rows), a choice row's name text, a binding row's
    /// "Label", or a gamepad row's "Container/Label". Null if it has none.
    /// </summary>
    private static TMP_Text? FindRowLabel(Transform row)
    {
        Il2CppScheduleOne.UIOption? option = row.GetComponent<Il2CppScheduleOne.UIOption>();
        if (option != null && option.nameText != null) return option.nameText;

        TMP_Text? name = row.Find(UIPathIndex.RowOptionName)?.GetComponent<TMP_Text>();
        if (name != null) return name;

        return row.Find(UIPathIndex.RowLabel)?.GetComponent<TMP_Text>()
            ?? row.Find(UIPathIndex.RowContainer + "/" + UIPathIndex.RowLabel)?.GetComponent<TMP_Text>();
    }

    /// <summary>
    /// The label column: left edge at <paramref name="columnLeft"/> (row space), the column's width, text
    /// right-aligned and wrapping, centred on its line. Height untouched.
    /// </summary>
    /// <param name="isNote">
    /// A note under the label keeps its own vertical alignment ([CHANGED] centring it moved Auto Backup's
    /// note up over its label once it wrapped onto more lines).
    /// </param>
    private static void PlaceInLabelColumn(Transform row, RectTransform rect, TMP_Text text, float columnLeft,
        bool isNote = false)
    {
        Vector2 anchorMin = rect.anchorMin;
        Vector2 anchorMax = rect.anchorMax;
        Vector2 pivot = rect.pivot;
        anchorMin.x = 0f;
        anchorMax.x = 0f;
        pivot.x = 0f;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = new Vector2(LabelColumnWidth, rect.sizeDelta.y);
        rect.anchoredPosition = new Vector2(0f, rect.anchoredPosition.y);

        // Now at its holder's left edge; move it to the column's (measured in the row, like everything else).
        rect.anchoredPosition += new Vector2(columnLeft - RowLeftEdge(row, rect), 0f);

        text.horizontalAlignment = HorizontalAlignmentOptions.Right;
        if (!isNote) text.verticalAlignment = VerticalAlignmentOptions.Middle;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;

    }

    /// <summary>A rect's left edge in the row's space.</summary>
    private static float RowLeftEdge(Transform row, RectTransform rect) =>
        row.InverseTransformPoint(rect.TransformPoint(new Vector3(rect.rect.xMin, 0f))).x;

    /// <summary>
    /// Where a control visibly starts, in the row's space: the left edge of its leftmost shown graphic
    /// (slider track, toggle circle, dropdown box). Transparent graphics are click areas, not what the
    /// player sees, so they don't count.
    /// </summary>
    private static float VisibleLeftEdge(Transform row, RectTransform control)
    {
        float left = float.MaxValue;
        foreach (Graphic graphic in control.GetComponentsInChildren<Graphic>(true))
        {
            if (!graphic.enabled || graphic.color.a <= 0.01f || !IsShownWithin(graphic.transform, control)) continue;
            left = Mathf.Min(left, RowLeftEdge(row, graphic.rectTransform));
        }
        return left == float.MaxValue ? RowLeftEdge(row, control) : left;
    }

    /// <summary>
    /// Whether an object is switched on, along with every parent up to <paramref name="root"/>. (Not
    /// activeInHierarchy: the page may still be hidden while it's being built.)
    /// </summary>
    private static bool IsShownWithin(Transform t, Transform root)
    {
        for (Transform? current = t; current != null; current = current.parent)
        {
            if (!current.gameObject.activeSelf) return false;
            if (current == root) return true;
        }
        return true;
    }

    /// <summary>
    /// Anchors a rect horizontally to its parent's left edge without moving or resizing it now, so later
    /// changes to the parent's width don't move it. Vertical anchoring is untouched.
    /// </summary>
    private static void AnchorLeftKeepPlace(RectTransform rect)
    {
        Vector3 position = rect.position;
        float width = rect.rect.width;

        Vector2 anchorMin = rect.anchorMin;
        Vector2 anchorMax = rect.anchorMax;
        anchorMin.x = 0f;
        anchorMax.x = 0f;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
        rect.position = position;
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