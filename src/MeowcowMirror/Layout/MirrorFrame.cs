using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Il2CppInterop.Runtime;
using Il2CppTMPro;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.PlayerScripts;
using UObject = UnityEngine.Object;
using Creator = Il2CppScheduleOne.CharacterCreator.CharacterCreator;
using CreatorUI = Il2CppScheduleOne.CharacterCreator.CharacterCreatorInterface;

namespace MeowcowMirror.Layout
{
    /// <summary>
    /// Mirror's creator frame: a wider panel, side tabs, a left-aligned title, and a footer with
    /// Cancel and Done. Built from clones of the game's own UI pieces so it matches vanilla style.
    /// </summary>
    public static class MirrorFrame
    {
        // Canvas units (1920x1080 reference), taken from the mockup. Moves to config later.
        private const float PanelWidth = 700f;   // two vanilla rows side by side
        private const float PanelHeight = 650f;
        private static readonly Vector2 PanelPosition = new Vector2(-396f, -26f);
        private const float TabWidth = 143f;     // visible part of a tab
        private const float TabOverlap = 18f;    // extra length past the panel edge, clipped away
        private const float TabHeight = 40f;
        private const float TabGap = 6f;
        private const float TabTopOffset = 10f;
        private const float BarPadding = 12f;

        private static CreatorUI _ui;
        private static RectTransform _footer;
        private static TMP_FontAsset _tabFont;
        private static RectTransform _tabStrip;
        private static Button _cancel;
        private static Button _done;
        private static readonly List<Button> _tabs = new List<Button>();
        private static ColorBlock _selectedColors;
        private static ColorBlock _unselectedColors;
        private static CommitMode _commitMode;
        private static int _tabCount;

        /// <summary>Builds the frame once per interface instance. Call before rows are positioned.</summary>
        public static void EnsureBuilt(CreatorUI ui, RectTransform contents)
        {
            if (_ui != null && _ui == ui && _footer != null) return;
            Build(ui, contents);
        }

        public static void SetTabs(IList<string> names, CommitMode mode)
        {
            _commitMode = mode;
            _tabCount = names.Count;

            while (_tabs.Count < names.Count) _tabs.Add(CreateTab(_tabs.Count));

            for (int i = 0; i < _tabs.Count; i++)
            {
                bool used = i < names.Count;
                _tabs[i].gameObject.SetActive(used);
                if (used) SetLabel(_tabs[i], names[i]);
            }

            _cancel.gameObject.SetActive(mode == CommitMode.Mirror);
        }

        public static void Select(int index)
        {
            if (_ui == null || index < 0 || index >= _tabCount) return;

            _ui.OpenWindow(index);
            for (int i = 0; i < _tabCount; i++)
                _tabs[i].colors = i == index ? _selectedColors : _unselectedColors;
        }

        // ---------- Building ----------

        private static void Build(CreatorUI ui, RectTransform contents)
        {
            _ui = ui;
            _tabs.Clear();

            var menu = contents.parent.GetComponent<RectTransform>();
                        var topbar = menu.Find("Topbar").GetComponent<RectTransform>();
            float barHeight = topbar.rect.height;

            // Panel size and position.
            menu.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            menu.anchoredPosition = PanelPosition;

            // Title: left-aligned across the top bar; the right side is kept free for presets.
            var label = ui._categoryLabel;
            label.alignment = TextAlignmentOptions.Left;
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(BarPadding, 0f);
            labelRect.offsetMax = new Vector2(-BarPadding, 0f);

            // Tab colors come from the vanilla buttons: Next's blue when selected, Back's disabled grey otherwise.
            _selectedColors = ui._nextButton.colors;
            var grey = ui._backButton.colors.disabledColor;
            grey.a = 1f;
            _unselectedColors = _selectedColors;
            _unselectedColors.normalColor = grey;
            _unselectedColors.selectedColor = grey;

            // Footer: an empty holder pinned to the bottom, with a vertically flipped copy of the
            // top bar behind the buttons so its rounded corners face down.
            var footerGo = new GameObject("MirrorFooter");
            _footer = footerGo.AddComponent<RectTransform>();
            _footer.SetParent(menu, false);
            _footer.anchorMin = new Vector2(0f, 0f);
            _footer.anchorMax = new Vector2(1f, 0f);
            _footer.pivot = new Vector2(0.5f, 0f);
            _footer.sizeDelta = new Vector2(0f, barHeight);
            _footer.anchoredPosition = Vector2.zero;
            _footer.SetAsLastSibling();

            var footerBg = UObject.Instantiate(topbar.gameObject, _footer).GetComponent<RectTransform>();
            footerBg.name = "Background";
            for (int i = footerBg.childCount - 1; i >= 0; i--)
                UObject.DestroyImmediate(footerBg.GetChild(i).gameObject);
            footerBg.anchorMin = Vector2.zero;
            footerBg.anchorMax = Vector2.one;
            footerBg.pivot = new Vector2(0.5f, 0.5f);
            footerBg.offsetMin = Vector2.zero;
            footerBg.offsetMax = Vector2.zero;
            footerBg.localScale = new Vector3(1f, -1f, 1f);

            // Contents now stops above the footer.
            contents.offsetMin = new Vector2(contents.offsetMin.x, barHeight);

            _cancel = CloneButton(ui._doneButton, _footer, "Cancel"); // Back's style reads as plain text here
            PinToFooter(_cancel, left: true);
            _cancel.onClick.AddListener(ToUnityAction(OnCancel));

            _done = CloneButton(ui._doneButton, _footer, null);
            PinToFooter(_done, left: false);
            _done.onClick.AddListener(ToUnityAction(OnDone));

            // Vanilla Back/Next are replaced by the side tabs.
            ui._backButton.gameObject.SetActive(false);
            ui._nextButton.gameObject.SetActive(false);

            // Tab strip: covers only the visible part of the tabs, with a RectMask2D clipping the
            // part that reaches under the panel. Clipping (rather than relying on draw order) keeps
            // the hidden part hidden during vanilla's fade, where every piece is see-through at once.
            var stripGo = new GameObject("MirrorTabs");
            _tabStrip = stripGo.AddComponent<RectTransform>();
            stripGo.AddComponent<RectMask2D>();
            _tabStrip.SetParent(menu, false);
            _tabStrip.anchorMin = new Vector2(0f, 1f);
            _tabStrip.anchorMax = new Vector2(0f, 1f);
            _tabStrip.pivot = new Vector2(1f, 1f);
            _tabStrip.sizeDelta = new Vector2(TabWidth, PanelHeight);
            _tabStrip.anchoredPosition = new Vector2(0f, -TabTopOffset);

            _tabFont = ui._categoryLabel.font; // upright font, like the mockup

            MirrorMod.Log.Msg($"Frame built (bar height {barHeight:0.#})");
        }

