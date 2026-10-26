using System.Globalization;
using UnityEngine;

namespace MeowcowCore.Settings
{
    /// <summary>
    /// Colours stored as hex text: "#RRGGBB" or "#RRGGBBAA" (the # is optional when typing).
    /// Six digits means "no alpha given", so callers can keep their own alpha.
    /// </summary>
    public static class ColorHex
    {
        public static bool TryParse(string text, out Color color, out bool hasAlpha)
        {
            color = Color.white;
            hasAlpha = false;
            if (string.IsNullOrWhiteSpace(text)) return false;

            string s = text.Trim().TrimStart('#');
            if (s.Length != 6 && s.Length != 8) return false;
            if (!uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v)) return false;

            if (s.Length == 6) v = (v << 8) | 0xFF;
            else hasAlpha = true;

            color = new Color(((v >> 24) & 0xFF) / 255f, ((v >> 16) & 0xFF) / 255f,
                              ((v >> 8) & 0xFF) / 255f, (v & 0xFF) / 255f);
            return true;
        }

        /// <summary>Canonical spelling: leading #, upper case.</summary>
        public static string Normalize(string text) => "#" + text.Trim().TrimStart('#').ToUpperInvariant();
    }
}