using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Il2CppInterop.Runtime;
using Il2CppScheduleOne.DevUtilities;
using MelonLoader;
using MeowcowCore.Foundation;
using MeowcowCore.SuiteUI;
using UnityEngine;
using UnityEngine.UI;
using AudioSlider = Il2CppScheduleOne.UI.Settings.AudioSlider;
using EAudioType = Il2CppScheduleOne.Core.Audio.EAudioType;
using HardwarePlatformUtility = Il2CppScheduleOne.Platform.HardwarePlatformUtility;
using IntefaceScaleSlider = Il2CppScheduleOne.UI.Settings.IntefaceScaleSlider;
using MonitorDropdown = Il2CppScheduleOne.UI.Settings.MonitorDropdown;
using PlayerLogExporter = Il2CppScheduleOne.DevUtilities.PlayerLogExporter;
using PlayerLogExporterButton = Il2CppScheduleOne.UI.Settings.PlayerLogExporterButton;
using SensitivitySlider = Il2CppScheduleOne.UI.Settings.SensitivitySlider;
using GamepadSettings = Il2CppScheduleOne.Gamepad.GamepadSettings; // [CHANGED] not in DevUtilities (build fix)
using VanillaAudioSettings = Il2CppScheduleOne.DevUtilities.AudioSettings;
using VanillaSettings = Il2CppScheduleOne.DevUtilities.Settings;

namespace MeowcowCore.SuiteSettings;

/// <summary>The kind of row an adapter shows as.</summary>
internal enum AdapterKind { Toggle, Slider, Choice, Button }

/// <summary>A vanilla slider's range, read from vanilla's own (untouched) row.</summary>
internal readonly record struct SliderRange(float Min, float Max, bool WholeNumbers);

/// <summary>
/// One vanilla setting, bound for Core's own row: how to read it, how to set it (change, apply, save, as
/// vanilla's own row script does), and which vanilla script it stands in for.
/// </summary>
/// <remarks>
/// [CHANGED] New (Settings rebuild R2, design §9 "Rebuilt with adapters"). Values are in the units vanilla's
/// row shows (e.g. volume 0–100), converted exactly as the row script converts them.
/// </remarks>
internal sealed class VanillaAdapter
{
    /// <summary>Referenced by the layout table.</summary>
    public string Id { get; init; } = "";

    /// <summary>
    /// The vanilla row script this stands in for: its type name, plus ":variant" where one script type serves
    /// several rows (an audio channel, mouse or gamepad).
    /// </summary>
    public string Script { get; init; } = "";

    public string Label { get; init; } = "";
    public string Description { get; init; } = "";
    public AdapterKind Kind { get; init; }

    /// <summary>The vanilla settings fields this covers ("AudioSettings.MasterVolume"), for the field scan.</summary>
    public string[] Fields { get; init; } = Array.Empty<string>();

    // Toggle
    public Func<VanillaSettings, bool>? GetBool { get; init; }
    public Action<VanillaSettings, bool>? SetBool { get; init; }

    // Slider (in the slider's units; the range is vanilla's own row's)
    public Func<VanillaSettings, SliderRange, float>? GetFloat { get; init; }
    public Action<VanillaSettings, SliderRange, float>? SetFloat { get; init; }

    /// <summary>
    /// Applying is costly, so it waits until the value has stopped changing (a drag has settled) instead of
    /// running on every step. Used for input settings: vanilla's apply reloads every key binding.
    /// </summary>
    public bool Settle { get; init; }

    // Choice
    public Func<string[]>? Choices { get; init; }
    public Func<VanillaSettings, int>? GetIndex { get; init; }
    public Action<VanillaSettings, int>? SetIndex { get; init; }

    // Button
    public Action<Action>? Click { get; init; }    // runs the action; calls back on success
    public Func<bool>? Enabled { get; init; }

    // [CHANGED] R3
    /// <summary>How a slider's value is shown (e.g. "100%"); null: the number.</summary>
    public Func<float, string>? Format { get; init; }

    /// <summary>A fixed slider range, used instead of vanilla's row's (vanilla sets some ranges at enable).</summary>
    public SliderRange? Range { get; init; }

    /// <summary>Whether the row is shown at all (e.g. Monitor only with two or more displays).</summary>
    public Func<bool>? Visible { get; init; }

