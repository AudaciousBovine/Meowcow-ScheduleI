# MeowcowCore — Design Plan

Status: **agreed design, not yet built.** This is the source for the `.aidevs` docs.
Everything here was decided in discussion; items still open are listed at the end.

---

## 1. What Core is

- The base every suite mod depends on, and a foundation other modders can build on.
- The only mod that does anything at launch: it sets up the main menu, then wakes other
  modules when they are needed.
- The owner of shared UI (templates, style tokens, windows), settings, saves and the
  game's lifecycle hooks.
- **Core does, Themes looks.** Core owns functionality; the separate themes mod owns
  colours, fonts and main menu positions.

---

## 2. Practices

### Code style (adopted from LilithsEngine)
- File-scoped namespaces, nullable enabled, explicit access modifiers.
- `StringComparison.Ordinal` comparisons.
- LilithsEngine naming suffixes (`*Service`, `*Registry`, `*Index`, `*Patch`, `*Builder`, ...).
- Comment tags:
  - `[CHANGED]` — why something changed from a previous version.
  - `[PERFORMANCE]` — cost notes and O-notation.
  - `[SOURCE]` — where an approach came from (another Schedule I mod, vanilla code, or general practice).

### Working rules
- **Build when needed.** No system is built ahead of a real use (event bus, value passing,
  contracts project are all deferred).
- **Zero cost until needed.** Modules do nothing until woken; no per-frame work without a reason.
- **Nothing outside the game is supported while it runs.** JSON is hand-edited only with the
  game closed; save folders are only changed through the game's and Core's own buttons.

### IL2CPP rules (learned from Mirror and vanilla)
- Prefer subscribing to game events over Harmony patches; IL2CPP inlining can skip patches.
- Keep converted delegates referenced so they aren't collected.
- Never let an exception escape into native code or a game event.
- Compare objects by `Pointer`.
- Defer layout work one frame after a UI is switched on.
- `FullRank` operators aren't callable through interop; compare fields directly.
- Clone vanilla UI pieces for style instead of building styling from scratch.

### Game rules (from vanilla `LoadManager` / `SaveManager`)
- **Subscribe to game statics at session start, never earlier.** `LoadManager.CleanUp()` runs
  at the start of every load and nulls `Player.onLocalPlayerSpawned`, `Property.onPropertyAcquired`,
  `Business.onOperation*`, `Customer.onCustomerUnlocked` and others.
- **Never write into a save folder.** `SaveManager.ClearBaseLevelOutdatedSaves` deletes anything
  the game didn't write itself.
- **Only the host saves.** `SaveManager.Save` returns early on clients.
- **Only Core hooks the game's lifecycle events.** `onLoadComplete` has no try/catch; one throwing
  listener skips the rest and hangs the load. On exit, a throw would skip vanilla's `SaveManager.Clean`.

---

## 3. Modules

### Interface
Each mod's `MelonMod` class implements `IMeowcowModule` and registers once in `OnInitializeMelon`.
Methods have empty defaults, so a module implements only what it uses.

```csharp
public interface IMeowcowModule
{
    string Id { get; }                       // e.g. "MeowcowMirror"
    WakeTrigger WakeOn { get; }              // which triggers wake this module
    void OnWake() { }                        // once, the first time it's needed
    void OnSessionStart(SessionInfo session) { }
    void OnSessionEnd() { }
    void OnQuit() { }
}
```
*(Shape for discussion; final names decided when built.)*

- **Version** comes from `MelonInfo`; never declared twice.
- **Installed checks:** `CoreModules.IsInstalled(id, minVersion)` for tie-ins.

### Wake-on-demand
| Trigger | Who wakes |
|---|---|
| Game launch | Core only (main menu setup) |
| Vanilla Settings | Core only |
| Meowcow Options | Every module |
| Load Game / Continue / New Game | Save-menu modules (today: LifeIsHard) |
| A game finishes loading | Gameplay modules |
| Another module needs it | That module, before the caller continues |

- A module stays awake once woken.
- **Dependencies wake each other**, so load order never matters and no two-phase start is needed.
  `[SOURCE]` general lazy-initialization practice.
- `[PERFORMANCE]` launch costs only Core's menu setup; unused modules never load anything.

