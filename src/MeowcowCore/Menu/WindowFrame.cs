using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Il2CppInterop.Runtime;
using Il2CppTMPro;
using MeowcowCore.Settings;
using S = MeowcowCore.Settings.CoreSettings;
using UObject = UnityEngine.Object;
using MenuScreen = Il2CppScheduleOne.UI.MainMenu.MenuScreen;
using SettingsScreen = Il2CppScheduleOne.UI.MainMenu.SettingsScreen;

namespace MeowcowCore.Menu
{
    /// <summary>
    /// Core's shared window frame for menu screens: one shared size, draggable by the title (position
    /// remembered, kept on screen, double-click to centre), tabs down the left, and a Back button in the
    /// footer. Restyles the vanilla screen in place, so its tabs, rows and logic keep working. If any
    /// expected piece is missing, that screen is left vanilla.
    /// </summary>
    internal static class WindowFrame
    {
        // Canvas units (1920x1080 reference)
        private const float Margin = 20f;          // inner padding
        private const float TitleHeight = 70f;     // title bar, also the drag handle
        private const float FooterHeight = 50f;
        private const float TabWidth = 170f;
        private const float TabHeight = 34f;
        private const float TabSpacing = 4f;
        private const float BumperRow = 30f;       // room above the tabs for the LB/RB prompts (gamepad only)
        private const float DoubleClickTime = 0.3f;

        private sealed class Frame
        {
            public MenuScreen Screen;
            public RectTransform Root;
            public RectTransform Pattern;
            public Vector2 PatternSize;
            public RectTransform Title;
            public Image Background, Content;
            public TMP_Text TitleText, BackText;
            public readonly List<(Image bg, Image check, TMP_Text label)> Tabs = new List<(Image, Image, TMP_Text)>();
            public Color BackgroundColor, ContentColor, TitleColor, TabColor, TabSelectedColor, TabTextColor, BackTextColor;
        }

        private static readonly List<Frame> _frames = new List<Frame>();
        private static UnityAction _onBack; // kept referenced so the converted delegate isn't collected
        private static bool _subscribed;
        private static GameObject _cornerPrompt;
        private static bool _cornerPromptWasActive;

        // Dragging
        private static Frame _dragging;
        private static Vector2 _lastMouse;
        private static float _lastClick;

        private static bool Enabled => S.File == null || S.File.GetBool(S.Windows, S.ImprovedWindows, true);

        /// <summary>Called by MainMenu after Core's menu setup.</summary>
        internal static void OnMenuReady()
        {
            if (!_subscribed)
            {
                _subscribed = true;
                S.Changed += ApplyLayoutToAll;
                WindowStyle.Changed += ApplyColorsToAll;
            }

            _frames.Clear();
            _dragging = null;
            if (!Enabled) return;

            FindCornerPrompt();
            TryFrame(UObject.FindObjectOfType<SettingsScreen>(true));
        }

        /// <summary>Every frame: dragging, and hiding the corner prompt while a framed window is open.</summary>
        internal static void Tick()
        {
            if (_frames.Count == 0) return;

            Frame open = null;
            foreach (var f in _frames)
                if (f.Screen != null && f.Screen.IsOpen) { open = f; break; }

            UpdateCornerPrompt(open != null);
            if (open != null) TickDrag(open);
            else _dragging = null;
        }

        // ---------- Building ----------

        private static void TryFrame(MenuScreen screen)
        {
            if (screen == null) return;
            try
            {
                var frame = Build(screen);
                if (frame == null) { CoreMod.Log.Warning($"'{screen.name}' doesn't look as expected; left vanilla"); return; }

                _frames.Add(frame);
                ApplyLayout(frame);
                ApplyColors(frame);
                CoreMod.Log.Msg($"Window frame applied to '{screen.name}'");
            }
            catch (Exception e)
            {
                CoreMod.Log.Error($"Window frame failed on '{screen.name}': {e}");
            }
        }

