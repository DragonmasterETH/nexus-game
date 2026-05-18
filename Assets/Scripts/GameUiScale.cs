using UnityEngine;

namespace NexusGame
{
    /// <summary>
    /// Shared IMGUI scale for menus and HUD — padded panel (<see cref="GetPaddedModalPanelGuiRect"/>) plus a
    /// short-side readability floor so phones stay legible. Fonts use <see cref="ImGuiFontScale"/>.
    /// </summary>
    public static class GameUiScale
    {
        public const float ImGuiReferenceWidth = 900f;
        public const float ImGuiReferenceHeight = 1300f;
        const float GlobalImGuiFontFactor = 0.86f;
        /// <summary>Soft clamp only — keeps extreme DPI / simulator sizes sane without pinning phones and tablets to one size.</summary>
        const float ImGuiFontScaleMin = 0.28f;
        const float ImGuiFontScaleMax = 2.55f;

        /// <summary>
        /// Canvas-scaler style uniform scale for the IMGUI virtual reference frame (900x1300).
        /// </summary>
        public static float ImGuiCanvasScale()
        {
            return Mathf.Min(Screen.width / ImGuiReferenceWidth, Screen.height / ImGuiReferenceHeight);
        }

        /// <summary>
        /// Letterboxed frame matching the 900x1300 IMGUI reference aspect, centered on screen.
        /// </summary>
        public static Rect GetImGuiCanvasRect()
        {
            float s = ImGuiCanvasScale();
            float w = ImGuiReferenceWidth * s;
            float h = ImGuiReferenceHeight * s;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;
            return new Rect(x, y, w, h);
        }

        public static Rect GetPaddedModalPanelGuiRect()
        {
            const float marginRef = 14f;
            Rect canvas = GetImGuiCanvasRect();
            float s = ImGuiCanvasScale();
            float margin = marginRef * s;
            float marginTop = margin + 28f * s;
            return new Rect(
                canvas.x + margin,
                canvas.y + marginTop,
                canvas.width - margin * 2f,
                canvas.height - marginTop - margin);
        }

        /// <summary>
        /// Near full-screen panel (5% inset on each side, 90% of width and height). Matches the tile-info / deploy
        /// modal frame — full physical screen width unlike the letterboxed <see cref="GetPaddedModalPanelGuiRect"/>.
        /// </summary>
        public static Rect GetFullscreenModalStylePanelGuiRect()
        {
            return new Rect(
                Screen.width * 0.05f,
                Screen.height * 0.05f,
                Screen.width * 0.90f,
                Screen.height * 0.90f);
        }

        /// <summary>
        /// Full display rect for IMGUI (origin top-left): edge-to-edge width and height. Use for the battle overlay
        /// so layout fills the screen instead of the 90% inset modal frame.
        /// </summary>
        public static Rect GetFullBleedScreenGuiRect()
        {
            return new Rect(0f, 0f, Screen.width, Screen.height);
        }

        /// <summary>
        /// Same horizontal insets and bottom margin as <see cref="GetPaddedModalPanelGuiRect"/>, but the rect is
        /// anchored to the <b>physical top</b> of the screen (not the vertically centered letterbox canvas). Use for
        /// the main gameplay HUD so the top bar sits under the status/safe inset instead of floating mid-screen.
        /// </summary>
        public static Rect GetMainHudPanelGuiRect()
        {
            const float marginRef = 14f;
            Rect canvas = GetImGuiCanvasRect();
            float s = ImGuiCanvasScale();
            float margin = marginRef * s;
            float marginTop = margin + 28f * s;
            float x = canvas.x + margin;
            float w = canvas.width - margin * 2f;
            float y = marginTop;
            float h = Screen.height - marginTop - margin;
            return new Rect(x, y, w, Mathf.Max(1f, h));
        }

