using System.Collections.Generic;
using UnityEngine;

namespace NexusGame
{
    public class BoardTile
    {
        public int Q; // axial coords
        public int R;
        public TileType Type;
        public GameObject View;
        public PlayerState Owner;
        public int HomeBaseStartingOwnerIndex = -1;
        public int ExtraMineYield;
        public ExplorationReward ExplorationReward;
        public bool ExplorationRevealed;
        public GameObject ExplorationMarker;
        public GameObject Highlight;
        public GameObject MineLabel;

        /// <summary>Original hex fill color before selection dim; used by <see cref="MobileInputController"/>.</summary>
        public Color HexFillBaseColor;

        public bool HexFillBaseColorCaptured;
    }

    public enum BoardLayoutMode
    {
        OneVOne,
        TwoToFour,        // radius-3 mainland
        TwoToFourSmall    // 12-hex outer, 6-hex inner, 1 center
    }

    public class BoardGenerator : MonoBehaviour
    {
        [Header("Config")]
        public NexusConfig Config;
        public int RingRadius = 2; // main board radius (monolith + mines)
        public float HexRadius = 0.7f;

        [Header("Visuals")]
        public GameObject HexPrefab;

        public BoardLayoutMode LayoutMode = BoardLayoutMode.OneVOne;

        /// <summary>Shared material for unrevealed exploration markers (<c>Ore Unrevealed.png</c> in Resources).</summary>
        Material _explorationUnrevealedSharedMat;

        static Material _selectionLineSharedMaterial;

        static Material SelectionLineMaterial()
        {
            if (_selectionLineSharedMaterial != null)
                return _selectionLineSharedMaterial;
            var sh = Shader.Find("Nexus/SelectionLine");
            if (sh == null)
                sh = Shader.Find("Sprites/Default");
            _selectionLineSharedMaterial = new Material(sh);
            return _selectionLineSharedMaterial;
        }

        public Dictionary<(int q, int r), BoardTile> Tiles { get; private set; } =
            new Dictionary<(int q, int r), BoardTile>();

        public IEnumerable<BoardTile> AllTiles => Tiles.Values;

        void Awake()
        {
            if (Config == null)
            {
                Config = NexusConfig.CreateDefault();
            }

            GenerateBoard();
        }

        public void Regenerate()
        {
            // Clear existing visual children
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }

            GenerateBoard();
        }

        void GenerateBoard()
        {
            Tiles.Clear();

            switch (LayoutMode)
            {
                case BoardLayoutMode.OneVOne:
                    GenerateOneVOneLayout();
                    break;
                case BoardLayoutMode.TwoToFour:
                    GenerateTwoToFourLayout();
                    break;
                case BoardLayoutMode.TwoToFourSmall:
                    GenerateTwoToFourSmallLayout();
                    break;
            }
        }

        void GenerateOneVOneLayout()
        {
            // simple hex map: center monolith, RingRadius rings around
            for (int q = -RingRadius; q <= RingRadius; q++)
            {
                int r1 = Mathf.Max(-RingRadius, -q - RingRadius);
                int r2 = Mathf.Min(RingRadius, -q + RingRadius);
                for (int r = r1; r <= r2; r++)
                {
                    Vector3 pos = AxialToWorld(q, r);
                    TileType type = GetTileTypeForCoordinates(q, r);

                    GameObject tileObj = CreateHexVisual(pos, type);

                    var tile = new BoardTile
                    {
                        Q = q,
                        R = r,
                        Type = type,
                        View = tileObj,
                        ExtraMineYield = 0,
                        ExplorationReward = ExplorationReward.None,
                        ExplorationRevealed = false,
                        Highlight = tileObj.transform.Find("Highlight") != null
                            ? tileObj.transform.Find("Highlight").gameObject
                            : null,
                        MineLabel = null
                    };

                    // Attach click proxy to map collider hits back to this tile.
                    var proxy = tileObj.AddComponent<TileClickProxy>();
                    proxy.Q = q;
                    proxy.R = r;

                    Tiles[(q, r)] = tile;
                }
            }

            AssignExplorationTokens();
        }

