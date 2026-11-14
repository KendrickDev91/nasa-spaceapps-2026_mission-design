using MissionDesign.Simulation;
using NUnit.Framework;

namespace MissionDesign.Tests
{
    public class MissionTelemetryTests
    {
        private TestFixtures fx;

        [SetUp] public void SetUp() => fx = new TestFixtures();
        [TearDown] public void TearDown() => fx.Dispose();

        [Test]
        public void Mass_SumsStructureInstrumentsAndPropellant()
        {
            var d = fx.ValidLeoDesign(out _);
            var t = MissionTelemetry.Calculate(d);

            // bus 400 + solar 50 + batería 15 + antena 10 + instrumento 200 = 675 seco; + 100 combustible
            Assert.AreEqual(675f, t.dryMassKg, 0.01f);
            Assert.AreEqual(100f, t.propellantMassKg, 0.01f);
            Assert.AreEqual(775f, t.totalMassKg, 0.01f);
            Assert.AreEqual(675f * 1.1f + 100f, t.massWithMarginKg, 0.01f);
            Assert.AreEqual(5000f, t.launchCapacityKg, 0.01f);
        }

        [Test]
        public void Power_OrbitAverageGenerationAccountsForEclipse()
        {
            var d = fx.ValidLeoDesign(out _);
            var t = MissionTelemetry.Calculate(d);

            Assert.AreEqual(2000f, t.solarPowerSunlitW, 0.01f);
            Assert.AreEqual(2000f * 0.65f, t.powerGeneratedAvgW, 0.01f);   // 35% en eclipse
            Assert.AreEqual(200f + 50f + 250f, t.peakLoadW, 0.01f);          // bus + antena + instrumento
            Assert.AreEqual(1300f - 500f, t.powerBalanceW, 0.01f);
            Assert.AreEqual(500f * 36f / 60f, t.eclipseEnergyRequiredWh, 0.01f);
            Assert.AreEqual(1f, t.powerAvailabilityFraction);
        }

        [Test]
        public void Power_SolarFollowsInverseSquareWithSunDistance()
        {
            var solar = fx.Solar(watts: 1000);
            Assert.AreEqual(1000f / (1.52f * 1.52f), solar.GetPowerW(1.52f), 0.1f);  // Marte
        }

        [Test]
        public void Power_DeficitRaisesAlertAndThrottlesScience()
        {
            var d = fx.ValidLeoDesign(out _);
            d.SetSolarArray(fx.Solar(watts: 400));       // 260 W medios < 500 W pico
            var t = MissionTelemetry.Calculate(d);

            Assert.Less(t.powerBalanceW, 0f);
            Assert.AreEqual(ResourceState.Critical, t.Get(ResourceKind.Power).state);
            Assert.AreEqual(260f / 500f, t.powerAvailabilityFraction, 0.001f);
            Assert.That(t.alerts, Has.Some.Contains("DÉFICIT ENERGÉTICO"));
        }

        [Test]
        public void Budget_IncludesHardwareLaunchAndOperations()
        {
            var d = fx.ValidLeoDesign(out _);
            var t = MissionTelemetry.Calculate(d);

            Assert.AreEqual(100f + 10f + 2f + 5f + 50f, t.hardwareCostMUSD, 0.01f);
            Assert.AreEqual(50f, t.launchCostMUSD, 0.01f);
            // equipo de control + red terrena (0.1 $M/mes en el fixture) durante 60 meses
            Assert.AreEqual(60f * (MissionTelemetry.OperationsCostPerMonth(Core.OrbitRegime.LEO) + 0.1f), t.operationsCostMUSD, 0.01f);
            Assert.AreEqual(t.hardwareCostMUSD + t.launchCostMUSD + t.operationsCostMUSD, t.totalCostMUSD, 0.01f);
        }

        [Test]
        public void Comms_DownlinkMatchesLinkBudgetFormula()
        {
            var d = fx.ValidLeoDesign(out _);
            var t = MissionTelemetry.Calculate(d);

            // 400 Mbps a 2000 km (distancia de referencia) × 2 h/día → 400·7200/8/1000 = 360 GB/día
            Assert.AreEqual(400f, t.downlinkRateMbps, 0.01f);
            Assert.AreEqual(360f, t.downlinkCapacityGBPerDay, 0.01f);
            Assert.AreEqual(60f, t.dataGeneratedGBPerDay, 0.01f);
            Assert.AreEqual(0f, t.dataLostGBPerDay, 0.01f);
        }