        private static Frame Build(MenuScreen screen)
        {
            var root = screen.transform.TryCast<RectTransform>();
            var background = FindRect(root, MenuPaths.FrameBackground);
            var title = FindRect(root, MenuPaths.FrameTitle);
            var tabs = FindRect(root, MenuPaths.FrameTabs);
            var tabsLayout = FindRect(root, MenuPaths.FrameTabsLayout);
            var content = FindRect(root, MenuPaths.FrameContent);
            if (root == null || background == null || title == null || tabs == null || tabsLayout == null || content == null)
                return null;

            var frame = new Frame
            {
                Screen = screen,
                Root = root,
                Title = title,
                Pattern = FindRect(root, MenuPaths.FramePattern),
                Background = background.GetComponent<Image>(),
                Content = content.GetComponent<Image>(),
                TitleText = title.GetComponent<TMP_Text>(),
            };
            if (frame.Pattern != null) frame.PatternSize = frame.Pattern.sizeDelta;

            // Tabs: re-stack vertically. A GameObject can't hold two layout groups, so the horizontal one goes first.
            var horizontal = tabsLayout.GetComponent<HorizontalLayoutGroup>();
            if (horizontal != null) UObject.DestroyImmediate(horizontal);
            var vertical = tabsLayout.GetComponent<VerticalLayoutGroup>() ?? tabsLayout.gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.spacing = TabSpacing;
            vertical.childAlignment = TextAnchor.UpperLeft;
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;

            for (int i = 0; i < tabsLayout.childCount; i++)
            {
                var tab = tabsLayout.GetChild(i);
                var element = tab.GetComponent<LayoutElement>() ?? tab.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = TabHeight;
                element.minHeight = TabHeight;

                frame.Tabs.Add((FindImage(tab, MenuPaths.TabBackground), FindImage(tab, MenuPaths.TabCheckmark), FindText(tab, MenuPaths.TabLabel)));
            }

            // Gamepad bumper prompts: side by side above the tab column.
            PlaceBumper(FindRect(root, MenuPaths.FrameLeftBumper), 0f);
            PlaceBumper(FindRect(root, MenuPaths.FrameRightBumper), 30f);

            frame.BackText = BuildBackButton(root, screen);
            RecordColors(frame);
            return frame;
        }

        private static void PlaceBumper(RectTransform bumper, float x)
        {
            if (bumper == null) return;
            bumper.anchorMin = new Vector2(0f, 1f);
            bumper.anchorMax = new Vector2(0f, 1f);
            bumper.pivot = new Vector2(0f, 0f);
            bumper.anchoredPosition = new Vector2(Margin + x, -(TitleHeight + BumperRow - 2f));
        }

        /// <summary>
        /// A Back button in the footer, cloned from the save slots' dark Import button. It closes the screen
        /// exactly like Escape. Skipped (Escape still works) if the style source isn't found.
        /// </summary>
        private static TMP_Text BuildBackButton(RectTransform root, MenuScreen screen)
        {
            var source = FindDarkButton();
            if (source == null) { CoreMod.Log.Warning("Dark button style not found; no Back button (Escape still works)"); return null; }

            var go = UObject.Instantiate(source.gameObject, root);
            go.name = "MeowcowBack";
            go.SetActive(true);

            // Keep the look (Image, Button, text); drop the import script.
            foreach (var behaviour in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour.TryCast<Graphic>() == null && behaviour.TryCast<Selectable>() == null)
                    UObject.DestroyImmediate(behaviour);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.sizeDelta = new Vector2(120f, 30f);
            rt.anchoredPosition = new Vector2(Margin, (FooterHeight - 30f) / 2f);

            var button = go.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            _onBack ??= DelegateSupport.ConvertDelegate<UnityAction>(new Action(OnBack));
            button.onClick.AddListener(_onBack);

            var text = go.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.richText = true;
                text.text = "<color=#9A9A9A>Esc</color>   Back";
            }
            return text;
        }

        /// <summary>Back closes whichever framed screen is open.</summary>
        private static void OnBack()
        {
            try
            {
                foreach (var f in _frames)
                    if (f.Screen != null && f.Screen.IsOpen) { f.Screen.Close(); return; }
            }
            catch (Exception e)
            {
                CoreMod.Log.Error($"Back failed: {e}");
            }
        }

        private static Button FindDarkButton()
        {
            foreach (var button in UObject.FindObjectsOfType<Button>(true))
                if (button.name == MenuPaths.DarkButtonName && button.GetComponentInChildren<TMP_Text>(true) != null)
                    return button;
            return null;
        }

        // ---------- Layout ----------

        private static void ApplyLayoutToAll()
        {
            foreach (var f in _frames)
            {
                try { if (f.Root != null) ApplyLayout(f); }
                catch (Exception e) { CoreMod.Log.Warning($"Window layout failed: {e.Message}"); }
            }
        }

