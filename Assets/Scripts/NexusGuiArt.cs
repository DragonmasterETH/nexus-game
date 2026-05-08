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

        /// <summary>Largest rect inside <paramref name="container"/> that preserves native aspect (letterboxed).</summary>
        public Rect RectAspectFit(Rect container)
        {
            if (IsEmpty || container.width < 0.001f || container.height < 0.001f)
                return container;
            float ar = AspectRatio;
            float cw = container.width;
            float ch = container.height;
            float boxAr = cw / ch;
            if (ar > boxAr)
            {
                float h = cw / ar;
                return new Rect(container.x, container.y + (ch - h) * 0.5f, cw, h);
            }

            float w = ch * ar;
            return new Rect(container.x + (cw - w) * 0.5f, container.y, w, ch);
        }

        public void DrawAspectFit(Rect container)
        {
            Draw(RectAspectFit(container));
        }

        /// <summary>
        /// Smallest rect aligned with <paramref name="container"/> that preserves aspect and fully covers it
        /// (like CSS <c>background-size: cover</c>) — no letterboxing on 9:16 portrait when art is 16:9, etc.
        /// </summary>
        public Rect RectAspectCover(Rect container)
        {
            if (IsEmpty || container.width < 0.001f || container.height < 0.001f)
                return container;
            float ar = AspectRatio;
            float cw = container.width;
            float ch = container.height;
            float boxAr = cw / ch;
            if (ar >= boxAr)
            {
                float w = ch * ar;
                return new Rect(container.x + (cw - w) * 0.5f, container.y, w, ch);
            }

            float h = cw / ar;
            return new Rect(container.x, container.y + (ch - h) * 0.5f, cw, h);
        }

        public void DrawAspectCover(Rect container)
        {
            Draw(RectAspectCover(container));
        }

        /// <summary>Fills <paramref name="container"/> exactly (may stretch non-uniformly); for full-screen framed art on portrait.</summary>
        public void DrawStretchFill(Rect container)
        {
            if (IsEmpty || container.height <= 0f)
                return;
            if (Sprite != null)
            {
                var t = Sprite.texture;
                var tr = Sprite.textureRect;
                float tw = t.width;
                float th = t.height;
                var uv = new Rect(tr.x / tw, tr.y / th, tr.width / tw, tr.height / th);
                GUI.DrawTextureWithTexCoords(container, t, uv, true);
            }
            else
            {
                GUI.DrawTexture(container, Texture, ScaleMode.StretchToFill, true);
            }
        }

        /// <summary>Same as <see cref="DrawStretchFill"/> mirrored horizontally (e.g. left-side army chrome).</summary>
        public void DrawStretchFillFlippedH(Rect container)
        {
            DrawFlippedH(container);
        }

        public void DrawFlippedHAspectFit(Rect container)
        {
            DrawFlippedH(RectAspectFit(container));
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

        /// <summary>Same as <see cref="Draw"/> but mirrored horizontally (defender blade in clash UI).</summary>
        public void DrawFlippedH(Rect r)
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
                uv = new Rect(uv.xMax, uv.y, -uv.width, uv.height);
                GUI.DrawTextureWithTexCoords(r, t, uv, true);
            }
            else
            {
                GUI.DrawTextureWithTexCoords(r, Texture, new Rect(1f, 0f, -1f, 1f), true);
            }
        }
    }

    public static class NexusGuiArt
    {
        /// <summary>Main HUD quick-reference button (Resources <c>Sprites/Info.png</c>).</summary>
        public static NexusGuiImage LoadMainHudInfoIcon()
        {
            return Load(
                "Sprites/Info",
                "Sprites/UI/Info",
                "Sprites/Icons/Info",
                "Sprites/icons/Info");
        }

        /// <summary>Main HUD settings button (Resources <c>Sprites/Settings.png</c>).</summary>
        public static NexusGuiImage LoadMainHudSettingsIcon()
        {
            return Load(
                "Sprites/Settings",
                "Sprites/UI/Settings",
                "Sprites/Icons/Settings",
                "Sprites/icons/Settings");
        }

        /// <summary>Full-screen / modal frame art (Resources, e.g. &quot;Sprites/Tile info screen&quot;).</summary>
        public static NexusGuiImage LoadTileInfoScreenBackground()
        {
            return Load(
                "Sprites/Tile info screen",
                "Sprites/TileInfoScreen",
                "Sprites/UI/Tile info screen",
                "Sprites/UI/TileInfoScreen",
                "Sprites/Tile_Info_Screen");
        }

        /// <summary>Battle overlay panel frame (<c>Battle Screen.png</c> under Resources).</summary>
        public static NexusGuiImage LoadBattleScreenBackground()
        {
            return Load(
                "Sprites/Battle Screen",
                "Sprites/BattleScreen",
                "Sprites/UI/Battle Screen",
                "Sprites/UI/BattleScreen");
        }

        /// <summary>Hand / card pile modal panel frame (<c>Card screen.png</c> under Resources).</summary>
        public static NexusGuiImage LoadCardScreenBackground()
        {
            return Load(
                "Sprites/Card screen",
                "Sprites/CardScreen",
                "Sprites/UI/Card screen",
                "Sprites/UI/CardScreen");
        }

        /// <summary>Casualty pick modals — battle + Dragon firebolt (<c>Casualty screen.png</c> under Resources).</summary>
        public static NexusGuiImage LoadCasualtyScreenBackground()
        {
            return Load(
                "Sprites/Casualty screen",
                "Sprites/CasualtyScreen",
                "Sprites/UI/Casualty screen",
                "Sprites/UI/CasualtyScreen");
        }

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

        /// <summary>Fungoid art per seat (e.g. Sprites/Units/Fungus Red).</summary>
        public static NexusGuiImage LoadFungoidForPlayer(PlayerState owner)
        {
            string c = DragonColorSuffix(owner);
            return Load(
                $"Sprites/Units/Fungus {c}",
                $"Sprites/units/Fungus {c}",
                $"Sprites/Units/Fungus_{c}",
                $"Sprites/units/Fungus_{c}",
                $"Sprites/Units/Fungus{c}",
                $"Sprites/Units/Fungoid_{c}",
                $"Sprites/Units/Fungoid {c}");
        }

        /// <summary>Ribbon / fallback when no owner.</summary>
        public static NexusGuiImage LoadFungoidLegendIcon()
        {
            return Load(
                "Sprites/Units/Fungus Blue",
                "Sprites/units/Fungus Blue",
                "Sprites/Units/Fungus_Blue",
                "Sprites/Units/FungusBlue",
                "Sprites/Units/Fungoid");
        }

        /// <summary>Human art per seat (e.g. Sprites/Units/Human Red).</summary>
        public static NexusGuiImage LoadHumanForPlayer(PlayerState owner)
        {
            string c = DragonColorSuffix(owner);
            return Load(
                $"Sprites/Units/Human {c}",
                $"Sprites/units/Human {c}",
                $"Sprites/Units/Human_{c}",
                $"Sprites/units/Human_{c}",
                $"Sprites/Units/Human{c}",
                $"Sprites/Units/Colonist_{c}",
                $"Sprites/Units/Colonist {c}");
        }

        public static NexusGuiImage LoadHumanLegendIcon()
        {
            return Load(
                "Sprites/Units/Human Blue",
                "Sprites/units/Human Blue",
                "Sprites/Units/Human_Blue",
                "Sprites/Units/Human",
                "Sprites/Units/Colonist");
        }

        /// <summary>Lava Leaper art per seat (e.g. Sprites/Units/Leaper Red).</summary>
        public static NexusGuiImage LoadLavaLeaperForPlayer(PlayerState owner)
        {
            string c = DragonColorSuffix(owner);
            return Load(
                $"Sprites/Units/Leaper {c}",
                $"Sprites/units/Leaper {c}",
                $"Sprites/Units/Leaper_{c}",
                $"Sprites/units/Leaper_{c}",
                $"Sprites/Units/Leaper{c}",
                $"Sprites/Units/LavaLeaper_{c}",
                $"Sprites/Units/LavaLeaper {c}",
                $"Sprites/Units/Lava_Leaper_{c}");
        }

        public static NexusGuiImage LoadLavaLeaperLegendIcon()
        {
            return Load(
                "Sprites/Units/Leaper Blue",
                "Sprites/units/Leaper Blue",
                "Sprites/Units/Leaper_Blue",
                "Sprites/Units/LavaLeaper",
                "Sprites/Units/Lava_Leaper");
        }

        /// <summary>Crystalline art per seat (e.g. Sprites/Units/Crystal Red).</summary>
        public static NexusGuiImage LoadCrystallineForPlayer(PlayerState owner)
        {
            string c = DragonColorSuffix(owner);
            return Load(
                $"Sprites/Units/Crystal {c}",
                $"Sprites/units/Crystal {c}",
                $"Sprites/Units/Crystal_{c}",
                $"Sprites/units/Crystal_{c}",
                $"Sprites/Units/Crystal{c}",
                $"Sprites/Units/Crystalline_{c}",
                $"Sprites/Units/Crystalline {c}");
        }

        public static NexusGuiImage LoadCrystallineLegendIcon()
        {
            return Load(
                "Sprites/Units/Crystal Blue",
                "Sprites/units/Crystal Blue",
                "Sprites/Units/Crystal_Blue",
                "Sprites/Units/Crystal",
                "Sprites/Units/Crystalline");
        }

        /// <summary>
        /// Uncolored generic unit art for battle initiative ribbon / banners (<c>Human.png</c>, <c>Crystal.png</c>, … under <c>Resources/Sprites/Units</c>).
        /// </summary>
        public static NexusGuiImage LoadBattleBannerNeutralIcon(UnitType type)
        {
            return type switch
            {
                UnitType.Human => Load("Sprites/Units/Human", "Sprites/units/Human"),
                UnitType.Fungoid => Load("Sprites/Units/Fungus", "Sprites/units/Fungus"),
                UnitType.Crystalline => Load("Sprites/Units/Crystal", "Sprites/units/Crystal"),
                UnitType.RockStrider => Load("Sprites/Units/Strider", "Sprites/units/Strider"),
                UnitType.LavaLeaper => Load("Sprites/Units/Leaper", "Sprites/units/Leaper"),
                UnitType.RubiumDragon => Load("Sprites/Units/Dragon", "Sprites/units/Dragon"),
                _ => default
            };
        }

        /// <summary>Desaturated / empty-slot HUD art (e.g. &quot;Human Gray&quot;, &quot;Leaper Gray&quot;).</summary>
        public static NexusGuiImage LoadGrayUnitIcon(UnitType type)
        {
            return type switch
            {
                UnitType.Human => Load(
                    "Sprites/Units/Human Gray",
                    "Sprites/Units/Human_Gray",
                    "Sprites/units/Human Gray",
                    "Sprites/Units/HumanGray",
                    "Sprites/Units/human gray"),
                UnitType.Fungoid => Load(
                    "Sprites/Units/Fungus Gray",
                    "Sprites/Units/Fungus_Gray",
                    "Sprites/Units/Fungoid Gray",
                    "Sprites/units/Fungus Gray",
                    "Sprites/Units/FungoidGray"),
                UnitType.Crystalline => Load(
                    "Sprites/Units/Crystal Gray",
                    "Sprites/Units/Crystal_Gray",
                    "Sprites/Units/Crystalline Gray",
                    "Sprites/units/Crystal Gray",
                    "Sprites/Units/CrystallineGray"),
                UnitType.RockStrider => Load(
                    "Sprites/Units/Strider Gray",
                    "Sprites/Units/Strider_Gray",
                    "Sprites/Units/Rock Strider Gray",
                    "Sprites/Units/RockStrider Gray",
                    "Sprites/units/Strider Gray",
                    "Sprites/Units/StriderGray"),
                UnitType.LavaLeaper => Load(
                    "Sprites/Units/Leaper Gray",
                    "Sprites/Units/Leaper_Gray",
                    "Sprites/Units/Lava Leaper Gray",
                    "Sprites/Units/LavaLeaper Gray",
                    "Sprites/units/Leaper Gray",
                    "Sprites/Units/LeaperGray"),
                UnitType.RubiumDragon => Load(
                    "Sprites/Units/Dragon Gray",
                    "Sprites/Units/Dragon_Gray",
                    "Sprites/Units/Rubium Dragon Gray",
                    "Sprites/Units/RubiumDragon Gray",
                    "Sprites/units/Dragon Gray",
                    "Sprites/Units/DragonGray"),
                _ => default
            };
        }

        /// <summary>Seat-colored sword art (e.g. Resources &quot;Sprites/Sword Red&quot;, &quot;Sword_Blue&quot;).</summary>
        public static NexusGuiImage LoadSwordForPlayer(PlayerState owner)
        {
            string c = DragonColorSuffix(owner);
            return Load(
                $"Sprites/Sword {c}",
                $"Sprites/Sword_{c}",
                $"Sprites/Swords/Sword {c}",
                $"Sprites/Swords/Sword_{c}",
                $"Sprites/sword {c}",
                $"Sprites/Sword{c}");
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
