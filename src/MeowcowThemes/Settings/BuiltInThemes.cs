namespace MeowcowThemes.Settings
{
    /// <summary>Themes shipped with MeowcowThemes. Same format as a theme file.</summary>
    internal static class BuiltInThemes
    {
        /// <summary>The Meowcow suite's own look.</summary>
        internal static readonly Theme Meowcow = Theme.Parse(@"
{
  ""name"": ""Meowcow"",
  ""values"": {
    ""mainMenu"":         { ""shineLength"": 2, ""textColor"": ""#FF66CC"", ""shineColor"": ""#1BD11B"" },
    ""mainMenu.header"":  { ""anchor"": ""TopLeft"", ""y"": 150, ""scale"": 2, ""color"": ""#1BD11B"" },
    ""mainMenu.version"": { ""anchor"": ""BottomRight"", ""y"": 0 },
    ""mainMenu.list"":    { ""anchor"": ""Center"", ""scale"": 2, ""textAlign"": ""Center"" },
    ""mainMenu.socials"": { ""color"": ""#FF66CC"" },
    ""mainMenu.lobby"":   { ""slotColor"": ""#FF66CC"" },
    ""mainMenu.label"":   { ""anchor"": ""TopLeft"", ""x"": 390, ""y"": 305, ""scale"": 1 }
  }
}");
    }
}
