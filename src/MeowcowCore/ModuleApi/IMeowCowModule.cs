namespace MeowcowCore.ModuleApi;

/// <summary>
/// What a Meowcow mod implements to join the suite. Implemented by the mod's <c>MelonMod</c> class and
/// registered with <see cref="SuiteModules.Register"/> in <c>OnInitializeMelon</c>.
/// </summary>
/// <remarks>
/// [CHANGED] New (design §5). Every call has an empty default body, so a module only writes the ones it
/// uses. Core always calls through this interface, which is what default bodies need: implement them as
/// plain public methods with the same signature. More calls arrive with the systems that need them
/// (<c>OnBecameCoop</c> with play mode, <c>OnSaving</c> with save management); adding them won't break
/// existing modules.
/// The module's Id, name and version come from its <c>MelonInfo</c>, so nothing is declared twice.
/// [SOURCE] C# default interface methods; LilithsEngine's module contract.
/// </remarks>
public interface IMeowcowModule
{
    /// <summary>When this module wakes on its own.</summary>
    WakeTrigger WakeOn { get; }

    /// <summary>
    /// Once, the first time the module is needed, before any other call. If it throws, the module stays
    /// asleep and the next trigger tries again.
    /// </summary>
    void OnWake() { }

    /// <summary>A game finished loading. Every awake module gets this.</summary>
    void OnSessionStart(SessionInfo session) { }

    /// <summary>
    /// Leaving the game. Every module that got <see cref="OnSessionStart"/> gets this, even if its start
    /// threw. Cleanup only: anything that must survive is written when it changes or at a save, never here.
    /// </summary>
    void OnSessionEnd() { }
}