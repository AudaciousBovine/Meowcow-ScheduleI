using System;
using System.Linq;
using MeowcowCore.Foundation;
using MeowcowCore.SuiteSettingsManager;
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
            // [CHANGED] Settings shown in vanilla's Settings (Core's switches for vanilla behaviour) aren't
            // repeated here: Meowcow Settings is for the mods' own settings.
            if (!subtab.Settings.Any(st => !st.VanillaSettingsOnly)) continue;
            any = true;
            WindowPage page = tab.AddPage(subtab.Name);
            foreach (Setting setting in subtab.Settings)
            {
                if (!setting.VanillaSettingsOnly) SettingRows.Add(page, setting);
            }
        }

        if (!any) tab.AddPage(EmptyPage).AddText(EmptyText);
    }

}