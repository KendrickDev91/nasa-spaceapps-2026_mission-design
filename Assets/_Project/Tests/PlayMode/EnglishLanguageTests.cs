using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MissionDesign.Core;
using MissionDesign.Multiplayer;
using MissionDesign.Simulation;
using MissionDesign.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MissionDesign.Tests
{
    /// <summary>Todas las pruebas corren en español salvo las que piden inglés explícitamente.</summary>
    [SetUpFixture]
    public class LanguageFixture
    {
        [OneTimeSetUp]
        public void Spanish() => Loc.SetForTests(GameLanguage.Spanish);
    }

    /// <summary>El juego en inglés: menús y pantalla de diseño traducidos (capturas en Logs/).</summary>
    public class EnglishLanguageTests
    {
        [UnitySetUp]
        public IEnumerator LoadInEnglish()
        {
            Loc.SetForTests(GameLanguage.English);
            SceneManager.LoadScene(SceneFlow.MissionDesignScene);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator BackToSpanish()
        {
            Loc.SetForTests(GameLanguage.Spanish);
            yield return null;
        }

        /// <summary>Palabras que delatan texto en español que se quedó sin traducir.</summary>
        internal static readonly Regex SpanishWords = new Regex(
            @"\b(misión|diseño|órbita|energía|presupuesto|jugador(es)?|lanzamiento|científico|ingeniero|equipo|cohete|antena|batería|turno|aprobar|guardar|cargar|comparar|siguiente|anterior|cerrar|salir|opciones|planos|centro espacial|valor|consenso|telemetría|días?|meses|también|según|conflicto|advertencia)\b",
            RegexOptions.IgnoreCase);

        internal static List<string> SpanishLeftovers()
        {
            return Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(t => t.isActiveAndEnabled && !string.IsNullOrEmpty(t.text) && t.alpha > 0f)
                .Where(t => SpanishWords.IsMatch(Regex.Replace(t.text, "<[^>]+>", "")))
                .Select(t => $"{Regex.Replace(t.text, "<[^>]+>", "")}   ({HierarchyPath(t.transform)})")
                .Distinct().ToList();
        }

        private static string HierarchyPath(Transform t) => t.parent == null ? t.name : HierarchyPath(t.parent) + "/" + t.name;

        [UnityTest]
        public IEnumerator SpaceCenter_IsInEnglish()
        {
            SceneManager.LoadScene(SceneFlow.SpaceCenterScene);
            yield return new WaitForSeconds(1.5f);
            var hud = Object.FindAnyObjectByType<MissionDesign.Exploration.ExplorationHud>();
            Assert.IsNotNull(hud);
            // Todos los textos de la escena, también los de paneles ocultos (pausa, info, mapa).
            var all = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => !string.IsNullOrEmpty(t.text))
                .Select(t => Regex.Replace(t.text, "<[^>]+>", ""))
                .Where(s => SpanishWords.IsMatch(s)).Distinct().ToList();
            yield return Shot("Logs/screenshot_en_space_center.png");
            Debug.Log("[EnglishAudit/SpaceCenter] " + string.Join("\n", all));
            Assert.IsEmpty(all, "Texto en español en el centro espacial:\n" + string.Join("\n", all));
        }

        [UnityTest]
        public IEnumerator MainMenu_AndDesignScreen_AreInEnglish()
        {
            var menus = Object.FindAnyObjectByType<GameMenus>();
            Assert.IsTrue(menus.IsBlocking, "El menú principal se muestra al iniciar.");
            var texts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(t => t.text).ToList();
            Assert.That(texts, Has.Some.Contains("PLAY · DESIGN A MISSION"));
            Assert.That(texts, Has.Some.Contains("Options"));
            yield return Shot("Logs/screenshot_en_main_menu.png");

            menus.CloseAll();
            yield return new WaitForSeconds(0.3f);
            yield return Shot("Logs/screenshot_en_design.png");

            OptionsMenu.Open();
            yield return null;
            var optionLabels = Object.FindAnyObjectByType<OptionsMenu>().GetComponentsInChildren<TMP_Text>().Select(t => t.text).ToList();
            Assert.That(optionLabels, Has.Some.Contains("Master volume"));
            Assert.That(optionLabels, Has.Some.Contains("English"));
            OptionsMenu.Close();
        }

        /// <summary>
        /// Misión tripulada completa en inglés: diseño, vuelo, análisis post-misión e informe de exploración.
        /// Junta todo el texto visible que aún delate español y lo informa (y falla si queda alguno).
        /// </summary>
        [UnityTest]
        public IEnumerator FullCrewedMission_InEnglish_HasNoSpanishLeftovers()
        {
            var leftovers = new HashSet<string>();
            void Collect(string where)
            {
                foreach (var s in SpanishLeftovers()) leftovers.Add($"[{where}] {s.Replace("\n", " ⏎ ")}");
            }

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
            foreach (var role in RoleTurnManager.AllRoles)
            {
                roles.SetActiveRole(role);
                yield return new WaitForSeconds(0.2f);
                Collect("design/" + role);
            }
            Assert.IsTrue(mgr.CurrentValidation.canLaunch, mgr.CurrentValidation.ToConsoleString());

            roles.SetPlayerCount(1);
            roles.ApproveAsActivePlayer();
            var runner = Object.FindAnyObjectByType<MissionSimulationRunner>();
            runner.FixedSeed = 3;
            runner.SecondsPerPhase = 2.5f;
            Assert.IsTrue(roles.TryRequestLaunch(out string msg), msg);
            float t0 = Time.realtimeSinceStartup;
            var seen = new HashSet<MissionPhase>();
            while (mgr.Phase != MissionPhase.PostMission && Time.realtimeSinceStartup - t0 < 60f)
            {
                if (seen.Add(mgr.Phase)) { yield return new WaitForSeconds(0.6f); Collect("flight/" + mgr.Phase); }
                yield return null;
            }
            Assert.AreEqual(MissionPhase.PostMission, mgr.Phase);
            var post = Object.FindAnyObjectByType<PostMissionPanel>();
            t0 = Time.realtimeSinceStartup;
            while (post.Revealing && Time.realtimeSinceStartup - t0 < 10f) yield return null;
            yield return new WaitForSeconds(0.5f);
            Collect("post-mission");
            yield return Shot("Logs/screenshot_en_post_mission.png");

            var report = Object.FindAnyObjectByType<ExplorationReportPanel>(FindObjectsInactive.Include);
            if (report != null)
            {
                report.Open();
                yield return new WaitForSeconds(0.5f);
                Collect("exploration-report");
                yield return Shot("Logs/screenshot_en_exploration_report.png");
            }

            Debug.Log("[EnglishAudit] " + leftovers.Count + " posibles restos en español:\n" + string.Join("\n", leftovers));
            Assert.IsEmpty(leftovers, "Texto en español con el juego en inglés:\n" + string.Join("\n", leftovers));
        }

        /// <summary>Renderiza la cámara principal con la UI (los lienzos superpuestos pasan un instante a la cámara).</summary>
        internal static IEnumerator Shot(string path)
        {
            yield return null;
            var cam = Camera.main;
            var rt = new RenderTexture(1920, 1080, 24);
            var overlays = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToList();
            foreach (var c in overlays)
            {
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = cam.nearClipPlane + 0.05f + 0.001f * (2000 - Mathf.Min(c.sortingOrder, 1999));
            }
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            RenderTexture.active = null;
            cam.targetTexture = null;
            foreach (var c in overlays) c.renderMode = RenderMode.ScreenSpaceOverlay;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
        }
    }
}
