using UnityEngine;

namespace NexusGame
{
    /// <summary>
    /// Win conditions and end-game snapshot (stats overlay uses <see cref="FinalSnapshot"/>).
    /// </summary>
    public partial class GameController
    {
        [Header("End game")]
        [Tooltip("First player to reach this many Victory Points wins.")]
        [Min(1)]
        public int VictoryPointsToWin = 10;

        [Tooltip("0 = no turn limit. Otherwise, after this many completed player-turns (each End Turn advances), " +
                 "the game ends and highest VP wins (ties: lower player number wins).")]
        [Min(0)]
        public int MaxPlayerTurnsBeforeTiebreak = 0;

        int _completedPlayerTurns;

        public bool IsGameOver { get; private set; }

        /// <summary>Null if tiebreak edge case without single winner (should not happen).</summary>
        public PlayerState Winner { get; private set; }

        public string EndGameReason { get; private set; }

        public GameEndSnapshot FinalSnapshot { get; private set; }

        /// <summary>Call after any Victory Points change.</summary>
        public bool CheckGameEndAfterVpChange()
        {
            if (IsGameOver || Players == null || Players.Count == 0)
                return IsGameOver;

            foreach (var p in Players)
            {
                if (p != null && p.VictoryPoints >= VictoryPointsToWin)
                {
                    EndGame(p, $"Reached {VictoryPointsToWin} Victory Points.");
                    return true;
                }
            }

            return false;
        }

        PlayerState SelectHighestVpPlayerTiebreak()
        {
            PlayerState best = Players[0];
            for (int i = 1; i < Players.Count; i++)
            {
                var p = Players[i];
                if (p.VictoryPoints > best.VictoryPoints)
                    best = p;
                else if (p.VictoryPoints == best.VictoryPoints && p.PlayerIndex < best.PlayerIndex)
                    best = p;
            }

            return best;
        }

        void EndGame(PlayerState winner, string reason)
        {
            if (IsGameOver || winner == null)
                return;

            IsGameOver = true;
            Winner = winner;
            EndGameReason = reason ?? "";
            FinalSnapshot = BuildGameEndSnapshot(winner, EndGameReason);
            StopBattleFlowForGameEnd();
            NotifyOnlineStateChanged();
            Debug.Log($"[GameEnd] P{winner.PlayerIndex + 1} wins: {EndGameReason}");
        }

        void StopBattleFlowForGameEnd()
        {
            // Do not StopCoroutine — <see cref="BattlePhaseCoroutine"/> may be the caller; it exits via break.
            BattlePhaseBlockingPlay = false;
            PendingBattleArrangement = false;
            EnergizePromptPlayer = null;
            EnergizeBattleContext = null;
            _energizeRoundActive = false;
            CasualtyPick = null;
            FocusFirePicker = null;
            SecretMissionOffer = null;
            DragonPhase = null;
            CancelFortressPlacement();
            _liveBattleLines = null;
            BattlePlan.Clear();
        }

        GameEndSnapshot BuildGameEndSnapshot(PlayerState winner, string reason)
        {
            var snap = new GameEndSnapshot
            {
                WinReason = reason,
                WinnerPlayerIndex = winner.PlayerIndex,
                TurnsElapsed = _completedPlayerTurns
            };

            int n = Players.Count;
            snap.PlayerIndex = new int[n];
            snap.VictoryPoints = new int[n];
            snap.Rubium = new int[n];
            snap.UnitCounts = new int[n];
            snap.PlayerColor = new Color[n];

            for (int i = 0; i < n; i++)
            {
                var p = Players[i];
                snap.PlayerIndex[i] = p.PlayerIndex;
                snap.VictoryPoints[i] = p.VictoryPoints;
                snap.Rubium[i] = p.Rubium;
                snap.UnitCounts[i] = CountUnitsForPlayer(p);
                snap.PlayerColor[i] = p.Color;
            }

            return snap;
        }

        int CountUnitsForPlayer(PlayerState p)
        {
            if (p == null)
                return 0;
            int c = 0;
            foreach (var u in FindObjectsOfType<UnitInstance>())
            {
                if (u != null && u.Owner == p)
                    c++;
            }

            return c;
        }

        internal void ResetEndGameState()
        {
            IsGameOver = false;
            Winner = null;
            EndGameReason = "";
            FinalSnapshot = null;
            _completedPlayerTurns = 0;
        }
    }

    /// <summary>Captured when the game ends for the results overlay.</summary>
    public class GameEndSnapshot
    {
        public int WinnerPlayerIndex;
        public string WinReason;
        public int TurnsElapsed;
        public int[] PlayerIndex;
        public int[] VictoryPoints;
        public int[] Rubium;
        public int[] UnitCounts;
        public Color[] PlayerColor;
    }
}
