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

    internal SliderRow(Slider slider, TMP_Text? valueText)
    {
        _slider = slider;
        _valueText = valueText;
    }

    public bool IsAlive => _slider != null;

    public void SetValue(float value)
    {
        if (!IsAlive) return;
        _slider.SetValueWithoutNotify(value);
        if (_valueText != null) _valueText.text = Format(value);
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
}