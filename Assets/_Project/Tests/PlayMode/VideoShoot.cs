using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MissionDesign.Core;
using MissionDesign.Multiplayer;
using MissionDesign.Simulation;
using MissionDesign.UI;
using MissionDesign.UI.Visual3D;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MissionDesign.Tests
{
    /// <summary>
    /// Rodaje del video promocional: juega una misión real (demo Artemis II) a 30 fps fijos y guarda "planos" en JPG
    /// (Recordings/Plates/&lt;plano&gt;_H a 1920×1080 y _V a 1080×1920). El montaje, la voz y la música los hace
    /// MissionDesign.Editor.Video.VideoMaker. Es [Explicit]: no corre con el resto de pruebas.
    /// </summary>
    [Explicit("Rodaje del video: se ejecuta a mano con -testFilter VideoShoot")]
    [Category("Video")]
    public class VideoShoot
    {
        private const int Fps = 30;
        private const string Root = "Recordings/Plates";

        private Camera camH, camV;
        private RenderTexture rtH, rtV;
        private Texture2D texH, texV;

        private sealed class Plate
        {
            public string name; public bool vertical; public int frames;
        }

        private void Setup()
        {
            Time.captureFramerate = Fps;
            rtH = new RenderTexture(1920, 1080, 24) { antiAliasing = 4 };
            rtV = new RenderTexture(1080, 1920, 24) { antiAliasing = 4 };
            texH = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            texV = new Texture2D(1080, 1920, TextureFormat.RGB24, false);
            camH = new GameObject("VideoCamH").AddComponent<Camera>();
            camV = new GameObject("VideoCamV").AddComponent<Camera>();
            Object.DontDestroyOnLoad(camH.gameObject);
            Object.DontDestroyOnLoad(camV.gameObject);
            camH.enabled = camV.enabled = false;
        }

        private void Teardown()
        {
            Time.captureFramerate = 0;
            Object.Destroy(camH.gameObject); Object.Destroy(camV.gameObject);
            rtH.Release(); rtV.Release();
        }

        private static Plate Begin(string name, bool vertical)
        {
            foreach (var suffix in vertical ? new[] { "_H", "_V" } : new[] { "_H" })
            {
                string dir = Path.Combine(Root, name + suffix);
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
                Directory.CreateDirectory(dir);
            }
            return new Plate { name = name, vertical = vertical };
        }

        private static void Save(RenderTexture rt, Texture2D tex, string path)
        {
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply(false);
            RenderTexture.active = null;
            File.WriteAllBytes(path, tex.EncodeToJPG(90));
        }

        /// <summary>Renderiza una cámara a un RT sin alterar su destino habitual.</summary>
        private static void RenderTo(Camera cam, RenderTexture rt, float fovMultiplier = 1f)
        {
            var prev = cam.targetTexture;
            float fov = cam.fieldOfView;
            cam.targetTexture = rt;
            cam.fieldOfView = Mathf.Min(100f, fov * fovMultiplier);
            cam.Render();
            cam.fieldOfView = fov;
            cam.targetTexture = prev;
        }

        private static void CopyPose(Camera from, Camera to, float fovMultiplier)
        {
            to.CopyFrom(from);
            to.targetTexture = null;
            to.enabled = false;
            to.fieldOfView = Mathf.Min(100f, from.fieldOfView * fovMultiplier);
        }

        private static void GameUi(bool on)
        {
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceCamera && c.name != "CinematicOverlay") c.enabled = on;
        }

        /// <summary>Graba un fotograma del plano. H: cámara principal con interfaz (ui) o cámara propia sin ella.</summary>
        private void Frame(Plate p, Camera main, bool ui, System.Action<Camera> poseV = null, System.Action<Camera> poseH = null, float fovV = 1.55f)
        {
            string n = $"frame_{p.frames:D5}.jpg";
            if (ui && poseH == null) { GameUi(true); RenderTo(main, rtH); }
            else
            {
                GameUi(false);
                CopyPose(main, camH, 1f);
                poseH?.Invoke(camH);
                RenderTo(camH, rtH);
            }
            Save(rtH, texH, Path.Combine(Root, p.name + "_H", n));
            if (p.vertical)
            {
                GameUi(false);
                CopyPose(main, camV, fovV);
                poseV?.Invoke(camV);
                RenderTo(camV, rtV);
                Save(rtV, texV, Path.Combine(Root, p.name + "_V", n));
            }
            GameUi(true);
            p.frames++;
        }

        private static void Pose(Camera cam, Transform t, float fov)
        {
            cam.transform.SetPositionAndRotation(t.position, t.rotation);
            cam.fieldOfView = fov;
        }

        // ───────────── Rodaje de la misión ─────────────

        [UnityTest]
        public IEnumerator Shoot_Mission()
        {
            Setup();
            SceneManager.LoadScene(SceneFlow.MissionDesignScene);
            yield return null;
            yield return null;
            Object.FindAnyObjectByType<GameMenus>().CloseAll();
            var main = Camera.main;
            var mgr = MissionStateManager.Instance;
            var roles = Object.FindAnyObjectByType<RoleTurnManager>();
            var runner = Object.FindAnyObjectByType<MissionSimulationRunner>();
            var orbit = Object.FindAnyObjectByType<OrbitViewController>();
            var director = Object.FindAnyObjectByType<MissionCameraDirector>();
            var cine = Object.FindAnyObjectByType<SplashdownCinematic>();
            var bf = BindingFlags.NonPublic | BindingFlags.Instance;
            var reactionPose = (Transform)typeof(MissionCameraDirector).GetField("reactionPose", bf).GetValue(director);
            var widePose = (Transform)typeof(MissionCameraDirector).GetField("wideRoomPose", bf).GetValue(director);

            var demo = DesignLibrary.Demos()[0].Item2;   // 1 · Artemis II con fotos (éxito)
            Object.FindAnyObjectByType<DesignLibraryPanel>().Load(demo);
            roles.SetPlayerCount(4);
            yield return new WaitForSeconds(0.5f);

            // A) Diseño: los 4 roles con su catálogo.
            var design = Begin("design", false);
            for (int i = 0; i < 14 * Fps; i++)
            {
                if (i % (int)(2.2f * Fps) == 0) roles.SetActiveRole(RoleTurnManager.AllRoles[(i / (int)(2.2f * Fps)) % 4]);
                Frame(design, main, ui: true);
                yield return null;
            }

            // B) Consenso: cada rol aprueba y las píldoras se encienden.
            var consensus = Begin("consensus", false);
            for (int i = 0; i < 4 * Fps; i++)
            {
                if (i % 20 == 5 && i / 20 < 4)
                {
                    roles.SetActiveRole(RoleTurnManager.AllRoles[i / 20]);
                    roles.ApproveAsActivePlayer();
                }
                Frame(consensus, main, ui: true);
                yield return null;
            }

            // C) Lanzamiento.
            runner.SecondsPerPhase = 8f;
            Assert.IsTrue(roles.TryRequestLaunch(out string msg), msg);
            var launch = Begin("launch", true);
            for (int i = 0; i < 10 * Fps && mgr.Phase == MissionPhase.Launch; i++)
            {
                Frame(launch, main, ui: false, fovV: 1.45f);
                yield return null;
            }
            while (mgr.Phase == MissionPhase.Launch) yield return null;

            // D) Sala de control celebrando: de frente a un grupo de operadores (se ven caras y aplausos) y luego el general.
            var posers = Object.FindObjectsByType<MissionDesign.Characters.CrewPoser>(FindObjectsSortMode.None)
                .Where(c => c.GetComponentInChildren<Animator>() != null && c.transform.position.y < 50f).ToList();
            var lead = posers.OrderBy(c => Vector3.Distance(c.transform.position, widePose.position)).Skip(2).First();
            var group = posers.Where(c => Vector3.Distance(c.transform.position, lead.transform.position) < 4.5f).ToList();
            Vector3 crew = group.Aggregate(Vector3.zero, (acc, c) => acc + c.transform.position) / group.Count;
            Vector3 face = lead.transform.forward;
            var room = Begin("room", true);
            for (int i = 0; i < 6 * Fps; i++)
            {
                float k = i / (6f * Fps);
                Vector3 eye = crew + face * Mathf.Lerp(3.6f, 3.0f, k) + Vector3.up * 1.55f + lead.transform.right * Mathf.Lerp(-0.6f, 0.6f, k);
                var look = Quaternion.LookRotation(crew + Vector3.up * 1.25f - eye);
                bool front = i < 3.5f * Fps;
                Frame(room, main, ui: false,
                    poseV: c => { c.transform.SetPositionAndRotation(eye, look); c.fieldOfView = 68f; },
                    poseH: c => { if (front) { c.transform.SetPositionAndRotation(eye, look); c.fieldOfView = 46f; } else Pose(c, widePose, 52f); });
                yield return null;
            }

            // E) Vuelo: el "8" (vertical: Tierra arriba, Luna abajo, centrado) y el acercamiento a la Luna.
            var flight = Begin("flight", true);
            Vector3 moonWorld = orbit.transform.TransformPoint(new Vector3(-OrbitViewController.MoonDisplayDistance, 0f, 0f));
            Vector3 topEye = orbit.transform.TransformPoint(new Vector3(-36f, 92f, 0f));
            var topRot = Quaternion.LookRotation(Vector3.down, orbit.transform.right);
            // En vertical la leyenda queda fuera del encuadre (se cortaría): se oculta solo en la toma vertical.
            var legend = orbit.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Legend")).Select(t => t.gameObject).ToList();
            for (int i = 0; i < 70 * Fps && mgr.Phase < MissionPhase.DataTransmission; i++)
            {
                bool roomShot = director.ShowingReaction || director.Current == MissionCameraDirector.Shot.ControlRoom;
                float zoom = Mathf.InverseLerp(110f, 45f, Vector3.Distance(main.transform.position, moonWorld));
                if (legend.Count == 0)
                    legend = orbit.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Legend")).Select(t => t.gameObject).ToList();
                Frame(flight, main, ui: !roomShot, fovV: 1.45f, poseV: c =>
                {
                    foreach (var g in legend) g.SetActive(false);
                    if (roomShot) return;
                    var wide = new Pose(topEye, topRot);
                    c.transform.SetPositionAndRotation(Vector3.Lerp(wide.position, c.transform.position, zoom),
                        Quaternion.Slerp(wide.rotation, c.transform.rotation, zoom));
                    c.fieldOfView = Mathf.Lerp(62f, c.fieldOfView, zoom);
                });
                foreach (var g in legend) g.SetActive(true);
                yield return null;
            }

            // F) Cinemática del amerizaje (sin sus rótulos: el video pone los suyos).
            while (!cine.Playing && mgr.Phase != MissionPhase.PostMission) yield return null;
            var splash = Begin("splash", true);
            while (cine.Playing)
            {
                var overlay = cine.GetComponentsInChildren<Canvas>(true).FirstOrDefault(c => c.name == "CinematicOverlay");
                if (overlay != null) overlay.enabled = false;
                RenderTo(cine.Camera, rtH);
                Save(rtH, texH, Path.Combine(Root, "splash_H", $"frame_{splash.frames:D5}.jpg"));
                RenderTo(cine.Camera, rtV, 1.3f);
                Save(rtV, texV, Path.Combine(Root, "splash_V", $"frame_{splash.frames:D5}.jpg"));
                splash.frames++;
                yield return null;
            }

            // G) Análisis post-misión (con su animación de entrada).
            while (mgr.Phase != MissionPhase.PostMission) yield return null;
            var analysis = Begin("analysis", false);
            for (int i = 0; i < 7 * Fps; i++)
            {
                Frame(analysis, main, ui: true);
                yield return null;
            }

            File.WriteAllText(Path.Combine(Root, "mission_frames.txt"),
                string.Join("\n", new[] { design, consensus, launch, room, flight, splash, analysis }.Select(p => $"{p.name} {p.frames}")));
            Teardown();
        }

        // ───────────── Rodaje del centro espacial ─────────────

        [UnityTest]
        public IEnumerator Shoot_SpaceCenter()
        {
            Setup();
            SceneManager.LoadScene(SceneFlow.SpaceCenterScene);
            yield return null;
            yield return null;
            yield return new WaitForSeconds(1f);
            var main = Camera.main;
            var rocket = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.name.StartsWith("LV_SLS")).OrderByDescending(t => t.position.z).First();
            Vector3 center = rocket.position + Vector3.up * 48f;
            var plate = Begin("center", true);
            for (int i = 0; i < 7 * Fps; i++)
            {
                float a = Mathf.Lerp(-112f, -70f, i / (7f * Fps)) * Mathf.Deg2Rad;
                Frame(plate, main, ui: false,
                    poseH: c => { c.transform.position = center + new Vector3(Mathf.Cos(a) * 125f, -18f + i * 0.05f, Mathf.Sin(a) * 125f); c.transform.LookAt(center); c.fieldOfView = 50f; },
                    poseV: c => { c.transform.position = center + new Vector3(Mathf.Cos(a) * 125f, -26f + i * 0.05f, Mathf.Sin(a) * 125f); c.transform.LookAt(center + Vector3.up * 4f); c.fieldOfView = 72f; });
                yield return null;
            }
            File.WriteAllText(Path.Combine(Root, "center_frames.txt"), $"center {plate.frames}");
            Teardown();
        }
    }
}