    /// <summary>The choices can change (resolutions follow the monitor), so they're re-read on refresh.</summary>
    public bool DynamicChoices { get; init; }

    /// <summary>
    /// After this applies, every row re-reads its value: display changes can change other rows (the monitor
    /// changes the resolutions; a revert puts everything back).
    /// </summary>
    public bool RefreshAllAfter { get; init; }
}

/// <summary>
/// The adapter table, and the scans that catch vanilla settings without an adapter (a game update adding
/// one). Rows are built from these by <c>Menus/SettingsMenu</c>.
/// </summary>
/// <remarks>
/// [CHANGED] New (R2). R2 covers Other, Audio, Gamepad and Input (not bindings). [CHANGED] R3 adds Display
/// (through vanilla's unapplied copy, with Core's confirm dialog for the changes vanilla confirms) and Graphics.
/// [SOURCE] Vanilla <c>Settings</c> (Apply…/Write…), and the row scripts <c>AudioSlider</c>,
/// <c>SensitivitySlider</c>, <c>PointerSensitivitySlider</c>, <c>InvertYToggle</c>, <c>SprintModeDropdown</c>,
/// <c>AutoBackupSavesToggle</c>, <c>PlayerLogExporterButton</c>.
/// [PERFORMANCE] The table is built once per run. The scans run once per window build: one pass over the
/// vanilla screen's scripts and a few dozen fields.
/// </remarks>
internal static class VanillaSettingAdapters
{
    private static readonly MeowcowLogger Log = CoreMod.Log.Tagged("Adapters");

    /// <summary>Namespace of vanilla's setting row scripts: every row's script lives here.</summary>
    private const string RowScriptNamespace = "ScheduleOne.UI.Settings";

    private const float SensitivityMultiplier = 1f / 30f; // SensitivitySlider.Multiplier (0.033333335)
    private const float VolumeMultiplier = 0.01f;         // AudioSlider.MULTIPLIER
    private const float SettleSeconds = 0.25f;

    // ---------- The table ----------

