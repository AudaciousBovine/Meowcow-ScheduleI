using System;
using System.Collections;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Il2CppInterop.Runtime;
using Il2CppTMPro;
using UObject = UnityEngine.Object;
using Lobby = Il2CppScheduleOne.Networking.Lobby;
using LobbyInterface = Il2CppScheduleOne.UI.Multiplayer.LobbyInterface;

namespace MeowcowCore.Menu
{
    /// <summary>
    /// The lobby panel, wherever it appears (main menu and pause menu): its title becomes a Solo/Multiplayer
    /// toggle with a hover shine, and Solo hides the invite slots. Vanilla rewrites the title on every lobby
    /// change; we listen to the same event after vanilla does, so our changes land on top of its.
    /// Other mods (MeowcowThemes) style the panel through the properties and events here.
    /// </summary>
    public static class LobbyToggle
    {
        private const string SoloLabel = "Solo";
        private const float ShinePadding = 20f;   // extra width either side of the title text
        private const float ShineFadeSpeed = 10f; // alpha per second; about vanilla's quick fade

        private static LobbyInterface _ui;
        private static Transform _entries;
        private static TMP_Text _title;
        private static Button _titleButton;
        private static RectTransform _shine;
        private static CanvasGroup _shineGroup;
        private static Image _shineLeft, _shineRight;
        private static Lobby _lobby;
        private static IntPtr _hookedLobby;
        private static Il2CppSystem.Action _onLobbyChange; // kept referenced so the converted delegates aren't collected
        private static UnityAction _onTitleClick;
        private static bool _subscribed, _wroteSolo;
        private static float _shineLength = 1f;

        // ---------- Public hooks ----------

        /// <summary>This scene's lobby panel, or null.</summary>
        public static LobbyInterface Panel => _ui;

        /// <summary>The clickable title.</summary>
        public static TMP_Text Title => _title;

        /// <summary>The invite button and player slots (hidden in Solo).</summary>
        public static Transform Slots => _entries;

        /// <summary>The shine's colour from this scene's vanilla art, for tinting.</summary>
        public static Color VanillaShineColor { get; private set; } = Color.white;

        /// <summary>Raised when a scene's lobby panel has been set up.</summary>
        public static event Action Ready;

        /// <summary>Raised when the title or slots changed (play mode, players joining or leaving).</summary>
        public static event Action Changed;

        /// <summary>The hover shine's colour.</summary>
        public static Color ShineColor
        {
            set
            {
                if (_shineLeft != null) _shineLeft.color = value;
                if (_shineRight != null) _shineRight.color = value;
            }
        }

        /// <summary>The hover shine's length relative to the title text (1 = text plus padding).</summary>
        public static float ShineLength
        {
            get => _shineLength;
            set { _shineLength = value; RefitShine(); }
        }

        /// <summary>Re-sizes the shine to the title; call after moving or re-aligning the title.</summary>
        public static void RefitShine()
        {
            if (_shine == null || _title == null) return;

            var r = UiTools.VisibleRect(_title);
            var parent = _shine.parent;
            Vector3 a = parent.InverseTransformPoint(_title.rectTransform.TransformPoint(new Vector3(r.xMin, r.yMin)));
            Vector3 b = parent.InverseTransformPoint(_title.rectTransform.TransformPoint(new Vector3(r.xMax, r.yMax)));

            float height = _title.rectTransform.rect.height;
            _shine.sizeDelta = new Vector2((Mathf.Abs(b.x - a.x) + ShinePadding * 2f) * _shineLength, height);
            _shine.localPosition = new Vector3((a.x + b.x) / 2f, _title.rectTransform.localPosition.y, 0f);
        }

        // ---------- Lifecycle ----------

        /// <summary>Called on every scene load; finds that scene's lobby panel a frame later.</summary>
        internal static void OnSceneLoaded()
        {
            if (!_subscribed)
            {
                _subscribed = true;
                PlayMode.Changed += _ => ApplyAndNotify();
            }
            _ui = null;
            MelonCoroutines.Start(CaptureNextFrame());
        }

        /// <summary>Every frame: keeps the Solo label and fades the shine.</summary>
        internal static void Tick()
        {
            if (_ui == null) return;
            var container = _ui.Container;
            if (container == null) { _ui = null; return; }

            // Vanilla rewrites the title on scene changes too; put "Solo" back if it did.
            if (PlayMode.IsSolo && _title != null && _title.text != SoloLabel)
            {
                _title.text = SoloLabel;
                RefitShine();
            }

            if (container.gameObject.activeInHierarchy) FadeShine();
        }

        // ---------- Setup ----------

        private static IEnumerator CaptureNextFrame()
        {
            yield return null;
            try
            {
                if (!Capture()) yield break;
            }
            catch (Exception e)
            {
                CoreMod.Log.Error($"Lobby setup failed: {e}");
                yield break;
            }

            try { Ready?.Invoke(); }
            catch (Exception e) { CoreMod.Log.Error($"LobbyToggle.Ready handler failed: {e}"); }
        }

        private static bool Capture()
        {
            var ui = UObject.FindObjectOfType<LobbyInterface>(true);
            if (ui == null || ui.Container == null) return false; // this scene has no lobby panel

            _ui = ui;
            _entries = ui.Container.Find(MenuPaths.LobbyEntries);
            _title = ui.LobbyTitle;
            _wroteSolo = false;

            if (_title != null)
            {
                SetupTitleButton();
                BuildShine();
            }
            else
            {
                CoreMod.Log.Warning("Lobby title not found; Solo toggle unavailable");
            }

            HookLobby();
            ApplyState();
            return true;
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
            var source = UiTools.FindShineInScene();
            VanillaShineColor = source.Color;

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

            _shineLeft = UiTools.CreateShineHalf(_shine, source, flipped: true);
            _shineRight = UiTools.CreateShineHalf(_shine, source, flipped: false);
            RefitShine();
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
                ApplyAndNotify();
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

        private static void ApplyAndNotify()
        {
            ApplyState();
            try { Changed?.Invoke(); }
            catch (Exception e) { CoreMod.Log.Error($"LobbyToggle.Changed handler failed: {e}"); }
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
            RefitShine();
        }

        /// <summary>Fades in while the pointer is over a clickable title, out otherwise.</summary>
        private static void FadeShine()
        {
            if (_shineGroup == null || _titleButton == null) return;

            bool hover = _titleButton.interactable && UiTools.ContainsPointer(_title.rectTransform);
            float target = hover ? 1f : 0f;
            _shineGroup.alpha = Mathf.MoveTowards(_shineGroup.alpha, target, ShineFadeSpeed * Time.unscaledDeltaTime);
        }
    }
}