        void GenerateTwoToFourLayout()
        {
            // Fixed radius-3 hex layout with random terrain per ring based on desired counts.
            RingRadius = 3;

            int radius = 3;
            var center = (q: 0, r: 0);
            var ring1 = new List<(int q, int r)>();
            var ring2 = new List<(int q, int r)>();
            var ring3 = new List<(int q, int r)>();

            // First, record coordinates per ring (distance from center)
            for (int q = -radius; q <= radius; q++)
            {
                int r1 = Mathf.Max(-radius, -q - radius);
                int r2 = Mathf.Min(radius, -q + radius);
                for (int r = r1; r <= r2; r++)
                {
                    int dist = AxialDistance(0, 0, q, r);
                    if (dist > radius) continue;

                    Tiles[(q, r)] = new BoardTile
                    {
                        Q = q,
                        R = r,
                        Type = TileType.Plains, // placeholder
                        View = null,
                        ExtraMineYield = 0,
                        ExplorationReward = ExplorationReward.None,
                        ExplorationRevealed = false,
                        Highlight = null,
                        MineLabel = null
                    };

                    if (dist == 1) ring1.Add((q, r));
                    else if (dist == 2) ring2.Add((q, r));
                    else if (dist == 3) ring3.Add((q, r));
                }
            }

            // Center monolith
            var centerTile = Tiles[center];
            centerTile.Type = TileType.Monolith;
            centerTile.View = CreateHexVisual(AxialToWorld(center.q, center.r), TileType.Monolith);
            centerTile.Highlight = centerTile.View.transform.Find("Highlight") != null
                ? centerTile.View.transform.Find("Highlight").gameObject
                : null;
            var centerProxy = centerTile.View.AddComponent<TileClickProxy>();
            centerProxy.Q = center.q;
            centerProxy.R = center.r;

            // Ring 1: all Lava
            foreach (var (q, r) in ring1)
            {
                var tile = Tiles[(q, r)];
                tile.Type = TileType.Lava;
                tile.View = CreateHexVisual(AxialToWorld(q, r), TileType.Lava);
                tile.Highlight = tile.View.transform.Find("Highlight") != null
                    ? tile.View.transform.Find("Highlight").gameObject
                    : null;
                var proxy = tile.View.AddComponent<TileClickProxy>();
                proxy.Q = q;
                proxy.R = r;
            }

            // Ring 2: mix of Forest and Rock (half / half)
            var ring2Types = new List<TileType>();
            int ring2Count = ring2.Count;
            int forestCount = ring2Count / 2;
            int rockCount = ring2Count - forestCount;
            for (int i = 0; i < forestCount; i++) ring2Types.Add(TileType.Forest);
            for (int i = 0; i < rockCount; i++) ring2Types.Add(TileType.Rock);
            Shuffle(ring2Types);

            for (int i = 0; i < ring2.Count; i++)
            {
                var (q, r) = ring2[i];
                var tile = Tiles[(q, r)];
                tile.Type = ring2Types[i];
                tile.View = CreateHexVisual(AxialToWorld(q, r), tile.Type);
                tile.Highlight = tile.View.transform.Find("Highlight") != null
                    ? tile.View.transform.Find("Highlight").gameObject
                    : null;
                var proxy = tile.View.AddComponent<TileClickProxy>();
                proxy.Q = q;
                proxy.R = r;
            }

            // Ring 3: mix of CrystalField, Forest, and Rock (approx equal thirds)
            var ring3Types = new List<TileType>();
            int ring3Count = ring3.Count; // 18 for radius 3
            int perType = ring3Count / 3; // 6 each
            for (int i = 0; i < perType; i++) ring3Types.Add(TileType.CrystalField);
            for (int i = 0; i < perType; i++) ring3Types.Add(TileType.Forest);
            for (int i = 0; i < ring3Count - 2 * perType; i++) ring3Types.Add(TileType.Rock);
            Shuffle(ring3Types);

            for (int i = 0; i < ring3.Count; i++)
            {
                var (q, r) = ring3[i];
                var tile = Tiles[(q, r)];
                tile.Type = ring3Types[i];
                tile.View = CreateHexVisual(AxialToWorld(q, r), tile.Type);
                tile.Highlight = tile.View.transform.Find("Highlight") != null
                    ? tile.View.transform.Find("Highlight").gameObject
                    : null;
                var proxy = tile.View.AddComponent<TileClickProxy>();
                proxy.Q = q;
                proxy.R = r;
            }

            AssignExplorationTokens();
        }

