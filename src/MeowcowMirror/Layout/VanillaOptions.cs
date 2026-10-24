using System.Collections.Generic;

namespace MeowcowMirror.Layout
{
    /// <summary>A vanilla creator row: our stable ID, the vanilla tab it starts in, and its GameObject name.</summary>
    public readonly struct VanillaOption
    {
        public readonly string Id;
        public readonly string TabId;
        public readonly string RowName;

        public VanillaOption(string id, string tabId, string rowName)
        {
            Id = id;
            TabId = tabId;
            RowName = rowName;
        }
    }

    /// <summary>
    /// The only place that knows vanilla row names. If a game update renames a row, fix it here.
    /// Tab IDs are the vanilla window names, lowercased.
    /// </summary>
    public static class VanillaOptions
    {
        public const string Done = "vanilla.done";
        public const string FinalizeTab = "finalize";

        public static readonly VanillaOption[] All =
        {
            new VanillaOption("vanilla.gender",                "body",     "Gender"),
            new VanillaOption("vanilla.weight",                "body",     "Weight"),
            new VanillaOption("vanilla.skinColor",             "body",     "SkinColor"),

            new VanillaOption("vanilla.hair",                  "hair",     "Hair"),
            new VanillaOption("vanilla.hairColor",             "hair",     "Hair Color"),

            new VanillaOption("vanilla.face",                  "face",     "Face"),
            new VanillaOption("vanilla.facialHair",            "face",     "Facial Hair"),
            new VanillaOption("vanilla.facialDetails",         "face",     "Facial Details"),
            new VanillaOption("vanilla.facialDetailIntensity", "face",     "Facial Detail Intensity"),

            new VanillaOption("vanilla.eyeballColor",          "eyes",     "EyeballColor"),
            new VanillaOption("vanilla.pupilDilation",         "eyes",     "PupilDilation"),
            new VanillaOption("vanilla.upperEyelid",           "eyes",     "Upper Eyelid Position"),
            new VanillaOption("vanilla.lowerEyelid",           "eyes",     "Lower Eyelid Position"),

            new VanillaOption("vanilla.eyebrowScale",          "eyebrows", "EyebrowScale"),
            new VanillaOption("vanilla.eyebrowThickness",      "eyebrows", "EyebrowThickness"),
            new VanillaOption("vanilla.eyebrowHeight",         "eyebrows", "EyebrowRestingHeight"),
            new VanillaOption("vanilla.eyebrowAngle",          "eyebrows", "EyebrowRestingAngle"),

            new VanillaOption("vanilla.top",                   "clothing", "Top"),
            new VanillaOption("vanilla.bottom",                "clothing", "Bottom"),
            new VanillaOption("vanilla.shoes",                 "clothing", "Shoes"),

            new VanillaOption("vanilla.finalizeText",          FinalizeTab, "Text (TMP)"),
            new VanillaOption(Done,                            FinalizeTab, "Done"),
        };

        private static readonly (string id, string name)[] TabOrder =
        {
            ("body", "Body"), ("hair", "Hair"), ("face", "Face"), ("eyes", "Eyes"),
            ("eyebrows", "Eyebrows"), ("clothing", "Clothing"), (FinalizeTab, "Finalize"),
        };

        /// <summary>The vanilla layout, rebuilt from the table above. Should look identical to the game.</summary>
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

        public static TabDefinition CreateFinalizeTab() =>
            new TabDefinition(FinalizeTab, "Finalize", "vanilla.finalizeText", Done);

        /// <summary>Test layout: a new custom tab, eyebrows merged into Eyes, clothing removed.</summary>
        public static LayoutDefinition CreateTestLayout() => new LayoutDefinition
        {
            Tabs = new List<TabDefinition>
            {
                new TabDefinition("body", "Body", "vanilla.gender", "vanilla.weight", "vanilla.skinColor"),
                new TabDefinition("hair", "Hair", "vanilla.hair", "vanilla.hairColor"),
                new TabDefinition("face", "Face", "vanilla.face", "vanilla.facialHair"),
                new TabDefinition("details", "Details", "vanilla.facialDetails", "vanilla.facialDetailIntensity"),
                new TabDefinition("eyes", "Eyes & Brows",
                    "vanilla.eyeballColor", "vanilla.pupilDilation", "vanilla.upperEyelid", "vanilla.lowerEyelid",
                    "vanilla.eyebrowScale", "vanilla.eyebrowThickness", "vanilla.eyebrowHeight", "vanilla.eyebrowAngle"),
                CreateFinalizeTab(),
            }
        };
    }
}
