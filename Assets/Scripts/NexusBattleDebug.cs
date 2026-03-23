using UnityEngine;

namespace NexusGame
{
    /// <summary>
    /// Toggle extra Unity console output for battle resolution (mobile builds: keep off for shipping).
    /// </summary>
    public static class NexusBattleDebug
    {
#if UNITY_EDITOR
        public static bool VerboseBattleLogs = true;
#else
        public static bool VerboseBattleLogs = false;
#endif

        public static void LogBattle(string line)
        {
            if (!VerboseBattleLogs || string.IsNullOrEmpty(line))
                return;
            Debug.Log("[Battle][verbose] " + line);
        }
    }
}
