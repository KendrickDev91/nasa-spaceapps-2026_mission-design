using System.IO;
using UnityEditor;
using UnityEngine;

namespace MissionDesign.Editor
{
    /// <summary>
    /// Texturas procedurales propias (sin descargas ni derechos de terceros): Tierra (continentes,
    /// hielo y nubes), Luna (cráteres), cielo estrellado panorámico con banda de la Vía Láctea y
    /// una partícula suave. Se guardan como PNG en Assets/_Project/Art/Textures.
    /// </summary>
    public static class TextureFactory
    {
        public const string Folder = "Assets/_Project/Art/Textures";

        public static Texture2D Earth() => GetOrCreate("Earth_Albedo", 1024, 512, EarthPixel, TextureWrapMode.Repeat);
        public static Texture2D Moon() => GetOrCreate("Moon_Albedo", 512, 256, MoonPixel, TextureWrapMode.Repeat);
        public static Texture2D Stars() => GetOrCreate("Stars_Panorama", 2048, 1024, null, TextureWrapMode.Clamp, StarsTexture);
        public static Texture2D SoftDot() => GetOrCreate("FX_SoftDot", 64, 64, DotPixel, TextureWrapMode.Clamp);

        private delegate Color PixelFn(float u, float v);

        private static Texture2D GetOrCreate(string name, int w, int h, PixelFn fn, TextureWrapMode wrap, System.Func<int, int, Texture2D> custom = null)
        {
            string path = $"{Folder}/{name}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            Directory.CreateDirectory(Folder);
            Texture2D tex;
            if (custom != null) tex = custom(w, h);
            else
            {
                tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var px = new Color[w * h];
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = fn((x + 0.5f) / w, (y + 0.5f) / h);
                tex.SetPixels(px);
            }
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is TextureImporter ti)
            {
                ti.wrapMode = wrap;
                ti.alphaIsTransparency = name == "FX_SoftDot";
                ti.mipmapEnabled = name != "Stars_Panorama";
                ti.maxTextureSize = 2048;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Ruido 3D aproximado a partir de Perlin 2D: continuo sobre la esfera (sin costura).
        private static float Noise3(Vector3 p) =>
            (Mathf.PerlinNoise(p.x, p.y) + Mathf.PerlinNoise(p.y + 31.7f, p.z) + Mathf.PerlinNoise(p.z + 71.3f, p.x)) / 3f;

        private static float Fbm(Vector3 p, int octaves)
        {
            float sum = 0f, amp = 0.5f, freq = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * Noise3(p * freq);
                norm += amp;
                amp *= 0.5f;
                freq *= 2.1f;
            }
            return sum / norm;
        }

        private static Vector3 Sphere(float u, float v)
        {
            float lon = (u - 0.5f) * 2f * Mathf.PI, lat = (v - 0.5f) * Mathf.PI;
            return new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
        }

        private static Color EarthPixel(float u, float v)
        {
            Vector3 p = Sphere(u, v) * 2.2f + new Vector3(10f, 10f, 10f);
            float land = Fbm(p, 6);
            float lat = Mathf.Abs(v - 0.5f) * 2f;
            Color ocean = Color.Lerp(new Color(0.02f, 0.09f, 0.3f), new Color(0.05f, 0.25f, 0.5f), Mathf.InverseLerp(0.35f, 0.52f, land));
            Color c = ocean;
            if (land > 0.52f)
            {
                float dry = Fbm(p * 1.7f + Vector3.one * 5f, 4);
                Color green = new Color(0.16f, 0.36f, 0.14f), desert = new Color(0.62f, 0.52f, 0.33f), rock = new Color(0.42f, 0.38f, 0.32f);
                c = Color.Lerp(green, desert, Mathf.InverseLerp(0.45f, 0.65f, dry) * (1f - lat));
                c = Color.Lerp(c, rock, Mathf.InverseLerp(0.62f, 0.75f, land));
            }
            if (lat > 0.82f + (Fbm(p * 3f, 3) - 0.5f) * 0.15f) c = new Color(0.92f, 0.95f, 0.98f);
            float clouds = Mathf.InverseLerp(0.5f, 0.68f, Fbm(p * 2.6f + new Vector3(3f, 7f, 1f), 5));
            return Color.Lerp(c, Color.white, clouds * 0.75f);
        }

        private static Color MoonPixel(float u, float v)
        {
            Vector3 p = Sphere(u, v) * 3f + new Vector3(5f, 5f, 5f);
            float g = 0.35f + 0.35f * Fbm(p, 6);
            float maria = Mathf.InverseLerp(0.55f, 0.45f, Fbm(p * 0.5f + Vector3.one * 2f, 3));
            g -= maria * 0.12f;
            float crater = Fbm(p * 6f, 2);
            if (crater > 0.68f) g += 0.12f;
            else if (crater > 0.64f) g -= 0.06f;
            return new Color(g, g, g * 0.97f);
        }

        private static Color DotPixel(float u, float v)
        {
            float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            float a = Mathf.Clamp01(1f - d);
            return new Color(1f, 1f, 1f, a * a);
        }

        private static Texture2D StarsTexture(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                // Banda tenue de la Vía Láctea inclinada.
                float band = Mathf.Exp(-Mathf.Pow((v - 0.5f - 0.18f * Mathf.Sin(u * 2f * Mathf.PI)) / 0.07f, 2f));
                float dust = Fbm(Sphere(u, v) * 4f + Vector3.one * 9f, 4);
                float glow = band * (0.05f + 0.07f * dust);
                px[y * w + x] = new Color(0.008f + glow * 0.9f, 0.01f + glow, 0.025f + glow * 1.2f, 1f);
            }
            var rng = new System.Random(2026);
            for (int i = 0; i < 9000; i++)
            {
                int x = rng.Next(w), y = rng.Next(h);
                float b = Mathf.Pow((float)rng.NextDouble(), 3f) * 0.9f + 0.1f;
                float hue = (float)rng.NextDouble();
                var star = hue < 0.15f ? new Color(1f, 0.8f, 0.6f) : hue > 0.85f ? new Color(0.7f, 0.8f, 1f) : Color.white;
                px[y * w + x] = Color.Lerp(px[y * w + x], star, b);
                if (b > 0.75f)
                    foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        int xx = (x + dx + w) % w, yy = Mathf.Clamp(y + dy, 0, h - 1);
                        px[yy * w + xx] = Color.Lerp(px[yy * w + xx], star, b * 0.35f);
                    }
            }
            tex.SetPixels(px);
            return tex;
        }
    }
}
