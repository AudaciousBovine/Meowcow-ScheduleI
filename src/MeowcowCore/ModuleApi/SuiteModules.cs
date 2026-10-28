using System;
using System.Collections.Generic;
using MelonLoader;
using MeowcowCore.Foundation;

namespace MeowcowCore.ModuleApi;

/// <summary>
/// The registry of the suite's modules: registration, waking, and calling them at each phase.
/// Every call into a module is isolated, so one failing module never stops Core or the others.
/// </summary>
/// <remarks>
/// [CHANGED] New (design §5; renamed from CoreModules, since these aren't parts of Core).
/// For now: waking on <see cref="WakeTrigger.Session"/>, session start and end. Waking from Meowcow
/// Settings, the save menu and other modules arrives with those systems.
/// [SOURCE] LilithsEngine's module registry; isolation per call is general practice.
/// [PERFORMANCE] One walk over a short list per phase; nothing per frame.
/// </remarks>
public static class SuiteModules
{
    /// <summary>One registered module and what Core knows about it.</summary>
    private sealed class Entry
    {
        public Entry(MelonMod mod, IMeowcowModule module)
        {
            Id = mod.Info.Name;
            Module = module;
            Log = new MeowcowLogger(mod.LoggerInstance);
        }

        /// <summary>The module Id: its MelonInfo name.</summary>
        public string Id { get; }
        public IMeowcowModule Module { get; }

        /// <summary>The module's own logger, so its errors appear under its name.</summary>
        public MeowcowLogger Log { get; }

        public bool IsAwake { get; set; }
        public int WakeAttempts { get; set; }

        /// <summary>Got OnSessionStart and is owed OnSessionEnd.</summary>
        public bool GotSessionStart { get; set; }
    }

    private static readonly List<Entry> Modules = new();
    private static bool _summaryLogged;

    /// <summary>
    /// Adds a mod to the suite. Call it from the mod's <c>OnInitializeMelon</c>:
    /// <c>SuiteModules.Register(this);</c>
    /// </summary>
    /// <remarks>
    /// The constraint makes the compiler check "a MelonMod that implements IMeowcowModule", so a mistake
    /// fails the build instead of showing up in-game.
    /// Doesn't depend on Core having started: MelonLoader doesn't promise Core initialises first, so this
    /// only stores the module and logs through the mod's own logger.
    /// </remarks>
    public static void Register<T>(T mod) where T : MelonMod, IMeowcowModule
    {
        if (mod == null) throw new ArgumentNullException(nameof(mod));

        var entry = new Entry(mod, mod);
        foreach (Entry existing in Modules)
        {
            if (!string.Equals(existing.Id, entry.Id, StringComparison.Ordinal)) continue;
            entry.Log.Error($"A module named '{entry.Id}' is already registered; this one is ignored");
            return;
        }

        Modules.Add(entry);
        entry.Log.Debug("Registered with MeowcowCore");
    }

    /// <summary>Logs which modules registered, once, at the first menu. Useful in bug reports.</summary>
    internal static void LogRegisteredOnce()
    {
        if (_summaryLogged) return;
        _summaryLogged = true;

        MeowcowLogger log = CoreMod.Log.Tagged("Modules");
        if (Modules.Count == 0)
        {
            log.Info("No modules registered");
            return;
        }

        var ids = new string[Modules.Count];
        for (int i = 0; i < Modules.Count; i++) ids[i] = Modules[i].Id;
        log.Info($"{Modules.Count} registered: {string.Join(", ", ids)}");
    }

    // ---------- Phases (called by CoreLifecycle) ----------

    /// <summary>Wakes the modules that wake on Session, then gives every awake module the session.</summary>
    internal static void SessionStarted(SessionInfo session)
    {
        foreach (Entry entry in Modules)
        {
            if (WakesOn(entry, WakeTrigger.Session)) Wake(entry, "session start");
        }

        foreach (Entry entry in Modules)
        {
            if (!entry.IsAwake) continue;

            // Marked before the call: a start that throws halfway still gets its end, to clean up.
            entry.GotSessionStart = true;
            try
            {
                entry.Module.OnSessionStart(session);
            }
            catch (Exception e)
            {
                entry.Log.Error("OnSessionStart failed", e);
            }
        }
    }

    /// <summary>Gives every module that got a session start its session end.</summary>
    internal static void SessionEnded()
    {
        foreach (Entry entry in Modules)
        {
            if (!entry.GotSessionStart) continue;

            entry.GotSessionStart = false;
            try
            {
                entry.Module.OnSessionEnd();
            }
            catch (Exception e)
            {
                entry.Log.Error("OnSessionEnd failed", e);
            }
        }
    }

    // ---------- Waking ----------

    /// <summary>
    /// Wakes a module if it's asleep. A failed wake leaves it asleep; the next trigger tries again.
    /// </summary>
    /// <param name="triggeredBy">What caused this attempt, shown in the log.</param>
    /// <returns>True if the module is awake afterwards.</returns>
    private static bool Wake(Entry entry, string triggeredBy)
    {
        if (entry.IsAwake) return true;

        entry.WakeAttempts++;
        try
        {
            entry.Module.OnWake();
            entry.IsAwake = true;
            entry.Log.Info($"Woke (triggered by: {triggeredBy})");
            return true;
        }
        catch (Exception e)
        {
            entry.Log.Error(
                $"Failed to wake (attempt {entry.WakeAttempts}, triggered by: {triggeredBy}); " +
                "it stays asleep until the next trigger", e);
            return false;
        }
    }

    /// <summary>Whether a module wakes on a trigger. Isolated too, since WakeOn is module code.</summary>
    /// <remarks>[PERFORMANCE] A bitwise check rather than HasFlag.</remarks>
    private static bool WakesOn(Entry entry, WakeTrigger trigger)
    {
        try
        {
            return (entry.Module.WakeOn & trigger) != 0;
        }
        catch (Exception e)
        {
            entry.Log.Error("Reading WakeOn failed; treated as None", e);
            return false;
        }
    }
}