using System;
using System.Collections.Generic;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppTMPro;
using MeowcowCore.Foundation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using InputPromptsBindingData = Il2CppScheduleOne.UI.Input.InputPromptsBindingData;
using InputPromptsManager = Il2CppScheduleOne.UI.Input.InputPromptsManager;
using UObject = UnityEngine.Object;

namespace MeowcowCore.SuiteUI;

/// <summary>
/// Gamepad button pictures (A, B, LB, D-pad up...) for any control, as vanilla shows them: the right picture
/// for the pad in use (Xbox, PlayStation, Switch, others). A glyph is a small image Core can place anywhere;
/// it's pointed at a control path ("&lt;Gamepad&gt;/dpad/up") or at an action's gamepad binding.
/// </summary>
/// <remarks>
/// [CHANGED] New (window navigation prompts). Reusable by any suite UI that shows gamepad buttons.
/// [SOURCE] Vanilla <c>InputPromptsManager.GetBindingDataFromActionReference</c> (its binding data list: one
/// entry per control path and platform, falling back to the Steam pictures) and
/// <c>EmbeddedInputPromptUI.UpdatePrompt</c> (how an entry is drawn: sprite variation, colour, pixels-per-unit,
/// sliced when scaled, an optional label).
/// [PERFORMANCE] A lookup is one pass over vanilla's binding list (a few hundred entries), done when a glyph is
/// shown or the pad changes, never per frame.
/// </remarks>
public static class GamepadGlyphs
{
    // Gamepad control paths (Unity Input System layout names).
    public const string DpadUp = "<Gamepad>/dpad/up";
    public const string DpadDown = "<Gamepad>/dpad/down";
    public const string DpadLeft = "<Gamepad>/dpad/left";
    public const string DpadRight = "<Gamepad>/dpad/right";
    public const string LeftShoulder = "<Gamepad>/leftShoulder";
    public const string RightShoulder = "<Gamepad>/rightShoulder";
    public const string LeftTrigger = "<Gamepad>/leftTrigger";
    public const string RightTrigger = "<Gamepad>/rightTrigger";
    public const string Select = "<Gamepad>/select"; // View / Share / Select: left of Start (vanilla has no picture)
    /// <summary>[CHANGED] Y on Xbox, Triangle on PlayStation: vanilla's phone button, which has pictures.</summary>
    public const string ButtonNorth = "<Gamepad>/buttonNorth";

    private const string LabelName = "Label";
    private static readonly MeowcowLogger Log = CoreMod.Log.Tagged("Glyphs");
    private static readonly HashSet<string> Missing = new(StringComparer.Ordinal); // logged once each

    /// <summary>
    /// A new, empty glyph under <paramref name="parent"/>, <paramref name="size"/> square, with a label in the
    /// font of <paramref name="style"/> (some pictures carry a short label). Hidden until <see cref="Show"/>.
    /// </summary>
    public static GameObject Create(Transform parent, string name, float size, TMP_Text? style)
    {
        var glyph = new GameObject(name);
        RectTransform rect = glyph.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(size, size);
        LayoutElement element = glyph.AddComponent<LayoutElement>();
        element.preferredWidth = size;
        element.preferredHeight = size;
        Image image = glyph.AddComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;

        if (style != null)
        {
            TMP_Text label = UIBuilder.CloneText(style, LabelName, "");
            label.transform.SetParent(glyph.transform, false);
            label.fontSize = size * 0.5f;
            label.horizontalAlignment = HorizontalAlignmentOptions.Center;
            label.verticalAlignment = VerticalAlignmentOptions.Middle;
            label.raycastTarget = false;
            MeowcowWindow.SetRect(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            label.gameObject.SetActive(false);
        }

        glyph.SetActive(false);
        return glyph;
    }

    /// <summary>
    /// Shows <paramref name="glyph"/> as the picture for <paramref name="controlPath"/> on the pad in use, or
    /// hides it when <paramref name="controlPath"/> is null or vanilla has no picture for it.
    /// </summary>
    public static void Show(GameObject? glyph, string? controlPath)
    {
        if (glyph == null) return;
        InputPromptsBindingData? data = controlPath != null ? Find(controlPath) : null;
        if (data == null)
        {
            glyph.SetActive(false);
            return;
        }

        Image image = glyph.GetComponent<Image>();
        image.sprite = data.SpriteVariation != null ? data.SpriteVariation : data.Sprite; // as EmbeddedInputPromptUI
        image.color = data.SpriteColor;
        image.pixelsPerUnitMultiplier = data.SpritePixelMultiplier;
        image.type = data.SpritePixelMultiplier > 1f ? Image.Type.Sliced : Image.Type.Simple;

        Transform? labelObject = glyph.transform.Find(LabelName);
        if (labelObject != null)
        {
            string text = data.SpriteLabel ?? "";
            labelObject.gameObject.SetActive(text.Length > 0);
            if (text.Length > 0) labelObject.GetComponent<TMP_Text>().text = text;
        }
        glyph.SetActive(true);
    }

    /// <summary>Hides a glyph (null-safe).</summary>
    public static void Hide(GameObject? glyph)
    {
        if (glyph != null && glyph.activeSelf) glyph.SetActive(false);
    }

    /// <summary>The gamepad control path an action is bound to (e.g. Confirm's "&lt;Gamepad&gt;/buttonSouth"), or null.</summary>
    /// <remarks>Follows rebinding: the effective path, as vanilla's prompts use.</remarks>
    public static string? PathOf(InputActionReference? reference)
    {
        InputAction? action = reference?.action;
        if (action == null) return null;
        var bindings = action.bindings;
        for (int i = 0; i < bindings.Count; i++)
        {
            string path = bindings[i].effectivePath;
            if (!string.IsNullOrEmpty(path) && path.Contains("Gamepad")) return path;
        }
        return null;
    }

    /// <summary>
    /// Vanilla's picture for a control path on the current pad, else its Steam picture (as vanilla falls back).
    /// </summary>
    private static InputPromptsBindingData? Find(string controlPath)
    {
        InputPromptsManager? manager = Singleton<InputPromptsManager>.Instance;
        var list = manager != null ? manager._bindingDataList : null;
        if (list == null) return null;

        // [CHANGED] The platform enum isn't named here (build fix: its namespace isn't Il2CppScheduleOne.Platform):
        // the current platform is compared as returned, and the Steam fallback by name.
        var platform = GameInput.CurrentPlatformType;
        InputPromptsBindingData? fallback = null;
        foreach (InputPromptsBindingData data in list)
        {
            if (data == null || data.ControlPath != controlPath) continue;
            if (data.PlatformType == platform) return data;
            if (data.PlatformType.ToString() == "Steam") fallback = data;
        }
        if (fallback == null && Missing.Add(controlPath))
            Log.Debug($"No picture for '{controlPath}' (platform {platform}); that prompt stays hidden");
        return fallback;
    }
}