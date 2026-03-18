using System;
using System.Collections.Generic;
using UnityEngine;

namespace NexusGame
{
    public enum TileType
    {
        HomeBase,
        Monolith,
        Plains,
        Forest,
        CrystalField,
        Lava,
        Rock
    }

    public enum UnitType
    {
        Human,
        Fungoid,
        Crystalline,
        RockStrider,
        LavaLeaper,
        RubiumDragon
    }

    public enum ExplorationReward
    {
        None,
        FreeHuman,
        FreeFungoid,
        FreeRockStrider,
        Mine1,
        Mine2,
        Mine3,
        FreeHumanAndMine2
    }

    [Serializable]
    public class UnitDefinition
    {
        public UnitType Type;
        public int Cost;
        public int AttackDice;
        [Range(1, 6)]
        public int HitOnOrAbove;
        [Header("Movement")]
        public int MaxMoveDistance = 1;
        public bool CanEnterPlains = true;
        public bool CanEnterForest = true;
        public bool CanEnterCrystal = true;
        public bool CanEnterLava = true;
        public bool CanEnterRock = true;
        public bool CanEnterMonolith = true;
    }

    [Serializable]
    public class TileDefinition
    {
        public TileType Type;
        public int RubiumYield;
        public Color Color;
    }

    [CreateAssetMenu(fileName = "NexusConfig", menuName = "Nexus Ops/Config")]
    public class NexusConfig : ScriptableObject
    {
        [Header("Tiles")]
        public List<TileDefinition> TileDefinitions = new List<TileDefinition>();

        [Header("Units")]
        public List<UnitDefinition> UnitDefinitions = new List<UnitDefinition>();

        public static NexusConfig CreateDefault()
        {
            var config = CreateInstance<NexusConfig>();

            // Base terrain tiles no longer give Rubium by themselves; all income comes from mines
            // (home-base printed mines and exploration-discovered mines).
            config.TileDefinitions = new List<TileDefinition>
            {
                new TileDefinition { Type = TileType.HomeBase, RubiumYield = 0, Color = Color.white },
                new TileDefinition { Type = TileType.Monolith, RubiumYield = 0, Color = new Color(0.5f, 0.2f, 0.7f) },
                new TileDefinition { Type = TileType.Plains, RubiumYield = 0, Color = new Color(0.8f, 0.8f, 0.8f) },
                new TileDefinition { Type = TileType.Forest, RubiumYield = 0, Color = new Color(0.1f, 0.5f, 0.1f) },
                new TileDefinition { Type = TileType.CrystalField, RubiumYield = 0, Color = new Color(0.2f, 0.8f, 0.9f) },
                new TileDefinition { Type = TileType.Lava, RubiumYield = 0, Color = new Color(0.9f, 0.3f, 0.0f) },
                new TileDefinition { Type = TileType.Rock, RubiumYield = 0, Color = new Color(0.4f, 0.4f, 0.4f) }
            };

            config.UnitDefinitions = new List<UnitDefinition>
            {
                new UnitDefinition
                {
                    Type = UnitType.Human,
                    Cost = 1,
                    AttackDice = 1,
                    HitOnOrAbove = 5,
                    MaxMoveDistance = 1,
                    CanEnterPlains = true,
                    CanEnterForest = true,
                    CanEnterCrystal = true,
                    CanEnterLava = false,      // cannot enter Magma Pool
                    CanEnterRock = true,
                    CanEnterMonolith = false   // cannot enter Monolith
                },
                new UnitDefinition
                {
                    Type = UnitType.Fungoid,
                    Cost = 2,
                    AttackDice = 2,
                    HitOnOrAbove = 5,
                    MaxMoveDistance = 1,
                    CanEnterPlains = true,
                    CanEnterForest = true,
                    CanEnterCrystal = true,
                    CanEnterLava = true,
                    CanEnterRock = true,
                    CanEnterMonolith = false   // cannot enter Monolith
                },
                new UnitDefinition
                {
                    Type = UnitType.Crystalline,
                    Cost = 2,
                    AttackDice = 2,
                    HitOnOrAbove = 4,
                    MaxMoveDistance = 1,
                    CanEnterPlains = true,
                    CanEnterForest = true,
                    CanEnterCrystal = true,
                    CanEnterLava = true,
                    CanEnterRock = true,
                    CanEnterMonolith = false   // cannot enter Monolith
                },
                new UnitDefinition
                {
                    Type = UnitType.RockStrider,
                    Cost = 3,
                    AttackDice = 3,
                    HitOnOrAbove = 5,
                    MaxMoveDistance = 2,
                    CanEnterPlains = true,
                    CanEnterForest = true,
                    CanEnterCrystal = true,
                    CanEnterLava = true,
                    CanEnterRock = true,
                    CanEnterMonolith = true    // allowed in Monolith
                },
                new UnitDefinition
                {
                    Type = UnitType.LavaLeaper,
                    Cost = 4,
                    AttackDice = 3,
                    HitOnOrAbove = 4,
                    MaxMoveDistance = 1,
                    CanEnterPlains = true,
                    CanEnterForest = true,
                    CanEnterCrystal = true,
                    CanEnterLava = true,
                    CanEnterRock = true,
                    CanEnterMonolith = true    // allowed in Monolith
                },
                new UnitDefinition
                {
                    Type = UnitType.RubiumDragon,
                    Cost = 8,
                    AttackDice = 4,
                    HitOnOrAbove = 4,
                    MaxMoveDistance = 1,
                    CanEnterPlains = true,
                    CanEnterForest = true,
                    CanEnterCrystal = true,
                    CanEnterLava = true,
                    CanEnterRock = true,
                    CanEnterMonolith = true    // allowed in Monolith
                }
            };

            return config;
        }

        public TileDefinition GetTile(TileType type)
        {
            return TileDefinitions.Find(t => t.Type == type);
        }

        public UnitDefinition GetUnit(UnitType type)
        {
            return UnitDefinitions.Find(u => u.Type == type);
        }
    }

    [Serializable]
    public class PlayerState
    {
        public int PlayerIndex;
        public Color Color;
        public int Rubium;
        public int VictoryPoints;

        [NonSerialized]
        public List<EnergizeBattleId> BattleEnergize = new List<EnergizeBattleId>();

        [NonSerialized]
        public List<EnergizeDeploymentId> DeployEnergize = new List<EnergizeDeploymentId>();

        /// <summary>Rubium discounted from next deployment purchase (min cost 1).</summary>
        [NonSerialized]
        public int DeploymentPurchaseDiscountRubium;

        [NonSerialized]
        public List<SecretMissionInHand> SecretMissions = new List<SecretMissionInHand>();
    }
}

