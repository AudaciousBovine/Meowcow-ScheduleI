using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Il2CppInterop.Runtime;
using Il2CppTMPro;
using MeowcowCore.Settings;
using UObject = UnityEngine.Object;
using UIPanel = Il2CppScheduleOne.UIPanel;
using UISelectable = Il2CppScheduleOne.UISelectable;
using LabelLerp = Il2CppScheduleOne.UI.RectTransformLerpList;

namespace MeowcowCore.Menu
{
    /// <summary>
    /// The main menu's button list: adds "Meowcow Options" after Settings, and lines up the button
    /// labels (left, centre or right) with the hover slide to match.
    /// </summary>
    internal static class MenuList
    {
        private const string OptionsLabel = "Meowcow Options";
        private const string OptionsName = "MeowcowOptions";
        private const float LabelMargin = 10f;

        /// <summary>A button's hover shine: vanilla placement plus the two halves used when centred.</summary>
        private sealed class ShineParts
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Position;
            public Vector2 Size;      // vanilla sizeDelta (the shine stretches with the button, plus this)
            public Vector3 Scale;
            public Color Color;
            public Image Left;        // flipped copy: hard edge on its right, fading left
            public Image Right;       // normal copy: hard edge on its left, fading right
        }

        private static readonly List<Button> _buttons = new List<Button>();
        private static readonly List<(TMP_Text label, Color vanilla)> _labels = new List<(TMP_Text, Color)>();
        private static readonly List<ShineParts> _shines = new List<ShineParts>();
        private static UnityAction _onOptions; // kept referenced so the converted delegate isn't collected
        private static float _slide = 15f;     // vanilla's hover slide, read from the Settings button

        /// <summary>Called once per menu load, after the panel is under Home and before its vanilla placement is recorded.</summary>
        internal static void Setup(RectTransform panel)
        {
            _buttons.Clear();
            _labels.Clear();
            _shines.Clear();

            var settings = panel.Find(MenuElements.SettingsButton);
            if (settings == null)
            {
                CoreMod.Log.Warning("Settings button not found; Meowcow Options not added");
            }
            else
            {
                var highlighted = FindRect(settings, MenuElements.ButtonTextHighlighted);
                if (highlighted != null) _slide = Mathf.Abs(highlighted.anchoredPosition.x);

                try { AddOptionsButton(panel, settings); }
                catch (Exception e) { CoreMod.Log.Error($"Couldn't add Meowcow Options: {e}"); }
            }

            for (int i = 0; i < panel.childCount; i++)
            {
                var button = panel.GetChild(i).GetComponent<Button>();
                if (button == null) continue;
                _buttons.Add(button);

                var label = FindText(button.transform, MenuElements.ButtonText);
                if (label != null) _labels.Add((label, label.color));

                var shine = BuildShine(button.transform);
                if (shine != null) _shines.Add(shine);
            }
        }

        /// <summary>
        /// Label alignment (the hover slide goes right for Left, left for Right, none for Center), plus the
        /// hover bar's length and tint.
        /// </summary>
        /// <summary>Colours every button label (the effective colour: the list's own, else the global one).</summary>
        internal static void ApplyTextColor(ColorOverride color)
        {
            foreach (var (label, vanilla) in _labels)
                if (label != null) label.color = color.Apply(vanilla);
        }

        internal static void ApplyStyle(string align, float length, ShineTint tint)
        {
            var horizontal = align switch
            {
                "Center" => HorizontalAlignmentOptions.Center,
                "Right"  => HorizontalAlignmentOptions.Right,
                _        => HorizontalAlignmentOptions.Left,
            };
            float slide = align == "Center" ? 0f : align == "Right" ? -_slide : _slide;

            foreach (var shine in _shines) ApplyShine(shine, align, length, tint);

            foreach (var button in _buttons)
            {
                if (button == null) continue;
                var t = button.transform;

                foreach (var name in new[] { MenuElements.ButtonText, MenuElements.ButtonTextDefault, MenuElements.ButtonTextHighlighted })
                {
                    var text = FindText(t, name);
                    if (text != null) text.horizontalAlignment = horizontal;
                }

                var highlighted = FindRect(t, MenuElements.ButtonTextHighlighted);
                if (highlighted != null)
                    highlighted.anchoredPosition = new Vector2(slide, highlighted.anchoredPosition.y);

                // The hover lerp shortens its duration by how far the label slides. Centred labels don't
                // slide at all, which would make the hover grow snap instantly, so use the full duration.
                var label = t.Find(MenuElements.ButtonText);
                var lerp = label != null ? label.GetComponent<LabelLerp>() : null;
                if (lerp != null) lerp._scaleDurationWithDistance = slide != 0f;
            }
        }

        // ---------- Hover shine ----------

        /// <summary>
        /// Vanilla's shine has a hard left edge and fades out to the right. For centred labels, two copies
        /// (one flipped) meet in the middle and fade out to both sides. They sit inside the shine, so its
        /// CanvasGroup fades them on hover exactly like the original.
        /// </summary>
        private static ShineParts BuildShine(Transform button)
        {
            var rect = FindRect(button, MenuElements.ButtonShine);
            var image = rect != null ? rect.GetComponent<Image>() : null;
            if (image == null) return null;

            return new ShineParts
            {
                Rect = rect,
                Image = image,
                Position = rect.anchoredPosition,
                Size = rect.sizeDelta,
                Scale = rect.localScale,
                Color = image.color,
                Left = HiddenHalf(rect, image, flipped: true),
                Right = HiddenHalf(rect, image, flipped: false),
            };
        }