        private static Button CreateTab(int index)
        {
            var tab = CloneButton(_ui._nextButton, _tabStrip, null);
            tab.name = "MirrorTab" + index;

            var rt = tab.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(TabWidth + TabOverlap, TabHeight);
            rt.anchoredPosition = new Vector2(TabOverlap, -index * (TabHeight + TabGap)); // overlap gets clipped

            // Center the label in the visible part of the tab, not the part under the panel.
            var text = tab.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null)
            {
                if (_tabFont != null) text.font = _tabFont;
                text.fontStyle = FontStyles.Normal;
                var textRect = text.rectTransform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(4f, 0f);
                textRect.offsetMax = new Vector2(-(TabOverlap + 4f), 0f);
            }

            int captured = index;
            tab.onClick.AddListener(ToUnityAction(() => Select(captured)));
            return tab;
        }

        private static Button CloneButton(Button source, Transform parent, string label)
        {
            var go = UObject.Instantiate(source.gameObject, parent);
            go.SetActive(true);
            var button = go.GetComponent<Button>();
            button.onClick.RemoveAllListeners(); // vanilla adds its listeners at runtime, so this clears them all
            button.interactable = true;
            if (label != null) SetLabel(button, label);
            return button;
        }

        private static void SetLabel(Button button, string label)
        {
            var text = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null) text.text = label;
        }

        private static void PinToFooter(Button button, bool left)
        {
            var rt = button.GetComponent<RectTransform>();
            float x = left ? 0f : 1f;
            rt.anchorMin = new Vector2(x, 0.5f);
            rt.anchorMax = new Vector2(x, 0.5f);
            rt.pivot = new Vector2(x, 0.5f);
            rt.anchoredPosition = new Vector2(left ? BarPadding : -BarPadding, 0f);
        }

        private static UnityAction ToUnityAction(Action action) => DelegateSupport.ConvertDelegate<UnityAction>(action);

        // ---------- Footer actions ----------

        private static void OnDone()
        {
            var creator = UObject.FindObjectOfType<Creator>(true);
            if (creator == null) return;

            if (_commitMode == CommitMode.Vanilla)
            {
                creator.Done(); // the intro handles everything from here
                return;
            }

            var appearance = creator._currentState?.Appearance;
            var player = Player.Local;
            if (appearance != null && player != null)
            {
                player.SetAppearance_Server(appearance.Clone());
                MirrorMod.Log.Msg("Appearance applied");
            }

            creator.Close();
            RestoreCamera();
        }

        private static void OnCancel()
        {
            var creator = UObject.FindObjectOfType<Creator>(true);
            if (creator == null) return;

            creator.Close();
            RestoreCamera();
            MirrorMod.Log.Msg("Edit cancelled");
        }

        /// <summary>Undoes the creator's camera override; vanilla leaves this to IntroManager.</summary>
        internal static void RestoreCamera()
        {
            var cam = PlayerSingleton<PlayerCamera>.Instance;
            if (cam == null) { MirrorMod.Log.Warning("PlayerCamera not found; camera not restored"); return; }

            cam.StopTransformOverride(0f, false, false);
            cam.StopFOVOverride(0f);
        }
    }
}