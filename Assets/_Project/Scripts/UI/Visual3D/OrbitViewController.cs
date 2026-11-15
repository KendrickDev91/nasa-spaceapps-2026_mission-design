using System;
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
        [Tooltip("Modelo de la nave (Orion) que recorre la trayectoria del sobrevuelo.")]
        [SerializeField] private GameObject spacecraftPrefab;
        [SerializeField] private Material trailMaterial;
        [Tooltip("Si hay simulación en curso, la nave avanza con el progreso real de la fase (sin pausas ni saltos).")]
        [SerializeField] private MissionSimulationRunner runner;
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
        private readonly List<TrajectorySample> kmPath = new List<TrajectorySample>();
        private Transform craft;
        private TrailRenderer trail;
        private int nextMilestone;
        private float smoothK;

        /// <summary>Telemetría del vuelo de retorno libre (para el HUD).</summary>
        public float CurrentDay { get; private set; }
        public float DistanceEarthKm { get; private set; }
        public float DistanceMoonKm { get; private set; }
        public float SpeedKmS { get; private set; }
        public FreeReturnSolution Solution => freeReturn;
        public MissionPhase Phase => phase;
        public Transform Craft => craft;
        public event Action<TrajectoryMilestone> MilestoneReached;

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
                // Como el diagrama de la ESA: Tierra arriba, Luna abajo, ida a la izquierda y vuelta a la derecha.
                // El "8" ocupa la mitad izquierda de la pantalla; la derecha queda para la telemetría y las fotos.
                Vector3 target = center + new Vector3(-33f, 0f, -38f);
                var wide = new Pose(target + Vector3.up * 162f, Quaternion.LookRotation(Vector3.down, Vector3.right));
                float zoom = FlybyZoom();
                if (zoom <= 0.001f || craft == null) return wide;
                // Durante el sobrevuelo la cámara baja hacia la Luna y sigue a la nave.
                Vector3 focus = Vector3.Lerp(transform.TransformPoint(new Vector3(-MoonDisplayDistance, 0f, 0f)), craft.position, 0.35f);
                Vector3 eye = focus + new Vector3(10f, 26f, -9f);
                var close = new Pose(eye, Quaternion.LookRotation(focus - eye, Vector3.right));
                return new Pose(Vector3.Lerp(wide.position, close.position, zoom), Quaternion.Slerp(wide.rotation, close.rotation, zoom));
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
            smoothK = 0f;
            if (p == MissionPhase.Launch || p == MissionPhase.Design) Prepare();
            if (p == MissionPhase.Launch && trail != null) trail.Clear();
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
            kmPath.Clear();
            nextMilestone = 0;
            Draw(outboundLine, new List<Vector3>());
            Draw(returnLine, new List<Vector3>());

            if (regime == OrbitRegime.LunarFlyby)
            {
                PrepareFreeReturn();
                return;
            }
            if (craft != null) { Destroy(craft.gameObject); craft = null; trail = null; }
            if (marker != null) SetMarkerVisible(true);

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
            foreach (var pre in freeReturn.preTliSamples)
            {
                var p = Map(pre.xKm, pre.yKm);
                flybyPath.Add((pre.tDays, p));
                kmPath.Add(pre);
                parkingPath.Add(p);
            }
            foreach (var sample in freeReturn.samples)
            {
                var p = Map(sample.xKm, sample.yKm);
                flybyPath.Add((sample.tDays, p));
                kmPath.Add(sample);
                if (sample.leg == 0) outbound.Add(p);
                else
                {
                    if (back.Count == 0 && outbound.Count > 0) back.Add(outbound[outbound.Count - 1]);
                    back.Add(p);
                }
            }
            Draw(parkingLine, parkingPath);
            Draw(outboundLine, outbound);
            Draw(returnLine, back);
            AddArrows(outbound, outboundMaterial, 5);
            AddArrows(back, returnMaterial, 6);

            foreach (var m in freeReturn.milestones)
            {
                var pos = Map(m.xKm, m.yKm);
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = $"Milestone_{m.number}";
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform, false);
                go.transform.localPosition = pos + Vector3.up * 0.3f;
                go.transform.localScale = Vector3.one * 1.6f;
                if (milestoneMaterial != null) go.GetComponent<Renderer>().sharedMaterial = m.number <= 5 ? outboundMaterial : returnMaterial;
                decorations.Add(go);

                // Etiquetas como en el diagrama: hitos de ida a la izquierda (+Z) y de vuelta a la derecha (-Z);
                // los de la Tierra (1-3 y 7-8) en columnas a ambos lados del planeta, con una guía hasta el hito.
                var textGo = new GameObject($"MilestoneLabel_{m.number}");
                textGo.transform.SetParent(transform, false);
                // 1-4: columna a la izquierda de la Tierra · 5: bajo la Luna · 6: dentro del "8" · 7-8: columna a la derecha.
                bool left = m.number <= 4 || m.number == 6;
                bool center = false;
                if (m.number == 5) left = true;
                Vector3 labelPos;
                if (m.number <= 4) labelPos = new Vector3(9f - 5.5f * (m.number - 1), 1.5f, 12f);
                else if (m.number >= 7) labelPos = new Vector3(-4f + 6f * (m.number - 7), 1.5f, -21f);
                else if (m.number == 5) labelPos = pos + new Vector3(0f, 1.5f, 6f);
                else labelPos = pos + new Vector3(0f, 1.5f, 3f);
                textGo.transform.localPosition = labelPos;
                var tmp = textGo.AddComponent<TextMeshPro>();
                tmp.text = $"<b>{m.number}</b>  {m.title}";
                tmp.fontSize = 26f;
                tmp.alignment = center ? TextAlignmentOptions.Center : left ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.outlineWidth = 0.2f;
                tmp.rectTransform.sizeDelta = new Vector2(70f, 8f);
                tmp.rectTransform.pivot = new Vector2(center ? 0.5f : left ? 1f : 0f, 0.5f);
                tmp.color = m.number <= 5 ? new Color(0.86f, 0.8f, 1f) : new Color(1f, 0.86f, 0.62f);
                textGo.AddComponent<FaceCamera>();
                decorations.Add(textGo);
                if (m.number <= 4 || m.number >= 7)
                {
                    var guide = new GameObject($"MilestoneGuide_{m.number}").AddComponent<LineRenderer>();
                    guide.transform.SetParent(transform, false);
                    guide.useWorldSpace = false;
                    guide.sharedMaterial = left ? outboundMaterial : returnMaterial;
                    guide.widthMultiplier = 0.12f;
                    guide.positionCount = 2;
                    guide.SetPositions(new[] { labelPos + new Vector3(0f, -1f, left ? -1f : 1f), pos });
                    decorations.Add(guide.gameObject);
                }
            }

            BuildCraft();
            if (marker != null)
            {
                marker.localPosition = parkingPath.Count > 0 ? parkingPath[0] : Vector3.zero;
                SetMarkerVisible(craft == null);
            }
            if (label != null) label.text = Caption(phase);
        }

        private void BuildCraft()
        {
            if (craft != null) return;
            var root = new GameObject("FlybyCraft").transform;
            root.SetParent(transform, false);
            if (spacecraftPrefab != null)
            {
                var model = Instantiate(spacecraftPrefab, root);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                var rends = model.GetComponentsInChildren<Renderer>();
                if (rends.Length > 0)
                {
                    var b = rends[0].bounds;
                    foreach (var r in rends) b.Encapsulate(r.bounds);
                    float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
                    if (size > 1e-3f) model.transform.localScale *= 3.2f / size;
                }
            }
            else
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(sphere.GetComponent<Collider>());
                sphere.transform.SetParent(root, false);
            }
            var glow = root.gameObject.AddComponent<Light>();
            glow.type = LightType.Point; glow.range = 10f; glow.intensity = 4f; glow.color = new Color(1f, 0.92f, 0.7f);
            trail = root.gameObject.AddComponent<TrailRenderer>();
            trail.time = 3.5f;
            trail.widthMultiplier = 0.9f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.minVertexDistance = 0.3f;
            trail.sharedMaterial = trailMaterial != null ? trailMaterial : outboundMaterial;
            trail.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(new Color(1f, 0.95f, 0.75f), 0f), new GradientColorKey(new Color(1f, 0.6f, 0.2f), 1f) },
                alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            };
            craft = root;
        }

        private void SetMarkerVisible(bool visible)
        {
            var r = marker.GetComponent<Renderer>();
            if (r != null) r.enabled = visible;
            var l = marker.GetComponent<Light>();
            if (l != null) l.enabled = visible;
        }

        /// <summary>0 lejos de la Luna, 1 en pleno sobrevuelo: acerca la cámara a la cara oculta.</summary>
        private float FlybyZoom()
        {
            if (freeReturn == null || phase != MissionPhase.ScienceOperations) return 0f;
            float d = Mathf.Abs(CurrentDay - freeReturn.flybyTimeDays);
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.1f, 0.35f, d));
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
            // En el acercamiento al sobrevuelo los rótulos del diagrama se ocultan (serían enormes).
            bool showLabels = FlybyZoom() < 0.3f;
            foreach (var d in decorations)
                if (d != null && (d.name.StartsWith("MilestoneLabel_") || d.name.StartsWith("MilestoneGuide_")) && d.activeSelf != showLabels)
                    d.SetActive(showLabels);
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
            float flyby = freeReturn.flybyTimeDays, total = freeReturn.totalDays, start = freeReturn.launchDays;
            // Progreso de la fase: el del runner (dura lo mismo que la fase, eventos incluidos) o, sin runner, el tiempo.
            float target = runner != null && runner.IsRunning ? runner.PhaseProgress : Mathf.Clamp01(phaseTime / orbitSeconds);
            smoothK = Mathf.MoveTowards(smoothK, target, Time.deltaTime * 0.9f);
            if (target < smoothK) smoothK = target;
            float k = smoothK;
            float day;
            switch (phase)
            {
                case MissionPhase.Launch: day = Mathf.Lerp(start, 0f, Mathf.SmoothStep(0f, 1f, k)); break;
                case MissionPhase.OrbitInsertion: day = Mathf.Lerp(0f, 0.6f, k); break;
                case MissionPhase.ScienceOperations: day = Mathf.Lerp(0.6f, flyby + 0.5f, k); break;
                case MissionPhase.DataTransmission: day = Mathf.Lerp(flyby + 0.5f, total - 0.4f, k); break;
                case MissionPhase.ReturnAndRecovery:
                case MissionPhase.PostMission: day = Mathf.Lerp(total - 0.4f, total, k); break;
                default: day = start; break;
            }
            Vector3 pos = AtDay(day);
            Vector3 ahead = AtDay(day + 0.02f);
            marker.localPosition = pos;
            if (craft != null)
            {
                craft.localPosition = pos + Vector3.up * 0.5f;
                Vector3 dir = ahead - pos;
                if (dir.sqrMagnitude > 1e-6f)
                    craft.localRotation = Quaternion.Slerp(craft.localRotation, Quaternion.LookRotation(dir.normalized, Vector3.up), 1f - Mathf.Exp(-8f * Time.deltaTime));
                if (phase == MissionPhase.Design && trail != null) trail.Clear();
            }
            UpdateTelemetry(day);
        }

        private void UpdateTelemetry(float day)
        {
            CurrentDay = day;
            var a = KmAt(day);
            double mu = FreeReturnTrajectory.Mu, L = FreeReturnTrajectory.LengthKm;
            DistanceEarthKm = (float)Math.Sqrt((a.x + mu * L) * (a.x + mu * L) + a.y * a.y);
            DistanceMoonKm = (float)Math.Sqrt((a.x - (1 - mu) * L) * (a.x - (1 - mu) * L) + a.y * a.y);
            // Velocidad inercial: se deshace la rotación del marco Tierra-Luna (w = 2*pi / 27,32 días).
            const float dt = 0.01f;
            Vector2 p0 = Inertial(KmAt(day - dt), day - dt), p1 = Inertial(KmAt(day + dt), day + dt);
            SpeedKmS = (p1 - p0).magnitude / (2f * dt * 86400f);
            while (phase != MissionPhase.Design && nextMilestone < freeReturn.milestones.Count &&
                   day >= freeReturn.milestones[nextMilestone].tDays - 1e-4f)
            {
                MilestoneReached?.Invoke(freeReturn.milestones[nextMilestone]);
                nextMilestone++;
            }
        }

        private static Vector2 Inertial(Vector2 rotatingKm, float day)
        {
            float w = 2f * Mathf.PI / 27.321661f * day;
            float c = Mathf.Cos(w), sn = Mathf.Sin(w);
            return new Vector2(rotatingKm.x * c - rotatingKm.y * sn, rotatingKm.x * sn + rotatingKm.y * c);
        }

        private Vector2 KmAt(float day)
        {
            if (kmPath.Count == 0) return Vector2.zero;
            if (day <= kmPath[0].tDays) return new Vector2(kmPath[0].xKm, kmPath[0].yKm);
            for (int i = 1; i < kmPath.Count; i++)
                if (kmPath[i].tDays >= day)
                {
                    var p0 = kmPath[i - 1];
                    var p1 = kmPath[i];
                    float f = p1.tDays > p0.tDays ? (day - p0.tDays) / (p1.tDays - p0.tDays) : 0f;
                    return new Vector2(Mathf.Lerp(p0.xKm, p1.xKm, f), Mathf.Lerp(p0.yKm, p1.yKm, f));
                }
            var last = kmPath[kmPath.Count - 1];
            return new Vector2(last.xKm, last.yKm);
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
                    case MissionPhase.Launch: return "Despegue, órbita baja y órbita terrestre alta (~1 día de pruebas)";
                    case MissionPhase.OrbitInsertion: return "El motor principal impulsa la nave hacia la Luna (TLI) · ida en morado";
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
