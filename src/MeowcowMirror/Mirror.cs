using MeowcowMirror.Contexts;
using UObject = UnityEngine.Object;
using Creator = Il2CppScheduleOne.CharacterCreator.CharacterCreator;

namespace MeowcowMirror
{
    /// <summary>Mirror's public entry point for opening the creator.</summary>
    public static class Mirror
    {
        private static string _pending;

        /// <summary>The context of the open creator session, or null when closed.</summary>
        public static ContextDefinition ActiveContext { get; internal set; }

        /// <summary>
        /// Opens the creator in a registered context, starting from the player's current look.
        /// Returns false if the context is unknown or the creator can't open right now.
        /// </summary>
        public static bool Open(string contextId)
        {
            if (!ContextRegistryHas(contextId, out var context)) return false;

            var creator = UObject.FindObjectOfType<Creator>(true);
            if (creator == null) { MirrorMod.Log.Warning("CharacterCreator not found"); return false; }

            var container = creator._container.gameObject;
            if (creator.IsOpen)
            {
                if (container.activeSelf) { MirrorMod.Log.Msg("Creator already open"); return false; }

                // Open but hidden: a reopen raced vanilla's close timer. Recover.
                MirrorMod.Log.Warning("Creator stuck open but hidden; recovering");
                creator.Close();
                Layout.MirrorFrame.RestoreCamera();
                return false;
            }
            if (container.activeSelf) { MirrorMod.Log.Msg("Creator still closing; try again in a moment"); return false; }

            CreatorHooks.EnsureHooked(creator);
            _pending = context.Id;
            creator.Open(); // CreatorHooks.OnOpened sets everything up from here
            return true;
        }

        /// <summary>Takes the context set by Open(); null means vanilla opened the creator (the intro).</summary>
        internal static string TakePending()
        {
            var pending = _pending;
            _pending = null;
            return pending;
        }

        internal static void EndSession()
        {
            ActiveContext = null;
            Costs.Receipt.End();
        }

        private static bool ContextRegistryHas(string contextId, out ContextDefinition context)
        {
            if (MirrorContexts.TryGet(contextId, out context)) return true;
            MirrorMod.Log.Warning($"Unknown context '{contextId}'");
            return false;
        }
    }
}
