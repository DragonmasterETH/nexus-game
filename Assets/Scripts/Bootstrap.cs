using UnityEngine;

namespace NexusGame
{
    public class Bootstrap : MonoBehaviour
    {
        enum UiState
        {
            MainMenu,
            MapSelect,
            InGame
        }

        public Texture2D TwoToFourPlayerMapPreview;

        UiState _state = UiState.MainMenu;
        BoardLayoutMode _selectedLayout = BoardLayoutMode.OneVOne;
        bool _debugMode;
        bool _vsAi;

        void Awake()
        {
            EnsureCamera();
            EnsureLight();
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

            mainCam.transform.position = new Vector3(0f, 8f, -8f);
            mainCam.transform.LookAt(Vector3.zero);
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = new Color(0.02f, 0.02f, 0.05f);
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

            // VS AI always uses the 1v1 board (two seats: you vs AI).
            board.LayoutMode = _vsAi ? BoardLayoutMode.OneVOne : _selectedLayout;
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
            game.VsAiMode = _vsAi;
            game.AiPlayerIndex = 1;
            game.ResetAndStartNewMatch();

            var input = FindObjectOfType<MobileInputController>();
            if (input == null)
            {
                var inputGo = new GameObject("InputController");
                input = inputGo.AddComponent<MobileInputController>();
                input.Game = game;
            }

            input.Game = game;
            input.DebugClicks = _debugMode;
            input.SuppressMovementDiagnosticLogs = _vsAi;

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

            var ai = game.GetComponent<SimpleAiController>();
            if (ai == null)
                ai = game.gameObject.AddComponent<SimpleAiController>();
            ai.Game = game;
            ai.Input = input;
            ai.enabled = _vsAi;
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
                case UiState.InGame:
                    // In-game UI is handled by DemoHUD.
                    break;
            }
        }

        void DrawMainMenu()
        {
            const int w = 260;
            const int h = 240;
            var rect = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.Box(rect, "Nexus Ops");

            float y = rect.y + 30f;
            float x = rect.x + 20f;
            float bw = w - 40f;

            if (GUI.Button(new Rect(x, y, bw, 30f), "Play"))
            {
                _debugMode = false;
                _vsAi = false;
                _state = UiState.MapSelect;
            }
            y += 40f;

            if (GUI.Button(new Rect(x, y, bw, 30f), "Play vs AI"))
            {
                _debugMode = false;
                _vsAi = true;
                _state = UiState.MapSelect;
            }
            y += 40f;

            if (GUI.Button(new Rect(x, y, bw, 30f), "Multiplayer"))
            {
                Debug.Log("Multiplayer: placeholder option.");
            }
            y += 40f;

            if (GUI.Button(new Rect(x, y, bw, 30f), "Settings"))
            {
                Debug.Log("Settings: placeholder option.");
            }
            y += 40f;

            if (GUI.Button(new Rect(x, y, bw, 30f), "Help (Rules)"))
            {
                Debug.Log("Help: show rules placeholder.");
            }
            y += 40f;

            if (GUI.Button(new Rect(x, y, bw, 30f), "Debug"))
            {
                // Enter map selection with click-debugging enabled.
                _debugMode = true;
                _vsAi = false;
                _state = UiState.MapSelect;
            }
        }

        void DrawMapSelect()
        {
            const int w = 320;
            const int h = 260;
            var rect = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.Box(rect, "Select Map");

            float y = rect.y + 30f;
            float x = rect.x + 20f;
            float bw = w - 40f;

            if (GUI.Button(new Rect(x, y, bw, 30f), "1v1 Map (Current Board)"))
            {
                _selectedLayout = BoardLayoutMode.OneVOne;
                EnsureGameSystems();
                _state = UiState.InGame;
            }
            y += 40f;

            GUI.Label(new Rect(x, y, bw, 20f), "2–4 Player Maps:");
            y += 25f;

            if (TwoToFourPlayerMapPreview != null)
            {
                float previewHeight = 140f;
                GUI.DrawTexture(new Rect(x, y, bw, previewHeight), TwoToFourPlayerMapPreview, ScaleMode.ScaleToFit);
                y += previewHeight + 10f;
            }
            else
            {
                GUI.Label(new Rect(x, y, bw, 20f), "(Assign 2–4 map preview texture)");
                y += 30f;
            }

            if (GUI.Button(new Rect(x, y, bw, 30f), "Use 2–4 Player Map A (radius-3)"))
            {
                _selectedLayout = BoardLayoutMode.TwoToFour;
                EnsureGameSystems();
                _state = UiState.InGame;
            }
            y += 40f;

            if (GUI.Button(new Rect(x, y, bw, 30f), "Use 2–4 Player Map B (12-6-1)"))
            {
                _selectedLayout = BoardLayoutMode.TwoToFourSmall;
                EnsureGameSystems();
                _state = UiState.InGame;
            }
            y += 40f;

            if (GUI.Button(new Rect(x, y, bw, 30f), "Back"))
            {
                _state = UiState.MainMenu;
            }
        }
    }
}

