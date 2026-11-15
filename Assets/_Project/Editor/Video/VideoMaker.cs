using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MissionDesign.UI;
using TMPro;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.UI;

namespace MissionDesign.Editor.Video
{
    /// <summary>
    /// Montaje del video promocional a partir de los planos que graba la prueba VideoShoot (Recordings/Plates):
    /// cortes al ritmo de la voz (edge-tts), subtítulos palabra por palabra, título gancho, marca, tarjeta final,
    /// música original sintetizada con "ducking" bajo la voz y efectos del propio juego. Codifica MP4 (H.264 + AAC)
    /// con el MediaEncoder de Unity: 16:9 para YouTube y 9:16 para TikTok, Reels, Facebook y Shorts.
    /// Uso: -executeMethod MissionDesign.Editor.Video.VideoMaker.MakeAll
    /// </summary>
    public static class VideoMaker
    {
        private const int Fps = 30;
        private const int Rate = 48000;
        private const string PlatesDir = "Recordings/Plates";
        private const string VoiceDir = "Assets/_Project/Video/Voice";
        public static string OutDir = "Recordings/Video";

        public const string Handle = "@KendrickDev91";
        public const string GitHub = "github.com/KendrickDev91";

        /// <summary>Full: plano a pantalla completa · Fit: plano horizontal dentro del vertical · Crop: recorte vertical que recorre la interfaz (pan.x → pan.y).</summary>
        private enum Layout { Full, Fit, Crop }

        private sealed class Shot
        {
            public string plate; public bool v; public int from; public float weight = 1f; public float zoom = 1.07f;
            public Vector2 pan; public Layout layout = Layout.Full;
        }

        private sealed class Sfx { public string id; public float at; public float gain = 0.6f; }

        private sealed class Seg
        {
            public string line; public float hold = 0.25f; public float intensity = 0.6f;
            public List<Shot> shots = new List<Shot>(); public List<Sfx> sfx = new List<Sfx>();
            public string title; public bool endCard;
            // calculados
            public float start, voiceStart, duration; public Line voice;
        }

        [Serializable] private sealed class Word { public string w; public float t0, t1; }
        [Serializable] private sealed class Line { public string text; public Word[] words; [NonSerialized] public float[] pcm; [NonSerialized] public float length; }

        private static Shot S(string plate, bool v, int from, float weight = 1f, float zoom = 1.07f, Layout layout = Layout.Full, float panX = 0f, float panY = 0f) =>
            new Shot { plate = plate, v = v, from = from, weight = weight, zoom = zoom, layout = layout, pan = new Vector2(panX, panY) };

        // ───────────── Guiones de montaje ─────────────

        private static List<Seg> Horizontal() => new List<Seg>
        {
            new Seg { line = "hook", hold = 0.35f, intensity = 0.35f, title = "¿A LA LUNA… Y DE VUELTA?",
                shots = { S("flight", false, 318, 0.5f, 1.12f), S("splash", false, 232, 0.5f, 1.1f) } },
            new Seg { line = "intro", intensity = 0.5f, shots = { S("design", false, 0, 1f, 1.12f) } },
            new Seg { line = "roles", intensity = 0.55f, shots = { S("design", false, 140, 0.72f, 1.1f), S("consensus", false, 0, 0.28f, 1.15f, panX: -0.06f, panY: -0.22f) } },
            new Seg { line = "datos", intensity = 0.6f, shots = { S("design", false, 300, 0.6f, 1.45f, panX: -0.3f, panY: 0.08f), S("consensus", false, 40, 0.4f, 1.3f, panX: -0.06f, panY: -0.25f) } },
            new Seg { line = "launch", hold = 2.0f, intensity = 1f, shots = { S("launch", false, 20, 1f, 1.08f) },
                sfx = { new Sfx { id = "countdown", at = 0f, gain = 0.45f }, new Sfx { id = "roar", at = 1.5f, gain = 0.9f } } },
            new Seg { line = "room", hold = 1.0f, intensity = 1f, shots = { S("room", false, 15, 1f, 1.06f) }, sfx = { new Sfx { id = "applause", at = 0f, gain = 0.7f } } },
            new Seg { line = "eight", intensity = 0.9f, shots = { S("flight", false, 0, 1f, 1.1f, panX: -0.05f) }, sfx = { new Sfx { id = "chime", at = 0.2f, gain = 0.5f } } },
            new Seg { line = "moon", intensity = 0.7f, shots = { S("flight", false, 300, 1f, 1.12f) },
                sfx = { new Sfx { id = "shutter", at = 0.9f }, new Sfx { id = "shutter", at = 2.2f } } },
            new Seg { line = "splash", hold = 1.4f, intensity = 1f, shots = { S("splash", false, 230, 0.58f, 1.06f), S("splash", false, 352, 0.42f, 1.05f) },
                sfx = { new Sfx { id = "chute", at = 0f, gain = 0.6f }, new Sfx { id = "splash", at = -0.42f, gain = 0.85f } } },
            new Seg { line = "analysis", intensity = 0.8f, shots = { S("analysis", false, 0, 1f, 1.06f) } },
            new Seg { line = "center", intensity = 0.85f, shots = { S("center", false, 0, 1f, 1.06f) } },
            new Seg { line = "cta_h", hold = 2.2f, intensity = 0.9f, endCard = true, shots = { S("splash", false, 470, 1f, 1.1f) } },
        };

