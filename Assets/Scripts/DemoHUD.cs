using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

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

        bool _showBuyMenu;
        Vector2 _scrollBattle;
        Vector2 _scrollHand;
        Vector2 _scrollHandBattle;
        Vector2 _scrollHandDeploy;
        Vector2 _scrollHandSecret;

        GUIStyle _cardTitleStyle;
        GUIStyle _cardBodyStyle;
        GUIStyle _cardBadgeStyle;
        GUIStyle _cardColumnLabelStyle;

        const float CardBarHeight = 152f;
        const float CardTileW = 112f;
        const float CardTileH = 104f;

        void Start()
        {
            if (Game == null)
                Game = FindObjectOfType<GameController>();
            if (InputController == null)
                InputController = FindObjectOfType<MobileInputController>();
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

        void OnGUI()
        {
            if (Game == null || Game.Players.Count == 0)
                return;

            var player = Game.CurrentPlayer;

            DrawFullBattleOverlays(player);
            DrawDragonPhaseOverlay();

            var sb = new StringBuilder();
            sb.AppendLine("Nexus Ops");
            sb.AppendLine("--------------------");
            sb.AppendLine($"Current: P{player.PlayerIndex + 1}  Rubium: {player.Rubium}  VP: {player.VictoryPoints}");
            sb.AppendLine(
                $"Battle cards: {player.BattleEnergize?.Count ?? 0}  Deploy: {player.DeployEnergize?.Count ?? 0}  Secrets: {player.SecretMissions?.Count ?? 0}");
            if (player.DeploymentPurchaseDiscountRubium > 0)
                sb.AppendLine($"Next buy discount: up to {player.DeploymentPurchaseDiscountRubium} Rubium");
            if (Game.BattlePhaseBlockingPlay)
                sb.AppendLine("(Finish battle phase to move.)");

            sb.AppendLine();
            sb.AppendLine("- $ : buy units + Deployment Energize. Free Human: select home hex first.");
            sb.AppendLine("- End Turn: Dragon shots, then next player.");

            GUI.Box(new Rect(10, 10, 340, 155), sb.ToString());
            if (!string.IsNullOrEmpty(Game.LastDrawPhaseLog))
            {
                GUI.Box(new Rect(360, 10, 600, 46), Game.LastDrawPhaseLog);
            }

            string battleLog =
                !string.IsNullOrEmpty(Game.LiveBattlePhaseLog) ? Game.LiveBattlePhaseLog : Game.LastBattlePhaseLog;

            if (!string.IsNullOrEmpty(battleLog) && !Game.PendingBattleArrangement)
            {
                var battleRect = new Rect(10, 175, 420, 140);
                GUI.Box(battleRect, "Battle log");
                GUI.Label(new Rect(battleRect.x + 6, battleRect.y + 20, battleRect.width - 12, battleRect.height - 26),
                    battleLog.Length > 560 ? battleLog.Substring(0, 560) + "..." : battleLog);
            }

            if (ShowDebugToggle && InputController != null)
            {
                bool newDebug = GUI.Toggle(new Rect(10, 285, 150, 20), InputController.DebugClicks, "Debug clicks");
                InputController.DebugClicks = newDebug;
            }

            float topY = string.IsNullOrEmpty(battleLog) || Game.PendingBattleArrangement ? 175f : 325f;
            if (Game.DragonPhase != null)
                topY = Mathf.Max(topY, Screen.height - 220f);
            // Keep main buttons above bottom card strip + dragon strip
            float reserveBottom = CardBarHeight + 18f + (Game.DragonPhase != null ? 200f : 0f);
            topY = Mathf.Min(topY, Mathf.Max(60f, Screen.height - reserveBottom));

            if (Game.BattlePhaseBlockingPlay || Game.DragonPhase != null)
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
                if (sel.Type == TileType.HomeBase && sel.Owner == player)
                    canBuyHere = true;
            }

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
                foreach (var g in player.DeployEnergize.GroupBy(x => x).OrderBy(x => x.Key.ToString()))
                {
                    var id = g.Key;
                    int n = g.Count();
                    string note = id == EnergizeDeploymentId.FreeHuman &&
                                  (sel == null || sel.Type != TileType.HomeBase || sel.Owner != player)
                        ? " [select home hex]"
                        : "";
                    if (GUILayout.Button(EnergizeDeploymentCatalog.GetName(id) + " x" + n + note))
                        Game.TryPlayDeploymentEnergize(id, sel);
                }

                if (player.DeployEnergize.Count == 0)
                    GUILayout.Label("(No deployment cards)");
                GUILayout.EndArea();
            }

            DrawBottomCardHand(player);

            DrawTilePopup(player);
        }

        void DrawBottomCardHand(PlayerState player)
        {
            EnsureCardStyles();

            float barY = Game.DragonPhase != null
                ? Screen.height - 200f - CardBarHeight - 8f
                : Screen.height - CardBarHeight - 8f;
            barY = Mathf.Max(40f, barY);

            float barX = 8f;
            float barW = Screen.width - 16f;
            GUI.Box(new Rect(barX, barY, barW, CardBarHeight), "");

            string deckLine = $"P{player.PlayerIndex + 1} hand  |  Secret deck: {Game.SecretDeckCount}  Energize deck: {Game.EnergizeDeckCount}";
            GUI.Label(new Rect(barX + 8, barY + 4, barW - 16, 18), deckLine, _cardColumnLabelStyle);

            float colGap = 8f;
            float colY = barY + 24f;
            float colH = CardBarHeight - 32f;
            float colW = (barW - 16f - colGap * 2f) / 3f;

            var battleGroups = player.BattleEnergize.GroupBy(x => x).OrderBy(g => g.Key.ToString()).ToList();
            float battleContentW = battleGroups.Count == 0
                ? CardTileW + 8f
                : battleGroups.Count * (CardTileW + 8f);
            DrawCardColumn(new Rect(barX + 8f, colY, colW, colH), "Battle Energize", ref _scrollHandBattle,
                battleContentW, () =>
                {
                    if (battleGroups.Count == 0)
                        DrawPlaceholderCard(new Rect(0, 0, CardTileW, CardTileH), "No cards");
                    else
                    {
                        float x = 0f;
                        foreach (var g in battleGroups)
                        {
                            string full = EnergizeBattleCatalog.GetName(g.Key);
                            DrawPlayingCard(new Rect(x, 0, CardTileW, CardTileH), new Color(0.15f, 0.28f, 0.55f),
                                CardShortTitle(full), CardDetailFromName(full), g.Count());
                            x += CardTileW + 8f;
                        }
                    }
                });

            var deployGroups = player.DeployEnergize.GroupBy(x => x).OrderBy(g => g.Key.ToString()).ToList();
            float deployContentW = deployGroups.Count == 0
                ? CardTileW + 8f
                : deployGroups.Count * (CardTileW + 8f);
            DrawCardColumn(new Rect(barX + 8f + colW + colGap, colY, colW, colH), "Deployment",
                ref _scrollHandDeploy, deployContentW, () =>
                {
                    if (deployGroups.Count == 0)
                        DrawPlaceholderCard(new Rect(0, 0, CardTileW, CardTileH), "No cards");
                    else
                    {
                        float x = 0f;
                        foreach (var g in deployGroups)
                        {
                            string full = EnergizeDeploymentCatalog.GetName(g.Key);
                            DrawPlayingCard(new Rect(x, 0, CardTileW, CardTileH), new Color(0.15f, 0.45f, 0.25f),
                                CardShortTitle(full), CardDetailFromName(full), g.Count());
                            x += CardTileW + 8f;
                        }
                    }
                });

            int secretCount = player.SecretMissions.Count;
            float secretContentW = secretCount == 0 ? CardTileW + 8f : secretCount * (CardTileW + 8f);
            DrawCardColumn(new Rect(barX + 8f + (colW + colGap) * 2f, colY, colW, colH), "Secret missions",
                ref _scrollHandSecret, secretContentW, () =>
                {
                    if (secretCount == 0)
                        DrawPlaceholderCard(new Rect(0, 0, CardTileW, CardTileH), "No missions");
                    else
                    {
                        float x = 0f;
                        for (int i = 0; i < player.SecretMissions.Count; i++)
                        {
                            var s = player.SecretMissions[i];
                            string full = SecretMissionLabel(s) + " (+" + s.VictoryPoints + " VP)";
                            DrawPlayingCard(new Rect(x, 0, CardTileW, CardTileH), new Color(0.42f, 0.15f, 0.5f),
                                "#" + i + " " + CardShortTitle(full), CardDetailFromName(full), 1);
                            x += CardTileW + 8f;
                        }
                    }
                });
        }

        void DrawCardColumn(Rect area, string columnTitle, ref Vector2 scroll, float contentWidth,
            System.Action drawInsideScroll)
        {
            GUI.Label(new Rect(area.x, area.y - 2f, area.width, 16f), columnTitle, _cardColumnLabelStyle);

            Rect view = new Rect(area.x, area.y + 14f, area.width, area.height - 14f);
            float cw = Mathf.Max(contentWidth, view.width);
            scroll = GUI.BeginScrollView(view, scroll, new Rect(0, 0, cw, CardTileH));
            drawInsideScroll();
            GUI.EndScrollView();
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
            if (Game.PendingBattleArrangement && Game.BattlePlan != null && Game.BattlePlan.Count > 0)
            {
                GUI.Window(900, new Rect(60, 40, Screen.width - 120, Screen.height - 120), WindowBattleArrange,
                    "Arrange battles");
                return;
            }

            if (Game.EnergizePromptPlayer != null && Game.FocusFirePicker == null)
            {
                GUI.Window(901, new Rect(80, 80, Screen.width - 160, 320), WindowEnergizeBattle,
                    WindowTitleEnergize());
                return;
            }

            if (Game.FocusFirePicker != null)
            {
                GUI.Window(902, new Rect(100, 120, Screen.width - 200, 280), WindowFocusFire,
                    "Focus Fire");
                return;
            }

            if (Game.CasualtyPick != null)
            {
                GUI.Window(903, new Rect(80, 100, Screen.width - 160, 400), WindowCasualty,
                    WindowTitleCasualty());
                return;
            }

            if (Game.SecretMissionOffer != null && Game.SecretMissionOffer.Waiting)
            {
                GUI.Window(904, new Rect(100, 100, Screen.width - 200, 340), WindowSecretMission,
                    "Secret mission");
            }
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
            GUILayout.Label("Battle order (top first). Pick defender per hex.");
            _scrollBattle = GUILayout.BeginScrollView(_scrollBattle, GUILayout.Height(Screen.height - 220));
            for (int i = 0; i < Game.BattlePlan.Count; i++)
            {
                var e = Game.BattlePlan[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label((i + 1) + ". (" + e.Hex.Q + "," + e.Hex.R + ")", GUILayout.Width(100));
                if (GUILayout.Button("^", GUILayout.Width(28)))
                    Game.MoveBattlePlanEntry(i, -1);
                if (GUILayout.Button("v", GUILayout.Width(28)))
                    Game.MoveBattlePlanEntry(i, 1);

                var opps = BattleResolver.OpponentsOnHex(e.Hex, Game.CurrentPlayer);
                GUILayout.Label("vs", GUILayout.Width(24));
                foreach (var o in opps)
                {
                    if (GUILayout.Button("P" + (o.PlayerIndex + 1), GUILayout.Width(48)) &&
                        e.DefenderPlayerIndex != o.PlayerIndex)
                        Game.SetBattleDefenderForEntry(i, o.PlayerIndex);
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
            GUILayout.Label(Game.EnergizeBattleContext ?? "");
            GUILayout.Label("P" + (p.PlayerIndex + 1) + ": Battle Energize or pass.");
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
            GUILayout.Label("Pick " + cp.Required + " unit(s). Selected: " + cp.Selected.Count);
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
            foreach (var opt in dp.Options.ToList())
            {
                string label = "Dragon (" + opt.Dragon.Tile.Q + "," + opt.Dragon.Tile.R + ") -> (" +
                               opt.TargetHex.Q + "," + opt.TargetHex.R + ")";
                if (GUI.Button(new Rect(30, y, Screen.width - 200, 24), label))
                    Game.ExecuteDragonStrike(opt);
                if (GUI.Button(new Rect(Screen.width - 160, y, 120, 24), "Skip"))
                    Game.SkipDragonStrikeOption(opt);
                y += 28;
            }

            if (GUI.Button(new Rect(30, y, 220, 26), "Skip all dragon strikes"))
                Game.SkipAllDragonStrikes();
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
            if (GUILayout.Button(line) && canAfford)
            {
                BoardTile homeTile = null;
                if (InputController != null && InputController.SelectedTile != null)
                {
                    var sel = InputController.SelectedTile;
                    if (sel.Type == TileType.HomeBase && sel.Owner == player)
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

        void DrawTilePopup(PlayerState player)
        {
            var popupTile = InputController != null ? InputController.SelectedTile : null;
            if (popupTile == null)
                return;

            var rect = new Rect(Screen.width - 260, 10, 250, 320);
            GUI.Box(rect, "Tile " + popupTile.Type);
            GUILayout.BeginArea(new Rect(rect.x + 10, rect.y + 25, rect.width - 20, rect.height - 55));

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

            GUILayout.Label("Mine: " + popupTile.ExtraMineYield);
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
                foreach (var kvp in counts)
                {
                    selectedCounts.TryGetValue(kvp.Key, out int chosen);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(kvp.Key + ": " + kvp.Value + " sel:" + chosen, GUILayout.Width(150));
                    if (GUILayout.Button("-", GUILayout.Width(20)))
                        InputController.AdjustMoveSelection(kvp.Key, -1);
                    if (GUILayout.Button("+", GUILayout.Width(20)))
                        InputController.AdjustMoveSelection(kvp.Key, +1);
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Label("All units:");
            foreach (var unit in FindObjectsOfType<UnitInstance>())
            {
                if (unit.Tile == popupTile)
                    GUILayout.Label(unit.Definition.Type + " P" + (unit.Owner.PlayerIndex + 1));
            }

            if (GUILayout.Button("Close") && InputController != null)
                InputController.ClearSelection();
            GUILayout.EndArea();
        }

        BoardTile FindHomeBaseTileForPlayer(PlayerState player)
        {
            foreach (var tile in Game.Board.AllTiles)
            {
                if (tile.Type == TileType.HomeBase && tile.Owner == player)
                    return tile;
            }

            return null;
        }
    }
}
