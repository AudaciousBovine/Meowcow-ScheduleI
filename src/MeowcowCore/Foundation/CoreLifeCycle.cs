using System;
using Il2CppFishNet;
using Il2CppInterop.Runtime;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
using MeowcowCore.Menus;
using MeowcowCore.ModuleApi;
using MeowcowCore.SuiteSettings;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace MeowcowCore.Foundation;

/// <summary>
/// The game's lifecycle as the suite sees it, in four phases:
/// <list type="table">
///   <item><term>Load</term><description><c>OnInitializeMelon</c> (handled by CoreMod)</description></item>
///   <item><term>Menu</term><description>The <c>"Menu"</c> scene initialised, every time (vanilla rebuilds the menu)</description></item>
///   <item><term>Session start</term><description><c>LoadManager.onLoadComplete</c> in the <c>"Main"</c> scene</description></item>
///   <item><term>Session end</term><description><c>LoadManager.onPreSceneChange</c> while a session is active,
///   or the Menu phase arriving while one still is (connection drops)</description></item>
/// </list>
/// There's no Quit phase: unsaved data is dropped like vanilla, and every launch starts clean, so
/// session end is cleanup only and nothing is ever written at quit.
/// </summary>
/// <remarks>
/// [CHANGED] New for the rebuild (design §4). Session start and end now call the suite's modules
/// through <see cref="SuiteModules"/>.
/// [SOURCE] Vanilla <c>LoadManager</c> (its own systems treat <c>onLoadComplete</c> as "game ready",
/// and <c>SaveManager</c> cleans up on <c>onPreSceneChange</c>); LilithsEngine's single-listener pattern.
/// [PERFORMANCE] Two event listeners and a flag; nothing per frame.
/// </remarks>
public static class CoreLifecycle
{
    private const string MenuScene = "Menu";
    private const string GameScene = "Main"; // the tutorial has its own scene, so it stays dormant

    private static readonly MeowcowLogger Log = CoreMod.Log.Tagged("Lifecycle");

    // IL2CPP rule: converted delegates are kept in static fields so they're never garbage collected
    // while the game still holds them.
    private static UnityAction? _onLoadComplete;
    private static UnityAction? _onPreSceneChange;
    private static bool _hooked;

    // [CHANGED] The last Menu scene instance we acted on. MelonLoader reported the Menu scene three
    // times per return to the menu; each real load gets a new Unity handle, so a repeat is skipped.
    // [SOURCE] General practice: make phase triggers idempotent.
    private static int _lastMenuHandle;

    /// <summary>
    /// True from session start to session end. Core's own flag rather than vanilla's: <c>onPreSceneChange</c>
    /// also fires going *into* a game and at the start of a client join, and vanilla's <c>IsGameLoaded</c>
    /// flips 1–2 s after load complete.
    /// </summary>
    public static bool IsInSession { get; private set; }

    /// <summary>Called by CoreMod for every scene. Only the Menu scene matters here.</summary>
    internal static void OnSceneInitialized(string sceneName)
    {
        if (!string.Equals(sceneName, MenuScene, StringComparison.Ordinal)) return;

        // [CHANGED] Once per real menu load, however many times the event arrives.
        // [PERFORMANCE] One scene lookup and one integer compare per Menu event.
        int handle = SceneManager.GetSceneByName(sceneName).handle;
        if (handle == _lastMenuHandle)
        {
            Log.Debug($"Menu event repeated for the same scene (handle {handle}); skipped");
            return;
        }
        _lastMenuHandle = handle;

        // [CHANGED] Safety net: FishNet's DefaultScene loads the Menu scene on any connection stop, so a
        // dropped connection can reach the menu before vanilla's ExitToMenu fires onPreSceneChange.
        // Ending the session here keeps the order right (Session end, then Menu) however we got back;
        // the late onPreSceneChange is then ignored because the session is already over.
        // [SOURCE] FishNet DefaultScene (ServerManager/ClientManager connection-stopped handlers).
        // [PERFORMANCE] One boolean check per menu load.
        if (IsInSession) EndSession("the menu loaded before the game's exit event");

        // LoadManager is a persistent singleton that survives scene changes, so one subscription lasts
        // the whole run. Hooked here rather than at launch, where it may not exist yet.
        if (!_hooked) HookLoadManager();

        // [CHANGED] Every mod has registered by now (OnInitializeMelon all run before the first scene).
        SuiteModules.LogRegisteredOnce();

        // [CHANGED] Writes settings files that differ from what was declared (new, removed or corrected
        // settings, or changes still pending). Only files whose contents changed are written.
        MeowcowSettings.FlushAll();

        Log.Info("Menu");

        // [CHANGED] Core's main menu additions. Started here, built one frame later (see MainMenu).
        try
        {
            MainMenu.OnMenu();
        }
        catch (Exception e)
        {
            Log.Error("Main menu setup couldn't start", e);
        }
    }

