using System.Collections;
using System.IO;
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
    /// <summary>Opciones: se abren desde el menú principal y la pausa, aplican al instante y guardan una captura en Logs/.</summary>
    public class OptionsMenuTests
    {
        private float savedMaster, savedSfx, savedMouse;
        private bool savedInvert;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            savedMaster = GameSettings.MasterVolume;
            savedSfx = GameSettings.SfxVolume;
            savedMouse = GameSettings.MouseSensitivity;
            savedInvert = GameSettings.InvertY;
            SceneManager.LoadScene("MissionDesign_Main");
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            OptionsMenu.Close();
            GameSettings.SetMasterVolume(savedMaster);
            GameSettings.SetSfxVolume(savedSfx);
            GameSettings.SetMouseSensitivity(savedMouse);
            GameSettings.SetInvertY(savedInvert);
            GameSettings.Save();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Options_OpenFromMenus_ApplyVolumeAndToggles()
        {
            var menus = Object.FindAnyObjectByType<GameMenus>();
            var optionsButton = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .First(b => b.name == "OptionsButton");
            optionsButton.onClick.Invoke();
            yield return null;
            Assert.IsTrue(OptionsMenu.IsOpen, "El botón Opciones del menú principal abre la pantalla.");
            Assert.IsTrue(menus.IsBlocking, "Con las Opciones abiertas no se juega por debajo.");

            var root = Object.FindAnyObjectByType<OptionsMenu>();
            var labels = root.GetComponentsInChildren<TMP_Text>().Select(t => t.text).ToList();
            foreach (var expected in new[] { "Modo de pantalla", "Resolución", "Calidad gráfica", "Volumen general", "Efectos de sonido", "Sensibilidad del ratón" })
                Assert.That(labels, Has.Some.Contains(expected));

            var sliders = root.GetComponentsInChildren<Slider>();
            Assert.AreEqual(3, sliders.Length, "volumen general, efectos y sensibilidad");
            sliders[0].value = 0.4f;
            Assert.AreEqual(0.4f, AudioListener.volume, 1e-3f, "El volumen general se aplica al instante.");
            sliders[1].value = 0.25f;
            Assert.AreEqual(0.25f, GameSettings.SfxVolume, 1e-3f);

            bool invert = GameSettings.InvertY;
            var invertRow = root.GetComponentsInChildren<TMP_Text>().First(t => t.text == "Invertir eje vertical").transform;
            // El botón "›" de la fila es el último botón creado después de la etiqueta.
            var buttons = root.GetComponentsInChildren<Button>();
            var nextOfInvert = buttons.Where(b => b.transform.GetSiblingIndex() > invertRow.GetSiblingIndex()).First(b => b.GetComponentInChildren<TMP_Text>().text == "›");
            nextOfInvert.onClick.Invoke();
            Assert.AreNotEqual(invert, GameSettings.InvertY, "El selector cambia la preferencia.");

            yield return Capture("Logs/screenshot_options.png", root.GetComponent<Canvas>());
            OptionsMenu.Close();
            Assert.IsFalse(OptionsMenu.IsOpen);

            // También desde la pausa.
            menus.CloseAll();
            var pauseOptions = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .First(b => b.name == "PauseOptionsButton");
            pauseOptions.onClick.Invoke();
            yield return null;
            Assert.IsTrue(OptionsMenu.IsOpen, "El botón Opciones de la pausa abre la pantalla.");
        }

        private static IEnumerator Capture(string path, Canvas overlay)
        {
            var cam = Camera.main;
            var rt = new RenderTexture(1920, 1080, 24);
            var mode = overlay.renderMode;
            overlay.renderMode = RenderMode.ScreenSpaceCamera;
            overlay.worldCamera = cam;
            overlay.planeDistance = cam.nearClipPlane + 0.05f;
            cam.targetTexture = rt;
            yield return null;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            RenderTexture.active = null;
            cam.targetTexture = null;
            overlay.renderMode = mode;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
        }
    }
}
