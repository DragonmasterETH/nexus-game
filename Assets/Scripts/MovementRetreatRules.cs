using UnityEngine;

namespace NexusGame
{
    /// <summary>
    /// Optional retreat constraints for mobile rule testing:
    /// - Cannot move from a contested hex onto another hex that still has enemies (\"retreat into another fight\").
    /// - Retreats may be split sequentially from the same contested source hex.
    /// - Retreat must happen before normal movement for the turn.
    /// </summary>
    public static class MovementRetreatRules
    {
        public struct Result
        {
            public bool Allowed;
            public string Reason;
        }

        public static Result Evaluate(UnitInstance unit, BoardTile target, GameController game)
        {
            if (unit == null || target == null || game == null)
                return new Result { Allowed = true, Reason = "" };

            bool sourceContested = TileHasEnemy(unit.Tile, unit.Owner);
            bool destHasEnemy = TileHasEnemy(target, unit.Owner);

            if (sourceContested && destHasEnemy)
            {
                return new Result
                {
                    Allowed = false,
                    Reason = "Retreat blocked: destination hex still has enemy units (cannot retreat into another fight)."
                };
            }

            if (!sourceContested)
                return new Result { Allowed = true, Reason = "" };

            if (game.NormalMovementOccurredThisTurn)
            {
                return new Result
                {
                    Allowed = false,
                    Reason = "Retreat blocked: leaving a contested hex must be the first movement of your turn."
                };
            }

            if (game.ActiveRetreatSourceThisTurn != null && game.ActiveRetreatSourceThisTurn != unit.Tile)
            {
                return new Result
                {
                    Allowed = false,
                    Reason = "Retreat blocked: continue retreating from the same contested hex before other movement."
                };
            }

            return new Result { Allowed = true, Reason = "" };
        }

        static bool TileHasEnemy(BoardTile tile, PlayerState owner)
        {
            if (tile == null || owner == null)
                return false;
            foreach (var u in Object.FindObjectsOfType<UnitInstance>())
            {
                if (u != null && u.Tile == tile && u.Owner != owner)
                    return true;
            }

            return false;
        }
    }
}
