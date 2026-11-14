using System.Collections;
using MissionDesign.Core;
using UnityEngine;

namespace MissionDesign.UI.Visual3D
{
    /// <summary>
    /// Plataforma de lanzamiento: al entrar en la fase Launch coloca el modelo del cohete elegido,
    /// enciende los motores y reproduce el ascenso con separación de propulsores, primera etapa y carenado.
    /// Los modelos (Blender) traen hijos "Boosters", "Stage1", "Stage2" y "Fairing".
    /// </summary>
    public sealed class LaunchSiteController : MonoBehaviour
    {
        [SerializeField] private Transform rocketMount;
        [Tooltip("Cohete a usar si el lanzador elegido no tiene modelo.")]
        [SerializeField] private GameObject fallbackRocket;
        [SerializeField] private Material exhaustMaterial;
        [SerializeField] private Material smokeMaterial;
        [SerializeField] private float ascentDuration = 4f;
        [SerializeField] private float cameraDistance = 150f;

        private MissionStateManager manager;
        private GameObject rocket;
        private float rocketHeight = 70f;
        private Coroutine ascent;

        public Transform Rocket => rocket != null ? rocket.transform : rocketMount;

        private void Start()
        {
            manager = MissionStateManager.Instance;
            if (manager != null) manager.PhaseChanged += OnPhaseChanged;
        }

        private void OnDestroy()
        {
            if (manager != null) manager.PhaseChanged -= OnPhaseChanged;
        }

        /// <summary>Pose de cámara: a ras de suelo al despegar, siguiendo al cohete mientras sube.</summary>
        public Pose GetCameraPose()
        {
            Vector3 target = Rocket.position + Vector3.up * rocketHeight * 0.45f;
            Vector3 pos = rocketMount.position + new Vector3(-cameraDistance * 0.25f, rocketHeight * 0.3f, -cameraDistance);
            return new Pose(pos, Quaternion.LookRotation(target - pos, Vector3.up));
        }

        private void OnPhaseChanged(MissionPhase phase)
        {
            if (phase == MissionPhase.Launch)
            {
                SpawnRocket();
                if (ascent != null) StopCoroutine(ascent);
                ascent = StartCoroutine(Ascent());
            }
            else if (phase == MissionPhase.Design && rocket != null)
            {
                Destroy(rocket);
                rocket = null;
            }
        }

        private void SpawnRocket()
        {
            if (rocket != null) Destroy(rocket);
            var lv = manager.Design.LaunchVehicle;
            var prefab = lv != null && lv.prefab != null ? lv.prefab : fallbackRocket;
            if (prefab == null) return;
            rocket = Instantiate(prefab, rocketMount.position, rocketMount.rotation, transform);
            rocket.name = "LaunchingRocket";

            var bounds = new Bounds(rocket.transform.position, Vector3.zero);
            foreach (var r in rocket.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            rocketHeight = Mathf.Max(10f, bounds.size.y);
            float radius = Mathf.Max(0.5f, Mathf.Min(bounds.size.x, bounds.size.z) * 0.25f);

            // Cada etapa es una sola malla (Blender): las toberas se ubican por los límites del renderer.
            var core = FindChild(rocket.transform, "Stage1") ?? rocket.transform;
            var coreBounds = BoundsOf(core);
            Vector3 coreBase = new Vector3(coreBounds.center.x, coreBounds.min.y, coreBounds.center.z);
            float coreRadius = Mathf.Max(0.4f, Mathf.Min(coreBounds.size.x, coreBounds.size.z) * 0.35f);
            ExhaustFx.Create(core, core.InverseTransformPoint(coreBase), coreRadius, exhaustMaterial, smokeMaterial);

            var boosters = FindChild(rocket.transform, "Boosters");
            if (boosters != null)
            {
                var b = BoundsOf(boosters);
                float r = Mathf.Max(0.4f, Mathf.Min(b.size.x, b.size.z) * 0.12f);
                var points = new System.Collections.Generic.List<Vector3>
                {
                    new Vector3(b.min.x + r, b.min.y, b.center.z), new Vector3(b.max.x - r, b.min.y, b.center.z)
                };
                if (b.size.z > b.size.x * 0.5f)
                {
                    points.Add(new Vector3(b.center.x, b.min.y, b.min.z + r));
                    points.Add(new Vector3(b.center.x, b.min.y, b.max.z - r));
                }
                foreach (var p in points) ExhaustFx.Create(boosters, boosters.InverseTransformPoint(p), r, exhaustMaterial, null);
            }
            isCrewed = prefab.name.Contains("SLS");
        }

        private bool isCrewed;

        private static Bounds BoundsOf(Transform t)
        {
            var renderers = t.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(t.position, Vector3.one);
            var b = renderers[0].bounds;
            foreach (var r in renderers) if (!(r is ParticleSystemRenderer)) b.Encapsulate(r.bounds);
            return b;
        }

        private IEnumerator Ascent()
        {
            if (rocket == null) yield break;
            var boosters = FindChild(rocket.transform, "Boosters");
            var stage1 = FindChild(rocket.transform, "Stage1");
            var fairing = FindChild(rocket.transform, "Fairing");
            bool boostersGone = false, stage1Gone = false, fairingGone = false;

            Vector3 start = rocketMount.position;
            float t = 0f;
            yield return new WaitForSeconds(0.4f);               // encendido en la plataforma
            while (t < ascentDuration && rocket != null)
            {
                t += Time.deltaTime;
                float k = t / ascentDuration;
                float height = rocketHeight * (0.3f * k + 2.4f * k * k);   // acelera al despegar
                rocket.transform.position = start + Vector3.up * height + Vector3.right * rocketHeight * 0.25f * k * k;
                rocket.transform.rotation = rocketMount.rotation * Quaternion.Euler(0f, 0f, -12f * k * k);   // giro de cabeceo

                if (!boostersGone && boosters != null && k > 0.5f) { boostersGone = true; StartCoroutine(Jettison(boosters, Vector3.down * 4f, 1.5f)); }
                if (!stage1Gone && stage1 != null && k > 0.8f) { stage1Gone = true; StartCoroutine(Jettison(stage1, Vector3.down * 8f, 0.4f)); }
                if (!fairingGone && fairing != null && !isCrewed && k > 0.9f)   // en SLS "Fairing" es Orion: no se suelta
                { fairingGone = true; StartCoroutine(Jettison(fairing, Vector3.right * 6f, 2f)); }
                yield return null;
            }
            ascent = null;
        }

        /// <summary>Suelta una parte: hereda la velocidad, se aleja girando y desaparece.</summary>
        private IEnumerator Jettison(Transform part, Vector3 push, float spin)
        {
            part.SetParent(transform, true);
            Vector3 velocity = push;
            float life = 0f;
            while (life < 3f && part != null)
            {
                life += Time.deltaTime;
                velocity += Vector3.down * 9.8f * Time.deltaTime;
                part.position += velocity * Time.deltaTime;
                part.Rotate(Vector3.forward, spin * 20f * Time.deltaTime, Space.World);
                yield return null;
            }
            if (part != null) Destroy(part.gameObject);
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