    /// <summary>
    /// Subscribes to LoadManager's events, once. If it isn't there yet, the next Menu scene retries.
    /// </summary>
    private static void HookLoadManager()
    {
        try
        {
            LoadManager? loadManager = Singleton<LoadManager>.Instance;
            if (loadManager == null)
            {
                Log.Warning("LoadManager not found; sessions won't be detected until the next menu load");
                return;
            }

            _onLoadComplete ??= DelegateSupport.ConvertDelegate<UnityAction>(new Action(OnLoadComplete));
            _onPreSceneChange ??= DelegateSupport.ConvertDelegate<UnityAction>(new Action(OnPreSceneChange));

            loadManager.onLoadComplete.AddListener(_onLoadComplete);
            loadManager.onPreSceneChange.AddListener(_onPreSceneChange);
            _hooked = true;
            Log.Debug("Hooked LoadManager");
        }
        catch (Exception e)
        {
            Log.Error("Couldn't hook LoadManager; sessions won't be detected", e);
        }
    }

    // ---------- Game events ----------
    // Called from native code: exceptions must never escape. A throw inside onLoadComplete would skip
    // the game's other listeners and hang the load.

    private static void OnLoadComplete()
    {
        try
        {
            if (IsInSession) return;

            string scene = SceneManager.GetActiveScene().name;
            if (!string.Equals(scene, GameScene, StringComparison.Ordinal))
            {
                Log.Debug($"Load complete in '{scene}'; not a session");
                return;
            }

            // The game guarantees the local player exists by now (hosting and joining alike); this only
            // guards against a future game update changing that.
            if (Player.Local == null) Log.Warning("Session started before the local player existed");

            IsInSession = true;
            Log.Info("Session start");

            // [CHANGED] The in-game Settings, reorganised like the main menu's (plus Current Game).
            MeowcowCore.Menus.SettingsMenu.SetupInSave();

            // [CHANGED] Modules: wake Session modules, then give every awake module the session.
            SuiteModules.SessionStarted(new SessionInfo(isHost: InstanceFinder.IsServer));
        }
        catch (Exception e)
        {
            Log.Error("Session start failed", e);
        }
    }

    private static void OnPreSceneChange()
    {
        try
        {
            // Also fires going into a game and at the start of a client join; only a running session ends.
            if (!IsInSession) return;
            EndSession(null);
        }
        catch (Exception e)
        {
            Log.Error("Session end failed", e);
        }
    }

    /// <summary>
    /// Ends the running session. One place for it, since it's reached from two triggers: vanilla's
    /// <c>onPreSceneChange</c> (normal exits) and the Menu phase (connection drops).
    /// </summary>
    /// <param name="reason">Why it ended outside the normal path; null for a normal exit.</param>
    /// <remarks>[CHANGED] Split out of OnPreSceneChange so both triggers share it.</remarks>
    private static void EndSession(string? reason)
    {
        IsInSession = false;
        Log.Info(reason == null ? "Session end" : $"Session end ({reason})");

        // [CHANGED] Modules: every module that got a session start gets its end, on either path.
        SuiteModules.SessionEnded();
    }
}