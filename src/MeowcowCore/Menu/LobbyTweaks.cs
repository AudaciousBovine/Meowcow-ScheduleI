using System;
using System.Collections;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Il2CppInterop.Runtime;
using Il2CppTMPro;
using MeowcowCore.Settings;
using S = MeowcowCore.Settings.CoreSettings;
using UObject = UnityEngine.Object;
using Lobby = Il2CppScheduleOne.Networking.Lobby;
using LobbyInterface = Il2CppScheduleOne.UI.Multiplayer.LobbyInterface;

namespace MeowcowCore.Menu
{
    /// <summary>
    /// The lobby panel, wherever it appears (main menu and pause menu): makes its title a Solo/Multiplayer
    /// toggle with a menu-style hover shine, hides the invite slots in Solo, aligns the title, and places
    /// the panel from the lobby settings. Vanilla rewrites the title on every lobby change; we listen to the
    /// same event after vanilla does, so our changes land on top of its.
    /// </summary>
    internal static class LobbyTweaks
    {
        private const string SoloLabel = "Solo";
        private const float ShinePadding = 20f;   // extra width either side of the title text
        private const float ShineFadeSpeed = 10f; // alpha per second; about vanilla's quick fade

        private static LobbyInterface _ui;
        private static PlacedElement _placed;
        private static Transform _entries;
        private static TMP_Text _title;
        private static HorizontalAlignmentOptions _titleAlign;
        private static Vector2 _titlePos, _titleSize;   // vanilla placement of the title
        private static Color _titleColor;
        private static readonly System.Collections.Generic.List<(Image image, Color vanilla)> _slotImages =
            new System.Collections.Generic.List<(Image, Color)>();
        private static float _shineLength = 1f;
        private static Button _titleButton;
        private static RectTransform _shine;
        private static CanvasGroup _shineGroup;
        private static Image _shineLeft, _shineRight;
        private static Color _shineColor;
        private static Lobby _lobby;
        private static IntPtr _hookedLobby;
        private static Il2CppSystem.Action _onLobbyChange; // kept referenced so the converted delegates aren't collected
        private static UnityAction _onTitleClick;
        private static bool _subscribed, _pending, _wasVisible, _wroteSolo;
        private static Vector2Int _screen;

        /// <summary>Called on every scene load; finds that scene's lobby panel a frame later.</summary>
        internal static void OnSceneLoaded()
        {
            if (!_subscribed)
            {
                _subscribed = true;
                S.Changed += () => _pending = true;
                PlayMode.Changed += _ => { ApplyState(); _pending = true; };
            }
            _ui = null;
            MelonCoroutines.Start(CaptureNextFrame());
        }

        /// <summary>Every frame: keeps the Solo label, fades the shine, and places the panel when needed.</summary>
        internal static void Tick()
        {
            if (_ui == null) return;
            var container = _ui.Container;
            if (container == null) { _ui = null; return; }

            // Vanilla rewrites the title on scene changes too; put "Solo" back if it did.
            if (PlayMode.IsSolo && _title != null && _title.text != SoloLabel)
            {
                _title.text = SoloLabel;
                FitShine();
            }

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

            if (visible) FadeShine();
        }

        // ---------- Setup ----------

        private static IEnumerator CaptureNextFrame()
        {
            yield return null;
            try { Capture(); }
            catch (Exception e) { CoreMod.Log.Error($"Lobby setup failed: {e}"); }
        }

        private static void Capture()
        {
            var ui = UObject.FindObjectOfType<LobbyInterface>(true);
            if (ui == null || ui.Container == null) return; // this scene has no lobby panel

            _ui = ui;
            _placed = ElementPlacer.Record(S.Lobby, ui.Container, manageActive: false);
            _entries = ui.Container.Find(MenuElements.LobbyEntries);
            CaptureSlots();
            _title = ui.LobbyTitle;
            _wroteSolo = false;
            _wasVisible = false;

            if (_title != null)
            {
                _titleAlign = _title.horizontalAlignment;
                _titlePos = _title.rectTransform.anchoredPosition;
                _titleSize = _title.rectTransform.sizeDelta;
                _titleColor = _title.color;
                SetupTitleButton();
                BuildShine();
            }
            else
            {
                CoreMod.Log.Warning("Lobby title not found; Solo toggle unavailable");
            }

            HookLobby();
            ApplyState();
            _pending = true;
        }

