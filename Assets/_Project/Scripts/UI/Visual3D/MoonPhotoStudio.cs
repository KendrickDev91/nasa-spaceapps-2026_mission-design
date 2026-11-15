using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MissionDesign.Core;
using MissionDesign.Simulation;
using UnityEngine;

namespace MissionDesign.UI.Visual3D
{
    /// <summary>
    /// "Estudio fotográfico" lunar: si la nave lleva cámaras y llega a la Luna, durante el sobrevuelo (u órbita lunar)
    /// renderiza fotografías reales de la escena (Luna detallada, Tierra tras el limbo, ala solar de Orion, superficie)
    /// y al terminar las guarda junto al reporte. El informe de exploración las muestra con su pie de foto.
    /// </summary>
    public sealed class MoonPhotoStudio : MonoBehaviour
    {
        public sealed class Photo
        {
            public Texture2D texture;
            public string title;
            public string caption;
        }

        private struct Shot
        {
            public string title, lens;
            public Vector3 eye, aim, sunFrom, earthAt;
            public float fov, earthRadius;
            /// <summary>0 = Luna en su sitio · -1 = oculta · &gt;0 = Luna movida a moonAt con ese radio.</summary>
            public float moonRadius;
            public Vector3 moonAt;
            public bool orion, surface;
        }

        [SerializeField] private Camera photoCamera;
        [SerializeField] private Transform moon;
        [SerializeField] private Transform earth;
        [SerializeField] private Light studioSun;
        [SerializeField] private Transform orion;
        [Tooltip("Pose de cámara sobre la superficie lunar (escena del sobrevuelo).")]
        [SerializeField] private Transform surfacePose;
        [SerializeField] private MissionSimulationRunner runner;
        [SerializeField] private int width = 1280;
        [SerializeField] private int height = 720;

        private readonly List<Photo> photos = new List<Photo>();
        private MissionStateManager manager;
        private Coroutine session;
        private int planned;

        public IReadOnlyList<Photo> Photos => photos;
        /// <summary>Guardando PNG en segundo plano (no traba el juego).</summary>
        public bool Saving => pendingSaves > 0;
        private int pendingSaves;
        private int pendingReadbacks;
        /// <summary>Fotos cuya imagen aún se está copiando desde la GPU.</summary>
        public bool Developing => pendingReadbacks > 0;
        public string SavedFolder { get; private set; }
        public event Action<Photo, int, int> PhotoTaken;

        private void Start()
        {
            manager = MissionStateManager.Instance;
            if (manager == null) return;
            manager.PhaseChanged += OnPhaseChanged;
            manager.MissionCompleted += OnMissionCompleted;
            if (photoCamera != null) photoCamera.enabled = false;
            if (studioSun != null) studioSun.enabled = false;
            if (orion != null) orion.gameObject.SetActive(false);
            var sphere = HiResSphere(160, 80);
            if (moon != null && moon.TryGetComponent<MeshFilter>(out var mf)) mf.sharedMesh = sphere;
            if (earth != null && earth.TryGetComponent<MeshFilter>(out var ef)) ef.sharedMesh = sphere;
        }