        /// <summary>
        /// Same width as <see cref="GetPaddedModalPanelGuiRect"/>, but height is capped (~58% of screen) and the
        /// block is centered vertically. (Centering a nearly full-screen rect only shifts Y by a few dozen px — not
        /// enough to read as “centered”; casualty pick needs a shorter panel.)
        /// </summary>
        public static Rect GetBattleCasualtyModalPanelGuiRect()
        {
            Rect pad = GetPaddedModalPanelGuiRect();
            float m = pad.x;
            float w = pad.width;
            float hCap = Screen.height * 0.58f;
            float h = Mathf.Min(pad.height, hCap);
            float y = (Screen.height - h) * 0.5f;
            y = Mathf.Clamp(y, m, Mathf.Max(m, Screen.height - h - m));
            return new Rect(m, y, w, h);
        }

        /// <summary>
        /// Uniform scale to 1920×1080 (landscape) or 1080×1920 (portrait) logical panel minus margins.
        /// </summary>
        public static float TileInfoModalPanelScale(Rect panel)
        {
            // Keep existing API but tie it to the IMGUI reference-frame scaler.
            return ImGuiCanvasScale();
        }

        public static float TileInfoDeviceTextMultiplier()
        {
            float shortSide = Mathf.Min(Screen.width, Screen.height);
            return Mathf.Lerp(0.9f, 1.04f, Mathf.InverseLerp(720f, 1600f, shortSide));
        }

        /// <summary>
        /// Extra device-driven text fit so labels visibly shrink on small resolutions and grow on large displays.
        /// </summary>
        static float DeviceAdaptiveTextScale()
        {
            float shortSide = Mathf.Min(Screen.width, Screen.height);
            // Stronger range than the legacy multiplier so fixed min-font clamps are less likely to pin all text.
            return Mathf.Lerp(0.76f, 1.36f, Mathf.InverseLerp(640f, 2160f, shortSide));
        }

        public static int TileInfoScaledFont(float designSize, float panelScale, int minSize)
        {
            float px = designSize * panelScale * TileInfoDeviceTextMultiplier() * DeviceAdaptiveTextScale() * GlobalImGuiFontFactor;
            return Mathf.Max(minSize, Mathf.RoundToInt(px));
        }

        /// <summary>
        /// Shared IMGUI font helper for bounded "best fit to screen" sizing.
        /// </summary>
        public static int ImGuiScaledFont(float designSize, int minSize, int maxSize, float multiplier = 1f)
        {
            float px = designSize * ImGuiFontScale() * Mathf.Max(0.1f, multiplier);
            int n = Mathf.RoundToInt(px);
            return Mathf.Clamp(Mathf.Max(minSize, n), minSize, maxSize);
        }

        /// <summary>
        /// Chooses the largest font size in [<paramref name="minSize"/>, <paramref name="maxSize"/>] such that
        /// <paramref name="content"/> fits in the box. Uses a temporary copy of <paramref name="prototype"/> so the
        /// original style is never mutated during measurement.
        /// </summary>
        public static int ComputeBestFitFontSize(GUIStyle prototype, GUIContent content, float maxWidth, float maxHeight,
            int minSize, int maxSize, bool wordWrap)
        {
            if (content == null || string.IsNullOrEmpty(content.text))
                return Mathf.Clamp(minSize, minSize, maxSize);

            maxWidth = Mathf.Max(1f, maxWidth);
            maxHeight = Mathf.Max(1f, maxHeight);
            minSize = Mathf.Max(1, minSize);
            maxSize = Mathf.Max(minSize, maxSize);

            var style = new GUIStyle(prototype) { wordWrap = wordWrap };

            int lo = minSize;
            int hi = maxSize;
            int best = minSize;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                style.fontSize = mid;
                bool fits;
                if (wordWrap)
                {
                    float h = style.CalcHeight(content, maxWidth);
                    fits = h <= maxHeight + 1f;
                }
                else
                {
                    Vector2 sz = style.CalcSize(content);
                    fits = sz.x <= maxWidth + 1f && sz.y <= maxHeight + 1f;
                }

                if (fits)
                {
                    best = mid;
                    lo = mid + 1;
                }
                else
                    hi = mid - 1;
            }

            return best;
        }

        /// <summary>
        /// Same as <see cref="ComputeBestFitFontSize"/> but builds <see cref="GUIContent"/> from <paramref name="text"/>.
        /// </summary>
        public static int ComputeBestFitFontSize(GUIStyle prototype, string text, float maxWidth, float maxHeight,
            int minSize, int maxSize, bool wordWrap)
        {
            return ComputeBestFitFontSize(prototype, new GUIContent(text ?? ""), maxWidth, maxHeight, minSize, maxSize,
                wordWrap);
        }

