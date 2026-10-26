using UnityEngine;
using T = MeowcowThemes.Settings.ThemeSettings;

namespace MeowcowThemes.Menu
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
    /// The vanilla paths MeowcowThemes styles (from the scene dump). If a game update renames
    /// something, fix it here.
    /// </summary>
    internal static class MenuElements
    {
        /// <summary>
        /// Elements under Home. Header, version and list all sit in "Bank" (anchored bottom-left);
        /// each is moved straight under Home so they can be placed independently.
        /// </summary>
        public static readonly MenuElement[] Home =
        {
            new MenuElement(T.Header,   "Bank/Title",      new Vector2(0f, 0f)),
            new MenuElement(T.Version,  "Bank/Title/Text", new Vector2(0f, 0f)),
            new MenuElement(T.MenuList, "Bank/Panel",      new Vector2(0f, 0f)),
            new MenuElement(T.Socials,  "Socials",         new Vector2(1f, 0f)),
        };

        // Children of the list's buttons (each button has the same structure)
        public const string SettingsButton = "Settings";
        public const string ButtonText = "ButtonText";                                  // the label shown
        public const string ButtonTextDefault = "ButtonText Default Position";          // where the label rests
        public const string ButtonTextHighlighted = "ButtonText Highlighted Position";  // where it slides on hover
        public const string ButtonShine = "Shine";                                      // the bar that fades in on hover

        public const string SocialIcon = "Image";                 // under each social button: the icon itself

        // Lobby slots, under LobbyToggle.Slots
        public const string LobbyInvite = "Invite";               // the + box
        public const string LobbySlotFrame = "Frame";             // under each player slot: the outline around the avatar
        public const string LobbyInviteFrame = "Frame (1)";       // under Invite: its outline
        public const string LobbyInviteIcon = "Frame (1)/Icon";   // under Invite: the + itself
    }
}
