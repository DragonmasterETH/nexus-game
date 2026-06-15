using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace NexusGame
{
    /// <summary>Lobby + session flow via Unity Multiplayer Services (requires platform sign-in).</summary>
    public static class NexusLobbyService
    {
        public enum LobbyPhase
        {
            Idle,
            InRoom,
            Searching,
            Error
        }

        public const int MaxRoomSize = 4;
        public const float MatchmakingBotTimeoutSeconds = 30f;
        const string SessionName = "Nexus Ops";
        const string BotCountPropertyKey = "nexusBotCount";
        const string MatchSizePropertyKey = "nexusMatchSize";

        public static LobbyPhase Phase { get; private set; } = LobbyPhase.Idle;
        public static string JoinCode { get; private set; } = "";
        public static string StatusMessage { get; private set; } = "";
        public static int PlayersInRoom { get; private set; }
        public static bool IsHost { get; private set; }
        public static bool IsBusy { get; private set; }
        public static bool UseLiveServices { get; private set; }

        /// <summary>Room capacity for the current flow (2 = 1v1, 4 = four-player). Set by create/find, mirrored from the live session.</summary>
        public static int RoomSize { get; private set; } = 2;

        /// <summary>AI seats in the room (host-added in private rooms, or matchmaking backfill).</summary>
        public static int BotCount { get; private set; }

        /// <summary>Occupied seats: humans + bots.</summary>
        public static int SeatedCount => PlayersInRoom + BotCount;

        /// <summary>This device's seat (join order in the live session; host = 0).</summary>
        public static int LocalSeatIndex { get; private set; }

        /// <summary>Matchmaking queue timed out with other humans present — online match with disguised host-run bots.</summary>
        public static bool IsStealthBackfillMatch { get; private set; }

        /// <summary>Matchmaking requires a full room; private rooms can start with any 2+ seats filled.</summary>
        public static bool IsReadyToStart =>
            (Phase == LobbyPhase.InRoom || Phase == LobbyPhase.Searching) &&
            SeatedCount >= (FromMatchmakingQueue ? RoomSize : 2);

        /// <summary>True while waiting in the public matchmaking queue for a second player.</summary>
        public static bool IsInMatchmakingQueue { get; private set; }

        /// <summary>Seconds spent in the current matchmaking queue (for UI).</summary>
        public static float QueueWaitSeconds => IsInMatchmakingQueue ? Time.unscaledTime - _queueStartedAt : 0f;

        /// <summary>True after queue resolves (real opponent or stealth bot).</summary>
        public static bool MatchFound { get; private set; }

        /// <summary>Current lobby flow began via Find Match (queue), not create/join room.</summary>
        public static bool FromMatchmakingQueue { get; private set; }

        /// <summary>Queue timed out — start a disguised local bot opponent.</summary>
        public static bool IsStealthBotMatch { get; private set; }

        /// <summary>Human-readable lobby occupancy for the multiplayer UI.</summary>
        public static string PlayersLine { get; private set; } = "Players: 0/2";

        public static event Action OnLobbyUpdated;

        /// <summary>Raised when multiplayer is blocked until Game Center / Play Games sign-in succeeds.</summary>
        public static event Action OnSignInRequired;

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
        static bool _botFallbackTriggered;
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

            if (IsInMatchmakingQueue && QueueWaitSeconds >= MatchmakingBotTimeoutSeconds)
            {
                if (UseLiveServices && _activeSession != null && PlayersInRoom > 1)
                {
                    // Other humans found but the room never filled: the host quietly tops the
                    // lobby up with bots. Non-hosts keep polling — the host's timer starts the match.
                    if (IsHost)
                    {
                        TriggerStealthBackfill();
                        return;
                    }
                }
                else
                {
                    TriggerStealthBotMatch();
                    return;
                }
            }

            if (IsInMatchmakingQueue && (!UseLiveServices || _activeSession == null))
            {
                int waitSec = Mathf.FloorToInt(QueueWaitSeconds);
                StatusMessage = $"Searching for opponent… ({waitSec}s)";
                PlayersLine = "Searching…";
                Notify();
                return;
            }

            if (!UseLiveServices || _activeSession == null)
                return;

            int before = PlayersInRoom;
            ApplySessionSnapshot(_activeSession);
            if (PlayersInRoom != before)
                Debug.Log($"[Lobby] Presence poll: {PlayersInRoom}/{RoomSize} players.");
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
            BotCount = 0;
            LocalSeatIndex = 0;
            PlayersLine = $"Players: 0/{RoomSize}";
            IsHost = false;
            IsBusy = false;
            UseLiveServices = false;
            IsInMatchmakingQueue = false;
            IsClientMatchNetworkStarted = false;
            _autoStartScheduled = false;
            _botFallbackTriggered = false;
            MatchFound = false;
            FromMatchmakingQueue = false;
            IsStealthBotMatch = false;
            IsStealthBackfillMatch = false;
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

        public static void CreateRoom(int roomSize = 2)
        {
            if (!EnsureAuthorizedForAction())
                return;

            Leave();
            RoomSize = Mathf.Clamp(roomSize, 2, MaxRoomSize);
            IsInMatchmakingQueue = false;
            IsHost = true;
            Phase = LobbyPhase.InRoom;
            PlayersInRoom = 1;
            LocalSeatIndex = 0;
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

            if (!EnsureAuthorizedForAction())
                return false;

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

        public static bool StartFindMatch(int matchSize = 2)
        {
            if (!EnsureAuthorizedForAction())
                return false;

            Leave();
            RoomSize = Mathf.Clamp(matchSize, 2, MaxRoomSize);
            IsInMatchmakingQueue = true;
            FromMatchmakingQueue = true;
            MatchFound = false;
            IsStealthBotMatch = false;
            IsStealthBackfillMatch = false;
            _autoStartScheduled = false;
            _botFallbackTriggered = false;
            _queueStartedAt = Time.unscaledTime;
            IsHost = false;
            Phase = LobbyPhase.Searching;
            PlayersInRoom = 1;
            PlayersLine = "Searching…";
            IsBusy = true;
            JoinCode = "";
            StatusMessage = "Searching for opponent…";
            Notify();

            NexusUgsRunner.EnsureExists();
            NexusUgsRunner.Instance.Run(FindMatchAsync);
            return true;
        }

        /// <summary>Leave the matchmaking queue (same as <see cref="Leave"/>).</summary>
        public static void CancelMatchmaking() => Leave();

        static bool EnsureAuthorizedForAction()
        {
            if (NexusUgsAuth.IsMultiplayerAuthorized)
                return true;

            FailAuthRequired(FormatAuthFailureMessage());
            return false;
        }

        /// <summary>Start interactive platform sign-in. Shows the error modal only if sign-in fails.</summary>
        public static void RequestSignInForMultiplayer()
        {
            EnsureSignedIn(interactive: true, force: true);
        }

        /// <summary>Abort an in-flight sign-in attempt (e.g. player closed the modal).</summary>
        public static void CancelSignInAttempt()
        {
            IsBusy = false;
            Notify();
        }

        static void FailAuthRequired(string message)
        {
            Phase = LobbyPhase.Error;
            StatusMessage = message;
            UseLiveServices = false;
            IsBusy = false;
            OnSignInRequired?.Invoke();
            Notify();
        }

        /// <summary>Host can fill empty private-room seats with AI opponents.</summary>
        public static bool CanAddBot =>
            IsHost && !FromMatchmakingQueue && Phase == LobbyPhase.InRoom &&
            SeatedCount < RoomSize && !IsBusy;

        public static void AddBot()
        {
            if (!CanAddBot)
                return;

            BotCount++;
            AfterBotCountChanged();
        }

        public static void RemoveBot()
        {
            if (!IsHost || FromMatchmakingQueue || BotCount <= 0)
                return;

            BotCount--;
            AfterBotCountChanged();
        }

        static void AfterBotCountChanged()
        {
            RefreshPrivateRoomPresentation();
            Notify();
            PushBotCountToSession();
        }

        static string BotLineSuffix()
        {
            if (BotCount <= 0 || FromMatchmakingQueue)
                return "";
            return BotCount == 1 ? " (1 bot)" : $" ({BotCount} bots)";
        }

        static void RefreshPrivateRoomPresentation()
        {
            bool full = SeatedCount >= RoomSize;
            PlayersLine = $"Players: {SeatedCount}/{RoomSize}{BotLineSuffix()}" +
                          (full ? " — room full" : IsHost ? " — waiting for join" : "");

            if (IsHost)
                StatusMessage = SeatedCount >= 2
                    ? full
                        ? "Room full. You can start the match."
                        : "You can start the match, or wait for more players."
                    : "Waiting for opponent… Share the code below.";
            else
                StatusMessage = "Joined room. Waiting for host to start.";
        }

        /// <summary>
        /// Host: mirror bot seats into the live session so clients see them. Private rooms stay
        /// unlocked — humans joining displace bots (see the clamp in <see cref="ApplySessionSnapshot"/>).
        /// </summary>
        static void PushBotCountToSession()
        {
            if (!UseLiveServices || _activeSession == null || !_activeSession.IsHost)
                return;

            var session = _activeSession;
            int bots = BotCount;
            NexusUgsRunner.Instance?.Run(async () =>
            {
                try
                {
                    var host = session.AsHost();
                    host.SetProperty(BotCountPropertyKey, new SessionProperty(bots.ToString()));
                    await host.SavePropertiesAsync();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Lobby] Bot count sync failed: {ex.Message}");
                }
            });
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
                FailLiveSession(FormatAuthFailureMessage(), authFailure: true);
                return;
            }

            try
            {
                NexusNetworkSetup.EnsureNetworkManager();
                var options = new SessionOptions
                {
                    Name = SessionName,
                    MaxPlayers = RoomSize,
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
                FailLiveSession(msg);
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
                FailLiveSession(FormatAuthFailureMessage(), authFailure: true);
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
                FailLiveSession(FormatAuthFailureMessage(), authFailure: true);
                IsBusy = false;
                Notify();
                return;
            }

            try
            {
                NexusNetworkSetup.EnsureNetworkManager();
                // Indexed "match size" property keeps the 1v1 and 4-player queues separate pools.
                // (FilterField.MaxPlayers is rejected by the lobby backend — the SDK never maps it.)
                var quickJoin = new QuickJoinOptions { CreateSession = true };
                quickJoin.Filters.Add(new FilterOption(
                    FilterField.StringIndex1, RoomSize.ToString(), FilterOperation.Equal));
                var sessionOptions = new SessionOptions
                {
                    Name = SessionName,
                    MaxPlayers = RoomSize,
                    IsPrivate = false,
                    IsLocked = false,
                    SessionProperties = new Dictionary<string, SessionProperty>
                    {
                        [MatchSizePropertyKey] = new SessionProperty(RoomSize.ToString(),
                            VisibilityPropertyOptions.Public, PropertyIndex.String1)
                    }
                };

                StatusMessage = "Searching for opponent…";
                Notify();

                var session = await MultiplayerService.Instance.MatchmakeSessionAsync(quickJoin, sessionOptions);
                if (_botFallbackTriggered)
                    return;

                AttachSession(session);
                ApplySessionSnapshot(session);
            }
            catch (Exception ex)
            {
                if (_botFallbackTriggered)
                    return;

                UseLiveServices = false;
                if (IsInMatchmakingQueue)
                {
                    // Don't dead-end the queue on a service error — keep "searching" so the
                    // 30s stealth-bot fallback in TickPresence still gives the player a match.
                    Debug.LogWarning($"[Lobby] Matchmake failed ({ex.Message}) — staying in queue for bot fallback.");
                    Phase = LobbyPhase.Searching;
                    StatusMessage = "Searching for opponent…";
                }
                else
                {
                    Phase = LobbyPhase.Error;
                    StatusMessage = FriendlyError(ex);
                }
            }
            finally
            {
                if (!_botFallbackTriggered)
                    IsBusy = false;
                Notify();
            }
        }

        static async Task<bool> TryBeginLiveSessionAsync()
        {
            if (!await NexusUgsAuth.EnsureReadyAsync())
            {
                UseLiveServices = false;
                FailAuthRequired(FormatAuthFailureMessage());
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

            return $"Sign in with {NexusPlatformSignIn.RequiredPlatformLabel} to play online.";
        }

        /// <summary>Call when opening multiplayer UI — signs in before create/join/queue.</summary>
        public static void EnsureSignedIn(bool interactive = false, bool force = false)
        {
            if (NexusUgsAuth.IsMultiplayerAuthorized)
                return;

            if (IsBusy && !force)
                return;

            IsBusy = true;
            StatusMessage = interactive
                ? $"Opening {NexusPlatformSignIn.RequiredPlatformLabel} sign-in…"
                : "Signing in…";
            if (Phase == LobbyPhase.Error)
                Phase = LobbyPhase.Idle;
            Notify();
            NexusUgsRunner.EnsureExists();
            NexusUgsRunner.Instance.Run(async () =>
            {
                try
                {
                    if (await NexusUgsAuth.TrySignInAsync(interactive))
                    {
                        UseLiveServices = true;
                        StatusMessage = NexusUgsAuth.MultiplayerStatusLine();
                        Phase = LobbyPhase.Idle;
                    }
                    else
                    {
                        UseLiveServices = false;
                        FailAuthRequired(FormatAuthFailureMessage());
                    }
                }
                finally
                {
                    IsBusy = false;
                    Notify();
                }
            });
        }

        static void FailLiveSession(string message, bool authFailure = false)
        {
            Phase = LobbyPhase.Error;
            StatusMessage = message;
            UseLiveServices = false;
            if (authFailure)
                OnSignInRequired?.Invoke();
        }

        static void AttachSession(ISession session)
        {
            DetachSessionEvents(_activeSession);
            _activeSession = session;
            if (session == null)
                return;

            session.Changed += OnSessionChanged;
            session.SessionPropertiesChanged += OnSessionChanged;
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
            session.SessionPropertiesChanged -= OnSessionChanged;
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
            if (session == null || _botFallbackTriggered || IsStealthBotMatch)
                return;

            UseLiveServices = true;
            JoinCode = session.Code ?? JoinCode;
            RoomSize = Mathf.Clamp(session.MaxPlayers, 2, MaxRoomSize);
            int count = Mathf.Max(1, session.PlayerCount);
            PlayersInRoom = Mathf.Clamp(count, 1, RoomSize);
            IsHost = session.IsHost;
            LocalSeatIndex = ComputeLocalSeatIndex(session);

            // Host: humans joining a private room displace bots (UGS only counts humans toward MaxPlayers).
            if (session.IsHost && BotCount > 0 && SeatedCount > RoomSize)
            {
                BotCount = Mathf.Max(0, RoomSize - PlayersInRoom);
                PushBotCountToSession();
            }

            // Non-hosts mirror the host's bot seats from the session properties.
            if (!session.IsHost &&
                session.Properties != null &&
                session.Properties.TryGetValue(BotCountPropertyKey, out var botProp) &&
                int.TryParse(botProp?.Value, out int bots))
            {
                BotCount = Mathf.Clamp(bots, 0, RoomSize - PlayersInRoom);
            }

            if (IsInMatchmakingQueue && PlayersInRoom < RoomSize)
            {
                Phase = LobbyPhase.Searching;
                PlayersLine = "Searching…";
                int waitSec = Mathf.FloorToInt(QueueWaitSeconds);
                StatusMessage = RoomSize > 2
                    ? $"Searching for players… {PlayersInRoom}/{RoomSize} ({waitSec}s)"
                    : $"Searching for opponent… ({waitSec}s)";
                return;
            }

            if (IsInMatchmakingQueue && PlayersInRoom >= RoomSize)
            {
                Phase = LobbyPhase.InRoom;
                MatchFound = true;
                JoinCode = "";
                PlayersLine = "Match found";
                StatusMessage = "Match found!";
                TryAutoStartMatchmaking();
                return;
            }

            // A matchmaking client whose host backfilled with bots: present it as a found match.
            if (FromMatchmakingQueue)
            {
                Phase = LobbyPhase.InRoom;
                PlayersLine = MatchFound ? "Match found" : PlayersLine;
                return;
            }

            Phase = LobbyPhase.InRoom;
            RefreshPrivateRoomPresentation();
        }

        static int ComputeLocalSeatIndex(ISession session)
        {
            try
            {
                string selfId = session.CurrentPlayer?.Id;
                var players = session.Players;
                if (selfId == null || players == null)
                    return session.IsHost ? 0 : 1;

                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i] != null && players[i].Id == selfId)
                        return i;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Lobby] Seat lookup failed: {ex.Message}");
            }

            return session.IsHost ? 0 : 1;
        }

        static void TryAutoStartMatchmaking()
        {
            if (!IsInMatchmakingQueue || _autoStartScheduled || !IsReadyToStart)
                return;

            _autoStartScheduled = true;
            IsInMatchmakingQueue = false;
            Phase = LobbyPhase.InRoom;

            MatchFound = true;
            JoinCode = "";
            PlayersLine = "Match found";

            if (!IsHost)
            {
                StatusMessage = "Match found!";
                Notify();
                return;
            }

            StatusMessage = "Match found!";
            Notify();
            NexusUgsRunner.RunOnMainThread(() => OnMatchmakingMatchReady?.Invoke());
        }

        static void TriggerStealthBotMatch()
        {
            if (_botFallbackTriggered || !IsInMatchmakingQueue)
                return;

            _botFallbackTriggered = true;
            IsStealthBotMatch = true;
            MatchFound = true;
            IsInMatchmakingQueue = false;
            IsHost = true;
            Phase = LobbyPhase.InRoom;
            PlayersInRoom = 1;
            BotCount = RoomSize - 1;
            PlayersLine = "Match found";
            StatusMessage = "Match found!";
            JoinCode = "";
            IsBusy = false;
            UseLiveServices = false;
            _autoStartScheduled = true;
            DetachFromLiveSessionQuietly();
            Debug.Log($"[Lobby] Matchmaking queue timeout — starting disguised local match vs {BotCount} bot(s).");
            Notify();
            NexusUgsRunner.RunOnMainThread(() => OnMatchmakingMatchReady?.Invoke());
        }

        /// <summary>
        /// Host: queue timed out with 2-3 humans in a 4-player room. Keep the live session,
        /// fill the empty seats with host-run bots, and start — clients see a normal match.
        /// </summary>
        static void TriggerStealthBackfill()
        {
            if (_botFallbackTriggered || !IsInMatchmakingQueue || _activeSession == null || !IsHost)
                return;

            _botFallbackTriggered = true;
            IsStealthBackfillMatch = true;
            MatchFound = true;
            IsInMatchmakingQueue = false;
            Phase = LobbyPhase.InRoom;
            BotCount = Mathf.Max(1, RoomSize - PlayersInRoom);
            PlayersLine = "Match found";
            StatusMessage = "Match found!";
            JoinCode = "";
            IsBusy = false;
            _autoStartScheduled = true;
            Debug.Log($"[Lobby] Queue timeout with {PlayersInRoom} players — backfilling {BotCount} disguised bot seat(s).");
            Notify();

            // Publish bot seats + lock before relay start so late session updates can't admit more humans.
            var session = _activeSession;
            int bots = BotCount;
            var runner = NexusUgsRunner.Instance;
            if (runner == null)
            {
                OnMatchmakingMatchReady?.Invoke();
                return;
            }

            runner.Run(async () =>
            {
                try
                {
                    var host = session.AsHost();
                    host.SetProperty(BotCountPropertyKey, new SessionProperty(bots.ToString()));
                    host.IsLocked = true;
                    await host.SavePropertiesAsync();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Lobby] Backfill session sync failed: {ex.Message}");
                }

                NexusUgsRunner.RunOnMainThread(() => OnMatchmakingMatchReady?.Invoke());
            });
        }

        static void DetachFromLiveSessionQuietly()
        {
            var session = _activeSession;
            _activeSession = null;
            DetachSessionEvents(session);
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
                    Debug.LogWarning($"[Lobby] LeaveAsync after bot fallback: {ex.Message}");
                }
            });
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
    }
}
