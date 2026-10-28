using System;
using System.Collections;
using Il2CppTMPro;
using MelonLoader;
using MeowcowCore.Foundation;
using MeowcowCore.SuiteUI;
using UnityEngine;
using MenuScreen = Il2CppScheduleOne.UI.MainMenu.MenuScreen;

namespace MeowcowCore.Menus;

/// <summary>
/// Core's main menu additions (design §13): the Meowcow Settings button after Settings, and the
/// "Meowcow Suite" tag beside the version text. Rebuilt on every menu load, since vanilla rebuilds the menu.
/// </summary>
/// <remarks>
/// [CHANGED] New for the rebuild (was <c>Menu/MainMenu</c> + <c>OptionsButton</c>). The button opens the
/// Meowcow Settings window (placeholder tabs until SuiteSettings exists). Each addition is independent:
/// if one can't be built, it's skipped with a warning and the menu stays otherwise vanilla.
/// [PERFORMANCE] One MenuScreen search and two clones per menu load; nothing per frame.
/// </remarks>
public static class MainMenu
{
    private const string SettingsButtonName = "MeowcowSettings";
    private const string SettingsButtonLabel = "Meowcow Settings";
    private const string TagName = "MeowcowSuiteTag";
    private const string TagText = "Meowcow Suite";
    private const float TagGap = 8f; // space between the version's last letter and the tag
    private static readonly Color TagColor = new(1f, 0.4f, 0.8f, 1f); // #FF66CC

    private static readonly MeowcowLogger Log = CoreMod.Log.Tagged("MainMenu");

    /// <summary>Called by CoreLifecycle's Menu phase, once per real menu load.</summary>
    internal static void OnMenu() => MelonCoroutines.Start(SetupNextFrame());

    /// <summary>Waits one frame, so vanilla's Awake and Start have run before we clone anything.</summary>
    /// <remarks>[SOURCE] Findings doc §2 (timing).</remarks>
    private static IEnumerator SetupNextFrame()
    {
        yield return null;

        MenuScreen? home = UIPathIndex.FindMainMenuHome();
        if (home == null)
        {
            Log.Warning("Main menu not found; left vanilla");
            yield break;
        }

        AddSettingsButton(home.transform);
        AddSuiteTag(home.transform);
    }

    /// <summary>Meowcow Settings, right after vanilla's Settings.</summary>
    private static void AddSettingsButton(Transform home)
    {
        try
        {
            Transform? settings = home.Find(UIPathIndex.MainMenuList)?.Find(UIPathIndex.MainMenuSettingsButton);
            if (settings == null)
            {
                Log.Warning("Settings button not found; Meowcow Settings not added");
                return;
            }

            UIBuilder.AddListButton(settings, SettingsButtonName, SettingsButtonLabel, OnSettingsClicked);
        }
        catch (Exception e)
        {
            Log.Error("Couldn't add Meowcow Settings", e);
        }
    }

    /// <summary>
    /// "Meowcow Suite" in pink, just after the version text. It lives straight under Home and is anchored
    /// bottom-left, so moving the logo or version never drags it along.
    /// </summary>
    private static void AddSuiteTag(Transform home)
    {
        try
        {
            TMP_Text? version = home.Find(UIPathIndex.MainMenuVersion)?.GetComponent<TMP_Text>();
            if (version == null)
            {
                Log.Warning("Version text not found; Meowcow Suite tag not added");
                return;
            }

            TMP_Text tag = UIBuilder.CloneText(version, TagName, TagText);
            RectTransform rect = tag.rectTransform;
            rect.SetParent(home, true);
            UIBuilder.SetAnchorKeepRect(rect, Vector2.zero);
            tag.color = TagColor;
            tag.horizontalAlignment = HorizontalAlignmentOptions.Left;

            // Line the tag's first letter up just after the version's last one.
            float versionRight = home.InverseTransformPoint(version.rectTransform.TransformPoint(
                new Vector3(UIBuilder.VisibleRect(version).xMax, 0f))).x;
            float tagLeft = home.InverseTransformPoint(rect.TransformPoint(
                new Vector3(UIBuilder.VisibleRect(tag).xMin, 0f))).x;
            rect.position += home.TransformVector(new Vector3(versionRight + TagGap - tagLeft, 0f, 0f));
        }
        catch (Exception e)
        {
            Log.Error("Couldn't add the Meowcow Suite tag", e);
        }
    }

    // [CHANGED] The Meowcow Settings window, with placeholder tabs for now (SuiteUI slice 2b). Built on first click
    // rather than at menu load, so players who never open it pay nothing; rebuilt after a scene change.
    private const string SettingsWindowName = "MeowcowSettingsWindow";
    private static MeowcowWindow? _settingsWindow;

    private static void OnSettingsClicked()
    {
        if (_settingsWindow == null || !_settingsWindow.IsAlive)
        {
            _settingsWindow = MeowcowWindow.CreateFromSettings(SettingsWindowName, SettingsButtonLabel);

            // [CHANGED] Placeholder tabs (empty pages) to test tabs and LB/RB until SuiteSettings fills them.
            _settingsWindow?.AddTab("General");
            _settingsWindow?.AddTab("Modules");
            _settingsWindow?.AddTab("About");
        }

        _settingsWindow?.Open();
    }
}