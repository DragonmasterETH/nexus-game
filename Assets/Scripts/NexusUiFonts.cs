using TMPro;
using UnityEngine;

namespace NexusGame
{
    /// <summary>
    /// Seabox for IMGUI: loads the same source <see cref="Font"/> as <c>Resources/Fonts/Seabox. SDF</c> (TMP cannot draw in IMGUI).
    /// </summary>
    public static class NexusUiFonts
    {
        public const string TmpFontResourcePath = "Fonts/Seabox. SDF";
        public const string LegacyFontResourcePath = "Fonts/Seabox.";

        static Font _imguiFont;
        static bool _imguiFontTried;
        static bool _skinApplied;

        /// <summary>Underlying TrueType font paired with the project's TMP Seabox SDF asset.</summary>
        public static Font ImguiFont()
        {
            if (!_imguiFontTried)
            {
                _imguiFontTried = true;
                var tmp = Resources.Load<TMP_FontAsset>(TmpFontResourcePath);
                if (tmp != null && tmp.sourceFontFile != null)
                    _imguiFont = tmp.sourceFontFile;
                if (_imguiFont == null)
                    _imguiFont = Resources.Load<Font>(LegacyFontResourcePath);
            }

            return _imguiFont;
        }

        public static void ApplyTo(GUIStyle style)
        {
            Font f = ImguiFont();
            if (f == null || style == null)
                return;
            style.font = f;
        }

        /// <summary>Call once per IMGUI frame before drawing so labels, buttons, boxes, and fields use Seabox (including digits).</summary>
        public static void EnsureImGuiSkinFonts()
        {
            Font f = ImguiFont();
            if (f == null)
                return;

            WarmupHudGlyphs(f);

            if (_skinApplied && GUI.skin != null && GUI.skin.label.font == f)
                return;

            var skin = GUI.skin;
            if (skin == null)
                return;

            ApplyFontToStyle(skin.label);
            ApplyFontToStyle(skin.button);
            ApplyFontToStyle(skin.box);
            ApplyFontToStyle(skin.window);
            ApplyFontToStyle(skin.textField);
            ApplyFontToStyle(skin.textArea);
            ApplyFontToStyle(skin.toggle);
            _skinApplied = true;
        }

        static void ApplyFontToStyle(GUIStyle style)
        {
            Font f = ImguiFont();
            if (f == null || style == null)
                return;
            style.font = f;
        }

        /// <summary>Preload digits and common HUD punctuation so IMGUI number labels don't fall back to Arial.</summary>
        static void WarmupHudGlyphs(Font font)
        {
            const string glyphs =
                "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-+×/·:!?.,'\"()[]PST";
            font.RequestCharactersInTexture(glyphs, 32, FontStyle.Normal);
        }
    }
}
