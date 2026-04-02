using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NexusGame
{
    /// <summary>
    /// HUD: turn state, buy menu, battles, dragon strikes.
    /// </summary>
    public class DemoHUD : MonoBehaviour
    {
        public GameController Game;
        public MobileInputController InputController;
        public bool ShowDebugToggle = false;

        [Header("UI art")]
        [Tooltip("Rubium icon. Leave empty to load Assets/Resources/Sprites/Rubium.png at runtime.")]
        public Texture2D RubiumIcon;

        [Tooltip("Optional: assign a Sprite instead (e.g. if Texture Type is Sprite 2D). Overrides Resources path when set.")]
        public Sprite RubiumSprite;

        [Tooltip("Victory points icon. Default: Resources/Sprites/VP.png")]
        public Texture2D VPIcon;

        public Sprite VPSprite;

        [Tooltip("Mine yield icons (1–3 Rubium). Default: Resources/Sprites/OreChip1..3.png")]
        public Texture2D OreChip1Icon;

        public Texture2D OreChip2Icon;
        public Texture2D OreChip3Icon;

        public Sprite OreChip1Sprite;
        public Sprite OreChip2Sprite;
        public Sprite OreChip3Sprite;

        bool _showBuyMenu;
        bool _showQuickRef;
        bool _showSettingsMenu;
        int _quickRefTab; // 0 = rules, 1 = units
        Vector2 _scrollQuickRef;
        GUIStyle _quickRefBodyStyle;
        bool _showMyEnergizeHelp;
        Vector2 _scrollMyEnergizeHelp;
        PlayerState _energizeHelpSubject;
        bool _showEndGameStats;
        bool _moveAllChecked;
        BoardTile _moveAllTile;
        float _lastCardBarY;

        /// <summary>Which hand pile modal is open (tap a pile in the compact bar).</summary>
        enum HandPileViewerKind
        {
            None,
            Battle,
            Deploy,
            Secret
        }

        HandPileViewerKind _handPileViewer;

        Vector2 _scrollBattle;
        Vector2 _scrollBattleMainLog;
        Vector2 _scrollBattleLogPanel;
        int _lastBattleLogLen;
        Vector2 _scrollHand;
        Vector2 _scrollHandBattle;
        Vector2 _scrollHandDeploy;
        Vector2 _scrollHandSecret;
        Vector2 _scrollTilePanel;

        GUIStyle _cardTitleStyle;
        GUIStyle _cardBodyStyle;
        GUIStyle _cardBadgeStyle;
        GUIStyle _cardColumnLabelStyle;

        const float CardBarHeight = 152f;
        const float PhaseRibbonHeight = 26f;
        /// <summary>Card tiles in pile modal (full detail).</summary>
        const float CardTileW = 112f;
        const float CardTileH = 104f;

        /// <summary>Main HUD row: Rubium + VP icons (larger than discount line).</summary>
        const float MainHudIconHeight = 28f;

        const float DiscountLineIconHeight = 22f;

        NexusGuiImage _rubiumResources;
        bool _rubiumResourcesTried;

        NexusGuiImage _vpResources;
        bool _vpResourcesTried;

        NexusGuiImage _oreResources1;
        NexusGuiImage _oreResources2;
        NexusGuiImage _oreResources3;
        bool _oreResourcesTried;
        static Texture2D _dimTex;
        readonly Dictionary<UnitType, NexusGuiImage> _unitIconCache = new Dictionary<UnitType, NexusGuiImage>();
        GUIStyle _battleWindowStyle;
        Texture2D _battleWindowBg;
        GUIStyle _topIconButtonStyle;

        void Start()
        {
            if (Game == null)
                Game = FindObjectOfType<GameController>();
            if (InputController == null)
                InputController = FindObjectOfType<MobileInputController>();
        }

        NexusGuiImage GetRubiumGui()
        {
            var direct = NexusGuiArt.FromFields(RubiumIcon, RubiumSprite);
            if (!direct.IsEmpty)
                return direct;
            if (!_rubiumResourcesTried)
            {
                _rubiumResourcesTried = true;
                _rubiumResources = NexusGuiArt.Load("Sprites/Rubium", "Sprites/rubium");
            }

            return _rubiumResources;
        }

        NexusGuiImage GetVPGui()
        {
            var direct = NexusGuiArt.FromFields(VPIcon, VPSprite);
            if (!direct.IsEmpty)
                return direct;
            if (!_vpResourcesTried)
            {
                _vpResourcesTried = true;
                _vpResources = NexusGuiArt.Load("Sprites/VP", "Sprites/Vp");
            }

            return _vpResources;
        }

        void EnsureOreChipResources()
        {
            if (_oreResourcesTried)
                return;
            _oreResourcesTried = true;
            _oreResources1 = NexusGuiArt.Load("Sprites/OreChip1", "Sprites/Ore_Chip_1", "Sprites/Ore Chip 1");
            _oreResources2 = NexusGuiArt.Load("Sprites/OreChip2", "Sprites/Ore_Chip_2", "Sprites/Ore Chip 2");
            _oreResources3 = NexusGuiArt.Load("Sprites/OreChip3", "Sprites/Ore_Chip_3", "Sprites/Ore Chip 3");
        }

        /// <summary>Ore chip for mine yield 1–3; empty if yield is 0 or no art.</summary>
        NexusGuiImage GetOreChipGui(int mineYield)
        {
            switch (mineYield)
            {
                case 1:
                {
                    var d = NexusGuiArt.FromFields(OreChip1Icon, OreChip1Sprite);
                    if (!d.IsEmpty)
                        return d;
                    EnsureOreChipResources();
                    return _oreResources1;
                }
                case 2:
                {
                    var d = NexusGuiArt.FromFields(OreChip2Icon, OreChip2Sprite);
                    if (!d.IsEmpty)
                        return d;
                    EnsureOreChipResources();
                    return _oreResources2;
                }
                case 3:
                {
                    var d = NexusGuiArt.FromFields(OreChip3Icon, OreChip3Sprite);
                    if (!d.IsEmpty)
                        return d;
                    EnsureOreChipResources();
                    return _oreResources3;
                }
                default:
                    return default;
            }
        }

        void EnsureCardStyles()
        {
            if (_cardTitleStyle != null)
                return;
            _cardTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
            _cardBodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                normal = { textColor = new Color(0.15f, 0.15f, 0.15f) }
            };
            _cardBadgeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = Color.yellow }
            };
            _cardColumnLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        }

        void EnsureBattleWindowStyle()
        {
            if (_battleWindowStyle != null)
                return;
            _battleWindowStyle = new GUIStyle(GUI.skin.window);
            _battleWindowBg = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            // Keep only ~5% of board visible through battle window body (95% opaque).
            _battleWindowBg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.95f));
            _battleWindowBg.Apply();
            _battleWindowStyle.normal.background = _battleWindowBg;
            _battleWindowStyle.onNormal.background = _battleWindowBg;
            _battleWindowStyle.focused.background = _battleWindowBg;
            _battleWindowStyle.onFocused.background = _battleWindowBg;
            _battleWindowStyle.active.background = _battleWindowBg;
            _battleWindowStyle.onActive.background = _battleWindowBg;
        }

        void OnGUI()
        {
            if (Game == null || Game.Players.Count == 0)
                return;
            if (Game.IsGameOver && Game.FinalSnapshot != null)
            {
                DrawEndGameOverlay(Game.FinalSnapshot);
                return;
            }

            var player = Game.CurrentPlayer;

            DrawFullBattleOverlays(player);
            DrawDragonPhaseOverlay();

            const float topBarY = 6f;
            const float topBarH = 52f;
            EnsureTopIconButtonStyle();

            var rubGui = GetRubiumGui();
            var vpGui = GetVPGui();
            var hudLabel = GUI.skin.label;

            // Top strip (Colonist-style): resources left, info + settings right
            var topBarBg = new Color(0.06f, 0.07f, 0.12f, 0.88f);
            Color prevGui = GUI.color;
            GUI.color = topBarBg;
            GUI.DrawTexture(new Rect(0f, topBarY, Screen.width, topBarH), Texture2D.whiteTexture);
            GUI.color = prevGui;

            float lx = 12f;
            float ly = topBarY + (topBarH - MainHudIconHeight) * 0.5f - 2f;
            float resLineH = Mathf.Max(22f, MainHudIconHeight + 2f);
            float rxRes = lx;
            if (!rubGui.IsEmpty)
                rxRes += rubGui.Draw(rxRes, ly, MainHudIconHeight) + 6f;
            var rubNumStyle = new GUIStyle(hudLabel)
            {
                fontSize = Mathf.Clamp(Screen.width / 32, 15, 20),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            GUI.Label(new Rect(rxRes, ly - 2f, 120f, resLineH), player.Rubium.ToString(), rubNumStyle);
            rxRes += Mathf.Max(28f, rubNumStyle.CalcSize(new GUIContent(player.Rubium.ToString())).x) + 12f;
            if (!vpGui.IsEmpty)
                rxRes += vpGui.Draw(rxRes, ly, MainHudIconHeight) + 6f;
            GUI.Label(new Rect(rxRes, ly - 2f, 80f, resLineH), player.VictoryPoints.ToString(), rubNumStyle);

            const float iconBtn = 44f;
            float iconY = topBarY + (topBarH - iconBtn) * 0.5f;
            float iconRight = Screen.width - 12f - iconBtn * 2f - 10f;
            if (GUI.Button(new Rect(iconRight, iconY, iconBtn, iconBtn), "\u2139", _topIconButtonStyle))
            {
                _showSettingsMenu = false;
                _showQuickRef = true;
            }

            if (GUI.Button(new Rect(iconRight + iconBtn + 10f, iconY, iconBtn, iconBtn), "\u2699", _topIconButtonStyle))
            {
                _showQuickRef = false;
                _showSettingsMenu = true;
            }

            float metaY = topBarY + topBarH + 6f;
            float metaW = Mathf.Min(400f, Screen.width - 20f);
            lx = 18f;
            ly = metaY + 6f;
            float lw = metaW - 16f;

            string curPrefix =
                $"Turn {Game.TurnNumber}  ·  P{player.PlayerIndex + 1}{(Game.IsAiControlled(player) ? " (AI)" : "")}";
            var metaLineStyle = new GUIStyle(hudLabel) { fontSize = 12, wordWrap = true };
            GUI.Label(new Rect(lx, ly, lw, 36f), curPrefix, metaLineStyle);
            ly += 22f;

            int b = player.BattleEnergize?.Count ?? 0;
            int d = player.DeployEnergize?.Count ?? 0;
            int s = player.SecretMissions?.Count ?? 0;
            GUI.Label(new Rect(lx, ly, lw, 22f), $"⚔{b}   ▲{d}   ★{s}", hudLabel);
            ly += 24f;

            if (player.DeploymentPurchaseDiscountRubium > 0)
            {
                float dx = lx;
                if (!rubGui.IsEmpty)
                    dx += rubGui.Draw(dx, ly + 1f, DiscountLineIconHeight) + 6f;
                GUI.Label(new Rect(dx, ly, 200f, 20f), player.DeploymentPurchaseDiscountRubium.ToString(), hudLabel);
                ly += 22f;
            }

            var sb = new StringBuilder();
            if (Game.AiVsAiMode)
            {
                sb.AppendLine(
                    $"[AI test] Goal {Game.AiTestVictoryTargetVp} VP  |  draw cap {Game.AiTestMaxTotalDrawPhases} (T{Game.TurnNumber})");
                if (Game.AiTestMatchCompleted && Game.AiTestWinner != null)
                    sb.AppendLine(
                        $"MATCH OVER — P{Game.AiTestWinner.PlayerIndex + 1} wins ({Game.AiTestWinner.VictoryPoints} VP)");
            }

            if (sb.Length > 0)
            {
                var bodyStyle = new GUIStyle(hudLabel) { wordWrap = true, fontSize = 11 };
                float bodyH = bodyStyle.CalcHeight(new GUIContent(sb.ToString()), lw);
                bodyH = Mathf.Clamp(bodyH + 6f, 24f, 220f);
                GUI.Label(new Rect(lx, ly, lw, bodyH), sb.ToString(), bodyStyle);
                ly += bodyH + 6f;
            }

            if (!string.IsNullOrEmpty(Game.LastDrawPhaseLog))
            {
                float logBarH = 40f;
                GUI.Box(new Rect(10f, ly, Mathf.Min(600f, Screen.width - 20f), logBarH), "");
                GUI.Label(new Rect(16f, ly + 10f, Screen.width - 32f, logBarH - 8f), Game.LastDrawPhaseLog,
                    new GUIStyle(hudLabel) { fontSize = 10, wordWrap = true });
                ly += logBarH + 6f;
            }

            string battleLog =
                !string.IsNullOrEmpty(Game.LiveBattlePhaseLog) ? Game.LiveBattlePhaseLog : Game.LastBattlePhaseLog;

            float hudBottom = ly + 4f;
            const float battleLogPanelH = 140f;
            if (!string.IsNullOrEmpty(battleLog) && !Game.PendingBattleArrangement)
            {
                var battleRect = new Rect(10, hudBottom + 8f, 420, battleLogPanelH);
                GUI.Box(battleRect, "Battle log");
                string safe = UiSafeText(battleLog);
                float viewX = battleRect.x + 6f;
                float viewY = battleRect.y + 20f;
                float viewW = battleRect.width - 12f;
                float viewH = battleRect.height - 26f;
                var battleLogStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
                float contentH = Mathf.Max(viewH, battleLogStyle.CalcHeight(new GUIContent(safe), viewW - 16f) + 8f);
                var view = new Rect(viewX, viewY, viewW, viewH);
                var content = new Rect(0f, 0f, viewW - 16f, contentH);
                _scrollBattleLogPanel = GUI.BeginScrollView(view, _scrollBattleLogPanel, content);
                GUI.Label(new Rect(0f, 0f, content.width, content.height), safe, battleLogStyle);
                GUI.EndScrollView();
            }

            if (ShowDebugToggle && InputController != null)
            {
                float dbgY = Mathf.Min(hudBottom + 6f, Screen.height - 120f);
                bool newDebug = GUI.Toggle(new Rect(10, dbgY, 180, 22), InputController.DebugClicks, "Debug clicks");
                InputController.DebugClicks = newDebug;
            }

            float topY = string.IsNullOrEmpty(battleLog) || Game.PendingBattleArrangement
                ? hudBottom + 8f
                : hudBottom + 8f + battleLogPanelH + 10f;
            if (Game.DragonPhase != null)
                topY = Mathf.Max(topY, Screen.height - 220f);
            // Keep main buttons above bottom card strip + dragon strip
            float reserveBottom = CardBarHeight + PhaseRibbonHeight + 24f + (Game.DragonPhase != null ? 200f : 0f);
            topY = Mathf.Min(topY, Mathf.Max(60f, Screen.height - reserveBottom));

            if (Game.BattlePhaseBlockingPlay || Game.DragonPhase != null || Game.IsAiControlled(player))
                GUI.enabled = false;
            if (GUI.Button(new Rect(10, topY, 130, 28), "End Turn"))
            {
                Game.EndTurn();
                _showBuyMenu = false;
            }

            GUI.enabled = true;

            bool canBuyHere = false;
            if (InputController != null && InputController.SelectedTile != null)
            {
                var sel = InputController.SelectedTile;
                if (Game.CanDeployToStartingHomeTile(player, sel))
                    canBuyHere = true;
            }
            if (Game.AnyMovementOccurredThisTurn)
                canBuyHere = false;

            if (!canBuyHere)
                GUI.enabled = false;
            if (GUI.Button(new Rect(150, topY, 40, 28), "$"))
                _showBuyMenu = !_showBuyMenu;

            GUI.enabled = true;

            if (_showBuyMenu && canBuyHere)
            {
                int y = (int)topY + 35;
                GUILayout.BeginArea(new Rect(10, y, 360, 520));
                GUILayout.Label("- Units -", GUI.skin.box);
                DrawBuyButtonGui("Human (1)", UnitType.Human, 1);
                DrawBuyButtonGui("Fungoid (2)", UnitType.Fungoid, 2);
                DrawBuyButtonGui("Crystalline (2)", UnitType.Crystalline, 2);
                DrawBuyButtonGui("Rock Strider (3)", UnitType.RockStrider, 3);
                DrawBuyButtonGui("Lava Leaper (4)", UnitType.LavaLeaper, 4);
                DrawBuyButtonGui("Rubium Dragon (8)", UnitType.RubiumDragon, 8);
                GUILayout.Space(6);
                GUILayout.Label("- Deployment Energize -", GUI.skin.box);
                var sel = InputController != null ? InputController.SelectedTile : null;
                if (Game.AnyMovementOccurredThisTurn)
                    GUILayout.Label("(Deployment locked after any movement this turn)");
                foreach (var g in player.DeployEnergize.GroupBy(x => x).OrderBy(x => x.Key.ToString()))
                {
                    var id = g.Key;
                    int n = g.Count();
                    string note = id == EnergizeDeploymentId.FreeHuman &&
                                  (sel == null || !Game.CanDeployToStartingHomeTile(player, sel))
                        ? " [select home hex]"
                        : "";
                    if (Game.AnyMovementOccurredThisTurn)
                        GUI.enabled = false;
                    if (GUILayout.Button(EnergizeDeploymentCatalog.GetName(id) + " x" + n + note))
                        Game.TryPlayDeploymentEnergize(id, sel);
                    GUI.enabled = true;
                }

                if (player.DeployEnergize.Count == 0)
                    GUILayout.Label("(No deployment cards)");
                GUILayout.EndArea();
            }

            DrawBottomCardHand(player);
            DrawPhaseRibbon(player);

            DrawHandPileViewerOverlay(player);
            DrawBattleFocusOverlay();
            DrawEnergizeHelpWindow();

            if (_showSettingsMenu)
                DrawSettingsOverlay();

            if (_showQuickRef)
                DrawQuickReferenceOverlay();
        }

        void EnsureTopIconButtonStyle()
        {
            if (_topIconButtonStyle != null)
                return;
            _topIconButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }

        void DrawSettingsOverlay()
        {
            var dim = new Color(0.02f, 0.02f, 0.06f, 0.78f);
            var prev = GUI.color;
            GUI.color = dim;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = prev;

            float w = Mathf.Min(340f, Screen.width - 32f);
            float h = 220f;
            var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.Box(panel, "");

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(new Rect(panel.x, panel.y + 12f, panel.width, 28f), "Settings", titleStyle);

            var btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            float by = panel.y + 56f;
            float bw = panel.width - 32f;
            float bx = panel.x + 16f;
            float bh = 48f;

            if (GUI.Button(new Rect(bx, by, bw, bh), "LEAVE GAME", btnStyle))
            {
                _showSettingsMenu = false;
                var bootstrap = FindObjectOfType<Bootstrap>();
                if (bootstrap != null)
                    bootstrap.ReturnToMainMenu();
                else
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }

            by += bh + 14f;
            if (GUI.Button(new Rect(bx, by, bw, 42f), "Close", GUI.skin.button))
                _showSettingsMenu = false;
        }

        void EnsureQuickRefBodyStyle()
        {
            if (_quickRefBodyStyle != null)
                return;
            _quickRefBodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true
            };
        }

        void DrawQuickReferenceOverlay()
        {
            EnsureQuickRefBodyStyle();

            var dim = new Color(0.02f, 0.02f, 0.06f, 0.72f);
            var prev = GUI.color;
            GUI.color = dim;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = prev;

            float pad = 16f;
            var panel = new Rect(pad, pad, Screen.width - 2f * pad, Screen.height - 2f * pad);
            GUI.Box(panel, "");

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            string header = _quickRefTab == 0 ? NexusRulebook.Title : NexusUnitQuickReference.Title;
            GUI.Label(new Rect(panel.x, panel.y + 6f, panel.width, 26f), header, titleStyle);

            const float tabH = 26f;
            float tabY = panel.y + 34f;
            if (GUI.Button(new Rect(panel.x + 12f, tabY, 88f, tabH), "Rules"))
            {
                if (_quickRefTab != 0)
                    _scrollQuickRef = Vector2.zero;
                _quickRefTab = 0;
            }

            if (GUI.Button(new Rect(panel.x + 106f, tabY, 88f, tabH), "Units"))
            {
                if (_quickRefTab != 1)
                    _scrollQuickRef = Vector2.zero;
                _quickRefTab = 1;
            }

            var cfg = Game != null ? Game.Config : null;
            string body = _quickRefTab == 0
                ? NexusRulebook.Body
                : NexusUnitQuickReference.Build(cfg);

            const float closeH = 36f;
            var scrollRect = new Rect(panel.x + 12f, panel.y + 34f + tabH + 6f, panel.width - 24f,
                panel.height - 34f - tabH - 6f - closeH - 14f);
            float innerW = scrollRect.width - 22f;
            float contentH = _quickRefBodyStyle.CalcHeight(new GUIContent(body), innerW);
            contentH = Mathf.Max(contentH + 32f, scrollRect.height * 0.45f);

            _scrollQuickRef = GUI.BeginScrollView(scrollRect, _scrollQuickRef, new Rect(0f, 0f, innerW, contentH));
            GUI.Label(new Rect(8f, 8f, innerW - 16f, contentH), body, _quickRefBodyStyle);
            GUI.EndScrollView();

            if (GUI.Button(new Rect(panel.xMax - 188f, panel.yMax - closeH - 10f, 168f, closeH), "Close"))
                _showQuickRef = false;
        }

        void DrawEnergizeHelpWindow()
        {
            if (!_showMyEnergizeHelp)
                return;

            float winH = Mathf.Min(480f, Screen.height - 100f);
            var r = new Rect(12f, 80f, Mathf.Min(580f, Screen.width - 24f), winH);
            GUI.Window(953, r, _ =>
            {
                var subject = _energizeHelpSubject != null ? _energizeHelpSubject : Game.CurrentPlayer;
                if (subject == null)
                {
                    if (GUILayout.Button("Close"))
                        _showMyEnergizeHelp = false;
                    return;
                }

                GUILayout.Label(
                    $"P{subject.PlayerIndex + 1}{(Game.IsAiControlled(subject) ? " (AI)" : "")} - Energize in hand",
                    GUI.skin.box);

                float scrollH = Mathf.Max(120f, winH - 110f);
                _scrollMyEnergizeHelp = GUILayout.BeginScrollView(_scrollMyEnergizeHelp, GUILayout.Height(scrollH));

                bool hasBattle = subject.BattleEnergize != null && subject.BattleEnergize.Count > 0;
                bool hasDeploy = subject.DeployEnergize != null && subject.DeployEnergize.Count > 0;
                if (!hasBattle && !hasDeploy)
                    GUILayout.Label("No Energize cards in hand.");

                if (hasBattle)
                {
                    GUILayout.Label("Battle (pre-dice step)", GUI.skin.box);
                    foreach (var g in subject.BattleEnergize.GroupBy(x => x).OrderBy(x => x.Key.ToString()))
                    {
                        GUILayout.Label($"- {EnergizeBattleCatalog.GetName(g.Key)}  x{g.Count()}");
                        GUILayout.Label(EnergizeBattleCatalog.GetDescription(g.Key));
                        GUILayout.Space(6f);
                    }
                }

                if (hasDeploy)
                {
                    GUILayout.Label("Deployment (buy phase)", GUI.skin.box);
                    foreach (var g in subject.DeployEnergize.GroupBy(x => x).OrderBy(x => x.Key.ToString()))
                    {
                        GUILayout.Label($"- {EnergizeDeploymentCatalog.GetName(g.Key)}  x{g.Count()}");
                        GUILayout.Label(EnergizeDeploymentCatalog.GetDescription(g.Key));
                        GUILayout.Space(6f);
                    }
                }

                GUILayout.EndScrollView();
                if (GUILayout.Button("Close"))
                    _showMyEnergizeHelp = false;
            }, "What do my Energize cards do?");
        }
        void DrawBottomCardHand(PlayerState player)
        {
            EnsureCardStyles();

            float barY = Game.DragonPhase != null
                ? Screen.height - 200f - CardBarHeight - PhaseRibbonHeight - 12f
                : Screen.height - CardBarHeight - PhaseRibbonHeight - 12f;
            barY = Mathf.Max(40f, barY);
            _lastCardBarY = barY;

            float barX = 8f;
            float barW = Screen.width - 16f;
            GUI.Box(new Rect(barX, barY, barW, CardBarHeight), "");

            const float pad = 8f;
            const float headerH = 18f;
            string deckLine =
                $"P{player.PlayerIndex + 1}  ·  Secret deck {Game.SecretDeckCount}  ·  Energize {Game.EnergizeDeckCount}  ·  tap a pile";
            var deckStyle = new GUIStyle(_cardColumnLabelStyle) { fontSize = 10 };
            GUI.Label(new Rect(barX + pad, barY + 2, barW - pad * 2f, 16f), deckLine, deckStyle);

            float innerX = barX + pad;
            float innerW = barW - pad * 2f;
            float contentY = barY + headerH;
            float contentH = CardBarHeight - headerH - 4f;

            const float splitGap = 8f;
            const float minTilePanelW = 120f;
            float maxLeft = Mathf.Max(160f, innerW - minTilePanelW - splitGap);
            float leftW = Mathf.Min(maxLeft, Mathf.Max(200f, innerW * 0.42f));
            float rightW = innerW - leftW - splitGap;
            float rightX = innerX + leftW + splitGap;

            float colGap = 6f;
            float colW = (leftW - colGap * 2f) / 3f;

            int bCount = player.BattleEnergize?.Count ?? 0;
            int dCount = player.DeployEnergize?.Count ?? 0;
            int sCount = player.SecretMissions?.Count ?? 0;

            var pileBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter
            };

            if (GUI.Button(new Rect(innerX, contentY, colW, contentH),
                    $"Battle\n⚔ {bCount}", pileBtnStyle))
            {
                _handPileViewer = _handPileViewer == HandPileViewerKind.Battle
                    ? HandPileViewerKind.None
                    : HandPileViewerKind.Battle;
            }

            if (GUI.Button(new Rect(innerX + colW + colGap, contentY, colW, contentH),
                    $"Deploy\n▲ {dCount}", pileBtnStyle))
            {
                _handPileViewer = _handPileViewer == HandPileViewerKind.Deploy
                    ? HandPileViewerKind.None
                    : HandPileViewerKind.Deploy;
            }

            if (GUI.Button(new Rect(innerX + (colW + colGap) * 2f, contentY, colW, contentH),
                    $"Secrets\n★ {sCount}", pileBtnStyle))
            {
                _handPileViewer = _handPileViewer == HandPileViewerKind.Secret
                    ? HandPileViewerKind.None
                    : HandPileViewerKind.Secret;
            }

            if (_handPileViewer != HandPileViewerKind.None)
            {
                var hi = new Rect(innerX, contentY, colW, contentH);
                if (_handPileViewer == HandPileViewerKind.Battle)
                    DrawOutlineRect(hi, new Color(0.95f, 0.78f, 0.2f, 0.95f), 2f);
                hi.x = innerX + colW + colGap;
                if (_handPileViewer == HandPileViewerKind.Deploy)
                    DrawOutlineRect(hi, new Color(0.95f, 0.78f, 0.2f, 0.95f), 2f);
                hi.x = innerX + (colW + colGap) * 2f;
                if (_handPileViewer == HandPileViewerKind.Secret)
                    DrawOutlineRect(hi, new Color(0.95f, 0.78f, 0.2f, 0.95f), 2f);
            }

            DrawBottomTilePanel(rightX, contentY, rightW, contentH, player);
        }

        void DrawBottomTilePanel(float x, float y, float w, float h, PlayerState player)
        {
            var panel = new Rect(x, y, w, h);
            GUI.Box(panel, "");

            var popupTile = InputController != null ? InputController.SelectedTile : null;
            var titleStyle = new GUIStyle(_cardColumnLabelStyle)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft
            };
            string title = popupTile != null ? "Tile " + popupTile.Type : "Selected tile";
            GUI.Label(new Rect(panel.x + 6f, panel.y + 4f, panel.width - 12f, 16f), title, titleStyle);

            const float tileScrollContentH = 360f;
            var scrollView = new Rect(panel.x + 4f, panel.y + 22f, panel.width - 8f, panel.height - 26f);
            float innerW = Mathf.Max(80f, scrollView.width - 18f);

            if (popupTile == null)
            {
                var hint = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true,
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11,
                    normal = { textColor = new Color(0.75f, 0.75f, 0.8f) }
                };
                GUI.Label(scrollView, "Tap the board to select a tile.", hint);
                return;
            }

            _scrollTilePanel = GUI.BeginScrollView(scrollView, _scrollTilePanel,
                new Rect(0f, 0f, innerW, tileScrollContentH));
            GUILayout.BeginArea(new Rect(0f, 0f, innerW, tileScrollContentH));
            DrawSelectedTilePanelBody(player, popupTile, innerW);
            GUILayout.EndArea();
            GUI.EndScrollView();
        }

        void DrawHandPileViewerOverlay(PlayerState player)
        {
            if (_handPileViewer == HandPileViewerKind.None || player == null)
                return;

            var dim = new Color(0.02f, 0.02f, 0.06f, 0.55f);
            Color prev = GUI.color;
            GUI.color = dim;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = prev;

            float w = Mathf.Min(Screen.width - 24f, 720f);
            float h = Mathf.Min(Screen.height - 80f, 420f);
            var win = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            string title = _handPileViewer switch
            {
                HandPileViewerKind.Battle => "Battle Energize",
                HandPileViewerKind.Deploy => "Deployment Energize",
                HandPileViewerKind.Secret => "Secret missions",
                _ => "Hand"
            };

            GUI.Box(win, "");
            DrawOutlineRect(win, new Color(0.95f, 0.82f, 0.2f, 0.95f), 2f);
            GUI.Label(new Rect(win.x + 12f, win.y + 8f, win.width - 100f, 22f), title, _cardColumnLabelStyle);
            if (GUI.Button(new Rect(win.xMax - 88f, win.y + 6f, 76f, 26f), "Close"))
                _handPileViewer = HandPileViewerKind.None;

            var content = new Rect(win.x + 10f, win.y + 38f, win.width - 20f, win.height - 48f);
            if (_handPileViewer == HandPileViewerKind.Battle)
                DrawHandPileModalBattle(content, player);
            else if (_handPileViewer == HandPileViewerKind.Deploy)
                DrawHandPileModalDeploy(content, player);
            else if (_handPileViewer == HandPileViewerKind.Secret)
                DrawHandPileModalSecret(content, player);
        }

        void DrawHandPileModalBattle(Rect content, PlayerState player)
        {
            var battleGroups = player.BattleEnergize.GroupBy(x => x).OrderBy(g => g.Key.ToString()).ToList();
            float cw = battleGroups.Count == 0
                ? CardTileW + 8f
                : battleGroups.Count * (CardTileW + 8f);
            cw = Mathf.Max(cw, content.width);
            _scrollHandBattle = GUI.BeginScrollView(content, _scrollHandBattle,
                new Rect(0, 0, cw, CardTileH + 8f));
            if (battleGroups.Count == 0)
                DrawPlaceholderCard(new Rect(4f, 4f, CardTileW, CardTileH), "No cards");
            else
            {
                float x = 4f;
                foreach (var g in battleGroups)
                {
                    string full = EnergizeBattleCatalog.GetName(g.Key);
                    DrawPlayingCard(new Rect(x, 4f, CardTileW, CardTileH), new Color(0.15f, 0.28f, 0.55f),
                        CardShortTitle(full), CardDetailFromName(full), g.Count());
                    x += CardTileW + 8f;
                }
            }

            GUI.EndScrollView();
        }

        void DrawHandPileModalDeploy(Rect content, PlayerState player)
        {
            var deployGroups = player.DeployEnergize.GroupBy(x => x).OrderBy(g => g.Key.ToString()).ToList();
            float cw = deployGroups.Count == 0
                ? CardTileW + 8f
                : deployGroups.Count * (CardTileW + 8f);
            cw = Mathf.Max(cw, content.width);
            _scrollHandDeploy = GUI.BeginScrollView(content, _scrollHandDeploy,
                new Rect(0, 0, cw, CardTileH + 8f));
            if (deployGroups.Count == 0)
                DrawPlaceholderCard(new Rect(4f, 4f, CardTileW, CardTileH), "No cards");
            else
            {
                float x = 4f;
                foreach (var g in deployGroups)
                {
                    string full = EnergizeDeploymentCatalog.GetName(g.Key);
                    DrawPlayingCard(new Rect(x, 4f, CardTileW, CardTileH), new Color(0.15f, 0.45f, 0.25f),
                        CardShortTitle(full), CardDetailFromName(full), g.Count());
                    x += CardTileW + 8f;
                }
            }

            GUI.EndScrollView();
        }

        void DrawHandPileModalSecret(Rect content, PlayerState player)
        {
            if (player.SecretMissions == null || player.SecretMissions.Count == 0)
            {
                _scrollHandSecret = GUI.BeginScrollView(content, _scrollHandSecret,
                    new Rect(0, 0, content.width, CardTileH + 8f));
                DrawPlaceholderCard(new Rect(4f, 4f, CardTileW, CardTileH), "No missions");
                GUI.EndScrollView();
                return;
            }

            float cw = player.SecretMissions.Count * (CardTileW + 8f);
            cw = Mathf.Max(cw, content.width);
            float ch = CardTileH + 16f;
            _scrollHandSecret = GUI.BeginScrollView(content, _scrollHandSecret, new Rect(0, 0, cw, ch));
            float x = 4f;
            for (int i = 0; i < player.SecretMissions.Count; i++)
            {
                var s = player.SecretMissions[i];
                string full = SecretMissionLabel(s) + " (+" + s.VictoryPoints + " VP)";
                DrawPlayingCard(new Rect(x, 4f, CardTileW, CardTileH), new Color(0.42f, 0.15f, 0.5f),
                    "#" + i + " " + CardShortTitle(full), CardDetailFromName(full), 1);
                x += CardTileW + 8f;
            }

            GUI.EndScrollView();
        }

        void DrawPhaseRibbon(PlayerState player)
        {
            float y = _lastCardBarY + CardBarHeight + 4f;
            if (y + PhaseRibbonHeight > Screen.height - 2f)
                y = _lastCardBarY - PhaseRibbonHeight - 4f;
            y = Mathf.Clamp(y, 4f, Screen.height - PhaseRibbonHeight - 2f);
            float x = 8f;
            float w = Screen.width - 16f;
            GUI.Box(new Rect(x, y, w, PhaseRibbonHeight), "");

            string[] phases = { "Draw", "Deployment", "Movement", "Battle", "Dragon", "End Turn" };
            string active = ActivePhaseLabel(player);
            float segW = (w - 8f) / phases.Length;
            for (int i = 0; i < phases.Length; i++)
            {
                var r = new Rect(x + 4f + segW * i, y + 2f, segW - 2f, PhaseRibbonHeight - 6f);
                bool on = phases[i] == active;
                var prev = GUI.color;
                GUI.color = on ? new Color(0.95f, 0.78f, 0.18f, 0.95f) : new Color(0.35f, 0.35f, 0.35f, 0.9f);
                GUI.Box(r, phases[i]);
                GUI.color = prev;
            }
        }

        string ActivePhaseLabel(PlayerState player)
        {
            if (Game.IsGameOver)
                return "End Turn";
            if (Game.DragonPhase != null)
                return "Dragon";
            if (Game.BattlePhaseBlockingPlay || Game.PendingBattleArrangement || Game.ActiveBattleHex != null)
                return "Battle";
            if (_showBuyMenu)
                return "Deployment";
            // In this implementation, deployment purchases/cards are available during movement window.
            if (player != null && !Game.IsAiControlled(player))
                return "Movement";
            return "Draw";
        }

        void DrawPlaceholderCard(Rect r, string text)
        {
            GUI.Box(r, "");
            DrawTintedRect(new Rect(r.x + 2, r.y + 2, r.width - 4, 22), new Color(0.3f, 0.3f, 0.3f));
            GUI.Label(new Rect(r.x + 6, r.y + 32, r.width - 12, r.height - 38), text, _cardBodyStyle);
        }

        void DrawPlayingCard(Rect r, Color headerColor, string title, string detail, int stack)
        {
            GUI.Box(r, "");
            DrawTintedRect(new Rect(r.x + 2, r.y + 2, r.width - 4, 22), headerColor);
            GUI.Label(new Rect(r.x + 4, r.y + 3, r.width - 32, 20), title, _cardTitleStyle);
            if (stack > 1)
                GUI.Label(new Rect(r.x + r.width - 30, r.y + 3, 26, 20), "x" + stack, _cardBadgeStyle);
            GUI.Label(new Rect(r.x + 6, r.y + 26, r.width - 12, r.height - 32), detail, _cardBodyStyle);
        }

        static void DrawTintedRect(Rect r, Color c)
        {
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = prev;
        }

        static void DrawOutlineRect(Rect r, Color color, float thickness)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - thickness, r.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, thickness, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - thickness, r.y, thickness, r.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        static string CardShortTitle(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
                return "";
            int cut = fullName.IndexOf('(');
            string s = cut > 0 ? fullName.Substring(0, cut).Trim() : fullName;
            if (s.Length > 22)
                s = s.Substring(0, 20) + "...";
            return s;
        }

        static string CardDetailFromName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
                return "";
            int cut = fullName.IndexOf('(');
            if (cut > 0 && cut < fullName.Length - 1)
                return fullName.Substring(cut).Trim();
            return fullName;
        }

        void DrawFullBattleOverlays(PlayerState currentPlayer)
        {
            bool active = Game.PendingBattleArrangement ||
                          Game.EnergizePromptPlayer != null ||
                          Game.FocusFirePicker != null ||
                          Game.CasualtyPick != null ||
                          (Game.SecretMissionOffer != null && Game.SecretMissionOffer.Waiting) ||
                          Game.ActiveBattleHex != null;
            if (!active)
                return;

            var actor = Game.EnergizePromptPlayer ?? Game.FocusFirePicker ?? Game.CasualtyPick?.Owner ??
                        Game.SecretMissionOffer?.Attacker ?? currentPlayer;
            if (actor != null && Game.IsAiControlled(actor))
                return;

            // If player has no battle Energize cards, auto-pass and do not show prompt UI.
            if (Game.EnergizePromptPlayer != null &&
                Game.FocusFirePicker == null &&
                (Game.EnergizePromptPlayer.BattleEnergize == null || Game.EnergizePromptPlayer.BattleEnergize.Count == 0))
            {
                Game.SubmitEnergizePass();
                return;
            }

            float w = Mathf.Min(Screen.width - 30f, 900f);
            float h = Mathf.Min(Screen.height - 30f, 620f);
            var r = new Rect((Screen.width - w) * 0.5f, 15f, w, h);
            EnsureBattleWindowStyle();
            GUI.Window(900, r, _ => { BattleMainWindow(currentPlayer, h); }, "Battle", _battleWindowStyle);
            DrawOutlineRect(r, new Color(0.95f, 0.82f, 0.2f, 0.95f), 2f);
        }

        void BattleMainWindow(PlayerState currentPlayer, float windowHeight)
        {
            var left = Game.ActiveBattleAttacker ?? currentPlayer;
            var right = Game.ActiveBattleDefender;
            var hex = Game.ActiveBattleHex;

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(UiSafeText(Game.EnergizeBattleContext ?? (hex != null ? "Battle in progress" : "Battle preparation")));
            GUILayout.Box(BattleNextActionText());
            if (Game.HasActiveBattleStep)
                GUILayout.Box(BattleStepLabel(Game.ActiveBattleStepUnitType));
            DrawBattleHitSummaryRow();
            DrawBattleOrderRibbon();
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            DrawBattleSideColumn(left, hex, true, Game.ActiveBattleAttacker);
            GUILayout.Space(12f);
            DrawBattleSideColumn(right, hex, false, Game.ActiveBattleDefender);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.Space(4f);

            GUILayout.BeginVertical(GUI.skin.box);
            if (Game.PendingBattleArrangement)
                BattleArrangeWindow();
            else if (Game.FocusFirePicker != null)
                FocusFireWindow();
            else if (Game.EnergizePromptPlayer != null)
                EnergizeWindow();
            else if (Game.CasualtyPick != null)
                CasualtyWindow();
            else if (Game.SecretMissionOffer != null && Game.SecretMissionOffer.Waiting)
                SecretMissionWindow();

            string lockedReason = BattleLockedReasonText();
            if (!string.IsNullOrEmpty(lockedReason))
                GUILayout.Label("Unavailable now: " + lockedReason);
            GUILayout.EndVertical();

            GUILayout.Space(6f);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Battle log", GUI.skin.box);
            string battleLog = !string.IsNullOrEmpty(Game.LiveBattlePhaseLog) ? Game.LiveBattlePhaseLog : Game.LastBattlePhaseLog;
            string safe = UiSafeText(battleLog);
            if (safe.Length != _lastBattleLogLen)
            {
                _lastBattleLogLen = safe.Length;
                _scrollBattleMainLog.y = 100000f;
            }
            float logH = Mathf.Max(90f, windowHeight * 0.23f);
            _scrollBattleMainLog = GUILayout.BeginScrollView(_scrollBattleMainLog, GUILayout.Height(logH));
            GUILayout.Label(string.IsNullOrEmpty(safe) ? "(No battle log yet)" : safe);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        void DrawBattleSideColumn(PlayerState player, BoardTile hex, bool isLeft, PlayerState expectedSide)
        {
            string title = player == null ? (isLeft ? "Your side" : "Opponent") : $"P{player.PlayerIndex + 1}";
            string sideTag = expectedSide != null && player == expectedSide
                ? (player == Game.ActiveBattleAttacker ? "ATTACKER" : "DEFENDER")
                : "";
            var prev = GUI.color;
            if (player == Game.ActiveBattleAttacker)
                GUI.color = new Color(0.2f, 0.38f, 0.8f, 0.95f);
            else if (player == Game.ActiveBattleDefender)
                GUI.color = new Color(0.78f, 0.26f, 0.2f, 0.95f);
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width((Screen.width - 150f) * 0.45f), GUILayout.MinHeight(86f));
            GUI.color = prev;
            GUILayout.Label(string.IsNullOrEmpty(sideTag) ? title : $"{title} ({sideTag})");
            if (player == null || hex == null)
            {
                GUILayout.Label("(waiting)");
                GUILayout.EndVertical();
                return;
            }

            var counts = new Dictionary<UnitType, int>();
            foreach (var u in FindObjectsOfType<UnitInstance>())
            {
                if (u.Tile != hex || u.Owner != player)
                    continue;
                if (!counts.ContainsKey(u.Definition.Type))
                    counts[u.Definition.Type] = 0;
                counts[u.Definition.Type]++;
            }

            if (counts.Count == 0)
                GUILayout.Label("(none)");
            else
            {
                foreach (var kvp in counts.OrderBy(k => Array.IndexOf(BattleResolver.BattleOrder, k.Key)))
                {
                    GUILayout.BeginHorizontal();
                    var ir = GUILayoutUtility.GetRect(16f, 16f, GUILayout.Width(18f), GUILayout.Height(16f));
                    DrawUnitMiniIcon(ir, kvp.Key);
                    GUILayout.Label(UnitUiName(kvp.Key) + " x" + kvp.Value);
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.EndVertical();
        }

        void DrawBattleHitSummaryRow()
        {
            if (Game.ActiveBattleAttacker == null || Game.ActiveBattleDefender == null)
                return;
            GUILayout.BeginHorizontal();
            DrawBattleChip($"Hits on P{Game.ActiveBattleAttacker.PlayerIndex + 1}: {Game.ActiveBattleHitsOnAttacker}",
                new Color(0.25f, 0.4f, 0.95f, 0.9f));
            GUILayout.Space(10f);
            DrawBattleChip($"Hits on P{Game.ActiveBattleDefender.PlayerIndex + 1}: {Game.ActiveBattleHitsOnDefender}",
                new Color(0.92f, 0.3f, 0.24f, 0.9f));
            GUILayout.EndHorizontal();
        }

        void DrawBattleChip(string text, Color color)
        {
            var prev = GUI.color;
            GUI.color = color;
            GUILayout.Box(text, GUILayout.Height(24f));
            GUI.color = prev;
        }

        void DrawBattleOrderRibbon()
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            foreach (var t in BattleResolver.BattleOrder)
            {
                bool active = Game.HasActiveBattleStep && Game.ActiveBattleStepUnitType == t;
                var prev = GUI.color;
                GUI.color = active ? new Color(1f, 0.85f, 0.2f, 1f) : new Color(0.35f, 0.35f, 0.35f, 0.9f);
                GUILayout.Box(UnitUiName(t), GUILayout.Height(20f));
                GUI.color = prev;
            }
            GUILayout.EndHorizontal();
        }

        string BattleNextActionText()
        {
            if (Game.PendingBattleArrangement)
                return "Next action: arrange/confirm active battles.";
            if (Game.FocusFirePicker != null)
                return $"Next action: P{Game.FocusFirePicker.PlayerIndex + 1} picks Focus Fire unit type.";
            if (Game.EnergizePromptPlayer != null)
                return $"Next action: P{Game.EnergizePromptPlayer.PlayerIndex + 1} plays Battle Energize or passes.";
            if (Game.CasualtyPick != null)
                return $"Next action: P{Game.CasualtyPick.Owner.PlayerIndex + 1} selects casualties.";
            if (Game.SecretMissionOffer != null && Game.SecretMissionOffer.Waiting)
                return $"Next action: P{Game.SecretMissionOffer.Attacker.PlayerIndex + 1} can play one secret mission.";
            if (Game.HasActiveBattleStep)
                return $"Resolving: {BattleStepLabel(Game.ActiveBattleStepUnitType)}.";
            return "Battle phase active.";
        }

        string BattleLockedReasonText()
        {
            if (Game.PendingBattleArrangement)
                return "card play and casualties unlock after battle confirmation.";
            if (Game.EnergizePromptPlayer != null)
                return "waiting for energize response before dice step.";
            if (Game.FocusFirePicker != null)
                return "waiting for focus-fire type selection.";
            if (Game.CasualtyPick != null)
                return "other actions locked until casualties are confirmed.";
            if (Game.SecretMissionOffer != null && Game.SecretMissionOffer.Waiting)
                return "waiting for secret mission decision.";
            return "";
        }

        string WindowTitleEnergize()
        {
            var p = Game.EnergizePromptPlayer;
            return p != null ? "Energize P" + (p.PlayerIndex + 1) : "Energize";
        }

        string WindowTitleCasualty()
        {
            var c = Game.CasualtyPick;
            return c != null
                ? "Casualties P" + (c.Owner.PlayerIndex + 1) + " (" + c.Required + ")"
                : "Casualties";
        }

        void WindowBattleArrange(int id)
        {
            BattleArrangeWindow();
        }

        void WindowEnergizeBattle(int id)
        {
            EnergizeWindow();
        }

        void WindowFocusFire(int id)
        {
            FocusFireWindow();
        }

        void WindowCasualty(int id)
        {
            CasualtyWindow();
        }

        void WindowSecretMission(int id)
        {
            SecretMissionWindow();
        }

        void BattleArrangeWindow()
        {
            if (Game.BattlePlan == null || Game.BattlePlan.Count == 0)
            {
                GUILayout.Label("No battles to resolve.");
                return;
            }

            if (Game.BattlePlan.Count == 1)
            {
                var e = Game.BattlePlan[0];
                GUILayout.Label("Battle ready");
                GUILayout.BeginHorizontal();
                GUILayout.Label("Battle vs", GUILayout.Width(90));
                GUILayout.Label("P" + (e.DefenderPlayerIndex + 1), GUILayout.Width(48));
                GUILayout.EndHorizontal();
                GUILayout.Space(8);
                if (GUILayout.Button("Confirm - start battle", GUILayout.Height(36)))
                    Game.ConfirmBattleArrangement();
                return;
            }

            bool canReorder = Game.BattlePlan.Count > 1;
            GUILayout.Label(canReorder
                ? "Battle order (top first). Reorder because multiple battles are active."
                : "One battle active.");
            float arrangeListH = Mathf.Clamp(Screen.height * 0.18f, 110f, 190f);
            _scrollBattle = GUILayout.BeginScrollView(_scrollBattle, GUILayout.Height(arrangeListH));
            for (int i = 0; i < Game.BattlePlan.Count; i++)
            {
                var e = Game.BattlePlan[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label("Battle " + (i + 1), GUILayout.Width(100));
                if (canReorder)
                {
                    if (GUILayout.Button("^", GUILayout.Width(28)))
                        Game.MoveBattlePlanEntry(i, -1);
                    if (GUILayout.Button("v", GUILayout.Width(28)))
                        Game.MoveBattlePlanEntry(i, 1);
                }
                else
                    GUILayout.Space(56);

                var opps = BattleResolver.OpponentsOnHex(e.Hex, Game.CurrentPlayer);
                GUILayout.Label("vs", GUILayout.Width(24));
                if (opps.Count <= 1)
                {
                    if (opps.Count == 1)
                        GUILayout.Label("P" + (opps[0].PlayerIndex + 1), GUILayout.Width(48));
                }
                else
                {
                    foreach (var o in opps)
                    {
                        if (GUILayout.Button("P" + (o.PlayerIndex + 1), GUILayout.Width(48)) &&
                            e.DefenderPlayerIndex != o.PlayerIndex)
                            Game.SetBattleDefenderForEntry(i, o.PlayerIndex);
                    }
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            GUILayout.Space(8);
            if (GUILayout.Button("Confirm - start battles", GUILayout.Height(36)))
                Game.ConfirmBattleArrangement();
        }

        void EnergizeWindow()
        {
            var p = Game.EnergizePromptPlayer;
            GUILayout.Label("Battle Energize step");
            GUILayout.Label("P" + (p.PlayerIndex + 1) + ": Battle Energize or pass.");
            if (GUILayout.Button("What do my cards do?", GUILayout.Height(26)))
            {
                _energizeHelpSubject = p;
                _showMyEnergizeHelp = true;
            }
            _scrollHand = GUILayout.BeginScrollView(_scrollHand, GUILayout.MinHeight(160));
            var distinct = p.BattleEnergize.GroupBy(x => x).OrderBy(g => g.Key.ToString());
            foreach (var g in distinct)
            {
                int count = g.Count();
                string label = EnergizeBattleCatalog.GetName(g.Key) + " x" + count;
                if (GUILayout.Button(label))
                    Game.SubmitEnergizePlay(g.Key);
            }

            GUILayout.EndScrollView();
            GUILayout.Space(6);
            if (GUILayout.Button("Pass", GUILayout.Height(32)))
                Game.SubmitEnergizePass();
        }

        void FocusFireWindow()
        {
            var types = new HashSet<UnitType>();
            var hex = Game.FocusFireBattleHex;
            foreach (var u in FindObjectsOfType<UnitInstance>())
            {
                if (u.Owner == Game.FocusFirePicker && u.Tile == hex)
                    types.Add(u.Definition.Type);
            }

            GUILayout.Label("Unit type for +2 dice:");
            foreach (var t in BattleResolver.BattleOrder)
            {
                if (!types.Contains(t))
                    continue;
                if (GUILayout.Button(t.ToString(), GUILayout.Height(28)))
                    Game.SubmitFocusFireUnitType(t);
            }

            if (types.Count == 0 && GUILayout.Button("Cancel (refund Focus Fire)"))
                Game.CancelFocusFireRefund();
        }

        void CasualtyWindow()
        {
            var cp = Game.CasualtyPick;
            cp.Pool.RemoveAll(u => u == null);
            cp.Selected.RemoveAll(u => u == null || !cp.Pool.Contains(u));
            cp.Required = Mathf.Clamp(cp.Required, 0, cp.Pool.Count);
            if (cp.Required == 0)
            {
                Game.SubmitCasualtyPick();
                GUILayout.Label("No valid casualties remain. Auto-continuing...");
                return;
            }

            bool isAttacker = Game.ActiveBattleAttacker != null && cp.Owner == Game.ActiveBattleAttacker;
            string side = isAttacker ? "ATTACKER" : "DEFENDER";
            var prev = GUI.color;
            GUI.color = isAttacker ? new Color(0.25f, 0.45f, 0.95f, 0.95f) : new Color(0.9f, 0.3f, 0.25f, 0.95f);
            GUILayout.Box($"Now selecting casualties: P{cp.Owner.PlayerIndex + 1} ({side})");
            GUI.color = prev;

            GUILayout.Label($"P{cp.Owner.PlayerIndex + 1} pick {cp.Required} unit(s). Selected: {cp.Selected.Count}");
            foreach (var u in cp.Pool)
            {
                if (u == null)
                    continue;
                bool on = cp.Selected.Contains(u);
                if (GUILayout.Toggle(on, " " + u.Definition.Type + " (" + u.GetInstanceID() + ")") != on)
                    Game.ToggleCasualtyUnit(u);
            }

            GUI.enabled = cp.Selected.Count == cp.Required;
            if (GUILayout.Button("Confirm casualties", GUILayout.Height(32)))
                Game.SubmitCasualtyPick();
            GUI.enabled = true;
        }

        void SecretMissionWindow()
        {
            var offer = Game.SecretMissionOffer;
            var att = offer.Attacker;
            GUILayout.Label("Battle won! P" + (att.PlayerIndex + 1) + " - play ONE secret or skip:");
            foreach (int idx in offer.EligibleIndices)
            {
                if (idx < 0 || idx >= att.SecretMissions.Count)
                    continue;
                var s = att.SecretMissions[idx];
                if (GUILayout.Button(SecretMissionLabel(s) + " +" + s.VictoryPoints + " VP [i" + idx + "]"))
                    Game.PlaySecretMissionAtIndex(idx);
            }

            GUILayout.Space(8);
            if (GUILayout.Button("Skip"))
                Game.SkipSecretMissionPlay();
        }

        static string SecretMissionLabel(SecretMissionInHand s)
        {
            return s.MissionTypeId switch
            {
                SecretMissionTypes.WinAnyBattle => "Win any battle",
                SecretMissionTypes.WinBattleKillTwoPlus => "Win battle (2+ kills)",
                SecretMissionTypes.WinBattleEnemyLostDragon => "Win battle (Dragon kill)",
                _ => "Mission " + s.MissionTypeId
            };
        }

        void DrawDragonPhaseOverlay()
        {
            var dp = Game.DragonPhase;
            if (dp == null)
                return;

            if (Game.IsAiControlled(dp.Player))
            {
                GUI.Box(new Rect(20, Screen.height - 200, Screen.width - 40, 190),
                    "Rubium Dragon — AI is choosing…");
                if (!string.IsNullOrEmpty(dp.LastLog))
                    GUI.Label(new Rect(30, Screen.height - 175, Screen.width - 60, 22), dp.LastLog);
                return;
            }

            GUI.Box(new Rect(20, Screen.height - 200, Screen.width - 40, 190), "Rubium Dragon (end of movement)");

            if (!string.IsNullOrEmpty(dp.LastLog))
                GUI.Label(new Rect(30, Screen.height - 175, Screen.width - 60, 22), dp.LastLog);

            if (dp.PendingHit != null && dp.PendingEnemies != null)
            {
                GUI.Label(new Rect(30, Screen.height - 150, 400, 20),
                    "Hit! Roll " + dp.PendingHit.LastRoll + ". Remove one enemy:");
                float x = 30;
                foreach (var v in dp.PendingEnemies)
                {
                    if (GUI.Button(new Rect(x, Screen.height - 125, 140, 26),
                            v.Definition.Type + " P" + (v.Owner.PlayerIndex + 1)))
                        Game.DragonStrikeChooseVictim(v);
                    x += 148;
                }

                return;
            }

            float y = Screen.height - 150;
            int optionNum = 1;
            foreach (var opt in dp.Options.ToList())
            {
                string label = "Dragon strike option #" + optionNum;
                if (GUI.Button(new Rect(30, y, Screen.width - 200, 24), label))
                    Game.ExecuteDragonStrike(opt);
                if (GUI.Button(new Rect(Screen.width - 160, y, 120, 24), "Skip"))
                    Game.SkipDragonStrikeOption(opt);

                DrawDragonTargetMarker(opt, optionNum);
                y += 28;
                optionNum++;
            }

            if (GUI.Button(new Rect(30, y, 220, 26), "Skip all dragon strikes"))
                Game.SkipAllDragonStrikes();
        }

        void DrawDragonTargetMarker(DragonStrikeOption opt, int optionNum)
        {
            if (opt == null || opt.TargetHex == null || opt.TargetHex.View == null)
                return;
            var cam = Camera.main;
            if (cam == null)
                return;

            var sp = cam.WorldToScreenPoint(opt.TargetHex.View.transform.position);
            if (sp.z <= 0f)
                return;

            float x = sp.x - 12f;
            float y = Screen.height - sp.y - 12f;
            var r = new Rect(x, y, 24f, 24f);

            Color prev = GUI.color;
            GUI.color = new Color(1f, 0.9f, 0.1f, 0.9f);
            GUI.Box(r, optionNum.ToString());
            GUI.color = prev;
        }

        void DrawBuyButtonGui(string label, UnitType type, int baseCost)
        {
            var player = Game.CurrentPlayer;
            int maxOff = Mathf.Max(0, baseCost - 1);
            int use = Mathf.Min(maxOff, player.DeploymentPurchaseDiscountRubium);
            int pay = baseCost - use;
            string line = use > 0 ? label + " pay " + pay : label;
            bool canAfford = player.Rubium >= pay;
            if (!canAfford)
                GUI.enabled = false;
            // Text-only: GUIContent(image) makes Unity draw a huge icon on each unit row (looked like Rubium troops).
            if (GUILayout.Button(line) && canAfford)
            {
                BoardTile homeTile = null;
                if (InputController != null && InputController.SelectedTile != null)
                {
                    var sel = InputController.SelectedTile;
                    if (Game.CanDeployToStartingHomeTile(player, sel))
                        homeTile = sel;
                }

                if (homeTile == null)
                    homeTile = FindHomeBaseTileForPlayer(player);
                if (homeTile != null)
                {
                    player.DeploymentPurchaseDiscountRubium -= use;
                    player.Rubium -= pay;
                    Game.SpawnUnit(player, type, homeTile);
                }
            }

            GUI.enabled = true;
        }

        void DrawSelectedTilePanelBody(PlayerState player, BoardTile popupTile, float contentWidth)
        {
            if (popupTile == null)
                return;

            float moveLabelW = Mathf.Max(72f, contentWidth - 52f);
            float unitLineW = Mathf.Max(80f, contentWidth - 12f);

            bool hasAnyUnit = false;
            bool hasOtherOwner = false;
            int? soleOwnerIndex = null;
            foreach (var unit in FindObjectsOfType<UnitInstance>())
            {
                if (unit.Tile != popupTile)
                    continue;
                hasAnyUnit = true;
                if (soleOwnerIndex == null)
                    soleOwnerIndex = unit.Owner.PlayerIndex;
                else if (soleOwnerIndex != unit.Owner.PlayerIndex)
                    hasOtherOwner = true;
            }

            if (hasAnyUnit && hasOtherOwner)
            {
                var prev = GUI.color;
                GUI.color = Color.red;
                GUILayout.Label("CONTESTED");
                GUI.color = prev;
            }
            else
                GUILayout.Label("Owner: " + (popupTile.Owner != null ? (popupTile.Owner.PlayerIndex + 1).ToString() : "None"));

            GUILayout.BeginHorizontal();
            int my = popupTile.ExtraMineYield;
            var chipGui = GetOreChipGui(my);
            if (chipGui.IsEmpty && my > 0)
                chipGui = GetRubiumGui();
            if (!chipGui.IsEmpty)
            {
                var ir = GUILayoutUtility.GetRect(26f, 26f, GUILayout.Width(30f), GUILayout.Height(26f));
                chipGui.Draw(ir);
            }

            GUILayout.Label(my > 0 ? my.ToString() : "—", GUILayout.Width(48));
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
            GUILayout.Label("Move selection:");
            var counts = new Dictionary<UnitType, int>();
            foreach (var unit in FindObjectsOfType<UnitInstance>())
            {
                if (unit.Tile == popupTile && unit.Owner == player && !unit.HasMovedThisTurn)
                {
                    if (!counts.ContainsKey(unit.Definition.Type))
                        counts[unit.Definition.Type] = 0;
                    counts[unit.Definition.Type]++;
                }
            }

            if (InputController != null)
            {
                var selectedCounts = InputController.SelectedMoveCounts;
                bool isMovementPhase = !Game.IsGameOver &&
                                       !Game.BattlePhaseBlockingPlay &&
                                       Game.DragonPhase == null &&
                                       !Game.IsAiControlled(player);

                if (_moveAllTile != popupTile)
                {
                    _moveAllTile = popupTile;
                    _moveAllChecked = false;
                }

                if (isMovementPhase && counts.Count > 0)
                {
                    bool nextMoveAll = GUILayout.Toggle(_moveAllChecked, "Move all");
                    if (nextMoveAll != _moveAllChecked)
                    {
                        _moveAllChecked = nextMoveAll;
                        foreach (var kvp in counts)
                            InputController.SetMoveSelection(kvp.Key, _moveAllChecked ? kvp.Value : 0);
                    }
                }

                foreach (var kvp in counts)
                {
                    selectedCounts.TryGetValue(kvp.Key, out int chosen);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(kvp.Key + ": " + kvp.Value + " sel:" + chosen, GUILayout.Width(moveLabelW));
                    if (GUILayout.Button("-", GUILayout.Width(20)))
                    {
                        InputController.AdjustMoveSelection(kvp.Key, -1);
                        _moveAllChecked = false;
                    }
                    if (GUILayout.Button("+", GUILayout.Width(20)))
                    {
                        InputController.AdjustMoveSelection(kvp.Key, +1);
                        _moveAllChecked = false;
                    }
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Label("Units on tile:");
            var stacks = new Dictionary<string, int>();
            foreach (var unit in FindObjectsOfType<UnitInstance>())
            {
                if (unit.Tile != popupTile)
                    continue;
                string key = "P" + (unit.Owner.PlayerIndex + 1) + "|" + unit.Definition.Type;
                if (!stacks.ContainsKey(key))
                    stacks[key] = 0;
                stacks[key]++;
            }

            foreach (var kvp in stacks.OrderBy(k => k.Key))
            {
                var split = kvp.Key.Split('|');
                string ownerText = split[0];
                string unitText = split.Length > 1 ? split[1] : "Unit";
                GUILayout.BeginHorizontal();
                var iconRect = GUILayoutUtility.GetRect(14f, 14f, GUILayout.Width(16f), GUILayout.Height(14f));
                if (System.Enum.TryParse<UnitType>(unitText, out var parsedType))
                    DrawUnitMiniIcon(iconRect, parsedType);
                else
                    DrawUnitMiniIcon(iconRect, UnitType.Human);
                GUILayout.Label(unitText + " x" + kvp.Value + " (" + ownerText + ")", GUILayout.Width(unitLineW));
                GUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Close") && InputController != null)
                InputController.ClearSelection();
        }

        void DrawUnitMiniIcon(Rect r, UnitType type)
        {
            var icon = GetUnitIcon(type);
            if (!icon.IsEmpty)
            {
                icon.Draw(r);
                GUI.Box(r, GUIContent.none);
                return;
            }

            DrawTintedRect(r, new Color(0.85f, 0.85f, 0.9f));
            GUI.Box(r, UnitUiName(type).Substring(0, 1));
        }

        NexusGuiImage GetUnitIcon(UnitType type)
        {
            if (_unitIconCache.TryGetValue(type, out var cached))
                return cached;

            string key = type.ToString();
            var loaded = NexusGuiArt.Load(
                "Sprites/" + key,
                "Sprites/Units/" + key,
                "Sprites/" + UnitUiName(type).Replace(" ", ""),
                "Sprites/" + UnitUiName(type).Replace(" ", "_"));
            _unitIconCache[type] = loaded;
            return loaded;
        }

        static string UnitUiName(UnitType type)
        {
            return type switch
            {
                UnitType.RockStrider => "Rock Strider",
                UnitType.LavaLeaper => "Lava Leaper",
                UnitType.RubiumDragon => "Rubium Dragon",
                _ => type.ToString()
            };
        }

        static string BattleStepLabel(UnitType type)
        {
            return type switch
            {
                UnitType.RubiumDragon => "Dragons fighting",
                UnitType.LavaLeaper => "Lava Leapers fighting",
                UnitType.RockStrider => "Rock Striders fighting",
                UnitType.Crystalline => "Crystalline fighting",
                UnitType.Fungoid => "Fungoids fighting",
                UnitType.Human => "Humans fighting",
                _ => UnitUiName(type) + " fighting"
            };
        }

        static string UiSafeText(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            // Hide axial coordinates in UI output.
            return Regex.Replace(value, @"\(\s*-?\d+\s*,\s*-?\d+\s*\)", "(hex)");
        }

        void DrawBattleFocusOverlay()
        {
            if (Game == null || Game.ActiveBattleHex == null)
                return;
            var cam = Camera.main;
            if (cam == null)
                return;

            var world = Game.ActiveBattleHex.View != null ? Game.ActiveBattleHex.View.transform.position : Vector3.zero;
            var sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0f)
                return;

            float sx = sp.x - 76f;
            float sy = Screen.height - sp.y - 22f;
            // Bright focus plate centered on the active battle hex.
            var focusPlate = new Rect(sx - 28f, sy - 18f, 208f, 62f);
            Color prev = GUI.color;
            GUI.color = new Color(1f, 0.15f, 0.1f, 0.26f);
            GUI.DrawTexture(focusPlate, Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = prev;
            var r = new Rect(sx, sy, 152f, 30f);
            prev = GUI.color;
            GUI.color = new Color(1f, 0.25f, 0.15f, 0.9f);
            GUI.Box(r, "BATTLE");
            GUI.color = prev;
        }

        void DrawEndGameOverlay(GameEndSnapshot snap)
        {
            if (_dimTex == null)
            {
                _dimTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _dimTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.7f));
                _dimTex.Apply();
            }

            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _dimTex, ScaleMode.StretchToFill);

            float w = Mathf.Min(640f, Screen.width - 40f);
            float h = Mathf.Min(520f, Screen.height - 40f);
            var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.Box(panel, "Victory");

            float x = panel.x + 14f;
            float y = panel.y + 30f;
            float lw = panel.width - 28f;
            GUI.Label(new Rect(x, y, lw, 24f), "Winner: P" + (snap.WinnerPlayerIndex + 1));
            y += 24f;
            GUI.Label(new Rect(x, y, lw, 44f), UiSafeText(snap.WinReason ?? ""));
            y += 48f;

            if (GUI.Button(new Rect(x, y, 140f, 32f), "Play again"))
            {
                _showEndGameStats = false;
                Game.ResetAndStartNewMatch();
            }

            if (GUI.Button(new Rect(x + 150f, y, 170f, 32f), "Back to main menu"))
            {
                _showEndGameStats = false;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }

            string statsBtn = _showEndGameStats ? "Hide stats" : "View stats";
            if (GUI.Button(new Rect(x + 330f, y, 130f, 32f), statsBtn))
                _showEndGameStats = !_showEndGameStats;

            y += 40f;
            if (_showEndGameStats)
            {
                GUI.Box(new Rect(x, y, lw, panel.yMax - y - 12f), "");
                float sy = y + 8f;
                GUI.Label(new Rect(x + 8f, sy, lw - 16f, 22f), "Final stats");
                sy += 24f;
                for (int i = 0; i < snap.PlayerIndex.Length; i++)
                {
                    string line =
                        $"P{snap.PlayerIndex[i] + 1}  VP {snap.VictoryPoints[i]}  Rubium {snap.Rubium[i]}  Units {snap.UnitCounts[i]}";
                    GUI.Label(new Rect(x + 8f, sy, lw - 16f, 20f), line);
                    sy += 20f;
                }
            }
        }

        BoardTile FindHomeBaseTileForPlayer(PlayerState player)
        {
            foreach (var tile in Game.Board.AllTiles)
            {
                if (Game.CanDeployToStartingHomeTile(player, tile))
                    return tile;
            }

            return null;
        }
    }
}




