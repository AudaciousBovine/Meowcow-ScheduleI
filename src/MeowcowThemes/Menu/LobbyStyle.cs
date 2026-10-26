using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Il2CppTMPro;
using MeowcowCore.Menu;
using MeowcowCore.Settings;
using T = MeowcowThemes.Settings.ThemeSettings;

namespace MeowcowThemes.Menu
{
    /// <summary>
    /// Styles Core's lobby panel wherever it appears: placement, title alignment over the slots, title
    /// colour, slot tint, and the hover shine's length and tint.
    /// </summary>
    internal static class LobbyStyle
    {
        private static PlacedElement _placed;
        private static TMP_Text _title;
        private static HorizontalAlignmentOptions _titleAlign;
        private static Vector2 _titlePos, _titleSize;   // vanilla placement of the title
        private static Color _titleColor;
        private static readonly List<(Image image, Color vanilla)> _slotImages = new List<(Image, Color)>();
        private static bool _pending, _wasVisible;
        private static Vector2Int _screen;

        /// <summary>From Core: a scene's lobby panel is set up.</summary>
        internal static void OnLobbyReady()
        {
            try
            {
                var panel = LobbyToggle.Panel;
                if (panel == null || panel.Container == null) return;

                _placed = ElementPlacer.Record(T.Lobby, panel.Container, manageActive: false);
                _title = LobbyToggle.Title;
                if (_title != null)
                {
                    _titleAlign = _title.horizontalAlignment;
                    _titlePos = _title.rectTransform.anchoredPosition;
                    _titleSize = _title.rectTransform.sizeDelta;
                    _titleColor = _title.color;
                }
                CaptureSlots();
                _wasVisible = false;
                _pending = true;
            }
            catch (Exception e)
            {
                ThemesMod.Log.Error($"Lobby theme setup failed: {e}");
            }
        }

        /// <summary>Re-apply on the next tick (settings changed, or Core says the title or slots changed).</summary>
        internal static void Refresh() => _pending = true;

        /// <summary>Every frame: places the panel when it appears or anything changed.</summary>
        internal static void Tick()
        {
            var panel = LobbyToggle.Panel;
            if (panel == null || _placed == null) return;
            var container = panel.Container;
            if (container == null) return;

            var screen = new Vector2Int(Screen.width, Screen.height);
            if (screen != _screen) { _screen = screen; _pending = true; }

            // In game the panel only shows while paused in a lobby; place it when it appears.
            bool visible = container.gameObject.activeInHierarchy;
            if (visible && !_wasVisible) _pending = true;
            _wasVisible = visible;

            if (_pending && visible)
            {
                _pending = false;
                Apply();
            }
        }

        /// <summary>
        /// Each slot's outline (its "Frame" child, the border around the avatar), plus the invite box's
        /// outline and + icon. Avatars are left out.
        /// </summary>
        private static void CaptureSlots()
        {
            _slotImages.Clear();
            var slots = LobbyToggle.Slots;
            if (slots == null) return;

            for (int i = 0; i < slots.childCount; i++)
            {
                var slot = slots.GetChild(i);
                bool invite = slot.name == MenuElements.LobbyInvite;

                AddSlotImage(slot, invite ? MenuElements.LobbyInviteFrame : MenuElements.LobbySlotFrame);
                if (invite) AddSlotImage(slot, MenuElements.LobbyInviteIcon);
            }
        }

        private static void AddSlotImage(Transform slot, string path)
        {
            var t = slot.Find(path);
            var image = t != null ? t.GetComponent<Image>() : null;
            if (image != null) _slotImages.Add((image, image.color));
        }

        private static void Apply()
        {
            try
            {
                var settings = T.Menu; // follows the chosen theme
                string align = settings.GetString(T.Lobby, T.TextAlign);

                if (_title != null)
                {
                    TextAlign.Apply(_title, align, _titleAlign);
                    string hex = settings.GetString(T.Lobby, T.ItemColor) ?? settings.GetString(T.MainMenu, T.TextColor);
                    _title.color = ColorOverride.Parse(hex).Apply(_titleColor);
                    AlignTitleOverSlots(align);
                }

                var slotTint = ColorOverride.Parse(settings.GetString(T.Lobby, T.SlotColor));
                foreach (var (image, vanilla) in _slotImages)
                    if (image != null) image.color = slotTint.Apply(vanilla);

                LobbyToggle.ShineColor = ShineTint.Parse(settings.GetString(T.MainMenu, T.ShineColor)).Apply(LobbyToggle.VanillaShineColor);
                LobbyToggle.ShineLength = settings.GetNumber(T.MainMenu, T.ShineLength) ?? 1f; // also refits the shine

                Canvas.ForceUpdateCanvases(); // the slots' grid must be laid out before measuring
                ElementPlacer.Place(_placed, settings, ElementPlacer.Padding(settings));
            }
            catch (Exception e)
            {
                ThemesMod.Log.Warning($"Couldn't style the lobby: {e.Message}");
            }
        }

        /// <summary>
        /// Vanilla's title box doesn't line up with the player slots, so for Left/Center/Right the box is
        /// stretched to span exactly the visible slots; the text then aligns over them. Vanilla alignment,
        /// or Solo (no slots), keeps vanilla's box.
        /// </summary>
        private static void AlignTitleOverSlots(string align)
        {
            var rt = _title.rectTransform;
            rt.anchoredPosition = _titlePos;
            rt.sizeDelta = _titleSize;

            var slots = LobbyToggle.Slots;
            if (align == null || align == T.VanillaAnchor) return;
            if (slots == null || !slots.gameObject.activeInHierarchy) return;

            Canvas.ForceUpdateCanvases(); // the slots' grid must be laid out before measuring
            var bounds = ElementPlacer.Bounds(slots.TryCast<RectTransform>(), rt.parent);
            if (bounds == null) return;

            rt.sizeDelta = new Vector2(bounds.Value.width, _titleSize.y);
            float shift = bounds.Value.center.x - rt.localPosition.x;
            rt.anchoredPosition = new Vector2(_titlePos.x + shift, _titlePos.y);
        }
    }
}
