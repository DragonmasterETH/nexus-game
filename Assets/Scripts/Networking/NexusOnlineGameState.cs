using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace NexusGame
{
    /// <summary>Binary snapshot of visible match state for host → client replication.</summary>
    public static class NexusOnlineGameState
    {
        const int SnapshotMagic = 0x4E58_4753; // NXGS

        public static byte[] Capture(GameController game)
        {
            if (game == null || game.Board == null || game.Players == null)
                return Array.Empty<byte>();

            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);

            w.Write(SnapshotMagic);
            w.Write(game.OnlineSyncVersion);
            w.Write(game.IsGameOver);
            w.Write(game.CurrentPlayer != null ? game.CurrentPlayer.PlayerIndex : 0);
            w.Write(game.TurnNumber);
            w.Write(game.CompletedPlayerTurnsForSync);
            w.Write(game.NormalMovementOccurredThisTurnForSync);
            w.Write(game.AnyMovementOccurredThisTurn);
            w.Write(game.BattlePhaseBlockingPlay);
            w.Write(game.PendingBattleArrangement);

            var retreat = game.ActiveRetreatSourceThisTurn;
            w.Write(retreat != null ? retreat.Q : int.MinValue);
            w.Write(retreat != null ? retreat.R : int.MinValue);

            w.Write(game.Players.Count);
            foreach (var p in game.Players)
            {
                w.Write(p.Rubium);
                w.Write(p.VictoryPoints);
                w.Write(p.DeploymentPurchaseDiscountRubium);
                WriteIntList(w, p.DeployEnergize?.Select(e => (int)e));
                WriteIntList(w, p.BattleEnergize?.Select(e => (int)e));
                game.WritePlayerSecretMissionsToNetwork(w, p);
            }

            var tiles = game.Board.AllTiles.OrderBy(t => t.Q).ThenBy(t => t.R).ToList();
            w.Write(tiles.Count);
            foreach (var t in tiles)
            {
                w.Write((short)t.Q);
                w.Write((short)t.R);
                w.Write((sbyte)(t.Owner != null ? t.Owner.PlayerIndex : -1));
                w.Write(t.ExplorationRevealed);
                w.Write((byte)Mathf.Clamp(t.ExtraMineYield, 0, 255));
                w.Write((sbyte)t.FortressOwnerPlayerIndex);
            }

            var units = UnityEngine.Object.FindObjectsOfType<UnitInstance>()
                .Where(u => u != null && u.Owner != null && u.Definition != null && u.Tile != null)
                .OrderBy(u => u.Owner.PlayerIndex)
                .ThenBy(u => (int)u.Definition.Type)
                .ThenBy(u => u.Tile.Q)
                .ThenBy(u => u.Tile.R)
                .ThenBy(u => u.GetInstanceID())
                .ToList();

            w.Write(units.Count);
            foreach (var u in units)
            {
                w.Write((byte)u.Owner.PlayerIndex);
                w.Write((byte)(int)u.Definition.Type);
                w.Write((short)u.Tile.Q);
                w.Write((short)u.Tile.R);
                w.Write(u.HasMovedThisTurn);
            }

            game.WriteOnlineBattleExtension(w);

            if (game.IsGameOver)
            {
                w.Write(game.Winner != null ? game.Winner.PlayerIndex : -1);
                w.Write(game.EndGameReason ?? "");
            }

            return ms.ToArray();
        }

        public static bool TryApply(GameController game, byte[] data, uint version, MobileInputController input)
        {
            if (game == null || data == null || data.Length == 0)
                return false;

            try
            {
                using var ms = new MemoryStream(data);
                using var r = new BinaryReader(ms);

                if (r.ReadInt32() != SnapshotMagic)
                    return false;

                uint snapVersion = r.ReadUInt32();
                uint effectiveVersion = (uint)Mathf.Max(snapVersion, version);
                if (effectiveVersion <= game.LastAppliedOnlineSyncVersion)
                    return false;

                game.ApplyOnlineSnapshotInternal(r, input);
                game.LastAppliedOnlineSyncVersion = effectiveVersion;
                Debug.Log($"[Net] Applied snapshot v{effectiveVersion} (turn={game.TurnNumber}, seat={game.CurrentPlayer?.PlayerIndex})");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Net] Failed to apply game snapshot: {ex.Message}");
                return false;
            }
        }

        static void WriteIntList(BinaryWriter w, IEnumerable<int> values)
        {
            var list = values?.ToList() ?? new List<int>();
            w.Write(list.Count);
            for (int i = 0; i < list.Count; i++)
                w.Write(list[i]);
        }
    }
}
