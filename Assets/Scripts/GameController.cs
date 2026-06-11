using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace NexusGame
{
    /// <summary>World start + yield for HUD rubium fly animation (one entry per contributing mine hex).</summary>
    public readonly struct MiningIncomeFlightInfo
    {
        public readonly Vector3 WorldStart;
        public readonly int Amount;

        public MiningIncomeFlightInfo(Vector3 worldStart, int amount)
        {
            WorldStart = worldStart;
            Amount = amount;
        }
    }

    /// <summary>VP gain for HUD fly animation (center → VP icon).</summary>
    public readonly struct VictoryPointFlightInfo
    {
        public readonly int Amount;

        public VictoryPointFlightInfo(int amount)
        {
            Amount = amount;
        }
    }

    public partial class GameController : MonoBehaviour
    {
        [Header("Scene References")]
        public BoardGenerator Board;
        public NexusConfig Config;

        [Header("Players")]
        public List<PlayerState> Players = new List<PlayerState>();
        public int StartingRubium = 10;

        [Header("Battle")]
        [Tooltip("Resolve battles at turn start (after draw + mining). Disabled by default; battles run on End Turn.")]
        public bool RunBattlePhaseAtTurnStart = false;

        [Header("Rules (movement)")]
        [Tooltip("If true, optional retreat constraints are enforced (see MovementRetreatRules).")]
        public bool EnforceRetreatRules = true;

        [Header("VS AI")]
        [Tooltip("When true, AiPlayerIndex is controlled by SimpleAiController (hotseat).")]
        public bool VsAiMode;

        [Tooltip("With VsAiMode: both seats are AI — for watch / stress testing.")]
        public bool WatchAiVsAiMode;

        /// <summary>
        /// Compatibility alias used by newer HUD/bootstrap paths.
        /// Maps to <see cref="WatchAiVsAiMode"/>.
        /// </summary>
        public bool AiVsAiMode
        {
            get => WatchAiVsAiMode;
            set => WatchAiVsAiMode = value;
        }

        [Header("AI test (compat)")]
        [Min(1)]
        public int AiTestVictoryTargetVp = 10;

        [Min(1)]
        public int AiTestMaxTotalDrawPhases = 500;

        public bool AiTestMatchCompleted { get; private set; }

        public PlayerState AiTestWinner { get; private set; }

        [Tooltip("Default: 1 = second player (red in 1v1).")]
        public int AiPlayerIndex = 1;

        /// <summary>Most recent battle phase log (for HUD / debugging).</summary>
        public string LastBattlePhaseLog { get; private set; }

        /// <summary>HUD: last mining income amount (for +Rubium popup).</summary>
        public int LastMiningIncomeAmount { get; private set; }

        public float IncomeFlashUntil { get; private set; }

        [Min(0.5f)]
        public float IncomeFlashSeconds = 2.5f;

        int _currentPlayerIndex;
        BoardTile _activeRetreatSourceThisTurn;
        bool _normalMovementOccurredThisTurn;
        bool _anyMovementOccurredThisTurn;
        readonly Dictionary<PlayerState, List<UnitInstance>> _unitsByPlayer =
            new Dictionary<PlayerState, List<UnitInstance>>();

        List<MiningIncomeFlightInfo> _miningIncomeFlightsForHud;
        List<VictoryPointFlightInfo> _victoryPointFlightsForHud;

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
            SpawnStartingUnits();
            BeginTurn();
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

            if (layout == BoardLayoutMode.BattleTest)
            {
                var leftHome = Board.GetTile(-1, 0);
                var rightHome = Board.GetTile(1, 0);
                if (leftHome == null || rightHome == null || Players.Count < 2)
                    return;

                var p1 = Players[0];
                var p2 = Players[1];

                // Seat player 1 (blue) on whichever home reads as top-left on screen for the current camera framing.
                Vector3 leftPos = Board.AxialToWorld(leftHome.Q, leftHome.R);
                Vector3 rightPos = Board.AxialToWorld(rightHome.Q, rightHome.R);
                bool leftIsP1 = ScreenTopLeftPrefersAOverB(leftPos, rightPos);
                BoardTile homeP1 = leftIsP1 ? leftHome : rightHome;
                BoardTile homeP2 = leftIsP1 ? rightHome : leftHome;

                leftHome.Type = TileType.HomeBase;
                rightHome.Type = TileType.HomeBase;

                homeP1.Owner = p1;
                homeP1.HomeBaseStartingOwnerIndex = p1.PlayerIndex;
                homeP2.Owner = p2;
                homeP2.HomeBaseStartingOwnerIndex = p2.PlayerIndex;

                // Pre-seeded varied armies for immediate one-step move into center battle.
                UnitType[] p1Start = { UnitType.Human, UnitType.Fungoid, UnitType.Crystalline, UnitType.RockStrider };
                UnitType[] p2Start = { UnitType.Human, UnitType.Fungoid, UnitType.LavaLeaper, UnitType.RubiumDragon };

                foreach (var t in p1Start)
                    CreateUnit(p1, t, homeP1, hasAlreadyMovedThisTurn: false);
                foreach (var t in p2Start)
                    CreateUnit(p2, t, homeP2, hasAlreadyMovedThisTurn: false);

                GrantBattleTestStartingBattleEnergize(p1, 6);
                GrantBattleTestStartingBattleEnergize(p2, 6);
                return;
            }

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

            SortHomeBaseStripsForTopLeftScreen(baseStrips);

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
                    t.HomeBaseStartingOwnerIndex = owner.PlayerIndex;
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

        /// <summary>
        /// Order home strips so <see cref="PlayerState.PlayerIndex"/> 0 gets the cluster that sits in the top-left
        /// of the screen for <see cref="BoardCameraPanZoom"/> (uses main camera right/up projected on XZ).
        /// </summary>
        void SortHomeBaseStripsForTopLeftScreen(List<List<BoardTile>> strips)
        {
            if (strips == null || strips.Count <= 1 || Board == null)
                return;

            var cam = Camera.main;
            if (cam == null)
                return;

            Vector3 boardCenter = Board.AxialToWorld(0, 0);

            Vector3 up = cam.transform.up;
            up.y = 0f;
            if (up.sqrMagnitude > 1e-8f)
                up.Normalize();
            else
                up = Vector3.forward;

            Vector3 right = cam.transform.right;
            right.y = 0f;
            if (right.sqrMagnitude > 1e-8f)
                right.Normalize();
            else
                right = Vector3.right;

            Vector3 topLeftDir = (up - right).normalized;

            strips.Sort((a, b) =>
            {
                float da = Vector3.Dot(StripCentroidWorld(a) - boardCenter, topLeftDir);
                float db = Vector3.Dot(StripCentroidWorld(b) - boardCenter, topLeftDir);
                int cmp = db.CompareTo(da);
                if (cmp != 0)
                    return cmp;
                long ka = StripSortKey(a);
                long kb = StripSortKey(b);
                return ka.CompareTo(kb);
            });
        }

        Vector3 StripCentroidWorld(List<BoardTile> strip)
        {
            Vector3 s = Vector3.zero;
            foreach (var t in strip)
                s += t.View != null ? t.View.transform.position : Board.AxialToWorld(t.Q, t.R);
            return s / Mathf.Max(1, strip.Count);
        }

        static long StripSortKey(List<BoardTile> strip)
        {
            long k = 0;
            foreach (var t in strip)
                k = unchecked(k * 397 ^ t.Q ^ ((long)t.R << 16));
            return k;
        }

        /// <summary>True if world point <paramref name="a"/> is more screen-top-left than <paramref name="b"/>.</summary>
        bool ScreenTopLeftPrefersAOverB(Vector3 a, Vector3 b)
        {
            var cam = Camera.main;
            if (cam == null || Board == null)
                return true;

            Vector3 boardCenter = Board.AxialToWorld(0, 0);

            Vector3 up = cam.transform.up;
            up.y = 0f;
            if (up.sqrMagnitude > 1e-8f)
                up.Normalize();
            else
                up = Vector3.forward;

            Vector3 right = cam.transform.right;
            right.y = 0f;
            if (right.sqrMagnitude > 1e-8f)
                right.Normalize();
            else
                right = Vector3.right;

            Vector3 topLeftDir = (up - right).normalized;
            return Vector3.Dot(a - boardCenter, topLeftDir) >= Vector3.Dot(b - boardCenter, topLeftDir);
        }

        void GrantBattleTestStartingBattleEnergize(PlayerState player, int count)
        {
            if (player == null || count <= 0)
                return;

            if (_cardRng == null)
                _cardRng = new System.Random(System.Environment.TickCount);

            var options = new List<EnergizeBattleId>();
            foreach (EnergizeBattleId id in System.Enum.GetValues(typeof(EnergizeBattleId)))
            {
                if (id != EnergizeBattleId.None)
                    options.Add(id);
            }

            if (options.Count == 0)
                return;

            for (int i = 0; i < count; i++)
            {
                if (CountEnergizeInHand(player) >= MaxEnergizeCardsInHand)
                    break;
                int idx = _cardRng.Next(0, options.Count);
                player.BattleEnergize.Add(options[idx]);
            }
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
                string mineYield = tile.ExtraMineYield.ToString();
                tm.text = mineYield;
                tm.characterSize = mineYield.Length >= 3 ? 0.18f : 0.22f;
            }

            var bgTransform = tile.MineLabel.transform.Find("HomeMineBg");
            if (bgTransform != null)
            {
                float widthMul = tile.ExtraMineYield >= 100 ? 1.15f : tile.ExtraMineYield >= 10 ? 1.0f : 0.85f;
                bgTransform.localScale = new Vector3(0.6f * widthMul, 0.6f, 1f);
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

            if (type == UnitType.RubiumDragon)
            {
                var dragonArt = NexusGuiArt.LoadRubiumDragonForPlayer(owner);
                if (!dragonArt.IsEmpty)
                {
                    float hoverY = 0.12f * (Board != null ? Board.HexRadius / 0.7f : 1f);
                    var dragonRootGo = new GameObject(type + "_Unit");
                    dragonRootGo.transform.position = tile.View.transform.position + Vector3.up * hoverY;

                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    quad.name = "DragonArt";
                    quad.transform.SetParent(dragonRootGo.transform, false);
                    quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    float hexScale = Board != null ? Board.HexRadius / 0.7f : 1f;
                    float baseSize = 0.62f * hexScale;
                    float a = Mathf.Max(0.2f, dragonArt.AspectRatio);
                    quad.transform.localScale = new Vector3(baseSize * Mathf.Min(1f, a), baseSize / Mathf.Max(1f, a),
                        1f);
                    Object.Destroy(quad.GetComponent<Collider>());

                    var qrend = quad.GetComponent<Renderer>();
                    var mat = new Material(Shader.Find("Sprites/Default"));
                    NexusGuiArt.ApplyImageToMaterial(mat, dragonArt, Color.magenta);
                    qrend.material = mat;

                    var dragonInstance = dragonRootGo.AddComponent<UnitInstance>();
                    dragonInstance.Initialize(owner, def, tile, hasAlreadyMovedThisTurn);
                    _unitsByPlayer[owner].Add(dragonInstance);
                    return dragonInstance;
                }
            }

            if (type == UnitType.RockStrider)
            {
                var striderArt = NexusGuiArt.LoadRockStriderForPlayer(owner);
                if (!striderArt.IsEmpty)
                {
                    float hoverY = 0.12f * (Board != null ? Board.HexRadius / 0.7f : 1f);
                    var striderRootGo = new GameObject(type + "_Unit");
                    striderRootGo.transform.position = tile.View.transform.position + Vector3.up * hoverY;

                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    quad.name = "StriderArt";
                    quad.transform.SetParent(striderRootGo.transform, false);
                    quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    float hexScale = Board != null ? Board.HexRadius / 0.7f : 1f;
                    float baseSize = 0.52f * hexScale;
                    float a = Mathf.Max(0.2f, striderArt.AspectRatio);
                    quad.transform.localScale = new Vector3(baseSize * Mathf.Min(1f, a), baseSize / Mathf.Max(1f, a),
                        1f);
                    Object.Destroy(quad.GetComponent<Collider>());

                    var qrend = quad.GetComponent<Renderer>();
                    var mat = new Material(Shader.Find("Sprites/Default"));
                    NexusGuiArt.ApplyImageToMaterial(mat, striderArt, Color.magenta);
                    qrend.material = mat;

                    var striderInstance = striderRootGo.AddComponent<UnitInstance>();
                    striderInstance.Initialize(owner, def, tile, hasAlreadyMovedThisTurn);
                    _unitsByPlayer[owner].Add(striderInstance);
                    return striderInstance;
                }
            }

            if (type == UnitType.Fungoid)
            {
                var fungusArt = NexusGuiArt.LoadFungoidForPlayer(owner);
                if (!fungusArt.IsEmpty)
                {
                    float hoverY = 0.12f * (Board != null ? Board.HexRadius / 0.7f : 1f);
                    var fungoidRootGo = new GameObject(type + "_Unit");
                    fungoidRootGo.transform.position = tile.View.transform.position + Vector3.up * hoverY;

                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    quad.name = "FungusArt";
                    quad.transform.SetParent(fungoidRootGo.transform, false);
                    quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    float hexScale = Board != null ? Board.HexRadius / 0.7f : 1f;
                    float baseSize = 0.48f * hexScale;
                    float a = Mathf.Max(0.2f, fungusArt.AspectRatio);
                    quad.transform.localScale = new Vector3(baseSize * Mathf.Min(1f, a), baseSize / Mathf.Max(1f, a),
                        1f);
                    Object.Destroy(quad.GetComponent<Collider>());

                    var qrend = quad.GetComponent<Renderer>();
                    var mat = new Material(Shader.Find("Sprites/Default"));
                    NexusGuiArt.ApplyImageToMaterial(mat, fungusArt, Color.magenta);
                    qrend.material = mat;

                    var fungoidInstance = fungoidRootGo.AddComponent<UnitInstance>();
                    fungoidInstance.Initialize(owner, def, tile, hasAlreadyMovedThisTurn);
                    _unitsByPlayer[owner].Add(fungoidInstance);
                    return fungoidInstance;
                }
            }

            if (type == UnitType.Human)
            {
                var humanArt = NexusGuiArt.LoadHumanForPlayer(owner);
                if (!humanArt.IsEmpty)
                {
                    float hoverY = 0.12f * (Board != null ? Board.HexRadius / 0.7f : 1f);
                    var humanRootGo = new GameObject(type + "_Unit");
                    humanRootGo.transform.position = tile.View.transform.position + Vector3.up * hoverY;

                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    quad.name = "HumanArt";
                    quad.transform.SetParent(humanRootGo.transform, false);
                    quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    float hexScale = Board != null ? Board.HexRadius / 0.7f : 1f;
                    float baseSize = 0.48f * hexScale;
                    float a = Mathf.Max(0.2f, humanArt.AspectRatio);
                    quad.transform.localScale = new Vector3(baseSize * Mathf.Min(1f, a), baseSize / Mathf.Max(1f, a),
                        1f);
                    Object.Destroy(quad.GetComponent<Collider>());

                    var qrend = quad.GetComponent<Renderer>();
                    var mat = new Material(Shader.Find("Sprites/Default"));
                    NexusGuiArt.ApplyImageToMaterial(mat, humanArt, Color.magenta);
                    qrend.material = mat;

                    var humanInstance = humanRootGo.AddComponent<UnitInstance>();
                    humanInstance.Initialize(owner, def, tile, hasAlreadyMovedThisTurn);
                    _unitsByPlayer[owner].Add(humanInstance);
                    return humanInstance;
                }
            }

            if (type == UnitType.LavaLeaper)
            {
                var leaperArt = NexusGuiArt.LoadLavaLeaperForPlayer(owner);
                if (!leaperArt.IsEmpty)
                {
                    float hoverY = 0.12f * (Board != null ? Board.HexRadius / 0.7f : 1f);
                    var leaperRootGo = new GameObject(type + "_Unit");
                    leaperRootGo.transform.position = tile.View.transform.position + Vector3.up * hoverY;

                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    quad.name = "LeaperArt";
                    quad.transform.SetParent(leaperRootGo.transform, false);
                    quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    float hexScale = Board != null ? Board.HexRadius / 0.7f : 1f;
                    float baseSize = 0.5f * hexScale;
                    float a = Mathf.Max(0.2f, leaperArt.AspectRatio);
                    quad.transform.localScale = new Vector3(baseSize * Mathf.Min(1f, a), baseSize / Mathf.Max(1f, a),
                        1f);
                    Object.Destroy(quad.GetComponent<Collider>());

                    var qrend = quad.GetComponent<Renderer>();
                    var mat = new Material(Shader.Find("Sprites/Default"));
                    NexusGuiArt.ApplyImageToMaterial(mat, leaperArt, Color.magenta);
                    qrend.material = mat;

                    var leaperInstance = leaperRootGo.AddComponent<UnitInstance>();
                    leaperInstance.Initialize(owner, def, tile, hasAlreadyMovedThisTurn);
                    _unitsByPlayer[owner].Add(leaperInstance);
                    return leaperInstance;
                }
            }

            if (type == UnitType.Crystalline)
            {
                var crystalArt = NexusGuiArt.LoadCrystallineForPlayer(owner);
                if (!crystalArt.IsEmpty)
                {
                    float hoverY = 0.12f * (Board != null ? Board.HexRadius / 0.7f : 1f);
                    var crystalRootGo = new GameObject(type + "_Unit");
                    crystalRootGo.transform.position = tile.View.transform.position + Vector3.up * hoverY;

                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    quad.name = "CrystalArt";
                    quad.transform.SetParent(crystalRootGo.transform, false);
                    quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    float hexScale = Board != null ? Board.HexRadius / 0.7f : 1f;
                    float baseSize = 0.48f * hexScale;
                    float a = Mathf.Max(0.2f, crystalArt.AspectRatio);
                    quad.transform.localScale = new Vector3(baseSize * Mathf.Min(1f, a), baseSize / Mathf.Max(1f, a),
                        1f);
                    Object.Destroy(quad.GetComponent<Collider>());

                    var qrend = quad.GetComponent<Renderer>();
                    var mat = new Material(Shader.Find("Sprites/Default"));
                    NexusGuiArt.ApplyImageToMaterial(mat, crystalArt, Color.magenta);
                    qrend.material = mat;

                    var crystalInstance = crystalRootGo.AddComponent<UnitInstance>();
                    crystalInstance.Initialize(owner, def, tile, hasAlreadyMovedThisTurn);
                    _unitsByPlayer[owner].Add(crystalInstance);
                    return crystalInstance;
                }
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
            if (IsGameOver)
                return;

            _completedPlayerTurns++;
            if (MaxPlayerTurnsBeforeTiebreak > 0 && _completedPlayerTurns >= MaxPlayerTurnsBeforeTiebreak)
            {
                var w = SelectHighestVpPlayerTiebreak();
                EndGame(w,
                    $"Turn limit ({MaxPlayerTurnsBeforeTiebreak} completed player-turns). Highest VP wins (tie: lower player #).");
                return;
            }

            _currentPlayerIndex = (_currentPlayerIndex + 1) % Players.Count;
            // Round number (HUD "Turn"): stays on 1 until play returns to the first player, then increments.
            if (_currentPlayerIndex == 0)
                _turnNumber++;

            BeginTurn();
            NotifyOnlineStateChanged();
        }

        /// <summary>End current turn: optional Rubium Dragon strikes, then next player.</summary>
        public void EndTurn()
        {
            if (IsGameOver)
                return;

            var endingPlayer = CurrentPlayer;
            bool hasContested = Config != null && endingPlayer != null &&
                                BattleResolver.FindContestedHexesForAttacker(endingPlayer).Count > 0;

            if (hasContested)
            {
                BeginBattleArrangement(endingPlayer);
                NotifyOnlineStateChanged();
                StartCoroutine(EndTurnAfterBattleThenDragon(endingPlayer));
                return;
            }

            BeginDragonPhaseIfNeeded(() =>
            {
                if (IsGameOver)
                    return;
                if (Players != null && _currentPlayerIndex >= 0 && _currentPlayerIndex < Players.Count)
                    Players[_currentPlayerIndex].DeploymentPurchaseDiscountRubium = 0;
                AdvanceToNextPlayerTurn();
            });
        }

        System.Collections.IEnumerator EndTurnAfterBattleThenDragon(PlayerState endingPlayer)
        {
            while (!IsGameOver && (PendingBattleArrangement || BattlePhaseBlockingPlay))
                yield return null;

            if (IsGameOver || endingPlayer == null || endingPlayer != CurrentPlayer)
                yield break;

            BeginDragonPhaseIfNeeded(() =>
            {
                if (IsGameOver)
                    return;
                if (Players != null && _currentPlayerIndex >= 0 && _currentPlayerIndex < Players.Count)
                    Players[_currentPlayerIndex].DeploymentPurchaseDiscountRubium = 0;
                AdvanceToNextPlayerTurn();
            });
        }

        public PlayerState CurrentPlayer => Players[_currentPlayerIndex];

        /// <summary>
        /// HUD hint: player still has unmoved units or deployment energize to spend before voluntarily ending the turn.
        /// </summary>
        public bool HasOptionalPreEndTurnActions(PlayerState player)
        {
            if (player == null)
                return false;
            if (player.DeployEnergize != null && player.DeployEnergize.Count > 0)
                return true;
            if (_unitsByPlayer.TryGetValue(player, out var units))
            {
                for (int i = 0; i < units.Count; i++)
                {
                    var u = units[i];
                    if (u == null || u.Tile == null)
                        continue;
                    if (!u.HasMovedThisTurn)
                        return true;
                }
            }

            return false;
        }

        internal BoardTile ActiveRetreatSourceThisTurn => _activeRetreatSourceThisTurn;
        internal bool NormalMovementOccurredThisTurn => _normalMovementOccurredThisTurn;
        public bool AnyMovementOccurredThisTurn => _anyMovementOccurredThisTurn;

        /// <summary>True if this player seat is run by the AI in VsAiMode.</summary>
        public bool IsAiControlled(PlayerState p) =>
            VsAiMode && p != null && (WatchAiVsAiMode || p.PlayerIndex == AiPlayerIndex);

        /// <summary>True if the human on this device may control the given seat (online: only your seat).</summary>
        public bool CanLocalPlayerActFor(PlayerState p)
        {
            if (p == null || IsGameOver)
                return false;
            if (NexusSession.IsOnline && !NexusConnectionMonitor.CanPlay)
                return false;
            if (IsAiControlled(p))
                return false;
            if (NexusSession.IsOnline && p.PlayerIndex != NexusSession.LocalPlayerIndex)
                return false;
            return true;
        }

        /// <summary>True if this device may act during the current turn.</summary>
        public bool CanLocalPlayerActNow() => CanLocalPlayerActFor(CurrentPlayer);

        /// <summary>Find any legal starting home tile for deployment/purchase.</summary>
        public BoardTile FindHomeBaseForPlayer(PlayerState player)
        {
            if (player == null || Board == null)
                return null;
            foreach (var t in Board.AllTiles)
            {
                if (CanDeployToStartingHomeTile(player, t))
                    return t;
            }

            return null;
        }

        public bool IsTileContested(BoardTile tile)
        {
            if (tile == null)
                return false;
            PlayerState sole = null;
            foreach (var u in FindObjectsOfType<UnitInstance>())
            {
                if (u == null || u.Tile != tile)
                    continue;
                if (sole == null)
                    sole = u.Owner;
                else if (sole != u.Owner)
                    return true;
            }

            return false;
        }

        public bool CanDeployToStartingHomeTile(PlayerState player, BoardTile tile)
        {
            if (player == null || tile == null || tile.Type != TileType.HomeBase)
                return false;
            if (tile.HomeBaseStartingOwnerIndex != player.PlayerIndex)
                return false;
            // Friendly stacks are allowed; enemy presence blocks home-base deployment.
            if (TileHasEnemyForOwner(tile, player))
                return false;
            return true;
        }

        /// <summary>
        /// Exploration rewards can reveal a Human on a lava hex; Humans cannot occupy lava — spawn on home base instead.
        /// </summary>
        public BoardTile ResolveExplorationUnitSpawnTile(UnitType unitType, BoardTile exploredTile, PlayerState owner)
        {
            if (exploredTile == null || owner == null)
                return null;
            if (unitType == UnitType.Human && exploredTile.Type == TileType.Lava)
            {
                var home = FindHomeBaseForPlayer(owner);
                if (home != null)
                    return home;
                Debug.LogWarning(
                    "ResolveExplorationUnitSpawnTile: Human reward on lava but no home base found — cannot spawn Human.");
                return null;
            }

            return exploredTile;
        }

        /// <summary>Buy a unit on a home hex (same rules as HUD buy buttons).</summary>
        public bool TryPurchaseUnit(PlayerState player, UnitType type, int baseCost)
        {
            if (IsGameOver || player != CurrentPlayer || BattlePhaseBlockingPlay || DragonPhase != null)
                return false;
            if (_anyMovementOccurredThisTurn)
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
            AfterOnlineHostMutation();
            return true;
        }

        void BeginTurn()
        {
            if (IsGameOver)
                return;

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
            _miningIncomeFlightsForHud = null;
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
                    if (!IsAiControlled(player))
                    {
                        if (_miningIncomeFlightsForHud == null)
                            _miningIncomeFlightsForHud = new List<MiningIncomeFlightInfo>();
                        Vector3 w = tile.View != null
                            ? tile.View.transform.position + Vector3.up * 0.4f
                            : Board.AxialToWorld(tile.Q, tile.R) + Vector3.up * 0.4f;
                        _miningIncomeFlightsForHud.Add(new MiningIncomeFlightInfo(w, tile.ExtraMineYield));
                    }
                }
            }

            player.Rubium += income;
            player.LastMiningIncomeCollectedThisTurn = income;
            LastMiningIncomeAmount = income;
            IncomeFlashUntil = Time.time + IncomeFlashSeconds;

            LastBattlePhaseLog = "";
            PendingBattleArrangement = false;
            BattlePhaseBlockingPlay = false;
            _activeRetreatSourceThisTurn = null;
            _normalMovementOccurredThisTurn = false;
            _anyMovementOccurredThisTurn = false;

            if (RunBattlePhaseAtTurnStart && Config != null)
            {
                var contested = BattleResolver.FindContestedHexesForAttacker(player);
                if (contested.Count > 0)
                    BeginBattleArrangement(player);
            }

            MaybePanCameraTowardCurrentPlayerHomes(player);
        }

        /// <summary>
        /// Ease the board camera toward this player's front line: a hex they occupy that is closest to the board center (monolith).
        /// Falls back to home-cluster centroid if they have no units on the board.
        /// </summary>
        void MaybePanCameraTowardCurrentPlayerHomes(PlayerState player)
        {
            if (player == null || Players == null || Players.Count < 2 || Board == null || IsGameOver)
                return;

            var cam = FindObjectOfType<BoardCameraPanZoom>();
            if (cam == null)
                return;

            if (!TryGetSpectateFocusWorld(player, out Vector3 focus))
                return;

            cam.BeginSmoothLookTarget(focus);
        }

        bool TryGetSpectateFocusWorld(PlayerState player, out Vector3 worldOnGround)
        {
            worldOnGround = default;
            BoardTile bestTile = null;
            int bestDist = int.MaxValue;

            foreach (var u in FindObjectsOfType<UnitInstance>())
            {
                if (u == null || u.Tile == null || u.Owner != player)
                    continue;
                var t = u.Tile;
                int d = AxialDistanceToCenter(t.Q, t.R);
                if (d < bestDist || (d == bestDist && IsTileEarlierInTieBreak(t, bestTile)))
                {
                    bestDist = d;
                    bestTile = t;
                }
            }

            if (bestTile != null)
            {
                worldOnGround = bestTile.View != null
                    ? bestTile.View.transform.position
                    : Board.AxialToWorld(bestTile.Q, bestTile.R);
                return true;
            }

            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (var t in Board.AllTiles)
            {
                if (t.Type != TileType.HomeBase || t.HomeBaseStartingOwnerIndex != player.PlayerIndex)
                    continue;
                sum += t.View != null ? t.View.transform.position : Board.AxialToWorld(t.Q, t.R);
                n++;
            }

            if (n == 0)
                return false;
            worldOnGround = sum / n;
            return true;
        }

        static int AxialDistanceToCenter(int q, int r)
        {
            int dq = q;
            int dr = r;
            int ds = -(q + r);
            return (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(ds)) / 2;
        }

        static bool IsTileEarlierInTieBreak(BoardTile a, BoardTile b)
        {
            if (b == null)
                return true;
            if (a.Q != b.Q)
                return a.Q < b.Q;
            return a.R < b.R;
        }

        /// <summary>HUD: take pending mine→bank rubium flights (cleared after call). Only populated for human turns with income hexes.</summary>
        public bool TryConsumeMiningIncomeFlights(out List<MiningIncomeFlightInfo> flights)
        {
            flights = _miningIncomeFlightsForHud;
            _miningIncomeFlightsForHud = null;
            return flights != null && flights.Count > 0;
        }

        /// <summary>Queue a HUD animation when a human (non-AI) who matches <see cref="CurrentPlayer"/> gains VP.</summary>
        void QueueVictoryPointHudFlight(PlayerState recipient, int amount)
        {
            if (recipient == null || amount <= 0)
                return;
            if (IsAiControlled(recipient) || recipient != CurrentPlayer)
                return;
            if (_victoryPointFlightsForHud == null)
                _victoryPointFlightsForHud = new List<VictoryPointFlightInfo>();
            _victoryPointFlightsForHud.Add(new VictoryPointFlightInfo(amount));
        }

        /// <summary>HUD: pending VP flights from center screen to VP icon (cleared after call).</summary>
        public bool TryConsumeVictoryPointFlights(out List<VictoryPointFlightInfo> flights)
        {
            flights = _victoryPointFlightsForHud;
            _victoryPointFlightsForHud = null;
            return flights != null && flights.Count > 0;
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

        internal void NotifyUnitMoved(PlayerState owner, BoardTile from, BoardTile to)
        {
            if (owner == null || owner != CurrentPlayer || from == null || to == null)
                return;
            _anyMovementOccurredThisTurn = true;

            bool fromContested = TileHasEnemyForOwner(from, owner);
            bool toHasEnemy = TileHasEnemyForOwner(to, owner);
            bool isRetreatMove = fromContested && !toHasEnemy;

            if (isRetreatMove)
            {
                if (_activeRetreatSourceThisTurn == null)
                    _activeRetreatSourceThisTurn = from;
                else if (_activeRetreatSourceThisTurn != from)
                    _normalMovementOccurredThisTurn = true;
            }
            else
            {
                _normalMovementOccurredThisTurn = true;
            }
        }

        bool TileHasEnemyForOwner(BoardTile tile, PlayerState owner)
        {
            if (tile == null || owner == null)
                return false;
            foreach (var u in FindObjectsOfType<UnitInstance>())
            {
                if (u != null && u.Tile == tile && u.Owner != owner)
                    return true;
            }

            return false;
        }
    }
}

