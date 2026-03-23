using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace NexusGame
{
    public partial class GameController : MonoBehaviour
    {
        [Header("Scene References")]
        public BoardGenerator Board;
        public NexusConfig Config;

        [Header("Players")]
        public List<PlayerState> Players = new List<PlayerState>();
        public int StartingRubium = 10;

        [Header("Battle")]
        [Tooltip("Resolve battles at turn start (after draw + mining).")]
        public bool RunBattlePhaseAtTurnStart = true;

        [Header("VS AI")]
        [Tooltip("When true, AiPlayerIndex is controlled by SimpleAiController (hotseat).")]
        public bool VsAiMode;

        [Tooltip("Default: 1 = second player (red in 1v1).")]
        public int AiPlayerIndex = 1;

        /// <summary>Most recent battle phase log (for HUD / debugging).</summary>
        public string LastBattlePhaseLog { get; private set; }

        int _currentPlayerIndex;
        readonly Dictionary<PlayerState, List<UnitInstance>> _unitsByPlayer =
            new Dictionary<PlayerState, List<UnitInstance>>();

        /// <summary>Set before AddComponent when Bootstrap will call <see cref="ResetAndStartNewMatch"/>.</summary>
        public static bool SkipStartInitOnce;

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

            if (SkipStartInitOnce)
            {
                SkipStartInitOnce = false;
                return;
            }

            InitPlayers();
            InitCardDecks();
            BeginTurn();
            SpawnStartingUnits();
        }

        void InitPlayers()
        {
            _unitsByPlayer.Clear();

            if (Players.Count == 0)
            {
                // VS AI is always a 2-player match on the current board layout.
                var layout = Board != null ? Board.LayoutMode : BoardLayoutMode.OneVOne;
                int playerCount = VsAiMode
                    ? 2
                    : (layout == BoardLayoutMode.TwoToFour || layout == BoardLayoutMode.TwoToFourSmall ? 4 : 2);

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
                p.BattleEnergize = new List<EnergizeBattleId>();
                p.DeployEnergize = new List<EnergizeDeploymentId>();
                p.DeploymentPurchaseDiscountRubium = 0;
                p.SecretMissions = new List<SecretMissionInHand>();
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

            // Distinct piece shapes per UnitType (so all units are visually unique).
            PrimitiveType prim;
            Quaternion rot = Quaternion.identity;
            Vector3 scale;
            float yOffset;

            switch (type)
            {
                case UnitType.Human:
                    prim = PrimitiveType.Capsule;
                    scale = Vector3.one * 0.35f;
                    yOffset = 0.25f;
                    break;
                case UnitType.Fungoid:
                    prim = PrimitiveType.Sphere;
                    scale = Vector3.one * 0.33f;
                    yOffset = 0.27f;
                    break;
                case UnitType.Crystalline:
                    prim = PrimitiveType.Cube;
                    scale = Vector3.one * 0.31f;
                    yOffset = 0.26f;
                    rot = Quaternion.Euler(0f, 45f, 0f);
                    break;
                case UnitType.RockStrider:
                    prim = PrimitiveType.Cylinder;
                    // Flatten so it reads like a “stride” base.
                    scale = new Vector3(0.34f, 0.20f, 0.34f);
                    yOffset = 0.25f;
                    break;
                case UnitType.LavaLeaper:
                    prim = PrimitiveType.Capsule;
                    // Rotate + flatten, then add a small “flame” cube child.
                    scale = new Vector3(0.40f, 0.22f, 0.40f);
                    yOffset = 0.25f;
                    rot = Quaternion.Euler(0f, 0f, 90f);
                    break;
                case UnitType.RubiumDragon:
                    prim = PrimitiveType.Cylinder;
                    // Taller body + two wings children.
                    scale = new Vector3(0.42f, 0.30f, 0.42f);
                    yOffset = 0.26f;
                    break;
                default:
                    prim = PrimitiveType.Capsule;
                    scale = Vector3.one * 0.35f;
                    yOffset = 0.25f;
                    break;
            }

            var unitGo = GameObject.CreatePrimitive(prim);
            unitGo.name = type + "_Unit";
            unitGo.transform.position = tile.View.transform.position + Vector3.up * yOffset;
            unitGo.transform.rotation = rot;
            unitGo.transform.localScale = scale;

            var rend = unitGo.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                // Use existing material if available, just tint it
                rend.material.color = owner.Color;
            }

            // Add small child parts so the “special” unit types read differently even if owner colors match.
            if (type == UnitType.LavaLeaper)
            {
                var flame = GameObject.CreatePrimitive(PrimitiveType.Cube);
                flame.name = "LavaFlame";
                flame.transform.SetParent(unitGo.transform, worldPositionStays: false);
                flame.transform.localPosition = new Vector3(0f, 0.20f, 0f);
                flame.transform.localRotation = Quaternion.identity;
                flame.transform.localScale = new Vector3(0.12f, 0.25f, 0.12f);
                var fr = flame.GetComponentInChildren<Renderer>();
                if (fr != null)
                    fr.material.color = new Color(1f, 0.45f, 0.05f, 1f);
            }
            else if (type == UnitType.RubiumDragon)
            {
                // Simple “wings”
                var wingL = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wingL.name = "WingL";
                wingL.transform.SetParent(unitGo.transform, worldPositionStays: false);
                wingL.transform.localPosition = new Vector3(-0.32f, 0.05f, 0f);
                wingL.transform.localScale = new Vector3(0.28f, 0.06f, 0.12f);
                var wlr = wingL.GetComponentInChildren<Renderer>();
                if (wlr != null)
                    wlr.material.color = owner.Color * 0.8f;

                var wingR = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wingR.name = "WingR";
                wingR.transform.SetParent(unitGo.transform, worldPositionStays: false);
                wingR.transform.localPosition = new Vector3(0.32f, 0.05f, 0f);
                wingR.transform.localScale = new Vector3(0.28f, 0.06f, 0.12f);
                var wr = wingR.GetComponentInChildren<Renderer>();
                if (wr != null)
                    wr.material.color = owner.Color * 0.8f;
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

        /// <summary>Call after dragon phase completes (or immediately if none).</summary>
        public void AdvanceToNextPlayerTurn()
        {
            _currentPlayerIndex = (_currentPlayerIndex + 1) % Players.Count;
            BeginTurn();
        }

        /// <summary>End current turn: optional Rubium Dragon strikes, then next player.</summary>
        public void EndTurn()
        {
            BeginDragonPhaseIfNeeded(() =>
            {
                if (Players != null && _currentPlayerIndex >= 0 && _currentPlayerIndex < Players.Count)
                    Players[_currentPlayerIndex].DeploymentPurchaseDiscountRubium = 0;
                AdvanceToNextPlayerTurn();
            });
        }

        public PlayerState CurrentPlayer => Players[_currentPlayerIndex];

        /// <summary>True if this player seat is run by the AI in VsAiMode.</summary>
        public bool IsAiControlled(PlayerState p) =>
            VsAiMode && p != null && p.PlayerIndex == AiPlayerIndex;

        /// <summary>Find any home-base tile owned by the player (for purchases / AI).</summary>
        public BoardTile FindHomeBaseForPlayer(PlayerState player)
        {
            if (player == null || Board == null)
                return null;
            foreach (var t in Board.AllTiles)
            {
                if (t != null && t.Type == TileType.HomeBase && t.Owner == player)
                    return t;
            }

            return null;
        }

        /// <summary>Buy a unit on a home hex (same rules as HUD buy buttons).</summary>
        public bool TryPurchaseUnit(PlayerState player, UnitType type, int baseCost)
        {
            if (player != CurrentPlayer || BattlePhaseBlockingPlay || DragonPhase != null)
                return false;

            var homeTile = FindHomeBaseForPlayer(player);
            if (homeTile == null)
                return false;

            int maxOff = Mathf.Max(0, baseCost - 1);
            int use = Mathf.Min(maxOff, player.DeploymentPurchaseDiscountRubium);
            int pay = baseCost - use;
            if (player.Rubium < pay)
                return false;

            player.DeploymentPurchaseDiscountRubium -= use;
            player.Rubium -= pay;
            SpawnUnit(player, type, homeTile);
            return true;
        }

        void BeginTurn()
        {
            var player = Players[_currentPlayerIndex];

            RunDrawPhase(player);

            if (_unitsByPlayer.TryGetValue(player, out var moveReset))
            {
                foreach (var u in moveReset)
                {
                    if (u != null)
                        u.HasMovedThisTurn = false;
                }
            }

            // Mining: collect from mines they occupy (uncontested).
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

            LastBattlePhaseLog = "";
            PendingBattleArrangement = false;
            BattlePhaseBlockingPlay = false;

            if (RunBattlePhaseAtTurnStart && Config != null)
            {
                var contested = BattleResolver.FindContestedHexesForAttacker(player);
                if (contested.Count > 0)
                    BeginBattleArrangement(player);
            }
        }

        /// <summary>Legacy auto-resolve (no UI). Builds plan from board state.</summary>
        public void RunBattlePhase(PlayerState attacker)
        {
            if (attacker == null || Config == null)
                return;

            BattlePlan.Clear();
            var contested = BattleResolver.FindContestedHexesForAttacker(attacker);
            contested.Sort((a, b) =>
            {
                int c = a.Q.CompareTo(b.Q);
                return c != 0 ? c : a.R.CompareTo(b.R);
            });
            foreach (var hex in contested)
            {
                var opps = BattleResolver.OpponentsOnHex(hex, attacker);
                if (opps.Count == 0)
                    continue;
                BattlePlan.Add(new PlannedBattleEntry
                {
                    Hex = hex,
                    DefenderPlayerIndex = opps[0].PlayerIndex
                });
            }

            RunLegacyAutoBattle(attacker);
        }

        public void RemoveUnit(UnitInstance unit)
        {
            if (unit == null)
                return;

            var tile = unit.Tile;
            if (_unitsByPlayer.TryGetValue(unit.Owner, out var list))
                list.Remove(unit);

            Destroy(unit.gameObject);
            UnitInstance.RelayoutTile(tile);
        }
    }
}

