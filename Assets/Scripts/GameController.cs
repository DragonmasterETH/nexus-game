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
                // Default to 2 players; if the board is in any 2–4 player mode, prepare 4 players.
                var layout = Board != null ? Board.LayoutMode : BoardLayoutMode.OneVOne;
                int playerCount = (layout == BoardLayoutMode.TwoToFour || layout == BoardLayoutMode.TwoToFourSmall) ? 4 : 2;

                if (playerCount >= 1)
                    Players.Add(new PlayerState { PlayerIndex = 0, Color = Color.blue, Rubium = StartingRubium });
                if (playerCount >= 2)
                    Players.Add(new PlayerState { PlayerIndex = 1, Color = Color.red, Rubium = StartingRubium });
                if (playerCount >= 3)
                    Players.Add(new PlayerState { PlayerIndex = 2, Color = Color.green, Rubium = StartingRubium });
                if (playerCount >= 4)
                    Players.Add(new PlayerState { PlayerIndex = 3, Color = Color.yellow, Rubium = StartingRubium });
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
            // Home bases are three-hex clusters just outside the main ring.
            // For OneVOne: left and right strips.
            // For TwoToFour / TwoToFourSmall: left, right, top, and bottom strips.
            var layout = Board != null ? Board.LayoutMode : BoardLayoutMode.OneVOne;

            var baseStrips = new List<List<BoardTile>>();

            int homeQLeft = -Board.RingRadius - 1;
            int homeQRight = Board.RingRadius + 1;
            int[] homeRs = { -1, 0, 1 };

            // Left strip
            var homesLeft = new List<BoardTile>();
            foreach (int r in homeRs)
            {
                var t = Board.GetTile(homeQLeft, r);
                if (t != null) homesLeft.Add(t);
            }
            if (homesLeft.Count == 3) baseStrips.Add(homesLeft);

            // Right strip
            var homesRight = new List<BoardTile>();
            foreach (int r in homeRs)
            {
                var t = Board.GetTile(homeQRight, r);
                if (t != null) homesRight.Add(t);
            }
            if (homesRight.Count == 3) baseStrips.Add(homesRight);

            if (layout == BoardLayoutMode.TwoToFour)
            {
                // Top strip
                int homeRTop = -Board.RingRadius - 1;
                int[] homeQs = { -1, 0, 1 };
                var homesTop = new List<BoardTile>();
                foreach (int q in homeQs)
                {
                    var t = Board.GetTile(q, homeRTop);
                    if (t != null) homesTop.Add(t);
                }
                if (homesTop.Count == 3) baseStrips.Add(homesTop);

                // Bottom strip
                int homeRBottom = Board.RingRadius + 1;
                var homesBottom = new List<BoardTile>();
                foreach (int q in homeQs)
                {
                    var t = Board.GetTile(q, homeRBottom);
                    if (t != null) homesBottom.Add(t);
                }
                if (homesBottom.Count == 3) baseStrips.Add(homesBottom);
            }

            // Assign ownership and printed mines (2,3,2) for as many players as we have strips and PlayerStates.
            int stripsToAssign = Mathf.Min(baseStrips.Count, Players.Count);
            for (int i = 0; i < stripsToAssign; i++)
            {
                var strip = baseStrips[i];
                var owner = Players[i];

                foreach (var t in strip)
                {
                    t.Type = TileType.HomeBase;
                    t.Owner = owner;
                }

                // 2,3,2 mines per strip
                strip[0].ExtraMineYield = 2;
                strip[1].ExtraMineYield = 3;
                strip[2].ExtraMineYield = 2;
                CreateHomeMineLabel(strip[0]);
                CreateHomeMineLabel(strip[1]);
                CreateHomeMineLabel(strip[2]);
            }

            // No starting units; players must purchase and deploy during their turns.
        }

        void CreateHomeMineLabel(BoardTile tile)
        {
            if (tile == null || tile.ExtraMineYield <= 0)
                return;

            if (tile.MineLabel == null)
            {
                var labelRoot = new GameObject("HomeMineLabel");
                labelRoot.transform.SetParent(tile.View.transform, worldPositionStays: false);
                labelRoot.transform.localPosition = new Vector3(0f, 0.04f, 0f);
                labelRoot.transform.localRotation = Quaternion.identity;
                labelRoot.transform.localScale = Vector3.one * 0.2f;

                var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
                bg.name = "HomeMineBg";
                bg.transform.SetParent(labelRoot.transform, worldPositionStays: false);
                bg.transform.localPosition = Vector3.zero;
                bg.transform.localRotation = Quaternion.identity;
                bg.transform.localScale = Vector3.one * 0.6f;
                var bgRenderer = bg.GetComponent<Renderer>();
                if (bgRenderer != null)
                {
                    bgRenderer.material = new Material(Shader.Find("Sprites/Default"));
                    bgRenderer.material.color = new Color(0f, 0f, 0f, 0.6f);
                }

                var textGo = new GameObject("HomeMineText");
                textGo.transform.SetParent(labelRoot.transform, worldPositionStays: false);
                textGo.transform.localPosition = new Vector3(0f, 0f, -0.02f);
                textGo.transform.localRotation = Quaternion.identity;
                textGo.transform.localScale = Vector3.one * 0.4f;

                var text = textGo.AddComponent<TextMesh>();
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.fontSize = 64;
                text.color = Color.yellow;

                tile.MineLabel = labelRoot;
            }

            var tm = tile.MineLabel.GetComponentInChildren<TextMesh>();
            if (tm != null)
            {
                tm.text = tile.ExtraMineYield.ToString();
            }
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
            _currentPlayerIndex = (_currentPlayerIndex + 1) % Players.Count;
            BeginTurn();
        }

        public PlayerState CurrentPlayer => Players[_currentPlayerIndex];

        void BeginTurn()
        {
            var player = Players[_currentPlayerIndex];

            // At the START of a player's turn, collect income from mines they actually occupy
            // (and that are not contested).
            int income = 0;
            foreach (var tile in Board.AllTiles)
            {
                if (tile.ExtraMineYield <= 0)
                    continue;

                bool hasPlayerUnit = false;
                bool hasOtherUnit = false;

                foreach (var unit in FindObjectsOfType<UnitInstance>())
                {
                    if (unit.Tile != tile)
                        continue;

                    if (unit.Owner == player)
                        hasPlayerUnit = true;
                    else
                        hasOtherUnit = true;
                }

                if (hasPlayerUnit && !hasOtherUnit)
                {
                    income += tile.ExtraMineYield;
                }
            }

            player.Rubium += income;

            // Reset HasMovedThisTurn for all units belonging to the current player
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

