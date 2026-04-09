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
                int fs = Mathf.Clamp(Screen.width / 26, 17, 24);
                _menuButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = fs,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true
                };
            }

            return _menuButtonStyle;
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
            mainCam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = new Color(0.02f, 0.02f, 0.05f);

            if (mainCam.GetComponent<BoardCameraPanZoom>() == null)
                mainCam.gameObject.AddComponent<BoardCameraPanZoom>();
        }

        /// <summary>Reloads the scene so the main menu shows (same as a fresh launch).</summary>
        public void ReturnToMainMenu()
        {
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

            // VS AI / AI test always uses the 1v1 board (two seats).
            board.LayoutMode = _vsAi || _aiVsAi ? BoardLayoutMode.OneVOne : _selectedLayout;
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
            game.AiVsAiMode = _aiVsAi;
            game.VsAiMode = _vsAi && !_aiVsAi;
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
            ai.enabled = _vsAi || _aiVsAi;

            BoardBackground.EnsureLoaded();
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
                case UiState.InGame:
                    // In-game UI is handled by DemoHUD.
                    break;
            }
        }

        void DrawMainMenu()
        {
            var btnStyle = MenuButtonStyle();
            float padX = 22f;
            float bw = Mathf.Min(440f, Screen.width - padX * 2f);
            const float titleH = 46f;
            const float btnH = 58f;
            const float btnGap = 12f;
            const float footerH = 28f;
            const int nBtn = 7;
            float h = titleH + nBtn * (btnH + btnGap) + footerH + 20f;
            h = Mathf.Min(h, Screen.height - 24f);
            var rect = new Rect((Screen.width - bw) / 2f, Mathf.Max(12f, (Screen.height - h) / 2f), bw, h);
            GUI.Box(rect, "");

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Clamp(Screen.width / 22, 20, 28),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(new Rect(rect.x, rect.y + 8f, rect.width, titleH), "Nexus Ops", titleStyle);

            float y = rect.y + titleH + 10f;
            float x = rect.x + padX;
            float innerW = bw - padX * 2f;

            if (GUI.Button(new Rect(x, y, innerW, btnH), "Play", btnStyle))
            {
                _debugMode = false;
                _vsAi = false;
                _aiVsAi = false;
                _state = UiState.MapSelect;
            }

            y += btnH + btnGap;

            if (GUI.Button(new Rect(x, y, innerW, btnH), "Play vs AI", btnStyle))
            {
                _debugMode = false;
                _vsAi = true;
                _aiVsAi = false;
                _state = UiState.MapSelect;
            }

            y += btnH + btnGap;

            if (GUI.Button(new Rect(x, y, innerW, btnH), "AI vs AI (test)", btnStyle))
            {
                _debugMode = false;
                _vsAi = false;
                _aiVsAi = true;
                _state = UiState.MapSelect;
            }

            y += btnH + btnGap;

            if (GUI.Button(new Rect(x, y, innerW, btnH), "Multiplayer", btnStyle))
                Debug.Log("Multiplayer: placeholder option.");

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
                _state = UiState.MapSelect;
            }

            float footerY = rect.yMax - footerH - 10f;
            var creditStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(new Rect(rect.x + 8f, footerY, rect.width - 16f, footerH),
                "Made by Clanker Games Inc", creditStyle);
        }

        void DrawMapSelect()
        {
            var btnStyle = MenuButtonStyle();
            float padX = 18f;
            float w = Mathf.Min(460f, Screen.width - 20f);
            float x0 = (Screen.width - w) / 2f;
            float y = 18f;
            const float btnH = 54f;
            const float gap = 12f;
            float x = x0 + padX;
            float bw = w - padX * 2f;

            var hdr = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Clamp(Screen.width / 24, 18, 26),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(new Rect(x0, y, w, 40f), "Select Map", hdr);
            y += 44f;

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

            var subHdr = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Clamp(Screen.width / 32, 15, 20),
                fontStyle = FontStyle.Bold
            };
            GUI.Label(new Rect(x, y, bw, 28f), "2–4 Player Maps:", subHdr);
            y += 30f;

            float previewCap = Mathf.Clamp(Screen.height * 0.28f, 100f, 200f);
            if (TwoToFourPlayerMapPreview != null)
            {
                GUI.DrawTexture(new Rect(x, y, bw, previewCap), TwoToFourPlayerMapPreview, ScaleMode.ScaleToFit);
                y += previewCap + gap;
            }
            else
            {
                GUI.Label(new Rect(x, y, bw, 44f), "(Assign 2–4 map preview texture)");
                y += 48f;
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

            y += btnH + gap + 6f;

            if (GUI.Button(new Rect(x, y, bw, btnH), "Back", btnStyle))
            {
                BoardBackground.Remove();
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
        }

        void DrawRulebook()
        {
            EnsureRulebookStyles();

            float pad = 14f;
            var panel = new Rect(pad, pad, Screen.width - 2f * pad, Screen.height - 2f * pad);
            GUI.Box(panel, "");

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            string header = _rulebookTab == 0 ? NexusRulebook.Title : NexusUnitQuickReference.Title;
            GUI.Label(new Rect(panel.x, panel.y + 8f, panel.width, 28f), header, titleStyle);

            var tabStyle = MenuButtonStyle();
            float tabH = 48f;
            float tabW = (panel.width - 40f) * 0.5f;
            float tabY = panel.y + 36f;
            if (GUI.Button(new Rect(panel.x + 14f, tabY, tabW - 6f, tabH), "Rules", tabStyle))
            {
                if (_rulebookTab != 0)
                    _rulebookScroll = Vector2.zero;
                _rulebookTab = 0;
            }

            if (GUI.Button(new Rect(panel.x + 14f + tabW + 6f, tabY, tabW - 6f, tabH), "Units", tabStyle))
            {
                if (_rulebookTab != 1)
                    _rulebookScroll = Vector2.zero;
                _rulebookTab = 1;
            }

            string body = _rulebookTab == 0
                ? NexusRulebook.Body
                : NexusUnitQuickReference.Build(null);

            const float backH = 54f;
            var scrollRect = new Rect(panel.x + 12f, panel.y + 36f + tabH + 8f, panel.width - 24f,
                panel.height - 36f - tabH - 8f - backH - 20f);
            float innerW = scrollRect.width - 22f;
            float contentH = _rulebookBodyStyle.CalcHeight(new GUIContent(body), innerW);
            contentH = Mathf.Max(contentH + 32f, scrollRect.height * 0.45f);

            _rulebookScroll = GUI.BeginScrollView(scrollRect, _rulebookScroll, new Rect(0f, 0f, innerW, contentH));
            GUI.Label(new Rect(8f, 8f, innerW - 16f, contentH), body, _rulebookBodyStyle);
            GUI.EndScrollView();

            float backW = Mathf.Min(panel.width - 40f, 420f);
            if (GUI.Button(
                    new Rect(panel.x + (panel.width - backW) * 0.5f, panel.yMax - backH - 12f, backW, backH),
                    "Back to menu", MenuButtonStyle()))
            {
                BoardBackground.Remove();
                _state = UiState.MainMenu;
            }
        }
    }
}

