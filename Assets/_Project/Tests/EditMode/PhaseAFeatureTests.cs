using System.IO;
using System.Linq;
using MissionDesign.Core;
using MissionDesign.Multiplayer;
using MissionDesign.Simulation;
using NUnit.Framework;

namespace MissionDesign.Tests
{
    /// <summary>Guardar/cargar/comparar diseños, ranking, sandbox, CubeSat y colaboración.</summary>
    public class PhaseAFeatureTests
    {
        private TestFixtures fx;
        private string tempDir;

        [SetUp]
        public void SetUp()
        {
            fx = new TestFixtures();
            tempDir = Path.Combine(Path.GetTempPath(), "mtoff-tests-" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            fx.Dispose();
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }

        [Test]
        public void Snapshot_RoundTripsThroughJson()
        {
            var d = fx.ValidLeoDesign(out _);
            var db = fx.DatabaseFrom(d);
            var json = DesignSnapshot.Capture(d, "Opción A", "Equipo Test").ToJson();

            var copy = new SpacecraftDesign();
            var missing = DesignSnapshot.FromJson(json).ApplyTo(copy, db);

            Assert.IsEmpty(missing);
            var t0 = MissionTelemetry.Calculate(d);
            var t1 = MissionTelemetry.Calculate(copy);
            Assert.AreEqual(t0.totalMassKg, t1.totalMassKg, 0.01f);
            Assert.AreEqual(t0.totalCostMUSD, t1.totalCostMUSD, 0.01f);
            Assert.AreEqual(t0.expectedScienceValue, t1.expectedScienceValue, 0.01f);
            Assert.AreEqual(d.GroundNetwork, copy.GroundNetwork);
        }

        [Test]
        public void Snapshot_ReportsPartsMissingFromCatalog()
        {
            var d = fx.ValidLeoDesign(out _);
            var snap = DesignSnapshot.Capture(d, "x");
            snap.instrumentIds.Add("INS_NO_EXISTE");
            var missing = snap.ApplyTo(new SpacecraftDesign(), fx.DatabaseFrom(d));
            CollectionAssert.Contains(missing, "INS_NO_EXISTE");
        }

        [Test]
        public void Library_SavesAndListsNewestFirst()
        {
            var d = fx.ValidLeoDesign(out _);
            DesignLibrary.Save(DesignSnapshot.Capture(d, "Uno"), tempDir);
            System.Threading.Thread.Sleep(1100);   // los nombres llevan la hora al segundo
            DesignLibrary.Save(DesignSnapshot.Capture(d, "Dos"), tempDir);

            var list = DesignLibrary.List(tempDir);
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual("Dos", list[0].snapshot.designName);
        }

        [Test]
        public void Leaderboard_RanksByScoreWithinScenario()
        {
            string path = Path.Combine(tempDir, "lb.json");
            void Add(string team, string scenario, int score) =>
                Leaderboard.Add(new LeaderboardEntry { team = team, scenarioId = scenario, score = score }, path);
            Add("Lento", "SCN_A", 60);
            Add("Rápido", "SCN_A", 92);
            Add("Otro escenario", "SCN_B", 99);

            var top = Leaderboard.Top("SCN_A", 5, path);
            CollectionAssert.AreEqual(new[] { "Rápido", "Lento" }, top.Select(e => e.team));
        }

        [Test]
        public void Comparison_MarksTheBetterDesignPerIndicator()
        {
            var a = fx.ValidLeoDesign(out _);
            var b = a.Clone();
            b.AddInstrument(fx.Instrument("Pesado", mass: 300, peakW: 50, dataGB: 10, science: 40));

            var rows = DesignComparison.Compare(a, b);
            Assert.AreEqual(-1, rows.Single(r => r.label.StartsWith("Masa")).better, "A es más ligero");
            Assert.AreEqual(1, rows.Single(r => r.label.StartsWith("Ciencia")).better, "B da más ciencia");
            var (winsA, winsB) = DesignComparison.Tally(rows);
            Assert.Greater(winsA + winsB, 0);
        }

        [Test]
        public void Sandbox_HasNoBudgetOrScheduleLimits()
        {
            var d = fx.ValidLeoDesign(out _);
            var scn = fx.Scenario(budget: 1, maxDev: 1);
            scn.isSandbox = true;
            scn.allowedOrbits = new OrbitRegime[0];
            d.SetScenario(scn);

            var t = MissionTelemetry.Calculate(d);
            var v = OrbitAndLaunchValidator.Validate(d, t);
            Assert.AreEqual(0f, t.budgetLimitMUSD);
            Assert.IsTrue(v.canLaunch, v.ToConsoleString());
            Assert.That(v.issues.Select(i => i.title), Has.None.EqualTo("Presupuesto excedido"));
        }

        [Test]
        public void CubeSatBus_ScalesOperationsCost()
        {
            var d = fx.ValidLeoDesign(out _);
            var cube = fx.Bus(dry: 6, prop: 0.5f, maxPayload: 300, maxPower: 400, cost: 1.5f);
            cube.operationsCostScale = 0.05f;
            d.SetBus(cube);

            var t = MissionTelemetry.Calculate(d);
            float expected = 60f * (MissionTelemetry.OperationsCostPerMonth(OrbitRegime.LEO) * 0.05f + 0.1f);
            Assert.AreEqual(expected, t.operationsCostMUSD, 0.01f);
        }

        [Test]
        public void CollaborationLog_ScoresRolesAndResolvedConflicts()
        {
            var log = new CollaborationLog();
            log.RecordChange(MissionRole.MissionDirector);
            log.RecordChange(MissionRole.SystemsEngineer);
            log.RecordChange(MissionRole.PrincipalScientist);

            var conflict = new LaunchValidationResult();
            conflict.issues.Add(new ValidationIssue { severity = IssueSeverity.Blocking, title = "Masa excede la capacidad del lanzador" });
            log.RecordEvaluation(conflict);
            log.RecordEvaluation(new LaunchValidationResult());   // resuelto

            Assert.AreEqual(3, log.RolesContributed);
            Assert.AreEqual(1, log.ConflictsResolved);
            Assert.AreEqual(6, log.Score);                       // 3 × 1.5 + 1 = 5.5 → 6
            StringAssert.Contains("3 de 4", log.Notes);
        }

        [Test]
        public void Collaboration_IsAddedToTheReportCards()
        {
            var o = MissionSimulator.Run(fx.ValidLeoDesign(out _), null,
                new SimulationSettings { seed = 1, allowLaunchFailure = false, forcedEvents = new System.Collections.Generic.List<Data.RandomEventSO>() });
            MissionSimulator.ApplyCollaboration(o, 8, "4 de 4 roles");

            Assert.AreEqual(8, o.collaborationScore);
            Assert.That(o.wentWell, Has.Some.Contains("trabajo en equipo 8/10"));
        }
    }
}
