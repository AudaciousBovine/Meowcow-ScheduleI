using UnityEngine;
using UnityEngine.UI;
using Il2CppTMPro;
using MeowcowCore.Menu;
using MeowcowCore.Settings;
using S = MeowcowThemes.Settings.ThemeSettings;

namespace MeowcowThemes.Menu
{
    /// <summary>One placeable element and its recorded vanilla placement.</summary>
    internal sealed class PlacedElement
    {
        public string SectionId;
        public RectTransform Rect;
        public Vector2 Position;   // vanilla anchoredPosition
        public Vector3 Scale;      // vanilla localScale
        public bool Active;        // vanilla active state
        public bool ManageActive;  // false for the lobby: vanilla switches it on and off itself
    }

    /// <summary>
    /// Places an element from its settings section: anchor, inward X/Y offset, scale and hide.
    /// Every call starts from the recorded vanilla placement, so it can run any number of times.
    /// Positions come from what the element actually draws (several are zero-size containers),
    /// in the root canvas's units (1920x1080 reference).
    /// </summary>
    internal static class ElementPlacer
    {
        internal static PlacedElement Record(string sectionId, RectTransform rt, bool manageActive) => new PlacedElement
        {
            SectionId = sectionId,
            Rect = rt,
            Position = rt.anchoredPosition,
            Scale = rt.localScale,
            Active = rt.gameObject.activeSelf,
            ManageActive = manageActive,
        };

        internal static float Padding(ISettingsSource settings) => settings.GetNumber(S.MainMenu, S.Padding) ?? 30f;

        internal static void Place(PlacedElement p, ISettingsSource settings, float padding)
        {
            var rt = p.Rect;
            if (rt == null) return;

            // Back to vanilla first.
            rt.anchoredPosition = p.Position;
            rt.localScale = p.Scale;
            if (p.ManageActive) rt.gameObject.SetActive(p.Active && !settings.GetBool(p.SectionId, S.Hide));
            if (!rt.gameObject.activeInHierarchy) return;

            var space = RootCanvas(rt);
            if (space == null) return;
            var before = Bounds(rt, space);
            if (before == null) return;

            float scale = settings.GetNumber(p.SectionId, S.Scale) ?? 1f;
            rt.localScale = p.Scale * scale;
            var after = Bounds(rt, space) ?? before.Value;

            string anchor = settings.GetString(p.SectionId, S.Anchor) ?? S.VanillaAnchor;
            Vector2 delta = anchor == S.VanillaAnchor
                ? before.Value.center - after.center // vanilla spot: scale around the element's centre
                : ToAnchor(after, space.rect, AnchorPoint(anchor),
                           settings.GetNumber(p.SectionId, S.X), settings.GetNumber(p.SectionId, S.Y), padding);

            delta = KeepOnScreen(after, delta, space.rect);
            rt.position += space.TransformVector(new Vector3(delta.x, delta.y, 0f));
        }

        /// <summary>
        /// The move that puts the element's bounds at its anchor. Offsets run inward from the anchored
        /// edge (0 = flush); on a centre axis they run right/up from the middle. Null offset = padding.
        /// </summary>
        private static Vector2 ToAnchor(Rect bounds, Rect screen, Vector2 anchor, float? x, float? y, float padding)
        {
            float ox = x ?? (anchor.x == 0.5f ? 0f : padding);
            float oy = y ?? (anchor.y == 0.5f ? 0f : padding);

            float dx = anchor.x == 0f ? screen.xMin + ox - bounds.xMin
                     : anchor.x == 1f ? screen.xMax - ox - bounds.xMax
                     : screen.center.x + ox - bounds.center.x;

            float dy = anchor.y == 0f ? screen.yMin + oy - bounds.yMin
                     : anchor.y == 1f ? screen.yMax - oy - bounds.yMax
                     : screen.center.y + oy - bounds.center.y;

            return new Vector2(dx, dy);
        }

        /// <summary>Adjusts a move so the bounds stay on screen. Too big to fit: the left and top edges win.</summary>
        private static Vector2 KeepOnScreen(Rect bounds, Vector2 delta, Rect screen)
        {
            if (bounds.xMax + delta.x > screen.xMax) delta.x = screen.xMax - bounds.xMax;
            if (bounds.xMin + delta.x < screen.xMin) delta.x = screen.xMin - bounds.xMin; // checked last, so it wins

            if (bounds.yMin + delta.y < screen.yMin) delta.y = screen.yMin - bounds.yMin;
            if (bounds.yMax + delta.y > screen.yMax) delta.y = screen.yMax - bounds.yMax; // checked last, so it wins

            return delta;
        }

        private static Vector2 AnchorPoint(string anchor) => anchor switch
        {
            "TopLeft"     => new Vector2(0f, 1f),
            "Top"         => new Vector2(0.5f, 1f),
            "TopRight"    => new Vector2(1f, 1f),
            "Left"        => new Vector2(0f, 0.5f),
            "Center"      => new Vector2(0.5f, 0.5f),
            "Right"       => new Vector2(1f, 0.5f),
            "BottomLeft"  => new Vector2(0f, 0f),
            "Bottom"      => new Vector2(0.5f, 0f),
            "BottomRight" => new Vector2(1f, 0f),
            _             => new Vector2(0.5f, 0.5f),
        };

        // ---------- Measuring ----------

        /// <summary>The topmost canvas above a rect. Walks up by hand (generic GetComponentInParent fails under Il2CppInterop).</summary>
        private static RectTransform RootCanvas(Transform t)
        {
            RectTransform root = null;
            for (var current = t; current != null; current = current.parent)
                if (current.GetComponent<Canvas>() != null) root = current.TryCast<RectTransform>();
            return root;
        }

        /// <summary>The area everything visible in the element covers, in the given space.</summary>
        internal static Rect? Bounds(RectTransform root, Transform space)
        {
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            bool any = false;

            foreach (var g in root.GetComponentsInChildren<Graphic>(false))
            {
                if (!IsVisible(g, root)) continue;

                var rt = g.rectTransform;
                var r = UiTools.VisibleRect(g);
                foreach (var corner in new[] { new Vector3(r.xMin, r.yMin), new Vector3(r.xMin, r.yMax),
                                               new Vector3(r.xMax, r.yMin), new Vector3(r.xMax, r.yMax) })
                {
                    var p = space.InverseTransformPoint(rt.TransformPoint(corner));
                    xMin = Mathf.Min(xMin, p.x); xMax = Mathf.Max(xMax, p.x);
                    yMin = Mathf.Min(yMin, p.y); yMax = Mathf.Max(yMax, p.y);
                }
                any = true;
            }

            return any ? Rect.MinMaxRect(xMin, yMin, xMax, yMax) : (Rect?)null;
        }

        /// <summary>
        /// Skips helpers that are present but never seen: disabled or transparent graphics, empty text,
        /// and anything under a fully faded CanvasGroup (e.g. the buttons' hover shine).
        /// </summary>
        private static bool IsVisible(Graphic g, Transform root)
        {
            if (!g.enabled || g.color.a <= 0.01f) return false;

            var text = g.TryCast<TMP_Text>();
            if (text != null && string.IsNullOrWhiteSpace(text.text)) return false;

            for (var t = g.transform; t != null; t = t.parent)
            {
                var group = t.GetComponent<CanvasGroup>();
                if (group != null && group.alpha <= 0.01f) return false;
                if (t == root) break;
            }
            return true;
        }
    }
}
