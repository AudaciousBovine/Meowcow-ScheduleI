using System.Collections.Generic;

namespace MeowcowMirror.Layout
{
    /// <summary>What the footer's Done button does.</summary>
    public enum CommitMode
    {
        /// <summary>Runs vanilla CharacterCreator.Done() (the intro). No Cancel button.</summary>
        Vanilla,
        /// <summary>Mirror applies the appearance, closes the creator and restores the camera. Cancel is shown.</summary>
        Mirror,
    }

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

    /// <summary>A full creator layout: tabs in display order, plus how Done behaves.</summary>
    public sealed class LayoutDefinition
    {
        public List<TabDefinition> Tabs { get; set; } = new List<TabDefinition>();
        public CommitMode CommitMode { get; set; } = CommitMode.Mirror;

        /// <summary>Text on the commit button, from the context.</summary>
        public string CommitLabel { get; set; } = "Done";

        /// <summary>The context this layout was filtered for.</summary>
        public string ContextId { get; set; }

        /// <summary>Whether the receipt panel is shown, from the context.</summary>
        public bool ShowReceipt { get; set; }
    }
}
