using System.Collections.Generic;
using UnityEngine;

namespace NexusGame
{
    /// <summary>Procedural top-down thumbnails for main-menu map selection.</summary>
    public static class BoardMapPreview
    {
        static readonly Dictionary<BoardLayoutMode, Texture2D> Cache = new();

        const int TexSize = 160;

        public static Texture2D Get(BoardLayoutMode mode)
        {
            if (Cache.TryGetValue(mode, out var tex) && tex != null)
                return tex;

            tex = Build(mode);
            Cache[mode] = tex;
            return tex;
        }

        /// <summary>Clears cached thumbnails (e.g. after preview layout tweaks).</summary>
        public static void ClearCache()
        {
            foreach (var kv in Cache)
            {
                if (kv.Value != null)
                    Object.Destroy(kv.Value);
            }

            Cache.Clear();
        }

        static Texture2D Build(BoardLayoutMode mode)
        {
            var tiles = CollectTiles(mode);
            var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var bg = new Color(0.06f, 0.1f, 0.18f, 1f);
            var pixels = new Color[TexSize * TexSize];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = bg;

            if (tiles.Count == 0)
            {
                tex.SetPixels(pixels);
                tex.Apply();
                return tex;
            }

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            var centers = new List<(float px, float py, Color color)>(tiles.Count);

            foreach (var (q, r, type) in tiles)
            {
                AxialToPreviewXY(q, r, out float x, out float y);
                centers.Add((x, y, TileColor(type)));
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }

            const float unitHexRadius = 1f;
            float spanX = Mathf.Max(0.01f, maxX - minX);
            float spanY = Mathf.Max(0.01f, maxY - minY);
            float fitSpan = Mathf.Max(spanX, spanY) + unitHexRadius * 2f;
            float scale = (TexSize - 10f) / fitSpan;
            float cx = (minX + maxX) * 0.5f;
            float cy = (minY + maxY) * 0.5f;
            float hexR = scale * unitHexRadius;

            for (int i = 0; i < centers.Count; i++)
            {
                var c = centers[i];
                centers[i] = (
                    (c.px - cx) * scale + TexSize * 0.5f,
                    (c.py - cy) * scale + TexSize * 0.5f,
                    c.color);
            }

            var tileIds = new int[TexSize * TexSize];
            for (int i = 0; i < tileIds.Length; i++)
                tileIds[i] = -1;

            PaintHexTiles(pixels, tileIds, TexSize, centers, hexR, bg);
            ApplyTileSeams(pixels, tileIds, TexSize);

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        static void PaintHexTiles(Color[] pixels, int[] tileIds, int size,
            List<(float px, float py, Color color)> centers, float hexR, Color bg)
        {
            for (int py = 0; py < size; py++)
            {
                float fy = py + 0.5f;
                for (int px = 0; px < size; px++)
                {
                    float fx = px + 0.5f;
                    int i = py * size + px;
                    float best = float.MaxValue;
                    int bestIdx = -1;

                    for (int t = 0; t < centers.Count; t++)
                    {
                        var (hx, hy, _) = centers[t];
                        float dx = fx - hx;
                        float dy = fy - hy;
                        float d = dx * dx + dy * dy;
                        if (d < best)
                        {
                            best = d;
                            bestIdx = t;
                        }
                    }

                    if (bestIdx < 0)
                        continue;

                    var (cx, cy, color) = centers[bestIdx];
                    if (!PointInHex(fx, fy, cx, cy, hexR))
                        continue;

                    pixels[i] = color;
                    tileIds[i] = bestIdx;
                }
            }
        }

        static void ApplyTileSeams(Color[] pixels, int[] tileIds, int size)
        {
            var seam = new Color(0.02f, 0.04f, 0.08f, 1f);

            for (int py = 1; py < size - 1; py++)
            {
                for (int px = 1; px < size - 1; px++)
                {
                    int i = py * size + px;
                    int id = tileIds[i];
                    if (id < 0)
                        continue;

                    if (tileIds[i - 1] != id || tileIds[i + 1] != id || tileIds[i - size] != id || tileIds[i + size] != id)
                        pixels[i] = seam;
                }
            }
        }

        static bool PointInHex(float px, float py, float cx, float cy, float radius)
        {
            float dx = Mathf.Abs(px - cx);
            float dy = Mathf.Abs(py - cy);
            if (dy > radius)
                return false;
            float slope = radius * 0.5f;
            return dx <= radius - slope * dy / radius;
        }

        static List<(int q, int r, TileType type)> CollectTiles(BoardLayoutMode mode)
        {
            switch (mode)
            {
                case BoardLayoutMode.BattleTest:
                    return new List<(int, int, TileType)>
                    {
                        (-1, 0, TileType.HomeBase),
                        (0, 0, TileType.Plains),
                        (1, 0, TileType.HomeBase)
                    };
                case BoardLayoutMode.TwoToFour:
                    return BuildRadiusLayout(3, ring1: TileType.Lava, ring2Mix: true, ring3Mix: true);
                case BoardLayoutMode.TwoToFourSmall:
                    return BuildRadiusLayout(2, ring1: TileType.Lava, ring2Mix: true, ring3Mix: false);
                default:
                    return BuildOneVOneTiles();
            }
        }

        static List<(int q, int r, TileType type)> BuildOneVOneTiles()
        {
            const int radius = 2;
            var list = new List<(int, int, TileType)>();
            for (int q = -radius; q <= radius; q++)
            {
                int r1 = Mathf.Max(-radius, -q - radius);
                int r2 = Mathf.Min(radius, -q + radius);
                for (int r = r1; r <= r2; r++)
                    list.Add((q, r, TileTypeForOneVOne(q, r)));
            }

            return list;
        }

        static List<(int q, int r, TileType type)> BuildRadiusLayout(int radius, TileType ring1,
            bool ring2Mix, bool ring3Mix)
        {
            var list = new List<(int, int, TileType)>();
            var ring2 = new List<(int q, int r)>();
            var ring3 = new List<(int q, int r)>();

            for (int q = -radius; q <= radius; q++)
            {
                int r1 = Mathf.Max(-radius, -q - radius);
                int r2 = Mathf.Min(radius, -q + radius);
                for (int r = r1; r <= r2; r++)
                {
                    int dist = AxialDistance(0, 0, q, r);
                    if (dist == 0)
                        list.Add((q, r, TileType.Monolith));
                    else if (dist == 1)
                        list.Add((q, r, ring1));
                    else if (dist == 2)
                        ring2.Add((q, r));
                    else if (dist == 3)
                        ring3.Add((q, r));
                }
            }

            if (ring2Mix)
                ApplyDeterministicMix(ring2, list, seed: 11);
            else
            {
                foreach (var (q, r) in ring2)
                    list.Add((q, r, TileType.Plains));
            }

            if (ring3Mix)
                ApplyDeterministicMix(ring3, list, seed: 29);

            return list;
        }

        static void ApplyDeterministicMix(List<(int q, int r)> ring, List<(int q, int r, TileType type)> list,
            int seed)
        {
            var types = new List<TileType>();
            int n = ring.Count;
            int per = n / 3;
            for (int i = 0; i < per; i++) types.Add(TileType.CrystalField);
            for (int i = 0; i < per; i++) types.Add(TileType.Forest);
            for (int i = 0; i < n - 2 * per; i++) types.Add(TileType.Rock);
            ShuffleDeterministic(types, seed);

            for (int i = 0; i < ring.Count; i++)
            {
                var (q, r) = ring[i];
                list.Add((q, r, types[i]));
            }
        }

        static void ShuffleDeterministic(List<TileType> list, int seed)
        {
            var rng = new System.Random(seed);
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        static TileType TileTypeForOneVOne(int q, int r)
        {
            if (q == 0 && r == 0)
                return TileType.Monolith;

            int hash = (q * 73856093) ^ (r * 19349663);
            hash = Mathf.Abs(hash);
            int v = hash % 5;
            return v switch
            {
                0 => TileType.Plains,
                1 => TileType.Forest,
                2 => TileType.CrystalField,
                3 => TileType.Lava,
                _ => TileType.Rock
            };
        }

        static int AxialDistance(int q1, int r1, int q2, int r2)
        {
            int dq = q1 - q2;
            int dr = r1 - r2;
            int ds = -(q1 + r1) - (-(q2 + r2));
            return (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(ds)) / 2;
        }

        static void AxialToPreviewXY(int q, int r, out float x, out float y)
        {
            x = Mathf.Sqrt(3f) * q + Mathf.Sqrt(3f) / 2f * r;
            y = 1.5f * r;
        }

        static Color TileColor(TileType type)
        {
            var def = NexusConfig.CreateDefault().GetTile(type);
            return def != null ? def.Color : Color.gray;
        }

    }
}
