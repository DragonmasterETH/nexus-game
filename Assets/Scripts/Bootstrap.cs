using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NexusGame
{
    public partial class Bootstrap : MonoBehaviour
    {
        enum UiState
        {
            MainMenu,
            MapSelect,
            Rulebook,
            MultiplayerHub,
            MultiplayerJoin,
            MultiplayerLobby,
            InGame
        }

        public Texture2D TwoToFourPlayerMapPreview;

        UiState _state = UiState.MainMenu;
        BoardLayoutMode _selectedLayout = BoardLayoutMode.OneVOne;
        bool _debugMode;
        bool _vsAi;
        bool _aiVsAi;

        Vector2 _rulebookScroll;
        GUIStyle _rulebookBodyStyle;
        int _rulebookTab; // 0 = rules, 1 = units

        GUIStyle _menuButtonStyle;
        GUIStyle _menuPanelStyle;
        Texture2D _menuDimTexture;
        string _joinRoomCodeInput = "";

        /// <summary>Design pixels × layout scale; menus use an extra tap-target bump on top of <see cref="GameUiScale.ImGuiHudScale"/>.</summary>
        const float MenuLayoutMul = 1.22f;
        const float MenuCameraHeight = 16f;
        const float GameCameraHeight = 12f;

        static float MenuS(float designPixels) =>
            Mathf.Max(1f, designPixels * GameUiScale.ImGuiHudScale() * MenuLayoutMul);

        void Awake()
        {
            ApplyPortraitForMobile();
            EnsureCamera();
            EnsureLight();
            NexusUgsRunner.EnsureExists();
            NexusAudioSettings.EnsureLoaded();
            _ = NexusUgsAuth.EnsureServicesInitializedAsync();
            NexusOnlineBridge.MatchStartRequested += OnNetworkMatchStartRequested;
            NexusLobbyService.OnMatchmakingMatchReady += OnMatchmakingMatchReady;
            NexusLobbyService.OnClientRelayReady += OnClientMatchNetworkReady;
            NexusLobbyService.OnSignInRequired += ShowSignInRequiredPopup;
            NexusUgsAuth.OnAuthStateChanged += HandleMultiplayerAuthStateChanged;
            BoardMapPreview.ClearCache();
            EnsureMenuPresentation();
        }

        static bool IsMenuUiState(UiState state) => state != UiState.InGame;

        void EnsureMenuPresentation()
        {
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.transform.SetPositionAndRotation(
                    new Vector3(0f, MenuCameraHeight, 0f),
                    Quaternion.Euler(90f, 0f, 0f));
            }

            var boardCam = FindObjectOfType<BoardCameraPanZoom>();
            if (boardCam != null)
                boardCam.enabled = false;

            BoardBackground.EnsureLoaded(BoardBackground.Presentation.Menu);
        }

        void EnsureGamePresentation()
        {
            var boardCam = FindObjectOfType<BoardCameraPanZoom>();
            if (boardCam != null)
                boardCam.enabled = true;

            var mainCam = Camera.main;
            if (mainCam != null)
            {
                var p = mainCam.transform.position;
                if (p.y < GameCameraHeight * 0.5f)
                    mainCam.transform.position = new Vector3(p.x, GameCameraHeight, p.z);
            }
        }

        static Texture2D CreateMenuTintTexture(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        void EnsureMenuPanelStyle()
        {
            if (_menuPanelStyle != null)
                return;

            _menuPanelStyle = new GUIStyle(GUI.skin.box);
            var panelBg = CreateMenuTintTexture(new Color(0.06f, 0.07f, 0.12f, 0.9f));
            _menuPanelStyle.normal.background = panelBg;
            _menuPanelStyle.border = new RectOffset(12, 12, 12, 12);
            _menuPanelStyle.padding = new RectOffset(8, 8, 8, 8);
        }

        void DrawMenuBackdrop()
        {
            if (_menuDimTexture == null)
                _menuDimTexture = CreateMenuTintTexture(new Color(0f, 0f, 0f, 0.28f));
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _menuDimTexture);
        }

        void DrawMenuPanel(Rect rect)
        {
            EnsureMenuPanelStyle();
            GUI.Box(rect, GUIContent.none, _menuPanelStyle);
        }

        void OnDestroy()
        {
            NexusOnlineBridge.MatchStartRequested -= OnNetworkMatchStartRequested;
            NexusLobbyService.OnMatchmakingMatchReady -= OnMatchmakingMatchReady;
            NexusLobbyService.OnClientRelayReady -= OnClientMatchNetworkReady;
            NexusLobbyService.OnSignInRequired -= ShowSignInRequiredPopup;
            NexusUgsAuth.OnAuthStateChanged -= HandleMultiplayerAuthStateChanged;
        }

        bool _showSignInRequiredPopup;
        System.Action _pendingMultiplayerAction;

        void ShowSignInRequiredPopup()
        {
            _showSignInRequiredPopup = true;
        }

        void HandleMultiplayerAuthStateChanged()
        {
            if (!NexusUgsAuth.IsMultiplayerAuthorized)
                return;

            _showSignInRequiredPopup = false;
            var pending = _pendingMultiplayerAction;
            _pendingMultiplayerAction = null;
            pending?.Invoke();
        }

        /// <summary>Returns false and shows sign-in popup when platform auth is required.</summary>
        protected bool RequireMultiplayerSignIn(System.Action onAuthorized)
        {
            if (NexusUgsAuth.IsMultiplayerAuthorized)
            {
                onAuthorized?.Invoke();
                return true;
            }

            _pendingMultiplayerAction = onAuthorized;
            _showSignInRequiredPopup = false;
            NexusLobbyService.RequestSignInForMultiplayer();
            return false;
        }

        void BeginInteractivePlayGamesSignIn()
        {
            _showSignInRequiredPopup = false;
            NexusLobbyService.RequestSignInForMultiplayer();
        }

        void OnMatchmakingMatchReady()
        {
            if (_state == UiState.InGame)
                return;

            if (NexusLobbyService.IsStealthBotMatch)
                EnterStealthBotMatch();
            else
                StartOnlineMatch();
        }

        void DrawMultiplayerSignInBanner(float x, ref float y, float bw, float gap)
        {
            var statusStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            statusStyle.fontSize = GameUiScale.ImGuiScaledFont(13f, 11, 24);

            string line = NexusUgsAuth.MultiplayerStatusLine();
            if (NexusLobbyService.IsBusy && !NexusUgsAuth.IsMultiplayerAuthorized)
                line = "Signing in…";

            statusStyle.normal.textColor = NexusUgsAuth.IsMultiplayerAuthorized
                ? new Color(0.45f, 0.95f, 0.55f)
                : NexusLobbyService.Phase == NexusLobbyService.LobbyPhase.Error
                    ? new Color(1f, 0.55f, 0.45f)
                    : Color.white;

            float h = statusStyle.CalcHeight(new GUIContent(line), bw);
            GUI.Label(new Rect(x, y, bw, h), line, statusStyle);
            y += h + gap;

            if (!NexusUgsAuth.IsMultiplayerAuthorized && !NexusLobbyService.IsBusy)
            {
                var btnStyle = MenuButtonStyle();
                float btnH = MenuS(52f);
                FitSharedMenuButtonFont(btnStyle, bw, btnH, 12, GameUiScale.ImGuiScaledFont(20f, 16, 32),
                    "Retry Sign-In");
                if (GUI.Button(new Rect(x, y, bw, btnH), "Retry Sign-In", btnStyle))
                    BeginInteractivePlayGamesSignIn();

                y += btnH + gap;
            }
        }

        void DrawSignInRequiredPopup()
        {
            if (!_showSignInRequiredPopup)
                return;

            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;

            float w = Mathf.Min(MenuS(420f), Screen.width - MenuS(32f));
            float pad = MenuS(18f);
            float btnH = MenuS(56f);
            float btnGap = MenuS(10f);
            float titleH = MenuS(56f);
            float innerW = w - pad * 2f;

            var bodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            bodyStyle.fontSize = GameUiScale.ImGuiScaledFont(14f, 11, 24);
            string detail = NexusUgsAuth.IsMultiplayerAuthorized
                ? "Signed in."
                : NexusLobbyService.IsBusy
                    ? $"Opening {NexusPlatformSignIn.RequiredPlatformLabel} sign-in…"
                    : string.IsNullOrEmpty(NexusUgsAuth.LastError)
                        ? $"Sign in with {NexusPlatformSignIn.RequiredPlatformLabel} to use matchmaking and private rooms."
                        : NexusUgsAuth.LastError;
            float bodyH = Mathf.Min(bodyStyle.CalcHeight(new GUIContent(detail), innerW), MenuS(160f));

            float panelH = MenuS(20f) + titleH + MenuS(12f) + bodyH + MenuS(16f) +
                           btnH + btnGap + btnH + MenuS(20f);
            panelH = Mathf.Min(panelH, Screen.height - MenuS(24f));
            var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - panelH) * 0.5f, w, panelH);

            DrawSignInModalPerimeterBlockers(panel);
            DrawMenuPanel(panel);

            float x = panel.x + pad;
            float y = panel.y + MenuS(20f);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            titleStyle.fontSize = GameUiScale.ImGuiScaledFont(22f, 18, 36);
            titleStyle.normal.textColor = new Color(1f, 0.82f, 0.45f);
            GUI.Label(new Rect(x, y, innerW, titleH), "Must be signed in to play", titleStyle);
            y += titleH + MenuS(12f);

            GUI.Label(new Rect(x, y, innerW, bodyH), detail, bodyStyle);
            y += bodyH + MenuS(16f);

            var btnStyle = MenuButtonStyle();
            FitSharedMenuButtonFont(btnStyle, innerW, btnH, 12, GameUiScale.ImGuiScaledFont(20f, 16, 32),
                "Try Again", "Close");

            bool prevEnabled = GUI.enabled;
            GUI.enabled = true;

            if (GUI.Button(new Rect(x, y, innerW, btnH), "Try Again", btnStyle))
                BeginInteractivePlayGamesSignIn();

            y += btnH + btnGap;

            if (GUI.Button(new Rect(x, y, innerW, btnH), "Close", btnStyle))
                CloseSignInRequiredPopup();

            GUI.enabled = prevEnabled;
        }

        static void DrawSignInModalPerimeterBlockers(Rect windowRect)
        {
            float sw = Screen.width;
            float sh = Screen.height;
            var none = GUIStyle.none;
            if (windowRect.x > 0f)
                GUI.Button(new Rect(0f, 0f, windowRect.x, sh), GUIContent.none, none);
            if (windowRect.xMax < sw)
                GUI.Button(new Rect(windowRect.xMax, 0f, Mathf.Max(0f, sw - windowRect.xMax), sh), GUIContent.none,
                    none);
            if (windowRect.y > 0f)
                GUI.Button(new Rect(windowRect.x, 0f, windowRect.width, windowRect.y), GUIContent.none, none);
            if (windowRect.yMax < sh)
                GUI.Button(new Rect(windowRect.x, windowRect.yMax, windowRect.width,
                        Mathf.Max(0f, sh - windowRect.yMax)),
                    GUIContent.none, none);
        }

        void CloseSignInRequiredPopup()
        {
            _showSignInRequiredPopup = false;
            _pendingMultiplayerAction = null;
            NexusLobbyService.CancelSignInAttempt();
            NexusLobbyService.Leave();
            if (_state != UiState.MainMenu)
                _state = UiState.MainMenu;
        }

        void DrawPlayGamesSignInProgressHint()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            style.fontSize = GameUiScale.ImGuiScaledFont(15f, 12, 28);
            style.normal.textColor = new Color(0.75f, 0.92f, 1f, 1f);
            float h = MenuS(72f);
            var r = new Rect(MenuS(16f), MenuS(12f), Screen.width - MenuS(32f), h);
            GUI.Label(r, $"Opening {NexusPlatformSignIn.RequiredPlatformLabel} sign-in…", style);
        }

        /// <summary>Lock handheld builds to portrait (Project Settings also default to portrait).</summary>
        static void ApplyPortraitForMobile()
        {
#if UNITY_ANDROID || UNITY_IOS
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
            Screen.orientation = ScreenOrientation.Portrait;
#endif
        }

        GUIStyle MenuButtonStyle()
        {
            if (_menuButtonStyle == null)
            {
                int pad = Mathf.Max(10, Mathf.RoundToInt(MenuS(14f)));
                _menuButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true,
                    padding = new RectOffset(pad, pad, pad, pad)
                };
                NexusUiFonts.ApplyTo(_menuButtonStyle);
            }

            return _menuButtonStyle;
        }

        /// <summary>
        /// One shared menu button style must fit every label in <paramref name="labels"/> inside the same pixel size
        /// (uses the tightest font).
        /// </summary>
        static void FitSharedMenuButtonFont(GUIStyle style, float buttonWidth, float buttonHeight, int minSize, int maxSize,
            params string[] labels)
        {
            float iw = Mathf.Max(1f, buttonWidth - style.padding.horizontal);
            float ih = Mathf.Max(1f, buttonHeight - style.padding.vertical);
            var content = new GUIContent();
            int best = maxSize;
            foreach (var label in labels)
            {
                content.text = label;
                int fit = GameUiScale.ComputeBestFitFontSize(style, content, iw, ih, minSize, maxSize, true);
                best = Mathf.Min(best, fit);
            }

            style.fontSize = best;
        }

        void EnsureCamera()
        {
            var mainCam = Camera.main;
            if (mainCam == null)
            {
                var camGo = new GameObject("Main Camera");
                mainCam = camGo.AddComponent<Camera>();
                camGo.tag = "MainCamera";
            }

            // True top-down so board sprites/quads read without perspective skew.
            const float initialHeight = 12f;
            mainCam.transform.position = new Vector3(0f, initialHeight, 0f);
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = new Color(0.02f, 0.02f, 0.05f);

            if (mainCam.GetComponent<BoardCameraPanZoom>() == null)
                mainCam.gameObject.AddComponent<BoardCameraPanZoom>();
            var boardCam = mainCam.GetComponent<BoardCameraPanZoom>();
            mainCam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        /// <summary>Reloads the scene so the main menu shows (same as a fresh launch).</summary>
        public void ReturnToMainMenu()
        {
            NexusConnectionMonitor.StopMonitoring();
            NexusLobbyService.Leave();
            NexusSession.Reset();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void EnsureLight()
        {
            if (FindObjectOfType<Light>() != null)
                return;

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, 30f, 0f);
        }

        void EnsureGameSystems()
        {
            var board = FindObjectOfType<BoardGenerator>();
            if (board == null)
            {
                var boardGo = new GameObject("Board");
                board = boardGo.AddComponent<BoardGenerator>();
            }

            bool online = NexusSession.IsOnline;
            bool stealthBot = NexusSession.StealthBotOpponent && !online;
            bool hostRunsOnlineBots = online && NexusSession.IsHost && NexusSession.BotSeatCount > 0;
            if (online || stealthBot)
            {
                // The room size decides the board: 1v1 rooms use the duel board, 3-4 seat rooms the 2-4 map.
                board.LayoutMode = NexusSession.RoomSize > 2 ? BoardLayoutMode.TwoToFour : BoardLayoutMode.OneVOne;
            }
            else if (_aiVsAi)
            {
                board.LayoutMode = BoardLayoutMode.OneVOne;
            }
            else
            {
                board.LayoutMode = _selectedLayout;
            }

            board.Regenerate();

            var game = FindObjectOfType<GameController>();
            if (game == null)
            {
                var gameGo = new GameObject("GameController");
                GameController.SkipStartInitOnce = true;
                game = gameGo.AddComponent<GameController>();
                game.Board = board;
            }

            game.Board = board;
            game.AiVsAiMode = _aiVsAi && !online;
            game.VsAiMode = (_vsAi || stealthBot) && !_aiVsAi && !online;
            game.AiPlayerIndex = 1;
            game.ResetAndStartNewMatch();
            NexusGameCommands.Game = game;

            var input = FindObjectOfType<MobileInputController>();
            if (input == null)
            {
                var inputGo = new GameObject("InputController");
                input = inputGo.AddComponent<MobileInputController>();
                input.Game = game;
            }

            input.Game = game;
            input.DebugClicks = _debugMode;
            input.SuppressMovementDiagnosticLogs = _vsAi || _aiVsAi || stealthBot || hostRunsOnlineBots;

            var hud = FindObjectOfType<DemoHUD>();
            if (hud == null)
            {
                var hudGo = new GameObject("DemoHUD");
                hud = hudGo.AddComponent<DemoHUD>();
                hud.Game = game;
                hud.InputController = input;
            }

            hud.Game = game;
            hud.InputController = input;
            hud.ShowDebugToggle = _debugMode;
            if (hud.RubiumIcon == null && hud.RubiumSprite == null)
            {
                var rub = NexusGuiArt.Load("Sprites/Rubium", "Sprites/rubium");
                if (rub.Sprite != null)
                    hud.RubiumSprite = rub.Sprite;
                else if (rub.Texture != null)
                    hud.RubiumIcon = rub.Texture;
            }

            if (hud.VPIcon == null && hud.VPSprite == null)
            {
                var vp = NexusGuiArt.Load("Sprites/VP", "Sprites/Vp");
                if (vp.Sprite != null)
                    hud.VPSprite = vp.Sprite;
                else if (vp.Texture != null)
                    hud.VPIcon = vp.Texture;
            }

            if (hud.OreChip1Icon == null && hud.OreChip1Sprite == null)
            {
                var o = NexusGuiArt.Load("Sprites/OreChip1", "Sprites/Ore_Chip_1", "Sprites/Ore Chip 1");
                if (o.Sprite != null)
                    hud.OreChip1Sprite = o.Sprite;
                else if (o.Texture != null)
                    hud.OreChip1Icon = o.Texture;
            }

            if (hud.OreChip2Icon == null && hud.OreChip2Sprite == null)
            {
                var o = NexusGuiArt.Load("Sprites/OreChip2", "Sprites/Ore_Chip_2", "Sprites/Ore Chip 2");
                if (o.Sprite != null)
                    hud.OreChip2Sprite = o.Sprite;
                else if (o.Texture != null)
                    hud.OreChip2Icon = o.Texture;
            }

            if (hud.OreChip3Icon == null && hud.OreChip3Sprite == null)
            {
                var o = NexusGuiArt.Load("Sprites/OreChip3", "Sprites/Ore_Chip_3", "Sprites/Ore Chip 3");
                if (o.Sprite != null)
                    hud.OreChip3Sprite = o.Sprite;
                else if (o.Texture != null)
                    hud.OreChip3Icon = o.Texture;
            }

            var ai = game.GetComponent<SimpleAiController>();
            if (ai == null)
                ai = game.gameObject.AddComponent<SimpleAiController>();
            ai.Game = game;
            ai.Input = input;
            ai.enabled = ((_vsAi || _aiVsAi || stealthBot) && !online) || hostRunsOnlineBots;
            if (stealthBot || (hostRunsOnlineBots && NexusSession.StealthBotOpponent))
                ai.EnableHumanLikePacing();

            EnsureGamePresentation();
            BoardBackground.EnsureLoaded(BoardBackground.Presentation.Game);
        }

        void StartOnlineMatch()
        {
            if (NexusLobbyService.IsBusy)
                return;

            if (!NexusUgsAuth.IsMultiplayerAuthorized || !NexusLobbyService.UseLiveServices)
            {
                RequireMultiplayerSignIn(StartOnlineMatch);
                return;
            }

            int localSeat = NexusLobbyService.IsHost
                ? 0
                : Mathf.Max(1, NexusLobbyService.LocalSeatIndex);

            NexusLobbyService.BeginMatchConnection(
                () => EnterOnlineMatch(localSeat, NexusLobbyService.IsHost),
                msg => Debug.LogWarning($"[Lobby] Match start failed: {msg}"));
        }

        void OnNetworkMatchStartRequested()
        {
            TryEnterClientOnlineMatch();
        }

        void OnClientMatchNetworkReady()
        {
            TryEnterClientOnlineMatch();
        }

        void TryEnterClientOnlineMatch()
        {
            if (_state == UiState.InGame || NexusLobbyService.IsHost)
                return;

            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening)
            {
                EnterClientOnlineMatchNow();
                return;
            }

            if (_clientEnterRoutine == null)
                _clientEnterRoutine = StartCoroutine(WaitForClientNetworkThenEnter());
        }

        Coroutine _clientEnterRoutine;

        System.Collections.IEnumerator WaitForClientNetworkThenEnter()
        {
            const float timeout = 30f;
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                if (_state == UiState.InGame || NexusLobbyService.IsHost)
                    yield break;

                var nm = NetworkManager.Singleton;
                if (nm != null && nm.IsListening)
                {
                    EnterClientOnlineMatchNow();
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (_state != UiState.InGame && !NexusLobbyService.IsHost)
                EnterClientOnlineMatchNow();
        }

        void EnterClientOnlineMatchNow()
        {
            if (_clientEnterRoutine != null)
            {
                StopCoroutine(_clientEnterRoutine);
                _clientEnterRoutine = null;
            }

            NexusOnlineBridge.EnsureSyncHandlerRegistered();
            EnterOnlineMatch(Mathf.Max(1, NexusLobbyService.LocalSeatIndex), false);
        }

        void EnterStealthBotMatch()
        {
            int totalSeats = Mathf.Clamp(NexusLobbyService.SeatedCount, 2, NexusLobbyService.MaxRoomSize);
            NexusSession.ConfigureStealthBotMatch(totalSeats);
            _debugMode = false;
            _vsAi = true;
            _aiVsAi = false;
            _selectedLayout = totalSeats > 2 ? BoardLayoutMode.TwoToFour : BoardLayoutMode.OneVOne;
            EnsureGameSystems();
            _state = UiState.InGame;
        }

        void EnterOnlineMatch(int localSeat, bool isHost)
        {
            int roomSize = Mathf.Clamp(NexusLobbyService.RoomSize, 2, NexusLobbyService.MaxRoomSize);
            int totalSeats;
            int humanSeats;
            bool stealthBots;
            if (isHost)
            {
                humanSeats = Mathf.Clamp(NexusLobbyService.PlayersInRoom, 1, roomSize);
                totalSeats = Mathf.Clamp(NexusLobbyService.SeatedCount, 2, roomSize);
                stealthBots = NexusLobbyService.IsStealthBackfillMatch;
            }
            else
            {
                // Prefer the setup the host sent with the match-start RPC; fall back to lobby state.
                totalSeats = NexusOnlineBridge.PendingMatchTotalSeats > 0
                    ? NexusOnlineBridge.PendingMatchTotalSeats
                    : Mathf.Clamp(NexusLobbyService.SeatedCount, 2, roomSize);
                humanSeats = NexusOnlineBridge.PendingMatchHumanSeats > 0
                    ? NexusOnlineBridge.PendingMatchHumanSeats
                    : Mathf.Clamp(NexusLobbyService.PlayersInRoom, 1, totalSeats);
                // Fallback: in matchmaking any bot seat is a disguised backfill bot.
                stealthBots = NexusOnlineBridge.PendingMatchTotalSeats > 0
                    ? NexusOnlineBridge.PendingMatchStealthBots
                    : NexusLobbyService.FromMatchmakingQueue;
            }

            NexusSession.ConfigureOnline(localSeat, isHost, NexusLobbyService.JoinCode,
                roomSize, totalSeats, humanSeats, stealthBots);
            NexusOnlineBridge.ClearPendingMatchSetup();
            _debugMode = false;
            _vsAi = false;
            _aiVsAi = false;
            _selectedLayout = roomSize > 2 ? BoardLayoutMode.TwoToFour : BoardLayoutMode.OneVOne;
            EnsureGameSystems();

            if (NexusSession.IsOnline)
            {
                var game = NexusGameCommands.Game;
                game?.EnableOnlineReplication();

                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                    game?.NotifyOnlineStateChanged();
                else
                {
                    NexusOnlineBridge.EnsureSyncHandlerRegistered();
                    NexusOnlineBridge.FlushStaticPendingSnapshot();
                    NexusGameCommands.Bridge?.FlushPendingSnapshot();
                    if (NexusGameCommands.Bridge != null)
                        NexusGameCommands.Bridge.RequestFullStateFromServer();
                    else
                        NexusOnlineBridge.SendRequestFullStateIntent();
                }

                NexusConnectionMonitor.BeginMonitoringMatch();
            }

            _state = UiState.InGame;
        }

        void OnGUI()
        {
            NexusUiFonts.EnsureImGuiSkinFonts();

            if (_showSignInRequiredPopup)
            {
                if (IsMenuUiState(_state))
                {
                    if (GameObject.Find("BoardBackground") == null)
                        EnsureMenuPresentation();
                    DrawMenuBackdrop();
                }

                DrawSignInRequiredPopup();
                return;
            }

            if (NexusLobbyService.IsBusy && !NexusUgsAuth.IsMultiplayerAuthorized)
                DrawPlayGamesSignInProgressHint();

            if (IsMenuUiState(_state))
            {
                if (GameObject.Find("BoardBackground") == null)
                    EnsureMenuPresentation();
                DrawMenuBackdrop();
            }

            switch (_state)
            {
                case UiState.MainMenu:
                    DrawMainMenu();
                    break;
                case UiState.MapSelect:
                    DrawMapSelect();
                    break;
                case UiState.Rulebook:
                    DrawRulebook();
                    break;
                case UiState.MultiplayerHub:
                    DrawMultiplayerHub();
                    break;
                case UiState.MultiplayerJoin:
                    DrawMultiplayerJoin();
                    break;
                case UiState.MultiplayerLobby:
                    DrawMultiplayerLobby();
                    break;
                case UiState.InGame:
                    // In-game UI is handled by DemoHUD.
                    break;
            }

        }

        void DrawMapSelect()
        {
            var btnStyle = MenuButtonStyle();
            float padX = MenuS(18f);
            float w = Mathf.Min(MenuS(460f), Screen.width - MenuS(20f));
            float btnH = MenuS(68f);
            float gap = MenuS(14f);
            float previewCap = Mathf.Clamp(Screen.height * 0.22f, MenuS(100f), MenuS(180f));
            float panelH = Mathf.Min(Screen.height - MenuS(20f), Screen.height * 0.92f);
            float x0 = (Screen.width - w) / 2f;
            float y0 = Mathf.Max(MenuS(10f), (Screen.height - panelH) * 0.5f);
            var panel = new Rect(x0, y0, w, panelH);
            DrawMenuPanel(panel);

            float x = panel.x + padX;
            float bw = panel.width - padX * 2f;
            float y = panel.y + MenuS(10f);

            var hdr = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            hdr.fontSize = GameUiScale.ComputeBestFitFontSize(hdr, "Select Map", w - MenuS(8f), MenuS(40f) - MenuS(4f),
                18, GameUiScale.ImGuiScaledFont(28f, 22, 52), false);
            GUI.Label(new Rect(panel.x, y, panel.width, MenuS(40f)), "Select Map", hdr);
            y += MenuS(44f);

            FitSharedMenuButtonFont(btnStyle, bw, btnH, 14, GameUiScale.ImGuiScaledFont(27f, 20, 56), "1v1 Map (Current Board)",
                "1v1 Battle Test (3 Hex)", "2–4 Map A (radius-3)", "2–4 Map B (12-6-1)", "Back");

            if (GUI.Button(new Rect(x, y, bw, btnH), "1v1 Map (Current Board)", btnStyle))
            {
                _selectedLayout = BoardLayoutMode.OneVOne;
                EnsureGameSystems();
                _state = UiState.InGame;
            }

            y += btnH + gap;

            if (GUI.Button(new Rect(x, y, bw, btnH), "1v1 Battle Test (3 Hex)", btnStyle))
            {
                _selectedLayout = BoardLayoutMode.BattleTest;
                EnsureGameSystems();
                _state = UiState.InGame;
            }

            y += btnH + gap;

            var subHdr = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            subHdr.fontSize = GameUiScale.ComputeBestFitFontSize(subHdr, "2–4 Player Maps:", bw, MenuS(28f) - MenuS(2f),
                12, GameUiScale.ImGuiScaledFont(19f, 14, 34), false);
            GUI.Label(new Rect(x, y, bw, MenuS(28f)), "2–4 Player Maps:", subHdr);
            y += MenuS(30f);

            if (TwoToFourPlayerMapPreview != null)
            {
                GUI.DrawTexture(new Rect(x, y, bw, previewCap), TwoToFourPlayerMapPreview, ScaleMode.ScaleToFit);
                y += previewCap + gap;
            }
            else
            {
                GUI.Label(new Rect(x, y, bw, MenuS(44f)), "(Assign 2–4 map preview texture)");
                y += MenuS(48f);
            }

            if (GUI.Button(new Rect(x, y, bw, btnH), "2–4 Map A (radius-3)", btnStyle))
            {
                _selectedLayout = BoardLayoutMode.TwoToFour;
                EnsureGameSystems();
                _state = UiState.InGame;
            }

            y += btnH + gap;

            if (GUI.Button(new Rect(x, y, bw, btnH), "2–4 Map B (12-6-1)", btnStyle))
            {
                _selectedLayout = BoardLayoutMode.TwoToFourSmall;
                EnsureGameSystems();
                _state = UiState.InGame;
            }

            y += btnH + gap + MenuS(6f);

            if (GUI.Button(new Rect(x, y, bw, btnH), "Back", btnStyle))
            {
                NexusSession.Reset();
                _state = UiState.MainMenu;
            }
        }

        void DrawMultiplayerHub()
        {
            EnsureColonistMenuStyles();

            var btnStyle = MenuButtonStyle();
            float padX = MenuS(18f);
            float w = Mathf.Min(MenuS(460f), Screen.width - MenuS(20f));
            float btnH = MenuS(68f);
            float gap = MenuS(14f);
            float tabH = MenuS(52f);
            float innerW = w - padX * 2f;

            float signInExtra = MenuS(120f);
            float contentSlotH = btnH * 2f + gap;
            float panelH = MenuS(56f) + signInExtra + tabH + gap + contentSlotH + gap + btnH + MenuS(22f);
            panelH = Mathf.Min(panelH, Screen.height - MenuS(24f));
            float x0 = (Screen.width - w) / 2f;
            float y0 = Mathf.Max(MenuS(12f), (Screen.height - panelH) * 0.5f);
            var panel = new Rect(x0, y0, w, panelH);
            DrawMenuPanel(panel);

            float x = panel.x + padX;
            float bw = innerW;
            float y = panel.y + MenuS(12f);

            var hdr = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            hdr.fontSize = GameUiScale.ComputeBestFitFontSize(hdr, "Multiplayer", w - MenuS(8f), MenuS(40f) - MenuS(4f),
                18, GameUiScale.ImGuiScaledFont(28f, 22, 52), false);
            GUI.Label(new Rect(panel.x, y, panel.width, MenuS(40f)), "Multiplayer", hdr);
            y += MenuS(44f);

            DrawMultiplayerSignInBanner(x, ref y, bw, gap);

            _multiplayerOnlineTab = (MultiplayerOnlineTab)DrawSegmentedRow(
                new Rect(x, y, bw, tabH), new[] { "Matchmaking", "Private Rooms" }, (int)_multiplayerOnlineTab);
            y += tabH + gap;

            var contentSlot = new Rect(x, y, bw, contentSlotH);
            y += contentSlotH + gap + MenuS(6f);

            GUI.enabled = NexusUgsAuth.IsMultiplayerAuthorized && !NexusLobbyService.IsBusy;

            if (_multiplayerOnlineTab == MultiplayerOnlineTab.Matchmaking)
            {
                var hint = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true
                };
                hint.fontSize = GameUiScale.ImGuiScaledFont(14f, 11, 24);
                GUI.Label(new Rect(contentSlot.x, contentSlot.y, contentSlot.width, MenuS(28f)),
                    "Quick match against another player.", hint);

                FitSharedMenuButtonFont(btnStyle, bw, btnH, 14, GameUiScale.ImGuiScaledFont(27f, 20, 56), "Find Match");
                if (GUI.Button(new Rect(contentSlot.x, contentSlot.yMax - btnH, bw, btnH), "Find Match", btnStyle))
                {
                    RequireMultiplayerSignIn(() =>
                    {
                        NexusLobbyService.StartFindMatch(_onlineMatchSize);
                        _state = UiState.MultiplayerLobby;
                    });
                }
            }
            else
            {
                FitSharedMenuButtonFont(btnStyle, bw, btnH, 14, GameUiScale.ImGuiScaledFont(27f, 20, 56),
                    "Create Room", "Join Room");

                if (GUI.Button(new Rect(contentSlot.x, contentSlot.y, bw, btnH), "Create Room", btnStyle))
                {
                    RequireMultiplayerSignIn(() =>
                    {
                        NexusLobbyService.CreateRoom(_onlineMatchSize);
                        _state = UiState.MultiplayerLobby;
                    });
                }

                if (GUI.Button(new Rect(contentSlot.x, contentSlot.y + btnH + gap, bw, btnH), "Join Room", btnStyle))
                {
                    RequireMultiplayerSignIn(() =>
                    {
                        _joinRoomCodeInput = "";
                        _state = UiState.MultiplayerJoin;
                    });
                }
            }

            if (GUI.Button(new Rect(x, y, bw, btnH), "Back", btnStyle))
            {
                NexusLobbyService.Leave();
                NexusSession.Reset();
                _state = UiState.MainMenu;
            }

            GUI.enabled = true;
        }

        void DrawMultiplayerJoin()
        {
            var btnStyle = MenuButtonStyle();
            float padX = MenuS(18f);
            float w = Mathf.Min(MenuS(460f), Screen.width - MenuS(20f));
            float btnH = MenuS(68f);
            float gap = MenuS(14f);
            float fieldH = MenuS(52f);
            float panelH = MenuS(48f) + MenuS(32f) + fieldH + 2f * (btnH + gap) + MenuS(56f);
            float x0 = (Screen.width - w) / 2f;
            float y0 = Mathf.Max(MenuS(12f), (Screen.height - panelH) * 0.5f);
            var panel = new Rect(x0, y0, w, panelH);
            DrawMenuPanel(panel);

            float x = panel.x + padX;
            float bw = panel.width - padX * 2f;
            float y = panel.y + MenuS(12f);

            var hdr = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            hdr.fontSize = GameUiScale.ComputeBestFitFontSize(hdr, "Join Room", w - MenuS(8f), MenuS(40f) - MenuS(4f),
                18, GameUiScale.ImGuiScaledFont(28f, 22, 52), false);
            GUI.Label(new Rect(panel.x, y, panel.width, MenuS(40f)), "Join Room", hdr);
            y += MenuS(48f);

            var lbl = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            lbl.fontSize = GameUiScale.ImGuiScaledFont(15f, 12, 30);
            GUI.Label(new Rect(x, y, bw, MenuS(28f)), "Enter room code", lbl);
            y += MenuS(32f);

            var fieldStyle = new GUIStyle(GUI.skin.textField)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            fieldStyle.fontSize = GameUiScale.ImGuiScaledFont(22f, 16, 40);
            _joinRoomCodeInput = GUI.TextField(new Rect(x, y, bw, fieldH), _joinRoomCodeInput, 8, fieldStyle)
                .ToUpperInvariant();
            y += fieldH + gap;

            if (NexusLobbyService.Phase == NexusLobbyService.LobbyPhase.Error &&
                !string.IsNullOrEmpty(NexusLobbyService.StatusMessage))
            {
                var err = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true,
                    normal = { textColor = new Color(1f, 0.45f, 0.4f) }
                };
                err.fontSize = GameUiScale.ImGuiScaledFont(14f, 11, 26);
                float errH = err.CalcHeight(new GUIContent(NexusLobbyService.StatusMessage), bw);
                GUI.Label(new Rect(x, y, bw, errH), NexusLobbyService.StatusMessage, err);
                y += errH + gap;
            }

            FitSharedMenuButtonFont(btnStyle, bw, btnH, 14, GameUiScale.ImGuiScaledFont(27f, 20, 56), "Join", "Back");

            if (GUI.Button(new Rect(x, y, bw, btnH), "Join", btnStyle))
            {
                RequireMultiplayerSignIn(() =>
                {
                    if (NexusLobbyService.JoinRoom(_joinRoomCodeInput))
                        _state = UiState.MultiplayerLobby;
                });
            }

            y += btnH + gap;

            if (GUI.Button(new Rect(x, y, bw, btnH), "Back", btnStyle))
            {
                _multiplayerOnlineTab = MultiplayerOnlineTab.PrivateRooms;
                _state = UiState.MainMenu;
            }
        }

        void DrawMultiplayerLobby()
        {
            var btnStyle = MenuButtonStyle();
            float padX = MenuS(18f);
            float w = Mathf.Min(MenuS(460f), Screen.width - MenuS(20f));
            float btnH = MenuS(66f);
            float gap = MenuS(10f);
            float panelH = Mathf.Min(Screen.height - MenuS(24f), Screen.height * 0.88f);
            float x0 = (Screen.width - w) / 2f;
            float y0 = Mathf.Max(MenuS(12f), (Screen.height - panelH) * 0.5f);
            var panel = new Rect(x0, y0, w, panelH);
            DrawMenuPanel(panel);

            float x = panel.x + padX;
            float bw = panel.width - padX * 2f;
            float y = panel.y + MenuS(12f);

            bool inQueue = NexusLobbyService.IsInMatchmakingQueue;
            bool searching = NexusLobbyService.Phase == NexusLobbyService.LobbyPhase.Searching;
            bool fromMatchmaking = NexusLobbyService.FromMatchmakingQueue || inQueue || searching;
            string title = fromMatchmaking ? "Matchmaking" : "Private Room";
            var hdr = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            hdr.fontSize = GameUiScale.ComputeBestFitFontSize(hdr, title, w - MenuS(8f), MenuS(40f) - MenuS(4f),
                16, GameUiScale.ImGuiScaledFont(24f, 18, 44), false);
            GUI.Label(new Rect(panel.x, y, panel.width, MenuS(40f)), title, hdr);
            y += MenuS(44f);

            var statusStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            statusStyle.fontSize = GameUiScale.ImGuiScaledFont(14f, 11, 28);
            string status = NexusLobbyService.StatusMessage;
            if (string.IsNullOrEmpty(status))
                status = searching ? "Searching…" : "Waiting for players…";
            float statusH = statusStyle.CalcHeight(new GUIContent(status), bw);
            GUI.Label(new Rect(x, y, bw, statusH), status, statusStyle);
            y += statusH + gap;

            bool matchFound = NexusLobbyService.MatchFound;
            if (matchFound)
            {
                var foundStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.45f, 0.95f, 0.55f) }
                };
                foundStyle.fontSize = GameUiScale.ImGuiScaledFont(28f, 20, 48);
                GUI.Label(new Rect(x, y, bw, MenuS(52f)), "Match found", foundStyle);
                y += MenuS(56f);
            }
            else if (inQueue || searching)
            {
                var queueStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.55f, 0.82f, 1f) }
                };
                queueStyle.fontSize = GameUiScale.ImGuiScaledFont(13f, 11, 22);
                int waitSec = Mathf.FloorToInt(NexusLobbyService.QueueWaitSeconds);
                GUI.Label(new Rect(x, y, bw, MenuS(24f)), $"Searching… {waitSec}s", queueStyle);
                y += MenuS(28f);
            }

            if (NexusLobbyService.Phase == NexusLobbyService.LobbyPhase.Error ||
                (!NexusUgsAuth.IsMultiplayerAuthorized && !NexusLobbyService.UseLiveServices))
            {
                DrawMultiplayerSignInBanner(x, ref y, bw, gap);
            }
            else if (NexusLobbyService.UseLiveServices && !inQueue && !matchFound &&
                     !NexusLobbyService.FromMatchmakingQueue)
            {
                var liveStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.45f, 0.95f, 0.55f) }
                };
                liveStyle.fontSize = GameUiScale.ImGuiScaledFont(12f, 10, 20);
                GUI.Label(new Rect(x, y, bw, MenuS(22f)), "Live room — players sync across devices", liveStyle);
                y += MenuS(26f);
            }

            if (!inQueue && !searching && !matchFound && !NexusLobbyService.FromMatchmakingQueue &&
                !string.IsNullOrEmpty(NexusLobbyService.JoinCode))
            {
                var codeLbl = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
                codeLbl.fontSize = GameUiScale.ImGuiScaledFont(13f, 10, 24);
                GUI.Label(new Rect(x, y, bw, MenuS(24f)), "Room code", codeLbl);
                y += MenuS(26f);

                var codeStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                codeStyle.fontSize = GameUiScale.ImGuiScaledFont(32f, 22, 56);
                GUI.Label(new Rect(x, y, bw, MenuS(48f)), NexusLobbyService.JoinCode, codeStyle);
                y += MenuS(52f);
            }

            if (!matchFound)
            {
                var countStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
                countStyle.fontSize = GameUiScale.ImGuiScaledFont(14f, 11, 26);
                string players = string.IsNullOrEmpty(NexusLobbyService.PlayersLine)
                    ? $"Players: {NexusLobbyService.SeatedCount}/{NexusLobbyService.RoomSize}"
                    : NexusLobbyService.PlayersLine;
                GUI.Label(new Rect(x, y, bw, MenuS(28f)), players, countStyle);
                y += MenuS(34f);
            }

            // Private-room hosts can fill empty seats with AI opponents.
            if (!fromMatchmaking && NexusLobbyService.IsHost &&
                NexusLobbyService.Phase == NexusLobbyService.LobbyPhase.InRoom)
            {
                float halfW = (bw - gap) * 0.5f;
                FitSharedMenuButtonFont(btnStyle, halfW, btnH, 12, GameUiScale.ImGuiScaledFont(20f, 15, 40),
                    "Add Bot", "Remove Bot");

                bool prevEnabled = GUI.enabled;
                GUI.enabled = NexusLobbyService.CanAddBot;
                if (GUI.Button(new Rect(x, y, halfW, btnH), "Add Bot", btnStyle))
                    NexusLobbyService.AddBot();

                GUI.enabled = NexusLobbyService.BotCount > 0;
                if (GUI.Button(new Rect(x + halfW + gap, y, halfW, btnH), "Remove Bot", btnStyle))
                    NexusLobbyService.RemoveBot();

                GUI.enabled = prevEnabled;
                y += btnH + gap;
            }

            FitSharedMenuButtonFont(btnStyle, bw, btnH, 13, GameUiScale.ImGuiScaledFont(24f, 18, 52),
                "Start Match", "Leave");

            bool canManualStart = !inQueue &&
                NexusLobbyService.IsReadyToStart &&
                NexusLobbyService.IsHost &&
                !NexusLobbyService.IsBusy;
            if (canManualStart)
            {
                GUI.enabled = true;
                if (GUI.Button(new Rect(x, y, bw, btnH), "Start Match", btnStyle))
                    StartOnlineMatch();
                y += btnH + gap;
            }
            else if ((inQueue || matchFound) && NexusLobbyService.IsBusy)
            {
                var wait = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true
                };
                wait.fontSize = GameUiScale.ImGuiScaledFont(13f, 10, 24);
                GUI.Label(new Rect(x, y, bw, MenuS(36f)), "Starting match…", wait);
                y += MenuS(40f);
            }
            else if (matchFound)
            {
                var wait = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true
                };
                wait.fontSize = GameUiScale.ImGuiScaledFont(13f, 10, 24);
                GUI.Label(new Rect(x, y, bw, MenuS(36f)), "Starting match…", wait);
                y += MenuS(40f);
            }
            else if (!inQueue && NexusLobbyService.IsReadyToStart && !NexusLobbyService.IsHost)
            {
                var wait = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true
                };
                wait.fontSize = GameUiScale.ImGuiScaledFont(13f, 10, 24);
                string waitText = NexusLobbyService.UseLiveServices &&
                                  NexusLobbyService.IsClientMatchNetworkStarted
                    ? "Host started — joining match…"
                    : "Waiting for host to start…";
                GUI.Label(new Rect(x, y, bw, MenuS(36f)), waitText, wait);
                y += MenuS(40f);
            }

            string leaveLabel = inQueue || searching ? "Cancel Queue" : "Leave";
            if (GUI.Button(new Rect(x, y, bw, btnH), leaveLabel, btnStyle))
            {
                NexusLobbyService.Leave();
                _state = UiState.MainMenu;
            }
        }

        void EnsureRulebookStyles()
        {
            if (_rulebookBodyStyle != null)
                return;
            _rulebookBodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                richText = false
            };
            NexusUiFonts.ApplyTo(_rulebookBodyStyle);
        }

        void DrawRulebook()
        {
            EnsureRulebookStyles();

            _rulebookBodyStyle.fontSize = GameUiScale.ImGuiScaledFont(15f, 11, 32);

            float pad = MenuS(14f);
            var panel = new Rect(pad, pad, Screen.width - 2f * pad, Screen.height - 2f * pad);
            DrawMenuPanel(panel);

            string header = _rulebookTab == 0 ? NexusRulebook.Title : NexusUnitQuickReference.Title;
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.fontSize = GameUiScale.ComputeBestFitFontSize(titleStyle, header, panel.width - MenuS(20f),
                MenuS(28f) - MenuS(2f), 14, GameUiScale.ImGuiScaledFont(20f, 16, 40), true);
            GUI.Label(new Rect(panel.x, panel.y + MenuS(8f), panel.width, MenuS(28f)), header, titleStyle);

            var tabStyle = MenuButtonStyle();
            float tabH = MenuS(56f);
            float tabW = (panel.width - MenuS(40f)) * 0.5f;
            float tabY = panel.y + MenuS(36f);
            FitSharedMenuButtonFont(tabStyle, tabW - MenuS(6f), tabH, 14, GameUiScale.ImGuiScaledFont(22f, 18, 48), "Rules",
                "Units");
            if (GUI.Button(new Rect(panel.x + MenuS(14f), tabY, tabW - MenuS(6f), tabH), "Rules", tabStyle))
            {
                if (_rulebookTab != 0)
                    _rulebookScroll = Vector2.zero;
                _rulebookTab = 0;
            }

            if (GUI.Button(new Rect(panel.x + MenuS(14f) + tabW + MenuS(6f), tabY, tabW - MenuS(6f), tabH), "Units",
                    tabStyle))
            {
                if (_rulebookTab != 1)
                    _rulebookScroll = Vector2.zero;
                _rulebookTab = 1;
            }

            string body = _rulebookTab == 0
                ? NexusRulebook.Body
                : NexusUnitQuickReference.Build(null);

            float backH = MenuS(64f);
            float topBlock = MenuS(36f) + tabH + MenuS(8f);
            var scrollRect = new Rect(panel.x + MenuS(12f), panel.y + topBlock, panel.width - MenuS(24f),
                panel.height - topBlock - backH - MenuS(20f));
            float innerW = scrollRect.width - MenuS(22f);
            float contentH = _rulebookBodyStyle.CalcHeight(new GUIContent(body), innerW);
            contentH = Mathf.Max(contentH + MenuS(32f), scrollRect.height * 0.45f);

            _rulebookScroll = GUI.BeginScrollView(scrollRect, _rulebookScroll, new Rect(0f, 0f, innerW, contentH));
            GUI.Label(new Rect(MenuS(8f), MenuS(8f), innerW - MenuS(16f), contentH), body, _rulebookBodyStyle);
            GUI.EndScrollView();

            float backW = Mathf.Min(panel.width - MenuS(40f), MenuS(420f));
            var backStyle = MenuButtonStyle();
            FitSharedMenuButtonFont(backStyle, backW, backH, 14, GameUiScale.ImGuiScaledFont(24f, 18, 48), "Back to menu");
            if (GUI.Button(
                    new Rect(panel.x + (panel.width - backW) * 0.5f, panel.yMax - backH - MenuS(12f), backW, backH),
                    "Back to menu", backStyle))
                _state = UiState.MainMenu;
        }
    }
}

