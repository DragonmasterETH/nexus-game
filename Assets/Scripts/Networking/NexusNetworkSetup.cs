using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace NexusGame
{
    /// <summary>Creates the NGO NetworkManager the Multiplayer Services SDK expects.</summary>
    public static class NexusNetworkSetup
    {
        public static NetworkManager EnsureNetworkManager()
        {
            if (NetworkManager.Singleton != null)
                return NetworkManager.Singleton;

            var go = new GameObject("NetworkManager");
            Object.DontDestroyOnLoad(go);

            var transport = go.AddComponent<UnityTransport>();
            var networkManager = go.AddComponent<NetworkManager>();
            networkManager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                EnableSceneManagement = false,
                ConnectionApproval = false
            };

            return networkManager;
        }

        public static NexusOnlineBridge SpawnOnlineBridge()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
                return null;

            if (NexusOnlineBridge.Instance != null)
                return NexusOnlineBridge.Instance;

            var go = new GameObject("NexusOnlineBridge");
            go.AddComponent<NetworkObject>();
            var bridge = go.AddComponent<NexusOnlineBridge>();
            go.GetComponent<NetworkObject>().Spawn(true);
            return bridge;
        }

        public static void ShutdownIfListening()
        {
            if (NetworkManager.Singleton == null)
                return;

            if (NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();
        }
    }
}
