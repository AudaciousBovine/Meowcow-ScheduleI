using System;
using System.Collections;
using System.Collections.Generic;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppTMPro;
using MelonLoader;
using MeowcowCore.Foundation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UIOption = Il2CppScheduleOne.UIOption;
using UIPanel = Il2CppScheduleOne.UIPanel;
using UIScreenManager = Il2CppScheduleOne.UIScreenManager;
using UISelectable = Il2CppScheduleOne.UISelectable;

namespace MeowcowCore.SuiteUI;

/// <summary>
/// Gamepad navigation for a Core window (design §9, "Window navigation"). Core controls the selection itself;
/// vanilla's gamepad system is detached from the window (see MeowcowWindow).
/// <list type="bullet">
/// <item><b>Tab control:</b> D-pad up/down cycles the left tabs, left/right the subtabs. Confirm enters the
/// page's options; Back closes the window.</item>
/// <item><b>Options:</b> D-pad up/down moves between rows. Confirm flips a toggle, presses a button or opens
/// a dropdown; on a slider, LB/RB move it 1 and LT/RT 10. Back returns to tab control.</item>
/// <item><b>Open dropdown:</b> D-pad up/down moves through its list; Confirm picks and closes; Back closes
/// without a change.</item>
/// </list>
/// Only with a gamepad: mouse and keyboard work as before.
/// </summary>
/// <remarks>
/// [CHANGED] Rewritten (Evelyna's scheme). The first version drove only the tab level and left rows to
/// vanilla's navigation, which wandered into the lobby panel and lost the selection; it also read the wrong
/// Submit (GameInput's, not the UI's). Now every move is ours, from the page's own row list in order.
/// Selection goes through Unity's event system, so vanilla's selection frame and the description panel work
/// as before. Vanilla's per-row scripts that read the D-pad (UIOption: sliders moved on left/right) are
/// switched off on the window's rows; their value wiring is unaffected.
/// [SOURCE] Vanilla <c>GameInput</c> (UINavigationDirection; LB/RB = UITabNavigationPrimaryAxis, LT/RT =
/// UITabNavigationSecondaryAxis, probed in game), <c>UIScreenManager.SubmitInputAction</c> (vanilla's UI
/// Confirm), <c>UIOption.DetectInput</c>, <c>UIPanel.ScrollToChild</c> (keeping the selection in view),
/// repeat timing 0.6 s then 0.11 s (<c>UIScreenManager.NavigationRepeatDelay/Rate</c>).
/// [PERFORMANCE] One coroutine while the window is open: a few axis and button reads per frame. Row lists
/// are read on a move, not per frame. Nothing runs while the window is closed.
/// </remarks>
internal sealed class WindowNavigator
{
    private enum Level { Tabs, Options, Dropdown }

    private const float NavThreshold = 0.5f;     // D-pad/stick past half-way counts as a direction
    private const float TriggerThreshold = 0.5f; // a trigger past half-way counts as pressed
    private const float FirstRepeat = 0.6f;      // UIScreenManager.NavigationRepeatDelay
    private const float NextRepeat = 0.11f;      // UIScreenManager.NavigationRepeatRate
    private const float SmallStep = 1f;          // LB/RB
    private const float BigStep = 10f;           // LT/RT
    private const float ScrollMargin = 10f;      // room kept around a selected row when scrolling to it
    private const int ExitPriority = 50;         // above any screen; below ConfirmDialog (100)

    private static readonly MeowcowLogger Log = CoreMod.Log.Tagged("Navigation");

    private readonly MeowcowWindow _window;
    private Level _level = Level.Tabs;
    private int _token;
    private bool _wasGamepad;
    private bool _optionsSilenced;
    private GameInput.ExitDelegate? _exitListener; // kept referenced (IL2CPP rule)

    private int _row;               // index into the page's selectable rows
    private TMP_Dropdown? _dropdown; // the open dropdown, at the Dropdown level
    private int _item;              // index into its list

    private Vector2Int _navHeld;
    private float _navTimer;
    private float _stepHeld;
    private float _stepTimer;

    internal WindowNavigator(MeowcowWindow window) => _window = window;

