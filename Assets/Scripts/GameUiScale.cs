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

        public static int TileInfoScaledFont(float designSize, float panelScale, int minSize)
        {
            float px = designSize * panelScale * TileInfoDeviceTextMultiplier() * GlobalImGuiFontFactor;
            return Mathf.Max(minSize, Mathf.RoundToInt(px));
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
            return ImGuiCanvasScale() * TileInfoDeviceTextMultiplier() * GlobalImGuiFontFactor;
        }

        /// <summary>
        /// Battle overlay layout scale — panel uniform scale with the same readability floor as the main HUD.
        /// </summary>
        public static float BattleHudUiScale(Rect panel)
        {
            return ImGuiCanvasScale();
        }
    }
}
