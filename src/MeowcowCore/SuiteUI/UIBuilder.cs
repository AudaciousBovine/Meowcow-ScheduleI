using System;
using System.Collections.Generic;
using System.Text;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppTMPro;
using MeowcowCore.Foundation;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UIPanel = Il2CppScheduleOne.UIPanel;
using UISelectable = Il2CppScheduleOne.UISelectable;
using UObject = UnityEngine.Object;

namespace MeowcowCore.SuiteUI;

/// <summary>
/// The only way mods create UI. Clones vanilla pieces for their look, then makes them the suite's own:
/// vanilla wiring removed, gamepad navigation kept working.
/// </summary>
/// <remarks>
/// [CHANGED] New (design §7). Only what the main menu needs for now: list buttons, text labels, click
/// handlers and two layout helpers. More arrives with each menu that needs it.
/// [SOURCE] Ported from the pre-rebuild <c>OptionsButton</c> and <c>UiTools</c> (see the findings doc).
/// </remarks>
public static class UIBuilder
{
    private const float ListLabelMargin = 10f;  // space after a list label, so it never touches the edge
    private const float DefaultHoverSlide = 15f; // used if a button has no highlighted-position child

    private static readonly MeowcowLogger Log = CoreMod.Log.Tagged("UI");

    // IL2CPP rule: converted delegates must stay referenced. Cached per handler, so the same handler
    // reused on every menu load converts once instead of piling up.
    // [PERFORMANCE] One dictionary lookup per click handler set.
    private static readonly Dictionary<Action, UnityAction> ClickHandlers = new();

    /// <summary>
    /// Adds a button to a vanilla menu list, right after <paramref name="after"/>, cloned from it.
    /// The list grows by one button (top edge stays put) and widens if the label needs it, and the
    /// button is added to the list's gamepad navigation.
    /// </summary>
    /// <exception cref="InvalidOperationException">The button isn't in a list.</exception>
    /// <remarks>[PERFORMANCE] One clone, one layout pass over the list's buttons when it widens.</remarks>
    public static Button AddListButton(Transform after, string name, string label, Action onClick)
    {
        RectTransform list = after.parent?.TryCast<RectTransform>()
            ?? throw new InvalidOperationException($"'{after.name}' isn't in a list");

        GameObject go = UObject.Instantiate(after.gameObject, list);
        go.name = name;
        go.transform.SetSiblingIndex(after.GetSiblingIndex() + 1);

        Button button = go.GetComponent<Button>();
        SetClick(button, onClick);

        TMP_Text? text = go.transform.Find(UIPathIndex.ListButtonText)?.GetComponent<TMP_Text>();
        if (text != null)
        {
            text.text = label;
            text.enableWordWrapping = false;
        }

        GrowList(list, after, text);
        AddToGamepadNavigation(list, after, go);
        return button;
    }

    /// <summary>
    /// A copy of a text element (same font, size and style) with new text, under the same parent.
    /// Every script except the text itself is removed, so nothing overwrites it.
    /// </summary>
    /// <remarks>
    /// Removing the scripts matters: the version text's script would write the game version back.
    /// [SOURCE] Findings doc §2.
    /// </remarks>
    public static TMP_Text CloneText(TMP_Text source, string name, string text)
    {
        GameObject go = UObject.Instantiate(source.gameObject, source.transform.parent);
        go.name = name;

        foreach (MonoBehaviour behaviour in go.GetComponents<MonoBehaviour>())
        {
            if (behaviour.TryCast<TMP_Text>() == null) UObject.DestroyImmediate(behaviour);
        }

        TMP_Text clone = go.GetComponent<TMP_Text>();
        clone.text = text;
        clone.enableWordWrapping = false;
        clone.overflowMode = TextOverflowModes.Overflow;
        return clone;
    }

    /// <summary>
    /// A copy of a button that keeps only its look: every script except graphics and the button itself is
    /// removed (so e.g. the Import button no longer imports), its label is set, and clicks run
    /// <paramref name="onClick"/>.
    /// </summary>
    /// <remarks>
    /// [CHANGED] New for the window's Back button. Vanilla's gamepad selectable is removed too: a button
    /// like Back is reached with Escape or B instead.
    /// [SOURCE] Findings doc §2 (strip behaviours, keep the look).
    /// [PERFORMANCE] One clone and one pass over its components.
    /// </remarks>
    public static Button CloneButton(Button source, Transform parent, string name, string label, Action onClick)
    {
        GameObject go = UObject.Instantiate(source.gameObject, parent);
        go.name = name;
        StripToLook(go);

        var labels = go.GetComponentsInChildren<TMP_Text>(true);
        if (labels.Length > 0)
        {
            labels[0].text = label;
            labels[0].enableWordWrapping = false;
        }

        Button button = go.GetComponent<Button>();
        SetClick(button, onClick);
        return button;
    }

