using System.Text.Json.Nodes;

namespace MeowcowCore.Settings
{
    /// <summary>Anything settings can be read from: the settings file itself, or a theme layered over it.</summary>
    public interface ISettingsSource
    {
        /// <summary>The value to use for a setting (null when neither a value nor a default exists).</summary>
        JsonNode GetEffective(string sectionId, string key);
    }

    /// <summary>Typed reads for any settings source.</summary>
    public static class SettingsSourceExtensions
    {
        public static bool GetBool(this ISettingsSource source, string sectionId, string key, bool fallback = false) =>
            source.GetEffective(sectionId, key) is JsonValue v && v.TryGetValue(out bool b) ? b : fallback;

        /// <summary>Null when neither a value nor a default is set (the mod decides).</summary>
        public static float? GetNumber(this ISettingsSource source, string sectionId, string key) =>
            source.GetEffective(sectionId, key) is JsonValue v && v.TryGetValue(out double d) ? (float)d : (float?)null;

        public static string GetString(this ISettingsSource source, string sectionId, string key) =>
            source.GetEffective(sectionId, key) is JsonValue v && v.TryGetValue(out string s) ? s : null;
    }
}