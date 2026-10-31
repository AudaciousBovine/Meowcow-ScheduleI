using System;
using System.Collections;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppTMPro;
using MelonLoader;
using MeowcowCore.Foundation;
using UnityEngine;
using UnityEngine.UI;
using UIPanel = Il2CppScheduleOne.UIPanel;
using UObject = UnityEngine.Object;

namespace MeowcowCore.SuiteUI;

/// <summary>
/// A "keep these changes?" box over a window, counting down to an automatic revert: Keep or Revert by mouse,
/// Back (Escape/B) or the timer reverts, Submit (Enter/A) keeps. One open at a time.
/// </summary>
/// <remarks>
/// [CHANGED] New (Settings rebuild R3), for display settings: vanilla's own revert dialog lives inside its
/// (hidden) Settings screen. Reusable by any window, e.g. a module's "keep these changes?" prompt.
/// While open, a dimmed cover takes the mouse and the window's selected gamepad panel is locked, so nothing
/// behind it can change.
/// [SOURCE] Vanilla <c>ConfirmDisplaySettings</c> (15 s countdown, Back reverts, "Reverting in {0:0.0}
/// seconds"), <c>GameInput.RegisterExitListener</c> (higher priority runs first; <c>Used</c> stops the rest).
/// [PERFORMANCE] Built once per window, on first use. While open, one coroutine updates the countdown text
/// each frame; nothing runs while closed.
/// </remarks>
public static class ConfirmDialog
{
    private const float BoxWidth = 560f;
    private const float BoxHeight = 190f;
    private const float Padding = 20f;
    private const float ButtonHeight = 40f;
    private const float ButtonGap = 20f;
    private const float TitleRatio = 0.6f;    // of the window title's font size
    private const float SubtitleRatio = 0.45f;
    private const float SubmitGrace = 0.25f;  // ignore the press that caused the dialog (e.g. A on a dropdown)
    private const int ExitPriority = 100;     // above every screen (vanilla's own dialog uses 6)
    private const string HolderName = MeowcowWindow.CoreAdditionPrefix + "ConfirmDialog";

    private static readonly Color Dim = new(0f, 0f, 0f, 0.55f);
    private static readonly Color BoxColor = new(0.12f, 0.12f, 0.12f, 0.97f);

    private static readonly MeowcowLogger Log = CoreMod.Log.Tagged("Dialog");

    // The open dialog (at most one).
    private static GameObject? _open;
    private static TMP_Text? _subtitle;
    private static Action? _onKeep;
    private static Action? _onRevert;
    private static string _countdownFormat = "";
    private static float _remaining;
    private static float _openedAt;
    private static int _token; // bumped on every open and close, so an old countdown stops
    private static UIPanel? _lockedPanel;
    private static GameInput.ExitDelegate? _exitListener; // kept referenced (IL2CPP rule)

    /// <summary>Whether a dialog is open.</summary>
    public static bool IsOpen => _open != null && _open.activeSelf;

    /// <summary>
    /// Shows the dialog over <paramref name="window"/>. <paramref name="countdownFormat"/> gets the seconds left
    /// as {0} (e.g. "Reverting in {0:0.0} seconds"). Exactly one of the two actions runs.
    /// </summary>
    public static void Show(MeowcowWindow window, string title, string countdownFormat, float seconds,
        Action onKeep, Action onRevert)
    {
        if (IsOpen) Close(revert: true); // a new question replaces the old one, safely

        GameObject? holder = Build(window, title);
        if (holder == null)
        {
            Log.Warning("Couldn't show the confirm dialog; keeping the change");
            UIBuilder.RunSafely(onKeep, "dialog keep");
            return;
        }

        _open = holder;
        _onKeep = onKeep;
        _onRevert = onRevert;
        _countdownFormat = countdownFormat;
        _remaining = seconds;
        _openedAt = Time.unscaledTime;
        holder.transform.SetAsLastSibling();
        holder.SetActive(true);
        LockGamepad(window);

        _exitListener ??= UIBuilder.ToIl2Cpp<GameInput.ExitDelegate>(new Action<ExitAction>(OnExit));
        GameInput.RegisterExitListener(_exitListener, ExitPriority); // vanilla clears these on scene change

        MelonCoroutines.Start(Countdown(++_token));
    }

    // ---------- Building ----------

