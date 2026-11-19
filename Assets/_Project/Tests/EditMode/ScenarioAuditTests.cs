using System.Collections.Generic;
using System.Linq;
using System.Text;
using MissionDesign.Core;
using MissionDesign.Data;
using MissionDesign.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MissionDesign.Tests
{
    /// <summary>
    /// Auditoría de los 6 escenarios con el catálogo real. Para cada escenario y dificultad, una búsqueda local
    /// (reinicios al azar + cambiar una pieza a la vez quedándose con lo que mejora) busca el mejor diseño lanzable,
    /// sin eventos ni fallo del cohete. Exige que cada escenario no sandbox tenga al menos un diseño con ÉXITO.
    /// El informe completo queda en el log y en Temp/scenario_audit.txt.
    /// </summary>
    public class ScenarioAuditTests
    {
        private const string DatabasePath = "Assets/_Project/Data/Generated/NasaDatabase.asset";
        private const int Restarts = 40;
        private const int Steps = 250;

        private sealed class Genome
        {
            public int orbit, lv, bus, solar, bat, ant, net, cdh, adcs;
            public List<int> ins = new List<int>();
            public Genome Copy() => new Genome
            {
                orbit = orbit, lv = lv, bus = bus, solar = solar, bat = bat, ant = ant, net = net, cdh = cdh, adcs = adcs,
                ins = new List<int>(ins)
            };
        }

        private sealed class Result
        {
            public bool launchable;
            public float fitness;
            public MissionOutcomeReport outcome;
            public LaunchValidationResult validation;
        }

        private sealed class Stats
        {
            public int evaluated, launchable, success, partial;
            public MissionOutcomeReport best;
            public string bestDesign;
            public readonly Dictionary<string, int> blockers = new Dictionary<string, int>();
        }

        private sealed class Context
        {
            public NasaDatabaseSO db;
            public MissionScenarioSO scn;
            public DifficultyLevel difficulty;
            public List<OrbitSO> orbits;
            public Stats st = new Stats();
            public int seed;
        }

        private static SpacecraftDesign Build(Context c, Genome g)
        {
            var db = c.db;
            var d = new SpacecraftDesign();
            d.SetDifficulty(c.difficulty);
            d.SetScenario(c.scn);
            d.SetOrbit(c.orbits[g.orbit]);
            d.SetLaunchVehicle(db.launchVehicles[g.lv]);
            d.SetBus(db.buses[g.bus]);
            d.SetSolarArray(db.solarArrays[g.solar]);
            d.SetBattery(db.batteries[g.bat]);
            d.SetAntenna(db.antennas[g.ant]);
            d.SetGroundNetwork(db.groundNetworks[g.net]);
            if (c.difficulty == DifficultyLevel.Advanced)
            {
                d.SetComputer(db.computers[g.cdh]);
                d.SetAttitudeControl(db.attitudeControls[g.adcs]);
            }
            foreach (int i in g.ins.Distinct()) d.AddInstrument(db.instruments[i]);
            return d;
        }

        private static Result Evaluate(Context c, Genome g)
        {
            var d = Build(c, g);
            var tel = MissionTelemetry.Calculate(d);
            var v = OrbitAndLaunchValidator.Validate(d, tel);
            c.st.evaluated++;
            var r = new Result { validation = v };
            if (!v.canLaunch)
            {
                foreach (var b in v.Blocking)
                {
                    c.st.blockers.TryGetValue(b.title, out int n);
                    c.st.blockers[b.title] = n + 1;
                }
                // Guía hacia la zona lanzable: menos bloqueos y, a igualdad, menos exceso de presupuesto.
                float overBudget = tel.budgetLimitMUSD > 0f ? Mathf.Max(0f, tel.totalCostMUSD / tel.budgetLimitMUSD - 1f) : 0f;
                r.fitness = -100f * v.Blocking.Count() - 50f * Mathf.Min(overBudget, 1.8f);
                return r;
            }
            r.launchable = true;
            c.st.launchable++;
            var o = MissionSimulator.Run(d, c.db.randomEvents, new SimulationSettings
            {
                seed = c.seed, allowLaunchFailure = false, forcedEvents = new List<RandomEventSO>()
            });
            r.outcome = o;
            r.fitness = 1000f + o.score + (o.verdict == MissionVerdict.Success ? 500f : o.verdict == MissionVerdict.PartialSuccess ? 200f : 0f);
            if (o.verdict == MissionVerdict.Success) c.st.success++;
            else if (o.verdict == MissionVerdict.PartialSuccess) c.st.partial++;
            if (c.st.best == null || Rank(o) > Rank(c.st.best))
            {
                c.st.best = o;
                c.st.bestDesign = $"{o.orbit} · {o.launchVehicle} · {o.bus} · {o.solarArray} · {o.battery} · {o.antenna} · {o.groundNetwork}" +
                                  $" · {o.computer} · {o.attitudeControl} · [{string.Join(", ", o.instruments)}]";
            }
            return r;
        }

        private static int Rank(MissionOutcomeReport o) =>
            (o.verdict == MissionVerdict.Success ? 2000 : o.verdict == MissionVerdict.PartialSuccess ? 1000 : 0) + o.score;

        private static Genome Random(Context c, System.Random rng)
        {
            var db = c.db;
            var g = new Genome
            {
                orbit = rng.Next(c.orbits.Count), lv = rng.Next(db.launchVehicles.Count), bus = rng.Next(db.buses.Count),
                solar = rng.Next(db.solarArrays.Count), bat = rng.Next(db.batteries.Count), ant = rng.Next(db.antennas.Count),
                net = rng.Next(db.groundNetworks.Count), cdh = rng.Next(db.computers.Count), adcs = rng.Next(db.attitudeControls.Count)
            };
            int n = 1 + rng.Next(3);
            for (int k = 0; k < n; k++) g.ins.Add(rng.Next(db.instruments.Count));
            return g;
        }

        private static Genome Mutate(Context c, Genome g, System.Random rng)
        {
            var db = c.db;
            var m = g.Copy();
            switch (rng.Next(11))
            {
                case 0: m.orbit = rng.Next(c.orbits.Count); break;
                case 1: m.lv = rng.Next(db.launchVehicles.Count); break;
                case 2: m.bus = rng.Next(db.buses.Count); break;
                case 3: m.solar = rng.Next(db.solarArrays.Count); break;
                case 4: m.bat = rng.Next(db.batteries.Count); break;
                case 5: m.ant = rng.Next(db.antennas.Count); break;
                case 6: m.net = rng.Next(db.groundNetworks.Count); break;
                case 7: m.cdh = rng.Next(db.computers.Count); m.adcs = rng.Next(db.attitudeControls.Count); break;
                case 8: if (m.ins.Count < 4) m.ins.Add(rng.Next(db.instruments.Count)); break;
                case 9: if (m.ins.Count > 1) m.ins.RemoveAt(rng.Next(m.ins.Count)); break;
                default: m.ins[rng.Next(m.ins.Count)] = rng.Next(db.instruments.Count); break;
            }
            return m;
        }

        private static List<int> Affordable<T>(IList<T> list, float limit, System.Func<T, float> cost) =>
            Enumerable.Range(0, list.Count).Where(i => list[i] != null && cost(list[i]) <= limit).ToList();

        private static void Exhaustive(Context c)
        {
            var db = c.db;
            float b = c.scn.budgetLimitMUSD;
            var buses = Affordable(db.buses, b, x => x.costMUSD);
            var lvs = Affordable(db.launchVehicles, b, x => x.launchCostMUSD);
            var sols = Affordable(db.solarArrays, b * 0.5f, x => x.costMUSD);
            var bats = Affordable(db.batteries, b * 0.5f, x => x.costMUSD);
            var ants = Affordable(db.antennas, b * 0.5f, x => x.costMUSD);
            var nets = Affordable(db.groundNetworks, b, x => x.costPerMonthMUSD * 12f);
            var cdhs = c.difficulty == DifficultyLevel.Advanced ? Affordable(db.computers, b * 0.5f, x => x.costMUSD) : new List<int> { 0 };
            var adcss = c.difficulty == DifficultyLevel.Advanced ? Affordable(db.attitudeControls, b * 0.5f, x => x.costMUSD) : new List<int> { 0 };
            var ins = Affordable(db.instruments, b * 0.6f, x => x.costMUSD);
            var sets = new List<List<int>>();
            foreach (int i in ins) sets.Add(new List<int> { i });
            foreach (int i in ins) foreach (int j in ins) if (j > i) sets.Add(new List<int> { i, j });
            foreach (int o in Enumerable.Range(0, c.orbits.Count))
            foreach (int bu in buses) foreach (int lv in lvs) foreach (int so in sols) foreach (int ba in bats)
            foreach (int an in ants) foreach (int ne in nets) foreach (int cd in cdhs) foreach (int ad in adcss)
            foreach (var set in sets)
                Evaluate(c, new Genome { orbit = o, bus = bu, lv = lv, solar = so, bat = ba, ant = an, net = ne, cdh = cd, adcs = ad, ins = set });
        }

        private static Stats Audit(NasaDatabaseSO db, MissionScenarioSO scn, DifficultyLevel difficulty, int seed)
        {
            var c = new Context
            {
                db = db, scn = scn, difficulty = difficulty, seed = seed,
                orbits = db.orbits.Where(o => o != null && (scn.isSandbox || scn.allowedOrbits == null || scn.allowedOrbits.Length == 0 ||
                                                            scn.allowedOrbits.Contains(o.regime))).ToList()
            };
            // Presupuestos pequeños (CubeSat): el subconjunto asequible es chico y se recorre entero.
            if (scn.budgetLimitMUSD > 0f && scn.budgetLimitMUSD < 50f)
            {
                Exhaustive(c);
                return c.st;
            }
            var rng = new System.Random(seed);
            for (int r = 0; r < Restarts; r++)
            {
                var g = Random(c, rng);
                var best = Evaluate(c, g);
                for (int s = 0; s < Steps; s++)
                {
                    var m = Mutate(c, g, rng);
                    var e = Evaluate(c, m);
                    if (e.fitness >= best.fitness) { g = m; best = e; }
                }
            }
            return c.st;
        }

        [Test]
        public void InstrumentOffTheScenarioObjective_GivesHalfItsScience()
        {
            var db = AssetDatabase.LoadAssetAtPath<NasaDatabaseSO>(DatabasePath);
            var climate = db.scenarios.First(s => s.scenarioId == "SCN_EARTH_CLIMATE");
            var sandbox = db.scenarios.First(s => s.isSandbox);
            var leo = db.GetOrbit(OrbitRegime.LEO);
            var modis = db.instruments.First(i => i.componentId == "INS_MODIS");
            var lroc = db.instruments.First(i => i.componentId == "INS_LROC");
            Assert.IsTrue(MissionTelemetry.MatchesObjective(modis, climate), "MODIS estudia el clima.");
            Assert.IsFalse(MissionTelemetry.MatchesObjective(lroc, climate), "LROC estudia la Luna, no el clima.");
            Assert.IsTrue(MissionTelemetry.MatchesObjective(lroc, sandbox), "En el Sandbox todo cuenta.");
            float onSandbox = MissionTelemetry.InstrumentScience(lroc, leo, null, sandbox);
            float offTarget = MissionTelemetry.InstrumentScience(lroc, leo, null, climate);
            Assert.AreEqual(onSandbox * MissionTelemetry.OffObjectiveScienceFactor, offTarget, 1e-3f);
        }

        [Test]
        public void EveryScenario_HasALaunchableWinningDesign()
        {
            var db = AssetDatabase.LoadAssetAtPath<NasaDatabaseSO>(DatabasePath);
            Assert.IsNotNull(db, DatabasePath);
            Assert.AreEqual(6, db.scenarios.Count(s => s != null), "Se esperan 6 escenarios.");
            var report = new StringBuilder();
            var failures = new List<string>();
            foreach (var scn in db.scenarios.Where(s => s != null))
            foreach (var diff in new[] { DifficultyLevel.Basic, DifficultyLevel.Advanced })
            {
                var st = Audit(db, scn, diff, 1234);
                report.AppendLine($"== {scn.displayName} [{diff}] · evaluados {st.evaluated} · lanzables {st.launchable} · éxito {st.success} · parcial {st.partial}");
                if (st.best != null)
                    report.AppendLine($"   mejor: {st.best.score} ({st.best.rank}) {st.best.verdict} · ciencia {st.best.scienceAchieved:F0}/{st.best.scienceTarget:F0}" +
                                      $" · costo {st.best.totalCostMUSD:F1}/{st.best.budgetLimitMUSD:F0} · desarrollo {st.best.developmentMonths:F0} m · {st.bestDesign}");
                foreach (var kv in st.blockers.OrderByDescending(k => k.Value).Take(4))
                    report.AppendLine($"   bloqueo: {kv.Key} ×{kv.Value}");
                if (!scn.isSandbox && st.success == 0) failures.Add($"{scn.displayName} [{diff}]");
            }
            Debug.Log("[ScenarioAudit]\n" + report);
            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.temporaryCachePath, "scenario_audit.txt"), report.ToString());
            Assert.IsEmpty(failures, "Escenarios sin ningún diseño ganador:\n" + report);
        }
    }
}
