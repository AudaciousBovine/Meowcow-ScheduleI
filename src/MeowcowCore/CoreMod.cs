using MelonLoader;
using MeowcowCore.Foundation;
using UnityEngine;
using UnityEngine.SceneManagement;

// [CHANGED] Rebuild: version 0.1.0 (pre-rebuild Core was 0.0.1), so logs and bug reports can tell them apart.
// The name "MeowcowCore" is Core's module Id (design §5): it names its settings folder and log prefix,
// so it never changes once released.
[assembly: MelonInfo(typeof(MeowcowCore.CoreMod), "MeowcowCore", "0.1.0", "Evelyna")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace MeowcowCore;

/// <summary>
/// Core's entry point. Kept thin on purpose: it receives MelonLoader's callbacks and hands the work to
/// Foundation classes, holding no logic of its own.
/// </summary>
/// <remarks>
/// [CHANGED] Rewritten for the rebuild. Unlike the old CoreMod, it has no <c>OnUpdate</c>: nothing runs
/// per frame. [CHANGED] No <c>OnApplicationQuit</c> either: there's no Quit phase (see CoreLifecycle).
/// </remarks>
public sealed class CoreMod : MelonMod
{
    private const string PreferencesCategory = "MeowcowCore";
    private const string DebugEntry = "DebugLogging";

    /// <summary>Core's logger. Other mods create their own from their <c>LoggerInstance</c>.</summary>
    /// <remarks>
    /// <c>null!</c> tells the compiler it's set before use: MelonLoader always calls
    /// <see cref="OnInitializeMelon"/> before anything else. [SOURCE] common MelonLoader practice.
    /// </remarks>
    internal static MeowcowLogger Log { get; private set; } = null!;

    public override void OnInitializeMelon()
    {
        Log = new MeowcowLogger(LoggerInstance);
        LoadDebugSetting();

        // The version comes from MelonInfo above, so it's never declared twice.
        // [CHANGED] MelonLoader already prints "[MeowcowCore]" before every line, so the name isn't repeated.
        Log.Info($"Version {Info.Version} loaded");
    }

    /// <summary>Forwarded to <see cref="CoreLifecycle"/>, which only acts on the Menu scene.</summary>
    /// <remarks>
    /// [CHANGED] New: CoreLifecycle's Menu phase. Only a MelonMod receives this callback, so CoreMod passes it on.
    /// [PERFORMANCE] One string comparison per scene change.
    /// </remarks>
    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
        CoreLifecycle.OnSceneInitialized(sceneName);
    }

    /// <summary>
    /// Logs a scene event with what Unity knows about it: the handle tells whether two events are the
    /// same scene instance, the frame tells whether they happened together, and the scene count shows
    /// how many scenes are loaded at that moment.
    /// </summary>
    private static void LogSceneEvent(string kind, int buildIndex, string sceneName)
    {
        if (!MeowcowLogger.DebugEnabled) return;
        Scene scene = SceneManager.GetSceneByName(sceneName);
        Log.Debug($"Scene {kind}: '{sceneName}' index {buildIndex}, handle {scene.handle}, " +
                  $"frame {Time.frameCount}, loaded scenes {SceneManager.sceneCount}, " +
                  $"active '{SceneManager.GetActiveScene().name}'");
    }

    /// <summary>
    /// Sets the suite-wide debug toggle from MelonPreferences (debug settings stay there, not in
    /// Meowcow Settings). Read once at launch; a change takes effect on the next launch.
    /// </summary>
    /// <remarks>[PERFORMANCE] One preference read at launch.</remarks>
    private static void LoadDebugSetting()
    {
        MelonPreferences_Category category = MelonPreferences.CreateCategory(PreferencesCategory);
        MelonPreferences_Entry<bool> debug = category.CreateEntry(
            DebugEntry,
            false,
            "Debug logging",
            "Extra log lines from every Meowcow mod, for troubleshooting.");

        MeowcowLogger.DebugEnabled = debug.Value;
        if (MeowcowLogger.DebugEnabled) Log.Info("Debug logging is on");
    }
}