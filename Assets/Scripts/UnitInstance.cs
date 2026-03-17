using UnityEngine;

namespace NexusGame
{
    public class UnitInstance : MonoBehaviour
    {
        public PlayerState Owner { get; private set; }
        public UnitDefinition Definition { get; private set; }
        public BoardTile Tile { get; private set; }
        public bool HasMovedThisTurn { get; set; }

        public void Initialize(PlayerState owner, UnitDefinition def, BoardTile tile, bool hasAlreadyMovedThisTurn)
        {
            Owner = owner;
            Definition = def;
            HasMovedThisTurn = hasAlreadyMovedThisTurn;
            MoveTo(tile);
        }

        public void MoveTo(BoardTile tile)
        {
            Tile = tile;
            if (tile != null)
            {
                transform.position = tile.View.transform.position + Vector3.up * 0.25f;
            }

            // Mark that this unit has used its move for the current turn.
            HasMovedThisTurn = true;
        }
    }
}

