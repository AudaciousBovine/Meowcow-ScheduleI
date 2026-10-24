using System.Collections.Generic;
using C = MeowcowMirror.Contexts.MirrorContexts; // C.Barber etc.; plain 'Mirror' would mean the API class

namespace MeowcowMirror.Layout
{
    /// <summary>
    /// A vanilla creator row: our stable ID, the vanilla tab it starts in, its GameObject name, and
    /// the contexts it appears in by default (see docs/contexts.md, section 3).
    /// </summary>
    public readonly struct VanillaOption
    {
        public readonly string Id;
        public readonly string TabId;
        public readonly string RowName;
        public readonly string[] Contexts;

        public VanillaOption(string id, string tabId, string rowName, params string[] contexts)
        {
            Id = id;
            TabId = tabId;
            RowName = rowName;
            Contexts = contexts;
        }
    }

    /// <summary>
    /// The only place that knows vanilla row names. If a game update renames a row, fix it here.
    /// Tab IDs are the vanilla window names, lowercased.
    /// </summary>
    public static class VanillaOptions
    {
        public const string FinalizeTab = "finalize";

        public static readonly VanillaOption[] All =
        {
            new VanillaOption("vanilla.gender",                "body",     "Gender", C.Create),
            new VanillaOption("vanilla.weight",                "body",     "Weight", C.Create, C.Surgeon),
            new VanillaOption("vanilla.skinColor",             "body",     "SkinColor", C.Create),

            new VanillaOption("vanilla.hair",                  "hair",     "Hair", C.Create, C.Barber),
            new VanillaOption("vanilla.hairColor",             "hair",     "Hair Color", C.Create, C.Barber),

            new VanillaOption("vanilla.face",                  "face",     "Face", C.Create, C.Mirror),
            new VanillaOption("vanilla.facialHair",            "face",     "Facial Hair", C.Create, C.Barber, C.Mirror),
            new VanillaOption("vanilla.facialDetails",         "face",     "Facial Details", C.Create, C.Surgeon),
            new VanillaOption("vanilla.facialDetailIntensity", "face",     "Facial Detail Intensity", C.Create, C.Surgeon),

            new VanillaOption("vanilla.eyeballColor",          "eyes",     "EyeballColor", C.Create, C.Surgeon),
            new VanillaOption("vanilla.pupilDilation",         "eyes",     "PupilDilation", C.Create, C.Mirror),
            new VanillaOption("vanilla.upperEyelid",           "eyes",     "Upper Eyelid Position", C.Create, C.Mirror),
            new VanillaOption("vanilla.lowerEyelid",           "eyes",     "Lower Eyelid Position", C.Create, C.Mirror),

            new VanillaOption("vanilla.eyebrowScale",          "eyebrows", "EyebrowScale", C.Create, C.Barber, C.Mirror),
            new VanillaOption("vanilla.eyebrowThickness",      "eyebrows", "EyebrowThickness", C.Create, C.Barber, C.Mirror),
            new VanillaOption("vanilla.eyebrowHeight",         "eyebrows", "EyebrowRestingHeight", C.Create, C.Barber, C.Mirror),
            new VanillaOption("vanilla.eyebrowAngle",          "eyebrows", "EyebrowRestingAngle", C.Create, C.Barber, C.Mirror),

            new VanillaOption("vanilla.top",                   "clothing", "Top", C.Create),
            new VanillaOption("vanilla.bottom",                "clothing", "Bottom", C.Create),
            new VanillaOption("vanilla.shoes",                 "clothing", "Shoes", C.Create),

            // Captured so they can be parked; the frame's footer replaces the Finalize tab.
            new VanillaOption("vanilla.finalizeText",          FinalizeTab, "Text (TMP)"),
            new VanillaOption("vanilla.done",                  FinalizeTab, "Done"),
        };

        private static readonly (string id, string name)[] TabOrder =
        {
            ("body", "Body"), ("hair", "Hair"), ("face", "Face"),
            ("eyes", "Eyes"), ("eyebrows", "Eyebrows"), ("clothing", "Clothing"),
        };

        /// <summary>The vanilla tabs and options, minus Finalize (the footer has Done now).</summary>
        public static LayoutDefinition CreateVanillaLayout()
        {
            var layout = new LayoutDefinition();
            foreach (var (id, name) in TabOrder)
            {
                var tab = new TabDefinition(id, name);
                foreach (var opt in All)
                    if (opt.TabId == id) tab.Options.Add(opt.Id);
                layout.Tabs.Add(tab);
            }
            return layout;
        }
    }
}
