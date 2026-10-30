using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace MeowcowCore.SuiteSettingsManager;

/// <summary>
/// One declared setting, without its value type. Core uses this to load, save and list settings;
/// modules use the typed <see cref="Setting{T}"/>.
/// </summary>
/// <remarks>[CHANGED] New (design §8). [SOURCE] LilithsEngine schema-driven settings; pre-rebuild SettingsModel.</remarks>
public abstract class Setting
{
    private protected Setting(ModuleSettings owner, string subtab, string key, string label, string description)
    {
        Owner = owner;
        Subtab = subtab;
        Key = key;
        Label = label;
        Description = description;
    }

    /// <summary>The module this setting belongs to.</summary>
    public ModuleSettings Owner { get; }

    /// <summary>The subtab it's shown in. Not part of the file: moving a setting keeps its value.</summary>
    public string Subtab { get; }

    /// <summary>The key in the module's settings file. Unique within the module; never rename it.</summary>
    public string Key { get; }

    public string Label { get; }
    public string Description { get; }

    /// <summary>
    /// Shown in vanilla's Settings instead of Meowcow Settings: Core's switches for vanilla behaviour
    /// (e.g. Skip Disclaimer). Meowcow Settings is kept for the mods' own settings.
    /// </summary>
    /// <remarks>[CHANGED] New (Settings rework slice 3). Core only: see <see cref="SettingExtensions"/>.</remarks>
    public bool VanillaSettingsOnly { get; internal set; }

    /// <summary>Sets the value from the file without notifying. Null means missing: the default is used.</summary>
    /// <param name="problem">Why the stored value was rejected or changed, for the log; null if it was fine.</param>
    internal abstract void Load(JsonNode? value, out string? problem);

    /// <summary>This setting's entry in the file: value, default, description and any limits.</summary>
    internal abstract JsonObject ToJson();
}

/// <summary>Declaration helpers for Core's own settings.</summary>
internal static class SettingExtensions
{
    /// <summary>
    /// Marks a Core setting as shown in vanilla's Settings (placed by its layout table) instead of Meowcow
    /// Settings. Internal: a module's setting placed nowhere would be invisible.
    /// </summary>
    internal static TSetting ShownInVanillaSettings<TSetting>(this TSetting setting) where TSetting : Setting
    {
        setting.VanillaSettingsOnly = true;
        return setting;
    }
}

/// <summary>
/// A setting with a live value. Setting <see cref="Value"/> validates it, notifies <see cref="Changed"/>
/// and schedules the file write; reading it is free.
/// </summary>
/// <remarks>
/// [CHANGED] New. Values are live (not read once at wake) because Meowcow Settings applies changes the
/// moment they're made, like vanilla's Settings.
/// [PERFORMANCE] Reading <see cref="Value"/> is a field read. A change costs one validation, the Changed
/// handlers, and a write scheduled for half a second later (one write per burst of changes).
/// </remarks>
public abstract class Setting<T> : Setting where T : notnull
{
    private T _value;

    private protected Setting(ModuleSettings owner, string subtab, string key, string label, string description, T defaultValue)
        : base(owner, subtab, key, label, description)
    {
        Default = defaultValue;
        _value = defaultValue;
    }

    public T Default { get; }

    /// <summary>The current value. Setting it corrects invalid values (e.g. clamps numbers) first.</summary>
    public T Value
    {
        get => _value;
        set => Change(value);
    }

    /// <summary>
    /// Fired after the value changes, from the menu or from code. Each handler runs on its own, so one
    /// failing handler doesn't stop the others.
    /// </summary>
    public event Action<T>? Changed;

    /// <summary>A valid version of <paramref name="value"/>, and why it had to change (null if it didn't).</summary>
    private protected abstract T Normalize(T value, out string? problem);

    /// <summary>Reads a value from the file. Throws if the JSON has the wrong type.</summary>
    private protected abstract T Read(JsonNode node);

    private protected abstract JsonNode Write(T value);

    /// <summary>Extra lines for hand-editors (limits, choices).</summary>
    private protected virtual void AddInfo(JsonObject entry) { }

    private void Change(T value)
    {
        T normalized = Normalize(value, out string? problem);
        if (problem != null) Owner.Log.Debug($"'{Key}': {problem}");
        if (EqualityComparer<T>.Default.Equals(normalized, _value)) return;

        _value = normalized;
        Owner.Store.ScheduleWrite();

        Action<T>? handlers = Changed;
        if (handlers == null) return;
        foreach (Delegate handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action<T>)handler)(normalized);
            }
            catch (Exception e)
            {
                Owner.Log.Error($"A handler for setting '{Key}' failed", e);
            }
        }
    }

    internal override void Load(JsonNode? node, out string? problem)
    {
        problem = null;
        if (node == null)
        {
            _value = Default;
            return;
        }

        T read;
        try
        {
            read = Read(node);
        }
        catch (Exception)
        {
            problem = $"{node.ToJsonString()} isn't a valid value";
            _value = Default;
            return;
        }
        _value = Normalize(read, out problem);
    }

    internal override JsonObject ToJson()
    {
        var entry = new JsonObject
        {
            ["Value"] = Write(_value),
            ["Default"] = Write(Default),
        };
        if (Description.Length > 0) entry["Description"] = Description;
        AddInfo(entry);
        return entry;
    }
}