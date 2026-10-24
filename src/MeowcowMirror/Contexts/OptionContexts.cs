using System;
using System.Collections.Generic;
using MeowcowMirror.Layout;

namespace MeowcowMirror.Contexts
{
    /// <summary>
    /// Which contexts each option appears in. Vanilla defaults come from VanillaOptions; later,
    /// JSON layers (mods, player overrides) will replace entries here.
    /// </summary>
    public static class OptionContexts
    {
        private static readonly Dictionary<string, HashSet<string>> _map =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        static OptionContexts()
        {
            foreach (var opt in VanillaOptions.All)
                Set(opt.Id, opt.Contexts);
        }

        /// <summary>Replaces the contexts an option appears in.</summary>
        public static void Set(string optionId, IEnumerable<string> contexts)
        {
            _map[optionId] = new HashSet<string>(contexts ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        }

        public static bool IsIn(string optionId, string contextId)
        {
            if (!_map.TryGetValue(optionId, out var set)) return false;
            return set.Contains(MirrorContexts.Any) || set.Contains(contextId);
        }
    }
}
