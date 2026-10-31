using Il2CppTMPro;
using UnityEngine.UI;
using UIToggle = Il2CppScheduleOne.UIToggle;

namespace MeowcowCore.SuiteUI;

// [CHANGED] New. Handles to rows on a WindowPage, so their shown value can follow a change made elsewhere
// (e.g. a module setting a value from code). Setting a row's value never fires its change handler.

/// <summary>An on/off row (vanilla's On/Off control).</summary>
public sealed class ToggleRow
{
    private readonly UIToggle _toggle;

    internal ToggleRow(UIToggle toggle) => _toggle = toggle;

    /// <summary>False once the window is gone.</summary>
    public bool IsAlive => _toggle != null;

    public void SetValue(bool value)
    {
        if (IsAlive) _toggle.SetStateWithoutNotify(value);
    }
}

/// <summary>A slider row (vanilla's slider with its value text).</summary>
public sealed class SliderRow
{
    private readonly Slider _slider;
    private readonly TMP_Text? _valueText;
    private readonly System.Func<float, string>? _format;

    internal SliderRow(Slider slider, TMP_Text? valueText, System.Func<float, string>? format = null)
    {
        _slider = slider;
        _valueText = valueText;
        _format = format;
    }

    public bool IsAlive => _slider != null;

    public void SetValue(float value)
    {
        if (!IsAlive) return;
        _slider.SetValueWithoutNotify(value);
        if (_valueText != null) _valueText.text = _format != null ? _format(value) : Format(value);
    }

    /// <summary>The same format vanilla's UISlider uses, so our text matches what it writes while dragging.</summary>
    internal static string Format(float value) => value.ToString("0.##");
}

/// <summary>A choice row (vanilla's dropdown).</summary>
public sealed class ChoiceRow
{
    private readonly TMP_Dropdown _dropdown;

    internal ChoiceRow(TMP_Dropdown dropdown) => _dropdown = dropdown;

    public bool IsAlive => _dropdown != null;

    public void SetValue(int index)
    {
        if (IsAlive) _dropdown.SetValueWithoutNotify(index);
    }

    /// <summary>
    /// [CHANGED] New (R3). Replaces the options (e.g. resolutions after the monitor changes) and selects
    /// <paramref name="index"/>, without firing the change handler. [PERFORMANCE] Rebuilds the option list.
    /// </summary>
    public void SetChoices(System.Collections.Generic.IReadOnlyList<string> choices, int index)
    {
        if (!IsAlive) return;
        var options = new Il2CppSystem.Collections.Generic.List<string>();
        foreach (string choice in choices) options.Add(choice);
        _dropdown.ClearOptions();
        _dropdown.AddOptions(options);
        _dropdown.SetValueWithoutNotify(index);
    }
}

/// <summary>
/// [CHANGED] New (Settings rebuild R2). A button row (vanilla's full-width row button, e.g. Export Player
/// Log): can be greyed out, and can show a short message in place of its label.
/// </summary>
public sealed class ButtonRow
{
    private readonly Button _button;
    private readonly TMP_Text? _text;
    private readonly string _label;

    internal ButtonRow(Button button, TMP_Text? text, string label)
    {
        _button = button;
        _text = text;
        _label = label;
    }

    public bool IsAlive => _button != null;

    /// <summary>Greys the button out (false) or makes it clickable again.</summary>
    public void SetEnabled(bool enabled)
    {
        if (IsAlive) _button.interactable = enabled;
    }

    /// <summary>Shows <paramref name="message"/> instead of the label, or the label again when null.</summary>
    public void ShowMessage(string? message)
    {
        if (IsAlive && _text != null) _text.text = message ?? _label;
    }
}