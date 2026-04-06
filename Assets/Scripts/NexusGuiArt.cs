using UnityEngine;

namespace NexusGame
{
    /// <summary>
    /// IMGUI drawing for HUD icons. Unity often imports PNGs as <see cref="Sprite"/> only;
    /// <see cref="Resources.Load{T}"/> with <see cref="Texture2D"/> then returns null.
    /// </summary>
    public readonly struct NexusGuiImage
    {
        public readonly Texture2D Texture;
        public readonly Sprite Sprite;

        public NexusGuiImage(Texture2D t)
        {
            Texture = t;
            Sprite = null;
        }

        public NexusGuiImage(Sprite s)
        {
            Texture = null;
            Sprite = s;
        }

        public bool IsEmpty => Texture == null && Sprite == null;

        public float AspectRatio
        {
            get
            {
                if (Sprite != null)
                    return Mathf.Max(0.01f, Sprite.rect.width / Sprite.rect.height);
                if (Texture != null && Texture.height > 0)
                    return (float)Texture.width / Texture.height;
                return 1f;
            }
        }

        /// <summary>Draw scaled to height; returns drawn width.</summary>
        public float Draw(float x, float y, float height)
        {
            if (IsEmpty || height <= 0f)
                return 0f;
            float w = height * AspectRatio;
            Draw(new Rect(x, y, w, height));
            return w;
        }

        public void Draw(Rect r)
        {
            if (IsEmpty || r.height <= 0f)
                return;
            if (Sprite != null)
            {
                var t = Sprite.texture;
                var tr = Sprite.textureRect;
                float tw = t.width;
                float th = t.height;
                var uv = new Rect(tr.x / tw, tr.y / th, tr.width / tw, tr.height / th);
                GUI.DrawTextureWithTexCoords(r, t, uv, true);
            }
            else
            {
                GUI.DrawTexture(r, Texture, ScaleMode.ScaleToFit, true);
            }
        }
    }

    public static class NexusGuiArt
    {
        public static NexusGuiImage FromFields(Texture2D icon, Sprite spr)
        {
            if (icon != null)
                return new NexusGuiImage(icon);
            if (spr != null)
                return new NexusGuiImage(spr);
            return default;
        }

        /// <summary>Tries Sprite (single path, then all sprites in asset), then Texture2D.</summary>
        public static NexusGuiImage Load(params string[] paths)
        {
            if (paths == null || paths.Length == 0)
                return default;

            foreach (var p in paths)
            {
                if (string.IsNullOrEmpty(p))
                    continue;
                var s = Resources.Load<Sprite>(p);
                if (s != null)
                    return new NexusGuiImage(s);
            }

            foreach (var p in paths)
            {
                if (string.IsNullOrEmpty(p))
                    continue;
                var sprites = Resources.LoadAll<Sprite>(p);
                if (sprites != null && sprites.Length > 0)
                    return new NexusGuiImage(sprites[0]);
            }

            foreach (var p in paths)
            {
                if (string.IsNullOrEmpty(p))
                    continue;
                var t = Resources.Load<Texture2D>(p);
                if (t != null)
                    return new NexusGuiImage(t);
            }

            return default;
        }

        /// <summary>Rubium Dragon art per player color (Resources e.g. Sprites/Units/Dragon_Blue).</summary>
        public static NexusGuiImage LoadRubiumDragonForPlayer(PlayerState owner)
        {
            string c = DragonColorSuffix(owner);
            return Load(
                $"Sprites/Units/Dragon_{c}",
                $"Sprites/units/Dragon_{c}",
                $"Sprites/Units/Dragon {c}",
                $"Sprites/units/Dragon {c}",
                $"Sprites/Units/RubiumDragon_{c}",
                $"Sprites/Units/RubiumDragon {c}",
                $"Sprites/Units/{c}Dragon",
                $"Sprites/Units/Dragon{c}");
        }

        /// <summary>Shared battle-ribbon / generic HUD dragon when no owner context (defaults to P1-style blue index 0).</summary>
        public static NexusGuiImage LoadRubiumDragonLegendIcon()
        {
            return Load(
                "Sprites/Units/Dragon_Blue",
                "Sprites/units/Dragon_Blue",
                "Sprites/Units/Dragon Blue",
                "Sprites/Units/RubiumDragon",
                "Sprites/Units/RubiumDragon_Blue");
        }

