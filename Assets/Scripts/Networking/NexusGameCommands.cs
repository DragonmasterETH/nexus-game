using UnityEngine;

namespace NexusGame
{
    /// <summary>
    /// Player intents for host-authoritative play. Local paths call <see cref="GameController"/> directly;
    /// online clients will send RPCs here once NGO + Relay are wired.
    /// </summary>
    public static class NexusGameCommands
    {
        public static GameController Game { get; set; }

        public static void RequestEndTurn()
        {
            if (Game == null || Game.IsGameOver)
                return;
            if (!Game.CanLocalPlayerActNow())
                return;

            if (NexusSession.IsOnline && !NexusSession.IsHost)
            {
                Debug.Log("[Net] RequestEndTurn — will send to host when Relay/NGO is connected.");
                return;
            }

            Game.EndTurn();
        }
    }
}
