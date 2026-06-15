using UnityEngine;

namespace NexusGame
{
    public partial class Bootstrap
    {
        enum MainMenuMode
        {
            Solo,
            VsAi,
            Online
        }

        MainMenuMode _mainMenuMode = MainMenuMode.Solo;
        int _mainMenuMapIndex;
        int _mainMenuAiDifficulty; // 0 easy, 1 medium, 2 hard (placeholder)

        enum MultiplayerOnlineTab
        {
            Matchmaking = 0,
            PrivateRooms = 1
        }

        MultiplayerOnlineTab _multiplayerOnlineTab = MultiplayerOnlineTab.Matchmaking;

        /// <summary>Online room capacity: 2 = 1v1, 4 = four-player (separate matchmaking pools).</summary>
        int _onlineMatchSize = 2;

        struct MainMenuMapOption
        {
            public BoardLayoutMode Layout;
            public string Title;
            public string Subtitle;
        }

        static readonly MainMenuMapOption[] MainMenuMapOptions =
        {
            new() { Layout = BoardLayoutMode.OneVOne, Title = "Standard 1v1", Subtitle = "Classic duel board" },
            new() { Layout = BoardLayoutMode.BattleTest, Title = "Battle Lane", Subtitle = "3-hex skirmish" },
            new() { Layout = BoardLayoutMode.TwoToFour, Title = "Large 2–4", Subtitle = "Radius-3 mainland" },
            new() { Layout = BoardLayoutMode.TwoToFourSmall, Title = "Compact 2–4", Subtitle = "12-6-1 layout" }
        };

        GUIStyle _menuCardLightStyle;
        GUIStyle _menuCardDarkStyle;
        GUIStyle _menuTabStyle;
        GUIStyle _menuTabSelectedStyle;
        GUIStyle _menuSegmentStyle;
        GUIStyle _menuSegmentSelectedStyle;
        GUIStyle _menuStartStyle;
        GUIStyle _menuMapCardStyle;
        GUIStyle _menuMapCardSelectedStyle;
        GUIStyle _menuChipStyle;
        GUIStyle _menuTitleStyle;
        GUIStyle _menuSubtitleStyle;
        Texture2D _menuMapSelectedBorderTex;
        Texture2D _menuOrangeTex;
        Texture2D _menuTabSelectedTex;
        Texture2D _menuTabIdleTex;
        Texture2D _menuCardLightTex;
        Texture2D _menuCardDarkTex;

        static readonly Color MenuBlueDark = new Color(0.07f, 0.11f, 0.2f, 0.94f);
        static readonly Color MenuBlueMid = new Color(0.1f, 0.18f, 0.32f, 0.94f);
        static readonly Color MenuBlueLight = new Color(0.16f, 0.32f, 0.52f, 0.96f);
        static readonly Color MenuOrange = new Color(0.96f, 0.49f, 0.12f, 1f);
        static readonly Color MenuMapSelectedBorder = new Color(0.35f, 0.78f, 1f, 1f);

        /// <summary>Fixed mode-strip height so map grid and start button stay put across Solo / Vs AI / Online tabs.</summary>
        static float MainMenuModeStripHeight() => MenuS(164f);

