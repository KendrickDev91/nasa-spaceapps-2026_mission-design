using System;
using System.Collections;
using System.Collections.Generic;
using MissionDesign.Simulation;
using TMPro;
using MissionDesign.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MissionDesign.UI.Visual3D
{
    /// <summary>
    /// Cinemática del amerizaje de Orion como el de Artemis II (10 de abril de 2026, Pacífico frente a San Diego):
    /// reentrada con plasma → 2 paracaídas de frenado (~7 600 m) → 3 principales naranja y blanco que se abren por etapas
    /// (~2 900 m) → amerizaje a ~27 km/h con salpicadura → bolsas de enderezamiento y buque de recuperación.
    /// Toda la geometría es procedural (sin modelos ni vídeos de terceros); los materiales llegan del builder.
    /// Dura ~17 s y se puede saltar (Espacio, Esc, clic o el botón "Saltar" de la simulación).
    /// </summary>
    public sealed class SplashdownCinematic : MonoBehaviour, IMissionCinematic
    {
        [Header("Materiales (los crea el builder)")]
        [SerializeField] private Material capsuleMaterial;
        [SerializeField] private Material heatShieldMaterial;
        [SerializeField] private Material chuteMaterial;
        [SerializeField] private Material drogueMaterial;
        [SerializeField] private Material lineMaterial;
        [SerializeField] private Material oceanMaterial;
        [SerializeField] private Material skyMaterial;
        [SerializeField] private Material plasmaMaterial;
        [SerializeField] private Material sprayMaterial;
        [SerializeField] private Material cloudMaterial;
        [SerializeField] private Material airbagMaterial;
        [SerializeField] private Material shipMaterial;

        [Header("Ritmo")]
        [SerializeField] private float timeScale = 1f;

        /// <summary>Tomas de la cinemática (s): reentrada, frenado, principales, descenso final, amerizaje y fin.</summary>
        public const float TReentry = 0f, TDrogues = 3.2f, TMains = 6.2f, TFinal = 9.4f, TSplash = 12f, TEnd = 17.5f;

        /// <summary>Velocidad real de amerizaje de Orion con 3 paracaídas: ~27 km/h.</summary>
        public const float SplashSpeedMs = 7.5f;

        public bool Playing { get; private set; }
        /// <summary>Cámara de la cinemática (también dibuja los rótulos).</summary>
        public Camera Camera => cam;
        /// <summary>Nombre de la toma actual (para pruebas y depuración).</summary>
        public string Shot { get; private set; } = "";

        private Transform root, capsule, ocean, sky, ship, shockGlow, wake;
        private Camera cam;
        private Light sun;
        private Mesh oceanMesh;
        private Vector3[] oceanBase, oceanVerts;
        private readonly List<Transform> mains = new List<Transform>(), drogues = new List<Transform>(), airbags = new List<Transform>();
        private readonly List<(LineRenderer line, Transform canopy, Vector3[] skirt)> rigging = new List<(LineRenderer, Transform, Vector3[])>();
        private ParticleSystem plasma, spray, foam, clouds;
        private AudioSource audioSource;
        private Canvas overlay;
        private TMP_Text title, subtitle, hint;
        private Image fade;
        private readonly List<Canvas> hiddenCanvases = new List<Canvas>();
        private readonly List<Camera> pausedCameras = new List<Camera>();

        public bool WantsToPlay(MissionOutcomeReport o) =>
            o != null && o.returnsToEarth && o.launchSucceeded && o.orbitInsertionSucceeded && o.survivedOperations;

        public IEnumerator Play(MissionOutcomeReport outcome, Func<bool> skipRequested)
        {
            Build();
            Playing = true;
            HideOtherCanvases(true);
            PauseOtherCameras(true);
            root.gameObject.SetActive(true);
            var fog = (RenderSettings.fog, RenderSettings.fogMode, RenderSettings.fogColor, RenderSettings.fogDensity);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.62f, 0.74f, 0.86f);

            float t = 0f;
            int stage = -1;
            bool skip = false;
            while (t < TEnd && !skip)
            {
                skip = (skipRequested != null && skipRequested()) || SkipPressed();
                int newStage = t < TDrogues ? 0 : t < TMains ? 1 : t < TFinal ? 2 : t < TSplash ? 3 : 4;
                if (newStage != stage) { stage = newStage; EnterStage(stage); }
                Animate(t);
                t += Time.deltaTime * timeScale;
                yield return null;
            }

            // Fundido a negro y vuelta a la sala de control / análisis.
            for (float f = 0f; f < 0.5f; f += Time.deltaTime)
            {
                SetFade(f / 0.5f);
                yield return null;
            }
            (RenderSettings.fog, RenderSettings.fogMode, RenderSettings.fogColor, RenderSettings.fogDensity) = fog;
            root.gameObject.SetActive(false);
            HideOtherCanvases(false);
            PauseOtherCameras(false);
            SetFade(0f);
            Playing = false;
            Shot = "";
        }

        private static bool SkipPressed()
        {
            var k = Keyboard.current;
            var m = Mouse.current;
            return (k != null && (k.spaceKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame))
                   || (m != null && m.leftButton.wasPressedThisFrame);
        }

        // ───────────── Guion ─────────────

        private void EnterStage(int stage)
        {
            switch (stage)
            {
                case 0:
                    Shot = "reentrada";
                    Caption("REENTRADA", "Interfaz de entrada a 122 km · ~11 km/s · el escudo térmico supera los 2 700 °C");
                    PlaySound(Synth.ReentryRoar(TDrogues));
                    ocean.gameObject.SetActive(false);
                    foreach (var d in drogues) d.gameObject.SetActive(false);
                    foreach (var m in mains) m.gameObject.SetActive(false);
                    foreach (var a in airbags) a.gameObject.SetActive(false);
                    plasma.Play();
                    clouds.Stop();
                    shockGlow.gameObject.SetActive(true);
                    wake.gameObject.SetActive(true);
                    break;
                case 1:
                    Shot = "frenado";
                    Caption("PARACAÍDAS DE FRENADO", "A ~7 600 m se suelta la cubierta y se abren 2 paracaídas de frenado (drogues)");
                    PlaySound(Synth.ChuteSnap());
                    plasma.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    shockGlow.gameObject.SetActive(false);
                    wake.gameObject.SetActive(false);
                    foreach (var d in drogues) d.gameObject.SetActive(true);
                    clouds.Play();
                    break;
                case 2:
                    Shot = "principales";
                    Caption("3 PARACAÍDAS PRINCIPALES", "A ~2 900 m se sueltan los de frenado y se abren los 3 principales (35 m de diámetro cada uno)");
                    PlaySound(Synth.ChuteSnap());
                    foreach (var d in drogues) d.gameObject.SetActive(false);
                    foreach (var m in mains) m.gameObject.SetActive(true);
                    break;
                case 3:
                    Shot = "descenso";
                    Caption("DESCENSO FINAL", "Océano Pacífico, frente a San Diego · bajando a ~27 km/h");
                    ocean.gameObject.SetActive(true);
                    clouds.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    break;
                case 4:
                    Shot = "amerizaje";
                    Caption("¡AMERIZAJE!", "Océano Pacífico, frente a San Diego · Bienvenidos a casa, tripulación");
                    PlaySound(Synth.Splash());
                    spray.Play();
                    foam.Play();
                    ship.gameObject.SetActive(true);
                    foreach (var a in airbags) a.gameObject.SetActive(true);
                    break;
            }
        }

        private void Animate(float t)
        {
            float time = Time.time;
            Vector3 up = Vector3.up;
            // Altura ficticia por toma: el agua solo aparece en el descenso final (a la velocidad real de amerizaje).
            float h = t < TDrogues ? 2000f - t * 120f : t < TMains ? 1200f - (t - TDrogues) * 60f : t < TFinal ? 600f - (t - TMains) * 30f
                : Mathf.Max(0f, SplashSpeedMs * (TSplash - t));
            float bob = t >= TSplash ? Mathf.Sin(time * 1.3f) * 0.35f + WaveHeight(0f, 0f, time) : 0f;
            // En la reentrada también avanza en horizontal: la trayectoria es una diagonal larga sobre el Pacífico.
            float x = t < TDrogues ? (t - TDrogues) * 160f : 0f;
            capsule.localPosition = new Vector3(x, h + bob, 0f);
            Vector3 c = capsule.localPosition;

            if (t < TDrogues)
            {
                // Reentrada: el escudo térmico mira hacia donde avanza (abajo y adelante) y la cápsula vibra.
                var velocity = new Vector3(160f, -120f, 0f);
                capsule.localRotation = Quaternion.FromToRotation(Vector3.down, velocity.normalized)
                                        * Quaternion.Euler(Mathf.Sin(time * 23f) * 1.5f, time * 20f, Mathf.Sin(time * 17f) * 1.2f);
                SetSky(0.02f + 0.12f * t / TDrogues);
                // El plasma parpadea y se estira; se apaga al final de la toma (la nave ya frenó).
                float heat = 1f - Mathf.Clamp01((t - (TDrogues - 0.8f)) / 0.8f);
                float flicker = 1f + Mathf.PerlinNoise(time * 9f, 0.3f) * 0.25f;
                shockGlow.localScale = new Vector3(1.12f, 1.25f, 1.12f) * (heat * flicker + 0.001f);
                wake.localScale = new Vector3(1f, 0.6f + 0.6f * flicker, 1f) * (heat + 0.001f);
                // Cámara por delante y de lado: se ve el escudo incandescente que avanza hacia nosotros y la estela detrás.
                Look(c + new Vector3(13f, -5f, -15f), c + new Vector3(-3f, 2f, 0f), 50f);
            }
            else
            {
                // Bajo los paracaídas: escudo térmico abajo, balanceo suave.
                float swing = t >= TSplash ? 0f : 1f;
                capsule.localRotation = Quaternion.Euler(Mathf.Sin(time * 0.9f) * 4f * swing, time * 6f, Mathf.Cos(time * 0.7f) * 3f * swing);
                SetSky(t < TMains ? 0.55f : 0.85f);
            }

            if (t >= TDrogues && t < TMains)
            {
                float k = Mathf.Clamp01((t - TDrogues) / 0.5f);
                foreach (var d in drogues) d.localScale = new Vector3(k, 1f + (1f - k) * 2f, k) * (1f + Mathf.Sin(time * 6f + d.GetHashCode()) * 0.04f);
                Look(c + new Vector3(16f, -9f, -20f), c + new Vector3(0f, 14f, 0f), 50f);
            }
            if (t >= TMains)
            {
                // Apertura por etapas (reefing): ~25 % → ~55 % → 100 %, como los principales de Orion.
                float s = t - TMains;
                float open = s < 0.9f ? Mathf.Lerp(0.08f, 0.25f, s / 0.9f) : s < 2f ? Mathf.Lerp(0.25f, 0.55f, (s - 0.9f) / 1.1f) : Mathf.Lerp(0.55f, 1f, Mathf.Clamp01((s - 2f) / 0.8f));
                float collapse = t >= TSplash ? Mathf.Clamp01((t - TSplash - 0.6f) / 2.2f) : 0f;
                for (int i = 0; i < mains.Count; i++)
                {
                    var m = mains[i];
                    float breathe = 1f + Mathf.Sin(time * 2.1f + i * 2.1f) * 0.035f;
                    m.localScale = new Vector3(open * breathe, (1f + (1f - open) * 1.6f) * (1f - collapse * 0.85f), open * breathe);
                    // Al tocar el agua se vacían y caen a un lado arrastrados por el viento.
                    // El viento las arrastra lejos de la cámara (que mira desde -X -Z).
                    m.localPosition = MainOffset(i) + new Vector3(collapse * 40f, -collapse * 51f, collapse * 46f);
                    if (collapse > 0f) m.localScale = Vector3.Scale(m.localScale, new Vector3(1f - collapse * 0.45f, 1f, 1f - collapse * 0.45f));
                }
                if (t < TFinal)
                    Look(c + new Vector3(Mathf.Sin(s * 0.25f) * 26f, -16f, -30f), c + new Vector3(0f, 34f, 0f), 58f);
                else if (t < TSplash + 0.4f)
                    Look(new Vector3(-70f, 9f, -96f), c + new Vector3(0f, 30f, 0f), 58f);
                else
                {
                    float e = t - TSplash;
                    Look(new Vector3(Mathf.Cos(0.6f + e * 0.08f) * -34f, 8f + e * 0.8f, Mathf.Sin(0.6f + e * 0.08f) * -40f), c + new Vector3(8f, 2f, 10f), 46f);
                }
                foreach (var a in airbags)
                {
                    float k = Mathf.Clamp01((t - TSplash - 1.2f) / 1.4f);
                    a.localScale = Vector3.one * (k * 1.25f);
                }
            }

            UpdateRigging();
            if (ocean.gameObject.activeSelf) AnimateOcean(time);
            if (sky != null) sky.position = cam.transform.position;
            if (t >= TEnd - 0.6f) SetFade((t - (TEnd - 0.6f)) / 0.6f * 0.6f);
        }

        /// <summary>Coloca la cámara (coordenadas locales del set).</summary>
        private void Look(Vector3 eyeLocal, Vector3 targetLocal, float fov)
        {
            Vector3 eye = root.TransformPoint(eyeLocal), target = root.TransformPoint(targetLocal);
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye, Vector3.up));
            cam.fieldOfView = fov;
        }

        // ───────────── Construcción ─────────────

        private void Build()
        {
            if (root != null) return;
            root = new GameObject("SplashdownSet").transform;
            root.SetParent(transform, false);

            var camGo = new GameObject("CinematicCamera");
            camGo.transform.SetParent(root, false);
            cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.45f, 0.62f, 0.82f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 6000f;
            cam.depth = 50f;
            audioSource = camGo.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;

            sun = new GameObject("CinematicSun").AddComponent<Light>();
            sun.transform.SetParent(root, false);
            sun.type = LightType.Directional;
            sun.intensity = 1.15f;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(38f, -30f, 0f);

            sky = new GameObject("SkyDome").transform;
            sky.SetParent(root, false);
            sky.gameObject.AddComponent<MeshFilter>().sharedMesh = InvertedSphere(MoonPhotoStudio.HiResSphere(48, 24));
            sky.gameObject.AddComponent<MeshRenderer>().sharedMaterial = skyMaterial;
            sky.localScale = Vector3.one * 5000f;

            BuildOcean();
            BuildCapsule();
            BuildChutes();
            BuildShip();
            BuildParticles();
            BuildOverlay();
            root.gameObject.SetActive(false);
        }

        private void BuildCapsule()
        {
            capsule = new GameObject("OrionCrewModule").transform;
            capsule.SetParent(root, false);
            // Módulo de tripulación de Orion: 5,02 m de diámetro, ~3,3 m de alto, cono de 32,5°.
            AddMesh(capsule, "Cone", Frustum(2.51f, 0.75f, 2.75f, 48), capsuleMaterial, Vector3.zero);
            AddMesh(capsule, "DockingTunnel", Frustum(0.62f, 0.55f, 0.55f, 32), capsuleMaterial, new Vector3(0f, 2.75f, 0f));
            AddMesh(capsule, "HeatShield", Cap(2.55f, 0.42f, 48, 8), heatShieldMaterial, Vector3.zero);
            // Ventanas: cuatro rectángulos oscuros en el cono.
            for (int i = 0; i < 4; i++)
            {
                var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(w.GetComponent<Collider>());
                w.name = "Window";
                w.transform.SetParent(capsule, false);
                float a = i * 90f + 45f;
                var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                w.transform.localPosition = dir * 1.6f + Vector3.up * 1.45f;
                w.transform.localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(-32.5f, 0f, 0f);
                w.transform.localScale = new Vector3(0.38f, 0.32f, 0.04f);
                w.GetComponent<Renderer>().sharedMaterial = heatShieldMaterial;
            }
            // Bolsas de enderezamiento (CMUS): 5 globos naranja sobre el cono tras amerizar.
            for (int i = 0; i < 5; i++)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(b.GetComponent<Collider>());
                b.name = "UprightingBag";
                b.transform.SetParent(capsule, false);
                var dir = Quaternion.Euler(0f, i * 72f, 0f) * Vector3.forward;
                b.transform.localPosition = dir * 0.55f + Vector3.up * 3.25f;
                b.GetComponent<Renderer>().sharedMaterial = airbagMaterial;
                airbags.Add(b.transform);
            }

            // Plasma de la reentrada: capa de choque incandescente delante del escudo y estela que se desvanece detrás.
            shockGlow = AddMesh(capsule, "ShockLayer", Tint(Cap(2.9f, 0.9f, 40, 6), new Color(1f, 0.62f, 0.2f, 0.95f), new Color(1f, 0.9f, 0.65f, 0.6f)),
                plasmaMaterial, new Vector3(0f, -0.15f, 0f));
            wake = AddMesh(capsule, "PlasmaWake", Tint(Frustum(2.7f, 0.3f, 34f, 32), new Color(1f, 0.45f, 0.1f, 0.55f), new Color(1f, 0.2f, 0.05f, 0f)),
                plasmaMaterial, new Vector3(0f, 0.6f, 0f));
            shockGlow.gameObject.SetActive(false);
            wake.gameObject.SetActive(false);
        }

        /// <summary>Colorea una malla con un degradado según la altura local (para los materiales de partículas).</summary>
        private static Mesh Tint(Mesh mesh, Color low, Color high)
        {
            var v = mesh.vertices;
            float min = float.MaxValue, max = float.MinValue;
            foreach (var p in v) { min = Mathf.Min(min, p.y); max = Mathf.Max(max, p.y); }
            var colors = new Color[v.Length];
            for (int i = 0; i < v.Length; i++) colors[i] = Color.Lerp(low, high, Mathf.InverseLerp(min, max, v[i].y));
            mesh.colors = colors;
            return mesh;
        }

        private void BuildChutes()
        {
            var confluence = new Vector3(0f, 2.9f, 0f);
            for (int i = 0; i < 2; i++)
            {
                var d = Canopy("Drogue", drogueMaterial, 3.5f, 16);
                d.SetParent(capsule, false);
                d.localPosition = new Vector3(i == 0 ? -4.5f : 4.5f, 24f, 0f);
                d.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 10f : -10f);
                drogues.Add(d);
                AddRigging(d, 3.5f, confluence, 6);
            }
            for (int i = 0; i < 3; i++)
            {
                var m = Canopy("Main", chuteMaterial, 17.7f, 32);
                m.SetParent(capsule, false);
                m.localPosition = MainOffset(i);
                var outward = Quaternion.Euler(0f, i * 120f, 0f) * Vector3.forward;
                m.localRotation = Quaternion.FromToRotation(Vector3.up, (Vector3.up * 2.6f + outward).normalized);
                mains.Add(m);
                AddRigging(m, 17.7f, confluence, 10);
            }
        }

        private static Vector3 MainOffset(int i) => Quaternion.Euler(0f, i * 120f, 0f) * Vector3.forward * 16f + Vector3.up * 52f;

        private Transform Canopy(string name, Material mat, float radius, int gores)
        {
            var t = new GameObject(name).transform;
            var mesh = new Mesh { name = name + "Canopy" };
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var tri = new List<int>();
            const int rings = 10;
            for (int r = 0; r <= rings; r++)
            {
                float phi = r / (float)rings * 1.35f;       // ~77°: el borde (falda) queda algo por debajo del ecuador
                for (int g = 0; g <= gores; g++)
                {
                    float th = g / (float)gores * Mathf.PI * 2f;
                    // Cada gajo se abomba un poco entre costuras, como la tela real.
                    float bulge = 1f + 0.035f * Mathf.Sin(th * gores) * Mathf.Sin(phi);
                    v.Add(new Vector3(Mathf.Sin(phi) * Mathf.Cos(th) * radius * bulge, Mathf.Cos(phi) * radius * 0.62f, Mathf.Sin(phi) * Mathf.Sin(th) * radius * bulge));
                    uv.Add(new Vector2(g / (float)gores, r / (float)rings));
                }
            }
            int row = gores + 1;
            for (int r = 0; r < rings; r++)
            for (int g = 0; g < gores; g++)
            {
                int i = r * row + g;
                tri.AddRange(new[] { i, i + 1, i + row, i + 1, i + row + 1, i + row });
            }
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateNormals();
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            t.gameObject.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return t;
        }

        private void AddRigging(Transform canopy, float radius, Vector3 confluence, int lines)
        {
            var lr = new GameObject("Lines_" + canopy.name).AddComponent<LineRenderer>();
            lr.transform.SetParent(capsule, false);
            lr.useWorldSpace = false;
            lr.sharedMaterial = lineMaterial;
            lr.widthMultiplier = 0.06f;
            lr.positionCount = lines * 2;
            var skirt = new Vector3[lines];
            for (int k = 0; k < lines; k++)
            {
                float th = k / (float)lines * Mathf.PI * 2f;
                skirt[k] = new Vector3(Mathf.Sin(1.35f) * Mathf.Cos(th) * radius, Mathf.Cos(1.35f) * radius * 0.62f, Mathf.Sin(1.35f) * Mathf.Sin(th) * radius);
            }
            rigging.Add((lr, canopy, skirt));
            lr.SetPosition(0, confluence);
        }

        private void UpdateRigging()
        {
            var confluence = new Vector3(0f, 2.9f, 0f);
            foreach (var (line, canopy, skirt) in rigging)
            {
                line.enabled = canopy.gameObject.activeSelf;
                if (!line.enabled) continue;
                // Zig-zag confluencia → borde → confluencia: un solo LineRenderer dibuja todo el abanico de cuerdas.
                for (int k = 0; k < skirt.Length; k++)
                {
                    var p = capsule.InverseTransformPoint(canopy.TransformPoint(skirt[k]));
                    line.SetPosition(k * 2, confluence);
                    line.SetPosition(k * 2 + 1, p);
                }
            }
        }

        private void BuildOcean()
        {
            ocean = new GameObject("Ocean").transform;
            ocean.SetParent(root, false);
            const int n = 110;
            const float size = 900f;
            oceanMesh = new Mesh { name = "Ocean", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            oceanBase = new Vector3[(n + 1) * (n + 1)];
            var tris = new List<int>();
            for (int z = 0; z <= n; z++)
            for (int x = 0; x <= n; x++)
            {
                // Celdas más finas cerca de la cápsula (centro) y más gruesas lejos.
                float u = x / (float)n * 2f - 1f, w = z / (float)n * 2f - 1f;
                oceanBase[z * (n + 1) + x] = new Vector3(Mathf.Sign(u) * u * u * size * 0.5f, 0f, Mathf.Sign(w) * w * w * size * 0.5f);
            }
            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int i = z * (n + 1) + x;
                tris.AddRange(new[] { i, i + n + 1, i + 1, i + 1, i + n + 1, i + n + 2 });
            }
            oceanVerts = (Vector3[])oceanBase.Clone();
            oceanMesh.vertices = oceanVerts;
            oceanMesh.SetTriangles(tris, 0);
            oceanMesh.RecalculateNormals();
            ocean.gameObject.AddComponent<MeshFilter>().sharedMesh = oceanMesh;
            ocean.gameObject.AddComponent<MeshRenderer>().sharedMaterial = oceanMaterial;
        }

        private static float WaveHeight(float x, float z, float t) =>
            Mathf.Sin(x * 0.045f + t * 0.9f) * 0.55f + Mathf.Sin(z * 0.06f - t * 1.1f + x * 0.02f) * 0.4f
            + Mathf.Sin((x + z) * 0.17f + t * 1.9f) * 0.12f;

        private void AnimateOcean(float t)
        {
            for (int i = 0; i < oceanBase.Length; i++)
            {
                var b = oceanBase[i];
                oceanVerts[i] = new Vector3(b.x, WaveHeight(b.x, b.z, t), b.z);
            }
            oceanMesh.vertices = oceanVerts;
            oceanMesh.RecalculateNormals();
        }

        private void BuildShip()
        {
            // Buque anfibio de recuperación (clase San Antonio, ~208 m) a ~600 m, como el USS John P. Murtha en Artemis II.
            ship = new GameObject("RecoveryShip").transform;
            ship.SetParent(root, false);
            ship.localPosition = new Vector3(260f, 0f, 560f);
            ship.localRotation = Quaternion.Euler(0f, -70f, 0f);
            void Box(Vector3 pos, Vector3 size)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(b.GetComponent<Collider>());
                b.transform.SetParent(ship, false);
                b.transform.localPosition = pos;
                b.transform.localScale = size;
                b.GetComponent<Renderer>().sharedMaterial = shipMaterial;
            }
            Box(new Vector3(0f, 3f, 0f), new Vector3(208f, 10f, 32f));          // casco
            Box(new Vector3(96f, 4.5f, 0f), new Vector3(22f, 7f, 22f));          // proa
            Box(new Vector3(-64f, 9f, 0f), new Vector3(80f, 2f, 30f));           // cubierta de vuelo (popa)
            Box(new Vector3(18f, 14f, 0f), new Vector3(82f, 12f, 28f));          // superestructura
            Box(new Vector3(24f, 24f, 0f), new Vector3(34f, 8f, 20f));           // puente
            Box(new Vector3(6f, 33f, 0f), new Vector3(9f, 14f, 9f));             // mástil integrado
            Box(new Vector3(44f, 32f, 0f), new Vector3(8f, 12f, 8f));
            ship.gameObject.SetActive(false);
        }

        private void BuildParticles()
        {
            plasma = MakeParticles("Plasma", plasmaMaterial, capsule, new Vector3(0f, -0.4f, 0f));
            var main = plasma.main;
            main.startLifetime = 0.45f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.15f), new Color(1f, 0.85f, 0.5f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = plasma.emission;
            em.rateOverTime = 260f;
            var shape = plasma.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 2.4f;
            var col = plasma.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Fade(new Color(1f, 0.9f, 0.6f), new Color(1f, 0.25f, 0.05f)));
            var sol = plasma.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));

            spray = MakeParticles("Spray", sprayMaterial, root, Vector3.zero);
            main = spray.main;
            main.loop = false;
            main.duration = 0.6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 2.8f);
            main.gravityModifier = 1f;
            main.startColor = new Color(0.95f, 0.98f, 1f, 0.9f);
            em = spray.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 520) });
            shape = spray.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 52f;
            shape.radius = 2.4f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            col = spray.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Fade(Color.white, Color.white));

            foam = MakeParticles("Foam", sprayMaterial, root, new Vector3(0f, 0.3f, 0f));
            main = foam.main;
            main.loop = false;
            main.duration = 0.4f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            main.startColor = new Color(1f, 1f, 1f, 0.7f);
            em = foam.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 160) });
            shape = foam.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 3f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var vel = foam.velocityOverLifetime;
            vel.enabled = false;
            col = foam.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Fade(Color.white, Color.white));

            // Nubes que suben junto a la cámara mientras la cápsula baja: dan sensación de caída.
            clouds = MakeParticles("Clouds", cloudMaterial, capsule, new Vector3(0f, -40f, 0f));
            main = clouds.main;
            main.startLifetime = 4f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(30f, 70f);
            main.startColor = new Color(1f, 1f, 1f, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -2.2f;
            em = clouds.emission;
            em.rateOverTime = 14f;
            shape = clouds.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(160f, 10f, 160f);
            col = clouds.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Fade(Color.white, Color.white));
        }

        private static Gradient Fade(Color a, Color b) => new Gradient
        {
            colorKeys = new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0f, 1f) }
        };

        private static ParticleSystem MakeParticles(string name, Material mat, Transform parent, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            var main = ps.main;
            main.playOnAwake = false;
            main.maxParticles = 2000;
            return ps;
        }

        private void BuildOverlay()
        {
            var go = new GameObject("CinematicOverlay");
            go.transform.SetParent(root, false);
            overlay = go.AddComponent<Canvas>();
            // Rótulos dibujados por la propia cámara de la cinemática (aparecen también en capturas y grabaciones).
            overlay.renderMode = RenderMode.ScreenSpaceCamera;
            overlay.worldCamera = cam;
            overlay.planeDistance = 0.5f;
            overlay.sortingOrder = 900;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Image Bar(string n, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, float height)
            {
                var img = new GameObject(n).AddComponent<Image>();
                img.transform.SetParent(go.transform, false);
                img.color = Color.black;
                img.raycastTarget = false;
                var rt = img.rectTransform;
                rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
                rt.sizeDelta = new Vector2(0f, height);
                rt.anchoredPosition = Vector2.zero;
                return img;
            }
            Bar("LetterboxTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), 110f);
            Bar("LetterboxBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), 150f);

            TMP_Text Label(string n, float size, Vector2 pos, Vector2 sizeDelta, TextAlignmentOptions align, Color color)
            {
                var t = new GameObject(n).AddComponent<TextMeshProUGUI>();
                t.transform.SetParent(go.transform, false);
                t.fontSize = size;
                t.alignment = align;
                t.color = color;
                t.raycastTarget = false;
                t.textWrappingMode = TextWrappingModes.Normal;
                var rt = t.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                rt.anchoredPosition = pos;
                rt.sizeDelta = sizeDelta;
                return t;
            }
            title = Label("Title", 44f, new Vector2(80f, 72f), new Vector2(1700f, 60f), TextAlignmentOptions.BottomLeft, new Color(1f, 0.78f, 0.35f));
            title.fontStyle = FontStyles.Bold;
            subtitle = Label("Subtitle", 24f, new Vector2(80f, 22f), new Vector2(1760f, 48f), TextAlignmentOptions.TopLeft, new Color(0.92f, 0.95f, 1f));
            hint = Label("SkipHint", 18f, new Vector2(1500f, 1000f), new Vector2(380f, 40f), TextAlignmentOptions.TopRight, new Color(0.7f, 0.76f, 0.85f));
            hint.text = "Espacio / clic: saltar";
            var hrt = hint.rectTransform;
            hrt.anchorMin = hrt.anchorMax = new Vector2(1f, 1f);
            hrt.pivot = new Vector2(1f, 1f);
            hrt.anchoredPosition = new Vector2(-40f, -38f);

            fade = new GameObject("Fade").AddComponent<Image>();
            fade.transform.SetParent(go.transform, false);
            fade.color = new Color(0f, 0f, 0f, 0f);
            fade.raycastTarget = false;
            var frt = fade.rectTransform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;
        }

        private void Caption(string head, string line)
        {
            if (title != null) title.text = head;
            if (subtitle != null) subtitle.text = line;
        }

        private void SetFade(float a)
        {
            if (fade != null) fade.color = new Color(0f, 0f, 0f, Mathf.Clamp01(a));
        }

        /// <summary>0 = cielo alto oscuro (reentrada), 1 = cielo diurno junto al mar.</summary>
        private void SetSky(float day)
        {
            // A 100 km el cielo es casi negro; abajo, azul claro con bruma sobre el mar.
            if (skyMaterial != null) skyMaterial.SetColor("_BaseColor", Color.Lerp(new Color(0.06f, 0.08f, 0.18f), Color.white, day));
            cam.backgroundColor = Color.Lerp(new Color(0.01f, 0.02f, 0.06f), new Color(0.6f, 0.73f, 0.86f), day);
            RenderSettings.fogDensity = Mathf.Lerp(0.00005f, 0.0011f, day);
            RenderSettings.fogColor = Color.Lerp(new Color(0.03f, 0.05f, 0.14f), new Color(0.66f, 0.77f, 0.88f), day);
        }

        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip, 0.9f * GameSettings.SfxVolume);
        }

        private void HideOtherCanvases(bool hide)
        {
            if (hide)
            {
                hiddenCanvases.Clear();
                foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    if (c != overlay && c.isRootCanvas && c.enabled && c.renderMode != RenderMode.WorldSpace)
                    {
                        c.enabled = false;
                        hiddenCanvases.Add(c);
                    }
            }
            else
            {
                foreach (var c in hiddenCanvases) if (c != null) c.enabled = true;
                hiddenCanvases.Clear();
            }
        }

        /// <summary>Apaga las demás cámaras mientras dura la cinemática para no renderizar la escena dos veces.</summary>
        private void PauseOtherCameras(bool pause)
        {
            if (pause)
            {
                pausedCameras.Clear();
                foreach (var c in Camera.allCameras)
                    if (c != cam && c.enabled) { c.enabled = false; pausedCameras.Add(c); }
            }
            else
            {
                foreach (var c in pausedCameras) if (c != null) c.enabled = true;
                pausedCameras.Clear();
            }
        }

        // ───────────── Mallas ─────────────

        private static Transform AddMesh(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        /// <summary>Tronco de cono cerrado arriba (radio inferior, superior y altura).</summary>
        public static Mesh Frustum(float r0, float r1, float height, int segments)
        {
            var v = new List<Vector3>();
            var tri = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v.Add(d * r0);
                v.Add(d * r1 + Vector3.up * height);
            }
            for (int i = 0; i < segments; i++)
            {
                int k = i * 2;
                tri.AddRange(new[] { k, k + 1, k + 2, k + 2, k + 1, k + 3 });
            }
            int top = v.Count;
            v.Add(Vector3.up * height);
            for (int i = 0; i < segments; i++) tri.AddRange(new[] { top, i * 2 + 3, i * 2 + 1 });
            var mesh = new Mesh { name = "Frustum" };
            mesh.SetVertices(v);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        /// <summary>Casquete esférico poco profundo (escudo térmico) que mira hacia abajo.</summary>
        public static Mesh Cap(float radius, float depth, int segments, int rings)
        {
            var v = new List<Vector3>();
            var tri = new List<int>();
            v.Add(new Vector3(0f, -depth, 0f));
            for (int r = 1; r <= rings; r++)
            {
                float f = r / (float)rings;
                float y = -depth * (1f - f * f);
                for (int i = 0; i < segments; i++)
                {
                    float a = i / (float)segments * Mathf.PI * 2f;
                    v.Add(new Vector3(Mathf.Cos(a) * radius * f, y, Mathf.Sin(a) * radius * f));
                }
            }
            for (int i = 0; i < segments; i++) tri.AddRange(new[] { 0, 1 + (i + 1) % segments, 1 + i });
            for (int r = 1; r < rings; r++)
            {
                int a0 = 1 + (r - 1) * segments, b0 = 1 + r * segments;
                for (int i = 0; i < segments; i++)
                {
                    int i1 = (i + 1) % segments;
                    tri.AddRange(new[] { a0 + i, a0 + i1, b0 + i, a0 + i1, b0 + i1, b0 + i });
                }
            }
            var mesh = new Mesh { name = "HeatShield" };
            mesh.SetVertices(v);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh InvertedSphere(Mesh source)
        {
            var mesh = Instantiate(source);
            var tris = mesh.triangles;
            for (int i = 0; i < tris.Length; i += 3) (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
