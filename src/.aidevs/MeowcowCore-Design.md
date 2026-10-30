# MeowcowCore — Design

Status: **agreed design.** Supersedes `MeowcowCore-DesignPlan.md`.
Built and tested in-game: **Foundation** (`MeowcowLogger`, `CorePathIndex`, `CoreLifecycle`, `CoreMod`),
the first part of **ModuleApi** (`IMeowcowModule`, `WakeTrigger`, `SessionInfo`, `SuiteModules`), the first
part of **SuiteUI** (`UIPathIndex`, `UIBuilder`, `MeowcowWindow`, `WindowTab`, `WindowPage`), the first part
of **SuiteSettingsManager** (Host and Personal settings, the Meowcow Settings menu), the **main menu**
additions, slices 1–2 of the **vanilla Settings rework** (§9), the **template registry** and
**`InputPrompts`** (§7). Everything else is design only.
Findings from the pre-rebuild code that aren't built yet live in `MeowcowCore-Findings.md`.

---

## 1. What Core is

- The base every suite mod depends on, and a foundation other modders can build on.
- The only mod that does anything at launch: it sets up the main menu, then wakes other modules
  when they are needed.
- Owns shared UI, settings, keybinds, saves, play mode and the game's lifecycle hooks.
- **Core does, Themes looks.** Core owns functionality; the themes mod owns colours, fonts and
  main menu positions.

---

## 2. Practices

### Code style
- File-scoped namespaces, nullable enabled (**warnings only**), explicit access modifiers,
  `LangVersion` pinned to 10, implicit usings off.
- `StringComparison.Ordinal`; LilithsEngine naming suffixes (`*Service`, `*Registry`, `*Index`, `*Patch`, ...).
- Comment tags: `[CHANGED]` (why something changed), `[PERFORMANCE]` (cost, O-notation),
  `[SOURCE]` (where an approach came from: another mod, vanilla code, or general practice).

### Working rules
- **Discuss, decide, then write.** Nothing is written until explicitly approved; one small piece at a time.
- **Ask before creating any new file or folder**, with the reasoning. Prefer extending an existing file
  whose purpose fits (the intro skip went into `VanillaSkips`, not its own class).
- **Build when needed.** No system ahead of a real use.
- **Zero cost until needed.** Modules do nothing until woken; no per-frame work without a reason.
- **Nothing outside the game is supported while it runs.** JSON is hand-edited only with the game
  closed; save folders are only changed through Core's own menu.
- **Session end is cleanup only.** Anything that must survive is written when it changes or at a save,
  never at session end or quit. Unsaved data is dropped like vanilla, and every launch starts clean.

### IL2CPP rules
- Prefer subscribing to game events over Harmony patches (inlining can skip patches).
- Keep converted delegates referenced; never let an exception escape into native code or a game event.
- Compare objects by `Pointer`; defer layout one frame after a UI is switched on.
- `FullRank` operators aren't callable through interop.
- Clone vanilla UI pieces for style rather than styling from scratch.

### Game rules (from vanilla `LoadManager` / `SaveManager`)
- **Subscribe to game statics at session start, never earlier.** `LoadManager.CleanUp()` runs at the
  start of every load and nulls `Player.onLocalPlayerSpawned`, `Property.onPropertyAcquired`, and others.
- **Never write into the game's own save folder.** `SaveManager.ClearBaseLevelOutdatedSaves` deletes
  any file the game didn't write.
- **Only the host saves.** `SaveManager.Save` returns early on clients.
- **Only Core hooks the game's lifecycle events.** `onLoadComplete` has no try/catch; one throwing
  listener would hang the load.
- **Tie-ins ask for other modules at wake or session start and keep the result**, never per frame.
- **`GameInput.exitListeners` is cleared on `onPreSceneChange`.** Core's windows re-register their Back
  handling each session.
- **Anything cloned from vanilla must have its state reset, not just its scripts removed.** A clone
  copies the source's *current* state: after vanilla Settings had been opened, its first tab was "on"
  and unclickable, and every tab cloned from it started unclickable.
- **Don't borrow vanilla objects: hide them and show our own clone.** Hiding is one switch to undo;
  a borrowed object must be put back exactly, and can be destroyed with our window. (Vanilla Settings rows
  are moved deliberately and permanently, never put back: not borrowing.)
- **Prefer clones and vanilla's own pieces over Harmony patches.** A Back prompt that needed a patch became
  two vanilla pieces side by side, each doing what vanilla made it for.
- **`OnSceneWasLoaded` arrives before the scene's first frame is drawn; `OnSceneWasInitialized` after.**
  Anything that must act before the player sees it (Skip Disclaimer) uses the first.
- **Vanilla's black overlay always fades in**, even when opened instantly. For an instant cover, Core
  shows its own black canvas above everything, and removes it once vanilla's overlay is fully up.
- **A switched-off canvas has no size.** In a save, the pause menu's canvas is off until the game is
  paused: anything measured then is zero. Build or measure UI only once its canvas is on (the in-game
  Settings is built on its first open; windows re-fit on every open).
