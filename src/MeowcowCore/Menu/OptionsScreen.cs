using System;
using UObject = UnityEngine.Object;
using MainMenuPopup = Il2CppScheduleOne.UI.MainMenu.MainMenuPopup;

namespace MeowcowCore.Menu
{
    /// <summary>
    /// Meowcow Options. Placeholder for now: shows the vanilla main menu popup until the real
    /// options screen (built from each mod's settings file) exists.
    /// </summary>
    internal static class OptionsScreen
    {
        internal static void Open()
        {
            // Called from a button click: exceptions must not escape into native code.
            try
            {
                var popup = UObject.FindObjectOfType<MainMenuPopup>(true);
                if (popup == null) { CoreMod.Log.Warning("Main menu popup not found"); return; }

                popup.Open("Meowcow Options",
                    "The options screen is coming soon. For now, edit UserData/MeowcowCore/MeowcowCoreSettings.json; " +
                    "the menu updates as soon as you save.",
                    false);
            }
            catch (Exception e)
            {
                CoreMod.Log.Error($"Couldn't open Meowcow Options: {e}");
            }
        }
    }
}