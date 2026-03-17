using System.Collections.Generic;
using UnityEngine;

namespace NexusGame
{
    public class GameController : MonoBehaviour
    {
        [Header("Scene References")]
        public BoardGenerator Board;
        public NexusConfig Config;

        [Header("Players")]
        public List<PlayerState> Players = new List<PlayerState>();
        public int StartingRubium = 10;

        int _currentPlayerIndex;
        readonly Dictionary<PlayerState, List<UnitInstance>> _unitsByPlayer =
            new Dictionary<PlayerState, List<UnitInstance>>();

        void Start()
        {
            if (Board == null)
            {
                Board = FindObjectOfType<BoardGenerator>();
            }

            if (Config == null && Board != null)
            {
                Config = Board.Config;
            }

            if (Board == null || Config == null)
            {
                Debug.LogError("GameController is missing Board or Config.");
                return;
            }

            InitPlayers();
            BeginTurn();
            SpawnStartingUnits();
        }

        void InitPlayers()
        {
            if (Players.Count == 0)
            {
                Players.Add(new PlayerState { PlayerIndex = 0, Color = Color.blue, Rubium = StartingRubium });
                Players.Add(new PlayerState { PlayerIndex = 1, Color = Color.red, Rubium = StartingRubium });
            }

            foreach (var p in Players)
            {
                p.Rubium = StartingRubium;
                p.VictoryPoints = 0;
                _unitsByPlayer[p] = new List<UnitInstance>();
            }

            _currentPlayerIndex = 0;
        }

        void SpawnStartingUnits()
        {
            // Home bases are three-hex clusters just outside the main ring on opposite sides.
            var homesP0 = new List<BoardTile>();
            var homesP1 = new List<BoardTile>();

            int homeQLeft = -Board.RingRadius - 1;
            int homeQRight = Board.RingRadius + 1;
            int[] homeRs = { -1, 0, 1 };

            foreach (int r in homeRs)
            {
                var t0 = Board.GetTile(homeQLeft, r);
                if (t0 != null) homesP0.Add(t0);
                var t1 = Board.GetTile(homeQRight, r);
                if (t1 != null) homesP1.Add(t1);
            }

            foreach (var t in homesP0)
            {
                t.Type = TileType.HomeBase;
                t.Owner = Players[0];
            }
            foreach (var t in homesP1)
            {
                t.Type = TileType.HomeBase;
                t.Owner = Players[1];
            }

            // Start with a single Human on the central home hex for each player
            if (homesP0.Count > 0)
                CreateUnit(Players[0], UnitType.Human, homesP0[1], hasAlreadyMovedThisTurn: false);
            if (homesP1.Count > 0)
                CreateUnit(Players[1], UnitType.Human, homesP1[1], hasAlreadyMovedThisTurn: false);
        }

        UnitInstance CreateUnit(PlayerState owner, UnitType type, BoardTile tile, bool hasAlreadyMovedThisTurn)
        {
            if (tile == null)
                return null;

            var def = Config.GetUnit(type);
            if (def == null)
            {
                Debug.LogError($"Missing unit definition for {type}");
                return null;
            }

            var unitGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            unitGo.transform.position = tile.View.transform.position + Vector3.up * 0.25f;
            unitGo.transform.localScale = Vector3.one * 0.35f;

            var rend = unitGo.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                // Use existing material if available, just tint it
                rend.material.color = owner.Color;
            }

            var instance = unitGo.AddComponent<UnitInstance>();
            instance.Initialize(owner, def, tile, hasAlreadyMovedThisTurn);

            _unitsByPlayer[owner].Add(instance);
            return instance;
        }

        public UnitInstance SpawnUnit(PlayerState owner, UnitType type, BoardTile tile)
        {
            // Units spawned during the current turn (purchases, exploration rewards)
            // should NOT be able to move again this turn.
            return CreateUnit(owner, type, tile, hasAlreadyMovedThisTurn: true);
        }

        public void EndTurn()
        {
            // Income: sum tile yields + any mines from exploration
            var player = Players[_currentPlayerIndex];
            int income = 0;
            foreach (var tile in Board.AllTiles)
            {
                if (tile.Owner == player)
                {
                    var tileDef = Config.GetTile(tile.Type);
                    if (tileDef != null)
                        income += tileDef.RubiumYield;
                    income += tile.ExtraMineYield;
                }
            }

            player.Rubium += income;
            _currentPlayerIndex = (_currentPlayerIndex + 1) % Players.Count;
            BeginTurn();
        }

        public PlayerState CurrentPlayer => Players[_currentPlayerIndex];

        void BeginTurn()
        {
            // Reset HasMovedThisTurn for all units belonging to the current player
            var player = Players[_currentPlayerIndex];
            if (_unitsByPlayer.TryGetValue(player, out var list))
            {
                foreach (var u in list)
                {
                    if (u != null)
                        u.HasMovedThisTurn = false;
                }
            }
        }
    }
}