- **UI positions: lessons from the Settings rework.**
  - A row's own coordinates are measured from its **pivot, usually its centre**, not its left edge.
    Measure from `rect.xMin`.
  - A page only lays out its rows **when it's shown**, after any setup code runs; rows can still resize
    or move then. Align within the row, and pin moved parts to the row's left.
  - Some vanilla layout groups are **disabled** (the keyboard binding rows): only an enabled one positions
    anything.
  - Transparent graphics are **click areas**, not visible edges (the toggle's is wider than its circle).
  - When guessing fails twice, **log the numbers**: a temporary diagnostic found the pivot issue at once.
- **Build menu UI on scene Initialized, never Loaded.** Exiting a game loads the Menu scene three
  times; on Loaded, two of those copies are about to be destroyed (§4).
- **`IntroManager` subscribes to `CharacterCreator.OnComplete` and never unsubscribes.** Any later
  `Done()` would re-run the end of the intro unless the listeners are swapped out (§11).

---

## 3. Project setup and folders

- `Directory.Build.props`: `net6.0`, `LangVersion 10`, `Nullable enable`, `ImplicitUsings disable`,
  game/MelonLoader references with `Private="false"`, `GamePaths.props` per machine (gitignored).

```
MeowcowCore/
  CoreMod.cs              entry point
  Foundation/             MeowcowLogger, CorePathIndex, CoreLifecycle
  ModuleApi/              IMeowcowModule, WakeTrigger, SessionInfo, SuiteModules
  SuiteUI/                UIPathIndex, UITemplateRegistry, UIStyle, UIBuilder, MeowcowWindow
  Menus/                  how vanilla menus are changed: MainMenu, later SaveMenu, SettingsMenu
  SuiteSettingsManager/   the settings system: declarations, store, scopes, lobby sync, keybind registry
  SuiteSettings/          Core's own settings and what they do: VanillaSkips, later the input split
  SuiteSaveManager/       the save system: playthroughs, snapshots, new game, import
  Patches/                Harmony patches (*Patch)
```
- `Suite` prefix marks systems that parallel a vanilla one, to avoid confusion.
- Namespaces follow folders. Folders are created only when their first file is written.
- **`Menus/` holds the screens; the systems hold the logic.** "How does this menu look and behave" is in
  `Menus/` (e.g. the save menu's screens); "what does it do with data" is in the system (e.g.
  `SuiteSaveManager` keeps playthroughs and file work). `SuiteUI` is the toolkit both use.
- `src/MeowcowTestModule/`: dev-only module that logs every call and can fail on purpose (waking,
  retries, isolation). Never shipped; deleted once Mirror runs on the new Core.

---

## 4. Foundation

### `MeowcowLogger` — built
- One per mod, created from its MelonLoader logger: `new MeowcowLogger(LoggerInstance)`.
- Levels: Info, Warning, Error (with an exception overload), Debug.
- **One suite-wide debug toggle** (MelonPreferences, set by Core).
- **Tags** per class: `Log.Tagged("Receipt")` → `[Receipt] ...`; tags can nest.
- **Zero-cost debug lines:** `Log.Debug($"...")` uses a C# 10 interpolated string handler, so the
  string is never built when debug is off. `[SOURCE]` general .NET 6 practice.

### `CorePathIndex` — built
- The only place the suite builds paths. Root: `UserData/Meowcow/`; layout in §10.
- **Builds paths only: never chooses names, never touches the disk.** Writers call `Ensure(folder)`
  first. Names (playthrough folders, duplicate numbering, snapshot folders) are `SuiteSaveManager`'s job.
- Core's folder is `MeowcowCore/`, named by its module Id like every other mod's.
- **Save folders share one shape** (`Game/` + `modules.json`), so `GameDir(saveDir)` and
  `ModulesFile(saveDir)` work for `Current/` and every snapshot alike.
- `CleanName` (public) is path safety, not naming: invalid characters → `_`, trailing dots and spaces
  trimmed (Windows drops them), reserved device names (`CON`, `NUL`, `COM1`, ...) get `_` appended,
  empty results rejected.
- The game's own paths are read where used, not here.
- Note for outside modders: a module Id of `Saves` or `DeletedSaves` would collide with those folders.

### `CoreLifecycle` — built
| Phase | Trigger |
|---|---|
| Load | `OnInitializeMelon` |
| Menu | `"Menu"` scene initialised, once per real load |
| Session start | `LoadManager.onLoadComplete` in the `"Main"` scene |
| Session end | `LoadManager.onPreSceneChange` while a session is active, or the Menu phase arriving while one still is |

- **No Quit phase.** Nothing is written at quit and every launch starts clean (§2).
- `LoadManager` is a `PersistentSingleton`: hooked once, on the first Menu scene (it may not exist at
  launch); retried on the next Menu if missing.
- **Tutorial: dormant.** Session start only fires in `"Main"`.
- At session start the save is loaded, `Player.Local` exists and `PlayerLoaded` is true
  (both `StartGame` and `LoadAsClient`). Core keeps a null check that logs a warning.
- **Core's own "in session" flag is required** (`IsInSession`, public): `onPreSceneChange` also fires
  going *into* a game and at the start of a client join.
- Don't use `IsGameLoaded` during session start; it flips 1–2 s later.
- **Isolation:** Core holds the single listener and calls each module in its own try/catch.
  `[SOURCE]` vanilla `LoadManager`; LilithsEngine event bus pattern.
- `[PERFORMANCE]` session start runs inside the load; heavy work goes into a coroutine.

**Exiting a game loads the Menu scene three times**, all in one frame:
- FishNet's `DefaultScene` loads its offline scene (`Menu`) when the server stops, again when the client
  stops (no longer a server by then), plus vanilla's own `LoadSceneAsync("Menu")`. Unity only switches
  scenes next frame, so `DefaultScene`'s "already in Menu" guard doesn't catch the repeats.
- Each load is a new scene instance (new handle); only the last survives. By Initialized, all events
  report the survivor, so **the Menu phase skips a repeated handle**.
- A client leaving should show two loads (its server never started). Untested.
- `[SOURCE]` FishNet `DefaultScene`; vanilla `LoadManager.ExitToMenu`.

**Exit timing** (vanilla `ExitToMenu` coroutine): clients first ask the host to save their player data
(up to 3 s), then a 1.25 s wait, then `onPreSceneChange`, then the connections stop, then the Menu load,
then `onSceneChangeDone`. So session-end cleanup runs while the game is still intact.
- `onSceneChangeDone` fires once vanilla's Menu load finishes: a vanilla-sourced alternative for the
  Menu phase if the handle check ever proves fragile.

**Connection drops:** FishNet loads Menu on *any* connection stop, so a drop can reach the menu before
vanilla's exit fires `onPreSceneChange`. The Menu phase ends a still-open session first, so Session end
always comes before Menu; the late `onPreSceneChange` is then ignored. Untested (needs co-op).

### `CoreMod` — built
- Thin entry point: receives MelonLoader's callbacks and hands them to Foundation; no logic, nothing
  per frame. Version `0.1.0` for the rebuild.
- Creates the suite-wide debug toggle: MelonPreferences category `MeowcowCore`, entry `DebugLogging`,
  read once at launch.

---

## 5. ModuleApi

### Contract (shape)
```csharp
public interface IMeowcowModule
{
    WakeTrigger WakeOn { get; }                    // None, SaveMenu, Session (combinable flags)

    void OnWake() { }                              // once, before anything else          — built
    void OnSessionStart(SessionInfo session) { }   // every awake module                  — built
    void OnSessionEnd() { }                        // pairs with OnSessionStart           — built
    void OnBecameCoop() { }                        // once, when a playthrough becomes co-op — with play mode
    object? OnSaving() => null;                    // progress data, asked at each save   — with save management
}
```
- Default bodies: a module writes only the calls it uses. Core always calls through the interface.
- Adding a call with a default body (or a property to `SessionInfo`) doesn't break existing modules,
  so each part arrives with the system that needs it.

### Identity and registration
- Name and version come from `MelonInfo`; nothing is declared twice.
- `SuiteModules.Register(this)` in `OnInitializeMelon`. Generic constraint
  (`where T : MelonMod, IMeowcowModule`), so "must be both" is checked at build time.
- **Registration doesn't depend on Core having started** (MelonLoader doesn't promise Core initialises
  first): it only stores the module, and logs through the module's own logger, so its errors appear
  under its name. A duplicate Id is rejected. Core logs the registered list at the first menu.