        // 2–4 player layout B: 12-hex outer ring, 6-hex inner ring, 1 monolith center.
        void GenerateTwoToFourSmallLayout()
        {
            // Mainland is radius-2 hex: center (0), ring1 (6), ring2 (12)
            RingRadius = 2;

            int radius = 2;
            var center = (q: 0, r: 0);
            var ring1 = new List<(int q, int r)>(); // inner ring
            var ring2 = new List<(int q, int r)>(); // outer ring

            for (int q = -radius; q <= radius; q++)
            {
                int r1 = Mathf.Max(-radius, -q - radius);
                int r2 = Mathf.Min(radius, -q + radius);
                for (int r = r1; r <= r2; r++)
                {
                    int dist = AxialDistance(0, 0, q, r);
                    if (dist > radius) continue;

                    Tiles[(q, r)] = new BoardTile
                    {
                        Q = q,
                        R = r,
                        Type = TileType.Plains,
                        View = null,
                        ExtraMineYield = 0,
                        ExplorationReward = ExplorationReward.None,
                        ExplorationRevealed = false,
                        Highlight = null,
                        MineLabel = null
                    };

                    if (dist == 1) ring1.Add((q, r));
                    else if (dist == 2) ring2.Add((q, r));
                }
            }

            // Center monolith
            var centerTile = Tiles[center];
            centerTile.Type = TileType.Monolith;
            centerTile.View = CreateHexVisual(AxialToWorld(center.q, center.r), TileType.Monolith);
            centerTile.Highlight = centerTile.View.transform.Find("Highlight") != null
                ? centerTile.View.transform.Find("Highlight").gameObject
                : null;
            var centerProxy = centerTile.View.AddComponent<TileClickProxy>();
            centerProxy.Q = center.q;
            centerProxy.R = center.r;

            // Inner ring: all Lava
            foreach (var (q, r) in ring1)
            {
                var tile = Tiles[(q, r)];
                tile.Type = TileType.Lava;
                tile.View = CreateHexVisual(AxialToWorld(q, r), TileType.Lava);
                tile.Highlight = tile.View.transform.Find("Highlight") != null
                    ? tile.View.transform.Find("Highlight").gameObject
                    : null;
                var proxy = tile.View.AddComponent<TileClickProxy>();
                proxy.Q = q;
                proxy.R = r;
            }

            // Outer ring: 4 CrystalField, 4 Forest, 4 Rock, randomized
            var ring2Types = new List<TileType>();
            int ring2Count = ring2.Count; // should be 12
            int perType = ring2Count / 3; // 4 each
            for (int i = 0; i < perType; i++) ring2Types.Add(TileType.CrystalField);
            for (int i = 0; i < perType; i++) ring2Types.Add(TileType.Forest);
            for (int i = 0; i < ring2Count - 2 * perType; i++) ring2Types.Add(TileType.Rock);
            Shuffle(ring2Types);

            for (int i = 0; i < ring2.Count; i++)
            {
                var (q, r) = ring2[i];
                var tile = Tiles[(q, r)];
                tile.Type = ring2Types[i];
                tile.View = CreateHexVisual(AxialToWorld(q, r), tile.Type);
                tile.Highlight = tile.View.transform.Find("Highlight") != null
                    ? tile.View.transform.Find("Highlight").gameObject
                    : null;
                var proxy = tile.View.AddComponent<TileClickProxy>();
                proxy.Q = q;
                proxy.R = r;
            }

            AssignExplorationTokens();
        }

        int AxialDistance(int q1, int r1, int q2, int r2)
        {
            int dq = q1 - q2;
            int dr = r1 - r2;
            int ds = -(q1 + r1) - (-(q2 + r2));
            return (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(ds)) / 2;
        }

        void Shuffle<T>(List<T> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                int j = Random.Range(i, list.Count);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        GameObject CreateHexVisual(Vector3 position, TileType type)
        {
            GameObject go;
            if (HexPrefab != null)
            {
                go = Instantiate(HexPrefab, position, Quaternion.identity, transform);
            }
            else
            {
                // Create a flat hexagonal mesh instead of a square quad
                go = new GameObject("HexTile");
                go.transform.SetParent(transform, worldPositionStays: true);
                go.transform.position = position;
                go.transform.rotation = Quaternion.identity;

                var meshFilter = go.AddComponent<MeshFilter>();
                var meshRenderer = go.AddComponent<MeshRenderer>();

                var mesh = new Mesh();

                // Center + 6 vertices around
                var verts = new Vector3[7];
                verts[0] = Vector3.zero;
                for (int i = 0; i < 6; i++)
                {
                    float angle = Mathf.Deg2Rad * (60f * i + 30f);
                    float x = Mathf.Cos(angle) * HexRadius;
                    float z = Mathf.Sin(angle) * HexRadius;
                    verts[i + 1] = new Vector3(x, 0f, z);
                }
                mesh.vertices = verts;

                // Triangles fan from center
                var tris = new int[6 * 3];
                for (int i = 0; i < 6; i++)
                {
                    int triIndex = i * 3;
                    tris[triIndex + 0] = 0;
                    tris[triIndex + 1] = i + 1;
                    tris[triIndex + 2] = i == 5 ? 1 : i + 2;
                }
                mesh.triangles = tris;
                mesh.RecalculateNormals();
                meshFilter.mesh = mesh;
            }

            var rend = go.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                var tileDef = Config.GetTile(type);

                // Use an unlit shader so tiles are always visible regardless of pipeline/lighting.
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null)
                    shader = Shader.Find("Unlit/Color");

                if (shader != null)
                {
                    rend.material = new Material(shader);
                }

                if (tileDef != null)
                {
                    rend.material.color = tileDef.Color;
                }
                else
                {
                    rend.material.color = Color.gray;
                }
            }

