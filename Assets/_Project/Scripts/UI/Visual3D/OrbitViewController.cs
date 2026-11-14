using System.Collections.Generic;
using MissionDesign.Core;
using MissionDesign.Simulation;
using TMPro;
using UnityEngine;

namespace MissionDesign.UI.Visual3D
{
    /// <summary>
    /// Vista orbital esquemática (1 unidad ≈ 1 000 km cerca de la Tierra; la distancia Tierra-Luna se comprime).
    /// Misiones en órbita: estacionamiento → transferencia (Hohmann o translunar) → órbita objetivo.
    /// Sobrevuelo tripulado (tipo Artemis II): dibuja la trayectoria de retorno libre CALCULADA con el CR3BP
    /// (FreeReturnTrajectory), ida en morado y vuelta en naranja, con flechas de sentido y los 8 hitos numerados.
    /// </summary>
    public sealed class OrbitViewController : MonoBehaviour
    {
        public const float EarthRadius = 6.371f;
        public const float ParkingRadius = EarthRadius + 0.2f;
        public const float MoonDisplayDistance = 70f;
        public const float MoonDisplayRadius = 1.737f;
        public const float LunarOrbitRadius = 2.6f;

        [SerializeField] private Transform earth;
        [SerializeField] private Transform moon;
        [SerializeField] private LineRenderer parkingLine;
        [SerializeField] private LineRenderer transferLine;
        [SerializeField] private LineRenderer targetLine;
        [SerializeField] private LineRenderer linkLine;
        [Tooltip("Sobrevuelo tripulado: tramo de ida (morado) y de vuelta (naranja).")]
        [SerializeField] private LineRenderer outboundLine;
        [SerializeField] private LineRenderer returnLine;
        [SerializeField] private Material outboundMaterial;
        [SerializeField] private Material returnMaterial;
        [SerializeField] private Material milestoneMaterial;
        [SerializeField] private Transform marker;
        [SerializeField] private TMP_Text label;
        [Tooltip("Cámara que renderiza esta vista a la pantalla grande de la sala de control.")]
        [SerializeField] private Camera screenCamera;
        [SerializeField] private float transferSeconds = 3.5f;
        [SerializeField] private float orbitSeconds = 4f;

        private MissionStateManager manager;
        private readonly List<Vector3> transferPath = new List<Vector3>();
        private readonly List<Vector3> targetPath = new List<Vector3>();
        private readonly List<Vector3> parkingPath = new List<Vector3>();
        private readonly List<(float t, Vector3 p)> flybyPath = new List<(float, Vector3)>();
        private readonly List<GameObject> decorations = new List<GameObject>();
        private FreeReturnSolution freeReturn;
        private MissionPhase phase;
        private float phaseTime;
        private OrbitRegime regime = OrbitRegime.LEO;

        public bool ShowsFreeReturn => regime == OrbitRegime.LunarFlyby && freeReturn != null;
        public int MilestoneCount => decorations.FindAll(d => d.name.StartsWith("Milestone_")).Count;

        private void Start()
        {
            FreeReturnTrajectory.WarmUpAsync();
            manager = MissionStateManager.Instance;
            if (manager == null) return;
            manager.PhaseChanged += OnPhaseChanged;
            manager.Design.Changed += Prepare;
            Prepare();
        }

        private void OnDestroy()
        {
            if (manager == null) return;
            manager.PhaseChanged -= OnPhaseChanged;
            if (manager.Design != null) manager.Design.Changed -= Prepare;
        }

