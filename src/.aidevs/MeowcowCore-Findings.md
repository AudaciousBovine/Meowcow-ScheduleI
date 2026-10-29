# MeowcowCore — Findings from the pre-rebuild code

What the old Core (deleted from the `Rebuild` branch) taught us that **isn't built yet**, kept so nothing
has to be rediscovered. Each section says where in the design doc (`MeowcowCore-Design.md`) it will be used.
The original code is still on `main`; file names are given as `[SOURCE]` for each finding.

Remove a finding from this doc once it has been built into the new Core.
Already built (and so removed): the main menu paths and additions, button and text cloning, gamepad
list registration, layout helpers, the window frame (metrics, pattern, Back, corner prompt, dragging),
the one-frame setup delay, and the settings files (code-defined settings, validation, atomic writes).

---

## 1. UI techniques not yet used → SuiteUI (§7)

### Hover shine
- Path: `<list button>/Shine`, the bar that fades in on hover. `[SOURCE]` `Menu/MenuPaths.cs`
- Vanilla's shine sprite has a **hard edge on one side** and fades to the other. A centred shine is two
  halves: left half flipped with `localScale.x = -1`, right half normal, hard edges meeting in the middle.
  `raycastTarget = false` on both. `[SOURCE]` `Menu/UiTools.cs` (`CreateShineHalf`)
- Art source: the first `Image` named `Shine` whose parent has a `Button`; copy its sprite, material,
  type and colour. Fallback when none exists: plain white at alpha 0.12. `[SOURCE]` `Menu/UiTools.cs` (`FindShineInScene`)
- Fade with a `CanvasGroup` (not interactable, no raycasts), `MoveTowards` at about 10 alpha/s using
  `unscaledDeltaTime`; place the shine as a sibling just before the text so it draws underneath.
  `[SOURCE]` `Menu/LobbyToggle.cs`
- `[PERFORMANCE]` the fade ran every frame; the new version should only tick while hovered or fading.

### Pointer
- **Pointer over a rect, any canvas:** walk up to the root canvas; pass its `worldCamera` unless it's
  `ScreenSpaceOverlay` (then `null`) to `RectTransformUtility.RectangleContainsScreenPoint`. `[SOURCE]` `Menu/UiTools.cs` (`ContainsPointer`)

---

## 2. Main menu and window extras → SuiteUI (§7), Themes (§14)

- **Colours:** recorded from vanilla at build time (background, content, title, tab, tab selected via
  `Checkmark`, tab text, Back text) so a style override can always fall back to vanilla.
- A `Ready` event after Core's additions let other mods record the menu's "vanilla" state including them.
  Useful pattern for live element handles.

---

## 3. Lobby → Play mode (§6) and Main menu (§13)

`[SOURCE]` `Menu/LobbyToggle.cs`, `PlayMode.cs`

- **Paths:** `LobbyInterface.Container/Entries` (invite button and player slots),
  `LobbyInterface.LobbyTitle` (the "Lobby (n/4)" text). `[SOURCE]` `Menu/MenuPaths.cs`
- **The lobby panel exists in the main menu and in game scenes** (the pause menu).
- **Lobby events:** `Lobby.add_OnLobbyChange(Il2CppSystem.Action)`; read `Lobby.PlayerCount`
  (clamp to at least 1). Convert with `DelegateSupport.ConvertDelegate` and keep the delegate referenced.
- **The `Lobby` object outlives scenes:** subscribe once per lobby, tracked by `Pointer`.
- **Vanilla rewrites the lobby title** on every lobby change *and* on scene changes. Listening to the same
  event after vanilla lets our text land on top; `LobbyInterface.UpdateUI()` restores vanilla's
  "Lobby (n/4)".
- **Title as a button:** `raycastTarget = true`, add a `Button` with the title as target graphic, white
  normal/highlighted/selected, 0.85 pressed, 0.5 alpha disabled; disabled when Solo isn't allowed.
- Hiding `Container/Entries` hides the invite button and slots.
- **Old rule, superseded by §6:** Solo was refused while others were in the lobby, and a join forced
  Multiplayer. The new design makes co-op permanent per playthrough instead.

---

## 4. Colour settings → SuiteSettings (§8), when a colour setting is needed

`[SOURCE]` `Settings/ColorHex.cs`

- **`ColorHex`:** parses `#RRGGBB` or `#RRGGBBAA` (`#` optional); six digits reports `hasAlpha = false`
  so callers can keep their own alpha; `Normalize` gives a leading `#` and upper case. Port as is.

---

## 5. Debug tools → SuiteUI (§7), or a debug area

`[SOURCE]` `Diagnostics/SceneDump.cs`

- `UIBuilder.DumpHierarchy` now covers one object at a time (debug log). The old **SceneDump** went
  further, if that is ever needed again: it wrote every root canvas in the loaded scenes to a text file: path, active state,
  components, `RectTransform` layout, text (legacy and TMP, cut at 120 characters), `MenuScreen`
  settings, and each button's persistent `onClick` targets.
- Triggered by a MelonPreferences key (default F11, Ctrl required).
- `[PERFORMANCE]` heavy (walks every UI object); debug only, never automatic.