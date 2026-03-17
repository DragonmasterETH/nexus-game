using System.Text;
using UnityEngine;

namespace NexusGame
{
    /// <summary>
    /// Very simple on-screen HUD to explain controls and show basic state.
    /// </summary>
    public class DemoHUD : MonoBehaviour
    {
        public GameController Game;
        public MobileInputController InputController;
        public bool ShowDebugToggle = false;

        bool _showBuyMenu;

        void Start()
        {
            if (Game == null)
            {
                Game = FindObjectOfType<GameController>();
            }
            if (InputController == null)
            {
                InputController = FindObjectOfType<MobileInputController>();
            }
        }

        void OnGUI()
        {
            if (Game == null || Game.Players.Count == 0)
                return;

            var player = Game.CurrentPlayer;

            var sb = new StringBuilder();
            sb.AppendLine("Nexus Ops Demo");
            sb.AppendLine("--------------------");
            sb.AppendLine($"Current Player: {player.PlayerIndex + 1}");
            sb.AppendLine($"Rubium: {player.Rubium}");
            sb.AppendLine($"Victory Points: {player.VictoryPoints}");
            sb.AppendLine();
            sb.AppendLine("Controls:");
            sb.AppendLine("- Click/tap a hex: reveal exploration or select tile.");
            sb.AppendLine("- In popup, use +/- to choose units to move, then click an adjacent hex to move them.");
            sb.AppendLine("- Click moneybag to open the buy menu on a home-base hex.");

            GUI.Box(new Rect(10, 10, 320, 170), sb.ToString());

            // Optional debug toggle
            if (ShowDebugToggle && InputController != null)
            {
                bool newDebug = GUI.Toggle(new Rect(10, 165, 150, 20), InputController.DebugClicks, "Debug clicks");
                InputController.DebugClicks = newDebug;
            }

            if (GUI.Button(new Rect(10, 190, 120, 30), "End Turn"))
            {
                Game.EndTurn();
                _showBuyMenu = false;
            }

            // Moneybag button to open buy menu (enabled only when a valid home hex is selected)
            bool canBuyHere = false;
            if (InputController != null && InputController.SelectedTile != null)
            {
                var sel = InputController.SelectedTile;
                if (sel.Type == TileType.HomeBase && sel.Owner == player)
                {
                    canBuyHere = true;
                }
            }

            if (!canBuyHere)
                GUI.enabled = false;

            if (GUI.Button(new Rect(140, 190, 40, 30), "$"))
            {
                _showBuyMenu = !_showBuyMenu;
            }

            GUI.enabled = true;

            // Contextual buy menu, shown only when toggled on and at a valid home hex
            if (_showBuyMenu && canBuyHere)
            {
                int y = 230;
                DrawBuyButton(ref y, "Buy Human (1)", UnitType.Human, 1);
                DrawBuyButton(ref y, "Buy Fungoid (2)", UnitType.Fungoid, 2);
                DrawBuyButton(ref y, "Buy Crystalline (2)", UnitType.Crystalline, 2);
                DrawBuyButton(ref y, "Buy Rock Strider (3)", UnitType.RockStrider, 3);
                DrawBuyButton(ref y, "Buy Lava Leaper (4)", UnitType.LavaLeaper, 4);
                DrawBuyButton(ref y, "Buy Rubium Dragon (8)", UnitType.RubiumDragon, 8);
            }

            // Tile popup: show units and group-move selection on selected tile
            var popupTile = InputController != null ? InputController.SelectedTile : null;

            if (popupTile != null)
            {
                string title = $"Tile ({popupTile.Type})";
                // Make the popup taller so all unit lines fit comfortably.
                var rect = new Rect(Screen.width - 260, 10, 250, 320);
                GUI.Box(rect, title);

                GUILayout.BeginArea(new Rect(rect.x + 10, rect.y + 25, rect.width - 20, rect.height - 55));

                // Determine if this hex is contested (units from more than one player present)
                bool hasAnyUnit = false;
                bool hasOtherOwner = false;
                int? soleOwnerIndex = null;
                foreach (var unit in FindObjectsOfType<UnitInstance>())
                {
                    if (unit.Tile != popupTile)
                        continue;

                    hasAnyUnit = true;
                    if (soleOwnerIndex == null)
                    {
                        soleOwnerIndex = unit.Owner.PlayerIndex;
                    }
                    else if (soleOwnerIndex != unit.Owner.PlayerIndex)
                    {
                        hasOtherOwner = true;
                        break;
                    }
                }

                if (hasAnyUnit && hasOtherOwner)
                {
                    var prevColor = GUI.color;
                    GUI.color = Color.red;
                    GUILayout.Label("Owner: CONTESTED");
                    GUI.color = prevColor;
                }
                else
                {
                    GUILayout.Label($"Owner: {(popupTile.Owner != null ? (popupTile.Owner.PlayerIndex + 1).ToString() : "None")}");
                }
                GUILayout.Label($"Base Yield: {Game.Config.GetTile(popupTile.Type)?.RubiumYield ?? 0}");
                GUILayout.Label($"Mine Bonus: {popupTile.ExtraMineYield}");
                GUILayout.Space(5);
                GUILayout.Label("Units here (select to move):");

                // Count units by type for current player (movable this turn)
                var counts = new System.Collections.Generic.Dictionary<UnitType, int>();
                foreach (var unit in FindObjectsOfType<UnitInstance>())
                {
                    if (unit.Tile == popupTile && unit.Owner == player && !unit.HasMovedThisTurn)
                    {
                        if (!counts.ContainsKey(unit.Definition.Type))
                            counts[unit.Definition.Type] = 0;
                        counts[unit.Definition.Type]++;
                    }
                }

                var selectedCounts = InputController.SelectedMoveCounts;

                foreach (var kvp in counts)
                {
                    var type = kvp.Key;
                    int available = kvp.Value;
                    selectedCounts.TryGetValue(type, out int chosen);

                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{type}: {available}  Sel: {chosen}", GUILayout.Width(150));
                    if (GUILayout.Button("-", GUILayout.Width(20)))
                    {
                        InputController.AdjustMoveSelection(type, -1);
                    }
                    if (GUILayout.Button("+", GUILayout.Width(20)))
                    {
                        InputController.AdjustMoveSelection(type, +1);
                    }
                    GUILayout.EndHorizontal();
                }

                GUILayout.Space(5);
                GUILayout.Label("All units on tile:");
                foreach (var unit in FindObjectsOfType<UnitInstance>())
                {
                    if (unit.Tile == popupTile)
                    {
                        GUILayout.Label($"- {unit.Definition.Type} (P{unit.Owner.PlayerIndex + 1})");
                    }
                }

                if (GUILayout.Button("Close"))
                {
                    InputController.ClearSelection();
                }

                GUILayout.EndArea();
            }
        }

