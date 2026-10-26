using UnityEngine;
using UnityEngine.UI;
using Il2CppTMPro;
using UObject = UnityEngine.Object;

namespace MeowcowCore.Menu
{
    /// <summary>What a hover shine is drawn with: vanilla's sprite and colour, or a plain soft bar.</summary>
    public readonly struct ShineSource
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

    /// <summary>Small UI helpers shared by Core and the mods that build on it.</summary>
    public static class UiTools
    {
        /// <summary>The first list-button shine in the scene, so new shines match vanilla's art.</summary>
        public static ShineSource FindShineInScene()
        {
            foreach (var image in UObject.FindObjectsOfType<Image>(true))
            {
                if (image.name != MenuPaths.ButtonShine) continue;
                var parent = image.transform.parent;
                if (parent != null && parent.GetComponent<Button>() != null) return ShineSource.FromImage(image);
            }
            return ShineSource.Fallback;
        }

        /// <summary>
        /// One half of a centred shine. Vanilla's sprite has a hard edge on one side and fades out to the
        /// other; the flipped copy fills the left half and the normal one the right, meeting in the middle.
        /// </summary>
        public static Image CreateShineHalf(RectTransform parent, ShineSource source, bool flipped)
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
        /// The part of a graphic that's actually drawn, in its own space. Text is measured by its letters,
        /// not its box.
        /// </summary>
        public static Rect VisibleRect(Graphic g)
        {
            var text = g.TryCast<TMP_Text>();
            if (text == null) return g.rectTransform.rect;

            text.ForceMeshUpdate(false, false);
            var b = text.textBounds;
            if (b.size.x <= 0f || b.size.y <= 0f) return g.rectTransform.rect;
            return Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y);
        }

        /// <summary>Changes a rect's anchors without moving or resizing it on screen.</summary>
        public static void SetAnchorKeepRect(RectTransform rt, Vector2 anchor)
        {
            Vector2 size = rt.rect.size;
            Vector3 position = rt.position;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.sizeDelta = size;
            rt.position = position;
        }

        /// <summary>True if the mouse is over the rect (works for overlay and camera canvases).</summary>
        public static bool ContainsPointer(RectTransform rect)
        {
            Canvas canvas = null;
            for (var t = rect.transform; t != null; t = t.parent)
            {
                var c = t.GetComponent<Canvas>();
                if (c != null) canvas = c;
            }
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, cam);
        }
    }
}
