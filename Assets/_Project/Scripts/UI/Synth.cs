using UnityEngine;

namespace MissionDesign.UI
{
    /// <summary>Sonidos sintetizados por código (sin archivos de audio ni derechos de terceros).</summary>
    public static class Synth
    {
        public const int Rate = 44100;

        public static float Env(float t, float length, float attack = 0.01f) =>
            Mathf.Clamp01(t / attack) * Mathf.Clamp01((length - t) / (length * 0.6f));

        public static AudioClip Make(string name, float seconds, System.Func<float, float> wave, float gain = 0.5f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(wave(i / (float)Rate) * gain, -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Sine(float f, float t) => Mathf.Sin(2f * Mathf.PI * f * t);

        /// <summary>Obturador de cámara: dos chasquidos mecánicos.</summary>
        public static AudioClip Shutter()
        {
            var rng = new System.Random(3);
            return Make("Shutter", 0.16f, t =>
            {
                float n = (float)rng.NextDouble() * 2f - 1f;
                float a = t < 0.025f ? 1f - t / 0.025f : 0f;
                float b = t > 0.075f && t < 0.105f ? 1f - (t - 0.075f) / 0.03f : 0f;
                return n * (a + 0.8f * b) + 0.3f * Sine(2400f, t) * a;
            }, 0.6f);
        }

        /// <summary>
        /// Aplausos y vítores de la sala de control: 28 personas que aplauden cada una a su ritmo (palmadas con timbre propio),
        /// voces de festejo ("¡uuuh!") que suben y bajan y un par de silbidos. Crece rápido y se apaga al final.
        /// </summary>
        public static AudioClip Applause(float seconds = 4.5f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            var rng = new System.Random(11);
            float R() => (float)rng.NextDouble();
            float Envelope(float t) => Mathf.Clamp01(t / 0.35f) * Mathf.Clamp01((seconds - t) / 1.4f);

            // 1) Palmadas: cada persona con su ritmo (2,4-3,8 Hz), su tono (filtro) y pequeñas irregularidades.
            for (int person = 0; person < 28; person++)
            {
                float rate = 2.4f + R() * 1.4f;
                float gain = 0.35f + R() * 0.45f;
                float bright = 0.25f + R() * 0.55f;            // palmas "huecas" (graves) o "planas" (agudas)
                float t = R() * 0.6f;                           // empiezan escalonados, como en la sala
                while (t < seconds)
                {
                    int start = (int)(t * Rate);
                    int len = (int)((0.008f + R() * 0.01f) * Rate);
                    float amp = gain * Envelope(t) * (0.7f + R() * 0.3f);
                    float lp = 0f;
                    for (int k = 0; k < len && start + k < n; k++)
                    {
                        float noise = R() * 2f - 1f;
                        lp += bright * (noise - lp);
                        float decay = Mathf.Exp(-k / (len * 0.35f));
                        data[start + k] += (lp * 1.6f + noise * 0.25f) * amp * decay;
                    }
                    t += 1f / rate * (0.85f + R() * 0.3f);
                }
            }

            // 2) Voces de festejo: vocales "uuu/ooo" con vibrato que suben de tono (varias a la vez).
            for (int v = 0; v < 6; v++)
            {
                float t0 = 0.2f + R() * (seconds - 1.6f), dur = 0.7f + R() * 0.8f;
                float f0 = 180f + R() * 160f, rise = 1.25f + R() * 0.35f, gain = 0.05f + R() * 0.05f;
                float ph = 0f;
                for (int i = (int)(t0 * Rate); i < Mathf.Min(n, (int)((t0 + dur) * Rate)); i++)
                {
                    float u = (i / (float)Rate - t0) / dur;
                    float f = f0 * Mathf.Lerp(1f, rise, Mathf.Sin(u * Mathf.PI * 0.5f)) * (1f + 0.02f * Mathf.Sin(u * 40f));
                    ph += 2f * Mathf.PI * f / Rate;
                    float voice = Mathf.Sin(ph) + 0.5f * Mathf.Sin(2f * ph) + 0.25f * Mathf.Sin(3f * ph);
                    data[i] += voice * gain * Mathf.Sin(u * Mathf.PI) * Envelope(i / (float)Rate);
                }
            }

            // 3) Dos silbidos de festejo (subida y bajada rápida).
            for (int w = 0; w < 2; w++)
            {
                float t0 = 0.6f + w * 1.4f + R() * 0.4f, dur = 0.55f;
                float ph = 0f;
                for (int i = (int)(t0 * Rate); i < Mathf.Min(n, (int)((t0 + dur) * Rate)); i++)
                {
                    float u = (i / (float)Rate - t0) / dur;
                    float f = 1900f + 900f * Mathf.Sin(u * Mathf.PI);
                    ph += 2f * Mathf.PI * f / Rate;
                    data[i] += Mathf.Sin(ph) * 0.07f * Mathf.Sin(u * Mathf.PI);
                }
            }

            // Normalizar al 80 % para que no sature.
            float peak = 1e-4f;
            for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.8f;
            var clip = AudioClip.Create("Applause", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Campanita suave (dos notas) para los hitos de la trayectoria.</summary>
        public static AudioClip Chime() => Make("Chime", 0.7f, t =>
            (Sine(784f, t) * Mathf.Exp(-t * 6f) + (t > 0.12f ? Sine(1175f, t) * Mathf.Exp(-(t - 0.12f) * 5f) : 0f)) * 0.7f);

        /// <summary>Cuenta regresiva: tres pitidos cortos y uno largo.</summary>
        public static AudioClip Countdown() => Make("Countdown", 2.2f, t =>
        {
            float local = Mathf.Repeat(t, 0.5f);
            bool last = t >= 1.5f;
            float len = last ? 0.6f : 0.12f;
            float tt = last ? t - 1.5f : local;
            return tt < len ? Sine(last ? 1320f : 880f, t) * Env(tt, len) : 0f;
        });

        /// <summary>Fanfarria de éxito: arpegio Do-Mi-Sol-Do y acorde.</summary>
        public static AudioClip Fanfare() => Make("Fanfare", 1.6f, t =>
        {
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            int k = Mathf.Min(3, (int)(t / 0.13f));
            if (t < 0.52f) return (Sine(notes[k], t) + 0.3f * Sine(notes[k] * 2f, t)) * Env(Mathf.Repeat(t, 0.13f), 0.13f);
            float tt = t - 0.52f;
            return (Sine(523.25f, t) + Sine(659.25f, t) + Sine(783.99f, t)) * 0.4f * Mathf.Exp(-tt * 2.2f);
        });

        /// <summary>Tono descendente de falla.</summary>
        public static AudioClip Failure() => Make("Failure", 1.4f, t =>
        {
            float f = Mathf.Lerp(392f, 196f, Mathf.Clamp01(t / 1.2f));
            return (Mathf.Sign(Sine(f, t)) * 0.3f + Sine(f, t) * 0.5f) * Env(t, 1.4f, 0.02f);
        });

        /// <summary>Chirridos de módem para el enlace de bajada de datos.</summary>
        public static AudioClip Modem()
        {
            var rng = new System.Random(5);
            float f = 1200f;
            return Make("Modem", 1.2f, t =>
            {
                if (Mathf.Repeat(t, 0.06f) < 0.0005f) f = 900f + (float)rng.NextDouble() * 1600f;
                return Sine(f, t) * 0.4f * Env(t, 1.2f);
            });
        }

        /// <summary>Paso suave (golpe grave filtrado).</summary>
        public static AudioClip Footstep(int seed)
        {
            var rng = new System.Random(seed);
            float lp = 0f;
            return Make("Step" + seed, 0.12f, t =>
            {
                lp += 0.15f * ((float)rng.NextDouble() * 2f - 1f - lp);
                return (lp * 3f + Sine(70f, t)) * Mathf.Exp(-t * 35f);
            }, 0.45f);
        }

        /// <summary>Puerta corrediza neumática.</summary>
        public static AudioClip DoorWhoosh()
        {
            var rng = new System.Random(9);
            float lp = 0f;
            return Make("Door", 0.7f, t =>
            {
                float a = 0.02f + 0.1f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.7f));
                lp += a * ((float)rng.NextDouble() * 2f - 1f - lp);
                return lp * 4f * Env(t, 0.7f, 0.05f);
            });
        }

        /// <summary>Crepitar de fuego en bucle (círculo de entrada).</summary>
        public static AudioClip Crackle()
        {
            var rng = new System.Random(21);
            float lp = 0f;
            return Make("Crackle", 2f, t =>
            {
                lp += 0.05f * ((float)rng.NextDouble() * 2f - 1f - lp);
                float pop = rng.NextDouble() < 0.0015 ? ((float)rng.NextDouble() * 2f - 1f) * 3f : 0f;
                return lp * 2f + pop;
            }, 0.35f);
        }

        /// <summary>Rugido de la reentrada: ruido grave filtrado que crece y vibra (plasma alrededor de la cápsula).</summary>
        public static AudioClip ReentryRoar(float seconds = 3.2f)
        {
            var rng = new System.Random(41);
            float lp = 0f, lp2 = 0f;
            return Make("ReentryRoar", seconds, t =>
            {
                lp += 0.025f * ((float)rng.NextDouble() * 2f - 1f - lp);
                lp2 += 0.2f * (lp - lp2);
                float swell = Mathf.Clamp01(t / 0.8f) * Mathf.Clamp01((seconds - t) / 0.6f);
                return lp2 * 9f * swell * (0.8f + 0.2f * Sine(7f, t));
            }, 0.55f);
        }

        /// <summary>Apertura de un paracaídas: chasquido de tela seguido de un golpe sordo.</summary>
        public static AudioClip ChuteSnap()
        {
            var rng = new System.Random(57);
            float lp = 0f;
            return Make("ChuteSnap", 0.9f, t =>
            {
                lp += 0.3f * ((float)rng.NextDouble() * 2f - 1f - lp);
                float crack = t < 0.06f ? 1f - t / 0.06f : 0f;
                float thump = Sine(55f, t) * Mathf.Exp(-t * 6f);
                return lp * 2.2f * crack + thump * 0.9f + lp * 0.4f * Mathf.Exp(-t * 4f);
            }, 0.6f);
        }

        /// <summary>Amerizaje: golpe en el agua y salpicadura que se apaga.</summary>
        public static AudioClip Splash()
        {
            var rng = new System.Random(63);
            float lp = 0f;
            return Make("Splash", 2.2f, t =>
            {
                float n = (float)rng.NextDouble() * 2f - 1f;
                lp += 0.12f * (n - lp);
                float body = Sine(70f, t) * Mathf.Exp(-t * 5f);
                return (n * 0.5f + lp * 2.5f) * Mathf.Exp(-t * 2.2f) * Mathf.Clamp01(t / 0.01f) + body;
            }, 0.55f);
        }

        /// <summary>Barrido ascendente (entrar a la consola).</summary>
        public static AudioClip Rise() => Make("Rise", 1.2f, t =>
            Sine(Mathf.Lerp(220f, 880f, t / 1.2f), t) * Env(t, 1.2f, 0.2f) * 0.8f);

        /// <summary>Aviso corto (cambio de equipo, mensajes).</summary>
        public static AudioClip Blip() => Make("Blip", 0.18f, t => Sine(t < 0.07f ? 1046f : 1568f, t) * Env(t, 0.18f));
    }
}
