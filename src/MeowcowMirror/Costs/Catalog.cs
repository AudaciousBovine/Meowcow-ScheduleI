using System;
using System.Collections.Generic;
using Il2CppScheduleOne.Avatar;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Levelling;
using MeowcowMirror.Contexts;
using CreatorUI = Il2CppScheduleOne.CharacterCreator.CharacterCreatorInterface;
using ObjectSelector = Il2CppScheduleOne.CharacterCreator.CharacterCreatorAvatarObjectSelector;
using PlayerAppearance = Il2CppScheduleOne.Avatar.Player.PlayerAppearance;

namespace MeowcowMirror.Costs
{
    /// <summary>
    /// Decides which styles each context's hair and facial hair grids show. Each selector keeps one
    /// growing set of buttons (vanilla's plus any created for wider catalogs); a context only
    /// switches buttons on or off, so nothing is rebuilt and selection keeps working.
    /// </summary>
    internal static class Catalog
    {
        internal static bool FullBarberCatalogInCreate;

        internal static void Apply(CreatorUI ui, string contextId, PlayerAppearance current)
        {
            if (ui == null || string.IsNullOrEmpty(contextId)) return;
            ApplySelector(ui._hairSelector, contextId, "vanilla.hair", current?.HairStyleId);
            ApplySelector(ui._facialHairSelector, contextId, "vanilla.facialHair", current?.FacialHairId);
        }

        private static void ApplySelector(ObjectSelector selector, string contextId, string serviceId, string currentId)
        {
            if (selector == null) return;
            var service = ServiceCatalog.All.Find(s => s.Id == serviceId);

            // What this context offers, in display order.
            var wanted = new List<string>();
            void Add(string id) { if (id != null && !wanted.Contains(id)) wanted.Add(id); }

            bool isBarber = string.Equals(contextId, MirrorContexts.Barber, StringComparison.OrdinalIgnoreCase);
            bool isCreate = string.Equals(contextId, MirrorContexts.Create, StringComparison.OrdinalIgnoreCase);

            if (isBarber)
            {
                foreach (var id in PriceBook.GetCatalog(MirrorContexts.Barber, serviceId)) Add(id);
            }
            else
            {
                foreach (var id in VanillaIds(selector)) Add(id);
                if (isCreate && FullBarberCatalogInCreate)
                    foreach (var id in PriceBook.GetCatalog(MirrorContexts.Barber, serviceId)) Add(id);
            }
            Add(currentId ?? ""); // the player's current style always shows

            // Make sure every wanted style has a button (created once, kept for later contexts).
            var buttons = ButtonsById(selector);
            foreach (var id in wanted)
            {
                if (buttons.ContainsKey(id) || id == "") continue;
                var obj = AvatarObjectLibrary.GetAvatarObjectById(id);
                if (obj == null) continue;
                selector.CreateButton(obj);
            }
            buttons = ButtonsById(selector);

            // Show only what this context offers and the player has unlocked.
            var allowed = new HashSet<string>(wanted);
            foreach (var pair in buttons)
            {
                bool show = allowed.Contains(pair.Key);
                if (show && pair.Key != (currentId ?? "") && !isCreate && IsRankLocked(contextId, service, pair.Key))
                    show = false;
                pair.Value.gameObject.SetActive(show);
            }
        }

        /// <summary>Styles vanilla's creator offers in this selector ("" is None).</summary>
        private static IEnumerable<string> VanillaIds(ObjectSelector selector)
        {
            if (selector.CanSelectNone) yield return "";
            var options = selector.Options;
            if (options == null) yield break;
            for (int i = 0; i < options.Count; i++)
                if (options[i] != null) yield return options[i].Id;
        }

        private static Dictionary<string, UnityEngine.UI.Button> ButtonsById(ObjectSelector selector)
        {
            var result = new Dictionary<string, UnityEngine.UI.Button>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in selector.buttonOptionMap)
            {
                string id = pair.Value != null ? pair.Value.Id : "";
                if (!result.ContainsKey(id)) result[id] = pair.Key;
            }
            return result;
        }

        internal static bool IsRankLocked(string contextId, Service service, string id)
        {
            if (!Receipt.EnforceRankLocks || service == null) return false;
            if (!PriceBook.TryGetRequiredRank(contextId, service, id, out var required)) return false;

            var levels = NetworkSingleton<LevelManager>.Instance;
            if (levels == null) return false;
            return levels.Rank < required.Rank || (levels.Rank == required.Rank && levels.Tier < required.Tier);
        }
    }
}