        private static List<Seg> Vertical() => new List<Seg>
        {
            new Seg { line = "hook", hold = 0.35f, intensity = 0.35f, title = "¿A LA LUNA…\nY DE VUELTA?",
                shots = { S("flight", true, 318, 0.5f, 1.12f), S("splash", true, 232, 0.5f, 1.1f) } },
            new Seg { line = "intro", intensity = 0.5f, shots = { S("design", false, 0, 1f, 1.0f, Layout.Crop, panX: 0.0f, panY: 0.42f) } },
            new Seg { line = "roles", intensity = 0.55f, shots = { S("design", false, 140, 0.72f, 1.0f, Layout.Crop, panX: 0.42f, panY: 1.0f), S("consensus", false, 0, 0.28f, 1.0f, Layout.Crop, panX: 0.47f, panY: 0.47f) } },
            new Seg { line = "launch", hold = 2.0f, intensity = 1f, shots = { S("launch", true, 20, 1f, 1.08f) },
                sfx = { new Sfx { id = "countdown", at = 0f, gain = 0.45f }, new Sfx { id = "roar", at = 1.5f, gain = 0.9f } } },
            new Seg { line = "room", hold = 1.0f, intensity = 1f, shots = { S("room", true, 15, 1f, 1.06f) }, sfx = { new Sfx { id = "applause", at = 0f, gain = 0.7f } } },
            new Seg { line = "eight", intensity = 0.9f, shots = { S("flight", true, 0, 1f, 1.08f) }, sfx = { new Sfx { id = "chime", at = 0.2f, gain = 0.5f } } },
            new Seg { line = "moon", intensity = 0.7f, shots = { S("flight", true, 300, 1f, 1.12f) },
                sfx = { new Sfx { id = "shutter", at = 0.9f }, new Sfx { id = "shutter", at = 2.2f } } },
            new Seg { line = "splash", hold = 1.4f, intensity = 1f, shots = { S("splash", true, 230, 0.58f, 1.06f), S("splash", true, 352, 0.42f, 1.05f) },
                sfx = { new Sfx { id = "chute", at = 0f, gain = 0.6f }, new Sfx { id = "splash", at = -0.42f, gain = 0.85f } } },
            new Seg { line = "cta_v", hold = 2.2f, intensity = 0.9f, endCard = true, shots = { S("splash", true, 470, 1f, 1.1f) } },
        };

        [MenuItem("NASA SpaceApps/Video/Make All")]
        public static void MakeAll()
        {
            Make(false);
            Make(true);
        }

        public static void MakeHorizontal() => Make(false);
        public static void MakeVertical() => Make(true);

        // ───────────── Montaje ─────────────