    /// <summary>Every adapter, in no particular order (the layout table orders rows).</summary>
    internal static readonly VanillaAdapter[] All =
    {
        // Other
        new()
        {
            Id = "other.autoBackup", Script = "AutoBackupSavesToggle", Kind = AdapterKind.Toggle,
            Label = "Auto Backup Saves",
            Description = "When loading an existing save on a new version of the game, a backup is made first.",
            Fields = new[] { "OtherSettings.AutoBackupSaves" },
            GetBool = s => s.OtherSettings.AutoBackupSaves,
            SetBool = (s, v) =>
            {
                s.OtherSettings.AutoBackupSaves = v;
                s.ReloadOtherSettings();
                s.WriteOtherSettings(s.OtherSettings);
            },
        },
        new()
        {
            Id = "log.export", Script = "PlayerLogExporterButton:Current", Kind = AdapterKind.Button,
            Label = "Export Player Log",
            Click = done => ExportLog(previous: false, done), Enabled = () => LogExists(previous: false),
        },
        new()
        {
            Id = "log.exportPrevious", Script = "PlayerLogExporterButton:Previous", Kind = AdapterKind.Button,
            Label = "Export Previous Player Log",
            Click = done => ExportLog(previous: true, done), Enabled = () => LogExists(previous: true),
        },

        // Audio (AudioSlider: slider value = volume × 100)
        Volume("audio.master", "Master", "Master Volume", "The overall volume.", "MasterVolume",
            s => s.AudioSettings.MasterVolume, (a, v) => a.MasterVolume = v),
        Volume("audio.music", nameof(EAudioType.Music), "Music", "", "MusicVolume",
            s => s.AudioSettings.MusicVolume, (a, v) => a.MusicVolume = v),
        Volume("audio.sfx", nameof(EAudioType.FX), "Sound Effects", "", "SFXVolume",
            s => s.AudioSettings.SFXVolume, (a, v) => a.SFXVolume = v),
        Volume("audio.ambient", nameof(EAudioType.Ambient), "Ambience", "", "AmbientVolume",
            s => s.AudioSettings.AmbientVolume, (a, v) => a.AmbientVolume = v),
        Volume("audio.weather", nameof(EAudioType.Weather), "Weather", "", "WeatherVolume",
            s => s.AudioSettings.WeatherVolume, (a, v) => a.WeatherVolume = v),
        Volume("audio.footsteps", nameof(EAudioType.Footsteps), "Footsteps", "", "FootstepsVolume",
            s => s.AudioSettings.FootstepsVolume, (a, v) => a.FootstepsVolume = v),
        Volume("audio.dialogue", nameof(EAudioType.Voice), "Dialogue", "", "DialogueVolume",
            s => s.AudioSettings.DialogueVolume, (a, v) => a.DialogueVolume = v),
        Volume("audio.ui", nameof(EAudioType.UI), "Interface", "The volume of menu and interface sounds.", "UIVolume",
            s => s.AudioSettings.UIVolume, (a, v) => a.UIVolume = v),

        // Gamepad
        new()
        {
            Id = "gamepad.pointer", Script = "PointerSensitivitySlider", Kind = AdapterKind.Slider,
            Label = "Pointer Sensitivity", Description = "How fast the on-screen pointer moves with a gamepad.",
            Fields = new[] { "GamepadSettings.PointerSensitivity" },
            GetFloat = (s, r) => Mathf.Clamp(InversePointer(s.GamepadSettings.PointerSensitivity, r), 1f, r.Max),
            SetFloat = (s, r, v) =>
            {
                s.GamepadSettings.PointerSensitivity = Pointer(Mathf.Clamp(v, 1f, r.Max), r);
                s.ReloadGamepadSettings();
                s.WriteGamepadSettings(s.GamepadSettings);
            },
        },
        new()
        {
            Id = "gamepad.cameraSensitivity", Script = "SensitivitySlider:Gamepad", Kind = AdapterKind.Slider,
            Label = "Gamepad Camera Sensitivity", Description = "How fast the camera turns with a gamepad.",
            Fields = new[] { "GamepadSettings.GamepadCameraSensitivity" },
            GetFloat = (s, _) => s.GamepadSettings.GamepadCameraSensitivity / SensitivityMultiplier,
            SetFloat = (s, _, v) =>
            {
                s.GamepadSettings.GamepadCameraSensitivity = v * SensitivityMultiplier;
                s.ReloadGamepadSettings();
                s.WriteGamepadSettings(s.GamepadSettings);
            },
        },

        // Input (not bindings). Applying reloads every binding, so sliders settle first.
        new()
        {
            Id = "input.mouseSensitivity", Script = "SensitivitySlider:Mouse", Kind = AdapterKind.Slider,
            Label = "Mouse Camera Sensitivity", Description = "How fast the camera turns with the mouse.",
            Fields = new[] { "InputSettings.MouseSensitivity" }, Settle = true,
            GetFloat = (s, _) => s.InputSettings.MouseSensitivity / SensitivityMultiplier,
            SetFloat = (s, _, v) =>
            {
                s.InputSettings.MouseSensitivity = v * SensitivityMultiplier;
                s.ReloadInputSettings();
                s.WriteInputSettings(s.InputSettings);
            },
        },
        new()
        {
            Id = "input.invertY", Script = "InvertYToggle", Kind = AdapterKind.Toggle,
            Label = "Mouse Invert Y", Description = "Inverts looking up and down with the mouse.",
            Fields = new[] { "InputSettings.InvertMouse" },
            GetBool = s => s.InputSettings.InvertMouse,
            SetBool = (s, v) =>
            {
                s.InputSettings.InvertMouse = v;
                s.ReloadInputSettings();
                s.WriteInputSettings(s.InputSettings);
            },
        },
        new()
        {
            Id = "input.sprintMode", Script = "SprintModeDropdown", Kind = AdapterKind.Choice,
            Label = "Sprint Mode", Description = "How the sprint key works: press to toggle, or hold.",
            Fields = new[] { "InputSettings.SprintMode" },
            // Vanilla's own options: the action modes in enum order, index = value.
            Choices = () => Enum.GetNames(typeof(InputSettings.EActionMode)),
            GetIndex = s => (int)s.InputSettings.SprintMode,
            SetIndex = (s, i) =>
            {
                s.InputSettings.SprintMode = (InputSettings.EActionMode)i;
                s.ReloadInputSettings();
                s.WriteInputSettings(s.InputSettings);
            },
        },
    };

