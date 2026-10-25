using System;
using UnityEngine;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.PlayerScripts;
using MeowcowMirror.Layout;
using AvatarRig = Il2CppScheduleOne.AvatarFramework.Avatar;
using CustomizationUI = Il2CppScheduleOne.UI.CharacterCustomization.CharacterCustomizationUI;

namespace MeowcowMirror
{
    /// <summary>
    /// Mouse controls for the character preview, active only when the mouse isn't over a menu:
    /// drag sideways to turn the character (without limit), drag up/down to move the camera,
    /// and scroll to zoom. Works in Mirror's creator, Mirror shop visits, and vanilla shops.
    /// </summary>
    internal static class PreviewControls
    {
        internal static float DegreesPerPixel = 0.35f;
        internal static bool InvertRotate;
        internal static float PanPerPixel = 0.003f;  // metres the camera moves per pixel (unzoomed)
        internal static bool InvertPan;
        internal static float PanLimit = 0.4f;       // global default: max metres up or down (contexts can override)
        internal static float DeadzonePixels = 8f;   // drag distance before a drag locks to rotate or pan
        internal static float SwitchPixels = 40f;    // sustained movement on the other axis needed to switch modes mid-drag
        internal static float ZoomStep = 0.075f;     // fraction of the camera's distance per wheel notch
        internal static float MaxZoom = 0.75f;       // global default: how far in (fraction of the distance; contexts can override)
        internal static float MinZoom = 0f;          // global default: how far out past the default spot (0 or negative; contexts can override)

        /// <summary>The most recently opened vanilla customization shop, recorded by the Open patches.</summary>
        internal static CustomizationUI LastShop;

        private enum DragAxis { None, Rotate, Pan }

        private static bool _dragging;
        private static DragAxis _axis;
        private static Vector2 _deadzoneTravel;
        private static float _opposingTravel;
        private static Vector2 _lastMouse;
        private static float _angle;      // unbounded, so the character can turn forever
        private static float _zoom;       // fraction of the distance to the focus point: 0 = default spot, negative = further out
        private static float _pan;
        private static IntPtr _viewKey;   // camera spot of the menu the zoom/pan belong to

        private struct Target
        {
            public Transform RigContainer;      // what turns the character
            public Action<float> SetVanillaYaw; // keeps the game's own rotation target in step
            public Transform CameraBase;        // the camera spot the menu uses
            public AvatarRig Avatar;            // the character being previewed
            public float PanLimit;              // max metres up or down for this menu's context
            public float MaxZoom;               // zoom-in limit for this menu's context
            public float MinZoom;               // zoom-out limit for this menu's context
            public bool OverUi;                 // mouse is over this menu's windows
        }

        /// <summary>Called every frame; does nothing unless a customization menu is open.</summary>
        internal static void Tick()
        {
            if (!TryGetTarget(out var target))
            {
                _dragging = false;
                return;
            }

            // A different camera spot means a different menu opened: start fresh.
            if (target.CameraBase != null && target.CameraBase.Pointer != _viewKey)
            {
                _viewKey = target.CameraBase.Pointer;
                _zoom = 0f;
                _pan = 0f;
            }

            bool cameraChanged = TickDrag(target);
            cameraChanged |= TickZoom(target);
            if (cameraChanged) ApplyCamera(target);
        }

        // ---------- Drag: rotate (sideways) and pan (up/down) ----------

        private static bool TickDrag(Target target)
        {
            if (!Input.GetMouseButton(0))
            {
                _dragging = false;
                return false;
            }

            Vector2 mouse = Input.mousePosition;
            if (Input.GetMouseButtonDown(0))
            {
                _dragging = !target.OverUi; // only start a drag on empty space
                _axis = DragAxis.None;
                _deadzoneTravel = Vector2.zero;
                _opposingTravel = 0f;
                _lastMouse = mouse;
                if (_dragging && target.RigContainer != null) _angle = target.RigContainer.localEulerAngles.y;
                return false;
            }
            if (!_dragging) return false;

            Vector2 delta = mouse - _lastMouse;
            _lastMouse = mouse;
            if (delta == Vector2.zero) return false;

            // Deadzone: wait until the drag has clearly gone sideways or up/down, then stick to that
            // axis for the rest of the drag. The travel collected meanwhile is applied on lock.
            if (_axis == DragAxis.None)
            {
                _deadzoneTravel += delta;
                if (_deadzoneTravel.magnitude < DeadzonePixels) return false;
                _axis = Mathf.Abs(_deadzoneTravel.x) >= Mathf.Abs(_deadzoneTravel.y) ? DragAxis.Rotate : DragAxis.Pan;
                delta = _deadzoneTravel;
            }
            else
            {
                // Switching: movement on the other axis builds up, movement on the locked axis wears
                // it back down, so wobble cancels out and only a sustained change of direction switches.
                float along = _axis == DragAxis.Rotate ? Mathf.Abs(delta.x) : Mathf.Abs(delta.y);
                float across = _axis == DragAxis.Rotate ? Mathf.Abs(delta.y) : Mathf.Abs(delta.x);
                _opposingTravel = Mathf.Max(0f, _opposingTravel + across - along);

                if (_opposingTravel >= SwitchPixels)
                {
                    _axis = _axis == DragAxis.Rotate ? DragAxis.Pan : DragAxis.Rotate;
                    _opposingTravel = 0f;
                }
            }

            if (_axis == DragAxis.Rotate && target.RigContainer != null)
            {
                _angle += delta.x * DegreesPerPixel * (InvertRotate ? -1f : 1f);
                float yaw = Mathf.Repeat(_angle, 360f);
                target.RigContainer.localEulerAngles = new Vector3(0f, yaw, 0f);
                target.SetVanillaYaw?.Invoke(yaw); // the game eases toward its target; keep it equal so it never fights us
                return false;
            }

            if (_axis == DragAxis.Pan && target.CameraBase != null)
            {
                // Grab-the-scene feel: drag up and the character moves up (camera goes down).
                // Scaled by distance, so it feels the same at any zoom.
                float scale = 1f - _zoom;
                float pan = Mathf.Clamp(_pan - delta.y * PanPerPixel * scale * (InvertPan ? -1f : 1f), -target.PanLimit, target.PanLimit);
                if (!Mathf.Approximately(pan, _pan)) { _pan = pan; return true; }
            }
            return false;
        }

