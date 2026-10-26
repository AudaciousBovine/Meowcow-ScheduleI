using System;
using System.IO;
using UnityEngine;
using MelonLoader.Utils;
using static MeowcowCore.Settings.SettingDefinition;

namespace MeowcowCore.Settings
{
    /// <summary>
    /// MeowcowCore's own settings: UserData/MeowcowCore/MeowcowCoreSettings.json.
    /// Section IDs and keys are constants so the layout code never spells them twice.
    /// </summary>
    internal static class CoreSettings
    {
        // Sections
        internal const string MainMenu = "mainMenu";
        internal const string Header = "mainMenu.header";
        internal const string Version = "mainMenu.version";
        internal const string MenuList = "mainMenu.list";
        internal const string Socials = "mainMenu.socials";
        internal const string Lobby = "mainMenu.lobby";
        internal const string Label = "mainMenu.label";

        // Keys
        internal const string Padding = "padding";
        internal const string ThemeKey = "theme";
        internal const string Anchor = "anchor";
        internal const string X = "x";
        internal const string Y = "y";
        internal const string Scale = "scale";
        internal const string Hide = "hide";
        internal const string PlayMode = "playMode";
        internal const string TextAlign = "textAlign";
        internal const string ShineLength = "shineLength";
        internal const string ShineColor = "shineColor";
        internal const string TextColor = "textColor";
        internal const string ItemColor = "color";
        internal const string SlotColor = "slotColor";
        internal const string LabelText = "text";
        internal const string LabelDefaultText = "Meowcow Suite";
        internal const string LabelDefaultColor = "#FF66CC";

        internal const string VanillaAnchor = "Vanilla";
        internal const string VanillaTheme = "Vanilla";
        internal const string MeowcowTheme = "Meowcow";
        internal const string CustomTheme = "Custom";
        internal const string Multiplayer = "Multiplayer";
        internal const string Solo = "Solo";

        internal static readonly string[] Anchors =
        {
            VanillaAnchor,
            "TopLeft", "Top", "TopRight",
            "Left", "Center", "Right",
            "BottomLeft", "Bottom", "BottomRight",
        };

        internal static SettingsFile File { get; private set; }

        /// <summary>
        /// Where menu appearance is read from, per the chosen theme: Vanilla ignores the file's appearance
        /// settings, Meowcow uses the built-in preset, Custom reads the file.
        /// </summary>
        internal static ISettingsSource Menu => (File?.GetString(MainMenu, ThemeKey)) switch
        {
            VanillaTheme => new ThemeSource(File, null),
            CustomTheme  => File,
            _            => new ThemeSource(File, BuiltInThemes.Meowcow),
        };

        /// <summary>Appearance settings follow the theme; the theme choice and play mode never do.</summary>
        internal static bool IsThemed(string sectionId, string key) =>
            !(sectionId == MainMenu && key == ThemeKey) && !(sectionId == Lobby && key == PlayMode);

        /// <summary>The file changed on disk (hand edits, or the options screen later). Not raised for Core's own saves.</summary>
        internal static event Action Changed;

        private const float CheckInterval = 1f;
        private static float _nextCheck;

