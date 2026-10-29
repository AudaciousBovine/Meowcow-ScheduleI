using System;
using System.Text.Json.Nodes;

namespace MeowcowCore.SuiteSettings;

/// <summary>A number between limits. Shown with vanilla's slider.</summary>
/// <remarks>
/// [CHANGED] New. Values are clamped to the limits and, for whole numbers, rounded; NaN and infinity are
/// rejected. [SOURCE] Pre-rebuild SettingsModel validation (findings doc §4).
/// </remarks>
public sealed class NumberSetting : Setting<float>
{
    internal NumberSetting(ModuleSettings owner, string subtab, string key, string label, string description,
        float defaultValue, float min, float max, float step, bool wholeNumbers)
        : base(owner, subtab, key, label, description, defaultValue)
    {
        if (!(max > min)) throw new ArgumentException($"Setting '{key}': max must be above min.");
        Min = min;
        Max = max;
        Step = step > 0f ? step : 1f;
        WholeNumbers = wholeNumbers;
    }

    public float Min { get; }
    public float Max { get; }

    /// <summary>How far one gamepad left/right press moves the slider.</summary>
    public float Step { get; }

    public bool WholeNumbers { get; }

    /// <summary>The value rounded to a whole number, for settings used as counts.</summary>
    public int IntValue => (int)MathF.Round(Value);

    private protected override float Normalize(float value, out string? problem)
    {
        problem = null;
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            problem = $"{value} isn't a number; using the default";
            return Default;
        }

        float result = Math.Clamp(value, Min, Max);
        if (WholeNumbers) result = MathF.Round(result);
        if (result != value) problem = $"{value} adjusted to {result} (limits {Min} to {Max})";
        return result;
    }

    private protected override float Read(JsonNode node) => (float)node.GetValue<double>();

    // Through decimal so 0.1 is written as 0.1, not 0.10000000149.
    private protected override JsonNode Write(float value) => JsonValue.Create((decimal)value);

    private protected override void AddInfo(JsonObject entry)
    {
        entry["Min"] = JsonValue.Create((decimal)Min);
        entry["Max"] = JsonValue.Create((decimal)Max);
        if (WholeNumbers) entry["WholeNumbers"] = true;
    }
}