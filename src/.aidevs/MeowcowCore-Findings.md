# MeowcowCore — Findings from the pre-rebuild code

What the old Core (deleted from the `Rebuild` branch) taught us, kept so nothing has to be rediscovered.
Each section says where in the design doc (`MeowcowCore-Design.md`) it will be used.
The original code is still on `main`; file names are given as `[SOURCE]` for each finding.

Remove a finding from this doc once it has been built into the new Core.

---

## 1. Vanilla UI paths → SuiteUI `UIPathIndex` (§7)

`[SOURCE]` `Menu/MenuPaths.cs`, found with `SceneDump`.

### Main menu
| Path | What it is |
|---|---|
| `Home` | The home screen: a `MenuScreen` with `OpenOnStart` |
| `Home/Bank/Panel` | The button list |
| `Home/Bank/Title/Text` | The version text under the logo |
| `<button>/ButtonText` | A list button's label |
| `<button>/ButtonText Highlighted Position` | Where the label slides to on hover (its X offset is the slide distance; 15 if missing) |
| `<button>/Shine` | The bar that fades in on hover |
| `Bank/Panel/Settings` | The Settings button (the clone source for new list buttons) |

- **Finding Home reliably:** a `MenuScreen` with `OpenOnStart == true`, `PreviousScreen == null` and name `Home`.

### Lobby panel
| Path | What it is |
|---|---|
| `LobbyInterface.Container/Entries` | The invite button and player slots |
| `LobbyInterface.LobbyTitle` | The title text ("Lobby (n/4)") |

### Framed screens (Settings, Load Game, New Game)
Children of the screen's root:
| Path | What it is |
|---|---|
| `Background` | Window background image |
| `Background/Image` | The pattern behind, 1000×842 in vanilla |
| `Title` | Title text |
| `TabsPanel` / `TabsPanel/TabsLayout` | Tab strip and its layout group |
| `Content` | Content area image |
| `Left bumper prompt` / `Right bumper prompt` | Gamepad LB/RB prompts |
| `<tab>/Background`, `<tab>/Checkmark`, `<tab>/Label` | Each tab; `Checkmark` shows when selected |

### Other pieces
| Name | What it is |
|---|---|
| `Import` (a `Button` with a TMP label) | The save slots' dark small button: the style source for Back |
| `BottomLeftMenuPivot` | The loose "Esc Back" prompt in the screen corner, on the menu's prompt canvas |

---

## 2. UI techniques → SuiteUI `UIBuilder` / `UITemplateRegistry` (§7)

### Cloning buttons
- **A cloned button keeps vanilla's scene wiring** (e.g. Settings → `SettingsScreen.Open`).
  `RemoveAllListeners` can't clear persistent calls; **replace the event**:
  `button.onClick = new Button.ButtonClickedEvent();` `[SOURCE]` `Menu/OptionsButton.cs`
- **Strip behaviours, keep the look:** destroy every `MonoBehaviour` that isn't a `Graphic` or
  `Selectable`. Removes scripts like the Import button's import logic. `[SOURCE]` `Menu/WindowFrame.cs`
- **Cloning the version text:** remove its version script, or it overwrites the text with the game
  version. `[SOURCE]` `Menu/MainMenu.cs`
- New list labels: set `enableWordWrapping = false`.

### Gamepad navigation
- New list buttons must be added to the list's `UIPanel.selectables`, right after the button they
  follow. **Remove first** (`selectables.Remove(added)`), since a clone may self-register on Awake;
  fall back to `AddSelectable` if the anchor isn't found. `[SOURCE]` `Menu/OptionsButton.cs`

### Layout
- **A GameObject can't hold two layout groups.** Destroy the `HorizontalLayoutGroup`
  (`DestroyImmediate`) before adding a `VerticalLayoutGroup`. `[SOURCE]` `Menu/WindowFrame.cs`
- Growing the menu list by one button: height += button height + group spacing, shifted by the pivot
  so the top edge stays put. If the group doesn't control child width, widen children manually.
  `[SOURCE]` `Menu/OptionsButton.cs`
- **Change anchors without moving a rect:** save `rect.size` and world `position`, set anchors, then
  restore `sizeDelta` and `position`. `[SOURCE]` `Menu/UiTools.cs` (`SetAnchorKeepRect`)
- **Measure text by its letters, not its box:** `ForceMeshUpdate()` then `textBounds`; fall back to the
  rect if the bounds are empty. Used to line one label up exactly after another. `[SOURCE]` `Menu/UiTools.cs` (`VisibleRect`)
- **Pointer over a rect, any canvas:** walk up to the root canvas; pass its `worldCamera` unless it's
  `ScreenSpaceOverlay` (then `null`) to `RectTransformUtility.RectangleContainsScreenPoint`. `[SOURCE]` `Menu/UiTools.cs` (`ContainsPointer`)
- **Screen pixels → canvas units:** divide by the root canvas's `scaleFactor`. `[SOURCE]` `Menu/WindowFrame.cs`

### Hover shine
- Vanilla's shine sprite has a **hard edge on one side** and fades to the other. A centred shine is two
  halves: left half flipped with `localScale.x = -1`, right half normal, hard edges meeting in the middle.
  `raycastTarget = false` on both. `[SOURCE]` `Menu/UiTools.cs` (`CreateShineHalf`)
