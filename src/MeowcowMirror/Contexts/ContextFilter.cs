using System.Linq;
using MeowcowMirror.Layout;

namespace MeowcowMirror.Contexts
{
    /// <summary>
    /// Turns the shared layout into what one context shows: options not in the context are dropped,
    /// and tabs left empty are dropped with them.
    /// </summary>
    public static class ContextFilter
    {
        public static LayoutDefinition Filter(LayoutDefinition layout, ContextDefinition context)
        {
            var result = new LayoutDefinition
            {
                CommitMode = context.Id == MirrorContexts.Create ? CommitMode.Vanilla : CommitMode.Mirror,
                CommitLabel = context.CommitLabel,
                ShowReceipt = context.Receipt,
            };

            foreach (var tab in layout.Tabs)
            {
                var visible = tab.Options.Where(id => OptionContexts.IsIn(id, context.Id)).ToArray();
                if (visible.Length > 0)
                    result.Tabs.Add(new TabDefinition(tab.Id, tab.Name, visible));
            }

            return result;
        }

        /// <summary>True if the context would show at least one option.</summary>
        public static bool HasOptions(string contextId)
        {
            if (!MirrorContexts.TryGet(contextId, out var context)) return false;
            return Filter(VanillaOptions.CreateVanillaLayout(), context).Tabs.Count > 0;
        }
    }
}
