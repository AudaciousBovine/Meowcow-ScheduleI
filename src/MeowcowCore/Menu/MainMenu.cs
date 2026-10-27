using System;
using System.Collections;
using MelonLoader;
using UnityEngine;
using Il2CppTMPro;
using UObject = UnityEngine.Object;
using MenuScreen = Il2CppScheduleOne.UI.MainMenu.MenuScreen;

namespace MeowcowCore.Menu
{
    /// <summary>
    /// Core's main menu additions (the Meowcow Options button and the "Meowcow Suite" label), and the
    /// hook other mods use to build on the menu: Ready fires once Core's additions are in place, so
    /// anything recording the menu's "vanilla" state sees them as part of it.
    /// </summary>
    public static class MainMenu
    {
        public const string DefaultLabelText = "Meowcow Suite";
        public static readonly Color DefaultLabelColor = new Color(1f, 0.4f, 0.8f, 1f); // #FF66CC

        private const float LabelGap = 8f; // space between the version and the label

        /// <summary>The main menu's home screen, or null when not on the main menu.</summary>
        public static MenuScreen Home { get; private set; }

        /// <summary>Core's "Meowcow Suite" label (under Home, anchored bottom-left), or null.</summary>
        public static TMP_Text Label { get; private set; }

        /// <summary>Raised on every main menu load, after Core's additions are in place.</summary>
        public static event Action Ready;

        internal static void OnMenuLoaded() => MelonCoroutines.Start(SetupNextFrame());

        /// <summary>Waits a frame so vanilla's Awake/Start have run.</summary>
        private static IEnumerator SetupNextFrame()
        {
            yield return null;
            try
            {
                if (!Setup()) yield break;
            }
            catch (Exception e)
            {
                CoreMod.Log.Error($"Main menu setup failed: {e}");
                yield break;
            }

            try { WindowFrame.OnMenuReady(); }
            catch (Exception e) { CoreMod.Log.Error($"Window frame setup failed: {e}"); }

            try { Ready?.Invoke(); }
            catch (Exception e) { CoreMod.Log.Error($"MainMenu.Ready handler failed: {e}"); }
        }

        private static bool Setup()
        {
            Home = FindHome();
            Label = null;
            if (Home == null) { CoreMod.Log.Warning("Main menu Home screen not found; menu unchanged"); return false; }

            var list = Home.transform.Find(MenuPaths.List);
            if (list != null) OptionsButton.Add(list.TryCast<RectTransform>());
            else CoreMod.Log.Warning("Main menu list not found; Meowcow Options not added");

            var versionT = Home.transform.Find(MenuPaths.Version);
            var version = versionT != null ? versionT.GetComponent<TMP_Text>() : null;
            if (version != null)
            {
                try { Label = CreateLabel(version); }
                catch (Exception e) { CoreMod.Log.Warning($"Couldn't create the Meowcow Suite label: {e.Message}"); }
            }
            return true;
        }

        private static MenuScreen FindHome()
        {
            foreach (var screen in UObject.FindObjectsOfType<MenuScreen>(true))
                if (screen.OpenOnStart && screen.PreviousScreen == null && screen.name == MenuPaths.HomeName)
                    return screen;
            return null;
        }

        /// <summary>
        /// A copy of the version text (same font and size), pink, just right of the version. It lives
        /// straight under Home, so moving the logo or version never drags it along.
        /// </summary>
        private static TMP_Text CreateLabel(TMP_Text version)
        {
            var go = UObject.Instantiate(version.gameObject, version.transform.parent);
            go.name = MenuPaths.LabelName;

            // Drop the version script (it writes the game version); keep only the text itself.
            foreach (var behaviour in go.GetComponents<MonoBehaviour>())
                if (behaviour.TryCast<TMP_Text>() == null) UObject.DestroyImmediate(behaviour);

            var label = go.GetComponent<TMP_Text>();
            var rt = label.rectTransform;
            rt.SetParent(Home.transform, true);
            UiTools.SetAnchorKeepRect(rt, Vector2.zero);

            label.text = DefaultLabelText;
            label.color = DefaultLabelColor;
            label.horizontalAlignment = HorizontalAlignmentOptions.Left;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;

            // Line its first letter up just after the version's last one.
            var space = Home.transform;
            float versionRight = space.InverseTransformPoint(version.rectTransform.TransformPoint(
                new Vector3(UiTools.VisibleRect(version).xMax, 0f))).x;
            float labelLeft = space.InverseTransformPoint(rt.TransformPoint(
                new Vector3(UiTools.VisibleRect(label).xMin, 0f))).x;
            rt.position += space.TransformVector(new Vector3(versionRight + LabelGap - labelLeft, 0f, 0f));

            return label;
        }
    }
}