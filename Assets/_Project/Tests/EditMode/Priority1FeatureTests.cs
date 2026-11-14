using System.Collections.Generic;
using System.Linq;
using MissionDesign.Core;
using MissionDesign.Data;
using MissionDesign.Simulation;
using NUnit.Framework;

namespace MissionDesign.Tests
{
    /// <summary>Dificultad, segmento terreno, subsistemas avanzados, tarjetas post-misión y vista previa.</summary>
    public class Priority1FeatureTests
    {
        private TestFixtures fx;

        [SetUp] public void SetUp() => fx = new TestFixtures();
        [TearDown] public void TearDown() => fx.Dispose();

        private static SimulationSettings With(params RandomEventSO[] events) =>
            new SimulationSettings { seed = 1, allowLaunchFailure = false, forcedEvents = events.ToList() };

        private SpacecraftDesign AdvancedDesign(OnboardComputerSO computer = null, AttitudeControlSO adcs = null)
        {
            var d = fx.ValidLeoDesign(out _);
            d.SetScenario(fx.Scenario(minScience: 40));
            d.SetDifficulty(DifficultyLevel.Advanced);
            d.SetComputer(computer ?? fx.Computer());
            d.SetAttitudeControl(adcs ?? fx.Adcs());
            return d;
        }

        // ───── Dificultad ─────

        [Test]
        public void Basic_DoesNotRequireAdvancedSubsystems_Advanced_Does()
        {
            var d = fx.ValidLeoDesign(out _);
            Assert.IsTrue(OrbitAndLaunchValidator.Validate(d).canLaunch, "Básico debe poder lanzar sin computadora ni ADCS");

            d.SetDifficulty(DifficultyLevel.Advanced);
            var missing = OrbitAndLaunchValidator.Validate(d).Blocking.Select(i => i.explanation).ToList();
            Assert.That(missing, Has.Some.Contains("computadora de a bordo"));
            Assert.That(missing, Has.Some.Contains("control de orientación"));
        }

        [Test]
        public void SwitchingToBasic_RemovesAdvancedOnlyParts()
        {
            var d = AdvancedDesign();
            var removed = new List<SpacecraftComponentSO>();
            d.ComponentRemoved += removed.Add;

            d.SetDifficulty(DifficultyLevel.Basic);

            Assert.IsNull(d.Computer);
            Assert.IsNull(d.AttitudeControl);
            Assert.AreEqual(2, removed.Count, "el ensamblador 3D debe enterarse para quitar sus modelos");
        }

        // ───── Apuntamiento ─────

        [Test]
        public void PoorPointing_ReducesScience_AndWarns()
        {
            var d = AdvancedDesign(adcs: fx.Adcs(accuracyDeg: 2f));
            d.Instruments[0].requiredPointingDeg = 0.01f;           // 200× más exigente que el ADCS
            var t = MissionTelemetry.Calculate(d);

            float expected = 1f / (1f + UnityEngine.Mathf.Log10(200f));   // ≈ 0.30
            Assert.AreEqual(50f * expected, t.potentialScienceValue, 0.05f);
            Assert.That(OrbitAndLaunchValidator.Validate(d).Warnings.Select(w => w.title), Has.Member("Apuntamiento insuficiente"));
        }

        [Test]
        public void GoodPointing_KeepsFullScience()
        {
            var d = AdvancedDesign(adcs: fx.Adcs(accuracyDeg: 0.01f));
            Assert.AreEqual(50f, MissionTelemetry.Calculate(d).potentialScienceValue, 0.01f);
        }

        // ───── Segmento terreno ─────

        [Test]
        public void NetworkWithoutCoverage_BlocksLaunch()
        {
            var d = fx.ValidLeoDesign(out _);
            d.SetGroundNetwork(fx.Network(gain: 30f, regimes: OrbitRegime.Lunar));
            var titles = OrbitAndLaunchValidator.Validate(d).Blocking.Select(i => i.title).ToList();
            Assert.That(titles, Has.Some.Contains("no cubre LEO"));
        }

