using System;
using MeowcowCore.Foundation;
using UnityEngine;
using UnityEngine.InputSystem;
using RebindActionUI = Il2CppScheduleOne.DevUtilities.RebindActionUI;

namespace MeowcowCore.SuiteUI;

/// <summary>
/// One gate for all of Core's menu input around rebinding: while any rebind runs (vanilla's or Core's), and
/// after it until <see cref="Grace"/> has passed and every button is released, <see cref="Blocking"/> is true and
/// Core's menus ignore their input. The press that finishes a rebind then can't also switch menus, move the
/// selection or close the window.
/// </summary>
/// <remarks>
/// [CHANGED] New. The menu switch (vanilla's TogglePhone) fired the moment its own button was rebound: vanilla
/// re-enables the action in the same callback that completes the rebind, while the button is still held.
/// [SOURCE] Vanilla <c>RebindActionUI</c> (static <c>IsRebindingInProgress</c>; its own 0.1 s
/// <c>_timeOnLastRebind</c> grace, which only guards starting another rebind). Waiting for release so a held
/// press never counts as a new one: general input practice.
/// [PERFORMANCE] Polled once per frame by open windows: a static read and a time compare. The button sweep
/// (about 20 reads) runs only during the short wait after a rebind. Nothing runs while windows are closed.
/// </remarks>
public static class RebindGuard
{
    /// <summary>Seconds after a rebind before input counts again (vanilla's own grace is 0.1 s).</summary>
    private const float Grace = 0.25f;

    private static readonly MeowcowLogger Log = CoreMod.Log.Tagged("RebindGuard");

    private static int _coreRebinds;                 // Core's own rebinds in progress
    private static float _lastSeen = float.MinValue; // unscaled time a rebind was last seen running
    private static bool _settling;                   // a rebind ended; waiting for the grace and release
    private static bool _vanillaFailed;              // vanilla's check threw once; logged and skipped since

    /// <summary>
    /// True while Core's menus should ignore input. Poll it every frame a menu is open, so a rebind is seen
    /// while it runs.
    /// </summary>
    public static bool Blocking
    {
        get
        {
            if (_coreRebinds > 0 || VanillaRebinding())
            {
                MarkSeen();
                return true;
            }
            if (!_settling) return false;
            if (Time.unscaledTime - _lastSeen < Grace || AnyButtonHeld()) return true;
            _settling = false;
            return false;
        }
    }

    /// <summary>A Core rebind starts listening (Slice 4's rows). Pair with <see cref="End"/>.</summary>
    public static void Begin()
    {
        _coreRebinds++;
        MarkSeen();
    }

    /// <summary>A Core rebind finished or was cancelled.</summary>
    public static void End()
    {
        if (_coreRebinds > 0) _coreRebinds--;
        MarkSeen();
    }

    private static void MarkSeen()
    {
        _lastSeen = Time.unscaledTime;
        _settling = true;
    }

    /// <summary>Vanilla's rebind rows (only enabled ones count, so a hidden vanilla screen costs nothing).</summary>
    private static bool VanillaRebinding()
    {
        if (_vanillaFailed) return false;
        try
        {
            return RebindActionUI.IsRebindingInProgress;
        }
        catch (Exception e)
        {
            _vanillaFailed = true;
            Log.Error("Couldn't read vanilla's rebinding state; only Core's rebinds are guarded", e);
            return false;
        }
    }

    /// <summary>Any key, mouse button or gamepad button held right now.</summary>
    private static bool AnyButtonHeld()
    {
        Keyboard? keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.isPressed) return true;

        Mouse? mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed
            || mouse.middleButton.isPressed || mouse.backButton.isPressed || mouse.forwardButton.isPressed))
            return true;

        Gamepad? pad = Gamepad.current;
        return pad != null && (pad.buttonSouth.isPressed || pad.buttonEast.isPressed || pad.buttonWest.isPressed
            || pad.buttonNorth.isPressed || pad.leftShoulder.isPressed || pad.rightShoulder.isPressed
            || pad.leftTrigger.isPressed || pad.rightTrigger.isPressed || pad.leftStickButton.isPressed
            || pad.rightStickButton.isPressed || pad.startButton.isPressed || pad.selectButton.isPressed
            || pad.dpad.up.isPressed || pad.dpad.down.isPressed || pad.dpad.left.isPressed
            || pad.dpad.right.isPressed);
    }
}
