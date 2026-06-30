using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace NexusGame
{
    public partial class GameController
    {
        const int BattleExtensionMagic = 0x42545A31; // BTZ1

        internal void WriteOnlineBattleExtension(BinaryWriter w)
        {
            w.Write(BattleExtensionMagic);

            w.Write(BattlePlan?.Count ?? 0);
            if (BattlePlan != null)
            {
                foreach (var e in BattlePlan)
                {
                    w.Write(e?.Hex != null ? (short)e.Hex.Q : short.MinValue);
                    w.Write(e?.Hex != null ? (short)e.Hex.R : short.MinValue);
                    w.Write(e?.DefenderPlayerIndex ?? -1);
                }
            }

            WriteTileRef(w, _battleHex);
            w.Write(_battleAttacker != null ? _battleAttacker.PlayerIndex : -1);
            w.Write(_battleDefender != null ? _battleDefender.PlayerIndex : -1);

            w.Write(EnergizePromptPlayer != null ? EnergizePromptPlayer.PlayerIndex : -1);
            w.Write(EnergizeBattleContext ?? "");

            w.Write(FocusFirePicker != null ? FocusFirePicker.PlayerIndex : -1);
            w.Write(FocusFireForAttackerSide);
            WriteTileRef(w, _focusFireHex);
            w.Write(_pendingFocusFireCard);

            bool hasCasualty = CasualtyPick != null;
            w.Write(hasCasualty);
            if (hasCasualty)
            {
                w.Write(CasualtyPick.Owner != null ? CasualtyPick.Owner.PlayerIndex : -1);
                w.Write(CasualtyPick.Required);
                var pool = BuildDeterministicCasualtyPool(CasualtyPick.Pool);
                w.Write(pool.Count);
                for (int i = 0; i < pool.Count; i++)
                    WriteUnitRef(w, pool[i]);
                var selectedIdx = new List<byte>();
                foreach (var u in CasualtyPick.Selected)
                {
                    int idx = pool.IndexOf(u);
                    if (idx >= 0 && idx < 255)
                        selectedIdx.Add((byte)idx);
                }

                w.Write(selectedIdx.Count);
                for (int i = 0; i < selectedIdx.Count; i++)
                    w.Write(selectedIdx[i]);
            }

            bool hasSecret = SecretMissionOffer != null;
            w.Write(hasSecret);
            if (hasSecret)
            {
                w.Write(SecretMissionOffer.Player != null ? SecretMissionOffer.Player.PlayerIndex : -1);
                w.Write(SecretMissionOffer.Waiting);
                w.Write(SecretMissionOffer.OffersFallbackBattleVp);
                var eligible = SecretMissionOffer.EligibleIndices ?? new List<int>();
                w.Write(eligible.Count);
                for (int i = 0; i < eligible.Count; i++)
                    w.Write(eligible[i]);
            }

            w.Write(HasActiveBattleStep);
            w.Write((byte)(int)ActiveBattleStepUnitType);
            w.Write(ActiveBattleHitsOnAttacker);
            w.Write(ActiveBattleHitsOnDefender);
            w.Write(_battleClashIntroActive);

            var dice = LastBattleUiDiceRoll;
            w.Write(dice.HasValue);
            if (dice.HasValue)
            {
                var d = dice.Value;
                w.Write((byte)(int)d.UnitType);
                w.Write(d.AttackerRolling);
                w.Write(d.Dice);
                w.Write(d.Need);
                w.Write(d.Impossible);
                w.Write(d.Hits);
                w.Write(d.Rolls?.Length ?? 0);
                if (d.Rolls != null)
                {
                    for (int i = 0; i < d.Rolls.Length; i++)
                        w.Write(d.Rolls[i]);
                }
            }

            w.Write(LiveBattlePhaseLog ?? "");
            w.Write(_battleArrangementPickCount);
        }

        /// <summary>Reset battle UI fields before applying a new extension block (avoids stale hex / casualty state on clients).</summary>
        internal void ClearOnlineBattleUiState()
        {
            BattlePlan.Clear();
            _battleArrangementPickCount = 0;
            _battleHex = null;
            _battleAttacker = null;
            _battleDefender = null;
            EnergizePromptPlayer = null;
            EnergizeBattleContext = null;
            _energizeRoundActive = false;
            FocusFirePicker = null;
            FocusFireForAttackerSide = false;
            _focusFireHex = null;
            _pendingFocusFireCard = false;
            CasualtyPick = null;
            SecretMissionOffer = null;
            HasActiveBattleStep = false;
            ActiveBattleStepUnitType = default;
            ActiveBattleHitsOnAttacker = 0;
            ActiveBattleHitsOnDefender = 0;
            _battleClashIntroActive = false;
            _lastBattleUiDiceRoll = null;
            _liveBattleLines = null;
            ClearBattleCasualtyDeathFx();
        }

        internal void ReadOnlineBattleExtension(BinaryReader r, bool battleBlocking)
        {
            if (r.BaseStream.Position >= r.BaseStream.Length)
            {
                if (!battleBlocking)
                    ClearOnlineBattleUiState();
                return;
            }

            if (r.ReadInt32() != BattleExtensionMagic)
            {
                Debug.LogWarning("[Net] Battle snapshot extension magic mismatch — keeping prior battle UI.");
                if (!battleBlocking)
                    ClearOnlineBattleUiState();
                return;
            }

            ClearOnlineBattleUiState();

            int planCount = r.ReadInt32();
            BattlePlan.Clear();
            for (int i = 0; i < planCount; i++)
            {
                short q = r.ReadInt16();
                short tr = r.ReadInt16();
                int defenderIdx = r.ReadInt32();
                var hex = ReadTileRef(q, tr);
                if (hex != null)
                {
                    BattlePlan.Add(new PlannedBattleEntry
                    {
                        Hex = hex,
                        DefenderPlayerIndex = defenderIdx
                    });
                }
            }

            _battleHex = ReadTileRef(r.ReadInt16(), r.ReadInt16());
            int attIdx = r.ReadInt32();
            int defIdx = r.ReadInt32();
            _battleAttacker = PlayerByIndex(attIdx);
            _battleDefender = PlayerByIndex(defIdx);

            int energizeIdx = r.ReadInt32();
            EnergizePromptPlayer = PlayerByIndex(energizeIdx);
            EnergizeBattleContext = r.ReadString();

            int focusIdx = r.ReadInt32();
            FocusFirePicker = PlayerByIndex(focusIdx);
            FocusFireForAttackerSide = r.ReadBoolean();
            _focusFireHex = ReadTileRef(r.ReadInt16(), r.ReadInt16());
            _pendingFocusFireCard = r.ReadBoolean();

            if (r.ReadBoolean())
            {
                int ownerIdx = r.ReadInt32();
                int required = r.ReadInt32();
                int poolCount = r.ReadInt32();
                var pool = new List<UnitInstance>(poolCount);
                for (int i = 0; i < poolCount; i++)
                {
                    var u = ReadUnitRef(r);
                    if (u != null)
                        pool.Add(u);
                }

                int selCount = r.ReadInt32();
                var selected = new List<UnitInstance>();
                for (int i = 0; i < selCount; i++)
                {
                    int idx = r.ReadByte();
                    if (idx >= 0 && idx < pool.Count)
                        selected.Add(pool[idx]);
                }

                CasualtyPick = new CasualtyPickState
                {
                    Owner = PlayerByIndex(ownerIdx),
                    Pool = pool,
                    Required = required,
                    Selected = selected
                };
            }
            else
            {
                CasualtyPick = null;
            }

            if (r.ReadBoolean())
            {
                int playerIdx = r.ReadInt32();
                bool waiting = r.ReadBoolean();
                bool fallback = r.ReadBoolean();
                int eligCount = r.ReadInt32();
                var eligible = new List<int>(eligCount);
                for (int i = 0; i < eligCount; i++)
                    eligible.Add(r.ReadInt32());

                SecretMissionOffer = new SecretMissionOfferState
                {
                    Player = PlayerByIndex(playerIdx),
                    Waiting = waiting,
                    OffersFallbackBattleVp = fallback,
                    EligibleIndices = eligible
                };
            }
            else
            {
                SecretMissionOffer = null;
            }

            HasActiveBattleStep = r.ReadBoolean();
            ActiveBattleStepUnitType = (UnitType)r.ReadByte();
            ActiveBattleHitsOnAttacker = r.ReadInt32();
            ActiveBattleHitsOnDefender = r.ReadInt32();
            _battleClashIntroActive = r.ReadBoolean();

            if (r.ReadBoolean())
            {
                var unitType = (UnitType)r.ReadByte();
                bool attackerRolling = r.ReadBoolean();
                int dice = r.ReadInt32();
                int need = r.ReadInt32();
                bool impossible = r.ReadBoolean();
                int hits = r.ReadInt32();
                int rollCount = r.ReadInt32();
                var rolls = new int[rollCount];
                for (int i = 0; i < rollCount; i++)
                    rolls[i] = r.ReadInt32();
                _lastBattleUiDiceRoll = new BattleUiDiceRoll(unitType, attackerRolling, dice, need, impossible, hits,
                    rolls);
            }
            else
            {
                _lastBattleUiDiceRoll = null;
            }

            string liveLog = r.ReadString();
            _liveBattleLines = string.IsNullOrEmpty(liveLog)
                ? null
                : liveLog.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).ToList();

            if (r.BaseStream.Position < r.BaseStream.Length)
                _battleArrangementPickCount = r.ReadInt32();
            else
                _battleArrangementPickCount = PendingBattleArrangement && BattlePlan.Count == 1 ? 1 : 0;
        }

        static void WriteTileRef(BinaryWriter w, BoardTile tile)
        {
            w.Write(tile != null ? (short)tile.Q : short.MinValue);
            w.Write(tile != null ? (short)tile.R : short.MinValue);
        }

        BoardTile ReadTileRef(short q, short tr)
        {
            if (q == short.MinValue)
                return null;
            return Board?.GetTile(q, tr);
        }

        static void WriteUnitRef(BinaryWriter w, UnitInstance u)
        {
            w.Write(u?.Owner != null ? (byte)u.Owner.PlayerIndex : byte.MaxValue);
            w.Write(u?.Definition != null ? (byte)(int)u.Definition.Type : byte.MaxValue);
            w.Write(u?.Tile != null ? (short)u.Tile.Q : short.MinValue);
            w.Write(u?.Tile != null ? (short)u.Tile.R : short.MinValue);
        }

        UnitInstance ReadUnitRef(BinaryReader r)
        {
            int ownerIdx = r.ReadByte();
            var type = (UnitType)r.ReadByte();
            short q = r.ReadInt16();
            short tr = r.ReadInt16();
            if (ownerIdx == byte.MaxValue || q == short.MinValue)
                return null;

            return FindUnitInstance(ownerIdx, type, q, tr);
        }

        UnitInstance FindUnitInstance(int ownerIdx, UnitType type, int q, int r)
        {
            foreach (var u in FindObjectsOfType<UnitInstance>())
            {
                if (u == null || u.Owner == null || u.Definition == null || u.Tile == null)
                    continue;
                if (u.Owner.PlayerIndex == ownerIdx && u.Definition.Type == type && u.Tile.Q == q && u.Tile.R == r)
                    return u;
            }

            return null;
        }

        PlayerState PlayerByIndex(int seat)
        {
            return PlayerBySeat(seat);
        }

        static List<UnitInstance> BuildDeterministicCasualtyPool(List<UnitInstance> source)
        {
            if (source == null)
                return new List<UnitInstance>();
            return source.Where(u => u != null)
                .OrderBy(u => (int)u.Definition.Type)
                .ThenBy(u => u.GetInstanceID())
                .ToList();
        }

        public bool ApplyCasualtySelectionFromTypeCounts(int seat, int[] types, int[] counts)
        {
            if (CasualtyPick == null || CasualtyPick.Owner == null || CasualtyPick.Owner.PlayerIndex != seat)
                return false;

            CasualtyPick.Pool.RemoveAll(u => u == null);
            var pool = BuildDeterministicCasualtyPool(CasualtyPick.Pool);
            CasualtyPick.Pool = pool;
            CasualtyPick.Selected.Clear();

            if (types != null && counts != null)
            {
                int n = Mathf.Min(types.Length, counts.Length);
                for (int i = 0; i < n; i++)
                {
                    var type = (UnitType)types[i];
                    int want = counts[i];
                    for (int k = 0; k < want; k++)
                    {
                        var next = pool.FirstOrDefault(u =>
                            u != null && u.Definition.Type == type && !CasualtyPick.Selected.Contains(u));
                        if (next == null)
                            return false;
                        CasualtyPick.Selected.Add(next);
                    }
                }
            }

            SubmitCasualtyPick();
            return true;
        }

        internal void WritePlayerSecretMissionsToNetwork(BinaryWriter w, PlayerState p) =>
            WritePlayerSecretMissions(w, p);

        void WritePlayerSecretMissions(BinaryWriter w, PlayerState p)
        {
            var list = p?.SecretMissions ?? new List<SecretMissionInHand>();
            w.Write(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                w.Write(s?.InstanceId ?? 0);
                w.Write((int)(s?.Kind ?? SecretMissionKind.Battle));
                w.Write(s?.VictoryPoints ?? 0);
                w.Write(s?.MissionTypeId ?? 0);
            }
        }

        void ReadPlayerSecretMissions(BinaryReader r, PlayerState p)
        {
            if (p == null)
                return;
            int count = r.ReadInt32();
            p.SecretMissions = new List<SecretMissionInHand>(count);
            for (int i = 0; i < count; i++)
            {
                p.SecretMissions.Add(new SecretMissionInHand
                {
                    InstanceId = r.ReadInt32(),
                    Kind = (SecretMissionKind)r.ReadInt32(),
                    VictoryPoints = r.ReadInt32(),
                    MissionTypeId = r.ReadInt32()
                });
            }
        }

        void BattleUiStateChanged()
        {
            NotifyOnlineStateChanged();
        }
    }
}