        // ---------- Scroll: zoom ----------

        private static bool TickZoom(Target target)
        {
            if (target.CameraBase == null || target.Avatar == null) return false;
            if (target.OverUi || _dragging) return false;

            float wheel = Input.mouseScrollDelta.y;
            if (wheel == 0f) return false;

            float zoom = Mathf.Clamp(_zoom + Mathf.Sign(wheel) * ZoomStep, target.MinZoom, target.MaxZoom);
            if (Mathf.Approximately(zoom, _zoom)) return false;
            _zoom = zoom;
            return true;
        }

        /// <summary>
        /// Zoom moves along the line to a focus point that glides from the body's centre (default and
        /// zoomed out) to the face (fully in), which keeps the character in place on screen.
        /// Pan then shifts the camera straight up or down. The camera keeps the menu's angle.
        /// </summary>
        private static void ApplyCamera(Target target)
        {
            var cam = PlayerSingleton<PlayerCamera>.Instance;
            if (cam == null || target.CameraBase == null || target.Avatar == null) return;

            Vector3 centre = target.Avatar.CenterPoint;
            Vector3 face = target.Avatar.Eyes != null ? target.Avatar.Eyes.transform.position : centre;

            // The focus only glides toward the face while zooming in; zoomed out it stays on the body.
            float faceWeight = target.MaxZoom > 0f ? Mathf.Clamp01(_zoom / target.MaxZoom) : 0f;
            Vector3 focus = Vector3.Lerp(centre, face, faceWeight);

            // Unclamped so negative zoom moves the camera back along the same line.
            Vector3 position = Vector3.LerpUnclamped(target.CameraBase.position, focus, _zoom) + Vector3.up * _pan;

            cam.OverrideTransform(position, target.CameraBase.rotation, 0.08f, false);
        }

        // ---------- Which menu is open ----------

        private static bool TryGetTarget(out Target target)
        {
            target = default;
            Vector2 mouse = Input.mousePosition;

            // Mirror's creator, on its own stage or on a vanilla shop.
            if (Mirror.ActiveContext != null)
            {
                if (CreatorLayoutBuilder.CurrentInterface == null) return false;
                target.OverUi = MirrorFrame.ContainsScreenPoint(mouse);

                var shop = ShopSession.Shop;
                if (shop != null)
                {
                    FillFromShop(ref target, shop);
                }
                else if (CreatorHooks.Current != null)
                {
                    var creator = CreatorHooks.Current;
                    target.RigContainer = creator._avatarContainer;
                    target.SetVanillaYaw = yaw => creator._rigTargetY = yaw;
                    target.CameraBase = creator._cameraPosition;
                    target.Avatar = creator._avatar;
                }
                ApplyLimits(ref target, Mirror.ActiveContext);
                return true;
            }

            // A vanilla shop showing its own menu.
            var vanillaShop = LastShop;
            if (vanillaShop != null && vanillaShop.IsOpen && vanillaShop.Canvas.enabled)
            {
                FillFromShop(ref target, vanillaShop);
                target.OverUi = Contains(vanillaShop.MainContainer, mouse);

                // A shop still on its vanilla menu uses its context's limits too (e.g. tattoo).
                Contexts.MirrorContexts.TryGet(Patches.ShopRedirect.ContextFor(vanillaShop), out var shopContext);
                ApplyLimits(ref target, shopContext);
                return true;
            }

            return false;
        }

        private static void FillFromShop(ref Target target, CustomizationUI shop)
        {
            var stage = shop.CharacterCustomizationShop;
            target.RigContainer = stage.RigContainer;
            target.SetVanillaYaw = yaw => shop.rigTargetY = yaw;
            target.CameraBase = stage.CameraPosition;
            target.Avatar = stage.AvatarRig;
        }

        /// <summary>A context's own camera limits, or the global settings where it has none.</summary>
        private static void ApplyLimits(ref Target target, Contexts.ContextDefinition context)
        {
            target.PanLimit = context?.PanLimit ?? PanLimit;
            target.MaxZoom = Mathf.Clamp(context?.MaxZoom ?? MaxZoom, 0f, 0.95f);   // never all the way into the character
            target.MinZoom = Mathf.Clamp(context?.MinZoom ?? MinZoom, -3f, 0f);
        }

        internal static bool Contains(RectTransform rect, Vector2 screenPoint)
        {
            if (rect == null || !rect.gameObject.activeInHierarchy) return false;
            var canvas = rect.GetComponentInParent<Canvas>();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, cam);
        }
    }
}
