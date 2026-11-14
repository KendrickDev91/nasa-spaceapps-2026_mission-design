using System.Linq;
using MissionDesign.Core;
using MissionDesign.Data;
using MissionDesign.Simulation;
using UnityEditor;
using UnityEngine;

namespace MissionDesign.Editor
{
    /// <summary>
    /// Ejecuta tres diseños de ejemplo con el catálogo real y muestra en consola los 6 recursos
    /// y el veredicto del validador. Útil para la demo ante el jurado y para calibrar datos.
    /// Batch: -executeMethod MissionDesign.Editor.TelemetryDemoMenu.Run
    /// </summary>
    public static class TelemetryDemoMenu
    {
        private const string DatabasePath = "Assets/_Project/Data/Generated/NasaDatabase.asset";

        [MenuItem("NASA SpaceApps/Run Telemetry Demo (Console)")]
        public static void Run()
        {
            var db = AssetDatabase.LoadAssetAtPath<NasaDatabaseSO>(DatabasePath);
            if (db == null)
            {
                Debug.LogError("[DEMO] No existe la base de datos. Ejecuta primero NASA SpaceApps → Generate Initial NASA Database.");
                return;
            }

            // 1) Observatorio climático equilibrado en LEO
            Print("A · Clima LEO: Falcon 9 + bus mediano + MODIS + OCO-2 + banda X", db,
                "SCN_EARTH_CLIMATE", OrbitRegime.LEO, "LV_FALCON9", "BUS_MEDIUM", "SOL_WINGS", "BAT_2000", "ANT_X",
                "INS_MODIS", "INS_OCO2");

            // 2) El científico pide NISAR pero el lanzador es Electron: conflicto de masa
            Print("B · Conflicto: NISAR sobre Electron", db,
                "SCN_EARTH_CLIMATE", OrbitRegime.LEO, "LV_ELECTRON", "BUS_SMALL", "SOL_BODY", "BAT_500", "ANT_X",
                "INS_NISAR");

            // 3) GEO meteorológico con antena insuficiente: pérdida de ciencia
            Print("C · GEO: ABI con banda X (enlace saturado)", db,
                "SCN_GEO_WEATHER", OrbitRegime.GEO, "LV_ARIANE64", "BUS_LARGE", "SOL_WINGS", "BAT_2000", "ANT_X",
                "INS_ABI");

            // 4) Lunar para Artemis con DSN
            Print("D · Luna: LROC sobre Falcon 9 con banda Ka", db,
                "SCN_LUNAR_RECON", OrbitRegime.Lunar, "LV_FALCON9", "BUS_MEDIUM", "SOL_WINGS", "BAT_2000", "ANT_KA",
                "INS_LROC");

            // 5) El bus pequeño no lleva combustible para frenar en la Luna
            Print("E · Luna con bus pequeño: sin Δv para la inserción", db,
                "SCN_LUNAR_RECON", OrbitRegime.Lunar, "LV_FALCON9", "BUS_SMALL", "SOL_BODY", "BAT_500", "ANT_KA",
                "INS_LROC");
        }

        private static void Print(string title, NasaDatabaseSO db, string scenarioId, OrbitRegime regime, string lvId,
            string busId, string solarId, string batteryId, string antennaId, params string[] instrumentIds)
        {
            var d = new SpacecraftDesign();
            d.SetScenario(db.scenarios.FirstOrDefault(s => s.scenarioId == scenarioId));
            d.SetOrbit(db.GetOrbit(regime));
            d.SetLaunchVehicle(db.launchVehicles.FirstOrDefault(v => v.vehicleId == lvId));
            d.SetBus(db.buses.FirstOrDefault(c => c.componentId == busId));
            d.SetSolarArray(db.solarArrays.FirstOrDefault(c => c.componentId == solarId));
            d.SetBattery(db.batteries.FirstOrDefault(c => c.componentId == batteryId));
            d.SetAntenna(db.antennas.FirstOrDefault(c => c.componentId == antennaId));
            foreach (var id in instrumentIds)
                d.AddInstrument(db.instruments.FirstOrDefault(i => i.componentId == id));

            var telemetry = MissionTelemetry.Calculate(d);
            var validation = OrbitAndLaunchValidator.Validate(d, telemetry);
            Debug.Log($"[DEMO] ===== {title} =====\n{telemetry.ToConsoleString()}{validation.ToConsoleString()}");
        }
    }
}
