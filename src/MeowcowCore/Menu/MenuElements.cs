using UnityEngine;
using MeowcowCore.Settings;

namespace MeowcowCore.Menu
{
    /// <summary>A movable main menu element: its settings section, path under Home, and vanilla anchor.</summary>
    internal readonly struct MenuElement
    {
        public readonly string SectionId;
        public readonly string Path;
        public readonly Vector2 Anchor;

        public MenuElement(string sectionId, string path, Vector2 anchor)
        {
            SectionId = sectionId;
            Path = path;
            Anchor = anchor;
        }
    }

    /// <summary>
    /// The only place that knows vanilla main menu paths (from the scene dump). If a game update
    /// renames something, fix it here.
    /// </summary>
    internal static class MenuElements
    {
        public const string LabelName = "MeowcowLabel"; // Core's own custom label, cloned from the version text
        public const float LabelGap = 8f;               // space between the version and the label's default spot

        /// <summary>The main menu's home screen: a plain MenuScreen with OpenOnStart.</summary>
        public const string HomeName = "Home";

        /// <summary>
        /// Elements under Home. Header, version and list all sit in "Bank" (anchored bottom-left);
        /// Core moves each straight under Home so they can be placed independently.
        /// </summary>
        public static readonly MenuElement[] Home =
        {
            new MenuElement(CoreSettings.Header,   "Bank/Title",      new Vector2(0f, 0f)),
            new MenuElement(CoreSettings.Version,  "Bank/Title/Text", new Vector2(0f, 0f)),
            new MenuElement(CoreSettings.MenuList, "Bank/Panel",      new Vector2(0f, 0f)),
            new MenuElement(CoreSettings.Socials,  "Socials",         new Vector2(1f, 0f)),
        };

        // Children of the list's buttons (each button has the same structure).
        public const string SettingsButton = "Settings";
        public const string ButtonShine = "Shine";
        public const string SocialIcon = "Image";                                       // under each social button: the icon itself                                      // the bar that fades in on hover
        public const string ButtonText = "ButtonText";                                  // the label shown
        public const string ButtonTextDefault = "ButtonText Default Position";          // where the label rests
        public const string ButtonTextHighlighted = "ButtonText Highlighted Position";  // where it slides on hover

        // The lobby isn't under Home: it's LobbyInterface.Container, found through the component.
        public const string LobbyEntries = "Entries";        // under Container: the invite button and player slots
        public const string LobbyInvite = "Invite";               // under Entries: the + box
        public const string LobbySlotFrame = "Frame";             // under each player slot: the outline around the avatar
        public const string LobbyInviteFrame = "Frame (1)";       // under Invite: its outline
        public const string LobbyInviteIcon = "Frame (1)/Icon";   // under Invite: the + itself
    }
}