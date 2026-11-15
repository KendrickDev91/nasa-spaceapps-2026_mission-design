using MissionDesign.Characters;
using MissionDesign.UI;
using UnityEngine;

namespace MissionDesign.Exploration
{
    /// <summary>
    /// Sonidos del centro espacial: pasos (más rápidos al correr), puertas corredizas, crepitar del círculo de entrada,
    /// barrido al sentarse en la consola y aviso al cambiar de equipo según el ambiente.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class ExplorationSfx : MonoBehaviour
    {
        [Range(0f, 1f)] [SerializeField] private float volume = 0.5f;

        private AudioSource source;
        private AudioClip[] steps;
        private AudioClip door, rise, blip;
        private PlayerController player;
        private ExplorationHud hud;
        private AutoDoor[] doors;
        private bool[] doorWasOpening;
        private DesignEntryRing ring;
        private bool ringEntered;
        private Outfit lastOutfit;
        private float stepTimer;

        public int StepsPlayed { get; private set; }
        public int DoorsPlayed { get; private set; }

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            steps = new[] { Synth.Footstep(1), Synth.Footstep(2), Synth.Footstep(3) };
            door = Synth.DoorWhoosh();
            rise = Synth.Rise();
            blip = Synth.Blip();
        }

        private void Start()
        {
            player = FindAnyObjectByType<PlayerController>();
            hud = FindAnyObjectByType<ExplorationHud>();
            doors = FindObjectsByType<AutoDoor>();
            doorWasOpening = new bool[doors.Length];
            ring = FindAnyObjectByType<DesignEntryRing>();
            if (hud != null) lastOutfit = hud.CurrentOutfit;
            if (ring != null)
            {
                // Fuego del círculo: sonido 3D en bucle que se oye al acercarse.
                var fire = ring.gameObject.AddComponent<AudioSource>();
                fire.clip = Synth.Crackle();
                fire.loop = true;
                fire.spatialBlend = 1f;
                fire.minDistance = 1.5f;
                fire.maxDistance = 14f;
                fire.rolloffMode = AudioRolloffMode.Linear;
                fire.volume = volume * 0.7f;
                fire.Play();
            }
        }

        private void Update()
        {
            if (player != null && player.IsGrounded && !player.Frozen && player.AnimatorSpeed > 0.25f)
            {
                stepTimer -= Time.deltaTime * (player.AnimatorSpeed > 1.4f ? 1.9f : 1.15f);
                if (stepTimer <= 0f)
                {
                    stepTimer = 0.5f;
                    source.pitch = Random.Range(0.9f, 1.1f);
                    source.PlayOneShot(steps[Random.Range(0, steps.Length)], volume * 0.6f);
                    source.pitch = 1f;
                    StepsPlayed++;
                }
            }
            else stepTimer = 0f;

            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i] == null) continue;
                bool opening = doors[i].Openness > 0.05f;
                if (opening && !doorWasOpening[i])
                {
                    AudioSource.PlayClipAtPoint(door, doors[i].transform.position, volume);
                    DoorsPlayed++;
                }
                doorWasOpening[i] = opening;
            }

            if (ring != null && ring.IsEntering && !ringEntered)
            {
                ringEntered = true;
                source.PlayOneShot(rise, volume);
            }

            if (hud != null && hud.CurrentOutfit != lastOutfit)
            {
                lastOutfit = hud.CurrentOutfit;
                source.PlayOneShot(blip, volume * 0.6f);
            }
        }
    }
}