- Art source: the first `Image` named `Shine` whose parent has a `Button`; copy its sprite, material,
  type and colour. Fallback when none exists: plain white at alpha 0.12. `[SOURCE]` `Menu/UiTools.cs` (`FindShineInScene`)
- Fade with a `CanvasGroup` (not interactable, no raycasts), `MoveTowards` at about 10 alpha/s using
  `unscaledDeltaTime`; place the shine as a sibling just before the text so it draws underneath.
  `[SOURCE]` `Menu/LobbyToggle.cs`
- `[PERFORMANCE]` the fade ran every frame; the new version should only tick while hovered or fading.

### Timing
- **Set up one frame after the scene initialises** (a coroutine with `yield return null`), so vanilla's
  `Awake`/`Start` have run. `[SOURCE]` `Menu/MainMenu.cs`, `Menu/LobbyToggle.cs`

---

## 3. Window frame → SuiteUI `MeowcowWindow` (§7)

`[SOURCE]` `Menu/WindowFrame.cs`. Restyled vanilla framed screens in place; left a screen vanilla if
any expected piece was missing.

- **Metrics that looked right** (canvas units, 1920×1080 reference): margin 20, title bar 70 (also the
  drag handle), footer 50, tab column 170 wide, tabs 34 tall with 4 spacing, a 30-unit row above the tabs
  for the bumper prompts (placed side by side, 30 apart). Default window 1100×700, capped to the canvas.
- **Pattern:** scale the original 1000×842 pattern to cover the bigger window, keeping its shape
  (`max(w/pw, h/ph, 1) × 1.05`).
- **Dragging:** mouse down on the title starts a drag; position is centre-anchored, clamped so the whole
  window stays on screen, rounded and saved on release. **Double-click the title (0.3 s) to recentre.**
- **Back button:** a clone of the dark `Import` button, 120×30 at the footer's left, text
  `<color=#9A9A9A>Esc</color>   Back`; closes whichever framed screen `IsOpen` via `MenuScreen.Close()`.
  If the style source is missing, no button (Escape still works).
- **Corner prompt:** hide `BottomLeftMenuPivot` while a framed window is open; restore it only if it
  was active before.
- **Colours:** recorded from vanilla at build time (background, content, title, tab, tab selected via
  `Checkmark`, tab text, Back text) so a style override can always fall back to vanilla.

---

## 4. Main menu → Main menu (§13)

`[SOURCE]` `Menu/MainMenu.cs`, `Menu/OptionsButton.cs`

- New list button: clone Settings, insert at Settings' sibling index + 1, then grow the list (§2).
- **Meowcow Suite label:** a clone of the version text, reparented straight under `Home` (so moving the
  logo or version never drags it), anchored bottom-left with `SetAnchorKeepRect`, left-aligned, no wrap,
  overflow on, placed 8 units after the version's last letter (measured with `VisibleRect`).
  Colour `#FF66CC`.
- A `Ready` event after Core's additions let other mods record the menu's "vanilla" state including them.
  Useful pattern for live element handles.

---

## 5. Lobby → Play mode (§6) and Main menu (§13)

`[SOURCE]` `Menu/LobbyToggle.cs`, `PlayMode.cs`

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

## 6. Settings files → SuiteSettings (§8)

`[SOURCE]` `Settings/SettingsFile.cs`, `Settings/SettingsModel.cs`, `Settings/ColorHex.cs`

- **Code defines, file supplies values:** each mod defines its settings in code (labels, types, limits,
  defaults); loading merges the file's values in and writes the result back, so new settings appear,
  removed ones disappear and descriptions stay current while the player's values are kept.
- **Validation:** invalid values fall back to the default with a warning; numbers are clamped to min/max;
  choices match case-insensitively and are stored in their canonical spelling; NaN and infinity rejected.
- **Atomic write:** write to a temp file, then `File.Move(tmp, path, overwrite: true)`, so a reader never
  sees half a file. Only write when the contents changed.
- **JSON options:** `WriteIndented = true`, `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` (keeps `+` and
  `'` readable in a local file); `Value` always written so hand-editors see it.
- **A null default meant "the mod decides"** (e.g. vanilla placement).
- **Dropped:** the old timestamp polling for hand edits while the game runs. The new design only
  supports hand-editing with the game closed (§2), so it isn't needed.
- **`ColorHex`:** parses `#RRGGBB` or `#RRGGBBAA` (`#` optional); six digits reports `hasAlpha = false`
  so callers can keep their own alpha; `Normalize` gives a leading `#` and upper case. Port as is.

---

## 7. Debug tools → SuiteUI (§7), or a debug area

`[SOURCE]` `Diagnostics/SceneDump.cs`

- **SceneDump** wrote every root canvas in the loaded scenes to a text file: path, active state,
  components, `RectTransform` layout, text (legacy and TMP, cut at 120 characters), `MenuScreen`
  settings, and each button's persistent `onClick` targets.
- Triggered by a MelonPreferences key (default F11, Ctrl required).
- **IL2CPP note:** generic `GetComponentInParent(bool)` fails under Il2CppInterop; walk up the parents
  by hand.
- `[PERFORMANCE]` heavy (walks every UI object); debug only, never automatic.