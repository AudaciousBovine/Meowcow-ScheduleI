using System;
using System.Collections;
using Il2CppScheduleOne.DevUtilities;
using Il2CppTMPro;
using MeowcowCore.Foundation;
using MeowcowCore.SuiteSettingsManager;
using UnityEngine;
using Disclaimer = Il2CppScheduleOne.UI.MainMenu.Disclaimer;
using IntroManager = Il2CppScheduleOne.Cutscenes.IntroManager;
using SetupScreen = Il2CppScheduleOne.UI.MainMenu.SetupScreen;
using UObject = UnityEngine.Object;

namespace MeowcowCore.SuiteSettings;

/// <summary>
/// [CHANGED] Folder: <c>SuiteSettings</c> holds Core's own settings and what they do (the settings system
/// itself is <c>SuiteSettingsManager</c>).
/// Core's switches for skipping vanilla's steps (design §9, General › Main): Skip Disclaimer, Always Skip
/// Tutorial, Always Skip New Game Intro, and holding vanilla's intro skip skipping the whole intro. Each works
/// through vanilla's own flow, so nothing is patched. Shown in vanilla's Settings only.
/// </summary>
/// <remarks>
/// [CHANGED] New (Settings rework slice 3). Skip Splash Screen was removed: the TVGS logo is Unity's own
/// splash (with the studio's logo), which has finished before MelonLoader starts any mod.
/// [CHANGED] The intro skip was merged in (was its own IntroSkip class).
/// [SOURCE] Vanilla <c>IntroManager</c> (Update's hold-to-skip, Play's coroutine); <c>Disclaimer</c> (Awake starts a fade: text in, wait <c>Duration</c>, OnClose, then the
/// screen fades out), <c>SetupScreen.StartGame</c> (reads <c>SkipIntroToggle</c> only when the save is created).
/// [PERFORMANCE] A few field writes when the menu scene loads; one toggle set per main menu load.
/// </remarks>
internal static class VanillaSkips
{
    internal const string SkipDisclaimerKey = "skipDisclaimer";
    internal const string AlwaysSkipTutorialKey = "alwaysSkipTutorial";
    internal const string AlwaysSkipIntroKey = "alwaysSkipIntro";

    /// <summary>How long a session waits for a new game's intro to start before deciding there isn't one.</summary>
    private const float IntroStartTimeout = 30f;

    /// <summary>Vanilla's hold-to-skip time (IntroManager.SkipInputTime).</summary>
    private const float HoldToSkipTime = 0.5f;

    private const string MenuScene = "Menu";

    private static readonly MeowcowLogger Log = CoreMod.Log.Tagged("Skips");

    private static ToggleSetting? _skipDisclaimer;
    private static ToggleSetting? _alwaysSkipTutorial;
    private static ToggleSetting? _alwaysSkipIntro;
    private static bool _watchingIntro;

    /// <summary>Declares the settings on Core. Placed in vanilla's Settings by its layout table.</summary>
    internal static void Declare(ModuleSettings core)
    {
        SettingsSubtab vanilla = core.Subtab("Vanilla"); // not shown as a subtab: these live in vanilla Settings
        _skipDisclaimer = vanilla.Toggle(SkipDisclaimerKey, "Skip Disclaimer", false,
            "Skips the disclaimer shown before the main menu, with a short fade in.").ShownInVanillaSettings();
        _alwaysSkipTutorial = vanilla.Toggle(AlwaysSkipTutorialKey, "Always Skip Tutorial", false,
            "New games always skip the tutorial, and the tutorial option is hidden on the New Game screen.").ShownInVanillaSettings();

        _alwaysSkipIntro = vanilla.Toggle(AlwaysSkipIntroKey, "Always Skip New Game Intro", false,
            "New games go straight to the character creator, without the intro cutscene.").ShownInVanillaSettings();

        _alwaysSkipTutorial.Changed += _ => ApplyTutorial();
    }

