using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace NexusGame
{
    /// <summary>Creates the NGO NetworkManager the Multiplayer Services SDK expects.</summary>
    public static class NexusNetworkSetup
    {
        static GameObject _bridgePrefab;
        static bool _bridgePrefabRegistered;
        static bool _connectionCallbacksRegistered;

        public static NetworkManager EnsureNetworkManager()
        {
            NetworkManager networkManager;
            if (NetworkManager.Singleton != null)
            {
                networkManager = NetworkManager.Singleton;
            }
            else
            {
                var go = new GameObject("NetworkManager");
                Object.DontDestroyOnLoad(go);

                var transport = go.AddComponent<UnityTransport>();
                networkManager = go.AddComponent<NetworkManager>();
                networkManager.NetworkConfig = new NetworkConfig
                {
                    NetworkTransport = transport,
                    EnableSceneManagement = false,
                    ConnectionApproval = false
                };
            }

            EnsureBridgePrefabRegistered(networkManager);
            RegisterConnectionCallbacks();
            NexusOnlineBridge.EnsureSyncHandlerRegistered();
            return networkManager;
        }

        public static void RegisterConnectionCallbacks()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || _connectionCallbacksRegistered)
                return;

            nm.OnClientDisconnectCallback += NexusConnectionMonitor.HandleClientDisconnect;
            nm.OnClientStopped += NexusConnectionMonitor.HandleClientStopped;
            nm.OnServerStopped += NexusConnectionMonitor.HandleServerStopped;
            nm.OnTransportFailure += NexusConnectionMonitor.HandleTransportFailure;
            nm.OnClientStarted += OnNetworkLocalClientStarted;
            _connectionCallbacksRegistered = true;
        }

        static void OnNetworkLocalClientStarted()
        {
            NexusOnlineBridge.EnsureSyncHandlerRegistered();
        }

        static void EnsureBridgePrefabRegistered(NetworkManager networkManager)
        {
            if (_bridgePrefabRegistered || networkManager == null)
                return;

            _bridgePrefab = new GameObject("NexusOnlineBridgeNetPrefab");
            _bridgePrefab.SetActive(false);
            Object.DontDestroyOnLoad(_bridgePrefab);
            _bridgePrefab.AddComponent<NetworkObject>();
            _bridgePrefab.AddComponent<NexusOnlineBridge>();

            networkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = _bridgePrefab });
            _bridgePrefabRegistered = true;
        }

        public static NexusOnlineBridge SpawnOnlineBridge()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
                return null;

            if (NexusOnlineBridge.Instance != null)
                return NexusOnlineBridge.Instance;

            EnsureBridgePrefabRegistered(NetworkManager.Singleton);

            var go = Object.Instantiate(_bridgePrefab);
            go.SetActive(true);
            var bridge = go.GetComponent<NexusOnlineBridge>();
            go.GetComponent<NetworkObject>().Spawn(true);
            return bridge;
        }

        public static void ShutdownIfListening()
        {
            if (NetworkManager.Singleton == null)
                return;

            NexusOnlineBridge.UnregisterSyncHandler();

            if (NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();
        }
    }
}
