using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader.Utils;

namespace MeowcowCore.Foundation;

/// <summary>
/// The only place the suite builds file paths. Everything lives under <c>UserData/Meowcow/</c>:
/// <code>
/// UserData/Meowcow/
///   Saves/
///     Business Name/            one playthrough
///       playthrough.json
///       Current/                Game/ + modules.json
///       Snapshots/
///         &lt;snapshot&gt;/         one per snapshot, each: Game/ + modules.json
///   DeletedSaves/               last 5 deleted playthroughs
///   MeowcowCore/                Core's own data (settings, keybind mirror, vanilla-imports.json)
///   &lt;ModuleId&gt;/settings.json   one folder per mod, named by its module Id
/// </code>
/// It only builds paths: it never chooses names and never touches the disk. Names (playthrough
/// folders, duplicate numbering, snapshot folders) are decided by <c>SuiteSaveManager</c>.
/// Whoever writes a file calls <see cref="Ensure"/> first, so mods that never wake never leave
/// empty folders behind.
/// The game's own paths (like the vanilla saves folder) are read where they're used, not here.
/// </summary>
/// <remarks>
/// [SOURCE] LilithsEngine's HeartPathIndex: one root and one index for all suite files.
/// Note for outside modders: a module Id of "Saves" or "DeletedSaves" would collide with those folders.
/// </remarks>
public static class CorePathIndex
{
    private const string CoreModuleId = "MeowcowCore";
    private const string SettingsFileName = "settings.json";

    // [CHANGED] New file and folder names for the playthrough layout (design §10).
    private const string PlaythroughFileName = "playthrough.json";
    private const string ModulesFileName = "modules.json";
    private const string VanillaImportsFileName = "vanilla-imports.json";
    private const string CurrentFolderName = "Current";
    private const string SnapshotsFolderName = "Snapshots";
    private const string GameFolderName = "Game";

    // [CHANGED] Windows reserved device names. A folder can't be named any of these,
    // even with an extension ("CON.txt"), and the check is case-insensitive.
    // [SOURCE] Microsoft's "Naming Files, Paths, and Namespaces" documentation.
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    // [PERFORMANCE] Built once, the first time anything here is used.
    private static readonly char[] InvalidNameChars = Path.GetInvalidFileNameChars();

    /// <summary><c>UserData/Meowcow/</c>, the root for every suite file.</summary>
    public static readonly string Root = Path.Combine(MelonEnvironment.UserDataDirectory, "Meowcow");

    /// <summary><c>UserData/Meowcow/MeowcowCore/</c>, Core's own data.</summary>
    /// <remarks>
    /// [CHANGED] Was <c>Core/</c>. Core now follows the same rule as every module (folder named by
    /// its Id), so <c>SettingsFile("MeowcowCore")</c> and <c>CoreDir</c> point to the same place.
    /// </remarks>
    public static readonly string CoreDir = ModDir(CoreModuleId);

    /// <summary><c>UserData/Meowcow/Saves/</c>, one folder per playthrough.</summary>
    /// <remarks>[CHANGED] New. Replaces <c>Core/Saves/</c> (the old per-slot extras folder).</remarks>
    public static readonly string SavesDir = Path.Combine(Root, "Saves");

    /// <summary><c>UserData/Meowcow/DeletedSaves/</c>, the last 5 deleted playthroughs with their data.</summary>
    /// <remarks>[CHANGED] Moved up from <c>Core/DeletedSaves/</c> to the root, per the §10 layout.</remarks>
    public static readonly string DeletedSavesDir = Path.Combine(Root, "DeletedSaves");

    /// <summary><c>MeowcowCore/vanilla-imports.json</c>, which vanilla saves were imported or declined.</summary>
    /// <remarks>[CHANGED] New.</remarks>
    public static readonly string VanillaImportsFile = Path.Combine(CoreDir, VanillaImportsFileName);

    // [CHANGED] Removed SaveExtrasDir and SaveExtrasFile: extras now live inside each
    // playthrough (playthrough.json and modules.json) instead of a separate folder.

    // ── Mods ────────────────────────────────────────────────────────────────

    /// <summary>A mod's own folder, e.g. <c>UserData/Meowcow/MeowcowMirror/</c>.</summary>
    /// <remarks>[PERFORMANCE] One string join per call.</remarks>
    public static string ModDir(string moduleId) => Path.Combine(Root, CleanName(moduleId));

    /// <summary>A mod's settings file, e.g. <c>MeowcowMirror/settings.json</c>.</summary>
    public static string SettingsFile(string moduleId) => Path.Combine(ModDir(moduleId), SettingsFileName);

