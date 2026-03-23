namespace NexusGame
{
    /// <summary>
    /// Short unit blurbs for the in-game codex (bottom sheet). Expand with real rulebook text later.
    /// </summary>
    public static class UnitCodexData
    {
        public static string GetBody(UnitType t) =>
            t switch
            {
                UnitType.Human => "1 attack die (Energize can add more), hits on 5+. Cannot enter lava; cannot enter Monolith.",
                UnitType.Fungoid => "1 attack die, hits on 5+. Full terrain access except Monolith.",
                UnitType.Crystalline => "1 attack die, hits on 4+. Stronger hit threshold.",
                UnitType.RockStrider => "1 attack die; 2 hex move; can enter Monolith.",
                UnitType.LavaLeaper => "1 attack die, hits on 4+. Monolith access.",
                UnitType.RubiumDragon => "1 attack die in battle, hits on 4+. Rubium Dragon ranged strike at end of turn (separate roll).",
                _ => "No description."
            };
    }
}