- The `MelonInfo` name is the module Id: it names the settings folder, save data keys and log prefix.
  A mod's name is effectively permanent once released.

### Waking
| Trigger | Wakes |
|---|---|
| Game launch | Core only |
| Vanilla Settings | Core only |
| Meowcow Settings | Every module |
| `SaveMenu` | Load Game / Continue / New Game opens |
| `Session` | A game finishes loading |
| Another module asks | That module, before the caller continues |

- Modules stay awake once woken. Dependencies wake each other, so load order never matters.
- **A failed wake leaves the module asleep;** the next real event retries. Each error log says
  what triggered the attempt, e.g. `(attempt 3, triggered by: requested by MeowcowLifeIsHard)`.
- While asleep, a module gets no calls, and tie-ins asking for it are told it isn't available.
- `[PERFORMANCE]` triggers are flag checks; launch costs only Core's menu setup.

### Calls
| Call | When | Who |
|---|---|---|
| `OnWake` | First time needed | That module, exactly once |
| `OnSessionStart` | A game finishes loading | Every awake module |
| `OnSessionEnd` | Leaving the game | Every module that got `OnSessionStart`, even if it threw (marked before the call, so it can clean up) |
| `OnBecameCoop` | First time anyone joins the playthrough | Every awake module |

- Built so far: waking on `Session`, session start and end. Waking from Meowcow Settings, `SaveMenu`
  and "another module asks" arrive with those systems.

### `SessionInfo` (fixed for the session)
| Value | Meaning | On clients |
|---|---|---|
| `IsHost` | This machine runs and saves the game (FishNet `InstanceFinder.IsServer`) — built | `false` |
| `Playthrough` | Playthrough folder name | `null` |
| `SaveMode` | Classic, Modern or Hardcore | `null` |

`Playthrough` and `SaveMode` arrive with save management. Only Core creates a `SessionInfo`.
Play mode isn't in `SessionInfo`; it's a live Core value (§6).

### Module save data
| Kind | Example | Stored in | Written |
|---|---|---|---|
| **Playthrough** | Difficulty, slot fields | `playthrough.json`, module's section | Anytime, through Core |
| **Progress** | Bills, anything that changes as you play | Each save point's `modules.json` | Core asks `OnSaving()` at each save |

- Progress data is handed back at session start (or nothing, for a new run or newly installed
  module). Exact hand-back shape decided when written.
- Each module has its own section keyed by Id. Host only.
- Data are plain C# classes serialised to JSON; no global enum converter (LilithsEngine lesson).
- Data from uninstalled modules is kept untouched.
- **Pull, not push:** Core asks at save time, so module data always matches the game's save.
  `[SOURCE]` vanilla `SaveManager` / `ISaveable` pattern.
- Playthrough data can't be rewound by loading an older snapshot (no reload exploits).

### Character creator provider
- Core can't reference Mirror (Mirror depends on Core), so Core offers a **provider slot** for the
  character creator. Mirror registers itself there when it wakes.
- When Core needs the creator (New Character, §11), it calls the registered provider, or falls back to
  vanilla's creator. Mirror adds a context for it: like `create` (starting outfit, no Cancel), but
  committed by Mirror instead of vanilla's `Done`.
- First real case of the parked cross-mod question, solved by a provider registered with Core.

---

## 6. Play mode (Solo / Co-op)

- **One way only: Solo → Co-op, permanent per playthrough.** Stored in `playthrough.json`, set the
  first time anyone joins. A co-op playthrough with one player left is still co-op.
- A live Core value modules can check; modules are told once via `OnBecameCoop()`.
- **Settings can declare separate solo and co-op values;** solo values apply until the playthrough
  becomes co-op.
- **Vanilla's death rule follows ours.** Vanilla decides solo vs co-op on death with
  `Lobby.IsInLobby`; Core overrides that in the death screen (§11), so a co-op playthrough loaded alone
  still takes the co-op route.
- The main menu lobby label toggles the intended mode for new sessions.

---

## 7. SuiteUI

### Parts
| Part | Role | Status |
|---|---|---|
| `UIPathIndex` | The only place that knows where vanilla UI lives (paths, and finding Home, the Settings screen, the Import button). | Built (main menu and window paths) |
| `UIBuilder` | The only way mods create UI: list buttons, text labels, buttons (look kept, scripts stripped), click handlers, layout helpers, debug hierarchy dump. | Built (what the main menu and window need) |
| `MeowcowWindow` | The shared window: left tabs, subtabs, scroll pages of rows, Back at bottom-left, draggable, gamepad navigation. | Built |
| `UITemplateRegistry` | Ready-made pieces, made once per run from vanilla's parts, kept across scenes. | Built (Back button) |
| `InputPrompts` | Key and button prompts for any action, following the input device. | Built |
| `UIStyle` | Semantic style tokens (`button.primary`, `tab.selected`, `text.error`, ...). | With Themes |
| Live elements | Named handles to live UI (`mainmenu.header`, `mainmenu.lobby`, ...) for Themes. | With Themes |

`[SOURCE]` LilithsEngine `LUIAssets`; Mirror's `VanillaOptions` single-place rule.

### Style tokens
- Defaults read from vanilla; mods register their own tokens with defaults; Themes only writes overrides.
- **Applied live:** `UIBuilder` records element + token bindings; a theme change re-applies only
  changed tokens. `[PERFORMANCE]` zero per-frame cost.

### Windows
- Used by vanilla Settings, Meowcow Settings, the save menu, and Mirror's creator.
- **Vanilla menus are restyled in place** (vanilla components kept). `[SOURCE]` Mirror's `CreatorLayoutBuilder`.
- **New menus are built fresh.**

### `UIBuilder` rules
- **Cloned buttons get a fresh click event:** a clone keeps vanilla's scene wiring, which
  `RemoveAllListeners` can't clear.
- **Click handlers are converted once per handler and cached** (IL2CPP rule), and every click runs in a
  try/catch, since it comes from native code.
- `UIBuilder` throws on missing pieces; the menu using it catches and falls back.

### `MeowcowWindow` — built
- **A clone of vanilla's Settings screen** (main menu for now; the pause menu later). Gives vanilla's
  look, open/close fade, Escape (returns to `PreviousScreen`), gamepad focus and LB/RB tab cycling.