        /// <summary>Size, remembered position, and where the tabs, content and pattern sit inside.</summary>
        private static void ApplyLayout(Frame f)
        {
            var file = S.File;
            float width = file?.GetNumber(S.Windows, S.WindowWidth) ?? 1100f;
            float height = file?.GetNumber(S.Windows, S.WindowHeight) ?? 700f;

            var canvas = CanvasRect(f.Root);
            if (canvas != null)
            {
                width = Mathf.Min(width, canvas.rect.width);
                height = Mathf.Min(height, canvas.rect.height);
            }
            f.Root.sizeDelta = new Vector2(width, height);

            float? x = file?.GetNumber(S.Windows, S.WindowX);
            float? y = file?.GetNumber(S.Windows, S.WindowY);
            f.Root.anchoredPosition = Clamp(f, new Vector2(x ?? 0f, y ?? 0f));

            // Pattern behind: scaled up to cover the bigger window, keeping its shape.
            if (f.Pattern != null && f.PatternSize.x > 0f && f.PatternSize.y > 0f)
            {
                float cover = Mathf.Max(width / f.PatternSize.x, height / f.PatternSize.y, 1f) * 1.05f;
                f.Pattern.sizeDelta = f.PatternSize * cover;
            }

            // Title bar across the top.
            f.Title.anchorMin = new Vector2(0f, 1f);
            f.Title.anchorMax = new Vector2(1f, 1f);
            f.Title.pivot = new Vector2(0.5f, 1f);
            f.Title.sizeDelta = new Vector2(0f, TitleHeight);
            f.Title.anchoredPosition = Vector2.zero;

            // Tab column on the left, below the bumper prompts and above the footer.
            var tabs = FindRect(f.Root, MenuPaths.FrameTabs);
            tabs.anchorMin = new Vector2(0f, 0f);
            tabs.anchorMax = new Vector2(0f, 1f);
            tabs.pivot = new Vector2(0f, 1f);
            tabs.offsetMin = new Vector2(Margin, FooterHeight);
            tabs.offsetMax = new Vector2(Margin + TabWidth, -(TitleHeight + BumperRow));

            var layout = FindRect(f.Root, MenuPaths.FrameTabsLayout);
            layout.anchorMin = Vector2.zero;
            layout.anchorMax = Vector2.one;
            layout.pivot = new Vector2(0.5f, 1f);
            layout.offsetMin = Vector2.zero;
            layout.offsetMax = Vector2.zero;

            // Content fills the rest.
            var content = FindRect(f.Root, MenuPaths.FrameContent);
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 0.5f);
            content.offsetMin = new Vector2(Margin * 2f + TabWidth, FooterHeight);
            content.offsetMax = new Vector2(-Margin, -TitleHeight);
        }

        /// <summary>Keeps the whole window on screen. The window is centre-anchored, so 0,0 is the middle.</summary>
        private static Vector2 Clamp(Frame f, Vector2 position)
        {
            var canvas = CanvasRect(f.Root);
            if (canvas == null) return position;

            float maxX = Mathf.Max(0f, (canvas.rect.width - f.Root.sizeDelta.x) / 2f);
            float maxY = Mathf.Max(0f, (canvas.rect.height - f.Root.sizeDelta.y) / 2f);
            return new Vector2(Mathf.Clamp(position.x, -maxX, maxX), Mathf.Clamp(position.y, -maxY, maxY));
        }

        // ---------- Dragging ----------

        private static void TickDrag(Frame f)
        {
            Vector2 mouse = Input.mousePosition;

            if (Input.GetMouseButtonDown(0) && UiTools.ContainsPointer(f.Title))
            {
                // Double-click the title: back to the centre.
                if (Time.unscaledTime - _lastClick < DoubleClickTime)
                {
                    _lastClick = 0f;
                    SavePosition(null);
                    f.Root.anchoredPosition = Clamp(f, Vector2.zero);
                    return;
                }
                _lastClick = Time.unscaledTime;
                _dragging = f;
                _lastMouse = mouse;
                return;
            }

            if (_dragging != f) return;

            if (!Input.GetMouseButton(0))
            {
                _dragging = null;
                SavePosition(f.Root.anchoredPosition);
                return;
            }

            // Screen pixels to canvas units.
            float scale = CanvasScale(f.Root);
            Vector2 delta = (mouse - _lastMouse) / scale;
            _lastMouse = mouse;
            if (delta != Vector2.zero) f.Root.anchoredPosition = Clamp(f, f.Root.anchoredPosition + delta);
        }

        private static void SavePosition(Vector2? position)
        {
            var file = S.File;
            if (file == null) return;
            if (position.HasValue)
            {
                file.Set(S.Windows, S.WindowX, Mathf.Round(position.Value.x));
                file.Set(S.Windows, S.WindowY, Mathf.Round(position.Value.y));
            }
            else
            {
                file.Reset(S.Windows, S.WindowX);
                file.Reset(S.Windows, S.WindowY);
            }
            file.Save();
        }

        // ---------- Colours ----------

        private static void RecordColors(Frame f)
        {
            if (f.Background != null) f.BackgroundColor = f.Background.color;
            if (f.Content != null) f.ContentColor = f.Content.color;
            if (f.TitleText != null) f.TitleColor = f.TitleText.color;
            if (f.BackText != null) f.BackTextColor = f.BackText.color;
            if (f.Tabs.Count > 0)
            {
                var (bg, check, label) = f.Tabs[0];
                if (bg != null) f.TabColor = bg.color;
                if (check != null) f.TabSelectedColor = check.color;
                if (label != null) f.TabTextColor = label.color;
            }
        }

        private static void ApplyColorsToAll()
        {
            foreach (var f in _frames)
                if (f.Root != null) ApplyColors(f);
        }

        private static void ApplyColors(Frame f)
        {
            if (f.Background != null) f.Background.color = WindowStyle.Or(WindowStyle.Background, f.BackgroundColor);
            if (f.Content != null) f.Content.color = WindowStyle.Or(WindowStyle.Content, f.ContentColor);
            if (f.TitleText != null) f.TitleText.color = WindowStyle.Or(WindowStyle.Title, f.TitleColor);
            if (f.BackText != null) f.BackText.color = WindowStyle.Or(WindowStyle.ButtonText, f.BackTextColor);
            foreach (var (bg, check, label) in f.Tabs)
            {
                if (bg != null) bg.color = WindowStyle.Or(WindowStyle.Tab, f.TabColor);
                if (check != null) check.color = WindowStyle.Or(WindowStyle.TabSelected, f.TabSelectedColor);
                if (label != null) label.color = WindowStyle.Or(WindowStyle.TabText, f.TabTextColor);
            }
        }

        // ---------- Corner prompt ----------

        /// <summary>The loose "Esc Back" prompt in the screen corner; our Back button replaces it.</summary>
        private static void FindCornerPrompt()
        {
            _cornerPrompt = null;
            foreach (var t in UObject.FindObjectsOfType<RectTransform>(true))
                if (t.name == MenuPaths.MenuPromptPivot) { _cornerPrompt = t.gameObject; break; }
        }

        private static void UpdateCornerPrompt(bool frameOpen)
        {
            if (_cornerPrompt == null) return;
            if (frameOpen)
            {
                if (_cornerPrompt.activeSelf) { _cornerPromptWasActive = true; _cornerPrompt.SetActive(false); }
            }
            else if (_cornerPromptWasActive)
            {
                _cornerPromptWasActive = false;
                _cornerPrompt.SetActive(true);
            }
        }

        // ---------- Helpers ----------

        private static RectTransform CanvasRect(Transform t)
        {
            RectTransform root = null;
            for (var current = t.parent; current != null; current = current.parent)
                if (current.GetComponent<Canvas>() != null) root = current.TryCast<RectTransform>();
            return root;
        }

        private static float CanvasScale(Transform t)
        {
            Canvas canvas = null;
            for (var current = t; current != null; current = current.parent)
            {
                var c = current.GetComponent<Canvas>();
                if (c != null) canvas = c;
            }
            return canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        }

        private static RectTransform FindRect(Transform parent, string path)
        {
            var t = parent.Find(path);
            return t != null ? t.TryCast<RectTransform>() : null;
        }

        private static Image FindImage(Transform parent, string path)
        {
            var t = parent.Find(path);
            return t != null ? t.GetComponent<Image>() : null;
        }

        private static TMP_Text FindText(Transform parent, string path)
        {
            var t = parent.Find(path);
            return t != null ? t.GetComponent<TMP_Text>() : null;
        }
    }
}