    /// <summary>The window's dialog (made on first use), with this title.</summary>
    private static GameObject? Build(MeowcowWindow window, string title)
    {
        if (!window.IsAlive || window.Title == null) return null;
        Transform frame = window.Content.parent ?? window.Content;

        Transform? existing = frame.Find(HolderName);
        if (existing != null)
        {
            Transform? titleObject = existing.Find("Box/Title");
            TMP_Text? existingTitle = titleObject != null ? titleObject.GetComponent<TMP_Text>() : null; // [CHANGED] renamed (build fix: clashed with the build branch's titleText)
            if (existingTitle != null) existingTitle.text = title;
            Transform? subtitleObject = existing.Find("Box/Subtitle");
            _subtitle = subtitleObject != null ? subtitleObject.GetComponent<TMP_Text>() : null;
            return existing.gameObject;
        }

        // The cover: fills the window, dimmed, takes the mouse.
        var holder = new GameObject(HolderName);
        RectTransform holderRect = holder.AddComponent<RectTransform>();
        holderRect.SetParent(frame, false);
        MeowcowWindow.SetRect(holderRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image dim = holder.AddComponent<Image>();
        dim.color = Dim;
        dim.raycastTarget = true;
        holder.SetActive(false);

        // The box, centred.
        var box = new GameObject("Box");
        RectTransform boxRect = box.AddComponent<RectTransform>();
        boxRect.SetParent(holderRect, false);
        boxRect.anchorMin = boxRect.anchorMax = boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.sizeDelta = new Vector2(BoxWidth, BoxHeight);
        Image boxImage = box.AddComponent<Image>();
        boxImage.color = BoxColor;

        TMP_Text titleText = AddText(window.Title, boxRect, "Title", title, TitleRatio,
            new Vector2(0f, 1f), new Vector2(1f, 1f), -Padding);
        _subtitle = AddText(window.Title, boxRect, "Subtitle", "", SubtitleRatio,
            new Vector2(0f, 1f), new Vector2(1f, 1f), -Padding - titleText.fontSize * 1.4f);
        _subtitle.color = new Color(1f, 1f, 1f, 0.8f);

        AddButton(boxRect, "Keep", "Keep", OnKeepClicked, -(ButtonGap / 2f), rightAligned: true);
        AddButton(boxRect, "Revert", "Revert", OnRevertClicked, ButtonGap / 2f, rightAligned: false);
        return holder;
    }

    private static TMP_Text AddText(TMP_Text style, RectTransform box, string name, string text, float ratio,
        Vector2 anchorMin, Vector2 anchorMax, float top)
    {
        TMP_Text label = UIBuilder.CloneText(style, name, text);
        label.transform.SetParent(box, false);
        label.fontSize = style.fontSize * ratio;
        label.horizontalAlignment = HorizontalAlignmentOptions.Center;
        label.verticalAlignment = VerticalAlignmentOptions.Top;
        label.enableWordWrapping = true;
        label.raycastTarget = false;
        RectTransform rect = label.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(Padding, 0f);
        rect.offsetMax = new Vector2(-Padding, 0f);
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, label.fontSize * 1.4f);
        rect.anchoredPosition = new Vector2(0f, top);
        return label;
    }

    /// <summary>A small button along the box's bottom, either side of its centre.</summary>
    private static void AddButton(RectTransform box, string name, string label, Action onClick, float x, bool rightAligned)
    {
        GameObject? go = UITemplateRegistry.Create(UITemplateRegistry.SmallButton, box, name);
        if (go == null) return; // Back and Submit still work
        float width = UITemplateRegistry.FitToLabel(go, label);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(rightAligned ? 1f : 0f, 0f);
        rect.sizeDelta = new Vector2(width, ButtonHeight);
        rect.anchoredPosition = new Vector2(x, Padding);
        UIBuilder.SetClick(go.GetComponent<Button>(), onClick);
    }

    // ---------- Running ----------

    private static IEnumerator Countdown(int token)
    {
        while (token == _token && IsOpen)
        {
            _remaining -= Time.unscaledDeltaTime;
            if (_subtitle != null) _subtitle.text = string.Format(_countdownFormat, Mathf.Max(0f, _remaining));

            if (_remaining <= 0f)
            {
                Close(revert: true);
                yield break;
            }
            if (Time.unscaledTime - _openedAt > SubmitGrace && GameInput.GetButtonDown(GameInput.ButtonCode.Submit))
            {
                Close(revert: false);
                yield break;
            }
            yield return null;
        }
    }

    // Called from native code (button clicks, Back): exceptions must never escape.
    private static void OnKeepClicked() => UIBuilder.RunSafely(() => Close(revert: false), "dialog keep");
    private static void OnRevertClicked() => UIBuilder.RunSafely(() => Close(revert: true), "dialog revert");

    private static void OnExit(ExitAction action) => UIBuilder.RunSafely(() =>
    {
        if (action.Used || !IsOpen || action.Type != ExitType.Primary) return;
        action.Used = true; // the window behind stays open
        Close(revert: true);
    }, "dialog back");

    /// <summary>Closes the dialog and runs its keep or revert action.</summary>
    private static void Close(bool revert)
    {
        _token++;
        Action? action = revert ? _onRevert : _onKeep;
        _onKeep = null;
        _onRevert = null;

        if (_open != null) _open.SetActive(false);
        _open = null;
        if (_exitListener != null) GameInput.DeregisterExitListener(_exitListener);
        UnlockGamepad();

        if (action != null) UIBuilder.RunSafely(action, revert ? "dialog revert" : "dialog keep");
    }

    /// <summary>Locks the window's selected gamepad panel, so Submit/D-pad don't reach the rows behind.</summary>
    private static void LockGamepad(MeowcowWindow window)
    {
        UIPanel? panel = window.Screen.uiScreen?.CurrentSelectedPanel;
        if (panel == null || panel.IsLocked) return;
        panel.IsLocked = true;
        _lockedPanel = panel;
    }

    private static void UnlockGamepad()
    {
        if (_lockedPanel != null) _lockedPanel.IsLocked = false;
        _lockedPanel = null;
    }
}