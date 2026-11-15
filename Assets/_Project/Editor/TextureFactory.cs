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
        public static Texture2D Moon() => NasaMoonAlbedo() ?? GetOrCreate("Moon_Albedo", 512, 256, MoonPixel, TextureWrapMode.Repeat);
        public static Texture2D Stars() => GetOrCreate("Stars_Panorama", 2048, 1024, null, TextureWrapMode.Clamp, StarsTexture);
        /// <summary>Tierra en alta resolución para las fotografías desde órbita.</summary>
        public static Texture2D EarthPhoto() => GetOrCreate("Earth_Photo_Albedo", 2048, 1024, EarthPixel, TextureWrapMode.Repeat);
        /// <summary>Paneles de los monitores de la sala de control: 0 telemetría · 1 traza terrestre · 2 estado · 3 trayectoria.</summary>
        public static Texture2D Dashboard(int variant) =>
            GetOrCreate($"UI_Dashboard{variant}", 512, 320, null, TextureWrapMode.Repeat, (w, h) => DashboardTexture(w, h, variant));

        private static Texture2D DashboardTexture(int w, int h, int variant)
        {
            var px = new Color[w * h];
            var bg = new Color(0.02f, 0.05f, 0.1f, 1f);
            for (int i = 0; i < px.Length; i++) px[i] = bg;
            void Dot(int x, int y, Color c, int r = 1)
            {
                for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int xx = (x + dx + w) % w, yy = y + dy;
                    if (yy >= 0 && yy < h) px[yy * w + xx] = c;
                }
            }
            void Rect(int x0, int y0, int x1, int y1, Color c)
            {
                for (int y = Mathf.Max(0, y0); y < Mathf.Min(h, y1); y++)
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(w, x1); x++) px[y * w + x] = c;
            }
            var rng = new System.Random(100 + variant);
            Color header = new Color(0.05f, 0.16f, 0.32f), text = new Color(0.55f, 0.7f, 0.85f);
            Rect(0, h - 26, w, h, header);
            for (int k = 0; k < 5; k++) Rect(10 + k * 70, h - 18, 10 + k * 70 + 30 + rng.Next(30), h - 10, text);
            switch (variant)
            {
                case 0:   // telemetría: rejilla y tres curvas
                    for (int x = 0; x < w; x += 32) Rect(x, 0, x + 1, h - 26, new Color(0.06f, 0.12f, 0.2f));
                    for (int y = 0; y < h - 26; y += 32) Rect(0, y, w, y + 1, new Color(0.06f, 0.12f, 0.2f));
                    var cols = new[] { new Color(0.3f, 0.85f, 1f), new Color(0.35f, 1f, 0.5f), new Color(1f, 0.75f, 0.2f) };
                    for (int c = 0; c < 3; c++)
                    for (int x = 0; x < w; x++)
                    {
                        float u = x / (float)w * 2f * Mathf.PI;
                        float v = 0.5f + 0.25f * Mathf.Sin(u * (c + 1) + c) + 0.06f * Mathf.Sin(u * 9f + c * 2f);
                        Dot(x, Mathf.RoundToInt(v * (h - 40)) + 6, cols[c]);
                    }
                    break;
                case 1:   // traza terrestre sobre un mapa
                    for (int y = 0; y < h - 26; y++)
                    for (int x = 0; x < w; x++)
                    {
                        var e = EarthPixel((x + 0.5f) / w, (y + 0.5f) / (h - 26));
                        px[y * w + x] = new Color(e.r * 0.35f + 0.01f, e.g * 0.45f + 0.03f, e.b * 0.6f + 0.06f, 1f);
                    }
                    for (int x = 0; x < w; x++)
                    {
                        float v = 0.5f + 0.32f * Mathf.Sin((x / (float)w) * 4f * Mathf.PI + 0.6f);
                        if ((x / 4) % 2 == 0) Dot(x, Mathf.RoundToInt(v * (h - 26)), new Color(1f, 0.6f, 0.15f));
                    }
                    foreach (var (sx, sy) in new[] { (0.27f, 0.62f), (0.48f, 0.66f), (0.86f, 0.3f) })
                        Dot(Mathf.RoundToInt(sx * w), Mathf.RoundToInt(sy * (h - 26)), new Color(0.4f, 1f, 0.5f), 4);
                    break;
                case 2:   // estado de subsistemas
                    for (int r = 0; r < 9; r++)
                    {
                        int y = h - 50 - r * 30;
                        Rect(14, y, 14 + 120 + rng.Next(80), y + 10, text);
                        float st = (float)rng.NextDouble();
                        var c = st < 0.75f ? new Color(0.3f, 0.85f, 0.45f) : st < 0.93f ? new Color(1f, 0.75f, 0.2f) : new Color(1f, 0.35f, 0.3f);
                        Rect(270, y - 2, 330, y + 12, c);
                        Rect(350, y + 2, 350 + rng.Next(140), y + 8, new Color(0.25f, 0.55f, 1f));
                    }
                    break;
                default:  // trayectoria en "8" (Tierra-Luna)
                    for (int k = 0; k < 2400; k++)
                    {
                        float t = k / 2400f * 2f * Mathf.PI;
                        float x = Mathf.Sin(t) / (1f + Mathf.Cos(t) * Mathf.Cos(t)) * 0.42f + 0.5f;
                        float y = Mathf.Sin(t) * Mathf.Cos(t) / (1f + Mathf.Cos(t) * Mathf.Cos(t)) * 0.7f + 0.45f;
                        Dot(Mathf.RoundToInt(x * w), Mathf.RoundToInt(y * (h - 26)), t < Mathf.PI ? new Color(0.7f, 0.55f, 1f) : new Color(1f, 0.65f, 0.25f));
                    }
                    for (int k = 0; k < 400; k++)
                    {
                        float a = k / 400f * 2f * Mathf.PI;
                        Dot(Mathf.RoundToInt(0.08f * w + Mathf.Cos(a) * 18), Mathf.RoundToInt(0.45f * (h - 26) + Mathf.Sin(a) * 18), new Color(0.3f, 0.6f, 1f));
                        Dot(Mathf.RoundToInt(0.92f * w + Mathf.Cos(a) * 9), Mathf.RoundToInt(0.45f * (h - 26) + Mathf.Sin(a) * 9), new Color(0.8f, 0.8f, 0.8f));
                    }
                    break;
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            return tex;
        }

        /// <summary>Gajos alternados de un paracaídas (u da la vuelta a la cúpula); con banda de ventilación cerca del ápice.</summary>
        public static Texture2D ChuteStripes(string name, int gores, Color a, Color b) =>
            GetOrCreate(name, gores * 8, 64, (u, v) =>
            {
                int gore = Mathf.FloorToInt(u * gores) % gores;
                var c = gore % 2 == 0 ? a : b;
                if (v < 0.08f) c *= 0.55f;                    // ventilación del ápice (sombra)
                float seam = Mathf.Abs(u * gores - Mathf.Round(u * gores));
                if (seam < 0.04f) c *= 0.82f;                 // costuras entre gajos
                c.a = 1f;
                return c;
            }, TextureWrapMode.Repeat);

        /// <summary>Degradado de cielo diurno sobre el mar (v = 0 abajo, 1 cénit).</summary>
        public static Texture2D SkyGradient() => GetOrCreate("Sky_Gradient", 4, 256, (u, v) =>
        {
            Color horizon = new Color(0.78f, 0.86f, 0.93f), mid = new Color(0.45f, 0.65f, 0.88f), zenith = new Color(0.16f, 0.36f, 0.72f);
            if (v < 0.5f) return Color.Lerp(new Color(0.55f, 0.66f, 0.76f), horizon, v / 0.5f);
            float k = (v - 0.5f) / 0.5f;
            return k < 0.25f ? Color.Lerp(horizon, mid, k / 0.25f) : Color.Lerp(mid, zenith, (k - 0.25f) / 0.75f);
        }, TextureWrapMode.Clamp);

        public static Texture2D SoftDot() => GetOrCreate("FX_SoftDot", 64, 64, DotPixel, TextureWrapMode.Clamp);

        /// <summary>Luna detallada para las fotografías de exploración (cráteres con borde y fondo, mares) + mapa de normales.</summary>
        public static Texture2D MoonPhoto() => NasaMoonAlbedo() ?? GetOrCreate("Moon_Photo_Albedo", 2048, 1024, null, TextureWrapMode.Repeat, (w, h) => MoonPhotoTexture(w, h, false));
        public static Texture2D MoonPhotoNormal()
        {
            var nasa = NasaMoonNormal();
            if (nasa != null) return nasa;
            var tex = GetOrCreate("Moon_Photo_Normal", 2048, 1024, null, TextureWrapMode.Repeat, (w, h) => MoonPhotoTexture(w, h, true));
            string path = AssetDatabase.GetAssetPath(tex);
            if (AssetImporter.GetAtPath(path) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ───────────── Luna real de NASA (CGI Moon Kit, LRO) ─────────────

        /// <summary>Carpeta con las texturas de NASA/GSFC (dominio público, ver README.md en la carpeta).</summary>
        public const string NasaFolder = Folder + "/NASA";

        /// <summary>
        /// Color real de la Luna (LROC WAC, 4096×2048, proyección equirectangular con longitud 0 en el centro).
        /// Si no está, se usa la Luna procedural (el proyecto compila igual sin descargas).
        /// </summary>
        public static Texture2D NasaMoonAlbedo()
        {
            string path = NasaFolder + "/Moon_LRO_Albedo_4k.jpg";
            if (AssetImporter.GetAtPath(path) is not TextureImporter ti) return null;
            if (ti.maxTextureSize != 4096 || ti.anisoLevel != 8 || ti.wrapModeV != TextureWrapMode.Clamp)
            {
                ti.maxTextureSize = 4096;
                ti.anisoLevel = 8;
                ti.mipmapEnabled = true;
                ti.wrapModeU = TextureWrapMode.Repeat;     // la longitud da la vuelta; los polos no
                ti.wrapModeV = TextureWrapMode.Clamp;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>
        /// Mapa de normales calculado con el relieve real (LOLA, ldem_4, en coma flotante) por ArtSource/NASA/moon_normals.py:
        /// pendiente correcta según la latitud y exageración ×5 para que los cráteres se lean con luz rasante.
        /// </summary>
        public static Texture2D NasaMoonNormal()
        {
            string path = NasaFolder + "/Moon_LRO_Normal_2k.png";
            if (AssetImporter.GetAtPath(path) is not TextureImporter ti) return null;
            if (ti.textureType != TextureImporterType.NormalMap || ti.convertToNormalmap || ti.wrapModeV != TextureWrapMode.Clamp)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.convertToNormalmap = false;
                ti.wrapModeU = TextureWrapMode.Repeat;
                ti.wrapModeV = TextureWrapMode.Clamp;
                ti.maxTextureSize = 2048;
                ti.anisoLevel = 4;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static float[] moonHeight;

        private static float[] MoonHeightField(int w, int h)
        {
            if (moonHeight != null && moonHeight.Length == w * h) return moonHeight;
            var rng = new System.Random(77);
            const int count = 420;
            var centers = new Vector3[count];
            var radii = new float[count];
            for (int i = 0; i < count; i++)
            {
                centers[i] = RandomUnit(rng);
                // Muchos cráteres pequeños y pocos grandes (ley de potencias).
                radii[i] = Mathf.Lerp(0.012f, 0.16f, Mathf.Pow((float)rng.NextDouble(), 3.2f));
            }
            var hf = new float[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Vector3 n = Sphere((x + 0.5f) / w, (y + 0.5f) / h);
                float height = (Fbm(n * 6f + Vector3.one * 3f, 5) - 0.5f) * 0.25f;
                for (int i = 0; i < count; i++)
                {
                    float dot = Vector3.Dot(n, centers[i]);
                    float r = radii[i];
                    if (dot < 1f - r * r * 1.4f) continue;           // fuera de 1,6 radios (aprox. de ángulo pequeño)
                    float d = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) / r;
                    float depth = Mathf.Min(1f, r * 9f);
                    if (d < 1f) height -= depth * 0.9f * (1f - d * d);
                    height += depth * 0.35f * Mathf.Exp(-Mathf.Pow((d - 1f) / 0.18f, 2f));
                }
                hf[y * w + x] = height;
            }
            return moonHeight = hf;
        }

        private static Vector3 RandomUnit(System.Random rng)
        {
            float z = (float)rng.NextDouble() * 2f - 1f, t = (float)rng.NextDouble() * 2f * Mathf.PI, r = Mathf.Sqrt(1f - z * z);
            return new Vector3(r * Mathf.Cos(t), z, r * Mathf.Sin(t));
        }

        private static Texture2D MoonPhotoTexture(int w, int h, bool normalMap)
        {
            var hf = MoonHeightField(w, h);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (normalMap)
                {
                    float hl = hf[y * w + (x - 1 + w) % w], hr = hf[y * w + (x + 1) % w];
                    float hd = hf[Mathf.Max(0, y - 1) * w + x], hu = hf[Mathf.Min(h - 1, y + 1) * w + x];
                    const float k = 6f;
                    var n = new Vector3(-(hr - hl) * k, -(hu - hd) * k, 1f).normalized;
                    px[i] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                    continue;
                }
                Vector3 p = Sphere((x + 0.5f) / w, (y + 0.5f) / h);
                float maria = Mathf.InverseLerp(0.53f, 0.43f, Fbm(p * 1.6f + Vector3.one * 2f, 4));
                float g = 0.52f + 0.18f * (Fbm(p * 9f + Vector3.one * 7f, 4) - 0.5f) - maria * 0.22f;
                g += Mathf.Clamp(hf[i], -0.6f, 0.4f) * 0.18f;           // bordes claros, fondos algo más oscuros
                g = Mathf.Clamp01(g);
                px[i] = new Color(g * 1.02f, g, g * 0.95f, 1f);
            }
            tex.SetPixels(px);
            return tex;
        }

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
