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
        /// <summary>Radio de la Luna en la vista (no a escala, como la ESA: ~la mitad de la Tierra para que se reconozca).</summary>
        public const float MoonDisplayRadius = 3.2f;
        public const float LunarOrbitRadius = MoonDisplayRadius + 1.2f;

        /// <summary>Las órbitas previas a la TLI se dibujan más compactas (fracción de su altura en la vista).</summary>
        public const float PreTliCompaction = 0.42f;

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
        /// <summary>Círculos numerados y sus números: se achican durante el acercamiento a la Luna.</summary>
        private readonly List<Transform> badges = new List<Transform>();
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

        private Material parkingDefaultMaterial;

        private void Start()
        {
            FreeReturnTrajectory.WarmUpAsync();
            if (parkingLine != null) parkingDefaultMaterial = parkingLine.sharedMaterial;
            if (moon != null && moon.TryGetComponent<MeshFilter>(out var moonMesh))
            {
                // Esfera con la parametrización del mapa de NASA (longitud 0 en +X local, norte en +Y local) y orientada
                // como en el diagrama de la ESA: la cara visible mira a la cámara cenital y el norte lunar queda arriba.
                moonMesh.sharedMesh = MoonPhotoStudio.HiResSphere(128, 64);
                moon.localRotation = Quaternion.LookRotation(Vector3.back, Vector3.right);
            }
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
                // Como el diagrama de la ESA: Tierra arriba y Luna abajo; la ida baja por la derecha, rodea la cara oculta
                // en sentido horario y la vuelta sube por la izquierda hasta cruzarse con la ida cerca de la Tierra.
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
            if (parkingLine != null && parkingDefaultMaterial != null) parkingLine.sharedMaterial = parkingDefaultMaterial;

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
                var p = MapPreTli(pre.xKm, pre.yKm);
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
            // Como en la ESA, las vueltas a la Tierra antes de la TLI son parte de la ida (morado).
            if (parkingLine != null && outboundMaterial != null) parkingLine.sharedMaterial = outboundMaterial;
            Draw(parkingLine, parkingPath);
            Draw(outboundLine, outbound);
            Draw(returnLine, back);
            AddArrows(outbound, outboundMaterial, 5);
            AddArrows(back, returnMaterial, 6);

            // Como el diagrama de la ESA: un círculo numerado sobre cada hito y una leyenda aparte (1-8 + Ida/Vuelta),
            // así ningún texto ni guía cruza la Tierra o la trayectoria.
            var idaColor = new Color(0.62f, 0.5f, 1f);
            var vueltaColor = new Color(1f, 0.68f, 0.2f);
            foreach (var m in freeReturn.milestones)
            {
                var pos = m.tDays < 0f ? MapPreTli(m.xKm, m.yKm) : Map(m.xKm, m.yKm);
                // Como en el diagrama de la ESA, el 1 (despegue desde Florida) va sobre la Tierra.
                if (m.number == 1) pos = new Vector3(-1.5f, 0f, 1.5f);
                bool ida = m.number <= 5;
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = $"Milestone_{m.number}";
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform, false);
                // Por encima de la esfera terrestre (radio 6,4) para que el hito del despegue no quede tapado.
                go.transform.localPosition = new Vector3(pos.x, BadgeHeight, pos.z);
                go.transform.localScale = Vector3.one * 3.0f;
                if (milestoneMaterial != null) go.GetComponent<Renderer>().sharedMaterial = ida ? outboundMaterial : returnMaterial;
                decorations.Add(go);
                badges.Add(go.transform);

                var number = new GameObject($"MilestoneNumber_{m.number}");
                number.transform.SetParent(transform, false);
                number.transform.localPosition = new Vector3(pos.x, BadgeHeight + 1.6f, pos.z);
                var num = number.AddComponent<TextMeshPro>();
                num.text = $"<b>{m.number}</b>";
                num.fontSize = 22f;
                num.alignment = TextAlignmentOptions.Center;
                num.textWrappingMode = TextWrappingModes.NoWrap;
                num.color = ida ? Color.white : new Color(0.12f, 0.07f, 0f);
                num.rectTransform.sizeDelta = new Vector2(4f, 4f);
                number.AddComponent<FaceCamera>();
                decorations.Add(number);
                badges.Add(number.transform);

                AddLegendLine(LegendRow(m.number), $"<b>{m.number}</b>  {m.title}", ida ? new Color(0.88f, 0.84f, 1f) : new Color(1f, 0.88f, 0.66f), null);
            }
            AddLegendLine(LegendRow(9) - 1.5f, "Ida", idaColor, outboundMaterial);
            AddLegendLine(LegendRow(10) - 1.5f, "Vuelta", vueltaColor, returnMaterial);

            BuildCraft();
            if (marker != null)
            {
                marker.localPosition = parkingPath.Count > 0 ? parkingPath[0] : Vector3.zero;
                SetMarkerVisible(craft == null);
            }
            if (label != null) label.text = Caption(phase);
        }

        private const float BadgeHeight = 7.5f;

        /// <summary>Borde izquierdo de la leyenda, pegada al margen izquierdo de la pantalla (+Z es la izquierda, +X arriba).</summary>
        private const float LegendColumnZ = 60f;

        private static float LegendRow(int row) => 11f - 5.2f * (row - 1);

        private void AddLegendLine(float x, string text, Color color, Material swatch)
        {
            var textGo = new GameObject("Legend_" + text.Replace("<b>", "").Replace("</b>", "").Trim());
            textGo.transform.SetParent(transform, false);
            textGo.transform.localPosition = new Vector3(x, 1.5f, LegendColumnZ);
            var tmp = textGo.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = 24f;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.outlineWidth = 0.2f;
            tmp.rectTransform.sizeDelta = new Vector2(70f, 6f);
            tmp.rectTransform.pivot = new Vector2(0f, 0.5f);
            if (swatch != null) textGo.transform.localPosition += new Vector3(0f, 0f, -8f);   // a la derecha de la muestra
            tmp.color = color;
            textGo.AddComponent<FaceCamera>();
            decorations.Add(textGo);
            if (swatch == null) return;
            // Muestra de color de la línea (como "— Ida / — Vuelta" en el diagrama), a la derecha del texto.
            var line = new GameObject("LegendSwatch_" + text).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.useWorldSpace = false;
            line.sharedMaterial = swatch;
            line.widthMultiplier = 0.6f;
            line.positionCount = 2;
            line.SetPositions(new[] { new Vector3(x, 1.5f, LegendColumnZ - 0.5f), new Vector3(x, 1.5f, LegendColumnZ - 6.5f) });
            decorations.Add(line.gameObject);
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
        /// Del marco rotatorio CR3BP (km) a la vista, "no a escala" como el diagrama de la ESA: la Tierra y la Luna se
        /// dibujan grandes, la zona cercana a la Tierra se amplía (órbitas previas a la TLI y el cruce del "8", que en la
        /// física real queda a ~25 000 km de la Tierra) y alrededor de la Luna también, para que el sobrevuelo se lea.
        /// Las distorsiones son radiales y monótonas: no cambian el recorrido, solo cómo se dibuja.
        /// Orientación real (vista desde el norte): la Luna abajo, el sentido de avance de la Luna (+y) a la derecha.
        /// </summary>
        public static Vector3 Map(float xKm, float yKm)
        {
            double mu = FreeReturnTrajectory.Mu, L = FreeReturnTrajectory.LengthKm;
            double ex = xKm + mu * L, ey = yKm, de = System.Math.Sqrt(ex * ex + ey * ey);
            double sEarth = (MoonDisplayDistance - EarthRadius) / (L - FreeReturnTrajectory.EarthRadiusKm);
            double re = de <= FreeReturnTrajectory.EarthRadiusKm
                ? de / FreeReturnTrajectory.EarthRadiusKm * EarthRadius                       // dentro de la Tierra: lineal
                : EarthRadius + (MoonDisplayDistance - EarthRadius) * NearEarthWarp((de - FreeReturnTrajectory.EarthRadiusKm) / (L - FreeReturnTrajectory.EarthRadiusKm));
            Vector2 pe = de > 1e-6 ? new Vector2((float)(ex / de * re), (float)(ey / de * re)) : Vector2.zero;

            double mx = xKm - (1 - mu) * L, my = yKm, dm = System.Math.Sqrt(mx * mx + my * my);
            double rm = dm <= FreeReturnTrajectory.MoonRadiusKm
                ? dm / FreeReturnTrajectory.MoonRadiusKm * MoonDisplayRadius
                : MoonDisplayRadius + MoonZoom(dm - FreeReturnTrajectory.MoonRadiusKm, sEarth)
                  // La Luna se dibuja más grande que su escala; ese extra se desvanece con la distancia para que, lejos de
                  // la Luna, esta vista coincida con la terrestre y la mezcla entre ambas no forme ganchos.
                  - (MoonDisplayRadius - sEarth * FreeReturnTrajectory.MoonRadiusKm) * (1 - System.Math.Exp(-(dm - FreeReturnTrajectory.MoonRadiusKm) / 25000.0));
            Vector2 pm = new Vector2(MoonDisplayDistance, 0f) + (dm > 1e-6 ? new Vector2((float)(mx / dm * rm), (float)(my / dm * rm)) : Vector2.zero);

            float w = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(60000f, 15000f, (float)dm));
            Vector2 p = Vector2.Lerp(pe, pm, w);
            // La Luna queda en −X, como en el resto de vistas; +y (hacia donde avanza la Luna) va a −Z, que la cámara
            // muestra a la derecha: así se ve desde el norte, con la Tierra en sentido antihorario y la Luna en horario.
            return new Vector3(-p.x, 0f, -p.y);
        }

        /// <summary>Intensidad de la ampliación logarítmica cerca de la Tierra (más alto = el cruce del "8" baja hacia el centro).</summary>
        public const double NearEarthLogK = 60.0;

        /// <summary>
        /// u ∈ [0,1] (superficie terrestre → distancia de la Luna) → fracción dibujada. Mezcla una escala logarítmica
        /// (amplía las decenas de miles de km donde se cruza el "8", pero casi no despega la órbita baja de la superficie)
        /// con la lineal: g(0)=0, g(1)=1, g'(1)=1 (empalma con la ampliación lunar) y es creciente en todo el tramo.
        /// </summary>
        public static double NearEarthWarp(double u)
        {
            if (u <= 0) return 0;
            if (u >= 1) return u;
            double log = System.Math.Log(1 + NearEarthLogK * u) / System.Math.Log(1 + NearEarthLogK);
            double k = (1 - u) * (1 - u);
            return u + (log - u) * k;
        }

        /// <summary>Como <see cref="Map"/>, pero con las órbitas previas a la TLI compactadas alrededor de la Tierra.</summary>
        public static Vector3 MapPreTli(float xKm, float yKm)
        {
            var p = Map(xKm, yKm);
            float r = p.magnitude;
            if (r <= EarthRadius) return p;
            return p / r * (EarthRadius + (r - EarthRadius) * PreTliCompaction);
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
            badges.Clear();
        }

        // ───────────── Animación ─────────────

        private void Update()
        {
            if (marker == null) return;
            phaseTime += Time.deltaTime;
            if (regime == OrbitRegime.LunarFlyby && flybyPath.Count > 1) AnimateFreeReturn();
            else AnimateOrbit();
            // En el acercamiento a la Luna los círculos numerados se ocultan: vistos en ángulo se separan de la línea.
            float shrink = 1f - FlybyZoom();
            foreach (var b in badges)
                if (b != null) b.localScale = (b.name.StartsWith("MilestoneNumber") ? Vector3.one : Vector3.one * 3f) * shrink;

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
                // Durante el despegue la cámara muestra el cohete en la plataforma: la nave espera en el hito 1.
                case MissionPhase.Launch: day = start; break;
                // El mapa aparece en esta fase: primero recorre 1 → 2 → 3 (órbita baja y alta) y luego la TLI (4).
                // El tramo inicial queda en el 1 mientras dura el plano de reacción de la sala.
                case MissionPhase.OrbitInsertion:
                {
                    const float hold = 0.12f, preTli = 0.75f;
                    day = k < preTli
                        ? Mathf.Lerp(start, 0f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(hold, preTli, k)))
                        : Mathf.Lerp(0f, 0.6f, Mathf.InverseLerp(preTli, 1f, k));
                    break;
                }
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