        void DrawBuyButton(ref int y, string label, UnitType type, int cost)
        {
            var player = Game.CurrentPlayer;
            bool canAfford = player.Rubium >= cost;
            if (!canAfford)
                GUI.enabled = false;

            if (GUI.Button(new Rect(10, y, 220, 25), label) && canAfford)
            {
                // If the selected tile is one of this player's home-base hexes, deploy there;
                // otherwise fall back to the default home tile.
                BoardTile homeTile = null;
                if (InputController != null && InputController.SelectedTile != null)
                {
                    var sel = InputController.SelectedTile;
                    if (sel.Type == TileType.HomeBase && sel.Owner == player)
                    {
                        homeTile = sel;
                    }
                }

                if (homeTile == null)
                {
                    homeTile = FindHomeBaseTileForPlayer(player);
                }

                if (homeTile != null)
                {
                    Game.SpawnUnit(player, type, homeTile);
                    player.Rubium -= cost;
                }
            }

            GUI.enabled = true;
            y += 30;
        }

        BoardTile FindHomeBaseTileForPlayer(PlayerState player)
        {
            foreach (var tile in Game.Board.AllTiles)
            {
                if (tile.Type == TileType.HomeBase && tile.Owner == player)
                {
                    return tile;
                }
            }
            return null;
        }
    }
}

