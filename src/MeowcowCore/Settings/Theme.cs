using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace MeowcowCore.Settings
{
    /// <summary>
    /// A set of appearance values, in the same shape as a settings file but values only:
    /// { "name": "...", "values": { "sectionId": { "key": value, ... }, ... } }.
    /// Kept as plain JSON so themes can later live in their own shareable files.
    /// </summary>
    internal sealed class Theme
    {
        private readonly Dictionary<string, Dictionary<string, JsonNode>> _values =
            new Dictionary<string, Dictionary<string, JsonNode>>(StringComparer.OrdinalIgnoreCase);

        public string Name { get; private set; }

        public bool TryGet(string sectionId, string key, out JsonNode value)
        {
            value = null;
            return _values.TryGetValue(sectionId, out var keys) && keys.TryGetValue(key, out value) && value != null;
        }

        public static Theme Parse(string json)
        {
            var theme = new Theme();
            var root = JsonNode.Parse(json) as JsonObject;
            if (root == null) return theme;

            theme.Name = root["name"]?.GetValue<string>();
            if (root["values"] is not JsonObject sections) return theme;

            foreach (var section in sections)
            {
                if (section.Value is not JsonObject keys) continue;
                var map = new Dictionary<string, JsonNode>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in keys)
                    if (pair.Value != null) map[pair.Key] = JsonNode.Parse(pair.Value.ToJsonString()); // detached copy
                theme._values[section.Key] = map;
            }
            return theme;
        }
    }

    /// <summary>
    /// Reads appearance settings through a theme. Settings that aren't part of themes (the theme choice
    /// itself, play mode) always come from the file. A null theme is Vanilla: every themed setting uses
    /// its default, which means the game's own look.
    /// </summary>
    internal sealed class ThemeSource : ISettingsSource
    {
        private readonly SettingsFile _file;
        private readonly Theme _theme;

        public ThemeSource(SettingsFile file, Theme theme)
        {
            _file = file;
            _theme = theme;
        }

        public JsonNode GetEffective(string sectionId, string key)
        {
            var def = _file.Find(sectionId, key);
            if (def == null) return null;
            if (!CoreSettings.IsThemed(sectionId, key)) return def.Value ?? def.Default;

            if (_theme != null && _theme.TryGet(sectionId, key, out var node) && SettingsFile.TryNormalize(def, node, out var value))
                return value;
            return def.Default;
        }
    }
}