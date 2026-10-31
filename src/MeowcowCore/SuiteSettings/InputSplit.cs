using System;
using System.Collections;
using Il2CppScheduleOne.DevUtilities;
using MeowcowCore.Foundation;
using MeowcowCore.SuiteSettingsManager;
using MeowcowCore.SuiteUI;
using UnityEngine;
using GameInput = Il2CppScheduleOne.GameInput;
using InputSettings = Il2CppScheduleOne.DevUtilities.InputSettings;
using PlayerMovement = Il2CppScheduleOne.PlayerScripts.PlayerMovement;
using VanillaSettings = Il2CppScheduleOne.DevUtilities.Settings;

namespace MeowcowCore.SuiteSettings;

/// <summary>
/// Keyboard and gamepad input settings, fully decoupled (design §9): Sprint Mode, Sneak Mode and Invert Y
/// each have a keyboard and a gamepad value. Keyboard values are vanilla's own (its Mouse Invert Y and
/// Sprint Mode rows, saved by vanilla); gamepad values, and Sneak Mode, are Core's. Shown in vanilla's
/// Settings only.
/// </summary>
/// <remarks>
/// [CHANGED] New (Settings rework slice 3). No patch:
/// <list type="bullet">
/// <item>Vanilla reads <c>Settings.SprintMode</c> and <c>Settings.InvertMouse</c> every frame. When the
/// input device changes, Core writes the current device's values into them (vanilla's saved values for
/// keyboard, Core's for gamepad), and again after vanilla re-applies its input settings
/// (<c>onInputsApplied</c>), which resets them.</item>
/// <item>Vanilla's crouch is a toggle (Press). For Hold, Core stands the player up when the crouch button
/// is released after crouching with it, once vanilla's own CanStand allows it.</item>
/// </list>
/// [SOURCE] Vanilla <c>Settings.ApplyInputSettings</c>, <c>PlayerMovement.Move</c> (sprint and crouch input),
/// <c>PlayerCamera.RotateCamera</c> (invert).
/// [PERFORMANCE] Two field writes per device change or input apply; while the current device's Sneak Mode
/// is Hold, one button check per frame in a session.
/// </remarks>
internal static class InputSplit
{
    internal const string GamepadSprintModeKey = "gamepadSprintMode";
    internal const string KeyboardSneakModeKey = "keyboardSneakMode";
    internal const string GamepadSneakModeKey = "gamepadSneakMode";
    internal const string GamepadInvertYKey = "gamepadInvertY";

    private const string Hold = "Hold";

    private static readonly MeowcowLogger Log = CoreMod.Log.Tagged("Input");

    private static ChoiceSetting? _gamepadSprintMode;
    private static ChoiceSetting? _keyboardSneakMode;
    private static ChoiceSetting? _gamepadSneakMode;
    private static ToggleSetting? _gamepadInvertY;

    private static Il2CppSystem.Object? _deviceListener; // kept referenced (IL2CPP rule)
    private static Il2CppSystem.Object? _appliedListener;
    private static bool _watchingSneak;

    /// <summary>Declares the settings on Core. Placed in vanilla's Settings by its layout table.</summary>
    internal static void Declare(ModuleSettings core)
    {
        // Choices are vanilla's own action modes, so they match its Sprint Mode row exactly.
        string[] modes = Enum.GetNames(typeof(InputSettings.EActionMode));
        string press = Array.Exists(modes, m => m == "Press") ? "Press" : modes[0];

        SettingsSubtab vanilla = core.Subtab("Vanilla");
        _gamepadSprintMode = vanilla.Choice(GamepadSprintModeKey, "Sprint Mode", press, modes,
            "How the sprint button works on a gamepad: press to toggle, or hold.").ShownInVanillaSettings();
        _keyboardSneakMode = vanilla.Choice(KeyboardSneakModeKey, "Sneak Mode", press, modes,
            "How the crouch key works on keyboard: press to toggle, or hold.").ShownInVanillaSettings();
        _gamepadSneakMode = vanilla.Choice(GamepadSneakModeKey, "Sneak Mode", press, modes,
            "How the crouch button works on a gamepad: press to toggle, or hold.").ShownInVanillaSettings();
        _gamepadInvertY = vanilla.Toggle(GamepadInvertYKey, "Gamepad Invert Y", false,
            "Inverts looking up and down on a gamepad.").ShownInVanillaSettings();

        _gamepadSprintMode.Changed += _ => Apply();
        _gamepadInvertY.Changed += _ => Apply();
    }