        /// <summary>Encuadre de cámara según el tamaño de la trayectoria.</summary>
        public Pose GetCameraPose()
        {
            Vector3 center = transform.position;
            if (regime == OrbitRegime.LunarFlyby)
            {
                Vector3 c = center + new Vector3(-34f, 0f, -16f);
                Vector3 p = c + new Vector3(0f, 105f, -48f);
                return new Pose(p, Quaternion.LookRotation(c - p, Vector3.forward));
            }
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
            ClearDecorations();
            transferPath.Clear();
            targetPath.Clear();
            parkingPath.Clear();
            flybyPath.Clear();
            Draw(outboundLine, new List<Vector3>());
            Draw(returnLine, new List<Vector3>());

            if (regime == OrbitRegime.LunarFlyby)
            {
                PrepareFreeReturn();
                return;
            }

            float targetRadius = orbit != null ? EarthRadius + orbit.altitudeKm / 1000f : ParkingRadius + 0.5f;
            bool lunar = regime == OrbitRegime.Lunar || regime == OrbitRegime.Interplanetary;
            if (moon != null)
            {
                moon.gameObject.SetActive(lunar);
                moon.localPosition = new Vector3(-MoonDisplayDistance, 0f, 0f);
            }
            parkingPath.AddRange(Circle(Vector3.zero, ParkingRadius, 96));
            Draw(parkingLine, parkingPath);

            if (lunar)
            {
                // Inyección translunar: semielipse con perigeo en la órbita de estacionamiento y apogeo junto a la Luna.
                transferPath.AddRange(HalfEllipse(ParkingRadius, MoonDisplayDistance - LunarOrbitRadius));
                targetPath.AddRange(Circle(new Vector3(-MoonDisplayDistance, 0f, 0f), LunarOrbitRadius, 64));
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

        // ───────────── Sobrevuelo con retorno libre (Artemis II) ─────────────

        private void PrepareFreeReturn()
        {
            freeReturn = FreeReturnTrajectory.Default;
            Draw(transferLine, new List<Vector3>());
            Draw(targetLine, new List<Vector3>());
            if (moon != null)
            {
                moon.gameObject.SetActive(true);
                moon.localPosition = new Vector3(-MoonDisplayDistance, 0f, 0f);
            }

            var outbound = new List<Vector3>();
            var back = new List<Vector3>();
            foreach (var s in freeReturn.samples)
            {
                var p = Map(s.xKm, s.yKm);
                flybyPath.Add((s.tDays, p));
                if (s.leg == 0) outbound.Add(p);
                else
                {
                    if (back.Count == 0 && outbound.Count > 0) back.Add(outbound[outbound.Count - 1]);
                    back.Add(p);
                }
            }

            // Órbita terrestre alta previa a la TLI (ilustrativa, como en el diagrama de Artemis II).
            Vector3 injection = outbound.Count > 0 ? outbound[0] : new Vector3(ParkingRadius, 0f, 0f);
            float perigeeAngle = Mathf.Atan2(injection.z, injection.x);
            for (int i = 0; i <= 128; i++)
            {
                float th = 2f * Mathf.PI * i / 128;
                float rp = ParkingRadius, ra = EarthRadius + 18f, a = (rp + ra) / 2f, e = (ra - rp) / (ra + rp);
                float r = a * (1 - e * e) / (1 + e * Mathf.Cos(th));
                parkingPath.Add(new Vector3(Mathf.Cos(th + perigeeAngle) * r, 0f, Mathf.Sin(th + perigeeAngle) * r));
            }
            Draw(parkingLine, parkingPath);
            Draw(outboundLine, outbound);
            Draw(returnLine, back);
            AddArrows(outbound, outboundMaterial, 5);
            AddArrows(back, returnMaterial, 6);

            int nearEarthLabels = 0;
            foreach (var m in freeReturn.milestones)
            {
                var pos = Map(m.xKm, m.yKm);
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = $"Milestone_{m.number}";
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform, false);
                go.transform.localPosition = pos;
                go.transform.localScale = Vector3.one * 1.4f;
                if (milestoneMaterial != null) go.GetComponent<Renderer>().sharedMaterial = m.number <= 5 ? outboundMaterial : returnMaterial;
                decorations.Add(go);

                var textGo = new GameObject($"MilestoneLabel_{m.number}");
                textGo.transform.SetParent(transform, false);
                // Etiqueta hacia fuera del "8" (centro aprox. entre la Tierra y la Luna) para que no se encimen.
                Vector3 outward = pos - new Vector3(-MoonDisplayDistance * 0.5f, 0f, 0f);
                outward.y = 0f;
                outward = outward.sqrMagnitude > 1e-3f ? outward.normalized : Vector3.forward;
                bool nearEarth = pos.magnitude < 14f;
                textGo.transform.localPosition = nearEarth
                    ? new Vector3(10f, 1.5f, 9f - 5.5f * nearEarthLabels++)     // columna ordenada a la derecha de la Tierra
                    : pos + outward * 6f + Vector3.up * 1.5f;
                var tmp = textGo.AddComponent<TextMeshPro>();
                tmp.text = $"<b>{m.number}</b> {m.title}";
                tmp.fontSize = 24f;
                tmp.alignment = nearEarth ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.outlineWidth = 0.2f;
                tmp.rectTransform.sizeDelta = new Vector2(60f, 8f);
                if (nearEarth) tmp.rectTransform.pivot = new Vector2(0f, 0.5f);
                tmp.color = new Color(0.95f, 0.95f, 1f);
                textGo.AddComponent<FaceCamera>();
                decorations.Add(textGo);
            }
            if (marker != null) marker.localPosition = parkingPath[0];
            if (label != null) label.text = Caption(phase);
        }

        /// <summary>
        /// Del marco rotatorio CR3BP (km) a la vista: cerca de la Tierra 1 unidad ≈ 1 000 km, el tramo Tierra-Luna
        /// se comprime a 70 unidades y alrededor de la Luna se amplía para que el sobrevuelo se lea (no a escala).
        /// </summary>
        public static Vector3 Map(float xKm, float yKm)
        {
            double mu = FreeReturnTrajectory.Mu, L = FreeReturnTrajectory.LengthKm;
            double ex = xKm + mu * L, ey = yKm, de = System.Math.Sqrt(ex * ex + ey * ey);
            double sEarth = (MoonDisplayDistance - EarthRadius) / (L - FreeReturnTrajectory.EarthRadiusKm);
            double re = de <= FreeReturnTrajectory.EarthRadiusKm
                ? de / FreeReturnTrajectory.EarthRadiusKm * EarthRadius                       // dentro de la Tierra: lineal
                : EarthRadius + (de - FreeReturnTrajectory.EarthRadiusKm) * sEarth;
            Vector2 pe = de > 1e-6 ? new Vector2((float)(ex / de * re), (float)(ey / de * re)) : Vector2.zero;

            double mx = xKm - (1 - mu) * L, my = yKm, dm = System.Math.Sqrt(mx * mx + my * my);
            double rm = dm <= FreeReturnTrajectory.MoonRadiusKm
                ? dm / FreeReturnTrajectory.MoonRadiusKm * MoonDisplayRadius
                : MoonDisplayRadius + MoonZoom(dm - FreeReturnTrajectory.MoonRadiusKm, sEarth);
            Vector2 pm = new Vector2(MoonDisplayDistance, 0f) + (dm > 1e-6 ? new Vector2((float)(mx / dm * rm), (float)(my / dm * rm)) : Vector2.zero);

            float w = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(60000f, 15000f, (float)dm));
            Vector2 p = Vector2.Lerp(pe, pm, w);
            return new Vector3(-p.x, 0f, p.y);          // la Luna queda en −X, como en el resto de vistas
        }