    /// <summary>
    /// Skip Disclaimer, when the menu scene loads (the disclaimer has just started fading in): its text is
    /// hidden and its wait set to zero, so vanilla's own fade carries on straight to fading the screen out.
    /// The menu appears through a short fade, instead of all at once while it's still settling (which made
    /// the character and RV behind it jitter).
    /// </summary>
    /// <remarks>
    /// [CHANGED] Was: marking the disclaimer as already shown at launch, which skips it with no fade.
    /// On later menu loads vanilla skips the disclaimer itself (shown once per launch), so this only acts once.
    /// Called by CoreMod for every scene as it loads ([CHANGED] was: when initialised, which comes a frame
    /// later, after the text had already been drawn once).
    /// </remarks>
    internal static void OnSceneLoaded(string sceneName)
    {
        if (_skipDisclaimer?.Value != true || !string.Equals(sceneName, MenuScene, StringComparison.Ordinal)) return;
        try
        {
            Disclaimer? disclaimer = UObject.FindObjectOfType<Disclaimer>();
            if (disclaimer == null || !disclaimer.gameObject.activeInHierarchy) return; // already skipped by vanilla

            // [CHANGED] Every text on the disclaimer is switched off (was: switching off the text group's
            // object, which left the text showing, so the text isn't all under it). Only text is hidden: the
            // black screen stays, so the fade still happens.
            int hidden = 0;
            foreach (TMP_Text text in disclaimer.GetComponentsInChildren<TMP_Text>(true))
            {
                text.enabled = false;
                hidden++;
            }
            if (disclaimer.TextGroup != null) disclaimer.TextGroup.alpha = 1f; // ends vanilla's text fade-in
            disclaimer.Duration = 0f;            // no wait: straight to OnClose and the fade out
            Log.Debug($"Disclaimer skipped (fading out; {hidden} text hidden; text group on " +
                      $"'{disclaimer.TextGroup?.name}', screen group on '{disclaimer.Group?.name}')");
        }
        catch (Exception e)
        {
            Log.Error("Couldn't skip the disclaimer", e);
        }
    }
    /// <summary>
    /// Always Skip Tutorial on the New Game screen: on, the tutorial toggle is set on and its contents are
    /// hidden; off, they're shown again (the toggle keeps whatever it was). Called at each main menu load
    /// and when the setting changes.
    /// </summary>
    /// <remarks>
    /// The container's contents are hidden rather than the container: vanilla's <c>SetupScreen.Start</c>
    /// switches the container itself on when the screen first opens.
    /// </remarks>
    internal static void ApplyTutorial()
    {
        try
        {
            SetupScreen? screen = UObject.FindObjectOfType<SetupScreen>(true);
            if (screen == null || _alwaysSkipTutorial == null) return; // not in the main menu

            bool skip = _alwaysSkipTutorial.Value;
            if (skip && screen.SkipIntroToggle != null) screen.SkipIntroToggle.isOn = true;

            RectTransform? container = screen.SkipIntroContainer;
            if (container == null) return;
            for (int i = 0; i < container.childCount; i++) container.GetChild(i).gameObject.SetActive(!skip);
        }
        catch (Exception e)
        {
            Log.Error("Couldn't apply Always Skip Tutorial", e);
        }
    }

    // ---------- New-game intro ----------
    // The intro is one animation with an event at the start of each segment; vanilla's hold-to-skip (0.5 s)
    // jumps to the next segment's event. Skipping the whole intro fast-forwards it the same way, one segment
    // per frame (so each segment's event fires), then to the end: vanilla's coroutine sees the animation stop
    // and carries on as if it had played out (character creator, MarkIntroCompleted, onIntroDone, save).
    // Core's own instant black screen covers those frames, until vanilla opens the character creator.
    // [PERFORMANCE] One check per frame, only while a new game's intro is starting or playing (or up to
    // IntroStartTimeout in a save whose intro is done).

    /// <summary>A session started: if a new game's intro plays, watch it. Called by CoreLifecycle.</summary>
    internal static void OnSessionStart()
    {
        if (_watchingIntro) return;
        MelonLoader.MelonCoroutines.Start(WatchIntro());
    }

