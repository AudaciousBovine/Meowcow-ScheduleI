using System.Collections.Generic;

namespace MeowcowMirror.Layout
{
    /// <summary>One tab in the creator: a stable ID, a display name, and the option IDs it shows, in order.</summary>
    public sealed class TabDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<string> Options { get; set; } = new List<string>();

        public TabDefinition() { }

        public TabDefinition(string id, string name, params string[] options)
        {
            Id = id;
            Name = name;
            Options = new List<string>(options);
        }

        public TabDefinition Copy() => new TabDefinition(Id, Name, Options.ToArray());
    }

    /// <summary>A full creator layout: tabs in display order. Finalize is always forced last.</summary>
    public sealed class LayoutDefinition
    {
        public List<TabDefinition> Tabs { get; set; } = new List<TabDefinition>();
    }
}
