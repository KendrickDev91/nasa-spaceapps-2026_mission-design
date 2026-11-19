using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MissionDesign.Core
{
    /// <summary>
    /// Preferencias del jugador (vídeo, audio y controles), guardadas en PlayerPrefs y aplicadas al arrancar.
    /// Todo pasa por aquí: el menú de Opciones solo cambia valores y llama a <see cref="Save"/>.
    /// </summary>
    public static class GameSettings
    {
        private const string Prefix = "settings.";

        public static float MasterVolume { get; private set; } = 1f;
        public static float SfxVolume { get; private set; } = 1f;
        public static int QualityLevel { get; private set; } = -1;
        public static FullScreenMode DisplayMode { get; private set; } = FullScreenMode.FullScreenWindow;
        public static int ResolutionWidth { get; private set; }
        public static int ResolutionHeight { get; private set; }
        public static bool VSync { get; private set; } = true;
        /// <summary>Multiplicador de la sensibilidad del ratón de la cámara del centro espacial (1 = la de fábrica).</summary>
        public static float MouseSensitivity { get; private set; } = 1f;
        public static bool InvertY { get; private set; }

        /// <summary>Se dispara cada vez que cambia una preferencia.</summary>
        public static event Action Changed;

        public static readonly FullScreenMode[] DisplayModes =
            { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };

        public static string DisplayModeLabel(FullScreenMode m) => m switch
        {
            FullScreenMode.ExclusiveFullScreen => "Pantalla completa",
            FullScreenMode.FullScreenWindow => "Ventana sin bordes",
            _ => "Ventana"
        };

        /// <summary>Resoluciones de la pantalla sin duplicados por frecuencia, de menor a mayor.</summary>
        public static List<Vector2Int> AvailableResolutions()
        {
            var list = Screen.resolutions.Select(r => new Vector2Int(r.width, r.height))
                .Where(r => r.x >= 1024 && r.y >= 576).Distinct().OrderBy(r => r.x * r.y).ToList();
            var current = new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);
            if (list.Count == 0 || !list.Contains(current)) list.Add(current);
            return list.Distinct().OrderBy(r => r.x * r.y).ToList();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            Load();
            Apply(applyResolution: !Application.isEditor && !Application.isBatchMode);
        }

        public static void Load()
        {
            MasterVolume = PlayerPrefs.GetFloat(Prefix + "master", 1f);
            SfxVolume = PlayerPrefs.GetFloat(Prefix + "sfx", 1f);
            QualityLevel = PlayerPrefs.GetInt(Prefix + "quality", -1);
            DisplayMode = (FullScreenMode)PlayerPrefs.GetInt(Prefix + "displayMode", (int)FullScreenMode.FullScreenWindow);
            ResolutionWidth = PlayerPrefs.GetInt(Prefix + "width", 0);
            ResolutionHeight = PlayerPrefs.GetInt(Prefix + "height", 0);
            VSync = PlayerPrefs.GetInt(Prefix + "vsync", 1) == 1;
            MouseSensitivity = PlayerPrefs.GetFloat(Prefix + "mouse", 1f);
            InvertY = PlayerPrefs.GetInt(Prefix + "invertY", 0) == 1;
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat(Prefix + "master", MasterVolume);
            PlayerPrefs.SetFloat(Prefix + "sfx", SfxVolume);
            PlayerPrefs.SetInt(Prefix + "quality", QualityLevel);
            PlayerPrefs.SetInt(Prefix + "displayMode", (int)DisplayMode);
            PlayerPrefs.SetInt(Prefix + "width", ResolutionWidth);
            PlayerPrefs.SetInt(Prefix + "height", ResolutionHeight);
            PlayerPrefs.SetInt(Prefix + "vsync", VSync ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "mouse", MouseSensitivity);
            PlayerPrefs.SetInt(Prefix + "invertY", InvertY ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>Aplica todo al motor. La resolución solo se toca fuera del editor (en el editor la ventana la controla Unity).</summary>
        public static void Apply(bool applyResolution = true)
        {
            AudioListener.volume = Mathf.Clamp01(MasterVolume);
            if (QualityLevel >= 0 && QualityLevel < QualitySettings.names.Length && QualitySettings.GetQualityLevel() != QualityLevel)
                QualitySettings.SetQualityLevel(QualityLevel, true);
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = VSync ? -1 : 144;
            if (applyResolution)
            {
                int w = ResolutionWidth > 0 ? ResolutionWidth : Screen.currentResolution.width;
                int h = ResolutionHeight > 0 ? ResolutionHeight : Screen.currentResolution.height;
                Screen.SetResolution(w, h, DisplayMode);
            }
            Changed?.Invoke();
        }

        public static void SetMasterVolume(float v) { MasterVolume = Mathf.Clamp01(v); Apply(false); }
        public static void SetSfxVolume(float v) { SfxVolume = Mathf.Clamp01(v); Apply(false); }
        public static void SetQuality(int level) { QualityLevel = Mathf.Clamp(level, 0, QualitySettings.names.Length - 1); Apply(false); }
        public static void SetVSync(bool on) { VSync = on; Apply(false); }
        public static void SetMouseSensitivity(float v) { MouseSensitivity = Mathf.Clamp(v, 0.2f, 3f); Apply(false); }
        public static void SetInvertY(bool on) { InvertY = on; Apply(false); }

        public static void SetDisplay(FullScreenMode mode, Vector2Int resolution)
        {
            DisplayMode = mode;
            ResolutionWidth = resolution.x;
            ResolutionHeight = resolution.y;
            Apply(!Application.isEditor);
        }

        /// <summary>Vuelve a los valores de fábrica (resolución nativa, ventana sin bordes, calidad del proyecto).</summary>
        public static void ResetToDefaults()
        {
            foreach (var k in new[] { "master", "sfx", "quality", "displayMode", "width", "height", "vsync", "mouse", "invertY" })
                PlayerPrefs.DeleteKey(Prefix + k);
            Load();
            Apply(!Application.isEditor);
            Save();
        }
    }
}