    /// <summary>Starts with the window: at tab control.</summary>
    internal void Start()
    {
        _level = Level.Tabs;
        _wasGamepad = false;
        _navHeld = Vector2Int.zero;
        _stepHeld = 0f;
        _dropdown = null;
        SilenceRowScripts();
        _exitListener ??= UIBuilder.ToIl2Cpp<GameInput.ExitDelegate>(new Action<ExitAction>(OnExit));
        GameInput.RegisterExitListener(_exitListener, ExitPriority); // vanilla clears these on scene change
        MelonCoroutines.Start(Run(++_token));
        Log.Debug("Started");
    }

    /// <summary>Stops with the window.</summary>
    internal void Stop()
    {
        _token++;
        if (_exitListener != null) GameInput.DeregisterExitListener(_exitListener);
        if (_dropdown != null && _dropdown.IsExpanded) _dropdown.Hide();
        _dropdown = null;
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
        LockStrips(); // always: the bumpers and triggers never cycle tabs

        if (!GameInput.GetCurrentInputDeviceIsGamepad())
        {
            _wasGamepad = false;
            return;
        }
        if (!_wasGamepad)
        {
            _wasGamepad = true;
            SetLevel(Level.Tabs);
        }
        if (ConfirmDialog.IsOpen) return; // the dialog has the input

        switch (_level)
        {
            case Level.Tabs: TickTabs(); break;
            case Level.Options: TickOptions(); break;
            case Level.Dropdown: TickDropdown(); break;
        }
    }

    // ---------- Tab control ----------

    private void TickTabs()
    {
        WindowTab? tab = CurrentTab();
        if (tab == null) return;

        Vector2Int dir = NavDirection();
        if (dir.y != 0)
        {
            int next = Mathf.Clamp(_window.SelectedTab - dir.y, 0, _window.Tabs.Count - 1); // up is +y
            if (next != _window.SelectedTab) _window.SelectTab(next);
            HighlightTab();
        }
        else if (dir.x != 0 && tab.PageCount > 1)
        {
            tab.SelectPage(Mathf.Clamp(tab.SelectedPage + dir.x, 0, tab.PageCount - 1));
            HighlightTab();
        }

        if (ConfirmPressed()) EnterOptions(tab);
    }

    private void EnterOptions(WindowTab tab)
    {
        List<UISelectable> rows = Rows(tab.CurrentPage);
        if (rows.Count == 0) return; // nothing to change on this page
        _row = 0;
        SetLevel(Level.Options);
    }

    // ---------- Options ----------

    private void TickOptions()
    {
        WindowPage? page = CurrentTab()?.CurrentPage;
        if (page == null) return;
        List<UISelectable> rows = Rows(page);
        if (rows.Count == 0)
        {
            SetLevel(Level.Tabs);
            return;
        }
        _row = Mathf.Clamp(_row, 0, rows.Count - 1);

        Vector2Int dir = NavDirection();
        if (dir.y != 0)
        {
            int next = Mathf.Clamp(_row - dir.y, 0, rows.Count - 1);
            if (next != _row)
            {
                _row = next;
                SelectRow(page, rows[_row]);
            }
        }

        StepSlider(page, rows[_row]);
        if (ConfirmPressed()) Activate(page, rows[_row]);
    }

    /// <summary>Confirm on a row: flip a toggle, press a button, or open a dropdown.</summary>
    private void Activate(WindowPage page, UISelectable selected)
    {
        Transform row = RowOf(page, selected.transform);

        var dropdowns = row.GetComponentsInChildren<TMP_Dropdown>(true);
        if (dropdowns.Length > 0)
        {
            OpenDropdown(dropdowns[0]);
            return;
        }

        var toggles = row.GetComponentsInChildren<Toggle>(); // active ones only
        if (toggles.Length > 0)
        {
            if (toggles[0].interactable) toggles[0].isOn = !toggles[0].isOn;
            return;
        }

        var buttons = row.GetComponentsInChildren<Button>();
        if (buttons.Length > 0 && buttons[0].interactable) buttons[0].onClick.Invoke();
    }

    /// <summary>LB/RB ±1 and LT/RT ±10 on the selected row's slider, repeating when held.</summary>
    private void StepSlider(WindowPage page, UISelectable selected)
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

