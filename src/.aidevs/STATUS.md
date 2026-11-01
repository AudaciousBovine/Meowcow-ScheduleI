# MeowcowCore — Status

Current state of the Rebuild branch. **Read this with CLAUDE.md and `MeowcowCore-Design.md` at the start of
every session.** Update it at the end of each slice (or session): move finished items to Done, record
decisions, keep Open/Next current. Keep it short; history lives in git and the design doc.

_Last updated: 2026-11-01 (Slice 4 designed)_

---

## Done & committed

- **Improved Settings Menu** (design §9 "Rebuilt with adapters", R1–R4): a Core window cloned from vanilla's
  Settings; vanilla's own screen untouched (only Core additions named `Meowcow…`, which clones leave out).
  Every vanilla setting is a Core row bound through `SuiteSettings/VanillaSettingAdapters.cs` (Other, Audio,
  Gamepad, Input, Display via `UnappliedDisplaySettings`, Graphics). Slider ranges from vanilla's rows; each
  adapter uses its row script's exact conversion.
- **Unmapped-setting detection:** script scan (`ScheduleOne.UI.Settings` types + variants) and field scan;
  logs an error and shows a yellow footer line. Not adapted on purpose: ConfirmDisplaySettings,
  GameSettingsWindow, Keybinder, RestoreDefaultBindingsButton.
- **Improved/Vanilla toggle** (remembered): "Show Vanilla Settings Menu" in the footer / "Show Improved
  Settings Menu" + Back below vanilla's frame. Main and pause menu Settings buttons route to the chosen menu.
- **Current Game tab dropped:** Console and Seeded Mixing become playthrough settings in the save system.
- **SuiteUI/ConfirmDialogue.cs:** 15 s countdown confirm, Keep/Cancel with A/B prompts.
- **SuiteUI/WindowNavigator.cs:** Core drives gamepad navigation (D-pad tabs/subtabs, A/B enter/back,
  LB/RB ±1, LT/RT ±10, Y/Triangle switches menus). Prompts via `SuiteUI/GamepadGlyphs.cs`.
- **Description panel:** delayed hide (0.15 s) + 0.1 s fade.
- **Keybinds labels:** "Keyboard/Gamepad Sprint Mode", "Keyboard/Gamepad Sneak Mode".
- **Subtab clicks fixed** (`MeowCowWindow.StyleSubtab`): the tab's Background is kept at alpha 0 instead of
  switched off; it's the button's raycast target.
- **CLAUDE.md** added at the repo root; decompiles live in `Decompiles/` (gitignored).
- **Menu switch follows vanilla's `TogglePhone`** (`WindowNavigator.OptionsPressed/OptionsPath`): gamepad presses
  only; the prompt shows the action's gamepad binding. Tested: follows a rebound phone button.
- **`SuiteUI/RebindGuard.cs`:** one gate for Core's menu input around rebinds (vanilla's
  `RebindActionUI.IsRebindingInProgress` or Core's `Begin/End`), then until 0.25 s and every button released.
  Used by `WindowNavigator` (whole tick, Back swallowed) and `OptionsPressed` (so vanilla's screen too).
  Tested with vanilla's rebinds. Slice 4's rows must call `Begin/End`.
- **Pending-adapters placeholder removed** (`SettingsMenu`); an unknown layout page kind now logs a warning.
- **Slice 4 and 4b designed** in `MeowcowCore-Design.md` §9 ("Keybinds (slice 4)", "Input prompts (slice 4b)").
  The temporary input dumps are removed.

## Done this session (not yet committed)

- **Slice 4 risk tests passed** (design §9 "Order of work" 1); the temporary test file is removed. Findings that
  changed the design: Unity's `shortcutKeysConsumeInput` breaks vanilla (any composite outranks plain keys: the
  unused `Rotate` swallowed E), so combos use **Core's targeted suppression, context-filtered**; contexts come from
  `PlayerCamera.ActiveUIElements` names plus a few flags (design §9 "Active contexts").

---

## Next

1. **Slice 4 — Keybinds** (design §9), part 2: bindings data (add, save, load, restore per context). New files to be
   proposed first.
2. **Slice 4b — Prompts** (design §9). Read `Decompiles/…InputPromptsPanelUI.cs` first. Check vanilla's driving
   prompt: on gamepad it didn't show that the triggers drive (`VehicleDrive`); its module may list the wrong action.
3. **Slice 5** — General › Commands.
4. **Save system** — playthrough settings (Console, Seeded Mixing, …).

## Later (ideas, not designed)

- **UI navigation rework:** change how UI navigation works, especially in certain workstations. Needs each
  workstation looked at specifically (decompiles per station) before any design.
