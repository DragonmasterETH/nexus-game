using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace NexusGame
{
    /// <summary>
    /// Nexus Ops battle rules (see rulebook Battle section):
    /// - Battle order (strongest first): Rubium Dragon → Lava Leaper → Rock Strider → Crystalline → Fungoid → Human.
    /// - For each step, only units of that type that are still alive may roll; each unit rolls once using AttackDice d6 (Energize adds extra dice).
    /// - Attacker removes casualties from defender's hits first, then defender removes casualties from attacker's hits.
    /// - Only the active player (attacker) and chosen defender fight; other factions in the hex do not roll or take hits.
    /// - Attacker wins if all defender units in the battle are destroyed (including mutual destruction). +1 VP.
    /// </summary>
    public static class BattleResolver
    {
        /// <summary>Right-to-left on reference sheet: strongest attacks first.</summary>
        public static readonly UnitType[] BattleOrder =
        {
            UnitType.RubiumDragon,
            UnitType.LavaLeaper,
            UnitType.RockStrider,
            UnitType.Crystalline,
            UnitType.Fungoid,
            UnitType.Human
        };

        public sealed class BattleResult
        {
            public bool AttackerEliminatedDefender;
            public int VictoryPointsAwarded;
            public readonly List<string> LogLines = new List<string>();
        }

        /// <summary>
        /// Roll one unit's attack; each die >= effective threshold counts as one hit (d6).
        /// </summary>
        public static int RollHitsForUnit(UnitDefinition def, System.Random rng)
        {
            return RollHitsForUnit(def, rng, 0, 0);
        }

        public struct DiceRollResult
        {
            public int Dice;
            public int Need;
            public bool ImpossibleToHit;
            public int Hits;
            public List<int> Rolls;
        }

        /// <summary>
        /// Roll one unit's attack dice and return per-die outcomes.
        /// </summary>
        public static DiceRollResult RollDiceForUnit(
            UnitDefinition def,
            System.Random rng,
            int extraDice,
            int thresholdShift)
        {
            var result = new DiceRollResult
            {
                Dice = 0,
                Need = 0,
                ImpossibleToHit = false,
                Hits = 0,
                Rolls = new List<int>()
            };

            if (def == null)
                return result;

            int dice = Mathf.Max(0, def.AttackDice + extraDice);
            result.Dice = dice;

            if (dice <= 0)
                return result;

            int need = def.HitOnOrAbove + thresholdShift;
            result.Need = need;

            if (need > 6)
            {
                // Preserve legacy behavior: if it's impossible to hit, we don't roll.
                result.ImpossibleToHit = true;
                return result;
            }

            need = Mathf.Max(2, need);
            result.Need = need;

            int hits = 0;
            for (int i = 0; i < dice; i++)
            {
                int roll = rng.Next(1, 7);
                result.Rolls.Add(roll);
                if (roll >= need)
                    hits++;
            }

            result.Hits = hits;
            return result;
        }

        /// <param name="extraDice">Bonus dice (Energize, etc.).</param>
        /// <param name="thresholdShift">Positive = harder to hit (need higher roll); negative = easier.</param>
        public static int RollHitsForUnit(UnitDefinition def, System.Random rng, int extraDice, int thresholdShift)
        {
            return RollDiceForUnit(def, rng, extraDice, thresholdShift).Hits;
        }

        /// <summary>
        /// Picks casualties: remove weakest units first (Human before Dragon) so elites survive — common AI/table default.
        /// </summary>
        public static List<UnitInstance> PickCasualtiesWeakestFirst(List<UnitInstance> pool, int hitCount)
        {
            var toRemove = new List<UnitInstance>();
            if (hitCount <= 0 || pool == null || pool.Count == 0)
                return toRemove;

            var ordered = new List<UnitInstance>(pool);
            ordered.Sort(CompareWeakestFirst);

            for (int i = 0; i < hitCount && i < ordered.Count; i++)
                toRemove.Add(ordered[i]);

            return toRemove;
        }

        static int BattleOrderIndex(UnitType t)
        {
            for (int i = 0; i < BattleOrder.Length; i++)
            {
                if (BattleOrder[i] == t)
                    return i;
            }

            return BattleOrder.Length;
        }

        /// <summary>Weakest (attacks last) removed first.</summary>
        static int CompareWeakestFirst(UnitInstance a, UnitInstance b)
        {
            int ia = BattleOrderIndex(a.Definition.Type);
            int ib = BattleOrderIndex(b.Definition.Type);
            // Higher index = weaker in battle order → remove first
            int c = ib.CompareTo(ia);
            if (c != 0)
                return c;
            return a.GetInstanceID().CompareTo(b.GetInstanceID());
        }

        static List<UnitInstance> ParticipantsOnHex(BoardTile hex, PlayerState attacker, PlayerState defender)
        {
            var list = new List<UnitInstance>();
            foreach (var u in UnityEngine.Object.FindObjectsOfType<UnitInstance>())
            {
                if (u == null || u.Tile != hex)
                    continue;
                if (u.Owner == attacker || u.Owner == defender)
                    list.Add(u);
            }

            return list;
        }

        /// <summary>
        /// All enemy players with units on this hex (excluding attacker). Used to pick defender when 2+ opponents.
        /// </summary>
        public static List<PlayerState> OpponentsOnHex(BoardTile hex, PlayerState attacker)
        {
            var set = new HashSet<PlayerState>();
            foreach (var u in UnityEngine.Object.FindObjectsOfType<UnitInstance>())
            {
                if (u == null || u.Tile != hex || u.Owner == attacker)
                    continue;
                set.Add(u.Owner);
            }

            var list = new List<PlayerState>(set);
            list.Sort((a, b) => a.PlayerIndex.CompareTo(b.PlayerIndex));
            return list;
        }

        /// <summary>
        /// Resolve one battle. Mutates the board by destroying units via <paramref name="destroyUnit"/>.
        /// </summary>
        public static BattleResult ResolveBattle(
            BoardTile hex,
            PlayerState attacker,
            PlayerState defender,
            NexusConfig config,
            System.Random rng,
            Action<UnitInstance> destroyUnit)
        {
            var result = new BattleResult();
            var log = new StringBuilder();

            if (hex == null || attacker == null || defender == null || config == null || destroyUnit == null)
            {
                result.LogLines.Add("Battle aborted: invalid parameters.");
                return result;
            }

            if (attacker == defender)
            {
                result.LogLines.Add("Battle aborted: attacker and defender are the same.");
                return result;
            }

            log.AppendLine($"Battle at ({hex.Q},{hex.R}): P{attacker.PlayerIndex + 1} attacks P{defender.PlayerIndex + 1}");

            var aliveAtt = new List<UnitInstance>();
            var aliveDef = new List<UnitInstance>();

            void RefreshPools()
            {
                aliveAtt.Clear();
                aliveDef.Clear();
                foreach (var u in ParticipantsOnHex(hex, attacker, defender))
                {
                    if (u == null)
                        continue;
                    if (u.Owner == attacker)
                        aliveAtt.Add(u);
                    else if (u.Owner == defender)
                        aliveDef.Add(u);
                }
            }

            RefreshPools();

            if (aliveAtt.Count == 0 || aliveDef.Count == 0)
            {
                result.LogLines.Add("No battle: missing attacker or defender units.");
                return result;
            }

            foreach (var unitType in BattleOrder)
            {
                RefreshPools();
                if (aliveDef.Count == 0)
                {
                    log.AppendLine("Defender eliminated — battle ends.");
                    break;
                }

                if (aliveAtt.Count == 0)
                {
                    log.AppendLine("Attacker has no units left — battle lost.");
                    break;
                }

                var attOfType = aliveAtt.FindAll(u => u.Definition.Type == unitType);
                var defOfType = aliveDef.FindAll(u => u.Definition.Type == unitType);

                if (attOfType.Count == 0 && defOfType.Count == 0)
                    continue;

                int hitsOnAttacker = 0;
                foreach (var u in defOfType)
                {
                    var roll = RollDiceForUnit(u.Definition, rng, 0, 0);
                    hitsOnAttacker += roll.Hits;
                    if (roll.Dice > 0 && roll.Rolls != null && roll.Rolls.Count > 0)
                        log.AppendLine($"  {unitType} (def P{defender.PlayerIndex + 1}): rolled {roll.Dice}d6 [{string.Join(",", roll.Rolls)}], need >= {roll.Need} => {roll.Hits} hit(s)");
                    else if (roll.Dice > 0 && roll.ImpossibleToHit)
                        log.AppendLine($"  {unitType} (def P{defender.PlayerIndex + 1}): {roll.Dice}d6, need >= {roll.Need} (impossible) => 0 hit(s)");
                    else
                        log.AppendLine($"  {unitType} (def P{defender.PlayerIndex + 1}): {roll.Dice} dice => 0 hit(s)");
                }

                RefreshPools();
                int capAtt = Mathf.Min(hitsOnAttacker, aliveAtt.Count);
                if (capAtt > 0)
                {
                    var victims = PickCasualtiesWeakestFirst(aliveAtt, capAtt);
                    foreach (var v in victims)
                    {
                        log.AppendLine($"    → P{attacker.PlayerIndex + 1} dies: {v.Definition.Type}");
                        destroyUnit(v);
                    }
                }

                RefreshPools();
                if (aliveAtt.Count == 0)
                {
                    log.AppendLine("Attacker has no units left — battle lost.");
                    break;
                }

                attOfType = aliveAtt.FindAll(u => u.Definition.Type == unitType);

                int hitsOnDefender = 0;
                foreach (var u in attOfType)
                {
                    var roll = RollDiceForUnit(u.Definition, rng, 0, 0);
                    hitsOnDefender += roll.Hits;
                    if (roll.Dice > 0 && roll.Rolls != null && roll.Rolls.Count > 0)
                        log.AppendLine($"  {unitType} (atk P{attacker.PlayerIndex + 1}): rolled {roll.Dice}d6 [{string.Join(",", roll.Rolls)}], need >= {roll.Need} => {roll.Hits} hit(s)");
                    else if (roll.Dice > 0 && roll.ImpossibleToHit)
                        log.AppendLine($"  {unitType} (atk P{attacker.PlayerIndex + 1}): {roll.Dice}d6, need >= {roll.Need} (impossible) => 0 hit(s)");
                    else
                        log.AppendLine($"  {unitType} (atk P{attacker.PlayerIndex + 1}): {roll.Dice} dice => 0 hit(s)");
                }

                RefreshPools();
                int capDef = Mathf.Min(hitsOnDefender, aliveDef.Count);
                if (capDef > 0)
                {
                    var victims = PickCasualtiesWeakestFirst(aliveDef, capDef);
                    foreach (var v in victims)
                    {
                        log.AppendLine($"    → P{defender.PlayerIndex + 1} dies: {v.Definition.Type}");
                        destroyUnit(v);
                    }
                }
            }

            RefreshPools();
            bool defWiped = aliveDef.Count == 0;
            bool attWiped = aliveAtt.Count == 0;

            // Mutual destruction: attacker still wins (rulebook).
            if (defWiped)
            {
                result.AttackerEliminatedDefender = true;
                result.VictoryPointsAwarded = 1;
                if (attWiped)
                    log.AppendLine("Mutual destruction — attacker wins (+1 VP).");
                else
                    log.AppendLine($"P{attacker.PlayerIndex + 1} wins the battle (+1 VP).");
            }
            else
            {
                log.AppendLine("Battle ends — defender still has units in the hex.");
            }

            foreach (var line in log.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                result.LogLines.Add(line);

            return result;
        }

        /// <summary>
        /// Contested hexes where <paramref name="attacker"/> has at least one unit and at least one other player has units.
        /// </summary>
        public static List<BoardTile> FindContestedHexesForAttacker(PlayerState attacker)
        {
            var hexes = new HashSet<BoardTile>();
            foreach (var u in UnityEngine.Object.FindObjectsOfType<UnitInstance>())
            {
                if (u == null || u.Owner != attacker || u.Tile == null)
                    continue;
                var opponents = OpponentsOnHex(u.Tile, attacker);
                if (opponents.Count > 0)
                    hexes.Add(u.Tile);
            }

            return new List<BoardTile>(hexes);
        }
    }
}
