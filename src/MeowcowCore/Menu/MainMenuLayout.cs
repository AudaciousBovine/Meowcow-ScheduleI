using System;
using System.Collections;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using Il2CppTMPro;
using MeowcowCore.Settings;
using S = MeowcowCore.Settings.CoreSettings;
using UObject = UnityEngine.Object;
using MenuScreen = Il2CppScheduleOne.UI.MainMenu.MenuScreen;

namespace MeowcowCore.Menu
{
    /// <summary>
    /// Places the main menu's header, version, list and socials from Core's settings, and styles the
    /// list. The lobby panel is handled by LobbyTweaks, since it also appears in game.
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
        private static bool _subscribed;
        private static Vector2Int _screen;

        /// <summary>Called when the Menu scene loads. Waits a frame so vanilla's Awake/Start have run.</summary>
        internal static void OnMenuLoaded()
        {
            if (!_subscribed)
            {
                S.Changed += Refresh;
                _subscribed = true;
            }
            MelonCoroutines.Start(CaptureNextFrame());
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

        private static IEnumerator CaptureNextFrame()
        {
            yield return null;
            try
            {
                Capture();
                _pending = true;
            }
            catch (Exception e)
            {
                CoreMod.Log.Error($"Main menu layout setup failed: {e}");
            }
        }

        private static void Capture()
        {
            _placed.Clear();
            _socialIcons.Clear();
            _version = null;
            _label = null;
            _logo = null;
            _home = FindHome();
            if (_home == null) { CoreMod.Log.Warning("Main menu Home screen not found; layout unchanged"); return; }

            // Find everything first: moving the header out of Bank would break the version's path.
            var found = new List<(MenuElement element, RectTransform rect)>();
            foreach (var element in MenuElements.Home)
            {
                var t = _home.transform.Find(element.Path);
                var rt = t != null ? t.TryCast<RectTransform>() : null;
                if (rt == null) { CoreMod.Log.Warning($"Main menu element '{element.Path}' not found"); continue; }
                found.Add((element, rt));
            }

            // Then move each straight under Home, keeping it exactly where it was on screen.
            foreach (var (element, rt) in found)
            {
                rt.SetParent(_home.transform, true);
                SetAnchorKeepRect(rt, element.Anchor);
                if (element.SectionId == S.MenuList) MenuList.Setup(rt); // adds Meowcow Options before vanilla is recorded
                if (element.SectionId == S.Version)
                {
                    _version = rt.GetComponent<TMP_Text>();
                    if (_version != null)
                    {
                        _versionAlign = _version.horizontalAlignment;
                        _versionColor = _version.color;
                    }
                }
                if (element.SectionId == S.Header)
                {
                    _logo = rt.GetComponent<Image>();
                    if (_logo != null) _logoColor = _logo.color;
                }
                if (element.SectionId == S.Socials)
                {
                    for (int i = 0; i < rt.childCount; i++)
                    {
                        var iconT = rt.GetChild(i).Find(MenuElements.SocialIcon);
                        var icon = iconT != null ? iconT.GetComponent<Image>() : null;
                        if (icon != null) _socialIcons.Add((icon, icon.color));
                    }
                }
                _placed.Add(ElementPlacer.Record(element.SectionId, rt, manageActive: true));
            }

            // Core's custom label: created after everything else is in place, beside the version.
            if (_version != null)
            {
                try
                {
                    _label = CreateLabel(_version);
                    _placed.Add(ElementPlacer.Record(S.Label, _label.rectTransform, manageActive: true));
                }
                catch (Exception e) { CoreMod.Log.Warning($"Couldn't create the custom label: {e.Message}"); }
            }

            CoreMod.Log.Msg($"Main menu layout: captured {_placed.Count} elements");
        }

        /// <summary>
        /// A copy of the version text (same font and size), left-aligned, placed just right of the version.
        /// That spot is recorded as its "vanilla" placement, so the Vanilla anchor puts it back there.
        /// </summary>
        private static TMP_Text CreateLabel(TMP_Text version)
        {
            var go = UObject.Instantiate(version.gameObject, version.transform.parent);
            go.name = MenuElements.LabelName;

            // Drop the version script (it writes the game version); keep only the text itself.
            foreach (var behaviour in go.GetComponents<MonoBehaviour>())
                if (behaviour.TryCast<TMP_Text>() == null) UObject.DestroyImmediate(behaviour);

            var label = go.GetComponent<TMP_Text>();
            label.text = S.LabelDefaultText;
            label.horizontalAlignment = HorizontalAlignmentOptions.Left;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;

            // Line its first letter up just after the version's last one.
            var space = _home.transform;
            float versionRight = space.InverseTransformPoint(version.rectTransform.TransformPoint(
                new Vector3(ElementPlacer.VisibleRect(version).xMax, 0f))).x;
            float labelLeft = space.InverseTransformPoint(label.rectTransform.TransformPoint(
                new Vector3(ElementPlacer.VisibleRect(label).xMin, 0f))).x;
            label.rectTransform.position += space.TransformVector(new Vector3(versionRight + MenuElements.LabelGap - labelLeft, 0f, 0f));

            return label;
        }

        private static MenuScreen FindHome()
        {
            foreach (var screen in UObject.FindObjectsOfType<MenuScreen>(true))
                if (screen.OpenOnStart && screen.PreviousScreen == null && screen.name == MenuElements.HomeName)
                    return screen;
            return null;
        }

        /// <summary>Changes a rect's anchors without moving or resizing it on screen.</summary>
        private static void SetAnchorKeepRect(RectTransform rt, Vector2 anchor)
        {
            Vector2 size = rt.rect.size;
            Vector3 position = rt.position;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.sizeDelta = size;
            rt.position = position;
        }

        /// <summary>Home fades and scales when screens change; only measure once it's fully open.</summary>
        private static bool IsSettled() =>
            _home.IsOpen && _home.gameObject.activeInHierarchy && Mathf.Abs(_home.transform.localScale.x - 1f) < 0.001f;

        // ---------- Applying ----------

        /// <summary>
        /// Text takes its item's own colour, else the global text colour. The logo and social icons are
        /// images and only take their own tint, so coloured artwork is never recoloured by accident.
        /// </summary>
        private static void ApplyColors(ISettingsSource settings)
        {
            string global = settings.GetString(S.MainMenu, S.TextColor);
            ColorOverride TextColor(string section) => ColorOverride.Parse(settings.GetString(section, S.ItemColor) ?? global);

            MenuList.ApplyTextColor(TextColor(S.MenuList));
            if (_label != null)
                _label.color = ColorOverride.Parse(settings.GetString(S.Label, S.ItemColor) ?? global ?? S.LabelDefaultColor)
                                            .Apply(Color.white);
            if (_version != null) _version.color = TextColor(S.Version).Apply(_versionColor);

            if (_logo != null) _logo.color = ColorOverride.Parse(settings.GetString(S.Header, S.ItemColor)).Apply(_logoColor);

            var iconTint = ColorOverride.Parse(settings.GetString(S.Socials, S.ItemColor));
            foreach (var (icon, vanilla) in _socialIcons)
                if (icon != null) icon.color = iconTint.Apply(vanilla);
        }

        private static void Apply()
        {
            var settings = S.Menu; // follows the chosen theme
            MenuList.ApplyStyle(settings.GetString(S.MenuList, S.TextAlign),
                                settings.GetNumber(S.MainMenu, S.ShineLength) ?? 1f,
                                ShineTint.Parse(settings.GetString(S.MainMenu, S.ShineColor)));
            ShineArt.ApplyTextAlign(_version, settings.GetString(S.Version, S.TextAlign), _versionAlign);
            if (_label != null) _label.text = settings.GetString(S.Label, S.LabelText) ?? S.LabelDefaultText;
            ApplyColors(settings);
            Canvas.ForceUpdateCanvases(); // layout groups (socials, list) must be positioned before measuring

            float padding = ElementPlacer.Padding(settings);
            foreach (var p in _placed)
            {
                try { ElementPlacer.Place(p, settings, padding); }
                catch (Exception e) { CoreMod.Log.Warning($"Couldn't place '{p.SectionId}': {e.Message}"); }
            }
        }
    }
}