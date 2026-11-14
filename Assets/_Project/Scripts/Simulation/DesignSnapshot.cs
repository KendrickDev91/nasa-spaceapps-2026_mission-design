using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MissionDesign.Core;
using MissionDesign.Data;
using UnityEngine;

namespace MissionDesign.Simulation
{
    /// <summary>
    /// Diseño guardable/compartible: solo ids, nunca referencias a assets. Un JSON de esto se puede
    /// enviar a otro equipo y cargarlo en su juego para competir con el mismo objetivo.
    /// </summary>
    [Serializable]
    public sealed class DesignSnapshot
    {
        public string designName;
        public string teamName;
        public string savedAtUtc;
        public DifficultyLevel difficulty;
        public string scenarioId;
        public string orbitId;
        public string launchVehicleId;
        public string groundNetworkId;
        public List<string> componentIds = new List<string>();   // bus, paneles, batería, antena, computadora, ADCS
        public List<string> instrumentIds = new List<string>();

        public static DesignSnapshot Capture(SpacecraftDesign d, string name, string team = null) => new DesignSnapshot
        {
            designName = string.IsNullOrWhiteSpace(name) ? "Diseño sin nombre" : name.Trim(),
            teamName = team,
            savedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            difficulty = d.Difficulty,
            scenarioId = d.Scenario != null ? d.Scenario.scenarioId : null,
            orbitId = d.Orbit != null ? d.Orbit.orbitId : null,
            launchVehicleId = d.LaunchVehicle != null ? d.LaunchVehicle.vehicleId : null,
            groundNetworkId = d.GroundNetwork != null ? d.GroundNetwork.networkId : null,
            componentIds = d.AllComponents.Where(c => !(c is InstrumentSO)).Select(c => c.componentId).ToList(),
            instrumentIds = d.Instruments.Select(i => i.componentId).ToList()
        };

        /// <summary>Reconstruye el diseño a partir del catálogo. Devuelve los ids que no encontró.</summary>
        public List<string> ApplyTo(SpacecraftDesign d, NasaDatabaseSO db)
        {
            var missing = new List<string>();
            T Find<T>(IEnumerable<T> list, Func<T, string> id, string wanted) where T : class
            {
                if (string.IsNullOrEmpty(wanted)) return null;
                var hit = list.FirstOrDefault(x => x != null && id(x) == wanted);
                if (hit == null) missing.Add(wanted);
                return hit;
            }

            d.SetDifficulty(difficulty);
            d.SetScenario(Find(db.scenarios, s => s.scenarioId, scenarioId));
            d.SetOrbit(Find(db.orbits, o => o.orbitId, orbitId));
            d.SetLaunchVehicle(Find(db.launchVehicles, v => v.vehicleId, launchVehicleId));
            d.SetGroundNetwork(Find(db.groundNetworks, n => n.networkId, groundNetworkId));

            var all = db.AllComponents.ToList();
            d.SetBus(null); d.SetSolarArray(null); d.SetBattery(null); d.SetAntenna(null);
            d.SetComputer(null); d.SetAttitudeControl(null);
            d.ClearInstruments();
            foreach (var id in componentIds)
            {
                var c = Find(all, x => x.componentId, id);
                if (c != null) d.SetComponent(c);
            }
            foreach (var id in instrumentIds)
            {
                if (Find(all, x => x.componentId, id) is InstrumentSO i) d.AddInstrument(i);
            }
            return missing;
        }

        public string ToJson() => JsonUtility.ToJson(this, true);
        public static DesignSnapshot FromJson(string json) => JsonUtility.FromJson<DesignSnapshot>(json);
    }

    /// <summary>Carpeta de diseños guardados (persistentDataPath/Designs).</summary>
    public static class DesignLibrary
    {
        public static string DefaultFolder => Path.Combine(Application.persistentDataPath, "Designs");

        public static string Save(DesignSnapshot snapshot, string folder = null)
        {
            folder = folder ?? DefaultFolder;
            Directory.CreateDirectory(folder);
            string safe = new string(snapshot.designName.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == ' ' ? '-' : c).ToArray());
            string path = Path.Combine(folder, $"{DateTime.Now:yyyyMMdd-HHmmss}_{safe}.json");
            File.WriteAllText(path, snapshot.ToJson());
            return path;
        }

        /// <summary>Diseños guardados, del más reciente al más antiguo.</summary>
        public static List<(string path, DesignSnapshot snapshot)> List(string folder = null)
        {
            folder = folder ?? DefaultFolder;
            var result = new List<(string, DesignSnapshot)>();
            if (!Directory.Exists(folder)) return result;
            foreach (var file in Directory.GetFiles(folder, "*.json").OrderByDescending(f => f))
            {
                try
                {
                    var s = DesignSnapshot.FromJson(File.ReadAllText(file));
                    if (s != null) result.Add((file, s));
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[DesignLibrary] No se pudo leer {file}: {ex.Message}");
                }
            }
            return result;
        }
    }
}
