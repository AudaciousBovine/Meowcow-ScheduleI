using System;
using MelonLoader;
using MeowcowCore.Foundation;
using MeowcowCore.ModuleApi;

[assembly: MelonInfo(typeof(MeowcowTestModule.TestModuleMod), "MeowcowTestModule", "0.0.1", "Evelyna")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace MeowcowTestModule;

/// <summary>
/// Dev-only module that logs every SuiteModules call and can fail on purpose, to test waking, retries
/// and isolation. Never shipped; delete once Mirror runs on the new Core.
/// </summary>
/// <remarks>
/// [CHANGED] New. Switches live in MelonPreferences (category <c>MeowcowTestModule</c>), so tests don't
/// need a rebuild; edit them with the game closed.
/// </remarks>
public sealed class TestModuleMod : MelonMod, IMeowcowModule
{
    private MeowcowLogger _log = null!;
    private MelonPreferences_Entry<int> _wakeFailures = null!;
    private MelonPreferences_Entry<bool> _throwOnSessionStart = null!;
    private int _wakeCalls;

    public WakeTrigger WakeOn => WakeTrigger.Session;

    public override void OnInitializeMelon()
    {
        _log = new MeowcowLogger(LoggerInstance);

        MelonPreferences_Category category = MelonPreferences.CreateCategory("MeowcowTestModule");
        _wakeFailures = category.CreateEntry(
            "WakeFailures", 0, "Wake failures",
            "How many times OnWake fails before it succeeds. 0 = never fails. Tests retries and attempt counts.");
        _throwOnSessionStart = category.CreateEntry(
            "ThrowOnSessionStart", false, "Throw on session start",
            "OnSessionStart throws. Tests isolation, and that OnSessionEnd still arrives.");

        SuiteModules.Register(this);
        _log.Info($"Initialised (WakeFailures {_wakeFailures.Value}, ThrowOnSessionStart {_throwOnSessionStart.Value})");
    }

    public void OnWake()
    {
        _wakeCalls++;
        _log.Info($"OnWake (call {_wakeCalls})");
        if (_wakeCalls <= _wakeFailures.Value)
            throw new InvalidOperationException($"Test failure in OnWake ({_wakeCalls} of {_wakeFailures.Value})");
    }

    public void OnSessionStart(SessionInfo session)
    {
        _log.Info($"OnSessionStart (IsHost {session.IsHost})");
        if (_throwOnSessionStart.Value)
            throw new InvalidOperationException("Test failure in OnSessionStart");
    }

    public void OnSessionEnd() => _log.Info("OnSessionEnd");
}