    /// <summary>[CHANGED] R3: Display and Graphics, appended to the table (kept separate for readability).</summary>
    private static readonly VanillaAdapter[] DisplayAndGraphics =
    {
        // Display, applied at once and saved (vanilla: ApplyDisplaySettings(false))
        new()
        {
            Id = "display.pauseOnFocusLost", Script = "FocusLostPauseToggle", Kind = AdapterKind.Toggle,
            Label = "Pause on Focus Lost",
            Description = "Pauses the game when its window loses focus, such as when you switch to another program.",
            Fields = new[] { "DisplaySettings.PauseOnFocusLost" }, RefreshAllAfter = true,
            GetBool = s => s.DisplaySettings.PauseOnFocusLost,
            SetBool = (s, v) => EditDisplay(s, d => { d.PauseOnFocusLost = v; return d; }, confirm: false),
        },
        new()
        {
            Id = "display.interfaceScale", Script = "IntefaceScaleSlider", Kind = AdapterKind.Slider,
            Label = "Interface Scale", Description = "The size of the game's interface.",
            Fields = new[] { "DisplaySettings.UIScale" }, Settle = true, RefreshAllAfter = true,
            // Vanilla sets this slider's range when it's enabled (7–15 = 70%–150%), so it's fixed here.
            Range = new SliderRange(IntefaceScaleSlider.MinScale / IntefaceScaleSlider.MULTIPLIER,
                IntefaceScaleSlider.MaxScale / IntefaceScaleSlider.MULTIPLIER, true),
            Format = v => Mathf.Round(v * 10f) + "%",
            GetFloat = (s, _) => s.DisplaySettings.UIScale / IntefaceScaleSlider.MULTIPLIER,
            SetFloat = (s, _, v) => EditDisplay(s, d => { d.UIScale = v * IntefaceScaleSlider.MULTIPLIER; return d; }, confirm: false),
        },
        new()
        {
            Id = "display.units", Script = "UnitsModeDropdown", Kind = AdapterKind.Choice,
            Label = "Units", Description = "Metric or imperial units, for weights and distances.",
            Fields = new[] { "DisplaySettings.UnitType" }, RefreshAllAfter = true,
            Choices = () => new[] { "Metric", "Imperial" }, // vanilla's own two options
            GetIndex = s => (int)s.DisplaySettings.UnitType,
            SetIndex = (s, i) => EditDisplay(s, d =>
            {
                d.UnitType = i == 0 ? VanillaSettings.EUnitType.Metric : VanillaSettings.EUnitType.Imperial;
                return d;
            }, confirm: false),
        },
        new()
        {
            Id = "display.cameraBob", Script = "CameraBobSlider", Kind = AdapterKind.Slider,
            Label = "Camera Bobbing", Description = "How much the camera bobs while you move.",
            Fields = new[] { "DisplaySettings.CameraBobbing" }, Settle = true, RefreshAllAfter = true,
            GetFloat = (s, _) => s.DisplaySettings.CameraBobbing * 10f,
            SetFloat = (s, _, v) => EditDisplay(s, d => { d.CameraBobbing = v / 10f; return d; }, confirm: false),
        },
        new()
        {
            Id = "display.targetFps", Script = "TargetFPSSlider", Kind = AdapterKind.Slider,
            Label = "Target Frame Rate", Description = "The frame rate limit, when V-Sync is off.",
            Fields = new[] { "DisplaySettings.TargetFPS" }, Settle = true, RefreshAllAfter = true,
            GetFloat = (s, _) => s.DisplaySettings.TargetFPS,
            SetFloat = (s, _, v) => EditDisplay(s, d => { d.TargetFPS = Mathf.RoundToInt(v); return d; }, confirm: false),
        },

        // Display, applied then confirmed (vanilla: ApplyDisplaySettings(true))
        new()
        {
            Id = "display.resolution", Script = "ResolutionDropdown", Kind = AdapterKind.Choice,
            Label = "Resolution", Fields = new[] { "DisplaySettings.ResolutionIndex" },
            DynamicChoices = true, RefreshAllAfter = true,
            Choices = Resolutions,
            GetIndex = s => s.UnappliedDisplaySettings.ResolutionIndex,
            SetIndex = (s, i) => EditDisplay(s, d => { d.ResolutionIndex = i; return d; }, confirm: true),
        },
        new()
        {
            Id = "display.monitor", Script = "MonitorDropdown", Kind = AdapterKind.Choice,
            Label = "Monitor", Fields = new[] { "DisplaySettings.ActiveDisplayIndex" }, RefreshAllAfter = true,
            Visible = () => Display.displays.Length > 1, // as vanilla (SettingsScreen.Awake)
            Choices = () => Enumerable.Range(1, Display.displays.Length).Select(n => "Monitor " + n).ToArray(),
            GetIndex = _ => MonitorDropdown.GetCurrentDisplayNumber(),
            SetIndex = (s, i) =>
            {
                DisplaySettings d = s.UnappliedDisplaySettings;
                d.ActiveDisplayIndex = i;
                s.UnappliedDisplaySettings = d;
                s.onUnappliedDisplayIndexChanged?.Invoke(); // vanilla's resolution list listens for this
                ApplyDisplay(s, confirm: true);
            },
        },
        new()
        {
            Id = "display.mode", Script = "DisplayModeDropdown", Kind = AdapterKind.Choice,
            Label = "Display Mode", Fields = new[] { "DisplaySettings.DisplayMode" }, RefreshAllAfter = true,
            Choices = () => DisplayModes().Select(ModeName).ToArray(),
            // [CHANGED] Looked up in the available list (vanilla selects by enum number, which is wrong when the
            // platform offers fewer modes).
            GetIndex = s => Math.Max(0, Array.IndexOf(DisplayModes(), s.UnappliedDisplaySettings.DisplayMode)),
            SetIndex = (s, i) =>
            {
                DisplaySettings.EDisplayMode[] modes = DisplayModes();
                if (i < 0 || i >= modes.Length) return;
                EditDisplay(s, d => { d.DisplayMode = modes[i]; return d; }, confirm: true);
            },
        },
        new()
        {
            Id = "display.vsync", Script = "VSyncToggle", Kind = AdapterKind.Toggle,
            Label = "V-Sync", Description = "Matches frames to your monitor's refresh rate to prevent tearing.",
            Fields = new[] { "DisplaySettings.VSync" }, RefreshAllAfter = true,
            GetBool = s => s.UnappliedDisplaySettings.VSync,
            SetBool = (s, v) => EditDisplay(s, d => { d.VSync = v; return d; }, confirm: true),
        },

        // Graphics (set, apply, save; vanilla applies on every change)
        new()
        {
            Id = "graphics.quality", Script = "QualityDropdown", Kind = AdapterKind.Choice,
            Label = "Quality", Fields = new[] { "GraphicsSettings.GraphicsQuality" },
            Choices = () => Enum.GetNames(typeof(GraphicsSettings.EGraphicsQuality)),
            GetIndex = s => (int)s.GraphicsSettings.GraphicsQuality,
            SetIndex = (s, i) => EditGraphics(s, g => g.GraphicsQuality = (GraphicsSettings.EGraphicsQuality)i),
        },
        new()
        {
            Id = "graphics.antiAliasing", Script = "AntiAliasingDropdown", Kind = AdapterKind.Choice,
            Label = "Anti-Aliasing", Description = "Smooths jagged edges.",
            Fields = new[] { "GraphicsSettings.AntiAliasingMode" },
            Choices = () => Enum.GetNames(typeof(GraphicsSettings.EAntiAliasingMode))
                .Select(n => n.Replace("MSAAx2", "2x MSAA").Replace("MSAAx4", "4x MSAA").Replace("MSAAx8", "8x MSAA"))
                .ToArray(),
            GetIndex = s => (int)s.GraphicsSettings.AntiAliasingMode,
            SetIndex = (s, i) => EditGraphics(s, g => g.AntiAliasingMode = (GraphicsSettings.EAntiAliasingMode)i),
        },
        new()
        {
            Id = "graphics.fov", Script = "FOVSLider", Kind = AdapterKind.Slider,
            Label = "Field of View", Description = "How wide the camera sees.",
            Fields = new[] { "GraphicsSettings.FOV" },
            GetFloat = (s, _) => s.GraphicsSettings.FOV,
            SetFloat = (s, _, v) => EditGraphics(s, g => g.FOV = v),
        },
        new()
        {
            Id = "graphics.ssao", Script = "SSAOToggle", Kind = AdapterKind.Toggle,
            Label = "Ambient Occlusion", Description = "Soft shadows where surfaces meet. Costs some performance.",
            Fields = new[] { "GraphicsSettings.SSAO" },
            GetBool = s => s.GraphicsSettings.SSAO,
            SetBool = (s, v) => EditGraphics(s, g => g.SSAO = v),
        },
        new()
        {
            Id = "graphics.godRays", Script = "GodRaysToggle", Kind = AdapterKind.Toggle,
            Label = "God Rays", Description = "Visible shafts of sunlight. Costs some performance.",
            Fields = new[] { "GraphicsSettings.GodRays" },
            GetBool = s => s.GraphicsSettings.GodRays,
            SetBool = (s, v) => EditGraphics(s, g => g.GodRays = v),
        },
    };

