# Meowcow-ScheduleI

Meowcow, a MelonLoader mod suite for Schedule I (IL2CPP).

| Project | Purpose |
|---|---|
| `MeowcowCore` | Shared services used by every Meowcow mod |
| `MeowcowMirror` | Appearance and character-creator extensions |

## Building

1. Install the .NET 6 SDK or newer.
2. Run the game once with MelonLoader so `MelonLoader/Il2CppAssemblies` is generated.
3. Copy `GamePaths.props.example` to `GamePaths.props` and set `GameDir`.
4. `dotnet build` from the repo root. DLLs are copied to `Mods/` automatically.

Game and interop assemblies are never committed; they are referenced from your local install.
