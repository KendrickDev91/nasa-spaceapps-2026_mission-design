using System.Collections.Generic;
using MissionDesign.Core;
using TMPro;
using UnityEngine;

namespace MissionDesign.UI.Visual3D
{
    /// <summary>
    /// Vista orbital esquemática (1 unidad = 1 000 km; la distancia Tierra-Luna se comprime y se indica).
    /// Dibuja órbita de estacionamiento, transferencia (Hohmann o translunar, como el diagrama de Artemis)
    /// y órbita objetivo; mueve la nave por cada fase y muestra el enlace de datos con tierra.
    /// </summary>
    public sealed class OrbitViewController : MonoBehaviour
    {
        public const float EarthRadius = 6.371f;
        public const float ParkingRadius = EarthRadius + 0.2f;
        public const float MoonDisplayDistance = 70f;
        public const float LunarOrbitRadius = 2.6f;

        [SerializeField] private Transform earth;
        [SerializeField] private Transform moon;
        [SerializeField] private LineRenderer parkingLine;
        [SerializeField] private LineRenderer transferLine;
        [SerializeField] private LineRenderer targetLine;
        [SerializeField] private LineRenderer linkLine;
        [SerializeField] private Transform marker;
        [SerializeField] private TMP_Text label;
        [Tooltip("Cámara que renderiza esta vista a la pantalla grande de la sala de control.")]
        [SerializeField] private Camera screenCamera;
        [SerializeField] private float transferSeconds = 3.5f;
        [SerializeField] private float orbitSeconds = 4f;

        private MissionStateManager manager;
        private readonly List<Vector3> transferPath = new List<Vector3>();
        private readonly List<Vector3> targetPath = new List<Vector3>();
        private MissionPhase phase;
        private float phaseTime;
        private OrbitRegime regime = OrbitRegime.LEO;

        private void Start()
        {
            manager = MissionStateManager.Instance;
            if (manager == null) return;
            manager.PhaseChanged += OnPhaseChanged;
            Prepare();
        }

        private void OnDestroy()
        {
            if (manager != null) manager.PhaseChanged -= OnPhaseChanged;
        }

        /// <summary>Encuadre de cámara según el tamaño de la trayectoria.</summary>
        public Pose GetCameraPose()
        {
            Vector3 center = transform.position;
            float distance;
            switch (regime)
            {
                case OrbitRegime.LEO: distance = 34f; break;
                case OrbitRegime.MEO: distance = 85f; break;
                case OrbitRegime.GEO: distance = 125f; break;
                default: distance = 120f; center += new Vector3(-MoonDisplayDistance * 0.5f, 0f, 0f); break;
            }
            Vector3 pos = center + new Vector3(0f, distance * 0.62f, -distance * 0.85f);
            Vector3 aim = center - new Vector3(0f, distance * 0.16f, 0f);    // la Tierra queda arriba del panel del HUD
            return new Pose(pos, Quaternion.LookRotation(aim - pos, Vector3.up));
        }

        private void OnPhaseChanged(MissionPhase p)
        {
            phase = p;
            phaseTime = 0f;
            if (p == MissionPhase.Launch || p == MissionPhase.Design) Prepare();
            if (linkLine != null) linkLine.enabled = p == MissionPhase.DataTransmission || p == MissionPhase.PostMission;
            if (label != null) label.text = Caption(p);
        }

        private void Prepare()
        {
            var orbit = manager != null ? manager.Design.Orbit : null;
            regime = orbit != null ? orbit.regime : OrbitRegime.LEO;
            float targetRadius = orbit != null ? EarthRadius + orbit.altitudeKm / 1000f : ParkingRadius + 0.5f;
            bool lunar = regime == OrbitRegime.Lunar || regime == OrbitRegime.Interplanetary;

            if (moon != null) moon.gameObject.SetActive(lunar);
            Draw(parkingLine, Circle(Vector3.zero, ParkingRadius, 96));

            transferPath.Clear();
            targetPath.Clear();
            if (lunar)
            {
                // Inyección translunar: semielipse con perigeo en la órbita de estacionamiento y apogeo junto a la Luna.
                transferPath.AddRange(HalfEllipse(ParkingRadius, MoonDisplayDistance - LunarOrbitRadius));
                Vector3 moonPos = new Vector3(-MoonDisplayDistance, 0f, 0f);
                if (moon != null) moon.localPosition = moonPos;
                targetPath.AddRange(Circle(moonPos, LunarOrbitRadius, 64));
            }
            else
            {
                // Transferencia de Hohmann: medio camino elíptico entre la órbita baja y la final.
                if (targetRadius - ParkingRadius > 0.3f) transferPath.AddRange(HalfEllipse(ParkingRadius, targetRadius));
                targetPath.AddRange(Circle(Vector3.zero, targetRadius, 128));
            }
            Draw(transferLine, transferPath);
            Draw(targetLine, targetPath);
            if (linkLine != null) linkLine.enabled = false;
            if (marker != null) marker.localPosition = new Vector3(ParkingRadius, 0f, 0f);
            if (label != null) label.text = Caption(phase);
        }

