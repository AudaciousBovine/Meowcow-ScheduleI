using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace MeowcowCore.SuiteSettingsManager;

/// <summary>One of a fixed list of choices. Shown with vanilla's dropdown.</summary>
/// <remarks>
/// [CHANGED] New. Matching is case-insensitive and the value is stored in its listed spelling.
/// [SOURCE] Pre-rebuild SettingsModel validation (findings doc §4).
/// </remarks>
public sealed class ChoiceSetting : Setting<string>
{
    private readonly string[] _choices;

    internal ChoiceSetting(ModuleSettings owner, string subtab, string key, string label, string description,
        string defaultValue, IEnumerable<string> choices)
        : base(owner, subtab, key, label, description, defaultValue)
    {
        _choices = choices.ToArray();
        if (_choices.Length == 0) throw new ArgumentException($"Setting '{key}': needs at least one choice.");
        if (IndexOf(defaultValue) < 0) throw new ArgumentException($"Setting '{key}': the default isn't one of the choices.");
    }

    public IReadOnlyList<string> Choices => _choices;

    /// <summary>The position of the current value in <see cref="Choices"/>.</summary>
    public int Index => IndexOf(Value);

    /// <summary>The position of a choice, ignoring case; -1 if it isn't one.</summary>
    public int IndexOf(string value)
    {
        for (int i = 0; i < _choices.Length; i++)
        {
            if (string.Equals(_choices[i], value, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }

    private protected override string Normalize(string value, out string? problem)
    {
        problem = null;
        int index = IndexOf(value);
        if (index >= 0) return _choices[index]; // the listed spelling
        problem = $"'{value}' isn't one of the choices; using the default";
        return Default;
    }

    private protected override string Read(JsonNode node) => node.GetValue<string>();

    private protected override JsonNode Write(string value) => JsonValue.Create(value)!;

    private protected override void AddInfo(JsonObject entry)
    {
        var list = new JsonArray();
        foreach (string choice in _choices) list.Add(choice);
        entry["Choices"] = list;
    }
}