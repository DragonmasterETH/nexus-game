namespace NexusGame
{
    /// <summary>
    /// Reference text for Monolith rules. Gameplay hook: <see cref="GameController.BattleFlow"/> draw phase
    /// (<c>PlayerControlsMonolithAlone</c> → extra Energize draws).
    /// </summary>
    public static class MonolithRulesDoc
    {
        public const string ShortSummary =
            "If you alone occupy the Monolith hex at the start of your turn, you draw 2 Energize cards during the draw phase " +
            "(in addition to your normal Secret draw).";

        public const string ImplementationNote =
            "Implemented in code path: RunDrawPhase → PlayerControlsMonolithAlone → DrawEnergizeCards(player, 2).";
    }
}
