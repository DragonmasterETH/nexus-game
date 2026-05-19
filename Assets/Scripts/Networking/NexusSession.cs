namespace NexusGame
{
    /// <summary>
    /// Active match configuration (local, vs AI, or online). Set before <see cref="Bootstrap"/> starts the game scene.
    /// </summary>
    public static class NexusSession
    {
        public enum MatchKind
        {
            LocalHotseat,
            VsAi,
            AiVsAi,
            Online
        }

        public static MatchKind Kind { get; private set; } = MatchKind.LocalHotseat;

        /// <summary>Seat controlled on this device (0 = first player). Online only.</summary>
        public static int LocalPlayerIndex { get; private set; }

        /// <summary>Online: room creator runs simulation until NGO host is wired.</summary>
        public static bool IsHost { get; private set; }

        public static string RoomCode { get; private set; } = "";

        public static bool IsOnline => Kind == MatchKind.Online;

        public static void Reset()
        {
            Kind = MatchKind.LocalHotseat;
            LocalPlayerIndex = 0;
            IsHost = false;
            RoomCode = "";
        }

        public static void ConfigureLocalHotseat()
        {
            Kind = MatchKind.LocalHotseat;
            LocalPlayerIndex = 0;
            IsHost = true;
            RoomCode = "";
        }

        public static void ConfigureVsAi()
        {
            Kind = MatchKind.VsAi;
            LocalPlayerIndex = 0;
            IsHost = true;
            RoomCode = "";
        }

        public static void ConfigureAiVsAi()
        {
            Kind = MatchKind.AiVsAi;
            LocalPlayerIndex = 0;
            IsHost = true;
            RoomCode = "";
        }

        public static void ConfigureOnline(int localPlayerIndex, bool isHost, string roomCode)
        {
            Kind = MatchKind.Online;
            LocalPlayerIndex = localPlayerIndex;
            IsHost = isHost;
            RoomCode = roomCode ?? "";
        }
    }
}