        [Test]
        public void MissingNetwork_IsBlockingForTelecom()
        {
            var d = fx.ValidLeoDesign(out _);
            d.SetGroundNetwork(null);
            var issue = OrbitAndLaunchValidator.Validate(d).Blocking.Single(i => i.explanation.Contains("estaciones terrenas"));
            Assert.AreEqual(MissionRole.TelecomLaunchEngineer, issue.responsibleRole);
        }

        [Test]
        public void CommercialNetwork_DoublesContactAndDownlink_AtHigherCost()
        {
            var d = fx.ValidLeoDesign(out _);
            var nsn = MissionTelemetry.Calculate(d);
            d.SetGroundNetwork(fx.Network(contact: 2f, costPerMonth: 0.25f));
            var commercial = MissionTelemetry.Calculate(d);

            Assert.AreEqual(nsn.downlinkCapacityGBPerDay * 2f, commercial.downlinkCapacityGBPerDay, 0.01f);
            Assert.AreEqual(4f, commercial.contactHoursPerDay, 0.001f);
            Assert.Greater(commercial.operationsCostMUSD, nsn.operationsCostMUSD);
        }

        [Test]
        public void BigDishes_RaiseDataRate()
        {
            var orbit = fx.Leo();
            orbit.groundStationRangeKm = 384400;
            var antenna = fx.Antenna(refMbps: 150, refRange: 40000, maxMbps: 3500);
            float small = orbit.GetDataRateMbps(antenna, fx.Network(gain: 1f));
            float dsn = orbit.GetDataRateMbps(antenna, fx.Network(gain: 30f));
            Assert.AreEqual(small * 30f, dsn, 0.01f);
        }

        [Test]
        public void OutageWeight_DependsOnNetwork()
        {
            var outage = fx.Event("Corte");
            outage.eventType = RandomEventType.GroundStationOutage;
            var other = fx.Event("Otro");
            other.eventType = RandomEventType.SolarStorm;
            var reliable = fx.Network(outage: 0f);   // red que nunca falla

            for (int seed = 0; seed < 30; seed++)
            {
                var picked = MissionSimulator.PickEvents(new[] { outage, other }, fx.Leo(), new System.Random(seed), 1, 2, reliable);
                CollectionAssert.DoesNotContain(picked, outage);
            }
        }

        // ───── Subsistemas que mitigan eventos ─────

        [Test]
        public void OnboardStorage_BuffersGroundOutage()
        {
            var outage = fx.Event("Corte DSN", comms: 0.6f);
            outage.eventType = RandomEventType.GroundStationOutage;

            var withStorage = MissionSimulator.Run(AdvancedDesign(fx.Computer(storageGB: 256f)), null, With(outage));   // 256/60 ≈ 4 días
            var noStorage = MissionSimulator.Run(AdvancedDesign(fx.Computer(storageGB: 10f)), null, With(outage));     // < 1 día

            Assert.AreEqual(360f, withStorage.downlinkGBPerDay, 0.01f);
            Assert.IsTrue(withStorage.events.Single().mitigated);
            Assert.That(withStorage.wentWell, Has.Some.Contains("resistió"));
            Assert.AreEqual(360f * 0.6f, noStorage.downlinkGBPerDay, 0.01f);
        }

        [Test]
        public void RadiationHardenedComputer_IgnoresSingleEventUpset()
        {
            var seu = fx.Event("SEU", science: 0.95f, data: 0.9f, safeMode: true, safeHours: 6);
            seu.eventType = RandomEventType.SingleEventUpset;

            var hardened = MissionSimulator.Run(AdvancedDesign(fx.Computer(radHard: true)), null, With(seu));
            var commercial = MissionSimulator.Run(AdvancedDesign(fx.Computer(radHard: false)), null, With(seu));

            Assert.AreEqual(50f, hardened.scienceAchieved, 0.01f);
            Assert.AreEqual(50f * 0.95f, commercial.scienceAchieved, 0.01f);
            StringAssert.Contains("endurecida", hardened.events.Single().impact);
        }

