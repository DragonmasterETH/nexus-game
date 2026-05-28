using UnityEngine;

namespace NexusGame
{
    /// <summary>
    /// Player intents for host-authoritative play. Local paths call <see cref="GameController"/> directly;
    /// online clients send RPCs through <see cref="NexusOnlineBridge"/>.
    /// </summary>
    public static class NexusGameCommands
    {
        public static GameController Game { get; set; }
        public static NexusOnlineBridge Bridge { get; set; }

        public static void RequestEndTurn()
        {
            if (Game == null || Game.IsGameOver)
                return;
            if (!Game.CanLocalPlayerActNow())
                return;

            if (NexusSession.IsOnline && !NexusSession.IsHost)
            {
                if (Bridge != null && Bridge.IsSpawned)
                {
                    Bridge.RequestEndTurnServerRpc(NexusSession.LocalPlayerIndex);
                    return;
                }

                Debug.LogWarning("[Net] RequestEndTurn — online bridge not connected yet.");
                return;
            }

            Game.EndTurn();
        }
    }
}
