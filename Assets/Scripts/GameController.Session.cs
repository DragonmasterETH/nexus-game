using UnityEngine;

namespace NexusGame
{
    public partial class GameController
    {
        /// <summary>
        /// Wipe units and player state, then run the same setup as <see cref="Start"/>.
        /// Call from Bootstrap after configuring <see cref="VsAiMode"/> and regenerating the board.
        /// </summary>
        public void ResetAndStartNewMatch()
        {
            if (Board == null)
                Board = FindObjectOfType<BoardGenerator>();
            if (Config == null && Board != null)
                Config = Board.Config;
            if (Board == null || Config == null)
            {
                Debug.LogError("ResetAndStartNewMatch: missing Board or Config.");
                return;
            }

            HardResetFlowState();
            ResetAiTestMatch();

            foreach (var u in FindObjectsOfType<UnitInstance>())
            {
                if (u != null)
                    Destroy(u.gameObject);
            }

            Players.Clear();
            InitPlayers();
            InitCardDecks();
            BeginTurn();
            SpawnStartingUnits();
        }
    }
}
