using System;
using Unity.Netcode;
using UnityEngine;

namespace NexusGame
{
    /// <summary>First NGO bridge: match start signal + host-authoritative end turn.</summary>
    public class NexusOnlineBridge : NetworkBehaviour
    {
        public static NexusOnlineBridge Instance { get; private set; }

        /// <summary>Client receives <see cref="BeginMatchClientRpc"/> after host starts relay.</summary>
        public static event Action MatchStartRequested;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                Instance = this;
            NexusGameCommands.Bridge = this;
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this)
                Instance = null;
            if (NexusGameCommands.Bridge == this)
                NexusGameCommands.Bridge = null;
        }

        [ClientRpc]
        void BeginMatchClientRpc()
        {
            if (IsServer)
                return;
            MatchStartRequested?.Invoke();
        }

        public void NotifyClientsMatchStarting()
        {
            if (!IsServer)
                return;
            BeginMatchClientRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestEndTurnServerRpc(int requestingSeat, ServerRpcParams rpcParams = default)
        {
            var game = NexusGameCommands.Game;
            if (game == null || game.IsGameOver)
                return;

            var current = game.CurrentPlayer;
            if (current == null || current.PlayerIndex != requestingSeat)
                return;

            game.EndTurn();
        }
    }
}