        /// <summary>
        /// Full-bleed panels use physical <see cref="Screen.width"/> while <see cref="ImGuiCanvasScale"/> is tied to the
        /// letterboxed <see cref="ImGuiReferenceWidth"/> frame — text would stay tiny on ultrawide displays without this boost.
        /// </summary>
        public static float FullBleedPanelWidthToCanvasWidthRatio(Rect panelGuiRect)
        {
            float canvasW = Mathf.Max(1f, GetImGuiCanvasRect().width);
            float pw = panelGuiRect.width > 0.5f ? panelGuiRect.width : canvasW;
            return Mathf.Clamp(pw / canvasW, 0.88f, 2.6f);
        }

        /// <summary>
        /// IMGUI font for full-bleed overlays (e.g. battle screen): same recipe as <see cref="TileInfoScaledFont"/> plus
        /// <see cref="FullBleedPanelWidthToCanvasWidthRatio"/> so type tracks physical panel width.
        /// </summary>
        public static int FullBleedImGuiScaledFont(float designSize, Rect panelGuiRect, int minSize, int maxSize = 48)
        {
            float ratio = FullBleedPanelWidthToCanvasWidthRatio(panelGuiRect);
            // Dampen width-ratio gain so text scales up on larger screens without overrunning short rows.
            float ratioDamped = Mathf.Lerp(1f, ratio, 0.6f);
            float px = designSize * ImGuiCanvasScale() * TileInfoDeviceTextMultiplier() * DeviceAdaptiveTextScale() * GlobalImGuiFontFactor * ratioDamped;
            int n = Mathf.RoundToInt(px);
            return Mathf.Clamp(Mathf.Max(minSize, n), minSize, maxSize);
        }

        /// <summary>
        /// Minimum scale so main HUD / menus stay legible and tappable on phones. Pure
        /// <see cref="TileInfoModalPanelScale"/> alone can be ~0.35–0.5 on narrow portrait, which makes buttons unreadable.
        /// </summary>
        static float HudReadabilityScaleFloor()
        {
            float shortSide = Mathf.Min(Screen.width, Screen.height);
            return Mathf.Clamp(shortSide / 460f, 0.86f, 1.12f);
        }

        /// <summary>
        /// IMGUI layout scale — <see cref="TileInfoModalPanelScale"/> on the padded panel, with a readability floor on small devices.
        /// </summary>
        public static float ImGuiHudScale()
        {
            return ImGuiCanvasScale();
        }

        /// <summary>
        /// IMGUI font scale — panel scale × device text boost, with the same floor as layout so labels match button size.
        /// </summary>
        public static float ImGuiFontScale()
        {
            // Width-responsive bump: on wide monitors the letterboxed canvas can be narrower than the physical panel.
            float canvasW = Mathf.Max(1f, GetImGuiCanvasRect().width);
            float widthRatio = Mathf.Clamp(Screen.width / canvasW, 1f, 2.4f);
            float widthBoost = Mathf.Lerp(1f, widthRatio, 0.35f);
            float raw = ImGuiCanvasScale() * TileInfoDeviceTextMultiplier() * DeviceAdaptiveTextScale() * GlobalImGuiFontFactor * widthBoost;
            return Mathf.Clamp(raw, ImGuiFontScaleMin, ImGuiFontScaleMax);
        }

        /// <summary>
        /// Battle overlay layout scale — canvas scale plus a bump on tall/narrow phones so battle controls stay tappable.
        /// </summary>
        public static float BattleHudUiScale(Rect panel)
        {
            float canvas = ImGuiCanvasScale();
            float shortSide = Mathf.Min(Screen.width, Screen.height);
            float phoneBoost = Mathf.Lerp(1f, 1.32f, Mathf.InverseLerp(680f, 1200f, shortSide));
            float heightBoost = panel.height > 1f
                ? Mathf.Clamp(panel.height / Mathf.Max(1f, ImGuiReferenceHeight * canvas), 0.92f, 1.28f)
                : 1f;
            return canvas * phoneBoost * heightBoost;
        }
    }
}
