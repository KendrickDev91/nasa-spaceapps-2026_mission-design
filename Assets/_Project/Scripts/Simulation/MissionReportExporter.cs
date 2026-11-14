using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MissionDesign.Simulation
{
    /// <summary>Guarda el reporte final en JSON (para comparar) y TXT (para leer) en persistentDataPath/MissionReports.</summary>
    public static class MissionReportExporter
    {
        public static string ReportsFolder => Path.Combine(Application.persistentDataPath, "MissionReports");

        /// <returns>Ruta del JSON; el TXT queda al lado con el mismo nombre.</returns>
        public static string Export(MissionOutcomeReport report, string folder = null)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            folder = folder ?? ReportsFolder;
            Directory.CreateDirectory(folder);

            string scenario = string.IsNullOrEmpty(report.scenarioId) ? "SIN-ESCENARIO" : report.scenarioId;
            string baseName = $"{DateTime.Now:yyyy-MM-dd_HHmmss}_{Sanitize(scenario)}_{report.verdict}_seed{report.seed}";
            string json = Path.Combine(folder, baseName + ".json");
            File.WriteAllText(json, report.ToJson());
            File.WriteAllText(Path.Combine(folder, baseName + ".txt"), report.ToText());
            return json;
        }

        private static string Sanitize(string s) =>
            new string(s.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == ' ' ? '-' : c).ToArray());
    }
}
