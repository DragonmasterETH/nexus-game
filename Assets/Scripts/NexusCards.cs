using System;
using System.Collections.Generic;
using UnityEngine;

namespace NexusGame
{
    /// <summary>Energize cards playable during Battle phase (before dice).</summary>
    public enum EnergizeBattleId
    {
        None = 0,
        /// <summary>+1 die on every attack roll you make this battle.</summary>
        BattleFury = 1,
        /// <summary>Enemies need +1 on each die to hit your units this battle.</summary>
        Elusive = 2,
        /// <summary>Your attacks treat hit threshold as 1 lower (min 2).</summary>
        DeadlyAim = 3,
        /// <summary>Ignore the first hit scored against your units this battle.</summary>
        Aegis = 4,
        /// <summary>+2 dice on attack rolls for one unit type you choose (prompt after play).</summary>
        FocusFire = 5,
        /// <summary>Draw 1 Energize card (end of battle or now — we draw immediately).</summary>
        BattleCache = 6
    }

    public enum SecretMissionKind
    {
        Battle,
        Objective
    }

    [Serializable]
    public class SecretMissionInHand
    {
        public int InstanceId;
        public SecretMissionKind Kind;
        /// <summary>VP when played after fulfillment.</summary>
        public int VictoryPoints;
        /// <summary>Mission type id for fulfillment checks.</summary>
        public int MissionTypeId;
    }

    /// <summary>Built-in mission types for Battle phase.</summary>
    public static class SecretMissionTypes
    {
        public const int WinAnyBattle = 1;
        public const int WinBattleKillTwoPlus = 2;
        public const int WinBattleEnemyLostDragon = 3;
    }

    public static class EnergizeBattleCatalog
    {
        public static string GetName(EnergizeBattleId id)
        {
            return id switch
            {
                EnergizeBattleId.BattleFury => "Battle Fury (+1 die, your attacks)",
                EnergizeBattleId.Elusive => "Elusive (+1 to hit vs your units)",
                EnergizeBattleId.DeadlyAim => "Deadly Aim (hit on threshold-1)",
                EnergizeBattleId.Aegis => "Aegis (ignore 1st hit vs you)",
                EnergizeBattleId.FocusFire => "Focus Fire (+2 dice, one type)",
                EnergizeBattleId.BattleCache => "Battle Cache (draw 1 Energize)",
                _ => id.ToString()
            };
        }

        /// <summary>Full rules text for UI tooltips / help popup.</summary>
        public static string GetDescription(EnergizeBattleId id)
        {
            return id switch
            {
                EnergizeBattleId.BattleFury =>
                    "During this battle, each of your units rolls one extra attack die on every attack step where they participate.",
                EnergizeBattleId.Elusive =>
                    "During this battle, enemy attacks against your units need +1 on each d6 to count as a hit (harder for them to hit you).",
                EnergizeBattleId.DeadlyAim =>
                    "During this battle, your attacks treat the hit threshold as 1 lower than normal (minimum 2 on the die).",
                EnergizeBattleId.Aegis =>
                    "During this battle, the first hit that would be applied to your units is ignored.",
                EnergizeBattleId.FocusFire =>
                    "After you play this card, you choose one unit type present in this battle; that type rolls +2 extra attack dice for the rest of the fight.",
                EnergizeBattleId.BattleCache =>
                    "When played, you immediately draw one card from the Energize deck (battle or deployment, depending on the draw).",
                _ => ""
            };
        }

        public static bool AppliesDuringBattle(EnergizeBattleId id) => id != EnergizeBattleId.None;
    }

    /// <summary>Energize cards playable only during Deployment (buy units on home base).</summary>
    public enum EnergizeDeploymentId
    {
        None = 0,
        /// <summary>Gain 2 rubium.</summary>
        StripMine = 1,
        /// <summary>Draw 1 Energize card.</summary>
        Convoy = 2,
        /// <summary>Next unit purchase this turn costs up to 2 less rubium (min 1).</summary>
        RushOrder = 3,
        /// <summary>Place 1 Human on a home-base hex you select (free).</summary>
        FreeHuman = 4,
        /// <summary>Gain 1 rubium and draw 1 Energize.</summary>
        SupplyRun = 5
    }

    public static class EnergizeDeploymentCatalog
    {
        public static string GetName(EnergizeDeploymentId id)
        {
            return id switch
            {
                EnergizeDeploymentId.StripMine => "Strip Mine (+2 Rubium)",
                EnergizeDeploymentId.Convoy => "Convoy (draw 1 Energize)",
                EnergizeDeploymentId.RushOrder => "Rush Order (-2 next buy, min 1)",
                EnergizeDeploymentId.FreeHuman => "Free Human (deploy on home hex)",
                EnergizeDeploymentId.SupplyRun => "Supply Run (+1 Rubium, draw 1)",
                _ => id.ToString()
            };
        }

