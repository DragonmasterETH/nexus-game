using System;
using System.Threading;
using System.Threading.Tasks;
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
        const string SessionName = "Nexus Ops";

        public static LobbyPhase Phase { get; private set; } = LobbyPhase.Idle;
        public static string JoinCode { get; private set; } = "";
        public static string StatusMessage { get; private set; } = "";
        public static int PlayersInRoom { get; private set; }
        public static bool IsHost { get; private set; }
        public static bool IsBusy { get; private set; }
        public static bool UseLiveServices { get; private set; }
        public static bool IsReadyToStart => Phase == LobbyPhase.InRoom && PlayersInRoom >= MaxPlayers;

        public static event Action OnLobbyUpdated;

        static ISession _activeSession;

        static void Notify()
        {
            OnLobbyUpdated?.Invoke();
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
            IsHost = false;
            IsBusy = false;
            UseLiveServices = false;
            Notify();

            NexusNetworkSetup.ShutdownIfListening();

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
            IsHost = false;
            Phase = LobbyPhase.Searching;
            PlayersInRoom = 1;
            IsBusy = true;
            JoinCode = "";
            StatusMessage = "Searching for opponent…";
            Notify();

            NexusUgsRunner.EnsureExists();
            NexusUgsRunner.Instance.Run(FindMatchAsync);
            return true;
        }

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
            StatusMessage = IsHost
                ? "Opponent joined. You can start the match."
                : "Opponent joined. Waiting for host to start.";
            Debug.Log("[Lobby] SimulateOpponentJoined (stub).");
            Notify();
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
                    var bridge = NexusNetworkSetup.SpawnOnlineBridge();
                    bridge?.NotifyClientsMatchStarting();

                    IsBusy = false;
                    StatusMessage = "Connected.";
                    Notify();
                    onSuccess?.Invoke();
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

        static async Task CreateRoomAsync()
        {
            if (!await TryBeginLiveSessionAsync())
            {
                CreateRoomStub();
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
                Debug.LogWarning($"[Lobby] CreateSession failed, using stub: {ex.Message}");
                CreateRoomStub();
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
                JoinRoomStub(code);
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
                StatusMessage = "Searching for opponent… (offline stub — simulate match found.)";
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

                var session = await MultiplayerService.Instance.MatchmakeSessionAsync(quickJoin, sessionOptions);
                AttachSession(session);
                ApplySessionSnapshot(session);
                Phase = LobbyPhase.InRoom;
                StatusMessage = session.IsHost
                    ? "Match found. You can start the match."
                    : "Match found. Waiting for host to start.";
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
                return false;
            }

            UseLiveServices = true;
            return true;
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
        }

        static void OnSessionChanged()
        {
            ApplySessionSnapshot(_activeSession);
            Notify();
        }

        static void OnSessionPlayerChanged(string playerId)
        {
            Debug.Log($"[Lobby] Player changed: {playerId}");
            ApplySessionSnapshot(_activeSession);
            Notify();
        }

        static void OnSessionDeleted()
        {
            StatusMessage = "Room closed.";
            Leave();
        }

        static void OnRemovedFromSession()
        {
            StatusMessage = "Removed from room.";
            Leave();
        }

        static void ApplySessionSnapshot(ISession session)
        {
            if (session == null)
                return;

            UseLiveServices = true;
            JoinCode = session.Code ?? JoinCode;
            PlayersInRoom = Mathf.Clamp(session.PlayerCount, 1, MaxPlayers);
            IsHost = session.IsHost;
            Phase = LobbyPhase.InRoom;

            if (PlayersInRoom >= MaxPlayers && IsHost)
                StatusMessage = "Opponent joined. You can start the match.";
            else if (PlayersInRoom >= MaxPlayers)
                StatusMessage = "Opponent joined. Waiting for host to start.";
            else if (IsHost)
                StatusMessage = "Waiting for opponent… Share the code below.";
            else
                StatusMessage = "Joined room. Waiting for host to start.";
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
            await WaitForConnectedClientsAsync(MaxPlayers, TimeSpan.FromSeconds(45));
        }

        static async Task WaitForConnectedClientsAsync(int expectedPlayers, TimeSpan timeout)
        {
            var nm = NexusNetworkSetup.EnsureNetworkManager();
            var cts = new CancellationTokenSource(timeout);
            int requiredClientConnections = Mathf.Max(0, expectedPlayers - 1);

            while (!cts.IsCancellationRequested)
            {
                if (requiredClientConnections == 0 || nm.ConnectedClientsIds.Count >= requiredClientConnections)
                    return;
                await Task.Delay(250, cts.Token);
            }

            throw new TimeoutException("Timed out waiting for the opponent to connect.");
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
            JoinCode = GenerateStubJoinCode();
            StatusMessage = "Waiting for opponent… Share the code below. (Offline stub — wire Game Center / Play Games + UGS for live rooms.)";
            Debug.Log($"[Lobby] Created room (stub). Code: {JoinCode}");
        }

        static void JoinRoomStub(string code)
        {
            UseLiveServices = false;
            IsHost = false;
            Phase = LobbyPhase.InRoom;
            PlayersInRoom = MaxPlayers;
            JoinCode = code;
            StatusMessage = "Joined room. Waiting for host to start. (Offline stub.)";
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
