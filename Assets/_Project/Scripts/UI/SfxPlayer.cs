using MissionDesign.Core;
using MissionDesign.Simulation;
using MissionDesign.UI.Visual3D;
using UnityEngine;
using UnityEngine.UI;

namespace MissionDesign.UI
{
    /// <summary>
    /// Sonidos sintetizados por código (sin archivos de audio): clic al elegir, zumbido de error si el lanzamiento se bloquea,
    /// cuenta regresiva y rugido del cohete, aplausos de la sala de control al despegar, campanita en cada hito de la trayectoria,
    /// obturador en cada fotografía, módem durante la bajada de datos, alarma en eventos críticos y fanfarria / tono de falla al final.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class SfxPlayer : MonoBehaviour
    {
        [SerializeField] private MissionSimulationRunner runner;
        [Range(0f, 1f)] [SerializeField] private float volume = 0.5f;

        private AudioSource source;
        private AudioSource rumbleSource;
        private AudioClip click, error, alarm, rumble, countdown, applause, chime, shutter, modem, fanfare, failure;
        private MissionStateManager manager;
        private OrbitViewController orbit;
        private MoonPhotoStudio studio;
        private int lastFrameClick = -1;

        /// <summary>Último sonido reproducido (para pruebas).</summary>
        public string LastPlayed { get; private set; }

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            rumbleSource = gameObject.AddComponent<AudioSource>();
            rumbleSource.playOnAwake = false;
            rumbleSource.loop = true;

            click = Synth.Make("Click", 0.05f, t => Mathf.Sin(2f * Mathf.PI * 1250f * t) * Synth.Env(t, 0.05f));
            error = Synth.Make("Error", 0.32f, t => Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 150f * t)) * 0.5f * Synth.Env(t, 0.32f));
            alarm = Synth.Make("Alarm", 0.6f, t => Mathf.Sin(2f * Mathf.PI * (Mathf.Repeat(t, 0.3f) < 0.15f ? 960f : 720f) * t) * 0.6f * Synth.Env(t, 0.6f));
            var rng = new System.Random(7);
            float lp = 0f;
            rumble = Synth.Make("Rumble", 2f, t =>
            {
                lp += 0.04f * ((float)rng.NextDouble() * 2f - 1f - lp);   // ruido marrón (filtrado)
                return Mathf.Clamp(lp * 9f, -1f, 1f);
            });
            countdown = Synth.Countdown();
            applause = Synth.Applause();
            chime = Synth.Chime();
            shutter = Synth.Shutter();
            modem = Synth.Modem();
            fanfare = Synth.Fanfare();
            failure = Synth.Failure();
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
                manager.MissionCompleted += o => Play(o != null && o.verdict == MissionVerdict.Failure ? failure : fanfare);
            }
            if (runner != null) runner.EntryEmitted += e => { if (e.tone == LogTone.Critical) Play(alarm); };
            orbit = FindAnyObjectByType<OrbitViewController>();
            if (orbit != null) orbit.MilestoneReached += _ => Play(chime, 0.7f);
            studio = FindAnyObjectByType<MoonPhotoStudio>();
            if (studio != null) studio.PhotoTaken += (_, __, ___) => Play(shutter);
            foreach (var b in FindObjectsByType<Button>(FindObjectsInactive.Include))
                b.onClick.AddListener(PlayClick);
        }

        private void OnDestroy()
        {
            if (manager != null && manager.Design != null) manager.Design.Changed -= PlayClick;
        }

        private void OnLaunch()
        {
            Play(countdown, 0.8f);
            rumbleSource.clip = rumble;
            rumbleSource.volume = volume * 0.8f;
            rumbleSource.PlayDelayed(1.6f);
        }

        private void OnPhase(MissionPhase phase)
        {
            if (phase != MissionPhase.Launch && rumbleSource.isPlaying) rumbleSource.Stop();
            if (phase == MissionPhase.OrbitInsertion) Play(applause, 0.9f);     // el cohete llegó al espacio: la sala aplaude
            if (phase == MissionPhase.DataTransmission) Play(modem, 0.5f);
        }

        private void PlayClick()
        {
            if (lastFrameClick == Time.frameCount) return;    // un solo clic por fotograma
            lastFrameClick = Time.frameCount;
            Play(click);
        }

        private void Play(AudioClip clip, float gain = 1f)
        {
            if (clip == null || source == null) return;
            source.PlayOneShot(clip, volume * gain);
            LastPlayed = clip.name;
        }
    }
}
