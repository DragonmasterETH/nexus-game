using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace NexusGame
{
    public partial class GameController
    {
        uint _onlineSyncVersion;
        uint _lastAppliedOnlineSyncVersion;
        bool _onlineReplicationEnabled;

        public uint OnlineSyncVersion => _onlineSyncVersion;
        public uint LastAppliedOnlineSyncVersion
        {
            get => _lastAppliedOnlineSyncVersion;
            internal set => _lastAppliedOnlineSyncVersion = value;
        }

        /// <summary>Exposed for snapshot capture (same as internal retreat source).</summary>
        public int CompletedPlayerTurnsForSync => _completedPlayerTurns;
        public bool NormalMovementOccurredThisTurnForSync => _normalMovementOccurredThisTurn;

        public bool IsApplyingOnlineSnapshot { get; private set; }

        /// <summary>Called after match setup so init spawns do not spam snapshots.</summary>
        public void EnableOnlineReplication()
        {
            _onlineReplicationEnabled = true;
        }

        /// <summary>Host: broadcast a snapshot immediately after local simulation changes.</summary>
        public void NotifyOnlineStateChanged()
        {
            if (!NexusSession.IsOnline || IsApplyingOnlineSnapshot)
                return;

            if (!IsOnlineSimulationAuthority())
            {
                if (NexusSession.IsHost)
                    Debug.LogWarning("[Net] Host game changed but NGO server is not active — state will not sync.");
                return;
            }

            _onlineSyncVersion++;
            var bridge = NexusGameCommands.Bridge;
            if (bridge != null && bridge.IsSpawned)
                bridge.PushGameStateToClients();
            else
                NexusOnlineBridge.Instance?.PushGameStateToClients();
        }

        internal void AfterOnlineHostMutation()
        {
            if (!_onlineReplicationEnabled || IsApplyingOnlineSnapshot)
                return;
            NotifyOnlineStateChanged();
        }

        /// <summary>Alias kept for existing call sites.</summary>
        internal void MaybeNotifyOnlineStateChanged() => AfterOnlineHostMutation();

        static bool IsOnlineSimulationAuthority()
        {
            var nm = NetworkManager.Singleton;
            return nm != null && nm.IsServer && nm.IsListening;
        }

        public bool ApplyOnlineSnapshot(byte[] data, uint version)
        {
            if (!NexusSession.IsOnline || IsApplyingOnlineSnapshot)
                return false;

            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsServer)
                return false;

            var input = FindObjectOfType<MobileInputController>();
            return NexusOnlineGameState.TryApply(this, data, version, input);
        }

        internal void ApplyOnlineSnapshotInternal(BinaryReader r, MobileInputController input)
        {
            IsApplyingOnlineSnapshot = true;
            try
            {
                bool isGameOver = r.ReadBoolean();
                int currentPlayerSeat = r.ReadInt32();
                _turnNumber = r.ReadInt32();
                _completedPlayerTurns = r.ReadInt32();
                _normalMovementOccurredThisTurn = r.ReadBoolean();
                _anyMovementOccurredThisTurn = r.ReadBoolean();
                BattlePhaseBlockingPlay = r.ReadBoolean();
                PendingBattleArrangement = r.ReadBoolean();

                int retreatQ = r.ReadInt32();
                int retreatR = r.ReadInt32();
                _activeRetreatSourceThisTurn = retreatQ == int.MinValue
                    ? null
                    : Board?.GetTile(retreatQ, retreatR);

                int playerCount = r.ReadInt32();
                for (int i = 0; i < playerCount && i < Players.Count; i++)
                {
                    var p = Players[i];
                    p.Rubium = r.ReadInt32();
                    p.VictoryPoints = r.ReadInt32();
                    p.DeploymentPurchaseDiscountRubium = r.ReadInt32();
                    p.DeployEnergize = ReadEnumList<EnergizeDeploymentId>(r);
                    p.BattleEnergize = ReadEnumList<EnergizeBattleId>(r);
                    ReadPlayerSecretMissions(r, p);
                }

                for (int i = Players.Count; i < playerCount; i++)
                    SkipPlayerPayload(r);

                int tileCount = r.ReadInt32();
                for (int i = 0; i < tileCount; i++)
                {
                    short q = r.ReadInt16();
                    short tr = r.ReadInt16();
                    sbyte ownerSeat = r.ReadSByte();
                    bool revealed = r.ReadBoolean();
                    byte mineYield = r.ReadByte();

                    var tile = Board?.GetTile(q, tr);
                    if (tile == null)
                        continue;

                    tile.Owner = PlayerBySeat(ownerSeat);
                    tile.ExtraMineYield = mineYield;

                    if (revealed && !tile.ExplorationRevealed)
                    {
                        tile.ExplorationRevealed = true;
                        if (tile.ExplorationMarker != null)
                        {
                            Destroy(tile.ExplorationMarker);
                            tile.ExplorationMarker = null;
                        }
                    }
                    else if (!revealed)
                    {
                        tile.ExplorationRevealed = false;
                    }

                    if (input != null && mineYield > 0)
                        input.UpdateMineLabel(tile);
                }

                RebuildUnitsFromSnapshot(r);

                _currentPlayerIndex = SeatToListIndex(currentPlayerSeat);
                ReadOnlineBattleExtension(r, BattlePhaseBlockingPlay);

                if (isGameOver && !IsGameOver)
                {
                    int winnerIndex = r.ReadInt32();
                    string reason = r.ReadString();
                    var winner = Players.FirstOrDefault(p => p.PlayerIndex == winnerIndex);
                    if (winner != null)
                        EndGame(winner, reason);
                }
                else if (isGameOver && IsGameOver)
                {
                    r.ReadInt32();
                    r.ReadString();
                }

                input?.ClearSelection();
            }
            finally
            {
                IsApplyingOnlineSnapshot = false;
            }
        }

        void RebuildUnitsFromSnapshot(BinaryReader r)
        {
            int unitCount = r.ReadInt32();
            var desired = new List<(PlayerState owner, UnitType type, BoardTile tile, bool hasMoved)>(unitCount);
            for (int i = 0; i < unitCount; i++)
            {
                int ownerIndex = r.ReadByte();
                var type = (UnitType)r.ReadByte();
                short q = r.ReadInt16();
                short tr = r.ReadInt16();
                bool hasMoved = r.ReadBoolean();

                if (ownerIndex < 0 || ownerIndex >= Players.Count)
                    continue;

                var tile = Board?.GetTile(q, tr);
                if (tile == null)
                    continue;

                desired.Add((Players[ownerIndex], type, tile, hasMoved));
            }

            var existing = new List<UnitInstance>();
            foreach (var u in FindObjectsOfType<UnitInstance>())
            {
                if (u != null && u.Owner != null && u.Definition != null)
                    existing.Add(u);
            }

            var usedExisting = new HashSet<UnitInstance>();
            var matchedDesired = new bool[desired.Count];

            for (int i = 0; i < desired.Count; i++)
            {
                var d = desired[i];
                foreach (var u in existing)
                {
                    if (usedExisting.Contains(u))
                        continue;
                    if (u.Owner != d.owner || u.Definition.Type != d.type || u.Tile != d.tile)
                        continue;

                    u.SyncToTile(d.tile, d.hasMoved);
                    usedExisting.Add(u);
                    matchedDesired[i] = true;
                    break;
                }
            }

            for (int i = 0; i < desired.Count; i++)
            {
                if (matchedDesired[i])
                    continue;

                var d = desired[i];
                UnitInstance match = null;
                foreach (var u in existing)
                {
                    if (usedExisting.Contains(u))
                        continue;
                    if (u.Owner != d.owner || u.Definition.Type != d.type)
                        continue;
                    match = u;
                    break;
                }

                if (match != null)
                {
                    match.SyncToTile(d.tile, d.hasMoved);
                    usedExisting.Add(match);
                    matchedDesired[i] = true;
                }
            }

            for (int i = 0; i < desired.Count; i++)
            {
                if (matchedDesired[i])
                    continue;

                var d = desired[i];
                CreateUnit(d.owner, d.type, d.tile, d.hasMoved);
            }

            foreach (var u in existing)
            {
                if (!usedExisting.Contains(u))
                    Destroy(u.gameObject);
            }

            foreach (var p in Players)
                _unitsByPlayer[p] = new List<UnitInstance>();

            foreach (var u in FindObjectsOfType<UnitInstance>())
            {
                if (u == null || u.Owner == null)
                    continue;
                if (_unitsByPlayer.TryGetValue(u.Owner, out var list))
                    list.Add(u);
            }
        }

        static List<TEnum> ReadEnumList<TEnum>(BinaryReader r) where TEnum : struct
        {
            int count = r.ReadInt32();
            var list = new List<TEnum>(count);
            for (int i = 0; i < count; i++)
                list.Add((TEnum)(object)r.ReadInt32());
            return list;
        }

        static void SkipPlayerPayload(BinaryReader r)
        {
            r.ReadInt32();
            r.ReadInt32();
            r.ReadInt32();
            SkipIntList(r);
            SkipIntList(r);
            SkipSecretMissionPayload(r);
        }

        static void SkipSecretMissionPayload(BinaryReader r)
        {
            int count = r.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                r.ReadInt32();
                r.ReadInt32();
                r.ReadInt32();
                r.ReadInt32();
            }
        }

        static void SkipIntList(BinaryReader r)
        {
            int count = r.ReadInt32();
            for (int i = 0; i < count; i++)
                r.ReadInt32();
        }

        int SeatToListIndex(int seat)
        {
            if (Players == null || Players.Count == 0)
                return 0;
            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].PlayerIndex == seat)
                    return i;
            }

            return Mathf.Clamp(seat, 0, Players.Count - 1);
        }

        PlayerState PlayerBySeat(int seat)
        {
            if (seat < 0 || Players == null)
                return null;
            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].PlayerIndex == seat)
                    return Players[i];
            }

            return null;
        }

        /// <summary>Buy on a specific home tile (HUD path).</summary>
        public bool TryPurchaseUnitOnHome(PlayerState player, UnitType type, int discountUse, int pay, BoardTile homeTile)
        {
            if (IsGameOver || player != CurrentPlayer || BattlePhaseBlockingPlay || DragonPhase != null)
                return false;
            if (_anyMovementOccurredThisTurn)
                return false;
            if (homeTile == null || !CanDeployToStartingHomeTile(player, homeTile))
                return false;
            if (player.Rubium < pay)
                return false;

            player.DeploymentPurchaseDiscountRubium -= discountUse;
            player.Rubium -= pay;
            SpawnUnit(player, type, homeTile);
            AfterOnlineHostMutation();
            return true;
        }
    }
}
