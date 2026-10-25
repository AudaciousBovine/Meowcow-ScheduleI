using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Il2CppInterop.Runtime;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.PlayerScripts;
using MeowcowMirror.Layout;
using UObject = UnityEngine.Object;
using Creator = Il2CppScheduleOne.CharacterCreator.CharacterCreator;
using CreatorUI = Il2CppScheduleOne.CharacterCreator.CharacterCreatorInterface;
using CustomizationUI = Il2CppScheduleOne.UI.CharacterCustomization.CharacterCustomizationUI;
using NakedAppearance = Il2CppScheduleOne.Core.Avatar.NakedAppearance;
using PlayerAppearance = Il2CppScheduleOne.Avatar.Player.PlayerAppearance;

namespace MeowcowMirror
{
    /// <summary>
    /// Runs Mirror's menus on top of a vanilla shop. The vanilla shop opens as normal (its camera,
    /// preview rig, fade and input state); only its menu is hidden and replaced with the creator UI.
    /// Changes are previewed on the shop's rig, and closing goes through the shop's own Close(),
    /// which applies the appearance and restores the camera the vanilla way.
    /// </summary>
    internal static class ShopSession
    {
        private const float OpenTimeout = 3f;

        private static CustomizationUI _shop;
        private static GameObject _hiddenTitle;
        private static IntPtr _hookedAppearance;
        private static IntPtr _hookedSlider;
        private static Il2CppSystem.Action<NakedAppearance> _onPreviewChanged; // kept referenced so they aren't collected
        private static UnityAction<float> _onRotation;

        internal static bool Active => _shop != null;

        private static bool IsFor(CustomizationUI shop) => _shop != null && shop != null && _shop.Pointer == shop.Pointer;

        /// <summary>Waits for the vanilla shop to finish opening, then swaps its menu for Mirror's.</summary>
        internal static IEnumerator TakeOver(CustomizationUI shop, string contextId)
        {
            // The vanilla shop fades in and only sets IsOpen once its camera and rig are in place.
            for (float t = 0f; !shop.IsOpen; t += Time.unscaledDeltaTime)
            {
                if (t > OpenTimeout) { MirrorMod.Log.Warning("Shop didn't finish opening; leaving the vanilla menu"); yield break; }
                yield return null;
            }

            var creator = UObject.FindObjectOfType<Creator>(true);
            var ui = CreatorLayoutBuilder.FindInterface();
            if (creator == null || ui == null) { MirrorMod.Log.Warning("Creator not found; leaving the vanilla menu"); yield break; }

            HookPreview(creator, ui);

            _shop = shop;
            if (!Mirror.Open(contextId))
            {
                _shop = null; // the vanilla menu stays usable
                yield break;
            }

            shop.Canvas.enabled = false; // hide the vanilla menu; everything else stays vanilla
            PointCameraAtShop();
            SetCreatorTitleVisible(ui, false);
            ui._avatarRotationSlider.SetValueWithoutNotify(0f);
            shop.rigTargetY = 0f;
        }

        /// <summary>Ends the session from Mirror's Done (apply) or Cancel (no apply).</summary>
        internal static void Finish(bool apply, PlayerAppearance appearance)
        {
            var shop = _shop;
            if (shop == null) return;
            End();

            // The vanilla shop applies currentAppearance when it closes, so hand it ours.
            if (apply && appearance != null) shop.currentAppearance = appearance.Clone();

            var creator = UObject.FindObjectOfType<Creator>(true);
            if (creator != null && creator.IsOpen) creator.Close();
            shop.Close();
        }

        /// <summary>The vanilla shop closed by itself (e.g. Escape): close the creator with it.</summary>
        internal static void OnShopClosed(CustomizationUI shop)
        {
            if (!IsFor(shop)) return;
            End();
            Mirror.EndSession();

            var creator = UObject.FindObjectOfType<Creator>(true);
            if (creator != null && creator.IsOpen) creator.Close();
            MirrorMod.Log.Msg("Shop closed; creator closed with it");
        }

        private static void End()
        {
            _shop = null;
            if (_hiddenTitle != null) _hiddenTitle.SetActive(true);
            _hiddenTitle = null;
        }

        /// <summary>The creator moved the camera to its own stage when it opened; put it back on the shop.</summary>
        private static void PointCameraAtShop()
        {
            var cam = PlayerSingleton<PlayerCamera>.Instance;
            var stage = _shop.CharacterCustomizationShop;
            if (cam == null || stage == null) return;

            cam.OverrideTransform(stage.CameraPosition.position, stage.CameraPosition.rotation, 0f, false);
            cam.OverrideFOV(60f, 0f);
        }

        /// <summary>Vanilla shops have no "Customize Appearance" title, so hide the creator's.</summary>
        private static void SetCreatorTitleVisible(CreatorUI ui, bool visible)
        {
            var title = ui.transform.Find("Title Background");
            if (title == null) return;
            title.gameObject.SetActive(visible);
            if (!visible) _hiddenTitle = title.gameObject;
        }

        /// <summary>
        /// Subscribes once to the creator's preview avatar and rotation slider. The avatar fires
        /// OnNakedAppearanceChanged on every change, which we copy to the shop's rig; the slider
        /// turns the shop's rig instead of the creator's.
        /// </summary>
        private static void HookPreview(Creator creator, CreatorUI ui)
        {
            var appearance = creator._avatar.Appearance;
            if (appearance.Pointer != _hookedAppearance)
            {
                _onPreviewChanged = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<NakedAppearance>>(
                    new Action<NakedAppearance>(OnPreviewChanged));
                appearance.add_OnNakedAppearanceChanged(_onPreviewChanged);
                _hookedAppearance = appearance.Pointer;
            }

            var slider = ui._avatarRotationSlider;
            if (slider.Pointer != _hookedSlider)
            {
                _onRotation = DelegateSupport.ConvertDelegate<UnityAction<float>>(new Action<float>(OnRotation));
                slider.onValueChanged.AddListener(_onRotation);
                _hookedSlider = slider.Pointer;
            }
        }

        private static void OnPreviewChanged(NakedAppearance naked)
        {
            if (_shop == null || naked == null) return;
            try
            {
                _shop.CharacterCustomizationShop.AvatarRig.Appearance.ApplyNakedAppearance(naked.Clone());
            }
            catch (Exception e)
            {
                MirrorMod.Log.Error($"Shop preview failed: {e.Message}");
            }
        }

        private static void OnRotation(float value)
        {
            if (_shop != null) _shop.rigTargetY = value * 359f; // the shop's Update eases its rig toward this
        }
    }
}
