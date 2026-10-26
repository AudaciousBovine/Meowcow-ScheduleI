using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace MeowcowCore.Settings
{
    public enum SettingType { Bool, Number, Choice, Text, Color }

    /// <summary>
    /// One setting: its value plus everything Core's options screen needs to show it.
    /// A null Value means "use Default"; a null Default means the mod works the default out itself
    /// (e.g. "vanilla placement").
    /// </summary>
    public sealed class SettingDefinition
    {
        public string Key { get; set; }
        public string Label { get; set; }
        public string Description { get; set; }
        public SettingType Type { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)] // always written, so hand-editors see it
        public JsonNode Value { get; set; }

        public JsonNode Default { get; set; }
        public double? Min { get; set; }
        public double? Max { get; set; }
        public double? Step { get; set; }
        public List<string> Choices { get; set; }

        public static SettingDefinition Bool(string key, string label, string description, bool def) => new SettingDefinition
        {
            Key = key, Label = label, Description = description, Type = SettingType.Bool,
            Default = JsonValue.Create(def),
        };

        public static SettingDefinition Number(string key, string label, string description, double? def,
                                               double min, double max, double step) => new SettingDefinition
        {
            Key = key, Label = label, Description = description, Type = SettingType.Number,
            Default = def.HasValue ? JsonValue.Create(def.Value) : null,
            Min = min, Max = max, Step = step,
        };

        public static SettingDefinition Choice(string key, string label, string description, string def,
                                               params string[] choices) => new SettingDefinition
        {
            Key = key, Label = label, Description = description, Type = SettingType.Choice,
            Default = JsonValue.Create(def),
            Choices = new List<string>(choices),
        };

        /// <summary>A colour as hex text ("#RRGGBB" or "#RRGGBBAA"). A null default means the mod's own colour.</summary>
        public static SettingDefinition Color(string key, string label, string description, string def) => new SettingDefinition
        {
            Key = key, Label = label, Description = description, Type = SettingType.Color,
            Default = def != null ? JsonValue.Create(def) : null,
        };

        public static SettingDefinition Text(string key, string label, string description, string def) => new SettingDefinition
        {
            Key = key, Label = label, Description = description, Type = SettingType.Text,
            Default = def != null ? JsonValue.Create(def) : null,
        };
    }

    /// <summary>A group of settings; becomes one block on the options screen.</summary>
    public sealed class SettingsSection
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<SettingDefinition> Settings { get; set; } = new List<SettingDefinition>();
    }

    /// <summary>A whole settings file: one per mod.</summary>
    public sealed class SettingsDocument
    {
        public int FormatVersion { get; set; } = 1;
        public string Module { get; set; }
        public List<SettingsSection> Sections { get; set; } = new List<SettingsSection>();
    }
}