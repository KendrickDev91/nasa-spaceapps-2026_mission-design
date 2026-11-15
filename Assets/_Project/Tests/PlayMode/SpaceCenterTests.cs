using System.Collections;
using System.IO;
using System.Linq;
using MissionDesign.Characters;
using MissionDesign.Core;
using MissionDesign.Exploration;
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
    /// Recorre el centro espacial caminable y vuela la misión tripulada de retorno libre; guarda capturas en Logs/.
    /// Requiere NASA SpaceApps → Build Everything.
    /// </summary>
    public class SpaceCenterTests
    {
        [UnityTest]
        public IEnumerator SpaceCenter_PlayerWalks_DoorsOpen_InteractionsAndPlansWork()
        {
            SceneManager.LoadScene(SceneFlow.SpaceCenterScene);
            yield return null;
            yield return new WaitForSeconds(1f);

            var player = Object.FindAnyObjectByType<PlayerController>();
            var hud = Object.FindAnyObjectByType<ExplorationHud>();
            Assert.IsNotNull(player, "Falta el jugador.");
            Assert.IsNotNull(hud, "Falta el HUD de exploración.");
            Assert.IsTrue(player.IsGrounded, $"El jugador debe estar en el suelo (y = {player.transform.position.y:F2}).");
            Assert.GreaterOrEqual(Object.FindObjectsByType<Interactable>().Length, 6);
            var doors = Object.FindObjectsByType<AutoDoor>();
            Assert.GreaterOrEqual(doors.Length, 3);
            Assert.IsTrue(hud.MapRendered, "El mapa geolocalizador debe renderizarse al iniciar.");
            StringAssert.Contains("° N", hud.GeoText);
            Assert.AreEqual(7.5f, player.RunSpeed, 0.01f, "+25 % de velocidad");
            Assert.GreaterOrEqual(Object.FindObjectsByType<CrewPoser>().Length, 5, "Operadores sentados en la sala de control + jugador.");
            Assert.IsNotNull(Object.FindAnyObjectByType<DesignEntryRing>(), "Falta el círculo de entrada al diseño.");
            yield return Capture("Logs/spacecenter_01_entrance.png");

            // Caminar hacia el Centro de Control: avanza, se anima y abre la puerta de entrada.
            Vector3 start = player.transform.position;
            player.SimulatedMove = new Vector2(0f, 1f);
            yield return new WaitForSeconds(2.5f);
            Assert.Greater(Vector3.Distance(start, player.transform.position), 3f,
                $"El jugador no avanzó: de {start} a {player.transform.position}, anim {player.AnimatorSpeed:F2}, bloqueado {player.InputLocked}, " +
                $"timeScale {Time.timeScale}, suelo {player.IsGrounded}, cámara {Camera.main.transform.forward}, cc {player.GetComponent<CharacterController>().enabled}");
            Assert.Greater(player.AnimatorSpeed, 0.5f, "La animación de caminar no se activó.");
            player.SimulatedMove = new Vector2(0f, 1f);
            player.SimulatedRun = true;
            yield return new WaitForSeconds(1.2f);
            player.SimulatedMove = Vector2.zero;
            player.SimulatedRun = false;
            Assert.IsTrue(doors.Any(d => d.Openness > 0.3f), "Ninguna puerta se abrió al acercarse.");
            yield return Capture("Logs/spacecenter_02_door.png");

            // Vestíbulo: recepción con su panel de información.
            var reception = Object.FindObjectsByType<Interactable>().First(i => i.interactionId == "Reception");
            player.Teleport(reception.transform.position + new Vector3(0f, 0.1f, 1.6f), 180f);
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(reception, hud.Current, "El aviso [E] debe apuntar a recepción.");
            StringAssert.Contains("Centro de Control", hud.CurrentZone);
            yield return Capture("Logs/spacecenter_03_lobby_prompt.png");
            hud.Interact(hud.Current);
            Assert.IsTrue(hud.IsBlocking);
            StringAssert.Contains("BIENVENIDA", hud.CurrentTitle);
            hud.CloseInfo();

            // Planos (M).
            hud.OpenPlans();
            Assert.AreEqual(SpaceCenterContent.Plans.Length, hud.PageCount);
            yield return Capture("Logs/spacecenter_04_plans.png");
            hud.CloseInfo();

            // Viaje rápido: sala de control, ensamblaje (SLS apilado) y plataforma.
            hud.TravelTo(1);
            yield return new WaitForSeconds(0.8f);
            StringAssert.Contains("Sala de control", hud.CurrentZone);
            yield return Capture("Logs/spacecenter_05_control_room.png");
            hud.TravelTo(2);
            yield return new WaitForSeconds(0.6f);
            player.SimulatedMove = new Vector2(0f, 1f);
            yield return new WaitForSeconds(4.5f);
            player.SimulatedMove = Vector2.zero;
            yield return new WaitForSeconds(0.3f);
            StringAssert.Contains("Ensamblaje", hud.CurrentZone, "El jugador debe entrar al edificio por la puerta de personal.");
            Assert.AreEqual(Outfit.AssemblyEngineer, hud.CurrentOutfit, "En la nave de ensamblaje lleva casco.");
            yield return Capture("Logs/spacecenter_06_assembly.png");
            var cleanRoom = Object.FindObjectsByType<ZoneVolume>().First(z => z.zoneName.Contains("Sala limpia"));
            player.Teleport(cleanRoom.transform.position + new Vector3(3.5f, -4.9f, 3.5f), 225f);   // dentro de las cortinas, junto a la cápsula
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(Outfit.CleanRoom, hud.CurrentOutfit, "En la sala limpia, mono, capucha y mascarilla.");
            yield return Capture("Logs/spacecenter_06b_cleanroom_suit.png");
            hud.TravelTo(3);
            yield return new WaitForSeconds(0.6f);
            player.SimulatedMove = new Vector2(0f, 1f);
            player.SimulatedRun = true;
            yield return new WaitForSeconds(10f);   // ~55 m: rampa de 50 m (2,3°) hasta la losa
            player.SimulatedMove = Vector2.zero;
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(player.transform.position.y, 1.5f, "El jugador debe subir la rampa hasta la losa.");
            Assert.AreEqual(Outfit.Astronaut, hud.CurrentOutfit, "En la plataforma, traje espacial con casco.");
            yield return Capture("Logs/spacecenter_07_launch_pad.png");

            var panel = Object.FindObjectsByType<Interactable>().First(i => i.interactionId == "LaunchPanel");
            hud.Interact(panel);
            Assert.AreEqual(3, hud.PageCount, "Plataforma, retorno libre y fotos de Artemis II.");
            hud.CloseInfo();
        }

        [UnityTest]
        public IEnumerator DesignRing_SeatsThePlayer_AndOpensMissionDesign()
        {
            SceneManager.LoadScene(SceneFlow.SpaceCenterScene);
            yield return null;
            yield return new WaitForSeconds(0.8f);
            var player = Object.FindAnyObjectByType<PlayerController>();
            var ring = Object.FindAnyObjectByType<DesignEntryRing>();
            Assert.IsNotNull(ring);
            player.Teleport(ring.transform.position + Vector3.up * 0.05f + ring.transform.forward * 1.6f, 180f);
            yield return new WaitForSeconds(0.6f);
            yield return Capture("Logs/spacecenter_08_design_ring.png");
            player.Teleport(ring.transform.position + Vector3.up * 0.05f, 180f);
            float t0 = Time.realtimeSinceStartup;
            while (!ring.IsEntering && Time.realtimeSinceStartup - t0 < 3f) yield return null;
            Assert.IsTrue(ring.IsEntering, "Pisar el círculo debe iniciar la entrada al diseño.");
            yield return new WaitForSeconds(1.0f);
            Assert.Greater(player.GetComponent<CrewPoser>().SitAmount, 0.5f, "El personaje se sienta en la silla.");
            yield return Capture("Logs/spacecenter_09_seated.png");
            t0 = Time.realtimeSinceStartup;
            while (SceneManager.GetActiveScene().name != SceneFlow.MissionDesignScene && Time.realtimeSinceStartup - t0 < 6f) yield return null;
            yield return null;
            yield return null;
            Assert.AreEqual(SceneFlow.MissionDesignScene, SceneManager.GetActiveScene().name);
            Assert.IsFalse(Object.FindAnyObjectByType<GameMenus>().IsBlocking, "Desde la consola se entra directo al diseño (sin menú principal).");
        }

        [UnityTest]
        public IEnumerator Feet_Elevator_AndFirstPerson()
        {
            SceneManager.LoadScene(SceneFlow.SpaceCenterScene);
            yield return null;
            yield return new WaitForSeconds(1.2f);
            var player = Object.FindAnyObjectByType<PlayerController>();
            var hud = Object.FindAnyObjectByType<ExplorationHud>();

            // Pies apoyados: la suela más baja queda a menos de 4 cm del suelo, quieto y caminando.
            var planter = player.GetComponent<FootPlanter>();
            Assert.IsNotNull(planter);
            Assert.Less(Mathf.Abs(planter.LastGap), 0.04f, $"De pie: hueco {planter.LastGap:F3} m");
            player.SimulatedMove = new Vector2(0f, 1f);
            yield return new WaitForSeconds(1.5f);
            Assert.Less(Mathf.Abs(planter.LastGap), 0.06f, $"Caminando: hueco {planter.LastGap:F3} m");
            player.SimulatedMove = Vector2.zero;
            yield return Capture("Logs/spacecenter_10_feet.png");

            // Ascensor de la torre: subir al brazo de acceso de la tripulación.
            var elevator = Object.FindAnyObjectByType<ElevatorController>();
            Assert.IsNotNull(elevator, "Falta el ascensor de la torre de servicio.");
            var cab = Object.FindObjectsByType<Interactable>().First(i => i.interactionId == "ElevatorCab");
            player.Teleport(cab.transform.position + Vector3.up * 0.1f, 0f);
            yield return new WaitForSeconds(0.4f);
            Assert.IsTrue(elevator.IsOnPlatform(player.transform.position), "El jugador debe estar en la cabina.");
            hud.Interact(cab);
            float t0 = Time.realtimeSinceStartup;
            while (!elevator.AtTop && Time.realtimeSinceStartup - t0 < 20f) yield return null;
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(elevator.AtTop);
            Assert.Greater(player.transform.position.y, 76f, "El jugador sube con la plataforma.");
            player.SimulatedMove = new Vector2(0f, 1f);    // cámara mirando a +Z: sale por la pasarela hacia el brazo
            var cam = Object.FindAnyObjectByType<ThirdPersonCamera>();
            cam.SnapBehind(180f);
            yield return new WaitForSeconds(1.6f);
            player.SimulatedMove = Vector2.zero;
            Assert.Greater(player.transform.position.y, 76f, "No se cae de la pasarela.");
            StringAssert.Contains("Brazo de acceso", hud.CurrentZone);
            yield return Capture("Logs/spacecenter_11_crew_arm.png");

            // Primera persona.
            cam.SetFirstPerson(true);
            cam.SnapBehind(270f);
            yield return new WaitForSeconds(0.2f);
            Assert.IsTrue(cam.FirstPerson);
            Assert.Less(Vector3.Distance(cam.transform.position, player.transform.position + Vector3.up * 1.66f), 0.3f);
            yield return Capture("Logs/spacecenter_12_first_person.png");
            cam.SetFirstPerson(false);
        }

        [UnityTest]
        public IEnumerator DemoDesigns_AreListed_AndLoadWithTheirFixedSeed()
        {
            SceneManager.LoadScene(SceneFlow.MissionDesignScene);
            yield return null;
            yield return null;
            Object.FindAnyObjectByType<GameMenus>().CloseAll();
            var demos = DesignLibrary.Demos();
            Assert.AreEqual(3, demos.Count, "Tres diseños de demostración en StreamingAssets/Demos.");
            var library = Object.FindAnyObjectByType<DesignLibraryPanel>();
            var runner = Object.FindAnyObjectByType<MissionSimulationRunner>();
            foreach (var (_, demo) in demos)
            {
                Assert.IsTrue(demo.IsDemo);
                library.Load(demo);
                yield return null;
                Assert.AreEqual(demo.demoSeed, runner.FixedSeed);
                Assert.IsTrue(MissionStateManager.Instance.CurrentValidation.canLaunch, $"{demo.designName}: {MissionStateManager.Instance.CurrentValidation.ToConsoleString()}");
            }
            library.Open();
            yield return Capture("Logs/screenshot_10_demo_designs.png");
        }

        [UnityTest]
        public IEnumerator CrewedFreeReturn_FliesFigureEight_AndRecoversPhotos()
        {
            SceneManager.LoadScene(SceneFlow.MissionDesignScene);
            yield return null;
            yield return null;
            Object.FindAnyObjectByType<GameMenus>().CloseAll();
            var mgr = MissionStateManager.Instance;
            var db = mgr.Database;
            mgr.SelectScenario(db.scenarios.First(s => s.scenarioId == "SCN_ARTEMIS_FLYBY"));
            mgr.SelectOrbit(db.orbits.First(o => o.orbitId == "ORB_FREE_RETURN"));
            mgr.SelectLaunchVehicle(db.launchVehicles.First(v => v.vehicleId == "LV_SLS_B1"));
            // El enlace láser O2O de Orion necesita estaciones ópticas (no la DSN de radio).
            mgr.SelectGroundNetwork(db.groundNetworks.First(n => n.networkId == "GND_OPTICAL"));
            foreach (var id in new[] { "BUS_ORION", "SOL_ESM", "BAT_2000", "ANT_O2O", "CDH_ORION", "INS_ORION_CAMERAS", "INS_CREW_CAMERAS" })
            {
                var c = db.AllComponents.FirstOrDefault(x => x.componentId == id);
                Assert.IsNotNull(c, id);
                mgr.SelectComponent(c);
            }
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(mgr.CurrentValidation.canLaunch, mgr.CurrentValidation.ToConsoleString());

            var roles = Object.FindAnyObjectByType<RoleTurnManager>();
            roles.SetPlayerCount(1);
            roles.ApproveAsActivePlayer();
            var runner = Object.FindAnyObjectByType<MissionSimulationRunner>();
            runner.FixedSeed = 3;
            runner.SecondsPerPhase = 2.5f;
            Assert.IsTrue(roles.TryRequestLaunch(out string msg), msg);

            var director = Object.FindAnyObjectByType<MissionCameraDirector>();
            var orbit = Object.FindAnyObjectByType<OrbitViewController>();
            var studio = Object.FindAnyObjectByType<MoonPhotoStudio>();
            float t0 = Time.realtimeSinceStartup;
            yield return new WaitForSeconds(1.2f);
            yield return Capture("Logs/screenshot_05b_earth_orbits.png");
            while (mgr.Phase < MissionPhase.OrbitInsertion && Time.realtimeSinceStartup - t0 < 20f) yield return null;
            yield return new WaitForSeconds(1.0f);
            Assert.IsTrue(director.ShowingReaction, "Tras un despegue exitoso la cámara muestra la sala de control.");
            Assert.That(Object.FindObjectsByType<CrewPoser>().Select(p => p.Current), Has.Some.EqualTo(CrewReaction.Celebrate));
            yield return Capture("Logs/screenshot_08_control_room_reaction.png");

            while (mgr.Phase < MissionPhase.ScienceOperations && Time.realtimeSinceStartup - t0 < 30f) yield return null;
            yield return new WaitForSeconds(0.3f);
            yield return Capture("Logs/screenshot_06_free_return.png");
            while (Mathf.Abs(orbit.CurrentDay - orbit.Solution.flybyTimeDays) > 0.25f && mgr.Phase == MissionPhase.ScienceOperations
                   && Time.realtimeSinceStartup - t0 < 40f) yield return null;
            yield return new WaitForSeconds(0.4f);
            Assert.Greater(studio.Photos.Count, 0, "Las cámaras deben tomar fotos durante el sobrevuelo.");
            yield return Capture("Logs/screenshot_06c_flyby_zoom.png");

            runner.SkipToEnd();
            t0 = Time.realtimeSinceStartup;
            while (mgr.Phase != MissionPhase.PostMission && Time.realtimeSinceStartup - t0 < 15f) yield return null;
            Assert.AreEqual(MissionPhase.PostMission, mgr.Phase);
            var o = mgr.LastOutcome;
            Assert.IsTrue(o.crewed);
            Assert.IsTrue(o.returnsToEarth);
            Assert.AreEqual(8, o.photoShots, "Dos cámaras: 8 fotografías para el informe.");
            // Las fotos que faltan se completan una por fotograma y los PNG se guardan en segundo plano.
            t0 = Time.realtimeSinceStartup;
            while ((studio.Photos.Count < o.photoShotsAvailable || studio.SavedFolder == null || studio.Saving) && Time.realtimeSinceStartup - t0 < 15f)
                yield return null;
            Assert.AreEqual("Fanfare", Object.FindAnyObjectByType<SfxPlayer>().LastPlayed, "Sonido de éxito al terminar.");
            Assert.AreEqual(o.photoShotsAvailable, studio.Photos.Count);
            Assert.IsTrue(System.IO.Directory.Exists(studio.SavedFolder), "Las fotos se guardan junto al reporte.");
            Assert.AreEqual(8, System.IO.Directory.GetFiles(studio.SavedFolder, "foto_*.png").Length);
            var post = Object.FindAnyObjectByType<PostMissionPanel>();
            t0 = Time.realtimeSinceStartup;
            while (post.Revealing && Time.realtimeSinceStartup - t0 < 10f) yield return null;   // presentación animada
            yield return new WaitForSeconds(1.5f);                                                // que caiga el confeti
            yield return Capture("Logs/screenshot_07_crewed_post_mission.png");

            var report = Object.FindAnyObjectByType<ExplorationReportPanel>();
            report.Open();
            Assert.IsTrue(report.IsOpen);
            Assert.AreEqual(8, report.PhotoCount);
            StringAssert.Contains("cara oculta", report.Body);
            yield return new WaitForSeconds(1.8f);   // la bitácora se "escribe"
            yield return Capture("Logs/screenshot_09_exploration_report.png");
            for (int i = 0; i < 8; i++)
            {
                report.Select(i);
                yield return Capture($"Logs/moon_photo_{i + 1:00}.png");
            }
        }

        private static IEnumerator Capture(string path)
        {
            var cam = Camera.main;
            var rt = new RenderTexture(1920, 1080, 24);
            var previous = cam.targetTexture;
            cam.targetTexture = rt;
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = previous;
            Object.Destroy(tex);
            rt.Release();
            Object.Destroy(rt);
        }
    }
}
