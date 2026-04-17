using UnityEngine;

namespace NexusGame
{
    /// <summary>
    /// Shared IMGUI scale for menus and HUD — padded panel (<see cref="GetPaddedModalPanelGuiRect"/>) plus a
    /// short-side readability floor so phones stay legible. Fonts use <see cref="ImGuiFontScale"/>.
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
            Rect panel = GetPaddedModalPanelGuiRect();
            return Mathf.Max(TileInfoModalPanelScale(panel), HudReadabilityScaleFloor());
        }

        /// <summary>
        /// IMGUI font scale — panel scale × device text boost, with the same floor as layout so labels match button size.
        /// </summary>
        public static float ImGuiFontScale()
        {
            Rect panel = GetPaddedModalPanelGuiRect();
            float refF = TileInfoModalPanelScale(panel) * TileInfoDeviceTextMultiplier();
            return Mathf.Max(refF, HudReadabilityScaleFloor() * TileInfoDeviceTextMultiplier());
        }

        /// <summary>
        /// Battle overlay layout scale — panel uniform scale with the same readability floor as the main HUD.
        /// </summary>
        public static float BattleHudUiScale(Rect panel)
        {
            return Mathf.Max(TileInfoModalPanelScale(panel), HudReadabilityScaleFloor());
        }
    }
}