    /// <summary>
    /// Removes every script except graphics and Unity's controls: what's left is only how it looks and that
    /// it can be clicked. For simple pieces like buttons.
    /// </summary>
    /// <remarks>[CHANGED] Split out of CloneButton, for the template registry too.</remarks>
    public static void StripToLook(GameObject root)
    {
        foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null || behaviour.TryCast<Graphic>() != null || behaviour.TryCast<Selectable>() != null) continue;
            UObject.DestroyImmediate(behaviour);
        }
    }

    // [CHANGED] Vanilla scripts kept when stripping a clone: generic controls and gamepad pieces, never
    // anything tied to one game setting. By type name, since several are vanilla classes.
    private static readonly HashSet<string> KeptVanillaScripts = new(StringComparer.Ordinal)
    {
        "UISelectable", "UIToggle", "UISlider", "UIContentPanel", "UIDropdownContent",
        "ButtonSound", "AudioSourceController",
    };

    /// <summary>
    /// Removes every script from a clone except an allow-list: graphics, Unity's controls and layout, and
    /// vanilla's generic controls (<c>UIToggle</c>, <c>UISlider</c>, <c>UISelectable</c>, gamepad panels,
    /// sounds). Anything else, like the script tying a row to one game setting (<c>VSyncToggle</c>,
    /// <c>AudioSlider</c>), is removed, including scripts we've never seen.
    /// </summary>
    /// <remarks>
    /// [CHANGED] New for setting rows. An allow-list rather than a list of scripts to remove: a future game
    /// script is removed by default instead of left running on our UI. EventTriggers go too: their saved
    /// targets may be the removed scripts.
    /// [PERFORMANCE] One pass over the clone's scripts; templates only, once per window build.
    /// </remarks>
    public static void StripScripts(GameObject root)
    {
        foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null || IsKept(behaviour)) continue;
            UObject.DestroyImmediate(behaviour);
        }
    }

    private static bool IsKept(MonoBehaviour behaviour) =>
        behaviour.TryCast<Graphic>() != null
        || behaviour.TryCast<Selectable>() != null
        || behaviour.TryCast<LayoutGroup>() != null
        || behaviour.TryCast<LayoutElement>() != null
        || behaviour.TryCast<ContentSizeFitter>() != null
        || behaviour.TryCast<Mask>() != null
        || behaviour.TryCast<RectMask2D>() != null
        || behaviour.TryCast<ScrollRect>() != null
        || behaviour.TryCast<ToggleGroup>() != null
        || KeptVanillaScripts.Contains(behaviour.GetIl2CppType().Name);

    /// <summary>
    /// Converts a handler for IL2CPP, failing loudly instead of returning null. Keep the result referenced.
    /// </summary>
    /// <remarks>[CHANGED] New: one place for the conversion and its null check.</remarks>
    public static T ToIl2Cpp<T>(Delegate handler) where T : Il2CppObjectBase =>
        DelegateSupport.ConvertDelegate<T>(handler)
        ?? throw new InvalidOperationException($"Couldn't convert a handler to {typeof(T).Name} for IL2CPP");

    /// <summary>Runs code called from native code (UI events): exceptions are logged, never thrown.</summary>
    public static void RunSafely(Action action, string what)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            Log.Error($"{what} failed", e);
        }
    }

    /// <summary>
    /// The root canvas above a UI object, or null. Walks up by hand: under IL2CPP, the generic
    /// GetComponentInParent with includeInactive fails (findings doc §7).
    /// </summary>
    public static Canvas? RootCanvas(Transform from)
    {
        Canvas? found = null;
        for (Transform? t = from; t != null; t = t.parent)
        {
            Canvas? canvas = t.GetComponent<Canvas>();
            if (canvas != null) found = canvas;
        }
        return found;
    }

    /// <summary>
    /// Replaces a button's click handling with one handler. Replaces the whole event: a cloned button keeps
    /// vanilla's scene wiring (e.g. Settings → <c>SettingsScreen.Open</c>), which <c>RemoveAllListeners</c>
    /// can't clear.
    /// </summary>
    public static void SetClick(Button button, Action onClick)
    {
        if (!ClickHandlers.TryGetValue(onClick, out UnityAction? converted))
        {
            // [CHANGED] ConvertDelegate is declared as possibly null; fail loudly rather than cache a dead handler.
            converted = DelegateSupport.ConvertDelegate<UnityAction>(new Action(() => RunClick(onClick)))
                ?? throw new InvalidOperationException("Couldn't convert the click handler for IL2CPP");
            ClickHandlers[onClick] = converted;
        }

        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(converted);
    }

    // ---------- Layout helpers ----------

    /// <summary>
    /// The part of a graphic that's actually drawn, in its own space. Text is measured by its letters,
    /// not its box, so things can line up with the words themselves.
    /// </summary>
    public static Rect VisibleRect(Graphic graphic)
    {
        TMP_Text? text = graphic.TryCast<TMP_Text>();
        if (text == null) return graphic.rectTransform.rect;

        text.ForceMeshUpdate(false, false);
        Bounds bounds = text.textBounds;
        if (bounds.size.x <= 0f || bounds.size.y <= 0f) return graphic.rectTransform.rect;
        return Rect.MinMaxRect(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y);
    }

    /// <summary>Changes a rect's anchors without moving or resizing it on screen.</summary>
    public static void SetAnchorKeepRect(RectTransform rect, Vector2 anchor)
    {
        Vector2 size = rect.rect.size;
        Vector3 position = rect.position;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.sizeDelta = size;
        rect.position = position;
    }

    // ---------- Debug tools ----------

    /// <summary>
    /// Logs a UI object's hierarchy (names, active state, components) at debug level, to find out how a
    /// vanilla screen is built without guessing. Replaces what SceneDump did, scoped to one object.
    /// </summary>
    /// <param name="maxDepth">How many levels below the root to show; deeper levels are counted, not listed.</param>
    /// <remarks>
    /// [CHANGED] New. Debug-only: returns at once when debug logging is off.
    /// [PERFORMANCE] Walks the subtree once and logs one block; never called automatically per frame.
    /// [SOURCE] Pre-rebuild SceneDump (findings doc §7).
    /// </remarks>
    public static void DumpHierarchy(Transform root, MeowcowLogger log, int maxDepth = 4)
    {
        if (!MeowcowLogger.DebugEnabled) return;

        var text = new StringBuilder();
        text.Append("Hierarchy of '").Append(root.name).Append('\'').AppendLine();
        DumpLevel(root, 0, maxDepth, text);
        log.Debug(text.ToString());
    }

    private static void DumpLevel(Transform node, int depth, int maxDepth, StringBuilder text)
    {
        text.Append(' ', depth * 2).Append(node.name);
        if (!node.gameObject.activeSelf) text.Append(" (inactive)");
        text.Append(" [");
        bool first = true;
        foreach (Component component in node.GetComponents<Component>())
        {
            if (component == null) continue; // a missing script
            if (!first) text.Append(", ");
            text.Append(component.GetIl2CppType().Name);
            first = false;
        }
        text.Append(']').AppendLine();

        if (depth == maxDepth)
        {
            if (node.childCount > 0)
                text.Append(' ', (depth + 1) * 2).Append("... ").Append(node.childCount).AppendLine(" children");
            return;
        }

        for (int i = 0; i < node.childCount; i++) DumpLevel(node.GetChild(i), depth + 1, maxDepth, text);
    }

    // ---------- Internals ----------

    /// <summary>Runs a click handler. Clicks come from native code, so exceptions must never escape.</summary>
    private static void RunClick(Action onClick)
    {
        try
        {
            onClick();
        }
        catch (Exception e)
        {
            Log.Error("A button's click handler failed", e);
        }
    }

    /// <summary>
    /// One button taller with the top edge kept in place, and wide enough for the new label with the
    /// left edge kept in place.
    /// </summary>
    private static void GrowList(RectTransform list, Transform template, TMP_Text? label)
    {
        VerticalLayoutGroup? group = list.GetComponent<VerticalLayoutGroup>();
        float spacing = group != null ? group.spacing : 0f;
        float step = template.TryCast<RectTransform>()!.rect.height + spacing;

        Vector2 size = list.sizeDelta;
        Vector2 position = list.anchoredPosition;
        Vector2 pivot = list.pivot;

        size.y += step;
        position.y -= step * (1f - pivot.y);

        if (label != null)
        {
            float slide = DefaultHoverSlide;
            RectTransform? highlighted = template.Find(UIPathIndex.ListButtonTextHighlighted)?.TryCast<RectTransform>();
            if (highlighted != null) slide = Mathf.Abs(highlighted.anchoredPosition.x);

            float needed = label.preferredWidth + slide + ListLabelMargin;
            if (needed > size.x)
            {
                position.x += (needed - size.x) * pivot.x;
                size.x = needed;

                // If the layout group doesn't size the buttons, widen them ourselves.
                if (group != null && !group.childControlWidth)
                {
                    for (int i = 0; i < list.childCount; i++)
                    {
                        RectTransform? child = list.GetChild(i).TryCast<RectTransform>();
                        if (child != null) child.sizeDelta = new Vector2(needed, child.sizeDelta.y);
                    }
                }
            }
        }

        list.sizeDelta = size;
        list.anchoredPosition = position;
    }

    /// <summary>
    /// Adds the new button to the list's gamepad panel, right after the button it was cloned from.
    /// A failure here only costs gamepad reach, so it warns instead of undoing the button.
    /// </summary>
    private static void AddToGamepadNavigation(RectTransform list, Transform after, GameObject added)
    {
        try
        {
            UIPanel? panel = list.GetComponent<UIPanel>();
            UISelectable? selectable = added.GetComponent<UISelectable>();
            if (panel == null || selectable == null)
            {
                Log.Warning($"'{added.name}' has no gamepad panel; it may not be reachable with a gamepad");
                return;
            }

            // A clone may have registered itself on Awake; remove it so it's never listed twice.
            panel.selectables.Remove(selectable);
            UISelectable? original = after.GetComponent<UISelectable>();
            int index = original != null ? panel.selectables.IndexOf(original) : -1;
            if (index >= 0) panel.selectables.Insert(index + 1, selectable);
            else panel.AddSelectable(selectable);
        }
        catch (Exception e)
        {
            Log.Warning($"Couldn't add '{added.name}' to gamepad navigation: {e.Message}");
        }
    }
}