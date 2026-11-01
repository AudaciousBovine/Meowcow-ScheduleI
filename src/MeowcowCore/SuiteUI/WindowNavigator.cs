using System;
using System.Collections;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using MelonLoader;
using MeowcowCore.Foundation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UIPanel = Il2CppScheduleOne.UIPanel;

namespace MeowcowCore.SuiteUI;

/// <summary>
/// Gamepad navigation for a Core window (design §9, "Window navigation"). Two levels:
/// <list type="bullet">
/// <item><b>Tabs:</b> D-pad up/down moves along the left tabs (showing each), right steps into that tab's
/// subtabs (left/right along them, left from the first goes back). Confirm enters the page; Back closes
/// the window as usual.</item>
/// <item><b>Page:</b> vanilla's own row navigation. Back returns to the tabs. On a slider, LB/RB move it 1
/// and LT/RT 10, in its own units, repeating when held.</item>
/// </list>
/// Only with a gamepad: mouse and keyboard work as before.
/// </summary>
/// <remarks>
/// [CHANGED] New (window navigation slice). While at the tabs, the page's gamepad panel is locked (its rows
/// don't move) and the tab strips are locked (LB/RB and the triggers no longer cycle tabs; they're the slider
/// buttons now). Back is caught by an exit listener above the window's own (below the confirm dialog's).
/// [SOURCE] Vanilla <c>GameInput</c> (UINavigationDirection; LB/RB = UITabNavigationPrimaryAxis, LT/RT =
/// UITabNavigationSecondaryAxis, found with an in-game probe), <c>UIPanel</c> (IsLocked stops its input),
/// <c>UIContentPanel</c> repeat timing (0.6 s, then 0.11 s), <c>GameInput.RegisterExitListener</c>.
/// [PERFORMANCE] One coroutine while the window is open: a few axis and button reads per frame, and a short
/// parent walk to find a selected slider. Nothing runs while the window is closed.
/// </remarks>
internal sealed class WindowNavigator
{
    private enum Level { Tabs, Subtabs, Page }

    private const float NavThreshold = 0.5f;     // D-pad/stick past half-way counts as a direction
    private const float TriggerThreshold = 0.5f; // a trigger past half-way counts as pressed
    private const float FirstRepeat = 0.6f;      // as vanilla's UIContentPanel
    private const float NextRepeat = 0.11f;
    private const float SmallStep = 1f;          // LB/RB
    private const float BigStep = 10f;           // LT/RT
    private const int ExitPriority = 50;         // above any screen; below ConfirmDialog (100)

    private static readonly MeowcowLogger Log = CoreMod.Log.Tagged("Navigation");

    private readonly MeowcowWindow _window;
    private Level _level = Level.Tabs;
    private int _token;
    private bool _wasGamepad;
    private UIPanel? _lockedPage;
    private GameInput.ExitDelegate? _exitListener; // kept referenced (IL2CPP rule)

    private Vector2Int _navHeld;
    private float _navTimer;
    private float _stepHeld;
    private float _stepTimer;

    internal WindowNavigator(MeowcowWindow window) => _window = window;

    /// <summary>Starts with the window: at the tabs.</summary>
    internal void Start()
    {
        _level = Level.Tabs;
        _wasGamepad = false;
        _navHeld = Vector2Int.zero;
        _stepHeld = 0f;
        _exitListener ??= UIBuilder.ToIl2Cpp<GameInput.ExitDelegate>(new Action<ExitAction>(OnExit));
        GameInput.RegisterExitListener(_exitListener, ExitPriority); // vanilla clears these on scene change
        MelonCoroutines.Start(Run(++_token));
    }

    /// <summary>Stops with the window.</summary>
    internal void Stop()
    {
        _token++;
        if (_exitListener != null) GameInput.DeregisterExitListener(_exitListener);
        UnlockPage();
    }

    // ---------- The loop ----------

    private IEnumerator Run(int token)
    {
        while (token == _token && _window.IsOpen)
        {
            try
            {
                Tick();
            }
            catch (Exception e)
            {
                Log.Error("Navigation failed; stopping it for this window", e);
                Stop();
                yield break;
            }
            yield return null;
        }
    }

    private void Tick()
    {
        bool gamepad = GameInput.GetCurrentInputDeviceIsGamepad();
        if (!gamepad)
        {
            if (_wasGamepad) UnlockPage(); // the mouse uses the page directly
            _wasGamepad = false;
            return;
        }
        if (!_wasGamepad)
        {
            _wasGamepad = true;
            _level = Level.Tabs; // a gamepad picked up mid-window starts at the tabs
            HighlightFocus();
        }
        if (ConfirmDialog.IsOpen) return; // the dialog has the input

        LockStrips();
        WindowTab? tab = CurrentTab();
        if (tab == null) return;

        if (_level == Level.Page)
        {
            StepSlider(tab);
            return;
        }

        LockPage(tab.CurrentPage?.Panel);
        Vector2Int dir = NavDirection();
        if (dir != Vector2Int.zero) Move(tab, dir);
        if (GameInput.GetButtonDown(GameInput.ButtonCode.Submit)) EnterPage(tab);
    }

    // ---------- Tabs and subtabs ----------

    private void Move(WindowTab tab, Vector2Int dir)
    {
        if (_level == Level.Tabs)
        {
            if (dir.y != 0)
            {
                int next = Mathf.Clamp(_window.SelectedTab - dir.y, 0, _window.Tabs.Count - 1); // up is +y
                if (next != _window.SelectedTab) _window.SelectTab(next);
            }
            else if (dir.x > 0 && tab.PageCount > 1)
            {
                _level = Level.Subtabs;
            }
        }
        else // subtabs
        {
            if (dir.x < 0 && tab.SelectedPage <= 0) _level = Level.Tabs;
            else if (dir.x != 0) tab.SelectPage(Mathf.Clamp(tab.SelectedPage + dir.x, 0, tab.PageCount - 1));
        }
        HighlightFocus();
    }