        /// <summary>Every frame: checks the file's timestamp once a second and reloads it if it changed.</summary>
        internal static void Tick()
        {
            if (File == null || Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + CheckInterval;
            if (!File.ReloadIfChanged()) return;

            try { Changed?.Invoke(); }
            catch (Exception e) { CoreMod.Log.Error($"Settings change handler failed: {e}"); }
        }

        internal static void Load()
        {
            File ??= new SettingsFile(
                Path.Combine(MelonEnvironment.UserDataDirectory, "MeowcowCore", "MeowcowCoreSettings.json"),
                Define);
            File.Load();
        }

        private static SettingsDocument Define()
        {
            var doc = new SettingsDocument { Module = "MeowcowCore" };

            doc.Sections.Add(new SettingsSection
            {
                Id = MainMenu,
                Name = "Main Menu",
                Settings =
                {
                    Choice(ThemeKey, "Theme",
                        "Vanilla: the game's own look (plus the custom label). Meowcow: the suite's look. Custom: the settings in this file",
                        MeowcowTheme, VanillaTheme, MeowcowTheme, CustomTheme),
                    Number(Padding, "Edge padding", "Distance from the screen edge when an element is moved to a new anchor", 30, 0, 200, 1),
                    Number(ShineLength, "Hover length",
                        "Stretches every menu hover bar away from the text's side (both ways when centred)", 1, 0.5, 3, 0.05),
                    Color(TextColor, "Text colour",
                        "Colours all menu text (list, version, lobby title), e.g. #FF66CC. Each item's own colour overrides it. Images are never tinted by this",
                        null),
                    Color(ShineColor, "Hover colour",
                        "Tints every menu hover bar, e.g. #FF66CC. Add two digits for opacity (#FF66CC80); without them vanilla's opacity is kept",
                        null),
                },
            });

            var header = Element(Header, "Header", "The Schedule I logo", canHide: true);
            header.Settings.Add(Color(ItemColor, "Tint", "Tints the logo image (null keeps its own colours)", null));
            doc.Sections.Add(header);
            var version = Element(Version, "Version", "The game version under the logo", canHide: true);
            version.Settings.Add(TextAlignSetting("Lines the text up within its box, which is as wide as the logo"));
            version.Settings.Add(TextColorSetting());
            doc.Sections.Add(version);
            var list = Element(MenuList, "Menu List", "Continue, New Game, Settings and the rest", canHide: false);
            list.Settings.Add(Choice(TextAlign, "Text alignment", "How the button labels line up; the hover slide follows",
                "Left", "Left", "Center", "Right"));
            list.Settings.Add(TextColorSetting());
            doc.Sections.Add(list);
            var socials = Element(Socials, "Social Buttons", "Discord, X, Reddit and Trello", canHide: true);
            socials.Settings.Add(Color(ItemColor, "Tint", "Tints the icons only (null keeps their own colours)", null));
            doc.Sections.Add(socials);

            var lobby = Element(Lobby, "Lobby", "The lobby panel, on the main menu and in the pause menu", canHide: false);
            lobby.Settings.Add(TextAlignSetting("Lines the title up over the player slots"));
            lobby.Settings.Add(TextColorSetting());
            lobby.Settings.Add(Color(SlotColor, "Slot tint",
                "Tints every player slot outline and the invite box with its + (avatars are never tinted)", null));
            lobby.Settings.Add(Choice(PlayMode, "Play mode",
                "Solo hides the invite slots; a friend joining switches back to Multiplayer. Click the lobby title to change it.",
                Multiplayer, Multiplayer, Solo));
            doc.Sections.Add(lobby);

            var label = Element(Label, "Custom Label", "Your own text on the main menu; starts just right of the version", canHide: true);
            label.Settings.Add(Text(LabelText, "Text", "What the label says", LabelDefaultText));
            label.Settings.Add(Color(ItemColor, "Text colour",
                "Colour of the label. Null uses the global text colour, or pink (#FF66CC) if that's not set either", null));
            doc.Sections.Add(label);

            return doc;
        }

        private static SettingDefinition TextColorSetting() =>
            Color(ItemColor, "Text colour", "Overrides the global text colour for this item (null uses the global one)", null);

        private static SettingDefinition TextAlignSetting(string description) =>
            Choice(TextAlign, "Text alignment", description, VanillaAnchor, VanillaAnchor, "Left", "Center", "Right");

        /// <summary>
        /// Placement settings for one main menu element. Anchor "Vanilla" keeps the game's placement.
        /// X/Y are measured inward from the anchored edge; null means "edge padding".
        /// </summary>
        private static SettingsSection Element(string id, string name, string description, bool canHide)
        {
            var section = new SettingsSection
            {
                Id = id,
                Name = name,
                Description = description,
                Settings =
                {
                    Choice(Anchor, "Anchor", "Where on screen the element sits", VanillaAnchor, Anchors),
                    Number(X, "X offset", "Distance from the anchored side (left/right from the middle on a centre anchor)", null, -1920, 1920, 1),
                    Number(Y, "Y offset", "Distance from the anchored side (up/down from the middle on a centre anchor)", null, -1080, 1080, 1),
                    Number(Scale, "Scale", "Size of the element", 1, 0.25, 3, 0.05),
                },
            };
            if (canHide) section.Settings.Add(Bool(Hide, "Hide", "Hide this element", false));
            return section;
        }
    }
}