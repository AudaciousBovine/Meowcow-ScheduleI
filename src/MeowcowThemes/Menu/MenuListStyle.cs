using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Il2CppTMPro;
using MeowcowCore.Menu;
using LabelLerp = Il2CppScheduleOne.UI.RectTransformLerpList;

namespace MeowcowThemes.Menu
{
    /// <summary>
    /// Styles the main menu's button list (Core's Meowcow Options included): label alignment with the
    /// hover slide to match, hover bar length and tint, and label colour.
    /// </summary>
    internal static class MenuListStyle
    {
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
        private static float _slide = 15f; // vanilla's hover slide, read from the Settings button

        /// <summary>Called once per menu load, after Core's additions, before the list's placement is recorded.</summary>
        internal static void Setup(RectTransform panel)
        {
            _buttons.Clear();
            _labels.Clear();
            _shines.Clear();

            var settings = panel.Find(MenuElements.SettingsButton);
            var highlighted = settings != null ? FindRect(settings, MenuElements.ButtonTextHighlighted) : null;
            if (highlighted != null) _slide = Mathf.Abs(highlighted.anchoredPosition.x);

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

        /// <summary>Colours every button label.</summary>
        internal static void ApplyTextColor(ColorOverride color)
        {
            foreach (var (label, vanilla) in _labels)
                if (label != null) label.color = color.Apply(vanilla);
        }

        /// <summary>
        /// Label alignment (the hover slide goes right for Left, left for Right, none for Center), plus the
        /// hover bar's length and tint.
        /// </summary>
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

            var source = ShineSource.FromImage(image);
            var left = UiTools.CreateShineHalf(rect, source, flipped: true);
            var right = UiTools.CreateShineHalf(rect, source, flipped: false);
            left.gameObject.SetActive(false);
            right.gameObject.SetActive(false);

            return new ShineParts
            {
                Rect = rect,
                Image = image,
                Position = rect.anchoredPosition,
                Size = rect.sizeDelta,
                Scale = rect.localScale,
                Color = image.color,
                Left = left,
                Right = right,
            };
        }

        /// <summary>
        /// Left: vanilla. Right: mirrored. Center: the two halves, centred behind the label.
        /// Length stretches the bar away from the hard edge (both ways when centred).
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
