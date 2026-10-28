using System;

namespace MeowcowCore.ModuleApi;

/// <summary>
/// When a module wakes on its own (design §5). Combinable: <c>SaveMenu | Session</c> wakes on whichever
/// comes first. Every module also wakes when Meowcow Settings opens or another module asks for it.
/// </summary>
/// <remarks>
/// [CHANGED] New. <c>SaveMenu</c> can't fire until the save menu is built, but modules declare it now so
/// they don't have to change later.
/// </remarks>
[Flags]
public enum WakeTrigger
{
    /// <summary>Only wakes when Meowcow Settings opens or another module asks.</summary>
    None = 0,

    /// <summary>Load Game, Continue or New Game opens.</summary>
    SaveMenu = 1 << 0,

    /// <summary>A game finishes loading.</summary>
    Session = 1 << 1,
}