        /// <summary>Rock Strider art per seat (e.g. Sprites/Units/Strider Blue).</summary>
        public static NexusGuiImage LoadRockStriderForPlayer(PlayerState owner)
        {
            string c = DragonColorSuffix(owner);
            return Load(
                $"Sprites/Units/Strider {c}",
                $"Sprites/units/Strider {c}",
                $"Sprites/Units/Strider_{c}",
                $"Sprites/units/Strider_{c}",
                $"Sprites/Units/Strider{c}",
                $"Sprites/Units/RockStrider_{c}",
                $"Sprites/Units/RockStrider {c}");
        }

        /// <summary>Ribbon / fallback when no owner (blue seat).</summary>
        public static NexusGuiImage LoadRockStriderLegendIcon()
        {
            return Load(
                "Sprites/Units/Strider Blue",
                "Sprites/units/Strider Blue",
                "Sprites/Units/Strider_Blue",
                "Sprites/Units/StriderBlue",
                "Sprites/Units/RockStrider");
        }

        static string DragonColorSuffix(PlayerState owner)
        {
            if (owner == null)
                return "Blue";
            var col = owner.Color;
            if (ChannelNear(col.r, 1f) && col.g < 0.35f && col.b < 0.35f)
                return "Red";
            if (col.r < 0.35f && col.g < 0.35f && ChannelNear(col.b, 1f))
                return "Blue";
            if (col.r < 0.35f && ChannelNear(col.g, 1f) && col.b < 0.35f)
                return "Green";
            if (col.r > 0.85f && col.g > 0.85f && col.b < 0.35f)
                return "Yellow";

            return owner.PlayerIndex switch
            {
                0 => "Blue",
                1 => "Red",
                2 => "Green",
                3 => "Yellow",
                _ => "Blue"
            };
        }

        static bool ChannelNear(float v, float target, float tol = 0.08f) => Mathf.Abs(v - target) < tol;

        /// <summary>
        /// Apply loaded art to a world-space material (e.g. exploration quad). Uses <see cref="Sprite.textureRect"/> UVs when needed.
        /// </summary>
        public static void ApplyImageToMaterial(Material mat, NexusGuiImage img, Color colorWhenEmpty)
        {
            if (mat == null)
                return;

            if (img.IsEmpty)
            {
                mat.mainTexture = null;
                mat.color = colorWhenEmpty;
                mat.mainTextureScale = Vector2.one;
                mat.mainTextureOffset = Vector2.zero;
                return;
            }

            if (img.Texture != null)
            {
                mat.mainTexture = img.Texture;
                mat.mainTextureScale = Vector2.one;
                mat.mainTextureOffset = Vector2.zero;
                mat.color = Color.white;
                return;
            }

            if (img.Sprite != null)
            {
                var t = img.Sprite.texture;
                mat.mainTexture = t;
                var tr = img.Sprite.textureRect;
                float tw = t.width;
                float th = t.height;
                mat.mainTextureScale = new Vector2(tr.width / tw, tr.height / th);
                mat.mainTextureOffset = new Vector2(tr.x / tw, tr.y / th);
                mat.color = Color.white;
            }
        }

        static Material[] _sharedWorldOreChipMats;

        /// <summary>
        /// Shared world materials for mine bonus quads (yield 1–3). Matches HUD ore chip Resources paths.
        /// </summary>
        public static Material GetSharedWorldOreChipMaterial(int mineYield)
        {
            if (mineYield < 1)
                return null;

            int key = mineYield > 3 ? 3 : mineYield;

            if (_sharedWorldOreChipMats == null)
                _sharedWorldOreChipMats = new Material[4];

            if (_sharedWorldOreChipMats[key] != null)
                return _sharedWorldOreChipMats[key];

            NexusGuiImage img = key switch
            {
                1 => Load("Sprites/OreChip1", "Sprites/Ore_Chip_1", "Sprites/Ore Chip 1"),
                2 => Load("Sprites/OreChip2", "Sprites/Ore_Chip_2", "Sprites/Ore Chip 2"),
                3 => Load("Sprites/OreChip3", "Sprites/Ore_Chip_3", "Sprites/Ore Chip 3"),
                _ => default
            };

            var m = new Material(Shader.Find("Sprites/Default"));
            ApplyImageToMaterial(m, img, new Color(1f, 0.92f, 0.25f, 0.95f));
            _sharedWorldOreChipMats[key] = m;
            return m;
        }
    }
}