        private static void Make(bool vertical)
        {
            int W = vertical ? 1080 : 1920, H = vertical ? 1920 : 1080;
            var segs = vertical ? Vertical() : Horizontal();
            float t = 0f;
            foreach (var s in segs)
            {
                s.voice = LoadLine(s.line);
                s.start = t;
                s.voiceStart = t + 0.12f;
                s.duration = 0.12f + s.voice.length + s.hold;
                t += s.duration;
            }
            float total = t;
            int frames = Mathf.CeilToInt(total * Fps);
            Debug.Log($"[Video] {(vertical ? "9:16" : "16:9")} {total:F1} s, {frames} fotogramas");

            var mix = MixAudio(segs, total);
            Directory.CreateDirectory(OutDir);
            string name = vertical ? "AstroByte_Shorts_Reels_TikTok_9x16" : "AstroByte_YouTube_16x9";
            string path = Path.Combine(OutDir, name + ".mp4");
            WriteSrt(segs, Path.Combine(OutDir, name + ".srt"));

            var rig = new Rig(W, H, vertical);
            var plateCount = new Dictionary<string, int>();
            var va = new VideoTrackAttributes { frameRate = new MediaRational(Fps), width = (uint)W, height = (uint)H, includeAlpha = false, bitRateMode = VideoBitrateMode.High };
            var aa = new AudioTrackAttributes { sampleRate = new MediaRational(Rate), channelCount = 2, language = "es" };
            int spf = Rate / Fps;
            var chunk = new NativeArray<float>(spf * 2, Allocator.Persistent);
            try
            {
                using (var enc = new MediaEncoder(path, va, aa))
                {
                    for (int f = 0; f < frames; f++)
                    {
                        float time = f / (float)Fps;
                        var seg = segs.Last(s => s.start <= time + 1e-4f);
                        rig.Compose(seg, time, total, segs, plateCount);
                        enc.AddFrame(rig.Read());
                        for (int i = 0; i < spf * 2; i++)
                        {
                            int k = f * spf * 2 + i;
                            chunk[i] = k < mix.Length ? mix[k] : 0f;
                        }
                        enc.AddSamples(chunk);
                        // Vista previa: un fotograma a mitad de cada segmento (para revisar el montaje sin reproducir el MP4).
                        int si = segs.IndexOf(seg);
                        if (f == Mathf.RoundToInt((seg.start + seg.duration * 0.55f) * Fps))
                        {
                            Directory.CreateDirectory(Path.Combine(OutDir, "preview"));
                            rig.SaveJpg(Path.Combine(OutDir, "preview", $"{name}_{si:D2}_{seg.line}.jpg"));
                        }
                        if (f % 300 == 0) Debug.Log($"[Video] {name}: {f}/{frames}");
                    }
                }
            }
            finally { rig.Dispose(); chunk.Dispose(); }
            Debug.Log($"[Video] Listo: {path} ({new FileInfo(path).Length / 1048576f:F1} MB)");
        }

        private static Line LoadLine(string id)
        {
            var line = JsonUtility.FromJson<Line>(File.ReadAllText(Path.Combine(VoiceDir, id + ".json")));
            string clipPath = $"{VoiceDir}/{id}.mp3";
            if (AssetImporter.GetAtPath(clipPath) is AudioImporter ai && ai.defaultSampleSettings.loadType != AudioClipLoadType.DecompressOnLoad)
            {
                var st = ai.defaultSampleSettings; st.loadType = AudioClipLoadType.DecompressOnLoad; ai.defaultSampleSettings = st; ai.SaveAndReimport();
            }
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            line.pcm = ToMono48k(clip);
            line.length = line.words.Length > 0 ? Mathf.Max(line.words.Last().t1 + 0.1f, line.pcm.Length / (float)Rate * 0.0f) : line.pcm.Length / (float)Rate;
            line.length = Mathf.Min(line.length, line.pcm.Length / (float)Rate);
            return line;
        }

        private static float[] ToMono48k(AudioClip clip)
        {
            clip.LoadAudioData();
            var raw = new float[clip.samples * clip.channels];
            clip.GetData(raw, 0);
            int n = clip.samples, ch = clip.channels;
            float ratio = clip.frequency / (float)Rate;
            int outN = Mathf.CeilToInt(n / ratio);
            var o = new float[outN];
            for (int i = 0; i < outN; i++)
            {
                float src = i * ratio;
                int a = Mathf.Min(n - 1, (int)src), b = Mathf.Min(n - 1, a + 1);
                float fa = 0f, fb = 0f;
                for (int c = 0; c < ch; c++) { fa += raw[a * ch + c]; fb += raw[b * ch + c]; }
                o[i] = Mathf.Lerp(fa, fb, src - a) / ch;
            }
            return o;
        }

        // ───────────── Subtítulos ─────────────