        void EnsureColonistMenuStyles()
        {
            if (_menuCardLightStyle != null)
                return;

            _menuOrangeTex = CreateMenuTintTexture(MenuOrange);
            _menuTabSelectedTex = CreateMenuTintTexture(MenuBlueLight);
            _menuTabIdleTex = CreateMenuTintTexture(MenuBlueMid);
            _menuCardLightTex = CreateMenuTintTexture(new Color(MenuBlueLight.r, MenuBlueLight.g, MenuBlueLight.b, 0.88f));
            _menuCardDarkTex = CreateMenuTintTexture(MenuBlueDark);

            _menuCardLightStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _menuCardLightTex }
            };
            _menuCardDarkStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _menuCardDarkTex }
            };

            _menuTabStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                padding = new RectOffset(4, 4, 8, 8),
                normal = { background = _menuTabIdleTex, textColor = new Color(0.82f, 0.9f, 1f) }
            };
            NexusUiFonts.ApplyTo(_menuTabStyle);

            _menuTabSelectedStyle = new GUIStyle(_menuTabStyle)
            {
                normal = { background = _menuTabSelectedTex, textColor = Color.white }
            };

            _menuSegmentStyle = new GUIStyle(_menuTabStyle)
            {
                fontSize = GameUiScale.ImGuiScaledFont(18f, 15, 32),
                padding = new RectOffset(6, 6, 4, 4)
            };
            _menuSegmentSelectedStyle = new GUIStyle(_menuSegmentStyle)
            {
                normal = { background = _menuTabSelectedTex, textColor = Color.white }
            };

            _menuStartStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(12, 12, 16, 16),
                normal = { background = _menuOrangeTex, textColor = Color.white }
            };
            NexusUiFonts.ApplyTo(_menuStartStyle);
            _menuStartStyle.fontSize = GameUiScale.ImGuiScaledFont(26f, 20, 40);

            _menuMapSelectedBorderTex = CreateMenuTintTexture(MenuMapSelectedBorder);
            _menuMapCardStyle = new GUIStyle(_menuCardDarkStyle);
            _menuMapCardSelectedStyle = new GUIStyle(_menuCardLightStyle);

            _menuChipStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.85f, 0.92f, 1f, 0.9f) }
            };
            NexusUiFonts.ApplyTo(_menuChipStyle);
            _menuChipStyle.fontSize = GameUiScale.ImGuiScaledFont(13f, 11, 22);

            _menuTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };
            NexusUiFonts.ApplyTo(_menuTitleStyle);

            _menuSubtitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                normal = { textColor = new Color(0.78f, 0.86f, 0.96f, 0.92f) }
            };
            NexusUiFonts.ApplyTo(_menuSubtitleStyle);
            _menuSubtitleStyle.fontSize = GameUiScale.ImGuiScaledFont(13f, 11, 20);
        }

        void DrawMainMenu()
        {
            EnsureColonistMenuStyles();

            float margin = MenuS(14f);
            float contentW = Screen.width - margin * 2f;
            float x0 = margin;
            float topBarH = MenuS(48f);
            float startH = MenuS(64f);
            float gap = MenuS(10f);
            float tabRowH = MenuS(58f);
            float modeStripH = MainMenuModeStripHeight();

            DrawMainMenuTopBar(new Rect(x0, MenuS(6f), contentW, topBarH));

            float y = topBarH + MenuS(14f);
            DrawMainMenuModeTabs(new Rect(x0, y, contentW, tabRowH));
            y += tabRowH + gap;

            float modeStripY = y;
            y += modeStripH + gap;
            if (_mainMenuMode != MainMenuMode.Solo)
                DrawMainMenuModeStrip(new Rect(x0, modeStripY, contentW, modeStripH));

            float startY = Screen.height - startH - MenuS(16f);
            var mapGrid = new Rect(x0, y, contentW, startY - gap - y);
            DrawMainMenuMapGrid(mapGrid);

            DrawMainMenuStartButton(new Rect(x0, startY, contentW, startH));
        }

        void DrawMainMenuTopBar(Rect bar)
        {
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            titleStyle.fontSize = GameUiScale.ComputeBestFitFontSize(titleStyle, "Nexus Ops", bar.width * 0.5f,
                bar.height, 16, GameUiScale.ImGuiScaledFont(24f, 18, 36), false);

            var iconStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            NexusUiFonts.ApplyTo(iconStyle);
            iconStyle.fontSize = GameUiScale.ImGuiScaledFont(18f, 14, 28);

            float iconSize = bar.height;
            if (GUI.Button(new Rect(bar.x, bar.y, iconSize, iconSize), "☰", iconStyle))
            {
                _rulebookScroll = Vector2.zero;
                _rulebookTab = 0;
                _state = UiState.Rulebook;
            }

            GUI.Label(new Rect(bar.x, bar.y, bar.width, bar.height), "Nexus Ops", titleStyle);

            var creditStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.75f, 0.82f, 0.92f, 0.85f) }
            };
            creditStyle.fontSize = GameUiScale.ImGuiScaledFont(11f, 9, 16);
            GUI.Label(new Rect(bar.xMax - MenuS(110f), bar.y + MenuS(4f), MenuS(106f), bar.height - MenuS(8f)),
                "Clanker Games", creditStyle);
        }

        void DrawMainMenuModeTabs(Rect rect)
        {
            string[] labels = { "Solo", "Vs AI", "Online" };
            float segW = (rect.width - MenuS(8f)) / 3f;
            for (int i = 0; i < 3; i++)
            {
                var seg = new Rect(rect.x + i * (segW + MenuS(4f)), rect.y, segW, rect.height);
                bool selected = (int)_mainMenuMode == i;
                var style = selected ? _menuTabSelectedStyle : _menuTabStyle;
                FitSharedMenuButtonFont(style, seg.width, seg.height, 12, GameUiScale.ImGuiScaledFont(20f, 16, 32), labels[i]);
                if (GUI.Button(seg, labels[i], style))
                    _mainMenuMode = (MainMenuMode)i;
            }
        }

        void DrawMainMenuModeStrip(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, _menuCardDarkStyle);
            float pad = MenuS(4f);
            var inner = new Rect(rect.x + pad, rect.y + pad, rect.width - pad * 2f, rect.height - pad * 2f);

            if (_mainMenuMode == MainMenuMode.VsAi)
            {
                float rowH = MenuS(50f);
                float rowY = inner.y + (inner.height - rowH) * 0.5f;
                _mainMenuAiDifficulty = DrawSegmentedRow(
                    new Rect(inner.x, rowY, inner.width, rowH),
                    new[] { "Easy", "Medium", "Hard" },
                    _mainMenuAiDifficulty);
            }
            else
                DrawMainMenuOnlineModeStrip(inner);
        }

        void DrawMainMenuMapGrid(Rect rect)
        {
            const float gridScale = 0.78f;
            const float cellAspect = 1.08f; // width : height — fixed so mode strip never squashes cells
            float gap = MenuS(12f);

            float cellW = (rect.width - gap) * 0.5f * gridScale;
            float cellH = cellW * cellAspect;
            float gridW = cellW * 2f + gap;
            float gridH = cellH * 2f + gap;

            if (gridH > rect.height && rect.height > 1f)
            {
                float shrink = rect.height / gridH;
                cellW *= shrink;
                cellH *= shrink;
                gridW = cellW * 2f + gap;
                gridH = cellH * 2f + gap;
            }

            float originX = rect.x + (rect.width - gridW) * 0.5f;
            float originY = rect.y;

            for (int i = 0; i < MainMenuMapOptions.Length; i++)
            {
                int col = i % 2;
                int row = i / 2;
                var cell = new Rect(
                    originX + col * (cellW + gap),
                    originY + row * (cellH + gap),
                    cellW,
                    cellH);
                DrawMainMenuMapCell(cell, i, MainMenuMapOptions[i]);
            }

            _selectedLayout = MainMenuMapOptions[Mathf.Clamp(_mainMenuMapIndex, 0, MainMenuMapOptions.Length - 1)]
                .Layout;
        }

        void DrawMainMenuMapCell(Rect rect, int index, MainMenuMapOption option)
        {
            bool selected = _mainMenuMapIndex == index;
            var cardStyle = selected ? _menuMapCardSelectedStyle : _menuMapCardStyle;
            GUI.Box(rect, GUIContent.none, cardStyle);

            if (selected)
            {
                float border = MenuS(3f);
                var borderRect = new Rect(rect.x + border, rect.y + border, rect.width - border * 2f,
                    rect.height - border * 2f);
                DrawMenuRectOutline(borderRect, _menuMapSelectedBorderTex, MenuS(2f));
            }

            float pad = MenuS(6f);
            float labelH = MenuS(36f);
            var previewRect = new Rect(rect.x + pad, rect.y + pad, rect.width - pad * 2f,
                rect.height - pad * 2f - labelH);
            var labelRect = new Rect(rect.x + pad, previewRect.yMax + MenuS(4f), rect.width - pad * 2f, labelH);

            var previewBg = CreateMenuTintTexture(new Color(0.04f, 0.07f, 0.12f, 0.95f));
            GUI.Box(previewRect, GUIContent.none, new GUIStyle { normal = { background = previewBg } });

            Texture2D preview = BoardMapPreview.Get(option.Layout);
            if (preview != null)
            {
                float side = Mathf.Min(previewRect.width, previewRect.height) - MenuS(4f);
                var texRect = new Rect(
                    previewRect.x + (previewRect.width - side) * 0.5f,
                    previewRect.y + (previewRect.height - side) * 0.5f,
                    side,
                    side);
                GUI.DrawTexture(texRect, preview, ScaleMode.ScaleToFit, true);
            }

            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                _mainMenuMapIndex = index;

            _menuTitleStyle.fontSize = GameUiScale.ComputeBestFitFontSize(_menuTitleStyle, option.Title, labelRect.width,
                labelRect.height * 0.55f, 11, GameUiScale.ImGuiScaledFont(17f, 13, 24), false);
            GUI.Label(new Rect(labelRect.x, labelRect.y, labelRect.width, labelRect.height * 0.55f), option.Title,
                _menuTitleStyle);

            _menuSubtitleStyle.fontSize = GameUiScale.ImGuiScaledFont(11f, 9, 16);
            GUI.Label(
                new Rect(labelRect.x, labelRect.y + labelRect.height * 0.5f, labelRect.width, labelRect.height * 0.5f),
                option.Subtitle, _menuSubtitleStyle);
        }

        static Rect InsetRect(Rect r, float inset) =>
            new Rect(r.x + inset, r.y + inset, Mathf.Max(0f, r.width - inset * 2f), Mathf.Max(0f, r.height - inset * 2f));

        void DrawMenuRectOutline(Rect rect, Texture2D lineTex, float thickness)
        {
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), lineTex);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), lineTex);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), lineTex);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), lineTex);
        }

        void DrawMainMenuOnlineModeStrip(Rect rect)
        {
            const float tabDesignH = 46f;
            const float sizeDesignH = 42f;
            const float contentDesignH = 46f;
            const float innerGapDesign = 6f;
            float tabH = MenuS(tabDesignH);
            float sizeH = MenuS(sizeDesignH);
            float innerGap = MenuS(innerGapDesign);
            float contentH = MenuS(contentDesignH);
            float y = rect.y;

            var tabRect = new Rect(rect.x, y, rect.width, tabH);
            _multiplayerOnlineTab = (MultiplayerOnlineTab)DrawSegmentedRow(tabRect,
                new[] { "Matchmaking", "Private Rooms" }, (int)_multiplayerOnlineTab);
            y += tabH + innerGap;

            int sizeSelected = DrawSegmentedRow(new Rect(rect.x, y, rect.width, sizeH),
                new[] { "1v1", "4 Players" }, _onlineMatchSize == 4 ? 1 : 0);
            _onlineMatchSize = sizeSelected == 1 ? 4 : 2;
            y += sizeH + innerGap;

            var content = new Rect(rect.x, y, rect.width, contentH);
            if (_multiplayerOnlineTab == MultiplayerOnlineTab.PrivateRooms)
                DrawMainMenuPrivateRoomActions(content);
            else
                DrawMainMenuMatchmakingHint(content);
        }

        void DrawMainMenuMatchmakingHint(Rect rect)
        {
            var hint = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color(0.78f, 0.86f, 0.96f, 0.92f) }
            };
            hint.fontSize = GameUiScale.ImGuiScaledFont(13f, 11, 20);
            string vs = _onlineMatchSize == 4 ? "Quick match with up to 4 players." : "Quick match against another player.";
            GUI.Label(rect, vs + "\nTap Find Match below.", hint);
        }

        void DrawMainMenuPrivateRoomActions(Rect rect)
        {
            float btnH = rect.height;
            float gap = MenuS(8f);
            float bw = (rect.width - gap) * 0.5f;
            FitSharedMenuButtonFont(_menuSegmentStyle, bw, btnH, 14, GameUiScale.ImGuiScaledFont(20f, 16, 32),
                "Create Room", "Join Room");
            FitSharedMenuButtonFont(_menuSegmentSelectedStyle, bw, btnH, 14, GameUiScale.ImGuiScaledFont(20f, 16, 32),
                "Create Room", "Join Room");

            if (GUI.Button(new Rect(rect.x, rect.y, bw, btnH), "Create Room", _menuSegmentStyle))
            {
                RequireMultiplayerSignIn(() =>
                {
                    NexusSession.Reset();
                    NexusLobbyService.Leave();
                    _joinRoomCodeInput = "";
                    _multiplayerOnlineTab = MultiplayerOnlineTab.PrivateRooms;
                    NexusLobbyService.CreateRoom(_onlineMatchSize);
                    _state = UiState.MultiplayerLobby;
                });
            }

            if (GUI.Button(new Rect(rect.x + bw + gap, rect.y, bw, btnH), "Join Room", _menuSegmentStyle))
            {
                RequireMultiplayerSignIn(() =>
                {
                    _joinRoomCodeInput = "";
                    _multiplayerOnlineTab = MultiplayerOnlineTab.PrivateRooms;
                    _state = UiState.MultiplayerJoin;
                });
            }
        }

        int DrawSegmentedRow(Rect rect, string[] labels, int selected)
        {
            int n = labels.Length;
            float gap = MenuS(4f);
            float segW = (rect.width - gap * (n - 1)) / n;
            for (int i = 0; i < n; i++)
            {
                var seg = new Rect(rect.x + i * (segW + gap), rect.y, segW, rect.height);
                var style = i == selected ? _menuSegmentSelectedStyle : _menuSegmentStyle;
                FitSharedMenuButtonFont(style, seg.width, seg.height, 14, GameUiScale.ImGuiScaledFont(20f, 16, 32),
                    labels);
                if (GUI.Button(seg, labels[i], style))
                    selected = i;
            }

            return selected;
        }

        void DrawMainMenuStartButton(Rect rect)
        {
            string label = "Start Game";
            if (_mainMenuMode == MainMenuMode.Online)
                label = _multiplayerOnlineTab == MultiplayerOnlineTab.Matchmaking ? "Find Match" : "Create Room";

            FitSharedMenuButtonFont(_menuStartStyle, rect.width, rect.height, 18, GameUiScale.ImGuiScaledFont(28f, 22, 40),
                label);
            if (GUI.Button(rect, label, _menuStartStyle))
                MainMenuStartGame();
        }

        void MainMenuStartGame()
        {
            _debugMode = false;

            switch (_mainMenuMode)
            {
                case MainMenuMode.VsAi:
                    _vsAi = true;
                    _aiVsAi = false;
                    NexusSession.ConfigureVsAi();
                    EnsureGameSystems();
                    _state = UiState.InGame;
                    return;

                case MainMenuMode.Online:
                    if (!RequireMultiplayerSignIn(StartOnlineFromMainMenu))
                        return;
                    return;

                default:
                    _vsAi = false;
                    _aiVsAi = false;
                    NexusSession.ConfigureLocalHotseat();
                    EnsureGameSystems();
                    _state = UiState.InGame;
                    break;
            }
        }

        void StartOnlineFromMainMenu()
        {
            NexusSession.Reset();
            NexusLobbyService.Leave();
            _joinRoomCodeInput = "";
            if (_multiplayerOnlineTab == MultiplayerOnlineTab.Matchmaking)
            {
                NexusLobbyService.StartFindMatch(_onlineMatchSize);
                _state = UiState.MultiplayerLobby;
            }
            else
            {
                NexusLobbyService.CreateRoom(_onlineMatchSize);
                _state = UiState.MultiplayerLobby;
            }
        }
    }
}
