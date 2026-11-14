using MissionDesign.Core;
using MissionDesign.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace MissionDesign.UI
{
    /// <summary>
    /// Sonidos sintetizados por código (sin archivos de audio): clic al elegir, zumbido de error si el
    /// lanzamiento se bloquea, campanita de éxito, alarma en eventos críticos y rugido del cohete.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class SfxPlayer : MonoBehaviour
    {
        [SerializeField] private MissionSimulationRunner runner;
        [Range(0f, 1f)] [SerializeField] private float volume = 0.5f;

        private const int Rate = 44100;
        private AudioSource source;
        private AudioSource rumbleSource;
        private AudioClip click, error, success, alarm, rumble;
        private MissionStateManager manager;
        private int lastFrameClick = -1;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            rumbleSource = gameObject.AddComponent<AudioSource>();
            rumbleSource.playOnAwake = false;
            rumbleSource.loop = true;

            click = Tone("Click", 0.05f, t => Mathf.Sin(2f * Mathf.PI * 1250f * t) * Env(t, 0.05f));
            error = Tone("Error", 0.32f, t => Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 150f * t)) * 0.5f * Env(t, 0.32f));
            success = Tone("Success", 0.45f, t => (Mathf.Sin(2f * Mathf.PI * (t < 0.18f ? 880f : 1320f) * t)) * Env(t, 0.45f));
            alarm = Tone("Alarm", 0.6f, t => Mathf.Sin(2f * Mathf.PI * (Mathf.Repeat(t, 0.3f) < 0.15f ? 960f : 720f) * t) * 0.6f * Env(t, 0.6f));
            var rng = new System.Random(7);
            float lp = 0f;
            rumble = Tone("Rumble", 2f, t =>
            {
                lp += 0.04f * ((float)rng.NextDouble() * 2f - 1f - lp);   // ruido marrón (filtrado)
                return Mathf.Clamp(lp * 9f, -1f, 1f);
            });
        }

        private void Start()
        {
            manager = MissionStateManager.Instance;
            if (manager != null)
            {
                manager.Design.Changed += PlayClick;
                manager.LaunchRejected += _ => Play(error);
                manager.LaunchApproved += OnLaunch;
                manager.PhaseChanged += OnPhase;
                manager.MissionCompleted += o => Play(o != null && o.verdict == MissionVerdict.Failure ? error : success);
            }
            if (runner != null) runner.EntryEmitted += e => { if (e.tone == LogTone.Critical) Play(alarm); };
            foreach (var b in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                b.onClick.AddListener(PlayClick);
        }

        private void OnDestroy()
        {
            if (manager != null && manager.Design != null) manager.Design.Changed -= PlayClick;
        }

        private void OnLaunch()
        {
            Play(success);
            rumbleSource.clip = rumble;
            rumbleSource.volume = volume * 0.8f;
            rumbleSource.Play();
        }

        private void OnPhase(MissionPhase phase)
        {
            if (phase != MissionPhase.Launch && rumbleSource.isPlaying) rumbleSource.Stop();
        }

        private void PlayClick()
        {
            if (lastFrameClick == Time.frameCount) return;    // un solo clic por fotograma
            lastFrameClick = Time.frameCount;
            Play(click);
        }

        private void Play(AudioClip clip)
        {
            if (clip != null && source != null) source.PlayOneShot(clip, volume);
        }

        private static float Env(float t, float length) => Mathf.Clamp01(t / 0.01f) * Mathf.Clamp01((length - t) / (length * 0.6f));

        private static AudioClip Tone(string name, float seconds, System.Func<float, float> wave)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = wave(i / (float)Rate) * 0.5f;
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