        /// <summary>Palabras del guion con su puntuación, alineadas con los tiempos de la voz.</summary>
        private static List<(string text, float t0, float t1)> Tokens(Line line)
        {
            var tokens = line.text.Split(' ').Where(x => x.Length > 0).ToList();
            var outList = new List<(string, float, float)>();
            int wi = 0;
            foreach (var tok in tokens)
            {
                string bare = new string(tok.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
                if (bare.Length == 0 && outList.Count > 0)
                {
                    var last = outList[outList.Count - 1];
                    outList[outList.Count - 1] = (last.Item1 + " " + tok, last.Item2, last.Item3);
                    continue;
                }
                var w = wi < line.words.Length ? line.words[wi] : line.words.LastOrDefault();
                outList.Add((tok, w != null ? w.t0 : 0f, w != null ? w.t1 : 0f));
                wi++;
            }
            return outList;
        }

        /// <summary>Frases cortas (máx. 3 palabras / 16 letras) que cortan en la puntuación, como en los videos virales.</summary>
        private static List<List<(string text, float t0, float t1)>> Chunks(Line line)
        {
            var chunks = new List<List<(string, float, float)>>();
            var cur = new List<(string, float, float)>();
            foreach (var tok in Tokens(line))
            {
                cur.Add(tok);
                int letters = cur.Sum(c => c.Item1.Length);
                bool punct = ",.:;?!…".IndexOf(tok.text[tok.text.Length - 1]) >= 0;
                if (cur.Count >= 3 || letters >= 16 || punct) { chunks.Add(cur); cur = new List<(string, float, float)>(); }
            }
            if (cur.Count > 0) chunks.Add(cur);
            return chunks;
        }

        private static void WriteSrt(List<Seg> segs, string path)
        {
            var sb = new StringBuilder();
            int n = 1;
            string Ts(float s) { var t = TimeSpan.FromSeconds(s); return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00},{t.Milliseconds:000}"; }
            var cues = new List<(float a, float b, string text)>();
            foreach (var s in segs)
                foreach (var c in Chunks(s.voice))
                    cues.Add((s.voiceStart + c[0].t0, s.voiceStart + c[c.Count - 1].t1 + 0.15f, string.Join(" ", c.Select(x => x.text))));
            for (int i = 0; i < cues.Count; i++)
            {
                float end = i + 1 < cues.Count ? Mathf.Min(cues[i].b, cues[i + 1].a - 0.01f) : cues[i].b;   // sin solaparse
                sb.AppendLine((n++).ToString());
                sb.AppendLine($"{Ts(cues[i].a)} --> {Ts(end)}");
                sb.AppendLine(cues[i].text);
                sb.AppendLine();
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        // ───────────── Audio: voz + música original + efectos ─────────────

        private static float[] MixAudio(List<Seg> segs, float total)
        {
            int n = Mathf.CeilToInt(total * Rate) + Rate;
            var voice = new float[n];
            var duck = new float[n];
            foreach (var s in segs)
            {
                int o = Mathf.RoundToInt(s.voiceStart * Rate);
                for (int i = 0; i < s.voice.pcm.Length && o + i < n; i++) voice[o + i] += s.voice.pcm[i];
                int a = Mathf.RoundToInt((s.voiceStart + (s.voice.words.Length > 0 ? s.voice.words[0].t0 : 0f)) * Rate);
                int b = Mathf.RoundToInt((s.voiceStart + s.voice.length + 0.15f) * Rate);
                for (int i = Mathf.Max(0, a); i < Mathf.Min(n, b); i++) duck[i] = 1f;
            }

            // Efectos del juego (Synth): mono 44,1 kHz → 48 kHz.
            var sfx = new float[n];
            foreach (var s in segs)
                foreach (var e in s.sfx)
                {
                    var clip = SfxClip(e.id);
                    if (clip == null) continue;
                    var pcm = ToMono48k(clip);
                    float at = e.at >= 0f ? s.start + e.at : s.start + s.duration * -e.at;
                    if (e.id == "splash") at = s.start + s.duration * 0.58f;
                    int o = Mathf.RoundToInt(at * Rate);
                    for (int i = 0; i < pcm.Length && o + i < n; i++) sfx[o + i] += pcm[i] * e.gain;
                }

            var music = Music(segs, total, n);
            var mix = new float[n * 2];
            float g = 1f;
            for (int i = 0; i < n; i++)
            {
                float target = duck[i] > 0f ? 0.3f : 1f;
                g += (target - g) * (target < g ? 0.0009f : 0.00012f);       // baja rápido, sube despacio
                float fade = Mathf.Clamp01((total - i / (float)Rate) / 1.6f) * Mathf.Clamp01(i / (Rate * 0.08f));
                float m = music[i] * g * 0.55f * fade;
                float v = voice[i] * 1.0f;
                float x = sfx[i] * fade;
                // Música algo abierta en estéreo; voz y efectos al centro. Limitador suave.
                mix[i * 2] = SoftClip(v + x + m * 0.92f + music[Mathf.Max(0, i - 480)] * g * 0.08f * fade);
                mix[i * 2 + 1] = SoftClip(v + x + m);
            }
            float peak = mix.Max(Mathf.Abs);
            float norm = peak > 0f ? 0.89f / peak : 1f;      // ≈ −1 dBFS
            for (int i = 0; i < mix.Length; i++) mix[i] *= norm;
            return mix;
        }

        private static float SoftClip(float x) => (float)Math.Tanh(x * 1.1f) / 1.1f;

        private static AudioClip SfxClip(string id)
        {
            switch (id)
            {
                case "countdown": return Synth.Countdown();
                case "roar": return Synth.ReentryRoar(3.4f);
                case "applause": return Synth.Applause(3.2f);
                case "chime": return Synth.Chime();
                case "shutter": return Synth.Shutter();
                case "chute": return Synth.ChuteSnap();
                case "splash": return Synth.Splash();
                default: return null;
            }
        }

        /// <summary>
        /// Música original (sin derechos de terceros): La menor, Am–F–C–G a 96 ppm. Capas según la intensidad del momento:
        /// colchón de acordes → arpegio → bajo → hi-hat → bombo → caja. Subida de tensión antes del despegue e impactos.
        /// </summary>
        private static float[] Music(List<Seg> segs, float total, int n)
        {
            var o = new float[n];
            const float bpm = 96f, beat = 60f / bpm, bar = beat * 4f;
            float[][] chords =
            {
                new[] { 110f, 220f, 261.63f, 329.63f },   // Am
                new[] { 87.31f, 174.61f, 220f, 261.63f }, // F
                new[] { 130.81f, 261.63f, 329.63f, 392f },// C
                new[] { 98f, 196f, 246.94f, 293.66f },    // G
            };
            var launch = segs.FirstOrDefault(s => s.line == "launch");
            float impact = launch != null ? launch.start + 1.5f : -10f;
            var splash = segs.FirstOrDefault(s => s.line == "splash");
            float splashHit = splash != null ? splash.start + splash.duration * 0.58f : -10f;
            var rng = new System.Random(7);
            float intensity = 0.3f;
            int segIndex = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                while (segIndex + 1 < segs.Count && t >= segs[segIndex + 1].start) segIndex++;
                float targetI = segs[segIndex].intensity;
                if (launch != null && t > launch.start && t < impact) targetI = 0.45f;      // tensión: se quitan los tambores
                intensity += (targetI - intensity) * 0.00004f;
                var ch = chords[(int)(t / bar) % 4];
                float beatPos = t % beat, barPos = t % bar;
                float s = 0f;

                // Colchón: acorde con leve desafinación (coro) y entrada suave por compás.
                float padEnv = 0.6f + 0.4f * Mathf.Clamp01(barPos / 0.6f);
                for (int k = 1; k < 4; k++)
                    s += (Mathf.Sin(2f * Mathf.PI * ch[k] * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * ch[k] * 1.004f * t)) * 0.05f * padEnv;

                // Arpegio en semicorcheas.
                if (intensity > 0.42f)
                {
                    float step = beat / 4f;
                    int idx = (int)(t / step);
                    float local = t - idx * step;
                    float f = ch[1 + idx % 3] * (idx % 8 >= 4 ? 2f : 1f);
                    float env = Mathf.Exp(-local * 18f);
                    s += (Mathf.Sin(2f * Mathf.PI * f * t) + 0.3f * Mathf.Sin(6f * Mathf.PI * f * t)) * 0.07f * env * Mathf.Clamp01((intensity - 0.42f) * 6f);
                }
                // Bajo en cada pulso.
                if (intensity > 0.58f)
                    s += Mathf.Sin(2f * Mathf.PI * ch[0] * t) * 0.16f * Mathf.Exp(-beatPos * 3f) * Mathf.Clamp01((intensity - 0.58f) * 6f);
                // Hi-hat en contratiempo.
                if (intensity > 0.7f)
                {
                    float off = (t + beat / 2f) % beat;
                    s += ((float)rng.NextDouble() * 2f - 1f) * 0.05f * Mathf.Exp(-off * 60f);
                }
                // Bombo en cada pulso.
                if (intensity > 0.84f)
                {
                    float f = 45f + 90f * Mathf.Exp(-beatPos * 30f);
                    s += Mathf.Sin(2f * Mathf.PI * f * beatPos) * 0.42f * Mathf.Exp(-beatPos * 9f);
                }
                // Caja en los tiempos 2 y 4.
                if (intensity > 0.95f)
                {
                    float sp = (t + beat) % (beat * 2f);
                    s += ((float)rng.NextDouble() * 2f - 1f) * 0.12f * Mathf.Exp(-sp * 22f);
                }
                // Subida antes del despegue (ruido + tono ascendente) e impacto grave al despegar y al amerizar.
                if (launch != null && t > launch.start && t < impact)
                {
                    float u = (t - launch.start) / (impact - launch.start);
                    s += ((float)rng.NextDouble() * 2f - 1f) * 0.08f * u * u + Mathf.Sin(2f * Mathf.PI * (200f + 600f * u * u) * t) * 0.04f * u;
                }
                foreach (float hit in new[] { impact, splashHit })
                    if (t >= hit && t < hit + 1.6f)
                    {
                        float d = t - hit;
                        s += Mathf.Sin(2f * Mathf.PI * (38f + 40f * Mathf.Exp(-d * 8f)) * d) * 0.55f * Mathf.Exp(-d * 2.5f);
                    }
                o[i] = s;
            }
            return o;
        }

        // ───────────── Composición de cada fotograma ─────────────

        private sealed class Rig : IDisposable
        {
            private readonly int w, h;
            private readonly bool vertical;
            private readonly GameObject root;
            private readonly Camera cam;
            private readonly RenderTexture rt;
            private readonly Texture2D read;
            private readonly Texture2D plateTex;
            private readonly RawImage bg, main;
            private readonly Image dim, progress, fade, titleBand;
            private readonly TextMeshProUGUI caption, title, brand, endTitle, endSub, endHandle;
            private readonly CanvasGroup endGroup;
            private string loaded;

            public Rig(int width, int height, bool vertical)
            {
                w = width; h = height; this.vertical = vertical;
                root = new GameObject("VideoRig") { hideFlags = HideFlags.HideAndDontSave };
                rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
                read = new Texture2D(w, h, TextureFormat.RGBA32, false);
                plateTex = new Texture2D(2, 2, TextureFormat.RGB24, false);
                cam = new GameObject("VideoRigCam").AddComponent<Camera>();
                cam.transform.SetParent(root.transform);
                cam.transform.position = new Vector3(0f, -10000f, 0f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.cullingMask = 1 << 5;
                cam.targetTexture = rt;
                cam.enabled = false;

                var canvasGo = new GameObject("VideoCanvas", typeof(RectTransform));
                canvasGo.transform.SetParent(root.transform);
                canvasGo.layer = 5;
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 1f;
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

                RectTransform Rect(string n, Transform parent)
                {
                    var go = new GameObject(n, typeof(RectTransform)) { layer = 5 };
                    go.transform.SetParent(parent, false);
                    return (RectTransform)go.transform;
                }
                void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
                TextMeshProUGUI Txt(string n, Transform parent, float size, Color c, TextAlignmentOptions align)
                {
                    var t = Rect(n, parent).gameObject.AddComponent<TextMeshProUGUI>();
                    t.fontSize = size; t.color = c; t.alignment = align; t.textWrappingMode = TextWrappingModes.Normal;
                    t.fontStyle = FontStyles.Bold; t.raycastTarget = false;
                    t.outlineWidth = 0.24f; t.outlineColor = new Color32(0, 0, 0, 255);
                    return t;
                }

                bg = Rect("Bg", canvasGo.transform).gameObject.AddComponent<RawImage>(); Stretch(bg.rectTransform);
                bg.color = new Color(0.32f, 0.34f, 0.4f);
                main = Rect("Main", canvasGo.transform).gameObject.AddComponent<RawImage>(); Stretch(main.rectTransform);
                dim = Rect("Dim", canvasGo.transform).gameObject.AddComponent<Image>(); Stretch(dim.rectTransform);
                dim.color = new Color(0f, 0f, 0f, 0f);

                caption = Txt("Caption", canvasGo.transform, vertical ? 84 : 66, Color.white, TextAlignmentOptions.Center);
                var cr = caption.rectTransform;
                cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0f);
                cr.sizeDelta = vertical ? new Vector2(920, 330) : new Vector2(1600, 200);
                cr.anchoredPosition = vertical ? new Vector2(0, 640) : new Vector2(0, 150);

                titleBand = Rect("TitleBand", canvasGo.transform).gameObject.AddComponent<Image>();
                titleBand.color = new Color(0f, 0f, 0f, 0f);
                var tb = titleBand.rectTransform;
                tb.anchorMin = new Vector2(0f, 1f); tb.anchorMax = new Vector2(1f, 1f); tb.pivot = new Vector2(0.5f, 1f);
                tb.sizeDelta = new Vector2(0f, vertical ? 620f : 260f);
                tb.anchoredPosition = Vector2.zero;
                title = Txt("Title", canvasGo.transform, vertical ? 104 : 84, new Color(1f, 0.82f, 0.25f), TextAlignmentOptions.Center);
                var tr = title.rectTransform;
                tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 1f);
                tr.sizeDelta = vertical ? new Vector2(980, 300) : new Vector2(1700, 140);
                tr.anchoredPosition = vertical ? new Vector2(0, -360) : new Vector2(0, -110);

                brand = Txt("Brand", canvasGo.transform, vertical ? 40 : 34, new Color(0.55f, 0.82f, 1f), vertical ? TextAlignmentOptions.Center : TextAlignmentOptions.Left);
                brand.text = "AstroByte  <size=70%><color=#FFFFFFCC>· NASA Space Apps 2026</color></size>";
                var br = brand.rectTransform;
                br.anchorMin = br.anchorMax = vertical ? new Vector2(0.5f, 1f) : new Vector2(0f, 1f);
                br.pivot = vertical ? new Vector2(0.5f, 1f) : new Vector2(0f, 1f);
                br.sizeDelta = new Vector2(900, 60);
                br.anchoredPosition = vertical ? new Vector2(0, -170) : new Vector2(48, -36);

                progress = Rect("Progress", canvasGo.transform).gameObject.AddComponent<Image>();
                progress.color = new Color(1f, 0.82f, 0.25f);
                var pr = progress.rectTransform;
                pr.anchorMin = new Vector2(0f, 1f); pr.anchorMax = new Vector2(0f, 1f); pr.pivot = new Vector2(0f, 1f);
                pr.anchoredPosition = Vector2.zero;
                progress.enabled = vertical;

                var end = Rect("EndCard", canvasGo.transform); Stretch(end);
                endGroup = end.gameObject.AddComponent<CanvasGroup>();
                endTitle = Txt("EndTitle", end, vertical ? 170 : 150, Color.white, TextAlignmentOptions.Center);
                endTitle.text = "Astro<color=#8CD2FF>Byte</color>";
                endTitle.rectTransform.sizeDelta = new Vector2(vertical ? 1000 : 1600, 220);
                endTitle.rectTransform.anchoredPosition = new Vector2(0, vertical ? 360 : 170);
                endSub = Txt("EndSub", end, vertical ? 44 : 40, new Color(1f, 0.82f, 0.25f), TextAlignmentOptions.Center);
                endSub.text = "Equipo ASTROBYTE · NASA Space Apps Challenge 2026\n<size=70%><color=#FFFFFFDD>Alejandro Marco Toranzo Cazorla · Keni Breython Ramos Ramirez\nJhamil Obando Requis · Tony Cahuaya</color></size>";
                endSub.rectTransform.sizeDelta = new Vector2(vertical ? 1000 : 1600, 260);
                endSub.rectTransform.anchoredPosition = new Vector2(0, vertical ? 140 : 0);
                endHandle = Txt("EndHandle", end, vertical ? 62 : 54, Color.white, TextAlignmentOptions.Center);
                endHandle.text = $"Síguenos  <color=#8CD2FF>{Handle}</color>\n<size=62%><color=#FFFFFFCC>Código en GitHub: {GitHub}</color></size>";
                endHandle.rectTransform.sizeDelta = new Vector2(vertical ? 1000 : 1600, 220);
                endHandle.rectTransform.anchoredPosition = new Vector2(0, vertical ? -120 : -190);

                fade = Rect("Fade", canvasGo.transform).gameObject.AddComponent<Image>(); Stretch(fade.rectTransform);
                fade.color = Color.clear;
            }

            public void Compose(Seg seg, float time, float total, List<Seg> segs, Dictionary<string, int> counts)
            {
                float local = time - seg.start;
                float sum = seg.shots.Sum(s => s.weight);
                float acc = 0f;
                Shot shot = seg.shots[0];
                float shotStart = 0f, shotLen = seg.duration;
                foreach (var s in seg.shots)
                {
                    float len = seg.duration * s.weight / sum;
                    if (local < acc + len || s == seg.shots.Last()) { shot = s; shotStart = acc; shotLen = len; break; }
                    acc += len;
                }
                float u = Mathf.Clamp01((local - shotStart) / Mathf.Max(0.01f, shotLen));
                string folder = Path.Combine(PlatesDir, shot.plate + (shot.v ? "_V" : "_H"));
                if (!counts.TryGetValue(folder, out int count)) counts[folder] = count = Directory.GetFiles(folder, "*.jpg").Length;
                int frame = Mathf.Clamp(shot.from + Mathf.FloorToInt((local - shotStart) * Fps), 0, count - 1);
                string file = Path.Combine(folder, $"frame_{frame:D5}.jpg");
                if (file != loaded) { plateTex.LoadImage(File.ReadAllBytes(file)); loaded = file; }

                // Ken Burns: acercamiento lento durante el plano (con desplazamiento opcional hacia una zona de interés).
                float z = Mathf.Lerp(1f, shot.zoom, Mathf.SmoothStep(0f, 1f, u));
                float s2 = 1f / z;
                Vector2 center = new Vector2(0.5f, 0.5f) + shot.pan * (1f - s2) * Mathf.SmoothStep(0f, 1f, u) * 1.6f;
                var uv = new UnityEngine.Rect(Mathf.Clamp(center.x - s2 / 2f, 0f, 1f - s2), Mathf.Clamp(center.y - s2 / 2f, 0f, 1f - s2), s2, s2);
                main.texture = plateTex;
                main.uvRect = uv;
                if (shot.layout == Layout.Crop && vertical)
                {
                    // Recorte 9:16 de un plano 16:9 que se desplaza por la interfaz (texto legible en el celular).
                    float cw = ((float)w / h) / (16f / 9f) / z, chh = 1f / z;
                    float x = Mathf.Lerp(shot.pan.x, shot.pan.y, Mathf.SmoothStep(0f, 1f, u)) * (1f - cw);
                    main.uvRect = new UnityEngine.Rect(x, (1f - chh) / 2f, cw, chh);
                }
                if (shot.layout == Layout.Fit && vertical)
                {
                    // Plano horizontal dentro del vertical: fondo con el mismo plano ampliado y oscurecido.
                    bg.enabled = true;
                    bg.texture = plateTex;
                    bg.uvRect = new UnityEngine.Rect(0.5f - 0.17f, 0f, 0.34f, 1f);
                    var mr = main.rectTransform;
                    mr.anchorMin = mr.anchorMax = new Vector2(0.5f, 0.5f);
                    mr.sizeDelta = new Vector2(w, w * 9f / 16f);
                    mr.anchoredPosition = new Vector2(0f, 230f);
                }
                else
                {
                    bg.enabled = false;
                    var mr = main.rectTransform;
                    mr.anchorMin = Vector2.zero; mr.anchorMax = Vector2.one; mr.offsetMin = mr.offsetMax = Vector2.zero;
                }

                // Subtítulos: frase corta con la palabra que se está diciendo resaltada.
                float vt = time - seg.voiceStart;
                caption.text = "";
                foreach (var c in Chunks(seg.voice))
                {
                    if (vt < c[0].t0 - 0.05f || vt > c[c.Count - 1].t1 + 0.25f) continue;
                    var sb = new StringBuilder();
                    for (int i = 0; i < c.Count; i++)
                    {
                        bool now = vt >= c[i].t0 - 0.03f && vt <= c[i].t1 + 0.08f;
                        string txt = c[i].text.ToUpperInvariant();
                        sb.Append(now ? $"<color=#FFD23F>{txt}</color>" : txt);
                        if (i < c.Count - 1) sb.Append(' ');
                    }
                    caption.text = sb.ToString();
                    float pop = Mathf.Clamp01((vt - c[0].t0 + 0.05f) / 0.12f);
                    caption.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.86f, 1f, pop);
                    break;
                }

                // Título gancho al principio, marca después del gancho, barra de progreso y tarjeta final.
                title.text = seg.title ?? "";
                title.alpha = seg.title != null ? Mathf.Clamp01(local / 0.25f) * Mathf.Clamp01((seg.duration - local) / 0.3f) : 0f;
                title.rectTransform.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(local * 4f));
                titleBand.color = new Color(0f, 0f, 0f, 0.45f * title.alpha);
                brand.alpha = segs.IndexOf(seg) == 0 || seg.endCard ? 0f : 0.95f;
                progress.rectTransform.sizeDelta = new Vector2(w * time / total, 10f);

                float endA = seg.endCard ? Mathf.Clamp01((local - 0.2f) / 0.5f) : 0f;
                endGroup.alpha = endA;
                dim.color = new Color(0f, 0f, 0f, 0.62f * endA);
                if (seg.endCard) caption.rectTransform.anchoredPosition = vertical ? new Vector2(0, 360) : new Vector2(0, 92);
                else caption.rectTransform.anchoredPosition = vertical ? new Vector2(0, 640) : new Vector2(0, 150);
                endHandle.rectTransform.localScale = Vector3.one * (1f + 0.03f * Mathf.Sin(local * 5f));

                // Fundido de entrada y salida.
                float fadeA = Mathf.Max(Mathf.Clamp01(1f - time / 0.25f), Mathf.Clamp01((time - (total - 0.6f)) / 0.6f));
                fade.color = new Color(0f, 0f, 0f, fadeA);

                Canvas.ForceUpdateCanvases();
                caption.ForceMeshUpdate(); title.ForceMeshUpdate();
                cam.Render();
            }

            public Texture2D Read()
            {
                RenderTexture.active = rt;
                read.ReadPixels(new UnityEngine.Rect(0, 0, w, h), 0, 0);
                // Sin Apply(): el codificador lee la copia en CPU; volver a subirla a la GPU solo carga la tarjeta.
                RenderTexture.active = null;
                return read;
            }

            public void SaveJpg(string path) => File.WriteAllBytes(path, read.EncodeToJPG(92));

            public void Dispose()
            {
                rt.Release();
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(read);
                UnityEngine.Object.DestroyImmediate(plateTex);
            }
        }
    }
}