        /// <summary>
        /// Ampliación alrededor de la Luna que se desvanece con la distancia: cerca vale 3,2 u / 7400 km (el sobrevuelo se ve),
        /// lejos tiende a la escala terrestre. Es monótona, así que la mezcla con la vista terrestre no forma bucles.
        /// </summary>
        private static double MoonZoom(double altitudeKm, double sEarth)
        {
            const double near = 3.2 / 7400.0, fade = 12000.0;
            return sEarth * altitudeKm + (near - sEarth) * altitudeKm * System.Math.Exp(-altitudeKm / fade);
        }

        private void AddArrows(List<Vector3> path, Material mat, int count)
        {
            if (path.Count < 4 || mat == null) return;
            for (int k = 1; k <= count; k++)
            {
                int i = Mathf.Clamp(path.Count * k / (count + 1), 1, path.Count - 2);
                Vector3 dir = (path[i + 1] - path[i - 1]).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                var go = new GameObject($"Arrow_{mat.name}_{k}");
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = mat;
                lr.useWorldSpace = false;
                lr.widthMultiplier = 0.45f;
                lr.positionCount = 3;
                lr.SetPositions(new[] { path[i] - dir * 2.2f + side * 1.6f, path[i], path[i] - dir * 2.2f - side * 1.6f });
                decorations.Add(go);
            }
        }

