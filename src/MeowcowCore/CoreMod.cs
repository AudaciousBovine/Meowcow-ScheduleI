using MelonLoader;
using MeowcowCore.Foundation;
using MeowcowCore.SuiteSettingsManager;

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

        // [CHANGED] Core's settings: its tab in Meowcow Settings (shown first), plus its switches for vanilla
        // behaviour, which live in vanilla's Settings.
        ModuleSettings core = MeowcowSettings.ForCore(this);
        SuiteSettings.VanillaSkips.Declare(core);
        SuiteSettings.InputSplit.Declare(core);

        // The version comes from MelonInfo above, so it's never declared twice.
        // [CHANGED] MelonLoader already prints "[MeowcowCore]" before every line, so the name isn't repeated.
        Log.Info($"Version {Info.Version} loaded");
    }

    /// <summary>Forwarded to <see cref="CoreLifecycle"/>, which only acts on the Menu scene.</summary>
    /// <remarks>
    /// [CHANGED] New: CoreLifecycle's Menu phase. Only a MelonMod receives this callback, so CoreMod passes it on.
    /// [PERFORMANCE] One string comparison per scene change.
    /// </remarks>
    public override void OnSceneWasInitialized(int buildIndex, string sceneName) =>
        CoreLifecycle.OnSceneInitialized(sceneName);

    /// <summary>
    /// Arrives in the frame the scene loads, right after its objects' Awake and before anything is drawn
    /// (OnSceneWasInitialized comes a frame later). Used by Skip Disclaimer, which must act before the
    /// disclaimer's text is first drawn.
    /// </summary>
    /// <remarks>[CHANGED] New. [PERFORMANCE] One string comparison per scene load.</remarks>
    public override void OnSceneWasLoaded(int buildIndex, string sceneName) =>
        SuiteSettings.VanillaSkips.OnSceneLoaded(sceneName);

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