        [Test]
        public void LinkMargin_AbsorbsOutage_CountsAsWentWell()
        {
            var outage = fx.Event("Corte", comms: 0.6f);      // 360 → 216 GB/día, y solo se generan 60
            var o = MissionSimulator.Run(fx.ValidLeoDesign(out _), null, With(outage));

            Assert.AreEqual(1f, o.dataReturnFraction, 0.001f);
            Assert.That(o.wentWell, Has.Some.Contains("el margen del enlace absorbió Corte"));
            Assert.That(o.wentWrong, Has.None.Contains("Corte"));
        }

        // ───── Post-misión: rango, tarjetas y misión real parecida ─────

        [TestCase(95, "A")]
        [TestCase(80, "B")]
        [TestCase(65, "C")]
        [TestCase(45, "D")]
        [TestCase(10, "F")]
        public void RankLetters(int score, string rank) => Assert.AreEqual(rank, MissionSimulator.RankFor(score));

        [Test]
        public void Report_SplitsWhatWentWellAndWrong()
        {
            var good = MissionSimulator.Run(fx.ValidLeoDesign(out _), null, With());
            Assert.IsNotEmpty(good.wentWell);
            Assert.AreEqual(good.explanations.Count, good.wentWell.Count + good.wentWrong.Count);
            Assert.AreEqual(MissionSimulator.RankFor(good.score), good.rank);

            var d = fx.ValidLeoDesign(out _);
            d.SetScenario(fx.Scenario(minScience: 100));
            d.AddInstrument(fx.Instrument("Radar", mass: 100, peakW: 100, dataGB: 540));
            var lossy = MissionSimulator.Run(d, null, With());
            Assert.That(lossy.wentWrong, Has.Some.Contains("solo transmitió"));
        }

        [Test]
        public void SimilarMission_PrefersSameOrbitAndInstrument()
        {
            var match = fx.RealMission("Gemela LEO", OrbitRegime.LEO, 800, new[] { "INS_IMAGER" }, "tierra");
            var lunar = fx.RealMission("Lunar", OrbitRegime.Lunar, 800, new string[0], "luna");
            var settings = With();
            settings.realMissions = new[] { lunar, match };

            var o = MissionSimulator.Run(fx.ValidLeoDesign(out _), null, settings);
            StringAssert.StartsWith("Gemela LEO", o.similarMission);
        }

        // ───── Vista previa de consecuencias (tooltips) ─────

        [Test]
        public void Preview_WarnsWhenAPartWouldOverloadTheRocket_WithoutTouchingTheDesign()
        {
            var d = fx.ValidLeoDesign(out _);
            d.SetLaunchVehicle(fx.Rocket("Pequeño", leoKg: 800));
            var heavy = fx.Instrument("Radar pesado", mass: 400, peakW: 100, dataGB: 10);

            var lines = DesignPreview.Describe(d, x => x.AddInstrument(heavy));

            Assert.That(lines, Has.Some.StartsWith("Masa: 775 → 1,175 kg").Or.Some.StartsWith("Masa: 775 → 1.175 kg").Or.Some.StartsWith("Masa: 775 → 1175 kg"));
            Assert.That(lines, Has.Some.StartsWith("Provocaría: Masa excede la capacidad del lanzador"));
            Assert.AreEqual(1, d.Instruments.Count, "la vista previa no debe modificar el diseño real");

            d.AddInstrument(heavy);
            var undo = DesignPreview.Describe(d, x => x.RemoveInstrument(heavy));
            Assert.That(undo, Has.Some.StartsWith("Resolvería: Masa excede la capacidad del lanzador"));
        }
    }
}
