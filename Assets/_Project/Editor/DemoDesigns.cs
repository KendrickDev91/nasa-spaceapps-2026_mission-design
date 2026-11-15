using System.Collections.Generic;
using System.IO;
using System.Linq;
using MissionDesign.Core;
using MissionDesign.Data;
using MissionDesign.Simulation;
using UnityEditor;
using UnityEngine;

namespace MissionDesign.Editor
{
    /// <summary>
    /// Genera los diseños de demostración (StreamingAssets/Demos) para la presentación:
    /// 1) éxito al 100 % con fotos de la Luna · 2) éxito parcial (~80 % de la ciencia) · 3) falla por batería insuficiente.
    /// Para cada uno busca, con el catálogo y el simulador reales, una semilla que dé ese resultado y la guarda en el diseño:
    /// así la demo sale igual cada vez que se carga desde la ventana «Diseños».
    /// </summary>
    public static class DemoDesigns
    {
        public const string Folder = "Assets/StreamingAssets/Demos";
        private const string DatabasePath = "Assets/_Project/Data/Generated/NasaDatabase.asset";

        private sealed class Candidate
        {
            public string file, name, note;
            public string scenario, orbit, vehicle, network;
            public string[] components, instruments;
            public System.Func<MissionOutcomeReport, bool> wanted;
            public System.Func<MissionOutcomeReport, float> rank = o => 0f;   // menor = mejor
        }

        [MenuItem("NASA SpaceApps/Generate Demo Designs")]
        public static void Generate()
        {
            var db = AssetDatabase.LoadAssetAtPath<NasaDatabaseSO>(DatabasePath);
            if (db == null) { Debug.LogError("[Demos] Falta la base de datos."); return; }
            Directory.CreateDirectory(Folder);

            string[] orion = { "BUS_ORION", "SOL_ESM", "BAT_2000", "ANT_O2O", "CDH_ORION" };
            var candidates = new List<Candidate[]>
            {
                new[]
                {
                    new Candidate
                    {
                        file = "demo1_artemis_exito", name = "1 · Artemis II con fotos (éxito 100 %)",
                        note = "Éxito total: 32 cámaras de Orion + cámaras de la tripulación, enlace láser y fotos de la Luna en el informe.",
                        scenario = "SCN_ARTEMIS_FLYBY", orbit = "ORB_FREE_RETURN", vehicle = "LV_SLS_B1", network = "GND_OPTICAL",
                        components = orion, instruments = new[] { "INS_ORION_CAMERAS", "INS_CREW_CAMERAS" },
                        wanted = o => o.verdict == MissionVerdict.Success && o.photoShotsAvailable == o.photoShots && o.photoShots > 0,
                        rank = o => -o.score
                    }
                },
                new[]
                {
                    Partial("INS_CREW_CAMERAS", "INS_EXPLORER_CAMS"),
                    Partial("INS_CREW_CAMERAS", "INS_ATENEA"),
                    Partial("INS_EXPLORER_CAMS", "INS_ATENEA"),
                    Partial("INS_CREW_CAMERAS"),
                    Partial("INS_ORION_CAMERAS"),
                },
                new[]
                {
                    Failure("BAT_CUBESAT"), Failure("BAT_500"),
                }
            };

            Candidate Partial(params string[] instruments) => new Candidate
            {
                file = "demo2_artemis_parcial", name = "2 · Sobrevuelo económico (parcial ~80 %)",
                note = "Menos cámaras y menos ciencia: se cumple solo una parte del objetivo. Compáralo con la demo 1.",
                scenario = "SCN_ARTEMIS_FLYBY", orbit = "ORB_FREE_RETURN", vehicle = "LV_SLS_B1", network = "GND_OPTICAL",
                components = orion, instruments = instruments,
                wanted = o => o.verdict == MissionVerdict.PartialSuccess && o.photoShots > 0,
                rank = o => Mathf.Abs(Fraction(o) - 0.8f)
            };

            Candidate Failure(string battery) => new Candidate
            {
                file = "demo3_artemis_falla", name = "3 · Batería insuficiente (falla)",
                note = "La batería no aguanta un evento: la cápsula no vuelve y solo quedan las fotos transmitidas. Lección: margen de energía.",
                scenario = "SCN_ARTEMIS_FLYBY", orbit = "ORB_FREE_RETURN", vehicle = "LV_SLS_B1", network = "GND_OPTICAL",
                components = new[] { "BUS_ORION", "SOL_ESM", battery, "ANT_O2O", "CDH_ORION" },
                instruments = new[] { "INS_ORION_CAMERAS", "INS_CREW_CAMERAS" },
                wanted = o => o.verdict == MissionVerdict.Failure && o.launchSucceeded,
                rank = o => o.photoShotsAvailable > 0 ? 0f : 1f
            };

            foreach (var group in candidates)
            {
                Candidate bestC = null;
                MissionOutcomeReport bestO = null;
                int bestSeed = -1;
                foreach (var c in group)
                {
                    var snap = Snapshot(c);
                    var design = new SpacecraftDesign();
                    var missing = snap.ApplyTo(design, db);
                    if (missing.Count > 0) { Debug.LogWarning($"[Demos] {c.name}: faltan {string.Join(", ", missing)}"); continue; }
                    var validation = OrbitAndLaunchValidator.Validate(design);
                    if (!validation.canLaunch) { Debug.LogWarning($"[Demos] {c.name} bloqueado:\n{validation.ToConsoleString()}"); continue; }
                    for (int seed = 0; seed < 400; seed++)
                    {
                        var o = MissionSimulator.Run(design, db.randomEvents, new SimulationSettings
                        {
                            seed = seed, allowLaunchFailure = false, minEvents = 1, maxEvents = 1, realMissions = db.realMissions
                        });
                        if (!c.wanted(o)) continue;
                        if (bestO == null || c.rank(o) < bestC.rank(bestO) - 1e-4f) { bestC = c; bestO = o; bestSeed = seed; }
                        if (c.rank(o) <= 0.02f) break;
                    }
                }
                if (bestC == null) { Debug.LogError($"[Demos] No se encontró una semilla para «{group[0].name}»."); continue; }
                var final = Snapshot(bestC);
                final.demoSeed = bestSeed;
                File.WriteAllText(Path.Combine(Folder, bestC.file + ".json"), final.ToJson());
                Debug.Log($"[Demos] {bestC.name}: semilla {bestSeed} → {bestO.verdict} · ciencia {Fraction(bestO):P0} · {bestO.score}/100 · " +
                          $"fotos {bestO.photoShotsAvailable}/{bestO.photoShots} · instrumentos {string.Join("+", bestC.instruments)}");
            }
            AssetDatabase.Refresh();
        }

        private static float Fraction(MissionOutcomeReport o) => o.scienceTarget > 0f ? o.scienceAchieved / o.scienceTarget : 0f;

        private static DesignSnapshot Snapshot(Candidate c) => new DesignSnapshot
        {
            designName = c.name,
            teamName = "Demo",
            savedAtUtc = "demo",
            difficulty = DifficultyLevel.Basic,
            scenarioId = c.scenario,
            orbitId = c.orbit,
            launchVehicleId = c.vehicle,
            groundNetworkId = c.network,
            componentIds = c.components.ToList(),
            instrumentIds = c.instruments.ToList(),
            demoNote = c.note
        };
    }
}
