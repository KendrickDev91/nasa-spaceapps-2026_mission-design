using System.Collections;
using System.Linq;
using MissionDesign.Core;
using MissionDesign.Exploration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MissionDesign.Tests
{
    /// <summary>
    /// Controles táctiles del centro espacial (móvil / tablet en el navegador): el joystick mueve al personaje, arrastrar gira
    /// la cámara y los controles se ocultan con el menú de pausa para no tapar sus botones.
    /// </summary>
    public class TouchControlsTests
    {
        [TearDown]
        public void Reset() => TouchControls.ForceForTests = false;

        [UnityTest]
        public IEnumerator TouchJoystick_MovesThePlayer_AndDragTurnsTheCamera()
        {
            TouchControls.ForceForTests = true;
            SceneManager.LoadScene(SceneFlow.SpaceCenterScene);
            yield return null;
            yield return new WaitForSeconds(1f);

            var touch = Object.FindAnyObjectByType<TouchControls>();
            Assert.IsNotNull(touch, "En una pantalla táctil aparecen los controles.");
            Assert.IsTrue(TouchControls.Active);
            var labels = touch.GetComponentsInChildren<TMPro.TMP_Text>(true).Select(t => t.text).ToList();
            Assert.That(labels, Has.Some.Contains("Saltar").Or.Some.Contains("Jump"));
            yield return EnglishLanguageTests.Shot("Logs/screenshot_touch_controls.png");

            var player = Object.FindAnyObjectByType<PlayerController>();
            Vector3 start = player.transform.position;
            TouchControls.SimulateForTests(new Vector2(0f, 1f), Vector2.zero);
            yield return new WaitForSeconds(2f);
            TouchControls.SimulateForTests(Vector2.zero, Vector2.zero);
            Assert.Greater(Vector3.Distance(start, player.transform.position), 3f, "El joystick no movió al personaje.");

            Vector3 before = Camera.main.transform.forward;
            TouchControls.SimulateForTests(Vector2.zero, new Vector2(400f, 0f));
            yield return null;
            yield return null;
            Assert.Greater(Vector3.Angle(before, Camera.main.transform.forward), 20f, "Arrastrar no giró la cámara.");

            // Con el menú de pausa abierto los controles se ocultan (sus botones no tapan los del menú).
            var menuButton = touch.GetComponentsInChildren<Button>(true).First(b => b.name == "Touch_Menu");
            menuButton.onClick.Invoke();
            yield return null;
            yield return null;
            Assert.IsTrue(Object.FindAnyObjectByType<ExplorationHud>().IsBlocking, "El botón Menú abre la pausa.");
            Assert.IsFalse(touch.gameObject.activeSelf, "Los controles táctiles se ocultan con la pausa.");
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator Desktop_WithoutTouchscreen_HasNoTouchControls()
        {
            if (TouchControls.IsTouchDevice) Assert.Ignore("Esta máquina tiene pantalla táctil.");
            SceneManager.LoadScene(SceneFlow.SpaceCenterScene);
            yield return null;
            yield return new WaitForSeconds(0.5f);
            Assert.IsNull(Object.FindAnyObjectByType<TouchControls>());
        }
    }
}
