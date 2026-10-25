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
using CyclerEvent = Il2CppScheduleOne.UI.CyclerEvent;

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
        private const float PromptGap = 6f;          // space between the bumper prompts and the first tab
        private const float PromptSpacing = 8f;      // space between the two prompts
        private const float ScrollbarGap = 14f;      // space between the panel and the scrollbar
        private const float ScrollbarMargin = 16f;   // inset from the panel's top and bottom
        private const float ScrollSensitivity = 30f;

        private static CreatorUI _ui;
        private static RectTransform _footer;
        private static TMP_FontAsset _tabFont;
        private static RectTransform _tabStrip;
        private static Button _cancel;
        private static ScrollRect _scroll;
        private static Slider _scrollbar;
        private static RectTransform _viewport;
        private static bool _syncing;
        private static CyclerEvent _onCycled; // kept referenced so the converted delegate isn't collected
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

        public static void SetTabs(IList<string> names, CommitMode mode, string commitLabel)
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
            SetLabel(_done, string.IsNullOrWhiteSpace(commitLabel) ? "Done" : commitLabel);
        }

        /// <summary>The scroll view around the tab contents; vanilla panels use it to scroll to gamepad selections.</summary>
        public static ScrollRect Scroll => _scroll;

        public static void Select(int index)
        {
            if (_ui == null || index < 0 || index >= _tabCount) return;

            // OpenWindow selects the tab's gamepad panel, which scrolls through our ScrollRect,
            // so the scroll view must already point at this tab's contents.
            _scroll.content = _ui._windows[index].Container;
            _ui.OpenWindow(index);
            SyncToWindow(index);
        }

        /// <summary>Updates the tab highlight and scroll view to match the open window.</summary>
        private static void SyncToWindow(int index)
        {
            if (index < 0 || index >= _tabCount) return;

            for (int i = 0; i < _tabCount; i++)
                _tabs[i].colors = i == index ? _selectedColors : _unselectedColors;

            // Point the scroll view at this tab's contents, back at the top.
            var content = _ui._windows[index].Container;
            content.anchoredPosition = Vector2.zero;
            _scroll.content = content;
            _scroll.verticalNormalizedPosition = 1f;

            bool scrolls = content.rect.height > _viewport.rect.height + 1f;
            _scrollbar.gameObject.SetActive(scrolls);
            _scroll.vertical = scrolls;
            SetScrollbarSilently(0f);
        }

        /// <summary>
        /// Gamepad bumpers: vanilla's cycler handler (subscribed first, in the interface's Awake)
        /// has already opened the new window when this runs, so just follow it.
        /// </summary>
        private static void OnCycled(int direction) => SyncToWindow(_ui._openWindowIndex);

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

            BuildScrollView(menu, contents);

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

            MoveBumperPrompts(ui, menu);

            if (ui._cyclerController != null)
            {
                _onCycled = DelegateSupport.ConvertDelegate<CyclerEvent>(new Action<int>(OnCycled));
                ui._cyclerController.SubscribeToCyclerEvent(_onCycled);
            }

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


        /// <summary>
        /// Vanilla shows the LB/RB prompts inside the Back/Next buttons at the top bar's edges. With
        /// side tabs, they read better side by side just above the first tab. The prompts keep their
        /// own components, so they still only show on gamepad.
        /// </summary>
        private static void MoveBumperPrompts(CreatorUI ui, RectTransform menu)
        {
            var left = ui._backButton.transform.Find("Left bumper prompt");
            var right = ui._nextButton.transform.Find("Right bumper prompt");
            if (left == null || right == null) { MirrorMod.Log.Warning("Bumper prompts not found; leaving them"); return; }

            var holderGo = new GameObject("MirrorTabPrompts");
            var holder = holderGo.AddComponent<RectTransform>();
            holder.SetParent(menu, false);
            holder.anchorMin = new Vector2(0f, 1f);
            holder.anchorMax = new Vector2(0f, 1f);
            holder.pivot = new Vector2(1f, 0f); // bottom-right: sits just above the first tab
            holder.sizeDelta = new Vector2(TabWidth, TabHeight);
            holder.anchoredPosition = new Vector2(0f, -TabTopOffset + PromptGap);

            var row = holderGo.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.LowerCenter;
            row.spacing = PromptSpacing;
            row.childControlWidth = false;
            row.childControlHeight = false;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;

            left.SetParent(holder, false);
            right.SetParent(holder, false);
        }

        // ---------- Scrolling ----------

        /// <summary>
        /// Contents becomes the scroll viewport (clipped, catches the mouse wheel); each tab's container
        /// is swapped in as the scroll content when selected. The scrollbar is a vertical clone of the
        /// creator's rotation slider, outside the panel's right edge.
        /// </summary>
        private static void BuildScrollView(RectTransform menu, RectTransform contents)
        {
            var go = contents.gameObject;
            _viewport = contents;

            if (go.GetComponent<RectMask2D>() == null) go.AddComponent<RectMask2D>();

            // An invisible graphic so the wheel works over empty space, not just over rows.
            if (go.GetComponent<Graphic>() == null)
            {
                var catcher = go.AddComponent<Image>();
                catcher.color = new Color(0f, 0f, 0f, 0f);
            }

            _scroll = go.GetComponent<ScrollRect>() ?? go.AddComponent<ScrollRect>();
            _scroll.viewport = contents;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = ScrollSensitivity;
            _scroll.inertia = true;
            _scroll.onValueChanged.AddListener(
                DelegateSupport.ConvertDelegate<UnityAction<Vector2>>(new Action<Vector2>(OnScrolled)));

            // Scrollbar: the rotation slider, cloned and turned on its side (its handle is round,
            // so rotating it looks the same). Rotated -90 degrees, its value runs 0 at the top to 1 at the bottom.
            var source = menu.parent.Find("Slider").GetComponent<Slider>();
            float thickness = source.GetComponent<RectTransform>().rect.height;

            var clone = UObject.Instantiate(source.gameObject, menu);
            clone.name = "MirrorScrollbar";
            _scrollbar = clone.GetComponent<Slider>();
            _scrollbar.onValueChanged.RemoveAllListeners();
            _scrollbar.minValue = 0f;
            _scrollbar.maxValue = 1f;
            _scrollbar.wholeNumbers = false;
            _scrollbar.onValueChanged.AddListener(
                DelegateSupport.ConvertDelegate<UnityAction<float>>(new Action<float>(OnScrollbarMoved)));

            var rt = clone.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(PanelHeight - ScrollbarMargin * 2f, thickness);
            rt.anchoredPosition = new Vector2(ScrollbarGap + thickness / 2f, 0f);
            rt.localRotation = Quaternion.Euler(0f, 0f, -90f);
        }

        private static void OnScrolled(Vector2 position)
        {
            if (_syncing) return;
            SetScrollbarSilently(1f - position.y);
        }

        private static void OnScrollbarMoved(float value)
        {
            if (_syncing) return;
            _syncing = true;
            _scroll.verticalNormalizedPosition = 1f - value;
            _syncing = false;
        }

        private static void SetScrollbarSilently(float value)
        {
            _syncing = true;
            _scrollbar.SetValueWithoutNotify(Mathf.Clamp01(value));
            _syncing = false;
        }

        // ---------- Footer actions ----------

        private static void OnDone()
        {
            var creator = UObject.FindObjectOfType<Creator>(true);
            if (creator == null) return;

            if (_commitMode == CommitMode.Vanilla)
            {
                Mirror.EndSession();
                creator.Done(); // the intro handles everything from here
                return;
            }

            var appearance = creator._currentState?.Appearance;

            // On a vanilla shop, the shop's own Close() applies it and restores the camera.
            if (ShopSession.Active)
            {
                Mirror.EndSession();
                ShopSession.Finish(apply: true, appearance);
                MirrorMod.Log.Msg("Appearance applied through the shop");
                return;
            }

            var player = Player.Local;
            if (appearance != null && player != null)
            {
                player.SetAppearance_Server(appearance.Clone());
                MirrorMod.Log.Msg("Appearance applied");
            }

            Mirror.EndSession();
            creator.Close();
            RestoreCamera();
        }

        private static void OnCancel()
        {
            if (ShopSession.Active)
            {
                Mirror.EndSession();
                ShopSession.Finish(apply: false, null);
                MirrorMod.Log.Msg("Shop visit cancelled");
                return;
            }

            var creator = UObject.FindObjectOfType<Creator>(true);
            if (creator == null) return;

            Mirror.EndSession();
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
