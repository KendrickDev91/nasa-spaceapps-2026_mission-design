using System.Collections;
using System.IO;
using System.Linq;
using MissionDesign.Core;
using MissionDesign.Data;
using MissionDesign.Multiplayer;
using MissionDesign.Simulation;
using MissionDesign.UI;
using MissionDesign.UI.Visual3D;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MissionDesign.Tests
{
    /// <summary>
    /// Prueba de humo de la escena principal: carga la escena real, juega el flujo de diseño
    /// como lo haría el equipo y guarda capturas en Logs/ para revisión visual.
    /// Requiere haber ejecutado NASA SpaceApps → Build Main Scene.
    /// </summary>
    public class MainSceneSmokeTests
    {
        private const string SceneName = "MissionDesign_Main";

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene(SceneName);
            yield return null;
            yield return null;
            // El menú principal aparece al iniciar; las pruebas juegan directamente.
            var menus = Object.FindAnyObjectByType<GameMenus>();
            Assert.IsNotNull(menus, "Falta el menú principal en la escena.");
            Assert.IsTrue(menus.IsBlocking, "El menú principal debe mostrarse al iniciar.");
            menus.CloseAll();
            Assert.IsFalse(menus.IsBlocking);
        }

        [UnityTest]
        public IEnumerator BalancedDesign_AssemblesSpacecraft_UpdatesHud_AndLaunchesWithConsensus()
        {
            var mgr = MissionStateManager.Instance;
            Assert.IsNotNull(mgr, "No hay MissionStateManager en la escena.");
            var db = mgr.Database;
            var roles = Object.FindAnyObjectByType<RoleTurnManager>();
            var catalog = Object.FindAnyObjectByType<CatalogPanel>();
            Assert.Greater(catalog.ItemCount, 0, "El catálogo del rol inicial está vacío.");

            mgr.SelectLaunchVehicle(db.launchVehicles.First(v => v.vehicleId == "LV_FALCON9"));
            mgr.SelectGroundNetwork(db.DefaultNetworkFor(OrbitRegime.LEO));
            Select(mgr, db, "BUS_MEDIUM", "SOL_WINGS", "BAT_2000", "ANT_X", "INS_MODIS", "INS_OCO2");
            yield return new WaitForSeconds(0.6f);

            var assembler = Object.FindAnyObjectByType<ModularSpacecraftAssembler>();
            Assert.AreEqual(6, assembler.MountedCount, "bus + paneles + batería + antena + 2 instrumentos");
            Assert.AreEqual(7, assembler.MountedInstances.Count(), "los paneles se montan en pareja");

            var massBar = Object.FindObjectsByType<ResourceBarView>(FindObjectsSortMode.None).First(b => b.Kind == ResourceKind.Mass);
            StringAssert.Contains("kg", massBar.ValueText);
            Assert.IsTrue(mgr.CurrentValidation.canLaunch, mgr.CurrentValidation.ToConsoleString());

            // Sin consenso no se lanza.
            Assert.IsFalse(roles.TryRequestLaunch(out string blockedMsg));
            StringAssert.Contains("consenso", blockedMsg);

            foreach (var role in RoleTurnManager.AllRoles)
            {
                roles.SetActiveRole(role);
                roles.ApproveAsActivePlayer();
            }
            Assert.IsTrue(roles.AllApproved);

            roles.SetActiveRole(MissionRole.PrincipalScientist);
            yield return Capture("Logs/screenshot_01_design_ok.png");

            var runner = Object.FindAnyObjectByType<MissionSimulationRunner>();
            runner.FixedSeed = 7;
            runner.SecondsPerPhase = 2.5f;

            Assert.IsTrue(roles.TryRequestLaunch(out string msg), msg);
            Assert.AreEqual(MissionPhase.Launch, mgr.Phase);

            // Cada set 3D durante la reproducción: plataforma, órbita y sala de control.
            yield return new WaitForSeconds(1.9f);
            yield return Capture("Logs/screenshot_03_launch.png");
            float t0 = Time.realtimeSinceStartup;
            while (mgr.Phase < MissionPhase.ScienceOperations && Time.realtimeSinceStartup - t0 < 15f) yield return null;
            yield return new WaitForSeconds(0.8f);
            yield return Capture("Logs/screenshot_03_orbit.png");
            t0 = Time.realtimeSinceStartup;
            while (mgr.Phase < MissionPhase.DataTransmission && Time.realtimeSinceStartup - t0 < 20f) yield return null;
            yield return new WaitForSeconds(0.5f);
            yield return Capture("Logs/screenshot_03_control_room.png");

            runner.SkipToEnd();
            t0 = Time.realtimeSinceStartup;
            while (mgr.Phase != MissionPhase.PostMission && Time.realtimeSinceStartup - t0 < 10f) yield return null;
            Assert.AreEqual(MissionPhase.PostMission, mgr.Phase, "La simulación no terminó.");

            var outcome = mgr.LastOutcome;
            Assert.IsNotNull(outcome);
            Assert.IsNotEmpty(outcome.explanations);
            Assert.IsTrue(File.Exists(mgr.LastReportPath), $"No se exportó el reporte: {mgr.LastReportPath}");
            var post = Object.FindAnyObjectByType<PostMissionPanel>();
            StringAssert.Contains(MissionOutcomeReport.VerdictLabel(outcome.verdict), post.VerdictText);
            StringAssert.Contains(outcome.rank, post.RankText);
            Assert.IsFalse(string.IsNullOrEmpty(outcome.similarMission), "Debe sugerir una misión real parecida.");
            yield return Capture("Logs/screenshot_04_post_mission.png");

            // Volver al diseño reinicia el consenso.
            mgr.ReturnToDesign();
            Assert.IsFalse(roles.AllApproved);
        }

        [UnityTest]
        public IEnumerator CatalogClick_AddsHeavyRadar_AndValidatorBlocksElectron()
        {
            var mgr = MissionStateManager.Instance;
            var db = mgr.Database;
            var roles = Object.FindAnyObjectByType<RoleTurnManager>();

            mgr.SelectLaunchVehicle(db.launchVehicles.First(v => v.vehicleId == "LV_ELECTRON"));
            mgr.SelectGroundNetwork(db.DefaultNetworkFor(OrbitRegime.LEO));
            Select(mgr, db, "BUS_SMALL", "SOL_BODY", "BAT_500", "ANT_X");

            roles.SetActiveRole(MissionRole.PrincipalScientist);
            yield return null;
            var nisarButton = Object.FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b => b.name == "Item_INS_NISAR");
            Assert.IsNotNull(nisarButton, "No se encontró el botón de NISAR en el catálogo.");

            // Tooltip educativo ANTES de elegir: debe avisar de la consecuencia.
            var trigger = nisarButton.GetComponent<TooltipTrigger>();
            Assert.IsNotNull(trigger, "Los botones del catálogo deben tener tooltip.");
            string tip = trigger.Resolve();
            StringAssert.Contains("Provocaría: Masa excede la capacidad del lanzador", tip);
            StringAssert.Contains("Electron", tip);
            yield return Capture("Logs/screenshot_05_tooltip.png", cam =>
            {
                var rt = (RectTransform)nisarButton.transform;
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, rt.position);
                TooltipView.Instance.Show(tip, screen + new Vector2(rt.rect.width * 0.25f, 0f));
            });
            TooltipView.Instance.Hide();

            // Clic real sobre el botón del catálogo del Científico.
            nisarButton.onClick.Invoke();
            yield return new WaitForSeconds(0.5f);

            Assert.IsTrue(mgr.Design.Instruments.Any(i => i.componentId == "INS_NISAR"));
            var v = mgr.CurrentValidation;
            Assert.IsFalse(v.canLaunch);
            Assert.That(v.Blocking.Select(i => i.title), Has.Member("Masa excede la capacidad del lanzador"));

            // Aun con consenso, el validador bloquea el lanzamiento.
            roles.SetPlayerCount(1);
            roles.ApproveAsActivePlayer();
            Assert.IsFalse(roles.TryRequestLaunch(out string msg));
            StringAssert.Contains("bloqueado", msg);
            Assert.AreEqual(MissionPhase.Design, mgr.Phase);

            yield return Capture("Logs/screenshot_02_conflict.png");
        }

        [UnityTest]
        public IEnumerator MainMenuAndTutorial_Render()
        {
            var menus = Object.FindAnyObjectByType<GameMenus>();
            menus.ShowMainMenu();
            Assert.IsTrue(menus.IsBlocking);
            yield return Capture("Logs/screenshot_00_main_menu.png");
            menus.CloseAll();
            menus.OpenTutorial();
            yield return Capture("Logs/screenshot_00_tutorial.png");
            menus.CloseAll();
            Assert.IsFalse(menus.IsBlocking);
        }

        private static void Select(MissionStateManager mgr, NasaDatabaseSO db, params string[] ids)
        {
            foreach (var id in ids)
            {
                var c = db.AllComponents.FirstOrDefault(x => x.componentId == id);
                Assert.IsNotNull(c, $"No existe {id} en la base de datos.");
                mgr.SelectComponent(c);
            }
        }

        /// <summary>Renderiza la cámara principal (con el HUD ScreenSpace-Camera) a un PNG 1920×1080.</summary>
        private static IEnumerator Capture(string path, System.Action<Camera> beforeRender = null)
        {
            var cam = Camera.main;
            var rt = new RenderTexture(1920, 1080, 24);
            var previous = cam.targetTexture;
            cam.targetTexture = rt;
            yield return null;
            yield return null;
            beforeRender?.Invoke(cam);
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
            Debug.Log($"[Smoke] Captura guardada en {Path.GetFullPath(path)}");
        }
    }
}