        var sliders = RowOf(page, selected.transform).GetComponentsInChildren<Slider>(true);
        if (sliders.Length == 0) return;
        Slider slider = sliders[0];
        // Setting the value fires the slider's own change event: the row applies it as if dragged.
        slider.value = Mathf.Clamp(slider.value + step, slider.minValue, slider.maxValue);
    }

    // ---------- Open dropdown ----------

    private void OpenDropdown(TMP_Dropdown dropdown)
    {
        dropdown.Show();
        _dropdown = dropdown;
        _item = dropdown.value;
        SetLevel(Level.Dropdown);
    }

    private void TickDropdown()
    {
        if (_dropdown == null || !_dropdown.IsExpanded)
        {
            SetLevel(Level.Options); // closed some other way (a click)
            return;
        }
        Toggle[] items = Items(_dropdown);
        if (items.Length == 0) return;
        _item = Mathf.Clamp(_item, 0, items.Length - 1);

        Vector2Int dir = NavDirection();
        if (dir.y != 0)
        {
            int next = Mathf.Clamp(_item - dir.y, 0, items.Length - 1);
            if (next != _item)
            {
                _item = next;
                SelectItem(items[_item]);
            }
        }

        if (ConfirmPressed())
        {
            items[_item].isOn = true; // vanilla's item handler sets the value and closes the list
            if (_dropdown.IsExpanded) _dropdown.Hide();
            SetLevel(Level.Options);
        }
    }

    /// <summary>The open list's items, in order (the hidden template item excluded).</summary>
    private static Toggle[] Items(TMP_Dropdown dropdown)
    {
        Transform? list = dropdown.transform.Find("Dropdown List");
        if (list == null) return Array.Empty<Toggle>();
        var found = list.GetComponentsInChildren<Toggle>(); // active ones only
        var items = new Toggle[found.Length];
        for (int i = 0; i < found.Length; i++) items[i] = found[i];
        return items;
    }

    private static void SelectItem(Toggle item)
    {
        EventSystem.current?.SetSelectedGameObject(item.gameObject);
        ScrollRect? scroll = FindScrollAbove(item.transform);
        if (scroll != null) ScrollIntoView(scroll, item.GetComponent<RectTransform>());
    }

    // ---------- Back ----------

    /// <summary>Back: closes an open dropdown, or leaves the options for tab control; at tab control, not ours.</summary>
    private void OnExit(ExitAction action) => UIBuilder.RunSafely(() =>
    {
        if (action.Used || action.Type != ExitType.Primary || !_window.IsOpen) return;
        if (!GameInput.GetCurrentInputDeviceIsGamepad() || ConfirmDialog.IsOpen) return;
        if (_level == Level.Tabs) return; // the window closes, as before

        action.Used = true;
        if (_level == Level.Dropdown)
        {
            if (_dropdown != null && _dropdown.IsExpanded) _dropdown.Hide(); // no change
            SetLevel(Level.Options);
        }
        else
        {
            SetLevel(Level.Tabs);
        }
    }, "navigation back");

    // ---------- Levels and selection ----------

    private void SetLevel(Level level)
    {
        _level = level;
        _navHeld = Vector2Int.zero; // a held direction doesn't carry into the new level
        _stepHeld = 0f;

        switch (level)
        {
            case Level.Tabs:
                _dropdown = null;
                HighlightTab();
                break;
            case Level.Options:
                _dropdown = null;
                WindowPage? page = CurrentTab()?.CurrentPage;
                List<UISelectable> rows = Rows(page);
                if (page != null && rows.Count > 0) SelectRow(page, rows[Mathf.Clamp(_row, 0, rows.Count - 1)]);
                break;
            case Level.Dropdown:
                if (_dropdown == null) break;
                Toggle[] items = Items(_dropdown);
                if (items.Length > 0) SelectItem(items[Mathf.Clamp(_item, 0, items.Length - 1)]);
                break;
        }
        Log.Debug($"Level: {level}");
    }

    /// <summary>Vanilla's gamepad selection frame on the current left tab.</summary>
    private void HighlightTab()
    {
        WindowTab? tab = CurrentTab();
        if (tab != null) EventSystem.current?.SetSelectedGameObject(tab.Toggle.gameObject);
    }

    /// <summary>Selects a row (its frame and description show) and scrolls it into view.</summary>
    private static void SelectRow(WindowPage page, UISelectable row)
    {
        EventSystem.current?.SetSelectedGameObject(row.gameObject);
        if (page.Scroll != null) ScrollIntoView(page.Scroll, RowOf(page, row.transform).GetComponent<RectTransform>());
    }

    private WindowTab? CurrentTab()
    {
        int index = _window.SelectedTab;
        return index >= 0 && index < _window.Tabs.Count ? _window.Tabs[index] : null;
    }

    /// <summary>The page's selectable rows, in the order they were added (top to bottom), shown ones only.</summary>
    private static List<UISelectable> Rows(WindowPage? page)
    {
        var rows = new List<UISelectable>();
        if (page?.Panel == null) return rows;
        foreach (UISelectable selectable in page.Panel.selectables)
        {
            if (selectable != null && selectable.gameObject.activeInHierarchy) rows.Add(selectable);
        }
        return rows;
    }

    /// <summary>The row (child of the page's row list) holding <paramref name="t"/>; t itself if outside it.</summary>
    private static Transform RowOf(WindowPage page, Transform t)
    {
        Transform row = t;
        while (row.parent != null && row.parent != page.Rows) row = row.parent;
        return row.parent == page.Rows ? row : t;
    }

    // ---------- Scrolling ----------

    /// <summary>Scrolls just enough to show <paramref name="child"/> (with a margin). As vanilla's ScrollToChild, instantly.</summary>
    private static void ScrollIntoView(ScrollRect scroll, RectTransform? child)
    {
        if (child == null || scroll.content == null) return;
        RectTransform viewport = scroll.viewport != null ? scroll.viewport : scroll.GetComponent<RectTransform>();
        Canvas.ForceUpdateCanvases();

        Rect view = viewport.rect;
        Vector2 min = viewport.InverseTransformPoint(child.TransformPoint(child.rect.min));
        Vector2 max = viewport.InverseTransformPoint(child.TransformPoint(child.rect.max));
        float shift = 0f;
        if (max.y + ScrollMargin > view.yMax) shift = max.y + ScrollMargin - view.yMax;        // above: scroll up
        else if (min.y - ScrollMargin < view.yMin) shift = min.y - ScrollMargin - view.yMin;   // below: scroll down
        if (shift == 0f) return;

        Vector3 local = scroll.content.localPosition;
        local.y -= shift;
        scroll.content.localPosition = local;
    }

    private static ScrollRect? FindScrollAbove(Transform t)
    {
        for (Transform? p = t.parent; p != null; p = p.parent)
        {
            ScrollRect? scroll = p.GetComponent<ScrollRect>();
            if (scroll != null) return scroll;
        }
        return null;
    }

    // ---------- Input ----------

    /// <summary>Vanilla's UI Confirm (A), pressed this frame.</summary>
    private static bool ConfirmPressed()
    {
        var manager = Singleton<UIScreenManager>.Instance;
        var action = manager != null ? manager.SubmitInputAction?.action : null;
        return action != null && action.WasPressedThisFrame();
    }

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

    // ---------- Vanilla's own input, kept out ----------

    /// <summary>
    /// The tab strips stay locked, so vanilla's tab cycling (LB/RB, triggers) never runs: those buttons are
    /// the slider buttons now. Every frame, since nothing else should unlock them but it's cheap to be sure.
    /// </summary>
    private void LockStrips()
    {
        _window.TabStrip.IsLocked = true;
        UIPanel? subtabs = CurrentTab()?.Strip;
        if (subtabs != null) subtabs.IsLocked = true;
    }

    /// <summary>
    /// Switches off vanilla's per-row input scripts (UIOption: they move sliders and toggles on D-pad
    /// left/right while selected). Their value wiring is untouched: a slider's change still reaches its row.
    /// Once per window: rows are built before it first opens.
    /// </summary>
    private void SilenceRowScripts()
    {
        if (_optionsSilenced) return;
        _optionsSilenced = true;
        int count = 0;
        foreach (UIOption option in _window.Content.GetComponentsInChildren<UIOption>(true))
        {
            if (option == null) continue;
            option.enabled = false;
            count++;
        }
        Log.Debug($"Silenced {count} vanilla row scripts");
    }
}