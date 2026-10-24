using System;
using System.Collections.Generic;

namespace MeowcowMirror.Contexts
{
    /// <summary>
    /// A situation the creator opens in. Every registered context starts from the player's current
    /// look, is committed by Mirror, and can always be cancelled. See docs/contexts.md.
    /// </summary>
    public sealed class ContextDefinition
    {
        public string Id { get; }
        public string Name { get; }

        /// <summary>Whether the receipt panel is shown (contexts with costs). Not built yet.</summary>
        public bool Receipt { get; }

        /// <summary>Text on the commit button: "Done", "Pay for service", "Apply".</summary>
        public string CommitLabel { get; }

        public ContextDefinition(string id, string name, bool receipt = false, string commitLabel = "Done")
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Context id is required", nameof(id));
            Id = id.Trim().ToLowerInvariant();
            Name = string.IsNullOrWhiteSpace(name) ? Id : name;
            Receipt = receipt;
            CommitLabel = string.IsNullOrWhiteSpace(commitLabel) ? "Done" : commitLabel;
        }
    }

    /// <summary>Built-in context IDs and the registry mods add their own contexts to.</summary>
    public static class MirrorContexts
    {
        public const string Create = "create";   // internal: the intro
        public const string Any = "any";         // special: an option marked Any appears everywhere
        public const string Mirror = "mirror";
        public const string Barber = "barber";
        public const string Tattoo = "tattoo";
        public const string NailSalon = "nailsalon";
        public const string Surgeon = "surgeon";
        public const string Dentist = "dentist";

        /// <summary>The intro's context. Internal to Mirror: preset start, vanilla Done, no Cancel.</summary>
        internal static readonly ContextDefinition CreateDefinition = new ContextDefinition(Create, "Create");

        private static readonly Dictionary<string, ContextDefinition> _registry =
            new Dictionary<string, ContextDefinition>(StringComparer.OrdinalIgnoreCase);

        static MirrorContexts()
        {
            Register(new ContextDefinition(Mirror, "Mirror", commitLabel: "Apply"));
            Register(new ContextDefinition(Barber, "Barbershop", receipt: true, commitLabel: "Pay for service"));
            Register(new ContextDefinition(Tattoo, "Tattoo Shop", receipt: true, commitLabel: "Pay for service"));
            Register(new ContextDefinition(NailSalon, "Nail Salon", receipt: true, commitLabel: "Pay for service"));
            Register(new ContextDefinition(Surgeon, "Surgeon", receipt: true, commitLabel: "Pay for service"));
            Register(new ContextDefinition(Dentist, "Dentist", receipt: true, commitLabel: "Pay for service"));
        }

        /// <summary>Registers a context. Fails for reserved IDs (create, any) and duplicates.</summary>
        public static bool Register(ContextDefinition context)
        {
            if (context == null) return false;
            if (context.Id == Create || context.Id == Any)
            {
                MirrorMod.Log.Warning($"Context id '{context.Id}' is reserved");
                return false;
            }
            if (_registry.ContainsKey(context.Id))
            {
                MirrorMod.Log.Warning($"Context '{context.Id}' is already registered");
                return false;
            }
            _registry[context.Id] = context;
            return true;
        }

        public static bool TryGet(string id, out ContextDefinition context)
        {
            context = null;
            return !string.IsNullOrWhiteSpace(id) && _registry.TryGetValue(id.Trim(), out context);
        }

        public static IEnumerable<ContextDefinition> All => _registry.Values;
    }
}