        private void ClearDecorations()
        {
            foreach (var d in decorations) if (d != null) Destroy(d);
            decorations.Clear();
        }

        // ───────────── Animación ─────────────

        private void Update()
        {
            if (marker == null) return;
            phaseTime += Time.deltaTime;
            if (regime == OrbitRegime.LunarFlyby && flybyPath.Count > 1) AnimateFreeReturn();
            else AnimateOrbit();

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

        private void AnimateOrbit()
        {
            switch (phase)
            {
                case MissionPhase.Launch:
                    marker.localPosition = Along(parkingPath, Mathf.Repeat(phaseTime / orbitSeconds, 1f) * 0.25f);
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
        }

        /// <summary>Recorre la trayectoria calculada por ventanas de tiempo de misión según la fase.</summary>
        private void AnimateFreeReturn()
        {
            float flyby = freeReturn.flybyTimeDays, total = freeReturn.totalDays;
            float k = Mathf.Clamp01(phaseTime / orbitSeconds);
            switch (phase)
            {
                case MissionPhase.Launch:
                    marker.localPosition = Along(parkingPath, Mathf.Repeat(phaseTime / orbitSeconds, 1f));
                    return;
                case MissionPhase.OrbitInsertion: marker.localPosition = AtDay(Mathf.Lerp(0f, 0.6f, k)); return;
                case MissionPhase.ScienceOperations: marker.localPosition = AtDay(Mathf.Lerp(0.6f, flyby + 0.4f, k)); return;
                case MissionPhase.DataTransmission: marker.localPosition = AtDay(Mathf.Lerp(flyby + 0.4f, total - 0.4f, k)); return;
                case MissionPhase.ReturnAndRecovery:
                case MissionPhase.PostMission: marker.localPosition = AtDay(Mathf.Lerp(total - 0.4f, total, k)); return;
            }
        }

        private Vector3 AtDay(float day)
        {
            for (int i = 1; i < flybyPath.Count; i++)
                if (flybyPath[i].t >= day)
                {
                    var (t0, p0) = flybyPath[i - 1];
                    var (t1, p1) = flybyPath[i];
                    return Vector3.Lerp(p0, p1, t1 > t0 ? (day - t0) / (t1 - t0) : 0f);
                }
            return flybyPath[flybyPath.Count - 1].p;
        }

        private string Caption(MissionPhase p)
        {
            if (regime == OrbitRegime.LunarFlyby)
            {
                switch (p)
                {
                    case MissionPhase.Launch: return "Ascenso y órbita terrestre alta (verificación de sistemas)";
                    case MissionPhase.OrbitInsertion: return "Inyección translunar (TLI) · ida en morado";
                    case MissionPhase.ScienceOperations: return "Correcciones de rumbo y sobrevuelo por la cara oculta";
                    case MissionPhase.DataTransmission: return "Retorno libre · vuelta en naranja · enlace láser";
                    case MissionPhase.ReturnAndRecovery: return "Reentrada y amerizaje · recuperación de tarjetas SD";
                    default: return "Trayectoria de retorno libre calculada (CR3BP) · no a escala";
                }
            }
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