    // ---------- Display and Graphics helpers (R3) ----------

    /// <summary>
    /// Shows Core's "keep these display settings?" dialog: given the keep and revert actions. Set by the
    /// menu that owns the window; without one, confirmed changes are simply kept.
    /// </summary>
    internal static Action<Action, Action>? ConfirmDisplay { get; set; }

    /// <summary>
    /// Changes vanilla's unapplied display settings, then applies them. <c>DisplaySettings</c> is a struct:
    /// the property returns a copy, so the whole struct is read, changed and written back.
    /// </summary>
    private static void EditDisplay(VanillaSettings s, Func<DisplaySettings, DisplaySettings> edit, bool confirm)
    {
        s.UnappliedDisplaySettings = edit(s.UnappliedDisplaySettings);
        ApplyDisplay(s, confirm);
    }

    /// <summary>
    /// Applies the unapplied display settings as vanilla's <c>SettingsScreen.ApplyDisplaySettings</c> does:
    /// without confirmation, applied and saved; with it, applied, then saved on Keep or reverted (as vanilla's
    /// <c>ConfirmDisplaySettings.Close</c>) on Revert, Back or timeout.
    /// </summary>
    private static void ApplyDisplay(VanillaSettings s, bool confirm)
    {
        DisplaySettings old = s.DisplaySettings;
        DisplaySettings next = s.UnappliedDisplaySettings;
        s.ApplyDisplaySettings(next);

        if (!confirm || ConfirmDisplay == null)
        {
            s.WriteDisplaySettings(next);
            return;
        }

        ConfirmDisplay(
            () => s.WriteDisplaySettings(next),
            () =>
            {
                s.ApplyDisplaySettings(old);
                s.DisplaySettings = old;
                s.UnappliedDisplaySettings = old;
            });
    }

