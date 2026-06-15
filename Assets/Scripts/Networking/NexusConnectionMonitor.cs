using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace NexusGame
{
    /// <summary>Tracks in-match connectivity, auto-reconnects to the same room, and drives disconnect UI.</summary>
    public static class NexusConnectionMonitor
    {
        public enum ConnectionPhase
        {
            Connected,
            Reconnecting,
            OpponentDisconnected,
            Disconnected,
            RoomClosed
        }

        const int MaxReconnectAttempts = 6;
        const float ReconnectDelaySeconds = 2f;

        public static ConnectionPhase Phase { get; private set; } = ConnectionPhase.Connected;
        public static string StatusMessage { get; private set; } = "";
        public static int ReconnectAttempt { get; private set; }

        public static bool IsMonitoringMatch { get; private set; }
        public static bool CanPlay => !IsMonitoringMatch || Phase == ConnectionPhase.Connected;

        public static event Action OnConnectionStateChanged;

        static string _savedRoomCode = "";
        static bool _savedIsHost;
        static bool _reconnectLoopRunning;

        public static void BeginMonitoringMatch()
        {
            if (!NexusSession.IsOnline)
                return;

            IsMonitoringMatch = true;
            _savedRoomCode = NexusSession.RoomCode;
            _savedIsHost = NexusSession.IsHost;
            Phase = ConnectionPhase.Connected;
            StatusMessage = "";
            ReconnectAttempt = 0;
            NexusNetworkSetup.RegisterConnectionCallbacks();
            NotifyStateChanged();
        }

        public static void StopMonitoring()
        {
            IsMonitoringMatch = false;
            Phase = ConnectionPhase.Connected;
            StatusMessage = "";
            ReconnectAttempt = 0;
            _reconnectLoopRunning = false;
            NotifyStateChanged();
        }

        public static void NotifyOpponentDisconnected()
        {
            if (!IsMonitoringMatch || !NexusSession.IsOnline)
                return;
            if (Phase == ConnectionPhase.Disconnected || Phase == ConnectionPhase.RoomClosed)
                return;

            Phase = ConnectionPhase.OpponentDisconnected;
            StatusMessage = "Opponent disconnected. Waiting for them to reconnect…";
            ReconnectAttempt = 0;
            Debug.Log("[Net] Opponent disconnected.");
            NotifyStateChanged();
        }

        public static void NotifyLocalConnectionLost(string reason)
        {
            if (!IsMonitoringMatch || !NexusSession.IsOnline)
                return;
            if (Phase == ConnectionPhase.RoomClosed)
                return;

            BeginReconnect(string.IsNullOrEmpty(reason) ? "Connection lost." : reason);
        }

        public static void NotifyRoomClosed()
        {
            if (!IsMonitoringMatch)
                return;

            Phase = ConnectionPhase.RoomClosed;
            StatusMessage = "The room was closed.";
            ReconnectAttempt = 0;
            Debug.Log("[Net] Room closed.");
            NotifyStateChanged();
        }

        public static void NotifyOpponentReconnected()
        {
            if (!IsMonitoringMatch)
                return;
            if (Phase != ConnectionPhase.OpponentDisconnected)
                return;

            Phase = ConnectionPhase.Connected;
            StatusMessage = "";
            ReconnectAttempt = 0;
            Debug.Log("[Net] Opponent reconnected.");
            NotifyStateChanged();
        }

        public static void NotifyReconnected()
        {
            if (!IsMonitoringMatch)
                return;

            Phase = ConnectionPhase.Connected;
            StatusMessage = "";
            ReconnectAttempt = 0;
            Debug.Log("[Net] Reconnected to match.");
            NotifyStateChanged();
            NexusGameCommands.Bridge?.RequestFullStateFromServer();
            NexusGameCommands.Bridge?.FlushPendingSnapshot();
        }

        public static void ManualRetryReconnect()
        {
            if (!IsMonitoringMatch || !NexusSession.IsOnline)
                return;
            if (Phase == ConnectionPhase.RoomClosed)
                return;

            BeginReconnect("Retrying connection…");
        }

        static void BeginReconnect(string message)
        {
            if (_reconnectLoopRunning && Phase == ConnectionPhase.Reconnecting)
                return;

            Phase = ConnectionPhase.Reconnecting;
            StatusMessage = message;
            ReconnectAttempt = 0;
            NotifyStateChanged();
            ScheduleReconnectLoop();
        }

        static void ScheduleReconnectLoop()
        {
            if (_reconnectLoopRunning)
                return;

            NexusUgsRunner.EnsureExists();
            NexusUgsRunner.Instance.Run(ReconnectLoopAsync);
        }

        static async Task ReconnectLoopAsync()
        {
            if (_reconnectLoopRunning)
                return;

            _reconnectLoopRunning = true;
            try
            {
                if (!NexusLobbyService.UseLiveServices)
                {
                    Phase = ConnectionPhase.Disconnected;
                    StatusMessage = "Connection lost. Live services are required to reconnect.";
                    NotifyStateChanged();
                    return;
                }

                string roomCode = string.IsNullOrEmpty(_savedRoomCode) ? NexusSession.RoomCode : _savedRoomCode;
                for (int attempt = 1; attempt <= MaxReconnectAttempts; attempt++)
                {
                    ReconnectAttempt = attempt;
                    StatusMessage = $"Reconnecting to room… ({attempt}/{MaxReconnectAttempts})";
                    NotifyStateChanged();

                    bool ok = await NexusLobbyService.TryReconnectMatchAsync(roomCode, _savedIsHost);
                    if (ok)
                    {
                        NotifyReconnected();
                        return;
                    }

                    if (attempt < MaxReconnectAttempts)
                        await Task.Delay(TimeSpan.FromSeconds(ReconnectDelaySeconds));
                }

                Phase = ConnectionPhase.Disconnected;
                StatusMessage = "Could not reconnect to the room.";
                NotifyStateChanged();
            }
            finally
            {
                _reconnectLoopRunning = false;
            }
        }

        internal static void HandleClientDisconnect(ulong clientId)
        {
            if (!IsMonitoringMatch || !NexusSession.IsOnline)
                return;

            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer)
                return;

            Debug.Log($"[Net] Client {clientId} disconnected.");
            NotifyOpponentDisconnected();
        }

        internal static void HandleClientStopped(bool _)
        {
            if (!IsMonitoringMatch || !NexusSession.IsOnline)
                return;

            var nm = NetworkManager.Singleton;
            if (nm == null || nm.IsServer)
                return;

            Debug.Log("[Net] Client stopped.");
            NotifyLocalConnectionLost("Lost connection to host.");
        }

        internal static void HandleServerStopped(bool _)
        {
            if (!IsMonitoringMatch || !NexusSession.IsOnline)
                return;

            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer)
                return;

            Debug.Log("[Net] Server stopped.");
            NotifyLocalConnectionLost("Host connection lost. Reconnecting…");
        }

        internal static void HandleTransportFailure()
        {
            if (!IsMonitoringMatch || !NexusSession.IsOnline)
                return;

            Debug.LogWarning("[Net] Transport failure.");
            NotifyLocalConnectionLost("Network error. Reconnecting…");
        }

        internal static void HandleSessionPlayerCountChanged(int playersInRoom)
        {
            if (!IsMonitoringMatch || !NexusSession.IsOnline)
                return;

            // Bots don't occupy session slots — only compare against the humans this match expects.
            if (playersInRoom < NexusSession.HumanSeatCount &&
                Phase == ConnectionPhase.Connected)
            {
                NotifyOpponentDisconnected();
            }
        }

        internal static void HandleSessionNetworkStateChanged(NetworkState state)
        {
            if (!IsMonitoringMatch || !NexusSession.IsOnline)
                return;

            if (state == NetworkState.Started)
            {
                if (Phase == ConnectionPhase.Reconnecting)
                    NotifyReconnected();
                return;
            }

            if (state == NetworkState.Starting)
                return;

            if (Phase == ConnectionPhase.OpponentDisconnected)
                return;

            NotifyLocalConnectionLost("Match network disconnected.");
        }

        static void NotifyStateChanged()
        {
            NexusUgsRunner.RunOnMainThread(() => OnConnectionStateChanged?.Invoke());
        }
    }
}