- **Built safely:** cloned inside an inactive staging object, so nothing on the clone runs until
  `SettingsScreen` (a `MenuScreen` subclass) has been swapped for a plain `MenuScreen` with its
  behaviour fields copied. Then one activation runs `Awake` exactly as vanilla's screen does.
- **Checks** that the clone's `MonoState` and fade group are its own; otherwise not built (it could
  open vanilla Settings instead). The `MonoState`'s default parent is the scene's shared state machine,
  as intended.
- **Detach gamepad panels before destroying anything:** vanilla's `UIScreen` keeps a panel list and
  crashes on destroyed panels when opened. Removing must happen *before* destroying, since
  `RemovePanel`'s Unity `==` treats a destroyed panel as null.
- **Layout:** 1100×700 capped to the screen; title bar 70 (drag handle), footer 50, tabs 170 wide in a
  vertical list level with the content's top, LB/RB centred above the first tab, pages padded 10.
  Pattern scaled to cover. `[SOURCE]` pre-rebuild window frame.
- **Tabs:** `AddTab(label)` returns a `WindowTab`. Vanilla's `UITab` cycles by direction, so the strip
  is set to vertical (`reverseCycleDirection` so RB moves down). The window selects the tab strip on
  open (vanilla's `SettingsScreen.OnEnable` did this).
- **Subtabs:** `WindowTab.AddPage(label)` returns a `WindowPage`. With two or more pages, a horizontal
  strip (a copy of vanilla's tab strip taken before the main one turns vertical) runs along the top
  and cycles on **LT/RT** (vanilla's `Secondary` tab input), so LB/RB keep switching tabs.
- **Pages** are clones of a whole vanilla Settings page (`UIContentPanel` + scroll view), each added to
  the window's `UIScreen`: up/down moves between rows, the page scrolls to the selected row, and hidden
  pages are skipped. Dropdown item lists also have a panel and scroll view, so they're skipped by name.
- **Rows** (`AddToggle`, `AddSlider`, `AddChoice`, `AddText`) are clones of vanilla's rows, keeping
  vanilla's generic controls (`UIToggle`, `UISlider`, `TMP_Dropdown`), which do the work, gamepad
  included, and returning handles to update the shown value. Configured while hidden, so vanilla's
  `Awake` runs once on the finished row. `SuiteUI` knows nothing of settings: rows are generic.
- **Scripts are stripped by allow-list** (`UIBuilder.StripScripts`): graphics, Unity controls and layout,
  vanilla's generic controls, gamepad panels and sounds stay; anything else goes, including scripts
  we've never seen and EventTriggers.
- **Back** (140×40) comes from the template registry, the same in every scene; one shared static handler
  closes `MenuScreen.Current`. Without the piece: no button; Escape still works. Vanilla's corner prompt
  (the main menu's `BottomLeftMenuPivot`, or the pause menu's `InputPrompt (Back)`) is **hidden** while
  the window is open, and shown again on close if it was showing.
- **Dragging** by Unity pointer events on the title; kept on screen; position remembered for the run
  (saved with SuiteSettingsManager later). **Double-click recentres**, with our own detection: Unity counts
  releasing a drag as a click when one object handles both, so `clickCount` misfired.
- **Tab styling:** no gap between the tab column and the content panel. The selected left tab's highlight
  is vanilla's tab shape (rounded top, flat bottom) turned a quarter left, in the content panel's colour,
  so it's rounded on the left and joins the panel. Subtabs are **reversed**: unselected ones are shaded
  (25% black), the selected one is clear and flush on its page. Vanilla's tab colours come from a colour
  transition with a transparent resting colour, so subtabs drive their shading themselves.
- **Two-column rows** (`WindowPage.AlignRow`, every row, ours and vanilla's): the label in a column
  **160 wide, 10 in** from the row's left edge, right-aligned and wrapping; controls start **24** past it.
  One rule for every kind of row (a label's layout group is switched off). Controls are measured by what
  reads as their edge: a dropdown's box, a slider's track, a toggle's circle. Notes under a label (Auto
  Backup's) join the column, top-aligned. Constants at the top of `WindowPage`.
- **Label icons** (Seeded Mixing's info icon: a small graphic, not a control or text, at most 40×40) sit
  4 past the label's end, in the gap before the controls.
- **Re-fitted on every open:** the size check (default 1100×700, or less on a small screen) runs when built
  and again on open, and ignores a canvas size below 400×300 (an inactive canvas reports zero).
- Two ways to build: **clone** the Settings screen (Meowcow Settings) or **adopt** an existing one in place
  (vanilla Settings, §9). Clones are made from a hidden, untouched copy of vanilla's Settings screen taken
  before it's reorganised.
- Built on first open, rebuilt after a scene change. `[PERFORMANCE]` nothing per frame.

### `UITemplateRegistry` — built
- The suite's library of ready-made pieces, so every scene builds from the same ones. Pieces are made
  once per run at the main menu (`CaptureMissing` retries any missing piece at the next menu), kept in
  one **hidden, inactive holder that survives scene changes**, and cloned by windows (`Create`).
- **Simple pieces are built from vanilla's look; complex controls stay templates.** A button is mostly
  look and is rebuilt; vanilla's dropdowns, sliders and toggles are mostly behaviour, so they're cloned.
- **Back button:** the Import button's look (`UIBuilder.StripToLook`), a gamepad prompt and key-cap slot
  (`InputPrompts`) pointed at `GameInput.PrimaryExitAction` (what Escape and B trigger), and "Back" at
  vanilla's prompt-text size (from the corner prompt's `PromptLabel`), laid out in a row.

### `InputPrompts` — built
- A prompt for **any action** is two vanilla pieces side by side:
  - vanilla's `EmbeddedInputPromptUI`, **gamepad only** as vanilla uses it (B, Circle...), redrawn by
    vanilla on device changes; an empty binding id means "the current device's binding";
  - a **key-cap slot** holding vanilla's own rounded key cap (`InputPromptsManager.GetPromptImage`, the
    caps behind vanilla's corner prompts), shown only on keyboard. Vanilla's prompt data has no keyboard
    pictures, which is why its embedded prompts are gamepad-only.
- Each slot shows the key of the embedded prompt just before it, so it needs no settings of its own.
- `AddKeyCapSlot` when making a piece; windows call `Shown`/`Hidden`, and every prompt inside follows the
  device. The key is read from the live binding on open (cap rebuilt only if it changed), so rebinding is
  followed. **One** listener on `GameInput.OnInputDeviceChanged` for the whole run.
- `[PERFORMANCE]` a cap made only when a window opens with a changed key; the listener runs only on device
  changes.

### Resilience
- **Automatic fallback:** if Core can't build a menu, it stays vanilla and a warning is logged.
- **Hidden debug switch** (MelonPreferences) skips all restyling.
- Improved UI isn't a player option.

---

## 8. SuiteSettingsManager (Meowcow Settings)

- **Folders:** `SuiteSettingsManager` is the settings *system* every module uses; `SuiteSettings` holds
  **Core's own settings and their effects** (e.g. `VanillaSkips`), built with it, like any module would.
  Matches `SuiteSaveManager` for the save system.

- Named **Meowcow Settings**, matching vanilla's Settings.
- **Declared in code**, in `OnInitializeMelon`, beside `SuiteModules.Register`:
  ```csharp
  var general = MeowcowSettings.For(this).Subtab("General");
  Enabled = general.Toggle("enabled", "Enabled", true);
  Price   = general.Number("price", "Price", 100, min: 0, max: 1000, step: 10);
  ```
  Key, label, default, limits, description and subtab. Core builds the menu and JSON from the
  declarations. `[SOURCE]` LilithsEngine schema-driven editor.
- **Named `MeowcowSettings`**, not `SuiteSettingsManager`: a class named like its folder clashes with the
  namespace inside Core (`SuiteSettingsManager.For` resolves to the namespace). It also matches the menu.
- **Types built:** on/off (`ToggleSetting`), number with min/max/step and whole numbers
  (`NumberSetting`), choice (`ChoiceSetting`). Text, colour and keybinds come when needed.
- **Live values:** each setting has `Value` (read any time; free) and a `Changed` event, each handler
  isolated. **Loaded when declared, at launch**, since a module may read a value at any time.
  `[PERFORMANCE]` one small file read per module at launch.
- **Changes apply the moment they're made**, like vanilla's Settings. The file is written **half a
  second after the last change**, and at once when the menu closes and at each menu load: a slider
  drag or a held stick is one write. Only written if the contents changed; atomic (temp file + move).
- **The file is keyed by setting, not subtab** (`{ "enabled": { "Value", "Default", "Description",
  ...limits } }`), so moving a setting between subtabs keeps the player's value.
- **Validation:** bad values are corrected, logged and written back; numbers clamped (NaN rejected);
  choices matched case-insensitively and stored in their listed spelling. An unreadable file is kept
  as `settings.json.broken` before defaults are written.
- **The menu:** one tab per module (Core first, "No settings yet" when empty), one page per subtab,
  one row per setting, bound both ways (a value changed in code updates its row; rows of a closed
  window unhook themselves). Tab labels drop the "Meowcow" prefix and space the words.
- **Shown in vanilla's Settings only** (`.ShownInVanillaSettings()`, internal to Core): Core's switches
  for vanilla behaviour live in vanilla's Settings, placed by its layout table (`@core` rows, bound both
  ways via `Menus/SettingRows`), and aren't repeated in Meowcow Settings, which is kept for the mods'
  own settings.
- Built so far: Host and Personal scopes (neither syncs), so scopes aren't declared yet. Client-needed
  sync and solo/co-op values come with co-op and play mode.
- Hand edits only with the game closed. Debug settings stay in MelonPreferences.

### Scopes
| Scope | Meaning | Sync |
|---|---|---|
| **Host** (default) | Used only by host logic | Never |
| **Client-needed** | Used by code on the client (prices in a shop they open) | On demand |
| **Personal** | Themes, keybinds, non-gameplay customisation | Never |

### Multiplayer
- Host's gameplay settings win; personal settings are each player's own.
- **On-demand sync via Steam lobby data:** the host publishes only non-default Client-needed values
  under `meowcow.*` keys (on lobby creation and when saved). Clients read them when a menu needs them.
- A host change applies at the client's next open; open menus keep their values.
- Clients' own gameplay settings are untouched; shown read-only as "set by host".
- **Version check:** members publish installed modules and versions (`SetLobbyMemberData`).
  `[SOURCE]` vanilla `SteamLobbyService.OnLobbyEntered`.
- Lobby chat isn't used (128-byte buffer). Data over Steam's limits is split across keys.

---

## 9. Vanilla Settings rework

Vanilla's Settings is **rebuilt into the suite's layout**, in four slices:
1. **Built:** the new layout with vanilla's own rows (main menu).
2. **Built:** in-game (pause menu), with **General › Current Game**.
3. New Core settings (**built:** the vanilla skips), the keyboard/gamepad split, and a description panel
   with info icons.
4. The keybind rework, with two bindings per action.

### Layout
| Tab | Subtab | Rows |
|---|---|---|
| **General** | Main | Skip Disclaimer*, Always Skip Tutorial*, Always Skip New Game Intro*, Pause on focus lost, Auto Backup Saves |
| | Current Game (in a save only) | that save's Seeded Mixing, Console, View Commands |
| | Misc | Export Player.log, Export Player-prev.log |
| **Gameplay** | Interface | Interface Scale, Units, Gamepad Pointer Sensitivity |
| | Camera | Camera Bobbing, FOV, Mouse Camera Sensitivity, Mouse Invert Y, Gamepad Camera Sensitivity, Gamepad Invert Y* |
| **Graphics** | | Resolution, Active Display, Display Mode, V-Sync, Target Framerate, Quality, Anti-aliasing, SSAO, God Rays (+ Apply) |
| **Audio** | | all eight volume sliders |
| **Keybinds** | Mouse & Keyboard | Restore Default Bindings, Sprint Mode, Sneak Mode*, Bindings (two per action*) |
| | Gamepad | Restore Default Bindings, Sprint Mode*, Sneak Mode*, Bindings |

\* new (slices 3–4). "Camera Sensitivity" keeps "Camera": it isn't the menu cursor's.

### The vanilla skips (`SuiteSettings/VanillaSkips`) — built
Each uses vanilla's own flow; nothing is patched.
- **Skip Disclaimer:** as the menu scene loads (`OnSceneWasLoaded`, before the first frame), the disclaimer's
  text is switched off and its wait set to zero, so vanilla's own fade invokes `OnClose` and fades the
  black screen out: a short fade into the menu, with no text and no jitter. Vanilla skips it itself after
  the first menu of a launch (`Disclaimer.Shown`).
- **Always Skip Tutorial:** the New Game screen's tutorial toggle (vanilla's `SkipIntroToggle`, read only
  when the save is created) is set on and its row's contents hidden (`SetupScreen.Start` forces the
  container itself on), at each menu load and when the setting changes.
- **Always Skip New Game Intro**, and **holding vanilla's skip skips the whole intro** (not one segment):
  the intro is one animation with an event per segment. Core fast-forwards it one segment per frame (so
  each event fires), then to the end; vanilla carries on to the character creator, `MarkIntroCompleted`,
  `onIntroDone` and the save. An instant black screen of Core's own goes up the frame before vanilla's hold
  completes (read from its hold timer), and comes down once vanilla opens the character creator.
- **Skip Splash Screen was dropped:** the TVGS logo is Unity's own splash, which ends before MelonLoader
  starts any mod (`Menu` is the first scene).
- Seed Mixing and Console defaults moved to playthrough creation (Save Manager, §10).
- `[PERFORMANCE]` a few writes when the menu loads; one check per frame only while a new game's intro is
  starting or playing.

### Planned additions (from the Settings rework discussion)
- **Description panel** (slice 3): a fixed panel on the right of each page shows the description of the
  hovered info icon (mouse) or the selected row (gamepad/keyboard). Info icons, cloned from vanilla's,
  sit beside every option with a description; Auto Backup's note moves into the panel.
- **General › Commands** (slice 5), between Current Game and Misc, in both menus, replacing View Commands:
  the command list rebuilt as labels (from the game's command data if simple, else a hand-kept table),
  with **pinned commands** as buttons at the top, enabled only when **host or solo and console on**,
  console counting as off outside a session.
- **Play mode (§6):** Current Game shown only to host or solo, instead of vanilla's Blocker.
- **Save Manager (§10):** Seeded Mixing and Console become playthrough settings (set at creation or in
  playthrough settings, not changeable mid-game), retiring Current Game.
- **Keyboard and gamepad fully decoupled:** Sprint Mode, Sneak Mode and Invert Y each get a keyboard and a
  gamepad value; Core applies the matching one to vanilla when the input device changes (slice 3).
  Sneak Mode (hold/toggle for crouching) needs Core to add the toggle behaviour vanilla lacks.
- Controls is renamed **Keybinds**: camera settings moved to Gameplay.

### How it's built (slice 1)
- **Vanilla's real screen is reorganised in place** (`MeowcowWindow.AdoptSettings`), not cloned:
  - rows apply their change with `GetComponentInParent<SettingsScreen>()`, so they must stay under it;
  - **rows are moved, not copied**: each keeps its own script, which keeps applying vanilla's setting;
  - `SettingsScreen` stays, so Apply, the display confirmation popup and host-only rows keep working;
  - vanilla's one-time tab wiring is marked done (`_initialized`); its pages go into a **hidden holder,
    never destroyed**, so vanilla's references stay valid;
  - all templates are copies and every check runs first: a failure before the point of no return leaves
    the screen untouched.
- **One layout table** in `Menus/SettingsMenu` (vanilla page, row name, tab, subtab, new label; `*` for
  "the rest of the page"). A row renamed by a game update is a one-line fix; a missing row is skipped.
- Each moved row registers **exactly the gamepad controls it had** on vanilla's lists.
- **Keybinds touch-ups:** Restore Default becomes "Restore Default Bindings" on its own row at the top
  (20 above, 8 below); the "Bindings" header opens the bindings; vanilla's two-button mouse row is split
  into one row per button; "(Cannot be rebound)" goes on its own line.
- A **pristine copy** of vanilla's Settings screen is kept per scene; Meowcow Settings is cloned from it.
- Done once per menu load, before the player can open it. `[PERFORMANCE]` about 40 reparents.

### In a save (slice 2)
- `SettingsScreen_Ingame` (under the pause menu) has **the same pages and row names**, so the same layout
  table is used, plus the Game page.
- **Built on its first open**, not at session start: the pause menu's canvas is off until the game is
  paused, and rows measured then are zero-sized. Core waits for the screen's first open
  (`OnBecomeTopSibling`), reorganises it, then runs the window's open handling itself (`NotifyOpened`),
  since the window's own handler is only added during that open.
- **Current Game adopts vanilla's whole Game page** (`WindowTab.AddExistingPage`; a `#page` entry marked
  in-save only): its `GameSettingsWindow` script lives on the page, wires the toggles and shows the
  host-only Blocker in `Awake`/`Start`, and a hidden page's script never runs. Moving the page whole keeps
  it running; it's re-registered as a gamepad panel and its rows are aligned.
- `[PERFORMANCE]` the reorganising (about 40 reparents) happens on the first Settings open of a session.

### Still from the original plan
- **Keybind registry:** one place for every binding.
  - Holds vanilla bindings (mirrored from the live actions once vanilla `Settings` has applied them) and
    every suite binding modules declare.
  - **Each binding has a context** (e.g. On foot, Driving, Menus, Character creator). The same key can
    be used in different contexts; conflicts are flagged only within a context. Modules can add contexts.
  - On a conflict, Core asks instead of silently clearing: "F is used by Interact (On foot). Replace it?"
  - The Keybinds tab is built from it (slice 4), grouped by context, keyboard and gamepad on their subtabs.
  - **Vanilla stays the source of truth** for its own bindings; the mirror is re-read every launch.
  - `[PERFORMANCE]` built once at launch; conflict checks only when a key changes.

### How vanilla input works (research)
- One `PlayerInput` in send-messages mode (`OnJump`, `OnCrouch`, ...); action names match
  `GameInput.ButtonCode`. **One action map, `"Generic"`** — vanilla has no contexts, so Core keeps a
  small mapping table for vanilla actions (e.g. `Vehicle*` → Driving, `Skateboard*` → Skateboard,
  `UI*` → Menus, the rest → On foot). One place to fix after a game update.
- **Exposed keys** are `Settings.PCRebindings` / `Settings.GamepadRebindings`; anything else in the map
  is unexposed.
- **Storage:** all vanilla settings live in PlayerPrefs. Keybinds are one string, `BindingOverrides`
  (`SaveBindingOverridesAsJson`). Defaults are captured at startup (`_defaultBindingOverrides`).
- **Saving vanilla binds:** `Settings.WriteInputSettings(InputSettings)`, then
  `ApplyInputSettings(ReadInputSettings())`, which fires `onInputsApplied`. Core uses the same path.
  `[SOURCE]` vanilla `Keybinder`.
- Vanilla's `RebindActionUI` (Unity's Rebinding UI sample) **clears a conflicting key on every other
  action**, ignoring contexts, and excludes mouse and gamepad. So **Core builds its own keybind rows**:
  styled from vanilla's row via `UIBuilder`, rebinding with the same `PerformInteractiveRebinding`, with
  Core's per-context conflict check.
- Input System is 1.4 or later (`OneModifierComposite` exists), so modifier combos use current names.
- Back/Escape: Core's windows use `GameInput.RegisterExitListener(..., priority)`. `[SOURCE]` vanilla
  `GameInput`.

### What can be bound
- **Vanilla actions:** any single input (keys, mouse buttons, gamepad buttons) through binding
  overrides, saved the vanilla way. Combos on vanilla actions are a possible later feature: they change
  an action's shape, and vanilla's handlers toggle pressed/released per callback.
- **Suite actions:** Core's **own action map, built in code**. Single inputs now; modifier combos and
  interactions (`press`, `hold`, `multiTap`) later. The registry stores path + modifiers + interaction
  from day one, so adding them changes no file format.
- **Suite keybinds:** Quick save **F5**, Quick load **F9**; stored in Core's settings, not PlayerPrefs.

---

## 10. SuiteSaveManager

### Layout
```
UserData/Meowcow/
  Saves/
    Business Name/                 one playthrough
      playthrough.json             pin, players, save mode, co-op flag, module playthrough data
      Current/                     latest save of any kind
        Game/                      what the game loads and saves
        modules.json               module progress data
      Snapshots/                   Modern only
        Auto/  Panel/  Quick/  Manual - <name>/   (each: Game/ + modules.json; names chosen by SuiteSaveManager)
  DeletedSaves/                    last 5 deleted playthroughs, data included
  MeowcowCore/                     Core's own data (settings, keybind mirror, vanilla-imports.json)
  <ModuleId>/settings.json
```
- The suite's own saves folder; vanilla's folders are never written to. Not split by Steam account.
- A full uninstall or clean reinstall can remove `UserData`; players should export first.

### Playthrough folders
- Named after the business at New Game; duplicates become `Name - 2`, `Name - 3`.
- Invalid characters replaced. Renamed only from the save menu; a failed rename keeps the old name.
- Renaming the company in-game doesn't affect the folder.

### Save modes (chosen at New Game)
| | Classic | Modern | Hardcore |
|---|---|---|---|
| Saves | Sleep and panel → `Current/` | Same, plus snapshots | Sleep and panel → `Current/` |
| Quick / manual saves | No | Yes | No |
| Death (solo) | Vanilla | Vanilla | Playthrough moved to `DeletedSaves/` |
| Death (co-op) | Vanilla | Vanilla | New Character: inventory, cash and clothing wiped, new look, respawn at the RV. Shared bank untouched. Save kept (§11). |

- Modes only change towards more forgiving: **Hardcore → Classic → Modern.** The starting mode isn't stored.

### Snapshots (Modern)
| Snapshot | Created by | Kept |
|---|---|---|
| Auto | Sleeping | 1 |
| Panel | A property's save panel | 1 |
| Quick | F5 | 1 |
| Manual | Player, named | Unlimited |

- The game always saves to `Current/` through vanilla code; after `SaveManager.onSaveComplete`, Core
  copies it into the right snapshot. `onSaveComplete` fires after vanilla's folder cleanup, which only
  touches the top level of `Game/`, so `modules.json` beside it is safe.

**Every caller of `SaveManager.Save()`** (research): `SleepController` (sleep routine), `SavePoint.Save()`
(property panel), `IntroManager` (first save after the intro), `Console.Save.Execute` (console command).
`Save(string)` is only called by `Save()`.

**Classifying a save at `onSaveStart`** (fires synchronously inside the `Save()` call):
| Check, in order | Filed as |
|---|---|
| Core started it (F5, Manual) | Quick / Manual |
| Panel flag set | Panel |
| Console flag set | Manual, named with date and time |
| `SleepController.Instance.IsSleepInProgress` | Auto |
| Anything else (intro save, unknown callers) | `Current/` only, logged if unknown |

- Sleep needs no patch: `Save()` runs while the sleep phase is still active. `[SOURCE]` vanilla `SleepController`.
- Panel: prefix sets / postfix clears a flag on `SavePoint.Interacted()` (the private one-line `Save()`
  is likely inlined; `Interacted` is called through the interaction event). Cleared even if the panel's
  cooldown blocks the save.
- Console: the same flag pattern on `Console.Save.Execute`.
- **F5 has no cooldown;** it waits for a save in progress ("Saving..."). Side effect, vanilla's rule:
  any save resets the panel's 60-second cooldown.
- Copies run off the main thread. `[PERFORMANCE]`
- **F9 loads only the Quick snapshot,** never anything else; "No quick save" if none exists.
  Other snapshots load only from the save menu.
- Quick load: refuse while saving, copy `Quick/` over `Current/`, reload like vanilla
  `LoadLastSave()`. `[SOURCE]` vanilla `LoadManager.LoadLastSave`.
- **Co-op quick load:** everyone sees a loading screen and returns together. A full in-place restore
  isn't feasible. By default a client leaves the lobby when the host's server stops
  (`Player.ClientConnectionStateChanged` → `HostExitedGame` → `ExitToMenu(preventLeaveLobby: false)`), so:
  - the host sets the lobby key `meowcow.quickload` before reloading;
  - on clients, a prefix on `Player.RpcLogic___HostExitedGame` sees the key and calls `ExitToMenu`
    **without leaving the lobby or showing the popup**;
  - vanilla's `ExitToMenu` then joins right away or waits for the host's `ready` message.
  `[SOURCE]` vanilla `ExitToMenu` / `SteamLobbyService`.

### New Game (Core's own step)
Vanilla `SetupScreen.StartGame` indexes the 5-entry `SaveGames` array, so Core repeats its steps
without it. `[SOURCE]` vanilla `SetupScreen.StartGame`.
1. Host check. Create the playthrough folder (naming rules above).
2. Copy `StreamingAssets/DefaultSave` into `Current/Game/`.
3. Write `Game.json` (organisation name, seed, `GameSettings`) and `Metadata.json` (dates, versions, tutorial flag).
4. Write `playthrough.json` (save mode); save-menu modules write their new-game data (LifeIsHard difficulty).
5. Read with `LoadManager.TryLoadSaveInfo` and load with `StartGame`.

### Save menu (replaces vanilla's slot screens)
- **Continue** loads the most recent playthrough's `Current/`; **Load Game** opens the menu.
- Playthroughs are listed by last played, pinned first. **Clicking one expands it** to show its snapshots.
- **Slot shows:** slot name, organisation name, net worth, last played, date created, game version,
  save mode, solo/co-op with players' Steam avatars (recorded each save), pin, total size, and fields
  modules add (LifeIsHard's Difficulty, only when installed).
- **Slot buttons:** Pin, Rename, Export, Delete.
- **Bottom bar:** Import, Import vanilla saves.
- Size is summed during the scan and after a save or delete. `[PERFORMANCE]`

### Vanilla saves
- When Load Game opens and unimported vanilla saves exist, a prompt offers to import them (copied,
  originals untouched). Declined saves are never asked about again; new vanilla saves are.
- Imports and declines recorded in `MeowcowCore/vanilla-imports.json` (vanilla slot + creation date).
- Copying runs off the main thread.

### Delete, export, import
- Deleted playthroughs go to `DeletedSaves/` (last 5), module data included.
- Export and Import carry all suite data.

---

## 11. Death and respawn

### How vanilla death works (research)
- `PlayerHealth` → `Die()` → `onDie` → `Player.OnDied` → on the dying player's machine,
  `DeathScreen.Open()`.
- `DeathScreen`: not in a lobby → **Load Save** (`ExitToMenu(ActiveSaveInfo)`, reloads); in a lobby →
  **Respawn** (hospital bill, revive at the medical centre, or a property during curfew).

### Core's death screen
A postfix on `DeathScreen.Open()` sets the title and buttons (`respawnButton` / `loadSaveButton` are
public fields) by **Core's play mode**, not the lobby.

| Situation | Title | Button |
|---|---|---|
| Solo, Classic / Modern | You Died | Load Save (vanilla; reloads `Current/Game/`) |
| Co-op, Classic / Modern | You Died | Respawn (vanilla hospital) |
| Solo, Hardcore | You Died Forever | Return to menu; once the menu loads, the playthrough moves to `DeletedSaves/` |
| Co-op, Hardcore | You Died Forever | **New Character** |

### New Character (co-op Hardcore)
Runs on the **dying player's own client** (inventory is owner-side: `PlayerInventory` is local and
loading only applies `if IsOwner`). A prefix on `DeathScreen.RespawnClicked` (a button listener)
replaces vanilla's routine:
1. Wipe inventory, pocket cash and clothing slots; free the player if arrested. Shared bank untouched.
2. Open the character creator through the provider slot (Mirror if installed, else vanilla). Vanilla's
   `Open()` already resets to the starting appearance and outfit.
3. **Without Mirror:** swap `CharacterCreator.OnComplete`'s listeners for Core's handler, keeping the
   originals aside; no patch on `Done()` (small enough to be inlined). Restore them afterwards so a
   later intro (a friend joining for the first time) still works.
4. Core's handler keeps only the useful parts of `IntroManager.CharacterCreationDone`: black overlay,
   `SetAppearance_Server(output.Appearance)`, each `GetClothingInstances()` →
   `Player.Local.Clothing.InsertClothingItem(...)`, and the camera restore (vanilla's `Close()` doesn't
   restore it). Skipped: intro teleport, `MarkIntroCompleted_Server`, intro events, immediate save.
5. Revive at the **RV's `InteriorSpawnPoint`**, wrecked or not (tested: you can walk out of the wreck).
   Error-only fallback if the RV can't be found: the medical centre, logged.

---

## 12. Item UI

- **Shift + click:** whole stack (vanilla; `Input.GetKey(KeyCode.LeftShift)` in `SlotClicked`).
- **Ctrl + click:** 1 item, or $100 of cash (vanilla's "single" cash amount).
- Holding both behaves like Ctrl + click.
- How: prefix on `ItemUIManager.SlotClicked` — if the move-one modifier is held on a left click, set
  `canControllerQuickMove` and `controllerQuickMoveSingle`; postfix clears `canControllerQuickMove`
  (vanilla clears the other in `LateUpdate`). Vanilla does the move with all its rules (locks, filters,
  capacity, stacking). `[SOURCE]` vanilla `ControllerQuickMoveSlotSingle`.
- Two rebindable Menus-context keybinds: **Move stack** (default Left Shift; binding Right Shift fixes
  vanilla's left-only check, which always keeps working) and **Move one** (default Ctrl).
- Research note: vanilla already moves one with Shift + right-click, but right-click doubles as "back"
  in menus, so it's easy to trigger by accident.

---

## 13. Main menu

- Skip logo and skip disclaimer (when enabled).
- Adds Meowcow Settings after Settings and a pink Meowcow Suite tag just after the version text under
  the logo. **Built**, in `Menus/MainMenu`, one frame after the Menu phase; each addition falls back
  separately. The button opens the Meowcow Settings window (placeholder tabs until SuiteSettingsManager).
- Renames vanilla Continue to **Load Game**; adds a new **Continue** (internally `ContinueLastSave`).
- Order: Continue, Load Game, New Game, Settings, Meowcow Settings, Credits, Quit.
- Lobby label click toggles Solo/Co-op intent.
- If possible, the background shows the last save's players.

---

## 14. Core and Themes

| Core (functionality) | Themes (look) |
|---|---|
| Windows, tabs, navigation, sizes and layouts | Colours and fonts (token overrides) |
| Save menu, Continue, Load Game, New Game | Main menu element positions, anchors, scale, hide toggles |
| Meowcow Settings, keybinds | Shareable presets |
| Play mode, UI registry, live element handles | Themes window drawn over other menus, draggable |

---

## 15. Planned patches and hooks

| Target | Kind | Why |
|---|---|---|
| `LoadManager.onLoadComplete` / `onPreSceneChange` | Event listener | Lifecycle (§4) |
| `SaveManager.onSaveStart` / `onSaveComplete` | Event listener | Save classification and snapshots (§10) |
| `SavePoint.Interacted` | Prefix + postfix | Panel save flag |
| `Console.Save.Execute` | Prefix + postfix | Console save flag |
| `Player.RpcLogic___HostExitedGame` | Prefix | Co-op quick load follow (§10) |
| `DeathScreen.Open` | Postfix | Titles and buttons by play mode (§11) |
| `DeathScreen.RespawnClicked` | Prefix | New Character in co-op Hardcore (§11) |
| `CharacterCreator.OnComplete` | Listener swap | New Character without Mirror (§11) |
| `ItemUIManager.SlotClicked` | Prefix + postfix | Ctrl + click (§12) |

Rule: prefer events and listener swaps; patch only methods reached through events or button
listeners, which IL2CPP calls by pointer.

---

## 16. Deferred

- **Event bus** — until needed (lifecycle isolation is a plain callback list).
- **Passing values between mods** — until a real tie-in shapes it.
- **Shared contracts project** — not needed.
- **Live multiplayer settings sync** — only if on-demand reads aren't enough.

---

## 17. Research

Done: save callers, death and respawn, host-disconnect behaviour, settings and keybinds, character
creator and intro, item UI, Input System version.

Remaining:
1. Steam lobby data limits (per key, per lobby) in the `ISteamMatchmaking` reference
   (`k_nMaxLobbyKeyLength`, `k_cubChatMetadataMax`) — needed when settings sync is built.
2. Vanilla `GameSettings` contents (new-game options LifeIsHard could set).