            // Dedicated click collider slightly above the hex so raycasts always hit,
            // independent of the render mesh details.
            var clickObj = new GameObject("ClickCollider");
            clickObj.transform.SetParent(go.transform, worldPositionStays: false);
            clickObj.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            clickObj.transform.localRotation = Quaternion.identity;
            var box = clickObj.AddComponent<BoxCollider>();
            float size = HexRadius * 1.7f;
            box.size = new Vector3(size, 0.1f, size);

            go.name = $"Tile_{type}_{position.x:0}_{position.z:0}";

            // Add a simple hex outline using LineRenderer (always drawn slightly above tile)
            var outline = new GameObject("Outline");
            outline.transform.SetParent(go.transform, worldPositionStays: false);
            outline.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            outline.transform.localRotation = Quaternion.identity;
            outline.transform.localScale = Vector3.one;

            var lr = outline.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = 7;
            lr.loop = true;
            lr.widthMultiplier = 0.05f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = lr.endColor = Color.black;

            float outlineRadius = HexRadius * 0.98f;
            for (int i = 0; i < 7; i++)
            {
                float angle = Mathf.Deg2Rad * (60f * i + 30f);
                float x = Mathf.Cos(angle) * outlineRadius;
                float z = Mathf.Sin(angle) * outlineRadius;
                lr.SetPosition(i, new Vector3(x, 0f, z));
            }

            // Selection highlight: white hex border (initially hidden; above black tile outline).
            // Uses Nexus/SelectionLine (ZTest Always) so neighbor hexes don't depth-occlude shared edges.
            var highlight = new GameObject("Highlight");
            highlight.transform.SetParent(go.transform, worldPositionStays: false);
            highlight.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            highlight.transform.localRotation = Quaternion.identity;
            highlight.transform.localScale = Vector3.one;

            var hlr = highlight.AddComponent<LineRenderer>();
            hlr.useWorldSpace = false;
            hlr.positionCount = 7;
            hlr.loop = true;
            hlr.widthMultiplier = 0.08f;
            hlr.sharedMaterial = SelectionLineMaterial();
            hlr.startColor = hlr.endColor = Color.white;
            hlr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            hlr.receiveShadows = false;

            float hiRadius = HexRadius * 1.02f;
            for (int i = 0; i < 7; i++)
            {
                float angle = Mathf.Deg2Rad * (60f * i + 30f);
                float x = Mathf.Cos(angle) * hiRadius;
                float z = Mathf.Sin(angle) * hiRadius;
                hlr.SetPosition(i, new Vector3(x, 0f, z));
            }

            highlight.SetActive(false);

