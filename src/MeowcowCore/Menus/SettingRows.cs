using System;
using MeowcowCore.Foundation;
using MeowcowCore.SuiteSettingsManager;
using MeowcowCore.SuiteUI;

namespace MeowcowCore.Menus;

/// <summary>
/// The row for a setting, bound both ways: changing the row sets the setting, and a setting changed
/// elsewhere updates the row. Shared by Meowcow Settings and vanilla Settings (Core's rows there).
/// </summary>
/// <remarks>
/// [CHANGED] Moved out of MeowcowSettingsMenu, so vanilla Settings can show Core settings the same way.
/// [PERFORMANCE] One handler per row; it removes itself once its window is gone.
/// </remarks>
internal static class SettingRows
{
    private static readonly MeowcowLogger Log = CoreMod.Log.Tagged("Settings");

    /// <summary>Adds the setting's row to the end of the page.</summary>
    internal static void Add(WindowPage page, Setting setting)
    {
        switch (setting)
        {
            case ToggleSetting toggle:
            {
                ToggleRow? row = page.AddToggle(toggle.Label, toggle.Value, v => toggle.Value = v, toggle.Description);
                if (row != null) Follow(toggle, () => row.IsAlive, row.SetValue);
                break;
            }
            case NumberSetting number:
            {
                SliderRow? row = page.AddSlider(number.Label, number.Min, number.Max, number.Step,
                    number.WholeNumbers, number.Value, v => number.Value = v, number.Description);
                if (row != null) Follow(number, () => row.IsAlive, row.SetValue);
                break;
            }
            case ChoiceSetting choice:
            {
                ChoiceRow? row = page.AddChoice(choice.Label, choice.Choices, choice.Index,
                    i => choice.Value = choice.Choices[i], choice.Description);
                if (row != null) Follow(choice, () => row.IsAlive, v => row.SetValue(choice.IndexOf(v)));
                break;
            }
            default:
                Log.Warning($"'{setting.Key}' has a type the menu can't show yet");
                break;
        }
    }

    /// <summary>
    /// Keeps a row showing the setting's value when it changes elsewhere. Once the row's window is gone,
    /// the handler removes itself, so rebuilt menus don't pile up handlers.
    /// </summary>
    private static void Follow<T>(Setting<T> setting, Func<bool> rowIsAlive, Action<T> showValue) where T : notnull
    {
        Action<T>? handler = null;
        handler = value =>
        {
            if (!rowIsAlive())
            {
                setting.Changed -= handler;
                return;
            }
            showValue(value);
        };
        setting.Changed += handler;
    }
}