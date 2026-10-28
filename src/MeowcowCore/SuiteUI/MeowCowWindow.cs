using System;
using Il2CppTMPro;
using MeowcowCore.Foundation;
using UnityEngine;
using MenuScreen = Il2CppScheduleOne.UI.MainMenu.MenuScreen;
using SettingsScreen = Il2CppScheduleOne.UI.MainMenu.SettingsScreen;
using UIPanel = Il2CppScheduleOne.UIPanel;
using UIScreen = Il2CppScheduleOne.UIScreen;
using UObject = UnityEngine.Object;

namespace MeowcowCore.SuiteUI;

/// <summary>
/// The suite's shared window (design §7). Built from a clone of vanilla's Settings screen, so it has
/// vanilla's look and behaviour: open and close transitions, Escape closing it and returning to the
/// previous screen, and gamepad focus.
/// </summary>
/// <remarks>
/// [CHANGED] New. Slice 2a: the shell only (clone, component swap, checks, empty content). Tabs, scroll
/// area, Back button and dragging come in 2b.
/// How the clone stays safe: it's made inside an inactive parent, so no vanilla code runs on it until
/// the settings script has been swapped for a plain <c>MenuScreen</c>.
/// [SOURCE] Vanilla <c>MenuScreen</c>, <c>SettingsScreen</c>, <c>MonoState</c> and <c>UITab</c>.
/// </remarks>
public sealed class MeowcowWindow
{
    private static readonly MeowcowLogger Log = CoreMod.Log.Tagged("Window");

    private MeowcowWindow(MenuScreen screen, RectTransform content)
    {
        Screen = screen;
        Content = content;
    }

    /// <summary>The window's screen: vanilla's open/close and Escape handling live here.</summary>
    public MenuScreen Screen { get; }

    /// <summary>Where the window's contents go. Empty after creation.</summary>
    public RectTransform Content { get; }

    /// <summary>False once the window's scene is gone (it's destroyed with the scene).</summary>
    public bool IsAlive => Screen != null;

    public bool IsOpen => IsAlive && Screen.IsOpen;

    public void Open()
    {
        if (IsAlive) Screen.Open();
    }

    public void Close()
    {
        if (IsAlive) Screen.Close();
    }

    /// <summary>
    /// Builds a new window from vanilla's Settings screen in the current scene, or returns null (with a
    /// warning) if the screen isn't there or doesn't look as expected. The window starts closed.
    /// </summary>
    /// <param name="objectName">Unique object name: vanilla tracks open UI by name.</param>
    /// <remarks>[PERFORMANCE] One clone of the Settings screen; done once per scene, on first use.</remarks>
    public static MeowcowWindow? CreateFromSettings(string objectName, string title)
    {
        SettingsScreen? source = UIPathIndex.FindSettingsScreen();
        if (source == null)
        {
            Log.Warning("Vanilla Settings screen not found; window not built");
            return null;
        }

        GameObject? clone = null;
        GameObject? staging = null;
        try
        {
            // Inside an inactive parent, nothing on the clone wakes up, even if the source is open.
            staging = new GameObject("MeowcowStaging");
            staging.SetActive(false);
            clone = UObject.Instantiate(source.gameObject, staging.transform);
            clone.name = objectName;

            UIBuilder.DumpHierarchy(clone.transform, Log); // debug only: how the shell is built

            MenuScreen screen = SwapToPlainScreen(clone, source);
            if (!BelongsToClone(screen, clone))
            {
                UObject.Destroy(clone);
                return null;
            }

            RectTransform? content = clone.transform.Find(UIPathIndex.WindowContent)?.TryCast<RectTransform>();
            if (content == null)
            {
                Log.Warning($"Settings screen has no '{UIPathIndex.WindowContent}'; window not built");
                UObject.Destroy(clone);
                return null;
            }

            // Vanilla's category panels and their settings rows go; the window brings its own contents.
            // [CHANGED] Their gamepad panels are detached first, or opening the window crashes (see DetachPanels).
            DetachPanels(screen, content);
            for (int i = content.childCount - 1; i >= 0; i--) UObject.DestroyImmediate(content.GetChild(i).gameObject);

            TMP_Text? titleText = clone.transform.Find(UIPathIndex.WindowTitle)?.GetComponent<TMP_Text>();
            if (titleText != null) titleText.text = title;

            // Into the real UI, then one activation so Awake runs exactly as for vanilla's screen:
            // MenuScreen.Awake registers Escape, hooks the state's open/close, and switches itself off.
            clone.SetActive(false);
            clone.transform.SetParent(source.transform.parent, false);
            clone.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
            clone.SetActive(true);

            Log.Debug($"Built '{objectName}'");
            return new MeowcowWindow(screen, content);
        }
        catch (Exception e)
        {
            Log.Error($"Couldn't build window '{objectName}'", e);
            if (clone != null) UObject.Destroy(clone);
            return null;
        }
        finally
        {
            if (staging != null) UObject.Destroy(staging);
        }
    }