    private static void EditGraphics(VanillaSettings s, Action<GraphicsSettings> edit)
    {
        edit(s.GraphicsSettings); // a class: changed in place
        s.ReloadGraphicsSettings();
        s.WriteGraphicsSettings(s.GraphicsSettings);
    }

    /// <summary>The current monitor's resolutions, as vanilla lists them ("1920x1080").</summary>
    private static string[] Resolutions() =>
        DisplaySettings.GetResolutions().ToArray().Select(r => r.width + "x" + r.height).ToArray();

    private static DisplaySettings.EDisplayMode[] DisplayModes() =>
        Enumerable.ToArray(HardwarePlatformUtility.GetAvailableDisplayModes()); // an IL2CPP array, copied

    /// <summary>Vanilla's display mode names, with its spacing.</summary>
    private static string ModeName(DisplaySettings.EDisplayMode mode) => mode.ToString()
        .Replace("FullscreenWindow", "Fullscreen Window")
        .Replace("ExclusiveFullscreen", "Exclusive Fullscreen");

    /// <summary>The adapter with this id, or null.</summary>
    internal static VanillaAdapter? Find(string id) =>
        Array.Find(All, a => a.Id == id) ?? Array.Find(DisplayAndGraphics, a => a.Id == id);

    /// <summary>[CHANGED] Every adapter, R2's and R3's.</summary>
    internal static IEnumerable<VanillaAdapter> Every => All.Concat(DisplayAndGraphics);

    /// <summary>One audio channel's slider: the shared shape of all eight.</summary>
    private static VanillaAdapter Volume(string id, string variant, string label, string description, string field,
        Func<VanillaSettings, float> get, Action<VanillaAudioSettings, float> set) => new()
    {
        Id = id, Script = "AudioSlider:" + variant, Kind = AdapterKind.Slider, Label = label,
        Description = description, Fields = new[] { "AudioSettings." + field },
        GetFloat = (s, _) => get(s) / VolumeMultiplier,
        SetFloat = (s, _, v) =>
        {
            set(s.AudioSettings, v * VolumeMultiplier);
            s.ReloadAudioSettings();
            s.WriteAudioSettings(s.AudioSettings);
        },
    };

