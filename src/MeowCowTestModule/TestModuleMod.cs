using System;
using MelonLoader;
using MeowcowCore.Foundation;
using MeowcowCore.ModuleApi;
using MeowcowCore.SuiteSettings;

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
        DeclareTestSettings();
        _log.Info($"Initialised (WakeFailures {_wakeFailures.Value}, ThrowOnSessionStart {_throwOnSessionStart.Value})");
    }

    /// <summary>
    /// One setting of each type across two subtabs, to test Meowcow Settings: rows, subtabs, saving, and the
    /// Changed events (logged).
    /// </summary>
    /// <remarks>[CHANGED] New for SuiteSettings slice 1.</remarks>
    private void DeclareTestSettings()
    {
        ModuleSettings settings = MeowcowSettings.For(this);

        SettingsSubtab general = settings.Subtab("General");
        ToggleSetting toggle = general.Toggle("testToggle", "Test toggle", true, "An on/off setting.");
        NumberSetting number = general.Number("testNumber", "Test number", 50f, 0f, 100f, step: 5f,
            wholeNumbers: true, description: "A whole number from 0 to 100.");

        SettingsSubtab more = settings.Subtab("More");
        ChoiceSetting choice = more.Choice("testChoice", "Test choice", "Medium", new[] { "Low", "Medium", "High" },
            "One of three choices.");

        toggle.Changed += v => _log.Info($"Test toggle → {v}");
        number.Changed += v => _log.Info($"Test number → {v}");
        choice.Changed += v => _log.Info($"Test choice → {v}");
        _log.Info($"Settings loaded: toggle {toggle.Value}, number {number.Value}, choice {choice.Value}");
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