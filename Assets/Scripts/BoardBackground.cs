using UnityEngine;

namespace NexusGame
{
    /// <summary>
    /// Full-screen ground plane under the hex board, textured from Resources (Sprites/background).
    /// </summary>
    public static class BoardBackground
    {
        const string GoName = "BoardBackground";

        public enum Presentation
        {
            Game,
            Menu
        }

        public static void Remove()
        {
            var existing = GameObject.Find(GoName);
            if (existing != null)
                Object.Destroy(existing);
        }

        public static void EnsureLoaded(Presentation presentation = Presentation.Game)
        {
            Remove();

            var img = NexusGuiArt.Load("Sprites/background", "Sprites/Background");
            if (img.IsEmpty)
                return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = GoName;
            Object.Destroy(go.GetComponent<Collider>());

            go.transform.SetPositionAndRotation(new Vector3(0f, -0.05f, 0f), Quaternion.Euler(90f, 0f, 0f));
            // Large plane so the ground still fills the view when the camera is pinched out; UV fit (below)
            // shrinks how much of the quad the artwork uses so the whole image reads “zoomed out”.
            const float planeExtent = 200f;
            go.transform.localScale = new Vector3(planeExtent, planeExtent, 1f);

            var rend = go.GetComponent<MeshRenderer>();
            float uvFit = presentation == Presentation.Menu
                ? ResolveUvCenterFitForMenu(Camera.main, planeExtent)
                : ResolveUvCenterFit(Camera.main, planeExtent);
            var mat = CreateUnlitMaterial(img, uvCenterFit: uvFit);
            if (mat == null)
            {
                Object.Destroy(go);
                return;
            }

            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }

        /// <summary>
        /// Maps the full background art onto a world-sized patch that roughly matches the camera’s ground footprint
        /// so portrait phones see the starscape instead of a tight crop on the planet center.
        /// </summary>
        static float ResolveUvCenterFit(Camera cam, float planeExtent)
        {
            const float fallback = 0.12f;
            if (cam == null || planeExtent < 1f)
                return fallback;

            float height = Mathf.Max(0.5f, cam.transform.position.y);
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            float vFovRad = cam.fieldOfView * Mathf.Deg2Rad;
            float hFovRad = 2f * Mathf.Atan(Mathf.Tan(vFovRad * 0.5f) * aspect);
            float visibleGroundWidth = height * Mathf.Tan(hFovRad * 0.5f) * 2f;

            // Slightly wider than the view so the full illustration (stars + planet) fits inside the frame.
            float fit = visibleGroundWidth * 1.18f / planeExtent;

            // Portrait phones need extra pull-back; wide screens can show a bit more detail.
            if (aspect < 0.72f)
                fit *= 0.82f;

            return Mathf.Clamp(fit, 0.045f, 0.38f);
        }

        /// <summary>Menus use a slightly wider framing so the starscape matches the main-menu camera height.</summary>
        static float ResolveUvCenterFitForMenu(Camera cam, float planeExtent)
        {
            float fit = ResolveUvCenterFit(cam, planeExtent);
            return Mathf.Clamp(fit * 0.9f, 0.045f, 0.38f);
        }

        /// <param name="uvCenterFit">
        /// Fraction of the quad (0–1) used for the full image on each axis, centered (letterbox).
        /// Smaller = more “zoomed out” (whole art smaller on the ground patch, wider starscape visible).
        /// </param>
        static Material CreateUnlitMaterial(NexusGuiImage img, float uvCenterFit)
        {
            Texture mainTex = null;
            var atlasScale = Vector2.one;
            var atlasOffset = Vector2.zero;

            if (img.Texture != null)
                mainTex = img.Texture;
            else if (img.Sprite != null)
            {
                var sp = img.Sprite;
                mainTex = sp.texture;
                var tr = sp.textureRect;
                float tw = sp.texture.width;
                float th = sp.texture.height;
                if (tw > 0f && th > 0f)
                {
                    atlasScale = new Vector2(tr.width / tw, tr.height / th);
                    atlasOffset = new Vector2(tr.x / tw, tr.y / th);
                }
            }

            if (mainTex == null)
                return null;

            // Letterboxing samples slightly outside the sprite rect; only safe for a dedicated full-image texture.
            bool fullImage =
                img.Texture != null ||
                (atlasScale.x >= 0.998f && atlasScale.y >= 0.998f && atlasOffset.sqrMagnitude < 1e-6f);

            Vector2 scale;
            Vector2 offset;
            if (fullImage)
            {
                float f = Mathf.Clamp(uvCenterFit, 0.08f, 1f);
                // tex = (meshUV / f - (1-f)/(2f)) * atlasScale + atlasOffset
                scale = atlasScale / f;
                offset = atlasOffset - atlasScale * ((1f - f) / (2f * f));
                mainTex.wrapMode = TextureWrapMode.Clamp;
            }
            else
            {
                scale = atlasScale;
                offset = atlasOffset;
            }

            Shader sh = Shader.Find("Unlit/Texture");
            if (sh == null)
                sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null)
                sh = Shader.Find("Sprites/Default");
            if (sh == null)
                return null;

            var mat = new Material(sh);
            if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTexture("_BaseMap", mainTex);
                mat.SetTextureScale("_BaseMap", scale);
                mat.SetTextureOffset("_BaseMap", offset);
            }
            else
            {
                mat.mainTexture = mainTex;
                mat.mainTextureScale = scale;
                mat.mainTextureOffset = offset;
            }

            return mat;
        }
    }
}
