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
        NexusGuiImage _tileInfoScreenBg;
        bool _tileInfoScreenTried;
        NexusGuiImage _battleScreenBg;
        bool _battleScreenTried;
        float _battlePanelContentWidth;
        float _battleHudUiScale = 1f;
        float _battlePanelScaleCached = 1f;

        /// <summary>Scales main gameplay HUD (not tile-info modal) for narrow phones — same idea as <see cref="BattleHudUiScale"/>.</summary>
        float _mainHudUiScale = 1f;

        /// <summary>Font size multiplier — <see cref="GameUiScale.ImGuiFontScale"/> (no touch floor; shrinks on small screens).</summary>
        float _hudFontScale = 1f;

        float _hudCardBarHeight = 136f;
        float _hudPhaseRibbonHeight = 26f;
        Texture2D _tileInfoScrollClearTex;
        GUIStyle _tileInfoScrollViewTransparent;
        GUIStyle _tileInfoHiddenHScrollbar;
        GUIStyle _tileInfoHiddenVScrollbar;
        Font _tileInfoUiFont;
        bool _tileInfoUiFontTried;

        /// <summary>IMGUI uses Unity <see cref="Font"/> from Resources (<c>Fonts/Bemora</c>). TextMesh Pro needs an SDF asset — generate it with the Nexus → Fonts menu in the editor.</summary>
        Font TileInfoUiFont()
        {
            if (!_tileInfoUiFontTried)
            {
                _tileInfoUiFontTried = true;
                _tileInfoUiFont = Resources.Load<Font>("Fonts/Bemora")
                                  ?? Resources.Load<Font>("Fonts/Bemora-Regular");
            }

            return _tileInfoUiFont;
        }

        void ApplyTileInfoFont(GUIStyle style)
        {
            Font f = TileInfoUiFont();
            if (f != null)
                style.font = f;
        }

        void EnsureTileInfoHiddenScrollbars()
        {
            if (_tileInfoHiddenVScrollbar != null)
                return;
            _tileInfoHiddenVScrollbar = new GUIStyle
            {
                fixedWidth = 0,
                fixedHeight = 0,
                stretchWidth = false,
                stretchHeight = false
            };
            _tileInfoHiddenHScrollbar = new GUIStyle
            {
                fixedWidth = 0,
                fixedHeight = 0,
                stretchWidth = false,
                stretchHeight = false
            };
        }

        public Rect GetCenterBuyModalPanelGuiRect()
        {
            return GameUiScale.GetPaddedModalPanelGuiRect();
        }

        /// <summary>Battle IMGUI group — same padded safe rect as tile-info (<see cref="GetCenterBuyModalPanelGuiRect"/>).</summary>
        public Rect GetBattleScreenPanelGuiRect()
        {
            return GetCenterBuyModalPanelGuiRect();
        }

        static int TileInfoScaledFont(float designSize, float panelScale, int minSize) =>
            GameUiScale.TileInfoScaledFont(designSize, panelScale, minSize);

        static float BattleHudUiScale(Rect panel) => GameUiScale.BattleHudUiScale(panel);

        /// <summary>Same as tile-info modal layout <c>S()</c> scale — <see cref="GameUiScale.ImGuiHudScale"/>.</summary>
        static float MainHudUiScale() => GameUiScale.ImGuiHudScale();

        static float MainHudFontScale() => GameUiScale.ImGuiFontScale();

        /// <summary>Scaled design pixels for main gameplay HUD (outside battle overlay / tile modal).</summary>
        float HudS(float designPixels) => Mathf.Max(1f, designPixels * _mainHudUiScale);

        /// <summary>Scaled hand / pile card tile size.</summary>
        float HudCardTileW() => HudS(112f);

        float HudCardTileH() => HudS(104f);

        /// <summary>Scaled design-pixel value for current battle overlay (spacing, min sizes).</summary>
        float BattleS(float designPixels) => Mathf.Max(1f, designPixels * _battleHudUiScale);

        /// <summary>Updates ribbon/button fonts from <see cref="_hudFontScale"/> and layout from <see cref="_battleHudUiScale"/> each battle frame.</summary>
        void ApplyBattleHudScaledStyles()
        {
            EnsureBattleHudStyles();
            float fs = _hudFontScale;
            float s = _battleHudUiScale;
            _battleRibbonLabelStyle.fontSize = Mathf.Max(14, Mathf.RoundToInt(16f * fs));
            _battlePrimaryButtonStyleCached.fontSize = Mathf.Max(16, Mathf.RoundToInt(18f * fs));
            _battlePrimaryButtonStyleCached.fixedHeight = Mathf.Max(44f, 50f * s);
            int pad = Mathf.Max(8, Mathf.RoundToInt(14f * s));
            int pady = Mathf.Max(8, Mathf.RoundToInt(12f * s));
            _battlePrimaryButtonStyleCached.padding = new RectOffset(pad, pad, pady, pady);
            _battleSecondaryButtonStyleCached.fontSize = Mathf.Max(15, Mathf.RoundToInt(17f * fs));
            _battleSecondaryButtonStyleCached.fixedHeight = Mathf.Max(42f, 48f * s);
            _battleSecondaryButtonStyleCached.padding = new RectOffset(pad, pad, pady, pady);
        }

        void ApplyMainHudScaledStyles()
        {
            EnsureCardStyles();
            float s = _hudFontScale;
            _cardTitleStyle.fontSize = Mathf.Max(12, Mathf.RoundToInt(12f * s));
            _cardBodyStyle.fontSize = Mathf.Max(11, Mathf.RoundToInt(11f * s));
            _cardBadgeStyle.fontSize = Mathf.Max(12, Mathf.RoundToInt(12f * s));
            _cardColumnLabelStyle.fontSize = Mathf.Max(12, Mathf.RoundToInt(12f * s));

            if (_flyRubiumAmountStyle != null)
                _flyRubiumAmountStyle.fontSize = Mathf.Max(14, Mathf.RoundToInt(14f * s));
            if (_flyVpAmountStyle != null)
            {
                _flyVpAmountStyle.fontSize = Mathf.Max(18, Mathf.RoundToInt(26f * s));
                if (_flyVpFallbackStyle != null)
                {
                    _flyVpFallbackStyle.fontSize = Mathf.Max(22, Mathf.RoundToInt(34f * s));
                }
            }

            EnsureEnergizeHelpWindowStyles();
            ApplyEnergizeHelpScaledStyles();
            EnsureQuickRefBodyStyle();
            ApplyQuickRefScaledStyles();
        }


        void ApplyEnergizeHelpScaledStyles()
        {
            if (_energizeHelpWindowStyle == null)
                return;
            float fs = _hudFontScale;
            float s = _mainHudUiScale;
            _energizeHelpWindowStyle.fontSize = Mathf.Max(12, Mathf.RoundToInt(14f * fs));
            int px = Mathf.RoundToInt(14f * s);
            int pyTop = Mathf.RoundToInt(24f * s);
            int pyBot = Mathf.RoundToInt(12f * s);
            _energizeHelpWindowStyle.padding = new RectOffset(px, px, pyTop, pyBot);
            _energizeHelpBodyLabelStyle.fontSize = Mathf.Max(10, Mathf.RoundToInt(12f * fs));
            _energizeHelpSectionLabelStyle.fontSize = _energizeHelpBodyLabelStyle.fontSize;
            if (_energizeHelpLayoutButtonStyle != null)
            {
                _energizeHelpLayoutButtonStyle.fontSize = Mathf.Max(15, Mathf.RoundToInt(18f * fs));
                _energizeHelpLayoutButtonStyle.fixedHeight = Mathf.Max(40f, HudS(44f));
            }
        }

        void ApplyQuickRefScaledStyles()
        {
            if (_quickRefBodyStyle == null)
                return;
            _quickRefBodyStyle.fontSize = Mathf.Max(13, Mathf.RoundToInt(15f * _hudFontScale));
        }

        /// <summary>Resources paths under <c>Assets/Resources/</c> (no extension) for full-card deploy shop art.</summary>
        static readonly Dictionary<UnitType, string> DeployShopResourcePaths = new Dictionary<UnitType, string>
        {
            { UnitType.Human, "Sprites/Units/Human Shop" },
            { UnitType.Fungoid, "Sprites/Units/Fungus Shop" },
            { UnitType.Crystalline, "Sprites/Units/Crystal Shop" },
            { UnitType.RockStrider, "Sprites/Units/Strider Shop" },
            { UnitType.LavaLeaper, "Sprites/Units/Leaper Shop" },
            { UnitType.RubiumDragon, "Sprites/Units/Dragon Shop" }
        };

        static readonly Dictionary<UnitType, Texture2D> DeployShopTextureCache = new Dictionary<UnitType, Texture2D>();
        static readonly Dictionary<UnitType, Texture2D> DeployShopGreyscaleCache = new Dictionary<UnitType, Texture2D>();
        static readonly Dictionary<int, Texture2D> GreyscaleFullTextureBySourceId = new Dictionary<int, Texture2D>();
        static readonly Dictionary<int, Texture2D> GreyscaleSpriteBySpriteId = new Dictionary<int, Texture2D>();

        static Texture2D CreateReadableTextureCopy(Texture2D src)
        {
            if (src == null || src.width <= 0 || src.height <= 0)
                return null;
            RenderTexture rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var copy = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            copy.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return copy;
        }

        static void ApplyLuminanceGreyscaleInPlace(Texture2D tex)
        {
            Color[] px = tex.GetPixels();
            for (int i = 0; i < px.Length; i++)
            {
                Color c = px[i];
                float y = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
                px[i] = new Color(y, y, y, c.a);
            }

            tex.SetPixels(px);
            tex.Apply();
        }

        static Texture2D GetOrCreateGreyscaleFullTexture(Texture2D source)
        {
            if (source == null)
                return null;
            int id = source.GetInstanceID();
            if (GreyscaleFullTextureBySourceId.TryGetValue(id, out var cached) && cached != null)
                return cached;
            Texture2D readable = CreateReadableTextureCopy(source);
            if (readable == null)
                return null;
            ApplyLuminanceGreyscaleInPlace(readable);
            GreyscaleFullTextureBySourceId[id] = readable;
            return readable;
        }

        static Texture2D GetDeployShopTextureGreyscale(UnitType type)
        {
            if (DeployShopGreyscaleCache.TryGetValue(type, out var g) && g != null)
                return g;
            Texture2D src = GetDeployShopTexture(type);
            if (src == null)
                return null;
            g = GetOrCreateGreyscaleFullTexture(src);
            if (g != null)
                DeployShopGreyscaleCache[type] = g;
            return g;
        }

        static Texture2D GetOrCreateGreyscaleSpritePixels(Sprite sp)
        {
            if (sp == null || sp.texture == null)
                return null;
            int sid = sp.GetInstanceID();
            if (GreyscaleSpriteBySpriteId.TryGetValue(sid, out var cached) && cached != null)
                return cached;
            Texture2D atlas = sp.texture;
            Rect tr = sp.textureRect;
            int x = Mathf.RoundToInt(tr.x);
            int y = Mathf.RoundToInt(tr.y);
            int w = Mathf.RoundToInt(tr.width);
            int h = Mathf.RoundToInt(tr.height);
            Texture2D readableAtlas = CreateReadableTextureCopy(atlas);
            if (readableAtlas == null)
                return null;
            Color[] px = readableAtlas.GetPixels(x, y, w, h);
            UnityEngine.Object.Destroy(readableAtlas);
            for (int i = 0; i < px.Length; i++)
            {
                Color c = px[i];
                float lum = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
                px[i] = new Color(lum, lum, lum, c.a);
            }

            var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            outTex.SetPixels(px);
            outTex.Apply();
            GreyscaleSpriteBySpriteId[sid] = outTex;
            return outTex;
        }

        static Texture2D GetDeployShopTexture(UnitType type)
        {
            if (DeployShopTextureCache.TryGetValue(type, out Texture2D cached) && cached != null)
                return cached;
            if (!DeployShopResourcePaths.TryGetValue(type, out string path))
                return null;
            Texture2D tex = Resources.Load<Texture2D>(path);
            if (tex == null)
            {
                var sp = Resources.Load<Sprite>(path);
                if (sp != null)
                    tex = sp.texture;
            }

            if (tex != null)
                DeployShopTextureCache[type] = tex;
            return tex;
        }

        /// <summary>Minimum height: large hex band + meta band (name/owner on purple strip).</summary>
        static float TileInfoFixedRowMinHeight(float contentWidth, float scale)
        {
            float S(float d) => d * scale;
            float w = contentWidth;
            float metaBandH = Mathf.Max(S(168f), S(380f) * 0.36f);
            float hexSide = Mathf.Clamp(Mathf.Min(w * 0.78f, S(420f)), S(150f), S(500f));
            float hexBandH = S(16f) + hexSide;
            return hexBandH + metaBandH;
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

        /// <summary>
        /// True when the pointer is over HUD UI that should block board tile taps/drags.
        /// Uses broad gameplay-safe regions to prevent click-through between IMGUI layers.
        /// </summary>
        public bool ScreenPointOverlapsBlockingHud(Vector2 screenPosition)
        {
            var gui = new Vector2(screenPosition.x, Screen.height - screenPosition.y);

            // Any overlay/modal captures board input.
            if (_showCenterBuyModal || _showQuickRef || _showSettingsMenu || _showMyEnergizeHelp || _showEndGameStats)
                return true;
            if (Game != null && Game.SecretMissionOverdraw != null && Game.SecretMissionOverdraw.Waiting)
                return true;
            if (_handPileViewer != HandPileViewerKind.None)
                return true;

            // Top strip and icon row (match OnGUI — same scale as <see cref="MainHudUiScale"/>).
            var hp = GameUiScale.GetPaddedModalPanelGuiRect();
            float hs = MainHudUiScale();
            float topBarY = hp.y + 6f * hs;
            float topBarH = 52f * hs;
            if (new Rect(hp.x, topBarY, hp.width, topBarH).Contains(gui))
                return true;

            // Bottom card/tile panel area (plus small pad above).
            if (_lastCardBarY > 0f && gui.y >= _lastCardBarY - 4f * hs)
                return true;

            // Dragon: human casualty pick uses full-screen modal; AI uses bottom strip; hex-target uses board taps.
            if (Game != null && Game.DragonPhase != null)
            {
                var dp = Game.DragonPhase;
                if (dp.PendingHit != null && dp.PendingEnemies != null && !Game.IsAiControlled(dp.Player))
                    return true;
                bool tallPanel = Game.IsAiControlled(dp.Player) ||
                    (dp.PendingHit != null && dp.PendingEnemies != null);
                if (tallPanel &&
                    new Rect(hp.x + 20f * hs, hp.yMax - 200f * hs, hp.width - 40f * hs, 190f * hs).Contains(gui))
                    return true;
                if (!tallPanel && dp.Options != null && dp.Options.Count > 0 &&
                    new Rect(hp.x + 12f * hs, hp.yMax - 44f * hs, hp.width - 24f * hs, 36f * hs).Contains(gui))
                    return true;
            }

            // Full-screen battle modal (dim + panel) — block board when it is shown.
            if (Game != null && Game.Players.Count > 0 &&
                ShouldPaintFullBattleOverlay(Game.CurrentPlayer, out _))
                return true;

            return false;
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
        int _lastContestedToastPlayerIndex = -1;
        int _lastContestedToastTurnNumber = -1;
        float _contestedToastUntilTime;

        /// <summary>Padded gameplay HUD rect — same reference frame as tile-info / battle IMGUI (<see cref="GetCenterBuyModalPanelGuiRect"/>).</summary>
        Rect _hudLayoutPanel;

        int _battleDiceAnimFingerprint;
        float _battleDiceAnimStartRealtime;

        /// <summary>Lazily filled from Resources (e.g. Sprites/dice/dice1 … dice6).</summary>
        NexusGuiImage[] _diceFaceArtCache;

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

        /// <summary>Card tiles in pile modal (full detail) — use <see cref="HudCardTileW"/> / <see cref="HudCardTileH"/> with <see cref="_mainHudUiScale"/>.</summary>

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
        readonly Dictionary<UnitType, NexusGuiImage> _grayUnitIconCache = new Dictionary<UnitType, NexusGuiImage>();
        readonly Dictionary<UnitType, NexusGuiImage> _battleBannerNeutralIconCache =
            new Dictionary<UnitType, NexusGuiImage>();
        readonly Dictionary<int, NexusGuiImage> _dragonIconByPlayerIndex = new Dictionary<int, NexusGuiImage>();
        readonly Dictionary<int, NexusGuiImage> _striderIconByPlayerIndex = new Dictionary<int, NexusGuiImage>();
        readonly Dictionary<int, NexusGuiImage> _fungoidIconByPlayerIndex = new Dictionary<int, NexusGuiImage>();
        readonly Dictionary<int, NexusGuiImage> _humanIconByPlayerIndex = new Dictionary<int, NexusGuiImage>();
        readonly Dictionary<int, NexusGuiImage> _lavaLeaperIconByPlayerIndex = new Dictionary<int, NexusGuiImage>();
        readonly Dictionary<int, NexusGuiImage> _crystallineIconByPlayerIndex = new Dictionary<int, NexusGuiImage>();
        readonly Dictionary<int, NexusGuiImage> _swordIconByPlayerIndex = new Dictionary<int, NexusGuiImage>();
        GUIStyle _battleWindowStyle;
        Texture2D _battleWindowBg;
        GUIStyle _energizeHelpWindowStyle;
        Texture2D _energizeHelpWindowBg;
        GUIStyle _energizeHelpBodyLabelStyle;
        GUIStyle _energizeHelpSectionLabelStyle;
        GUIStyle _energizeHelpLayoutButtonStyle;
        GUIStyle _battleRibbonLabelStyle;
        Texture2D _battleBtnPrimaryTex;
        Texture2D _battleBtnSecondaryTex;
        GUIStyle _battlePrimaryButtonStyleCached;
        GUIStyle _battleSecondaryButtonStyleCached;
        bool _battleHudStylesReady;
        Texture2D _endTurnBattleButtonTex;
        Texture2D _endTurnFireballButtonTex;
        Texture2D _endTurnNextTurnButtonTex;
        bool _endTurnAdvanceButtonTexTried;
        GUIStyle _endTurnAdvanceOverlayLabelStyle;
        float _endTurnAdvanceOverlayLabelStyleScale;
        GUIStyle _battlePanelBoxStyle;
        Texture2D _battlePanelBoxTex;
        GUIStyle _mainBoardTopIconHitStyle;
        bool _mainHudTopBarIconsTried;
        NexusGuiImage _mainHudTopBarInfoIcon;
        NexusGuiImage _mainHudTopBarSettingsIcon;
        GUIStyle _flyRubiumAmountStyle;
        GUIStyle _flyVpAmountStyle;
        GUIStyle _flyVpFallbackStyle;

        struct FlyingRubiumChip
        {
            /// <summary>World position on the mine tile — used during grow so the chip follows the board while panning.</summary>
            public Vector3 WorldStart;
            public float StartTime;
            public float GrowWorldDuration;
            public float FlyDuration;
            public float TotalDuration;
            /// <summary>Screen position where the fly phase starts (set once when grow ends).</summary>
            public Vector2 FlyStartGui;
            public bool FlyStartCaptured;
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

            var cam = Camera.main;

            if (Game.TryConsumeMiningIncomeFlights(out var list))
            {
                if (cam != null)
                {
                    float stagger = 0f;
                    const float staggerStep = 0.12f;
                    foreach (var info in list)
                    {
                        const float rubGrowWorld = 0.5f;
                        const float rubFly = 0.62f;
                        _flyingRubium.Add(new FlyingRubiumChip
                        {
                            WorldStart = info.WorldStart,
                            StartTime = Time.time + stagger,
                            GrowWorldDuration = rubGrowWorld,
                            FlyDuration = rubFly,
                            TotalDuration = rubGrowWorld + rubFly,
                            FlyStartGui = default,
                            FlyStartCaptured = false,
                            Amount = info.Amount
                        });
                        stagger += staggerStep;
                    }
                }
            }

            for (int i = _flyingRubium.Count - 1; i >= 0; i--)
            {
                if (Time.time > _flyingRubium[i].StartTime + _flyingRubium[i].TotalDuration)
                    _flyingRubium.RemoveAt(i);
            }

            if (cam != null)
            {
                for (int i = 0; i < _flyingRubium.Count; i++)
                {
                    var chip = _flyingRubium[i];
                    if (chip.FlyStartCaptured)
                        continue;
                    float elapsed = Time.time - chip.StartTime;
                    if (elapsed < chip.GrowWorldDuration)
                        continue;
                    var sp = cam.WorldToScreenPoint(chip.WorldStart);
                    if (sp.z <= 0f)
                        continue;
                    chip.FlyStartGui = new Vector2(sp.x, Screen.height - sp.y);
                    chip.FlyStartCaptured = true;
                    _flyingRubium[i] = chip;
                }
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
            var hp = GameUiScale.GetPaddedModalPanelGuiRect();
            float hs = MainHudUiScale();
            float topBarY = hp.y + 6f * hs;
            float topBarH = 52f * hs;
            float iconH = 28f * hs;
            float ly = topBarY + (topBarH - iconH) * 0.5f - 2f * hs;
            var rub = GetRubiumGui();
            float w = rub.IsEmpty ? iconH : iconH * rub.AspectRatio;
            float cx = hp.x + 12f * hs + w * 0.5f;
            float cy = ly + iconH * 0.5f;
            return new Vector2(cx, cy);
        }

        Vector2 GetVpBankIconCenterGui()
        {
            var hp = GameUiScale.GetPaddedModalPanelGuiRect();
            if (Game == null || Game.Players.Count == 0)
                return new Vector2(hp.x + hp.width * 0.5f, hp.y + 24f);

            float hs = MainHudUiScale();
            float topBarY = hp.y + 6f * hs;
            float topBarH = 52f * hs;
            float iconH = 28f * hs;
            float ly = topBarY + (topBarH - iconH) * 0.5f - 2f * hs;
            float cy = ly + iconH * 0.5f;
            var player = Game.CurrentPlayer;
            var rub = GetRubiumGui();
            var vp = GetVPGui();
            float rxRes = hp.x + 12f * hs;
            if (!rub.IsEmpty)
                rxRes += iconH * rub.AspectRatio + 6f * hs;
            int hudFontSize = Mathf.RoundToInt(Mathf.Clamp(hp.width / 32f, 15, 22) * MainHudFontScale());
            float tw = EstimateHudNumberWidth(player.Rubium, hudFontSize);
            rxRes += Mathf.Max(28f * hs, tw) + 12f * hs;
            float vpW = vp.IsEmpty ? iconH : iconH * vp.AspectRatio;
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

            var flyCam = Camera.main;
            float now = Time.time;
            Color prev = GUI.color;
            float iconBase = 28f * MainHudUiScale();
            const float peakScale = 1.48f;
            const float startScale = 0.35f;
            foreach (var f in _flyingRubium)
            {
                float elapsed = now - f.StartTime;
                if (elapsed < 0f || elapsed > f.TotalDuration)
                    continue;

                Vector2 p;
                float h;
                float alpha;
                Vector2 endGui = GetRubiumBankIconCenterGui();

                if (elapsed < f.GrowWorldDuration)
                {
                    if (flyCam == null)
                        continue;
                    var sp = flyCam.WorldToScreenPoint(f.WorldStart);
                    if (sp.z <= 0f)
                        continue;
                    p = new Vector2(sp.x, Screen.height - sp.y);
                    float gu = elapsed / f.GrowWorldDuration;
                    float scaleT = Mathf.SmoothStep(0f, 1f, gu);
                    h = iconBase * Mathf.Lerp(startScale, peakScale, scaleT);
                    alpha = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, gu * 1.5f));
                }
                else
                {
                    Vector2 flyFrom = f.FlyStartGui;
                    if (!f.FlyStartCaptured && flyCam != null)
                    {
                        var sp = flyCam.WorldToScreenPoint(f.WorldStart);
                        flyFrom = sp.z > 0f
                            ? new Vector2(sp.x, Screen.height - sp.y)
                            : new Vector2(Screen.width * 0.5f, Screen.height * 0.42f);
                    }
                    else if (!f.FlyStartCaptured)
                        flyFrom = new Vector2(Screen.width * 0.5f, Screen.height * 0.42f);

                    float flyElapsed = elapsed - f.GrowWorldDuration;
                    float fu = Mathf.Clamp01(f.FlyDuration > 0.001f ? flyElapsed / f.FlyDuration : 1f);
                    float t = fu * fu * (3f - 2f * fu);
                    p = Vector2.Lerp(flyFrom, endGui, t);
                    h = Mathf.Lerp(iconBase * peakScale, iconBase, Mathf.SmoothStep(0f, 1f, fu));
                    alpha = 1f;
                }

                float w = h * rub.AspectRatio;
                var r = new Rect(p.x - w * 0.5f, p.y - h * 0.5f, w, h);
                GUI.color = new Color(1f, 1f, 1f, alpha);
                rub.Draw(r);
                if (f.Amount > 1)
                    GUI.Label(new Rect(r.xMax + 2f, r.y, 36f * MainHudUiScale(), h), "+" + f.Amount,
                        _flyRubiumAmountStyle);
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

                float iconBase = 28f * MainHudUiScale();
                if (elapsed < f.PopDuration)
                {
                    p = f.CenterGui;
                    float pu = elapsed / f.PopDuration;
                    iconH = iconBase * Mathf.SmoothStep(0.4f, 1.08f, pu);
                    alpha = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, pu * 1.4f));
                }
                else
                {
                    float fu = (elapsed - f.PopDuration) / f.FlyDuration;
                    float t = fu * fu * (3f - 2f * fu);
                    p = Vector2.Lerp(f.CenterGui, f.EndGui, t);
                    iconH = Mathf.Lerp(iconBase * 1.08f, iconBase, Mathf.SmoothStep(0f, 1f, fu));
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
            _battleWindowBg.SetPixel(0, 0, new Color(0.03f, 0.04f, 0.07f, 0.97f));
            _battleWindowBg.Apply();
            _battleWindowStyle.normal.background = _battleWindowBg;
            _battleWindowStyle.onNormal.background = _battleWindowBg;
            _battleWindowStyle.focused.background = _battleWindowBg;
            _battleWindowStyle.onFocused.background = _battleWindowBg;
            _battleWindowStyle.active.background = _battleWindowBg;
            _battleWindowStyle.onActive.background = _battleWindowBg;
            _battleWindowStyle.fontSize = 14;
            _battleWindowStyle.fontStyle = FontStyle.Bold;
            _battleWindowStyle.alignment = TextAnchor.UpperLeft;
            // Title drawn manually (sci-fi header); keep padding tight.
            _battleWindowStyle.padding = new RectOffset(16, 16, 12, 12);
            _battleWindowStyle.normal.textColor = new Color(0.95f, 0.97f, 1f, 0.95f);
        }

        void EnsureEnergizeHelpWindowStyles()
        {
            if (_energizeHelpWindowStyle != null)
                return;
            _energizeHelpWindowStyle = new GUIStyle(GUI.skin.window);
            _energizeHelpWindowBg = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _energizeHelpWindowBg.SetPixel(0, 0, new Color(0.06f, 0.07f, 0.11f, 1f));
            _energizeHelpWindowBg.Apply();
            _energizeHelpWindowStyle.normal.background = _energizeHelpWindowBg;
            _energizeHelpWindowStyle.onNormal.background = _energizeHelpWindowBg;
            _energizeHelpWindowStyle.focused.background = _energizeHelpWindowBg;
            _energizeHelpWindowStyle.onFocused.background = _energizeHelpWindowBg;
            _energizeHelpWindowStyle.active.background = _energizeHelpWindowBg;
            _energizeHelpWindowStyle.onActive.background = _energizeHelpWindowBg;
            _energizeHelpWindowStyle.fontSize = 14;
            _energizeHelpWindowStyle.fontStyle = FontStyle.Bold;
            _energizeHelpWindowStyle.alignment = TextAnchor.UpperLeft;
            _energizeHelpWindowStyle.padding = new RectOffset(14, 14, 24, 12);
            _energizeHelpWindowStyle.normal.textColor = new Color(0.96f, 0.97f, 1f, 1f);

            _energizeHelpBodyLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                normal = { textColor = new Color(0.9f, 0.92f, 0.96f, 1f) }
            };
            _energizeHelpSectionLabelStyle = new GUIStyle(_energizeHelpBodyLabelStyle)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.96f, 0.88f, 1f) }
            };
            _energizeHelpLayoutButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }

        GUIStyle BattlePanelBoxStyle()
        {
            if (_battlePanelBoxStyle == null)
            {
                if (_battlePanelBoxTex == null)
                {
                    _battlePanelBoxTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    _battlePanelBoxTex.SetPixel(0, 0, new Color(0.03f, 0.04f, 0.08f, 0.4f));
                    _battlePanelBoxTex.Apply();
                }

                _battlePanelBoxStyle = new GUIStyle(GUI.skin.box);
                _battlePanelBoxStyle.normal.background = _battlePanelBoxTex;
                _battlePanelBoxStyle.onNormal.background = _battlePanelBoxTex;
                _battlePanelBoxStyle.hover.background = _battlePanelBoxTex;
                _battlePanelBoxStyle.active.background = _battlePanelBoxTex;
                _battlePanelBoxStyle.margin = new RectOffset(0, 0, 0, 0);
            }

            float s = _battleHudUiScale > 0.01f ? _battleHudUiScale : 1f;
            int bpX = Mathf.Max(4, Mathf.RoundToInt(6f * Mathf.Max(0.55f, s)));
            int bpY = Mathf.Max(6, Mathf.RoundToInt(9f * Mathf.Max(0.55f, s)));
            _battlePanelBoxStyle.padding = new RectOffset(bpX, bpX, bpY, bpY);
            return _battlePanelBoxStyle;
        }

        void EnsureBattleHudStyles()
        {
            if (_battleHudStylesReady)
                return;
            _battleHudStylesReady = true;

            _battleRibbonLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.96f, 0.97f, 1f, 1f) }
            };
            ApplyTileInfoFont(_battleRibbonLabelStyle);

            _battleBtnPrimaryTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _battleBtnPrimaryTex.SetPixel(0, 0, new Color(0.88f, 0.26f, 0.12f, 1f));
            _battleBtnPrimaryTex.Apply();
            _battleBtnSecondaryTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _battleBtnSecondaryTex.SetPixel(0, 0, new Color(0.14f, 0.14f, 0.18f, 1f));
            _battleBtnSecondaryTex.Apply();

            _battlePrimaryButtonStyleCached = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                fixedHeight = 42f,
                normal = { textColor = Color.white, background = _battleBtnPrimaryTex },
                hover = { textColor = Color.white, background = _battleBtnPrimaryTex },
                active = { textColor = Color.white, background = _battleBtnPrimaryTex }
            };
            _battlePrimaryButtonStyleCached.padding = new RectOffset(12, 12, 10, 10);
            ApplyTileInfoFont(_battlePrimaryButtonStyleCached);

            _battleSecondaryButtonStyleCached = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                fixedHeight = 40f,
                normal = { textColor = new Color(0.92f, 0.94f, 0.98f, 1f), background = _battleBtnSecondaryTex },
                hover = { textColor = Color.white, background = _battleBtnSecondaryTex },
                active = { textColor = Color.white, background = _battleBtnSecondaryTex }
            };
            _battleSecondaryButtonStyleCached.padding = new RectOffset(12, 12, 10, 10);
            ApplyTileInfoFont(_battleSecondaryButtonStyleCached);
        }

        /// <summary>Single-line battle HUD phase title (center ribbon).</summary>
        static string BattlePhaseStepTitle(GameController game)
        {
            if (game.PendingBattleArrangement)
                return "ARRANGEMENT STEP";
            if (game.FocusFirePicker != null)
                return "FOCUS FIRE STEP";
            if (game.EnergizePromptPlayer != null)
                return "ENERGIZE STEP";
            if (game.CasualtyPick != null)
                return "CASUALTY STEP";
            if (game.SecretMissionOffer != null && game.SecretMissionOffer.Waiting)
                return "SECRET STEP";
            if (game.HasActiveBattleStep)
                return "BATTLE STEP";
            return "BATTLE STEP";
        }

        void DrawBattlePhaseRibbon()
        {
            EnsureBattleHudStyles();
            float ribbonH = BattleS(40f);
            GUILayout.BeginHorizontal();
            var slot = GUILayoutUtility.GetRect(1f, ribbonH, GUILayout.ExpandWidth(true), GUILayout.Height(ribbonH));
            GUILayout.EndHorizontal();

            // Text only — no panel tint (frame art provides the bar).
            GUI.Label(slot, BattlePhaseStepTitle(Game), _battleRibbonLabelStyle);
        }

        void OnGUI()
        {
            if (Game == null || Game.Players.Count == 0)
                return;
            if (Game.IsGameOver && Game.FinalSnapshot != null)
            {
                _hudLayoutPanel = GetCenterBuyModalPanelGuiRect();
                _mainHudUiScale = MainHudUiScale();
                _hudFontScale = MainHudFontScale();
                ApplyMainHudScaledStyles();
                DrawEndGameOverlay(Game.FinalSnapshot);
                return;
            }

            var player = Game.CurrentPlayer;

            _hudLayoutPanel = GetCenterBuyModalPanelGuiRect();
            _mainHudUiScale = MainHudUiScale();
            _hudFontScale = MainHudFontScale();
            _hudCardBarHeight = HudS(152f);
            _hudPhaseRibbonHeight = HudS(36f);
            ApplyMainHudScaledStyles();
            MaybeQueueContestedRetreatToast(player);

            DrawDragonPhaseOverlay();

            var hp = _hudLayoutPanel;
            float topBarY = hp.y + HudS(6f);
            float topBarH = HudS(52f);
            float mainHudIconH = HudS(28f);

            var rubGui = GetRubiumGui();
            var vpGui = GetVPGui();
            var hudLabel = GUI.skin.label;

            float iconBtn = HudS(33f);
            float iconY = topBarY + (topBarH - iconBtn) * 0.5f;
            float iconRight = hp.xMax - HudS(12f) - iconBtn * 2f - HudS(10f);

            // Top strip: banner tinted by current player's color; rubium + VP + turn + player centered (phase is on the bottom ribbon).
            var baseBar = new Color(0.06f, 0.07f, 0.12f, 0.88f);
            Color pc = player.Color;
            var topBarTint = Color.Lerp(baseBar, new Color(pc.r, pc.g, pc.b, 1f), 0.48f);
            topBarTint.a = 0.9f;
            Color prevGui = GUI.color;
            GUI.color = topBarTint;
            GUI.DrawTexture(new Rect(hp.x, topBarY, hp.width, topBarH), Texture2D.whiteTexture);
            GUI.color = prevGui;
            DrawContestedRetreatToast(hp, topBarY + topBarH + HudS(6f));

            float ly = topBarY + (topBarH - mainHudIconH) * 0.5f - HudS(2f);
            float resLineH = Mathf.Max(HudS(22f), mainHudIconH + HudS(2f));
            float rowLeft = hp.x + HudS(12f);
            float rowRight = iconRight - HudS(8f);
            var rubNumStyle = new GUIStyle(hudLabel)
            {
                fontSize = Mathf.Max(11, Mathf.RoundToInt(Mathf.Clamp(hp.width / 38f, 11, 15) * _hudFontScale)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            ApplyTileInfoFont(rubNumStyle);
            var lightOnBar = new Color(0.96f, 0.97f, 1f, 1f);
            rubNumStyle.normal.textColor = lightOnBar;
            string midText = $"T{Game.TurnNumber}  ·  P{player.PlayerIndex + 1}";
            var centerMetaStyle = new GUIStyle(rubNumStyle) { alignment = TextAnchor.MiddleLeft };
            ApplyTileInfoFont(centerMetaStyle);
            float rubIconW = rubGui.IsEmpty ? 0f : mainHudIconH * rubGui.AspectRatio + HudS(6f);
            float rubNumW =
                Mathf.Max(HudS(28f), rubNumStyle.CalcSize(new GUIContent(player.Rubium.ToString())).x);
            float vpIconW = vpGui.IsEmpty ? 0f : mainHudIconH * vpGui.AspectRatio + HudS(6f);
            float vpNumW =
                Mathf.Max(HudS(28f), rubNumStyle.CalcSize(new GUIContent(player.VictoryPoints.ToString())).x);
            float gapRes = HudS(12f);
            float gapMid = HudS(14f);
            float totalRowW = rubIconW + rubNumW + gapRes + vpIconW + vpNumW + gapMid +
                centerMetaStyle.CalcSize(new GUIContent(midText)).x;
            float rxRes = rowLeft + Mathf.Max(0f, (rowRight - rowLeft - totalRowW) * 0.5f);
            if (!rubGui.IsEmpty)
                rxRes += rubGui.Draw(rxRes, ly, mainHudIconH) + HudS(6f);
            GUI.Label(new Rect(rxRes, ly - HudS(2f), HudS(120f), resLineH), player.Rubium.ToString(), rubNumStyle);
            rxRes += rubNumW + gapRes;
            if (!vpGui.IsEmpty)
                rxRes += vpGui.Draw(rxRes, ly, mainHudIconH) + HudS(6f);
            GUI.Label(new Rect(rxRes, ly - HudS(2f), HudS(80f), resLineH), player.VictoryPoints.ToString(), rubNumStyle);
            rxRes += vpNumW + gapMid;
            GUI.Label(new Rect(rxRes, ly - HudS(2f), rowRight - rxRes, resLineH), midText, centerMetaStyle);
            bool blockTopIcons = _showCenterBuyModal;
            bool prevEnabled = GUI.enabled;
            if (blockTopIcons)
                GUI.enabled = false;

            var infoIconRect = new Rect(iconRight, iconY, iconBtn, iconBtn);
            var settingsIconRect = new Rect(iconRight + iconBtn + HudS(10f), iconY, iconBtn, iconBtn);
            if (!_mainHudTopBarIconsTried)
            {
                _mainHudTopBarIconsTried = true;
                _mainHudTopBarInfoIcon = NexusGuiArt.LoadMainHudInfoIcon();
                _mainHudTopBarSettingsIcon = NexusGuiArt.LoadMainHudSettingsIcon();
            }

            if (Event.current.type == EventType.Repaint)
            {
                if (!_mainHudTopBarInfoIcon.IsEmpty)
                    _mainHudTopBarInfoIcon.DrawAspectFit(infoIconRect);
                else
                {
                    var fb = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = Mathf.Max(18, Mathf.RoundToInt(22f * _hudFontScale)),
                        alignment = TextAnchor.MiddleCenter
                    };
                    ApplyTileInfoFont(fb);
                    GUI.Label(infoIconRect, "\u2139", fb);
                }

                if (!_mainHudTopBarSettingsIcon.IsEmpty)
                    _mainHudTopBarSettingsIcon.DrawAspectFit(settingsIconRect);
                else
                {
                    var fb = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = Mathf.Max(18, Mathf.RoundToInt(22f * _hudFontScale)),
                        alignment = TextAnchor.MiddleCenter
                    };
                    ApplyTileInfoFont(fb);
                    GUI.Label(settingsIconRect, "\u2699", fb);
                }
            }

            if (GUI.Button(infoIconRect, GUIContent.none, MainBoardTopIconHitStyle()))
            {
                _showSettingsMenu = false;
                _showQuickRef = true;
            }

            if (GUI.Button(settingsIconRect, GUIContent.none, MainBoardTopIconHitStyle()))
            {
                _showQuickRef = false;
                _showSettingsMenu = true;
            }
            GUI.enabled = prevEnabled;

            // Turn / hand counts / deploy discount live in the bottom card strip — no duplicate block under the top bar.
            float metaW = Mathf.Min(HudS(400f), hp.width - HudS(36f));
            float lx = hp.x + HudS(18f);
            ly = topBarY + topBarH + HudS(6f);
            float lw = metaW - HudS(16f);

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
                var bodyStyle = new GUIStyle(hudLabel)
                {
                    wordWrap = true,
                    fontSize = Mathf.Max(10, Mathf.RoundToInt(11f * _hudFontScale))
                };
                float bodyH = bodyStyle.CalcHeight(new GUIContent(sb.ToString()), lw);
                bodyH = Mathf.Clamp(bodyH + HudS(6f), HudS(24f), HudS(220f));
                GUI.Label(new Rect(lx, ly, lw, bodyH), sb.ToString(), bodyStyle);
                ly += bodyH + HudS(6f);
            }

            string battleLog =
                !string.IsNullOrEmpty(Game.LiveBattlePhaseLog) ? Game.LiveBattlePhaseLog : Game.LastBattlePhaseLog;

            float hudBottom = ly + HudS(4f);
            float battleLogPanelH = HudS(140f);
            if (!string.IsNullOrEmpty(battleLog) && !Game.PendingBattleArrangement && !_showCenterBuyModal)
            {
                var battleRect = new Rect(hp.x + HudS(10f), hudBottom + HudS(8f),
                    Mathf.Min(HudS(420f), hp.width - HudS(20f)), battleLogPanelH);
                GUI.Box(battleRect, "Battle log");
                string safe = UiSafeText(battleLog);
                float viewX = battleRect.x + HudS(6f);
                float viewY = battleRect.y + HudS(20f);
                float viewW = battleRect.width - HudS(12f);
                float viewH = battleRect.height - HudS(26f);
                var battleLogStyle = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true,
                    fontSize = Mathf.Max(9, Mathf.RoundToInt(10f * _hudFontScale))
                };
                float contentH = Mathf.Max(viewH, battleLogStyle.CalcHeight(new GUIContent(safe), viewW - HudS(16f)) + HudS(8f));
                var view = new Rect(viewX, viewY, viewW, viewH);
                var content = new Rect(0f, 0f, viewW - HudS(16f), contentH);
                _scrollBattleLogPanel = GUI.BeginScrollView(view, _scrollBattleLogPanel, content);
                GUI.Label(new Rect(0f, 0f, content.width, content.height), safe, battleLogStyle);
                GUI.EndScrollView();
            }

            if (ShowDebugToggle && InputController != null)
            {
                float dbgY = Mathf.Min(hudBottom + HudS(6f), hp.yMax - HudS(120f));
                bool newDebug = GUI.Toggle(new Rect(hp.x + HudS(10f), dbgY, HudS(180f), HudS(22f)), InputController.DebugClicks,
                    "Debug clicks");
                InputController.DebugClicks = newDebug;
            }

            float topY = string.IsNullOrEmpty(battleLog) || Game.PendingBattleArrangement
                ? hudBottom + HudS(8f)
                : hudBottom + HudS(8f) + battleLogPanelH + HudS(10f);
            if (Game.DragonPhase != null)
            {
                var dp = Game.DragonPhase;
                // Bottom strip only for AI; human casualty uses full-screen modal (<see cref="DrawCasualtySelectionModalDragon"/>).
                bool tallDragonPanel = Game.IsAiControlled(dp.Player);
                if (tallDragonPanel)
                    topY = Mathf.Max(topY, hp.yMax - HudS(220f));
            }

            float dragonReserveBottom = 0f;
            if (Game.DragonPhase != null)
            {
                var dp = Game.DragonPhase;
                bool tallDragonPanel = Game.IsAiControlled(dp.Player);
                dragonReserveBottom = tallDragonPanel ? HudS(200f) : HudS(40f);
            }

            float reserveBottom = _hudCardBarHeight + _hudPhaseRibbonHeight + HudS(24f) + dragonReserveBottom;
            topY = Mathf.Min(topY, Mathf.Max(hp.y + HudS(60f), hp.yMax - reserveBottom));

            var dragonPhase = Game.DragonPhase;
            bool dragonSkipButton = dragonPhase != null && !Game.IsAiControlled(dragonPhase.Player) &&
                dragonPhase.PendingHit == null;
            bool blockEndTurn = Game.BattlePhaseBlockingPlay || Game.IsAiControlled(player);
            if (dragonPhase != null && !dragonSkipButton)
                blockEndTurn = true;

            GUI.enabled = !blockEndTurn;
            string endTurnLabel = dragonSkipButton ? "SKIP DRAGON'S BREATH" : EndTurnAdvanceLabel(player);
            EnsureEndTurnAdvanceButtonTextures();
            var endTurnVisual = GetEndTurnButtonVisualKind(player, dragonSkipButton);
            Texture2D endTurnBg = GetEndTurnAdvanceButtonTexture(endTurnVisual);

            float btnH = HudS(112f);
            float btnW;
            if (endTurnBg != null)
            {
                // Square hit area and framing; art scales inside via ScaleToFit.
                btnW = btnH;
            }
            else if (dragonSkipButton)
                btnW = HudS(560f);
            else
                btnW = HudS(endTurnLabel.Length >= 11 ? 440f : 340f);

            float endTurnX = hp.xMax - btnW - HudS(10f);
            float endTurnY = hp.yMax - reserveBottom - btnH;
            var endTurnRect = new Rect(endTurnX, endTurnY, btnW, btnH);

            Color endTurnGuiPrev = GUI.color;
            bool breatheIdleEndTurn =
                !dragonSkipButton &&
                !blockEndTurn &&
                !Game.HasOptionalPreEndTurnActions(player);
            if (breatheIdleEndTurn)
            {
                float a = 0.8f + 0.2f * Mathf.Sin(Time.realtimeSinceStartup * 5.8f);
                GUI.color = new Color(endTurnGuiPrev.r, endTurnGuiPrev.g, endTurnGuiPrev.b, endTurnGuiPrev.a * a);
            }

            if (endTurnBg != null)
            {
                GUI.DrawTexture(endTurnRect, endTurnBg, ScaleMode.ScaleToFit, true);
                var overlayStyle = EndTurnAdvanceOverlayLabelStyle();
                GuiLabelWithOutline(endTurnRect, endTurnLabel, overlayStyle);
                if (GUI.Button(endTurnRect, GUIContent.none, GUIStyle.none))
                {
                    if (dragonSkipButton)
                        Game.SkipAllDragonStrikes();
                    else
                    {
                        Game.EndTurn();
                        _showCenterBuyModal = false;
                    }
                }
            }
            else if (GUI.Button(endTurnRect, endTurnLabel))
            {
                if (dragonSkipButton)
                    Game.SkipAllDragonStrikes();
                else
                {
                    Game.EndTurn();
                    _showCenterBuyModal = false;
                }
            }

            GUI.color = endTurnGuiPrev;
            GUI.enabled = true;

            bool canOpenHexDetailModal = InputController != null && InputController.SelectedTile != null;
            if (Game.BattlePhaseBlockingPlay || Game.DragonPhase != null || Game.IsAiControlled(player))
                canOpenHexDetailModal = false;

            if (_showCenterBuyModal && !canOpenHexDetailModal)
                _showCenterBuyModal = false;

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

            // Battle modal above main HUD; clash intro flashes on top of it for a short beat.
            DrawFullBattleOverlays(player);
            if (Game.BattleClashIntroActive)
                DrawBattleClashIntroOverlay();

            // Dragon's Breath casualty pick: full tile-style modal on top of the HUD.
            var dpEnd = Game.DragonPhase;
            if (dpEnd != null && dpEnd.PendingHit != null && dpEnd.PendingEnemies != null &&
                !Game.IsAiControlled(dpEnd.Player))
                DrawCasualtySelectionModalDragon(dpEnd);
        }

        void DrawBattleClashIntroOverlay()
        {
            var dim = new Color(0f, 0f, 0f, 0.65f);
            Color prev = GUI.color;
            GUI.color = dim;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = prev;

            float s = _hudFontScale;
            var pulse = 0.85f + 0.15f * Mathf.Sin(Time.realtimeSinceStartup * 8f);
            int titleFs = Mathf.Clamp(Mathf.Max(28, Mathf.RoundToInt(40f * s)), 28, 56);
            var title = new GUIStyle(GUI.skin.label)
            {
                fontSize = titleFs,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.92f, 0.35f, pulse) }
            };
            GUI.Label(new Rect(0f, Screen.height * 0.38f, Screen.width, HudS(120f)), "⚔  ⚔  ⚔", title);
            var sub = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(14, Mathf.RoundToInt(16f * s)),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.9f, 0.9f, 0.95f, 0.85f) }
            };
            GUI.Label(new Rect(0f, Screen.height * 0.38f + HudS(72f), Screen.width, HudS(36f)),
                "(Sword clash animation — art TBD)", sub);
        }

        /// <summary>Transparent hit target over top-bar sprite buttons (no box chrome).</summary>
        GUIStyle MainBoardTopIconHitStyle()
        {
            if (_mainBoardTopIconHitStyle != null)
                return _mainBoardTopIconHitStyle;
            _mainBoardTopIconHitStyle = new GUIStyle(GUI.skin.label)
            {
                normal = { background = null },
                hover = { background = null },
                active = { background = null },
                focused = { background = null },
                onNormal = { background = null },
                onHover = { background = null },
                onActive = { background = null },
                border = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0),
                overflow = new RectOffset(0, 0, 0, 0)
            };
            return _mainBoardTopIconHitStyle;
        }

        void DrawSettingsOverlay()
        {
            var dim = new Color(0.02f, 0.02f, 0.06f, 0.78f);
            var prev = GUI.color;
            GUI.color = dim;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = prev;

            float s = _hudFontScale;
            float w = Mathf.Min(HudS(380f), Screen.width - HudS(32f));
            float h = HudS(280f);
            var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.Box(panel, "");

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(14, Mathf.RoundToInt(18f * s)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(new Rect(panel.x, panel.y + HudS(12f), panel.width, HudS(28f)), "Settings", titleStyle);

            var btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.Max(13, Mathf.RoundToInt(16f * s)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            float by = panel.y + HudS(56f);
            float bw = panel.width - HudS(32f);
            float bx = panel.x + HudS(16f);
            float bh = HudS(48f);

            if (GUI.Button(new Rect(bx, by, bw, bh), "LEAVE GAME", btnStyle))
            {
                _showSettingsMenu = false;
                var bootstrap = FindObjectOfType<Bootstrap>();
                if (bootstrap != null)
                    bootstrap.ReturnToMainMenu();
                else
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }

            by += bh + HudS(14f);
            var closeStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.Max(13, Mathf.RoundToInt(15f * s))
            };
            if (GUI.Button(new Rect(bx, by, bw, HudS(42f)), "Close", closeStyle))
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

            float s = _hudFontScale;
            float pad = HudS(16f);
            var panel = new Rect(pad, pad, Screen.width - 2f * pad, Screen.height - 2f * pad);
            GUI.Box(panel, "");

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(13, Mathf.RoundToInt(16f * s)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            string header = _quickRefTab == 0 ? NexusRulebook.Title : NexusUnitQuickReference.Title;
            GUI.Label(new Rect(panel.x, panel.y + HudS(6f), panel.width, HudS(26f)), header, titleStyle);

            var tabBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.Max(15, Mathf.RoundToInt(17f * s)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            var closeBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.Max(14, Mathf.RoundToInt(16f * s)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            float tabH = HudS(44f);
            float tabY = panel.y + HudS(34f);
            float tabW = HudS(120f);
            if (GUI.Button(new Rect(panel.x + HudS(12f), tabY, tabW, tabH), "Rules", tabBtnStyle))
            {
                if (_quickRefTab != 0)
                    _scrollQuickRef = Vector2.zero;
                _quickRefTab = 0;
            }

            if (GUI.Button(new Rect(panel.x + HudS(12f) + tabW + HudS(8f), tabY, tabW, tabH), "Units", tabBtnStyle))
            {
                if (_quickRefTab != 1)
                    _scrollQuickRef = Vector2.zero;
                _quickRefTab = 1;
            }

            var cfg = Game != null ? Game.Config : null;
            string body = _quickRefTab == 0
                ? NexusRulebook.Body
                : NexusUnitQuickReference.Build(cfg);

            float closeH = HudS(36f);
            float topBlock = HudS(34f) + tabH + HudS(6f);
            var scrollRect = new Rect(panel.x + HudS(12f), panel.y + topBlock, panel.width - HudS(24f),
                panel.height - topBlock - closeH - HudS(14f));
            float innerW = scrollRect.width - HudS(22f);
            float contentH = _quickRefBodyStyle.CalcHeight(new GUIContent(body), innerW);
            contentH = Mathf.Max(contentH + HudS(32f), scrollRect.height * 0.45f);

            _scrollQuickRef = GUI.BeginScrollView(scrollRect, _scrollQuickRef, new Rect(0f, 0f, innerW, contentH));
            GUI.Label(new Rect(HudS(8f), HudS(8f), innerW - HudS(16f), contentH), body, _quickRefBodyStyle);
            GUI.EndScrollView();

            if (GUI.Button(new Rect(panel.xMax - HudS(188f), panel.yMax - closeH - HudS(10f), HudS(168f), closeH),
                    "Close", closeBtnStyle))
                _showQuickRef = false;
        }

        void DrawEnergizeHelpWindow()
        {
            if (!_showMyEnergizeHelp)
                return;

            EnsureEnergizeHelpWindowStyles();
            var dim = new Color(0.02f, 0.02f, 0.06f, 0.82f);
            var prevCol = GUI.color;
            GUI.color = dim;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = prevCol;

            var hp = _hudLayoutPanel;
            float winH = Mathf.Min(HudS(480f), hp.height - HudS(100f));
            var r = new Rect(hp.x + HudS(12f), hp.y + HudS(80f), Mathf.Min(HudS(580f), hp.width - HudS(24f)), winH);
            GUI.Window(953, r, _ =>
            {
                var subject = _energizeHelpSubject != null ? _energizeHelpSubject : Game.CurrentPlayer;
                if (subject == null)
                {
                    if (GUILayout.Button("Close", _energizeHelpLayoutButtonStyle))
                        _showMyEnergizeHelp = false;
                    return;
                }

                GUILayout.Label(
                    $"P{subject.PlayerIndex + 1}{(Game.IsAiControlled(subject) ? " (AI)" : "")} - Energize in hand",
                    _energizeHelpSectionLabelStyle);

                float scrollH = Mathf.Max(HudS(120f), winH - HudS(110f));
                _scrollMyEnergizeHelp = GUILayout.BeginScrollView(_scrollMyEnergizeHelp, GUILayout.Height(scrollH));

                bool hasBattle = subject.BattleEnergize != null && subject.BattleEnergize.Count > 0;
                bool hasDeploy = subject.DeployEnergize != null && subject.DeployEnergize.Count > 0;
                if (!hasBattle && !hasDeploy)
                    GUILayout.Label("No Energize cards in hand.", _energizeHelpBodyLabelStyle);

                if (hasBattle)
                {
                    GUILayout.Label("Battle (pre-dice step)", _energizeHelpSectionLabelStyle);
                    foreach (var g in subject.BattleEnergize.GroupBy(x => x).OrderBy(x => x.Key.ToString()))
                    {
                        GUILayout.Label($"- {EnergizeBattleCatalog.GetName(g.Key)}  x{g.Count()}", _energizeHelpBodyLabelStyle);
                        GUILayout.Label(EnergizeBattleCatalog.GetDescription(g.Key), _energizeHelpBodyLabelStyle);
                        GUILayout.Space(HudS(6f));
                    }
                }

                if (hasDeploy)
                {
                    GUILayout.Label("Deployment (buy phase)", _energizeHelpSectionLabelStyle);
                    foreach (var g in subject.DeployEnergize.GroupBy(x => x).OrderBy(x => x.Key.ToString()))
                    {
                        GUILayout.Label($"- {EnergizeDeploymentCatalog.GetName(g.Key)}  x{g.Count()}",
                            _energizeHelpBodyLabelStyle);
                        GUILayout.Label(EnergizeDeploymentCatalog.GetDescription(g.Key), _energizeHelpBodyLabelStyle);
                        GUILayout.Space(HudS(6f));
                    }
                }

                GUILayout.EndScrollView();
                if (GUILayout.Button("Close", _energizeHelpLayoutButtonStyle))
                    _showMyEnergizeHelp = false;
            }, "What do my Energize cards do?", _energizeHelpWindowStyle);
        }
        void DrawBottomCardHand(PlayerState player)
        {
            EnsureCardStyles();

            var hp = _hudLayoutPanel;
            float dragonLift = 0f;
            if (Game.DragonPhase != null)
            {
                var dp = Game.DragonPhase;
                bool tallDragon = Game.IsAiControlled(dp.Player);
                dragonLift = tallDragon ? HudS(200f) : HudS(40f);
            }

            float barY = hp.yMax - dragonLift - _hudCardBarHeight - _hudPhaseRibbonHeight - HudS(12f);
            barY = Mathf.Max(hp.y + HudS(40f), barY);
            _lastCardBarY = barY;

            float barX = hp.x + HudS(8f);
            float barW = hp.width - HudS(16f);
            GUI.Box(new Rect(barX, barY, barW, _hudCardBarHeight), "");

            float pad = HudS(8f);
            float headerH = HudS(22f);
            string deckLine =
                $"P{player.PlayerIndex + 1}  ·  Secret deck {Game.SecretDeckCount}  ·  Energize {Game.EnergizeDeckCount}";
            var deckStyle = new GUIStyle(_cardColumnLabelStyle)
            {
                fontSize = Mathf.Max(12, Mathf.RoundToInt(13f * _hudFontScale))
            };
            GUI.Label(new Rect(barX + pad, barY + HudS(2f), barW - pad * 2f, HudS(16f)), deckLine, deckStyle);

            float innerX = barX + pad;
            float innerW = barW - pad * 2f;
            float contentY = barY + headerH;
            float contentH = _hudCardBarHeight - headerH - HudS(4f);

            float splitGap = HudS(8f);
            float minTilePanelW = HudS(120f);
            float stackBtnW = HudS(52f);
            float stackBtnH = HudS(32f);
            float stackGap = HudS(3f);
            float cardsHdrH = HudS(12f);

            int bCount = player.BattleEnergize?.Count ?? 0;
            int dCount = player.DeployEnergize?.Count ?? 0;
            int sCount = player.SecretMissions?.Count ?? 0;

            float stackColW = stackBtnW;
            float maxLeft = Mathf.Max(HudS(80f), innerW - minTilePanelW - splitGap);
            float leftW = Mathf.Min(stackColW, maxLeft);

            float rightW = innerW - leftW - splitGap;
            float rightX = innerX + leftW + splitGap;

            var pileBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.Max(10, Mathf.RoundToInt(11f * _hudFontScale)),
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                alignment = TextAnchor.MiddleCenter
            };

            var cardsHeadingStyle = new GUIStyle(_cardColumnLabelStyle)
            {
                fontSize = Mathf.Max(8, Mathf.RoundToInt(9f * _hudFontScale)),
                alignment = TextAnchor.UpperLeft,
                wordWrap = false
            };

            float stackBlockH = cardsHdrH + HudS(4f) + stackBtnH * 3f + stackGap * 2f;
            float stackTop = contentY + Mathf.Max(0f, (contentH - stackBlockH) * 0.5f);
            GUI.Label(new Rect(innerX, stackTop, stackBtnW, cardsHdrH + HudS(2f)), "CARDS", cardsHeadingStyle);

            float bx = innerX;
            float by = stackTop + cardsHdrH + HudS(4f);
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

            if (GUI.Button(rSecret, $"S {sCount}", pileBtnStyle))
            {
                _handPileViewer = _handPileViewer == HandPileViewerKind.Secret
                    ? HandPileViewerKind.None
                    : HandPileViewerKind.Secret;
            }

            if (_handPileViewer != HandPileViewerKind.None)
            {
                if (_handPileViewer == HandPileViewerKind.Battle)
                    DrawOutlineRect(rBattle, new Color(0.95f, 0.78f, 0.2f, 0.95f), HudS(2f));
                if (_handPileViewer == HandPileViewerKind.Deploy)
                    DrawOutlineRect(rDeploy, new Color(0.95f, 0.78f, 0.2f, 0.95f), HudS(2f));
                if (_handPileViewer == HandPileViewerKind.Secret)
                    DrawOutlineRect(rSecret, new Color(0.95f, 0.78f, 0.2f, 0.95f), HudS(2f));
            }

            DrawBottomTilePanel(rightX, contentY, rightW, contentH, player);
        }

        void DrawBottomTilePanel(float x, float y, float w, float h, PlayerState player)
        {
            var panel = new Rect(x, y, w, h);
            GUI.Box(panel, "");

            var popupTile = InputController != null ? InputController.SelectedTile : null;

            float tileScrollContentH = HudS(260f);
            float inset = HudS(4f);
            var scrollView = new Rect(panel.x + inset, panel.y + inset, panel.width - inset * 2f,
                panel.height - inset * 2f);
            float innerW = Mathf.Max(HudS(80f), scrollView.width - HudS(18f));

            if (popupTile == null)
            {
                var hint = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true,
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Max(10, Mathf.RoundToInt(11f * _hudFontScale)),
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
            bool forcingSecretOverdraw = Game != null &&
                                         Game.SecretMissionOverdraw != null &&
                                         Game.SecretMissionOverdraw.Waiting &&
                                         Game.SecretMissionOverdraw.Player == player;
            if (forcingSecretOverdraw)
                _handPileViewer = HandPileViewerKind.Secret;

            if (_handPileViewer == HandPileViewerKind.None || player == null)
                return;

            var dim = new Color(0.02f, 0.02f, 0.06f, 0.55f);
            Color prev = GUI.color;
            GUI.color = dim;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = prev;

            float w = Mathf.Min(Screen.width - HudS(24f), HudS(720f));
            float h = Mathf.Min(Screen.height - HudS(80f), HudS(420f));
            var win = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            string title = _handPileViewer switch
            {
                HandPileViewerKind.Battle => "Battle Energize",
                HandPileViewerKind.Deploy => "Deployment Energize",
                HandPileViewerKind.Secret => "Secret missions",
                _ => "Hand"
            };

            GUI.Box(win, "");
            DrawOutlineRect(win, new Color(0.95f, 0.82f, 0.2f, 0.95f), HudS(2f));
            GUI.Label(new Rect(win.x + HudS(12f), win.y + HudS(8f), win.width - HudS(100f), HudS(22f)), title,
                _cardColumnLabelStyle);
            var closePileStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.Max(15, Mathf.RoundToInt(17f * _hudFontScale)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            if (!forcingSecretOverdraw)
            {
                if (GUI.Button(new Rect(win.xMax - HudS(96f), win.y + HudS(6f), HudS(84f), HudS(44f)), "Close",
                        closePileStyle))
                    _handPileViewer = HandPileViewerKind.None;
            }

            var content = new Rect(win.x + HudS(10f), win.y + HudS(38f), win.width - HudS(20f), win.height - HudS(48f));
            if (_handPileViewer == HandPileViewerKind.Battle)
                DrawHandPileModalBattle(content, player);
            else if (_handPileViewer == HandPileViewerKind.Deploy)
                DrawHandPileModalDeploy(content, player);
            else if (_handPileViewer == HandPileViewerKind.Secret)
                DrawHandPileModalSecret(content, player, forcingSecretOverdraw);
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
            DrawTileInfoModalBackground(panel);

            float scale = GameUiScale.TileInfoModalPanelScale(panel);
            float S(float designUnits) => designUnits * scale;

            float headerH = S(72f);
            float topGapBelowHeader = S(8f);
            float insetX = S(38f);
            float insetBottom = S(144f);
            float contentLeft = panel.x + insetX;
            float contentWidth = panel.width - insetX * 2f;
            float closeSize = S(128f);
            int titleFont = TileInfoScaledFont(26f, scale, 16);
            var titleHdr = new GUIStyle(GUI.skin.label)
            {
                fontSize = titleFont,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter,
                richText = false,
                clipping = TextClipping.Overflow,
                normal = { textColor = new Color(0.96f, 0.98f, 1f, 1f) }
            };
            ApplyTileInfoFont(titleHdr);
            GUI.Label(new Rect(contentLeft, panel.y + S(26f), contentWidth, S(56f)), "TILE INFO",
                titleHdr);
            var closeRect = new Rect(panel.xMax - insetX - closeSize, panel.y + S(16f), closeSize, closeSize);
            if (GUI.Button(closeRect, GUIContent.none, GUIStyle.none))
                _showCenterBuyModal = false;
            int closeFont = TileInfoScaledFont(76f, scale, 30);
            var closeXLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = closeFont,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = false,
                normal = { textColor = new Color(0.92f, 0.22f, 0.22f, 1f) }
            };
            ApplyTileInfoFont(closeXLabel);
            // Faux-bold stroke so the glyph reads heavier on all device DPIs.
            Color prevCloseColor = closeXLabel.normal.textColor;
            closeXLabel.normal.textColor = new Color(0.48f, 0.08f, 0.08f, 1f);
            GUI.Label(new Rect(closeRect.x - S(1.8f), closeRect.y, closeRect.width, closeRect.height), "×", closeXLabel);
            GUI.Label(new Rect(closeRect.x + S(1.8f), closeRect.y, closeRect.width, closeRect.height), "×", closeXLabel);
            GUI.Label(new Rect(closeRect.x, closeRect.y - S(1.8f), closeRect.width, closeRect.height), "×", closeXLabel);
            GUI.Label(new Rect(closeRect.x, closeRect.y + S(1.8f), closeRect.width, closeRect.height), "×", closeXLabel);
            closeXLabel.normal.textColor = prevCloseColor;
            GUI.Label(closeRect, "×", closeXLabel);

            int deployGrp = player.DeployEnergize == null ? 0 : player.DeployEnergize.GroupBy(x => x).Count();
            float nameBoxH = S(226f);
            float shopIconSz = S(162f);
            float iconRowH = 0f;
            float costGap = 0f;
            float rowGap = 0f;
            const int shopColumns = 3;
            float rowStride = nameBoxH + costGap + iconRowH + rowGap;
            float buyH = rowStride * 2f;
            float energizeH = deployGrp > 0 ? S(34f) : 0f;

            float occupyingLabelH = S(50f);
            float creatureRowH = S(172f);
            float creatureRowGap = S(16f);
            float factionHdrH = Mathf.Max(S(22f), S(18f));
            float factionAfterHdrPad = S(4f);
            float factionAfterGridPad = S(10f);
            var ownersOnTile = GetPlayersWithUnitsOnTileOrdered(sel);
            float creatureBlock = occupyingLabelH + S(4f);
            if (ownersOnTile.Count <= 1)
                creatureBlock += creatureRowH * 2f + creatureRowGap;
            else
            {
                // Matches DrawHexModalCreatureGrid2Rows3Cols: P label + Space(4*scale) + grid + Space(10*scale) per owner.
                float perFaction = factionHdrH + factionAfterHdrPad + creatureRowH * 2f + creatureRowGap +
                    factionAfterGridPad;
                creatureBlock += ownersOnTile.Count * perFaction;
            }

            float shopBlock = 0f;
            if (showShop)
                shopBlock = S(20f) + S(24f) + buyH + S(10f) + energizeH + S(12f);

            float sepAfterFixed = S(4f);
            float minScrollBody = S(100f);
            float gapAfterScrollHeader = S(4f);
            float bodyBelowHeader = panel.height - headerH - topGapBelowHeader - insetBottom;
            float fixedTopH = Mathf.Max(S(220f), TileInfoFixedRowMinHeight(contentWidth, scale));
            float scrollNeed = sepAfterFixed + gapAfterScrollHeader + minScrollBody;
            if (fixedTopH + scrollNeed > bodyBelowHeader)
                fixedTopH = Mathf.Max(S(180f), bodyBelowHeader - scrollNeed);

            // Nudge hex + tile meta down (clear top safe area); shrink fixed band slightly so layout still fits.
            float fixedRowNudgeDown = S(20f);
            fixedTopH = Mathf.Max(S(164f), fixedTopH - fixedRowNudgeDown);
            var fixedRow = new Rect(contentLeft, panel.y + headerH + topGapBelowHeader + fixedRowNudgeDown,
                contentWidth, fixedTopH);
            DrawHexModalTopRow(fixedRow, player, sel, scale);

            float scrollTop = fixedRow.yMax + sepAfterFixed + gapAfterScrollHeader;
            var scrollRect = new Rect(contentLeft, scrollTop, contentWidth, panel.yMax - insetBottom - scrollTop);
            // Tight gap under tile name/owner; small extra pad when deploy shop needs vertical balance.
            float leadingPadOccupying =
                Mathf.Max(S(16f), showShop ? scrollRect.height * 0.04f : scrollRect.height * 0.1f);
            float scrollContentH = leadingPadOccupying + creatureBlock + shopBlock + S(48f);
            float scrollBottomPad = S(140f);
            scrollContentH += scrollBottomPad;
            // No visible scrollbars — full width minus tiny slop to avoid horizontal drift.
            float cw = Mathf.Floor(Mathf.Max(S(100f), scrollRect.width - 2f));

            // Default scroll view draws an opaque skin background over the tile art.
            if (_tileInfoScrollViewTransparent == null)
            {
                if (_tileInfoScrollClearTex == null)
                {
                    _tileInfoScrollClearTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    _tileInfoScrollClearTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0f));
                    _tileInfoScrollClearTex.Apply();
                }

                _tileInfoScrollViewTransparent = new GUIStyle(GUI.skin.scrollView);
                _tileInfoScrollViewTransparent.normal.background = _tileInfoScrollClearTex;
                _tileInfoScrollViewTransparent.hover.background = _tileInfoScrollClearTex;
                _tileInfoScrollViewTransparent.active.background = _tileInfoScrollClearTex;
                _tileInfoScrollViewTransparent.focused.background = _tileInfoScrollClearTex;
                _tileInfoScrollViewTransparent.onNormal.background = _tileInfoScrollClearTex;
            }

            EnsureTileInfoHiddenScrollbars();

            GUISkin skin = GUI.skin;
            GUIStyle prevScroll = skin.scrollView;
            skin.scrollView = _tileInfoScrollViewTransparent;
            try
            {
                _scrollCenterBuyDeploy = GUI.BeginScrollView(scrollRect, _scrollCenterBuyDeploy,
                    new Rect(0f, 0f, cw, scrollContentH), false, false,
                    _tileInfoHiddenHScrollbar, _tileInfoHiddenVScrollbar);
                GUILayout.BeginArea(new Rect(0f, 0f, cw, scrollContentH));

            int occFont = TileInfoScaledFont(24f, scale, 14);
            var occHdr = new GUIStyle(GUI.skin.label)
            {
                fontSize = occFont,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = false,
                normal = { textColor = new Color(0.88f, 0.94f, 1f, 1f) }
            };
            ApplyTileInfoFont(occHdr);
            float occForcesMaxW = Mathf.Min(S(680f), cw * 0.99f);
            float occForcesW = Mathf.Floor(Mathf.Min(occForcesMaxW, cw));
            GUILayout.Space(leadingPadOccupying);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical(GUILayout.Width(occForcesW));
            GUILayout.Label("Occupying forces", occHdr);
            GUILayout.Space(S(10f));
            DrawHexModalCreatureGrid2Rows3Cols(sel, player, occForcesW, creatureRowH, creatureRowGap, scale);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (showShop)
            {
                GUILayout.Space(S(18f));
                int depFont = TileInfoScaledFont(15f, scale, 10);
                var depHdr = new GUIStyle(GUI.skin.label)
                {
                    fontSize = depFont,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    richText = false
                };
                ApplyTileInfoFont(depHdr);
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("DEPLOY UNITS", depHdr, GUILayout.MaxWidth(cw));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Space(S(10f));
                Rect gridR = GUILayoutUtility.GetRect(cw, buyH);
                int depCardFont = TileInfoScaledFont(15f, scale, 10);
                DrawBuyUnitGrid(gridR.x, gridR.y, gridR.width, shopColumns, nameBoxH, shopIconSz, iconRowH, costGap,
                    rowGap, depCardFont, true, drawCardChrome: false, uiScale: scale);

                if (deployGrp > 0)
                {
                    GUILayout.Space(S(10f));
                    int depBoxFont = TileInfoScaledFont(13f, scale, 9);
                    bool hasFreeHuman = player.DeployEnergize != null &&
                        player.DeployEnergize.Contains(EnergizeDeploymentId.FreeHuman);
                    string deployText = hasFreeHuman
                        ? "Deployment Energize: FREE Human available (highlighted in shop)"
                        : $"Deployment Energize in hand: {deployGrp}";
                    if (Game.AnyMovementOccurredThisTurn)
                        deployText += "  •  Deployment locked after movement";
                    var depInfo = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = depBoxFont,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter,
                        wordWrap = true,
                        normal = { textColor = new Color(0.88f, 0.94f, 1f, 0.96f) }
                    };
                    ApplyTileInfoFont(depInfo);
                    GUILayout.Label(deployText, depInfo, GUILayout.MaxWidth(cw));
                }
            }

            GUILayout.Space(scrollBottomPad);

                GUILayout.EndArea();
                GUI.EndScrollView();
            }
            finally
            {
                skin.scrollView = prevScroll;
            }
        }

        void DrawTileInfoModalBackground(Rect panel)
        {
            if (!_tileInfoScreenTried)
            {
                _tileInfoScreenTried = true;
                _tileInfoScreenBg = NexusGuiArt.LoadTileInfoScreenBackground();
            }

            if (_tileInfoScreenBg.IsEmpty)
            {
                DrawTintedRect(panel, new Color(0.04f, 0.05f, 0.09f, 0.98f));
                return;
            }

            DrawTintedRect(panel, new Color(0.02f, 0.03f, 0.05f, 1f));
            // Full-bleed frame art (designed for this modal aspect).
            _tileInfoScreenBg.Draw(panel);
        }

        /// <summary>Full-bleed battle frame under the dim; stretched to the full display (portrait 9:16).</summary>
        void DrawBattleScreenModalBackground()
        {
            if (!_battleScreenTried)
            {
                _battleScreenTried = true;
                _battleScreenBg = NexusGuiArt.LoadBattleScreenBackground();
            }

            var full = new Rect(0f, 0f, Screen.width, Screen.height);
            if (_battleScreenBg.IsEmpty)
            {
                DrawTintedRect(full, new Color(0.04f, 0.05f, 0.09f, 0.98f));
                return;
            }

            DrawTintedRect(full, new Color(0.02f, 0.03f, 0.05f, 1f));
            _battleScreenBg.DrawStretchFill(full);
        }

        /// <summary>
        /// Draws tinted header + "SELECT CASUALTIES" / player label; returns content rect below (tile-modal insets).
        /// </summary>
        Rect DrawCasualtySelectionModalHeader(PlayerState picker, Rect panel, out float panelScale)
        {
            panelScale = GameUiScale.TileInfoModalPanelScale(panel);
            float scale = panelScale;
            float S(float d) => d * scale;
            float headerH = S(80f);
            var baseBar = new Color(0.06f, 0.07f, 0.12f, 0.88f);
            Color pc = picker.Color;
            var tint = Color.Lerp(baseBar, new Color(pc.r, pc.g, pc.b, 1f), 0.48f);
            tint.a = 0.95f;
            var headerRect = new Rect(panel.x, panel.y, panel.width, headerH);
            Color prevGui = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(headerRect, Texture2D.whiteTexture);
            GUI.color = prevGui;

            int titleFont = TileInfoScaledFont(28f, panelScale, 16);
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = titleFont,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = false,
                normal = { textColor = new Color(0.96f, 0.98f, 1f, 1f) }
            };
            ApplyTileInfoFont(titleStyle);
            GUI.Label(new Rect(panel.x, panel.y + S(10f), panel.width, S(40f)), "SELECT CASUALTIES", titleStyle);

            int subFont = TileInfoScaledFont(18f, panelScale, 12);
            var subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = subFont,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.88f, 0.92f, 1f, 0.92f) }
            };
            ApplyTileInfoFont(subStyle);
            GUI.Label(new Rect(panel.x, panel.y + S(44f), panel.width, S(28f)),
                $"Player {picker.PlayerIndex + 1}", subStyle);

            float insetX = S(38f);
            float insetBottom = S(36f);
            float topPad = S(14f);
            float contentTop = panel.y + headerH + topPad;
            return new Rect(panel.x + insetX, contentTop, panel.width - insetX * 2f,
                panel.yMax - contentTop - insetBottom);
        }

        /// <summary>Full-screen tile-style modal for Dragon’s Breath victim choice.</summary>
        void DrawCasualtySelectionModalDragon(DragonPhaseState dp)
        {
            if (dp?.Player == null || dp.PendingHit == null || dp.PendingEnemies == null)
                return;

            var dim = new Color(0f, 0f, 0f, 0.88f);
            Color prev = GUI.color;
            GUI.color = dim;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture,
                ScaleMode.StretchToFill);
            GUI.color = prev;

            var panel = GetCenterBuyModalPanelGuiRect();
            DrawTileInfoModalBackground(panel);
            Rect content = DrawCasualtySelectionModalHeader(dp.Player, panel, out float panelScale);
            float S(float d) => d * panelScale;

            _battlePanelContentWidth = content.width;
            _battlePanelScaleCached = panelScale;
            _battleHudUiScale = BattleHudUiScale(panel);
            ApplyBattleHudScaledStyles();
            EnsureBattleHudStyles();

            GUILayout.BeginArea(content);
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            if (!string.IsNullOrEmpty(dp.LastLog))
            {
                var logStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Max(11, Mathf.RoundToInt(13f * _hudFontScale)),
                    wordWrap = true,
                    normal = { textColor = new Color(0.88f, 0.92f, 0.98f, 1f) }
                };
                ApplyTileInfoFont(logStyle);
                GUILayout.Label(dp.LastLog, logStyle);
                GUILayout.Space(S(8f));
            }

            var hitStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = Mathf.Max(12, Mathf.RoundToInt(14f * _hudFontScale))
            };
            ApplyTileInfoFont(hitStyle);
            GUILayout.Label($"Hit! Roll {dp.PendingHit.LastRoll}. Remove one enemy:", hitStyle);
            GUILayout.Space(S(12f));

            float btnH = Mathf.Max(S(40f), BattleS(44f));
            foreach (var v in dp.PendingEnemies)
            {
                string label = v.Definition.Type + "  ·  P" + (v.Owner.PlayerIndex + 1);
                if (GUILayout.Button(label, _battlePrimaryButtonStyleCached, GUILayout.Height(btnH),
                        GUILayout.ExpandWidth(true)))
                    Game.DragonStrikeChooseVictim(v);
                GUILayout.Space(S(6f));
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        void DrawHexModalTopRow(Rect row, PlayerState player, BoardTile tile, float scale)
        {
            if (tile == null)
                return;

            float S(float d) => d * scale;

            TileDefinition def = Game.Config != null ? Game.Config.GetTile(tile.Type) : null;
            Color fill = def != null ? def.Color : new Color(0.45f, 0.45f, 0.48f);

            // Lower band: tile name + owner on the light purple strip (below hex art). Shorter meta band = hex sits lower on screen.
            float metaBandH = Mathf.Max(S(132f), row.height * 0.30f);
            metaBandH = Mathf.Min(metaBandH, row.height * 0.48f);
            float hexBandH = row.height - metaBandH;
            hexBandH = Mathf.Max(hexBandH, S(96f));

            float rubRightPad = S(8f);
            float rubW = Mathf.Clamp(row.width * 0.26f, S(120f), S(220f));
            var hexRowRect = new Rect(row.x, row.y, row.width, hexBandH);
            var leftBand = new Rect(hexRowRect.x + rubRightPad, hexRowRect.y, rubW, hexRowRect.height);
            var rightBand = new Rect(hexRowRect.xMax - rubW - rubRightPad, hexRowRect.y, rubW, hexRowRect.height);

            float maxHex = hexBandH - S(16f);
            float hexSide = Mathf.Min(row.width * 0.84f, maxHex);
            hexSide = Mathf.Clamp(hexSide, S(156f), S(520f));
            float hexTop = hexRowRect.y + (hexBandH - hexSide) * 0.5f + S(16f);
            hexTop = Mathf.Min(hexTop, hexRowRect.yMax - hexSide - S(4f));
            var hexR = new Rect(row.x + (row.width - hexSide) * 0.5f, hexTop, hexSide, hexSide);
            DrawModalHexPreview(hexR, fill);

            string tileName = TileTypeDisplayName(tile.Type);
            string meta = HexModalOwnerMetaLine(player, tile);
            bool contested = string.Equals(meta, "CONTESTED", StringComparison.OrdinalIgnoreCase);

            int titleSz = TileInfoScaledFont(29f, scale, 14);
            int statusSz = TileInfoScaledFont(19f, scale, 11);
            var tileTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = titleSz,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                richText = false,
                clipping = TextClipping.Overflow,
                normal = { textColor = new Color(0.93f, 0.88f, 1f, 1f) }
            };
            var statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = statusSz,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                richText = false,
                clipping = TextClipping.Overflow,
                normal = { textColor = new Color(0.78f, 0.72f, 0.95f, 1f) }
            };
            var contestedStyle = new GUIStyle(statusStyle)
            {
                clipping = TextClipping.Overflow,
                normal = { textColor = new Color(1f, 0.58f, 0.28f, 1f) }
            };

            ApplyTileInfoFont(tileTitleStyle);
            ApplyTileInfoFont(statusStyle);
            ApplyTileInfoFont(contestedStyle);

            float metaY = row.y + hexBandH;
            var metaRectFull = new Rect(row.x + S(8f), metaY, row.width - S(16f), metaBandH);
            float nameW = metaRectFull.width;
            float metaTextTopPad = S(54f);
            float metaBottomPad = S(18f);
            float innerH = metaBandH - metaTextTopPad - metaBottomPad;
            float statusH = contested
                ? contestedStyle.CalcHeight(new GUIContent("CONTESTED"), nameW)
                : statusStyle.CalcHeight(new GUIContent(meta), nameW);
            statusH = Mathf.Min(statusH + S(4f), innerH * 0.45f);
            float nameH = tileTitleStyle.CalcHeight(new GUIContent(tileName), nameW);
            nameH = Mathf.Min(nameH + S(4f), Mathf.Max(S(22f), innerH - statusH - S(6f)));
            var nameRect = new Rect(metaRectFull.x, metaY + metaTextTopPad, nameW, nameH);
            GUI.Label(nameRect, tileName, tileTitleStyle);

            float statusY = nameRect.yMax + S(8f);
            float statusHDraw = Mathf.Max(statusH, metaRectFull.yMax - statusY - metaBottomPad);
            var statusRect = new Rect(metaRectFull.x, statusY, nameW, statusHDraw);
            if (contested)
                GUI.Label(statusRect, "CONTESTED", contestedStyle);
            else
                GUI.Label(statusRect, meta, statusStyle);

            int rubHeadSz = TileInfoScaledFont(15f, scale, 11);
            int rubYieldSz = TileInfoScaledFont(30f, scale, 14);
            var rubHeadStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = rubHeadSz,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                richText = false,
                clipping = TextClipping.Overflow,
                normal = { textColor = new Color(0.82f, 0.88f, 0.96f, 1f) }
            };
            var rubYieldStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = rubYieldSz,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Overflow,
                richText = false
            };

            ApplyTileInfoFont(rubHeadStyle);
            ApplyTileInfoFont(rubYieldStyle);

            int yield = DisplayRubiumPerTurn(tile);
            string yieldText = yield > 0 ? yield.ToString() : "—";
            var rubGui = GetRubiumGui();

            float rubLineGap = S(10f);
            float iconH = S(56f);
            float line1H = Mathf.Max(S(18f), rubHeadStyle.CalcHeight(new GUIContent("Rubium / turn"), rubW));
            float line3H = Mathf.Max(S(26f), rubYieldStyle.CalcHeight(new GUIContent(yieldText), rubW));
            float iconDrawW = rubGui.IsEmpty ? 0f : iconH * rubGui.AspectRatio;
            float stackH = line1H + rubLineGap + iconH + rubLineGap + line3H;
            float stackY = hexRowRect.y + Mathf.Max(S(8f), (hexBandH - stackH) * 0.5f);

            int playerRubium = player != null ? player.Rubium : 0;
            string playerRubiumText = playerRubium.ToString();
            float playerLine3H = Mathf.Max(S(26f), rubYieldStyle.CalcHeight(new GUIContent(playerRubiumText), rubW));
            float playerStackH = line1H + rubLineGap + iconH + rubLineGap + playerLine3H;
            float playerStackY = hexRowRect.y + Mathf.Max(S(8f), (hexBandH - playerStackH) * 0.5f);
            GUI.Label(new Rect(leftBand.x, playerStackY, rubW, line1H), "Your rubium", rubHeadStyle);
            float playerIconY = playerStackY + line1H + rubLineGap;
            if (!rubGui.IsEmpty)
            {
                float ix = leftBand.x + (rubW - iconDrawW) * 0.5f;
                rubGui.Draw(ix, playerIconY, iconH);
            }

            float playerValueY = playerIconY + iconH + rubLineGap;
            Color prevPlayer = GUI.color;
            if (playerRubium <= 0)
                rubYieldStyle.normal.textColor = new Color(0.55f, 0.55f, 0.58f);
            else
                rubYieldStyle.normal.textColor = new Color(0.95f, 0.97f, 1f, 1f);
            GUI.Label(new Rect(leftBand.x, playerValueY, rubW, playerLine3H), playerRubiumText, rubYieldStyle);
            GUI.color = prevPlayer;

            GUI.Label(new Rect(rightBand.x, stackY, rubW, line1H), "Rubium / turn", rubHeadStyle);
            float iconY = stackY + line1H + rubLineGap;
            if (!rubGui.IsEmpty)
            {
                float ix = rightBand.x + (rubW - iconDrawW) * 0.5f;
                rubGui.Draw(ix, iconY, iconH);
            }

            float yieldY = iconY + iconH + rubLineGap;
            Color prevC = GUI.color;
            if (yield <= 0)
                rubYieldStyle.normal.textColor = new Color(0.55f, 0.55f, 0.58f);
            else
                rubYieldStyle.normal.textColor = new Color(0.95f, 0.97f, 1f, 1f);
            GUI.Label(new Rect(rightBand.x, yieldY, rubW, line3H), yieldText, rubYieldStyle);
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

        static int DisplayRubiumPerTurn(BoardTile tile)
        {
            if (tile == null)
                return 0;
            int y = Mathf.Max(0, tile.ExtraMineYield);
            if (tile.Type == TileType.HomeBase)
                return Mathf.Max(2, y);
            return y;
        }

        void DrawModalHexPreview(Rect r, Color fill)
        {
            var mask = HexModalSilhouetteMask();
            if (mask == null)
            {
                DrawTintedRect(r, fill);
                return;
            }

            Color p = GUI.color;
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

        /// <summary>Distinct players with at least one unit on the tile, ordered by player index.</summary>
        static List<PlayerState> GetPlayersWithUnitsOnTileOrdered(BoardTile tile)
        {
            var result = new List<PlayerState>();
            if (tile == null)
                return result;
            var seen = new HashSet<int>();
            foreach (var unit in FindObjectsOfType<UnitInstance>())
            {
                if (unit == null || unit.Tile != tile || unit.Owner == null)
                    continue;
                if (seen.Add(unit.Owner.PlayerIndex))
                    result.Add(unit.Owner);
            }

            result.Sort((a, b) => a.PlayerIndex.CompareTo(b.PlayerIndex));
            return result;
        }

        void DrawHexModalCreatureGrid2Rows3Cols(BoardTile tile, PlayerState hudPlayer, float width, float rowH,
            float rowGap, float scale)
        {
            var ownersOrdered = GetPlayersWithUnitsOnTileOrdered(tile);
            if (ownersOrdered.Count == 0)
            {
                DrawHexModalCreatureGrid2Rows3ColsForOwner(tile, hudPlayer, width, rowH, rowGap, null, scale);
                return;
            }

            if (ownersOrdered.Count == 1)
            {
                DrawHexModalCreatureGrid2Rows3ColsForOwner(tile, ownersOrdered[0], width, rowH, rowGap, ownersOrdered[0],
                    scale);
                return;
            }

            int factionHdrFont = TileInfoScaledFont(18f, scale, 11);
            foreach (var o in ownersOrdered)
            {
                var factionHdr = new GUIStyle(GUI.skin.label)
                {
                    fontSize = factionHdrFont,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                ApplyTileInfoFont(factionHdr);
                Color prev = GUI.color;
                GUI.color = o.Color;
                GUILayout.Label("P" + (o.PlayerIndex + 1), factionHdr);
                GUI.color = prev;
                GUILayout.Space(4f * scale);
                DrawHexModalCreatureGrid2Rows3ColsForOwner(tile, hudPlayer, width, rowH, rowGap, o, scale);
                GUILayout.Space(10f * scale);
            }
        }

        /// <param name="tintForCells">If null, aggregate all units on the tile for counts; otherwise only this owner's units. Tint uses the same owner when non-null, else <paramref name="hudPlayer"/> when counts are zero.</param>
        void DrawHexModalCreatureGrid2Rows3ColsForOwner(BoardTile tile, PlayerState hudPlayer, float width, float rowH,
            float rowGap, PlayerState tintForCells, float scale)
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
                if (tintForCells != null && unit.Owner != tintForCells)
                    continue;
                var t = unit.Definition.Type;
                if (countByType.ContainsKey(t))
                    countByType[t]++;
            }

            float gap = Mathf.Max(12f, 20f * scale);
            float innerW = Mathf.Floor(width);
            float cellW = Mathf.Floor((innerW - gap * 2f) / 3f);
            float rowUsedW = cellW * 3f + gap * 2f;
            float rowSidePad = Mathf.Max(0f, (innerW - rowUsedW) * 0.5f);
            PlayerState tintBase = tintForCells ?? hudPlayer;

            for (int row = 0; row < 2; row++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(rowSidePad);
                for (int col = 0; col < 3; col++)
                {
                    int i = row * 3 + col;
                    var ut = types[i];
                    int n = countByType[ut];
                    if (col > 0)
                        GUILayout.Space(gap);
                    Rect cell = GUILayoutUtility.GetRect(cellW, rowH, GUILayout.Width(cellW), GUILayout.Height(rowH));
                    DrawHexModalOccupyingForceCell(cell, ut, n, tintBase, scale);
                }

                GUILayout.Space(rowSidePad);
                GUILayout.EndHorizontal();
                if (row == 0)
                    GUILayout.Space(rowGap);
            }
        }

        void DrawHexModalOccupyingForceCell(Rect cell, UnitType type, int count, PlayerState tintOwner, float scale)
        {
            float padX = 3f * scale;
            float padY = 3f * scale;
            int countFont = TileInfoScaledFont(27f, scale, 16);
            var countStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = countFont,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Overflow
            };
            ApplyTileInfoFont(countStyle);
            float countH = Mathf.Max(18f * scale, countStyle.CalcHeight(new GUIContent("×00"), cell.width));
            float iconAreaH = Mathf.Max(12f * scale, cell.height - padY * 2f - countH - 2f * scale);
            float maxInCell = Mathf.Min(iconAreaH, cell.width - padX * 2f);
            float iconSz = Mathf.Clamp(Mathf.Max(56f * scale, maxInCell * 0.95f), 24f * scale, maxInCell);
            var iconR = new Rect(cell.x + (cell.width - iconSz) * 0.5f, cell.y + padY + (iconAreaH - iconSz) * 0.5f, iconSz,
                iconSz);
            DrawUnitMiniIcon(iconR, type, TintedIconOwnerForUnitOnSide(type, tintOwner), useGraySprite: count <= 0);

            var countRect = new Rect(cell.x + padX, cell.yMax - padY - countH, cell.width - padX * 2f, countH);
            Color p = GUI.color;
            if (count <= 0)
                GUI.color = new Color(0.42f, 0.42f, 0.46f);
            GUI.Label(countRect, "×" + count, countStyle);
            GUI.color = p;
        }

        void DrawHandPileModalBattle(Rect content, PlayerState player)
        {
            float tw = HudCardTileW();
            float th = HudCardTileH();
            float g = HudS(8f);
            float pad = HudS(4f);

            var battleGroups = player.BattleEnergize.GroupBy(x => x).OrderBy(gr => gr.Key.ToString()).ToList();
            float cw = battleGroups.Count == 0
                ? tw + g
                : battleGroups.Count * (tw + g);
            cw = Mathf.Max(cw, content.width);
            _scrollHandBattle = GUI.BeginScrollView(content, _scrollHandBattle,
                new Rect(0, 0, cw, th + g));
            if (battleGroups.Count == 0)
                DrawPlaceholderCard(new Rect(pad, pad, tw, th), "No cards");
            else
            {
                float x = pad;
                foreach (var grp in battleGroups)
                {
                    string full = EnergizeBattleCatalog.GetName(grp.Key);
                    DrawPlayingCard(new Rect(x, pad, tw, th), new Color(0.15f, 0.28f, 0.55f),
                        CardShortTitle(full), CardDetailFromName(full), grp.Count());
                    x += tw + g;
                }
            }

            GUI.EndScrollView();
        }

        void DrawHandPileModalDeploy(Rect content, PlayerState player)
        {
            float tw = HudCardTileW();
            float th = HudCardTileH();
            float g = HudS(8f);
            float pad = HudS(4f);

            var deployGroups = player.DeployEnergize.GroupBy(x => x).OrderBy(gr => gr.Key.ToString()).ToList();
            float cw = deployGroups.Count == 0
                ? tw + g
                : deployGroups.Count * (tw + g);
            cw = Mathf.Max(cw, content.width);
            _scrollHandDeploy = GUI.BeginScrollView(content, _scrollHandDeploy,
                new Rect(0, 0, cw, th + g));
            if (deployGroups.Count == 0)
                DrawPlaceholderCard(new Rect(pad, pad, tw, th), "No cards");
            else
            {
                float x = pad;
                foreach (var grp in deployGroups)
                {
                    string full = EnergizeDeploymentCatalog.GetName(grp.Key);
                    DrawPlayingCard(new Rect(x, pad, tw, th), new Color(0.15f, 0.45f, 0.25f),
                        CardShortTitle(full), CardDetailFromName(full), grp.Count());
                    x += tw + g;
                }
            }

            GUI.EndScrollView();
        }

        void DrawHandPileModalSecret(Rect content, PlayerState player, bool forcingOverdrawDiscard = false)
        {
            float tw = HudCardTileW();
            float th = HudCardTileH();
            float g = HudS(8f);
            float pad = HudS(4f);
            float extraTop = forcingOverdrawDiscard ? HudS(22f) : 0f;
            float discardH = forcingOverdrawDiscard ? HudS(24f) : 0f;
            float rowGap = forcingOverdrawDiscard ? HudS(30f) : HudS(10f);

            if (player.SecretMissions == null || player.SecretMissions.Count == 0)
            {
                DrawPlaceholderCard(new Rect(pad, pad, tw, th), "No missions");
                return;
            }

            int count = player.SecretMissions.Count;
            int rows = Mathf.Min(2, count);
            int cols = Mathf.CeilToInt(count / (float)rows);

            float cardW = (content.width - pad * 2f - g * (cols - 1)) / Mathf.Max(1, cols);
            cardW = Mathf.Clamp(cardW, HudS(90f), tw);
            float scale = tw > 1e-5f ? cardW / tw : 1f;
            float cardH = th * scale;

            if (forcingOverdrawDiscard)
            {
                var msgStyle = new GUIStyle(_cardBodyStyle)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontStyle = FontStyle.Bold,
                    wordWrap = true,
                    fontSize = Mathf.Max(11, Mathf.RoundToInt(12f * _hudFontScale))
                };
                GUI.Label(new Rect(content.x + pad, content.y + pad, content.width - pad * 2f, HudS(16f)),
                    "Hand limit reached (5). Choose one card to discard, then draw the pending secret.",
                    msgStyle);

                var declineStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = Mathf.Max(11, Mathf.RoundToInt(12f * _hudFontScale)),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                ApplyTileInfoFont(declineStyle);
                float declineW = Mathf.Min(HudS(170f), content.width * 0.35f);
                var declineRect = new Rect(content.x + content.width - declineW - pad, content.y + pad, declineW, HudS(24f));
                if (GUI.Button(declineRect, "Decline Draw", declineStyle))
                {
                    Game.DeclinePendingSecretMissionDraw();
                    _handPileViewer = HandPileViewerKind.None;
                }
            }

            for (int i = 0; i < count; i++)
            {
                int row = i / cols;
                int col = i % cols;
                float x = content.x + pad + col * (cardW + g);
                float cardY = content.y + pad + extraTop + row * (cardH + rowGap + discardH);
                var s = player.SecretMissions[i];
                string full = SecretMissionLabel(s) + " (+" + s.VictoryPoints + " VP)";
                DrawPlayingCard(new Rect(x, cardY, cardW, cardH), new Color(0.42f, 0.15f, 0.5f),
                    "#" + i + " " + CardShortTitle(full), CardDetailFromName(full), 1);
                if (forcingOverdrawDiscard)
                {
                    var discardRect = new Rect(x, cardY + cardH + HudS(4f), cardW, discardH);
                    if (GUI.Button(discardRect, "Discard"))
                        Game.DiscardSecretMissionForPendingDraw(i);
                }
            }
        }

        void DrawPhaseRibbon(PlayerState player)
        {
            var hp = _hudLayoutPanel;
            float margin = HudS(4f);
            float y = _lastCardBarY + _hudCardBarHeight + margin;
            if (y + _hudPhaseRibbonHeight > hp.yMax - HudS(2f))
                y = _lastCardBarY - _hudPhaseRibbonHeight - margin;
            y = Mathf.Clamp(y, hp.y + margin, hp.yMax - _hudPhaseRibbonHeight - HudS(2f));
            float x = hp.x + HudS(8f);
            float w = hp.width - HudS(16f);
            GUI.Box(new Rect(x, y, w, _hudPhaseRibbonHeight), "");

            string[] phases = { "Draw", "Deployment", "Movement", "Battle", "Dragon", "End Turn" };
            string active = ActivePhaseLabel(player);
            float innerPad = HudS(8f);
            float segW = (w - innerPad) / phases.Length;
            var phaseStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = Mathf.Max(11, Mathf.RoundToInt(12f * _hudFontScale)),
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                clipping = TextClipping.Clip
            };
            float segTop = HudS(2f);
            float segH = _hudPhaseRibbonHeight - HudS(6f);
            for (int i = 0; i < phases.Length; i++)
            {
                var r = new Rect(x + innerPad * 0.5f + segW * i, y + segTop, segW - HudS(2f), segH);
                bool on = phases[i] == active;
                var prev = GUI.color;
                GUI.color = on ? new Color(0.95f, 0.78f, 0.18f, 0.95f) : new Color(0.35f, 0.35f, 0.35f, 0.9f);
                GUI.Box(r, phases[i], phaseStyle);
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

        string EndTurnAdvanceLabel(PlayerState player)
        {
            if (Game == null || player == null)
                return "End Turn";
            if (Game.IsGameOver)
                return "End Turn";
            if (WillEnterCombatAfterEndTurn(player))
                return "To Combat";
            if (WillEnterDragonAfterEndTurn(player))
                return "To Dragon";
            return "To Next Turn";
        }

        bool WillEnterCombatAfterEndTurn(PlayerState player)
        {
            if (player == null || Game == null || Game.RunBattlePhaseAtTurnStart)
                return false;
            return BattleResolver.FindContestedHexesForAttacker(player).Count > 0;
        }

        bool WillEnterDragonAfterEndTurn(PlayerState player)
        {
            if (player == null || Game == null || Game.Board == null)
                return false;

            var allUnits = FindObjectsOfType<UnitInstance>();
            foreach (var u in allUnits)
            {
                if (u == null || u.Owner != player || u.Definition == null || u.Definition.Type != UnitType.RubiumDragon ||
                    u.Tile == null)
                    continue;

                if (!HexSoleControlledByPlayer(u.Tile, player, allUnits))
                    continue;

                foreach (var n in Game.Board.GetNeighbors(u.Tile))
                {
                    if (n == null || Game.IsTileContested(n))
                        continue;

                    foreach (var other in allUnits)
                    {
                        if (other != null && other.Tile == n && other.Owner != player)
                            return true;
                    }
                }
            }

            return false;
        }

        static bool HexSoleControlledByPlayer(BoardTile hex, PlayerState player, UnitInstance[] allUnits)
        {
            if (hex == null || player == null || allUnits == null)
                return false;

            PlayerState sole = null;
            foreach (var u in allUnits)
            {
                if (u == null || u.Tile != hex)
                    continue;
                if (sole == null)
                    sole = u.Owner;
                else if (sole != u.Owner)
                    return false;
            }

            return sole == player;
        }

        void MaybeQueueContestedRetreatToast(PlayerState player)
        {
            if (Game == null || player == null || Game.IsAiControlled(player))
                return;
            if (Game.RunBattlePhaseAtTurnStart)
                return;

            int turn = Game.TurnNumber;
            if (_lastContestedToastPlayerIndex == player.PlayerIndex && _lastContestedToastTurnNumber == turn)
                return;

            _lastContestedToastPlayerIndex = player.PlayerIndex;
            _lastContestedToastTurnNumber = turn;

            if (WillEnterCombatAfterEndTurn(player))
                _contestedToastUntilTime = Time.unscaledTime + 3.6f;
        }

        void DrawContestedRetreatToast(Rect hudPanel, float topY)
        {
            if (_contestedToastUntilTime <= Time.unscaledTime || Game == null)
                return;

            float tRemain = Mathf.Clamp01((_contestedToastUntilTime - Time.unscaledTime) / 3.6f);
            float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(tRemain * 2f)) *
                          Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((tRemain - 0.05f) / 0.95f));

            float w = Mathf.Min(HudS(560f), hudPanel.width - HudS(20f));
            float h = HudS(54f);
            var r = new Rect(hudPanel.x + (hudPanel.width - w) * 0.5f, topY, w, h);

            Color prev = GUI.color;
            GUI.color = new Color(0.08f, 0.10f, 0.16f, 0.90f * alpha);
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill);
            DrawOutlineRect(r, new Color(1f, 0.80f, 0.25f, 0.85f * alpha), HudS(1.5f));

            var st = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(11, Mathf.RoundToInt(12f * _hudFontScale)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color(0.97f, 0.98f, 1f, alpha) }
            };
            ApplyTileInfoFont(st);
            GUI.Label(new Rect(r.x + HudS(8f), r.y + HudS(4f), r.width - HudS(16f), r.height - HudS(8f)),
                "Contested hexes detected: you can move off them now to avoid forced battles at end turn.", st);
            GUI.color = prev;
        }

        enum EndTurnAdvanceVisualKind
        {
            NextTurn,
            Battle,
            Dragon
        }

        void EnsureEndTurnAdvanceButtonTextures()
        {
            if (_endTurnAdvanceButtonTexTried)
                return;
            _endTurnAdvanceButtonTexTried = true;
            _endTurnBattleButtonTex = Resources.Load<Texture2D>("Sprites/battle button") ??
                                      Resources.Load<Texture2D>("Sprites/Battle button");
            _endTurnFireballButtonTex = Resources.Load<Texture2D>("Sprites/fireball button") ??
                                        Resources.Load<Texture2D>("Sprites/Fireball button");
            _endTurnNextTurnButtonTex = Resources.Load<Texture2D>("Sprites/next turn button") ??
                                        Resources.Load<Texture2D>("Sprites/Next turn button");
        }

        EndTurnAdvanceVisualKind GetEndTurnButtonVisualKind(PlayerState player, bool dragonSkipButton)
        {
            if (dragonSkipButton)
                return EndTurnAdvanceVisualKind.NextTurn;
            if (WillEnterCombatAfterEndTurn(player))
                return EndTurnAdvanceVisualKind.Battle;
            if (WillEnterDragonAfterEndTurn(player))
                return EndTurnAdvanceVisualKind.Dragon;
            return EndTurnAdvanceVisualKind.NextTurn;
        }

        Texture2D GetEndTurnAdvanceButtonTexture(EndTurnAdvanceVisualKind kind)
        {
            return kind switch
            {
                EndTurnAdvanceVisualKind.Battle => _endTurnBattleButtonTex,
                EndTurnAdvanceVisualKind.Dragon => _endTurnFireballButtonTex,
                EndTurnAdvanceVisualKind.NextTurn => _endTurnNextTurnButtonTex
            };
        }

        /// <summary>
        /// IMGUI cannot render TMP SDF assets; uses the same Bemora <see cref="Font"/> as tile UI (paired with TMP Bemora SDF in Resources).
        /// </summary>
        GUIStyle EndTurnAdvanceOverlayLabelStyle()
        {
            float s = _hudFontScale;
            if (_endTurnAdvanceOverlayLabelStyle != null &&
                Mathf.Abs(_endTurnAdvanceOverlayLabelStyleScale - s) < 0.002f)
                return _endTurnAdvanceOverlayLabelStyle;

            _endTurnAdvanceOverlayLabelStyleScale = s;
            var f = TileInfoUiFont();
            _endTurnAdvanceOverlayLabelStyle = new GUIStyle(GUI.skin.label)
            {
                font = f,
                fontSize = Mathf.Max(11, Mathf.RoundToInt(14f * s)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                clipping = TextClipping.Overflow,
                normal = { textColor = Color.white }
            };
            return _endTurnAdvanceOverlayLabelStyle;
        }

        static void GuiLabelWithOutline(Rect r, string text, GUIStyle style)
        {
            if (string.IsNullOrEmpty(text))
                return;

            float a = GUI.color.a;
            var outline = new Color(0.06f, 0.05f, 0.1f, 0.94f * a);
            for (int ox = -1; ox <= 1; ox++)
            {
                for (int oy = -1; oy <= 1; oy++)
                {
                    if (ox == 0 && oy == 0)
                        continue;
                    Color prev = GUI.color;
                    GUI.color = outline;
                    GUI.Label(new Rect(r.x + ox, r.y + oy, r.width, r.height), text, style);
                    GUI.color = prev;
                }
            }

            GUI.Label(r, text, style);
        }

        void DrawPlaceholderCard(Rect r, string text)
        {
            float t = HudS(2f);
            float hdr = HudS(22f);
            GUI.Box(r, "");
            DrawTintedRect(new Rect(r.x + t, r.y + t, r.width - t * 2f, hdr), new Color(0.3f, 0.3f, 0.3f));
            GUI.Label(new Rect(r.x + HudS(6f), r.y + HudS(32f), r.width - HudS(12f), r.height - HudS(38f)), text,
                _cardBodyStyle);
        }

        void DrawPlayingCard(Rect r, Color headerColor, string title, string detail, int stack)
        {
            float t = HudS(2f);
            float hdr = HudS(22f);
            GUI.Box(r, "");
            DrawTintedRect(new Rect(r.x + t, r.y + t, r.width - t * 2f, hdr), headerColor);
            GUI.Label(new Rect(r.x + HudS(4f), r.y + HudS(3f), r.width - HudS(32f), HudS(20f)), title, _cardTitleStyle);
            if (stack > 1)
                GUI.Label(new Rect(r.x + r.width - HudS(30f), r.y + HudS(3f), HudS(26f), HudS(20f)), "x" + stack,
                    _cardBadgeStyle);
            GUI.Label(new Rect(r.x + HudS(6f), r.y + HudS(26f), r.width - HudS(12f), r.height - HudS(32f)), detail,
                _cardBodyStyle);
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

        static int ComputeBattleDiceFingerprint(BattleUiDiceRoll d)
        {
            unchecked
            {
                int h = (int)d.UnitType;
                h = h * 397 + (d.AttackerRolling ? 1 : 0);
                h = h * 397 + d.Dice;
                h = h * 397 + d.Need;
                h = h * 397 + d.Hits;
                h = h * 397 + (d.Impossible ? 1 : 0);
                if (d.Rolls != null)
                {
                    foreach (var x in d.Rolls)
                        h = h * 397 + x;
                }

                return h;
            }
        }

        void SyncBattleDiceAnimState()
        {
            var dOpt = Game.LastBattleUiDiceRoll;
            if (!dOpt.HasValue)
            {
                _battleDiceAnimFingerprint = 0;
                return;
            }

            var d = dOpt.Value;
            int fp = ComputeBattleDiceFingerprint(d);
            if (fp != _battleDiceAnimFingerprint)
            {
                _battleDiceAnimFingerprint = fp;
                _battleDiceAnimStartRealtime = Time.realtimeSinceStartup;
            }
        }

        static int SpinningPipValue(int dieIndex, float realtime)
        {
            int tick = Mathf.FloorToInt(realtime * 34f);
            uint u = unchecked((uint)(tick * 1664525 + dieIndex * 1013904223));
            return 1 + (int)(u % 6u);
        }

        NexusGuiImage GetDiceFaceArt(int pip1to6)
        {
            int idx = Mathf.Clamp(pip1to6, 1, 6) - 1;
            if (_diceFaceArtCache == null)
            {
                _diceFaceArtCache = new NexusGuiImage[6];
                for (int k = 0; k < 6; k++)
                {
                    int n = k + 1;
                    _diceFaceArtCache[k] = NexusGuiArt.Load(
                        $"Sprites/dice/dice{n}",
                        $"Sprites/Dice/dice{n}",
                        $"dice/dice{n}");
                }
            }

            return _diceFaceArtCache[idx];
        }

        void DrawBattleDieFace(Rect r, int pip1to6)
        {
            var art = GetDiceFaceArt(pip1to6);
            if (!art.IsEmpty)
            {
                art.Draw(r);
                return;
            }

            var faceBg = new Color(0.93f, 0.94f, 0.97f, 1f);
            var pipColor = new Color(0.11f, 0.13f, 0.2f, 1f);
            DrawBattleDieFaceProcedural(r, pip1to6, faceBg, pipColor);
        }

        static void DrawBattleDieFaceProcedural(Rect r, int pip1to6, Color faceBg, Color pipColor)
        {
            int v = Mathf.Clamp(pip1to6, 1, 6);
            DrawTintedRect(r, faceBg);
            DrawOutlineRect(r, new Color(0.38f, 0.42f, 0.52f, 1f), 1f);
            float m = Mathf.Min(r.width, r.height);
            float pr = m * 0.11f;

            void Pip(float nx, float ny)
            {
                float cx = r.x + r.width * nx;
                float cy = r.y + r.height * ny;
                DrawTintedRect(new Rect(cx - pr, cy - pr, pr * 2f, pr * 2f), pipColor);
            }

            switch (v)
            {
                case 1:
                    Pip(0.5f, 0.5f);
                    break;
                case 2:
                    Pip(0.28f, 0.28f);
                    Pip(0.72f, 0.72f);
                    break;
                case 3:
                    Pip(0.28f, 0.28f);
                    Pip(0.5f, 0.5f);
                    Pip(0.72f, 0.72f);
                    break;
                case 4:
                    Pip(0.28f, 0.28f);
                    Pip(0.72f, 0.28f);
                    Pip(0.28f, 0.72f);
                    Pip(0.72f, 0.72f);
                    break;
                case 5:
                    Pip(0.28f, 0.28f);
                    Pip(0.72f, 0.28f);
                    Pip(0.5f, 0.5f);
                    Pip(0.28f, 0.72f);
                    Pip(0.72f, 0.72f);
                    break;
                default:
                    Pip(0.28f, 0.32f);
                    Pip(0.28f, 0.5f);
                    Pip(0.28f, 0.68f);
                    Pip(0.72f, 0.32f);
                    Pip(0.72f, 0.5f);
                    Pip(0.72f, 0.68f);
                    break;
            }
        }

        static void DrawBattleDieImpossibleFace(Rect r, Color faceBg)
        {
            DrawTintedRect(r, faceBg);
            DrawOutlineRect(r, new Color(0.38f, 0.42f, 0.52f, 1f), 1f);
            var st = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.35f, 0.38f, 0.48f, 1f) }
            };
            GUI.Label(r, "—", st);
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

        /// <summary>
        /// Same gating as <see cref="DrawFullBattleOverlays"/> — when true, the full-screen battle modal is painted.
        /// <paramref name="submitEnergizePass"/> is set when the energize step should auto-pass without showing UI.
        /// </summary>
        bool ShouldPaintFullBattleOverlay(PlayerState currentPlayer, out bool submitEnergizePass)
        {
            submitEnergizePass = false;
            bool active = Game.PendingBattleArrangement ||
                          Game.EnergizePromptPlayer != null ||
                          Game.FocusFirePicker != null ||
                          Game.CasualtyPick != null ||
                          (Game.SecretMissionOffer != null && Game.SecretMissionOffer.Waiting) ||
                          Game.ActiveBattleHex != null;
            if (!active)
                return false;

            var actor = Game.EnergizePromptPlayer ?? Game.FocusFirePicker ?? Game.CasualtyPick?.Owner ??
                        Game.SecretMissionOffer?.Player ?? currentPlayer;
            if (actor != null && Game.IsAiControlled(actor))
                return false;

            if (Game.EnergizePromptPlayer != null &&
                Game.FocusFirePicker == null &&
                (Game.EnergizePromptPlayer.BattleEnergize == null || Game.EnergizePromptPlayer.BattleEnergize.Count == 0))
            {
                submitEnergizePass = true;
                return false;
            }

            return true;
        }

        void DrawFullBattleOverlays(PlayerState currentPlayer)
        {
            if (!ShouldPaintFullBattleOverlay(currentPlayer, out bool submitEnergizePass))
            {
                if (submitEnergizePass)
                    Game.SubmitEnergizePass();
                return;
            }

            var panel = GetBattleScreenPanelGuiRect();
            DrawTintedRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.015f, 0.02f, 0.04f, 0.72f));
            DrawBattleScreenModalBackground();
            GUI.BeginGroup(panel);
            BattleMainWindow(currentPlayer, panel);
            GUI.EndGroup();

            if (Game.CasualtyPick?.Owner != null)
                DrawBattleCasualtySelectionOverlay();
        }

        /// <summary>
        /// Modal layer on top of the battle art: tile frame, tinted header, 3×2 unit grid + Auto-pick / Clear / Confirm.
        /// </summary>
        void DrawBattleCasualtySelectionOverlay()
        {
            var cp = Game.CasualtyPick;
            if (cp?.Owner == null)
                return;

            cp.Pool.RemoveAll(u => u == null);
            cp.Selected.RemoveAll(u => u == null || !cp.Pool.Contains(u));
            cp.Required = Mathf.Clamp(cp.Required, 0, cp.Pool.Count);
            if (cp.Required == 0)
            {
                Game.SubmitCasualtyPick();
                return;
            }

            var hex = Game.ActiveBattleHex;
            var owner = cp.Owner;
            if (hex == null)
                return;

            Color prevGui = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.58f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture,
                ScaleMode.StretchToFill);
            GUI.color = prevGui;

            var panel = GetBattleScreenPanelGuiRect();
            DrawTileInfoModalBackground(panel);
            Rect content = DrawCasualtySelectionModalHeader(owner, panel, out float panelScale);

            _battlePanelContentWidth = content.width;
            _battlePanelScaleCached = panelScale;
            _battleHudUiScale = BattleHudUiScale(panel);
            ApplyBattleHudScaledStyles();
            EnsureBattleHudStyles();

            GUILayout.BeginArea(content);
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            bool isAttacker = Game.ActiveBattleAttacker != null && owner == Game.ActiveBattleAttacker;
            string side = isAttacker ? "ATTACKER" : "DEFENDER";
            var summaryStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = Mathf.Max(12, Mathf.RoundToInt(13f * _hudFontScale)),
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                normal = { textColor = new Color(0.94f, 0.96f, 1f, 1f) }
            };
            ApplyTileInfoFont(summaryStyle);
            GUILayout.Label(
                $"P{owner.PlayerIndex + 1} ({side})  ·  Tap a type to assign  ·  {cp.Selected.Count}/{cp.Required}",
                summaryStyle);

            var reqStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = Mathf.Max(12, Mathf.RoundToInt(13f * _hudFontScale)),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.9f, 0.35f, 1f) }
            };
            ApplyTileInfoFont(reqStyle);
            GUILayout.Label($"Choose {cp.Required} casualty unit{(cp.Required == 1 ? "" : "s")} to remove.", reqStyle);
            GUILayout.Space(BattleS(10f));

            DrawCasualtyOverlaySixTypeGrid(cp, hex, owner);

            GUILayout.Space(BattleS(14f));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("AUTO-PICK", _battleSecondaryButtonStyleCached, GUILayout.ExpandWidth(true),
                    GUILayout.Height(BattleS(44f))))
                AutoPickCasualties(cp);
            if (GUILayout.Button("CLEAR", _battleSecondaryButtonStyleCached, GUILayout.ExpandWidth(true),
                    GUILayout.Height(BattleS(44f))))
                cp.Selected.Clear();
            GUILayout.EndHorizontal();
            GUILayout.Space(BattleS(8f));
            GUI.enabled = cp.Selected.Count == cp.Required;
            if (GUILayout.Button("CONFIRM", _battlePrimaryButtonStyleCached, GUILayout.ExpandWidth(true),
                    GUILayout.Height(BattleS(48f))))
                Game.SubmitCasualtyPick();
            GUI.enabled = true;

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        /// <summary>3×2 grid of the six battle order types; same interaction as the former battle-strip casualty cells.</summary>
        void DrawCasualtyOverlaySixTypeGrid(CasualtyPickState cp, BoardTile hex, PlayerState player)
        {
            float fs = _hudFontScale;
            var dRollOpt = Game.LastBattleUiDiceRoll;
            var counts = new Dictionary<UnitType, int>();
            foreach (var u in FindObjectsOfType<UnitInstance>())
            {
                if (u == null || u.Tile != hex || u.Owner != player)
                    continue;
                if (!counts.ContainsKey(u.Definition.Type))
                    counts[u.Definition.Type] = 0;
                counts[u.Definition.Type]++;
            }

            var nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(12, Mathf.RoundToInt(13f * fs)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.LowerRight,
                wordWrap = false
            };
            ApplyTileInfoFont(nameStyle);

            float gridW = _battlePanelContentWidth > 8f
                ? _battlePanelContentWidth
                : Mathf.Max(100f, GameUiScale.GetPaddedModalPanelGuiRect().width - 16f);
            const int unitsPerRow = 3;
            float innerPad = BattleS(2f);
            float cellOuterW = Mathf.Floor((gridW - innerPad * 2f) / unitsPerRow) - BattleS(2f);
            cellOuterW = Mathf.Clamp(cellOuterW, BattleS(72f), BattleS(200f));
            float boxW = cellOuterW - BattleS(2f);
            float boxH = Mathf.Clamp(boxW * 0.72f, BattleS(56f), BattleS(120f));

            var unitOrder = BattleResolver.BattleOrder;
            for (int row = 0; row < 2; row++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                for (int col = 0; col < 3; col++)
                {
                    int idx = row * 3 + col;
                    if (idx >= unitOrder.Length)
                        break;
                    UnitType unitType = unitOrder[idx];
                    counts.TryGetValue(unitType, out int n);

                    GUILayout.BeginVertical(GUILayout.Width(cellOuterW));

                    var box = GUILayoutUtility.GetRect(boxW, boxH, GUILayout.Width(boxW), GUILayout.Height(boxH));
                    DrawTintedRect(box, new Color(0.08f, 0.1f, 0.16f, 0.97f));

                    bool highlightRolling = Game.HasActiveBattleStep && dRollOpt.HasValue &&
                        dRollOpt.Value.UnitType == unitType && player != null &&
                        ((dRollOpt.Value.AttackerRolling && player == Game.ActiveBattleAttacker) ||
                         (!dRollOpt.Value.AttackerRolling && player == Game.ActiveBattleDefender));
                    if (highlightRolling)
                        DrawOutlineRect(box, new Color(1f, 0.55f, 0.22f, 0.98f), BattleS(3f));

                    bool canPickHere = n > 0;
                    float labelH = BattleS(14f);
                    float countH = !canPickHere && n > 1 ? BattleS(14f) : 0f;
                    float innerH = box.height - labelH;
                    float maxIcon = Mathf.Min(boxW * 0.92f, innerH - countH - BattleS(4f));
                    float iconSz = Mathf.Clamp(maxIcon, BattleS(26f), Mathf.Min(boxW * 0.95f, innerH));
                    float blockH = iconSz + countH;
                    float blockY = box.y + (innerH - blockH) * 0.5f;
                    float ix = box.x + (box.width - iconSz) * 0.5f;
                    var iconR = new Rect(ix, blockY, iconSz, iconSz);
                    DrawUnitMiniIcon(iconR, unitType, TintedIconOwnerForUnitOnSide(unitType, player),
                        useGraySprite: n <= 0);

                    if (!canPickHere && n > 1)
                    {
                        GUI.Label(new Rect(box.x, iconR.yMax, box.width, BattleS(14f)), "×" + n,
                            new GUIStyle(GUI.skin.label)
                            {
                                fontSize = Mathf.Max(12, Mathf.RoundToInt(13f * fs)),
                                fontStyle = FontStyle.Bold,
                                alignment = TextAnchor.MiddleCenter,
                                normal = { textColor = new Color(0.88f, 0.92f, 1f, 1f) }
                            });
                    }

                    GUI.Label(
                        new Rect(box.x + BattleS(2f), box.yMax - BattleS(14f), box.width - BattleS(4f), BattleS(13f)),
                        UnitTypeAbbrev(unitType),
                        new GUIStyle(GUI.skin.label)
                        {
                            fontSize = Mathf.Max(10, Mathf.RoundToInt(11f * fs)),
                            wordWrap = true,
                            alignment = TextAnchor.LowerLeft
                        });
                    if (canPickHere && n > 0)
                        GUI.Label(new Rect(box.x + BattleS(2f), box.yMax - BattleS(16f), box.width - BattleS(4f), BattleS(14f)),
                            "×" + n, nameStyle);

                    if (canPickHere && n > 0)
                    {
                        int selected = cp.Selected.Count(u => u != null && u.Definition.Type == unitType);
                        string selText = $"{selected}/{n}";
                        if (selected > 0)
                        {
                            DrawTintedRect(new Rect(box.x + 1f, box.y + 1f, box.width - 2f, box.height - 2f),
                                new Color(0.92f, 0.64f, 0.12f, 0.26f));
                            DrawOutlineRect(new Rect(box.x + 1f, box.y + 1f, box.width - 2f, box.height - 2f),
                                new Color(1f, 0.82f, 0.2f, 0.98f), BattleS(2f));
                        }

                        var badgeRect = new Rect(box.xMax - BattleS(46f), box.y + BattleS(2f), BattleS(44f), BattleS(18f));
                        DrawTintedRect(badgeRect, selected > 0
                            ? new Color(0.95f, 0.72f, 0.18f, 0.92f)
                            : new Color(0.18f, 0.22f, 0.30f, 0.9f));
                        GUI.Label(badgeRect, selText,
                            new GUIStyle(GUI.skin.label)
                            {
                                fontSize = Mathf.Max(9, Mathf.RoundToInt(11f * fs)),
                                fontStyle = FontStyle.Bold,
                                alignment = TextAnchor.MiddleCenter,
                                normal = { textColor = selected > 0 ? new Color(0.15f, 0.1f, 0.02f, 1f) : new Color(0.82f, 0.88f, 0.98f, 1f) }
                            });

                        bool canAdd = cp.Selected.Count < cp.Required && selected < n;
                        bool canSub = selected > 0;
                        bool prevEnabled = GUI.enabled;
                        // Tap once to add, tap again to remove one casualty of this type.
                        GUI.enabled = canAdd || canSub;
                        if (GUI.Button(box, GUIContent.none, GUIStyle.none))
                        {
                            if (canSub)
                                AdjustCasualtyTypeSelection(cp, unitType, -1);
                            else if (canAdd)
                                AdjustCasualtyTypeSelection(cp, unitType, +1);
                        }
                        GUI.enabled = prevEnabled;

                        if (n > 1)
                        {
                            var plusRect = new Rect(box.x + BattleS(2f), box.y + BattleS(2f), BattleS(16f), BattleS(14f));
                            prevEnabled = GUI.enabled;
                            var prevColor = GUI.color;
                            GUI.enabled = canAdd;
                            if (!canAdd)
                                GUI.color = new Color(0.55f, 0.55f, 0.6f, 0.9f);
                            if (GUI.Button(plusRect, "+"))
                                AdjustCasualtyTypeSelection(cp, unitType, +1);
                            GUI.enabled = prevEnabled;
                            GUI.color = prevColor;
                        }
                    }

                    GUILayout.EndVertical();
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                if (row == 0 && unitOrder.Length > 3)
                    GUILayout.Space(BattleS(8f));
            }
        }

        void BattleMainWindow(PlayerState currentPlayer, Rect panel)
        {
            _battlePanelContentWidth = panel.width;
            _battlePanelScaleCached = GameUiScale.TileInfoModalPanelScale(panel);
            _battleHudUiScale = BattleHudUiScale(panel);
            ApplyBattleHudScaledStyles();
            float windowHeight = panel.height;
            // Shift layout up: 5% global, then an extra 10% for the gap above step title + dice banner row.
            float liftAll = Mathf.Clamp(panel.height * 0.05f, BattleS(6f), BattleS(56f));
            float liftStepBanner = Mathf.Clamp(panel.height * 0.10f, BattleS(10f), BattleS(120f));
            var left = Game.ActiveBattleAttacker ?? currentPlayer;
            var right = Game.ActiveBattleDefender;
            var hex = Game.ActiveBattleHex;
            bool casualtyDecision = Game.CasualtyPick != null;
            bool compactForDecision = casualtyDecision ||
                                      Game.EnergizePromptPlayer != null ||
                                      Game.FocusFirePicker != null ||
                                      (Game.SecretMissionOffer != null && Game.SecretMissionOffer.Waiting);

            // BeginGroup clips but does not always give GUILayout a height budget; BeginArea fixes FlexibleSpace.
            GUILayout.BeginArea(new Rect(0f, 0f, panel.width, panel.height));
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.Space(Mathf.Max(BattleS(8f),
                Mathf.Clamp(panel.height * 0.026f, BattleS(14f), BattleS(46f)) - liftAll * 0.35f));
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.BeginVertical(BattlePanelBoxStyle());
            DrawBattleContextBar(hex, _battlePanelScaleCached);
            GUILayout.EndVertical();
            // Push battle strip / ribbon / actions down (~10% panel height); tile name stays in the bar above.
            GUILayout.Space(Mathf.Max(BattleS(8f),
                Mathf.Clamp(panel.height * 0.10f, BattleS(12f), BattleS(96f)) - liftAll * 0.35f));

            GUILayout.BeginVertical(BattlePanelBoxStyle());
            SyncBattleDiceAnimState();
            // Fit strip inside battle panel (same width reference as tile-info scaling).
            float battleWinW = panel.width;
            float stripBudget = Mathf.Max(BattleS(260f), battleWinW - BattleS(20f));
            float clashColW = Mathf.Clamp(stripBudget * 0.24f, BattleS(96f), BattleS(168f));
            float battleColW = (stripBudget - clashColW) * 0.5f - BattleS(4f);
            battleColW = Mathf.Clamp(battleColW, BattleS(120f), BattleS(480f));
            float battleStripInnerW = battleColW * 2f + clashColW;
            if (battleStripInnerW > stripBudget)
                battleColW = Mathf.Max(BattleS(110f), (stripBudget - clashColW) * 0.5f - BattleS(3f));
            battleStripInnerW = battleColW * 2f + clashColW;

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal(GUILayout.Width(battleStripInnerW));
            DrawBattleSideColumn(left, hex, true, Game.ActiveBattleAttacker, battleColW);
            DrawBattleCenterClashAndDice(clashColW);
            DrawBattleSideColumn(right, hex, false, Game.ActiveBattleDefender, battleColW);
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            // Space below P1/P2 strip before step title + order ribbon (pull up step + banner vs strip).
            float prePhaseGap = Mathf.Clamp(panel.height * 0.10f, BattleS(12f), BattleS(96f))
                + BattleS(14f)
                + Mathf.Clamp(panel.height * 0.018f, BattleS(8f), BattleS(22f))
                - liftAll * 0.3f
                - liftStepBanner;
            GUILayout.Space(Mathf.Max(BattleS(4f), prePhaseGap));
            DrawBattlePhaseRibbon();

            GUILayout.Space(BattleS(8f));

            GUILayout.BeginVertical(BattlePanelBoxStyle());
            DrawBattleOrderRibbonIcons();

            DrawBattleDiceRollBanner();
            GUILayout.EndVertical();

            GUILayout.Space(BattleS(12f));

            GUILayout.BeginVertical(BattlePanelBoxStyle());
            if (Game.PendingBattleArrangement)
                BattleArrangeWindow();
            else if (Game.FocusFirePicker != null)
                FocusFireWindow();
            else if (Game.EnergizePromptPlayer != null)
                EnergizeWindow();
            else if (Game.CasualtyPick != null)
            {
                // Casualty picking UI is drawn in <see cref="DrawBattleCasualtySelectionOverlay"/> above the battle strip.
                GUILayout.Space(BattleS(6f));
            }
            else if (Game.SecretMissionOffer != null && Game.SecretMissionOffer.Waiting)
                SecretMissionWindow();

            GUILayout.EndVertical();

            if (!compactForDecision)
            {
                GUILayout.Space(BattleS(10f));
                GUILayout.BeginVertical(BattlePanelBoxStyle());
                GUILayout.Label("📜 Log", new GUIStyle(GUI.skin.label)
                    { fontStyle = FontStyle.Bold, fontSize = Mathf.Max(10, Mathf.RoundToInt(11f * _hudFontScale)) });
                string battleLog = !string.IsNullOrEmpty(Game.LiveBattlePhaseLog) ? Game.LiveBattlePhaseLog : Game.LastBattlePhaseLog;
                string safe = UiSafeText(battleLog);
                if (safe.Length != _lastBattleLogLen)
                {
                    _lastBattleLogLen = safe.Length;
                    _scrollBattleMainLog.y = 100000f;
                }
                float logH = Mathf.Clamp(windowHeight * 0.2f, BattleS(56f), Mathf.Min(BattleS(140f), windowHeight * 0.28f));
                _scrollBattleMainLog = GUILayout.BeginScrollView(_scrollBattleMainLog, GUILayout.Height(logH));
                var logBody = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Max(9, Mathf.RoundToInt(10f * _hudFontScale)),
                    wordWrap = true
                };
                GUILayout.Label(string.IsNullOrEmpty(safe) ? "(No battle log yet)" : safe, logBody);
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
            }

            GUILayout.EndVertical();
            // Non-compact: leave flexible room below the log; compact: inner column already fills height.
            if (!compactForDecision)
                GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        void DrawBattleContextBar(BoardTile hex, float panelScale)
        {
            string ctx;
            if (hex != null)
                ctx = TileTypeDisplayName(hex.Type);
            else if (!string.IsNullOrEmpty(Game.EnergizeBattleContext))
                ctx = Game.EnergizeBattleContext;
            else
                ctx = "Battle";
            int fs = TileInfoScaledFont(27f, panelScale, 17);
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = fs,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = false,
                normal = { textColor = new Color(0.95f, 0.97f, 1f, 0.98f) }
            };
            ApplyTileInfoFont(style);
            GUILayout.Label(ctx, style, GUILayout.ExpandWidth(true));
        }

        void DrawBattleDiceRollBanner()
        {
            SyncBattleDiceAnimState();
            var dOpt = Game.LastBattleUiDiceRoll;
            if (!dOpt.HasValue)
                return;

            var d = dOpt.Value;
            bool revealFinal = (Time.realtimeSinceStartup - _battleDiceAnimStartRealtime) >=
                               GameController.BattleDiceRollSpinSeconds;
            float rt = Time.realtimeSinceStartup;
            var bannerFont = Mathf.Max(13, Mathf.RoundToInt(16f * _hudFontScale));
            var bannerLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = bannerFont,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            ApplyTileInfoFont(bannerLabel);

            GUILayout.BeginHorizontal();
            string side = d.AttackerRolling ? "ATK" : "DEF";
            var sideC = d.AttackerRolling ? new Color(0.35f, 0.55f, 1f) : new Color(1f, 0.45f, 0.35f);
            var prev = GUI.color;
            GUI.color = sideC;
            GUILayout.Label(side, bannerLabel, GUILayout.Width(BattleS(40f)));
            GUI.color = prev;

            // Active battle step: icon + dice animate in the center column; banner stays text-only.
            if (Game.HasActiveBattleStep)
            {
                if (d.Impossible && d.Dice > 0)
                    GUILayout.Label($"need ≥{d.Need} (—)", bannerLabel, GUILayout.ExpandWidth(false));
                else if (d.Dice > 0)
                    GUILayout.Label($"need ≥{d.Need}  →  {d.Hits} hit(s)", bannerLabel, GUILayout.ExpandWidth(false));
                GUILayout.EndHorizontal();
                return;
            }

            var ir = GUILayoutUtility.GetRect(BattleS(28f), BattleS(28f), GUILayout.Width(BattleS(32f)),
                GUILayout.Height(BattleS(30f)));
            DrawBattleBannerUnitIcon(ir, d.UnitType);

            int dieCount = 0;
            if (d.Rolls != null && d.Rolls.Length > 0)
                dieCount = d.Rolls.Length;
            else if (d.Dice > 0 && d.Impossible)
                dieCount = d.Dice;

            var faceBgImpossible = new Color(0.93f, 0.94f, 0.97f, 1f);

            if (dieCount > 0)
            {
                for (int i = 0; i < dieCount; i++)
                {
                    var dr = GUILayoutUtility.GetRect(BattleS(38f), BattleS(38f), GUILayout.Width(BattleS(40f)),
                        GUILayout.Height(BattleS(40f)));
                    if (d.Rolls != null && i < d.Rolls.Length)
                    {
                        int pip = revealFinal ? d.Rolls[i] : SpinningPipValue(i, rt);
                        DrawBattleDieFace(dr, pip);
                    }
                    else if (d.Impossible)
                    {
                        if (revealFinal)
                            DrawBattleDieImpossibleFace(dr, faceBgImpossible);
                        else
                            DrawBattleDieFace(dr, SpinningPipValue(i, rt));
                    }
                }
            }
            else if (d.Dice <= 0)
            {
                GUILayout.Label("0🎲", bannerLabel, GUILayout.Width(BattleS(44f)));
            }

            if (d.Impossible && d.Dice > 0)
                GUILayout.Label($"need ≥{d.Need} (—)", bannerLabel, GUILayout.ExpandWidth(false));
            else if (d.Dice > 0)
                GUILayout.Label($"need ≥{d.Need}  →  {d.Hits} hit(s)", bannerLabel, GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
        }

        void DrawBattleSideColumn(PlayerState player, BoardTile hex, bool isLeft, PlayerState expectedSide,
            float panelWidth)
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
            float sidePanelW = panelWidth;
            float fs = _hudFontScale;
            GUILayout.BeginVertical(BattlePanelBoxStyle(), GUILayout.Width(sidePanelW), GUILayout.MinHeight(BattleS(140f)));
            GUI.color = prev;
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = Mathf.Max(16, Mathf.RoundToInt(19f * fs)),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.93f, 0.96f, 1f, 1f) }
            };
            ApplyTileInfoFont(titleStyle);
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

            var dRollOpt = Game.LastBattleUiDiceRoll;

            // 2 columns × 3 rows — always six unit slots (gray art when count is 0).
            const int unitsPerRow = 2;
            float innerPad = BattleS(2f);
            float cellOuterW = Mathf.Floor((sidePanelW - innerPad * 2f) / unitsPerRow) - BattleS(2f);
            cellOuterW = Mathf.Clamp(cellOuterW, BattleS(72f), BattleS(168f));
            float boxW = cellOuterW - BattleS(2f);
            float boxH = Mathf.Clamp(boxW * 0.72f, BattleS(56f), BattleS(104f));

            var unitOrder = BattleResolver.BattleOrder;
            for (int row = 0; row < 3; row++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                for (int col = 0; col < unitsPerRow; col++)
                {
                    var unitType = unitOrder[row * unitsPerRow + col];
                    counts.TryGetValue(unitType, out int n);

                    GUILayout.BeginVertical(GUILayout.Width(cellOuterW));

                var box = GUILayoutUtility.GetRect(boxW, boxH, GUILayout.Width(boxW), GUILayout.Height(boxH));
                DrawTintedRect(box, new Color(0.08f, 0.1f, 0.16f, 0.97f));

                bool highlightRolling = Game.HasActiveBattleStep && dRollOpt.HasValue &&
                    dRollOpt.Value.UnitType == unitType && player != null &&
                    ((dRollOpt.Value.AttackerRolling && player == Game.ActiveBattleAttacker) ||
                     (!dRollOpt.Value.AttackerRolling && player == Game.ActiveBattleDefender));
                if (highlightRolling)
                    DrawOutlineRect(box, new Color(1f, 0.55f, 0.22f, 0.98f), BattleS(3f));

                // Reserve bottom strip for type abbrev; optional stack count under icon when shown.
                float labelH = BattleS(14f);
                float countH = n > 1 ? BattleS(14f) : 0f;
                float innerH = box.height - labelH;
                float maxIcon = Mathf.Min(boxW * 0.92f, innerH - countH - BattleS(4f));
                float iconSz = Mathf.Clamp(maxIcon, BattleS(26f), Mathf.Min(boxW * 0.95f, innerH));
                float blockH = iconSz + countH;
                float blockY = box.y + (innerH - blockH) * 0.5f;
                float ix = box.x + (box.width - iconSz) * 0.5f;
                var iconR = new Rect(ix, blockY, iconSz, iconSz);
                DrawUnitMiniIcon(iconR, unitType, TintedIconOwnerForUnitOnSide(unitType, player),
                    useGraySprite: n <= 0);

                if (n > 1)
                {
                    GUI.Label(new Rect(box.x, iconR.yMax, box.width, BattleS(14f)), "×" + n,
                        new GUIStyle(GUI.skin.label)
                        {
                            fontSize = Mathf.Max(12, Mathf.RoundToInt(13f * fs)),
                            fontStyle = FontStyle.Bold,
                            alignment = TextAnchor.MiddleCenter,
                            normal = { textColor = new Color(0.88f, 0.92f, 1f, 1f) }
                        });
                }

                GUI.Label(
                    new Rect(box.x + BattleS(2f), box.yMax - BattleS(14f), box.width - BattleS(4f), BattleS(13f)),
                    UnitTypeAbbrev(unitType),
                    new GUIStyle(GUI.skin.label)
                    {
                        fontSize = Mathf.Max(10, Mathf.RoundToInt(11f * fs)),
                        wordWrap = true,
                        alignment = TextAnchor.LowerLeft
                    });

                    GUILayout.EndVertical();
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
        }

        void DrawBattleOrderRibbonIcons()
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal(BattlePanelBoxStyle(), GUILayout.ExpandWidth(false));
            GUILayout.BeginVertical();
            GUILayout.Space(BattleS(14f));
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            int n = BattleResolver.BattleOrder.Length;
            float ribbonW = _battlePanelContentWidth > 8f
                ? _battlePanelContentWidth
                : Mathf.Max(100f, GameUiScale.GetPaddedModalPanelGuiRect().width - 16f);
            float usableW = ribbonW - BattleS(28f);
            float sq = Mathf.Floor((usableW - BattleS(6f)) / Mathf.Max(1, n)) - BattleS(3f);
            sq = Mathf.Clamp(sq, BattleS(52f), BattleS(96f));
            var hex = Game.ActiveBattleHex;
            foreach (var t in BattleResolver.BattleOrder)
            {
                PlayerState sampleOnHex = null;
                if (hex != null)
                {
                    foreach (var u in FindObjectsOfType<UnitInstance>())
                    {
                        if (u == null || u.Tile != hex || u.Definition.Type != t)
                            continue;
                        sampleOnHex = u.Owner;
                        break;
                    }
                }

                bool active = Game.HasActiveBattleStep && Game.ActiveBattleStepUnitType == t;
                GUILayout.BeginVertical(GUILayout.Width(sq + 2f));
                var face = GUILayoutUtility.GetRect(sq, sq, GUILayout.Width(sq), GUILayout.Height(sq));
                DrawTintedRect(face, new Color(0.07f, 0.09f, 0.14f, 0.96f));
                DrawOutlineRect(face, active
                    ? new Color(1f, 0.55f, 0.22f, 0.95f)
                    : new Color(0.45f, 0.48f, 0.55f, 0.75f), active ? BattleS(2f) : BattleS(1f));
                float pad = BattleS(3f);
                float iconL = Mathf.Clamp(Mathf.Min(sq - pad * 2f, face.height - pad * 2f), BattleS(14f), sq);
                float ix = face.x + (face.width - iconL) * 0.5f;
                float iy = face.y + (face.height - iconL) * 0.5f;
                var ir = new Rect(ix, iy, iconL, iconL);
                DrawBattleBannerUnitIcon(ir, t, sampleOnHex == null ? 0.4f : 1f);
                GUILayout.EndVertical();
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(BattleS(12f));
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
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
            EnsureBattleHudStyles();
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
                if (GUILayout.Button("CONFIRM", _battlePrimaryButtonStyleCached, GUILayout.ExpandWidth(true)))
                    Game.ConfirmBattleArrangement();
                return;
            }

            bool canReorder = Game.BattlePlan.Count > 1;
            GUILayout.Label(canReorder
                ? "Battle order (top first). Reorder because multiple battles are active."
                : "One battle active.");
            float arrangeListH = Mathf.Clamp(GameUiScale.GetPaddedModalPanelGuiRect().height * 0.18f, 110f, 190f);
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
            if (GUILayout.Button("CONFIRM", _battlePrimaryButtonStyleCached, GUILayout.ExpandWidth(true)))
                Game.ConfirmBattleArrangement();
        }

        void EnergizeWindow()
        {
            EnsureBattleHudStyles();
            var p = Game.EnergizePromptPlayer;
            float colW = Mathf.Max(BattleS(120f), _battlePanelContentWidth);
            float contentW = Mathf.Min(BattleS(720f), colW);

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical(GUILayout.Width(contentW));
            if (GUILayout.Button("What do my cards do?", GUILayout.Height(BattleS(44f)), GUILayout.ExpandWidth(true)))
            {
                _energizeHelpSubject = p;
                _showMyEnergizeHelp = true;
            }
            GUILayout.Space(BattleS(8f));

            var distinct = p.BattleEnergize.GroupBy(x => x).OrderBy(g => g.Key.ToString()).ToList();
            int nCards = distinct.Count;
            const int cols = 2;
            float gridW = contentW;
            float gap = BattleS(14f);
            float cardW = Mathf.Floor((gridW - gap * (cols - 1)) / cols);
            cardW = Mathf.Clamp(cardW, BattleS(120f), BattleS(360f));
            // Taller tiles so two-line names stay readable on phone.
            float cardH = Mathf.Clamp(cardW * 0.42f, BattleS(64f), BattleS(120f));
            int rows = Mathf.CeilToInt(nCards / (float)cols);
            float rowStride = cardH + gap;
            float wantedScrollH = Mathf.Max(BattleS(80f), rows * rowStride + BattleS(20f));
            float panelH = GameUiScale.GetPaddedModalPanelGuiRect().height;
            float maxScrollH = Mathf.Clamp(panelH * 0.42f, BattleS(180f), BattleS(420f));
            float scrollH = Mathf.Min(wantedScrollH, maxScrollH);

            int cardFont = Mathf.Max(14, Mathf.RoundToInt(17f * _hudFontScale));
            int cardPad = Mathf.Max(6, Mathf.RoundToInt(8f * _battleHudUiScale));
            var energizeCardStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = cardFont,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(cardPad, cardPad, cardPad, cardPad)
            };
            ApplyTileInfoFont(energizeCardStyle);

            _scrollHand = GUILayout.BeginScrollView(_scrollHand, GUILayout.Height(scrollH), GUILayout.MaxHeight(maxScrollH));
            for (int row = 0; row * cols < nCards; row++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                for (int c = 0; c < cols; c++)
                {
                    int idx = row * cols + c;
                    if (idx >= nCards)
                        break;
                    var g = distinct[idx];
                    int count = g.Count();
                    string label = EnergizeBattleCatalog.GetName(g.Key) + "\nx" + count;
                    if (GUILayout.Button(label, energizeCardStyle, GUILayout.Width(cardW), GUILayout.Height(cardH)))
                        Game.SubmitEnergizePlay(g.Key);
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                if ((row + 1) * cols < nCards)
                    GUILayout.Space(gap);
            }

            GUILayout.EndScrollView();
            GUILayout.Space(BattleS(14f));
            if (GUILayout.Button("PASS", _battleSecondaryButtonStyleCached, GUILayout.Height(BattleS(48f)),
                    GUILayout.ExpandWidth(true)))
                Game.SubmitEnergizePass();
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        void FocusFireWindow()
        {
            EnsureBattleHudStyles();
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
                if (GUILayout.Button(UnitUiName(t), _battlePrimaryButtonStyleCached,
                        GUILayout.Height(BattleS(38f)),
                        GUILayout.ExpandWidth(true)))
                    Game.SubmitFocusFireUnitType(t);
                GUILayout.EndHorizontal();
            }

            if (types.Count == 0 && GUILayout.Button("CANCEL (REFUND)", _battleSecondaryButtonStyleCached,
                    GUILayout.ExpandWidth(true)))
                Game.CancelFocusFireRefund();
        }

        void CasualtyWindow()
        {
            EnsureBattleHudStyles();
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
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("AUTO-PICK", _battleSecondaryButtonStyleCached, GUILayout.ExpandWidth(true)))
                AutoPickCasualties(cp);
            if (GUILayout.Button("CLEAR", _battleSecondaryButtonStyleCached, GUILayout.ExpandWidth(true)))
                cp.Selected.Clear();
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUI.enabled = cp.Selected.Count == cp.Required;
            if (GUILayout.Button("CONFIRM", _battlePrimaryButtonStyleCached, GUILayout.ExpandWidth(true)))
                Game.SubmitCasualtyPick();
            GUI.enabled = true;
        }

        void DrawBattleCenterClashAndDice(float colW)
        {
            GUILayout.BeginVertical(GUILayout.Width(colW));
            GUILayout.Space(BattleS(10f));

            DrawBattleCenterClashSwords(colW);

            var dOpt = Game.LastBattleUiDiceRoll;
            if (Game.HasActiveBattleStep && dOpt.HasValue)
            {
                GUILayout.Space(BattleS(10f));
                var d = dOpt.Value;

                // Larger than side-grid tiles so the active roller is easy to read.
                float iconBox = Mathf.Clamp(colW - BattleS(2f), BattleS(96f), BattleS(152f));
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                var iconOuter = GUILayoutUtility.GetRect(iconBox, iconBox, GUILayout.Width(iconBox),
                    GUILayout.Height(iconBox));
                DrawTintedRect(iconOuter, new Color(0.08f, 0.1f, 0.16f, 0.97f));
                DrawOutlineRect(iconOuter, new Color(1f, 0.55f, 0.22f, 0.95f), BattleS(2f));
                float pad = BattleS(2f);
                var ir = new Rect(iconOuter.x + pad, iconOuter.y + pad, iconOuter.width - pad * 2f,
                    iconOuter.height - pad * 2f);
                DrawBattleBannerUnitIcon(ir, d.UnitType);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                bool revealFinal = (Time.realtimeSinceStartup - _battleDiceAnimStartRealtime) >=
                                     GameController.BattleDiceRollSpinSeconds;
                float rt = Time.realtimeSinceStartup;

                int dieCount = 0;
                if (d.Rolls != null && d.Rolls.Length > 0)
                    dieCount = d.Rolls.Length;
                else if (d.Dice > 0 && d.Impossible)
                    dieCount = d.Dice;

                if (dieCount > 0)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    var faceBgImpossible = new Color(0.93f, 0.94f, 0.97f, 1f);
                    int show = Mathf.Min(dieCount, 6);
                    float gap = BattleS(4f);
                    float dieSz = Mathf.Min(BattleS(46f),
                        (colW - BattleS(8f) - (show - 1) * gap) / Mathf.Max(1, show));
                    dieSz = Mathf.Max(BattleS(34f), dieSz * 1.08f);
                    for (int i = 0; i < show; i++)
                    {
                        var dr = GUILayoutUtility.GetRect(dieSz, dieSz, GUILayout.Width(dieSz),
                            GUILayout.Height(dieSz));
                        if (d.Rolls != null && i < d.Rolls.Length)
                        {
                            int pip = revealFinal ? d.Rolls[i] : SpinningPipValue(i, rt);
                            DrawBattleDieFace(dr, pip);
                        }
                        else if (d.Impossible)
                        {
                            if (revealFinal)
                                DrawBattleDieImpossibleFace(dr, faceBgImpossible);
                            else
                                DrawBattleDieFace(dr, SpinningPipValue(i, rt));
                        }
                    }

                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }
                else if (d.Dice <= 0)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("0🎲", GUILayout.Width(BattleS(36f)));
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Space(BattleS(12f));
            GUILayout.EndVertical();
        }

        void DrawBattleCenterClashSwords(float colW)
        {
            float rowH = Mathf.Clamp(colW * 0.62f, BattleS(56f), BattleS(104f));
            var row = GUILayoutUtility.GetRect(colW, rowH, GUILayout.Width(colW), GUILayout.Height(rowH));

            var att = Game.ActiveBattleAttacker;
            var def = Game.ActiveBattleDefender;
            var leftSword = GetSwordIconForPlayer(att);
            var rightSword = GetSwordIconForPlayer(def);

            // Same layout box for both; each blade is aspect-fit inside it (no stretch).
            if (!leftSword.IsEmpty && !rightSword.IsEmpty)
            {
                leftSword.DrawAspectFit(row);
                rightSword.DrawFlippedHAspectFit(row);
                return;
            }

            if (!leftSword.IsEmpty)
            {
                leftSword.DrawAspectFit(row);
            }

            if (!rightSword.IsEmpty)
            {
                rightSword.DrawFlippedHAspectFit(row);
            }

            if (leftSword.IsEmpty && rightSword.IsEmpty)
            {
                var s = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Max(16, Mathf.RoundToInt(22f * _hudFontScale)),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1f, 0.42f, 0.24f, 1f) }
                };
                GUI.Label(row, "⚔", s);
            }
        }

        NexusGuiImage GetSwordIconForPlayer(PlayerState player)
        {
            if (player == null)
                return default;
            if (_swordIconByPlayerIndex.TryGetValue(player.PlayerIndex, out var cached))
                return cached;
            var img = NexusGuiArt.LoadSwordForPlayer(player);
            _swordIconByPlayerIndex[player.PlayerIndex] = img;
            return img;
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
            EnsureBattleHudStyles();
            var offer = Game.SecretMissionOffer;
            var att = offer.Player;
            GUILayout.Label("Battle won! P" + (att.PlayerIndex + 1) + " - play ONE secret or skip:");
            foreach (int idx in offer.EligibleIndices)
            {
                if (idx < 0 || idx >= att.SecretMissions.Count)
                    continue;
                var s = att.SecretMissions[idx];
                if (GUILayout.Button(SecretMissionLabel(s) + " +" + s.VictoryPoints + " VP [i" + idx + "]",
                        _battlePrimaryButtonStyleCached, GUILayout.ExpandWidth(true)))
                    Game.PlaySecretMissionAtIndex(idx);
            }

            GUILayout.Space(8);
            if (GUILayout.Button("SKIP", _battleSecondaryButtonStyleCached, GUILayout.ExpandWidth(true)))
                Game.SkipSecretMissionPlay();
        }

        void SecretMissionOverdrawWindow()
        {
            EnsureBattleHudStyles();
            var state = Game.SecretMissionOverdraw;
            if (state == null || !state.Waiting || state.Player == null)
                return;

            var bodyStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true,
                fontSize = Mathf.Max(12, Mathf.RoundToInt(13f * _hudFontScale)),
                normal = { textColor = new Color(0.92f, 0.94f, 0.98f, 1f) }
            };
            ApplyTileInfoFont(bodyStyle);

            var p = state.Player;
            int pendingCount = state.PendingDraws?.Count ?? 0;
            GUILayout.Label(
                $"Secret hand limit reached (5). P{p.PlayerIndex + 1}: discard one mission to draw the new one.",
                bodyStyle);
            if (pendingCount > 1)
                GUILayout.Label($"Pending secret draws: {pendingCount}", bodyStyle);

            if (p.SecretMissions == null || p.SecretMissions.Count == 0)
            {
                GUILayout.Label("No mission available to discard.", bodyStyle);
                return;
            }

            for (int i = 0; i < p.SecretMissions.Count; i++)
            {
                var s = p.SecretMissions[i];
                string label = $"Discard: {SecretMissionLabel(s)} +{s.VictoryPoints} VP [i{i}]";
                if (GUILayout.Button(label, _battleSecondaryButtonStyleCached, GUILayout.ExpandWidth(true)))
                    Game.DiscardSecretMissionForPendingDraw(i);
            }
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

            var hp = _hudLayoutPanel;
            float edge = HudS(20f);
            float panelH = HudS(190f);
            float panelTop = hp.yMax - HudS(200f);
            var panelRect = new Rect(hp.x + edge, panelTop, hp.width - edge * 2f, panelH);

            if (Game.IsAiControlled(dp.Player))
            {
                GUI.Box(panelRect, "Rubium Dragon — AI is choosing…");
                if (!string.IsNullOrEmpty(dp.LastLog))
                    GUI.Label(new Rect(hp.x + HudS(30f), hp.yMax - HudS(175f), hp.width - HudS(60f), HudS(22f)),
                        dp.LastLog);
                return;
            }

            // Human casualty pick: full-screen tile-style modal is drawn at end of OnGUI (<see cref="DrawCasualtySelectionModalDragon"/>).
            if (dp.PendingHit != null && dp.PendingEnemies != null)
                return;

            // Hex targets: orange rings on the board + End Turn becomes "SKIP DRAGON'S BREATH". Optional hint.
            if (dp.Options != null && dp.Options.Count > 0)
            {
                var hint = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Max(11, Mathf.RoundToInt(12f * _hudFontScale)),
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true,
                    normal = { textColor = new Color(0.92f, 0.93f, 0.96f, 0.9f) }
                };
                float hintH = HudS(36f);
                GUI.Label(new Rect(hp.x + HudS(12f), hp.yMax - HudS(44f), hp.width - HudS(24f), hintH),
                    "Tap an orange-highlighted hex to fire. Skip with the top-left button.", hint);
            }
        }

        void DrawBuyUnitGrid(float x0, float y0, float width, int columns, float nameBoxH, float shopIconSize,
            float iconRowH, float costGap, float rowGap, int nameFontSize, bool largeShopCards = false,
            bool drawCardChrome = true, float uiScale = 1f)
        {
            float colGap = largeShopCards ? 0f : (columns >= 2 ? 12f : 8f);
            float wFloor = Mathf.Floor(width);
            float totalGaps = colGap * (columns - 1f);
            float cellW = Mathf.Floor((wFloor - totalGaps) / columns);
            float rowUsed = cellW * columns + totalGaps;
            float xStart = x0 + (width - rowUsed) * 0.5f;
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
                float cx = xStart + col * (cellW + colGap);
                float cy = y0 + row * rowStride;
                var cardRect = new Rect(cx, cy, cellW, cardH);
                DrawBuyUnitCell(cardRect, items[i].Item2, items[i].Item3, items[i].Item1, costGap, iconRowH,
                    shopIconSize, nameFontSize, largeShopCards, drawCardChrome, uiScale);
            }
        }

        void DrawBuyUnitCell(Rect cardRect, UnitType type, int baseCost, string displayName, float costGap,
            float iconRowH, float shopIconSize = 34f, int nameFontSize = 10, bool largeShopCard = false,
            bool drawCardChrome = true, float uiScale = 1f)
        {
            var player = Game.CurrentPlayer;
            var selectedHome = InputController != null ? InputController.SelectedTile : null;
            bool canPlayFreeHuman = type == UnitType.Human &&
                player.DeployEnergize != null &&
                player.DeployEnergize.Contains(EnergizeDeploymentId.FreeHuman) &&
                !Game.AnyMovementOccurredThisTurn &&
                Game.CanDeployToStartingHomeTile(player, selectedHome);
            int maxOff = Mathf.Max(0, baseCost - 1);
            int use = Mathf.Min(maxOff, player.DeploymentPurchaseDiscountRubium);
            int pay = baseCost - use;
            int effectivePay = canPlayFreeHuman ? 0 : pay;
            bool canAfford = canPlayFreeHuman || player.Rubium >= pay;

            if (largeShopCard)
            {
                Texture2D shopArt = canAfford ? GetDeployShopTexture(type) : GetDeployShopTextureGreyscale(type);
                if (shopArt != null)
                    GUI.DrawTexture(cardRect, shopArt, ScaleMode.ScaleToFit, true);
                else
                {
                    DrawTintedRect(cardRect, new Color(0.09f, 0.1f, 0.14f, 0.96f));
                    float s = Mathf.Min(cardRect.width, cardRect.height) * 0.55f;
                    var fallback = new Rect(
                        cardRect.x + (cardRect.width - s) * 0.5f,
                        cardRect.y + (cardRect.height - s) * 0.5f,
                        s, s);
                    if (canAfford)
                        DrawUnitMiniIcon(fallback, type, TintedIconOwnerForUnitOnSide(type, player));
                    else
                        DrawUnitMiniIconGreyscaleLuminance(fallback, type, TintedIconOwnerForUnitOnSide(type, player));
                }

                if (drawCardChrome)
                {
                    var chrome = canPlayFreeHuman
                        ? new Color(0.34f, 1f, 0.82f, 0.95f)
                        : new Color(0.88f, 0.78f, 0.28f, 0.55f);
                    DrawOutlineRect(cardRect, chrome, 1.5f);
                }
                if (GUI.Button(cardRect, GUIContent.none, GUIStyle.none) && canAfford)
                {
                    if (canPlayFreeHuman)
                        Game.TryPlayDeploymentEnergize(EnergizeDeploymentId.FreeHuman, selectedHome);
                    else
                        TryBuyUnit(type, player, use, pay);
                }
                return;
            }

            var nameStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                fontSize = nameFontSize,
                fontStyle = FontStyle.Bold,
                clipping = largeShopCard ? TextClipping.Overflow : TextClipping.Clip,
                richText = false
            };

            if (largeShopCard)
                ApplyTileInfoFont(nameStyle);

            float pad = largeShopCard ? 10f : 2f;
            float barH = largeShopCard ? iconRowH + 14f : iconRowH;
            float costY = largeShopCard ? cardRect.yMax - barH : cardRect.yMax - iconRowH;
            float iconTop = cardRect.y + pad;

            float iconUse;
            float nameTop;
            float nameH;
            float nameMaxBottom;

            if (largeShopCard)
            {
                float gapAboveBar = 22f;
                nameMaxBottom = costY - gapAboveBar;
                float nameGapAfterIcon = 10f;
                float minNameBlock = 56f;
                float maxIcon = Mathf.Max(22f, nameMaxBottom - iconTop - minNameBlock - nameGapAfterIcon);
                iconUse = Mathf.Min(shopIconSize, maxIcon);
                iconUse = Mathf.Max(30f, iconUse);
                nameTop = iconTop + iconUse + nameGapAfterIcon;
                float nameAvail = Mathf.Max(0f, nameMaxBottom - nameTop);
                float calcName = nameStyle.CalcHeight(new GUIContent(displayName), cardRect.width - pad * 2f);
                nameH = nameAvail <= 1f
                    ? 16f
                    : Mathf.Clamp(calcName, 16f, Mathf.Max(16f, nameAvail));
            }
            else
            {
                float bodyBottom = costY - costGap;
                iconUse = Mathf.Min(shopIconSize, (bodyBottom - iconTop) * 0.65f);
                iconUse = Mathf.Max(22f, iconUse);
                nameTop = iconTop + iconUse + 4f;
                nameMaxBottom = bodyBottom;
                nameH = Mathf.Max(16f, nameMaxBottom - nameTop - costGap);
            }

            if (largeShopCard)
            {
                DrawTintedRect(cardRect, new Color(0.09f, 0.1f, 0.14f, 0.96f));
                if (drawCardChrome)
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
            if (canAfford)
                DrawUnitMiniIcon(shopIconRect, type, TintedIconOwnerForUnitOnSide(type, player));
            else
                DrawUnitMiniIconGreyscaleLuminance(shopIconRect, type, TintedIconOwnerForUnitOnSide(type, player));

            var nameLabelRect = new Rect(cardRect.x + pad, nameTop, cardRect.width - pad * 2f, nameH);
            GUI.Label(nameLabelRect, displayName, nameStyle);

            if (GUI.Button(cardRect, GUIContent.none, GUIStyle.none) && canAfford)
            {
                if (canPlayFreeHuman)
                    Game.TryPlayDeploymentEnergize(EnergizeDeploymentId.FreeHuman, selectedHome);
                else
                    TryBuyUnit(type, player, use, pay);
            }

            Color prev = GUI.color;

            var rub = GetRubiumGui();
            float rubH = largeShopCard ? Mathf.Min(14f, barH - 8f) : 18f;
            float iconW = canPlayFreeHuman || rub.IsEmpty ? 0f : rubH * rub.AspectRatio;
            float textW = canPlayFreeHuman ? (largeShopCard ? 86f : 58f) : (largeShopCard ? 44f : 36f);
            float rowW = iconW + 6f + textW;
            float startX = cardRect.x + (cardRect.width - rowW) * 0.5f;
            float costLineY = costY + (barH - 1f - rubH) * 0.5f;
            if (!rub.IsEmpty && !canPlayFreeHuman)
                rub.Draw(startX, costLineY, rubH);
            var costStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = largeShopCard ? TileInfoScaledFont(14f, uiScale, 11) : Mathf.Max(13, Mathf.RoundToInt(13f * uiScale)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            if (largeShopCard)
                ApplyTileInfoFont(costStyle);
            if (!canAfford)
                GUI.color = new Color(0.55f, 0.55f, 0.58f);
            if (canPlayFreeHuman)
                costStyle.normal.textColor = new Color(0.34f, 1f, 0.82f, 1f);
            GUI.Label(new Rect(startX + iconW + 6f, costLineY, textW, rubH),
                canPlayFreeHuman ? "FREE" : effectivePay.ToString(), costStyle);
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

            if (HexOccupationVisuals.TryGetOccupationRingColor(popupTile, out Color occStrip))
            {
                var strip = GUILayoutUtility.GetRect(contentWidth, 5f);
                DrawTintedRect(strip, occStrip);
                GUILayout.Space(2f);
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Mine", tiny, GUILayout.Width(30f));
            int my = DisplayRubiumPerTurn(popupTile);
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

        /// <summary>Units with seat-colored sprites (Human Red, Leaper Blue, etc.).</summary>
        static bool UsesPerPlayerTint(UnitType t) =>
            t == UnitType.Human || t == UnitType.Fungoid || t == UnitType.Crystalline ||
            t == UnitType.RockStrider || t == UnitType.LavaLeaper || t == UnitType.RubiumDragon;

        static PlayerState TintedIconOwnerForUnitOnSide(UnitType t, PlayerState sidePlayer) =>
            UsesPerPlayerTint(t) ? sidePlayer : null;

        static PlayerState TintedIconOwnerForBattleSide(UnitType t, PlayerState rollingPlayer) =>
            UsesPerPlayerTint(t) ? rollingPlayer : null;

        void DrawNexusGuiImageGreyscaleLuminance(Rect r, NexusGuiImage img)
        {
            if (img.IsEmpty || r.height <= 0f)
                return;
            if (img.Texture != null)
            {
                Texture2D g = GetOrCreateGreyscaleFullTexture(img.Texture);
                if (g != null)
                    GUI.DrawTexture(r, g, ScaleMode.ScaleToFit, true);
                return;
            }

            if (img.Sprite != null)
            {
                Texture2D g = GetOrCreateGreyscaleSpritePixels(img.Sprite);
                if (g != null)
                    GUI.DrawTexture(r, g, ScaleMode.ScaleToFit, true);
            }
        }

        NexusGuiImage GetBattleBannerNeutralIcon(UnitType type)
        {
            if (_battleBannerNeutralIconCache.TryGetValue(type, out var cached))
                return cached;
            var loaded = NexusGuiArt.LoadBattleBannerNeutralIcon(type);
            _battleBannerNeutralIconCache[type] = loaded;
            return loaded;
        }

        /// <summary>
        /// Generic <c>Sprites/Units/*.png</c> art for battle initiative ribbon and dice row — no seat tint, no Gray assets.
        /// </summary>
        void DrawBattleBannerUnitIcon(Rect r, UnitType type, float alphaMultiplier = 1f)
        {
            NexusGuiImage icon = GetBattleBannerNeutralIcon(type);
            if (!icon.IsEmpty)
            {
                Color prev = GUI.color;
                Color c = prev;
                c.a *= alphaMultiplier;
                GUI.color = c;
                icon.DrawAspectFit(r);
                GUI.color = prev;
                return;
            }

            DrawTintedRect(r,
                alphaMultiplier < 0.95f
                    ? new Color(0.22f, 0.22f, 0.26f, 0.55f * Mathf.Clamp01(alphaMultiplier))
                    : new Color(0.22f, 0.22f, 0.26f));
            var letterStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip
            };
            Color prevL = GUI.color;
            GUI.color = new Color(0.88f, 0.9f, 0.96f, prevL.a * Mathf.Clamp01(alphaMultiplier));
            GUI.Label(r, UnitUiName(type).Substring(0, 1), letterStyle);
            GUI.color = prevL;
        }

        void DrawUnitMiniIconGreyscaleLuminance(Rect r, UnitType type, PlayerState ownerForTint)
        {
            NexusGuiImage icon = UsesPerPlayerTint(type) && ownerForTint != null
                ? IconForUnitWithOwner(type, ownerForTint)
                : GetUnitIcon(type);
            if (!icon.IsEmpty)
            {
                DrawNexusGuiImageGreyscaleLuminance(r, icon);
                return;
            }

            DrawTintedRect(r, new Color(0.22f, 0.22f, 0.26f));
            var letterStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip
            };
            GUI.Label(r, UnitUiName(type).Substring(0, 1), letterStyle);
        }

        void DrawUnitMiniIcon(Rect r, UnitType type, PlayerState ownerForTint = null, bool useGraySprite = false)
        {
            if (useGraySprite)
            {
                var gray = GetGrayUnitIcon(type);
                if (!gray.IsEmpty)
                {
                    gray.Draw(r);
                    return;
                }
            }

            NexusGuiImage icon = UsesPerPlayerTint(type) && ownerForTint != null
                ? IconForUnitWithOwner(type, ownerForTint)
                : GetUnitIcon(type);

            if (!icon.IsEmpty)
            {
                Color prev = GUI.color;
                if (useGraySprite)
                    GUI.color = new Color(0.38f, 0.38f, 0.42f, 1f);
                icon.Draw(r);
                GUI.color = prev;
                return;
            }

            DrawTintedRect(r,
                useGraySprite ? new Color(0.22f, 0.22f, 0.26f) : new Color(0.85f, 0.85f, 0.9f));
            var letterStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip
            };
            Color prevL = GUI.color;
            if (useGraySprite)
                GUI.color = new Color(0.48f, 0.48f, 0.52f, 1f);
            GUI.Label(r, UnitUiName(type).Substring(0, 1), letterStyle);
            GUI.color = prevL;
        }

        NexusGuiImage GetGrayUnitIcon(UnitType type)
        {
            if (_grayUnitIconCache.TryGetValue(type, out var cached))
                return cached;
            var loaded = NexusGuiArt.LoadGrayUnitIcon(type);
            _grayUnitIconCache[type] = loaded;
            return loaded;
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

            float sx = sp.x - HudS(76f);
            float sy = Screen.height - sp.y - HudS(22f);
            // Bright focus plate centered on the active battle hex.
            var focusPlate = new Rect(sx - HudS(28f), sy - HudS(18f), HudS(208f), HudS(62f));
            Color prev = GUI.color;
            GUI.color = new Color(1f, 0.15f, 0.1f, 0.26f);
            GUI.DrawTexture(focusPlate, Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = prev;
            var battleTagStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = Mathf.Max(13, Mathf.RoundToInt(15f * _hudFontScale)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            var r = new Rect(sx, sy, HudS(152f), HudS(34f));
            prev = GUI.color;
            GUI.color = new Color(1f, 0.25f, 0.15f, 0.9f);
            GUI.Box(r, "BATTLE", battleTagStyle);
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

            float s = _hudFontScale;
            float w = Mathf.Min(HudS(640f), Screen.width - HudS(40f));
            float h = Mathf.Min(HudS(520f), Screen.height - HudS(40f));
            var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            var boxStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = Mathf.Max(16, Mathf.RoundToInt(20f * s)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter
            };
            GUI.Box(panel, "Victory", boxStyle);

            var lineStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(14, Mathf.RoundToInt(17f * s)),
                wordWrap = true
            };
            var smallLineStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(13, Mathf.RoundToInt(15f * s)),
                wordWrap = true
            };
            var rowBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.Max(14, Mathf.RoundToInt(16f * s)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };

            float x = panel.x + HudS(14f);
            float y = panel.y + HudS(30f);
            float lw = panel.width - HudS(28f);
            float lineH = HudS(26f);
            GUI.Label(new Rect(x, y, lw, lineH), "Winner: P" + (snap.WinnerPlayerIndex + 1), lineStyle);
            y += lineH;
            float reasonH = HudS(48f);
            GUI.Label(new Rect(x, y, lw, reasonH), UiSafeText(snap.WinReason ?? ""), lineStyle);
            y += reasonH + HudS(4f);

            float btnH = HudS(44f);
            float btnGap = HudS(8f);
            float bw1 = HudS(148f);
            float bw2 = HudS(178f);
            float bw3 = HudS(138f);
            float rowW = bw1 + btnGap + bw2 + btnGap + bw3;
            if (rowW > lw)
            {
                float scaleDown = lw / rowW;
                bw1 *= scaleDown;
                bw2 *= scaleDown;
                bw3 *= scaleDown;
                btnGap *= scaleDown;
            }

            if (GUI.Button(new Rect(x, y, bw1, btnH), "Play again", rowBtnStyle))
            {
                _showEndGameStats = false;
                Game.ResetAndStartNewMatch();
            }

            if (GUI.Button(new Rect(x + bw1 + btnGap, y, bw2, btnH), "Back to main menu", rowBtnStyle))
            {
                _showEndGameStats = false;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }

            string statsBtn = _showEndGameStats ? "Hide stats" : "View stats";
            if (GUI.Button(new Rect(x + bw1 + btnGap + bw2 + btnGap, y, bw3, btnH), statsBtn, rowBtnStyle))
                _showEndGameStats = !_showEndGameStats;

            y += btnH + HudS(12f);
            if (_showEndGameStats)
            {
                GUI.Box(new Rect(x, y, lw, panel.yMax - y - HudS(12f)), "");
                float sy = y + HudS(8f);
                float statHdrH = HudS(24f);
                GUI.Label(new Rect(x + HudS(8f), sy, lw - HudS(16f), statHdrH), "Final stats", lineStyle);
                sy += statHdrH + HudS(4f);
                float statLineH = HudS(22f);
                for (int i = 0; i < snap.PlayerIndex.Length; i++)
                {
                    string line =
                        $"P{snap.PlayerIndex[i] + 1}  VP {snap.VictoryPoints[i]}  Rubium {snap.Rubium[i]}  Units {snap.UnitCounts[i]}";
                    GUI.Label(new Rect(x + HudS(8f), sy, lw - HudS(16f), statLineH), line, smallLineStyle);
                    sy += statLineH;
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




