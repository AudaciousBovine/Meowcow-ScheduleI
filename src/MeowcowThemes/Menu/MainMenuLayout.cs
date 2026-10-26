using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Il2CppTMPro;
using MeowcowCore.Menu;
using MeowcowCore.Settings;
using T = MeowcowThemes.Settings.ThemeSettings;
using MenuScreen = Il2CppScheduleOne.UI.MainMenu.MenuScreen;

namespace MeowcowThemes.Menu
{
    /// <summary>
    /// Places and colours the main menu's header, version, list, socials and Core's label from the
    /// chosen theme, and styles the list. Starts when Core's MainMenu.Ready fires, so Core's additions
    /// (the Meowcow Options button, the label) are part of the recorded "vanilla" state.
    /// </summary>
    internal static class MainMenuLayout
    {
        private static MenuScreen _home;
        private static readonly List<PlacedElement> _placed = new List<PlacedElement>();
        private static TMP_Text _version;
        private static HorizontalAlignmentOptions _versionAlign;
        private static Color _versionColor;
        private static TMP_Text _label;
        private static Image _logo;
        private static Color _logoColor;
        private static readonly List<(Image icon, Color vanilla)> _socialIcons = new List<(Image, Color)>();
        private static bool _pending;
        private static Vector2Int _screen;

        /// <summary>From Core: the main menu is set up.</summary>
        internal static void OnMenuReady()
        {
            try
            {
                Capture();
                _pending = true;
            }
            catch (Exception e)
            {
                ThemesMod.Log.Error($"Main menu theme setup failed: {e}");
            }
        }

        /// <summary>Re-apply on the next tick.</summary>
        internal static void Refresh() => _pending = true;

        /// <summary>Every frame: re-applies when settings or the screen size changed.</summary>
        internal static void Tick()
        {
            if (_home == null) return; // not on the main menu

            var screen = new Vector2Int(Screen.width, Screen.height);
            if (screen != _screen) { _screen = screen; _pending = true; }

            if (_pending && IsSettled())
            {
                _pending = false;
                Apply();
            }
        }

        // ---------- Capturing vanilla ----------

        private static void Capture()
        {
            _placed.Clear();
            _socialIcons.Clear();
            _version = null;
            _logo = null;
            _home = MainMenu.Home;
            if (_home == null) return;

            // Find everything first: moving the header out of Bank would break the version's path.
            var found = new List<(MenuElement element, RectTransform rect)>();
            foreach (var element in MenuElements.Home)
            {
                var t = _home.transform.Find(element.Path);
                var rt = t != null ? t.TryCast<RectTransform>() : null;
                if (rt == null) { ThemesMod.Log.Warning($"Main menu element '{element.Path}' not found"); continue; }
                found.Add((element, rt));
            }

            // Then move each straight under Home, keeping it exactly where it was on screen.
            foreach (var (element, rt) in found)
            {
                rt.SetParent(_home.transform, true);
                UiTools.SetAnchorKeepRect(rt, element.Anchor);
                CaptureDetails(element.SectionId, rt);
                _placed.Add(ElementPlacer.Record(element.SectionId, rt, manageActive: true));
            }

            // Core's label: already under Home, beside the version.
            _label = MainMenu.Label;
            if (_label != null) _placed.Add(ElementPlacer.Record(T.Label, _label.rectTransform, manageActive: true));

            ThemesMod.Log.Msg($"Main menu theme: captured {_placed.Count} elements");
        }

        private static void CaptureDetails(string sectionId, RectTransform rt)
        {
            if (sectionId == T.MenuList)
            {
                MenuListStyle.Setup(rt);
            }
            else if (sectionId == T.Version)
            {
                _version = rt.GetComponent<TMP_Text>();
                if (_version != null)
                {
                    _versionAlign = _version.horizontalAlignment;
                    _versionColor = _version.color;
                }
            }
            else if (sectionId == T.Header)
            {
                _logo = rt.GetComponent<Image>();
                if (_logo != null) _logoColor = _logo.color;
            }
            else if (sectionId == T.Socials)
            {
                for (int i = 0; i < rt.childCount; i++)
                {
                    var iconT = rt.GetChild(i).Find(MenuElements.SocialIcon);
                    var icon = iconT != null ? iconT.GetComponent<Image>() : null;
                    if (icon != null) _socialIcons.Add((icon, icon.color));
                }
            }
        }

        /// <summary>Home fades and scales when screens change; only measure once it's fully open.</summary>
        private static bool IsSettled() =>
            _home.IsOpen && _home.gameObject.activeInHierarchy && Mathf.Abs(_home.transform.localScale.x - 1f) < 0.001f;

        // ---------- Applying ----------

        private static void Apply()
        {
            var settings = T.Menu; // follows the chosen theme

            MenuListStyle.ApplyStyle(settings.GetString(T.MenuList, T.TextAlign),
                                     settings.GetNumber(T.MainMenu, T.ShineLength) ?? 1f,
                                     ShineTint.Parse(settings.GetString(T.MainMenu, T.ShineColor)));
            TextAlign.Apply(_version, settings.GetString(T.Version, T.TextAlign), _versionAlign);
            if (_label != null) _label.text = settings.GetString(T.Label, T.LabelText) ?? MainMenu.DefaultLabelText;
            ApplyColors(settings);

            Canvas.ForceUpdateCanvases(); // layout groups (socials, list) must be positioned before measuring

            float padding = ElementPlacer.Padding(settings);
            foreach (var p in _placed)
            {
                try { ElementPlacer.Place(p, settings, padding); }
                catch (Exception e) { ThemesMod.Log.Warning($"Couldn't place '{p.SectionId}': {e.Message}"); }
            }
        }

        /// <summary>
        /// Text takes its item's own colour, else the global text colour. The logo and social icons are
        /// images and only take their own tint, so coloured artwork is never recoloured by accident.
        /// </summary>
        private static void ApplyColors(ISettingsSource settings)
        {
            string global = settings.GetString(T.MainMenu, T.TextColor);
            ColorOverride TextColor(string section) => ColorOverride.Parse(settings.GetString(section, T.ItemColor) ?? global);

            MenuListStyle.ApplyTextColor(TextColor(T.MenuList));
            if (_version != null) _version.color = TextColor(T.Version).Apply(_versionColor);
            if (_label != null) _label.color = TextColor(T.Label).Apply(MainMenu.DefaultLabelColor); // pink unless set

            if (_logo != null) _logo.color = ColorOverride.Parse(settings.GetString(T.Header, T.ItemColor)).Apply(_logoColor);

            var iconTint = ColorOverride.Parse(settings.GetString(T.Socials, T.ItemColor));
            foreach (var (icon, vanilla) in _socialIcons)
                if (icon != null) icon.color = iconTint.Apply(vanilla);
        }
    }
}
