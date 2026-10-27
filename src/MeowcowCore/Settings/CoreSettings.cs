using System;
using System.IO;
using UnityEngine;
using MelonLoader.Utils;
using static MeowcowCore.Settings.SettingDefinition;

namespace MeowcowCore.Settings
{
    /// <summary>
    /// MeowcowCore's own settings: UserData/MeowcowCore/MeowcowCoreSettings.json.
    /// Appearance settings live in MeowcowThemes; Core keeps only what affects how things work.
    /// </summary>
    internal static class CoreSettings
    {
        // Sections
        internal const string Lobby = "lobby";
        internal const string Windows = "windows";

        // Keys
        internal const string PlayMode = "playMode";
        internal const string ImprovedWindows = "improved";
        internal const string WindowWidth = "width";
        internal const string WindowHeight = "height";
        internal const string WindowX = "x";
        internal const string WindowY = "y";

        internal const string Multiplayer = "Multiplayer";
        internal const string Solo = "Solo";

        internal static SettingsFile File { get; private set; }

        /// <summary>The file changed on disk (hand edits, or the options screen later). Not raised for Core's own saves.</summary>
        internal static event Action Changed;

        private const float CheckInterval = 1f;
        private static float _nextCheck;

        internal static void Load()
        {
            File ??= new SettingsFile(
                Path.Combine(MelonEnvironment.UserDataDirectory, "MeowcowCore", "MeowcowCoreSettings.json"),
                Define);
            File.Load();
        }

        /// <summary>Every frame: checks the file's timestamp once a second and reloads it if it changed.</summary>
        internal static void Tick()
        {
            if (File == null || Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + CheckInterval;
            if (!File.ReloadIfChanged()) return;

            try { Changed?.Invoke(); }
            catch (Exception e) { CoreMod.Log.Error($"Settings change handler failed: {e}"); }
        }

        private static SettingsDocument Define()
        {
            var doc = new SettingsDocument { Module = "MeowcowCore" };

            doc.Sections.Add(new SettingsSection
            {
                Id = Lobby,
                Name = "Lobby",
                Settings =
                {
                    Choice(PlayMode, "Play mode",
                        "Solo hides the invite slots; a friend joining switches back to Multiplayer. Click the lobby title to change it.",
                        Multiplayer, Multiplayer, Solo),
                },
            });

            doc.Sections.Add(new SettingsSection
            {
                Id = Windows,
                Name = "Windows",
                Description = "Settings, Load Game, New Game and Meowcow Options share one window frame",
                Settings =
                {
                    Bool(ImprovedWindows, "Improved windows",
                        "Bigger, draggable windows with tabs on the left and a Back button. Off: the game's own screens", true),
                    Number(WindowWidth, "Width", "Window width (screen is 1920 wide)", 1100, 700, 1800, 10),
                    Number(WindowHeight, "Height", "Window height (screen is 1080 tall)", 700, 400, 1000, 10),
                    Number(WindowX, "X position", "Set by dragging the title; null centres it (double-click the title)", null, -960, 960, 1),
                    Number(WindowY, "Y position", "Set by dragging the title; null centres it (double-click the title)", null, -540, 540, 1),
                },
            });

            return doc;
        }
    }
}