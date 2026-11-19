using System.Collections;
using MissionDesign.Characters;
using MissionDesign.Core;
using UnityEngine;

namespace MissionDesign.Exploration
{
    /// <summary>
    /// Círculo de entrada al diseño (estilo "marcador de misión"): anillo luminoso con llamas en el puesto de la consola.
    /// Al pisarlo, el personaje se sienta en la silla, la pantalla se funde y se abre la sala de diseño de misión.
    /// </summary>
    public sealed class DesignEntryRing : MonoBehaviour
    {
        [SerializeField] private Transform seat;
        [SerializeField] private Transform disc;
        [SerializeField] private Light glow;
        [SerializeField] private GameObject label;
        [SerializeField] private float radius = 0.95f;
        [SerializeField] private float holdSeconds = 0.35f;
        [SerializeField] private float sitSeconds = 1.6f;

        private PlayerController player;
        private ExplorationHud hud;
        private float inside;
        private bool entering;

        public bool IsEntering => entering;
        public float Radius => radius;
        public Transform Seat => seat;

        private void Start()
        {
            player = FindAnyObjectByType<PlayerController>();
            hud = FindAnyObjectByType<ExplorationHud>();
        }

        private void Update()
        {
            float pulse = 1f + 0.06f * Mathf.Sin(Time.time * 4f);
            if (disc != null) disc.localScale = new Vector3(2f * radius * pulse, 2f * radius * pulse, 1f);
            if (glow != null) glow.intensity = 2.5f + 1.2f * Mathf.Sin(Time.time * 6f) + Random.Range(-0.3f, 0.3f);
            if (entering || player == null) return;
            Vector3 d = player.transform.position - transform.position;
            if (label != null) label.SetActive(d.magnitude > 3f);
            bool on = new Vector2(d.x, d.z).magnitude < radius && Mathf.Abs(d.y) < 1.5f && (hud == null || !hud.IsBlocking);
            inside = on ? inside + Time.deltaTime : 0f;
            if (inside >= holdSeconds) StartCoroutine(Enter());
        }

        private IEnumerator Enter()
        {
            entering = true;
            if (label != null) label.SetActive(false);
            if (hud != null) hud.ShowToast(Loc.L("Entrando a la consola de diseño de misión…", "Entering the mission design console…"));
            if (hud != null) hud.CutsceneLock = true;
            var target = seat != null ? seat : transform;
            Vector3 face = Vector3.ProjectOnPlane(target.forward, Vector3.up);
            float yaw = face.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(face).eulerAngles.y : player.transform.eulerAngles.y;
            player.Teleport(new Vector3(target.position.x, transform.position.y + 0.05f, target.position.z) - Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * 0.15f, yaw);
            player.SetFrozen(true);
            var poser = player.GetComponent<CrewPoser>();
            if (poser != null) poser.SitAt(target);
            yield return new WaitForSeconds(sitSeconds);
            if (hud != null) yield return hud.FadeOut(0.6f);
            SceneFlow.OpenMissionDesign();
        }
    }
}
