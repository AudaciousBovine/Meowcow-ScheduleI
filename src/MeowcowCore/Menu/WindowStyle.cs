using System;
using UnityEngine;

namespace MeowcowCore.Menu
{
    /// <summary>
    /// Colours for Core's framed windows. Every slot is null by default, meaning vanilla's own colour.
    /// MeowcowThemes fills these from its palette and calls Apply(); Core never needs to know it exists.
    /// </summary>
    public static class WindowStyle
    {
        public static Color? Background;    // the window's background panel
        public static Color? Content;       // the area behind the rows
        public static Color? Title;         // window title text
        public static Color? Tab;           // a tab's background
        public static Color? TabSelected;   // the selected tab's marker
        public static Color? TabText;       // tab labels
        public static Color? ButtonText;    // footer buttons (Back)

        /// <summary>Raised by Apply(); framed windows recolour themselves.</summary>
        public static event Action Changed;

        /// <summary>Call after changing any colour.</summary>
        public static void Apply()
        {
            try { Changed?.Invoke(); }
            catch (Exception e) { CoreMod.Log.Error($"WindowStyle.Changed handler failed: {e}"); }
        }

        /// <summary>The slot's colour, or the original when the slot is null.</summary>
        internal static Color Or(Color? slot, Color original) => slot ?? original;
    }
}