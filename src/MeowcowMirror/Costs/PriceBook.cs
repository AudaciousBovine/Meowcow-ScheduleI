using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MelonLoader.Utils;
using UObject = UnityEngine.Object;
using MeowcowMirror.Contexts;
using BarbershopUI = Il2CppScheduleOne.UI.CharacterCustomization.BarbershopUI;
using CustomizationCategory = Il2CppScheduleOne.UI.CharacterCustomization.CharacterCustomizationCategory;
using CustomizationOption = Il2CppScheduleOne.UI.CharacterCustomization.CharacterCustomizationOption;

namespace MeowcowMirror.Costs
{
    /// <summary>
    /// Resolves the price of a service in a context, in this order:
    /// 1. prices.json: the service's own entry (per value, then default)
    /// 2. Vanilla shop prices (read from the shop's options)
    /// 3. prices.json: the context's "*" entry (a default for that context only)
    /// 4. The DefaultServicePrice setting
    /// prices.json lives in UserData/MeowcowMirror/ and is re-read every time a menu opens.
    /// </summary>
    internal static class PriceBook
    {
        internal static float DefaultPrice = 10f;

        private const string AnyValue = "*";
        private static readonly Dictionary<string, float> _vanilla = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> _vanillaLoaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, Dictionary<string, PriceEntry>> _overrides =
            new Dictionary<string, Dictionary<string, PriceEntry>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>One service's override: a price for all values, and/or per-value prices.</summary>
        internal sealed class PriceEntry
        {
            public float? Default { get; set; }
            public Dictionary<string, float> Values { get; set; }
        }

        private static string Folder => Path.Combine(MelonEnvironment.UserDataDirectory, "MeowcowMirror");
        private static string OverridesPath => Path.Combine(Folder, "prices.json");

        internal static float GetPrice(string contextId, Service service, string value)
        {
            EnsureVanilla(contextId);
            value ??= "";

            if (_overrides.TryGetValue(contextId, out var services) && services.TryGetValue(service.Id, out var entry))
            {
                if (entry.Values != null && entry.Values.TryGetValue(value, out float perValue)) return perValue;
                if (entry.Default.HasValue) return entry.Default.Value;
            }

            if (_vanilla.TryGetValue(Key(contextId, service.Id, value), out float vanilla)) return vanilla;
            if (_vanilla.TryGetValue(Key(contextId, service.Id, AnyValue), out vanilla)) return vanilla;

            if (services != null && services.TryGetValue(AnyValue, out var contextDefault) && contextDefault.Default.HasValue)
                return contextDefault.Default.Value;

            return DefaultPrice;
        }

        /// <summary>Re-reads prices.json, writing an example file on first run.</summary>
        internal static void ReloadOverrides()
        {
            var result = new Dictionary<string, Dictionary<string, PriceEntry>>(StringComparer.OrdinalIgnoreCase);
            try
            {
                Directory.CreateDirectory(Folder);
                WriteExampleIfMissing();
                if (File.Exists(OverridesPath))
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true,
                    };
                    var raw = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, PriceEntry>>>(File.ReadAllText(OverridesPath), options);
                    if (raw != null)
                    {
                        foreach (var context in raw)
                        {
                            var services = new Dictionary<string, PriceEntry>(StringComparer.OrdinalIgnoreCase);
                            foreach (var service in context.Value)
                            {
                                var entry = service.Value ?? new PriceEntry();
                                if (entry.Values != null)
                                    entry.Values = new Dictionary<string, float>(entry.Values, StringComparer.OrdinalIgnoreCase);
                                services[service.Key] = entry;
                            }
                            result[context.Key] = services;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MirrorMod.Log.Warning($"Couldn't read prices.json, using vanilla and default prices: {e.Message}");
            }
            _overrides = result;
        }

        private static void WriteExampleIfMissing()
        {
            string example = Path.Combine(Folder, "prices.example.json");
            if (File.Exists(example)) return;
            File.WriteAllText(example,
@"// Copy to prices.json to use. Prices are per context, then per service.
// ""default"" prices every value; ""values"" prices specific ones (avatar object IDs).
// A ""*"" entry is the fallback for anything in that context without its own price.
{
  ""barber"": {
    ""*"":                 { ""default"": 15 },
    ""vanilla.hair"":      { ""default"": 30, ""values"": { ""mohawk"": 45 } },
    ""vanilla.hairColor"": { ""default"": 40 }
  },
  ""surgeon"": {
    ""*"":                 { ""default"": 250 }
  }
}
");
        }

        /// <summary>Reads prices from the vanilla shop backing a context, once per session.</summary>
        private static void EnsureVanilla(string contextId)
        {
            if (!_vanillaLoaded.Add(contextId)) return;

            if (string.Equals(contextId, MirrorContexts.Barber, StringComparison.OrdinalIgnoreCase))
            {
                var shop = UObject.FindObjectOfType<BarbershopUI>(true);
                if (shop == null) { MirrorMod.Log.Warning("Barbershop not found; using default prices"); return; }

                ReadCategory(contextId, shop.HairStyleCategory, "vanilla.hair");
                ReadCategory(contextId, shop.BeardCategory, "vanilla.facialHair");
                // Vanilla colour changes are free, but dyeing isn't: colour falls through to the defaults.
            }
        }

        private static void ReadCategory(string contextId, CustomizationCategory category, string serviceId)
        {
            if (category == null) return;
            foreach (var option in category.GetComponentsInChildren<CustomizationOption>(true))
            {
                string id = option.AvatarObject != null ? option.AvatarObject.Id : "";
                _vanilla[Key(contextId, serviceId, id)] = option.Price;
            }
        }

        private static string Key(string context, string service, string value) => $"{context}|{service}|{value}";
    }
}