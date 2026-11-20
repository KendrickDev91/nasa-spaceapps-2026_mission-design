using System.Collections;
using System.IO;
using System.Linq;
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
    /// Capturas para la página de tienda (itch.io / Steam) en el idioma pedido. Se ejecuta a mano:
    /// -testFilter StoreScreenshots. Deja los PNG en Logs/store_{en|es}_*.png.
    /// </summary>
    public class StoreScreenshots
    {
        [UnityTest, Explicit("Capturas de tienda: se ejecuta a mano con -testFilter StoreScreenshots")]
        public IEnumerator Shoot_English() => Shoot(GameLanguage.English, "en");

        [UnityTest, Explicit("Capturas de tienda: se ejecuta a mano con -testFilter StoreScreenshots")]
        public IEnumerator Shoot_Spanish() => Shoot(GameLanguage.Spanish, "es");

        private static IEnumerator Shoot(GameLanguage lang, string tag)
        {
            Loc.SetForTests(lang);
            SceneManager.LoadScene(SceneFlow.MissionDesignScene);
            yield return null;
            yield return null;
            yield return new WaitForSeconds(0.5f);
            yield return EnglishLanguageTests.Shot($"Logs/store_{tag}_01_main_menu.png");
            Object.FindAnyObjectByType<GameMenus>().CloseAll();

            var mgr = MissionStateManager.Instance;
            var db = mgr.Database;
            mgr.SelectScenario(db.scenarios.First(s => s.scenarioId == "SCN_ARTEMIS_FLYBY"));
            mgr.SelectOrbit(db.orbits.First(o => o.orbitId == "ORB_FREE_RETURN"));
            mgr.SelectLaunchVehicle(db.launchVehicles.First(v => v.vehicleId == "LV_SLS_B1"));
            mgr.SelectGroundNetwork(db.groundNetworks.First(n => n.networkId == "GND_OPTICAL"));
            foreach (var id in new[] { "BUS_ORION", "SOL_ESM", "BAT_2000", "ANT_O2O", "CDH_ORION", "INS_ORION_CAMERAS", "INS_CREW_CAMERAS" })
                mgr.SelectComponent(db.AllComponents.First(x => x.componentId == id));
            var roles = Object.FindAnyObjectByType<RoleTurnManager>();
            roles.SetActiveRole(MissionRole.PrincipalScientist);
            yield return new WaitForSeconds(1.2f);
            yield return EnglishLanguageTests.Shot($"Logs/store_{tag}_02_design.png");

            roles.SetPlayerCount(1);
            roles.ApproveAsActivePlayer();
            var runner = Object.FindAnyObjectByType<MissionSimulationRunner>();
            runner.FixedSeed = 3;
            runner.SecondsPerPhase = 6f;
            Assert.IsTrue(roles.TryRequestLaunch(out string msg), msg);
            float t0 = Time.realtimeSinceStartup;
            yield return new WaitForSeconds(3.5f);
            yield return EnglishLanguageTests.Shot($"Logs/store_{tag}_03_launch.png");

            while (mgr.Phase < MissionPhase.OrbitInsertion && Time.realtimeSinceStartup - t0 < 60f) yield return null;
            yield return new WaitForSeconds(4.5f);
            yield return EnglishLanguageTests.Shot($"Logs/store_{tag}_04_trajectory.png");

            var orbit = Object.FindAnyObjectByType<OrbitViewController>();
            while (mgr.Phase < MissionPhase.ScienceOperations && Time.realtimeSinceStartup - t0 < 90f) yield return null;
            while (Mathf.Abs(orbit.CurrentDay - orbit.Solution.flybyTimeDays) > 0.15f && mgr.Phase == MissionPhase.ScienceOperations
                   && Time.realtimeSinceStartup - t0 < 120f) yield return null;
            yield return new WaitForSeconds(0.4f);
            yield return EnglishLanguageTests.Shot($"Logs/store_{tag}_05_flyby.png");

            // Amerizaje: la cinemática usa su propia cámara.
            var cine = Object.FindAnyObjectByType<SplashdownCinematic>(FindObjectsInactive.Include);
            while ((cine == null || cine.Shot != "principales") && mgr.Phase != MissionPhase.PostMission && Time.realtimeSinceStartup - t0 < 200f)
                yield return null;
            if (cine != null && cine.Camera != null && cine.Shot == "principales")
            {
                yield return new WaitForSeconds(1.5f);
                yield return CaptureCamera(cine.Camera, $"Logs/store_{tag}_06_splashdown.png");
            }

            while (mgr.Phase != MissionPhase.PostMission && Time.realtimeSinceStartup - t0 < 260f) yield return null;
            var post = Object.FindAnyObjectByType<PostMissionPanel>();
            float t1 = Time.realtimeSinceStartup;
            while (post.Revealing && Time.realtimeSinceStartup - t1 < 12f) yield return null;
            yield return new WaitForSeconds(2.5f);
            yield return EnglishLanguageTests.Shot($"Logs/store_{tag}_07_post_mission.png");

            var report = Object.FindAnyObjectByType<ExplorationReportPanel>(FindObjectsInactive.Include);
            report.Open();
            yield return new WaitForSeconds(6f);   // el texto se escribe con animación
            yield return EnglishLanguageTests.Shot($"Logs/store_{tag}_08_exploration_report.png");
            Loc.SetForTests(GameLanguage.Spanish);
        }

        [UnityTest, Explicit("Capturas de tienda: se ejecuta a mano con -testFilter StoreScreenshots")]
        public IEnumerator Shoot_Scenarios_English() => ShootScenarios(GameLanguage.English, "en");

        [UnityTest, Explicit("Capturas de tienda: se ejecuta a mano con -testFilter StoreScreenshots")]
        public IEnumerator Shoot_Scenarios_Spanish() => ShootScenarios(GameLanguage.Spanish, "es");

        /// <summary>
        /// Una misión por escenario (el ganador de ejemplo en Avanzado): pantalla de diseño, despegue con su cohete y
        /// la nave en su órbita (LEO, GEO, Luna). Muestra la variedad del juego más allá del sobrevuelo tripulado.
        /// </summary>
        private static IEnumerator ShootScenarios(GameLanguage lang, string tag)
        {
            var plan = new[]
            {
                ("SCN_EARTH_CLIMATE", "climate", MissionRole.PrincipalScientist),
                ("SCN_GEO_WEATHER", "geo", MissionRole.TelecomLaunchEngineer),
                ("SCN_LUNAR_RECON", "lunar", MissionRole.SystemsEngineer),
                ("SCN_CUBESAT_EDU", "cubesat", MissionRole.MissionDirector),
            };
            foreach (var (scenarioId, name, role) in plan)
            {
                Loc.SetForTests(lang);
                SceneManager.LoadScene(SceneFlow.MissionDesignScene);
                yield return null;
                yield return null;
                yield return new WaitForSeconds(0.5f);
                Object.FindAnyObjectByType<GameMenus>().CloseAll();

                var mgr = MissionStateManager.Instance;
                var example = DesignLibrary.Demos().First(d => d.snapshot.scenarioId == scenarioId
                                                              && d.snapshot.difficulty == DifficultyLevel.Advanced && d.snapshot.demoOrder == 0);
                Object.FindAnyObjectByType<DesignLibraryPanel>(FindObjectsInactive.Include).Load(example.snapshot);
                var roles = Object.FindAnyObjectByType<RoleTurnManager>();
                roles.SetActiveRole(role);
                yield return new WaitForSeconds(1.2f);
                yield return EnglishLanguageTests.Shot($"Logs/store_{tag}_s_{name}_1_design.png");

                roles.SetPlayerCount(1);
                roles.ApproveAsActivePlayer();
                var runner = Object.FindAnyObjectByType<MissionSimulationRunner>();
                runner.FixedSeed = 3;
                runner.SecondsPerPhase = 6f;
                Assert.IsTrue(roles.TryRequestLaunch(out string msg), scenarioId + ": " + msg);
                float t0 = Time.realtimeSinceStartup;
                yield return new WaitForSeconds(3.2f);
                yield return EnglishLanguageTests.Shot($"Logs/store_{tag}_s_{name}_2_launch.png");

                while (mgr.Phase < MissionPhase.ScienceOperations && Time.realtimeSinceStartup - t0 < 90f) yield return null;
                yield return new WaitForSeconds(3f);
                yield return EnglishLanguageTests.Shot($"Logs/store_{tag}_s_{name}_3_orbit.png");

                while (mgr.Phase != MissionPhase.PostMission && Time.realtimeSinceStartup - t0 < 200f) yield return null;
                var post = Object.FindAnyObjectByType<PostMissionPanel>();
                float t1 = Time.realtimeSinceStartup;
                while (post != null && post.Revealing && Time.realtimeSinceStartup - t1 < 12f) yield return null;
                yield return new WaitForSeconds(2f);
                yield return EnglishLanguageTests.Shot($"Logs/store_{tag}_s_{name}_4_result.png");
            }

            // Centro espacial: el recorrido en tercera persona.
            SceneManager.LoadScene(SceneFlow.SpaceCenterScene);
            yield return new WaitForSeconds(2f);
            yield return EnglishLanguageTests.Shot($"Logs/store_{tag}_s_space_center.png");
            Loc.SetForTests(GameLanguage.Spanish);
        }

        /// <summary>Renderiza una cámara concreta con sus lienzos de cámara (la cinemática).</summary>
        private static IEnumerator CaptureCamera(Camera cam, string path)
        {
            yield return null;
            var rt = new RenderTexture(1920, 1080, 24);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            RenderTexture.active = null;
            cam.targetTexture = prev;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
        }
    }
}
