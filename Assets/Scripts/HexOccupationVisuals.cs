using UnityEngine;

namespace NexusGame
{
    /// <summary>
    /// Occupation colors for tile info UI only (modal hex ring, bottom strip). No world hex overlay.
    /// </summary>
    public static class HexOccupationVisuals
    {
        public static readonly Color ContestedRingColor = new Color(0.95f, 0.82f, 0.18f, 1f);

        /// <summary>True if any unit is on the tile; <paramref name="color"/> is ring/strip color.</summary>
        public static bool TryGetOccupationRingColor(BoardTile tile, out Color color)
        {
            color = default;
            if (tile == null)
                return false;

            bool hasAny = false;
            bool contested = false;
            int? soleIdx = null;
            PlayerState soleOwner = null;

            foreach (var unit in Object.FindObjectsOfType<UnitInstance>())
            {
                if (unit == null || unit.Tile != tile)
                    continue;
                hasAny = true;
                if (soleIdx == null)
                {
                    soleIdx = unit.Owner.PlayerIndex;
                    soleOwner = unit.Owner;
                }
                else if (soleIdx != unit.Owner.PlayerIndex)
                {
                    contested = true;
                }
            }

            if (!hasAny)
                return false;

            color = contested ? ContestedRingColor : (soleOwner != null ? soleOwner.Color : Color.white);
            return true;
        }
    }
}
