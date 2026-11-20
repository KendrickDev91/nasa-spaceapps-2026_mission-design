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
        [Tooltip("Diseños de demostración: semilla fija para que el resultado se repita en la presentación (-1 = aleatoria).")]
        public int demoSeed = -1;
        public string demoNote;
        [Tooltip("Ejemplos: nombre y nota en inglés (los guardados por el jugador no los tienen).")]
        public string designNameEn;
        public string demoNoteEn;
        [Tooltip("Ejemplos: orden dentro de su escenario y dificultad (0 = ganador primero, luego los errores típicos).")]
        public int demoOrder;

        public bool IsDemo => demoSeed >= 0;
        public string DisplayName => Loc.L(designName, designNameEn);
        public string DisplayNote => Loc.L(demoNote, demoNoteEn);

        public static DesignSnapshot Capture(SpacecraftDesign d, string name, string team = null) => new DesignSnapshot
        {
            designName = string.IsNullOrWhiteSpace(name) ? Loc.L("Diseño sin nombre", "Untitled design") : name.Trim(),
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

        /// <summary>Carpeta de solo lectura con los diseños de demostración que acompañan al juego.</summary>
        public static string DemoFolder => Path.Combine(Application.streamingAssetsPath, "Demos");

        /// <summary>Paquete de los ejemplos dentro de Resources (lo escribe el editor al generar o compilar).</summary>
        public const string DemoBundleResource = "DemoDesigns";

        [Serializable]
        public sealed class DemoBundle
        {
            public List<string> files = new List<string>();
            public List<string> json = new List<string>();
        }

        private static List<(string, DesignSnapshot)> bundleCache;

        /// <summary>
        /// Diseños de demostración, ordenados por nombre. Se leen de StreamingAssets/Demos; en el navegador (WebGL) esa
        /// carpeta no se puede leer como disco, así que se usa el paquete de Resources.
        /// </summary>
        public static List<(string path, DesignSnapshot snapshot)> Demos()
        {
            var list = List(DemoFolder);
            if (list.Count == 0) list = FromBundle();
            return list.OrderBy(d => d.snapshot.designName).ToList();
        }

        private static List<(string, DesignSnapshot)> FromBundle()
        {
            if (bundleCache != null) return new List<(string, DesignSnapshot)>(bundleCache);
            bundleCache = new List<(string, DesignSnapshot)>();
            var asset = Resources.Load<TextAsset>(DemoBundleResource);
            if (asset == null) return new List<(string, DesignSnapshot)>();
            var bundle = JsonUtility.FromJson<DemoBundle>(asset.text);
            for (int i = 0; bundle != null && i < bundle.json.Count; i++)
            {
                var s = DesignSnapshot.FromJson(bundle.json[i]);
                if (s != null) bundleCache.Add((i < bundle.files.Count ? bundle.files[i] : $"demo{i}", s));
            }
            return new List<(string, DesignSnapshot)>(bundleCache);
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