    // ── Playthroughs ────────────────────────────────────────────────────────
    // [CHANGED] All new. "playthrough" is the folder name, e.g. "Business Name - 2".
    // Choosing that name (including duplicate numbering) is SuiteSaveManager's job,
    // since it has to look at the disk; this class only builds the paths.

    /// <summary>A playthrough's folder, e.g. <c>Saves/Business Name/</c>.</summary>
    public static string PlaythroughDir(string playthrough) => Path.Combine(SavesDir, CleanName(playthrough));

    /// <summary>A playthrough's <c>playthrough.json</c> (pin, players, save mode, module playthrough data).</summary>
    public static string PlaythroughFile(string playthrough) =>
        Path.Combine(PlaythroughDir(playthrough), PlaythroughFileName);

    /// <summary><c>Current/</c>, the latest save of any kind. A save folder (see <see cref="GameDir"/>).</summary>
    public static string CurrentDir(string playthrough) =>
        Path.Combine(PlaythroughDir(playthrough), CurrentFolderName);

    /// <summary><c>Snapshots/</c>, Modern mode only.</summary>
    public static string SnapshotsDir(string playthrough) =>
        Path.Combine(PlaythroughDir(playthrough), SnapshotsFolderName);

    /// <summary>One snapshot's folder, e.g. <c>Snapshots/Quick/</c>. A save folder (see <see cref="GameDir"/>).</summary>
    /// <remarks>
    /// [CHANGED] Now takes the snapshot's folder name instead of a <c>SnapshotKind</c>, and replaces
    /// <c>ManualSnapshotDir</c>. Snapshot names ("Quick", "Manual - ...") are naming rules, so
    /// <c>SuiteSaveManager</c> owns them; this only builds the path.
    /// [PERFORMANCE] Two string joins per call.
    /// </remarks>
    public static string SnapshotDir(string playthrough, string snapshotFolder) =>
        Path.Combine(SnapshotsDir(playthrough), CleanName(snapshotFolder));

    // ── Save folders ────────────────────────────────────────────────────────
    // [CHANGED] New. Current/ and every snapshot share one shape (Game/ + modules.json), so these
    // take any save folder. Restoring a snapshot is then the same copy for every kind.
    // [SOURCE] General practice: describe a repeated shape once.

    /// <summary>The <c>Game/</c> folder inside a save folder: what the game itself loads and saves.</summary>
    public static string GameDir(string saveDir) => Path.Combine(saveDir, GameFolderName);

    /// <summary>The <c>modules.json</c> inside a save folder: module progress data.</summary>
    /// <remarks>Sits beside <c>Game/</c>, never inside it: vanilla deletes files it didn't write in there.</remarks>
    public static string ModulesFile(string saveDir) => Path.Combine(saveDir, ModulesFileName);

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a folder if it doesn't exist yet, and returns it. Call this right before
    /// writing into a folder. Does nothing if the folder already exists.
    /// </summary>
    /// <example><c>File.WriteAllText(Path.Combine(CorePathIndex.Ensure(dir), "x.json"), json);</c></example>
    public static string Ensure(string directory)
    {
        Directory.CreateDirectory(directory);
        return directory;
    }

    /// <summary>
    /// Turns any name into a safe file or folder name, so it can never break a path or point
    /// outside the suite's folder. The save manager uses this too, so the save menu shows the
    /// real folder name. Throws if nothing usable is left.
    /// </summary>
    /// <remarks>
    /// [CHANGED] Now public (was private). Also trims trailing dots and spaces, and guards
    /// reserved device names; both are Windows rules the old version missed.
    /// [PERFORMANCE] O(n) over the name's length, plus one hash lookup.
    /// </remarks>
    /// <example><c>"Bob's: Finest" → "Bob's_ Finest"</c>, <c>"Con" → "Con_"</c>, <c>"Shop..." → "Shop"</c></example>
    public static string CleanName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A name is required to build a path.", nameof(name));

        char[] chars = name.Trim().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(InvalidNameChars, chars[i]) >= 0) chars[i] = '_';
        }

        // [CHANGED] Windows silently drops trailing dots and spaces, so the folder on disk wouldn't
        // match the name we stored. Trimming them ourselves keeps the two identical.
        // This also covers "." and "..", which trim to nothing and are rejected below.
        string cleaned = new string(chars).TrimEnd('.', ' ');
        if (cleaned.Length == 0)
            throw new ArgumentException($"'{name}' isn't a usable name.", nameof(name));

        // [CHANGED] Reserved names count even with an extension ("CON.txt"), so check the part before the first dot.
        int dot = cleaned.IndexOf('.');
        string baseName = dot < 0 ? cleaned : cleaned.Substring(0, dot);
        if (ReservedNames.Contains(baseName))
            cleaned = baseName + "_" + cleaned.Substring(baseName.Length);

        return cleaned;
    }
}