    private static IEnumerator WatchIntro()
    {
        _watchingIntro = true;
        IntroManager? intro = null;
        try
        {
            // Wait for the intro to start (a new game), or give up (a save whose intro is done).
            float waited = 0f;
            while (true)
            {
                if (!CoreLifecycle.IsInSession || waited > IntroStartTimeout) yield break;
                intro = Singleton<IntroManager>.Instance;
                if (intro != null && intro.IsPlaying) break;
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (_alwaysSkipIntro?.Value == true)
            {
                ShowBlackScreen();
                while (intro != null && !intro.Anim.isPlaying) yield return null; // starts once the game has loaded
                if (intro == null) yield break; // the scene changed while waiting
                Log.Debug("Skipping the intro (Always Skip New Game Intro)");
                yield return FastForward(intro);
                yield return HideBlackScreenWhenCreatorOpens(intro);
                yield break;
            }

            // Watch the hold: black goes up the frame before vanilla's skip fires, so nothing after it shows.
            bool started = false;
            bool wasReady = true;
            int blackSinceFrame = -1;
            while (intro != null && CoreLifecycle.IsInSession)
            {
                bool playing = intro.Anim.isPlaying;
                if (playing) started = true;
                else if (started) yield break; // the intro played out

                bool ready = intro.depressed;
                if (playing && wasReady && !ready)
                {
                    Log.Debug("Hold-to-skip used: skipping the whole intro");
                    ShowBlackScreen();
                    yield return FastForward(intro);
                    yield return HideBlackScreenWhenCreatorOpens(intro);
                    yield break;
                }

                // [CHANGED] The hold will complete on vanilla's next update: the black screen goes up now.
                // If the button is let go at the last moment instead, it comes down again.
                if (playing && ready && blackSinceFrame < 0 && intro.currentSkipTime + Time.deltaTime >= HoldToSkipTime)
                {
                    ShowBlackScreen();
                    blackSinceFrame = Time.frameCount;
                }
                else if (blackSinceFrame >= 0 && ready && intro.currentSkipTime <= 0f && Time.frameCount - blackSinceFrame > 2)
                {
                    HideBlackScreen();
                    blackSinceFrame = -1;
                }

                wasReady = ready;
                yield return null;
            }
        }
        finally
        {
            _watchingIntro = false;
            if (intro == null) HideBlackScreen(); // never left up if the scene goes away
        }
    }

    /// <summary>
    /// Plays the intro's remaining segments one per frame (each jump the same as vanilla's skip, so each
    /// segment's event fires), then moves to the end. Vanilla's coroutine sees the animation stop and
    /// carries on from there.
    /// </summary>
    private static IEnumerator FastForward(IntroManager intro)
    {
        AnimationClip? clip = intro.Anim.clip;
        if (clip == null) yield break;
        AnimationState state = intro.Anim[clip.name];
        var events = clip.events;

        while (intro != null && intro.Anim.isPlaying && intro.CurrentStep + 1 < events.Length)
        {
            int next = intro.CurrentStep + 1;
            state.time = events[next].time;
            intro.CurrentStep = next;
            yield return null;
        }

        if (intro != null && intro.Anim.isPlaying) state.time = clip.length;
    }

    // ---------- Instant black screen ----------
    // [CHANGED] Core's own black screen (was: vanilla's overlay, which fades in over a few frames and let the
    // fast-forward show through). It appears instantly, on top of everything, and is removed once vanilla
    // has opened the character creator: by then vanilla's own black overlay is fully up, and it fades out
    // over the creator as usual.

    private static GameObject? _blackScreen;

    private static void ShowBlackScreen()
    {
        try
        {
            if (_blackScreen != null) return;
            _blackScreen = new GameObject("MeowcowBlackScreen");
            UObject.DontDestroyOnLoad(_blackScreen);
            Canvas canvas = _blackScreen.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue; // above every vanilla canvas

            var fill = new GameObject("Fill");
            fill.transform.SetParent(_blackScreen.transform, false);
            UnityEngine.UI.Image image = fill.AddComponent<UnityEngine.UI.Image>();
            image.color = Color.black;
            image.raycastTarget = false;
            RectTransform rect = fill.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
        catch (Exception e)
        {
            Log.Warning($"Couldn't black out the screen: {e.Message}");
        }
    }

    private static void HideBlackScreen()
    {
        if (_blackScreen != null) UObject.Destroy(_blackScreen);
        _blackScreen = null;
    }

    /// <summary>
    /// Waits until vanilla has opened the character creator (it switches the intro's container off right
    /// after), then removes the black screen: vanilla's own overlay is fully up by then.
    /// </summary>
    private static IEnumerator HideBlackScreenWhenCreatorOpens(IntroManager intro)
    {
        float waited = 0f;
        while (intro != null && intro.Container != null && intro.Container.activeSelf && waited < 10f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        HideBlackScreen();
    }
}