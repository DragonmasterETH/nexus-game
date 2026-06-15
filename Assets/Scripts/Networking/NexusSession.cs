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

        /// <summary>Online: room creator runs simulation authority for end-turn (first RPC wired).</summary>
        public static bool IsHost { get; private set; }

        public static string RoomCode { get; private set; } = "";

        /// <summary>Room capacity the board layout is derived from (2 = 1v1 board, 3-4 = 2-4 board).</summary>
        public static int RoomSize { get; private set; } = 2;

        /// <summary>Seats actually playing this match (humans + bots), 2-4.</summary>
        public static int TotalSeats { get; private set; } = 2;

        /// <summary>Seats 0..HumanSeatCount-1 are humans; HumanSeatCount..TotalSeats-1 are bots.</summary>
        public static int HumanSeatCount { get; private set; } = 2;

        public static int BotSeatCount => TotalSeats > HumanSeatCount ? TotalSeats - HumanSeatCount : 0;

        /// <summary>True if this seat is run by AI (host-side online, or locally vs AI).</summary>
        public static bool IsBotSeat(int seat) => seat >= HumanSeatCount && seat < TotalSeats;

        /// <summary>Bot seats run local/host AI but the UI presents them as human opponents.</summary>
        public static bool StealthBotOpponent { get; private set; }

        public static bool IsOnline => Kind == MatchKind.Online;

        public static void Reset()
        {
            Kind = MatchKind.LocalHotseat;
            LocalPlayerIndex = 0;
            IsHost = false;
            RoomCode = "";
            RoomSize = 2;
            TotalSeats = 2;
            HumanSeatCount = 2;
            StealthBotOpponent = false;
        }

        public static void ConfigureLocalHotseat()
        {
            Kind = MatchKind.LocalHotseat;
            LocalPlayerIndex = 0;
            IsHost = true;
            RoomCode = "";
            RoomSize = 4;
            TotalSeats = 4;
            HumanSeatCount = 4; // all seats human — no bot seats
            StealthBotOpponent = false;
        }

        public static void ConfigureVsAi()
        {
            Kind = MatchKind.VsAi;
            LocalPlayerIndex = 0;
            IsHost = true;
            RoomCode = "";
            RoomSize = 2;
            TotalSeats = 2;
            HumanSeatCount = 1;
            StealthBotOpponent = false;
        }

        public static void ConfigureAiVsAi()
        {
            Kind = MatchKind.AiVsAi;
            LocalPlayerIndex = 0;
            IsHost = true;
            RoomCode = "";
            RoomSize = 2;
            TotalSeats = 2;
            HumanSeatCount = 0;
            StealthBotOpponent = false;
        }

        public static void ConfigureOnline(int localPlayerIndex, bool isHost, string roomCode,
            int roomSize, int totalSeats, int humanSeats, bool stealthBots)
        {
            Kind = MatchKind.Online;
            LocalPlayerIndex = localPlayerIndex;
            IsHost = isHost;
            RoomCode = roomCode ?? "";
            RoomSize = Clamp(roomSize, 2, 4);
            TotalSeats = Clamp(totalSeats, 2, RoomSize);
            HumanSeatCount = Clamp(humanSeats, 1, TotalSeats);
            StealthBotOpponent = stealthBots;
        }

        /// <summary>Local vs AI after matchmaking timeout — opponents appear as real players.</summary>
        public static void ConfigureStealthBotMatch(int totalSeats)
        {
            Kind = MatchKind.VsAi;
            LocalPlayerIndex = 0;
            IsHost = true;
            RoomCode = "";
            RoomSize = Clamp(totalSeats, 2, 4);
            TotalSeats = RoomSize;
            HumanSeatCount = 1;
            StealthBotOpponent = true;
        }

        static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
    }
}
