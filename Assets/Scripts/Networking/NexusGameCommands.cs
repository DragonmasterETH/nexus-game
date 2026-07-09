using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace NexusGame
{
    /// <summary>
    /// Player intents for host-authoritative play. Local paths call <see cref="GameController"/> directly;
    /// online clients send RPCs through <see cref="NexusOnlineBridge"/>.
    /// </summary>
    public static class NexusGameCommands
    {
        public static GameController Game { get; set; }
        public static NexusOnlineBridge Bridge { get; set; }

        static bool ShouldRouteOnlineIntentToHost()
        {
            if (!NexusSession.IsOnline)
                return false;
            var nm = NetworkManager.Singleton;
            return nm != null && nm.IsListening && !nm.IsServer;
        }

        static NexusOnlineBridge ResolveBridge()
        {
            if (Bridge != null && Bridge.IsSpawned)
                return Bridge;

            var found = Object.FindObjectOfType<NexusOnlineBridge>();
            if (found != null && found.IsSpawned)
            {
                Bridge = found;
                return found;
            }

            return null;
        }

        static bool SendEndTurnToHost(int seat)
        {
            var bridge = ResolveBridge();
            if (bridge != null)
            {
                bridge.RequestEndTurnServerRpc(seat);
                return true;
            }

            return NexusOnlineBridge.SendEndTurnIntent(seat);
        }

        static bool SendMoveGroupToHost(int seat, BoardTile from, BoardTile to, int[] types, int[] counts)
        {
            var bridge = ResolveBridge();
            if (bridge != null)
            {
                bridge.RequestMoveGroupServerRpc(seat, from.Q, from.R, to.Q, to.R, types, counts);
                return true;
            }

            return NexusOnlineBridge.SendMoveGroupIntent(seat, from.Q, from.R, to.Q, to.R, types, counts);
        }

        static bool SendPurchaseToHost(int seat, int unitType, int discountUse, int pay, BoardTile homeTile)
        {
            var bridge = ResolveBridge();
            if (bridge != null)
            {
                bridge.RequestPurchaseServerRpc(seat, unitType, discountUse, pay, homeTile.Q, homeTile.R);
                return true;
            }

            return NexusOnlineBridge.SendPurchaseIntent(seat, unitType, discountUse, pay, homeTile.Q, homeTile.R);
        }

        static bool SendConfirmBattleArrangementToHost(int seat)
        {
            var bridge = ResolveBridge();
            if (bridge != null)
            {
                bridge.RequestConfirmBattleArrangementServerRpc(seat);
                return true;
            }

            return NexusOnlineBridge.SendConfirmBattleArrangementIntent(seat);
        }

        static bool SendMoveBattlePlanEntryToHost(int seat, int index, int delta)
        {
            var bridge = ResolveBridge();
            if (bridge != null)
            {
                bridge.RequestMoveBattlePlanEntryServerRpc(seat, index, delta);
                return true;
            }

            return NexusOnlineBridge.SendMoveBattlePlanEntryIntent(seat, index, delta);
        }

        static bool SendSetBattleDefenderToHost(int seat, int planIndex, int defenderPlayerIndex)
        {
            var bridge = ResolveBridge();
            if (bridge != null)
            {
                bridge.RequestSetBattleDefenderServerRpc(seat, planIndex, defenderPlayerIndex);
                return true;
            }

            return NexusOnlineBridge.SendSetBattleDefenderIntent(seat, planIndex, defenderPlayerIndex);
        }

        static bool SendPickBattleAsNextToHost(int seat, int planIndex)
        {
            var bridge = ResolveBridge();
            if (bridge != null)
            {
                bridge.RequestPickBattleAsNextServerRpc(seat, planIndex);
                return true;
            }

            return NexusOnlineBridge.SendPickBattleAsNextIntent(seat, planIndex);
        }

        static bool SendStartBattleFromArrangementToHost(int seat, int planIndex)
        {
            var bridge = ResolveBridge();
            if (bridge != null)
            {
                bridge.RequestStartBattleFromArrangementServerRpc(seat, planIndex);
                return true;
            }

            return NexusOnlineBridge.SendStartBattleFromArrangementIntent(seat, planIndex);
        }

        static bool SendSubmitEnergizePassToHost(int seat)
        {
            var bridge = ResolveBridge();
            if (bridge != null)
            {
                bridge.RequestSubmitEnergizePassServerRpc(seat);
                return true;
            }

            return NexusOnlineBridge.SendSubmitEnergizePassIntent(seat);
        }

        static bool SendSubmitEnergizePlayToHost(int seat, int energizeId)
        {
            var bridge = ResolveBridge();
            if (bridge != null)
            {
                bridge.RequestSubmitEnergizePlayServerRpc(seat, energizeId);
                return true;
            }

            return NexusOnlineBridge.SendSubmitEnergizePlayIntent(seat, energizeId);
        }

        static bool SendSubmitFocusFireUnitTypeToHost(int seat, int unitType)
        {
            var bridge = ResolveBridge();
            if (bridge != null)
            {
                bridge.RequestSubmitFocusFireUnitTypeServerRpc(seat, unitType);
                return true;
            }

            return NexusOnlineBridge.SendSubmitFocusFireUnitTypeIntent(seat, unitType);
        }

        static bool SendCancelFocusFireRefundToHost(int seat)
        {
            var bridge = ResolveBridge();
            if (bridge != null)
            {
                bridge.RequestCancelFocusFireRefundServerRpc(seat);
                return true;
            }

            return NexusOnlineBridge.SendCancelFocusFireRefundIntent(seat);
        }

        static bool SendSubmitCasualtyPickToHost(int seat, int[] types, int[] counts)
        {
            var bridge = ResolveBridge();
            if (bridge != null)
            {
                bridge.RequestSubmitCasualtyPickServerRpc(seat, types, counts);
                return true;
            }

            return NexusOnlineBridge.SendSubmitCasualtyPickIntent(seat, types, counts);
        }

        static bool SendClaimFallbackBattleSecretVpToHost(int seat)
        {
            var bridge = ResolveBridge();
            if (bridge != null)
            {
                bridge.RequestClaimFallbackBattleSecretVpServerRpc(seat);
                return true;
            }

            return NexusOnlineBridge.SendClaimFallbackBattleSecretVpIntent(seat);
        }

        static bool SendPlaySecretMissionAtIndexToHost(int seat, int indexInHand)
        {
            var bridge = ResolveBridge();
            if (bridge != null)
            {
                bridge.RequestPlaySecretMissionAtIndexServerRpc(seat, indexInHand);
                return true;
            }

            return NexusOnlineBridge.SendPlaySecretMissionAtIndexIntent(seat, indexInHand);
        }

        static bool SendSkipSecretMissionPlayToHost(int seat)
        {
            var bridge = ResolveBridge();
            if (bridge != null)
            {
                bridge.RequestSkipSecretMissionPlayServerRpc(seat);
                return true;
            }

            return NexusOnlineBridge.SendSkipSecretMissionPlayIntent(seat);
        }

        public static void RequestEndTurn()
        {
            if (Game == null || Game.IsGameOver)
                return;
            if (!Game.CanLocalPlayerActNow())
                return;

            if (ShouldRouteOnlineIntentToHost())
            {
                if (!SendEndTurnToHost(NexusSession.LocalPlayerIndex))
                    Debug.LogWarning("[Net] RequestEndTurn — not connected to host.");
                return;
            }

            Game.EndTurn();
        }

        public static void RequestMoveGroup(BoardTile from, BoardTile to,
            IReadOnlyDictionary<UnitType, int> selection, IReadOnlyCollection<UnitType> explicitTypes)
        {
            if (Game == null || from == null || to == null || selection == null || explicitTypes == null)
                return;
            if (!Game.CanLocalPlayerActNow())
                return;

            if (ShouldRouteOnlineIntentToHost())
            {
                PackMoveSelection(selection, explicitTypes, out var types, out var counts);
                if (!SendMoveGroupToHost(NexusSession.LocalPlayerIndex, from, to, types, counts))
                    Debug.LogWarning("[Net] RequestMoveGroup — not connected to host.");
                return;
            }

            var input = Object.FindObjectOfType<MobileInputController>();
            if (input != null)
                input.TryExecuteMoveGroup(Game.CurrentPlayer, from, to, selection, explicitTypes);
        }

        public static void RequestPurchase(PlayerState player, UnitType type, int discountUse, int pay,
            BoardTile homeTile)
        {
            if (Game == null || player == null || homeTile == null)
                return;
            if (!Game.CanLocalPlayerActNow() || Game.CurrentPlayer != player)
                return;

            if (ShouldRouteOnlineIntentToHost())
            {
                if (!SendPurchaseToHost(player.PlayerIndex, (int)type, discountUse, pay, homeTile))
                    Debug.LogWarning("[Net] RequestPurchase — not connected to host.");
                return;
            }

            if (Game.TryPurchaseUnitOnHome(player, type, discountUse, pay, homeTile))
                return;
        }

        static void PackMoveSelection(IReadOnlyDictionary<UnitType, int> selection,
            IReadOnlyCollection<UnitType> explicitTypes, out int[] types, out int[] counts)
        {
            var typeList = new List<int>();
            var countList = new List<int>();
            foreach (var kvp in selection)
            {
                if (kvp.Value <= 0 || !explicitTypes.Contains(kvp.Key))
                    continue;
                typeList.Add((int)kvp.Key);
                countList.Add(kvp.Value);
            }

            types = typeList.ToArray();
            counts = countList.ToArray();
        }

        public static void RequestConfirmBattleArrangement()
        {
            if (Game == null || !Game.PendingBattleArrangement || Game.CurrentPlayer == null)
                return;
            if (!Game.CanLocalPlayerActFor(Game.CurrentPlayer))
                return;

            if (ShouldRouteOnlineIntentToHost())
            {
                if (!SendConfirmBattleArrangementToHost(NexusSession.LocalPlayerIndex))
                    Debug.LogWarning("[Net] RequestConfirmBattleArrangement — not connected to host.");
                return;
            }

            Game.ConfirmBattleArrangement();
        }

        public static void RequestMoveBattlePlanEntry(int index, int delta)
        {
            if (Game == null || Game.CurrentPlayer == null)
                return;
            if (!Game.CanLocalPlayerActFor(Game.CurrentPlayer))
                return;

            if (ShouldRouteOnlineIntentToHost())
            {
                if (!SendMoveBattlePlanEntryToHost(NexusSession.LocalPlayerIndex, index, delta))
                    Debug.LogWarning("[Net] RequestMoveBattlePlanEntry — not connected to host.");
                return;
            }

            Game.MoveBattlePlanEntry(index, delta);
        }

        public static void RequestPickBattleAsNext(int planIndex)
        {
            if (Game == null || !Game.PendingBattleArrangement || Game.CurrentPlayer == null)
                return;
            if (!Game.CanLocalPlayerActFor(Game.CurrentPlayer))
                return;

            if (ShouldRouteOnlineIntentToHost())
            {
                if (!SendPickBattleAsNextToHost(NexusSession.LocalPlayerIndex, planIndex))
                    Debug.LogWarning("[Net] RequestPickBattleAsNext — not connected to host.");
                return;
            }

            Game.PickBattleAsNext(planIndex);
        }

        public static void RequestStartBattleFromArrangement(int planIndex)
        {
            if (Game == null || !Game.PendingBattleArrangement || Game.CurrentPlayer == null)
                return;
            if (!Game.CanLocalPlayerActFor(Game.CurrentPlayer))
                return;

            if (ShouldRouteOnlineIntentToHost())
            {
                if (!SendStartBattleFromArrangementToHost(NexusSession.LocalPlayerIndex, planIndex))
                    Debug.LogWarning("[Net] RequestStartBattleFromArrangement — not connected to host.");
                return;
            }

            Game.StartBattleFromArrangement(planIndex);
        }

        public static void RequestSetBattleDefender(int planIndex, int defenderPlayerIndex)
        {
            if (Game == null || Game.CurrentPlayer == null)
                return;
            if (!Game.CanLocalPlayerActFor(Game.CurrentPlayer))
                return;

            if (ShouldRouteOnlineIntentToHost())
            {
                if (!SendSetBattleDefenderToHost(NexusSession.LocalPlayerIndex, planIndex, defenderPlayerIndex))
                    Debug.LogWarning("[Net] RequestSetBattleDefender — not connected to host.");
                return;
            }

            Game.SetBattleDefenderForEntry(planIndex, defenderPlayerIndex);
        }

        public static void RequestSubmitEnergizePass()
        {
            if (Game?.EnergizePromptPlayer == null)
                return;
            if (!Game.CanLocalPlayerActFor(Game.EnergizePromptPlayer))
                return;

            if (ShouldRouteOnlineIntentToHost())
            {
                if (!SendSubmitEnergizePassToHost(NexusSession.LocalPlayerIndex))
                    Debug.LogWarning("[Net] RequestSubmitEnergizePass — not connected to host.");
                return;
            }

            Game.SubmitEnergizePass();
        }

        public static void RequestSubmitEnergizePlay(EnergizeBattleId id)
        {
            if (Game?.EnergizePromptPlayer == null)
                return;
            if (!Game.CanLocalPlayerActFor(Game.EnergizePromptPlayer))
                return;

            if (ShouldRouteOnlineIntentToHost())
            {
                if (!SendSubmitEnergizePlayToHost(NexusSession.LocalPlayerIndex, (int)id))
                    Debug.LogWarning("[Net] RequestSubmitEnergizePlay — not connected to host.");
                return;
            }

            Game.SubmitEnergizePlay(id);
        }

        public static void RequestSubmitFocusFireUnitType(UnitType type)
        {
            if (Game?.FocusFirePicker == null)
                return;
            if (!Game.CanLocalPlayerActFor(Game.FocusFirePicker))
                return;

            if (ShouldRouteOnlineIntentToHost())
            {
                if (!SendSubmitFocusFireUnitTypeToHost(NexusSession.LocalPlayerIndex, (int)type))
                    Debug.LogWarning("[Net] RequestSubmitFocusFireUnitType — not connected to host.");
                return;
            }

            Game.SubmitFocusFireUnitType(type);
        }

        public static void RequestCancelFocusFireRefund()
        {
            if (Game?.FocusFirePicker == null)
                return;
            if (!Game.CanLocalPlayerActFor(Game.FocusFirePicker))
                return;

            if (ShouldRouteOnlineIntentToHost())
            {
                if (!SendCancelFocusFireRefundToHost(NexusSession.LocalPlayerIndex))
                    Debug.LogWarning("[Net] RequestCancelFocusFireRefund — not connected to host.");
                return;
            }

            Game.CancelFocusFireRefund();
        }

        public static void RequestSubmitCasualtyPick()
        {
            if (Game?.CasualtyPick?.Owner == null)
                return;
            if (!Game.CanLocalPlayerActFor(Game.CasualtyPick.Owner))
                return;

            PackCasualtySelection(Game.CasualtyPick, out var types, out var counts);

            if (ShouldRouteOnlineIntentToHost())
            {
                if (!SendSubmitCasualtyPickToHost(Game.CasualtyPick.Owner.PlayerIndex, types, counts))
                    Debug.LogWarning("[Net] RequestSubmitCasualtyPick — not connected to host.");
                return;
            }

            Game.SubmitCasualtyPick();
        }

        public static void RequestClaimFallbackBattleSecretVp()
        {
            if (Game?.SecretMissionOffer?.Player == null)
                return;
            if (!Game.CanLocalPlayerActFor(Game.SecretMissionOffer.Player))
                return;

            if (ShouldRouteOnlineIntentToHost())
            {
                if (!SendClaimFallbackBattleSecretVpToHost(NexusSession.LocalPlayerIndex))
                    Debug.LogWarning("[Net] RequestClaimFallbackBattleSecretVp — not connected to host.");
                return;
            }

            Game.ClaimFallbackBattleSecretVp();
        }

        public static void RequestPlaySecretMissionAtIndex(int indexInHand)
        {
            if (Game?.SecretMissionOffer?.Player == null)
                return;
            if (!Game.CanLocalPlayerActFor(Game.SecretMissionOffer.Player))
                return;

            if (ShouldRouteOnlineIntentToHost())
            {
                if (!SendPlaySecretMissionAtIndexToHost(NexusSession.LocalPlayerIndex, indexInHand))
                    Debug.LogWarning("[Net] RequestPlaySecretMissionAtIndex — not connected to host.");
                return;
            }

            Game.PlaySecretMissionAtIndex(indexInHand);
        }

        public static void RequestSkipSecretMissionPlay()
        {
            if (Game?.SecretMissionOffer?.Player == null)
                return;
            if (!Game.CanLocalPlayerActFor(Game.SecretMissionOffer.Player))
                return;

            if (ShouldRouteOnlineIntentToHost())
            {
                if (!SendSkipSecretMissionPlayToHost(NexusSession.LocalPlayerIndex))
                    Debug.LogWarning("[Net] RequestSkipSecretMissionPlay — not connected to host.");
                return;
            }

            Game.SkipSecretMissionPlay();
        }

        static void PackCasualtySelection(CasualtyPickState cp, out int[] types, out int[] counts)
        {
            var typeList = new List<int>();
            var countList = new List<int>();
            if (cp?.Selected != null)
            {
                var grouped = new Dictionary<UnitType, int>();
                foreach (var u in cp.Selected)
                {
                    if (u?.Definition == null)
                        continue;
                    var type = u.Definition.Type;
                    grouped.TryGetValue(type, out int existing);
                    grouped[type] = existing + 1;
                }

                foreach (var kvp in grouped)
                {
                    typeList.Add((int)kvp.Key);
                    countList.Add(kvp.Value);
                }
            }

            types = typeList.ToArray();
            counts = countList.ToArray();
        }
    }
}
