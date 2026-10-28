using System;
using System.IO;
using MelonLoader.Utils;

namespace MeowcowCore.Foundation;

/// <summary>
/// The only place the suite builds file paths. Everything lives under <c>UserData/Meowcow/</c>:
/// <code>
/// UserData/Meowcow/
///   Core/
///     Saves/          per-save extras (SaveGame_2.json, ...)
///     DeletedSaves/   backups of deleted saves (last 5), extras included
///   MeowcowMirror/    one folder per mod, named by its module Id
///     settings.json
/// </code>
/// It only builds paths and never touches the disk: whoever writes a file calls
/// <see cref="Ensure"/> first, so mods that never wake never leave empty folders behind.
/// The game's own paths (like the vanilla saves folder) are read where they're used, not here.
/// </summary>
/// <remarks>[SOURCE] LilithsEngine's HeartPathIndex: one root and one index for all suite files.</remarks>
public static class CorePathIndex
{
    private const string SettingsFileName = "settings.json";

    // [PERFORMANCE] Built once, the first time anything here is used.

    /// <summary><c>UserData/Meowcow/</c>, the root for every suite file.</summary>
    public static readonly string Root = Path.Combine(MelonEnvironment.UserDataDirectory, "Meowcow");

    /// <summary><c>UserData/Meowcow/Core/</c>, Core's own data.</summary>
    public static readonly string CoreDir = Path.Combine(Root, "Core");

    /// <summary><c>Core/Saves/</c>, one extras file per save slot.</summary>
    public static readonly string SaveExtrasDir = Path.Combine(CoreDir, "Saves");

    /// <summary><c>Core/DeletedSaves/</c>, backups of deleted saves with their extras.</summary>
    public static readonly string DeletedSavesDir = Path.Combine(CoreDir, "DeletedSaves");

    /// <summary>A mod's own folder, e.g. <c>UserData/Meowcow/MeowcowMirror/</c>.</summary>
    /// <remarks>[PERFORMANCE] One string join per call.</remarks>
    public static string ModDir(string moduleId) => Path.Combine(Root, CleanName(moduleId));

    /// <summary>A mod's settings file, e.g. <c>MeowcowMirror/settings.json</c>.</summary>
    public static string SettingsFile(string moduleId) => Path.Combine(ModDir(moduleId), SettingsFileName);

    /// <summary>The extras file for a save, keyed by its folder name, e.g. <c>Core/Saves/SaveGame_2.json</c>.</summary>
    public static string SaveExtrasFile(string saveFolderName) =>
        Path.Combine(SaveExtrasDir, CleanName(saveFolderName) + ".json");

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
    /// Replaces any character Windows doesn't allow in a file or folder name, so an Id
    /// can never break a path (or point outside the suite's folder).
    /// </summary>
    private static string CleanName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A name is required to build a path.", nameof(name));

        string trimmed = name.Trim();

        // "." and ".." contain only valid characters but mean "this folder" and "the parent".
        if (trimmed == "." || trimmed == "..")
            throw new ArgumentException($"'{trimmed}' isn't a usable name.", nameof(name));

        char[] chars = trimmed.ToCharArray();
        char[] invalid = Path.GetInvalidFileNameChars();
        for (int i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0) chars[i] = '_';
        }
        return new string(chars);
    }
}