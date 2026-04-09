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

        bool _showCenterBuyModal;
        Vector2 _scrollCenterBuyDeploy;
        Texture2D _hexModalSilhouetteMask;

        public Rect GetCenterBuyModalPanelGuiRect()
        {
            float panelW = Mathf.Min(640f, Screen.width - 24f);
            float panelH = Mathf.Min(580f, Screen.height - 40f);
            float px = (Screen.width - panelW) * 0.5f;
            float py = (Screen.height - panelH) * 0.5f;
            return new Rect(px, py, panelW, panelH);
        }

        /// <summary>Centered deploy (buy) modal is open.</summary>
        public bool IsCenterBuyModalOpen => _showCenterBuyModal;

        public void OpenCenterBuyModal() => _showCenterBuyModal = true;

        public void HandleCenterBuyModalTap(Vector2 screenPosition)
        {
            var gui = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            if (!GetCenterBuyModalPanelGuiRect().Contains(gui))
                _showCenterBuyModal = false;
        }

        /// <summary>True when the pointer is over the deploy modal panel (suppresses drag-prep on that finger).</summary>
        public bool ScreenPointOverlapsBuyMenu(Vector2 screenPosition)
        {
            if (!_showCenterBuyModal)
                return false;
            var gui = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            return GetCenterBuyModalPanelGuiRect().Contains(gui);
        }
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

        const float CardBarHeight = 136f;
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
        readonly Dictionary<int, NexusGuiImage> _dragonIconByPlayerIndex = new Dictionary<int, NexusGuiImage>();
        readonly Dictionary<int, NexusGuiImage> _striderIconByPlayerIndex = new Dictionary<int, NexusGuiImage>();
        readonly Dictionary<int, NexusGuiImage> _fungoidIconByPlayerIndex = new Dictionary<int, NexusGuiImage>();
        readonly Dictionary<int, NexusGuiImage> _humanIconByPlayerIndex = new Dictionary<int, NexusGuiImage>();
        readonly Dictionary<int, NexusGuiImage> _lavaLeaperIconByPlayerIndex = new Dictionary<int, NexusGuiImage>();
        readonly Dictionary<int, NexusGuiImage> _crystallineIconByPlayerIndex = new Dictionary<int, NexusGuiImage>();
        GUIStyle _battleWindowStyle;
        Texture2D _battleWindowBg;
        GUIStyle _topIconButtonStyle;
        GUIStyle _flyRubiumAmountStyle;
        GUIStyle _flyVpAmountStyle;
        GUIStyle _flyVpFallbackStyle;

        struct FlyingRubiumChip
        {
            public Vector2 StartGui;
            public Vector2 EndGui;
            public float StartTime;
            public float Duration;
            public int Amount;
        }

        readonly List<FlyingRubiumChip> _flyingRubium = new List<FlyingRubiumChip>();

        struct FlyingVpChip
        {
            public Vector2 CenterGui;
            public Vector2 EndGui;
            public float StartTime;
            public float PopDuration;
            public float FlyDuration;
            public int Amount;
        }

        readonly List<FlyingVpChip> _flyingVp = new List<FlyingVpChip>();

        void Start()
        {
            if (Game == null)
                Game = FindObjectOfType<GameController>();
            if (InputController == null)
                InputController = FindObjectOfType<MobileInputController>();
        }

        void Update()
        {
            if (Game == null)
                return;

            if (Game.TryConsumeMiningIncomeFlights(out var list))
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    float stagger = 0f;
                    const float staggerStep = 0.12f;
                    foreach (var info in list)
                    {
                        var sp = cam.WorldToScreenPoint(info.WorldStart);
                        if (sp.z <= 0f)
                        {
                            stagger += staggerStep;
                            continue;
                        }

                        var startGui = new Vector2(sp.x, Screen.height - sp.y);
                        var endGui = GetRubiumBankIconCenterGui();
                        _flyingRubium.Add(new FlyingRubiumChip
                        {
                            StartGui = startGui,
                            EndGui = endGui,
                            StartTime = Time.time + stagger,
                            Duration = 0.72f,
                            Amount = info.Amount
                        });
                        stagger += staggerStep;
                    }
                }
            }

            for (int i = _flyingRubium.Count - 1; i >= 0; i--)
            {
                if (Time.time > _flyingRubium[i].StartTime + _flyingRubium[i].Duration)
                    _flyingRubium.RemoveAt(i);
            }

            if (Game.TryConsumeVictoryPointFlights(out var vpList))
            {
                float vpStagger = 0f;
                const float vpStaggerStep = 0.14f;
                var centerGui = new Vector2(Screen.width * 0.5f, Screen.height * 0.42f);
                var endGui = GetVpBankIconCenterGui();
                foreach (var info in vpList)
                {
                    _flyingVp.Add(new FlyingVpChip
                    {
                        CenterGui = centerGui,
                        EndGui = endGui,
                        StartTime = Time.time + vpStagger,
                        PopDuration = 0.22f,
                        FlyDuration = 0.7f,
                        Amount = info.Amount
                    });
                    vpStagger += vpStaggerStep;
                }
            }

            for (int i = _flyingVp.Count - 1; i >= 0; i--)
            {
                var fv = _flyingVp[i];
                if (Time.time > fv.StartTime + fv.PopDuration + fv.FlyDuration)
                    _flyingVp.RemoveAt(i);
            }
        }

        Vector2 GetRubiumBankIconCenterGui()
        {
            const float topBarY = 6f;
            const float topBarH = 52f;
            float ly = topBarY + (topBarH - MainHudIconHeight) * 0.5f - 2f;
            var rub = GetRubiumGui();
            float w = rub.IsEmpty ? MainHudIconHeight : MainHudIconHeight * rub.AspectRatio;
            float cx = 12f + w * 0.5f;
            float cy = ly + MainHudIconHeight * 0.5f;
            return new Vector2(cx, cy);
        }

        Vector2 GetVpBankIconCenterGui()
        {
            if (Game == null || Game.Players.Count == 0)
                return new Vector2(Screen.width * 0.5f, 24f);

            const float topBarY = 6f;
            const float topBarH = 52f;
            float ly = topBarY + (topBarH - MainHudIconHeight) * 0.5f - 2f;
            float cy = ly + MainHudIconHeight * 0.5f;
            var player = Game.CurrentPlayer;
            var rub = GetRubiumGui();
            var vp = GetVPGui();
            float rxRes = 12f;
            if (!rub.IsEmpty)
                rxRes += MainHudIconHeight * rub.AspectRatio + 6f;
            int hudFontSize = Mathf.Clamp(Screen.width / 32, 15, 20);
            float tw = EstimateHudNumberWidth(player.Rubium, hudFontSize);
            rxRes += Mathf.Max(28f, tw) + 12f;
            float vpW = vp.IsEmpty ? MainHudIconHeight : MainHudIconHeight * vp.AspectRatio;
            float cx = rxRes + vpW * 0.5f;
            return new Vector2(cx, cy);
        }

        static float EstimateHudNumberWidth(int value, int fontSize)
        {
            string s = Mathf.Max(0, value).ToString();
            // Approximate monospace-like digit width to avoid GUI API in Update().
            return s.Length * (fontSize * 0.58f) + 4f;
        }

        void DrawFlyingRubiumIncome()
        {
            var rub = GetRubiumGui();
            if (rub.IsEmpty || _flyingRubium.Count == 0)
                return;

            if (_flyRubiumAmountStyle == null)
            {
                _flyRubiumAmountStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = new Color(1f, 0.95f, 0.4f, 0.95f) }
                };
            }

            float now = Time.time;
            Color prev = GUI.color;
            foreach (var f in _flyingRubium)
            {
                float u = Mathf.InverseLerp(f.StartTime, f.StartTime + f.Duration, now);
                if (u < 0f || u > 1f)
                    continue;
                float t = u * u * (3f - 2f * u);
                var p = Vector2.Lerp(f.StartGui, f.EndGui, t);
                float h = Mathf.Lerp(MainHudIconHeight * 0.82f, MainHudIconHeight, t);
                float half = h * 0.5f;
                var r = new Rect(p.x - half, p.y - half, h * rub.AspectRatio, h);
                float a = u < 0.08f ? u / 0.08f : 1f;
                GUI.color = new Color(1f, 1f, 1f, a);
                rub.Draw(r);
                if (f.Amount > 1)
                    GUI.Label(new Rect(r.xMax + 2f, r.y, 36f, h), "+" + f.Amount, _flyRubiumAmountStyle);
            }

            GUI.color = prev;
        }

        void DrawFlyingVictoryPoints()
        {
            if (_flyingVp.Count == 0)
                return;

            if (_flyVpAmountStyle == null)
            {
                _flyVpAmountStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 26,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = new Color(1f, 0.92f, 0.35f, 1f) }
                };
                _flyVpFallbackStyle = new GUIStyle(_flyVpAmountStyle)
                {
                    fontSize = 34,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            var vpGui = GetVPGui();
            float now = Time.time;
            Color prev = GUI.color;

            foreach (var f in _flyingVp)
            {
                float elapsed = now - f.StartTime;
                if (elapsed < 0f)
                    continue;
                float total = f.PopDuration + f.FlyDuration;
                if (elapsed > total)
                    continue;

                Vector2 p;
                float iconH;
                float alpha;

                if (elapsed < f.PopDuration)
                {
                    p = f.CenterGui;
                    float pu = elapsed / f.PopDuration;
                    iconH = MainHudIconHeight * Mathf.SmoothStep(0.4f, 1.08f, pu);
                    alpha = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, pu * 1.4f));
                }
                else
                {
                    float fu = (elapsed - f.PopDuration) / f.FlyDuration;
                    float t = fu * fu * (3f - 2f * fu);
                    p = Vector2.Lerp(f.CenterGui, f.EndGui, t);
                    iconH = Mathf.Lerp(MainHudIconHeight * 1.08f, MainHudIconHeight, Mathf.SmoothStep(0f, 1f, fu));
                    alpha = 1f;
                }

                string bonus = "+" + f.Amount;
                float vpW = vpGui.IsEmpty ? iconH : iconH * vpGui.AspectRatio;
                float textW = _flyVpAmountStyle.CalcSize(new GUIContent(bonus)).x;
                float groupW = vpW + 4f + textW;
                float leftX = p.x - groupW * 0.5f;

                GUI.color = new Color(1f, 1f, 1f, alpha);
                if (!vpGui.IsEmpty)
                {
                    var iconRect = new Rect(leftX, p.y - iconH * 0.5f, vpW, iconH);
                    vpGui.Draw(iconRect);
                    GUI.Label(new Rect(iconRect.xMax + 4f, p.y - iconH * 0.5f, textW + 4f, iconH), bonus,
                        _flyVpAmountStyle);
                }
                else
                    GUI.Label(new Rect(p.x - 80f, p.y - 28f, 160f, 56f), "VP " + bonus, _flyVpFallbackStyle);
            }

            GUI.color = prev;
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
            // Deep navy translucent shell for a cleaner, modern battle modal.
            _battleWindowBg.SetPixel(0, 0, new Color(0.04f, 0.06f, 0.11f, 0.93f));
            _battleWindowBg.Apply();
            _battleWindowStyle.normal.background = _battleWindowBg;
            _battleWindowStyle.onNormal.background = _battleWindowBg;
            _battleWindowStyle.focused.background = _battleWindowBg;
            _battleWindowStyle.onFocused.background = _battleWindowBg;
            _battleWindowStyle.active.background = _battleWindowBg;
            _battleWindowStyle.onActive.background = _battleWindowBg;
            _battleWindowStyle.fontSize = 15;
            _battleWindowStyle.fontStyle = FontStyle.Bold;
            _battleWindowStyle.alignment = TextAnchor.UpperLeft;
            _battleWindowStyle.padding = new RectOffset(14, 14, 24, 10);
            _battleWindowStyle.normal.textColor = new Color(0.95f, 0.97f, 1f, 0.95f);
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

            if (!string.IsNullOrEmpty(Game.LastDrawPhaseLog) && !_showCenterBuyModal)
            {
                float logBarH = 40f;
                GUI.Box(new Rect(10f, ly, Mathf.Min(600f, Screen.width - 20f), logBarH), "");
                GUI.Label(new Rect(16f, ly + 10f, Mathf.Min(580f, Screen.width - 40f), logBarH - 8f),
                    Game.LastDrawPhaseLog,
                    new GUIStyle(hudLabel) { fontSize = 10, wordWrap = true });
                ly += logBarH + 6f;
            }

            string battleLog =
                !string.IsNullOrEmpty(Game.LiveBattlePhaseLog) ? Game.LiveBattlePhaseLog : Game.LastBattlePhaseLog;

            float hudBottom = ly + 4f;
            const float battleLogPanelH = 140f;
            if (!string.IsNullOrEmpty(battleLog) && !Game.PendingBattleArrangement && !_showCenterBuyModal)
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
                _showCenterBuyModal = false;
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

            bool canOpenHexDetailModal = InputController != null && InputController.SelectedTile != null;
            if (Game.BattlePhaseBlockingPlay || Game.DragonPhase != null || Game.IsAiControlled(player))
                canOpenHexDetailModal = false;

            if (_showCenterBuyModal && !canOpenHexDetailModal)
                _showCenterBuyModal = false;

            if (!canOpenHexDetailModal)
                GUI.enabled = false;
            if (GUI.Button(new Rect(150, topY, 40, 28), "$"))
                _showCenterBuyModal = !_showCenterBuyModal;

            GUI.enabled = true;

            DrawBottomCardHand(player);
            DrawPhaseRibbon(player);

            DrawHandPileViewerOverlay(player);
            DrawBattleFocusOverlay();
            DrawEnergizeHelpWindow();
            DrawCenterBuyDeployModal(player);

            if (_showSettingsMenu)
                DrawSettingsOverlay();

            if (_showQuickRef)
                DrawQuickReferenceOverlay();

            DrawFlyingRubiumIncome();
            DrawFlyingVictoryPoints();

            if (Game.BattleClashIntroActive)
                DrawBattleClashIntroOverlay();
        }

        void DrawBattleClashIntroOverlay()
        {
            var dim = new Color(0f, 0f, 0f, 0.65f);
            Color prev = GUI.color;
            GUI.color = dim;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = prev;

            var pulse = 0.85f + 0.15f * Mathf.Sin(Time.realtimeSinceStartup * 8f);
            var title = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Clamp(Screen.width / 14, 28, 56),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.92f, 0.35f, pulse) }
            };
            GUI.Label(new Rect(0f, Screen.height * 0.38f, Screen.width, 120f), "⚔  ⚔  ⚔", title);
            var sub = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.9f, 0.9f, 0.95f, 0.85f) }
            };
            GUI.Label(new Rect(0f, Screen.height * 0.38f + 72f, Screen.width, 28f),
                "(Sword clash animation — art TBD)", sub);
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
                $"P{player.PlayerIndex + 1}  ·  Secret deck {Game.SecretDeckCount}  ·  Energize {Game.EnergizeDeckCount}";
            var deckStyle = new GUIStyle(_cardColumnLabelStyle) { fontSize = 10 };
            GUI.Label(new Rect(barX + pad, barY + 2, barW - pad * 2f, 16f), deckLine, deckStyle);

            float innerX = barX + pad;
            float innerW = barW - pad * 2f;
            float contentY = barY + headerH;
            float contentH = CardBarHeight - headerH - 4f;

            const float splitGap = 8f;
            const float minTilePanelW = 120f;
            const float cardsLabelW = 44f;
            const float stackBtnW = 46f;
            const float stackBtnH = 26f;
            const float stackGap = 3f;
            const float cardsHdrH = 12f;
            const float cardsColGap = 6f;

            int bCount = player.BattleEnergize?.Count ?? 0;
            int dCount = player.DeployEnergize?.Count ?? 0;
            int sCount = player.SecretMissions?.Count ?? 0;

            float stackColW = cardsLabelW + cardsColGap + stackBtnW;
            float maxLeft = Mathf.Max(80f, innerW - minTilePanelW - splitGap);
            float leftW = Mathf.Min(stackColW, maxLeft);

            float rightW = innerW - leftW - splitGap;
            float rightX = innerX + leftW + splitGap;

            var pileBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                alignment = TextAnchor.MiddleCenter
            };

            var cardsHeadingStyle = new GUIStyle(_cardColumnLabelStyle)
            {
                fontSize = 9,
                alignment = TextAnchor.UpperLeft,
                wordWrap = false
            };

            float stackBlockH = cardsHdrH + 4f + stackBtnH * 3f + stackGap * 2f;
            float stackTop = contentY + Mathf.Max(0f, (contentH - stackBlockH) * 0.5f);
            GUI.Label(new Rect(innerX, stackTop, cardsLabelW, cardsHdrH + 2f), "CARDS", cardsHeadingStyle);

            float bx = innerX + cardsLabelW + cardsColGap;
            float by = stackTop + cardsHdrH + 4f;
            var rBattle = new Rect(bx, by, stackBtnW, stackBtnH);
            var rDeploy = new Rect(bx, by + stackBtnH + stackGap, stackBtnW, stackBtnH);
            var rSecret = new Rect(bx, by + (stackBtnH + stackGap) * 2f, stackBtnW, stackBtnH);

            if (GUI.Button(rBattle, $"⚔ {bCount}", pileBtnStyle))
            {
                _handPileViewer = _handPileViewer == HandPileViewerKind.Battle
                    ? HandPileViewerKind.None
                    : HandPileViewerKind.Battle;
            }

            if (GUI.Button(rDeploy, $"▲ {dCount}", pileBtnStyle))
            {
                _handPileViewer = _handPileViewer == HandPileViewerKind.Deploy
                    ? HandPileViewerKind.None
                    : HandPileViewerKind.Deploy;
            }

            if (GUI.Button(rSecret, $"★ {sCount}", pileBtnStyle))
            {
                _handPileViewer = _handPileViewer == HandPileViewerKind.Secret
                    ? HandPileViewerKind.None
                    : HandPileViewerKind.Secret;
            }

            if (_handPileViewer != HandPileViewerKind.None)
            {
                if (_handPileViewer == HandPileViewerKind.Battle)
                    DrawOutlineRect(rBattle, new Color(0.95f, 0.78f, 0.2f, 0.95f), 2f);
                if (_handPileViewer == HandPileViewerKind.Deploy)
                    DrawOutlineRect(rDeploy, new Color(0.95f, 0.78f, 0.2f, 0.95f), 2f);
                if (_handPileViewer == HandPileViewerKind.Secret)
                    DrawOutlineRect(rSecret, new Color(0.95f, 0.78f, 0.2f, 0.95f), 2f);
            }

            DrawBottomTilePanel(rightX, contentY, rightW, contentH, player);
        }

        void DrawBottomTilePanel(float x, float y, float w, float h, PlayerState player)
        {
            var panel = new Rect(x, y, w, h);
            GUI.Box(panel, "");

            var popupTile = InputController != null ? InputController.SelectedTile : null;

            const float tileScrollContentH = 260f;
            var scrollView = new Rect(panel.x + 4f, panel.y + 4f, panel.width - 8f, panel.height - 8f);
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

        void DrawCenterBuyDeployModal(PlayerState player)
        {
            if (!_showCenterBuyModal || player == null || InputController == null)
                return;

            var sel = InputController.SelectedTile;
            if (sel == null)
            {
                _showCenterBuyModal = false;
                return;
            }

            bool showShop = Game.CanDeployToStartingHomeTile(player, sel) && !Game.AnyMovementOccurredThisTurn;

            var dim = new Color(0f, 0f, 0f, 0.88f);
            Color prev = GUI.color;
            GUI.color = dim;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture,
                ScaleMode.StretchToFill);
            GUI.color = prev;

            var panel = GetCenterBuyModalPanelGuiRect();
            GUI.Box(panel, "");
            DrawOutlineRect(panel, new Color(0.95f, 0.82f, 0.2f, 0.95f), 2f);

            var hdr = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            GUI.Label(new Rect(panel.x + 12f, panel.y + 10f, 200f, 22f), "Tile info", hdr);
            if (GUI.Button(new Rect(panel.xMax - 78f, panel.y + 8f, 68f, 28f), "Close"))
                _showCenterBuyModal = false;

            var inner = new Rect(panel.x + 8f, panel.y + 42f, panel.width - 16f, panel.height - 50f);
            float cw = inner.width - 18f;
            int deployGrp = player.DeployEnergize == null ? 0 : player.DeployEnergize.GroupBy(x => x).Count();
            const float nameBoxH = 138f;
            const float shopIconSz = 86f;
            const float iconRowH = 30f;
            const float costGap = 10f;
            const float rowGap = 18f;
            const int shopColumns = 3;
            float rowStride = nameBoxH + costGap + iconRowH + rowGap;
            float buyH = rowStride * 2f;
            float energizeH = 36f + Mathf.Max(1, deployGrp) * 36f + 48f;

            const float hexRowH = 118f;
            const float occupyingLabelH = 20f;
            const float creatureRowH = 52f;
            const float creatureRowGap = 4f;
            float creatureBlock = occupyingLabelH + creatureRowH * 2f + creatureRowGap;
            float shopBlock = 0f;
            if (showShop)
                shopBlock = 20f + 24f + buyH + 22f + energizeH + 12f;
            float contentH = hexRowH + creatureBlock + shopBlock + 20f;

            _scrollCenterBuyDeploy = GUI.BeginScrollView(inner, _scrollCenterBuyDeploy, new Rect(0f, 0f, cw, contentH));
            GUILayout.BeginArea(new Rect(0f, 0f, cw, contentH));

            Rect hexRowRect = GUILayoutUtility.GetRect(cw, hexRowH);
            {
                Color prevRow = GUI.color;
                GUI.color = new Color(0.07f, 0.08f, 0.1f, 1f);
                GUI.DrawTexture(hexRowRect, Texture2D.whiteTexture);
                GUI.color = prevRow;
            }

            DrawHexModalTopRow(hexRowRect, player, sel);

            var occHdr = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            GUILayout.Label("Occupying forces:", occHdr);
            GUILayout.Space(4f);

            DrawHexModalCreatureGrid2Rows3Cols(sel, player, cw, creatureRowH, creatureRowGap);

            if (showShop)
            {
                GUILayout.Space(18f);
                var depHdr = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft
                };
                GUILayout.Label("DEPLOY UNITS", depHdr);
                GUILayout.Space(10f);
                Rect gridR = GUILayoutUtility.GetRect(cw, buyH);
                DrawBuyUnitGrid(gridR.x, gridR.y, gridR.width, shopColumns, nameBoxH, shopIconSz, iconRowH, costGap,
                    rowGap, 12, true);

                GUILayout.Space(12f);
                GUILayout.Label("Deployment Energize", new GUIStyle(GUI.skin.box)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft
                });
                if (Game.AnyMovementOccurredThisTurn)
                    GUILayout.Label("(Deployment locked after any movement this turn)");
                foreach (var g in player.DeployEnergize.GroupBy(x => x).OrderBy(x => x.Key.ToString()))
                {
                    var id = g.Key;
                    int n = g.Count();
                    string note = id == EnergizeDeploymentId.FreeHuman &&
                                  !Game.CanDeployToStartingHomeTile(player, sel)
                        ? " [select home hex]"
                        : "";
                    if (Game.AnyMovementOccurredThisTurn)
                        GUI.enabled = false;
                    if (GUILayout.Button(EnergizeDeploymentCatalog.GetName(id) + " x" + n + note))
                        Game.TryPlayDeploymentEnergize(id, sel);
                    GUI.enabled = true;
                }

                if (player.DeployEnergize == null || player.DeployEnergize.Count == 0)
                    GUILayout.Label("(No deployment cards)");
            }

            GUILayout.EndArea();
            GUI.EndScrollView();
        }

        void DrawHexModalTopRow(Rect row, PlayerState player, BoardTile tile)
        {
            if (tile == null)
                return;

            TileDefinition def = Game.Config != null ? Game.Config.GetTile(tile.Type) : null;
            Color fill = def != null ? def.Color : new Color(0.45f, 0.45f, 0.48f);
            Color stroke = new Color(fill.r * 0.55f, fill.g * 0.55f, fill.b * 0.55f, 1f);

            const float gapMid = 6f;
            float rightColW = Mathf.Clamp(row.width * 0.22f, 68f, 100f);
            float hexSide = Mathf.Min(row.height - 10f, 96f);
            float leftColW = hexSide + 10f;
            float centerW = row.width - leftColW - rightColW - gapMid * 2f;
            if (centerW < 64f)
            {
                hexSide = Mathf.Max(52f, hexSide - (64f - centerW));
                hexSide = Mathf.Min(hexSide, row.height - 10f);
                leftColW = hexSide + 10f;
                centerW = row.width - leftColW - rightColW - gapMid * 2f;
            }

            centerW = Mathf.Max(48f, centerW);
            var hexR = new Rect(row.x + 4f, row.y + (row.height - hexSide) * 0.5f, hexSide, hexSide);
            DrawModalHexPreview(hexR, fill, stroke);

            float centerX = row.x + leftColW + gapMid;
            var centerRect = new Rect(centerX, row.y + 4f, centerW, row.height - 8f);
            float rightX = centerX + centerW + gapMid;
            var rightRect = new Rect(rightX, row.y + 4f, rightColW, row.height - 8f);

            string tileName = TileTypeDisplayName(tile.Type);
            string meta = HexModalOwnerMetaLine(player, tile);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                clipping = TextClipping.Clip
            };
            var subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                normal = { textColor = new Color(0.78f, 0.8f, 0.88f) }
            };
            var rubSmall = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                alignment = TextAnchor.UpperRight,
                wordWrap = false,
                clipping = TextClipping.Clip
            };
            var numStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                clipping = TextClipping.Clip
            };

            int yield = tile.ExtraMineYield;
            string yieldText = yield > 0 ? yield.ToString() : "—";
            var rubGui = GetRubiumGui();

            const float padY = 2f;
            float yTop = centerRect.y + padY;
            float yBottom = Mathf.Min(centerRect.yMax, rightRect.yMax) - padY;

            float titleH = titleStyle.CalcHeight(new GUIContent(tileName), centerRect.width);
            titleH = Mathf.Max(titleH, 17f);
            float rubHeadH = rubSmall.CalcHeight(new GUIContent("Rubium / turn"), rightRect.width);
            rubHeadH = Mathf.Max(rubHeadH, 14f);
            float row1H = Mathf.Max(titleH, rubHeadH);

            GUI.Label(new Rect(centerRect.x, yTop, centerRect.width, row1H), tileName, titleStyle);
            GUI.Label(new Rect(rightRect.x, yTop, rightRect.width, row1H), "Rubium / turn", rubSmall);

            float y2 = yTop + row1H + 3f;
            float subH = subStyle.CalcHeight(new GUIContent(meta), centerRect.width);
            float iconH = 20f;
            float numW = Mathf.Min(numStyle.CalcSize(new GUIContent(yieldText)).x + 4f, rightRect.width * 0.5f);
            float numH = numStyle.CalcHeight(new GUIContent(yieldText), numW);
            numH = Mathf.Max(numH, 20f);
            float iconDrawW = rubGui.IsEmpty ? 0f : iconH * rubGui.AspectRatio;
            float gapIconNum = iconDrawW > 0f ? 6f : 0f;
            float bundleW = iconDrawW + gapIconNum + numW;
            bundleW = Mathf.Min(bundleW, rightRect.width);
            float iconRowH = Mathf.Max(iconH, numH);
            float rightBlockH = Mathf.Max(subH, iconRowH);
            rightBlockH = Mathf.Min(rightBlockH, Mathf.Max(0f, yBottom - y2));

            GUI.Label(new Rect(centerRect.x, y2, centerRect.width, rightBlockH), meta, subStyle);

            float startX = rightRect.x + rightRect.width - bundleW;
            startX = Mathf.Max(rightRect.x, startX);
            float iconNumY = y2 + (rightBlockH - iconRowH) * 0.5f;
            float midY = iconNumY + iconRowH * 0.5f;
            if (!rubGui.IsEmpty)
                rubGui.Draw(startX, midY - iconH * 0.5f, iconH);
            Color prevC = GUI.color;
            if (yield <= 0)
                GUI.color = new Color(0.55f, 0.55f, 0.58f);
            GUI.Label(new Rect(startX + iconDrawW + gapIconNum, midY - numH * 0.5f, numW, numH), yieldText,
                numStyle);
            GUI.color = prevC;
        }

        static string TileTypeDisplayName(TileType t)
        {
            return t switch
            {
                TileType.HomeBase => "Home base",
                TileType.CrystalField => "Crystal field",
                TileType.Rock => "Rock",
                TileType.Plains => "Plains",
                TileType.Forest => "Forest",
                TileType.Lava => "Lava",
                TileType.Monolith => "Monolith",
                _ => t.ToString()
            };
        }

        void DrawModalHexPreview(Rect r, Color fill, Color stroke)
        {
            var mask = HexModalSilhouetteMask();
            if (mask == null)
            {
                DrawTintedRect(r, fill);
                return;
            }

            const float strokePx = 3f;
            Color p = GUI.color;
            GUI.color = stroke;
            GUI.DrawTexture(
                new Rect(r.x - strokePx, r.y - strokePx, r.width + strokePx * 2f, r.height + strokePx * 2f),
                mask, ScaleMode.StretchToFill);
            GUI.color = fill;
            GUI.DrawTexture(r, mask, ScaleMode.StretchToFill);
            GUI.color = p;
        }

        Texture2D HexModalSilhouetteMask()
        {
            if (_hexModalSilhouetteMask != null)
                return _hexModalSilhouetteMask;

            const int n = 128;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            float cx = (n - 1) * 0.5f;
            float cy = (n - 1) * 0.5f;
            float R = n * 0.42f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    bool inside = PointInConvexPointyHex(x - cx, y - cy, R);
                    t.SetPixel(x, y, inside ? Color.white : Color.clear);
                }
            }

            t.Apply();
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            _hexModalSilhouetteMask = t;
            return _hexModalSilhouetteMask;
        }

        static bool PointInConvexPointyHex(float px, float py, float rad)
        {
            Vector2 p = new Vector2(px, py);
            Vector2[] v = new Vector2[6];
            for (int i = 0; i < 6; i++)
            {
                float a = Mathf.Deg2Rad * (60f * i + 30f);
                v[i] = new Vector2(rad * Mathf.Cos(a), rad * Mathf.Sin(a));
            }

            for (int i = 0; i < 6; i++)
            {
                Vector2 a = v[i];
                Vector2 b = v[(i + 1) % 6];
                float cross = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
                if (cross < 0f)
                    return false;
            }

            return true;
        }

        string HexModalOwnerMetaLine(PlayerState player, BoardTile tile)
        {
            bool hasAnyUnit = false;
            bool hasOtherOwner = false;
            int? soleOwnerIndex = null;
            foreach (var unit in FindObjectsOfType<UnitInstance>())
            {
                if (unit.Tile != tile)
                    continue;
                hasAnyUnit = true;
                if (soleOwnerIndex == null)
                    soleOwnerIndex = unit.Owner.PlayerIndex;
                else if (soleOwnerIndex != unit.Owner.PlayerIndex)
                    hasOtherOwner = true;
            }

            if (hasAnyUnit && hasOtherOwner)
                return "CONTESTED";
            if (tile.Owner != null)
                return "Owner P" + (tile.Owner.PlayerIndex + 1) + "  ·  (" + tile.Q + "," + tile.R + ")";
            return "Unowned  ·  (" + tile.Q + "," + tile.R + ")";
        }

        void DrawHexModalCreatureGrid2Rows3Cols(BoardTile tile, PlayerState hudPlayer, float width, float rowH,
            float rowGap)
        {
            var types = new[]
            {
                UnitType.Human, UnitType.Fungoid, UnitType.Crystalline,
                UnitType.RockStrider, UnitType.LavaLeaper, UnitType.RubiumDragon
            };

            var countByType = new Dictionary<UnitType, int>();
            foreach (var ut in types)
                countByType[ut] = 0;

            foreach (var unit in FindObjectsOfType<UnitInstance>())
            {
                if (unit == null || unit.Tile != tile)
                    continue;
                var t = unit.Definition.Type;
                if (countByType.ContainsKey(t))
                    countByType[t]++;
            }

            const float gap = 4f;
            float cellW = (width - gap * 2f) / 3f;

            for (int row = 0; row < 2; row++)
            {
                GUILayout.BeginHorizontal();
                for (int col = 0; col < 3; col++)
                {
                    int i = row * 3 + col;
                    var ut = types[i];
                    int n = countByType[ut];
                    PlayerState tintOwner = FirstOwnerOfUnitTypeOnTile(tile, ut, hudPlayer);
                    Rect cell = GUILayoutUtility.GetRect(cellW, rowH, GUILayout.Width(cellW), GUILayout.Height(rowH));
                    DrawHexModalOccupyingForceCell(cell, ut, n, tintOwner);
                }

                GUILayout.EndHorizontal();
                if (row == 0)
                    GUILayout.Space(rowGap);
            }
        }

        PlayerState FirstOwnerOfUnitTypeOnTile(BoardTile tile, UnitType type, PlayerState fallback)
        {
            foreach (var unit in FindObjectsOfType<UnitInstance>())
            {
                if (unit != null && unit.Tile == tile && unit.Definition.Type == type)
                    return unit.Owner;
            }

            return fallback;
        }

        void DrawHexModalOccupyingForceCell(Rect cell, UnitType type, int count, PlayerState tintOwner)
        {
            const float padX = 2f;
            const float padY = 2f;
            float iconSz = Mathf.Min(cell.height - padY * 2f, cell.width * 0.38f);
            iconSz = Mathf.Max(22f, iconSz);
            var iconR = new Rect(cell.x + padX, cell.y + (cell.height - iconSz) * 0.5f, iconSz, iconSz);
            DrawUnitMiniIcon(iconR, type, TintedIconOwnerForUnitOnSide(type, tintOwner));

            var countStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip
            };
            float countX = iconR.xMax + 4f;
            var countRect = new Rect(countX, cell.y + padY, cell.xMax - countX - padX, cell.height - padY * 2f);
            Color p = GUI.color;
            if (count <= 0)
                GUI.color = new Color(0.55f, 0.55f, 0.58f);
            GUI.Label(countRect, "×" + count, countStyle);
            GUI.color = p;
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
            if (_showCenterBuyModal)
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
            DrawTintedRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.02f, 0.03f, 0.06f, 0.68f));
            GUI.Window(900, r, _ => { BattleMainWindow(currentPlayer, h); }, "Battle", _battleWindowStyle);
            DrawOutlineRect(r, new Color(0.42f, 0.62f, 0.98f, 0.85f), 2f);
        }

        void BattleMainWindow(PlayerState currentPlayer, float windowHeight)
        {
            var left = Game.ActiveBattleAttacker ?? currentPlayer;
            var right = Game.ActiveBattleDefender;
            var hex = Game.ActiveBattleHex;
            bool casualtyDecision = Game.CasualtyPick != null;
            bool compactForDecision = casualtyDecision ||
                                      Game.EnergizePromptPlayer != null ||
                                      Game.FocusFirePicker != null ||
                                      (Game.SecretMissionOffer != null && Game.SecretMissionOffer.Waiting);

            GUILayout.BeginVertical(GUI.skin.box);
            DrawBattleContextBar(hex, left, right);
            GUILayout.Label(BattleNextActionShort(), new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.86f, 0.91f, 1f, 0.96f) }
            });
            if (Game.HasActiveBattleStep)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Now", GUILayout.Width(36));
                var stepIcon = GUILayoutUtility.GetRect(26f, 26f, GUILayout.Width(30f), GUILayout.Height(26f));
                DrawTintedRect(new Rect(stepIcon.x - 2f, stepIcon.y - 2f, stepIcon.width + 4f, stepIcon.height + 4f),
                    new Color(0.19f, 0.24f, 0.34f, 0.95f));
                DrawUnitMiniIcon(stepIcon, Game.ActiveBattleStepUnitType, BattleStepTintedUnitIconOwner());
                GUILayout.Label(UnitUiName(Game.ActiveBattleStepUnitType),
                    new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold });
                GUILayout.EndHorizontal();
            }

            DrawBattleDiceRollBanner();
            if (!casualtyDecision)
                DrawBattleOrderRibbonIcons();
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            DrawBattleSideColumn(left, hex, true, Game.ActiveBattleAttacker);
            GUILayout.FlexibleSpace();
            DrawBattleCenterClashMark(40);
            GUILayout.FlexibleSpace();
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
                GUILayout.Label("🔒 " + lockedReason, new GUIStyle(GUI.skin.label) { fontSize = 10, wordWrap = true });
            GUILayout.EndVertical();

            if (!compactForDecision)
            {
                GUILayout.Space(6f);
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("📜 Log", GUI.skin.box);
                string battleLog = !string.IsNullOrEmpty(Game.LiveBattlePhaseLog) ? Game.LiveBattlePhaseLog : Game.LastBattlePhaseLog;
                string safe = UiSafeText(battleLog);
                if (safe.Length != _lastBattleLogLen)
                {
                    _lastBattleLogLen = safe.Length;
                    _scrollBattleMainLog.y = 100000f;
                }
                float logH = Mathf.Max(90f, windowHeight * 0.23f);
                _scrollBattleMainLog = GUILayout.BeginScrollView(_scrollBattleMainLog, GUILayout.Height(logH));
                var logBody = new GUIStyle(GUI.skin.label) { fontSize = 10, wordWrap = true };
                GUILayout.Label(string.IsNullOrEmpty(safe) ? "(No battle log yet)" : safe, logBody);
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
            }
        }

        void DrawBattleContextBar(BoardTile hex, PlayerState left, PlayerState right)
        {
            string ctx;
            if (!string.IsNullOrEmpty(Game.EnergizeBattleContext))
                ctx = Game.EnergizeBattleContext;
            else if (hex != null && left != null && right != null)
                ctx = $"⬡({hex.Q},{hex.R})  P{left.PlayerIndex + 1}⚔P{right.PlayerIndex + 1}";
            else
                ctx = hex != null ? $"⬡({hex.Q},{hex.R})" : "Battle";
            GUILayout.Label(ctx, new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.95f, 0.97f, 1f, 0.98f) }
            });
        }

        void DrawBattleDiceRollBanner()
        {
            var dOpt = Game.LastBattleUiDiceRoll;
            if (!dOpt.HasValue)
                return;
            var d = dOpt.Value;
            GUILayout.BeginHorizontal(GUI.skin.box);
            string side = d.AttackerRolling ? "ATK" : "DEF";
            var sideC = d.AttackerRolling ? new Color(0.35f, 0.55f, 1f) : new Color(1f, 0.45f, 0.35f);
            var prev = GUI.color;
            GUI.color = sideC;
            GUILayout.Label(side, GUILayout.Width(30));
            GUI.color = prev;
            var ir = GUILayoutUtility.GetRect(22f, 22f, GUILayout.Width(26f), GUILayout.Height(24f));
            DrawUnitMiniIcon(ir, d.UnitType,
                TintedIconOwnerForBattleSide(d.UnitType,
                    d.AttackerRolling ? Game.ActiveBattleAttacker : Game.ActiveBattleDefender));
            if (d.Rolls != null && d.Rolls.Length > 0)
            {
                foreach (var v in d.Rolls)
                {
                    var dr = GUILayoutUtility.GetRect(26f, 26f, GUILayout.Width(28f), GUILayout.Height(28f));
                    DrawTintedRect(dr, new Color(0.17f, 0.22f, 0.32f, 0.98f));
                    GUI.Box(dr, v.ToString(), new GUIStyle(GUI.skin.box)
                    {
                        fontSize = 14,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = new Color(0.95f, 0.97f, 1f, 1f) }
                    });
                }
            }
            else if (d.Dice <= 0)
            {
                GUILayout.Label("0🎲", GUILayout.Width(36));
            }

            if (d.Impossible && d.Dice > 0)
                GUILayout.Label($"need ≥{d.Need} (—)", GUILayout.ExpandWidth(false));
            else if (d.Dice > 0)
                GUILayout.Label($"need ≥{d.Need}  →  {d.Hits}★", GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
        }

        void DrawBattleSideColumn(PlayerState player, BoardTile hex, bool isLeft, PlayerState expectedSide)
        {
            string title = player == null ? (isLeft ? "You" : "Opp") : $"P{player.PlayerIndex + 1}";
            string sideTag = expectedSide != null && player == expectedSide
                ? (player == Game.ActiveBattleAttacker ? "⚔" : "🛡")
                : "";
            int pendingHits = 0;
            if (player != null)
            {
                if (player == Game.ActiveBattleAttacker)
                    pendingHits = Game.ActiveBattleHitsOnAttacker;
                else if (player == Game.ActiveBattleDefender)
                    pendingHits = Game.ActiveBattleHitsOnDefender;
            }
            var prev = GUI.color;
            if (player == Game.ActiveBattleAttacker)
                GUI.color = new Color(0.2f, 0.38f, 0.8f, 0.95f);
            else if (player == Game.ActiveBattleDefender)
                GUI.color = new Color(0.78f, 0.26f, 0.2f, 0.95f);
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(220f), GUILayout.MinHeight(100f));
            GUI.color = prev;
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.93f, 0.96f, 1f, 1f) }
            };
            string hdr = string.IsNullOrEmpty(sideTag) ? title : $"{title} {sideTag}";
            if (player == Game.ActiveBattleAttacker || player == Game.ActiveBattleDefender)
                hdr += $" ☠{pendingHits}";
            GUILayout.Label(hdr, titleStyle);
            if (player == null || hex == null)
            {
                GUILayout.Label("(—)");
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
                int i = 0;
                GUILayout.BeginHorizontal();
                foreach (var kvp in counts.OrderBy(k => Array.IndexOf(BattleResolver.BattleOrder, k.Key)))
                {
                    if (i > 0 && i % 2 == 0)
                    {
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                    }

                    i++;
                    GUILayout.BeginVertical(GUILayout.Width(102f));
                    var box = GUILayoutUtility.GetRect(88f, 52f, GUILayout.Width(96f), GUILayout.Height(56f));
                    DrawTintedRect(box, new Color(0.15f, 0.19f, 0.29f, 0.95f));
                    GUI.Box(box, "");
                    var iconR = new Rect(box.x + 6f, box.y + 4f, 36f, 36f);
                    DrawTintedRect(new Rect(iconR.x - 1f, iconR.y - 1f, iconR.width + 2f, iconR.height + 2f),
                        new Color(0.24f, 0.30f, 0.44f, 0.95f));
                    DrawUnitMiniIcon(iconR, kvp.Key, TintedIconOwnerForUnitOnSide(kvp.Key, player));
                    var nameStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 9,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.LowerRight,
                        wordWrap = false
                    };
                    GUI.Label(new Rect(box.x + 2f, box.yMax - 16f, box.width - 4f, 14f), "×" + kvp.Value, nameStyle);
                    GUI.Label(new Rect(box.x + 44f, box.y + 6f, 48f, 40f), UnitTypeAbbrev(kvp.Key),
                        new GUIStyle(GUI.skin.label) { fontSize = 9, wordWrap = true });

                    var cp = Game.CasualtyPick;
                    bool canPickHere = cp != null && cp.Owner == player;
                    if (canPickHere)
                    {
                        int selected = cp.Selected.Count(u => u != null && u.Definition.Type == kvp.Key);
                        string selText = $"{selected}/{kvp.Value}";
                        if (selected > 0)
                        {
                            // Strong selected state: bright border + warm fill so chosen casualties are obvious.
                            DrawTintedRect(new Rect(box.x + 1f, box.y + 1f, box.width - 2f, box.height - 2f),
                                new Color(0.92f, 0.64f, 0.12f, 0.26f));
                            DrawOutlineRect(new Rect(box.x + 1f, box.y + 1f, box.width - 2f, box.height - 2f),
                                new Color(1f, 0.82f, 0.2f, 0.98f), 2f);
                        }
                        var badgeRect = new Rect(box.x + 2f, box.y + 2f, 44f, 18f);
                        DrawTintedRect(badgeRect, selected > 0
                            ? new Color(0.95f, 0.72f, 0.18f, 0.92f)
                            : new Color(0.18f, 0.22f, 0.30f, 0.9f));
                        GUI.Label(badgeRect, selText,
                            new GUIStyle(GUI.skin.label)
                            {
                                fontSize = 11,
                                fontStyle = FontStyle.Bold,
                                alignment = TextAnchor.MiddleCenter,
                                normal = { textColor = selected > 0 ? new Color(0.15f, 0.1f, 0.02f, 1f) : new Color(0.82f, 0.88f, 0.98f, 1f) }
                            });

                        bool canAdd = cp.Selected.Count < cp.Required && selected < kvp.Value;
                        bool prevEnabled = GUI.enabled;
                        GUI.enabled = canAdd;
                        if (GUI.Button(box, GUIContent.none, GUIStyle.none))
                            AdjustCasualtyTypeSelection(cp, kvp.Key, +1);
                        GUI.enabled = prevEnabled;

                        var minusRect = new Rect(box.xMax - 18f, box.y + 2f, 16f, 14f);
                        bool canSub = selected > 0;
                        prevEnabled = GUI.enabled;
                        var prevColor = GUI.color;
                        GUI.enabled = canSub;
                        if (!canSub)
                            GUI.color = new Color(0.55f, 0.55f, 0.6f, 0.9f);
                        if (GUI.Button(minusRect, "-"))
                            AdjustCasualtyTypeSelection(cp, kvp.Key, -1);
                        GUI.enabled = prevEnabled;
                        GUI.color = prevColor;
                    }
                    GUILayout.EndVertical();
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
        }

        void DrawBattleOrderRibbonIcons()
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            foreach (var t in BattleResolver.BattleOrder)
            {
                bool active = Game.HasActiveBattleStep && Game.ActiveBattleStepUnitType == t;
                var prev = GUI.color;
                GUI.color = active ? new Color(1f, 0.85f, 0.2f, 1f) : new Color(0.35f, 0.35f, 0.35f, 0.9f);
                GUILayout.BeginVertical(GUILayout.Width(34f));
                var ir = GUILayoutUtility.GetRect(28f, 28f, GUILayout.Width(32f), GUILayout.Height(30f));
                if (active)
                    DrawOutlineRect(new Rect(ir.x - 1f, ir.y - 1f, ir.width + 2f, ir.height + 2f),
                        new Color(1f, 0.83f, 0.33f, 0.95f), 1f);
                DrawUnitMiniIcon(ir, t);
                GUILayout.Label(UnitTypeAbbrev(t),
                    new GUIStyle(GUI.skin.label) { fontSize = 7, alignment = TextAnchor.MiddleCenter });
                GUILayout.EndVertical();
                GUI.color = prev;
            }

            GUILayout.EndHorizontal();
        }

        string BattleNextActionShort()
        {
            if (Game.PendingBattleArrangement)
                return "📋 Order fights → Confirm";
            if (Game.FocusFirePicker != null)
                return "🎯 Focus Fire: choose unit type (+2 dice)";
            if (Game.EnergizePromptPlayer != null)
                return "⚡ Battle Energize or Pass (P" + (Game.EnergizePromptPlayer.PlayerIndex + 1) + ")";
            if (Game.CasualtyPick != null)
                return "☠ Pick " + Game.CasualtyPick.Required + " casualty(s) (P" +
                       (Game.CasualtyPick.Owner.PlayerIndex + 1) + ")";
            if (Game.SecretMissionOffer != null && Game.SecretMissionOffer.Waiting)
                return "★ Optional secret mission (P" + (Game.SecretMissionOffer.Attacker.PlayerIndex + 1) + ")";
            if (Game.HasActiveBattleStep)
                return "🎲 Resolve step — casualties next if any";
            return "Battle";
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

            GUILayout.Label("+2🎲 on type:", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            foreach (var t in BattleResolver.BattleOrder)
            {
                if (!types.Contains(t))
                    continue;
                GUILayout.BeginHorizontal();
                var ir = GUILayoutUtility.GetRect(28f, 28f, GUILayout.Width(32f), GUILayout.Height(28f));
                DrawUnitMiniIcon(ir, t, TintedIconOwnerForUnitOnSide(t, Game.FocusFirePicker));
                if (GUILayout.Button(UnitUiName(t), GUILayout.Height(30f)))
                    Game.SubmitFocusFireUnitType(t);
                GUILayout.EndHorizontal();
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

            GUILayout.Label(
                $"Tap unit cards above to add casualties. Use the small '-' on a card to subtract.",
                new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            GUILayout.Label(
                $"P{cp.Owner.PlayerIndex + 1} pick {cp.Required}  ·  {cp.Selected.Count}/{cp.Required}",
                new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.9f, 0.35f, 1f) } });

            GUILayout.Space(6f);
            if (GUILayout.Button("Auto-pick casualties", GUILayout.Height(24f)))
                AutoPickCasualties(cp);
            if (GUILayout.Button("Clear selected casualties", GUILayout.Height(24f)))
                cp.Selected.Clear();

            GUI.enabled = cp.Selected.Count == cp.Required;
            if (GUILayout.Button("Confirm casualties", GUILayout.Height(28f)))
                Game.SubmitCasualtyPick();
            GUI.enabled = true;
        }

        void DrawBattleCenterClashMark(int fontSize)
        {
            var s = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.86f, 0.3f, 0.96f) }
            };
            GUILayout.Label("⚔", s, GUILayout.Width(fontSize + 16f), GUILayout.Height(fontSize + 10f));
        }

        static void AdjustCasualtyTypeSelection(CasualtyPickState cp, UnitType type, int delta)
        {
            if (cp == null || delta == 0)
                return;

            if (delta > 0)
            {
                if (cp.Selected.Count >= cp.Required)
                    return;

                var next = cp.Pool.FirstOrDefault(u => u != null && u.Definition.Type == type && !cp.Selected.Contains(u));
                if (next != null)
                    cp.Selected.Add(next);
                return;
            }

            for (int i = cp.Selected.Count - 1; i >= 0; i--)
            {
                var s = cp.Selected[i];
                if (s != null && s.Definition.Type == type)
                {
                    cp.Selected.RemoveAt(i);
                    return;
                }
            }
        }

        static void AutoPickCasualties(CasualtyPickState cp)
        {
            if (cp == null)
                return;

            cp.Selected.RemoveAll(u => u == null || !cp.Pool.Contains(u));
            while (cp.Selected.Count < cp.Required)
            {
                var next = cp.Pool.FirstOrDefault(u => u != null && !cp.Selected.Contains(u));
                if (next == null)
                    break;
                cp.Selected.Add(next);
            }
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

        void DrawBuyUnitGrid(float x0, float y0, float width, int columns, float nameBoxH, float shopIconSize,
            float iconRowH, float costGap, float rowGap, int nameFontSize, bool largeShopCards = false)
        {
            float colGap = largeShopCards ? 14f : (columns >= 2 ? 12f : 8f);
            float cellW = (width - colGap * (columns - 1f)) / columns;
            float cardH = nameBoxH + costGap + iconRowH;
            float rowStride = cardH + rowGap;

            var items = new[]
            {
                ("Human", UnitType.Human, 1),
                ("Fungoid", UnitType.Fungoid, 2),
                ("Crystalline", UnitType.Crystalline, 2),
                ("Rock Strider", UnitType.RockStrider, 3),
                ("Lava Leaper", UnitType.LavaLeaper, 4),
                ("Rubium Dragon", UnitType.RubiumDragon, 8),
            };

            for (int i = 0; i < items.Length; i++)
            {
                int col = i % columns;
                int row = i / columns;
                float cx = x0 + col * (cellW + colGap);
                float cy = y0 + row * rowStride;
                var cardRect = new Rect(cx, cy, cellW, cardH);
                DrawBuyUnitCell(cardRect, items[i].Item2, items[i].Item3, items[i].Item1, costGap, iconRowH,
                    shopIconSize, nameFontSize, largeShopCards);
            }
        }

        void DrawBuyUnitCell(Rect cardRect, UnitType type, int baseCost, string displayName, float costGap,
            float iconRowH, float shopIconSize = 34f, int nameFontSize = 10, bool largeShopCard = false)
        {
            var player = Game.CurrentPlayer;
            int maxOff = Mathf.Max(0, baseCost - 1);
            int use = Mathf.Min(maxOff, player.DeploymentPurchaseDiscountRubium);
            int pay = baseCost - use;
            bool canAfford = player.Rubium >= pay;

            var nameStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                fontSize = nameFontSize,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip
            };

            float pad = largeShopCard ? 10f : 2f;
            float barH = largeShopCard ? iconRowH + 8f : iconRowH;
            float costY = largeShopCard ? cardRect.yMax - barH : cardRect.yMax - iconRowH;
            float bodyBottom = largeShopCard ? costY : costY - costGap;
            float iconTop = cardRect.y + pad;
            float iconUse = Mathf.Min(shopIconSize, (bodyBottom - iconTop) * (largeShopCard ? 0.72f : 0.65f));
            iconUse = Mathf.Max(22f, iconUse);

            Color prev = GUI.color;
            if (!canAfford)
                GUI.color = new Color(0.55f, 0.55f, 0.58f);

            if (largeShopCard)
            {
                DrawTintedRect(cardRect, new Color(0.09f, 0.1f, 0.14f, 0.96f));
                DrawOutlineRect(cardRect, new Color(0.88f, 0.78f, 0.28f, 0.55f), 1.5f);
                var bar = new Rect(cardRect.x + 2f, costY, cardRect.width - 4f, barH - 1f);
                DrawTintedRect(bar, new Color(0.05f, 0.06f, 0.09f, 0.92f));
            }
            else
                GUI.Box(cardRect, "");

            var shopIconRect = new Rect(
                cardRect.x + (cardRect.width - iconUse) * 0.5f,
                iconTop,
                iconUse,
                iconUse);
            DrawUnitMiniIcon(shopIconRect, type, TintedIconOwnerForUnitOnSide(type, player));

            float nameTop = shopIconRect.yMax + (largeShopCard ? 6f : 4f);
            float nameH = Mathf.Max(16f, bodyBottom - nameTop - (largeShopCard ? 4f : costGap));
            var nameLabelRect = new Rect(cardRect.x + pad, nameTop, cardRect.width - pad * 2f, nameH);
            GUI.Label(nameLabelRect, displayName, nameStyle);

            if (GUI.Button(cardRect, GUIContent.none, GUIStyle.none) && canAfford)
                TryBuyUnit(type, player, use, pay);

            GUI.color = prev;

            var rub = GetRubiumGui();
            float rubH = largeShopCard ? Mathf.Min(22f, iconRowH) : 18f;
            float iconW = rub.IsEmpty ? 0f : rubH * rub.AspectRatio;
            float textW = largeShopCard ? 44f : 36f;
            float rowW = iconW + 6f + textW;
            float startX = cardRect.x + (cardRect.width - rowW) * 0.5f;
            float costLineY = largeShopCard ? costY + (barH - 1f - rubH) * 0.5f : costY;
            if (!rub.IsEmpty)
                rub.Draw(startX, costLineY + 1f, rubH);
            var costStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = largeShopCard ? 15 : 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            if (!canAfford)
                GUI.color = new Color(0.55f, 0.55f, 0.58f);
            GUI.Label(new Rect(startX + iconW + 6f, costLineY, textW, iconRowH), pay.ToString(), costStyle);
            GUI.color = prev;
        }

        void TryBuyUnit(UnitType type, PlayerState player, int discountUse, int pay)
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
            if (homeTile == null)
                return;
            player.DeploymentPurchaseDiscountRubium -= discountUse;
            player.Rubium -= pay;
            Game.SpawnUnit(player, type, homeTile);
        }

        void DrawSelectedTilePanelBody(PlayerState player, BoardTile popupTile, float contentWidth)
        {
            if (popupTile == null)
                return;

            var rowTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                wordWrap = false
            };
            var tiny = new GUIStyle(GUI.skin.label) { fontSize = 9, wordWrap = false };

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

            GUILayout.BeginHorizontal();
            GUILayout.Label(popupTile.Type.ToString(), rowTitle, GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();
            if (hasAnyUnit && hasOtherOwner)
            {
                var prev = GUI.color;
                GUI.color = Color.red;
                GUILayout.Label("CONTESTED", rowTitle);
                GUI.color = prev;
            }
            else
            {
                string ow = popupTile.Owner != null ? "P" + (popupTile.Owner.PlayerIndex + 1) : "None";
                GUILayout.Label("· " + ow, rowTitle);
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Mine", tiny, GUILayout.Width(30f));
            int my = popupTile.ExtraMineYield;
            var chipGui = GetOreChipGui(my);
            if (chipGui.IsEmpty && my > 0)
                chipGui = GetRubiumGui();
            if (!chipGui.IsEmpty)
            {
                var ir = GUILayoutUtility.GetRect(20f, 20f, GUILayout.Width(22f), GUILayout.Height(22f));
                chipGui.Draw(ir);
            }

            GUILayout.Label(my > 0 ? my.ToString() : "—", tiny, GUILayout.Width(28f));
            GUILayout.EndHorizontal();

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

            bool isMovementPhase = !Game.IsGameOver &&
                                   !Game.BattlePhaseBlockingPlay &&
                                   Game.DragonPhase == null &&
                                   !Game.IsAiControlled(player);

            if (_moveAllTile != popupTile)
            {
                _moveAllTile = popupTile;
                _moveAllChecked = false;
            }

            if (InputController != null && counts.Count > 0 && isMovementPhase)
            {
                GUILayout.BeginHorizontal();
                bool nextMoveAll = GUILayout.Toggle(_moveAllChecked, "Move all", GUILayout.Width(88f));
                if (nextMoveAll != _moveAllChecked)
                {
                    _moveAllChecked = nextMoveAll;
                    foreach (var kvp in counts)
                        InputController.SetMoveSelection(kvp.Key, _moveAllChecked ? kvp.Value : 0);
                }

                GUILayout.EndHorizontal();
            }

            const float boxSz = 40f;
            var selectedCounts = InputController != null ? InputController.SelectedMoveCounts : null;

            if (InputController != null && counts.Count > 0)
            {
                foreach (var kvp in counts.OrderBy(x => x.Key.ToString()))
                {
                    selectedCounts.TryGetValue(kvp.Key, out int chosen);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("-", GUILayout.Width(22f), GUILayout.Height(boxSz + 4f)) &&
                        isMovementPhase)
                    {
                        InputController.AdjustMoveSelection(kvp.Key, -1);
                        _moveAllChecked = false;
                    }

                    Rect boxR = GUILayoutUtility.GetRect(boxSz, boxSz, GUILayout.ExpandWidth(false));
                    DrawTileUnitQuantityBox(boxR, kvp.Key, chosen, kvp.Value, player);

                    if (GUILayout.Button("+", GUILayout.Width(22f), GUILayout.Height(boxSz + 4f)) &&
                        isMovementPhase)
                    {
                        InputController.AdjustMoveSelection(kvp.Key, +1);
                        _moveAllChecked = false;
                    }

                    GUILayout.EndHorizontal();
                }
            }

            bool anyReadonly = false;
            foreach (var kvp in stacks.OrderBy(k => k.Key))
            {
                var split = kvp.Key.Split('|');
                if (split.Length < 2 || !System.Enum.TryParse<UnitType>(split[1], out var ut))
                    continue;
                if (!int.TryParse(split[0].TrimStart('P'), out int pn))
                    continue;
                int idx = pn - 1;
                bool mine = idx == player.PlayerIndex;
                if (mine && counts.ContainsKey(ut))
                    continue;
                anyReadonly = true;
                break;
            }

            if (anyReadonly)
            {
                GUILayout.Space(4f);
                GUILayout.Label("On tile", tiny);
                GUILayout.BeginHorizontal();
                foreach (var kvp in stacks.OrderBy(k => k.Key))
                {
                    var split = kvp.Key.Split('|');
                    if (split.Length < 2 || !System.Enum.TryParse<UnitType>(split[1], out var ut))
                        continue;
                    if (!int.TryParse(split[0].TrimStart('P'), out int pn))
                        continue;
                    int idx = pn - 1;
                    bool mine = idx == player.PlayerIndex;
                    if (mine && counts.ContainsKey(ut))
                        continue;

                    Rect chipR = GUILayoutUtility.GetRect(64f, 36f, GUILayout.ExpandWidth(false));
                    PlayerState stackOwner =
                        Game != null && idx >= 0 && idx < Game.Players.Count ? Game.Players[idx] : null;
                    DrawTileUnitReadonlyChip(chipR, "P" + pn, ut, kvp.Value, stackOwner);
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6f);
            if (GUILayout.Button("Close", GUILayout.Height(22f)) && InputController != null)
                InputController.ClearSelection();
        }

        static string UnitTypeAbbrev(UnitType type)
        {
            string n = UnitUiName(type);
            return n.Length <= 2 ? n : n.Substring(0, 2);
        }

        void DrawTileUnitQuantityBox(Rect r, UnitType type, int selected, int available, PlayerState stackOwner)
        {
            GUI.Box(r, "");
            var iconR = new Rect(r.x + 2f, r.y + 9f, 22f, 22f);
            DrawUnitMiniIcon(iconR, type, TintedIconOwnerForUnitOnSide(type, stackOwner));
            float tx = r.x + 26f;
            float tw = r.width - 27f;
            var s1 = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false
            };
            var s2 = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false
            };
            GUI.Label(new Rect(tx, r.y + 3f, tw, 14f), UnitTypeAbbrev(type), s1);
            GUI.Label(new Rect(tx, r.y + 18f, tw, 18f), selected + "/" + available, s2);
        }

        void DrawTileUnitReadonlyChip(Rect r, string ownerPrefix, UnitType type, int count, PlayerState stackOwner)
        {
            DrawTintedRect(new Rect(r.x, r.y, r.width, r.height), new Color(0.22f, 0.22f, 0.28f));
            GUI.Box(r, "");
            var iconR = new Rect(r.x + 2f, r.y + 5f, 22f, r.height - 10f);
            DrawUnitMiniIcon(iconR, type, TintedIconOwnerForUnitOnSide(type, stackOwner));
            float tx = r.x + 26f;
            float tw = Mathf.Max(22f, r.width - 28f);
            var s0 = new GUIStyle(GUI.skin.label)
            {
                fontSize = 8,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false
            };
            var s1 = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false
            };
            GUI.Label(new Rect(tx, r.y + 2f, tw, 11f), ownerPrefix, s0);
            GUI.Label(new Rect(tx, r.y + 14f, tw, 18f), UnitTypeAbbrev(type) + "×" + count, s1);
        }

        void DrawTileUnitReadonlyChipLarge(Rect r, string ownerPrefix, UnitType type, int count, PlayerState stackOwner)
        {
            DrawTintedRect(new Rect(r.x, r.y, r.width, r.height), new Color(0.22f, 0.22f, 0.28f));
            GUI.Box(r, "");
            float iconSz = Mathf.Min(36f, r.height - 8f);
            var iconR = new Rect(r.x + 6f, r.y + (r.height - iconSz) * 0.5f, iconSz, iconSz);
            DrawUnitMiniIcon(iconR, type, TintedIconOwnerForUnitOnSide(type, stackOwner));
            float tx = iconR.xMax + 10f;
            float tw = Mathf.Max(40f, r.xMax - tx - 6f);
            var s0 = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false
            };
            var s1 = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false
            };
            GUI.Label(new Rect(tx, r.y + 6f, tw, 16f), ownerPrefix, s0);
            GUI.Label(new Rect(tx, r.y + 22f, tw, 22f), UnitUiName(type) + " ×" + count, s1);
        }

        PlayerState BattleStepTintedUnitIconOwner()
        {
            if (Game == null || !UsesPerPlayerTint(Game.ActiveBattleStepUnitType))
                return null;
            var d = Game.LastBattleUiDiceRoll;
            if (d.HasValue && d.Value.UnitType == Game.ActiveBattleStepUnitType)
                return d.Value.AttackerRolling ? Game.ActiveBattleAttacker : Game.ActiveBattleDefender;
            return Game.ActiveBattleAttacker;
        }

        /// <summary>Units with seat-colored sprites (Human Red, Leaper Blue, etc.).</summary>
        static bool UsesPerPlayerTint(UnitType t) =>
            t == UnitType.Human || t == UnitType.Fungoid || t == UnitType.Crystalline ||
            t == UnitType.RockStrider || t == UnitType.LavaLeaper || t == UnitType.RubiumDragon;

        static PlayerState TintedIconOwnerForUnitOnSide(UnitType t, PlayerState sidePlayer) =>
            UsesPerPlayerTint(t) ? sidePlayer : null;

        static PlayerState TintedIconOwnerForBattleSide(UnitType t, PlayerState rollingPlayer) =>
            UsesPerPlayerTint(t) ? rollingPlayer : null;

        void DrawUnitMiniIcon(Rect r, UnitType type, PlayerState ownerForTint = null)
        {
            var icon = UsesPerPlayerTint(type) && ownerForTint != null
                ? IconForUnitWithOwner(type, ownerForTint)
                : GetUnitIcon(type);
            if (!icon.IsEmpty)
            {
                icon.Draw(r);
                return;
            }

            DrawTintedRect(r, new Color(0.85f, 0.85f, 0.9f));
            var letterStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip
            };
            GUI.Label(r, UnitUiName(type).Substring(0, 1), letterStyle);
        }

        NexusGuiImage IconForUnitWithOwner(UnitType type, PlayerState owner)
        {
            switch (type)
            {
                case UnitType.RubiumDragon:
                    return GetDragonUnitIcon(owner);
                case UnitType.RockStrider:
                    return GetRockStriderUnitIcon(owner);
                case UnitType.Fungoid:
                    return GetFungoidUnitIcon(owner);
                case UnitType.Human:
                    return GetHumanUnitIcon(owner);
                case UnitType.LavaLeaper:
                    return GetLavaLeaperUnitIcon(owner);
                case UnitType.Crystalline:
                    return GetCrystallineUnitIcon(owner);
                default:
                    return GetUnitIcon(type);
            }
        }

        NexusGuiImage GetDragonUnitIcon(PlayerState owner)
        {
            if (owner == null)
                return GetUnitIcon(UnitType.RubiumDragon);

            if (_dragonIconByPlayerIndex.TryGetValue(owner.PlayerIndex, out var cached))
                return cached;

            var img = NexusGuiArt.LoadRubiumDragonForPlayer(owner);
            if (img.IsEmpty)
                img = GetUnitIcon(UnitType.RubiumDragon);
            _dragonIconByPlayerIndex[owner.PlayerIndex] = img;
            return img;
        }

        NexusGuiImage GetRockStriderUnitIcon(PlayerState owner)
        {
            if (owner == null)
                return GetUnitIcon(UnitType.RockStrider);

            if (_striderIconByPlayerIndex.TryGetValue(owner.PlayerIndex, out var cached))
                return cached;

            var img = NexusGuiArt.LoadRockStriderForPlayer(owner);
            if (img.IsEmpty)
                img = GetUnitIcon(UnitType.RockStrider);
            _striderIconByPlayerIndex[owner.PlayerIndex] = img;
            return img;
        }

        NexusGuiImage GetFungoidUnitIcon(PlayerState owner)
        {
            if (owner == null)
                return GetUnitIcon(UnitType.Fungoid);

            if (_fungoidIconByPlayerIndex.TryGetValue(owner.PlayerIndex, out var cached))
                return cached;

            var img = NexusGuiArt.LoadFungoidForPlayer(owner);
            if (img.IsEmpty)
                img = GetUnitIcon(UnitType.Fungoid);
            _fungoidIconByPlayerIndex[owner.PlayerIndex] = img;
            return img;
        }

        NexusGuiImage GetHumanUnitIcon(PlayerState owner)
        {
            if (owner == null)
                return GetUnitIcon(UnitType.Human);

            if (_humanIconByPlayerIndex.TryGetValue(owner.PlayerIndex, out var cached))
                return cached;

            var img = NexusGuiArt.LoadHumanForPlayer(owner);
            if (img.IsEmpty)
                img = GetUnitIcon(UnitType.Human);
            _humanIconByPlayerIndex[owner.PlayerIndex] = img;
            return img;
        }

        NexusGuiImage GetLavaLeaperUnitIcon(PlayerState owner)
        {
            if (owner == null)
                return GetUnitIcon(UnitType.LavaLeaper);

            if (_lavaLeaperIconByPlayerIndex.TryGetValue(owner.PlayerIndex, out var cached))
                return cached;

            var img = NexusGuiArt.LoadLavaLeaperForPlayer(owner);
            if (img.IsEmpty)
                img = GetUnitIcon(UnitType.LavaLeaper);
            _lavaLeaperIconByPlayerIndex[owner.PlayerIndex] = img;
            return img;
        }

        NexusGuiImage GetCrystallineUnitIcon(PlayerState owner)
        {
            if (owner == null)
                return GetUnitIcon(UnitType.Crystalline);

            if (_crystallineIconByPlayerIndex.TryGetValue(owner.PlayerIndex, out var cached))
                return cached;

            var img = NexusGuiArt.LoadCrystallineForPlayer(owner);
            if (img.IsEmpty)
                img = GetUnitIcon(UnitType.Crystalline);
            _crystallineIconByPlayerIndex[owner.PlayerIndex] = img;
            return img;
        }

        static void CollectUnitIconResourcePaths(UnitType type, List<string> paths)
        {
            string key = type.ToString();
            string ui = UnitUiName(type);
            string compact = ui.Replace(" ", "");
            string under = ui.Replace(" ", "_");

            void Add(string p)
            {
                if (string.IsNullOrEmpty(p) || paths.Contains(p))
                    return;
                paths.Add(p);
            }

            // Prefer typed art under Sprites/Units (common import location).
            Add("Sprites/Units/" + key);
            Add("Sprites/Units/" + compact);
            Add("Sprites/Units/" + under);
            Add("Sprites/units/" + key);
            Add("Sprites/units/" + compact);
            Add("Sprites/" + key);
            Add("Sprites/" + compact);
            Add("Sprites/" + under);

            switch (type)
            {
                case UnitType.RockStrider:
                    Add("Sprites/Units/Strider");
                    Add("Sprites/units/Strider");
                    Add("Sprites/Units/RockStrider");
                    break;
                case UnitType.LavaLeaper:
                    Add("Sprites/Units/LavaLeaper");
                    Add("Sprites/Units/Lava_Leaper");
                    break;
                case UnitType.Crystalline:
                    Add("Sprites/Units/Crystal");
                    break;
                case UnitType.Fungoid:
                    Add("Sprites/Units/Fungus");
                    break;
                case UnitType.Human:
                    Add("Sprites/Units/Colonist");
                    break;
            }
        }

        NexusGuiImage GetUnitIcon(UnitType type)
        {
            if (!UsesPerPlayerTint(type) && _unitIconCache.TryGetValue(type, out var cached))
                return cached;

            var pathList = new List<string>();
            CollectUnitIconResourcePaths(type, pathList);
            var loaded = NexusGuiArt.Load(pathList.ToArray());
            if (type == UnitType.RubiumDragon && loaded.IsEmpty)
                loaded = NexusGuiArt.LoadRubiumDragonLegendIcon();
            if (type == UnitType.RockStrider && loaded.IsEmpty)
                loaded = NexusGuiArt.LoadRockStriderLegendIcon();
            if (type == UnitType.Fungoid && loaded.IsEmpty)
                loaded = NexusGuiArt.LoadFungoidLegendIcon();
            if (type == UnitType.Human && loaded.IsEmpty)
                loaded = NexusGuiArt.LoadHumanLegendIcon();
            if (type == UnitType.LavaLeaper && loaded.IsEmpty)
                loaded = NexusGuiArt.LoadLavaLeaperLegendIcon();
            if (type == UnitType.Crystalline && loaded.IsEmpty)
                loaded = NexusGuiArt.LoadCrystallineLegendIcon();

            if (!UsesPerPlayerTint(type))
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




