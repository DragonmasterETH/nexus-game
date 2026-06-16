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
        NexusGuiImage _tileInfoNameBanner;
        bool _tileInfoNameBannerTried;
        NexusGuiImage _battleScreenBg;
        bool _battleScreenTried;
        NexusGuiImage _cardScreenBg;
        bool _cardScreenTried;
        NexusGuiImage _pileBattleCardFace;
        NexusGuiImage _pileDeployCardFace;
        NexusGuiImage _pileMissionCardFace;
        bool _pileCardFaceTried;
        NexusGuiImage _casualtyScreenBg;
        bool _casualtyScreenTried;
        float _battlePanelContentWidth;
        float _battleHudUiScale = 1f;

        /// <summary>Screen-space icon rects for battle strip slots (Repaint only). Used by death.png casualty FX.</summary>
        readonly Dictionary<(bool isLeft, UnitType t), Rect> _battleUnitSlotIconRects =
            new Dictionary<(bool, UnitType), Rect>();

        Texture2D _battleDeathFxTex;
        bool _battleDeathFxTexTried;
        float _battlePanelScaleCached = 1f;

        /// <summary>GUI rect for battle/casualty UI — drives <see cref="GameUiScale.FullBleedImGuiScaledFont"/> (physical width vs letterboxed canvas).</summary>
        Rect _battleFontReferencePanel;

        NexusGuiImage _battleStepBannerImg;
        NexusGuiImage _battleUnitRibbonImg;
        NexusGuiImage _battleArmyContainerImg;
        bool _battleScreenChromeArtTried;

        void EnsureBattleScreenChromeArt()
        {
            if (_battleScreenChromeArtTried)
                return;
            _battleScreenChromeArtTried = true;
            _battleStepBannerImg = NexusGuiArt.Load(
                "Sprites/step banner",
                "Sprites/step_banner",
                "Sprites/StepBanner");
            _battleUnitRibbonImg = NexusGuiArt.Load(
                "Sprites/unit ribbon",
                "Sprites/unit_ribbon",
                "Sprites/UnitRibbon");
            _battleArmyContainerImg = NexusGuiArt.Load(
                "Sprites/army container",
                "Sprites/army_container",
                "Sprites/ArmyContainer");
        }

        /// <summary>Scales main gameplay HUD (not tile-info modal) for narrow phones — same idea as <see cref="BattleHudUiScale"/>.</summary>
        float _mainHudUiScale = 1f;

        /// <summary>Font size multiplier — <see cref="GameUiScale.ImGuiFontScale"/> (no touch floor; shrinks on small screens).</summary>
        float _hudFontScale = 1f;

        float _hudCardBarHeight = 215f;
        float _hudPhaseRibbonHeight = 26f;
        Texture2D _tileInfoScrollClearTex;
        GUIStyle _tileInfoScrollViewTransparent;
        GUIStyle _tileInfoHiddenHScrollbar;
        GUIStyle _tileInfoHiddenVScrollbar;
        Font TileInfoUiFont() => NexusUiFonts.ImguiFont();

        void ApplyTileInfoFont(GUIStyle style) => NexusUiFonts.ApplyTo(style);

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

        void EnsureTileInfoScrollViewTransparentStyle()
        {
            if (_tileInfoScrollViewTransparent != null)
                return;
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

        public Rect GetCenterBuyModalPanelGuiRect()
        {
            return GameUiScale.GetPaddedModalPanelGuiRect();
        }

        /// <summary>Main board HUD layout rect — top-anchored (<see cref="GameUiScale.GetMainHudPanelGuiRect"/>).</summary>
        public Rect GetMainHudPanelGuiRect() => GameUiScale.GetMainHudPanelGuiRect();

        /// <summary>Battle overlay IMGUI group — full bleed (entire display), not letterboxed or 90% modal inset.</summary>
        public Rect GetBattleScreenPanelGuiRect()
        {
            return GameUiScale.GetFullBleedScreenGuiRect();
        }

        static int TileInfoScaledFont(float designSize, float panelScale, int minSize) =>
            GameUiScale.TileInfoScaledFont(designSize, panelScale, minSize);

        static float BattleHudUiScale(Rect panel) => GameUiScale.BattleHudUiScale(panel);

        /// <summary>Same as tile-info modal layout <c>S()</c> scale — <see cref="GameUiScale.ImGuiHudScale"/>.</summary>
        static float MainHudUiScale() => GameUiScale.ImGuiHudScale();

        static float MainHudFontScale() => GameUiScale.ImGuiFontScale();

        /// <summary>Scaled design pixels for main gameplay HUD (outside battle overlay / tile modal).</summary>
        float HudS(float designPixels) => Mathf.Max(1f, designPixels * _mainHudUiScale);

        /// <summary>Extra scale for bottom card bar (pile button, tile panel, move-all row) so controls read larger on tall bars.</summary>
        const float BottomHudInnerLayoutMul = 1.58f;

        /// <summary>Layout/design pixels for bottom HUD innards — <see cref="HudS"/> with <see cref="BottomHudInnerLayoutMul"/>.</summary>
        float BottomHudS(float designPixels) => HudS(designPixels * BottomHudInnerLayoutMul);

        /// <summary>Scaled hand / pile card tile size.</summary>
        float HudCardTileW() => HudS(112f);

        float HudCardTileH() => HudS(104f);

        /// <summary>Larger reference size for the hand-pile / energize modal — caps card scale (sprite fills this rect).</summary>
        float HandPileCardTileW() => HudS(200f);

        float HandPileCardTileH() => HudS(188f);

        /// <summary>Scaled design-pixel value for current battle overlay (spacing, min sizes).</summary>
        float BattleS(float designPixels) => Mathf.Max(1f, designPixels * _battleHudUiScale);

        /// <summary>Updates ribbon/button fonts from <see cref="_hudFontScale"/> and layout from <see cref="_battleHudUiScale"/> each battle frame.</summary>
        void ApplyBattleHudScaledStyles()
        {
            EnsureBattleHudStyles();
            float s = _battleHudUiScale;
            float wR = GameUiScale.FullBleedPanelWidthToCanvasWidthRatio(_battleFontReferencePanel);
            _battleRibbonLabelStyle.fontSize = GameUiScale.FullBleedImGuiScaledFont(20f, _battleFontReferencePanel, 12, 36);
            _battlePrimaryButtonStyleCached.fontSize = GameUiScale.ImGuiScaledFont(18f, 16, 34, wR);
            _battlePrimaryButtonStyleCached.fixedHeight = Mathf.Max(48f, 56f * s);
            int pad = Mathf.Max(10, Mathf.RoundToInt(16f * s));
            int pady = Mathf.Max(10, Mathf.RoundToInt(14f * s));
            _battlePrimaryButtonStyleCached.padding = new RectOffset(pad, pad, pady, pady);
            _battleSecondaryButtonStyleCached.fontSize = GameUiScale.ImGuiScaledFont(17f, 15, 30, wR);
            _battleSecondaryButtonStyleCached.fixedHeight = Mathf.Max(46f, 52f * s);
            _battleSecondaryButtonStyleCached.padding = new RectOffset(pad, pad, pady, pady);
        }

        void ApplyMainHudScaledStyles()
        {
            EnsureCardStyles();
            // Hand cards / flying FX pick font per frame via <see cref="GameUiScale.ComputeBestFitFontSize"/> so text
            // stays inside rects across resolutions.

            EnsureEnergizeHelpWindowStyles();
            ApplyEnergizeHelpScaledStyles();
            EnsureQuickRefBodyStyle();
            ApplyQuickRefScaledStyles();
        }


        void ApplyEnergizeHelpScaledStyles()
        {
            if (_energizeHelpWindowStyle == null)
                return;
            float s = _mainHudUiScale;
            _energizeHelpWindowStyle.fontSize = GameUiScale.ImGuiScaledFont(14f, 12, 26);
            int px = Mathf.RoundToInt(14f * s);
            int pyTop = Mathf.RoundToInt(24f * s);
            int pyBot = Mathf.RoundToInt(12f * s);
            _energizeHelpWindowStyle.padding = new RectOffset(px, px, pyTop, pyBot);
            _energizeHelpBodyLabelStyle.fontSize = GameUiScale.ImGuiScaledFont(12f, 10, 22);
            _energizeHelpSectionLabelStyle.fontSize = _energizeHelpBodyLabelStyle.fontSize;
            if (_energizeHelpLayoutButtonStyle != null)
            {
                _energizeHelpLayoutButtonStyle.fontSize = GameUiScale.ImGuiScaledFont(18f, 15, 30);
                _energizeHelpLayoutButtonStyle.fixedHeight = Mathf.Max(40f, HudS(44f));
            }
        }

        void ApplyQuickRefScaledStyles()
        {
            if (_quickRefBodyStyle == null)
                return;
            _quickRefBodyStyle.fontSize = GameUiScale.ImGuiScaledFont(15f, 13, 24);
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

        /// <summary>Full-card art when a deploy energize makes that row free (e.g. <see cref="EnergizeDeploymentId.FreeHuman"/>).</summary>
        static readonly Dictionary<UnitType, string> DeployShopFreeResourcePaths = new Dictionary<UnitType, string>
        {
            { UnitType.Human, "Sprites/Units/Human Shop Free" },
            { UnitType.Fungoid, "Sprites/Units/Fungus Shop Free" },
            { UnitType.Crystalline, "Sprites/Units/Crystal Shop Free" },
            { UnitType.RockStrider, "Sprites/Units/Strider Shop Free" },
            { UnitType.LavaLeaper, "Sprites/Units/Leaper Shop Free" },
            { UnitType.RubiumDragon, "Sprites/Units/Dragon Shop Free" }
        };

        static readonly Dictionary<UnitType, Texture2D> DeployShopTextureCache = new Dictionary<UnitType, Texture2D>();
        static readonly Dictionary<UnitType, Texture2D> DeployShopGreyscaleCache = new Dictionary<UnitType, Texture2D>();
        static readonly Dictionary<UnitType, Texture2D> DeployShopFreeTextureCache = new Dictionary<UnitType, Texture2D>();
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

        static Texture2D GetDeployShopFreeTexture(UnitType type)
        {
            if (DeployShopFreeTextureCache.TryGetValue(type, out Texture2D cached) && cached != null)
                return cached;
            if (!DeployShopFreeResourcePaths.TryGetValue(type, out string path))
                return null;
            Texture2D tex = Resources.Load<Texture2D>(path);
            if (tex == null)
            {
                var sp = Resources.Load<Sprite>(path);
                if (sp != null)
                    tex = sp.texture;
            }

            if (tex != null)
                DeployShopFreeTextureCache[type] = tex;
            return tex;
        }

        void EnsureTileInfoNameBanner()
        {
            if (_tileInfoNameBannerTried)
                return;
            _tileInfoNameBannerTried = true;
            _tileInfoNameBanner = NexusGuiArt.LoadTileInfoNameBanner();
        }

        /// <summary>Full-width name banner height (0 if art missing).</summary>
        float TileInfoNameBannerHeight(Rect panel, float scale)
        {
            EnsureTileInfoNameBanner();
            if (_tileInfoNameBanner.IsEmpty || panel.width < 1f)
                return 0f;
            float S(float d) => d * scale;
            float h = panel.width / _tileInfoNameBanner.AspectRatio;
            return Mathf.Clamp(h, S(36f), S(96f)) + S(8f);
        }

        /// <summary>Minimum height: large hex band + meta band (name/owner on name banner).</summary>
        float TileInfoFixedRowMinHeight(float contentWidth, Rect panel, float scale)
        {
            float S(float d) => d * scale;
            float w = contentWidth;
            float metaBandH = Mathf.Max(S(148f), S(380f) * 0.32f, TileInfoNameBannerHeight(panel, scale));
            float hexSide = Mathf.Clamp(Mathf.Min(w * 0.78f, S(400f)), S(150f), S(480f));
            float hexBandH = S(12f) + hexSide;
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
            var hp = GameUiScale.GetMainHudPanelGuiRect();
            float hs = MainHudUiScale();
            float topBarY = hp.y + 6f * hs;
            float topBarH = 124f * hs;
            if (new Rect(hp.x, topBarY, hp.width, topBarH).Contains(gui))
                return true;

            // Bottom card/tile panel band (includes overlap with end-turn art above the bar).
            if (_lastBottomHudInputBlockRect.width > 0f && _lastBottomHudInputBlockRect.height > 0f &&
                _lastBottomHudInputBlockRect.Contains(gui))
                return true;
            if (_lastCardBarY > 0f && gui.y >= _lastCardBarY - 4f * hs)
                return true;
            if (_lastTilePanelRect.width > 0f && _lastTilePanelRect.Contains(gui))
                return true;
            if (_lastCardsPileRect.width > 0f && _lastCardsPileRect.Contains(gui))
                return true;
            if (_lastUnitDetailRect.width > 0f && _lastUnitDetailRect.Contains(gui))
                return true;
            if (_lastPhaseRibbonRect.width > 0f && _lastPhaseRibbonRect.Contains(gui))
                return true;
            if (_lastEndTurnButtonRect.width > 0f && _lastEndTurnButtonRect.Contains(gui))
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
            }

            // Full-screen battle modal (dim + panel) — block board when it is shown.
            if (Game != null && Game.Players.Count > 0 &&
                ShouldPaintFullBattleOverlay(Game.CurrentPlayer))
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
        BoardTile _tilePanelTabTile;
        int _tilePanelViewPlayerIndex = -1;
        BoardTile _tilePanelDetailTile;
        UnitType _tilePanelDetailUnit;
        bool _tilePanelHasDetailUnit;
        int _tilePanelLastTurnPlayerIndex = -1;
        float _lastCardBarY;
        Rect _lastBottomHudInputBlockRect;
        Rect _lastEndTurnButtonRect;
        Rect _lastTilePanelRect;
        Rect _lastCardsPileRect;
        Rect _lastUnitDetailRect;
        Rect _lastPhaseRibbonRect;
        int _lastContestedToastPlayerIndex = -1;
        int _lastContestedToastTurnNumber = -1;
        float _contestedToastUntilTime;
        float _lastEnergizeAutoPassAttemptUnscaled = -999f;

        /// <summary>Main gameplay HUD rect — top-anchored (<see cref="GameUiScale.GetMainHudPanelGuiRect"/>). Tile/deploy modal uses <see cref="GameUiScale.GetFullscreenModalStylePanelGuiRect"/>; other menus may use <see cref="GetCenterBuyModalPanelGuiRect"/>.</summary>
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
        Vector2 _scrollHandSecret;
        Vector2 _scrollTilePanel;

        GUIStyle _cardTitleStyle;
        GUIStyle _cardBodyStyle;
        GUIStyle _cardBadgeStyle;
        GUIStyle _cardColumnLabelStyle;
        GUIStyle _handPileCardTitleStyle;
        GUIStyle _handPileCardBodyStyle;
        GUIStyle _handPileCardBadgeStyle;
        bool _handPileCardTextStylesReady;

        /// <summary>Card tiles — bottom bar uses <see cref="HudCardTileW"/> / <see cref="HudCardTileH"/>; hand-pile modal uses larger <see cref="HandPileCardTileW"/> / <see cref="HandPileCardTileH"/>.</summary>

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
        Texture2D _moveStackMinusButtonTex;
        Texture2D _moveStackPlusButtonTex;
        bool _moveStackPlusMinusButtonTexTried;
        Texture2D _bottomHudPanelTex;
        bool _bottomHudPanelTexTried;
        Texture2D _topHudPanelTex;
        bool _topHudPanelTexTried;
        Texture2D _cardsPileButtonTex;
        bool _cardsPileButtonTexTried;
        GUIStyle _endTurnAdvanceOverlayLabelStyle;
        float _endTurnAdvanceOverlayLabelStyleScale;
        GUIStyle _battlePanelBoxStyle;
        Texture2D _battlePanelBoxTex;
        GUIStyle _mainBoardTopIconHitStyle;
        GUIStyle _transparentHitButtonStyle;
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
            var hp = GameUiScale.GetMainHudPanelGuiRect();
            float hs = MainHudUiScale();
            float topBarY = hp.y + 6f * hs;
            float topBarH = 124f * hs;
            float iconH = 64f * hs;
            float ly = topBarY + (topBarH - iconH) * 0.5f - 2f * hs;
            var rub = GetRubiumGui();
            float w = rub.IsEmpty ? iconH : iconH * rub.AspectRatio;
            float cx = hp.x + 12f * hs + w * 0.5f;
            float cy = ly + iconH * 0.5f;
            return new Vector2(cx, cy);
        }

        Vector2 GetVpBankIconCenterGui()
        {
            var hp = GameUiScale.GetMainHudPanelGuiRect();
            if (Game == null || Game.Players.Count == 0)
                return new Vector2(hp.x + hp.width * 0.5f, hp.y + 24f);

            float hs = MainHudUiScale();
            float topBarY = hp.y + 6f * hs;
            float topBarH = 124f * hs;
            float iconH = 64f * hs;
            float ly = topBarY + (topBarH - iconH) * 0.5f - 2f * hs;
            float cy = ly + iconH * 0.5f;
            var player = Game.CurrentPlayer;
            bool handPileModalOpen = _handPileViewer != HandPileViewerKind.None;
            var rub = GetRubiumGui();
            var vp = GetVPGui();
            float rxRes = hp.x + 12f * hs;
            if (!rub.IsEmpty)
                rxRes += iconH * rub.AspectRatio + 6f * hs;
            int hudFontSize = GameUiScale.ImGuiScaledFont(18f, 15, 52);
            float tw = EstimateHudNumberWidth(player.Rubium, hudFontSize);
            rxRes += Mathf.Max(56f * hs, tw) + 12f * hs;
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
                ApplyTileInfoFont(_flyRubiumAmountStyle);
            }

            var flyCam = Camera.main;
            float now = Time.time;
            Color prev = GUI.color;
            float iconBase = 64f * MainHudUiScale();
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
                {
                    string amt = "+" + f.Amount;
                    float lw = Mathf.Min(160f * MainHudUiScale(), Screen.width * 0.22f);
                    _flyRubiumAmountStyle.fontSize = GameUiScale.ComputeBestFitFontSize(_flyRubiumAmountStyle, amt, lw, h,
                        10, GameUiScale.ImGuiScaledFont(14f, 12, 34), false);
                    GUI.Label(new Rect(r.xMax + 2f, r.y, lw, h), amt, _flyRubiumAmountStyle);
                }
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
                ApplyTileInfoFont(_flyVpAmountStyle);
                ApplyTileInfoFont(_flyVpFallbackStyle);
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

                float iconBase = 64f * MainHudUiScale();
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
                float textMaxW = Mathf.Min(Screen.width * 0.3f, 220f * MainHudUiScale());
                _flyVpAmountStyle.fontSize = GameUiScale.ComputeBestFitFontSize(_flyVpAmountStyle, bonus, textMaxW,
                    iconH, 12, GameUiScale.ImGuiScaledFont(26f, 16, 44), false);
                float textW = _flyVpAmountStyle.CalcSize(new GUIContent(bonus)).x;
                float groupW = vpW + 4f + textW;
                float leftX = p.x - groupW * 0.5f;

                GUI.color = new Color(1f, 1f, 1f, alpha);
                if (!vpGui.IsEmpty)
                {
                    var iconRect = new Rect(leftX, p.y - iconH * 0.5f, vpW, iconH);
                    vpGui.Draw(iconRect);
                    GUI.Label(new Rect(iconRect.xMax + 4f, p.y - iconH * 0.5f, textMaxW, iconH), bonus,
                        _flyVpAmountStyle);
                }
                else
                {
                    string fb = "VP " + bonus;
                    var fbRect = new Rect(p.x - Screen.width * 0.22f, p.y - 28f, Screen.width * 0.44f, 56f);
                    _flyVpFallbackStyle.fontSize = GameUiScale.ComputeBestFitFontSize(_flyVpFallbackStyle, fb,
                        fbRect.width, fbRect.height, 12, GameUiScale.ImGuiScaledFont(34f, 18, 52), false);
                    GUI.Label(fbRect, fb, _flyVpFallbackStyle);
                }
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
            ApplyTileInfoFont(_cardTitleStyle);
            ApplyTileInfoFont(_cardBodyStyle);
            ApplyTileInfoFont(_cardBadgeStyle);
            ApplyTileInfoFont(_cardColumnLabelStyle);
        }

        void EnsureHandPileCardFaces()
        {
            if (_pileCardFaceTried)
                return;
            _pileCardFaceTried = true;
            _pileBattleCardFace = NexusGuiArt.LoadBattleCardSprite();
            _pileDeployCardFace = NexusGuiArt.LoadDeploymentCardSprite();
            _pileMissionCardFace = NexusGuiArt.LoadMissionCardSprite();
        }

        void EnsureHandPileCardTextStyles()
        {
            if (_handPileCardTextStylesReady)
                return;
            _handPileCardTextStylesReady = true;
            Color w = Color.white;
            _handPileCardTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Clip,
                richText = false,
                normal = { textColor = w },
                hover = { textColor = w },
                active = { textColor = w },
                focused = { textColor = w },
                onNormal = { textColor = w },
                onHover = { textColor = w },
                onActive = { textColor = w },
                onFocused = { textColor = w }
            };
            _handPileCardBodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                clipping = TextClipping.Clip,
                richText = false,
                normal = { textColor = w },
                hover = { textColor = w },
                active = { textColor = w },
                focused = { textColor = w },
                onNormal = { textColor = w },
                onHover = { textColor = w },
                onActive = { textColor = w },
                onFocused = { textColor = w }
            };
            Color badgeYellow = new Color(1f, 0.92f, 0.18f, 1f);
            _handPileCardBadgeStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                richText = false,
                normal = { textColor = badgeYellow },
                hover = { textColor = badgeYellow },
                active = { textColor = badgeYellow },
                focused = { textColor = badgeYellow },
                onNormal = { textColor = badgeYellow },
                onHover = { textColor = badgeYellow },
                onActive = { textColor = badgeYellow },
                onFocused = { textColor = badgeYellow }
            };
            ApplyTileInfoFont(_handPileCardTitleStyle);
            ApplyTileInfoFont(_handPileCardBodyStyle);
            ApplyTileInfoFont(_handPileCardBadgeStyle);
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
            int ribbonCap = GameUiScale.FullBleedImGuiScaledFont(20f, _battleFontReferencePanel, 10, 38);
            float ribbonH = Mathf.Max(BattleS(30f), ribbonCap + BattleS(16f));
            GUILayout.BeginHorizontal();
            var slot = GUILayoutUtility.GetRect(1f, ribbonH, GUILayout.ExpandWidth(true), GUILayout.Height(ribbonH));
            GUILayout.EndHorizontal();

            if (Event.current.type == EventType.Repaint)
            {
                EnsureBattleScreenChromeArt();
                if (!_battleStepBannerImg.IsEmpty)
                    _battleStepBannerImg.DrawStretchFill(slot);
            }

            string ribbonTitle = BattlePhaseStepTitle(Game);
            int ribbonMax = GameUiScale.FullBleedImGuiScaledFont(20f, _battleFontReferencePanel, 10, 38);
            _battleRibbonLabelStyle.fontSize = GameUiScale.ComputeBestFitFontSize(_battleRibbonLabelStyle, ribbonTitle,
                slot.width, slot.height, 9, ribbonMax, false);
            GUI.Label(slot, ribbonTitle, _battleRibbonLabelStyle);
        }

        void OnGUI()
        {
            NexusUiFonts.EnsureImGuiSkinFonts();

            if (Game == null || Game.Players.Count == 0)
                return;
            if (Game.IsGameOver && Game.FinalSnapshot != null)
            {
                _hudLayoutPanel = GetMainHudPanelGuiRect();
                _mainHudUiScale = MainHudUiScale();
                _hudFontScale = MainHudFontScale();
                ApplyMainHudScaledStyles();
                DrawEndGameOverlay(Game.FinalSnapshot);
                DrawOnlineConnectionBanner();
                return;
            }

            var player = Game.CurrentPlayer;
            bool handPileModalOpen = _handPileViewer != HandPileViewerKind.None;

            _hudLayoutPanel = GetMainHudPanelGuiRect();
            _mainHudUiScale = MainHudUiScale();
            _hudFontScale = MainHudFontScale();
            _hudCardBarHeight = HudS(200f * 1.25f * 1.5f);
            _hudPhaseRibbonHeight = HudS(42f);
            ApplyMainHudScaledStyles();
            MaybeQueueContestedRetreatToast(player);

            DrawFortressPlacementHint();
            DrawDragonPhaseOverlay();

            var hp = _hudLayoutPanel;
            float topBarY = hp.y + HudS(6f);
            float topBarH = HudS(124f);
            float mainHudIconH = HudS(64f);

            var rubGui = GetRubiumGui();
            var vpGui = GetVPGui();
            var hudLabel = GUI.skin.label;

            float iconBtn = HudS(72f);
            float iconY = topBarY + (topBarH - iconBtn) * 0.5f;
            float iconRight = hp.xMax - HudS(12f) - iconBtn * 2f - HudS(10f);

            EnsureTopHudPanelTexture();
            if (_topHudPanelTex != null)
            {
                float tw = Mathf.Max(1f, (float)_topHudPanelTex.width);
                float th = (float)_topHudPanelTex.height;
                float naturalH = Screen.width * (th / tw);
                float topHudH = Mathf.Max(naturalH, topBarY + topBarH);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, topHudH), _topHudPanelTex, ScaleMode.StretchToFill);
            }

            // Top strip: banner tinted by current player's color; rubium + VP left; turn/player centered between VP and icons (phase is on the bottom ribbon).
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
            float resLineH = Mathf.Max(HudS(44f), mainHudIconH + HudS(2f));
            float rowLeft = hp.x + HudS(12f);
            float rubBoxW = HudS(148f);
            float vpBoxW = HudS(104f);
            var rubNumStyle = new GUIStyle(hudLabel)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            ApplyTileInfoFont(rubNumStyle);
            var lightOnBar = new Color(0.96f, 0.97f, 1f, 1f);
            rubNumStyle.normal.textColor = lightOnBar;
            string rubStr = player.Rubium.ToString();
            string vpStr = player.VictoryPoints.ToString();
            string midText = $"T{Game.TurnNumber}  ·  P{player.PlayerIndex + 1}";
            int statMax = GameUiScale.ImGuiScaledFont(26f, 20, 68);
            rubNumStyle.fontSize = GameUiScale.ComputeBestFitFontSize(rubNumStyle, rubStr, rubBoxW, resLineH, 18, statMax,
                false);
            var vpNumStyle = new GUIStyle(rubNumStyle);
            vpNumStyle.fontSize =
                GameUiScale.ComputeBestFitFontSize(vpNumStyle, vpStr, vpBoxW, resLineH, 14, statMax, false);
            float rubNumW = Mathf.Max(HudS(56f), rubNumStyle.CalcSize(new GUIContent(rubStr)).x);
            float vpNumW = Mathf.Max(HudS(56f), vpNumStyle.CalcSize(new GUIContent(vpStr)).x);
            float gapRes = HudS(12f);
            float gapBeforeMid = HudS(10f);
            float rxRes = rowLeft;
            if (!rubGui.IsEmpty)
                rxRes += rubGui.Draw(rxRes, ly, mainHudIconH) + HudS(6f);
            GUI.Label(new Rect(rxRes, ly - HudS(2f), rubBoxW, resLineH), rubStr, rubNumStyle);
            rxRes += rubNumW + gapRes;
            if (!vpGui.IsEmpty)
                rxRes += vpGui.Draw(rxRes, ly, mainHudIconH) + HudS(6f);
            GUI.Label(new Rect(rxRes, ly - HudS(2f), vpBoxW, resLineH), vpStr, vpNumStyle);
            rxRes += vpNumW;
            float midLeft = rxRes + gapBeforeMid;
            float midRight = iconRight - HudS(8f);
            float midW = Mathf.Max(HudS(48f), midRight - midLeft);
            var midCenterStyle = new GUIStyle(rubNumStyle) { alignment = TextAnchor.MiddleCenter };
            ApplyTileInfoFont(midCenterStyle);
            midCenterStyle.normal.textColor = lightOnBar;
            midCenterStyle.fontSize =
                GameUiScale.ComputeBestFitFontSize(midCenterStyle, midText, midW, resLineH, 16, statMax, false);
            GUI.Label(new Rect(midLeft, ly - HudS(2f), midW, resLineH), midText, midCenterStyle);
            bool blockTopIcons = BlocksGameplayHudInteractives();
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
                        fontSize = Mathf.Max(18, Mathf.RoundToInt(44f * _hudFontScale)),
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
                        fontSize = Mathf.Max(18, Mathf.RoundToInt(44f * _hudFontScale)),
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
                ApplyTileInfoFont(bodyStyle);
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
                ApplyTileInfoFont(battleLogStyle);
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
                dragonReserveBottom = tallDragonPanel ? HudS(200f) : 0f;
            }

            float reserveBottom = _hudCardBarHeight + _hudPhaseRibbonHeight + HudS(24f) + dragonReserveBottom;
            topY = Mathf.Min(topY, Mathf.Max(hp.y + HudS(60f), hp.yMax - reserveBottom));

            var dragonPhase = Game.DragonPhase;
            bool dragonSkipButton = dragonPhase != null && Game.CanLocalPlayerActFor(dragonPhase.Player) &&
                dragonPhase.PendingHit == null;
            bool blockEndTurn = Game.BattlePhaseBlockingPlay || !Game.CanLocalPlayerActNow() || handPileModalOpen;
            if (dragonPhase != null && !dragonSkipButton)
                blockEndTurn = true;

            bool canOpenHexDetailModal = InputController != null && InputController.SelectedTile != null;
            if (Game.BattlePhaseBlockingPlay || Game.DragonPhase != null || handPileModalOpen)
                canOpenHexDetailModal = false;

            if (_showCenterBuyModal && !canOpenHexDetailModal)
                _showCenterBuyModal = false;

            bool blockUnderlyingHud = BlocksGameplayHudInteractives();
            bool hudInteractivesPrevEnabled = GUI.enabled;
            if (blockUnderlyingHud)
                GUI.enabled = false;

            DrawCardsPileButtonLeft();
            DrawBottomCardHand(player);
            DrawPhaseRibbon(player);
            DrawEndTurnAdvanceButton(hp, player, reserveBottom, dragonSkipButton, blockEndTurn, handPileModalOpen);

            if (blockUnderlyingHud)
                GUI.enabled = hudInteractivesPrevEnabled;

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

            DrawOnlineConnectionBanner();
        }

        void DrawOnlineConnectionBanner()
        {
            if (!NexusSession.IsOnline || !NexusConnectionMonitor.IsMonitoringMatch)
                return;

            var phase = NexusConnectionMonitor.Phase;
            if (phase == NexusConnectionMonitor.ConnectionPhase.Connected)
                return;

            float s = MainHudUiScale();
            string message = NexusConnectionMonitor.StatusMessage;
            if (string.IsNullOrEmpty(message))
            {
                message = phase switch
                {
                    NexusConnectionMonitor.ConnectionPhase.Reconnecting => "Reconnecting…",
                    NexusConnectionMonitor.ConnectionPhase.OpponentDisconnected =>
                        "Opponent disconnected. Waiting for them to reconnect…",
                    NexusConnectionMonitor.ConnectionPhase.RoomClosed => "The room was closed.",
                    _ => "Connection lost."
                };
            }

            var dim = new Color(0f, 0f, 0f, 0.72f);
            Color prev = GUI.color;
            GUI.color = dim;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture,
                ScaleMode.StretchToFill);
            GUI.color = prev;

            float panelW = Mathf.Min(Screen.width * 0.88f, HudS(520f));
            float panelH = HudS(220f);
            var panel = new Rect((Screen.width - panelW) * 0.5f, (Screen.height - panelH) * 0.38f, panelW, panelH);
            GUI.Box(panel, GUIContent.none);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(18, Mathf.RoundToInt(24f * s)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter,
                wordWrap = true
            };
            ApplyTileInfoFont(titleStyle);

            var bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(14, Mathf.RoundToInt(18f * s)),
                alignment = TextAnchor.UpperCenter,
                wordWrap = true
            };
            ApplyTileInfoFont(bodyStyle);

            string title = phase switch
            {
                NexusConnectionMonitor.ConnectionPhase.Reconnecting => "Reconnecting",
                NexusConnectionMonitor.ConnectionPhase.OpponentDisconnected => "Player Disconnected",
                NexusConnectionMonitor.ConnectionPhase.RoomClosed => "Room Closed",
                _ => "Connection Lost"
            };

            float y = panel.y + HudS(16f);
            GUI.Label(new Rect(panel.x + HudS(16f), y, panel.width - HudS(32f), HudS(36f)), title, titleStyle);
            y += HudS(40f);
            GUI.Label(new Rect(panel.x + HudS(16f), y, panel.width - HudS(32f), HudS(72f)), message, bodyStyle);

            float btnW = (panel.width - HudS(40f)) * 0.5f;
            float btnH = HudS(44f);
            y = panel.yMax - btnH - HudS(16f);
            var btnStyle = new GUIStyle(GUI.skin.button) { fontSize = Mathf.Max(14, Mathf.RoundToInt(18f * s)) };
            ApplyTileInfoFont(btnStyle);

            bool canRetry = phase == NexusConnectionMonitor.ConnectionPhase.Reconnecting ||
                            phase == NexusConnectionMonitor.ConnectionPhase.Disconnected;
            if (canRetry)
            {
                if (GUI.Button(new Rect(panel.x + HudS(16f), y, btnW, btnH), "RETRY", btnStyle))
                    NexusConnectionMonitor.ManualRetryReconnect();
            }

            float leaveX = canRetry ? panel.x + HudS(24f) + btnW : panel.x + (panel.width - btnW) * 0.5f;
            if (GUI.Button(new Rect(leaveX, y, btnW, btnH), "LEAVE GAME", btnStyle))
            {
                var bootstrap = FindObjectOfType<Bootstrap>();
                if (bootstrap != null)
                    bootstrap.ReturnToMainMenu();
            }
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

        /// <summary>IMGUI button with no hover/active/focus box (avoids yellow press flash).</summary>
        GUIStyle TransparentHitButtonStyle()
        {
            if (_transparentHitButtonStyle != null)
                return _transparentHitButtonStyle;
            _transparentHitButtonStyle = new GUIStyle(GUI.skin.label)
            {
                normal = { background = null, textColor = Color.clear },
                hover = { background = null, textColor = Color.clear },
                active = { background = null, textColor = Color.clear },
                focused = { background = null, textColor = Color.clear },
                onNormal = { background = null, textColor = Color.clear },
                onHover = { background = null, textColor = Color.clear },
                onActive = { background = null, textColor = Color.clear },
                onFocused = { background = null, textColor = Color.clear },
                border = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0),
                overflow = new RectOffset(0, 0, 0, 0)
            };
            return _transparentHitButtonStyle;
        }

        /// <summary>Transparent hit target over top-bar sprite buttons (no box chrome).</summary>
        GUIStyle MainBoardTopIconHitStyle() => TransparentHitButtonStyle();

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
            DrawModalPerimeterClickBlockers(panel);
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
            DrawModalPerimeterClickBlockers(panel);
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
            DrawModalPerimeterClickBlockers(r);
            GUI.Window(953, r, _ =>
            {
                var subject = _energizeHelpSubject != null ? _energizeHelpSubject : Game.CurrentPlayer;
                if (subject == null)
                {
                    if (GUILayout.Button("Close", _energizeHelpLayoutButtonStyle))
                        _showMyEnergizeHelp = false;
                    return;
                }

                bool showAiTag = (Game.IsAiControlled(subject) ||
                                  (NexusSession.IsOnline && NexusSession.IsBotSeat(subject.PlayerIndex))) &&
                                 !NexusSession.StealthBotOpponent;
                GUILayout.Label(
                    $"P{subject.PlayerIndex + 1}{(showAiTag ? " (AI)" : "")} - Energize in hand",
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
        float ComputeBottomHudBarY(Rect hp)
        {
            float dragonLift = 0f;
            if (Game.DragonPhase != null)
            {
                var dp = Game.DragonPhase;
                bool tallDragon = Game.IsAiControlled(dp.Player);
                dragonLift = tallDragon ? HudS(200f) : 0f;
            }

            float barY = hp.yMax - dragonLift - _hudCardBarHeight - _hudPhaseRibbonHeight - HudS(12f);
            return Mathf.Max(hp.y + HudS(40f), barY);
        }

        void DrawCardsPileButtonLeft()
        {
            EnsureCardStyles();
            var hp = _hudLayoutPanel;
            float barY = ComputeBottomHudBarY(hp);

            float cardsSize = BottomHudS(68f);
            float gapAboveBar = BottomHudS(8f);
            float bx = hp.x + HudS(12f);
            float cardsY = barY - cardsSize - gapAboveBar;
            var rCards = new Rect(bx, cardsY, cardsSize, cardsSize);
            float pilePad = BottomHudS(6f);
            _lastCardsPileRect = new Rect(rCards.x - pilePad, rCards.y - pilePad, rCards.width + pilePad * 2f,
                rCards.height + pilePad * 2f);

            var pileBtnFallbackStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.Max(10, Mathf.RoundToInt(12f * _hudFontScale * BottomHudInnerLayoutMul)),
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                alignment = TextAnchor.MiddleCenter
            };
            ApplyTileInfoFont(pileBtnFallbackStyle);

            EnsureCardsPileButtonTexture();
            if (_cardsPileButtonTex != null)
            {
                if (Event.current.type == EventType.Repaint)
                    GUI.DrawTexture(rCards, _cardsPileButtonTex, ScaleMode.ScaleToFit, true);

                if (GUI.Button(rCards, GUIContent.none, TransparentHitButtonStyle()))
                {
                    _handPileViewer = _handPileViewer == HandPileViewerKind.Battle
                        ? HandPileViewerKind.None
                        : HandPileViewerKind.Battle;
                }
            }
            else if (GUI.Button(rCards, "🃏", pileBtnFallbackStyle))
            {
                _handPileViewer = _handPileViewer == HandPileViewerKind.Battle
                    ? HandPileViewerKind.None
                    : HandPileViewerKind.Battle;
            }
        }

        void DrawBottomCardHand(PlayerState player)
        {
            EnsureCardStyles();

            var hp = _hudLayoutPanel;
            float barY = ComputeBottomHudBarY(hp);
            _lastCardBarY = barY;

            float barX = hp.x + HudS(2f);
            float barW = hp.width - HudS(4f);
            float phaseY = ComputePhaseRibbonY(barY);
            float stripBottom = phaseY + _hudPhaseRibbonHeight;
            float blockTop = barY - HudS(140f);
            _lastBottomHudInputBlockRect = new Rect(hp.x, blockTop, hp.width, stripBottom - blockTop);
            _lastTilePanelRect = default;
            _lastUnitDetailRect = default;
            float stripTop = Mathf.Min(barY, phaseY);
            EnsureBottomHudPanelTexture();
            if (_bottomHudPanelTex != null)
            {
                float texW = Mathf.Max(1f, (float)_bottomHudPanelTex.width);
                float texH = (float)_bottomHudPanelTex.height;
                float stripH = stripBottom - stripTop;
                float naturalHFullBleed = Screen.width * (texH / texW);
                float bgH = Mathf.Max(stripH, naturalHFullBleed);
                float bgY = stripBottom - bgH;
                GUI.DrawTexture(new Rect(0f, bgY, Screen.width, bgH), _bottomHudPanelTex, ScaleMode.StretchToFill);
            }
            else
            {
                GUI.Box(new Rect(barX, barY, barW, _hudCardBarHeight), "");
            }

            float padL = 0f;
            float padR = 0f;
            float headerH = BottomHudS(4f);
            // Air below the (removed) deck line so pile + tile panel sit lower; does not move barY, mine strip, or phase ribbon.
            float cardBodyTopPad = BottomHudS(14f);
            var selTile = InputController != null ? InputController.SelectedTile : null;
            float bodyH = _hudCardBarHeight - headerH - cardBodyTopPad - BottomHudS(4f);
            float mineLayoutReserve = BottomHudS(46f);
            float mineBarH = selTile != null ? mineLayoutReserve : 0f;

            float innerX = barX + padL;
            float innerW = barW - padL - padR;
            float contentY = barY + headerH + cardBodyTopPad;
            float contentH = bodyH - mineBarH;

            float splitGap = BottomHudS(8f);
            float minTilePanelW = BottomHudS(168f);
            float leftColW = BottomHudS(96f);
            float leftWUsed = leftColW;

            float availTile = Mathf.Max(0f, innerW - leftWUsed - splitGap);
            float rightW = Mathf.Max(minTilePanelW, availTile);
            float rightX = innerX + innerW - rightW;
            if (rightX < innerX + leftWUsed + splitGap)
            {
                rightX = innerX + leftWUsed + splitGap;
                rightW = Mathf.Max(minTilePanelW, innerW - leftWUsed - splitGap);
            }

            float unitDetailBottom = barY + _hudCardBarHeight - mineBarH - BottomHudS(4f);
            var unitDetailRect = new Rect(innerX + BottomHudS(4f), contentY,
                leftColW - BottomHudS(8f), Mathf.Max(BottomHudS(48f), unitDetailBottom - contentY));
            _lastUnitDetailRect = unitDetailRect;
            _lastTilePanelRect = new Rect(rightX, contentY, rightW, contentH);

            DrawBottomTilePanel(rightX, contentY, rightW, contentH, player);
            DrawTilePanelUnitDetail(unitDetailRect, player, selTile);

            if (selTile != null)
            {
                // Rubium strip stays in the left (light grey) column with the cards button — not in the gap/tile panel.
                float myStripY = barY + _hudCardBarHeight - mineBarH - BottomHudS(2f);
                float gapPad = BottomHudS(6f);
                float mineSlotX = innerX + gapPad;
                float mineSlotW = Mathf.Max(0f, leftWUsed - gapPad * 2f);
                var mineStrip = new Rect(mineSlotX, myStripY, mineSlotW, mineBarH - BottomHudS(2f));
                DrawBottomHudMineStrip(mineStrip, player, selTile);
            }
        }

        void DrawBottomHudMineStrip(Rect r, PlayerState player, BoardTile tile)
        {
            if (tile == null || player == null)
                return;
            int myYield = DisplayRubiumPerTurn(tile);
            float bh = BottomHudInnerLayoutMul;
            var numStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(18, Mathf.RoundToInt(26f * _hudFontScale * bh)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                clipping = TextClipping.Overflow
            };
            ApplyTileInfoFont(numStyle);
            numStyle.normal.textColor = new Color(0.96f, 0.97f, 1f, 1f);

            string numStr = myYield > 0 ? myYield.ToString() : "—";
            float iconSz = Mathf.Min(r.height - BottomHudS(6f), BottomHudS(26f));
            iconSz = Mathf.Max(BottomHudS(20f), iconSz);
            float gap = BottomHudS(8f);
            Vector2 numSz = numStyle.CalcSize(new GUIContent(numStr));
            float padEdge = BottomHudS(6f);
            float totalW = iconSz + gap + numSz.x;
            float startX = r.xMax - padEdge - totalW;
            startX = Mathf.Max(r.x + padEdge, startX);
            float iconY = r.y + (r.height - iconSz) * 0.5f;
            var rubGui = GetRubiumGui();
            if (!rubGui.IsEmpty)
                rubGui.Draw(new Rect(startX, iconY, iconSz, iconSz));

            float numX = startX + iconSz + gap;
            float numY = r.y + (r.height - Mathf.Max(BottomHudS(24f), numSz.y)) * 0.5f;
            GUI.Label(new Rect(numX, numY, Mathf.Max(numSz.x, r.xMax - numX - padEdge), BottomHudS(28f)), numStr,
                numStyle);
        }

        /// <summary>Distinct friendly unit types on tile that can still move (drives compact tile-panel layout).</summary>
        static int CountMovableUnitTypesOnTile(PlayerState player, BoardTile popupTile)
        {
            if (player == null || popupTile == null)
                return 0;
            var set = new HashSet<UnitType>();
            foreach (var unit in FindObjectsOfType<UnitInstance>())
            {
                if (unit.Tile == popupTile && unit.Owner == player && !unit.HasMovedThisTurn)
                    set.Add(unit.Definition.Type);
            }

            return set.Count;
        }

        /// <summary>Movable friendly stacks on the selected tile (same rules as tile panel movement rows).</summary>
        static Dictionary<UnitType, int> GetMovableUnitCountsOnTile(PlayerState player, BoardTile tile)
        {
            var counts = new Dictionary<UnitType, int>();
            if (player == null || tile == null)
                return counts;
            foreach (var unit in FindObjectsOfType<UnitInstance>())
            {
                if (unit.Tile == tile && unit.Owner == player && !unit.HasMovedThisTurn)
                {
                    if (!counts.ContainsKey(unit.Definition.Type))
                        counts[unit.Definition.Type] = 0;
                    counts[unit.Definition.Type]++;
                }
            }

            return counts;
        }

        /// <summary>All unit stacks for an owner on a tile (readonly faction tabs).</summary>
        static Dictionary<UnitType, int> GetUnitCountsOnTileForOwner(BoardTile tile, PlayerState owner)
        {
            var counts = new Dictionary<UnitType, int>();
            if (tile == null || owner == null)
                return counts;
            foreach (var unit in FindObjectsOfType<UnitInstance>())
            {
                if (unit.Tile == tile && unit.Owner == owner)
                {
                    if (!counts.ContainsKey(unit.Definition.Type))
                        counts[unit.Definition.Type] = 0;
                    counts[unit.Definition.Type]++;
                }
            }

            return counts;
        }

        void DrawBottomTilePanel(float x, float y, float w, float h, PlayerState player)
        {
            var panel = new Rect(x, y, w, h);
            var popupTile = InputController != null ? InputController.SelectedTile : null;
            float inset = BottomHudS(2f);
            var viewRect = new Rect(panel.x + inset, panel.y + inset, panel.width - inset * 2f,
                panel.height - inset * 2f);
            float innerW = Mathf.Floor(Mathf.Max(1f, viewRect.width - 1f));

            if (popupTile == null)
            {
                var hint = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true,
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Max(10, Mathf.RoundToInt(11f * _hudFontScale * BottomHudInnerLayoutMul)),
                    normal = { textColor = new Color(0.75f, 0.75f, 0.8f) },
                    clipping = TextClipping.Overflow
                };
                ApplyTileInfoFont(hint);
                GUI.Label(viewRect, "Tap the board to select a tile.", hint);
                return;
            }

            GUILayout.BeginArea(viewRect);
            DrawSelectedTilePanelBody(player, popupTile, innerW, viewRect.height);
            GUILayout.EndArea();
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

            float w = Mathf.Min(Screen.width - HudS(24f), HudS(920f));
            // ~2× previous default height (420) for a roomier card list; still clamped to the display.
            float h = Mathf.Min(Screen.height - HudS(80f), HudS(840f));
            var win = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            DrawModalPerimeterClickBlockers(win);

            DrawHandPileModalPanelBackground(win);

            string title = _handPileViewer switch
            {
                HandPileViewerKind.Battle => "Battle Energize",
                HandPileViewerKind.Deploy => "Deployment Energize",
                HandPileViewerKind.Secret => "Secret missions",
                _ => "Hand"
            };

            float titlePadTop = HudS(8f);
            float titleLineH = HudS(46f);
            var titleStyle = new GUIStyle(_cardColumnLabelStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Max(18, Mathf.RoundToInt(22f * _hudFontScale)),
                fontStyle = FontStyle.Bold
            };
            ApplyTileInfoFont(titleStyle);
            GUI.Label(new Rect(win.x, win.y + titlePadTop, win.width, titleLineH), title, titleStyle);

            float handPileTabsDrop = HudS(44f);
            float tabY = win.y + titlePadTop + titleLineH + HudS(14f) + handPileTabsDrop;
            var tabBase = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.Max(10, Mathf.RoundToInt(11f * _hudFontScale)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            ApplyTileInfoFont(tabBase);
            float tabH = Mathf.Max(HudS(28f), tabBase.fontSize + HudS(14f));
            float tabGap = HudS(6f);
            float tabWAvail = (win.width - HudS(20f) - tabGap * 2f) / 3f;
            float tabW = Mathf.Floor(tabWAvail * 0.93f);
            float tabsRowW = tabW * 3f + tabGap * 2f;
            float tabLeft = win.x + (win.width - tabsRowW) * 0.5f;
            var tabSecret = new Rect(tabLeft, tabY, tabW, tabH);
            var tabDeploy = new Rect(tabSecret.xMax + tabGap, tabY, tabW, tabH);
            var tabBattle = new Rect(tabDeploy.xMax + tabGap, tabY, tabW, tabH);

            void SetHandPileTabTextColors(GUIStyle st)
            {
                st.normal.textColor = Color.white;
                st.hover.textColor = Color.white;
                st.active.textColor = Color.white;
                st.focused.textColor = Color.white;
                st.onNormal.textColor = Color.white;
                st.onHover.textColor = Color.white;
                st.onActive.textColor = Color.white;
                st.onFocused.textColor = Color.white;
            }

            var tabSecretStyle = new GUIStyle(tabBase);
            SetHandPileTabTextColors(tabSecretStyle);
            if (GUI.Button(tabSecret, "Missions", tabSecretStyle))
                _handPileViewer = HandPileViewerKind.Secret;

            if (!forcingSecretOverdraw)
            {
                var tabDeployStyle = new GUIStyle(tabBase);
                SetHandPileTabTextColors(tabDeployStyle);
                if (GUI.Button(tabDeploy, "Deployment", tabDeployStyle))
                    _handPileViewer = HandPileViewerKind.Deploy;

                var tabBattleStyle = new GUIStyle(tabBase);
                SetHandPileTabTextColors(tabBattleStyle);
                if (GUI.Button(tabBattle, "Battle", tabBattleStyle))
                    _handPileViewer = HandPileViewerKind.Battle;
            }
            else
            {
                GUI.enabled = false;
                var tabDeployDis = new GUIStyle(tabBase);
                SetHandPileTabTextColors(tabDeployDis);
                GUI.Button(tabDeploy, "Deployment", tabDeployDis);
                var tabBattleDis = new GUIStyle(tabBase);
                SetHandPileTabTextColors(tabBattleDis);
                GUI.Button(tabBattle, "Battle", tabBattleDis);
                GUI.enabled = true;
            }

            float closeW = HudS(132f);
            float closeH = HudS(46f);
            float closeBottomPad = HudS(14f);
            float closeY = win.yMax - closeBottomPad - closeH;
            float contentTop = tabY + tabH + HudS(10f);
            float contentBottom = closeY - HudS(10f);
            float contentH = Mathf.Max(HudS(48f), contentBottom - contentTop);
            var content = new Rect(win.x + HudS(10f), contentTop, win.width - HudS(20f), contentH);

            if (_handPileViewer == HandPileViewerKind.Battle)
                DrawHandPileModalBattle(content, player);
            else if (_handPileViewer == HandPileViewerKind.Deploy)
                DrawHandPileModalDeploy(content, player);
            else if (_handPileViewer == HandPileViewerKind.Secret)
                DrawHandPileModalSecret(content, player, forcingSecretOverdraw);

            var closePileStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.Max(15, Mathf.RoundToInt(17f * _hudFontScale)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            ApplyTileInfoFont(closePileStyle);
            if (!forcingSecretOverdraw)
            {
                if (GUI.Button(new Rect(win.x + (win.width - closeW) * 0.5f, closeY, closeW, closeH), "Close",
                        closePileStyle))
                {
                    if (Game != null)
                        Game.CancelFortressPlacement();
                    _handPileViewer = HandPileViewerKind.None;
                }
            }
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

            var panel = GameUiScale.GetFullscreenModalStylePanelGuiRect();
            DrawModalPerimeterClickBlockers(panel);
            DrawTileInfoModalBackground(panel);

            float scale = GameUiScale.TileInfoModalPanelScale(panel);
            float S(float designUnits) => designUnits * scale;

            float headerH = S(64f);
            float topGapBelowHeader = S(10f);
            float insetX = S(38f);
            float closeW = HudS(176f);
            float closeH = HudS(56f);
            float closeBottomPad = HudS(14f);
            float bottomReserve = Mathf.Max(S(72f), closeH + closeBottomPad + HudS(8f));
            float contentLeft = panel.x + insetX;
            float contentWidth = panel.width - insetX * 2f;
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

            int deployGrp = player.DeployEnergize == null ? 0 : player.DeployEnergize.GroupBy(x => x).Count();
            float nameBoxH = S(188f);
            float shopIconSz = S(136f);
            float iconRowH = 0f;
            float costGap = 0f;
            float rowGap = 0f;
            const int shopColumns = 3;
            float rowStride = nameBoxH + costGap + iconRowH + rowGap;
            float buyH = rowStride * 2f;
            float energizeH = deployGrp > 0 ? S(34f) : 0f;

            float occupyingLabelH = S(36f);
            float creatureRowH = S(124f);
            float creatureRowGap = S(16f);
            float factionHdrH = Mathf.Max(S(22f), S(18f));
            float factionAfterHdrPad = S(4f);
            float factionAfterGridPad = S(10f);
            var ownersOnTile = GetPlayersWithUnitsOnTileOrdered(sel, player);
            float creatureBlock = occupyingLabelH + S(4f);
            if (ownersOnTile.Count <= 1)
                creatureBlock += creatureRowH;
            else
            {
                // Matches DrawHexModalCreatureGrid layout: P label + Space(4*scale) + grid + Space(10*scale) per owner.
                float perFaction = factionHdrH + factionAfterHdrPad + creatureRowH + factionAfterGridPad;
                creatureBlock += ownersOnTile.Count * perFaction;
            }

            float shopBlock = 0f;
            if (showShop)
                shopBlock = S(14f) + S(18f) + buyH + S(8f) + energizeH + S(8f);

            float sepAfterFixed = 0f;
            float minScrollBody = S(100f);
            float gapAfterScrollHeader = S(1f);
            float bodyBelowHeader = panel.height - headerH - topGapBelowHeader - bottomReserve;
            float fixedTopH = Mathf.Max(S(220f), TileInfoFixedRowMinHeight(contentWidth, panel, scale));
            float scrollNeed = sepAfterFixed + gapAfterScrollHeader + minScrollBody;
            if (fixedTopH + scrollNeed > bodyBelowHeader)
                fixedTopH = Mathf.Max(S(180f), bodyBelowHeader - scrollNeed);

            // Nudge hex + tile meta down (clear top safe area); shrink fixed band slightly so layout still fits.
            float fixedRowNudgeDown = S(14f);
            fixedTopH = Mathf.Max(S(152f), fixedTopH - fixedRowNudgeDown);
            var fixedRow = new Rect(contentLeft, panel.y + headerH + topGapBelowHeader + fixedRowNudgeDown,
                contentWidth, fixedTopH);
            DrawHexModalTopRow(fixedRow, panel, player, sel, scale);

            float scrollTop = fixedRow.yMax + sepAfterFixed + gapAfterScrollHeader;
            var scrollRect = new Rect(contentLeft, scrollTop, contentWidth, panel.yMax - bottomReserve - scrollTop);
            // Minimal air above “Occupying forces” so it sits close under the tile meta strip.
            float leadingPadOccupying = showShop ? S(2f) : 0f;
            float scrollContentH = leadingPadOccupying + creatureBlock + shopBlock + S(8f);
            // Keep modest end padding so the Close button never clips content; avoid huge fake height that forces scrolling.
            float scrollBottomPad = S(28f);
            scrollContentH += scrollBottomPad;
            // No visible scrollbars — full width minus tiny slop to avoid horizontal drift.
            float cw = Mathf.Floor(Mathf.Max(S(100f), scrollRect.width - 2f));

            EnsureTileInfoScrollViewTransparentStyle();
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
            occHdr.margin = new RectOffset(0, 0, 0, 0);
            occHdr.padding = new RectOffset(0, 0, 0, 0);
            float occForcesMaxW = Mathf.Min(S(680f), cw * 0.99f);
            float occForcesW = Mathf.Floor(Mathf.Min(occForcesMaxW, cw));
            GUILayout.Space(leadingPadOccupying);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical(GUILayout.Width(occForcesW));
            GUILayout.Label("Occupying forces", occHdr);
            GUILayout.Space(S(2f));
            DrawHexModalCreatureGrid2Rows3Cols(sel, player, occForcesW, creatureRowH, creatureRowGap, scale);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (showShop)
            {
                GUILayout.Space(S(10f));
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
                GUILayout.Space(S(6f));
                Rect gridR = GUILayoutUtility.GetRect(cw, buyH);
                int depCardFont = TileInfoScaledFont(15f, scale, 10);
                DrawBuyUnitGrid(gridR.x, gridR.y, gridR.width, shopColumns, nameBoxH, shopIconSz, iconRowH, costGap,
                    rowGap, depCardFont, true, drawCardChrome: false, uiScale: scale);

                if (deployGrp > 0)
                {
                    GUILayout.Space(S(6f));
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

            var closeTileStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.Max(15, Mathf.RoundToInt(17f * _hudFontScale)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            ApplyTileInfoFont(closeTileStyle);
            float closeY = panel.yMax - closeBottomPad - closeH;
            if (GUI.Button(new Rect(panel.x + (panel.width - closeW) * 0.5f, closeY, closeW, closeH), "Close",
                    closeTileStyle))
                _showCenterBuyModal = false;
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

            // No underlay tint — keep the frame art transparent regions clear.
            _tileInfoScreenBg.Draw(panel);
        }

        void DrawHandPileModalPanelBackground(Rect panel)
        {
            if (!_cardScreenTried)
            {
                _cardScreenTried = true;
                _cardScreenBg = NexusGuiArt.LoadCardScreenBackground();
            }

            if (_cardScreenBg.IsEmpty)
            {
                DrawTintedRect(panel, new Color(0.06f, 0.06f, 0.1f, 0.96f));
                return;
            }

            // No underlay tint — otherwise it fills transparent regions in the frame art (solid dark slab behind cards).
            _cardScreenBg.Draw(panel);
        }

        void DrawCasualtyModalPanelBackground(Rect panel)
        {
            if (!_casualtyScreenTried)
            {
                _casualtyScreenTried = true;
                _casualtyScreenBg = NexusGuiArt.LoadCasualtyScreenBackground();
            }

            if (_casualtyScreenBg.IsEmpty)
            {
                DrawTintedRect(panel, new Color(0.06f, 0.06f, 0.1f, 0.96f));
                return;
            }

            _casualtyScreenBg.Draw(panel);
        }

        /// <summary>Full-bleed battle frame under the dim — stretched to exactly 100% of screen width and height.</summary>
        void DrawBattleScreenModalBackground()
        {
            if (!_battleScreenTried)
            {
                _battleScreenTried = true;
                _battleScreenBg = NexusGuiArt.LoadBattleScreenBackground();
            }

            var full = GameUiScale.GetFullBleedScreenGuiRect();
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
            float headerH = S(92f);
            var baseBar = new Color(0.04f, 0.06f, 0.2f, 0.96f);
            Color pc = picker.Color;
            var tint = Color.Lerp(baseBar, new Color(pc.r, pc.g, pc.b, 1f), 0.25f);
            tint.a = 0.98f;
            var headerRect = new Rect(panel.x, panel.y, panel.width, headerH);
            Color prevGui = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(headerRect, Texture2D.whiteTexture);
            GUI.color = prevGui;

            int titleFont = TileInfoScaledFont(33f, panelScale, 18);
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = titleFont,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = false,
                normal = { textColor = new Color(0.96f, 0.98f, 1f, 1f) }
            };
            ApplyTileInfoFont(titleStyle);
            var titleRect = new Rect(panel.x, panel.y + S(14f), panel.width, S(58f));
            // Faux stroke so the pixel font remains readable on bright/blue frame art.
            Color prevTitleColor = titleStyle.normal.textColor;
            titleStyle.normal.textColor = new Color(0.04f, 0.05f, 0.12f, 1f);
            GUI.Label(new Rect(titleRect.x - S(1.5f), titleRect.y, titleRect.width, titleRect.height), "SELECT CASUALTIES", titleStyle);
            GUI.Label(new Rect(titleRect.x + S(1.5f), titleRect.y, titleRect.width, titleRect.height), "SELECT CASUALTIES", titleStyle);
            GUI.Label(new Rect(titleRect.x, titleRect.y - S(1.5f), titleRect.width, titleRect.height), "SELECT CASUALTIES", titleStyle);
            GUI.Label(new Rect(titleRect.x, titleRect.y + S(1.5f), titleRect.width, titleRect.height), "SELECT CASUALTIES", titleStyle);
            titleStyle.normal.textColor = prevTitleColor;
            GUI.Label(titleRect, "SELECT CASUALTIES", titleStyle);

            float insetX = S(38f);
            float insetBottom = S(36f);
            float topPad = S(22f);
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

            var panel = GameUiScale.GetBattleCasualtyModalPanelGuiRect();
            DrawModalPerimeterClickBlockers(panel);
            DrawCasualtyModalPanelBackground(panel);
            Rect content = DrawCasualtySelectionModalHeader(dp.Player, panel, out float panelScale);
            float S(float d) => d * panelScale;

            _battlePanelContentWidth = content.width;
            _battlePanelScaleCached = panelScale;
            _battleHudUiScale = BattleHudUiScale(panel);
            _battleFontReferencePanel = content;
            ApplyBattleHudScaledStyles();
            EnsureBattleHudStyles();

            GUILayout.BeginArea(content);
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Space(BattleS(6f));

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

            var summaryStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = Mathf.Max(12, Mathf.RoundToInt(13f * _hudFontScale)),
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                normal = { textColor = new Color(0.94f, 0.96f, 1f, 1f) }
            };
            ApplyTileInfoFont(summaryStyle);
            string strikeTitle = dp.PendingHit.FortressSourceHex != null
                ? $"FORTRESS BREATH  ·  Roll {dp.PendingHit.LastRoll}  ·  Tap a target"
                : $"DRAGON STRIKE  ·  Roll {dp.PendingHit.LastRoll}  ·  Tap a target";
            GUILayout.Label(strikeTitle, summaryStyle);

            var reqStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = Mathf.Max(12, Mathf.RoundToInt(13f * _hudFontScale)),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.9f, 0.35f, 1f) }
            };
            ApplyTileInfoFont(reqStyle);
            GUILayout.Label("Choose 1 casualty unit to remove.", reqStyle);
            GUILayout.Space(S(12f));

            var victimGroups = dp.PendingEnemies
                .Where(v => v != null && v.Owner != null && v.Definition != null)
                .GroupBy(v => (owner: v.Owner, type: v.Definition.Type))
                .OrderBy(g => g.Key.owner.PlayerIndex)
                .ThenBy(g => g.Key.type.ToString())
                .ToList();

            DrawDragonVictimChoiceGrid(victimGroups);

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        /// <summary>Dragon casualty picker: choose from available enemy unit groups with icons (not text-only buttons).</summary>
        void DrawDragonVictimChoiceGrid(List<IGrouping<(PlayerState owner, UnitType type), UnitInstance>> victimGroups)
        {
            if (victimGroups == null || victimGroups.Count == 0)
            {
                var empty = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fontSize = Mathf.Max(11, Mathf.RoundToInt(12f * _hudFontScale)),
                    normal = { textColor = new Color(0.9f, 0.92f, 0.96f, 1f) }
                };
                ApplyTileInfoFont(empty);
                GUILayout.Label("No valid targets.", empty);
                return;
            }

            int cols = 2;
            float gap = BattleS(8f);
            float cellH = BattleS(74f);
            float panelW = _battlePanelContentWidth > 1e-4f ? _battlePanelContentWidth : GameUiScale.GetPaddedModalPanelGuiRect().width;
            float cellW = Mathf.Max(BattleS(120f), (panelW - gap * (cols - 1)) / cols);

            for (int i = 0; i < victimGroups.Count; i += cols)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                for (int c = 0; c < cols; c++)
                {
                    int idx = i + c;
                    if (idx >= victimGroups.Count)
                    {
                        GUILayout.Space(cellW);
                        continue;
                    }

                    var grp = victimGroups[idx];
                    var sample = grp.FirstOrDefault();
                    int count = grp.Count();
                    var owner = grp.Key.owner;
                    var type = grp.Key.type;

                    Rect r = GUILayoutUtility.GetRect(cellW, cellH, GUILayout.Width(cellW), GUILayout.Height(cellH));
                    DrawTintedRect(r, new Color(0.08f, 0.1f, 0.16f, 0.96f));
                    DrawOutlineRect(r, new Color(0.5f, 0.54f, 0.62f, 0.9f), BattleS(1.5f));

                    float iconSz = Mathf.Clamp(r.height - BattleS(16f), BattleS(34f), BattleS(54f));
                    var iconR = new Rect(r.x + BattleS(6f), r.y + (r.height - iconSz) * 0.5f, iconSz, iconSz);
                    DrawUnitMiniIcon(iconR, type, TintedIconOwnerForUnitOnSide(type, owner));

                    var textStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        fontStyle = FontStyle.Bold,
                        fontSize = Mathf.Max(11, Mathf.RoundToInt(13f * _hudFontScale)),
                        clipping = TextClipping.Clip,
                        normal = { textColor = new Color(0.96f, 0.98f, 1f, 1f) }
                    };
                    ApplyTileInfoFont(textStyle);
                    float tx = iconR.xMax + BattleS(8f);
                    var tRect = new Rect(tx, r.y + BattleS(8f), r.xMax - tx - BattleS(8f), r.height - BattleS(16f));
                    GUI.Label(tRect, $"{UnitUiName(type)}  ·  P{owner.PlayerIndex + 1}", textStyle);

                    if (count > 1)
                    {
                        var badgeStyle = new GUIStyle(GUI.skin.label)
                        {
                            alignment = TextAnchor.UpperRight,
                            fontStyle = FontStyle.Bold,
                            fontSize = Mathf.Max(10, Mathf.RoundToInt(11f * _hudFontScale)),
                            normal = { textColor = new Color(1f, 0.88f, 0.35f, 1f) }
                        };
                        ApplyTileInfoFont(badgeStyle);
                        GUI.Label(new Rect(r.x + BattleS(6f), r.y + BattleS(4f), r.width - BattleS(10f), BattleS(18f)),
                            "x" + count, badgeStyle);
                    }

                    if (sample != null && GUI.Button(r, GUIContent.none, GUIStyle.none))
                        Game.DragonStrikeChooseVictim(sample);

                    if (c + 1 < cols)
                        GUILayout.Space(gap);
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                if (i + cols < victimGroups.Count)
                    GUILayout.Space(gap);
            }
        }

        void DrawHexModalTopRow(Rect row, Rect panel, PlayerState player, BoardTile tile, float scale)
        {
            if (tile == null)
                return;

            float S(float d) => d * scale;
            EnsureTileInfoNameBanner();
            float bannerH = _tileInfoNameBanner.IsEmpty
                ? 0f
                : Mathf.Clamp(panel.width / _tileInfoNameBanner.AspectRatio, S(36f), S(96f));

            TileDefinition def = Game.Config != null ? Game.Config.GetTile(tile.Type) : null;
            Color fill = def != null ? def.Color : new Color(0.45f, 0.45f, 0.48f);

            // Lower band: tile name + owner on full-width banner below hex art.
            float metaBandH = Mathf.Max(S(132f), bannerH + S(16f), row.height * 0.30f);
            metaBandH = Mathf.Min(metaBandH, row.height * 0.55f);
            float hexBandH = row.height - metaBandH;
            hexBandH = Mathf.Max(hexBandH, S(96f));

            float rubRightPad = S(8f);
            float rubW = Mathf.Clamp(row.width * 0.26f, S(120f), S(220f));
            var hexRowRect = new Rect(row.x, row.y, row.width, hexBandH);
            var leftBand = new Rect(hexRowRect.x + rubRightPad, hexRowRect.y, rubW, hexRowRect.height);
            var rightBand = new Rect(hexRowRect.xMax - rubW - rubRightPad, hexRowRect.y, rubW, hexRowRect.height);

            float maxHex = hexBandH - S(20f);
            float hexSide = Mathf.Min(row.width * 0.78f, maxHex);
            hexSide *= 0.92f;
            hexSide = Mathf.Clamp(hexSide, S(144f), S(480f));
            float hexTop = hexRowRect.y + (hexBandH - hexSide) * 0.17f + S(2f);
            hexTop = Mathf.Min(hexTop, hexRowRect.yMax - hexSide - S(4f));
            var hexR = new Rect(row.x + (row.width - hexSide) * 0.5f, hexTop, hexSide, hexSide);
            DrawModalHexPreview(hexR, fill);
            DrawTileBoardOverlayOnModalHex(hexR, tile);

            string tileName = TileTypeDisplayName(tile.Type);
            string meta = HexModalOwnerMetaLine(player, tile);
            bool contested = string.Equals(meta, "CONTESTED", StringComparison.OrdinalIgnoreCase);

            int titleSz = TileInfoScaledFont(29f, scale, 14);
            int statusSz = TileInfoScaledFont(19f, scale, 11);
            var tileTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = titleSz,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter,
                wordWrap = true,
                richText = false,
                clipping = TextClipping.Overflow,
                normal = { textColor = new Color(0.93f, 0.88f, 1f, 1f) }
            };
            var statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = statusSz,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter,
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
            tileTitleStyle.margin = new RectOffset(0, 0, 0, 0);
            tileTitleStyle.padding = new RectOffset(0, 0, 0, 0);
            statusStyle.margin = new RectOffset(0, 0, 0, 0);
            statusStyle.padding = new RectOffset(0, 0, 0, 0);
            contestedStyle.margin = new RectOffset(0, 0, 0, 0);
            contestedStyle.padding = new RectOffset(0, 0, 0, 0);

            float hexBottom = hexTop + hexSide;
            float gapTileToName = S(14f) * 1.1f;
            float metaYFromLayout = row.y + hexBandH;
            float metaYFromHex = hexBottom + gapTileToName;
            float metaY = Mathf.Min(metaYFromLayout, metaYFromHex);
            float metaBandActual = Mathf.Max(0f, row.yMax - metaY);
            var metaRectFull = new Rect(row.x + S(8f), metaY, row.width - S(16f), metaBandActual);
            float nameW = metaRectFull.width;
            float metaTextTopPad = S(16f);
            float metaBottomPad = S(12f);
            float innerH = Mathf.Max(0f, metaBandActual - metaTextTopPad - metaBottomPad);
            float nameH = tileTitleStyle.CalcHeight(new GUIContent(tileName), nameW);
            float nameSizeY = tileTitleStyle.CalcSize(new GUIContent(tileName)).y;
            if (nameH <= nameSizeY * 1.35f)
                nameH = nameSizeY;
            nameH = Mathf.Min(nameH, Mathf.Max(S(22f), innerH - S(18f)));

            float statusHDraw = contested
                ? contestedStyle.CalcHeight(new GUIContent("CONTESTED"), nameW)
                : statusStyle.CalcHeight(new GUIContent(meta), nameW);
            float statusTight = contested
                ? contestedStyle.CalcSize(new GUIContent("CONTESTED")).y
                : statusStyle.CalcSize(new GUIContent(meta)).y;
            statusHDraw = Mathf.Max(statusTight, Mathf.Min(statusHDraw, statusTight * 2f));

            float textBlockH = nameH + statusHDraw;
            float bannerPadV = S(10f);
            float bannerDrawH = bannerH > 0f ? Mathf.Max(bannerH, textBlockH + bannerPadV * 2f) : 0f;
            float bannerY = bannerDrawH > 0f
                ? metaY + Mathf.Max(S(6f), (metaBandActual - bannerDrawH) * 0.5f)
                : metaY;
            var bannerR = new Rect(panel.x, bannerY, panel.width, bannerDrawH);

            if (bannerH > 0f && Event.current.type == EventType.Repaint)
                _tileInfoNameBanner.DrawStretchFill(bannerR);

            float textY = bannerDrawH > 0f
                ? bannerY + (bannerDrawH - textBlockH) * 0.5f
                : metaY + metaTextTopPad;
            var nameRect = new Rect(metaRectFull.x, textY, nameW, nameH);
            var statusRect = new Rect(metaRectFull.x, nameRect.yMax, nameW, statusHDraw);

            GUI.Label(nameRect, tileName, tileTitleStyle);
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

        static bool TileHasRefineryOverlay(BoardTile tile)
        {
            if (tile == null)
                return false;
            if (tile.Type == TileType.HomeBase)
                return true;
            return tile.View != null && tile.View.transform.Find("Refinery") != null;
        }

        static bool TryResolveTileBoardOverlay(BoardTile tile, out NexusGuiImage image, out Color tint)
        {
            image = default;
            tint = Color.white;
            if (tile == null || !TileHasRefineryOverlay(tile))
                return false;

            image = NexusGuiArt.LoadRefinery();
            if (image.IsEmpty)
                return false;

            tint = new Color(1f, 1f, 1f, NexusGuiArt.RefineryOverlayAlpha);
            return true;
        }

        void DrawTileBoardOverlayOnModalHex(Rect hexR, BoardTile tile)
        {
            if (!TryResolveTileBoardOverlay(tile, out NexusGuiImage image, out Color tint) || image.IsEmpty)
                return;

            float targetW = hexR.width * 0.85f;
            float aspect = image.AspectRatio;
            float w = targetW;
            float h = w / aspect;
            float maxH = hexR.height * 0.85f;
            if (h > maxH)
            {
                h = maxH;
                w = h * aspect;
            }

            var overlayR = new Rect(hexR.x + (hexR.width - w) * 0.5f, hexR.y + (hexR.height - h) * 0.5f, w, h);
            Color prev = GUI.color;
            GUI.color = tint;
            image.DrawAspectFit(overlayR);
            GUI.color = prev;
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

        /// <summary>Distinct players with at least one unit on the tile; <paramref name="prioritizeFirst"/> listed first.</summary>
        static List<PlayerState> GetPlayersWithUnitsOnTileOrdered(BoardTile tile, PlayerState prioritizeFirst = null)
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

            result.Sort((a, b) =>
            {
                if (prioritizeFirst != null)
                {
                    if (a.PlayerIndex == prioritizeFirst.PlayerIndex)
                        return -1;
                    if (b.PlayerIndex == prioritizeFirst.PlayerIndex)
                        return 1;
                }

                return a.PlayerIndex.CompareTo(b.PlayerIndex);
            });
            return result;
        }

        static int DefaultTilePanelViewPlayerIndex(List<PlayerState> ownersOnTile, PlayerState currentPlayer)
        {
            if (currentPlayer != null && ownersOnTile.Exists(o => o.PlayerIndex == currentPlayer.PlayerIndex))
                return currentPlayer.PlayerIndex;
            if (ownersOnTile.Count > 0)
                return ownersOnTile[0].PlayerIndex;
            return currentPlayer != null ? currentPlayer.PlayerIndex : 0;
        }

        void DrawHexModalCreatureGrid2Rows3Cols(BoardTile tile, PlayerState hudPlayer, float width, float rowH,
            float rowGap, float scale)
        {
            var ownersOrdered = GetPlayersWithUnitsOnTileOrdered(tile, hudPlayer);
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
            float _rowGap, PlayerState tintForCells, float scale)
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

            const int cols = 6;
            float gap = Mathf.Max(6f, 10f * scale);
            float innerW = Mathf.Floor(width);
            float cellW = Mathf.Floor((innerW - gap * (cols - 1)) / cols);
            float rowUsedW = cellW * cols + gap * (cols - 1);
            float rowSidePad = Mathf.Max(0f, (innerW - rowUsedW) * 0.5f);
            PlayerState tintBase = tintForCells ?? hudPlayer;

            GUILayout.BeginHorizontal();
            GUILayout.Space(rowSidePad);
            for (int col = 0; col < cols; col++)
            {
                var ut = types[col];
                int n = countByType[ut];
                if (col > 0)
                    GUILayout.Space(gap);
                Rect cell = GUILayoutUtility.GetRect(cellW, rowH, GUILayout.Width(cellW), GUILayout.Height(rowH));
                DrawHexModalOccupyingForceCell(cell, ut, n, tintBase, scale);
            }

            GUILayout.Space(rowSidePad);
            GUILayout.EndHorizontal();
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
            EnsureHandPileCardFaces();
            float tw = HandPileCardTileW();
            float th = HandPileCardTileH();
            float g = HudS(10f);
            float pad = HudS(4f);
            float rowGap = HudS(12f);

            var battleGroups = player.BattleEnergize.GroupBy(x => x).OrderBy(gr => gr.Key.ToString()).ToList();
            if (battleGroups.Count == 0)
            {
                float px = content.x + (content.width - tw) * 0.5f;
                float py = content.y + (content.height - th) * 0.5f;
                DrawPlaceholderCard(new Rect(px, py, tw, th), _pileBattleCardFace, "No cards");
                return;
            }

            int count = battleGroups.Count;
            int rows = Mathf.Min(2, count);
            int cols = Mathf.CeilToInt(count / (float)rows);

            float cardW = (content.width - pad * 2f - g * (cols - 1)) / Mathf.Max(1, cols);
            cardW = Mathf.Clamp(cardW, HudS(104f), tw);
            float scale = tw > 1e-5f ? cardW / tw : 1f;
            float cardH = th * scale;

            float totalH = rows * cardH + (rows > 1 ? (rows - 1) * rowGap : 0f);
            float yStart = content.y + Mathf.Max(pad, (content.height - totalH) * 0.5f);

            int i = 0;
            for (int row = 0; row < rows && i < count; row++)
            {
                int idx0 = row * cols;
                int cardsInRow = Mathf.Min(cols, count - idx0);
                float rowW = cardsInRow * cardW + (cardsInRow - 1) * g;
                float startX = content.x + (content.width - rowW) * 0.5f;
                float cardY = yStart + row * (cardH + rowGap);
                for (int j = 0; j < cardsInRow; j++)
                {
                    var grp = battleGroups[i++];
                    float x = startX + j * (cardW + g);
                    string full = EnergizeBattleCatalog.GetName(grp.Key);
                    DrawPlayingCard(new Rect(x, cardY, cardW, cardH), _pileBattleCardFace,
                        CardShortTitle(full), CardDetailFromName(full), grp.Count());
                }
            }
        }

        void DrawHandPileModalDeploy(Rect content, PlayerState player)
        {
            EnsureHandPileCardFaces();
            float tw = HandPileCardTileW();
            float th = HandPileCardTileH();
            float g = HudS(10f);
            float pad = HudS(4f);
            float rowGap = HudS(12f);

            var deployGroups = player.DeployEnergize.GroupBy(x => x).OrderBy(gr => gr.Key.ToString()).ToList();
            if (deployGroups.Count == 0)
            {
                float px = content.x + (content.width - tw) * 0.5f;
                float py = content.y + (content.height - th) * 0.5f;
                DrawPlaceholderCard(new Rect(px, py, tw, th), _pileDeployCardFace, "No cards");
                return;
            }

            int count = deployGroups.Count;
            int rows = Mathf.Min(2, count);
            int cols = Mathf.CeilToInt(count / (float)rows);

            float cardW = (content.width - pad * 2f - g * (cols - 1)) / Mathf.Max(1, cols);
            cardW = Mathf.Clamp(cardW, HudS(104f), tw);
            float scale = tw > 1e-5f ? cardW / tw : 1f;
            float cardH = th * scale;

            float totalH = rows * cardH + (rows > 1 ? (rows - 1) * rowGap : 0f);
            float yStart = content.y + Mathf.Max(pad, (content.height - totalH) * 0.5f);

            int i = 0;
            for (int row = 0; row < rows && i < count; row++)
            {
                int idx0 = row * cols;
                int cardsInRow = Mathf.Min(cols, count - idx0);
                float rowW = cardsInRow * cardW + (cardsInRow - 1) * g;
                float startX = content.x + (content.width - rowW) * 0.5f;
                float cardY = yStart + row * (cardH + rowGap);
                for (int j = 0; j < cardsInRow; j++)
                {
                    var grp = deployGroups[i++];
                    float x = startX + j * (cardW + g);
                    string full = EnergizeDeploymentCatalog.GetName(grp.Key);
                    var cardRect = new Rect(x, cardY, cardW, cardH);
                    DrawPlayingCard(cardRect, _pileDeployCardFace,
                        CardShortTitle(full), CardDetailFromName(full), grp.Count());
                    TryHandleDeployPileCardTap(cardRect, player, grp.Key);
                }
            }
        }

        void TryHandleDeployPileCardTap(Rect cardRect, PlayerState player, EnergizeDeploymentId id)
        {
            if (Game == null || player == null || Event.current.type != EventType.MouseUp)
                return;
            if (!cardRect.Contains(Event.current.mousePosition))
                return;
            if (!Game.CanUseDeploymentEnergizeNow())
                return;
            if (player.DeployEnergize == null || !player.DeployEnergize.Contains(id))
                return;

            Event.current.Use();
            if (id == EnergizeDeploymentId.FreeHuman)
            {
                var home = InputController != null ? InputController.SelectedTile : null;
                if (home != null)
                    Game.TryPlayDeploymentEnergize(id, home);
                return;
            }

            if (id == EnergizeDeploymentId.Fortress)
            {
                Game.TryPlayDeploymentEnergize(EnergizeDeploymentId.Fortress, null);
                _handPileViewer = HandPileViewerKind.None;
                return;
            }

            Game.TryPlayDeploymentEnergize(id, null);
        }

        void DrawHandPileModalSecret(Rect content, PlayerState player, bool forcingOverdrawDiscard = false)
        {
            EnsureHandPileCardFaces();
            float tw = HandPileCardTileW();
            float th = HandPileCardTileH();
            float g = HudS(10f);
            float pad = HudS(4f);
            float extraTop = forcingOverdrawDiscard ? HudS(22f) : 0f;
            float discardH = forcingOverdrawDiscard ? HudS(24f) : 0f;
            float rowGap = forcingOverdrawDiscard ? HudS(30f) : HudS(12f);

            if (player.SecretMissions == null || player.SecretMissions.Count == 0)
            {
                float px = content.x + (content.width - tw) * 0.5f;
                float py = content.y + (content.height - th) * 0.5f;
                DrawPlaceholderCard(new Rect(px, py, tw, th), _pileMissionCardFace, "No missions");
                return;
            }

            int count = player.SecretMissions.Count;
            int rows = Mathf.Min(2, count);
            int cols = Mathf.CeilToInt(count / (float)rows);

            float cardW = (content.width - pad * 2f - g * (cols - 1)) / Mathf.Max(1, cols);
            cardW = Mathf.Clamp(cardW, HudS(104f), tw);
            float scale = tw > 1e-5f ? cardW / tw : 1f;
            float cardH = th * scale;

            float stackH = extraTop + rows * cardH + (rows > 1 ? (rows - 1) * (rowGap + discardH) : 0f);
            float secretY0 = content.y + pad + extraTop;
            if (!forcingOverdrawDiscard)
                secretY0 = content.y + pad + extraTop +
                           Mathf.Max(0f, (content.height - pad * 2f - stackH) * 0.5f);

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
                    $"Hand limit reached ({GameController.MaxSecretMissionsInHand}). Choose one card to discard, then draw the pending secret.",
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

            int i = 0;
            for (int row = 0; row < rows && i < count; row++)
            {
                int idx0 = row * cols;
                int cardsInRow = Mathf.Min(cols, count - idx0);
                float rowW = cardsInRow * cardW + (cardsInRow - 1) * g;
                float startX = content.x + (content.width - rowW) * 0.5f;
                float cardY = secretY0 + row * (cardH + rowGap + discardH);
                for (int j = 0; j < cardsInRow; j++)
                {
                    int missionIndex = i++;
                    float x = startX + j * (cardW + g);
                    var s = player.SecretMissions[missionIndex];
                    string full = SecretMissionLabel(s) + " (+" + s.VictoryPoints + " VP)";
                    DrawPlayingCard(new Rect(x, cardY, cardW, cardH), _pileMissionCardFace,
                        "#" + missionIndex + " " + CardShortTitle(full), CardDetailFromName(full), 1);
                    if (forcingOverdrawDiscard)
                    {
                        var discardRect = new Rect(x, cardY + cardH + HudS(4f), cardW, discardH);
                        if (GUI.Button(discardRect, "Discard"))
                            Game.DiscardSecretMissionForPendingDraw(missionIndex);
                    }
                }
            }
        }

        static Vector2 GuiPointerPosition()
        {
            var e = Event.current;
            return new Vector2(e.mousePosition.x, Screen.height - e.mousePosition.y);
        }

        /// <summary>True when a modal/overlay is open — bottom HUD must not register buttons underneath.</summary>
        bool BlocksGameplayHudInteractives()
        {
            if (_showCenterBuyModal || _showQuickRef || _showSettingsMenu || _showMyEnergizeHelp || _showEndGameStats)
                return true;
            if (_handPileViewer != HandPileViewerKind.None)
                return true;
            if (Game != null && Game.SecretMissionOverdraw != null && Game.SecretMissionOverdraw.Waiting)
                return true;

            if (Game != null && Game.Players.Count > 0)
            {
                var player = Game.CurrentPlayer;
                if (player != null && ShouldPaintFullBattleOverlay(player))
                    return true;

                var dp = Game.DragonPhase;
                if (dp != null && dp.PendingHit != null && dp.PendingEnemies != null &&
                    !Game.IsAiControlled(dp.Player))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Invisible IMGUI hit targets around <paramref name="windowRect"/> so clicks on the dimmed backdrop do not
        /// reach controls drawn earlier in the same frame (e.g. cards pile under TILE INFO).
        /// </summary>
        static void DrawModalPerimeterClickBlockers(Rect windowRect)
        {
            float sw = Screen.width;
            float sh = Screen.height;
            var none = GUIStyle.none;
            if (windowRect.x > 0f)
                GUI.Button(new Rect(0f, 0f, windowRect.x, sh), GUIContent.none, none);
            if (windowRect.xMax < sw)
                GUI.Button(new Rect(windowRect.xMax, 0f, Mathf.Max(0f, sw - windowRect.xMax), sh), GUIContent.none, none);
            if (windowRect.y > 0f)
                GUI.Button(new Rect(windowRect.x, 0f, windowRect.width, windowRect.y), GUIContent.none, none);
            if (windowRect.yMax < sh)
                GUI.Button(new Rect(windowRect.x, windowRect.yMax, windowRect.width, Mathf.Max(0f, sh - windowRect.yMax)),
                    GUIContent.none, none);
        }

        bool PointerOverBottomHudInteractives()
        {
            if (Event.current == null)
                return false;
            var gui = GuiPointerPosition();
            if (_lastTilePanelRect.width > 0f && _lastTilePanelRect.Contains(gui))
                return true;
            if (_lastCardsPileRect.width > 0f && _lastCardsPileRect.Contains(gui))
                return true;
            if (_lastUnitDetailRect.width > 0f && _lastUnitDetailRect.Contains(gui))
                return true;
            if (_lastPhaseRibbonRect.width > 0f && _lastPhaseRibbonRect.Contains(gui))
                return true;
            if (_showCenterBuyModal && GetCenterBuyModalPanelGuiRect().Contains(gui))
                return true;
            return false;
        }

        void DrawEndTurnAdvanceButton(Rect hp, PlayerState player, float reserveBottom, bool dragonSkipButton,
            bool blockEndTurn, bool handPileModalOpen)
        {
            bool suppressEndTurn = handPileModalOpen || _showCenterBuyModal || PointerOverBottomHudInteractives();
            GUI.enabled = !blockEndTurn && !suppressEndTurn;

            string endTurnLabel = dragonSkipButton ? DragonBreathSkipLabel() : EndTurnAdvanceLabel(player);
            EnsureEndTurnAdvanceButtonTextures();
            var endTurnVisual = GetEndTurnButtonVisualKind(player, dragonSkipButton);
            Texture2D endTurnBg = GetEndTurnAdvanceButtonTexture(endTurnVisual);

            float btnH = HudS(224f);
            float btnW;
            if (endTurnBg != null)
                btnW = btnH;
            else if (dragonSkipButton)
                btnW = HudS(1120f);
            else
                btnW = HudS(endTurnLabel.Length >= 11 ? 880f : 680f);

            float endTurnX = hp.xMax - btnW - HudS(10f);
            float endTurnY = hp.yMax - reserveBottom - btnH;
            var endTurnRect = new Rect(endTurnX, endTurnY, btnW, btnH);
            float hitInsetX = btnW * 0.14f;
            float hitInsetTop = btnH * 0.42f;
            _lastEndTurnButtonRect = new Rect(endTurnX + hitInsetX, endTurnY + hitInsetTop,
                btnW - hitInsetX * 2f, btnH - hitInsetTop);
            var endTurnHitRect = _lastEndTurnButtonRect;

            Color endTurnGuiPrev = GUI.color;
            bool breatheIdleEndTurn =
                !dragonSkipButton &&
                !blockEndTurn &&
                !suppressEndTurn &&
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
                if (GUI.Button(endTurnHitRect, GUIContent.none, GUIStyle.none))
                {
                    if (dragonSkipButton)
                        Game.SkipAllDragonStrikes();
                    else
                    {
                        NexusGameCommands.RequestEndTurn();
                        _showCenterBuyModal = false;
                    }
                }
            }
            else if (GUI.Button(endTurnHitRect, endTurnLabel))
            {
                if (dragonSkipButton)
                    Game.SkipAllDragonStrikes();
                else
                {
                    NexusGameCommands.RequestEndTurn();
                    _showCenterBuyModal = false;
                }
            }

            GUI.color = endTurnGuiPrev;
            GUI.enabled = true;
        }

        void DrawPhaseRibbon(PlayerState player)
        {
            var hp = _hudLayoutPanel;
            float y = ComputePhaseRibbonY(_lastCardBarY);
            float x = hp.x + HudS(8f);
            float w = hp.width - HudS(16f);
            _lastPhaseRibbonRect = new Rect(x, y, w, _hudPhaseRibbonHeight);
            EnsureBottomHudPanelTexture();
            if (_bottomHudPanelTex == null)
                GUI.Box(new Rect(x, y, w, _hudPhaseRibbonHeight), "");

            string[] phases = { "Draw", "Deploy", "Move", "Battle", "Dragon", "End" };
            string active = ActivePhaseLabel(player);
            float innerPad = HudS(6f);
            float segW = (w - innerPad) / phases.Length;
            float segTop = HudS(2f);
            float segH = _hudPhaseRibbonHeight - HudS(6f);
            float segInnerW = segW - HudS(2f);
            var phaseStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Clip,
                normal = { textColor = Color.white }
            };
            ApplyTileInfoFont(phaseStyle);
            int phaseMax = GameUiScale.ImGuiScaledFont(15f, 11, 30);
            var gc = new GUIContent();
            int phaseFs = phaseMax;
            foreach (var ph in phases)
            {
                gc.text = ph;
                phaseFs = Mathf.Min(phaseFs,
                    GameUiScale.ComputeBestFitFontSize(phaseStyle, gc, segInnerW, segH, 10, phaseMax, false));
            }

            phaseStyle.fontSize = phaseFs;
            for (int i = 0; i < phases.Length; i++)
            {
                var r = new Rect(x + innerPad * 0.5f + segW * i, y + segTop, segInnerW, segH);
                bool on = phases[i] == active;
                var prev = GUI.color;
                GUI.color = on ? new Color(0.95f, 0.78f, 0.18f, 0.95f) : new Color(0.35f, 0.35f, 0.35f, 0.9f);
                GUI.Label(r, phases[i], phaseStyle);
                GUI.color = prev;
            }
        }

        string ActivePhaseLabel(PlayerState player)
        {
            if (Game.IsGameOver)
                return "End";
            if (NexusSession.IsOnline && Game.CurrentPlayer != null &&
                Game.CurrentPlayer.PlayerIndex != NexusSession.LocalPlayerIndex)
                return "Opponent";
            if (Game.DragonPhase != null)
                return "Dragon";
            if (Game.BattlePhaseBlockingPlay || Game.PendingBattleArrangement || Game.ActiveBattleHex != null)
                return "Battle";
            if (_showCenterBuyModal)
                return "Deploy";
            // In this implementation, deployment purchases/cards are available during movement window.
            if (player != null && Game.CanLocalPlayerActNow())
                return "Move";
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
            if (Game == null || player == null || !Game.CanLocalPlayerActFor(player))
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

            var toastInner = new Rect(r.x + HudS(8f), r.y + HudS(4f), r.width - HudS(16f), r.height - HudS(8f));
            const string toastMsg =
                "Contested hexes detected: you can move off them now to avoid forced battles at end turn.";
            var st = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color(0.97f, 0.98f, 1f, alpha) }
            };
            ApplyTileInfoFont(st);
            st.fontSize = GameUiScale.ComputeBestFitFontSize(st, toastMsg, toastInner.width, toastInner.height, 9,
                GameUiScale.ImGuiScaledFont(12f, 10, 22), true);
            GUI.Label(toastInner, toastMsg, st);
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

        void EnsureMoveStackPlusMinusButtonTextures()
        {
            if (_moveStackPlusMinusButtonTexTried)
                return;
            _moveStackPlusMinusButtonTexTried = true;
            _moveStackMinusButtonTex = Resources.Load<Texture2D>("Sprites/minus button") ??
                                        Resources.Load<Texture2D>("Sprites/Minus button");
            _moveStackPlusButtonTex = Resources.Load<Texture2D>("Sprites/plus button") ??
                                      Resources.Load<Texture2D>("Sprites/Plus button");
        }

        void EnsureBottomHudPanelTexture()
        {
            if (_bottomHudPanelTexTried)
                return;
            _bottomHudPanelTexTried = true;
            _bottomHudPanelTex = Resources.Load<Texture2D>("Sprites/Bottom Hud") ??
                                  Resources.Load<Texture2D>("Sprites/bottom hud");
        }

        void EnsureTopHudPanelTexture()
        {
            if (_topHudPanelTexTried)
                return;
            _topHudPanelTexTried = true;
            _topHudPanelTex = Resources.Load<Texture2D>("Sprites/Top Hud") ??
                              Resources.Load<Texture2D>("Sprites/top hud");
        }

        void EnsureCardsPileButtonTexture()
        {
            if (_cardsPileButtonTexTried)
                return;
            _cardsPileButtonTexTried = true;
            _cardsPileButtonTex = Resources.Load<Texture2D>("Sprites/Cards button") ??
                                    Resources.Load<Texture2D>("Sprites/cards button");
        }

        /// <summary>Phase ribbon Y — shared with bottom HUD background so art and ribbon stay aligned.</summary>
        float ComputePhaseRibbonY(float cardBarY)
        {
            var hp = _hudLayoutPanel;
            float margin = HudS(4f);
            float y = cardBarY + _hudCardBarHeight + margin;
            if (y + _hudPhaseRibbonHeight > hp.yMax - HudS(2f))
                y = cardBarY - _hudPhaseRibbonHeight - margin;
            return Mathf.Clamp(y, hp.y + margin, hp.yMax - _hudPhaseRibbonHeight - HudS(2f));
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
        /// IMGUI cannot render TMP SDF assets; uses the same Seabox <see cref="Font"/> as tile UI (paired with TMP Seabox SDF in Resources).
        /// </summary>
        GUIStyle EndTurnAdvanceOverlayLabelStyle()
        {
            float s = _hudFontScale;
            int wantSize = Mathf.Max(11, Mathf.RoundToInt(28f * s));
            if (_endTurnAdvanceOverlayLabelStyle != null &&
                Mathf.Abs(_endTurnAdvanceOverlayLabelStyleScale - s) < 0.002f &&
                _endTurnAdvanceOverlayLabelStyle.fontSize == wantSize)
                return _endTurnAdvanceOverlayLabelStyle;

            _endTurnAdvanceOverlayLabelStyleScale = s;
            _endTurnAdvanceOverlayLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = wantSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                clipping = TextClipping.Overflow,
                normal = { textColor = Color.white }
            };
            ApplyTileInfoFont(_endTurnAdvanceOverlayLabelStyle);
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

        void DrawPlaceholderCard(Rect r, NexusGuiImage cardFace, string text)
        {
            EnsureHandPileCardTextStyles();
            float pad = Mathf.Max(HudS(6f), r.width * 0.06f);
            var bodyR = new Rect(r.x + pad, r.y + r.height * 0.36f, r.width - pad * 2f, r.height * 0.48f);
            int bodyMax = GameUiScale.ImGuiScaledFont(15f, 11, 34);
            _handPileCardBodyStyle.fontSize =
                GameUiScale.ComputeBestFitFontSize(_handPileCardBodyStyle, text, bodyR.width, bodyR.height, 11, bodyMax,
                    true);

            if (Event.current.type == EventType.Repaint)
            {
                if (!cardFace.IsEmpty)
                    cardFace.DrawStretchFill(r);
                else
                {
                    DrawTintedRect(r, new Color(0.14f, 0.14f, 0.18f));
                    DrawTintedRect(new Rect(r.x + HudS(2f), r.y + HudS(2f), r.width - HudS(4f), HudS(22f)),
                        new Color(0.28f, 0.28f, 0.32f));
                }
            }

            GUI.Label(bodyR, text, _handPileCardBodyStyle);
        }

        void DrawPlayingCard(Rect r, NexusGuiImage cardFace, string title, string detail, int stack)
        {
            EnsureHandPileCardTextStyles();
            float pad = Mathf.Max(HudS(4f), r.width * 0.055f);
            float titleBandH = Mathf.Clamp(r.height * 0.24f, HudS(24f), HudS(58f));
            var titleR = new Rect(r.x + pad, r.y + r.height * 0.05f, r.width - pad * 2f, titleBandH);
            int titleMax = GameUiScale.ImGuiScaledFont(17f, 12, 42);
            _handPileCardTitleStyle.fontSize =
                GameUiScale.ComputeBestFitFontSize(_handPileCardTitleStyle, title, titleR.width, titleR.height, 8,
                    titleMax, false);

            float badgePad = Mathf.Max(HudS(5f), r.width * 0.035f);
            float badgeW = HudS(44f);
            float badgeH = HudS(28f);
            var stackBadgeR = new Rect(r.xMax - badgeW - badgePad, r.yMax - badgeH - badgePad, badgeW, badgeH);
            int badgeFontMax = GameUiScale.ImGuiScaledFont(17f, 12, 42);
            if (stack > 1)
                _handPileCardBadgeStyle.fontSize = GameUiScale.ComputeBestFitFontSize(_handPileCardBadgeStyle,
                    "x" + stack,
                    stackBadgeR.width, stackBadgeR.height, 11, badgeFontMax, false);

            float stackReserve = stack > 1 ? Mathf.Max(badgeH + badgePad + HudS(2f), r.height * 0.07f) : 0f;
            var bodyR = new Rect(r.x + pad, r.y + r.height * 0.30f, r.width - pad * 2f,
                Mathf.Max(HudS(40f), r.height * 0.58f - stackReserve));
            int bodyMax = GameUiScale.ImGuiScaledFont(15f, 11, 36);
            _handPileCardBodyStyle.fontSize =
                GameUiScale.ComputeBestFitFontSize(_handPileCardBodyStyle, detail, bodyR.width, bodyR.height, 10,
                    bodyMax, true);

            if (Event.current.type == EventType.Repaint)
            {
                if (!cardFace.IsEmpty)
                    cardFace.DrawStretchFill(r);
                else
                {
                    float t = HudS(2f);
                    float hdr = HudS(22f);
                    DrawTintedRect(r, new Color(0.14f, 0.14f, 0.18f));
                    DrawTintedRect(new Rect(r.x + t, r.y + t, r.width - t * 2f, hdr), new Color(0.2f, 0.25f, 0.4f));
                }
            }

            GUI.Label(titleR, title, _handPileCardTitleStyle);
            if (stack > 1)
                GUI.Label(stackBadgeR, "x" + stack, _handPileCardBadgeStyle);
            GUI.Label(bodyR, detail, _handPileCardBodyStyle);
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
        /// </summary>
        bool ShouldPaintFullBattleOverlay(PlayerState currentPlayer)
        {
            if (Game == null)
                return false;

            // Online: always show the full battle modal for any active battle phase (view-only for the other seat).
            if (NexusSession.IsOnline)
            {
                if (Game.BattlePhaseBlockingPlay || Game.PendingBattleArrangement)
                    return true;
                if (Game.BattleClashIntroActive || Game.HasActiveBattleStep ||
                    Game.EnergizePromptPlayer != null || Game.FocusFirePicker != null ||
                    Game.CasualtyPick != null || Game.ActiveBattleHex != null)
                    return true;
                if (Game.SecretMissionOffer != null && Game.SecretMissionOffer.Waiting)
                    return true;
            }

            bool active = Game.PendingBattleArrangement ||
                          Game.BattlePhaseBlockingPlay ||
                          Game.BattleClashIntroActive ||
                          Game.HasActiveBattleStep ||
                          Game.EnergizePromptPlayer != null ||
                          Game.FocusFirePicker != null ||
                          Game.CasualtyPick != null ||
                          (Game.SecretMissionOffer != null && Game.SecretMissionOffer.Waiting) ||
                          Game.ActiveBattleHex != null;
            if (!active)
                return false;

            var actor = Game.EnergizePromptPlayer ?? Game.FocusFirePicker ?? Game.CasualtyPick?.Owner ??
                        Game.SecretMissionOffer?.Player ?? currentPlayer;
            if (!NexusSession.IsOnline && actor != null && Game.IsAiControlled(actor))
                return false;

            return true;
        }

        void TryAutoPassEmptyEnergizeStep()
        {
            if (Game?.EnergizePromptPlayer == null || Game.FocusFirePicker != null)
                return;
            if (Game.EnergizePromptPlayer.BattleEnergize != null &&
                Game.EnergizePromptPlayer.BattleEnergize.Count > 0)
                return;
            if (!Game.CanLocalPlayerActFor(Game.EnergizePromptPlayer))
                return;

            if (Time.unscaledTime - _lastEnergizeAutoPassAttemptUnscaled < 0.35f)
                return;
            _lastEnergizeAutoPassAttemptUnscaled = Time.unscaledTime;
            NexusGameCommands.RequestSubmitEnergizePass();
        }

        void DrawFullBattleOverlays(PlayerState currentPlayer)
        {
            if (Game != null)
                Game.PruneExpiredBattleCasualtyDeathFx(Time.unscaledTime);

            TryAutoPassEmptyEnergizeStep();

            if (!ShouldPaintFullBattleOverlay(currentPlayer))
                return;

            var panel = GetBattleScreenPanelGuiRect();
            DrawModalPerimeterClickBlockers(panel);
            DrawTintedRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.015f, 0.02f, 0.04f, 0.72f));
            DrawBattleScreenModalBackground();
            GUI.BeginGroup(panel);
            BattleMainWindow(currentPlayer, panel);
            GUI.EndGroup();

            if (Game.CasualtyPick?.Owner != null)
                DrawBattleCasualtySelectionOverlay();

            // death.png casualty FX (same frame as battle strip layout — slot rects from Repaint).
            var gameFx = Game;
            if (gameFx != null)
            {
                if (gameFx.ActiveBattleCasualtyDeathFx.Count > 0)
                {
                    EnsureBattleDeathFxTexture();
                    if (_battleDeathFxTex != null)
                    {
                        float durFx = GameController.BattleCasualtyDeathFxSeconds;
                        int prevDepthFx = GUI.depth;
                        Color prevFx = GUI.color;
                        GUI.depth = -80;
                        foreach (var fx in gameFx.ActiveBattleCasualtyDeathFx)
                        {
                            if (!_battleUnitSlotIconRects.TryGetValue((fx.AttackerSide, fx.UnitType),
                                    out Rect slotFx))
                                continue;
                            float tf = Mathf.Clamp01((Time.unscaledTime - fx.StartTimeUnscaled) / durFx);
                            float riseFx = -BattleS(12f) * Mathf.SmoothStep(0f, 1f, tf);
                            var rf = new Rect(slotFx.x, slotFx.y + riseFx, slotFx.width, slotFx.height);
                            // Linear fade over durFx (default 0.5s) — matches BattleCasualtyDeathFxSeconds.
                            GUI.color = new Color(1f, 1f, 1f, 1f - tf);
                            GUI.DrawTexture(rf, _battleDeathFxTex, ScaleMode.ScaleToFit, true);
                        }

                        GUI.color = prevFx;
                        GUI.depth = prevDepthFx;
                    }
                }
            }
        }

        void EnsureBattleDeathFxTexture()
        {
            if (_battleDeathFxTexTried)
                return;
            _battleDeathFxTexTried = true;
            _battleDeathFxTex = Resources.Load<Texture2D>("Sprites/Death") ??
                                Resources.Load<Texture2D>("Sprites/death");
            if (_battleDeathFxTex == null)
            {
                var sp = Resources.Load<Sprite>("Sprites/Death") ?? Resources.Load<Sprite>("Sprites/death");
                if (sp != null)
                    _battleDeathFxTex = sp.texture;
            }
        }

        /// <summary>
        /// Modal layer on top of the battle art: tile frame, tinted header, 3×2 unit grid + Auto-pick / Clear / Confirm.
        /// </summary>
        void DrawBattleCasualtySelectionOverlay()
        {
            var cp = Game.CasualtyPick;
            if (cp?.Owner == null)
                return;

            bool canAct = Game.CanLocalPlayerActFor(cp.Owner);
            bool prevEnabled = GUI.enabled;
            GUI.enabled = canAct;

            cp.Pool.RemoveAll(u => u == null);
            cp.Selected.RemoveAll(u => u == null || !cp.Pool.Contains(u));
            cp.Required = Mathf.Clamp(cp.Required, 0, cp.Pool.Count);
            if (cp.Required == 0)
            {
                NexusGameCommands.RequestSubmitCasualtyPick();
                return;
            }

            var hex = Game.ActiveBattleHex;
            var owner = cp.Owner;
            if (hex == null)
                return;

            // Full battle modal already dimmed the screen — keep the army strip visible behind this picker.

            var panel = GameUiScale.GetBattleCasualtyModalPanelGuiRect();
            DrawModalPerimeterClickBlockers(panel);
            DrawCasualtyModalPanelBackground(panel);
            Rect content = DrawCasualtySelectionModalHeader(owner, panel, out float panelScale);

            _battlePanelContentWidth = content.width;
            _battlePanelScaleCached = panelScale;
            _battleHudUiScale = BattleHudUiScale(panel);
            _battleFontReferencePanel = content;
            ApplyBattleHudScaledStyles();
            EnsureBattleHudStyles();

            GUILayout.BeginArea(content);
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Space(BattleS(6f));

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

            float gw = content.width;
            float innerPad = BattleS(2f);
            float cellOuterWPre = Mathf.Floor((gw - innerPad * 2f) / 3f) - BattleS(2f);
            cellOuterWPre = Mathf.Clamp(cellOuterWPre, BattleS(72f), BattleS(200f));
            float boxWPre = cellOuterWPre - BattleS(2f);
            float boxHPre = Mathf.Clamp(boxWPre * 0.72f, BattleS(56f), BattleS(120f));
            float gridMinH = boxHPre * 2f + BattleS(8f) + BattleS(16f);
            // Reserve room for AUTO/CLEAR + CONFIRM rows so controls don't collide on shorter screens.
            float controlsReserve = BattleS(44f) + BattleS(12f) + BattleS(48f) + BattleS(36f);
            float gridMaxH = Mathf.Max(BattleS(120f), content.height - controlsReserve);
            gridMinH = Mathf.Min(gridMinH, gridMaxH);
            GUILayout.BeginVertical(GUILayout.MinHeight(gridMinH));
            DrawCasualtyOverlaySixTypeGrid(cp, hex, owner);
            GUILayout.EndVertical();

            GUILayout.Space(BattleS(18f));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("AUTO-PICK", _battleSecondaryButtonStyleCached, GUILayout.ExpandWidth(true),
                    GUILayout.Height(BattleS(44f))))
                AutoPickCasualties(cp);
            if (GUILayout.Button("CLEAR", _battleSecondaryButtonStyleCached, GUILayout.ExpandWidth(true),
                    GUILayout.Height(BattleS(44f))))
                cp.Selected.Clear();
            GUILayout.EndHorizontal();
            GUILayout.Space(BattleS(12f));
            GUI.enabled = canAct && cp.Selected.Count == cp.Required;
            if (GUILayout.Button("CONFIRM", _battlePrimaryButtonStyleCached, GUILayout.ExpandWidth(true),
                    GUILayout.Height(BattleS(48f))))
                NexusGameCommands.RequestSubmitCasualtyPick();
            GUI.enabled = prevEnabled;

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        /// <summary>3×2 grid of the six battle order types; same interaction as the former battle-strip casualty cells.</summary>
        void DrawCasualtyOverlaySixTypeGrid(CasualtyPickState cp, BoardTile hex, PlayerState player)
        {
            bool canAct = player != null && Game.CanLocalPlayerActFor(player);
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
                GUILayout.BeginHorizontal(GUILayout.MinHeight(boxH + BattleS(8f)));
                GUILayout.FlexibleSpace();
                for (int col = 0; col < 3; col++)
                {
                    int idx = row * 3 + col;
                    if (idx >= unitOrder.Length)
                        break;
                    UnitType unitType = unitOrder[idx];
                    counts.TryGetValue(unitType, out int n);

                    GUILayout.BeginVertical(GUILayout.Width(cellOuterW), GUILayout.MinHeight(boxH));

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

                        bool canAdd = canAct && cp.Selected.Count < cp.Required && selected < n;
                        bool canSub = canAct && selected > 0;
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
            if (Event.current.type == EventType.Repaint)
                _battleUnitSlotIconRects.Clear();

            _battlePanelContentWidth = panel.width;
            _battlePanelScaleCached = GameUiScale.TileInfoModalPanelScale(panel);
            _battleHudUiScale = BattleHudUiScale(panel);
            _battleFontReferencePanel = panel;
            ApplyBattleHudScaledStyles();
            float windowHeight = panel.height;
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
            GUILayout.Space(BattleS(20f));

            float centerColW = Mathf.Clamp(panel.width - BattleS(24f), BattleS(280f), panel.width - BattleS(12f));
            float savedPanelContentW = _battlePanelContentWidth;
            _battlePanelContentWidth = centerColW;

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical(GUILayout.Width(centerColW));

            GUILayout.BeginVertical(BattlePanelBoxStyle());
            DrawBattleContextBar(hex);
            GUILayout.EndVertical();
            GUILayout.Space(BattleS(14f));

            // Army strip: wide left/right columns, narrow clash swords between.
            GUILayout.BeginVertical(BattlePanelBoxStyle());
            SyncBattleDiceAnimState();
            float stripBudget = centerColW - BattleS(16f);
            float clashColW = Mathf.Clamp(BattleS(64f), BattleS(52f), BattleS(88f));
            float battleColW = (stripBudget - clashColW - BattleS(8f)) * 0.5f;
            battleColW = Mathf.Clamp(battleColW, BattleS(140f), stripBudget * 0.48f);
            float battleStripInnerW = battleColW * 2f + clashColW + BattleS(8f);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal(GUILayout.Width(battleStripInnerW));
            DrawBattleSideColumn(left, hex, true, Game.ActiveBattleAttacker, battleColW);
            DrawBattleCenterClashOnly(clashColW);
            DrawBattleSideColumn(right, hex, false, Game.ActiveBattleDefender, battleColW);
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(BattleS(12f));
            DrawBattleActiveRollRow(centerColW);

            GUILayout.Space(BattleS(14f));
            DrawBattlePhaseRibbon();
            GUILayout.Space(BattleS(14f));
            DrawBattleOrderRibbonIcons(drawRibbonInnerFrame: false);
            DrawBattleDiceRollBannerWhenIdle();

            GUILayout.Space(BattleS(10f));

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
                int logTitleFs = GameUiScale.FullBleedImGuiScaledFont(12f, _battleFontReferencePanel, 10, 24);
                var logTitleStyle = new GUIStyle(GUI.skin.label)
                    { fontStyle = FontStyle.Bold, fontSize = logTitleFs };
                ApplyTileInfoFont(logTitleStyle);
                GUILayout.Label("📜 Log", logTitleStyle);
                string battleLog = !string.IsNullOrEmpty(Game.LiveBattlePhaseLog) ? Game.LiveBattlePhaseLog : Game.LastBattlePhaseLog;
                string safe = UiSafeText(battleLog);
                if (safe.Length != _lastBattleLogLen)
                {
                    _lastBattleLogLen = safe.Length;
                    _scrollBattleMainLog.y = 100000f;
                }
                float logH = Mathf.Clamp(windowHeight * 0.22f, BattleS(64f), Mathf.Min(BattleS(160f), windowHeight * 0.32f));
                _scrollBattleMainLog = GUILayout.BeginScrollView(_scrollBattleMainLog, GUILayout.Height(logH));
                int logBodyFs = GameUiScale.FullBleedImGuiScaledFont(11f, _battleFontReferencePanel, 9, 22);
                var logBody = new GUIStyle(GUI.skin.label)
                {
                    fontSize = logBodyFs,
                    wordWrap = true
                };
                ApplyTileInfoFont(logBody);
                GUILayout.Label(string.IsNullOrEmpty(safe) ? "(No battle log yet)" : safe, logBody);
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
            }

            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            _battlePanelContentWidth = savedPanelContentW;

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        void DrawBattleContextBar(BoardTile hex)
        {
            string ctx;
            if (hex != null)
                ctx = TileTypeDisplayName(hex.Type);
            else if (!string.IsNullOrEmpty(Game.EnergizeBattleContext))
                ctx = Game.EnergizeBattleContext;
            else
                ctx = "Battle";
            int fs = GameUiScale.FullBleedImGuiScaledFont(27f, _battleFontReferencePanel, 17, 40);
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

        /// <summary>Legacy dice row when not in an active battle step (roll row handles the live step).</summary>
        void DrawBattleDiceRollBannerWhenIdle()
        {
            if (Game.HasActiveBattleStep)
                return;
            DrawBattleDiceRollBanner();
        }

        /// <summary>Active roller unit (left) and dice + result text (right), full width under the army strip.</summary>
        void DrawBattleActiveRollRow(float rowW)
        {
            SyncBattleDiceAnimState();
            var dOpt = Game.LastBattleUiDiceRoll;
            bool showRow = Game.HasActiveBattleStep || dOpt.HasValue;
            if (!showRow)
                return;

            float rowMinH = BattleS(92f);
            GUILayout.BeginHorizontal(GUILayout.Width(rowW), GUILayout.MinHeight(rowMinH));
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));

            UnitType unitType = Game.HasActiveBattleStep
                ? Game.ActiveBattleStepUnitType
                : (dOpt.HasValue ? dOpt.Value.UnitType : UnitType.Human);
            float iconBox = Mathf.Clamp(BattleS(80f), BattleS(68f), BattleS(108f));
            var iconOuter = GUILayoutUtility.GetRect(iconBox, iconBox, GUILayout.Width(iconBox),
                GUILayout.Height(iconBox));
            DrawTintedRect(iconOuter, new Color(0.08f, 0.1f, 0.16f, 0.97f));
            DrawOutlineRect(iconOuter, new Color(1f, 0.55f, 0.22f, 0.95f), BattleS(3f));
            float pad = BattleS(4f);
            var ir = new Rect(iconOuter.x + pad, iconOuter.y + pad, iconOuter.width - pad * 2f,
                iconOuter.height - pad * 2f);
            DrawBattleBannerUnitIcon(ir, unitType);

            if (dOpt.HasValue)
            {
                var d = dOpt.Value;
                bool revealFinal = (Time.realtimeSinceStartup - _battleDiceAnimStartRealtime) >=
                                   GameController.BattleDiceRollSpinSeconds;
                float rt = Time.realtimeSinceStartup;
                int bannerFont = GameUiScale.FullBleedImGuiScaledFont(17f, _battleFontReferencePanel, 12, 32);
                var bannerLabel = new GUIStyle(GUI.skin.label)
                {
                    fontSize = bannerFont,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = false
                };
                ApplyTileInfoFont(bannerLabel);

                GUILayout.Space(BattleS(12f));
                string side = d.AttackerRolling ? "ATK" : "DEF";
                var sideC = d.AttackerRolling ? new Color(0.35f, 0.55f, 1f) : new Color(1f, 0.45f, 0.35f);
                var prev = GUI.color;
                GUI.color = sideC;
                GUILayout.Label(side, bannerLabel, GUILayout.Width(BattleS(48f)));
                GUI.color = prev;

                int dieCount = 0;
                if (d.Rolls != null && d.Rolls.Length > 0)
                    dieCount = d.Rolls.Length;
                else if (d.Dice > 0 && d.Impossible)
                    dieCount = d.Dice;

                var faceBgImpossible = new Color(0.93f, 0.94f, 0.97f, 1f);
                if (dieCount > 0)
                {
                    int show = Mathf.Min(dieCount, 6);
                    float gap = BattleS(6f);
                    float dieSz = Mathf.Clamp(BattleS(52f),
                        BattleS(44f),
                        (rowW * 0.55f - BattleS(120f) - (show - 1) * gap) / Mathf.Max(1, show));
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
                }
                else if (d.Dice <= 0)
                {
                    GUILayout.Label("0🎲", bannerLabel, GUILayout.Width(BattleS(52f)));
                }

                GUILayout.Space(BattleS(8f));
                if (d.Impossible && d.Dice > 0)
                    GUILayout.Label($"need ≥{d.Need} (—)", bannerLabel, GUILayout.ExpandWidth(false));
                else if (d.Dice > 0)
                    GUILayout.Label($"need ≥{d.Need}  →  {d.Hits} hit(s)", bannerLabel, GUILayout.ExpandWidth(false));
            }

            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
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
            int bannerFont = GameUiScale.FullBleedImGuiScaledFont(16f, _battleFontReferencePanel, 11, 30);
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
            float sidePanelW = panelWidth;
            float fs = _hudFontScale;

            float innerPad = BattleS(2f);
            const int unitsPerRow = 2;
            float cellOuterW = Mathf.Floor((sidePanelW - innerPad * 2f) / unitsPerRow) - BattleS(2f);
            cellOuterW = Mathf.Clamp(cellOuterW, BattleS(88f), BattleS(220f));
            float boxW = cellOuterW - BattleS(2f);
            float boxH = Mathf.Clamp(boxW * 0.72f, BattleS(68f), BattleS(128f));
            float rowStripH = boxH + BattleS(8f);
            bool hasGrid = player != null && hex != null;
            float gridH = hasGrid ? 3f * rowStripH : BattleS(52f);
            float gapTitleToUnits = BattleS(12f);
            float colH = Mathf.Max(BattleS(140f), BattleS(24f) + gapTitleToUnits + gridH + BattleS(10f));

            var colRect = GUILayoutUtility.GetRect(sidePanelW, colH, GUILayout.Width(sidePanelW),
                GUILayout.Height(colH), GUILayout.ExpandWidth(false));
            if (Event.current.type == EventType.Repaint)
            {
                EnsureBattleScreenChromeArt();
                if (!_battleArmyContainerImg.IsEmpty)
                {
                    if (isLeft)
                        _battleArmyContainerImg.DrawStretchFillFlippedH(colRect);
                    else
                        _battleArmyContainerImg.DrawStretchFill(colRect);
                }
            }

            // BeginGroup + local BeginArea avoids GUILayout.BeginArea(absolute rect) swallowing sibling layout in strips.
            GUI.BeginGroup(colRect);
            GUILayout.BeginArea(new Rect(0f, 0f, colRect.width, colRect.height));
            GUILayout.BeginVertical(GUILayout.Width(sidePanelW), GUILayout.ExpandHeight(true));
            GUILayout.FlexibleSpace();

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
            if (!hasGrid)
            {
                GUILayout.Label("(—)");
                GUILayout.EndVertical();
                GUILayout.EndArea();
                GUI.EndGroup();
                return;
            }

            GUILayout.Space(gapTitleToUnits);

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
                float iconSz = Mathf.Clamp(maxIcon, BattleS(32f), Mathf.Min(boxW * 0.95f, innerH));
                float blockH = iconSz + countH;
                float blockY = box.yMax - labelH - blockH;
                float ix = box.x + (box.width - iconSz) * 0.5f;
                var iconR = new Rect(ix, blockY, iconSz, iconSz);
                if (Event.current.type == EventType.Repaint)
                {
                    // Parent battle strip uses coords inside BeginArea(0,0,panel.size); offsets by column rect.
                    _battleUnitSlotIconRects[(isLeft, unitType)] =
                        new Rect(colRect.x + iconR.x, colRect.y + iconR.y, iconR.width, iconR.height);
                }

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
            GUILayout.EndArea();
            GUI.EndGroup();
        }

        /// <param name="drawRibbonInnerFrame">When false (battle panel merged with cards), outer panel supplies the frame.</param>
        void DrawBattleOrderRibbonIcons(bool drawRibbonInnerFrame = true)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (drawRibbonInnerFrame)
                GUILayout.BeginHorizontal(BattlePanelBoxStyle(), GUILayout.ExpandWidth(false));
            else
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));

            int n = BattleResolver.BattleOrder.Length;
            float ribbonW = _battlePanelContentWidth > 8f
                ? _battlePanelContentWidth
                : Mathf.Max(100f, GameUiScale.GetPaddedModalPanelGuiRect().width - 16f);
            float usableW = ribbonW - BattleS(24f);
            float gap = BattleS(6f);
            float sq = n > 0 ? Mathf.Floor((usableW - gap * (n - 1)) / n) : BattleS(56f);
            sq = Mathf.Clamp(sq, BattleS(56f), BattleS(112f));
            float rowH = sq + BattleS(16f);

            GUILayout.BeginVertical(GUILayout.Width(ribbonW));
            GUILayout.Space(BattleS(8f));

            var rowRect = GUILayoutUtility.GetRect(ribbonW, rowH, GUILayout.Width(ribbonW));
            if (Event.current.type == EventType.Repaint)
            {
                EnsureBattleScreenChromeArt();
                if (!_battleUnitRibbonImg.IsEmpty)
                    _battleUnitRibbonImg.DrawStretchFill(rowRect);
            }

            var hex = Game.ActiveBattleHex;
            GUI.BeginGroup(rowRect);
            float totalIconsW = n * sq + Mathf.Max(0, n - 1) * gap;
            float x0 = Mathf.Max(0f, (rowRect.width - totalIconsW) * 0.5f);
            float y0 = (rowRect.height - sq) * 0.5f;

            int idx = 0;
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
                var face = new Rect(x0 + idx * (sq + gap), y0, sq, sq);
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
                idx++;
            }

            GUI.EndGroup();

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
            bool canAct = Game.CurrentPlayer != null && Game.CanLocalPlayerActFor(Game.CurrentPlayer);
            bool prevEnabled = GUI.enabled;
            GUI.enabled = canAct;
            if (Game.BattlePlan == null || Game.BattlePlan.Count == 0)
            {
                GUILayout.Label("No battles to resolve.");
                GUI.enabled = prevEnabled;
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
                    NexusGameCommands.RequestConfirmBattleArrangement();
                GUI.enabled = prevEnabled;
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
                        NexusGameCommands.RequestMoveBattlePlanEntry(i, -1);
                    if (GUILayout.Button("v", GUILayout.Width(28)))
                        NexusGameCommands.RequestMoveBattlePlanEntry(i, 1);
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
                            NexusGameCommands.RequestSetBattleDefender(i, o.PlayerIndex);
                    }
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            GUILayout.Space(8);
            if (GUILayout.Button("CONFIRM", _battlePrimaryButtonStyleCached, GUILayout.ExpandWidth(true)))
                NexusGameCommands.RequestConfirmBattleArrangement();
            GUI.enabled = prevEnabled;
        }

        void EnergizeWindow()
        {
            EnsureBattleHudStyles();
            EnsureHandPileCardFaces();
            var p = Game.EnergizePromptPlayer;
            bool canAct = p != null && Game.CanLocalPlayerActFor(p);
            bool prevEnabled = GUI.enabled;
            GUI.enabled = canAct;
            if (!canAct && p != null)
            {
                GUILayout.Label("Waiting for P" + (p.PlayerIndex + 1) + " to play Energize…");
                GUI.enabled = prevEnabled;
                return;
            }

            float colW = Mathf.Max(BattleS(120f), _battlePanelContentWidth);
            float contentW = colW;

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical(GUILayout.Width(contentW));

            var distinct = p.BattleEnergize.GroupBy(x => x).OrderBy(g => g.Key.ToString()).ToList();
            int nCards = distinct.Count;
            const int cols = 2;
            const float battleCardAspect = 188f / 200f;
            float gridW = contentW;
            float gap = BattleS(10f);
            float cardW = Mathf.Floor((gridW - gap * (cols - 1)) / cols);
            cardW = Mathf.Clamp(cardW, BattleS(96f), BattleS(260f));
            float cardH = Mathf.Clamp(cardW * battleCardAspect, BattleS(72f), BattleS(260f));
            int rows = Mathf.CeilToInt(nCards / (float)cols);

            float GridHeight()
            {
                if (rows <= 0)
                    return 0f;
                return rows * cardH + Mathf.Max(0, rows - 1) * gap;
            }

            float gridH = GridHeight();
            Rect full = GameUiScale.GetFullBleedScreenGuiRect();
            float budgetH = Mathf.Clamp(full.height * 0.38f, BattleS(110f), BattleS(560f));
            float reserveBelowGrid = BattleS(10f) + BattleS(44f);
            float maxGridH = Mathf.Max(BattleS(64f), budgetH - reserveBelowGrid);

            for (int iter = 0; iter < 30 && gridH > maxGridH && cardH > BattleS(36f); iter++)
            {
                gap = Mathf.Max(BattleS(4f), gap * 0.94f);
                cardW = Mathf.Floor((gridW - gap * (cols - 1)) / cols);
                cardW = Mathf.Clamp(cardW, BattleS(52f), BattleS(260f));
                cardH = Mathf.Clamp(cardW * battleCardAspect, BattleS(36f), BattleS(260f));
                gridH = GridHeight();
            }

            if (gridH > maxGridH && gridH > 0.5f)
            {
                float s = Mathf.Clamp(maxGridH / gridH, 0.42f, 1f);
                cardH = Mathf.Max(BattleS(28f), cardH * s);
                cardW = Mathf.Max(BattleS(44f), cardW * s);
                gap = Mathf.Max(BattleS(3f), gap * s);
                cardW = Mathf.Floor((gridW - gap * (cols - 1)) / cols);
                cardW = Mathf.Clamp(cardW, BattleS(44f), BattleS(260f));
                cardH = Mathf.Clamp(cardH, BattleS(28f), Mathf.Min(BattleS(260f), cardW * battleCardAspect));
                gridH = GridHeight();
            }

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
                    string cardFullName = EnergizeBattleCatalog.GetName(g.Key);
                    var cellRect = GUILayoutUtility.GetRect(cardW, cardH, GUILayout.Width(cardW),
                        GUILayout.Height(cardH));
                    DrawPlayingCard(cellRect, _pileBattleCardFace, CardShortTitle(cardFullName),
                        CardDetailFromName(cardFullName),
                        count);
                    if (GUI.Button(cellRect, GUIContent.none, GUIStyle.none))
                        NexusGameCommands.RequestSubmitEnergizePlay(g.Key);
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                if ((row + 1) * cols < nCards)
                    GUILayout.Space(gap);
            }

            GUILayout.Space(BattleS(10f));
            if (GUILayout.Button("PASS", _battleSecondaryButtonStyleCached, GUILayout.Height(BattleS(44f)),
                    GUILayout.ExpandWidth(true)))
                NexusGameCommands.RequestSubmitEnergizePass();
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUI.enabled = prevEnabled;
        }

        void FocusFireWindow()
        {
            EnsureBattleHudStyles();
            var picker = Game.FocusFirePicker;
            bool canAct = picker != null && Game.CanLocalPlayerActFor(picker);
            bool prevEnabled = GUI.enabled;
            GUI.enabled = canAct;
            if (!canAct && picker != null)
            {
                GUILayout.Label("Waiting for P" + (picker.PlayerIndex + 1) + " to choose Focus Fire…");
                GUI.enabled = prevEnabled;
                return;
            }

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
                    NexusGameCommands.RequestSubmitFocusFireUnitType(t);
                GUILayout.EndHorizontal();
            }

            if (types.Count == 0 && GUILayout.Button("CANCEL (REFUND)", _battleSecondaryButtonStyleCached,
                    GUILayout.ExpandWidth(true)))
                NexusGameCommands.RequestCancelFocusFireRefund();
            GUI.enabled = prevEnabled;
        }

        void CasualtyWindow()
        {
            EnsureBattleHudStyles();
            var cp = Game.CasualtyPick;
            bool canAct = cp?.Owner != null && Game.CanLocalPlayerActFor(cp.Owner);
            bool prevEnabled = GUI.enabled;
            GUI.enabled = canAct;
            if (!canAct && cp?.Owner != null)
            {
                GUILayout.Label("Waiting for P" + (cp.Owner.PlayerIndex + 1) + " to pick casualties…");
                GUI.enabled = prevEnabled;
                return;
            }

            cp.Pool.RemoveAll(u => u == null);
            cp.Selected.RemoveAll(u => u == null || !cp.Pool.Contains(u));
            cp.Required = Mathf.Clamp(cp.Required, 0, cp.Pool.Count);
            if (cp.Required == 0)
            {
                NexusGameCommands.RequestSubmitCasualtyPick();
                GUILayout.Label("No valid casualties remain. Auto-continuing...");
                GUI.enabled = prevEnabled;
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
            if (GUILayout.Button("AUTO-PICK", _battleSecondaryButtonStyleCached, GUILayout.Height(BattleS(42f)),
                    GUILayout.ExpandWidth(true)))
                AutoPickCasualties(cp);
            if (GUILayout.Button("CLEAR", _battleSecondaryButtonStyleCached, GUILayout.Height(BattleS(42f)),
                    GUILayout.ExpandWidth(true)))
                cp.Selected.Clear();
            GUILayout.EndHorizontal();

            GUILayout.Space(BattleS(10f));
            GUI.enabled = canAct && cp.Selected.Count == cp.Required;
            if (GUILayout.Button("CONFIRM", _battlePrimaryButtonStyleCached, GUILayout.Height(BattleS(46f)),
                    GUILayout.ExpandWidth(true)))
                NexusGameCommands.RequestSubmitCasualtyPick();
            GUI.enabled = prevEnabled;
        }

        void DrawBattleCenterClashOnly(float colW)
        {
            GUILayout.BeginVertical(GUILayout.Width(colW), GUILayout.MaxWidth(colW), GUILayout.ExpandWidth(false));
            GUILayout.Space(BattleS(26f));
            DrawBattleCenterClashSwords(colW);
            GUILayout.EndVertical();
        }

        void DrawBattleCenterClashSwords(float colW)
        {
            float rowH = Mathf.Clamp(colW * 0.72f, BattleS(64f), BattleS(120f));
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
            bool canAct = att != null && Game.CanLocalPlayerActFor(att);
            bool prevEnabled = GUI.enabled;
            GUI.enabled = canAct;
            if (!canAct && att != null)
            {
                GUILayout.Label("Waiting for P" + (att.PlayerIndex + 1) + " to play a secret mission…");
                GUI.enabled = prevEnabled;
                return;
            }

            if (offer.OffersFallbackBattleVp)
            {
                GUILayout.Label("Battle won! P" + (att.PlayerIndex + 1) +
                    " — no secret in hand matches this win. Claim +1 VP, or skip:");
                if (GUILayout.Button("Battle secret +1 VP (no card)", _battlePrimaryButtonStyleCached,
                        GUILayout.ExpandWidth(true)))
                    NexusGameCommands.RequestClaimFallbackBattleSecretVp();
            }
            else
            {
                GUILayout.Label("Battle won! P" + (att.PlayerIndex + 1) + " - play ONE secret or skip:");
                if (offer.EligibleIndices != null)
                {
                    foreach (int idx in offer.EligibleIndices)
                    {
                        if (idx < 0 || idx >= att.SecretMissions.Count)
                            continue;
                        var s = att.SecretMissions[idx];
                        if (GUILayout.Button(SecretMissionLabel(s) + " +" + s.VictoryPoints + " VP [i" + idx + "]",
                                _battlePrimaryButtonStyleCached, GUILayout.ExpandWidth(true)))
                            NexusGameCommands.RequestPlaySecretMissionAtIndex(idx);
                    }
                }
            }

            GUILayout.Space(8);
            if (GUILayout.Button("SKIP", _battleSecondaryButtonStyleCached, GUILayout.ExpandWidth(true)))
                NexusGameCommands.RequestSkipSecretMissionPlay();
            GUI.enabled = prevEnabled;
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
                $"Secret hand limit reached ({GameController.MaxSecretMissionsInHand}). P{p.PlayerIndex + 1}: discard one mission to draw the new one.",
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

        string DragonBreathSkipLabel()
        {
            var dp = Game?.DragonPhase;
            if (dp != null && dp.DuringDeployment)
                return "SKIP FORTRESS BREATH";
            return "SKIP DRAGON'S BREATH";
        }

        void DrawFortressPlacementHint()
        {
            if (Game == null || !Game.PendingFortressPlacement || !Game.CanLocalPlayerActNow())
                return;

            var hp = _hudLayoutPanel;
            float edge = HudS(20f);
            float panelH = HudS(72f);
            var panelRect = new Rect(hp.x + edge, hp.y + HudS(12f), hp.width - edge * 2f, panelH);
            var style = new GUIStyle(GUI.skin.box)
            {
                fontSize = Mathf.Max(12, Mathf.RoundToInt(14f * _hudFontScale)),
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            ApplyTileInfoFont(style);
            GUI.Box(panelRect,
                "Fortress — tap a hex you solely occupy (not home base). Close the pile viewer to cancel.");
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
                string aiTitle = dp.DuringDeployment
                    ? "Fortress breath — opponent is choosing…"
                    : NexusSession.StealthBotOpponent
                        ? "Rubium Dragon — opponent is choosing…"
                        : "Rubium Dragon — AI is choosing…";
                GUI.Box(panelRect, aiTitle);
                if (!string.IsNullOrEmpty(dp.LastLog))
                    GUI.Label(new Rect(hp.x + HudS(30f), hp.yMax - HudS(175f), hp.width - HudS(60f), HudS(22f)),
                        dp.LastLog);
                return;
            }

            // Human casualty pick: full-screen tile-style modal is drawn at end of OnGUI (<see cref="DrawCasualtySelectionModalDragon"/>).
            if (dp.PendingHit != null && dp.PendingEnemies != null)
                return;

            // Hex targets: orange rings on the board; skip via End Turn ("SKIP DRAGON'S BREATH").
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
                Texture2D shopArt;
                if (canAfford && canPlayFreeHuman)
                    shopArt = GetDeployShopFreeTexture(type) ?? GetDeployShopTexture(type);
                else
                    shopArt = canAfford ? GetDeployShopTexture(type) : GetDeployShopTextureGreyscale(type);
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

            NexusGameCommands.RequestPurchase(player, type, discountUse, pay, homeTile);
        }

        void DrawSelectedTilePanelBody(PlayerState player, BoardTile popupTile, float contentWidth, float contentHeight)
        {
            if (popupTile == null)
                return;

            float bhMul = BottomHudInnerLayoutMul;
            var rowTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(10, Mathf.RoundToInt(11f * _hudFontScale * bhMul)),
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                clipping = TextClipping.Overflow
            };
            ApplyTileInfoFont(rowTitle);

            var ownersOnTile = GetPlayersWithUnitsOnTileOrdered(popupTile, player);
            bool contested = ownersOnTile.Count > 1;

            if (_tilePanelTabTile != popupTile)
            {
                _tilePanelTabTile = popupTile;
                _tilePanelDetailTile = popupTile;
                _moveAllTile = popupTile;
                _moveAllChecked = false;
                _tilePanelHasDetailUnit = false;
                _tilePanelViewPlayerIndex = DefaultTilePanelViewPlayerIndex(ownersOnTile, player);
                _tilePanelLastTurnPlayerIndex = player.PlayerIndex;
            }
            else if (player != null && _tilePanelLastTurnPlayerIndex != player.PlayerIndex)
            {
                _tilePanelLastTurnPlayerIndex = player.PlayerIndex;
                if (ownersOnTile.Exists(o => o.PlayerIndex == player.PlayerIndex))
                {
                    _tilePanelViewPlayerIndex = player.PlayerIndex;
                    _tilePanelHasDetailUnit = false;
                }
            }

            if (_tilePanelDetailTile != popupTile)
            {
                _tilePanelDetailTile = popupTile;
                _tilePanelHasDetailUnit = false;
            }

            if (ownersOnTile.Count > 0 &&
                !ownersOnTile.Exists(o => o.PlayerIndex == _tilePanelViewPlayerIndex))
                _tilePanelViewPlayerIndex = DefaultTilePanelViewPlayerIndex(ownersOnTile, player);

            PlayerState viewOwner = null;
            if (Game != null && _tilePanelViewPlayerIndex >= 0 && _tilePanelViewPlayerIndex < Game.Players.Count)
                viewOwner = Game.Players[_tilePanelViewPlayerIndex];
            if (viewOwner == null)
                viewOwner = player;

            bool isMovementPhase = !Game.IsGameOver &&
                                   !Game.BattlePhaseBlockingPlay &&
                                   Game.DragonPhase == null &&
                                   Game.CanLocalPlayerActNow();
            bool viewingLocal = viewOwner.PlayerIndex == (NexusSession.IsOnline
                ? NexusSession.LocalPlayerIndex
                : player.PlayerIndex);
            bool interactiveStacks = viewingLocal && InputController != null && isMovementPhase;

            if (_moveAllTile != popupTile)
            {
                _moveAllTile = popupTile;
                _moveAllChecked = false;
            }

            GUILayout.BeginHorizontal();
            float contestedReserve = contested ? BottomHudS(92f) : 0f;
            float playerTabsReserve = ownersOnTile.Count > 1
                ? ownersOnTile.Count * BottomHudS(50f) + (ownersOnTile.Count - 1) * BottomHudS(4f) + BottomHudS(10f)
                : BottomHudS(56f);
            float tileNameMaxW = Mathf.Max(BottomHudS(64f),
                contentWidth - playerTabsReserve - contestedReserve - BottomHudS(28f));
            GUILayout.Label(TileTypeDisplayName(popupTile.Type), rowTitle, GUILayout.MaxWidth(tileNameMaxW));
            GUILayout.Space(BottomHudS(14f));

            if (ownersOnTile.Count > 1)
                DrawTilePanelFactionTabs(ownersOnTile, bhMul);
            else if (ownersOnTile.Count == 1)
            {
                GUILayout.Space(BottomHudS(6f));
                var solo = ownersOnTile[0];
                Color prev = GUI.color;
                GUI.color = solo.Color;
                GUILayout.Label("P" + (solo.PlayerIndex + 1), rowTitle, GUILayout.Width(BottomHudS(44f)));
                GUI.color = prev;
            }

            GUILayout.FlexibleSpace();
            if (contested)
            {
                var prev = GUI.color;
                GUI.color = Color.red;
                GUILayout.Label("CONTESTED", rowTitle, GUILayout.Width(BottomHudS(88f)));
                GUI.color = prev;
            }

            GUILayout.EndHorizontal();
            if (popupTile.FortressOwnerPlayerIndex >= 0)
            {
                var fortStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Max(10, Mathf.RoundToInt(11f * _hudFontScale * bhMul)),
                    fontStyle = FontStyle.Italic,
                    wordWrap = true
                };
                ApplyTileInfoFont(fortStyle);
                int fp = popupTile.FortressOwnerPlayerIndex + 1;
                bool canBreath = Game != null && Game.CurrentPlayer != null &&
                                 Game.TileHasFortressForPlayer(popupTile, Game.CurrentPlayer) &&
                                 Game.CanBeginFortressBreathDuringDeploy(popupTile);
                string fortLine = canBreath
                    ? $"Fortress (P{fp}) — tap hex to use breath"
                    : $"Fortress (P{fp})";
                GUILayout.Label(fortLine, fortStyle);
            }

            GUILayout.Space(BottomHudS(20f));

            var displayCounts = GetUnitCountsOnTileForOwner(popupTile, viewOwner);
            var seatPlayer = NexusSession.IsOnline && Game.Players.Count > NexusSession.LocalPlayerIndex
                ? Game.Players[NexusSession.LocalPlayerIndex]
                : player;
            var movableCounts = viewingLocal && InputController != null
                ? GetMovableUnitCountsOnTile(seatPlayer, popupTile)
                : new Dictionary<UnitType, int>();

            if (displayCounts.Count == 0)
            {
                var empty = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Max(10, Mathf.RoundToInt(11f * _hudFontScale * bhMul)),
                    wordWrap = true
                };
                ApplyTileInfoFont(empty);
                GUILayout.Label("No units on this tile for P" + (viewOwner.PlayerIndex + 1) + ".", empty);
                return;
            }

            var list = displayCounts.OrderBy(x => x.Key.ToString()).ToList();
            if (!_tilePanelHasDetailUnit || !displayCounts.ContainsKey(_tilePanelDetailUnit))
            {
                _tilePanelDetailUnit = list[0].Key;
                _tilePanelHasDetailUnit = true;
            }

            const int maxCols = 6;
            int layoutCols = Mathf.Clamp(list.Count, 1, maxCols);
            int numRows = Mathf.Max(1, Mathf.CeilToInt(list.Count / (float)maxCols));
            bool showMoveAll = interactiveStacks && movableCounts.Count > 0;
            float headerBlock = BottomHudS(46f);
            float moveAllReserve = showMoveAll ? BottomHudS(58f) : 0f;
            float unitAreaH = Mathf.Max(BottomHudS(48f), contentHeight - headerBlock - moveAllReserve);

            float stackW = BottomHudS(58f);
            float iconH = BottomHudS(48f);
            float countBandH = BottomHudS(20f);
            float iconToCountGap = BottomHudS(8f);
            float rowGap = BottomHudS(4f);
            float stackH = iconH + iconToCountGap + countBandH;
            stackH = Mathf.Min(stackH, (unitAreaH - rowGap * (numRows - 1)) / numRows);
            iconH = Mathf.Max(BottomHudS(36f), stackH - countBandH - iconToCountGap);

            float gap = BottomHudS(3f);
            float colW = (contentWidth - gap * (layoutCols - 1)) / layoutCols;
            stackW = Mathf.Min(stackW, colW);

            var countStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = Mathf.Max(10, Mathf.RoundToInt(12f * _hudFontScale * bhMul)),
                wordWrap = false,
                clipping = TextClipping.Overflow
            };
            ApplyTileInfoFont(countStyle);
            countStyle.normal.textColor = new Color(0.72f, 0.74f, 0.78f, 1f);

            void DrawSelectableUnitChip(UnitType unitType, int totalOnTile, float columnW)
            {
                float chipW = Mathf.Min(columnW, stackW);

                GUILayout.BeginVertical(GUILayout.Width(columnW), GUILayout.MinHeight(stackH));
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                Rect chipR = GUILayoutUtility.GetRect(chipW, iconH, GUILayout.Width(chipW), GUILayout.Height(iconH));
                if (Event.current.type == EventType.Repaint)
                    DrawTileUnitIconOnly(chipR, unitType, viewOwner);

                if (GUI.Button(chipR, GUIContent.none, TransparentHitButtonStyle()))
                {
                    _tilePanelDetailUnit = unitType;
                    _tilePanelHasDetailUnit = true;
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.Space(iconToCountGap);

                string countLabel;
                if (interactiveStacks)
                {
                    movableCounts.TryGetValue(unitType, out int movable);
                    int chosen = 0;
                    if (InputController != null)
                        InputController.SelectedMoveCounts.TryGetValue(unitType, out chosen);
                    countLabel = chosen + "/" + movable;
                }
                else
                    countLabel = "\u00d7" + totalOnTile;

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                Rect countR = GUILayoutUtility.GetRect(chipW, countBandH, GUILayout.Width(chipW),
                    GUILayout.Height(countBandH));
                if (Event.current.type == EventType.Repaint)
                    GUI.Label(countR, countLabel, countStyle);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();
            }

            for (int i = 0; i < list.Count; i += maxCols)
            {
                GUILayout.BeginHorizontal();
                for (int c = 0; c < maxCols; c++)
                {
                    int idx = i + c;
                    if (idx < list.Count)
                    {
                        var entry = list[idx];
                        DrawSelectableUnitChip(entry.Key, entry.Value, colW);
                    }
                    else
                        GUILayout.Label("", GUILayout.Width(colW), GUILayout.MinHeight(1f));

                    if (c < maxCols - 1)
                        GUILayout.Space(gap);
                }

                GUILayout.EndHorizontal();
                if (i + maxCols < list.Count)
                    GUILayout.Space(rowGap);
            }

            if (showMoveAll)
            {
                GUILayout.FlexibleSpace();
                float moveAllBtnH = BottomHudS(50f);
                var moveAllBtnStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = Mathf.Max(13, Mathf.RoundToInt(15f * _hudFontScale * bhMul)),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = false
                };
                ApplyTileInfoFont(moveAllBtnStyle);
                string moveAllLabel = _moveAllChecked ? "Clear move all" : "Move all";
                if (GUILayout.Button(moveAllLabel, moveAllBtnStyle, GUILayout.Height(moveAllBtnH),
                        GUILayout.ExpandWidth(true)))
                {
                    _moveAllChecked = !_moveAllChecked;
                    foreach (var kvp in movableCounts)
                        InputController.SetMoveSelection(kvp.Key, _moveAllChecked ? kvp.Value : 0);
                }
            }
        }

        void DrawTilePanelUnitDetail(Rect r, PlayerState player, BoardTile tile)
        {
            if (tile == null || r.width < 4f || r.height < 4f)
                return;

            float bhMul = BottomHudInnerLayoutMul;
            PlayerState viewOwner = null;
            if (Game != null && _tilePanelViewPlayerIndex >= 0 && _tilePanelViewPlayerIndex < Game.Players.Count)
                viewOwner = Game.Players[_tilePanelViewPlayerIndex];
            if (viewOwner == null)
                viewOwner = player;

            var displayCounts = GetUnitCountsOnTileForOwner(tile, viewOwner);
            if (displayCounts.Count == 0 || !_tilePanelHasDetailUnit || !displayCounts.ContainsKey(_tilePanelDetailUnit))
            {
                var hint = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true,
                    fontSize = Mathf.Max(9, Mathf.RoundToInt(10f * _hudFontScale * bhMul))
                };
                ApplyTileInfoFont(hint);
                hint.normal.textColor = new Color(0.65f, 0.67f, 0.72f, 1f);
                GUI.Label(r, "Tap a unit", hint);
                return;
            }

            UnitType unitType = _tilePanelDetailUnit;

            bool isMovementPhase = !Game.IsGameOver &&
                                   !Game.BattlePhaseBlockingPlay &&
                                   Game.DragonPhase == null &&
                                   Game.CanLocalPlayerActNow();
            bool viewingLocal = viewOwner.PlayerIndex == (NexusSession.IsOnline
                ? NexusSession.LocalPlayerIndex
                : player.PlayerIndex);
            bool interactiveStacks = viewingLocal && InputController != null && isMovementPhase;
            var movableCounts = interactiveStacks
                ? GetMovableUnitCountsOnTile(player, tile)
                : new Dictionary<UnitType, int>();
            movableCounts.TryGetValue(unitType, out int movable);

            float counterH = BottomHudS(28f);
            float gapIconToControls = BottomHudS(16f);
            float iconMaxH = Mathf.Max(BottomHudS(40f), r.height - counterH - gapIconToControls - BottomHudS(16f));
            float iconSz = Mathf.Clamp(Mathf.Min(r.width * 0.9f, iconMaxH), BottomHudS(44f), BottomHudS(70f));
            float blockH = iconSz + gapIconToControls + counterH;
            float iconY = r.yMax - blockH - BottomHudS(10f);
            float iconX = r.x + (r.width - iconSz) * 0.5f;
            var iconR = new Rect(iconX, iconY, iconSz, iconSz);
            DrawTileUnitIconOnly(iconR, unitType, viewOwner);

            float counterY = iconR.yMax + gapIconToControls;
            var countStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = Mathf.Max(11, Mathf.RoundToInt(13f * _hudFontScale * bhMul)),
                wordWrap = false
            };
            ApplyTileInfoFont(countStyle);
            countStyle.normal.textColor = new Color(0.72f, 0.74f, 0.78f, 1f);

            if (!interactiveStacks)
            {
                displayCounts.TryGetValue(unitType, out int totalOnTile);
                GUI.Label(new Rect(r.x, counterY, r.width, counterH), "\u00d7" + totalOnTile, countStyle);
                return;
            }

            EnsureMoveStackPlusMinusButtonTextures();
            var btnFallback = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = Mathf.Max(11, Mathf.RoundToInt(13f * _hudFontScale * bhMul))
            };
            ApplyTileInfoFont(btnFallback);

            float qtyBtnSz = BottomHudS(24f);
            float qtyBtnH = BottomHudS(24f);
            float countW = BottomHudS(40f);
            float rowW = qtyBtnSz * 2f + countW + BottomHudS(4f);
            float rowX = r.x + (r.width - rowW) * 0.5f;
            float qtyBtnY = counterY + (counterH - qtyBtnH) * 0.5f;
            bool canAdjust = movable > 0;
            int chosen = 0;
            if (InputController != null)
                InputController.SelectedMoveCounts.TryGetValue(unitType, out chosen);

            void DrawQtyBtn(Rect btnR, Texture2D tex, string fallback, System.Action onClick)
            {
                Color prevCol = GUI.color;
                if (!canAdjust)
                    GUI.color = new Color(prevCol.r, prevCol.g, prevCol.b, prevCol.a * 0.45f);
                if (Event.current.type == EventType.Repaint)
                {
                    if (tex != null)
                        GUI.DrawTexture(btnR, tex, ScaleMode.ScaleToFit, true);
                    else
                        GUI.Label(btnR, fallback, btnFallback);
                }

                bool prevEn = GUI.enabled;
                GUI.enabled = canAdjust;
                if (GUI.Button(btnR, GUIContent.none, TransparentHitButtonStyle()) && canAdjust)
                {
                    onClick();
                    _moveAllChecked = false;
                }

                GUI.enabled = prevEn;
                GUI.color = prevCol;
            }

            var minusR = new Rect(rowX, qtyBtnY, qtyBtnSz, qtyBtnH);
            DrawQtyBtn(minusR, _moveStackMinusButtonTex, "-", () => InputController.AdjustMoveSelection(unitType, -1));
            GUI.Label(new Rect(minusR.xMax + BottomHudS(2f), counterY, countW, counterH), chosen + "/" + movable,
                countStyle);
            var plusR = new Rect(minusR.xMax + BottomHudS(2f) + countW, qtyBtnY, qtyBtnSz, qtyBtnH);
            DrawQtyBtn(plusR, _moveStackPlusButtonTex, "+", () => InputController.AdjustMoveSelection(unitType, +1));
        }

        void DrawTilePanelFactionTabs(List<PlayerState> ownersOnTile, float bhMul)
        {
            float tabH = BottomHudS(30f);
            float tabGap = BottomHudS(4f);
            GUILayout.BeginHorizontal(GUILayout.Height(tabH));
            foreach (var o in ownersOnTile)
            {
                bool selected = _tilePanelViewPlayerIndex == o.PlayerIndex;
                var tabStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = Mathf.Max(10, Mathf.RoundToInt(12f * _hudFontScale * bhMul)),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = false,
                    padding = new RectOffset(6, 6, 2, 2)
                };
                ApplyTileInfoFont(tabStyle);
                if (selected)
                {
                    tabStyle.normal.textColor = Color.white;
                    tabStyle.hover.textColor = Color.white;
                    tabStyle.active.textColor = Color.white;
                }

                Color prevBg = GUI.backgroundColor;
                Color tint = o.Color;
                GUI.backgroundColor = selected
                    ? new Color(tint.r, tint.g, tint.b, 0.92f)
                    : new Color(tint.r * 0.55f, tint.g * 0.55f, tint.b * 0.55f, 0.75f);

                string label = "P" + (o.PlayerIndex + 1);
                if (GUILayout.Button(label, tabStyle, GUILayout.Height(tabH), GUILayout.MinWidth(BottomHudS(44f))))
                {
                if (_tilePanelViewPlayerIndex != o.PlayerIndex)
                {
                    _tilePanelViewPlayerIndex = o.PlayerIndex;
                    _moveAllChecked = false;
                    _tilePanelHasDetailUnit = false;
                }
                }

                GUI.backgroundColor = prevBg;
                GUILayout.Space(tabGap);
            }

            GUILayout.EndHorizontal();
        }

        static string UnitTypeAbbrev(UnitType type)
        {
            string n = UnitUiName(type);
            return n.Length <= 2 ? n : n.Substring(0, 2);
        }

        void DrawTileUnitIconOnly(Rect r, UnitType type, PlayerState stackOwner)
        {
            float m = BottomHudInnerLayoutMul;
            float iconSz = Mathf.Clamp(Mathf.Min(r.width, r.height) * 0.98f, 40f * m, r.height - 1f * m);
            float iconX = r.x + (r.width - iconSz) * 0.5f;
            float iconY = r.y + (r.height - iconSz) * 0.5f;
            DrawUnitMiniIcon(new Rect(iconX, iconY, iconSz, iconSz), type, TintedIconOwnerForUnitOnSide(type, stackOwner));
        }

        void DrawTileUnitReadonlyChip(Rect r, UnitType type, int count, PlayerState stackOwner)
        {
            float m = BottomHudInnerLayoutMul;
            float iconSz = Mathf.Clamp(
                Mathf.Min(r.width * 0.88f, r.height * 0.62f),
                30f * m,
                64f * m);
            float iconX = r.x + (r.width - iconSz) * 0.5f;
            float iconY = r.y + 4f * m;
            var iconR = new Rect(iconX, iconY, iconSz, iconSz);
            DrawUnitMiniIcon(iconR, type, TintedIconOwnerForUnitOnSide(type, stackOwner));

            float ty = iconR.yMax + 3f * m;
            var sCnt = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(10, Mathf.RoundToInt(13f * m)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                clipping = TextClipping.Overflow
            };
            ApplyTileInfoFont(sCnt);
            GUI.Label(new Rect(r.x, ty, r.width, Mathf.Max(14f * m, r.yMax - ty)), "\u00d7" + count, sCnt);
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
            if (Game.BattlePhaseBlockingPlay || Game.PendingBattleArrangement || Game.BattleClashIntroActive ||
                Game.HasActiveBattleStep || Game.EnergizePromptPlayer != null || Game.FocusFirePicker != null ||
                Game.CasualtyPick != null)
                return;
            if (ShouldPaintFullBattleOverlay(Game.CurrentPlayer))
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
            DrawModalPerimeterClickBlockers(panel);
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




