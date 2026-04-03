using UnityEngine;

namespace NexusGame
{
    public class UnitInstance : MonoBehaviour
    {
        public PlayerState Owner { get; private set; }
        public UnitDefinition Definition { get; private set; }
        public BoardTile Tile { get; private set; }
        public bool HasMovedThisTurn { get; set; }
        float _hoverHeight = 0.25f;

        public void Initialize(PlayerState owner, UnitDefinition def, BoardTile tile, bool hasAlreadyMovedThisTurn)
        {
            Owner = owner;
            Definition = def;
            if (tile != null && tile.View != null)
            {
                _hoverHeight = transform.position.y - tile.View.transform.position.y;
                if (_hoverHeight <= 0.01f)
                    _hoverHeight = 0.25f;
            }
            HasMovedThisTurn = hasAlreadyMovedThisTurn;
            MoveTo(tile);
        }

        public void MoveTo(BoardTile tile)
        {
            var previousTile = Tile;
            Tile = tile;
            RelayoutTile(previousTile);
            RelayoutTile(tile);

            // Mark that this unit has used its move for the current turn.
            HasMovedThisTurn = true;
        }

        public static void RelayoutTile(BoardTile tile)
        {
            if (tile == null || tile.View == null)
                return;

            var units = new System.Collections.Generic.List<UnitInstance>();
            foreach (var u in Object.FindObjectsOfType<UnitInstance>())
            {
                if (u != null && u.Tile == tile)
                    units.Add(u);
            }

            if (units.Count == 0)
                return;

            units.Sort((a, b) =>
            {
                int byPlayer = a.Owner.PlayerIndex.CompareTo(b.Owner.PlayerIndex);
                if (byPlayer != 0) return byPlayer;
                int byType = a.Definition.Type.CompareTo(b.Definition.Type);
                if (byType != 0) return byType;
                return a.GetInstanceID().CompareTo(b.GetInstanceID());
            });

            var center = tile.View.transform.position;
            for (int i = 0; i < units.Count; i++)
            {
                var offset = GetFormationOffset(i, units.Count);
                units[i].transform.position = center + offset + Vector3.up * units[i]._hoverHeight;
            }
        }

        static Vector3 GetFormationOffset(int index, int total)
        {
            if (total <= 1 || index <= 0)
                return Vector3.zero;

            // Ring layout: center + rings of 6, 12, 18...
            int remaining = index - 1;
            int ring = 1;
            while (remaining >= 6 * ring)
            {
                remaining -= 6 * ring;
                ring++;
            }

            int slots = 6 * ring;
            float angleDeg = (360f * remaining / slots) + (ring * 10f);
            // Ring radius: stay inside ~hex face (BoardGenerator HexRadius ~0.7); spread stacks clearly.
            const float maxFormationRadius = 0.62f;
            const float radiusPerRing = 0.30f;
            float radius = Mathf.Min(maxFormationRadius, radiusPerRing * ring);
            float angle = angleDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }
    }
}

