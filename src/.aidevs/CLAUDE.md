# CLAUDE.md — Meowcow-ScheduleI

Guidance for Claude Code in this repo. Read this first every session.

## Working with me

- Address me as **Mistress**.
- **Discuss and get my approval before writing code.** Propose the approach, explain the options, wait for a yes.
- **Ask before creating any new file or folder**, with the reasoning. Edits to existing files are fine once the approach is approved.
- **Confirm before big tasks** (multi-file changes, refactors, anything long-running).
- **Explain decisions**: why this approach, what the alternatives were.
- **Always note performance** and any possible performance issues (per-frame cost, allocations, hot paths).
- **Note where ideas came from**: an existing Schedule I mod, vanilla code, Unity samples, or general practice.
- **Keep responses short and readable.** No walls of text.
- **Ask me for technical information** (decompiles, game behaviour, versions) instead of guessing or searching the web. I supply Mono decompiles when asked.
- **Decompiles live in `Decompiles/`** at the repo root (gitignored, never committed). Check there first; ask me for any that's missing.
- Let reasoning unfold; don't jump to quick answers on design questions.
- After code changes, run `dotnet build` and fix errors before handing back. I test in game and share the log.

## Design philosophy

- **Modular and configurable.** Settings over hard-coded behaviour; modules tie into each other through Core.
- **Avoid Harmony patching where possible.** Prefer vanilla's public APIs, events and hooks (e.g. `GameInput.RegisterExitListener`, `Settings.onInputsApplied`). Patch only when there's no other way, and say why.
- **Clone or rebuild vanilla UI** rather than moving or patching vanilla objects. Hide vanilla pieces and show Core's own.
- **Nothing per frame unless necessary.** Event-driven first; coroutines only while something is open.
- **Fail soft.** A feature that can't build falls back to vanilla and logs why; exceptions never escape native callbacks.

## Comment conventions

Mark recent work in code so it can be reviewed:

- `[CHANGED]` — new or changed code, with a short note of what and why.
- `[SOURCE]` — where the approach came from (vanilla class, Unity sample, other mod, general practice).
- `[PERFORMANCE]` — the cost, especially anything per frame.
- `[TEMPORARY]` — research or scaffolding to remove later; fence whole blocks so they delete cleanly.

## Project facts

- **Game:** Schedule I (TVGS), **IL2CPP**, Unity 2022.3.62f2.
- **Loader:** MelonLoader 0.7.3, net6 runtime, Il2CppInterop. Game types are under `Il2Cpp…` namespaces (e.g. `Il2CppScheduleOne.GameInput`).
- **Repo:** monorepo, `src/<ModName>/` per mod; Core lives in `src/MeowcowCore/`.
- **Design doc:** `src/.aidevs/MeowcowCore-Design.md` — the source of truth for Core's architecture. Check it before proposing structure changes.
- **Log:** `<game>/MelonLoader/Latest.log`. Debug logging is a MelonPreferences toggle (`MeowcowCore/DebugLogging`).

## Code style

- C#, file-scoped namespaces, nullable enabled (warnings, not errors), `LangVersion` 10.
- Logging through `MeowcowLogger` (`CoreMod.Log.Tagged("Area")`), not `MelonLogger` directly.
- Core's folders use a **"Suite" prefix** (SuiteUI, SuiteSettings, …) so they're never confused with vanilla systems. `Menus/` holds how vanilla menus are altered.
- Objects Core adds to vanilla screens are named with the **`Meowcow` prefix** (`MeowcowWindow.CoreAdditionPrefix`), so cloned windows leave them out.
- Naming: **"Improved Settings Menu"** is Core's rebuild of vanilla Settings; **"Meowcow Settings"** is the mods' settings window. Never mix them.

## IL2CPP lessons (hard-won)

- `GetComponentInParent<T>(true)` throws `VerificationException`; walk parents instead. `GetComponentsInChildren<T>(bool)` is safe.
- Injected handler types must list `IEventSystemHandler` in their interfaces.
- **Keep converted delegates referenced** for their owner's lifetime (store the Il2Cpp delegate). Reuse the same instance to deregister: two conversions of one lambda are different objects.
- A window's open handler runs **before** `MenuScreen` sets `IsOpen`: never loop on `IsOpen` from an open handler; yield a frame and stop on a token bumped at close.
- `GameInput.GetButtonDown(Submit)` never fires in menus; use `UIScreenManager.SubmitInputAction.action.WasPressedThisFrame()`.
- Il2Cpp `GetFields` needs explicit `BindingFlags`.
- Avoid naming types whose namespace is unverified (e.g. `EPlatformType`); compare values instead.
- Use `TryCast<T>()` for Il2Cpp casts; don't use `?.` on Unity objects (it skips Unity's null check).
- Vanilla typos must be matched exactly: `FOVSLider`, `IntefaceScaleSlider`.
- Unity structs like `InputBinding` are **classes** under Il2CppInterop: `InputBinding?` is a nullable reference,
  not a `Nullable<T>` (no `.HasValue` / `.Value`). Check it with **`is null` / `is not null`**: its own `==`/`!=`
  operators go to native code and throw a NullReferenceException on null.

## Vanilla input notes

- `PlayerInput` uses **Send Messages**; `GameInput` toggles button state on each event (press and release).
- Bindings save as **overrides only** (`Settings.WriteInputSettings` → PlayerPrefs `BindingOverrides`); restore-defaults clears overrides only. Bindings Core adds use `path`, never `overridePath`, so vanilla never saves or clears them. Core saves and resets its own.
- Vanilla's rebind UI (`RebindActionUI`) is Unity's RebindingUI sample; it excludes the mouse and blocks Escape-exit via `IsRebindingInProgress` (getter only, no setter).
- Escape/B close screens through `GameInput` exit listeners, highest priority first; `action.Use()` stops the chain. Vanilla clears all listeners on scene change.