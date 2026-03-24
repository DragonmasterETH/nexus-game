namespace NexusGame
{
    /// <summary>Player-facing rules text for the main menu rulebook (matches in-game systems).</summary>
    public static class NexusRulebook
    {
        public const string Title = "How to Play — Nexus Ops";

        public static readonly string Body =
            "GOAL\n" +
            "• Expand from your home bases, collect Rubium, win battles, and score Victory Points (VP). " +
            "In friendly games, agree on a VP target or play to a time limit.\n\n" +

            "YOUR TURN (summary)\n" +
            "• Draw phase: you gain 1 Secret Mission. If you alone control the Monolith, you also draw extra Energize cards.\n" +
            "• Your units can move again (each unit moves once per turn).\n" +
            "• Mining: printed mines on home bases and bonus mines from exploration pay Rubium if you alone occupy that hex.\n" +
            "• Battle phase: if you share any hex with an enemy, you fight those battles (see Battles).\n" +
            "• Then move and buy (see Movement & Buying). End Turn when ready.\n\n" +

            "END TURN — DRAGON\n" +
            "• After you press End Turn, each Rubium Dragon you control on a hex you control may take a ranged shot " +
            "at an adjacent hex that contains enemies (1d6, hit on 4+). On a hit, the striking player chooses one enemy unit " +
            "on that target hex to remove. Then the next player's turn begins.\n\n" +

            "MOVEMENT\n" +
            "• Tap a hex to select it; use the tile panel to choose how many of each unit type to move, then tap the destination. " +
            "You can also drag from a unit to a hex to move that piece.\n" +
            "• Units move 1 hex (Rock Strider: up to 2). Terrain must be legal for that unit (lava, crystal, forest, etc.).\n" +
            "• You normally cannot move through enemy-occupied hexes (Rock Strider is an exception for multi-step moves).\n" +
            "• Stepping on unexplored hexes can reveal bonuses: free units, mine income, or both.\n\n" +

            "BUYING UNITS & DEPLOYMENT ENERGIZE\n" +
            "• Select one of your home-base hexes, then press $ to open the buy menu. Costs are paid in Rubium.\n" +
            "• Deployment Energize cards (bottom strip) are played here: extra Rubium, draws, discounts, or a free Human on a home hex.\n\n" +

            "BATTLES\n" +
            "• When your turn starts, each hex where you and an enemy both have units becomes a battle. You set battle order and pick " +
            "which enemy is the defender when several share a hex.\n" +
            "• Before dice, players take turns playing Battle Energize cards (or passing). Cards can add dice, change hit thresholds, " +
            "protect with Aegis, or set Focus Fire on one of your unit types for +2 dice.\n" +
            "• Combat steps go in order from strongest type to weakest: Rubium Dragon → Lava Leaper → Rock Strider → Crystalline → " +
            "Fungoid → Human. For each step, every surviving unit of that type on both sides rolls its attack dice once (d6). " +
            "Each die that meets the unit's hit threshold counts as one hit.\n" +
            "• The defender's hits are applied to your units first (you choose casualties among legal targets); then your hits to theirs. " +
            "Weakest units are a good default when choosing losses.\n" +
            "• If all defender units in that battle are eliminated, you win the battle and gain 1 VP. The defender draws an Energize card. " +
            "You may be able to score a Secret Mission that matches the battle.\n\n" +

            "SECRET MISSIONS\n" +
            "• One-shot cards with conditions (often battle-related). When you fulfill one after a win, you may play it for bonus VP " +
            "or skip to keep the card.\n\n" +

            "BOTTOM CARD STRIP\n" +
            "• Shows your Battle Energize, Deployment Energize, and Secret Missions. Scroll sideways if you have many.\n\n" +

            "MODES\n" +
            "• Play — hotseat / pass-and-play on one device.\n" +
            "• Play vs AI — you are Player 1; the AI is Player 2.\n" +
            "• AI vs AI (test) — watch two AIs play; match ends at the VP target shown in the HUD.\n\n" +

            "This digital version is inspired by Nexus Ops–style area control; exact tournament rules may differ.";
    }
}
