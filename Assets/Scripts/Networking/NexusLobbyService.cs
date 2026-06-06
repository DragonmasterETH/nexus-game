using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace NexusGame
{
    /// <summary>
    /// Lobby + session flow. Uses Unity Multiplayer Services when signed in; falls back to local stubs otherwise.
    /// </summary>
    public static class NexusLobbyService
    {
        public enum LobbyPhase
        {
            Idle,
            InRoom,
            Searching,
            Error
        }

        const int MaxPlayers = 2;
        public const int MaxPlayersForMatch = MaxPlayers;
        const string SessionName = "Nexus Ops";

        public static LobbyPhase Phase { get; private set; } = LobbyPhase.Idle;
        public static string JoinCode { get; private set; } = "";
        public static string StatusMessage { get; private set; } = "";
        public static int PlayersInRoom { get; private set; }
        public static bool IsHost { get; private set; }
        public static bool IsBusy { get; private set; }
        public static bool UseLiveServices { get; private set; }
        public static bool IsReadyToStart =>
            (Phase == LobbyPhase.InRoom || Phase == LobbyPhase.Searching) && PlayersInRoom >= MaxPlayers;

        /// <summary>True while waiting in the public matchmaking queue for a second player.</summary>
        public static bool IsInMatchmakingQueue { get; private set; }

        /// <summary>Seconds spent in the current matchmaking queue (for UI).</summary>
        public static float QueueWaitSeconds => IsInMatchmakingQueue ? Time.unscaledTime - _queueStartedAt : 0f;

        /// <summary>Human-readable lobby occupancy for the multiplayer UI.</summary>
        public static string PlayersLine { get; private set; } = "Players: 0/2";

        public static event Action OnLobbyUpdated;

        /// <summary>Host: queue filled — Bootstrap should start the online match.</summary>
        public static event Action OnMatchmakingMatchReady;

        /// <summary>Client: host started Relay; safe to enter the match (also signaled via NGO RPC).</summary>
        public static event Action OnClientRelayReady;

        /// <summary>Client: UGS relay network is up (host clicked Start Match).</summary>
        public static bool IsClientMatchNetworkStarted { get; private set; }

        static ISession _activeSession;
        static float _presencePollTimer;
        static float _queueStartedAt;
        static bool _autoStartScheduled;
        const float PresencePollInterval = 0.35f;

        static void Notify()
        {
            NexusUgsRunner.RunOnMainThread(() => OnLobbyUpdated?.Invoke());
        }

        /// <summary>Refresh player count while waiting in a live room (UGS events + poll).</summary>
        public static void TickPresence()
        {
            if (Phase != LobbyPhase.InRoom && Phase != LobbyPhase.Searching)
                return;

            _presencePollTimer -= Time.unscaledDeltaTime;
            if (_presencePollTimer > 0f)
                return;

            _presencePollTimer = PresencePollInterval;

            if (IsInMatchmakingQueue && (!UseLiveServices || _activeSession == null))
            {
                int waitSec = Mathf.FloorToInt(QueueWaitSeconds);
                StatusMessage = UseLiveServices || !AllowStubFallback
                    ? $"In matchmaking queue… ({waitSec}s)\nWaiting for another player."
                    : $"In matchmaking queue… ({waitSec}s)\n(Editor stub — use Simulate match found.)";
                PlayersLine = "Queue: 1/2 — waiting for opponent";
                Notify();
                return;
            }

            if (!UseLiveServices || _activeSession == null)
                return;

            int before = PlayersInRoom;
            ApplySessionSnapshot(_activeSession);
            if (PlayersInRoom != before)
                Debug.Log($"[Lobby] Presence poll: {PlayersInRoom}/{MaxPlayers} players.");
            Notify();
        }

        public static void ReportAsyncFailure(Exception ex)
        {
            Phase = LobbyPhase.Error;
            StatusMessage = FriendlyError(ex);
            IsBusy = false;
            Notify();
        }

        public static void Leave()
        {
            var session = _activeSession;
            _activeSession = null;
            DetachSessionEvents(session);

            Phase = LobbyPhase.Idle;
            JoinCode = "";
            StatusMessage = "";
            PlayersInRoom = 0;
            PlayersLine = "Players: 0/2";
            IsHost = false;
            IsBusy = false;
            UseLiveServices = false;
            IsInMatchmakingQueue = false;
            IsClientMatchNetworkStarted = false;
            _autoStartScheduled = false;
            _presencePollTimer = 0f;
            Notify();

            NexusNetworkSetup.ShutdownIfListening();
            NexusConnectionMonitor.StopMonitoring();

            if (session == null)
                return;

            NexusUgsRunner.Instance?.Run(async () =>
            {
                try
                {
                    await session.LeaveAsync();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Lobby] LeaveAsync failed: {ex.Message}");
                }
            });
        }

        public static void CreateRoom()
        {
            Leave();
            IsInMatchmakingQueue = false;
            IsHost = true;
            Phase = LobbyPhase.InRoom;
            PlayersInRoom = 1;
            IsBusy = true;
            StatusMessage = "Creating room…";
            Notify();

            NexusUgsRunner.EnsureExists();
            NexusUgsRunner.Instance.Run(CreateRoomAsync);
        }

        public static bool JoinRoom(string code)
        {
            string trimmed = (code ?? "").Trim().ToUpperInvariant();
            if (trimmed.Length < 4)
            {
                Phase = LobbyPhase.Error;
                StatusMessage = "Enter a valid room code.";
                Notify();
                return false;
            }

            Leave();
            IsInMatchmakingQueue = false;
            IsHost = false;
            Phase = LobbyPhase.InRoom;
            PlayersInRoom = 1;
            IsBusy = true;
            JoinCode = trimmed;
            StatusMessage = "Joining room…";
            Notify();

            NexusUgsRunner.EnsureExists();
            NexusUgsRunner.Instance.Run(() => JoinRoomAsync(trimmed));
            return true;
        }

        public static bool StartFindMatch()
        {
            Leave();
            IsInMatchmakingQueue = true;
            _autoStartScheduled = false;
            _queueStartedAt = Time.unscaledTime;
            IsHost = false;
            Phase = LobbyPhase.Searching;
            PlayersInRoom = 1;
            PlayersLine = "Queue: 1/2 — waiting for opponent";
            IsBusy = true;
            JoinCode = "";
            StatusMessage = "Joining matchmaking queue…";
            Notify();

            NexusUgsRunner.EnsureExists();
            NexusUgsRunner.Instance.Run(FindMatchAsync);
            return true;
        }

        /// <summary>Leave the matchmaking queue (same as <see cref="Leave"/>).</summary>
        public static void CancelMatchmaking() => Leave();

        public static void SimulateOpponentJoined()
        {
            if (UseLiveServices || (Phase != LobbyPhase.InRoom && Phase != LobbyPhase.Searching))
                return;

            if (Phase == LobbyPhase.Searching)
            {
                Phase = LobbyPhase.InRoom;
                IsHost = true;
                JoinCode = GenerateStubJoinCode();
            }

            PlayersInRoom = Mathf.Max(PlayersInRoom, MaxPlayers);
            PlayersLine = $"Players: {PlayersInRoom}/{MaxPlayers} — room full (stub)";
            StatusMessage = IsHost
                ? "Opponent joined. You can start the match."
                : "Opponent joined. Waiting for host to start.";
            Debug.Log("[Lobby] SimulateOpponentJoined (stub).");
            Notify();
            TryAutoStartMatchmaking();
        }

        public static void SimulateMatchFound()
        {
            if (UseLiveServices || Phase != LobbyPhase.Searching)
                return;
            SimulateOpponentJoined();
        }

        /// <summary>Host starts Relay + NGO, then runs <paramref name="onSuccess"/>.</summary>
        public static void BeginMatchConnection(Action onSuccess, Action<string> onFailure)
        {
            if (!UseLiveServices || _activeSession == null)
            {
                onSuccess?.Invoke();
                return;
            }

            if (!_activeSession.IsHost)
            {
                onFailure?.Invoke("Only the host can start the match.");
                return;
            }

            NexusUgsRunner.Instance.Run(async () =>
            {
                try
                {
                    StatusMessage = "Starting online connection…";
                    IsBusy = true;
                    Notify();

                    await EnsureRelayNetworkStartedAsync(_activeSession);

                    NexusUgsRunner.RunOnMainThread(() =>
                    {
                        IsBusy = false;
                        StatusMessage = "Connected.";
                        Notify();

                        onSuccess?.Invoke();

                        var bridge = NexusNetworkSetup.SpawnOnlineBridge();
                        bridge?.NotifyClientsMatchStarting();
                    });
                }
                catch (Exception ex)
                {
                    IsBusy = false;
                    var msg = FriendlyError(ex);
                    StatusMessage = msg;
                    Phase = LobbyPhase.Error;
                    Notify();
                    onFailure?.Invoke(msg);
                }
            });
        }

        /// <summary>Rejoin the same UGS room and restart Relay/NGO after a disconnect.</summary>
        public static async Task<bool> TryReconnectMatchAsync(string roomCode, bool wasHost)
        {
            if (string.IsNullOrWhiteSpace(roomCode))
                return false;

            if (!await TryBeginLiveSessionAsync())
                return false;

            try
            {
                NexusNetworkSetup.ShutdownIfListening();
                roomCode = roomCode.Trim().ToUpperInvariant();

                ISession session = _activeSession;
                if (session == null || !string.Equals(session.Code, roomCode, StringComparison.OrdinalIgnoreCase))
                {
                    NexusNetworkSetup.EnsureNetworkManager();
                    session = await MultiplayerService.Instance.JoinSessionByCodeAsync(roomCode);
                    AttachSession(session);
                    ApplySessionSnapshot(session);
                }

                if (session == null)
                    return false;

                if (wasHost)
                {
                    if (!session.IsHost)
                        return false;

                    await EnsureRelayNetworkStartedAsync(session);
                    var bridge = NexusOnlineBridge.Instance ?? NexusNetworkSetup.SpawnOnlineBridge();
                    bridge?.NotifyClientsMatchStarting();
                    NexusGameCommands.Game?.NotifyOnlineStateChanged();
                }
                else
                {
                    await WaitForNetworkStateAsync(session, NetworkState.Started, TimeSpan.FromSeconds(30));
                }

                var nm = NetworkManager.Singleton;
                return nm != null && nm.IsListening;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Lobby] Reconnect failed: {ex.Message}");
                return false;
            }
        }

        static async Task CreateRoomAsync()
        {
            if (!await TryBeginLiveSessionAsync())
            {
                FailLiveOrStub(CreateRoomStub, FormatAuthFailureMessage());
                return;
            }

            try
            {
                NexusNetworkSetup.EnsureNetworkManager();
                var options = new SessionOptions
                {
                    Name = SessionName,
                    MaxPlayers = MaxPlayers,
                    IsPrivate = true,
                    IsLocked = false
                };

                var session = await MultiplayerService.Instance.CreateSessionAsync(options);
                AttachSession(session);
                ApplySessionSnapshot(session);
                StatusMessage = "Waiting for opponent… Share the code below.";
            }
            catch (Exception ex)
            {
                var msg = FriendlyError(ex);
                Debug.LogWarning($"[Lobby] CreateSession failed: {ex.Message}");
                FailLiveOrStub(CreateRoomStub, msg);
            }
            finally
            {
                IsBusy = false;
                Notify();
            }
        }

        static async Task JoinRoomAsync(string code)
        {
            if (!await TryBeginLiveSessionAsync())
            {
                FailLiveOrStub(() => JoinRoomStub(code), FormatAuthFailureMessage());
                return;
            }

            try
            {
                NexusNetworkSetup.EnsureNetworkManager();
                var session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
                AttachSession(session);
                ApplySessionSnapshot(session);
                StatusMessage = "Joined room. Waiting for host to start.";
            }
            catch (Exception ex)
            {
                Phase = LobbyPhase.Error;
                StatusMessage = FriendlyError(ex);
                UseLiveServices = false;
            }
            finally
            {
                IsBusy = false;
                Notify();
            }
        }

        static async Task FindMatchAsync()
        {
            if (!await TryBeginLiveSessionAsync())
            {
                FailLiveOrStub(
                    () => StatusMessage = "Searching for opponent… (Editor stub — simulate match found.)",
                    FormatAuthFailureMessage());
                IsBusy = false;
                Notify();
                return;
            }

            try
            {
                NexusNetworkSetup.EnsureNetworkManager();
                var quickJoin = new QuickJoinOptions { CreateSession = true };
                var sessionOptions = new SessionOptions
                {
                    Name = SessionName,
                    MaxPlayers = MaxPlayers,
                    IsPrivate = false,
                    IsLocked = false
                };

                StatusMessage = "In queue… waiting for another player.";
                Notify();

                var session = await MultiplayerService.Instance.MatchmakeSessionAsync(quickJoin, sessionOptions);
                AttachSession(session);
                ApplySessionSnapshot(session);
            }
            catch (Exception ex)
            {
                Phase = LobbyPhase.Error;
                StatusMessage = FriendlyError(ex);
                UseLiveServices = false;
            }
            finally
            {
                IsBusy = false;
                Notify();
            }
        }

        static async Task<bool> TryBeginLiveSessionAsync()
        {
            if (!await NexusUgsAuth.EnsureReadyAsync())
            {
                UseLiveServices = false;
                Phase = LobbyPhase.Error;
                StatusMessage = FormatAuthFailureMessage();
                Debug.LogWarning($"[Lobby] Live services unavailable: {StatusMessage}");
                return false;
            }

            UseLiveServices = true;
            return true;
        }

        static string FormatAuthFailureMessage()
        {
            if (!string.IsNullOrEmpty(NexusUgsAuth.LastError))
                return NexusUgsAuth.LastError;

            return "Sign-in failed. Enable Anonymous + Google Play Games in the Unity Dashboard (Authentication).";
        }

        /// <summary>Call when opening multiplayer UI — signs in before create/join/queue.</summary>
        public static void EnsureSignedIn()
        {
            if (NexusUgsAuth.IsReady || IsBusy)
                return;

            IsBusy = true;
            StatusMessage = "Signing in…";
            Notify();
            NexusUgsRunner.EnsureExists();
            NexusUgsRunner.Instance.Run(async () =>
            {
                try
                {
                    if (await NexusUgsAuth.TrySignInAsync())
                    {
                        UseLiveServices = true;
                        StatusMessage = NexusUgsAuth.MultiplayerStatusLine();
                        Phase = LobbyPhase.Idle;
                    }
                    else
                    {
                        UseLiveServices = false;
                        Phase = LobbyPhase.Error;
                        StatusMessage = FormatAuthFailureMessage();
                    }
                }
                finally
                {
                    IsBusy = false;
                    Notify();
                }
            });
        }

