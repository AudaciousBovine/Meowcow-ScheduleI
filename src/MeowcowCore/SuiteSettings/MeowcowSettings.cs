using System;
using System.Collections.Generic;
using MelonLoader;
using MeowcowCore.Foundation;
using MeowcowCore.ModuleApi;

namespace MeowcowCore.SuiteSettings;

/// <summary>
/// Where modules declare their settings for Meowcow Settings. In <c>OnInitializeMelon</c>:
/// <code>
/// var general = MeowcowSettings.For(this).Subtab("General");
/// Enabled = general.Toggle("enabled", "Enabled", true);
/// </code>
/// Values are loaded as they're declared and stay live: read <c>Value</c> any time, or listen to
/// <c>Changed</c>.
/// </summary>
/// <remarks>
/// [CHANGED] New (design §8). Named MeowcowSettings rather than SuiteSettings: a class with its folder's
/// name clashes with the namespace inside MeowcowCore (e.g. <c>SuiteSettings.For</c> would resolve to the
/// namespace). It also matches the menu's name.
/// </remarks>
public static class MeowcowSettings
{
    private static readonly List<ModuleSettings> Modules = new();

    /// <summary>A module's settings, created on first use. Same compile-time check as SuiteModules.Register.</summary>
    public static ModuleSettings For<T>(T mod) where T : MelonMod, IMeowcowModule => Get(mod, isCore: false);

    /// <summary>Core's own settings (Core isn't a module, so it has its own way in).</summary>
    internal static ModuleSettings ForCore(MelonMod core) => Get(core, isCore: true);

    /// <summary>Every module with settings: Core first, then in the order they were first asked for.</summary>
    internal static IReadOnlyList<ModuleSettings> All
    {
        get
        {
            var ordered = new List<ModuleSettings>(Modules.Count);
            foreach (ModuleSettings m in Modules) if (m.IsCore) ordered.Add(m);
            foreach (ModuleSettings m in Modules) if (!m.IsCore) ordered.Add(m);
            return ordered;
        }
    }

    /// <summary>Writes every module's file that differs from disk. See <see cref="SettingsStore.Flush"/>.</summary>
    /// <remarks>[PERFORMANCE] Builds each module's JSON (small) and compares it; writes only differences.</remarks>
    internal static void FlushAll()
    {
        foreach (ModuleSettings module in Modules) module.Store.Flush();
    }

    private static ModuleSettings Get(MelonMod mod, bool isCore)
    {
        if (mod == null) throw new ArgumentNullException(nameof(mod));
        string id = mod.Info.Name;
        foreach (ModuleSettings existing in Modules)
        {
            if (string.Equals(existing.ModuleId, id, StringComparison.Ordinal)) return existing;
        }

        var settings = new ModuleSettings(id, new MeowcowLogger(mod.LoggerInstance), isCore);
        Modules.Add(settings);
        return settings;
    }
}