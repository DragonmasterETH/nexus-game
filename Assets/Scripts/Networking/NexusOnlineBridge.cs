using System;
using System.Collections;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace NexusGame
{
    /// <summary>NGO bridge: match start, host-authoritative commands, and game-state snapshots.</summary>
    public class NexusOnlineBridge : NetworkBehaviour
    {
        public const string SyncMessageName = "NexusGameSync";

        public static NexusOnlineBridge Instance { get; private set; }

        /// <summary>Client receives <see cref="BeginMatchClientRpc"/> after host starts relay.</summary>
        public static event Action MatchStartRequested;

        byte[] _pendingClientPayload;
        uint _pendingClientVersion;
        Coroutine _clientSyncWatchdog;
        const string IntentMessageName = "NexusGameIntent";

        enum OnlineIntentType : byte
        {
            EndTurn = 1,
            MoveGroup = 2,
            Purchase = 3,
            RequestFullState = 4,
            ConfirmBattleArrangement = 5,
            MoveBattlePlanEntry = 6,
            SetBattleDefender = 7,
            SubmitEnergizePass = 8,
            SubmitEnergizePlay = 9,
            SubmitFocusFireUnitType = 10,
            CancelFocusFireRefund = 11,
            SubmitCasualtyPick = 12,
            ClaimFallbackBattleSecretVp = 13,
            PlaySecretMissionAtIndex = 14,
            SkipSecretMissionPlay = 15,
        }

        static bool _syncHandlerRegistered;
        static bool _intentHandlerRegistered;
        static byte[] _staticPendingPayload;
        static uint _staticPendingVersion;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            NexusGameCommands.Bridge = this;
            EnsureSyncHandlerRegistered();

            if (IsServer)
            {
                if (NetworkManager.Singleton != null)
                    NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            }
            else
            {
                MatchStartRequested?.Invoke();
                FlushPendingSnapshot();
                RequestFullStateFromServer();
                _clientSyncWatchdog = StartCoroutine(ClientSyncWatchdog());
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;

            if (_clientSyncWatchdog != null)
            {
                StopCoroutine(_clientSyncWatchdog);
                _clientSyncWatchdog = null;
            }

            if (_matchStartRetryRoutine != null)
            {
                StopCoroutine(_matchStartRetryRoutine);
                _matchStartRetryRoutine = null;
            }

            if (Instance == this)
                Instance = null;
            if (NexusGameCommands.Bridge == this)
                NexusGameCommands.Bridge = null;
        }

        void Update()
        {
            if (IsServer)
                return;

            if (_pendingClientPayload != null && NexusGameCommands.Game != null)
                FlushPendingSnapshot();
            else if (_staticPendingPayload != null && NexusGameCommands.Game != null)
                FlushStaticPendingSnapshot();
        }

        public static void EnsureSyncHandlerRegistered()
        {
            var mgr = NetworkManager.Singleton?.CustomMessagingManager;
            if (mgr == null)
                return;

            if (!_syncHandlerRegistered)
            {
                mgr.UnregisterNamedMessageHandler(SyncMessageName);
                mgr.RegisterNamedMessageHandler(SyncMessageName, StaticOnSyncMessageReceived);
                _syncHandlerRegistered = true;
            }

            if (!_intentHandlerRegistered)
            {
                mgr.UnregisterNamedMessageHandler(IntentMessageName);
                mgr.RegisterNamedMessageHandler(IntentMessageName, StaticOnIntentMessageReceived);
                _intentHandlerRegistered = true;
            }
        }

        public static void UnregisterSyncHandler()
        {
            var mgr = NetworkManager.Singleton?.CustomMessagingManager;
            mgr?.UnregisterNamedMessageHandler(SyncMessageName);
            mgr?.UnregisterNamedMessageHandler(IntentMessageName);
            _syncHandlerRegistered = false;
            _intentHandlerRegistered = false;
        }

        static void StaticOnIntentMessageReceived(ulong senderClientId, FastBufferReader reader)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer)
                return;

            reader.ReadValueSafe(out byte intentType);
            switch ((OnlineIntentType)intentType)
            {
                case OnlineIntentType.EndTurn:
                    reader.ReadValueSafe(out int endTurnSeat);
                    ProcessEndTurnIntent(endTurnSeat);
                    break;
                case OnlineIntentType.MoveGroup:
                    reader.ReadValueSafe(out int moveSeat);
                    reader.ReadValueSafe(out int fromQ);
                    reader.ReadValueSafe(out int fromR);
                    reader.ReadValueSafe(out int toQ);
                    reader.ReadValueSafe(out int toR);
                    reader.ReadValueSafe(out int typeCount);
                    var moveTypes = new int[typeCount];
                    var moveCounts = new int[typeCount];
                    for (int i = 0; i < typeCount; i++)
                    {
                        reader.ReadValueSafe(out moveTypes[i]);
                        reader.ReadValueSafe(out moveCounts[i]);
                    }

                    ProcessMoveGroupIntent(moveSeat, fromQ, fromR, toQ, toR, moveTypes, moveCounts);
                    break;
                case OnlineIntentType.Purchase:
                    reader.ReadValueSafe(out int purchaseSeat);
                    reader.ReadValueSafe(out int unitType);
                    reader.ReadValueSafe(out int discountUse);
                    reader.ReadValueSafe(out int pay);
                    reader.ReadValueSafe(out int homeQ);
                    reader.ReadValueSafe(out int homeR);
                    ProcessPurchaseIntent(purchaseSeat, unitType, discountUse, pay, homeQ, homeR);
                    break;
                case OnlineIntentType.RequestFullState:
                    Instance?.PushGameStateToClient(senderClientId);
                    break;
                case OnlineIntentType.ConfirmBattleArrangement:
                    reader.ReadValueSafe(out int confirmSeat);
                    ProcessConfirmBattleArrangement(confirmSeat);
                    break;
                case OnlineIntentType.MoveBattlePlanEntry:
                    reader.ReadValueSafe(out int planSeat);
                    reader.ReadValueSafe(out int planIndex);
                    reader.ReadValueSafe(out int planDelta);
                    ProcessMoveBattlePlanEntry(planSeat, planIndex, planDelta);
                    break;
                case OnlineIntentType.SetBattleDefender:
                    reader.ReadValueSafe(out int defSeat);
                    reader.ReadValueSafe(out int defPlanIndex);
                    reader.ReadValueSafe(out int defPlayerIndex);
                    ProcessSetBattleDefender(defSeat, defPlanIndex, defPlayerIndex);
                    break;
                case OnlineIntentType.SubmitEnergizePass:
                    reader.ReadValueSafe(out int energizePassSeat);
                    ProcessSubmitEnergizePass(energizePassSeat);
                    break;
                case OnlineIntentType.SubmitEnergizePlay:
                    reader.ReadValueSafe(out int energizePlaySeat);
                    reader.ReadValueSafe(out int energizeId);
                    ProcessSubmitEnergizePlay(energizePlaySeat, energizeId);
                    break;
                case OnlineIntentType.SubmitFocusFireUnitType:
                    reader.ReadValueSafe(out int focusSeat);
                    reader.ReadValueSafe(out int focusUnitType);
                    ProcessSubmitFocusFireUnitType(focusSeat, focusUnitType);
                    break;
                case OnlineIntentType.CancelFocusFireRefund:
                    reader.ReadValueSafe(out int focusCancelSeat);
                    ProcessCancelFocusFireRefund(focusCancelSeat);
                    break;
                case OnlineIntentType.SubmitCasualtyPick:
                    reader.ReadValueSafe(out int casualtySeat);
                    reader.ReadValueSafe(out int casualtyTypeCount);
                    var casualtyTypes = new int[casualtyTypeCount];
                    var casualtyCounts = new int[casualtyTypeCount];
                    for (int i = 0; i < casualtyTypeCount; i++)
                    {
                        reader.ReadValueSafe(out casualtyTypes[i]);
                        reader.ReadValueSafe(out casualtyCounts[i]);
                    }

                    ProcessSubmitCasualtyPick(casualtySeat, casualtyTypes, casualtyCounts);
                    break;
                case OnlineIntentType.ClaimFallbackBattleSecretVp:
                    reader.ReadValueSafe(out int secretClaimSeat);
                    ProcessClaimFallbackBattleSecretVp(secretClaimSeat);
                    break;
                case OnlineIntentType.PlaySecretMissionAtIndex:
                    reader.ReadValueSafe(out int secretPlaySeat);
                    reader.ReadValueSafe(out int secretIndex);
                    ProcessPlaySecretMissionAtIndex(secretPlaySeat, secretIndex);
                    break;
                case OnlineIntentType.SkipSecretMissionPlay:
                    reader.ReadValueSafe(out int secretSkipSeat);
                    ProcessSkipSecretMissionPlay(secretSkipSeat);
                    break;
            }
        }

        static bool SendIntentToHost(OnlineIntentType intent, System.Action<FastBufferWriter> writePayload)
        {
            var nm = NetworkManager.Singleton;
            var mgr = nm?.CustomMessagingManager;
            if (mgr == null || nm == null || !nm.IsClient)
                return false;

            using var writer = new FastBufferWriter(512, Allocator.Temp);
            writer.WriteValueSafe((byte)intent);
            writePayload?.Invoke(writer);
            mgr.SendNamedMessage(IntentMessageName, NetworkManager.ServerClientId, writer,
                NetworkDelivery.ReliableSequenced);
            return true;
        }

        public static bool SendEndTurnIntent(int seat) =>
            SendIntentToHost(OnlineIntentType.EndTurn, w => w.WriteValueSafe(seat));

        public static bool SendMoveGroupIntent(int seat, int fromQ, int fromR, int toQ, int toR, int[] unitTypes,
            int[] unitCounts)
        {
            unitTypes ??= System.Array.Empty<int>();
            unitCounts ??= System.Array.Empty<int>();
            int n = Mathf.Min(unitTypes.Length, unitCounts.Length);
            return SendIntentToHost(OnlineIntentType.MoveGroup, w =>
            {
                w.WriteValueSafe(seat);
                w.WriteValueSafe(fromQ);
                w.WriteValueSafe(fromR);
                w.WriteValueSafe(toQ);
                w.WriteValueSafe(toR);
                w.WriteValueSafe(n);
                for (int i = 0; i < n; i++)
                {
                    w.WriteValueSafe(unitTypes[i]);
                    w.WriteValueSafe(unitCounts[i]);
                }
            });
        }

        public static bool SendPurchaseIntent(int seat, int unitType, int discountUse, int pay, int homeQ, int homeR) =>
            SendIntentToHost(OnlineIntentType.Purchase, w =>
            {
                w.WriteValueSafe(seat);
                w.WriteValueSafe(unitType);
                w.WriteValueSafe(discountUse);
                w.WriteValueSafe(pay);
                w.WriteValueSafe(homeQ);
                w.WriteValueSafe(homeR);
            });

        public static bool SendRequestFullStateIntent() =>
            SendIntentToHost(OnlineIntentType.RequestFullState, null);

        public static bool SendConfirmBattleArrangementIntent(int seat) =>
            SendIntentToHost(OnlineIntentType.ConfirmBattleArrangement, w => w.WriteValueSafe(seat));

        public static bool SendMoveBattlePlanEntryIntent(int seat, int index, int delta) =>
            SendIntentToHost(OnlineIntentType.MoveBattlePlanEntry, w =>
            {
                w.WriteValueSafe(seat);
                w.WriteValueSafe(index);
                w.WriteValueSafe(delta);
            });

        public static bool SendSetBattleDefenderIntent(int seat, int planIndex, int defenderPlayerIndex) =>
            SendIntentToHost(OnlineIntentType.SetBattleDefender, w =>
            {
                w.WriteValueSafe(seat);
                w.WriteValueSafe(planIndex);
                w.WriteValueSafe(defenderPlayerIndex);
            });

        public static bool SendSubmitEnergizePassIntent(int seat) =>
            SendIntentToHost(OnlineIntentType.SubmitEnergizePass, w => w.WriteValueSafe(seat));

        public static bool SendSubmitEnergizePlayIntent(int seat, int energizeId) =>
            SendIntentToHost(OnlineIntentType.SubmitEnergizePlay, w =>
            {
                w.WriteValueSafe(seat);
                w.WriteValueSafe(energizeId);
            });

        public static bool SendSubmitFocusFireUnitTypeIntent(int seat, int unitType) =>
            SendIntentToHost(OnlineIntentType.SubmitFocusFireUnitType, w =>
            {
                w.WriteValueSafe(seat);
                w.WriteValueSafe(unitType);
            });

        public static bool SendCancelFocusFireRefundIntent(int seat) =>
            SendIntentToHost(OnlineIntentType.CancelFocusFireRefund, w => w.WriteValueSafe(seat));

        public static bool SendSubmitCasualtyPickIntent(int seat, int[] unitTypes, int[] unitCounts)
        {
            unitTypes ??= System.Array.Empty<int>();
            unitCounts ??= System.Array.Empty<int>();
            int n = Mathf.Min(unitTypes.Length, unitCounts.Length);
            return SendIntentToHost(OnlineIntentType.SubmitCasualtyPick, w =>
            {
                w.WriteValueSafe(seat);
                w.WriteValueSafe(n);
                for (int i = 0; i < n; i++)
                {
                    w.WriteValueSafe(unitTypes[i]);
                    w.WriteValueSafe(unitCounts[i]);
                }
            });
        }

        public static bool SendClaimFallbackBattleSecretVpIntent(int seat) =>
            SendIntentToHost(OnlineIntentType.ClaimFallbackBattleSecretVp, w => w.WriteValueSafe(seat));

        public static bool SendPlaySecretMissionAtIndexIntent(int seat, int indexInHand) =>
            SendIntentToHost(OnlineIntentType.PlaySecretMissionAtIndex, w =>
            {
                w.WriteValueSafe(seat);
                w.WriteValueSafe(indexInHand);
            });

        public static bool SendSkipSecretMissionPlayIntent(int seat) =>
            SendIntentToHost(OnlineIntentType.SkipSecretMissionPlay, w => w.WriteValueSafe(seat));

        static void ProcessConfirmBattleArrangement(int requestingSeat)
        {
            var game = NexusGameCommands.Game;
            if (game == null || !game.PendingBattleArrangement || game.CurrentPlayer == null)
                return;
            if (game.CurrentPlayer.PlayerIndex != requestingSeat)
                return;
            game.ConfirmBattleArrangement();
        }

        static void ProcessMoveBattlePlanEntry(int requestingSeat, int index, int delta)
        {
            var game = NexusGameCommands.Game;
            if (game == null || game.CurrentPlayer == null || game.CurrentPlayer.PlayerIndex != requestingSeat)
                return;
            game.MoveBattlePlanEntry(index, delta);
            BroadcastAfterHostAction(game);
        }

        static void ProcessSetBattleDefender(int requestingSeat, int planIndex, int defenderPlayerIndex)
        {
            var game = NexusGameCommands.Game;
            if (game == null || game.CurrentPlayer == null || game.CurrentPlayer.PlayerIndex != requestingSeat)
                return;
            game.SetBattleDefenderForEntry(planIndex, defenderPlayerIndex);
            BroadcastAfterHostAction(game);
        }

        static void ProcessSubmitEnergizePass(int requestingSeat)
        {
            var game = NexusGameCommands.Game;
            if (game?.EnergizePromptPlayer == null || game.EnergizePromptPlayer.PlayerIndex != requestingSeat)
                return;
            game.SubmitEnergizePass();
            BroadcastAfterHostAction(game);
        }

        static void ProcessSubmitEnergizePlay(int requestingSeat, int energizeId)
        {
            var game = NexusGameCommands.Game;
            if (game?.EnergizePromptPlayer == null || game.EnergizePromptPlayer.PlayerIndex != requestingSeat)
                return;
            game.SubmitEnergizePlay((EnergizeBattleId)energizeId);
            BroadcastAfterHostAction(game);
        }

        static void ProcessSubmitFocusFireUnitType(int requestingSeat, int unitType)
        {
            var game = NexusGameCommands.Game;
            if (game?.FocusFirePicker == null || game.FocusFirePicker.PlayerIndex != requestingSeat)
                return;
            game.SubmitFocusFireUnitType((UnitType)unitType);
            BroadcastAfterHostAction(game);
        }

        static void ProcessCancelFocusFireRefund(int requestingSeat)
        {
            var game = NexusGameCommands.Game;
            if (game?.FocusFirePicker == null || game.FocusFirePicker.PlayerIndex != requestingSeat)
                return;
            game.CancelFocusFireRefund();
            BroadcastAfterHostAction(game);
        }

        static void ProcessSubmitCasualtyPick(int requestingSeat, int[] unitTypes, int[] unitCounts)
        {
            var game = NexusGameCommands.Game;
            if (game == null)
                return;
            game.ApplyCasualtySelectionFromTypeCounts(requestingSeat, unitTypes, unitCounts);
            BroadcastAfterHostAction(game);
        }

        static void ProcessClaimFallbackBattleSecretVp(int requestingSeat)
        {
            var game = NexusGameCommands.Game;
            if (game?.SecretMissionOffer?.Player == null ||
                game.SecretMissionOffer.Player.PlayerIndex != requestingSeat)
                return;
            game.ClaimFallbackBattleSecretVp();
            BroadcastAfterHostAction(game);
        }

        static void ProcessPlaySecretMissionAtIndex(int requestingSeat, int indexInHand)
        {
            var game = NexusGameCommands.Game;
            if (game?.SecretMissionOffer?.Player == null ||
                game.SecretMissionOffer.Player.PlayerIndex != requestingSeat)
                return;
            game.PlaySecretMissionAtIndex(indexInHand);
            BroadcastAfterHostAction(game);
        }

        static void ProcessSkipSecretMissionPlay(int requestingSeat)
        {
            var game = NexusGameCommands.Game;
            if (game?.SecretMissionOffer?.Player == null ||
                game.SecretMissionOffer.Player.PlayerIndex != requestingSeat)
                return;
            game.SkipSecretMissionPlay();
            BroadcastAfterHostAction(game);
        }

        static void ProcessEndTurnIntent(int requestingSeat)
        {
            var game = NexusGameCommands.Game;
            if (game == null || game.IsGameOver)
                return;

            var current = game.CurrentPlayer;
            if (current == null || current.PlayerIndex != requestingSeat)
                return;

            game.EndTurn();
        }

        static void ProcessMoveGroupIntent(int requestingSeat, int fromQ, int fromR, int toQ, int toR,
            int[] unitTypes, int[] unitCounts)
        {
            var game = NexusGameCommands.Game;
            if (game == null || game.IsGameOver || unitTypes == null || unitCounts == null)
                return;

            var current = game.CurrentPlayer;
            if (current == null || current.PlayerIndex != requestingSeat)
                return;

            var from = game.Board?.GetTile(fromQ, fromR);
            var to = game.Board?.GetTile(toQ, toR);
            if (from == null || to == null)
                return;

            var selection = new System.Collections.Generic.Dictionary<UnitType, int>();
            var explicitTypes = new System.Collections.Generic.HashSet<UnitType>();
            int n = Mathf.Min(unitTypes.Length, unitCounts.Length);
            for (int i = 0; i < n; i++)
            {
                if (unitCounts[i] <= 0)
                    continue;
                var type = (UnitType)unitTypes[i];
                selection[type] = unitCounts[i];
                explicitTypes.Add(type);
            }

            var input = UnityEngine.Object.FindObjectOfType<MobileInputController>();
            input?.TryExecuteMoveGroup(current, from, to, selection, explicitTypes);
        }

        static void ProcessPurchaseIntent(int requestingSeat, int unitType, int discountUse, int pay, int homeQ,
            int homeR)
        {
            var game = NexusGameCommands.Game;
            if (game == null || game.IsGameOver)
                return;

            var player = game.Players.Find(p => p.PlayerIndex == requestingSeat);
            if (player == null || game.CurrentPlayer != player)
                return;

            var home = game.Board?.GetTile(homeQ, homeR);
            game.TryPurchaseUnitOnHome(player, (UnitType)unitType, discountUse, pay, home);
        }

        static void StaticOnSyncMessageReceived(ulong senderClientId, FastBufferReader reader)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsServer)
                return;

            reader.ReadValueSafe(out uint version);
            reader.ReadValueSafe(out int length);
            if (length <= 0)
                return;

            var payload = new byte[length];
            reader.ReadBytesSafe(ref payload, length);
            ApplySnapshotOnClientStatic(payload, version);
        }

        static void ApplySnapshotOnClientStatic(byte[] payload, uint version)
        {
            if (Instance != null)
            {
                Instance.ApplySnapshotOnClient(payload, version);
                return;
            }

            var game = NexusGameCommands.Game;
            if (game == null)
            {
                _staticPendingPayload = payload;
                _staticPendingVersion = version;
                return;
            }

            game.ApplyOnlineSnapshot(payload, version);
        }

        public static void FlushStaticPendingSnapshot()
        {
            if (_staticPendingPayload == null || _staticPendingPayload.Length == 0)
                return;

            var game = NexusGameCommands.Game;
            if (game == null)
                return;

            game.ApplyOnlineSnapshot(_staticPendingPayload, _staticPendingVersion);
            _staticPendingPayload = null;
            _staticPendingVersion = 0;
        }

        void OnClientConnected(ulong clientId)
        {
            if (NexusConnectionMonitor.Phase == NexusConnectionMonitor.ConnectionPhase.OpponentDisconnected)
                NexusConnectionMonitor.NotifyOpponentReconnected();
            SendMatchStartToClient(clientId);
            PushGameStateToClient(clientId);
        }

        [ClientRpc(Delivery = RpcDelivery.Reliable)]
        void BeginMatchClientRpc(ClientRpcParams clientRpcParams = default)
        {
            if (IsServer)
                return;
            MatchStartRequested?.Invoke();
        }

        void SendMatchStartToClient(ulong clientId)
        {
            var rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
            };
            BeginMatchClientRpc(rpcParams);
        }

        public void NotifyClientsMatchStarting()
        {
            if (!IsServer)
                return;
            BeginMatchClientRpc();
            PushGameStateToClients();
            if (_matchStartRetryRoutine == null)
                _matchStartRetryRoutine = StartCoroutine(RetryMatchStartNotifications());
        }

        Coroutine _matchStartRetryRoutine;

        IEnumerator RetryMatchStartNotifications()
        {
            for (int i = 0; i < 12; i++)
            {
                yield return new WaitForSeconds(i < 4 ? 0.15f : 0.35f);
                if (!IsServer)
                    yield break;

                BeginMatchClientRpc();
                if (NexusGameCommands.Game != null)
                    PushGameStateToClients();
            }

            _matchStartRetryRoutine = null;
        }

        public void PushGameStateToClients()
        {
            if (!IsServer)
                return;
            BroadcastGameState();
        }

        public void RequestFullStateFromServer()
        {
            if (IsServer)
                return;

            if (IsSpawned)
            {
                RequestFullStateSyncServerRpc();
                return;
            }

            SendRequestFullStateIntent();
        }

        public void FlushPendingSnapshot()
        {
            if (_pendingClientPayload == null || _pendingClientPayload.Length == 0)
                return;

            ApplySnapshotOnClient(_pendingClientPayload, _pendingClientVersion);
            _pendingClientPayload = null;
            _pendingClientVersion = 0;
        }

        void BroadcastGameState()
        {
            var game = NexusGameCommands.Game;
            if (game == null || !IsServer)
                return;

            byte[] payload = NexusOnlineGameState.Capture(game);
            if (payload == null || payload.Length == 0)
                return;

            uint version = game.OnlineSyncVersion;
            var nm = NetworkManager.Singleton;
            int clientCount = nm != null ? nm.ConnectedClientsIds.Count : 0;
            if (clientCount == 0)
            {
                Debug.LogWarning($"[Net] Snapshot v{version} ready but no clients connected.");
                return;
            }

            SendSnapshotToAllClients(payload, version);
            Debug.Log($"[Net] Broadcasting snapshot v{version} to {clientCount} client(s).");
        }

        void PushGameStateToClient(ulong clientId)
        {
            var game = NexusGameCommands.Game;
            if (game == null || !IsServer)
                return;

            byte[] payload = NexusOnlineGameState.Capture(game);
            if (payload == null || payload.Length == 0)
                return;

            uint version = game.OnlineSyncVersion;
            SendSnapshotToClient(clientId, payload, version);
        }

        void SendSnapshotToAllClients(byte[] payload, uint version)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !IsServer)
                return;

            foreach (var clientId in nm.ConnectedClientsIds)
                SendSnapshotFast(clientId, payload, version);
        }

        void SendSnapshotToClient(ulong clientId, byte[] payload, uint version)
        {
            SendSnapshotReliable(clientId, payload, version);
        }

        void SendSnapshotFast(ulong clientId, byte[] payload, uint version)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !IsServer || payload == null || payload.Length == 0)
                return;

            SendSnapshotViaCustomMessage(clientId, payload, version, NetworkDelivery.ReliableSequenced);
        }

        void SendSnapshotReliable(ulong clientId, byte[] payload, uint version)
        {
            SendSnapshotFast(clientId, payload, version);
            var rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
            };
            ReceiveGameStateClientRpc(payload, version, rpcParams);
        }

        void SendSnapshotViaCustomMessage(ulong clientId, byte[] payload, uint version, NetworkDelivery delivery)
        {
            var mgr = NetworkManager.Singleton?.CustomMessagingManager;
            if (mgr == null)
                return;

            int size = sizeof(uint) + sizeof(int) + payload.Length;
            using var writer = new FastBufferWriter(size, Allocator.Temp);
            writer.WriteValueSafe(version);
            writer.WriteValueSafe(payload.Length);
            writer.WriteBytesSafe(payload, payload.Length);
            mgr.SendNamedMessage(SyncMessageName, clientId, writer, delivery);
        }

        [ClientRpc(Delivery = RpcDelivery.Reliable)]
        void ReceiveGameStateClientRpc(byte[] payload, uint version, ClientRpcParams clientRpcParams = default)
        {
            if (IsServer)
                return;
            ApplySnapshotOnClient(payload, version);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestEndTurnServerRpc(int requestingSeat, ServerRpcParams rpcParams = default)
        {
            ProcessEndTurnIntent(requestingSeat);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestMoveGroupServerRpc(int requestingSeat, int fromQ, int fromR, int toQ, int toR,
            int[] unitTypes, int[] unitCounts, ServerRpcParams rpcParams = default)
        {
            ProcessMoveGroupIntent(requestingSeat, fromQ, fromR, toQ, toR, unitTypes, unitCounts);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestPurchaseServerRpc(int requestingSeat, int unitType, int discountUse, int pay, int homeQ,
            int homeR, ServerRpcParams rpcParams = default)
        {
            ProcessPurchaseIntent(requestingSeat, unitType, discountUse, pay, homeQ, homeR);
        }

        [ServerRpc(RequireOwnership = false)]
        void RequestFullStateSyncServerRpc(ServerRpcParams rpcParams = default)
        {
            PushGameStateToClient(rpcParams.Receive.SenderClientId);
        }

        internal void ApplySnapshotOnClient(byte[] payload, uint version)
        {
            var game = NexusGameCommands.Game;
            if (game == null)
            {
                _pendingClientPayload = payload;
                _pendingClientVersion = version;
                return;
            }

            if (game.ApplyOnlineSnapshot(payload, version))
            {
                _pendingClientPayload = null;
                _pendingClientVersion = 0;
                _staticPendingPayload = null;
                _staticPendingVersion = 0;
            }
        }

        static void BroadcastAfterHostAction(GameController game)
        {
            game?.AfterOnlineHostMutation();
        }

        IEnumerator ClientSyncWatchdog()
        {
            for (int i = 0; i < 20; i++)
            {
                yield return new WaitForSeconds(0.5f);

                var game = NexusGameCommands.Game;
                if (game != null && game.LastAppliedOnlineSyncVersion > 0)
                    yield break;

                RequestFullStateFromServer();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestConfirmBattleArrangementServerRpc(int requestingSeat, ServerRpcParams rpcParams = default)
        {
            ProcessConfirmBattleArrangement(requestingSeat);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestMoveBattlePlanEntryServerRpc(int requestingSeat, int index, int delta,
            ServerRpcParams rpcParams = default)
        {
            ProcessMoveBattlePlanEntry(requestingSeat, index, delta);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestSetBattleDefenderServerRpc(int requestingSeat, int planIndex, int defenderPlayerIndex,
            ServerRpcParams rpcParams = default)
        {
            ProcessSetBattleDefender(requestingSeat, planIndex, defenderPlayerIndex);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestSubmitEnergizePassServerRpc(int requestingSeat, ServerRpcParams rpcParams = default)
        {
            ProcessSubmitEnergizePass(requestingSeat);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestSubmitEnergizePlayServerRpc(int requestingSeat, int energizeId,
            ServerRpcParams rpcParams = default)
        {
            ProcessSubmitEnergizePlay(requestingSeat, energizeId);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestSubmitFocusFireUnitTypeServerRpc(int requestingSeat, int unitType,
            ServerRpcParams rpcParams = default)
        {
            ProcessSubmitFocusFireUnitType(requestingSeat, unitType);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestCancelFocusFireRefundServerRpc(int requestingSeat, ServerRpcParams rpcParams = default)
        {
            ProcessCancelFocusFireRefund(requestingSeat);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestSubmitCasualtyPickServerRpc(int requestingSeat, int[] unitTypes, int[] unitCounts,
            ServerRpcParams rpcParams = default)
        {
            ProcessSubmitCasualtyPick(requestingSeat, unitTypes, unitCounts);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestClaimFallbackBattleSecretVpServerRpc(int requestingSeat, ServerRpcParams rpcParams = default)
        {
            ProcessClaimFallbackBattleSecretVp(requestingSeat);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestPlaySecretMissionAtIndexServerRpc(int requestingSeat, int indexInHand,
            ServerRpcParams rpcParams = default)
        {
            ProcessPlaySecretMissionAtIndex(requestingSeat, indexInHand);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestSkipSecretMissionPlayServerRpc(int requestingSeat, ServerRpcParams rpcParams = default)
        {
            ProcessSkipSecretMissionPlay(requestingSeat);
        }
    }
}