        /// <summary>Full rules text for UI tooltips / help popup.</summary>
        public static string GetDescription(EnergizeDeploymentId id)
        {
            return id switch
            {
                EnergizeDeploymentId.StripMine =>
                    "Gain 2 Rubium immediately. Usable during Deployment (buy phase) on your turn.",
                EnergizeDeploymentId.Convoy =>
                    "Draw 1 card from the Energize deck immediately (may be a battle or deployment card).",
                EnergizeDeploymentId.RushOrder =>
                    "The next unit you purchase this Deployment phase costs up to 2 less Rubium (you still pay at least 1).",
                EnergizeDeploymentId.FreeHuman =>
                    "Place 1 Human on a home-base hex you control for free (select the hex, then play this card). Humans cannot be placed on invalid terrain.",
                EnergizeDeploymentId.SupplyRun =>
                    "Gain 1 Rubium and draw 1 Energize card from the deck.",
                _ => ""
            };
        }
    }

    /// <summary>Single draw from the mixed Energize deck.</summary>
    public struct UnifiedEnergizeDraw
    {
        public bool IsDeployment;
        public EnergizeBattleId Battle;
        public EnergizeDeploymentId Deploy;
    }

    /// <summary>Per-battle modifiers from Energize; cleared each fight.</summary>
    public class BattleEnergizeModifiers
    {
        public int AttackerDiceBonus;
        public int DefenderDiceBonus;
        /// <summary>Added to hit threshold when attacker rolls vs defender (harder = positive).</summary>
        public int HitThresholdBonusWhenAttackingDefender;
        /// <summary>Added to threshold when defender rolls vs attacker.</summary>
        public int HitThresholdBonusWhenAttackingAttacker;
        public int AttackerHitThresholdReduction;
        public int DefenderHitThresholdReduction;
        public bool AttackerIgnoresNextHit;
        public bool DefenderIgnoresNextHit;
        public UnitType? AttackerFocusFireType;
        public int AttackerFocusFireExtraDice;
        public UnitType? DefenderFocusFireType;
        public int DefenderFocusFireExtraDice;
    }

    public static class CardDecks
    {
        static void Shuffle<T>(IList<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        public static Queue<UnifiedEnergizeDraw> BuildUnifiedEnergizeDeck(System.Random rng)
        {
            var list = new List<UnifiedEnergizeDraw>();

            void AddBattle(EnergizeBattleId b) =>
                list.Add(new UnifiedEnergizeDraw { IsDeployment = false, Battle = b });

            void AddDeploy(EnergizeDeploymentId d) =>
                list.Add(new UnifiedEnergizeDraw { IsDeployment = true, Deploy = d });

            for (int i = 0; i < 4; i++) AddBattle(EnergizeBattleId.BattleFury);
            for (int i = 0; i < 3; i++) AddBattle(EnergizeBattleId.Elusive);
            for (int i = 0; i < 3; i++) AddBattle(EnergizeBattleId.DeadlyAim);
            for (int i = 0; i < 3; i++) AddBattle(EnergizeBattleId.Aegis);
            for (int i = 0; i < 2; i++) AddBattle(EnergizeBattleId.FocusFire);
            for (int i = 0; i < 3; i++) AddBattle(EnergizeBattleId.BattleCache);

            for (int i = 0; i < 6; i++) AddDeploy(EnergizeDeploymentId.StripMine);
            for (int i = 0; i < 5; i++) AddDeploy(EnergizeDeploymentId.Convoy);
            for (int i = 0; i < 5; i++) AddDeploy(EnergizeDeploymentId.RushOrder);
            for (int i = 0; i < 4; i++) AddDeploy(EnergizeDeploymentId.FreeHuman);
            for (int i = 0; i < 4; i++) AddDeploy(EnergizeDeploymentId.SupplyRun);

            Shuffle(list, rng);
            var q = new Queue<UnifiedEnergizeDraw>();
            foreach (var x in list)
                q.Enqueue(x);
            return q;
        }

        public static Queue<SecretMissionInHand> BuildSecretDeck(System.Random rng, ref int nextInstanceId)
        {
            var list = new List<SecretMissionInHand>();
            int inst = nextInstanceId;
            void AddBattle(int typeId, int vp, int count)
            {
                for (int c = 0; c < count; c++)
                {
                    list.Add(new SecretMissionInHand
                    {
                        InstanceId = inst++,
                        Kind = SecretMissionKind.Battle,
                        VictoryPoints = vp,
                        MissionTypeId = typeId
                    });
                }
            }

            AddBattle(SecretMissionTypes.WinAnyBattle, 1, 8);
            AddBattle(SecretMissionTypes.WinBattleKillTwoPlus, 2, 4);
            AddBattle(SecretMissionTypes.WinBattleEnemyLostDragon, 2, 4);
            nextInstanceId = inst;
            Shuffle(list, rng);
            var q = new Queue<SecretMissionInHand>();
            foreach (var x in list) q.Enqueue(x);
            return q;
        }
    }
}
