using System;
using MeowcowCore.Settings;
using S = MeowcowCore.Settings.CoreSettings;

namespace MeowcowCore
{
    public enum SessionMode { Multiplayer, Solo }

    /// <summary>
    /// Whether the player is playing solo or with others. The player chooses it by clicking the lobby
    /// title; mods read it (or listen to Changed) to behave differently in each.
    /// Rules: Solo is refused while anyone else is in the lobby, and someone joining a Solo game
    /// switches it to Multiplayer, where it stays until the player switches back.
    /// The choice is saved in MeowcowCoreSettings.json.
    /// </summary>
    public static class PlayMode
    {
        private static SessionMode? _current;

        /// <summary>Raised whenever the mode changes, for any reason.</summary>
        public static event Action<SessionMode> Changed;

        public static SessionMode Current
        {
            get
            {
                if (!_current.HasValue) _current = ReadSaved();
                return _current.Value;
            }
        }

        public static bool IsSolo => Current == SessionMode.Solo;
        public static bool IsMultiplayer => Current == SessionMode.Multiplayer;

        /// <summary>Players in the lobby, including this one (1 when not in a lobby).</summary>
        public static int PlayerCount { get; private set; } = 1;

        /// <summary>Solo is only allowed while nobody else is in the lobby.</summary>
        public static bool CanPlaySolo => PlayerCount <= 1;

        /// <summary>Switches mode. Returns false if Solo was asked for while others are in the lobby.</summary>
        public static bool TrySet(SessionMode mode)
        {
            if (mode == SessionMode.Solo && !CanPlaySolo) return false;
            Set(mode);
            return true;
        }

        internal static void Toggle() => TrySet(IsSolo ? SessionMode.Multiplayer : SessionMode.Solo);

        /// <summary>From the lobby: someone joined or left.</summary>
        internal static void OnPlayerCountChanged(int count)
        {
            PlayerCount = Math.Max(1, count);
            if (IsSolo && !CanPlaySolo)
            {
                Set(SessionMode.Multiplayer);
                CoreMod.Log.Msg("Someone joined; switched to Multiplayer");
            }
        }

        /// <summary>The settings file was edited by hand.</summary>
        internal static void OnSettingsChanged()
        {
            var saved = ReadSaved();
            if (saved == Current) return;

            if (saved == SessionMode.Solo && !CanPlaySolo) { Set(SessionMode.Multiplayer); return; } // write the correction back
            _current = saved;
            RaiseChanged(saved);
        }

        private static void Set(SessionMode mode)
        {
            if (Current == mode) return;
            _current = mode;

            var file = S.File;
            if (file != null)
            {
                file.Set(S.Lobby, S.PlayMode, mode == SessionMode.Solo ? S.Solo : S.Multiplayer);
                file.Save();
            }
            RaiseChanged(mode);
        }

        private static void RaiseChanged(SessionMode mode)
        {
            try { Changed?.Invoke(mode); }
            catch (Exception e) { CoreMod.Log.Error($"PlayMode.Changed handler failed: {e}"); }
        }

        private static SessionMode ReadSaved() =>
            S.File?.GetString(S.Lobby, S.PlayMode) == S.Solo ? SessionMode.Solo : SessionMode.Multiplayer;
    }
}