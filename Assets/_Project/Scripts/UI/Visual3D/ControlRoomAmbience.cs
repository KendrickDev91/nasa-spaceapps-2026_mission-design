using System;
using MissionDesign.Core;
using MissionDesign.Simulation;
using TMPro;
using UnityEngine;

namespace MissionDesign.UI.Visual3D
{
    /// <summary>
    /// Vida de la sala de control: los monitores de los operadores desplazan sus gráficas y mapas, parpadean de vez en cuando
    /// y la fila de relojes muestra hora UTC, hora de Florida, tiempo de misión (MET), cuenta regresiva, distancia a la Luna y
    /// la estación de la Red del Espacio Profundo que tiene contacto (Goldstone, Madrid o Canberra según la hora).
    /// </summary>
    public sealed class ControlRoomAmbience : MonoBehaviour
    {
        [SerializeField] private Material[] scrollingScreens;
        [SerializeField] private float scrollSpeed = 0.035f;
        [SerializeField] private TMP_Text[] clocks;

        private MissionStateManager manager;
        private MissionSimulationRunner runner;
        private OrbitViewController orbit;
        private float nextClock;
        private float launchTime = -1f;

        private void Start()
        {
            manager = MissionStateManager.Instance;
            runner = FindAnyObjectByType<MissionSimulationRunner>();
            orbit = FindAnyObjectByType<OrbitViewController>();
            if (manager != null) manager.PhaseChanged += p => { if (p == MissionPhase.Launch) launchTime = Time.time; if (p == MissionPhase.Design) launchTime = -1f; };
        }

        private void Update()
        {
            if (scrollingScreens != null)
                for (int i = 0; i < scrollingScreens.Length; i++)
                    if (scrollingScreens[i] != null)
                        scrollingScreens[i].mainTextureOffset = new Vector2(Time.time * scrollSpeed * (i + 1), 0f);

            if (clocks == null || clocks.Length == 0 || Time.unscaledTime < nextClock) return;
            nextClock = Time.unscaledTime + 0.25f;
            var utc = DateTime.UtcNow;
            Set(0, $"UTC {utc:HH:mm:ss}");
            Set(1, $"FL {utc.AddHours(-5):HH:mm:ss}");
            Set(2, $"MET {Met()}");
            Set(3, Countdown());
            Set(4, orbit != null && orbit.ShowsFreeReturn && launchTime > 0f ? $"{Loc.L("LUNA", "MOON")} {orbit.DistanceMoonKm / 1000f:N0} Mm" : $"{Loc.L("LUNA", "MOON")} 384 Mm");
            string[] dsn = { "GOLDSTONE", "MADRID", "CANBERRA" };
            Set(5, $"DSN {dsn[(utc.Hour / 8) % 3]}");
        }

        private string Met()
        {
            if (launchTime < 0f || manager == null) return "000:00:00";
            float days;
            if (orbit != null && orbit.ShowsFreeReturn) days = Mathf.Max(0f, orbit.CurrentDay - (orbit.Solution != null ? orbit.Solution.launchDays : 0f));
            else days = (runner != null && runner.Current != null ? runner.Current.plannedOperationsDays : 1f) * Mathf.Clamp01((Time.time - launchTime) / 25f);
            var t = TimeSpan.FromDays(days);
            return $"{(int)t.TotalDays:000}:{t.Hours:00}:{t.Minutes:00}";
        }

        private string Countdown()
        {
            if (manager == null) return "T- 00:00:00";
            if (manager.Phase == MissionPhase.Design) return "T- 00:10:00 HOLD";
            float s = launchTime > 0f ? Time.time - launchTime : 0f;
            return $"T+ {(int)(s / 60):00}:{(int)(s % 60):00}";
        }

        private void Set(int i, string text)
        {
            if (i < clocks.Length && clocks[i] != null) clocks[i].text = text;
        }
    }
}
