using UnityEngine;

namespace NexusGame
{
    /// <summary>
    /// Shared IMGUI scale for menus and HUD — same padded panel and uniform scale as the tile / deploy modal
    /// (<see cref="GetPaddedModalPanelGuiRect"/> + <see cref="TileInfoModalPanelScale"/>). Fonts add
    /// <see cref="TileInfoDeviceTextMultiplier"/> via <see cref="ImGuiFontScale"/>.
    /// </summary>
    public static class GameUiScale
    {
        public static Rect GetPaddedModalPanelGuiRect()
        {
            const float marginRef = 14f;
            float s = Screen.width >= Screen.height
                ? Mathf.Min(Screen.width / 1920f, Screen.height / 1080f)
                : Mathf.Min(Screen.width / 1080f, Screen.height / 1920f);
            float margin = marginRef * s;
            float marginTop = margin + 28f * s;
            return new Rect(margin, marginTop, Screen.width - margin * 2f, Screen.height - marginTop - margin);
        }

        /// <summary>
        /// Uniform scale to 1920×1080 (landscape) or 1080×1920 (portrait) logical panel minus margins.
        /// </summary>
        public static float TileInfoModalPanelScale(Rect panel)
        {
            const float refLandW = 1920f - 28f;
            const float refLandH = 1080f - 28f;
            if (panel.width >= panel.height)
                return Mathf.Min(panel.width / refLandW, panel.height / refLandH);
            return Mathf.Min(panel.width / refLandH, panel.height / refLandW);
        }

        public static float TileInfoDeviceTextMultiplier()
        {
            float shortSide = Mathf.Min(Screen.width, Screen.height);
            return Mathf.Lerp(0.98f, 1.14f, Mathf.InverseLerp(720f, 1600f, shortSide));
        }

        public static int TileInfoScaledFont(float designSize, float panelScale, int minSize)
        {
            float px = designSize * panelScale * TileInfoDeviceTextMultiplier();
            return Mathf.Max(minSize, Mathf.RoundToInt(px));
        }

        /// <summary>
        /// IMGUI layout scale — same as tile-info modal <c>S()</c>: <see cref="TileInfoModalPanelScale"/> on the
        /// padded panel only (no touch floor). Keeps positions stable vs screen size like <see cref="DrawCenterBuyDeployModal"/>.
        /// </summary>
        public static float ImGuiHudScale()
        {
            Rect panel = GetPaddedModalPanelGuiRect();
            return TileInfoModalPanelScale(panel);
        }

        /// <summary>
        /// IMGUI font scale — panel uniform scale × device text boost (tile-info <see cref="TileInfoScaledFont"/>).
        /// </summary>
        public static float ImGuiFontScale()
        {
            Rect panel = GetPaddedModalPanelGuiRect();
            return TileInfoModalPanelScale(panel) * TileInfoDeviceTextMultiplier();
        }

        /// <summary>
        /// Battle overlay layout scale — uniform scale for the battle <paramref name="panel"/> (same idea as tile-info <c>scale</c>).
        /// </summary>
        public static float BattleHudUiScale(Rect panel)
        {
            return TileInfoModalPanelScale(panel);
        }
    }
}
