using UnityEngine;
using UnityEngine.SceneManagement;

namespace NexusGame
{
    public class Bootstrap : MonoBehaviour
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
        string _joinRoomCodeInput = "";

        /// <summary>Design pixels × layout scale (<see cref="GameUiScale.ImGuiHudScale"/>); menu text uses <see cref="GameUiScale.ImGuiFontScale"/>.</summary>
        static float MenuS(float designPixels) => Mathf.Max(1f, designPixels * GameUiScale.ImGuiHudScale());

        void Awake()
        {
            ApplyPortraitForMobile();
            EnsureCamera();
            EnsureLight();
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
                _menuButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true
                };
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
            // VS AI / AI test / online always uses the 1v1 board (two seats).
            board.LayoutMode = _vsAi || _aiVsAi || online ? BoardLayoutMode.OneVOne : _selectedLayout;
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
            game.VsAiMode = _vsAi && !_aiVsAi && !online;
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
            input.SuppressMovementDiagnosticLogs = _vsAi || _aiVsAi;

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
            ai.enabled = (_vsAi || _aiVsAi) && !online;

            BoardBackground.EnsureLoaded();
        }

        void StartOnlineMatch()
        {
            int localSeat = NexusLobbyService.IsHost ? 0 : 1;
            NexusSession.ConfigureOnline(localSeat, NexusLobbyService.IsHost, NexusLobbyService.JoinCode);
            _debugMode = false;
            _vsAi = false;
            _aiVsAi = false;
            _selectedLayout = BoardLayoutMode.OneVOne;
            EnsureGameSystems();
            _state = UiState.InGame;
        }

        void OnGUI()
        {
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

        void DrawMainMenu()
        {
            var btnStyle = MenuButtonStyle();
            float padX = MenuS(22f);
            float bw = Mathf.Min(MenuS(440f), Screen.width - padX * 2f);
            float titleH = MenuS(46f);
            float btnH = MenuS(58f);
            float btnGap = MenuS(12f);
            float footerH = MenuS(28f);
            const int nBtn = 7;
            float h = titleH + nBtn * (btnH + btnGap) + footerH + MenuS(20f);
            h = Mathf.Min(h, Screen.height - MenuS(24f));
            var rect = new Rect((Screen.width - bw) / 2f, Mathf.Max(MenuS(12f), (Screen.height - h) / 2f), bw, h);
            GUI.Box(rect, "");

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.fontSize = GameUiScale.ComputeBestFitFontSize(titleStyle, "Nexus Ops", rect.width - MenuS(16f),
                titleH - MenuS(4f), 16, GameUiScale.ImGuiScaledFont(26f, 22, 52), false);
            GUI.Label(new Rect(rect.x, rect.y + MenuS(8f), rect.width, titleH), "Nexus Ops", titleStyle);

            float y = rect.y + titleH + MenuS(10f);
            float x = rect.x + padX;
            float innerW = bw - padX * 2f;

            FitSharedMenuButtonFont(btnStyle, innerW, btnH, 11, GameUiScale.ImGuiScaledFont(23f, 18, 52), "Play",
                "Play vs AI", "AI vs AI (test)", "Multiplayer", "Settings", "How to Play", "Debug");

            if (GUI.Button(new Rect(x, y, innerW, btnH), "Play", btnStyle))
            {
                _debugMode = false;
                _vsAi = false;
                _aiVsAi = false;
                NexusSession.ConfigureLocalHotseat();
                _state = UiState.MapSelect;
            }

            y += btnH + btnGap;

            if (GUI.Button(new Rect(x, y, innerW, btnH), "Play vs AI", btnStyle))
            {
                _debugMode = false;
                _vsAi = true;
                _aiVsAi = false;
                NexusSession.ConfigureVsAi();
                _state = UiState.MapSelect;
            }

            y += btnH + btnGap;

            if (GUI.Button(new Rect(x, y, innerW, btnH), "AI vs AI (test)", btnStyle))
            {
                _debugMode = false;
                _vsAi = false;
                _aiVsAi = true;
                NexusSession.ConfigureAiVsAi();
                _state = UiState.MapSelect;
            }

            y += btnH + btnGap;

            if (GUI.Button(new Rect(x, y, innerW, btnH), "Multiplayer", btnStyle))
            {
                NexusSession.Reset();
                NexusLobbyService.Leave();
                _joinRoomCodeInput = "";
                _state = UiState.MultiplayerHub;
            }

            y += btnH + btnGap;

            if (GUI.Button(new Rect(x, y, innerW, btnH), "Settings", btnStyle))
                Debug.Log("Settings: placeholder option.");

            y += btnH + btnGap;

            if (GUI.Button(new Rect(x, y, innerW, btnH), "How to Play", btnStyle))
            {
                _rulebookScroll = Vector2.zero;
                _rulebookTab = 0;
                _state = UiState.Rulebook;
            }

            y += btnH + btnGap;

            if (GUI.Button(new Rect(x, y, innerW, btnH), "Debug", btnStyle))
            {
                _debugMode = true;
                _vsAi = false;
                _aiVsAi = false;
                NexusSession.ConfigureLocalHotseat();
                _state = UiState.MapSelect;
            }

            float footerY = rect.yMax - footerH - MenuS(10f);
            var creditStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            creditStyle.fontSize = GameUiScale.ComputeBestFitFontSize(creditStyle, "Made by Clanker Games Inc",
                rect.width - MenuS(16f), footerH, 10, GameUiScale.ImGuiScaledFont(14f, 12, 26), true);
            GUI.Label(new Rect(rect.x + MenuS(8f), footerY, rect.width - MenuS(16f), footerH),
                "Made by Clanker Games Inc", creditStyle);
        }

        void DrawMapSelect()
        {
            var btnStyle = MenuButtonStyle();
            float padX = MenuS(18f);
            float w = Mathf.Min(MenuS(460f), Screen.width - MenuS(20f));
            float x0 = (Screen.width - w) / 2f;
            float y = MenuS(18f);
            float btnH = MenuS(54f);
            float gap = MenuS(12f);
            float x = x0 + padX;
            float bw = w - padX * 2f;

            var hdr = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            hdr.fontSize = GameUiScale.ComputeBestFitFontSize(hdr, "Select Map", w - MenuS(8f), MenuS(40f) - MenuS(4f),
                16, GameUiScale.ImGuiScaledFont(24f, 18, 44), false);
            GUI.Label(new Rect(x0, y, w, MenuS(40f)), "Select Map", hdr);
            y += MenuS(44f);

            FitSharedMenuButtonFont(btnStyle, bw, btnH, 10, GameUiScale.ImGuiScaledFont(23f, 16, 48), "1v1 Map (Current Board)",
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

            float previewCap = Mathf.Clamp(Screen.height * 0.28f, MenuS(100f), MenuS(200f));
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
                BoardBackground.Remove();
                NexusSession.Reset();
                _state = UiState.MainMenu;
            }
        }

        void DrawMultiplayerHub()
        {
            var btnStyle = MenuButtonStyle();
            float padX = MenuS(18f);
            float w = Mathf.Min(MenuS(460f), Screen.width - MenuS(20f));
            float x0 = (Screen.width - w) / 2f;
            float y = MenuS(18f);
            float btnH = MenuS(54f);
            float gap = MenuS(12f);
            float x = x0 + padX;
            float bw = w - padX * 2f;

            var hdr = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            hdr.fontSize = GameUiScale.ComputeBestFitFontSize(hdr, "Multiplayer", w - MenuS(8f), MenuS(40f) - MenuS(4f),
                16, GameUiScale.ImGuiScaledFont(24f, 18, 44), false);
            GUI.Label(new Rect(x0, y, w, MenuS(40f)), "Multiplayer", hdr);
            y += MenuS(44f);

            var sub = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            sub.fontSize = GameUiScale.ImGuiScaledFont(14f, 11, 28);
            string subText = "Room codes and matchmaking use Unity Gaming Services once linked.";
            float subH = sub.CalcHeight(new GUIContent(subText), bw);
            GUI.Label(new Rect(x, y, bw, subH), subText, sub);
            y += subH + gap;

            FitSharedMenuButtonFont(btnStyle, bw, btnH, 10, GameUiScale.ImGuiScaledFont(22f, 16, 48),
                "Create Room", "Join Room", "Find Match", "Back");

            if (GUI.Button(new Rect(x, y, bw, btnH), "Create Room", btnStyle))
            {
                NexusLobbyService.CreateRoom();
                _state = UiState.MultiplayerLobby;
            }

            y += btnH + gap;

            if (GUI.Button(new Rect(x, y, bw, btnH), "Join Room", btnStyle))
            {
                _joinRoomCodeInput = "";
                _state = UiState.MultiplayerJoin;
            }

            y += btnH + gap;

            if (GUI.Button(new Rect(x, y, bw, btnH), "Find Match", btnStyle))
            {
                NexusLobbyService.StartFindMatch();
                _state = UiState.MultiplayerLobby;
            }

            y += btnH + gap + MenuS(6f);

            if (GUI.Button(new Rect(x, y, bw, btnH), "Back", btnStyle))
            {
                NexusLobbyService.Leave();
                NexusSession.Reset();
                _state = UiState.MainMenu;
            }
        }

        void DrawMultiplayerJoin()
        {
            var btnStyle = MenuButtonStyle();
            float padX = MenuS(18f);
            float w = Mathf.Min(MenuS(460f), Screen.width - MenuS(20f));
            float x0 = (Screen.width - w) / 2f;
            float y = MenuS(18f);
            float btnH = MenuS(54f);
            float gap = MenuS(12f);
            float x = x0 + padX;
            float bw = w - padX * 2f;

            var hdr = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            hdr.fontSize = GameUiScale.ComputeBestFitFontSize(hdr, "Join Room", w - MenuS(8f), MenuS(40f) - MenuS(4f),
                16, GameUiScale.ImGuiScaledFont(24f, 18, 44), false);
            GUI.Label(new Rect(x0, y, w, MenuS(40f)), "Join Room", hdr);
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
            float fieldH = MenuS(52f);
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

            FitSharedMenuButtonFont(btnStyle, bw, btnH, 10, GameUiScale.ImGuiScaledFont(22f, 16, 48), "Join", "Back");

            if (GUI.Button(new Rect(x, y, bw, btnH), "Join", btnStyle))
            {
                if (NexusLobbyService.JoinRoom(_joinRoomCodeInput))
                    _state = UiState.MultiplayerLobby;
            }

            y += btnH + gap;

            if (GUI.Button(new Rect(x, y, bw, btnH), "Back", btnStyle))
                _state = UiState.MultiplayerHub;
        }

        void DrawMultiplayerLobby()
        {
            var btnStyle = MenuButtonStyle();
            float padX = MenuS(18f);
            float w = Mathf.Min(MenuS(460f), Screen.width - MenuS(20f));
            float x0 = (Screen.width - w) / 2f;
            float y = MenuS(18f);
            float btnH = MenuS(52f);
            float gap = MenuS(10f);
            float x = x0 + padX;
            float bw = w - padX * 2f;

            bool searching = NexusLobbyService.Phase == NexusLobbyService.LobbyPhase.Searching;
            string title = searching ? "Finding Match" : "Room";
            var hdr = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            hdr.fontSize = GameUiScale.ComputeBestFitFontSize(hdr, title, w - MenuS(8f), MenuS(40f) - MenuS(4f),
                16, GameUiScale.ImGuiScaledFont(24f, 18, 44), false);
            GUI.Label(new Rect(x0, y, w, MenuS(40f)), title, hdr);
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

            if (!searching && !string.IsNullOrEmpty(NexusLobbyService.JoinCode))
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

            var countStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            countStyle.fontSize = GameUiScale.ImGuiScaledFont(14f, 11, 26);
            string players = $"Players: {NexusLobbyService.PlayersInRoom}/2";
            GUI.Label(new Rect(x, y, bw, MenuS(28f)), players, countStyle);
            y += MenuS(34f);

            FitSharedMenuButtonFont(btnStyle, bw, btnH, 9, GameUiScale.ImGuiScaledFont(18f, 14, 44),
                "Simulate opponent (dev)", "Simulate match found (dev)", "Start Match", "Leave");

            if (NexusLobbyService.Phase == NexusLobbyService.LobbyPhase.InRoom &&
                NexusLobbyService.PlayersInRoom < 2)
            {
                if (GUI.Button(new Rect(x, y, bw, btnH), "Simulate opponent (dev)", btnStyle))
                    NexusLobbyService.SimulateOpponentJoined();
                y += btnH + gap;
            }

            if (searching)
            {
                if (GUI.Button(new Rect(x, y, bw, btnH), "Simulate match found (dev)", btnStyle))
                    NexusLobbyService.SimulateMatchFound();
                y += btnH + gap;
            }

            bool canStart = NexusLobbyService.IsReadyToStart && NexusLobbyService.IsHost;
            GUI.enabled = canStart;
            if (GUI.Button(new Rect(x, y, bw, btnH), "Start Match", btnStyle))
                StartOnlineMatch();
            GUI.enabled = true;
            y += btnH + gap;

            if (!canStart && NexusLobbyService.IsReadyToStart && !NexusLobbyService.IsHost)
            {
                var wait = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true
                };
                wait.fontSize = GameUiScale.ImGuiScaledFont(13f, 10, 24);
                GUI.Label(new Rect(x, y, bw, MenuS(36f)), "Waiting for host to start…", wait);
                y += MenuS(40f);
            }

            if (GUI.Button(new Rect(x, y, bw, btnH), "Leave", btnStyle))
            {
                NexusLobbyService.Leave();
                _state = UiState.MultiplayerHub;
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
        }

        void DrawRulebook()
        {
            EnsureRulebookStyles();

            _rulebookBodyStyle.fontSize = GameUiScale.ImGuiScaledFont(15f, 11, 32);

            float pad = MenuS(14f);
            var panel = new Rect(pad, pad, Screen.width - 2f * pad, Screen.height - 2f * pad);
            GUI.Box(panel, "");

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
            float tabH = MenuS(48f);
            float tabW = (panel.width - MenuS(40f)) * 0.5f;
            float tabY = panel.y + MenuS(36f);
            FitSharedMenuButtonFont(tabStyle, tabW - MenuS(6f), tabH, 11, GameUiScale.ImGuiScaledFont(18f, 14, 40), "Rules",
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

            float backH = MenuS(54f);
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
            FitSharedMenuButtonFont(backStyle, backW, backH, 11, GameUiScale.ImGuiScaledFont(20f, 14, 40), "Back to menu");
            if (GUI.Button(
                    new Rect(panel.x + (panel.width - backW) * 0.5f, panel.yMax - backH - MenuS(12f), backW, backH),
                    "Back to menu", backStyle))
            {
                BoardBackground.Remove();
                _state = UiState.MainMenu;
            }
        }
    }
}

