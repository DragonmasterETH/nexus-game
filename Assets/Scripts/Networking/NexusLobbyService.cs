using System;
using UnityEngine;

namespace NexusGame
{
    /// <summary>
    /// Pre-UGS lobby flow (room code, find match). Replace internals with Unity Lobby + Relay when dashboard is linked.
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

        public static LobbyPhase Phase { get; private set; } = LobbyPhase.Idle;

        /// <summary>Shareable join code (stub until UGS Lobby).</summary>
        public static string JoinCode { get; private set; } = "";

        public static string StatusMessage { get; private set; } = "";

        public static int PlayersInRoom { get; private set; }

        public static bool IsHost { get; private set; }

        /// <summary>True when enough players are present to start (stub: 2 for 1v1).</summary>
        public static bool IsReadyToStart => Phase == LobbyPhase.InRoom && PlayersInRoom >= 2;

        public static event Action OnLobbyUpdated;

        static void Notify()
        {
            OnLobbyUpdated?.Invoke();
        }

        public static void Leave()
        {
            Phase = LobbyPhase.Idle;
            JoinCode = "";
            StatusMessage = "";
            PlayersInRoom = 0;
            IsHost = false;
            Notify();
        }

        /// <summary>Create a private room; host is this device.</summary>
        public static bool CreateRoom()
        {
            Leave();
            IsHost = true;
            Phase = LobbyPhase.InRoom;
            PlayersInRoom = 1;
            JoinCode = GenerateStubJoinCode();
            StatusMessage = "Waiting for opponent… Share the code below.";
            Debug.Log($"[Lobby] Created room (stub). Code: {JoinCode}");
            Notify();
            return true;
        }

        /// <summary>Join an existing room by code.</summary>
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
            PlayersInRoom = 2;
            JoinCode = trimmed;
            StatusMessage = "Joined room. Waiting for host to start.";
            Debug.Log($"[Lobby] Joined room (stub). Code: {JoinCode}");
            Notify();
            return true;
        }

        /// <summary>Queue for a random opponent (UGS Matchmaker later).</summary>
        public static bool StartFindMatch()
        {
            Leave();
            IsHost = false;
            Phase = LobbyPhase.Searching;
            PlayersInRoom = 1;
            JoinCode = "";
            StatusMessage = "Searching for opponent… (Matchmaker connects tomorrow.)";
            Debug.Log("[Lobby] Find match started (stub).");
            Notify();
            return true;
        }

        /// <summary>Dev stub: pretend a second player joined the room.</summary>
        public static void SimulateOpponentJoined()
        {
            if (Phase != LobbyPhase.InRoom && Phase != LobbyPhase.Searching)
                return;

            if (Phase == LobbyPhase.Searching)
            {
                Phase = LobbyPhase.InRoom;
                IsHost = true;
                JoinCode = GenerateStubJoinCode();
            }

            PlayersInRoom = Mathf.Max(PlayersInRoom, 2);
            StatusMessage = IsHost
                ? "Opponent joined. You can start the match."
                : "Opponent joined. Waiting for host to start.";
            Debug.Log("[Lobby] SimulateOpponentJoined (stub).");
            Notify();
        }

        /// <summary>Dev stub: complete find-match with a fake match.</summary>
        public static void SimulateMatchFound()
        {
            if (Phase != LobbyPhase.Searching)
                return;
            SimulateOpponentJoined();
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