    /// <summary>Confirm: into the page's rows (vanilla picks the entry row).</summary>
    private void EnterPage(WindowTab tab)
    {
        UIPanel? panel = tab.CurrentPage?.Panel;
        if (panel == null) return;
        UnlockPage();
        _level = Level.Page;
        _stepHeld = 0f;
        _window.Screen.uiScreen?.SetCurrentSelectedPanel(panel, null, true, true);
    }

    /// <summary>Back: from a page to the tabs; from the subtabs to the side tabs; at the tabs, not ours.</summary>
    private void OnExit(ExitAction action) => UIBuilder.RunSafely(() =>
    {
        if (action.Used || action.Type != ExitType.Primary || !_window.IsOpen) return;
        if (!GameInput.GetCurrentInputDeviceIsGamepad() || ConfirmDialog.IsOpen) return;
        if (_level == Level.Tabs) return; // the window closes, as before

        action.Used = true;
        _level = Level.Tabs;
        _navHeld = Vector2Int.zero;
        HighlightFocus();
    }, "navigation back");

    /// <summary>Shows vanilla's gamepad selection frame on the focused tab or subtab.</summary>
    private void HighlightFocus()
    {
        WindowTab? tab = CurrentTab();
        EventSystem? events = EventSystem.current;
        if (tab == null || events == null || _level == Level.Page) return;

        GameObject? target = _level == Level.Subtabs ? tab.SubtabButton(tab.SelectedPage) : tab.Toggle.gameObject;
        if (target != null) events.SetSelectedGameObject(target);
    }

    private WindowTab? CurrentTab()
    {
        int index = _window.SelectedTab;
        return index >= 0 && index < _window.Tabs.Count ? _window.Tabs[index] : null;
    }

    // ---------- Sliders ----------

    /// <summary>LB/RB ±1 and LT/RT ±10 on the selected slider, repeating when held.</summary>
    private void StepSlider(WindowTab tab)
    {
        float bumper = GameInput.UITabNavigationPrimaryAxis;
        float trigger = GameInput.UITabNavigationSecondaryAxis;
        float step = bumper != 0f ? Mathf.Sign(bumper) * SmallStep
            : Mathf.Abs(trigger) > TriggerThreshold ? Mathf.Sign(trigger) * BigStep
            : 0f;

        if (step == 0f)
        {
            _stepHeld = 0f;
            return;
        }
        if (step != _stepHeld)
        {
            _stepHeld = step;
            _stepTimer = FirstRepeat;
        }
        else
        {
            _stepTimer -= Time.unscaledDeltaTime;
            if (_stepTimer > 0f) return;
            _stepTimer = NextRepeat;
        }

        Slider? slider = SelectedSlider(tab.CurrentPage);
        // Setting the value fires the slider's own change event: the row applies it as if dragged.
        if (slider != null) slider.value = Mathf.Clamp(slider.value + step, slider.minValue, slider.maxValue);
    }

    /// <summary>The slider in the selected row, or null if the selection isn't a slider row.</summary>
    private static Slider? SelectedSlider(WindowPage? page)
    {
        GameObject? selected = EventSystem.current?.currentSelectedGameObject;
        if (page == null || selected == null) return null;

        // Up to the row (a child of the page's row list), then its slider.
        Transform row = selected.transform;
        while (row.parent != null && row.parent != page.Rows) row = row.parent;
        if (row.parent != page.Rows) return null;
        var sliders = row.GetComponentsInChildren<Slider>(true); // (the plural: safe under Il2CppInterop)
        return sliders.Length > 0 ? sliders[0] : null;
    }

    // ---------- Input ----------

    /// <summary>The D-pad/stick direction this frame, once per press and then on vanilla's repeat timing.</summary>
    private Vector2Int NavDirection()
    {
        Vector2 raw = GameInput.UINavigationDirection;
        Vector2Int dir = Vector2Int.zero;
        if (Mathf.Abs(raw.x) > Mathf.Abs(raw.y)) { if (Mathf.Abs(raw.x) > NavThreshold) dir.x = raw.x > 0f ? 1 : -1; }
        else if (Mathf.Abs(raw.y) > NavThreshold) dir.y = raw.y > 0f ? 1 : -1;

        if (dir == Vector2Int.zero)
        {
            _navHeld = Vector2Int.zero;
            return dir;
        }
        if (dir != _navHeld)
        {
            _navHeld = dir;
            _navTimer = FirstRepeat;
            return dir;
        }
        _navTimer -= Time.unscaledDeltaTime;
        if (_navTimer > 0f) return Vector2Int.zero;
        _navTimer = NextRepeat;
        return dir;
    }

    // ---------- Locks ----------

    /// <summary>
    /// The tab strips stay locked: LB/RB and the triggers are slider buttons now. Re-applied every frame,
    /// because the screen unlocks all its panels whenever it's selected again.
    /// </summary>
    private void LockStrips()
    {
        _window.TabStrip.IsLocked = true;
        UIPanel? subtabs = CurrentTab()?.Strip;
        if (subtabs != null) subtabs.IsLocked = true;
    }

    /// <summary>Locks the shown page's panel while at the tabs (unlocking a previously locked one).</summary>
    private void LockPage(UIPanel? panel)
    {
        if (_lockedPage != null && _lockedPage.Pointer == panel?.Pointer) return;
        UnlockPage();
        if (panel == null) return;
        panel.IsLocked = true;
        _lockedPage = panel;
    }

    private void UnlockPage()
    {
        if (_lockedPage != null) _lockedPage.IsLocked = false;
        _lockedPage = null;
    }
}