    // ---------- Conversions (as vanilla's row scripts do them) ----------

    /// <summary>PointerSensitivitySlider.MapSensitivity: lower half 0.1–1, upper half 1–1.5.</summary>
    private static float Pointer(float value, SliderRange r)
    {
        float half = r.Max / 2f;
        if (value < half) return Mathf.Lerp(0.1f, 1f, (value - r.Min) / (half - r.Min));
        return Mathf.Lerp(1f, 1.5f, (value - half) / (r.Max - half));
    }

    /// <summary>PointerSensitivitySlider.InverseMapSensitivity.</summary>
    private static float InversePointer(float sensitivity, SliderRange r)
    {
        float half = r.Max / 2f;
        if (sensitivity < 1f) return Mathf.Lerp(r.Min, half, (sensitivity - 0.1f) / 0.9f);
        return Mathf.Lerp(half, r.Max, (sensitivity - 1f) / 0.5f);
    }

    private static bool LogExists(bool previous) => File.Exists(PlayerLogExporter.GetLogPath(previous));

    private static Il2CppSystem.Action? _exportDone; // kept referenced until vanilla calls it (IL2CPP rule)

    /// <summary>Exports a log as vanilla's button does; <paramref name="done"/> runs when it succeeds.</summary>
    private static void ExportLog(bool previous, Action done)
    {
        _exportDone = UIBuilder.ToIl2Cpp<Il2CppSystem.Action>(new Action(() => UIBuilder.RunSafely(done, "log export")));
        PlayerLogExporter.ExportPlayerLog(previous, _exportDone);
    }

    // ---------- Applying ----------

    private static readonly Dictionary<string, float> SettleDeadline = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Action> SettleApply = new(StringComparer.Ordinal);

    /// <summary>
    /// Runs an adapter's apply now, or for a settling adapter once its value has stopped changing.
    /// </summary>
    /// <remarks>
    /// [PERFORMANCE] A settling apply checks one deadline per frame while it waits (one coroutine per adapter,
    /// not per change).
    /// </remarks>
    internal static void Apply(VanillaAdapter adapter, Action apply)
    {
        if (!adapter.Settle)
        {
            apply();
            return;
        }

        bool waiting = SettleApply.ContainsKey(adapter.Id);
        SettleApply[adapter.Id] = apply;
        SettleDeadline[adapter.Id] = Time.unscaledTime + SettleSeconds;
        if (!waiting) MelonCoroutines.Start(SettleThenApply(adapter.Id));
    }

    private static IEnumerator SettleThenApply(string id)
    {
        while (Time.unscaledTime < SettleDeadline[id]) yield return null;
        Action apply = SettleApply[id];
        SettleApply.Remove(id);
        UIBuilder.RunSafely(apply, $"applying '{id}'");
    }

    // ---------- Detection ----------

    /// <summary>
    /// Vanilla row scripts deliberately without an adapter yet: shown in a later slice (Display and Graphics
    /// in R3, Current Game in R4, key bindings in slice 4), or not a setting row.
    /// </summary>
    private static readonly HashSet<string> NotAdaptedScripts = new(StringComparer.Ordinal)
    {
        // [CHANGED] R3: Display and Graphics now have adapters. (Their scripts include vanilla's own spellings
        // "IntefaceScaleSlider" and "FOVSLider": adapter keys must match them exactly.)
        "ConfirmDisplaySettings", // the revert dialog, not a row (Core has its own: SuiteUI/ConfirmDialog)
        "GameSettingsWindow",     // [CHANGED] Current Game: Console and Seeded Mixing become playthrough settings (save system)
        "Keybinder", "RestoreDefaultBindingsButton", // slice 4: key bindings
    };

    /// <summary>Vanilla settings fields deliberately without an adapter yet (same slices as above).</summary>
    private static readonly HashSet<string> NotAdaptedFields = new(StringComparer.Ordinal)
    {
        "InputSettings.BindingOverrides", // slice 4
    };