        [Test]
        public void Comms_InverseSquareAndModemCap()
        {
            var a = fx.Antenna(refMbps: 100, refRange: 1000, maxMbps: 150);
            Assert.AreEqual(25f, a.GetDataRateMbps(2000f), 0.01f);   // doble distancia → 1/4
            Assert.AreEqual(150f, a.GetDataRateMbps(100f), 0.01f);   // limitado por el módem
            Assert.AreEqual(250f, fx.Antenna(refMbps: 1, refRange: 1000, maxMbps: 1000).GetDataRateMbps(1000f, 250f), 0.01f); // ganancia DSN
        }

        [Test]
        public void Data_ExceedingCommsLosesScienceWithExplanation()
        {
            var d = fx.ValidLeoDesign(out _);
            d.AddInstrument(fx.Instrument("Radar", mass: 100, peakW: 100, dataGB: 540));  // total 600 GB/día
            var t = MissionTelemetry.Calculate(d);

            Assert.AreEqual(600f, t.dataGeneratedGBPerDay, 0.01f);
            Assert.AreEqual(240f, t.dataLostGBPerDay, 0.01f);
            Assert.AreEqual(0.6f, t.dataReturnFraction, 0.001f);
            Assert.AreEqual(ResourceState.Critical, t.Get(ResourceKind.Comms).state);
            Assert.AreEqual(100f * 0.6f, t.expectedScienceValue, 0.01f);   // 2 × 50 de valor potencial
            Assert.That(t.alerts, Has.Some.Contains("360.0 GB/día").Or.Some.Contains("360,0 GB/día"));
        }

        [Test]
        public void Time_CriticalPathPlusIntegration()
        {
            var d = fx.ValidLeoDesign(out _);
            var t = MissionTelemetry.Calculate(d);

            // instrumento 36 meses es el camino crítico (lanzador 12, bus 30) + 6 de I&T
            Assert.AreEqual(36f + MissionTelemetry.IntegrationAndTestMonths, t.developmentMonths, 0.01f);
            Assert.AreEqual(60f, t.estimatedLifeMonths, 0.01f);
        }

        [Test]
        public void Science_OffOrbitInstrumentsAreHalved()
        {
            var d = fx.ValidLeoDesign(out var inst);
            inst.preferredOrbits = new[] { Core.OrbitRegime.GEO };
            var t = MissionTelemetry.Calculate(d);
            Assert.AreEqual(50f * MissionTelemetry.OffOrbitScienceFactor, t.potentialScienceValue, 0.01f);
        }

        [Test]
        public void Telemetry_RecalculatesWhenPartIsAddedOrRemoved()
        {
            var d = fx.ValidLeoDesign(out var inst);
            using (var telemetry = new MissionTelemetry(d))
            {
                int updates = 0;
                telemetry.Updated += _ => updates++;
                float before = telemetry.Current.totalMassKg;

                var extra = fx.Instrument("Lidar", mass: 120, peakW: 80, dataGB: 5);
                d.AddInstrument(extra);
                Assert.AreEqual(before + 120f, telemetry.Current.totalMassKg, 0.01f);

                d.RemoveInstrument(inst);
                Assert.AreEqual(before - 80f, telemetry.Current.totalMassKg, 0.01f);
                Assert.AreEqual(2, updates);
            }
        }

        [Test]
        public void Report_HasSixResourcesInOrder()
        {
            var t = MissionTelemetry.Calculate(fx.ValidLeoDesign(out _));
            CollectionAssert.AreEqual(
                new[] { ResourceKind.Mass, ResourceKind.Power, ResourceKind.Budget, ResourceKind.Data, ResourceKind.Comms, ResourceKind.Time },
                t.resources.ConvertAll(r => r.kind));
            Assert.IsFalse(t.HasCritical);
        }
    }
}
