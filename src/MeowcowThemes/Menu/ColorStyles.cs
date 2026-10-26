using UnityEngine;
using Il2CppTMPro;
using MeowcowCore.Settings;

namespace MeowcowThemes.Menu
{
    /// <summary>
    /// Hover bar tint. Multiplies the shine's own colour; replaces its opacity only if the hex gives one
    /// (eight digits), so "#FFFFFF" leaves vanilla unchanged.
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
    /// A colour that replaces the item's colour (text, logo, icons). Keeps the item's own opacity unless
    /// the hex gives one. Null/invalid leaves the original.
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

        public Color Apply(Color original)
        {
            if (!_color.HasValue) return original;
            var c = _color.Value;
            return new Color(c.r, c.g, c.b, _hasAlpha ? c.a : original.a);
        }
    }

    internal static class TextAlign
    {
        /// <summary>"Left", "Center", "Right", or anything else for the recorded original. Keeps vertical alignment.</summary>
        internal static void Apply(TMP_Text text, string choice, HorizontalAlignmentOptions original)
        {
            if (text == null) return;
            text.horizontalAlignment = choice switch
            {
                "Left"   => HorizontalAlignmentOptions.Left,
                "Center" => HorizontalAlignmentOptions.Center,
                "Right"  => HorizontalAlignmentOptions.Right,
                _        => original,
            };
        }
    }
}