### Tie-ins
- Core reports what's installed; tie-ins connect at session start.
- **Parked:** how values pass between mods. Decided when a real case (e.g. NickelAndDime reading
  the property mod's bill multipliers) shapes it.
- Two projects can't reference each other's code both ways; whatever is chosen must respect that.

---

## 4. Lifecycle — `CoreLifecycle`

| Phase | Trigger |
|---|---|
| Load | `OnInitializeMelon` |
| Menu | `"Menu"` scene loaded |
| Session start | `LoadManager.onLoadComplete` in the `"Main"` scene |
| Session end | `LoadManager.onPreSceneChange` while a session is active |
| Quit | MelonLoader `OnApplicationQuit` |

- `LoadManager` is a `PersistentSingleton`: hooked once at startup.
- **Tutorial:** dormant. Session start only fires in `"Main"`.
- **Session start guarantees:** the save is loaded, `Player.Local` exists and `PlayerLoaded` is true
  (true for both `StartGame` and `LoadAsClient`). Core keeps a null check that logs a warning.
- **Core's own "in session" flag is required:** `onPreSceneChange` also fires when going *into* a
  game and at the start of a client join (before authentication); a failed join fires it with no session.
- **Don't use `LoadManager.IsGameLoaded` during session start:** it flips true 1–2 s after
  `onLoadComplete`, while the loading screen is still up.
- **Isolation:** Core holds the single listener and calls each module inside its own try/catch.
  One broken mod can never hang a load or skip vanilla cleanup.
  `[SOURCE]` vanilla `LoadManager`; LilithsEngine event bus pattern.
- Core's own work (template capture, cleanup) runs before any module's.
- A module registering after a session started is called immediately.
- `[PERFORMANCE]` session start runs inside the game's load, behind the loading screen. Modules keep
  it light and push heavy work (large catalogs) into a coroutine over the next frames.
- Exit to menu waits for the player's save before `onPreSceneChange`, so session end can assume
  the vanilla save is done.

---

## 5. Foundation

### `MeowcowLogger`
- One per mod, wrapping its MelonLoader logger.
- Debug level behind a MelonPreferences toggle; optional subsystem tags (`[Receipt]`, `[Catalog]`).
- `[PERFORMANCE]` check `Log.IsDebug` before building debug strings.

### `CorePathIndex`
- One root for the suite: `UserData/Meowcow/<ModName>/` per mod; Core's data in `UserData/Meowcow/Core/`.
- Mods ask Core for their folder; nobody builds paths by hand.

---

## 6. UI

### Registry parts
| Part | Role |
|---|---|
| `UIPathIndex` | The only place that knows where vanilla UI lives. A game update means fixing one file. |
| `UITemplateRegistry` | Captured vanilla templates (buttons, bars, backgrounds, sliders, toggles, fonts). |
| `UIStyle` | Semantic style tokens (`button.primary`, `tab.selected`, `text.error`, ...). |
| Live elements | Named handles to live UI (`mainmenu.header`, `mainmenu.version`, `mainmenu.lobby`, ...) for Themes to position. |
| `UIBuilder` | The only way mods create UI (`CreateButton`, `CreatePanel`, `CreateScrollbar`, ...). |

`[SOURCE]` LilithsEngine `LUIAssets`; Mirror's `VanillaOptions` single-place rule.

### Templates
- Captured the first time their scene loads, cloned into a hidden, **inactive**, persistent object
  with listeners stripped. Available in every scene afterwards; their scripts never run.
- `[PERFORMANCE]` lookups once per template per launch; building costs one clone; small fixed memory.

### Style tokens
- Semantic names, so a theme changes what a role looks like without breaking meaning.
- **Defaults read from vanilla** (e.g. Next's blue for selected tabs). Core alone looks like the game.
- **Mods register their own tokens with defaults** (Mirror: `mirror.cash`, `mirror.card`, `mirror.error`).
- The themes mod only writes overrides into Core's token set; it never touches UI directly.
- **Applied live:** `UIBuilder` records element + token bindings; a theme change re-applies only the
  bindings for changed tokens. Bindings for destroyed elements are cleared at scene change.
- `[PERFORMANCE]` zero per-frame cost; a theme change is O(elements using the changed tokens).

### Windows
- **One shared `MeowcowWindow`:** left tabs, top subtabs, Back at the bottom-left showing the
  keyboard/controller key, draggable, gamepad navigation built in. Used by Settings, Meowcow Options,
  Load Game and New Game; Mirror's creator frame reuses it.
- **Vanilla menus are restyled in place** (vanilla Settings): existing tabs and rows move into the
  shared window, keeping vanilla's components so its logic keeps working. `[SOURCE]` Mirror's `CreatorLayoutBuilder`.
- **New menus are built fresh** (Meowcow Options, the save menu).

### Resilience
- **Automatic fallback:** if Core can't find what it needs for a menu, that menu stays vanilla and a
  warning is logged. A game update never makes the game unusable.
- **Debug switch:** a hidden MelonPreferences setting skips all restyling, for troubleshooting conflicts.
- Improved UI is not a player-facing option; it is simply how Core works.

---

## 7. Settings

- **Declared in code** by each mod: key, type, default, min/max, label, description, subtab, scope.
  Core builds the Meowcow Options UI and the JSON from the declarations.
  `[SOURCE]` LilithsEngine schema-driven config editor.
- **Read once when a module wakes; written only by Core** when the player saves in Meowcow Options.
- **Hand edits only with the game closed.** Edits made while running are overwritten by the next save.
- **Bad values** (missing, out of range) are defaulted or clamped at wake, logged, and the corrected
  file is written straight back.
- Debug settings stay in MelonPreferences.
- `[PERFORMANCE]` one file read per module per launch; writes only on save.

### Scopes
| Scope | Meaning | Sync |
|---|---|---|
| **Host** (default) | Used only by logic running on the host (bill scheduling, cop counts) | Never |
| **Client-needed** | Used by code on the client's machine (prices when they open a shop) | On demand |
| **Personal** | Themes and other non-gameplay customization | Never |

- Defaulting to Host is the safe failure: nothing syncs that shouldn't.

### Multiplayer
- The host's gameplay settings win; personal settings are always each player's own.
- **On-demand sync through Steam lobby data:** the host publishes only non-default Client-needed
  values under `meowcow.*` keys (on lobby creation and whenever it saves them). Clients read them
  when a menu needs them. Reads are local, so no request/response system is needed.
- Members always hold the latest lobby data, so a host change applies at the client's next open;
  an already-open menu keeps the values it opened with.
- A client's own gameplay settings are never touched; Options shows the host's values read-only.
- **Version check:** members publish their installed Meowcow modules and versions
  (`SetLobbyMemberData`), so mismatches are flagged both ways.
  `[SOURCE]` vanilla `SteamLobbyService.OnLobbyEntered` version check.
- Lobby chat messages are not used: vanilla reads them into a 128-byte buffer.
- If data ever outgrows Steam's limits, Core splits it across keys. More robust syncing only if the suite needs it.

---

## 8. Saves

### Per-save data
- Lives in `UserData/Meowcow/Core/Saves/<slot folder>.json`, one section per module
  (LifeIsHard difficulty, NickelAndDime bills, notes, favourites, ...).
- **Keyed by save folder** (`SaveGame_2`).
- **Written only when the game saves** (hooked on `SaveManager.onSaveStart`), so mod data never gets
  ahead of the vanilla save. Host only.

### Save menu (replaces both vanilla slot screens)
- **Unlimited slots:** Core scans the saves folder itself, reads each save with the public
  `LoadManager.TryLoadSaveInfo`, and loads with `LoadManager.StartGame`. Vanilla's fixed 5-entry
  `SaveGames` array is never used.
- **Continue** loads the most recent save; **Load Game** (renamed vanilla Continue) opens the save menu.
- Layout, favourites, notes and buttons as in the existing save menu plans.

### New Game — Core's own step
Vanilla `SetupScreen.StartGame` indexes the 5-entry array and would crash past slot 5, so Core
repeats its steps and skips the array: `[SOURCE]` vanilla `SetupScreen.StartGame`.
1. Pick the first free slot number (fills gaps, else the next number). Host-only check, as vanilla.
2. Create and clear `SaveGame_<n>`, copy `StreamingAssets/DefaultSave` into it.
3. Write `Game.json` (organisation name, random seed, `GameSettings`).
4. Write `Metadata.json` (dates, versions, tutorial flag from the skip-intro toggle).
5. Clear any leftover extras for that slot; save-menu modules write their new-game settings
   (LifeIsHard's preset) — before the tutorial, while modules are otherwise dormant.
6. Read the save with `TryLoadSaveInfo` and call `StartGame`.

### Deleting, exporting, importing
- Deleted saves go to a backup folder keeping the last 5. The slot's extras move into the backup
  with the save, so restoring it brings LifeIsHard's preset and the rest back; they're removed when
  the backup rotates out.
- Core's Export and Import carry the extras inside the zip; a vanilla-exported save starts with defaults.
- Each scan removes extras that have no matching save.
- Saves changed outside the game aren't supported (Schedule I has its own import/export).

---

## 9. Core and Themes

| Core (functionality) | Themes (look) |
|---|---|
| Windows, tabs, navigation, sizes and layouts | Colours and fonts (style token overrides) |
| Save menu, Continue, Load Game, New Game | Main menu element positions, anchors, scale, hide toggles |
| Meowcow Options and settings | Shareable presets (Vanilla / Meowcow / Custom) |
| Solo/Multiplayer play-mode state (lobby label click toggles it) | |
| UI registry, live element handles | |

---

## 10. Deferred

- **Event bus** — until something needs it. (Lifecycle isolation is a plain callback list, not a bus.)
- **Passing values between mods** — until a real tie-in shapes it.
- **Shared contracts project** — not needed; shared types live in Core.
- **Live multiplayer settings sync** — only if on-demand reads stop being enough.

---

## 11. Open items

- **Steam lobby data limits:** confirm per-key and per-lobby sizes in the `ISteamMatchmaking`
  API reference (believed ~255-character keys and ~8 KB total; unverified).
- **`GameSettings`:** check whether vanilla new-game options live there for LifeIsHard to set directly.
- **Steam callbacks under IL2CPP** (`LobbyDataUpdate_t`) — only needed if live sync is ever built.
