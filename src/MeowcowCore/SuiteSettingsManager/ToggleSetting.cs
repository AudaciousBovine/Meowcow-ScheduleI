using System.Text.Json.Nodes;

namespace MeowcowCore.SuiteSettingsManager;

/// <summary>An on/off setting. Shown with vanilla's On/Off control.</summary>
/// <remarks>[CHANGED] New.</remarks>
public sealed class ToggleSetting : Setting<bool>
{
    internal ToggleSetting(ModuleSettings owner, string subtab, string key, string label, string description, bool defaultValue)
        : base(owner, subtab, key, label, description, defaultValue)
    {
    }

    private protected override bool Normalize(bool value, out string? problem)
    {
        problem = null;
        return value;
    }

    private protected override bool Read(JsonNode node) => node.GetValue<bool>();

    private protected override JsonNode Write(bool value) => JsonValue.Create(value);
}