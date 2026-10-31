using System;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MeowcowCore.SuiteUI;

/// <summary>
/// Tells Core when the mouse enters or leaves a UI object (a whole row: its label, icon, controls and the
/// gaps between them). Pointer events bubble up from whatever is under the mouse, so one of these on the
/// row covers everything in it.
/// </summary>
/// <remarks>
/// [CHANGED] New (Settings rework slice 3), for the description panel. Vanilla turns hover into selection
/// only on the control itself (UISelectable.OnPointerEnter), and only when its panel is navigable, so a
/// row's label never counted and the page's auto-selected first row looked like a hover.
/// Only enter and exit are handled. <c>EventTrigger</c> would also work, but it implements every pointer
/// interface (scroll, drag, click), so on a row inside a scroll view it would swallow the mouse wheel and
/// the page would stop scrolling while hovering rows.
/// [SOURCE] Il2CppInterop's <c>ClassInjector</c> with injected interfaces (standard IL2CPP modding practice);
/// <c>IPointerEnterHandler</c>/<c>IPointerExitHandler</c> are Unity's own.
/// [PERFORMANCE] Event-driven only: nothing runs per frame. The optional hit area is a transparent image that
/// isn't drawn (cullTransparentMesh), so it costs one raycast target and no rendering.
/// </remarks>
internal sealed class RowHover : MonoBehaviour
{
    private static bool _registered;

    private Action? _onEnter;
    private Action? _onExit;

    /// <summary>Required by Il2CppInterop: native code creates the object, then wraps it.</summary>
    public RowHover(IntPtr pointer) : base(pointer) { }

    /// <summary>
    /// Registers the type with IL2CPP, with the two pointer interfaces Unity looks for. Once per run, before
    /// any row is built (called at Core start).
    /// </summary>
    internal static void Register()
    {
        if (_registered) return;
        ClassInjector.RegisterTypeInIl2Cpp<RowHover>(new RegisterTypeOptions
        {
            // [CHANGED] IEventSystemHandler listed too: Unity first gathers a GameObject's components as that
            // base interface, and IL2CPP keeps a class's interface list flat (Il2CppInterop adds only what's
            // named), so without it the component was silently skipped and hover never fired.
            Interfaces = new[] { typeof(IEventSystemHandler), typeof(IPointerEnterHandler), typeof(IPointerExitHandler) },
        });
        _registered = true;
    }

    /// <summary>
    /// Adds hover handling to <paramref name="target"/>. If nothing on the object itself can be hit by the
    /// mouse, a transparent hit area is added so the gaps between its children count too.
    /// </summary>
    [HideFromIl2Cpp]
    internal static RowHover? Attach(GameObject target, Action onEnter, Action onExit)
    {
        if (!_registered) return null;

        if (target.GetComponent<Graphic>() == null)
        {
            Image hitArea = target.AddComponent<Image>();
            hitArea.color = Color.clear;
            hitArea.raycastTarget = true;
            hitArea.canvasRenderer.cullTransparentMesh = true; // invisible: skip drawing it entirely
        }

        RowHover hover = target.GetComponent<RowHover>() ?? target.AddComponent<RowHover>();
        hover._onEnter = onEnter;
        hover._onExit = onExit;
        return hover;
    }

    // Called by Unity's event system (native code): exceptions must never escape.

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_onEnter != null) UIBuilder.RunSafely(_onEnter, "Row hover");
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_onExit != null) UIBuilder.RunSafely(_onExit, "Row hover");
    }

    /// <summary>
    /// A row hidden or destroyed under the mouse (page switched, window closed) gets no exit event, so it's
    /// sent here. Public so Il2CppInterop exposes it to Unity as a message.
    /// </summary>
    public void OnDisable()
    {
        if (_onExit != null) UIBuilder.RunSafely(_onExit, "Row hover");
    }
}