    /// <summary>
    /// Replaces the clone's <c>SettingsScreen</c> with a plain <c>MenuScreen</c>, copying the fields that
    /// carry the screen behaviour. <c>SettingsScreen</c> is a <c>MenuScreen</c> subclass, so it can't
    /// simply be removed; this keeps the behaviour and drops the settings logic.
    /// </summary>
    private static MenuScreen SwapToPlainScreen(GameObject clone, SettingsScreen source)
    {
        SettingsScreen cloned = clone.GetComponent<SettingsScreen>();
        MenuScreen screen = clone.AddComponent<MenuScreen>();

        // The clone's own copies (Group, State, uiScreen, uiPanel point inside the clone after Instantiate).
        screen.ExitInputPriority = cloned.ExitInputPriority;
        screen.OpenOnStart = false;
        screen.AttachLobbyToScreen = cloned.AttachLobbyToScreen;
        screen.PreviousScreen = source.PreviousScreen; // where Escape returns to (Home on the main menu)
        screen.Group = cloned.Group;
        screen.State = cloned.State;
        screen.uiScreen = cloned.uiScreen;
        screen.uiPanel = cloned.uiPanel;

        UObject.DestroyImmediate(cloned);
        return screen;
    }

    /// <summary>
    /// Takes every gamepad panel under <paramref name="subtree"/> out of the window's gamepad screen, so
    /// it can be destroyed safely. Otherwise vanilla's <c>UIScreen</c> keeps them in its list, and opening
    /// the window crashes when it asks a destroyed panel whether it's visible.
    /// </summary>
    /// <remarks>
    /// [CHANGED] New, after the first in-game test crashed in <c>UIScreen.SetCurrentSelectedPanel</c>.
    /// Must run <i>before</i> destroying: <c>RemovePanel</c> compares the panel with its current selection
    /// using Unity's <c>==</c>, which treats a destroyed object as null, so removing an already-destroyed
    /// panel would itself crash.
    /// [SOURCE] Vanilla <c>UIScreen</c> (panels list, <c>RemovePanel</c>).
    /// [PERFORMANCE] One pass over the screen's panels (about a dozen), once per window build.
    /// </remarks>
    private static void DetachPanels(MenuScreen screen, Transform subtree)
    {
        UIScreen? uiScreen = screen.uiScreen;
        if (uiScreen != null)
        {
            // Collect first: RemovePanel changes the list being read.
            var doomed = new System.Collections.Generic.List<UIPanel>();
            var panels = uiScreen.panels;
            for (int i = 0; i < panels.Count; i++)
            {
                UIPanel panel = panels[i];
                if (panel != null && panel.transform.IsChildOf(subtree)) doomed.Add(panel);
            }
            foreach (UIPanel panel in doomed) uiScreen.RemovePanel(panel);
            Log.Debug($"Detached {doomed.Count} gamepad panels; {panels.Count} left");
        }

        // The screen's starting panel, if it was one of them.
        if (screen.uiPanel != null && screen.uiPanel.transform.IsChildOf(subtree)) screen.uiPanel = null;
    }

    /// <summary>
    /// Checks the screen's state and fade group are the clone's own. If they pointed back at vanilla's
    /// screen, opening the window would open vanilla Settings instead, so the window isn't built.
    /// </summary>
    private static bool BelongsToClone(MenuScreen screen, GameObject clone)
    {
        Transform root = clone.transform;
        if (screen.State == null || !screen.State.transform.IsChildOf(root))
        {
            Log.Warning("Settings screen's state isn't part of the screen; window not built");
            return false;
        }
        if (screen.Group == null || !screen.Group.transform.IsChildOf(root))
        {
            Log.Warning("Settings screen's fade group isn't part of the screen; window not built");
            return false;
        }
        if (screen.uiScreen != null && !screen.uiScreen.transform.IsChildOf(root))
            Log.Warning("Settings screen's gamepad screen isn't part of the screen; gamepad focus may be wrong");
        return true;
    }
}