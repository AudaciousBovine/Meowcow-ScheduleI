namespace MeowcowCore.ModuleApi;

/// <summary>
/// Facts about the current session, fixed from session start to session end. Handed to
/// <see cref="IMeowcowModule.OnSessionStart"/>.
/// </summary>
/// <remarks>
/// [CHANGED] New. Only <see cref="IsHost"/> for now; <c>Playthrough</c> and <c>SaveMode</c> arrive with
/// save management (adding properties later doesn't break modules). Play mode isn't here on purpose:
/// it's a live Core value (design §6). Only Core creates these.
/// </remarks>
public sealed class SessionInfo
{
    internal SessionInfo(bool isHost)
    {
        IsHost = isHost;
    }

    /// <summary>True when this machine runs and saves the game; false on a client.</summary>
    public bool IsHost { get; }
}