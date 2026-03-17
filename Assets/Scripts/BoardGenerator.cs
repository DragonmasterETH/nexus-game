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
        public int ExtraMineYield;
        public ExplorationReward ExplorationReward;
        public bool ExplorationRevealed;
        public GameObject ExplorationMarker;
        public GameObject Highlight;
    }

    public class BoardGenerator : MonoBehaviour
    {
        [Header("Config")]
        public NexusConfig Config;
        public int RingRadius = 2; // main board radius (monolith + mines)
        public float HexRadius = 0.7f;

        [Header("Visuals")]
        public GameObject HexPrefab;

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

        void GenerateBoard()
        {
            Tiles.Clear();

            // simple hex map: center monolith, ringRadius rings around
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
                            : null
                    };

                    Tiles[(q, r)] = tile;
                }
            }

            AssignExplorationTokens();
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
                go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.transform.SetParent(transform, worldPositionStays: true);
                go.transform.position = position;
                go.transform.rotation = Quaternion.Euler(90, 0, 0);
                go.transform.localScale = Vector3.one * HexRadius * 1.8f;
            }

            var rend = go.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                var tileDef = Config.GetTile(type);
                // Try URP Lit first, then fall back to Standard, then use existing material
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                if (shader != null)
                {
                    rend.material = new Material(shader);
                }

                if (tileDef != null)
                {
                    rend.material.color = tileDef.Color;
                }
            }

            go.name = $"Tile_{type}_{position.x:0}_{position.z:0}";

            // Add a simple hex outline using LineRenderer
            var outline = new GameObject("Outline");
            outline.transform.SetParent(go.transform, worldPositionStays: true);
            outline.transform.localPosition = Vector3.zero;
            var lr = outline.AddComponent<LineRenderer>();
            lr.positionCount = 7;
            lr.loop = true;
            lr.widthMultiplier = 0.03f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = lr.endColor = Color.black;

            for (int i = 0; i < 7; i++)
            {
                float angle = Mathf.Deg2Rad * (60f * i + 30f);
                float x = Mathf.Cos(angle) * HexRadius;
                float z = Mathf.Sin(angle) * HexRadius;
                lr.SetPosition(i, new Vector3(x, 0.01f, z));
            }

            // Selection highlight (initially hidden)
            var highlight = GameObject.CreatePrimitive(PrimitiveType.Quad);
            highlight.name = "Highlight";
            highlight.transform.SetParent(go.transform, worldPositionStays: false);
            highlight.transform.localPosition = new Vector3(0, 0.03f, 0);
            highlight.transform.localRotation = Quaternion.Euler(90, 0, 0);
            highlight.transform.localScale = Vector3.one * HexRadius * 1.6f;
            var hr = highlight.GetComponent<Renderer>();
            if (hr != null)
            {
                hr.material = new Material(Shader.Find("Sprites/Default"));
                hr.material.color = new Color(0f, 1f, 1f, 0.35f);
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
                ExplorationReward.Mine1,
                ExplorationReward.Mine1,
                ExplorationReward.Mine2,
                ExplorationReward.Mine2,
                ExplorationReward.Mine3,
                ExplorationReward.Mine3,
                ExplorationReward.FreeHumanAndMine2,
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

            int count = Mathf.Min(rewards.Count, eligible.Count);
            for (int i = 0; i < count; i++)
            {
                var tile = eligible[i];
                tile.ExplorationReward = rewards[i];
                tile.ExplorationRevealed = false;

                // Create a small "?" marker above the tile
                var marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
                marker.name = "ExplorationMarker";
                marker.transform.SetParent(tile.View.transform, worldPositionStays: false);
                marker.transform.localPosition = new Vector3(0, 0.02f, 0);
                marker.transform.localRotation = Quaternion.Euler(90, 0, 0);
                marker.transform.localScale = Vector3.one * HexRadius * 0.6f;
                var mr = marker.GetComponent<Renderer>();
                if (mr != null)
                {
                    mr.material = new Material(Shader.Find("Sprites/Default"));
                    mr.material.color = new Color(1f, 1f, 0.4f, 0.9f);
                }

                tile.ExplorationMarker = marker;
            }

            // Create simple three-hex home bases just outside the main ring for two players.
            // Left side (-RingRadius-1, 0 and its vertical neighbors), right side mirror.
            int homeQLeft = -RingRadius - 1;
            int homeQRight = RingRadius + 1;
            int[] homeRs = { -1, 0, 1 };

            foreach (int r in homeRs)
            {
                CreateHomeTile(homeQLeft, r);
                CreateHomeTile(homeQRight, r);
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
                Highlight = highlight
            };

            Tiles[(q, r)] = tile;
        }
    }
}