            return go;
        }

        TileType GetTileTypeForCoordinates(int q, int r)
        {
            if (q == 0 && r == 0)
                return TileType.Monolith;

            // crude deterministic pattern based on coordinates
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

        public Vector3 AxialToWorld(int q, int r)
        {
            float x = HexRadius * (Mathf.Sqrt(3f) * q + Mathf.Sqrt(3f) / 2f * r);
            float z = HexRadius * (3f / 2f * r);
            return new Vector3(x, 0f, z);
        }

        public BoardTile GetTile(int q, int r)
        {
            Tiles.TryGetValue((q, r), out var tile);
            return tile;
        }

        public IEnumerable<BoardTile> GetNeighbors(BoardTile tile)
        {
            if (tile == null) yield break;

            int[][] dirs =
            {
                new[] { 1, 0 },
                new[] { 1, -1 },
                new[] { 0, -1 },
                new[] { -1, 0 },
                new[] { -1, 1 },
                new[] { 0, 1 }
            };

            foreach (var d in dirs)
            {
                int nq = tile.Q + d[0];
                int nr = tile.R + d[1];
                if (Tiles.TryGetValue((nq, nr), out var n))
                {
                    yield return n;
                }
            }
        }

        void AssignExplorationTokens()
        {
            // Basic approximation of the 18 exploration tokens around the monolith.
            // We give some free units, some mines, some both.
            var rewards = new List<ExplorationReward>
            {
                ExplorationReward.FreeHuman,
                ExplorationReward.FreeHuman,
                ExplorationReward.FreeFungoid,
                ExplorationReward.FreeFungoid,
                ExplorationReward.FreeRockStrider,
                ExplorationReward.Mine1,
                ExplorationReward.Mine1,
                ExplorationReward.Mine2,
                ExplorationReward.Mine2,
                ExplorationReward.Mine3,
                ExplorationReward.Mine3,
                ExplorationReward.FreeHumanAndMine2
            };

            // Fill up to number of eligible tiles
            var eligible = new List<BoardTile>();
            foreach (var t in AllTiles)
            {
                if (t.Type != TileType.HomeBase && t.Type != TileType.Monolith)
                {
                    eligible.Add(t);
                }
            }

            int seed = 12345;
            for (int i = 0; i < eligible.Count; i++)
            {
                int j = seed % eligible.Count;
                (eligible[i], eligible[j]) = (eligible[j], eligible[i]);
                seed = (seed * 1103515245 + 12345) & 0x7fffffff;
            }

            if (_explorationUnrevealedSharedMat == null)
            {
                _explorationUnrevealedSharedMat = new Material(Shader.Find("Sprites/Default"));
                NexusGuiArt.ApplyImageToMaterial(_explorationUnrevealedSharedMat,
                    NexusGuiArt.Load("Sprites/Ore Unrevealed", "Sprites/OreUnrevealed"),
                    new Color(1f, 1f, 0.4f, 0.9f));
            }

            int count = Mathf.Min(rewards.Count, eligible.Count);
            for (int i = 0; i < count; i++)
            {
                var tile = eligible[i];
                tile.ExplorationReward = rewards[i];
                tile.ExplorationRevealed = false;

                // Hidden exploration reward: textured quad (Ore Unrevealed) or yellow fallback
                var marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
                marker.name = "ExplorationMarker";
                marker.transform.SetParent(tile.View.transform, worldPositionStays: false);
                // Centered on hex face (small Y to clear z-fight with terrain)
                marker.transform.localPosition = new Vector3(0, 0.035f, 0);
                marker.transform.localRotation = Quaternion.Euler(90, 0, 0);
                marker.transform.localScale = Vector3.one * HexRadius * 1.12f;
                var mr = marker.GetComponent<Renderer>();
                if (mr != null)
                    mr.sharedMaterial = _explorationUnrevealedSharedMat;

                tile.ExplorationMarker = marker;
            }

            // Create simple three-hex home bases just outside the main ring for up to four players.
            // Left and right strips (q fixed, r varies)
            int homeQLeft = -RingRadius - 1;
            int homeQRight = RingRadius + 1;
            int[] homeRs = { -1, 0, 1 };

            foreach (int r in homeRs)
            {
                CreateHomeTile(homeQLeft, r);
                CreateHomeTile(homeQRight, r);
            }

            // Top and bottom strips (r fixed, q varies)
            int homeRTop = -RingRadius - 1;
            int homeRBottom = RingRadius + 1;
            int[] homeQs = { -1, 0, 1 };

            foreach (int q in homeQs)
            {
                CreateHomeTile(q, homeRTop);
                CreateHomeTile(q, homeRBottom);
            }
        }

        void CreateHomeTile(int q, int r)
        {
            if (Tiles.ContainsKey((q, r)))
                return;

            Vector3 pos = AxialToWorld(q, r);
            GameObject tileObj = CreateHexVisual(pos, TileType.HomeBase);
            var highlight = tileObj.transform.Find("Highlight") != null
                ? tileObj.transform.Find("Highlight").gameObject
                : null;

            var tile = new BoardTile
            {
                Q = q,
                R = r,
                Type = TileType.HomeBase,
                View = tileObj,
                ExtraMineYield = 0,
                ExplorationReward = ExplorationReward.None,
                ExplorationRevealed = true,
                Highlight = highlight,
                MineLabel = null
            };

            var proxy = tileObj.AddComponent<TileClickProxy>();
            proxy.Q = q;
            proxy.R = r;

            Tiles[(q, r)] = tile;
        }
    }
}