        /// <summary>Esfera UV de alta resolución con la misma parametrización que la textura lunar (sin facetas en el limbo).</summary>
        public static Mesh HiResSphere(int lonSegments, int latSegments)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int y = 0; y <= latSegments; y++)
            for (int x = 0; x <= lonSegments; x++)
            {
                float u = (float)x / lonSegments, v = (float)y / latSegments;
                float lon = (u - 0.5f) * 2f * Mathf.PI, lat = (v - 0.5f) * Mathf.PI;
                verts.Add(0.5f * new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon)));
                uvs.Add(new Vector2(u, v));
            }
            int row = lonSegments + 1;
            for (int y = 0; y < latSegments; y++)
            for (int x = 0; x < lonSegments; x++)
            {
                int i = y * row + x;
                tris.AddRange(new[] { i, i + row, i + 1, i + 1, i + row, i + row + 1 });
            }
            var mesh = new Mesh { name = "HiResSphere", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDestroy()
        {
            if (manager == null) return;
            manager.PhaseChanged -= OnPhaseChanged;
            manager.MissionCompleted -= OnMissionCompleted;
        }

        private void OnPhaseChanged(MissionPhase phase)
        {
            if (phase == MissionPhase.Launch) { Clear(); planned = 0; }
            if (phase != MissionPhase.ScienceOperations) return;
            // El resultado se calcula justo después de pasar a Launch: se lee al empezar las operaciones.
            var o = runner != null ? runner.Current : null;
            planned = o != null ? o.photoShots : 0;
            if (planned > 0 && session == null) session = StartCoroutine(Session());
        }

        private IEnumerator Session()
        {
            float spacing = (runner != null ? runner.SecondsPerPhase : 3f) * 0.85f / Mathf.Max(1, planned);
            yield return new WaitForSeconds(spacing * 0.5f);
            while (photos.Count < planned && manager.Phase == MissionPhase.ScienceOperations)
            {
                Take(photos.Count);
                yield return new WaitForSeconds(spacing);
            }
            session = null;
        }

        private void OnMissionCompleted(MissionOutcomeReport o)
        {
            if (session != null) { StopCoroutine(session); session = null; }
            if (o == null) return;
            StartCoroutine(Finish(o));
        }

        /// <summary>Completa las fotos que falten (una por fotograma, sin trabar) y las guarda en segundo plano.</summary>
        private IEnumerator Finish(MissionOutcomeReport o)
        {
            planned = o.photoShots;
            while (photos.Count < o.photoShots)
            {
                Take(photos.Count);
                yield return null;
            }
            while (pendingReadbacks > 0) yield return null;     // esperar a que la GPU entregue las imágenes
            // Si la cápsula no volvió, solo quedan las transmitidas.
            while (photos.Count > o.photoShotsAvailable) { Destroy(photos[photos.Count - 1].texture); photos.RemoveAt(photos.Count - 1); }
            Save(o);
        }

        public void Clear()
        {
            foreach (var p in photos) if (p.texture != null) Destroy(p.texture);
            photos.Clear();
            SavedFolder = null;
        }

        /// <summary>Renderiza la foto número <paramref name="index"/> (también usable desde pruebas).</summary>
        public Photo Take(int index)
        {
            var shots = ShotsFor(runner != null ? runner.Current : null);
            if (shots.Count == 0 || photoCamera == null) return null;
            var shot = shots[index % shots.Count];
            var o = runner != null ? runner.Current : null;
            string cameraName = o != null && o.cameras != null && o.cameras.Length > 0 ? o.cameras[index % o.cameras.Length] : "Cámara de a bordo";

            var tex = Render(shot);
            var photo = new Photo
            {
                texture = tex,
                title = shot.title,
                caption = $"{shot.title} · {cameraName} · {shot.lens}"
            };
            photos.Add(photo);
            PhotoTaken?.Invoke(photo, photos.Count, Mathf.Max(planned, photos.Count));
            return photo;
        }

        private List<Shot> ShotsFor(MissionOutcomeReport o)
        {
            float R = moon != null ? moon.localScale.x * 0.5f : 40f;
            Vector3 C = moon != null ? moon.position : transform.position;
            bool crewed = o != null && o.crewed;
            bool earthMission = o != null && !o.reachedMoon && o.photosTaken > 0;
            var list = earthMission ? EarthShots(C, R) : MoonShots(C, R);
            if (!crewed || orion == null) list.RemoveAll(sh => sh.orion);
            if (surfacePose == null) list.RemoveAll(sh => sh.surface);
            return list;
        }

        private static List<Shot> MoonShots(Vector3 C, float R) => new List<Shot>
        {
            new Shot { title = "Luna creciente sobre la cara oculta", lens = "35 mm", eye = C + new Vector3(0f, 0f, -3.4f * R), aim = C,
                       fov = 42f, sunFrom = new Vector3(1f, 0.1f, 0.35f) },
            new Shot { title = "Terminador: cráteres en relieve", lens = "400 mm", eye = C + new Vector3(0.2f * R, 0.4f * R, -2.2f * R),
                       aim = C + R * new Vector3(0.29f, 0.3f, -0.91f).normalized, fov = 9f, sunFrom = new Vector3(1f, 0f, 0.3f) },
            new Shot { title = "Puesta de la Tierra tras el limbo lunar", lens = "80–400 mm", eye = C + new Vector3(0f, 1.0f * R, -3f * R),
                       aim = C + new Vector3(0f, 1.25f * R, 8f * R), fov = 16f, sunFrom = new Vector3(1f, 0.25f, 0.5f),
                       earthAt = C + new Vector3(0f, 1.25f * R, 8f * R), earthRadius = 0.85f * R },
            new Shot { title = "Cuenca de impacto en la cara oculta", lens = "Cámara externa", eye = C + new Vector3(-1.1f * R, 0.3f * R, -1.7f * R),
                       aim = C + R * new Vector3(-0.45f, 0.1f, -0.88f).normalized, fov = 24f, sunFrom = new Vector3(1f, 0.3f, -0.2f) },
            new Shot { title = "Ala solar de Orion con la Luna", lens = "Cámara en el ala solar", orion = true, eye = C + new Vector3(-6f, 3f, -4.6f * R),
                       aim = C, fov = 55f, sunFrom = new Vector3(0.8f, 0.4f, -0.5f) },
            new Shot { title = "Superficie a baja altura", lens = "Teleobjetivo", surface = true, fov = 50f },
            new Shot { title = "La Luna casi llena durante la ida", lens = "80 mm", eye = C + new Vector3(0f, 0.2f * R, -3.8f * R), aim = C,
                       fov = 36f, sunFrom = new Vector3(0.25f, 0.15f, -1f) },
            new Shot { title = "La Tierra y la Luna juntas", lens = "35 mm", eye = C + new Vector3(-2.2f * R, 0.6f * R, -7f * R),
                       aim = C + new Vector3(-0.9f * R, 0.4f * R, 6f * R), fov = 40f, sunFrom = new Vector3(1f, 0.2f, -0.3f),
                       earthAt = C + new Vector3(-1.5f * R, 1.1f * R, 16f * R), earthRadius = 1.1f * R },
        };

        /// <summary>Misiones en órbita terrestre: la Tierra ocupa el lugar de la Luna en el estudio.</summary>
        private static List<Shot> EarthShots(Vector3 C, float R) => new List<Shot>
        {
            new Shot { title = "La Tierra completa desde órbita", lens = "35 mm", eye = C + new Vector3(0f, 0.2f * R, -4.2f * R), aim = C, fov = 34f,
                       sunFrom = new Vector3(0.5f, 0.3f, -1f), earthAt = C, earthRadius = R, moonRadius = -1f },
            new Shot { title = "Nubes y océanos", lens = "400 mm", eye = C + new Vector3(0.2f * R, 0.3f * R, -2f * R),
                       aim = C + R * new Vector3(0.15f, 0.25f, -0.96f).normalized, fov = 10f, sunFrom = new Vector3(0.4f, 0.6f, -1f),
                       earthAt = C, earthRadius = R, moonRadius = -1f },
            new Shot { title = "El día y la noche (terminador)", lens = "80 mm", eye = C + new Vector3(0f, 0f, -3.2f * R), aim = C, fov = 40f,
                       sunFrom = new Vector3(1f, 0.15f, 0.1f), earthAt = C, earthRadius = R, moonRadius = -1f },
            new Shot { title = "La Luna sobre el horizonte terrestre", lens = "80–400 mm", eye = C + new Vector3(0f, 1.0f * R, -2.6f * R),
                       aim = C + new Vector3(0.2f * R, 1.2f * R, 6f * R), fov = 22f, sunFrom = new Vector3(0.6f, 0.4f, -0.6f),
                       earthAt = C, earthRadius = R, moonAt = C + new Vector3(0.2f * R, 1.28f * R, 7f * R), moonRadius = 0.3f * R },
            new Shot { title = "Ala solar con la Tierra", lens = "Cámara en el ala solar", orion = true, eye = C + new Vector3(-6f, 3f, -4.6f * R),
                       aim = C, fov = 55f, sunFrom = new Vector3(0.8f, 0.4f, -0.5f), earthAt = C, earthRadius = R, moonRadius = -1f },
            new Shot { title = "Casquete polar y nubes", lens = "35 mm", eye = C + new Vector3(0f, 2.6f * R, -1.6f * R), aim = C + new Vector3(0f, 0.6f * R, 0f),
                       fov = 30f, sunFrom = new Vector3(1f, 0.6f, -0.5f), earthAt = C, earthRadius = R, moonRadius = -1f },
            new Shot { title = "Limbo terrestre al amanecer", lens = "35 mm", eye = C + new Vector3(0f, 1.03f * R, -2.4f * R),
                       aim = C + new Vector3(0f, 0.9f * R, 3f * R), fov = 34f, sunFrom = new Vector3(0.3f, 0.25f, 1f),
                       earthAt = C, earthRadius = R, moonRadius = -1f },
            new Shot { title = "La Tierra que se aleja", lens = "80 mm", eye = C + new Vector3(-1.8f * R, 0.4f * R, -8f * R), aim = C, fov = 22f,
                       sunFrom = new Vector3(0.7f, 0.3f, -0.6f), earthAt = C, earthRadius = R, moonRadius = -1f },
        };

        private Texture2D Render(Shot shot)
        {
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude).Where(l => l.type == LightType.Directional && l != studioSun && l.enabled).ToList();
            var ambient = RenderSettings.ambientLight;
            var ambientMode = RenderSettings.ambientMode;
            bool earthWasActive = earth != null && earth.gameObject.activeSelf;
            Vector3 moonPos = moon != null ? moon.position : Vector3.zero, moonScale = moon != null ? moon.localScale : Vector3.one;
            bool moonWasActive = moon != null && moon.gameObject.activeSelf;
            try
            {
                if (shot.surface)
                {
                    photoCamera.transform.SetPositionAndRotation(surfacePose.position, surfacePose.rotation);
                    photoCamera.farClipPlane = 3000f;
                }
                else
                {
                    foreach (var l in lights) l.enabled = false;
                    RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                    RenderSettings.ambientLight = new Color(0.025f, 0.025f, 0.03f);
                    if (studioSun != null)
                    {
                        studioSun.enabled = true;
                        studioSun.transform.rotation = Quaternion.LookRotation(-shot.sunFrom.normalized);
                    }
                    if (earth != null)
                    {
                        earth.gameObject.SetActive(shot.earthRadius > 0f);
                        if (shot.earthRadius > 0f) { earth.position = shot.earthAt; earth.localScale = Vector3.one * shot.earthRadius * 2f; }
                    }
                    if (orion != null) orion.gameObject.SetActive(shot.orion);
                    if (moon != null && shot.moonRadius != 0f)
                    {
                        moon.gameObject.SetActive(shot.moonRadius > 0f);
                        if (shot.moonRadius > 0f) { moon.position = shot.moonAt; moon.localScale = Vector3.one * shot.moonRadius * 2f; }
                    }
                    photoCamera.transform.SetPositionAndRotation(shot.eye, Quaternion.LookRotation(shot.aim - shot.eye, Vector3.up));
                    photoCamera.farClipPlane = 2500f;
                }
                photoCamera.fieldOfView = shot.fov;
                photoCamera.targetTexture = rt;
                photoCamera.Render();
                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                if (SystemInfo.supportsAsyncGPUReadback)
                {
                    // Copia asíncrona: la imagen llega uno o dos fotogramas después, sin congelar la animación.
                    var target = rt;
                    rt = null;                                   // se libera en el callback
                    System.Threading.Interlocked.Increment(ref pendingReadbacks);
                    UnityEngine.Rendering.AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32, req =>
                    {
                        if (!req.hasError && tex != null) { tex.LoadRawTextureData(req.GetData<byte>()); tex.Apply(); }
                        RenderTexture.ReleaseTemporary(target);
                        System.Threading.Interlocked.Decrement(ref pendingReadbacks);
                    });
                }
                else
                {
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    tex.Apply();
                }
                return tex;
            }
            finally
            {
                RenderTexture.active = null;
                photoCamera.targetTexture = null;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                foreach (var l in lights) l.enabled = true;
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientLight = ambient;
                if (studioSun != null) studioSun.enabled = false;
                if (orion != null) orion.gameObject.SetActive(false);
                if (earth != null) earth.gameObject.SetActive(earthWasActive);
                if (moon != null) { moon.position = moonPos; moon.localScale = moonScale; moon.gameObject.SetActive(moonWasActive); }
            }
        }

        private void Save(MissionOutcomeReport o)
        {
            if (photos.Count == 0 && (o.cameras == null || o.cameras.Length == 0)) return;
            try
            {
                string basePath = !string.IsNullOrEmpty(manager.LastReportPath)
                    ? Path.Combine(Path.GetDirectoryName(manager.LastReportPath) ?? MissionReportExporter.ReportsFolder,
                                   Path.GetFileNameWithoutExtension(manager.LastReportPath) + "_exploracion")
                    : Path.Combine(MissionReportExporter.ReportsFolder, $"exploracion_{DateTime.Now:yyyyMMdd_HHmmss}");
                Directory.CreateDirectory(basePath);
                // Codificación PNG en segundo plano: con 8 fotos 1280×720 evita el tirón al terminar la misión.
                for (int i = 0; i < photos.Count; i++)
                {
                    var tex = photos[i].texture;
                    byte[] raw = tex.GetRawTextureData<byte>().ToArray();
                    var format = tex.graphicsFormat;
                    uint w = (uint)tex.width, h = (uint)tex.height;
                    string file = Path.Combine(basePath, $"foto_{i + 1:00}.png");
                    System.Threading.Interlocked.Increment(ref pendingSaves);
                    Task.Run(() =>
                    {
                        try { File.WriteAllBytes(file, ImageConversion.EncodeArrayToPNG(raw, format, w, h)); }
                        catch (Exception ex) { Debug.LogWarning($"[Fotos] {file}: {ex.Message}"); }
                        finally { System.Threading.Interlocked.Decrement(ref pendingSaves); }
                    });
                }
                var roles = FindAnyObjectByType<Multiplayer.RoleTurnManager>();
                var report = ExplorationReport.Build(o, roles != null ? roles.TeamName : null);
                File.WriteAllText(Path.Combine(basePath, "informe_exploracion.txt"),
                    ExplorationReport.ToPlainText(report, photos.Select(p => p.caption).ToArray()));
                SavedFolder = basePath;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Fotos] No se pudieron guardar las fotografías: {ex.Message}");
            }
        }
    }
}
