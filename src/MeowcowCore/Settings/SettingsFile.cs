using System;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace MeowcowCore.Settings
{
    /// <summary>
    /// A mod's settings JSON. The mod defines its settings in code (labels, types, limits, defaults);
    /// the file on disk supplies only the values. Loading merges the two and writes the result back,
    /// so new settings appear, removed ones disappear, and descriptions stay current, while the
    /// player's values are kept. Invalid values fall back to the default with a log warning.
    /// </summary>
    public sealed class SettingsFile : ISettingsSource
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // keeps "+" and "'" readable in a local file
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        };

        private readonly Func<SettingsDocument> _define;
        private DateTime _lastWrite;

        public string FilePath { get; }
        public SettingsDocument Document { get; private set; }

        /// <param name="define">Builds a fresh document with every setting at its default.</param>
        public SettingsFile(string filePath, Func<SettingsDocument> define)
        {
            FilePath = filePath;
            _define = define;
        }

        // ---------- Loading and saving ----------

        public void Load()
        {
            var doc = _define();
            var disk = ReadDisk();
            if (disk != null) CopyValues(disk, doc);
            Document = doc;
            Save();
        }

        /// <summary>Re-reads the file if it changed since we last read or wrote it. Returns true if it did.</summary>
        public bool ReloadIfChanged()
        {
            if (Document != null && File.Exists(FilePath) && File.GetLastWriteTimeUtc(FilePath) == _lastWrite)
                return false;
            Load();
            return true;
        }

        /// <summary>Writes the file if its contents changed. Written to a temp file first, so readers never see half a file.</summary>
        public void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(Document, Options);
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));

                if (!File.Exists(FilePath) || File.ReadAllText(FilePath) != json)
                {
                    string tmp = FilePath + ".tmp";
                    File.WriteAllText(tmp, json);
                    File.Move(tmp, FilePath, true);
                }
                _lastWrite = File.GetLastWriteTimeUtc(FilePath);
            }
            catch (Exception e)
            {
                CoreMod.Log.Warning($"Couldn't save {Path.GetFileName(FilePath)}: {e.Message}");
            }
        }

        private SettingsDocument ReadDisk()
        {
            if (!File.Exists(FilePath)) return null;
            try
            {
                return JsonSerializer.Deserialize<SettingsDocument>(File.ReadAllText(FilePath), Options);
            }
            catch (Exception e)
            {
                // Keep the player's broken file so their edits aren't lost.
                string backup = FilePath + ".broken";
                try { File.Copy(FilePath, backup, true); } catch { }
                CoreMod.Log.Warning($"{Path.GetFileName(FilePath)} couldn't be read ({e.Message}); using defaults. " +
                                    $"The old file was kept as {Path.GetFileName(backup)}.");
                return null;
            }
        }

        private static void CopyValues(SettingsDocument from, SettingsDocument to)
        {
            if (from.Sections == null) return;
            foreach (var section in from.Sections)
            {
                if (section?.Settings == null) continue;
                var target = to.Sections.FirstOrDefault(s => Same(s.Id, section.Id));
                if (target == null) continue; // section no longer exists

                foreach (var setting in section.Settings)
                {
                    if (setting == null) continue;
                    var def = target.Settings.FirstOrDefault(d => Same(d.Key, setting.Key));
                    if (def == null || setting.Value == null) continue;

                    if (TryNormalize(def, setting.Value, out var value)) def.Value = value;
                    else CoreMod.Log.Warning($"Setting '{section.Id}.{def.Key}' has an invalid value ({setting.Value.ToJsonString()}); using the default");
                }
            }
        }

        // ---------- Reading values ----------

        public SettingDefinition Find(string sectionId, string key) =>
            Document?.Sections.FirstOrDefault(s => Same(s.Id, sectionId))?.Settings.FirstOrDefault(d => Same(d.Key, key));

        /// <summary>The value, or the default when the value is null.</summary>
        public JsonNode GetEffective(string sectionId, string key)
        {
            var def = Find(sectionId, key);
            return def?.Value ?? def?.Default;
        }

        // Typed reads (GetBool, GetNumber, GetString) come from SettingsSourceExtensions.

        // ---------- Changing values (call Save afterwards) ----------

        public bool Set(string sectionId, string key, bool value) => Set(sectionId, key, JsonValue.Create(value));
        public bool Set(string sectionId, string key, double value) => Set(sectionId, key, JsonValue.Create(value));
        public bool Set(string sectionId, string key, string value) => Set(sectionId, key, JsonValue.Create(value));

        /// <summary>Back to the default.</summary>
        public bool Reset(string sectionId, string key) => Set(sectionId, key, (JsonNode)null);

        /// <summary>Validates and stores a value (numbers are clamped). Null resets. False if invalid or unknown.</summary>
        public bool Set(string sectionId, string key, JsonNode value)
        {
            var def = Find(sectionId, key);
            if (def == null) return false;
            if (value == null) { def.Value = null; return true; }
            if (!TryNormalize(def, value, out var normalized)) return false;
            def.Value = normalized;
            return true;
        }

        // ---------- Validation ----------

        /// <summary>Checks a value against a setting's type and limits; numbers are clamped.</summary>
        public static bool TryNormalize(SettingDefinition def, JsonNode node, out JsonNode result)
        {
            result = null;
            if (node is not JsonValue value) return false;

            switch (def.Type)
            {
                case SettingType.Bool:
                    if (!value.TryGetValue(out bool b)) return false;
                    result = JsonValue.Create(b);
                    return true;

                case SettingType.Number:
                    if (!value.TryGetValue(out double d) || double.IsNaN(d) || double.IsInfinity(d)) return false;
                    if (def.Min.HasValue) d = Math.Max(d, def.Min.Value);
                    if (def.Max.HasValue) d = Math.Min(d, def.Max.Value);
                    result = JsonValue.Create(d);
                    return true;

                case SettingType.Choice:
                    if (!value.TryGetValue(out string s)) return false;
                    var match = def.Choices?.FirstOrDefault(c => Same(c, s)); // stored with its canonical spelling
                    if (match == null) return false;
                    result = JsonValue.Create(match);
                    return true;

                case SettingType.Text:
                    if (!value.TryGetValue(out string t)) return false;
                    result = JsonValue.Create(t);
                    return true;

                case SettingType.Color:
                    if (!value.TryGetValue(out string hex) || !ColorHex.TryParse(hex, out _, out _)) return false;
                    result = JsonValue.Create(ColorHex.Normalize(hex));
                    return true;
            }
            return false;
        }

        private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
