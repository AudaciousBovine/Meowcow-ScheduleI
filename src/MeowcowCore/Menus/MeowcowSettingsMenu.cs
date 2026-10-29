using System;
using MeowcowCore.Foundation;
using MeowcowCore.SuiteSettings;
using MeowcowCore.SuiteUI;

namespace MeowcowCore.Menus;

/// <summary>
/// The Meowcow Settings menu (design §8): one tab per module (Core first), one page per subtab, one row
/// per setting. Built from the declarations the first time it's opened in a scene.
/// </summary>
/// <remarks>
/// [CHANGED] New. Changes apply the moment they're made, like vanilla's Settings; files are written half a
/// second after the last change, and at once when the menu closes.
/// Rows and settings stay in step both ways: the row sets the value, and a value changed elsewhere (e.g.
/// by a module) updates the row. A row whose window is gone unhooks itself.
/// [PERFORMANCE] Built on first open per scene. Nothing per frame of ours.
/// </remarks>
internal static class MeowcowSettingsMenu
{
    private const string WindowName = "MeowcowSettingsWindow";
    private const string Title = "Meowcow Settings";
    private const string EmptyPage = "General";
    private const string EmptyText = "No settings yet";

    private static readonly MeowcowLogger Log = CoreMod.Log.Tagged("MeowcowSettings");

    private static MeowcowWindow? _window;

    /// <summary>Opens the menu, building it first if this scene doesn't have it yet.</summary>
    internal static void Open()
    {
        if (_window == null || !_window.IsAlive)
        {
            _window = Build();
            if (_window != null) _window.Closed += MeowcowSettings.FlushAll; // save pending changes on close
        }
        _window?.Open();
    }

    private static MeowcowWindow? Build()
    {
        MeowcowWindow? window = MeowcowWindow.CreateFromSettings(WindowName, Title);
        if (window == null) return null;

        foreach (ModuleSettings module in MeowcowSettings.All)
        {
            try
            {
                AddModule(window, module);
            }
            catch (Exception e)
            {
                Log.Error($"Couldn't show {module.ModuleId}'s settings", e);
            }
        }
        return window;
    }

    /// <summary>One tab: a page per subtab, or a single "No settings yet" page.</summary>
    private static void AddModule(MeowcowWindow window, ModuleSettings module)
    {
        WindowTab tab = window.AddTab(module.DisplayName);

        bool any = false;
        foreach (SettingsSubtab subtab in module.Subtabs)
        {
            if (subtab.Settings.Count == 0) continue;
            any = true;
            WindowPage page = tab.AddPage(subtab.Name);
            foreach (Setting setting in subtab.Settings) AddRow(page, setting);
        }

        if (!any) tab.AddPage(EmptyPage).AddText(EmptyText);
    }

    /// <summary>The row for one setting, bound both ways.</summary>
    private static void AddRow(WindowPage page, Setting setting)
    {
        switch (setting)
        {
            case ToggleSetting toggle:
            {
                ToggleRow? row = page.AddToggle(toggle.Label, toggle.Value, v => toggle.Value = v);
                if (row != null) Follow(toggle, () => row.IsAlive, row.SetValue);
                break;
            }
            case NumberSetting number:
            {
                SliderRow? row = page.AddSlider(number.Label, number.Min, number.Max, number.Step,
                    number.WholeNumbers, number.Value, v => number.Value = v);
                if (row != null) Follow(number, () => row.IsAlive, row.SetValue);
                break;
            }
            case ChoiceSetting choice:
            {
                ChoiceRow? row = page.AddChoice(choice.Label, choice.Choices, choice.Index,
                    i => choice.Value = choice.Choices[i]);
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