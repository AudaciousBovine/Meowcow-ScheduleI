using System;
using System.Collections.Generic;
using MeowcowCore.Foundation;

namespace MeowcowCore.SuiteSettings;

/// <summary>
/// One module's settings, grouped into subtabs. Get it with <c>MeowcowSettings.For(this)</c>.
/// In Meowcow Settings, the module has one tab, and each subtab is a page along that tab's top.
/// </summary>
/// <remarks>[CHANGED] New (design §8).</remarks>
public sealed class ModuleSettings
{
    private readonly List<SettingsSubtab> _subtabs = new();
    private readonly Dictionary<string, Setting> _byKey = new(StringComparer.Ordinal);
    private readonly List<Setting> _all = new();

    internal ModuleSettings(string moduleId, MeowcowLogger log, bool isCore)
    {
        ModuleId = moduleId;
        DisplayName = MakeDisplayName(moduleId);
        Log = log.Tagged("Settings");
        IsCore = isCore;
        Store = new SettingsStore(moduleId, Log);
    }

    /// <summary>The module Id, which also names its settings folder.</summary>
    public string ModuleId { get; }

    /// <summary>The tab's label: the Id without the "Meowcow" prefix, words spaced ("TestModule" → "Test Module").</summary>
    public string DisplayName { get; }

    public IReadOnlyList<SettingsSubtab> Subtabs => _subtabs;

    /// <summary>Every setting, in the order declared.</summary>
    internal IReadOnlyList<Setting> All => _all;

    internal MeowcowLogger Log { get; }
    internal SettingsStore Store { get; }

    /// <summary>Core's own settings: always the first tab.</summary>
    internal bool IsCore { get; }

    /// <summary>
    /// A subtab, created the first time it's asked for. Subtabs appear in the order first asked for;
    /// a module with one subtab shows no subtab strip.
    /// </summary>
    public SettingsSubtab Subtab(string name)
    {
        foreach (SettingsSubtab existing in _subtabs)
        {
            if (string.Equals(existing.Name, name, StringComparison.Ordinal)) return existing;
        }
        var subtab = new SettingsSubtab(this, name);
        _subtabs.Add(subtab);
        return subtab;
    }

    /// <summary>Adds a newly declared setting and loads its saved value. A duplicate key is a module bug.</summary>
    internal T Add<T>(T setting) where T : Setting
    {
        if (_byKey.ContainsKey(setting.Key))
            throw new ArgumentException($"{ModuleId} declares the setting '{setting.Key}' twice.");

        _byKey.Add(setting.Key, setting);
        _all.Add(setting);
        Store.LoadInto(setting);
        return setting;
    }

    private static string MakeDisplayName(string moduleId)
    {
        const string prefix = "Meowcow";
        string name = moduleId.StartsWith(prefix, StringComparison.Ordinal) && moduleId.Length > prefix.Length
            ? moduleId.Substring(prefix.Length)
            : moduleId;

        // "TestModule" → "Test Module"
        var spaced = new System.Text.StringBuilder(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && char.IsLower(name[i - 1])) spaced.Append(' ');
            spaced.Append(name[i]);
        }
        return spaced.ToString();
    }
}

/// <summary>A group of settings shown as one page, and the methods that declare them.</summary>
/// <remarks>[CHANGED] New.</remarks>
public sealed class SettingsSubtab
{
    private readonly List<Setting> _settings = new();

    internal SettingsSubtab(ModuleSettings owner, string name)
    {
        Owner = owner;
        Name = name;
    }

    public ModuleSettings Owner { get; }
    public string Name { get; }

    /// <summary>This page's settings, in the order declared.</summary>
    public IReadOnlyList<Setting> Settings => _settings;

    /// <summary>Declares an on/off setting.</summary>
    public ToggleSetting Toggle(string key, string label, bool defaultValue, string description = "") =>
        Add(new ToggleSetting(Owner, Name, key, label, description, defaultValue));

    /// <summary>
    /// Declares a number between <paramref name="min"/> and <paramref name="max"/>. <paramref name="step"/>
    /// is how far a gamepad left/right press moves it.
    /// </summary>
    public NumberSetting Number(string key, string label, float defaultValue, float min, float max,
        float step = 1f, bool wholeNumbers = false, string description = "") =>
        Add(new NumberSetting(Owner, Name, key, label, description, defaultValue, min, max, step, wholeNumbers));

    /// <summary>Declares a choice from a fixed list.</summary>
    public ChoiceSetting Choice(string key, string label, string defaultValue, IEnumerable<string> choices,
        string description = "") =>
        Add(new ChoiceSetting(Owner, Name, key, label, description, defaultValue, choices));

    private T Add<T>(T setting) where T : Setting
    {
        Owner.Add(setting);
        _settings.Add(setting);
        return setting;
    }
}