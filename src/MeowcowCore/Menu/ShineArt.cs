using UnityEngine;
using UnityEngine.UI;
using Il2CppTMPro;
using MeowcowCore.Settings;
using UObject = UnityEngine.Object;

namespace MeowcowCore.Menu
{
    /// <summary>What a hover shine is drawn with: vanilla's sprite and colour, or a plain soft bar.</summary>
    internal readonly struct ShineSource
    {
        public readonly Sprite Sprite;
        public readonly Material Material;
        public readonly Image.Type Type;
        public readonly Color Color;

        public ShineSource(Sprite sprite, Material material, Image.Type type, Color color)
        {
            Sprite = sprite;
            Material = material;
            Type = type;
            Color = color;
        }

        public static ShineSource FromImage(Image image) => new ShineSource(image.sprite, image.material, image.type, image.color);

        /// <summary>Used when no vanilla shine exists in the scene: a faint plain bar.</summary>
        public static ShineSource Fallback => new ShineSource(null, null, Image.Type.Simple, new Color(1f, 1f, 1f, 0.12f));
    }

    /// <summary>
    /// The global hover colour. Multiplies the shine's own colour; replaces its opacity only if the hex
    /// gives one (eight digits), so "#FFFFFF" leaves vanilla unchanged.
    /// </summary>
    internal readonly struct ShineTint
    {
        private readonly Color? _tint;
        private readonly bool _hasAlpha;

        private ShineTint(Color? tint, bool hasAlpha)
        {
            _tint = tint;
            _hasAlpha = hasAlpha;
        }

        public static ShineTint Parse(string hex) =>
            hex != null && ColorHex.TryParse(hex, out var color, out bool hasAlpha)
                ? new ShineTint(color, hasAlpha)
                : new ShineTint(null, false);

        public Color Apply(Color vanilla)
        {
            if (!_tint.HasValue) return vanilla;
            var t = _tint.Value;
            return new Color(vanilla.r * t.r, vanilla.g * t.g, vanilla.b * t.b, _hasAlpha ? t.a : vanilla.a);
        }
    }

    /// <summary>
    /// A colour setting that replaces the item's colour (unlike ShineTint, which multiplies). Keeps the
    /// item's own opacity unless the hex gives one (eight digits). Null/invalid leaves vanilla.
    /// </summary>
    internal readonly struct ColorOverride
    {
        private readonly Color? _color;
        private readonly bool _hasAlpha;

        private ColorOverride(Color? color, bool hasAlpha)
        {
            _color = color;
            _hasAlpha = hasAlpha;
        }

        public static ColorOverride Parse(string hex) =>
            hex != null && ColorHex.TryParse(hex, out var color, out bool hasAlpha)
                ? new ColorOverride(color, hasAlpha)
                : new ColorOverride(null, false);

        public Color Apply(Color vanilla)
        {
            if (!_color.HasValue) return vanilla;
            var c = _color.Value;
            return new Color(c.r, c.g, c.b, _hasAlpha ? c.a : vanilla.a);
        }
    }

    internal static class ShineArt
    {
        /// <summary>The first list-button shine in the scene, so new shines match vanilla's art.</summary>
        internal static ShineSource FindInScene()
        {
            foreach (var image in UObject.FindObjectsOfType<Image>(true))
            {
                if (image.name != MenuElements.ButtonShine) continue;
                var parent = image.transform.parent;
                if (parent != null && parent.GetComponent<Button>() != null) return ShineSource.FromImage(image);
            }
            return ShineSource.Fallback;
        }

        /// <summary>
        /// One half of a centred shine. Vanilla's sprite has a hard edge on one side and fades out to the
        /// other; the flipped copy fills the left half and the normal one the right, meeting in the middle.
        /// </summary>
        internal static Image CreateHalf(RectTransform parent, ShineSource source, bool flipped)
        {
            var go = new GameObject(flipped ? "MeowcowShineLeft" : "MeowcowShineRight");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(flipped ? 0f : 0.5f, 0f);
            rt.anchorMax = new Vector2(flipped ? 0.5f : 1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = new Vector3(flipped ? -1f : 1f, 1f, 1f); // flipped in place: its hard edge lands in the middle

            var image = go.AddComponent<Image>();
            image.sprite = source.Sprite;
            if (source.Material != null) image.material = source.Material;
            image.type = source.Type;
            image.color = source.Color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// A text alignment setting: "Left", "Center", "Right", or "Vanilla" (the recorded original).
        /// Keeps the vertical alignment as it was.
        /// </summary>
        internal static void ApplyTextAlign(TMP_Text text, string choice, HorizontalAlignmentOptions vanilla)
        {
            if (text == null) return;
            text.horizontalAlignment = choice switch
            {
                "Left"   => HorizontalAlignmentOptions.Left,
                "Center" => HorizontalAlignmentOptions.Center,
                "Right"  => HorizontalAlignmentOptions.Right,
                _        => vanilla,
            };
        }
    }
}