    /// <summary>
    /// Starts following the input device (once per run) and applies the current device's values. Called at
    /// each main menu load; vanilla's Settings and input exist for the whole run.
    /// </summary>
    internal static void Start()
    {
        try
        {
            ListenOnce();
            Apply();
        }
        catch (Exception e)
        {
            Log.Error("Couldn't start the keyboard/gamepad split", e);
        }
    }

    /// <summary>A session started: Sneak Mode Hold is watched while it lasts. Called by CoreLifecycle.</summary>
    internal static void OnSessionStart()
    {
        Apply();
        if (!_watchingSneak) MelonLoader.MelonCoroutines.Start(WatchSneak());
    }

    // ---------- Sprint Mode and Invert Y ----------

    /// <summary>Writes the current device's Sprint Mode and Invert Y into vanilla's live fields.</summary>
    private static void Apply()
    {
        VanillaSettings? settings = Singleton<VanillaSettings>.Instance;
        if (settings == null || settings.InputSettings == null) return;

        if (GameInput.GetCurrentInputDeviceIsGamepad())
        {
            if (_gamepadSprintMode != null && Enum.TryParse(_gamepadSprintMode.Value, out InputSettings.EActionMode mode))
                settings.SprintMode = mode;
            if (_gamepadInvertY != null) settings.InvertMouse = _gamepadInvertY.Value;
        }
        else
        {
            // Keyboard: vanilla's own saved values (its rows edit them).
            settings.SprintMode = settings.InputSettings.SprintMode;
            settings.InvertMouse = settings.InputSettings.InvertMouse;
        }
    }

    private static void ListenOnce()
    {
        if (_deviceListener != null) return;
        VanillaSettings? settings = Singleton<VanillaSettings>.Instance;
        if (settings == null) return;

        var onDevice = UIBuilder.ToIl2Cpp<Il2CppSystem.Action<GameInput.InputDeviceType>>(
            new Action<GameInput.InputDeviceType>(_ => SafeApply()));
        _deviceListener = onDevice;
        GameInput.OnInputDeviceChanged = Il2CppSystem.Delegate.Combine(GameInput.OnInputDeviceChanged, onDevice)
            .TryCast<Il2CppSystem.Action<GameInput.InputDeviceType>>();

        // Vanilla resets both fields whenever it applies its input settings (a row changed, a rebind).
        var onApplied = UIBuilder.ToIl2Cpp<Il2CppSystem.Action>(new Action(SafeApply));
        _appliedListener = onApplied;
        settings.onInputsApplied = Il2CppSystem.Delegate.Combine(settings.onInputsApplied, onApplied)
            .TryCast<Il2CppSystem.Action>();
    }

    /// <summary>Called from native code: exceptions must never escape.</summary>
    private static void SafeApply()
    {
        try
        {
            Apply();
        }
        catch (Exception e)
        {
            Log.Error("Couldn't apply the input settings", e);
        }
    }

    // ---------- Sneak Mode: Hold ----------

    /// <summary>
    /// While a session runs: if the current device's Sneak Mode is Hold, a crouch made while holding the
    /// button ends when it's released (once there's room to stand). Crouches from anything else are left alone.
    /// </summary>
    private static IEnumerator WatchSneak()
    {
        _watchingSneak = true;
        bool crouchedByHold = false;
        try
        {
            while (CoreLifecycle.IsInSession)
            {
                yield return null;

                ChoiceSetting? mode = GameInput.GetCurrentInputDeviceIsGamepad() ? _gamepadSneakMode : _keyboardSneakMode;
                if (mode?.Value != Hold)
                {
                    crouchedByHold = false;
                    continue;
                }

                PlayerMovement? movement = PlayerSingleton<PlayerMovement>.Instance;
                if (movement == null) continue;

                bool held = GameInput.GetButton(GameInput.ButtonCode.Crouch);
                if (!movement.IsCrouched)
                {
                    crouchedByHold = false;
                    continue;
                }

                if (held)
                {
                    crouchedByHold = true;
                }
                else if (crouchedByHold && movement.CanStand())
                {
                    movement.SetCrouched(false);
                    crouchedByHold = false;
                }
            }
        }
        finally
        {
            _watchingSneak = false;
        }
    }
}