        private static Image HiddenHalf(RectTransform shine, Image source, bool flipped)
        {
            var half = ShineArt.CreateHalf(shine, ShineSource.FromImage(source), flipped);
            half.gameObject.SetActive(false);
            return half;
        }

        /// <summary>
        /// Left: vanilla. Right: mirrored. Center: the two halves, centred behind the label.
        /// Length stretches the bar away from the hard edge (both ways when centred); the tint
        /// multiplies vanilla's colour, and replaces its opacity only if the hex gives one.
        /// </summary>
        private static void ApplyShine(ShineParts s, string align, float length, ShineTint tint)
        {
            if (s.Rect == null) return;
            bool center = align == "Center";
            bool right = align == "Right";

            // The shine spans the button plus its vanilla extra width; grow it while keeping its hard edge put.
            var button = s.Rect.parent != null ? s.Rect.parent.TryCast<RectTransform>() : null;
            float buttonWidth = button != null ? button.rect.width : 0f;
            float width = buttonWidth + s.Size.x;
            float grow = width * (length - 1f);

            float x = center ? 0f : right ? -s.Position.x - grow / 2f : s.Position.x + grow / 2f;
            s.Rect.anchoredPosition = new Vector2(x, s.Position.y);
            s.Rect.sizeDelta = new Vector2(s.Size.x + grow, s.Size.y);
            s.Rect.localScale = right ? new Vector3(-s.Scale.x, s.Scale.y, s.Scale.z) : s.Scale;

            var color = tint.Apply(s.Color);
            s.Image.color = color;
            s.Left.color = color;
            s.Right.color = color;

            s.Image.enabled = !center;
            s.Left.gameObject.SetActive(center);
            s.Right.gameObject.SetActive(center);
        }

        // ---------- Meowcow Options ----------

        private static void AddOptionsButton(RectTransform panel, Transform settings)
        {
            var go = UObject.Instantiate(settings.gameObject, panel);
            go.name = OptionsName;
            go.transform.SetSiblingIndex(settings.GetSiblingIndex() + 1);

            // The clone copies Settings' scene wiring (SettingsScreen.Open), which RemoveAllListeners
            // can't clear; a fresh event drops it.
            var button = go.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            _onOptions ??= DelegateSupport.ConvertDelegate<UnityAction>(new Action(OptionsScreen.Open));
            button.onClick.AddListener(_onOptions);

            var label = FindText(go.transform, MenuElements.ButtonText);
            if (label != null)
            {
                label.text = OptionsLabel;
                label.enableWordWrapping = false;
            }

            GrowPanel(panel, settings, label);
            RegisterForGamepad(panel, settings, go);
        }

        /// <summary>
        /// One button taller (top edge stays put), and wide enough for the new label (left edge stays put).
        /// </summary>
        private static void GrowPanel(RectTransform panel, Transform settings, TMP_Text label)
        {
            var group = panel.GetComponent<VerticalLayoutGroup>();
            float spacing = group != null ? group.spacing : 0f;
            float step = settings.TryCast<RectTransform>().rect.height + spacing;

            var size = panel.sizeDelta;
            var pos = panel.anchoredPosition;
            var pivot = panel.pivot;

            size.y += step;
            pos.y -= step * (1f - pivot.y);

            if (label != null)
            {
                float needed = label.preferredWidth + _slide + LabelMargin;
                if (needed > size.x)
                {
                    float extra = needed - size.x;
                    size.x = needed;
                    pos.x += extra * pivot.x;

                    // If the layout group doesn't size the buttons, widen them ourselves.
                    if (group != null && !group.childControlWidth)
                        for (int i = 0; i < panel.childCount; i++)
                        {
                            var child = panel.GetChild(i).TryCast<RectTransform>();
                            if (child != null) child.sizeDelta = new Vector2(needed, child.sizeDelta.y);
                        }
                }
            }

            panel.sizeDelta = size;
            panel.anchoredPosition = pos;
        }

        /// <summary>Adds the new button to the list's gamepad panel, right after Settings.</summary>
        private static void RegisterForGamepad(RectTransform panel, Transform settings, GameObject go)
        {
            try
            {
                var uiPanel = panel.GetComponent<UIPanel>();
                var added = go.GetComponent<UISelectable>();
                var original = settings.GetComponent<UISelectable>();
                if (uiPanel == null || added == null)
                {
                    CoreMod.Log.Warning("List has no gamepad panel; Meowcow Options may not be reachable by gamepad");
                    return;
                }

                var list = uiPanel.selectables;
                list.Remove(added); // in case it registered itself on Awake
                int index = original != null ? list.IndexOf(original) : -1;
                if (index >= 0) list.Insert(index + 1, added);
                else uiPanel.AddSelectable(added);
            }
            catch (Exception e)
            {
                CoreMod.Log.Warning($"Couldn't register Meowcow Options for gamepad: {e.Message}");
            }
        }

        // ---------- Helpers ----------

        private static TMP_Text FindText(Transform parent, string name)
        {
            var t = parent.Find(name);
            return t != null ? t.GetComponent<TMP_Text>() : null;
        }

        private static RectTransform FindRect(Transform parent, string name)
        {
            var t = parent.Find(name);
            return t != null ? t.TryCast<RectTransform>() : null;
        }
    }
}