    /// <summary>What a scan found: vanilla's row scripts by adapter key, and what has no adapter.</summary>
    internal sealed class ScanResult
    {
        public Dictionary<string, MonoBehaviour> Scripts { get; } = new(StringComparer.Ordinal);
        public List<string> Unmapped { get; } = new();
    }

    /// <summary>
    /// Scans vanilla's (untouched) Settings screen. Every row script must have an adapter or be on the
    /// not-adapted list; every field of vanilla's settings objects must be covered or listed.
    /// </summary>
    /// <remarks>
    /// [CHANGED] Detection is by row script (plus the script's own variant field) rather than row name: an
    /// update that adds a setting adds a script, or a new variant of one, whatever its row is called. Rows
    /// without a script are headers and spacing. The field scan is the second net.
    /// </remarks>
    internal static ScanResult Scan(Transform vanillaScreen)
    {
        var result = new ScanResult();
        var known = new HashSet<string>(StringComparer.Ordinal);
        var covered = new HashSet<string>(StringComparer.Ordinal);
        foreach (VanillaAdapter adapter in Every)
        {
            known.Add(adapter.Script);
            foreach (string field in adapter.Fields) covered.Add(field);
        }

        foreach (MonoBehaviour script in vanillaScreen.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (script == null) continue;
            var type = script.GetIl2CppType();
            if (type.Namespace != RowScriptNamespace) continue;

            string key = KeyOf(script, type.Name);
            if (known.Contains(key)) result.Scripts.TryAdd(key, script);
            else if (!NotAdaptedScripts.Contains(type.Name)) result.Unmapped.Add($"row '{script.name}' ({key})");
        }

        ScanFields<DisplaySettings>("DisplaySettings", covered, result.Unmapped);
        ScanFields<GraphicsSettings>("GraphicsSettings", covered, result.Unmapped);
        ScanFields<VanillaAudioSettings>("AudioSettings", covered, result.Unmapped);
        ScanFields<InputSettings>("InputSettings", covered, result.Unmapped);
        ScanFields<GamepadSettings>("GamepadSettings", covered, result.Unmapped);
        ScanFields<OtherSettings>("OtherSettings", covered, result.Unmapped);

        if (result.Unmapped.Count > 0)
            Log.Error($"{result.Unmapped.Count} vanilla setting(s) have no adapter, so the improved menu doesn't " +
                      $"show them: {string.Join(", ", result.Unmapped)}");
        return result;
    }

    /// <summary>An adapter key: the script's type name, plus its variant where one type serves several rows.</summary>
    private static string KeyOf(MonoBehaviour script, string typeName)
    {
        AudioSlider? audio = script.TryCast<AudioSlider>();
        if (audio != null) return "AudioSlider:" + (audio.Master ? "Master" : audio.AudioType.ToString());

        SensitivitySlider? sensitivity = script.TryCast<SensitivitySlider>();
        if (sensitivity != null) return "SensitivitySlider:" + sensitivity._sensitivityType;

        PlayerLogExporterButton? exporter = script.TryCast<PlayerLogExporterButton>();
        if (exporter != null) return "PlayerLogExporterButton:" + (exporter._exportPreviousLog ? "Previous" : "Current");

        return typeName;
    }

    private static void ScanFields<T>(string className, HashSet<string> covered, List<string> unmapped)
    {
        // [CHANGED] Il2Cpp's GetFields needs explicit flags (build fix): instance fields, public or not.
        var flags = Il2CppSystem.Reflection.BindingFlags.Instance | Il2CppSystem.Reflection.BindingFlags.Public
                    | Il2CppSystem.Reflection.BindingFlags.NonPublic;
        foreach (var field in Il2CppType.Of<T>().GetFields(flags))
        {
            if (field.Name.Contains('<')) continue; // compiler-made backing fields
            string key = className + "." + field.Name;
            if (!covered.Contains(key) && !NotAdaptedFields.Contains(key)) unmapped.Add($"field {key}");
        }
    }

    /// <summary>The range of a vanilla row script's slider, or null if it has none.</summary>
    internal static SliderRange? RangeOf(MonoBehaviour script)
    {
        Slider? slider = script.GetComponent<Slider>();
        return slider == null ? null : new SliderRange(slider.minValue, slider.maxValue, slider.wholeNumbers);
    }
}