- **Independent driving keys:** swap Motion's binding set while driving (binding mask on enter/exit vehicle), no
  Harmony. Needs an enter/exit hook and care for gamepad steering.
- **Hotbar number keys 1–0 rebindable:** hard-coded in `PlayerInventory` (`Input.GetKeyDown`); would need a patch.
- **Vehicle exit / look-around keys of their own:** vanilla reads Interact and Secondary directly; would need a patch.

---

## Slice 4 research findings

### Vanilla input (action dump)
- `PlayerInput` behaviour: **BroadcastMessages**. One map: **"Generic"**. Schemes: `Keyboard and Mouse`, `Gamepad`.
- Button interactions are on the **bindings**, and vary: `Press(behavior=2)`, plain `Press`, or none. `GameInput`
  toggles state per event, so added bindings copy them exactly.
- `groups` strings are inconsistent (`;Keyboard and Mouse`, `Keyboard and Mouse`, or empty): always copy, never build.
- Nearly all actions are `Button`. The one `PassThrough` is **VehicleDrive** (1D composite).
- **Motion** (WASD) is a 2D Vector composite.
- Gamepad-only actions: InventoryLeft/Right, Holster, TogglePause.
- Game's Input System has `OneModifierComposite`, `TwoModifiersComposite`, `modifiersOrder`,
  `shortcutKeysConsumeInput`.

### Vanilla's Keybinds rows (row dump)
- **Keyboard (14 + 2 fixed):** Forward/Backward/Left/Right (Motion parts), Jump, Sprint, Crouch, Interact, Phone,
  Flashlight, Map, Texts, Journal, View Avatar; fixed labels "Use/Drag/Attack" (Primary), "Pick up" (Secondary).
- **Gamepad (11 + 2 fixed):** Select and Back/Cancel (cannot be rebound: UISelect, PrimaryExit), Interact, Toggle
  Phone, Jump, Crouch, Sprint, Flashlight / Headlight, Map/Messages/Journal Shortcut, View Avatar, Equip / Unequip.
- **Derived actions:** keyboard only Jump → VehicleHandbrake. Gamepad: Interact → PlaceObject, UIListForSale,
  SkateboardMount, GraffitiUndo, Reload; Flashlight → VehicleToggleLights, ToggleHeatmap; Jump → VehicleHandbrake;
  Select → DismissHint; Back → UIBack.
- **Vanilla gap:** rebinding WASD doesn't move VehicleDrive or RotateHeldItem; Interact (E) doesn't move
  BuildRotateRight; Space-based actions (Spray, Drop, Pickpocket, Body Search) never follow Jump.
- Labels differ by device (Texts / Messages Shortcut, Flashlight / Flashlight / Headlight).

### Where vanilla reads its controls (decompiles + session dump)
- **Hotbar:** `PlayerInventory.UpdateHotbarSelection` reads `GameInput.MouseScrollDelta` (the `ScrollWheel` action,
  shared by every scroll user), `InventoryLeft/Right`, Holster's own action, and number keys via `Input.GetKeyDown`.
  `TertiaryClick` re-equips the last slot.
- **Car:** `LandVehicle`: throttle `GameInput.VehicleDriveAxis` (keyboard: `MotionAxis.y`; pad: `VehicleDrive`),
  steering `MotionAxis.x`, handbrake `VehicleHandbrake`, exit Interact + an exit listener. `VehicleCamera`: look
  around = hold `SecondaryClick` + mouse; reset `VehicleResetCamera`. `VehicleMoveCamera` has no handler.
- **Building:** `BuildUpdate_Grid/Surface` read `BuildManager`'s references = PlaceObject, BuildRotateLeft,
  BuildRotateRight (confirmed at runtime). Grid toggles temperatures with `VehicleToggleLights`.
- **Clipboard:** `ToggleHeatmap` toggles the temperature overlay.
- **Packaging (Mk2):** `PackagingTool`: conveyor = Player Left/Right or hard-coded ←/→; drop = `_dropAction` =
  DropProduct (confirmed at runtime).
- **Graffiti:** `SpraySurfaceInteraction`: paint = hold PrimaryClick; undo = on-screen button (GraffitiUndo on pad).
- From play: `RotateHeldItem` (A/D) rotates held items in workstations (watering, soil, pouring) and pots;
  `SprayMistBottle` waters mushroom spores; `Reload` is guns only; `Rotate` (Q/E composite) seems unused.
- **Prompts:** `InputPromptsManager` panel prompts list every binding for the device; text prompts
  (`TryGetActionBindingDisplayString`) take the first; embedded glyphs follow a binding ID.
- `RebindActionUI.IsRebindingInProgress` is static; vanilla's rebinds have no cancel key and re-enable the action in
  the completing callback.