#if UNITY_EDITOR
        static bool AllowStubFallback => true;
#else
        static bool AllowStubFallback => false;
#endif

        static void FailLiveOrStub(Action stubAction, string liveFailureContext)
        {
            if (AllowStubFallback)
            {
                Debug.LogWarning($"[Lobby] {liveFailureContext} — using Editor stub.");
                stubAction();
                return;
            }

            Phase = LobbyPhase.Error;
            StatusMessage = liveFailureContext;
        }

        static void AttachSession(ISession session)
        {
            DetachSessionEvents(_activeSession);
            _activeSession = session;
            if (session == null)
                return;

            session.Changed += OnSessionChanged;
            session.PlayerJoined += OnSessionPlayerChanged;
            session.PlayerHasLeft += OnSessionPlayerChanged;
            session.Deleted += OnSessionDeleted;
            session.RemovedFromSession += OnRemovedFromSession;
            session.Network.StateChanged += OnSessionNetworkStateChanged;
        }

        static void DetachSessionEvents(ISession session)
        {
            if (session == null)
                return;

            session.Changed -= OnSessionChanged;
            session.PlayerJoined -= OnSessionPlayerChanged;
            session.PlayerHasLeft -= OnSessionPlayerChanged;
            session.Deleted -= OnSessionDeleted;
            session.RemovedFromSession -= OnRemovedFromSession;
            session.Network.StateChanged -= OnSessionNetworkStateChanged;
        }

        static void OnSessionChanged()
        {
            ApplySessionSnapshot(_activeSession);
            Notify();
        }

        static void OnSessionPlayerChanged(string playerId)
        {
            int before = PlayersInRoom;
            Debug.Log($"[Lobby] Player event: {playerId} (count={_activeSession?.PlayerCount ?? 0})");
            ApplySessionSnapshot(_activeSession);
            if (PlayersInRoom != before)
                NexusConnectionMonitor.HandleSessionPlayerCountChanged(PlayersInRoom);
            Notify();
        }

        static void OnSessionNetworkStateChanged(NetworkState state)
        {
            Debug.Log($"[Lobby] Network state: {state} (host={_activeSession?.IsHost})");
            ApplySessionSnapshot(_activeSession);
            NexusConnectionMonitor.HandleSessionNetworkStateChanged(state);

            if (_activeSession != null && !_activeSession.IsHost)
            {
                IsClientMatchNetworkStarted = state == NetworkState.Starting ||
                                              state == NetworkState.Started;
                if (IsClientMatchNetworkStarted)
                    StatusMessage = "Host started — joining match…";
            }

            Notify();

            if (_activeSession == null || _activeSession.IsHost)
                return;

            if (state != NetworkState.Starting && state != NetworkState.Started)
                return;

            NexusUgsRunner.RunOnMainThread(() => OnClientRelayReady?.Invoke());
        }

        static void OnSessionDeleted()
        {
            if (NexusConnectionMonitor.IsMonitoringMatch)
            {
                NexusConnectionMonitor.NotifyRoomClosed();
                return;
            }

            StatusMessage = "Room closed.";
            Leave();
        }

        static void OnRemovedFromSession()
        {
            if (NexusConnectionMonitor.IsMonitoringMatch)
            {
                NexusConnectionMonitor.NotifyLocalConnectionLost("Removed from the room.");
                return;
            }

            StatusMessage = "Removed from room.";
            Leave();
        }

        static void ApplySessionSnapshot(ISession session)
        {
            if (session == null)
                return;

            UseLiveServices = true;
            JoinCode = session.Code ?? JoinCode;
            int count = Mathf.Max(1, session.PlayerCount);
            PlayersInRoom = Mathf.Clamp(count, 1, MaxPlayers);
            IsHost = session.IsHost;

            if (IsInMatchmakingQueue && PlayersInRoom < MaxPlayers)
            {
                Phase = LobbyPhase.Searching;
                PlayersLine = $"Queue: {PlayersInRoom}/{MaxPlayers} — waiting for opponent";
                int waitSec = Mathf.FloorToInt(QueueWaitSeconds);
                StatusMessage = $"In matchmaking queue… ({waitSec}s)\nWaiting for another player to join.";
                return;
            }

            if (IsInMatchmakingQueue && PlayersInRoom >= MaxPlayers)
            {
                Phase = LobbyPhase.InRoom;
                PlayersLine = $"Players: {PlayersInRoom}/{MaxPlayers} — match found!";
                StatusMessage = IsHost
                    ? "Opponent found! Starting match…"
                    : "Opponent found! Host is starting the match…";
                TryAutoStartMatchmaking();
                return;
            }

            Phase = LobbyPhase.InRoom;
            PlayersLine = $"Players: {PlayersInRoom}/{MaxPlayers}" +
                          (PlayersInRoom >= MaxPlayers ? " — room full" : IsHost ? " — waiting for join" : "");

            if (PlayersInRoom >= MaxPlayers && IsHost)
                StatusMessage = "Opponent joined. You can start the match.";
            else if (PlayersInRoom >= MaxPlayers)
                StatusMessage = "Opponent joined. Waiting for host to start.";
            else if (IsHost)
                StatusMessage = "Waiting for opponent… Share the code below.";
            else
                StatusMessage = "Joined room. Waiting for host to start.";
        }

        static void TryAutoStartMatchmaking()
        {
            if (!IsInMatchmakingQueue || _autoStartScheduled || !IsReadyToStart)
                return;

            _autoStartScheduled = true;
            IsInMatchmakingQueue = false;
            Phase = LobbyPhase.InRoom;

            if (!IsHost)
            {
                StatusMessage = "Match found! Waiting for host to start…";
                Notify();
                return;
            }

            StatusMessage = "Match found! Starting game…";
            Notify();
            NexusUgsRunner.RunOnMainThread(() => OnMatchmakingMatchReady?.Invoke());
        }

        static async Task EnsureRelayNetworkStartedAsync(ISession session)
        {
            if (session == null)
                throw new InvalidOperationException("No active session.");

            if (session.Network.State == NetworkState.Started)
                return;

            if (!session.IsHost)
                throw new InvalidOperationException("Relay start must run on the host.");

            var relayOptions = new RelayNetworkOptions(RelayProtocol.DTLS, null, true);
            await session.AsHost().Network.StartRelayNetworkAsync(relayOptions);
            await WaitForNetworkStateAsync(session, NetworkState.Started, TimeSpan.FromSeconds(45));
        }

        static async Task WaitForNetworkStateAsync(ISession session, NetworkState desired, TimeSpan timeout)
        {
            if (session.Network.State == desired)
                return;

            var cts = new CancellationTokenSource(timeout);
            var tcs = new TaskCompletionSource<bool>();

            void Handler(NetworkState state)
            {
                if (state != desired)
                    return;
                tcs.TrySetResult(true);
            }

            session.Network.StateChanged += Handler;
            try
            {
                if (session.Network.State == desired)
                    return;

                using (cts.Token.Register(() => tcs.TrySetCanceled()))
                    await tcs.Task;
            }
            finally
            {
                session.Network.StateChanged -= Handler;
            }
        }

        static void CreateRoomStub()
        {
            UseLiveServices = false;
            IsHost = true;
            Phase = LobbyPhase.InRoom;
            PlayersInRoom = 1;
            PlayersLine = $"Players: {PlayersInRoom}/{MaxPlayers} (stub)";
            JoinCode = GenerateStubJoinCode();
            StatusMessage =
                "Waiting for opponent… Share the code. (Offline stub — both devices need Play Games + UGS for live sync.)";
            Debug.Log($"[Lobby] Created room (stub). Code: {JoinCode}");
        }

        static void JoinRoomStub(string code)
        {
            UseLiveServices = false;
            IsHost = false;
            Phase = LobbyPhase.InRoom;
            PlayersInRoom = 1;
            PlayersLine = $"Players: {PlayersInRoom}/{MaxPlayers} (stub — other device cannot see you)";
            JoinCode = code;
            StatusMessage =
                "Joined stub room. Use live UGS + sign-in on both devices to sync. Host: Simulate opponent (dev).";
            Debug.Log($"[Lobby] Joined room (stub). Code: {JoinCode}");
        }

        static string FriendlyError(Exception ex)
        {
            if (ex is AggregateException agg && agg.InnerException != null)
                ex = agg.InnerException;

            if (ex is SessionException sessionEx)
                return SessionErrorMessage(sessionEx.Error, sessionEx.Message);

            return string.IsNullOrEmpty(ex.Message) ? "Online service error." : ex.Message;
        }

        static string SessionErrorMessage(SessionError error, string fallback)
        {
            return error switch
            {
                SessionError.RateLimitExceeded => "Too many requests — wait a moment and try again.",
                SessionError.SessionNotFound => "Room not found. Check the code and try again.",
                SessionError.Forbidden => "Not allowed — you may not be the host.",
                SessionError.NotAuthorized => $"Sign-in failed. Enable {NexusPlatformSignIn.PlatformLabel} in the UGS dashboard.",
                SessionError.SessionDeleted => "This room was closed.",
                SessionError.NetworkManagerStartFailed => "Network failed to start. Retry or rebuild.",
                SessionError.NetworkSetupFailed => "Relay setup failed. Check Relay is enabled in the dashboard.",
                _ => string.IsNullOrEmpty(fallback) ? "Online service error." : fallback
            };
        }

        static string GenerateStubJoinCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var rng = new System.Random(Environment.TickCount);
            char[] buf = new char[6];
            for (int i = 0; i < buf.Length; i++)
                buf[i] = chars[rng.Next(chars.Length)];
            return new string(buf);
        }
    }
}
