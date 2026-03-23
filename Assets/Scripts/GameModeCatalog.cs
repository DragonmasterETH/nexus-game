namespace NexusGame
{
    /// <summary>
    /// Placeholder catalog for secondary modes (data only; gameplay not wired yet).
    /// </summary>
    public enum SecondaryGameModeId
    {
        Standard = 0,
        /// <summary>Example: alternate win / setup — TBD.</summary>
        Skirmish = 1,
        /// <summary>Example: tutorial / puzzle — TBD.</summary>
        Tutorial = 2
    }

    public static class GameModeCatalog
    {
        public static string GetName(SecondaryGameModeId id) =>
            id switch
            {
                SecondaryGameModeId.Standard => "Standard (current rules)",
                SecondaryGameModeId.Skirmish => "Skirmish (not implemented)",
                SecondaryGameModeId.Tutorial => "Tutorial (not implemented)",
                _ => id.ToString()
            };
    }
}
