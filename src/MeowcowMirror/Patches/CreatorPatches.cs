using HarmonyLib;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.PlayerScripts;
using Creator = Il2CppScheduleOne.CharacterCreator.CharacterCreator;

namespace MeowcowMirror
{
    /// <summary>Tracks whether the open creator belongs to Mirror (true) or to the vanilla intro (false).</summary>
    internal static class MirrorSession
    {
        internal static bool Active;
    }

    /// <summary>
    /// Vanilla Close() never restores the camera; IntroManager does that after the intro.
    /// For Mirror sessions we do it ourselves, leaving the intro path untouched.
    /// </summary>
    [HarmonyPatch(typeof(Creator), nameof(Creator.Close))]
    internal static class CreatorClosePatch
    {
        private static void Postfix()
        {
            if (!MirrorSession.Active) return;
            MirrorSession.Active = false;

            var cam = PlayerSingleton<PlayerCamera>.Instance;
            if (cam == null) return;

            cam.StopTransformOverride(0f, false, false);
            cam.StopFOVOverride(0f);
            MirrorMod.Log.Msg("Mirror session closed; camera restored");
        }
    }
}