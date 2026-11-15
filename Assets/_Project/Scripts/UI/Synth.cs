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

        /// <summary>Aplausos y vítores: muchas palmadas al azar sobre un murmullo que crece y se apaga.</summary>
        public static AudioClip Applause(float seconds = 3.2f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            var rng = new System.Random(11);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float swell = Mathf.Clamp01(t / 0.4f) * Mathf.Clamp01((seconds - t) / 1.2f);
                lp += 0.08f * ((float)rng.NextDouble() * 2f - 1f - lp);
                data[i] = lp * 2.2f * swell;
            }
            int claps = Mathf.RoundToInt(seconds * 70f);
            for (int c = 0; c < claps; c++)
            {
                int start = rng.Next(n);
                float t0 = start / (float)Rate;
                float amp = (0.25f + 0.35f * (float)rng.NextDouble()) * Mathf.Clamp01(t0 / 0.3f) * Mathf.Clamp01((seconds - t0) / 1.0f);
                int len = (int)(0.012f * Rate);
                for (int k = 0; k < len && start + k < n; k++)
                    data[start + k] += ((float)rng.NextDouble() * 2f - 1f) * amp * (1f - k / (float)len);
            }
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(data[i] * 0.6f, -1f, 1f);
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

        /// <summary>Barrido ascendente (entrar a la consola).</summary>
        public static AudioClip Rise() => Make("Rise", 1.2f, t =>
            Sine(Mathf.Lerp(220f, 880f, t / 1.2f), t) * Env(t, 1.2f, 0.2f) * 0.8f);

        /// <summary>Aviso corto (cambio de equipo, mensajes).</summary>
        public static AudioClip Blip() => Make("Blip", 0.18f, t => Sine(t < 0.07f ? 1046f : 1568f, t) * Env(t, 0.18f));
    }
}
