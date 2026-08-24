using UnityEditor;
using UnityEngine;

namespace Party.EditorTools
{
    /// <summary>
    /// Generates smooth gradient textures as assets.
    ///
    /// Hard-edged geometry is what made the first backdrop look cheap: flat wedges meet
    /// at knife edges, alias badly, and z-fight. A soft gradient has no edges to alias,
    /// costs one texture, and is most of what separates "programmer art" from "classy".
    /// </summary>
    public static class GradientTextures
    {
        const string Dir = "Assets/_Party/Art";

        /// <summary>Radial vignette-style gradient: rich centre falling to deep edges.</summary>
        public static Texture2D Radial(string name, Color inner, Color outer, int size = 512,
                                       float power = 1.35f)
        {
            string path = $"{Dir}/{name}.png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
            float maxD = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), c) / maxD);
                    tex.SetPixel(x, y, Color.Lerp(inner, outer, Mathf.Pow(d, power)));
                }
            tex.Apply();
            return Save(tex, path);
        }

        /// <summary>Vertical gradient, optionally fading out in alpha - used for the UI scrim.</summary>
        public static Texture2D Horizontal(string name, Color left, Color right, int w = 256)
        {
            string path = $"{Dir}/{name}.png";
            var tex = new Texture2D(w, 4, TextureFormat.RGBA32, false);
            for (int x = 0; x < w; x++)
            {
                Color c = Color.Lerp(left, right, Mathf.SmoothStep(0f, 1f, x / (float)(w - 1)));
                for (int y = 0; y < 4; y++) tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return Save(tex, path);
        }

        /// <summary>A soft round dot - out-of-focus light, not a hard confetti chip.</summary>
        public static Texture2D Bokeh(string name, int size = 128)
        {
            string path = $"{Dir}/{name}.png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
            float maxD = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c) / maxD;
                    float a = Mathf.Clamp01(1f - d);
                    a = Mathf.SmoothStep(0f, 1f, a);
                    a = a * a * 0.9f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Save(tex, path);
        }

        /// <summary>
        /// A glossy rounded-rect "bubble": soft corners, a bright sheen across the top and
        /// a subtle darkening at the bottom, plus a rim highlight.
        ///
        /// Exported as a 9-sliced SPRITE so one texture stretches to any button size
        /// without the corners distorting - that is what keeps it looking moulded rather
        /// than smeared.
        /// </summary>
        public static Sprite Bubble(string name, int w = 256, int h = 128, int radius = 46)
        {
            string path = $"{Dir}/{name}.png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // Rounded-rect coverage, antialiased at the edge.
                    float dx = Mathf.Max(0f, Mathf.Max(radius - x, x - (w - 1 - radius)));
                    float dy = Mathf.Max(0f, Mathf.Max(radius - y, y - (h - 1 - radius)));
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float cover = Mathf.Clamp01(radius - dist + 0.5f);
                    if (dx == 0f && dy == 0f) cover = 1f;

                    float t = y / (float)(h - 1);            // 0 bottom .. 1 top
                    // Gloss: bright band across the upper third, gentle shade below.
                    float sheen = Mathf.SmoothStep(0.52f, 1f, t) * 0.55f;
                    float shade = (1f - Mathf.SmoothStep(0f, 0.55f, t)) * 0.22f;
                    float v = 1f + sheen - shade;

                    // Rim light around the very edge so it reads as a moulded surface.
                    float rim = Mathf.Clamp01(1f - Mathf.Abs(radius - dist) / 3f) * 0.35f;
                    v += rim;

                    tex.SetPixel(x, y, new Color(v, v, v, cover));
                }
            tex.Apply();

            System.IO.Directory.CreateDirectory(Dir);
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Bilinear;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            // 9-slice borders just outside the corner radius.
            imp.spriteBorder = new Vector4(radius + 4, radius + 4, radius + 4, radius + 4);
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Texture2D Save(Texture2D tex, string path)
        {
            System.IO.Directory.CreateDirectory(Dir);
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Default;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Bilinear;
            imp.mipmapEnabled = true;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Unlit, transparent, textured - for backdrops and soft particles.</summary>
        public static Material UnlitTex(string name, Texture2D tex, Color tint, bool additive = false)
        {
            string path = $"{Dir}/{name}.mat";
            Material m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 1f : 0f);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
