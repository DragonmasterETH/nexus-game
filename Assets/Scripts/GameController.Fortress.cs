using System;
using System.Collections.Generic;
using UnityEngine;

namespace NexusGame
{
    public partial class GameController
    {
        readonly HashSet<(int q, int r)> _fortressBreathUsedThisTurn = new HashSet<(int q, int r)>();

        public bool PendingFortressPlacement { get; private set; }

        public bool CanUseDeploymentEnergizeNow()
        {
            if (IsGameOver || BattlePhaseBlockingPlay || DragonPhase != null)
                return false;
            return !AnyMovementOccurredThisTurn;
        }

        public bool CanPlaceFortressOnTile(PlayerState player, BoardTile tile)
        {
            if (player == null || tile == null)
                return false;
            if (tile.Type == TileType.HomeBase)
                return false;
            if (tile.FortressOwnerPlayerIndex >= 0)
                return false;
            return IsHexControlledByPlayer(tile, player);
        }

        public bool TileHasFortressForPlayer(BoardTile tile, PlayerState player)
        {
            if (tile == null || player == null || tile.FortressOwnerPlayerIndex < 0)
                return false;
            return tile.FortressOwnerPlayerIndex == player.PlayerIndex &&
                   IsHexControlledByPlayer(tile, player);
        }

        public bool TryPlayDeploymentEnergizeFortress(BoardTile placementHex)
        {
            if (!CanUseDeploymentEnergizeNow())
                return false;

            var p = CurrentPlayer;
            if (p == null || !p.DeployEnergize.Contains(EnergizeDeploymentId.Fortress))
                return false;

            if (placementHex == null)
            {
                PendingFortressPlacement = true;
                return true;
            }

            return TryPlaceFortressOnHex(placementHex);
        }

        public void CancelFortressPlacement()
        {
            PendingFortressPlacement = false;
        }

        public bool TryPlaceFortressOnHex(BoardTile tile)
        {
            if (!CanUseDeploymentEnergizeNow())
                return false;

            var p = CurrentPlayer;
            if (p == null || !p.DeployEnergize.Contains(EnergizeDeploymentId.Fortress))
                return false;
            if (!CanPlaceFortressOnTile(p, tile))
                return false;

            tile.FortressOwnerPlayerIndex = p.PlayerIndex;
            EnsureFortressMarker(tile, p);
            p.DeployEnergize.Remove(EnergizeDeploymentId.Fortress);
            PendingFortressPlacement = false;
            AfterOnlineHostMutation();
            return true;
        }

        public bool CanBeginFortressBreathDuringDeploy(BoardTile fortressHex)
        {
            if (!CanUseDeploymentEnergizeNow())
                return false;
            var p = CurrentPlayer;
            if (p == null || fortressHex == null)
                return false;
            if (!TileHasFortressForPlayer(fortressHex, p))
                return false;
            if (_fortressBreathUsedThisTurn.Contains((fortressHex.Q, fortressHex.R)))
                return false;
            return BuildFortressStrikeOptionsForHex(p, fortressHex).Count > 0;
        }

        public bool TryBeginFortressBreathFromHex(BoardTile fortressHex)
        {
            if (!CanBeginFortressBreathDuringDeploy(fortressHex))
                return false;

            var p = CurrentPlayer;
            var options = BuildFortressStrikeOptionsForHex(p, fortressHex);
            if (options.Count == 0)
                return false;

            _fortressBreathUsedThisTurn.Add((fortressHex.Q, fortressHex.R));
            DragonPhase = new DragonPhaseState
            {
                Player = p,
                Options = options,
                Rng = new System.Random(Environment.TickCount),
                DuringDeployment = true,
                ActiveFortressHex = fortressHex
            };
            AfterOnlineHostMutation();
            return true;
        }

