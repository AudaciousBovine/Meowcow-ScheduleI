using System;
using System.Runtime.CompilerServices;
using MelonLoader;

namespace MeowcowCore.Foundation;

/// <summary>
/// The suite's logger. Each mod creates one from its own MelonLoader logger (which already
/// prints the mod's name), and classes can make tagged copies so every line shows where it
/// came from: <c>[Receipt] Paid $45</c>.
/// </summary>
/// <example>
/// <code>
/// // In a mod's OnInitializeMelon:
/// Log = new MeowcowLogger(LoggerInstance);
///
/// // In a class, created once:
/// private static readonly MeowcowLogger Log = MirrorMod.Log.Tagged("Receipt");
///
/// Log.Info("Receipt opened");
/// Log.Debug($"Price for {id} is {price}");   // never built when debug is off
/// </code>
/// </example>
public sealed class MeowcowLogger
{
    /// <summary>
    /// The suite-wide debug toggle. Set by Core from its MelonPreferences debug category;
    /// mods never set it themselves.
    /// </summary>
    public static bool DebugEnabled { get; internal set; }

    private readonly MelonLogger.Instance _melon;
    private readonly string _prefix; // "" or "[Tag] ", built once so writing a line never rebuilds it

    /// <summary>Creates a mod's logger from its MelonLoader logger (<c>LoggerInstance</c>).</summary>
    public MeowcowLogger(MelonLogger.Instance melon)
        : this(melon, string.Empty)
    {
    }

    private MeowcowLogger(MelonLogger.Instance melon, string prefix)
    {
        _melon = melon ?? throw new ArgumentNullException(nameof(melon));
        _prefix = prefix;
    }

    /// <summary>
    /// Returns a logger that starts every line with <c>[tag]</c>. Create it once per class
    /// (a static field), not per call.
    /// </summary>
    /// <remarks>[PERFORMANCE] One small allocation when created; nothing per line.</remarks>
    public MeowcowLogger Tagged(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return this;
        return new MeowcowLogger(_melon, $"{_prefix}[{tag.Trim()}] ");
    }

    // ---------- Levels ----------

    public void Info(string message) => _melon.Msg(_prefix + message);

    public void Warning(string message) => _melon.Warning(_prefix + message);

    public void Error(string message) => _melon.Error(_prefix + message);

    /// <summary>Logs an error with the exception's full details (type, message, stack trace).</summary>
    public void Error(string message, Exception exception) =>
        _melon.Error($"{_prefix}{message}\n{exception}");

    /// <summary>A debug line with no values in it. Skipped when debug is off.</summary>
    public void Debug(string message)
    {
        if (DebugEnabled) _melon.Msg("[Debug] " + _prefix + message);
    }

    /// <summary>
    /// A debug line built from an interpolated string, e.g. <c>Log.Debug($"Price is {price}")</c>.
    /// When debug is off, the string is never built: no formatting, no allocation.
    /// </summary>
    /// <remarks>
    /// [SOURCE] C# 10 interpolated string handlers (general .NET 6 practice; .NET's own
    /// <c>Debug.Assert</c> uses the same feature). The compiler picks this overload for any
    /// <c>$"..."</c> with values in it, and asks <see cref="DebugMessage"/> whether to build it.
    /// [PERFORMANCE] Debug off: one boolean check per call. Debug on: the same as normal logging.
    /// </remarks>
    public void Debug(ref DebugMessage message)
    {
        if (DebugEnabled) _melon.Msg("[Debug] " + _prefix + message.ToStringAndClear());
    }

    /// <summary>
    /// Builds a debug line only when debug is on. Used by the compiler, never directly:
    /// writing <c>Log.Debug($"...")</c> is all a caller does.
    /// </summary>
    [InterpolatedStringHandler]
    public ref struct DebugMessage
    {
        private DefaultInterpolatedStringHandler _inner;

        // The compiler calls this first. Setting isEnabled to false makes it skip every
        // Append call below, so none of the values are ever formatted.
        public DebugMessage(int literalLength, int formattedCount, out bool isEnabled)
        {
            isEnabled = DebugEnabled;
            _inner = isEnabled
                ? new DefaultInterpolatedStringHandler(literalLength, formattedCount)
                : default;
        }

        public void AppendLiteral(string value) => _inner.AppendLiteral(value);

        public void AppendFormatted<T>(T value) => _inner.AppendFormatted(value);

        public void AppendFormatted<T>(T value, string? format) => _inner.AppendFormatted(value, format);

        public void AppendFormatted<T>(T value, int alignment) => _inner.AppendFormatted(value, alignment);

        public void AppendFormatted<T>(T value, int alignment, string? format) =>
            _inner.AppendFormatted(value, alignment, format);

        public void AppendFormatted(string? value) => _inner.AppendFormatted(value);

        internal string ToStringAndClear() => _inner.ToStringAndClear();
    }
}