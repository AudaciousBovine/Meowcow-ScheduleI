using HarmonyLib;
using MelonLoader;
using MeowcowMirror.Contexts;
using CustomizationUI = Il2CppScheduleOne.UI.CharacterCustomization.CharacterCustomizationUI;
using BarbershopUI = Il2CppScheduleOne.UI.CharacterCustomization.BarbershopUI;
using TattooShopUI = Il2CppScheduleOne.UI.CharacterCustomization.TattooShopUI;

namespace MeowcowMirror.Patches
{
    /// <summary>
    /// Puts Mirror's menus on the vanilla barbershop and tattoo shop. The shop still opens normally;
    /// ShopSession then swaps its menu for Mirror's. The shops are opened and closed through
    /// UnityEvents and MonoState events, which call these methods through pointers, so the patches
    /// always run (no inlining to worry about).
    /// TattooShopUI uses the base Open; BarbershopUI overrides it, so both are patched.
    /// </summary>
    [HarmonyPatch(typeof(CustomizationUI), nameof(CustomizationUI.Open))]
    internal static class CustomizationOpenPatch
    {
        private static void Prefix(CustomizationUI __instance) => ShopRedirect.TakeOverIfSupported(__instance);
    }

    [HarmonyPatch(typeof(BarbershopUI), nameof(BarbershopUI.Open))]
    internal static class BarbershopOpenPatch
    {
        private static void Prefix(BarbershopUI __instance) => ShopRedirect.TakeOverIfSupported(__instance);
    }

    /// <summary>Catches the shop closing by itself (Escape) so the creator closes with it.</summary>
    [HarmonyPatch(typeof(CustomizationUI), nameof(CustomizationUI.OnClose))]
    internal static class CustomizationClosePatch
    {
        private static void Postfix(CustomizationUI __instance) => ShopSession.OnShopClosed(__instance);
    }

    internal static class ShopRedirect
    {
        internal static void TakeOverIfSupported(CustomizationUI ui)
        {
            PreviewControls.LastShop = ui; // preview mouse controls also work in shops that keep their vanilla menu
            ui.RigRotationSlider.gameObject.SetActive(false); // dragging the preview replaces it

            string contextId = ContextFor(ui);
            if (contextId == null) return; // some other customization UI: leave it alone

            // A context with nothing to show yet (tattoo, until tattoo rows exist) keeps the vanilla menu.
            if (!ContextFilter.HasOptions(contextId))
            {
                MirrorMod.Log.Msg($"Context '{contextId}' has no options yet; keeping the vanilla menu");
                return;
            }

            MelonCoroutines.Start(ShopSession.TakeOver(ui, contextId));
        }

        internal static string ContextFor(CustomizationUI ui)
        {
            if (ui.TryCast<BarbershopUI>() != null) return MirrorContexts.Barber;
            if (ui.TryCast<TattooShopUI>() != null) return MirrorContexts.Tattoo;
            return null;
        }
    }
}