        private void Update()
        {
            if (marker == null) return;
            phaseTime += Time.deltaTime;
            switch (phase)
            {
                case MissionPhase.Launch:
                    marker.localPosition = Along(Circle(Vector3.zero, ParkingRadius, 96), Mathf.Repeat(phaseTime / orbitSeconds, 1f) * 0.25f);
                    break;
                case MissionPhase.OrbitInsertion:
                    marker.localPosition = transferPath.Count > 1
                        ? Along(transferPath, Mathf.Clamp01(phaseTime / transferSeconds))
                        : Along(targetPath, 0f);
                    break;
                case MissionPhase.ScienceOperations:
                case MissionPhase.DataTransmission:
                case MissionPhase.PostMission:
                    if (targetPath.Count > 1) marker.localPosition = Along(targetPath, Mathf.Repeat(phaseTime / orbitSeconds, 1f));
                    break;
            }

            if (linkLine != null && linkLine.enabled)
            {
                // Enlace hacia la estación terrena más cercana en la superficie de la Tierra.
                Vector3 m = marker.localPosition;
                Vector3 ground = (m.sqrMagnitude > 0.01f ? m.normalized : Vector3.right) * EarthRadius;
                if (regime == OrbitRegime.Lunar) ground = new Vector3(-EarthRadius, 0f, 0f);   // cara de la Tierra que mira a la Luna
                if (linkLine.positionCount != 2) linkLine.positionCount = 2;
                linkLine.SetPosition(0, m);
                linkLine.SetPosition(1, ground);
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 8f);
                linkLine.widthMultiplier = 0.08f + 0.12f * pulse;
            }
            if (earth != null) earth.Rotate(Vector3.up, 6f * Time.deltaTime, Space.Self);
            if (screenCamera != null)
            {
                var pose = GetCameraPose();
                screenCamera.transform.SetPositionAndRotation(pose.position, pose.rotation);
            }
        }

        private string Caption(MissionPhase p)
        {
            bool lunar = regime == OrbitRegime.Lunar;
            switch (p)
            {
                case MissionPhase.Launch: return "Ascenso a la órbita de estacionamiento (~200 km)";
                case MissionPhase.OrbitInsertion:
                    return lunar ? "Inyección translunar (TLI) y crucero hacia la Luna" : $"Transferencia de Hohmann hacia {regime}";
                case MissionPhase.ScienceOperations: return lunar ? "Órbita lunar: operaciones científicas" : $"Órbita {regime}: operaciones científicas";
                case MissionPhase.DataTransmission: return "Enlace de bajada de datos con la red terrena";
                default: return lunar ? "Distancia Tierra-Luna no a escala" : string.Empty;
            }
        }

        private static List<Vector3> Circle(Vector3 center, float radius, int segments)
        {
            var pts = new List<Vector3>(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                float a = 2f * Mathf.PI * i / segments;
                pts.Add(center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
            return pts;
        }

        /// <summary>Semielipse de perigeo rp (en +X) a apogeo ra (en -X): r(θ) = a(1-e²)/(1+e·cosθ).</summary>
        private static List<Vector3> HalfEllipse(float rp, float ra, int segments = 96)
        {
            float a = (rp + ra) * 0.5f, e = (ra - rp) / (ra + rp);
            var pts = new List<Vector3>(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                float th = Mathf.PI * i / segments;
                float r = a * (1f - e * e) / (1f + e * Mathf.Cos(th));
                pts.Add(new Vector3(Mathf.Cos(th) * r, 0f, Mathf.Sin(th) * r));
            }
            return pts;
        }

        private static Vector3 Along(List<Vector3> path, float t)
        {
            if (path.Count == 0) return Vector3.zero;
            float f = Mathf.Clamp01(t) * (path.Count - 1);
            int i = Mathf.Min(path.Count - 2, Mathf.FloorToInt(f));
            return i < 0 ? path[0] : Vector3.Lerp(path[i], path[i + 1], f - i);
        }

        private static void Draw(LineRenderer line, List<Vector3> pts)
        {
            if (line == null) return;
            line.useWorldSpace = false;
            line.positionCount = pts.Count;
            line.SetPositions(pts.ToArray());
            line.enabled = pts.Count > 1;
        }
    }
}
