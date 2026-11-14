using System.Collections.Generic;
using System.IO;
using System.Linq;
using MissionDesign.Core;
using MissionDesign.Data;
using MissionDesign.Simulation;
using NUnit.Framework;

namespace MissionDesign.Tests
{
    public class MissionSimulatorTests
    {
        private TestFixtures fx;

        [SetUp] public void SetUp() => fx = new TestFixtures();
        [TearDown] public void TearDown() => fx.Dispose();

        private static SimulationSettings NoEvents(int seed = 1) =>
            new SimulationSettings { seed = seed, allowLaunchFailure = false, forcedEvents = new List<RandomEventSO>() };

        private static SimulationSettings With(params RandomEventSO[] events) =>
            new SimulationSettings { seed = 1, allowLaunchFailure = false, forcedEvents = events.ToList() };

        private SpacecraftDesign Design(int minScience)
        {
            var d = fx.ValidLeoDesign(out _);
            d.SetScenario(fx.Scenario(minScience: minScience));
            return d;
        }

        [Test]
        public void SameSeed_GivesSameMission()
        {
            var pool = new[] { fx.Event("A"), fx.Event("B"), fx.Event("C") };
            var d = Design(40);
            var s1 = new SimulationSettings { seed = 2026 };
            var s2 = new SimulationSettings { seed = 2026 };
            var a = MissionSimulator.Run(d, pool, s1);
            var b = MissionSimulator.Run(d, pool, s2);

            Assert.AreEqual(a.verdict, b.verdict);
            Assert.AreEqual(a.score, b.score);
            Assert.AreEqual(a.scienceAchieved, b.scienceAchieved, 1e-4f);
            CollectionAssert.AreEqual(a.events.Select(e => e.eventId + "@" + e.missionDay), b.events.Select(e => e.eventId + "@" + e.missionDay));
        }

        [Test]
        public void DrawsOneOrTwoEvents_WeightedByOrbit()
        {
            var pool = new[] { fx.Event("A"), fx.Event("B"), fx.Event("C") };
            var d = Design(40);
            for (int seed = 0; seed < 50; seed++)
            {
                var o = MissionSimulator.Run(d, pool, new SimulationSettings { seed = seed, allowLaunchFailure = false });
                Assert.That(o.events.Count, Is.InRange(1, 2), $"seed {seed}");
                Assert.AreEqual(o.events.Count, o.events.Select(e => e.eventId).Distinct().Count(), "sin eventos repetidos");
            }
            Assert.IsEmpty(MissionSimulator.Run(d, new RandomEventSO[0], new SimulationSettings { seed = 3, allowLaunchFailure = false }).events);
        }

        [Test]
        public void BalancedDesign_WithoutEvents_IsFullSuccess()
        {
            var o = MissionSimulator.Run(Design(50), null, NoEvents());

            Assert.AreEqual(MissionVerdict.Success, o.verdict, string.Join("\n", o.explanations));
            Assert.AreEqual(50f, o.scienceAchieved, 0.01f);
            Assert.AreEqual(100, o.score);
            Assert.IsTrue(o.launchSucceeded && o.orbitInsertionSucceeded && o.survivedOperations);
            CollectionAssert.IsSubsetOf(
                new[] { MissionPhase.Launch, MissionPhase.OrbitInsertion, MissionPhase.ScienceOperations, MissionPhase.DataTransmission, MissionPhase.PostMission },
                o.timeline.Select(e => e.phase).Distinct().ToList());
        }

        [Test]
        public void UnreliableRocket_CanLoseTheMissionAtLaunch()
        {
            var d = Design(50);
            d.LaunchVehicle.reliability = 0f;
            var o = MissionSimulator.Run(d, null, new SimulationSettings { seed = 5, allowLaunchFailure = true });

            Assert.AreEqual(MissionVerdict.Failure, o.verdict);
            Assert.IsFalse(o.launchSucceeded);
            Assert.AreEqual(0, o.score);
            Assert.That(o.explanations, Has.Some.Contains("lanzador"));
            Assert.IsFalse(o.timeline.Any(e => e.phase == MissionPhase.ScienceOperations));
        }

