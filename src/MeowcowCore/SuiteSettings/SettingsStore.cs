using System;
using System.Collections;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using MelonLoader;
using MeowcowCore.Foundation;
using UnityEngine;

namespace MeowcowCore.SuiteSettings;

/// <summary>
/// One module's settings file (<c>UserData/Meowcow/&lt;ModuleId&gt;/settings.json</c>): reading it once,
/// correcting bad values, and writing it after changes.
/// </summary>
/// <remarks>
/// [CHANGED] New (design §8).
/// <para>The file is keyed by setting, not subtab, so moving a setting keeps the player's value. Each entry
/// carries its default, description and limits for hand-editors (with the game closed).</para>
/// <para>Writes are delayed: half a second after the last change, or at once on <see cref="Flush"/>. A slider
/// drag or a held gamepad stick becomes one write. Nothing is written if the contents didn't change.</para>
/// [SOURCE] Pre-rebuild SettingsFile (atomic write, only write when changed, JSON options; findings doc §4).
/// [PERFORMANCE] One small file read per module at launch; one write per burst of changes. While a write
/// is pending (half a second), one coroutine checks the time each frame.
/// </remarks>
internal sealed class SettingsStore
{
    private const float WriteDelay = 0.5f; // seconds after the last change

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // keeps + and ' readable in a local file
    };

    private readonly string _moduleId;
    private readonly MeowcowLogger _log;
    private readonly string _path;
    private JsonObject? _file;       // what was on disk at launch; read once
    private string? _lastWritten;    // the text on disk, so unchanged contents aren't rewritten
    private ModuleSettings? _owner;
    private bool _writePending;
    private float _writeDue;

    internal SettingsStore(string moduleId, MeowcowLogger log)
    {
        _moduleId = moduleId;
        _log = log;
        _path = CorePathIndex.SettingsFile(moduleId);
    }

    /// <summary>Sets a newly declared setting's value from the file (or its default), logging corrections.</summary>
    internal void LoadInto(Setting setting)
    {
        _owner ??= setting.Owner;
        JsonObject file = ReadFileOnce();
        JsonNode? stored = file[setting.Key]?["Value"];
        setting.Load(stored, out string? problem);
        if (problem != null) _log.Warning($"'{setting.Key}': {problem}; corrected in the file");
    }

    /// <summary>Writes half a second after the last call. Called for every value change.</summary>
    internal void ScheduleWrite()
    {
        _writeDue = Time.unscaledTime + WriteDelay;
        if (_writePending) return;
        _writePending = true;
        MelonCoroutines.Start(WriteWhenDue());
    }

    /// <summary>
    /// Writes now if anything differs from the file: pending changes, corrected values, new or removed
    /// settings. Called when Meowcow Settings closes and at each menu load.
    /// </summary>
    internal void Flush()
    {
        _writePending = false;
        Write();
    }

    private IEnumerator WriteWhenDue()
    {
        while (_writePending && Time.unscaledTime < _writeDue) yield return null;
        if (!_writePending) yield break; // flushed meanwhile
        _writePending = false;
        Write();
    }

    private JsonObject ReadFileOnce()
    {
        if (_file != null) return _file;

        _file = new JsonObject();
        if (!File.Exists(_path)) return _file;

        string text;
        try
        {
            text = File.ReadAllText(_path);
        }
        catch (Exception e)
        {
            _log.Error($"Couldn't read {_path}; using defaults", e);
            return _file;
        }

        try
        {
            if (JsonNode.Parse(text) is JsonObject parsed) _file = parsed;
            else _log.Warning($"{_path} isn't a settings file; using defaults");
            _lastWritten = text;
        }
        catch (JsonException e)
        {
            // Keep the broken file beside the new one, so a hand-edit mistake loses nothing.
            _log.Warning($"{_path} couldn't be read ({e.Message}); using defaults. The old file is kept as settings.json.broken");
            TryCopy(_path, _path + ".broken");
        }
        return _file;
    }

    /// <summary>Writes every declared setting, in order, if the text differs from what's on disk.</summary>
    private void Write()
    {
        if (_owner == null || _owner.All.Count == 0) return; // nothing declared: no file

        try
        {
            var root = new JsonObject();
            foreach (Setting setting in _owner.All) root[setting.Key] = setting.ToJson();
            string text = root.ToJsonString(JsonOptions);
            if (text == _lastWritten) return;

            // Atomic: a reader never sees half a file.
            CorePathIndex.Ensure(Path.GetDirectoryName(_path)!);
            string temp = _path + ".tmp";
            File.WriteAllText(temp, text);
            File.Move(temp, _path, overwrite: true);
            _lastWritten = text;
            _log.Debug($"Saved {_moduleId} settings");
        }
        catch (Exception e)
        {
            _log.Error($"Couldn't save {_moduleId} settings", e);
        }
    }

    private void TryCopy(string from, string to)
    {
        try
        {
            File.Copy(from, to, overwrite: true);
        }
        catch (Exception e)
        {
            _log.Error($"Couldn't keep a copy of {from}", e);
        }
    }
}