        /// <summary>
        /// Each slot's outline (its "Frame" child, the border around the avatar), plus the invite box's
        /// outline and + icon. Avatars are left out.
        /// </summary>
        private static void CaptureSlots()
        {
            _slotImages.Clear();
            if (_entries == null) return;

            for (int i = 0; i < _entries.childCount; i++)
            {
                var slot = _entries.GetChild(i);
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

        /// <summary>The title becomes a button. The shine shows hover; the text only greys out when Solo isn't allowed.</summary>
        private static void SetupTitleButton()
        {
            _title.raycastTarget = true;
            var go = _title.gameObject;

            _titleButton = go.GetComponent<Button>();
            if (_titleButton == null) _titleButton = go.AddComponent<Button>();
            _titleButton.targetGraphic = _title;

            var colors = _titleButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.5f);
            _titleButton.colors = colors;

            _titleButton.onClick = new Button.ButtonClickedEvent();
            _onTitleClick ??= DelegateSupport.ConvertDelegate<UnityAction>(new Action(OnTitleClicked));
            _titleButton.onClick.AddListener(_onTitleClick);
        }

        /// <summary>
        /// A centred two-half shine behind the title (a sibling placed just before it, so it draws underneath),
        /// using this scene's vanilla shine art. Faded by its own CanvasGroup.
        /// </summary>
        private static void BuildShine()
        {
            var source = ShineArt.FindInScene();
            _shineColor = source.Color;

            var go = new GameObject("MeowcowLobbyShine");
            _shine = go.AddComponent<RectTransform>();
            _shine.SetParent(_title.transform.parent, false);
            _shine.SetSiblingIndex(_title.transform.GetSiblingIndex());
            _shine.anchorMin = new Vector2(0.5f, 0.5f);
            _shine.anchorMax = new Vector2(0.5f, 0.5f);
            _shine.pivot = new Vector2(0.5f, 0.5f);

            _shineGroup = go.AddComponent<CanvasGroup>();
            _shineGroup.alpha = 0f;
            _shineGroup.interactable = false;
            _shineGroup.blocksRaycasts = false;

            _shineLeft = ShineArt.CreateHalf(_shine, source, flipped: true);
            _shineRight = ShineArt.CreateHalf(_shine, source, flipped: false);
            FitShine();
        }

        /// <summary>Listens to the lobby once per lobby object (it outlives scenes).</summary>
        private static void HookLobby()
        {
            var lobby = UObject.FindObjectOfType<Lobby>(true);
            if (lobby == null) { CoreMod.Log.Warning("Lobby not found; Solo won't react to players joining"); return; }

            _lobby = lobby;
            PlayMode.OnPlayerCountChanged(lobby.PlayerCount);
            if (lobby.Pointer == _hookedLobby) return;

            _onLobbyChange ??= DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(new Action(OnLobbyChanged));
            lobby.add_OnLobbyChange(_onLobbyChange);
            _hookedLobby = lobby.Pointer;
        }

        // ---------- Events ----------

        private static void OnLobbyChanged()
        {
            // Called from native code: exceptions must not escape.
            try
            {
                if (_lobby != null) PlayMode.OnPlayerCountChanged(_lobby.PlayerCount);
                ApplyState();
                _pending = true; // the slots changed, so the panel's size did too
            }
            catch (Exception e)
            {
                CoreMod.Log.Error($"Lobby update failed: {e}");
            }
        }

        private static void OnTitleClicked()
        {
            try { PlayMode.Toggle(); }
            catch (Exception e) { CoreMod.Log.Error($"Play mode toggle failed: {e}"); }
        }

        // ---------- Applying ----------

        /// <summary>Solo: slots hidden, title reads "Solo". Multiplayer: vanilla's own title and slots.</summary>
        private static void ApplyState()
        {
            if (_ui == null) return;
            bool solo = PlayMode.IsSolo;

            if (_entries != null) _entries.gameObject.SetActive(!solo);

            if (_title != null)
            {
                if (solo)
                {
                    _title.text = SoloLabel;
                    _wroteSolo = true;
                }
                else if (_wroteSolo)
                {
                    _wroteSolo = false;
                    _ui.UpdateUI(); // vanilla writes "Lobby (n/4)" back
                }
            }

            if (_titleButton != null) _titleButton.interactable = solo || PlayMode.CanPlaySolo;
            FitShine();
        }

        private static void Apply()
        {
            try
            {
                var settings = S.Menu; // follows the chosen theme
                string align = settings.GetString(S.Lobby, S.TextAlign);
                _shineLength = settings.GetNumber(S.MainMenu, S.ShineLength) ?? 1f;
                ShineArt.ApplyTextAlign(_title, align, _titleAlign);
                if (_title != null)
                {
                    string hex = settings.GetString(S.Lobby, S.ItemColor) ?? settings.GetString(S.MainMenu, S.TextColor);
                    _title.color = ColorOverride.Parse(hex).Apply(_titleColor);
                }
                ApplyState();
                AlignTitleOverSlots(align);
                FitShine();

                var slotTint = ColorOverride.Parse(settings.GetString(S.Lobby, S.SlotColor));
                foreach (var (image, vanilla) in _slotImages)
                    if (image != null) image.color = slotTint.Apply(vanilla);

                var color = ShineTint.Parse(settings.GetString(S.MainMenu, S.ShineColor)).Apply(_shineColor);
                if (_shineLeft != null) _shineLeft.color = color;
                if (_shineRight != null) _shineRight.color = color;

                Canvas.ForceUpdateCanvases(); // the slots' grid must be laid out before measuring
                ElementPlacer.Place(_placed, settings, ElementPlacer.Padding(settings));
            }
            catch (Exception e)
            {
                CoreMod.Log.Warning($"Couldn't place the lobby: {e.Message}");
            }
        }

        /// <summary>
        /// Vanilla's title box doesn't line up with the player slots, so for Left/Center/Right the box is
        /// stretched to span exactly the visible slots; the text then aligns over them. Vanilla alignment,
        /// or Solo (no slots), keeps vanilla's box.
        /// </summary>
        private static void AlignTitleOverSlots(string align)
        {
            if (_title == null) return;
            var rt = _title.rectTransform;
            rt.anchoredPosition = _titlePos;
            rt.sizeDelta = _titleSize;

            if (align == null || align == S.VanillaAnchor) return;
            if (_entries == null || !_entries.gameObject.activeInHierarchy) return;

            Canvas.ForceUpdateCanvases(); // the slots' grid must be laid out before measuring
            var parent = rt.parent;
            var slots = ElementPlacer.Bounds(_entries.TryCast<RectTransform>(), parent);
            if (slots == null) return;

            rt.sizeDelta = new Vector2(slots.Value.width, _titleSize.y);
            float shift = slots.Value.center.x - rt.localPosition.x;
            rt.anchoredPosition = new Vector2(_titlePos.x + shift, _titlePos.y);
        }

        // ---------- Shine ----------

        /// <summary>Sizes the shine to the title's text (it changes between "Solo" and "Lobby (n/4)", and with alignment).</summary>
        private static void FitShine()
        {
            if (_shine == null || _title == null) return;

            var r = ElementPlacer.VisibleRect(_title);
            var parent = _shine.parent;
            Vector3 a = parent.InverseTransformPoint(_title.rectTransform.TransformPoint(new Vector3(r.xMin, r.yMin)));
            Vector3 b = parent.InverseTransformPoint(_title.rectTransform.TransformPoint(new Vector3(r.xMax, r.yMax)));

            float height = _title.rectTransform.rect.height;
            _shine.sizeDelta = new Vector2((Mathf.Abs(b.x - a.x) + ShinePadding * 2f) * _shineLength, height);
            _shine.localPosition = new Vector3((a.x + b.x) / 2f, _title.rectTransform.localPosition.y, 0f);
        }

        /// <summary>Fades in while the pointer is over a clickable title, out otherwise.</summary>
        private static void FadeShine()
        {
            if (_shineGroup == null || _titleButton == null) return;

            bool hover = _titleButton.interactable && ContainsPointer(_title.rectTransform);
            float target = hover ? 1f : 0f;
            _shineGroup.alpha = Mathf.MoveTowards(_shineGroup.alpha, target, ShineFadeSpeed * Time.unscaledDeltaTime);
        }

        private static bool ContainsPointer(RectTransform rect)
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