        [Test]
        public void DataBeyondDownlink_IsPartialAndExplainsTheNumbers()
        {
            var d = Design(100);
            d.AddInstrument(fx.Instrument("Espectrometro", mass: 100, peakW: 100, dataGB: 540));   // 600 GB/día vs 360
            var o = MissionSimulator.Run(d, null, NoEvents());

            Assert.AreEqual(MissionVerdict.PartialSuccess, o.verdict);
            Assert.AreEqual(0.6f, o.dataReturnFraction, 0.001f);
            Assert.AreEqual(60f, o.scienceAchieved, 0.01f);
            var why = o.explanations.First(e => e.StartsWith("Fallo parcial"));
            StringAssert.Contains("600 GB/día", why.Replace(",", ""));
            StringAssert.Contains("solo transmitió 360 GB/día", why);
            StringAssert.Contains("Espectrometro", why);
        }

        [Test]
        public void SolarStorm_WithTinyBattery_LosesSpacecraftInSafeMode()
        {
            var d = Design(50);
            d.SetBattery(fx.Battery(wh: 10));
            var storm = fx.Event("Tormenta", safeMode: true, safeHours: 24, science: 0.85f);
            var o = MissionSimulator.Run(d, null, With(storm));

            Assert.AreEqual(MissionVerdict.Failure, o.verdict);
            Assert.IsFalse(o.survivedOperations);
            Assert.Less(o.achievedOperationsDays, o.plannedOperationsDays);
            Assert.That(o.explanations, Has.Some.Contains("modo seguro"));
        }

        [Test]
        public void SolarStorm_WithAdequateBattery_IsSurvived()
        {
            var storm = fx.Event("Tormenta", safeMode: true, safeHours: 24, science: 0.85f);
            var o = MissionSimulator.Run(Design(40), null, With(storm));

            Assert.IsTrue(o.survivedOperations);
            Assert.AreEqual(50f * 0.85f, o.scienceAchieved, 0.01f);
            StringAssert.Contains("superado", o.events.Single().impact);
        }

        [Test]
        public void OptimalWindow_BoostsScience()
        {
            var window = fx.Event("Ventana", science: 1.25f, data: 1.2f, positive: true);
            var baseline = MissionSimulator.Run(Design(50), null, NoEvents());
            var boosted = MissionSimulator.Run(Design(50), null, With(window));

            Assert.AreEqual(baseline.scienceAchieved * 1.25f, boosted.scienceAchieved, 0.01f);
            Assert.That(boosted.explanations, Has.Some.StartsWith("A favor"));
        }

        [Test]
        public void Micrometeoroid_CanDisableAnInstrument()
        {
            var hit = fx.Event("Impacto", failureChance: 1f);
            var o = MissionSimulator.Run(Design(50), null, With(hit));

            Assert.That(o.explanations, Has.Some.Contains("fuera de servicio por Impacto"));
            Assert.That(o.explanations, Has.Some.StartsWith("En contra: Impacto"));
            Assert.Less(o.scienceAchieved, 50f);
        }

        [Test]
        public void Exporter_WritesJsonAndTxt()
        {
            var o = MissionSimulator.Run(Design(50), null, NoEvents(seed: 77));
            string folder = Path.Combine(Path.GetTempPath(), "mission-report-test-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                string json = MissionReportExporter.Export(o, folder);
                Assert.IsTrue(File.Exists(json));
                Assert.IsTrue(File.Exists(Path.ChangeExtension(json, ".txt")));
                StringAssert.Contains("\"verdict\"", File.ReadAllText(json));
                StringAssert.Contains("POR QUÉ", File.ReadAllText(Path.ChangeExtension(json, ".txt")));
                StringAssert.Contains("seed77", Path.GetFileName(json));
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }
    }
}
