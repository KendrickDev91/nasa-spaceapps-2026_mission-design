using System.Collections;
using System.Linq;
using MissionDesign.Core;
using MissionDesign.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MissionDesign.Tests
{
    /// <summary>La ventana «Diseños» filtra los ejemplos por escenario y dificultad y los carga en la mesa de diseño.</summary>
    public class DesignExamplesTests
    {
        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene(SceneFlow.MissionDesignScene);
            yield return null;
            yield return null;
            Object.FindAnyObjectByType<GameMenus>().CloseAll();
        }

        private static string[] RowTitles(DesignLibraryPanel panel) =>
            panel.GetComponentsInChildren<TMP_Text>().Where(t => t.name == "Name" && t.gameObject.activeInHierarchy).Select(t => t.text).ToArray();

        [UnityTest]
        public IEnumerator Library_FiltersExamplesByScenarioAndDifficulty_AndLoadsThem()
        {
            var mgr = MissionStateManager.Instance;
            var db = mgr.Database;
            mgr.SelectScenario(db.scenarios.First(s => s.scenarioId == "SCN_ARTEMIS_FLYBY"));
            var panel = Object.FindAnyObjectByType<DesignLibraryPanel>();
            panel.Open();
            yield return null;
            var titles = RowTitles(panel);
            Assert.That(titles, Has.Some.Contains("Artemis II con fotos"), "Al abrir muestra el escenario elegido (Artemis, Básico).");
            Assert.That(titles, Has.None.Contains("Clima"), "Los ejemplos de otros escenarios quedan filtrados.");
            yield return EnglishLanguageTests.Shot("Logs/screenshot_12_design_examples.png");

            // Ir al escenario anterior con el botón ‹ (en el catálogo, antes de Artemis está el Sandbox).
            var prev = panel.GetComponentsInChildren<Button>().First(b => b.name == "FilterButton" && b.GetComponentInChildren<TMP_Text>().text == "‹");
            prev.onClick.Invoke();
            yield return null;
            titles = RowTitles(panel);
            Assert.That(titles, Has.None.Contains("Artemis II con fotos"));
            Assert.That(titles, Has.Some.Contains("Sandbox"));

            // Cargar el ganador del clima en Avanzado.
            var climate = db.scenarios.FindIndex(s => s.scenarioId == "SCN_EARTH_CLIMATE");
            var example = Simulation.DesignLibrary.Demos().First(d => d.snapshot.scenarioId == "SCN_EARTH_CLIMATE"
                                                                     && d.snapshot.difficulty == DifficultyLevel.Advanced && d.snapshot.demoOrder == 0);
            panel.Load(example.snapshot);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual("SCN_EARTH_CLIMATE", mgr.Design.Scenario.scenarioId);
            Assert.AreEqual(DifficultyLevel.Advanced, mgr.Design.Difficulty);
            Assert.IsNotNull(mgr.Design.Computer, "En Avanzado el ejemplo trae computadora.");
            Assert.IsTrue(mgr.CurrentValidation.canLaunch, mgr.CurrentValidation.ToConsoleString());
            Assert.Greater(climate, -1);
        }
    }
}
