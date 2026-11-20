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
    /// Genera los diseños de ejemplo (StreamingAssets/Demos) que se cargan desde la ventana «Diseños»:
    /// · Artemis II: 1) éxito al 100 % con fotos · 2) éxito parcial (~80 %) · 3) falla por batería (la demo de la presentación).
    /// · Cada escenario y dificultad: un diseño ganador y, en Básico, un error típico (enlace saturado, instrumento fuera
    ///   del objetivo…), para ver de forma ordenada qué pasa en cada caso.
    /// Para cada uno busca, con el catálogo y el simulador reales (mismas reglas que el juego), una semilla que dé ese
    /// resultado y la guarda en el diseño: así sale igual cada vez que se carga.
    /// </summary>
    public static class DemoDesigns
    {
        public const string Folder = "Assets/StreamingAssets/Demos";
        private const string DatabasePath = "Assets/_Project/Data/Generated/NasaDatabase.asset";

        private sealed class Candidate
        {
            public string file, name, nameEn, note, noteEn;
            public int order;
            public DifficultyLevel difficulty = DifficultyLevel.Basic;
            public string scenario, orbit, vehicle, network;
            public string[] components, instruments;
            public System.Func<MissionOutcomeReport, bool> wanted;
            public System.Func<MissionOutcomeReport, float> rank = o => -o.score;   // menor = mejor
        }

        private static readonly System.Func<MissionOutcomeReport, bool> Wins = o => o.verdict == MissionVerdict.Success;
        private static readonly System.Func<MissionOutcomeReport, bool> Falls = o => o.launchSucceeded && o.verdict != MissionVerdict.Success;

        [MenuItem("NASA SpaceApps/Generate Demo Designs")]
        public static void Generate()
        {
            var db = AssetDatabase.LoadAssetAtPath<NasaDatabaseSO>(DatabasePath);
            if (db == null) { Debug.LogError("[Demos] Falta la base de datos."); return; }
            Directory.CreateDirectory(Folder);
            foreach (var group in Groups()) Solve(db, group);
            WriteBundle();
            AssetDatabase.Refresh();
        }

        public const string BundlePath = "Assets/Resources/" + DesignLibrary.DemoBundleResource + ".json";

        /// <summary>
        /// Empaqueta los JSON de StreamingAssets/Demos en Resources para la versión web, donde StreamingAssets no se
        /// puede leer como carpeta. Lo llaman Generate y las compilaciones.
        /// </summary>
        public static void WriteBundle()
        {
            var bundle = new DesignLibrary.DemoBundle();
            foreach (var file in Directory.GetFiles(Folder, "*.json").OrderBy(f => f))
            {
                bundle.files.Add(Path.GetFileName(file));
                bundle.json.Add(File.ReadAllText(file).Replace("\r\n", "\n"));   // igual con o sin CRLF de git
            }
            string text = JsonUtility.ToJson(bundle, true);
            if (File.Exists(BundlePath) && File.ReadAllText(BundlePath) == text) return;
            Directory.CreateDirectory(Path.GetDirectoryName(BundlePath));
            File.WriteAllText(BundlePath, text);
            AssetDatabase.ImportAsset(BundlePath);
            Debug.Log($"[Demos] Paquete para la web: {bundle.json.Count} diseños → {BundlePath}");
        }

        private static List<Candidate[]> Groups()
        {
            string[] orion = { "BUS_ORION", "SOL_ESM", "BAT_2000", "ANT_O2O", "CDH_ORION" };
            var groups = new List<Candidate[]>();

            // ── Artemis II (las 3 demos de la presentación) ──
            groups.Add(new[]
            {
                new Candidate
                {
                    file = "demo1_artemis_exito", order = 0,
                    name = "1 · Artemis II con fotos (éxito 100 %)", nameEn = "1 · Artemis II with photos (100% success)",
                    note = "Éxito total: 32 cámaras de Orion + cámaras de la tripulación, enlace láser y fotos de la Luna en el informe.",
                    noteEn = "Full success: Orion's 32 cameras + the crew cameras, laser link and Moon photos in the report.",
                    scenario = "SCN_ARTEMIS_FLYBY", orbit = "ORB_FREE_RETURN", vehicle = "LV_SLS_B1", network = "GND_OPTICAL",
                    components = orion, instruments = new[] { "INS_ORION_CAMERAS", "INS_CREW_CAMERAS" },
                    wanted = o => o.verdict == MissionVerdict.Success && o.photoShotsAvailable == o.photoShots && o.photoShots > 0
                }
            });
            Candidate Partial(params string[] instruments) => new Candidate
            {
                file = "demo2_artemis_parcial", order = 1,
                name = "2 · Sobrevuelo económico (parcial ~75 %)", nameEn = "2 · Budget flyby (partial ~75%)",
                note = "Menos cámaras y menos ciencia: se cumple solo una parte del objetivo. Compáralo con la demo 1.",
                noteEn = "Fewer cameras and less science: only part of the goal is met. Compare it with demo 1.",
                scenario = "SCN_ARTEMIS_FLYBY", orbit = "ORB_FREE_RETURN", vehicle = "LV_SLS_B1", network = "GND_OPTICAL",
                components = orion, instruments = instruments,
                wanted = o => o.verdict == MissionVerdict.PartialSuccess && o.photoShots > 0,
                rank = o => Mathf.Abs(Fraction(o) - 0.8f)
            };
            groups.Add(new[]
            {
                Partial("INS_CREW_CAMERAS", "INS_EXPLORER_CAMS"), Partial("INS_CREW_CAMERAS", "INS_ATENEA"),
                Partial("INS_EXPLORER_CAMS", "INS_ATENEA"), Partial("INS_CREW_CAMERAS"), Partial("INS_ORION_CAMERAS"),
            });
            Candidate Failure(string battery) => new Candidate
            {
                file = "demo3_artemis_falla", order = 2,
                name = "3 · Batería insuficiente (falla)", nameEn = "3 · Undersized battery (failure)",
                note = "La batería no aguanta un evento: la cápsula no vuelve y solo quedan las fotos transmitidas. Lección: margen de energía.",
                noteEn = "The battery cannot ride out an event: the capsule does not return and only the transmitted photos remain. Lesson: power margin.",
                scenario = "SCN_ARTEMIS_FLYBY", orbit = "ORB_FREE_RETURN", vehicle = "LV_SLS_B1", network = "GND_OPTICAL",
                components = new[] { "BUS_ORION", "SOL_ESM", battery, "ANT_O2O", "CDH_ORION" },
                instruments = new[] { "INS_ORION_CAMERAS", "INS_CREW_CAMERAS" },
                wanted = o => o.verdict == MissionVerdict.Failure && o.launchSucceeded,
                rank = o => o.photoShotsAvailable > 0 ? 0f : 1f
            };
            groups.Add(new[] { Failure("BAT_CUBESAT"), Failure("BAT_500") });

            // Artemis II en Avanzado: la misma nave con control de orientación.
            groups.Add(Variants("ex_artemis_avanzado_ganador", 0, DifficultyLevel.Advanced,
                "Artemis II (avanzado): ganador", "Artemis II (advanced): winner",
                "Orion con su computadora y ruedas de reacción: el apuntamiento de las cámaras no recorta ciencia. Hay 1-2 eventos y el SLS puede fallar.",
                "Orion with its computer and reaction wheels: camera pointing costs no science. There are 1-2 events and the SLS can fail.",
                "SCN_ARTEMIS_FLYBY", "ORB_FREE_RETURN", "LV_SLS_B1", "GND_OPTICAL",
                new[] { new[] { "BUS_ORION", "SOL_ESM", "BAT_2000", "ANT_O2O", "CDH_ORION", "ADCS_RW_SUN" },
                        new[] { "BUS_ORION", "SOL_ESM", "BAT_8000", "ANT_O2O", "CDH_ORION", "ADCS_RW_STAR" } },
                new[] { new[] { "INS_ORION_CAMERAS", "INS_CREW_CAMERAS" } }, Wins));

            // ── Vigilancia climática (LEO) ──
            string[] climateInstr = { "INS_MODIS", "INS_OCO2" };
            groups.Add(Variants("ex_clima_basico_ganador", 0, DifficultyLevel.Basic,
                "Clima (básico): ganador", "Climate (basic): winner",
                "MODIS + OCO-2, los dos instrumentos del objetivo clima, en LEO heliosíncrona con banda X: toda la ciencia llega a tierra y sobra presupuesto.",
                "MODIS + OCO-2, both climate-goal instruments, in sun-synchronous LEO with X band: all the science reaches the ground with budget to spare.",
                "SCN_EARTH_CLIMATE", "ORB_LEO_SSO", "LV_FALCON9", "GND_NSN",
                new[] { new[] { "BUS_MEDIUM", "SOL_WINGS", "BAT_2000", "ANT_X" }, new[] { "BUS_MEDIUM", "SOL_ROSA", "BAT_2000", "ANT_KA" } },
                new[] { climateInstr }, Wins));
            groups.Add(Variants("ex_clima_avanzado_ganador", 0, DifficultyLevel.Advanced,
                "Clima (avanzado): ganador", "Climate (advanced): winner",
                "Igual que en Básico, más un grabador de 4 TB (aguanta cortes de enlace) y rastreador estelar para el apuntamiento fino.",
                "Same as Basic, plus a 4 TB recorder (rides out link outages) and a star tracker for fine pointing.",
                "SCN_EARTH_CLIMATE", "ORB_LEO_SSO", "LV_FALCON9", "GND_NSN",
                new[] { new[] { "BUS_MEDIUM", "SOL_WINGS", "BAT_2000", "ANT_X", "CDH_SSR", "ADCS_RW_STAR" },
                        new[] { "BUS_MEDIUM", "SOL_ROSA", "BAT_2000", "ANT_KA", "CDH_RAD", "ADCS_RW_STAR" } },
                new[] { climateInstr }, Wins));
            groups.Add(Variants("ex_clima_basico_error", 1, DifficultyLevel.Basic,
                "Clima (básico): error de enlace", "Climate (basic): downlink mistake",
                "Los mismos instrumentos con una antena de banda S: generan más datos de los que se pueden bajar y se pierde ciencia. Compáralo con el ganador.",
                "The same instruments with an S-band antenna: they generate more data than can be downlinked and science is lost. Compare it with the winner.",
                "SCN_EARTH_CLIMATE", "ORB_LEO_SSO", "LV_FALCON9", "GND_NSN",
                new[] { new[] { "BUS_MEDIUM", "SOL_WINGS", "BAT_2000", "ANT_S" }, new[] { "BUS_MEDIUM", "SOL_WINGS", "BAT_2000", "ANT_UHF" } },
                new[] { climateInstr }, Falls));

            // ── Centinela meteorológico (GEO) ──
            groups.Add(Variants("ex_geo_basico_ganador", 0, DifficultyLevel.Basic,
                "Meteorología GEO (básico): ganador", "GEO weather (basic): winner",
                "El generador de imágenes ABI de GOES-R en GEO con banda Ka: 713 GB/día de huracanes en vivo, contacto 24 h con tierra.",
                "GOES-R's ABI imager in GEO with Ka band: 713 GB/day of live hurricane imagery, 24 h contact with the ground.",
                "SCN_GEO_WEATHER", "ORB_GEO", "LV_FALCON9", "GND_COMMERCIAL",
                new[] { new[] { "BUS_LARGE", "SOL_ROSA", "BAT_2000", "ANT_KA" }, new[] { "BUS_LARGE", "SOL_WINGS", "BAT_2000", "ANT_KA" } },
                new[] { new[] { "INS_ABI" } }, Wins));
            groups.Add(Variants("ex_geo_avanzado_ganador", 0, DifficultyLevel.Advanced,
                "Meteorología GEO (avanzado): ganador", "GEO weather (advanced): winner",
                "ABI con computadora endurecida con grabador de 4 TB y rastreador estelar: soporta la radiación y los cortes de enlace.",
                "ABI with a radiation-hardened computer, a 4 TB recorder and a star tracker: it withstands radiation and link outages.",
                "SCN_GEO_WEATHER", "ORB_GEO", "LV_FALCON9", "GND_COMMERCIAL",
                new[] { new[] { "BUS_LARGE", "SOL_ROSA", "BAT_2000", "ANT_KA", "CDH_SSR", "ADCS_RW_STAR" },
                        new[] { "BUS_LARGE", "SOL_ROSA", "BAT_8000", "ANT_KA", "CDH_RAD", "ADCS_RW_STAR" } },
                new[] { new[] { "INS_ABI" } }, Wins));
            groups.Add(Variants("ex_geo_basico_error", 1, DifficultyLevel.Basic,
                "Meteorología GEO (básico): error de antena", "GEO weather (basic): antenna mistake",
                "ABI con antena de banda X: a 36 000 km la señal es ~360 veces más débil que en LEO y casi toda la ciencia se pierde (ley 1/r²).",
                "ABI with an X-band antenna: at 36,000 km the signal is ~360 times weaker than in LEO and most of the science is lost (1/r² law).",
                "SCN_GEO_WEATHER", "ORB_GEO", "LV_FALCON9", "GND_COMMERCIAL",
                new[] { new[] { "BUS_LARGE", "SOL_ROSA", "BAT_2000", "ANT_X" }, new[] { "BUS_LARGE", "SOL_ROSA", "BAT_2000", "ANT_S" } },
                new[] { new[] { "INS_ABI" } }, Falls));

            // ── Reconocimiento lunar ──
            groups.Add(Variants("ex_luna_basico_ganador", 0, DifficultyLevel.Basic,
                "Reconocimiento lunar (básico): ganador", "Lunar recon (basic): winner",
                "La cámara LROC en órbita lunar polar, antena Ka y la DSN: cartografía las zonas de alunizaje del polo sur.",
                "The LROC camera in a polar lunar orbit, Ka antenna and the DSN: it maps the south-pole landing zones.",
                "SCN_LUNAR_RECON", "ORB_LUNAR", "LV_ARIANE64", "GND_DSN",
                new[] { new[] { "BUS_MEDIUM", "SOL_WINGS", "BAT_2000", "ANT_KA" }, new[] { "BUS_LARGE", "SOL_WINGS", "BAT_2000", "ANT_KA" } },
                new[] { new[] { "INS_LROC" }, new[] { "INS_LROC", "INS_EXPLORER_CAMS" } }, Wins));
            groups.Add(Variants("ex_luna_avanzado_ganador", 0, DifficultyLevel.Advanced,
                "Reconocimiento lunar (avanzado): ganador", "Lunar recon (advanced): winner",
                "LROC necesita apuntamiento fino: computadora endurecida (sin magnetosfera hay más radiación) y rastreador estelar.",
                "LROC needs fine pointing: a radiation-hardened computer (no magnetosphere means more radiation) and a star tracker.",
                "SCN_LUNAR_RECON", "ORB_LUNAR", "LV_ARIANE64", "GND_DSN",
                new[] { new[] { "BUS_MEDIUM", "SOL_WINGS", "BAT_2000", "ANT_KA", "CDH_RAD", "ADCS_RW_STAR" },
                        new[] { "BUS_LARGE", "SOL_WINGS", "BAT_2000", "ANT_KA", "CDH_SSR", "ADCS_RW_STAR" } },
                new[] { new[] { "INS_LROC" }, new[] { "INS_LROC", "INS_EXPLORER_CAMS" } }, Wins));
            groups.Add(Variants("ex_luna_basico_error", 1, DifficultyLevel.Basic,
                "Reconocimiento lunar (básico): instrumento equivocado", "Lunar recon (basic): wrong instrument",
                "MODIS estudia el clima terrestre: en la Luna está fuera de su órbita y fuera del objetivo, y aporta muy poca ciencia.",
                "MODIS studies Earth's climate: at the Moon it is outside its orbit and off the goal, and gives very little science.",
                "SCN_LUNAR_RECON", "ORB_LUNAR", "LV_ARIANE64", "GND_DSN",
                new[] { new[] { "BUS_MEDIUM", "SOL_WINGS", "BAT_2000", "ANT_KA" }, new[] { "BUS_LARGE", "SOL_WINGS", "BAT_2000", "ANT_KA" } },
                new[] { new[] { "INS_MODIS" }, new[] { "INS_OCO2" } }, Falls));

            // ── CubeSat universitario ──
            groups.Add(Variants("ex_cubesat_basico_ganador", 0, DifficultyLevel.Basic,
                "CubeSat (básico): ganador", "CubeSat (basic): winner",
                "CubeSat 12U con el espectrómetro solar MinXSS en un lanzamiento compartido: menos de $9 M, como la misión real MinXSS-1.",
                "A 12U CubeSat with the MinXSS solar spectrometer on a rideshare launch: under $9 M, like the real MinXSS-1 mission.",
                "SCN_CUBESAT_EDU", "ORB_LEO_SSO", "LV_RIDESHARE", "GND_UNIVERSITY",
                new[] { new[] { "BUS_CUBESAT_12U", "SOL_CUBESAT", "BAT_CUBESAT", "ANT_S_CUBESAT" },
                        new[] { "BUS_CUBESAT_12U", "SOL_CUBESAT", "BAT_500", "ANT_S_CUBESAT" },
                        new[] { "BUS_CUBESAT_6U", "SOL_CUBESAT", "BAT_CUBESAT", "ANT_S_CUBESAT" } },
                new[] { new[] { "INS_MINXSS" }, new[] { "INS_MINXSS", "INS_CUBECAM" } }, Wins, "GND_NSN"));
            groups.Add(Variants("ex_cubesat_avanzado_ganador", 0, DifficultyLevel.Advanced,
                "CubeSat (avanzado): ganador", "CubeSat (advanced): winner",
                "Con computadora comercial y magnetopares baratos: cabe en el presupuesto, aunque el apuntamiento es menos preciso.",
                "With a commercial computer and cheap magnetorquers: it fits the budget, although pointing is less precise.",
                "SCN_CUBESAT_EDU", "ORB_LEO_SSO", "LV_RIDESHARE", "GND_UNIVERSITY",
                new[] { new[] { "BUS_CUBESAT_12U", "SOL_CUBESAT", "BAT_CUBESAT", "ANT_S_CUBESAT", "CDH_COTS", "ADCS_MAG" },
                        new[] { "BUS_CUBESAT_12U", "SOL_CUBESAT", "BAT_500", "ANT_S_CUBESAT", "CDH_COTS", "ADCS_MAG" } },
                new[] { new[] { "INS_MINXSS", "INS_CUBECAM" }, new[] { "INS_MINXSS" } }, Wins, "GND_NSN"));
            groups.Add(Variants("ex_cubesat_basico_error", 1, DifficultyLevel.Basic,
                "CubeSat (básico): fuera del objetivo", "CubeSat (basic): off the goal",
                "Solo la cámara multiespectral, que fotografía la Tierra: el objetivo del escenario es el Sol, así que vale la mitad y no alcanza.",
                "Only the multispectral camera, which images the Earth: the scenario goal is the Sun, so it counts half and falls short.",
                "SCN_CUBESAT_EDU", "ORB_LEO_SSO", "LV_RIDESHARE", "GND_UNIVERSITY",
                new[] { new[] { "BUS_CUBESAT_12U", "SOL_CUBESAT", "BAT_CUBESAT", "ANT_S_CUBESAT" },
                        new[] { "BUS_CUBESAT_6U", "SOL_CUBESAT", "BAT_CUBESAT", "ANT_S_CUBESAT" } },
                new[] { new[] { "INS_CUBECAM" } }, Falls, "GND_NSN"));

            // ── Sandbox ──
            groups.Add(Variants("ex_sandbox_basico", 0, DifficultyLevel.Basic,
                "Sandbox (básico): radar NISAR", "Sandbox (basic): NISAR radar",
                "Sin límites: el radar NISAR (3 250 GB/día) necesita bus pesado, mucha energía y banda Ka. Prueba a cambiar la antena y mira qué pasa.",
                "No limits: the NISAR radar (3,250 GB/day) needs a heavy bus, lots of power and Ka band. Try changing the antenna and see what happens.",
                "SCN_SANDBOX", "ORB_LEO_SSO", "LV_FALCON9", "GND_COMMERCIAL",
                new[] { new[] { "BUS_LARGE", "SOL_ROSA", "BAT_8000", "ANT_KA" }, new[] { "BUS_LARGE", "SOL_ROSA", "BAT_2000", "ANT_KA" } },
                new[] { new[] { "INS_NISAR" }, new[] { "INS_GEDI", "INS_MODIS" } }, Wins));
            groups.Add(Variants("ex_sandbox_avanzado", 0, DifficultyLevel.Advanced,
                "Sandbox (avanzado): radar NISAR", "Sandbox (advanced): NISAR radar",
                "El mismo radar con grabador de 4 TB y rastreador estelar: en Avanzado el cohete puede fallar y hay 1-2 eventos.",
                "The same radar with a 4 TB recorder and a star tracker: in Advanced the rocket can fail and there are 1-2 events.",
                "SCN_SANDBOX", "ORB_LEO_SSO", "LV_FALCON9", "GND_COMMERCIAL",
                new[] { new[] { "BUS_LARGE", "SOL_ROSA", "BAT_8000", "ANT_KA", "CDH_SSR", "ADCS_RW_STAR" },
                        new[] { "BUS_LARGE", "SOL_ROSA", "BAT_2000", "ANT_KA", "CDH_SSR", "ADCS_RW_STAR" } },
                new[] { new[] { "INS_NISAR" }, new[] { "INS_GEDI", "INS_MODIS" } }, Wins));
            return groups;
        }

        /// <summary>Combina variantes de piezas e instrumentos (y una red alternativa) en candidatos del mismo ejemplo.</summary>
        private static Candidate[] Variants(string file, int order, DifficultyLevel difficulty, string name, string nameEn, string note, string noteEn,
            string scenario, string orbit, string vehicle, string network, string[][] components, string[][] instruments,
            System.Func<MissionOutcomeReport, bool> wanted, string altNetwork = null)
        {
            var list = new List<Candidate>();
            foreach (var net in altNetwork != null ? new[] { network, altNetwork } : new[] { network })
            foreach (var c in components)
            foreach (var i in instruments)
                list.Add(new Candidate
                {
                    file = file, order = order, difficulty = difficulty, name = name, nameEn = nameEn, note = note, noteEn = noteEn,
                    scenario = scenario, orbit = orbit, vehicle = vehicle, network = net, components = c, instruments = i, wanted = wanted,
                    rank = wanted == Falls ? (o => o.verdict == MissionVerdict.PartialSuccess ? 0f : 1f) : (System.Func<MissionOutcomeReport, float>)(o => -o.score)
                });
            return list.ToArray();
        }

        private static void Solve(NasaDatabaseSO db, Candidate[] group)
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
                if (!validation.canLaunch) { Debug.LogWarning($"[Demos] {c.name} ({string.Join("+", c.components)}) bloqueado:\n{validation.ToConsoleString()}"); continue; }
                bool advanced = c.difficulty == DifficultyLevel.Advanced;
                for (int seed = 0; seed < 400; seed++)
                {
                    // Mismas reglas que MissionSimulationRunner: en Avanzado el cohete puede fallar y hay 1-2 eventos.
                    var o = MissionSimulator.Run(design, db.randomEvents, new SimulationSettings
                    {
                        seed = seed, allowLaunchFailure = advanced, minEvents = 1, maxEvents = advanced ? 2 : 1, realMissions = db.realMissions
                    });
                    if (!c.wanted(o)) continue;
                    if (bestO == null || c.rank(o) < bestC.rank(bestO) - 1e-4f) { bestC = c; bestO = o; bestSeed = seed; }
                    if (c.rank(o) <= 0.02f || (c.wanted == Wins && o.score >= 98)) break;
                }
            }
            if (bestC == null) { Debug.LogError($"[Demos] No se encontró una semilla para «{group[0].name}»."); return; }
            var final = Snapshot(bestC);
            final.demoSeed = bestSeed;
            File.WriteAllText(Path.Combine(Folder, bestC.file + ".json"), final.ToJson());
            Debug.Log($"[Demos] {bestC.name}: semilla {bestSeed} → {bestO.verdict} · ciencia {Fraction(bestO):P0} · {bestO.score}/100 · " +
                      $"{string.Join("+", bestC.components)} · {string.Join("+", bestC.instruments)} · {bestC.network}");
        }

        private static float Fraction(MissionOutcomeReport o) => o.scienceTarget > 0f ? o.scienceAchieved / o.scienceTarget : 0f;

        private static DesignSnapshot Snapshot(Candidate c) => new DesignSnapshot
        {
            designName = c.name,
            designNameEn = c.nameEn,
            teamName = "Demo",
            savedAtUtc = "demo",
            difficulty = c.difficulty,
            scenarioId = c.scenario,
            orbitId = c.orbit,
            launchVehicleId = c.vehicle,
            groundNetworkId = c.network,
            componentIds = c.components.ToList(),
            instrumentIds = c.instruments.ToList(),
            demoNote = c.note,
            demoNoteEn = c.noteEn,
            demoOrder = c.order
        };
    }
}