        List<DragonStrikeOption> BuildFortressStrikeOptionsForHex(PlayerState player, BoardTile fortressHex)
        {
            var options = new List<DragonStrikeOption>();
            if (player == null || fortressHex == null || Board == null)
                return options;

            foreach (var n in Board.GetNeighbors(fortressHex))
            {
                if (IsTileContested(n))
                    continue;

                bool enemyHere = false;
                foreach (var o in FindObjectsOfType<UnitInstance>())
                {
                    if (o.Tile == n && o.Owner != player)
                    {
                        enemyHere = true;
                        break;
                    }
                }

                if (enemyHere)
                {
                    options.Add(new DragonStrikeOption
                    {
                        FortressSourceHex = fortressHex,
                        TargetHex = n
                    });
                }
            }

            return options;
        }

        public void RefreshFortressControlOnTile(BoardTile tile)
        {
            if (tile == null || tile.FortressOwnerPlayerIndex < 0)
                return;

            var owner = Players?.Find(pl => pl.PlayerIndex == tile.FortressOwnerPlayerIndex);
            if (owner == null || !IsHexControlledByPlayer(tile, owner))
                ClearFortressOnTile(tile);
        }

        void ClearFortressOnTile(BoardTile tile)
        {
            if (tile == null)
                return;

            tile.FortressOwnerPlayerIndex = -1;
            if (tile.FortressMarker != null)
            {
                Destroy(tile.FortressMarker);
                tile.FortressMarker = null;
            }
        }

        void EnsureFortressMarker(BoardTile tile, PlayerState owner)
        {
            if (tile?.View == null)
                return;

            if (tile.FortressMarker != null)
                Destroy(tile.FortressMarker);

            var root = new GameObject("FortressMarker");
            root.transform.SetParent(tile.View.transform, worldPositionStays: false);
            root.transform.localPosition = new Vector3(0f, 0.12f, 0f);

            var baseBlock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseBlock.name = "Base";
            baseBlock.transform.SetParent(root.transform, worldPositionStays: false);
            baseBlock.transform.localPosition = Vector3.zero;
            baseBlock.transform.localScale = new Vector3(0.34f, 0.22f, 0.34f);
            var baseR = baseBlock.GetComponent<Renderer>();
            if (baseR != null)
                baseR.material.color = owner.Color * 0.55f + Color.gray * 0.45f;

            var tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tower.name = "Tower";
            tower.transform.SetParent(root.transform, worldPositionStays: false);
            tower.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            tower.transform.localScale = new Vector3(0.2f, 0.28f, 0.2f);
            var towerR = tower.GetComponent<Renderer>();
            if (towerR != null)
                towerR.material.color = owner.Color * 0.75f;

            foreach (var col in root.GetComponentsInChildren<Collider>())
                UnityEngine.Object.Destroy(col);

            tile.FortressMarker = root;
        }

        void ClearFortressTurnState()
        {
            _fortressBreathUsedThisTurn.Clear();
            PendingFortressPlacement = false;
        }

        public bool TryAiPlaceFortress()
        {
            var p = CurrentPlayer;
            if (p == null || !p.DeployEnergize.Contains(EnergizeDeploymentId.Fortress))
                return false;
            var hex = PickAiFortressPlacementHex(p);
            return hex != null && TryPlaceFortressOnHex(hex);
        }

        public bool TryAiFortressBreathDuringDeploy()
        {
            if (!CanUseDeploymentEnergizeNow() || Board == null)
                return false;

            foreach (var tile in Board.AllTiles)
            {
                if (!CanBeginFortressBreathDuringDeploy(tile))
                    continue;
                return TryBeginFortressBreathFromHex(tile);
            }

            return false;
        }

        BoardTile PickAiFortressPlacementHex(PlayerState player)
        {
            if (player == null || Board == null)
                return null;

            BoardTile best = null;
            int bestScore = int.MinValue;
            foreach (var tile in Board.AllTiles)
            {
                if (!CanPlaceFortressOnTile(player, tile))
                    continue;

                int score = 0;
                foreach (var n in Board.GetNeighbors(tile))
                {
                    foreach (var u in FindObjectsOfType<UnitInstance>())
                    {
                        if (u.Tile == n && u.Owner != player)
                            score += 3;
                    }
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = tile;
                }
            }

            return best;
        }
    }
}
