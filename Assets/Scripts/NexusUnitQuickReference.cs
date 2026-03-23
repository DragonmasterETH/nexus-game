using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NexusGame
{
    /// <summary>Player-facing unit stats built from <see cref="NexusConfig"/> (stays in sync with the game).</summary>
    public static class NexusUnitQuickReference
    {
        public static string Title => "Units — Quick Reference";

        public static string Build(NexusConfig config)
        {
            if (config == null || config.UnitDefinitions == null || config.UnitDefinitions.Count == 0)
                config = NexusConfig.CreateDefault();

            var ordered = config.UnitDefinitions
                .OrderBy(u => BattleOrderIndex(u.Type))
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("RUBIUM INCOME (every unit type)");
            sb.AppendLine(
                "• Units do not produce Rubium. You collect Rubium at the start of your turn from mine values on hexes where you alone have pieces — your printed home-base mines (2 / 3 / 2 pattern) and extra mine bonuses from exploring.");
            sb.AppendLine();
            sb.AppendLine("Battle order (strongest fires first in combat): Dragon → Lava Leaper → Rock Strider → Crystalline → Fungoid → Human.");
            sb.AppendLine();

            foreach (var d in ordered)
            {
                AppendUnitBlock(sb, d);
                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }

        static int BattleOrderIndex(UnitType t)
        {
            for (var i = 0; i < BattleResolver.BattleOrder.Length; i++)
            {
                if (BattleResolver.BattleOrder[i] == t)
                    return i;
            }

            return 99;
        }

        static void AppendUnitBlock(StringBuilder sb, UnitDefinition d)
        {
            sb.AppendLine(DisplayName(d.Type) + "  —  Cost: " + d.Cost + " Rubium");
            sb.AppendLine("• Combat: " + d.AttackDice + "d6 per unit, each die hits on " + d.HitOnOrAbove + "+.");
            sb.AppendLine("• Move: up to " + d.MaxMoveDistance + " hex" + (d.MaxMoveDistance != 1 ? "es" : "") + " per turn.");

            if (d.Type == UnitType.RockStrider)
                sb.AppendLine("• Pathing: only Rock Striders may move through hexes containing enemies (when moving 2 hexes).");

            sb.AppendLine("• Terrain: " + TerrainSummary(d));

            if (d.Type == UnitType.RubiumDragon)
            {
                sb.AppendLine(
                    "• Dragon strike: after you press End Turn, if you control the dragon’s hex, it may shoot an adjacent hex that has enemies — roll 1d6, remove one enemy on a 4+.");
            }
        }

        static string TerrainSummary(UnitDefinition d)
        {
            var blocked = new List<string>();
            if (!d.CanEnterLava)
                blocked.Add("lava");
            if (!d.CanEnterMonolith)
                blocked.Add("monolith");

            if (blocked.Count == 0)
                return "May enter plains, forest, crystal, lava, rock, and monolith.";

            return "May enter plains, forest, crystal, rock" +
                   (d.CanEnterLava ? ", lava" : "") +
                   (d.CanEnterMonolith ? ", monolith" : "") +
                   "; cannot enter " + string.Join(" or ", blocked) + ".";
        }

        public static string DisplayName(UnitType t)
        {
            return t switch
            {
                UnitType.RockStrider => "Rock Strider",
                UnitType.LavaLeaper => "Lava Leaper",
                UnitType.RubiumDragon => "Rubium Dragon",
                _ => t.ToString()
            };
        }
    }
}
