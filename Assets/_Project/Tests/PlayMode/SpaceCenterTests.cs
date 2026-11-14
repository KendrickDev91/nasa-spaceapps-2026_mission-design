using System.Collections;
using System.IO;
using System.Linq;
using MissionDesign.Core;
using MissionDesign.Exploration;
using MissionDesign.Multiplayer;
using MissionDesign.Simulation;
using MissionDesign.UI;
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
            yield return Capture("Logs/spacecenter_06_assembly.png");
            hud.TravelTo(3);
            yield return new WaitForSeconds(0.6f);
            player.SimulatedMove = new Vector2(0f, 1f);
            player.SimulatedRun = true;
            yield return new WaitForSeconds(10f);   // ~55 m: rampa de 50 m (2,3°) hasta la losa
            player.SimulatedMove = Vector2.zero;
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(player.transform.position.y, 1.5f, "El jugador debe subir la rampa hasta la losa.");
            yield return Capture("Logs/spacecenter_07_launch_pad.png");

            var panel = Object.FindObjectsByType<Interactable>().First(i => i.interactionId == "LaunchPanel");
            hud.Interact(panel);
            Assert.AreEqual(3, hud.PageCount, "Plataforma, retorno libre y fotos de Artemis II.");
            hud.CloseInfo();
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

            float t0 = Time.realtimeSinceStartup;
            while (mgr.Phase < MissionPhase.ScienceOperations && Time.realtimeSinceStartup - t0 < 20f) yield return null;
            yield return new WaitForSeconds(1.6f);
            yield return Capture("Logs/screenshot_06_free_return.png");

            runner.SkipToEnd();
            t0 = Time.realtimeSinceStartup;
            while (mgr.Phase != MissionPhase.PostMission && Time.realtimeSinceStartup - t0 < 15f) yield return null;
            Assert.AreEqual(MissionPhase.PostMission, mgr.Phase);
            Assert.IsTrue(mgr.LastOutcome.crewed);
            Assert.IsTrue(mgr.LastOutcome.returnsToEarth);
            yield return Capture("Logs/screenshot_07_crewed_post_mission.png");
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
