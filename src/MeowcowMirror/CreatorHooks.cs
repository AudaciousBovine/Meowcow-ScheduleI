using System;
using System.Collections;
using MelonLoader;
using Il2CppInterop.Runtime;
using MeowcowMirror.Contexts;
using MeowcowMirror.Layout;
using Il2CppScheduleOne.PlayerScripts;
using UObject = UnityEngine.Object;
using Creator = Il2CppScheduleOne.CharacterCreator.CharacterCreator;
using CreatorState = Il2CppScheduleOne.CharacterCreator.CharacterCreatorState;

namespace MeowcowMirror
{
    /// <summary>
    /// Listens to the creator's OnOpen, which fires for both Mirror.Open() and the vanilla intro,
    /// and builds the right context. OnOpen is an event we subscribe to, not a patched method,
    /// so IL2CPP inlining can't skip it.
    /// </summary>
    internal static class CreatorHooks
    {
        private static IntPtr _hookedCreator;
        private static Il2CppSystem.Action<CreatorState> _onOpen; // kept referenced so it isn't collected

        internal static void EnsureHooked(Creator creator)
        {
            if (creator == null || creator.Pointer == _hookedCreator) return;

            _onOpen = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<CreatorState>>(new Action<CreatorState>(OnOpened));
            creator.add_OnOpen(_onOpen);
            _hookedCreator = creator.Pointer;
            MirrorMod.Log.Msg("Hooked character creator");
        }

        /// <summary>Called on scene load so the intro is caught even if Mirror.Open() never ran.</summary>
        internal static void HookIfPresent() => EnsureHooked(UObject.FindObjectOfType<Creator>(true));

        private static void OnOpened(CreatorState state)
        {
            // Exceptions must not escape into native code.
            try
            {
                var creator = UObject.FindObjectOfType<Creator>(true);
                string pending = Mirror.TakePending();

                ContextDefinition context;
                if (pending == null)
                {
                    context = MirrorContexts.CreateDefinition; // vanilla opened it: the intro
                }
                else if (!MirrorContexts.TryGet(pending, out context))
                {
                    MirrorMod.Log.Warning($"Pending context '{pending}' vanished; closing");
                    Abort(creator);
                    return;
                }
                else
                {
                    SeedFromPlayer(creator, state);
                }

                var layout = ContextFilter.Filter(VanillaOptions.CreateVanillaLayout(), context);
                if (layout.Tabs.Count == 0)
                {
                    MirrorMod.Log.Warning($"Context '{context.Id}' has no options; closing");
                    Abort(creator);
                    return;
                }

                Mirror.ActiveContext = context;

                // OnOpen fires in the same frame the creator's UI is switched on, before Unity has
                // laid any of it out, so measuring rows now gives nonsense. Build next frame; the
                // creator's fade-in starts fully transparent, so the one-frame wait is never visible.
                MelonCoroutines.Start(BuildNextFrame(creator, context, layout));
            }
            catch (Exception e)
            {
                MirrorMod.Log.Error($"Failed to set up creator: {e}");
            }
        }

        private static IEnumerator BuildNextFrame(Creator creator, ContextDefinition context, LayoutDefinition layout)
        {
            yield return null;

            if (creator == null || !creator.IsOpen || Mirror.ActiveContext != context) yield break;

            try
            {
                CreatorLayoutBuilder.Apply(layout);
                MirrorMod.Log.Msg($"Creator opened in context '{context.Id}'");
            }
            catch (Exception e)
            {
                MirrorMod.Log.Error($"Failed to build creator layout: {e}");
            }
        }

        /// <summary>Mirror contexts start from the player's current look instead of the preset.</summary>
        private static void SeedFromPlayer(Creator creator, CreatorState state)
        {
            var current = Player.Local?.CurrentAppearance;
            if (current == null || state == null) { MirrorMod.Log.Warning("Couldn't seed creator from player"); return; }

            state.Appearance = current.Clone();
            creator.ApplyNakedAppearance();
            CreatorLayoutBuilder.FindInterface()?.SetInputsToAppearance(state);
        }

        private static void Abort(Creator creator)
        {
            Mirror.EndSession();
            if (creator == null) return;
            creator.Close();
            MirrorFrame.RestoreCamera();
        }
    }
}
