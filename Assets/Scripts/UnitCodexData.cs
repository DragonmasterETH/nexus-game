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
                UnitType.Human => "Cheap generalist. 1 attack die, hits on 5+. Cannot enter lava; cannot enter Monolith.",
                UnitType.Fungoid => "2 attack dice, hits on 5+. Full terrain access except Monolith.",
                UnitType.Crystalline => "2 attack dice, hits on 4+. Strong baseline attacker.",
                UnitType.RockStrider => "3 attack dice; can move 2 hexes; can enter Monolith.",
                UnitType.LavaLeaper => "3 attack dice, hits on 4+. Elite mobility + Monolith access.",
                UnitType.RubiumDragon => "4 attack dice; expensive. Dragon strike phase at end of turn.",
                _ => "No description."
            };
    }
}
