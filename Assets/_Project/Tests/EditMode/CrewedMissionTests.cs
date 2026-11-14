using System.Collections.Generic;
using System.Linq;
using MissionDesign.Core;
using MissionDesign.Data;
using MissionDesign.Simulation;
using MissionDesign.UI.Visual3D;
using NUnit.Framework;

namespace MissionDesign.Tests
{
    /// <summary>Trayectoria de retorno libre (CR3BP) y misión tripulada tipo Artemis II.</summary>
    public class CrewedMissionTests
    {
        private TestFixtures fx;

        [SetUp] public void SetUp() => fx = new TestFixtures();
        [TearDown] public void TearDown() => fx.Dispose();

        // ───── Trayectoria ─────

        [Test]
        public void FreeReturn_ConvergesToArtemisLikeFlyby()
        {
            var s = FreeReturnTrajectory.Default;
            Assert.IsTrue(s.converged, $"flyby {s.flybyAltitudeKm} km, perigeo {s.returnPerigeeAltitudeKm} km");
            Assert.IsTrue(s.passesFarSide, "debe rodear la cara oculta de la Luna");
            Assert.AreEqual(7400f, s.flybyAltitudeKm, 370f);
            Assert.Less(s.returnPerigeeAltitudeKm, (float)FreeReturnTrajectory.EntryInterfaceKm, "debe volver a la atmósfera sin motor");
            Assert.That(s.totalDays, Is.InRange(6f, 12f), "Artemis II duró ~10 días");
            Assert.That(s.tliDeltaVKmS, Is.InRange(3.0f, 3.3f), "TLI desde LEO ≈ 3,1-3,2 km/s");
        }

        [Test]
        public void FreeReturn_HasEightOrderedMilestones()
        {
            var m = FreeReturnTrajectory.Default.milestones;
            CollectionAssert.AreEqual(Enumerable.Range(1, 8), m.Select(x => x.number));
            for (int i = 1; i < m.Count; i++) Assert.GreaterOrEqual(m[i].tDays, m[i - 1].tDays, m[i].title);
            StringAssert.Contains("Sobrevuelo", m[4].title);
        }

        [Test]
        public void FreeReturn_DrawsAFigureEight()
        {
            // En el marco rotatorio, la vuelta cruza la línea Tierra-Luna entre ambos cuerpos: el "8".
            var back = FreeReturnTrajectory.Default.samples.Where(p => p.leg == 1).ToList();
            bool crosses = false;
            for (int i = 1; i < back.Count; i++)
                if (back[i].xKm > 40000 && back[i].xKm < 340000 && System.Math.Sign(back[i].yKm) != System.Math.Sign(back[i - 1].yKm)) crosses = true;
            var outbound = FreeReturnTrajectory.Default.samples.Where(p => p.leg == 0).ToList();
            Assert.IsTrue(crosses || outbound.Any(p => p.yKm > 0) && back.Any(p => p.yKm < 0), "ida y vuelta deben ir por lados opuestos");
        }

        [Test]
        public void ViewMapping_PlacesEarthAndMoon()
        {
            double mu = FreeReturnTrajectory.Mu, L = FreeReturnTrajectory.LengthKm;
            var earth = OrbitViewController.Map((float)(-mu * L), 0f);
            var moon = OrbitViewController.Map((float)((1 - mu) * L), 0f);
            Assert.AreEqual(0f, earth.magnitude, 0.01f);
            Assert.AreEqual(-OrbitViewController.MoonDisplayDistance, moon.x, 0.01f);
        }

        // ───── Misión tripulada ─────

        private SpacecraftDesign CrewedDesign(out SpacecraftBusSO capsule)
        {
            var d = fx.ValidLeoDesign(out _);
            d.Orbit.regime = OrbitRegime.LunarFlyby;
            var scn = fx.Scenario(minScience: 40, life: 0.31f);
            scn.crewed = true; scn.crewSize = 4;
            scn.allowedOrbits = new[] { OrbitRegime.LunarFlyby };
            d.SetScenario(scn);
            d.SetGroundNetwork(fx.Network(regimes: OrbitRegime.LunarFlyby));
            d.LaunchVehicle.payloadCapacities.Add(new OrbitPayloadCapacity { regime = OrbitRegime.LunarFlyby, payloadKg = 5000, confidence = DataConfidence.Official });
            capsule = fx.Bus(cost: 100);
            capsule.crewCapable = true;
            capsule.recoverableStorageGB = 1000f;
            capsule.designLifeYears = 0.06f;
            d.SetBus(capsule);
            return d;
        }

        [Test]
        public void CrewedScenario_RequiresLifeSupport()
        {
            var d = CrewedDesign(out _);
            d.SetBus(fx.Bus());   // bus robótico
            var issue = OrbitAndLaunchValidator.Validate(d).Blocking.Single(i => i.title == "Nave sin soporte vital");
            Assert.AreEqual(MissionRole.SystemsEngineer, issue.responsibleRole);

            var ok = CrewedDesign(out _);
            Assert.IsTrue(OrbitAndLaunchValidator.Validate(ok).canLaunch, OrbitAndLaunchValidator.Validate(ok).ToConsoleString());
        }

        [Test]
        public void SplashdownRecoversStoredPhotos()
        {
            var d = CrewedDesign(out _);
            d.AddInstrument(fx.Instrument("Camaras", mass: 40, peakW: 100, dataGB: 540));   // 600 GB/día vs 360 de enlace
            var o = MissionSimulator.Run(d, null, new SimulationSettings { seed = 1, allowLaunchFailure = false, forcedEvents = new List<RandomEventSO>() });

            Assert.IsTrue(o.returnsToEarth && o.crewed);
            Assert.AreEqual(1.0f, o.dataRecoveredTB, 0.01f, "se recupera lo que cabe en 1 TB de tarjetas");
            Assert.Greater(o.dataReturnFraction, 0.6f, "la recuperación sube el retorno por encima del enlace (60 %)");
            Assert.IsTrue(o.timeline.Any(e => e.phase == MissionPhase.ReturnAndRecovery));
            Assert.That(o.wentWell, Has.Some.Contains("tarjetas SD"));
        }

        [Test]
        public void LostCapsule_LosesStoredData()
        {
            var d = CrewedDesign(out _);
            d.SetBattery(fx.Battery(wh: 10));
            var storm = fx.Event("Tormenta", safeMode: true, safeHours: 24);
            var o = MissionSimulator.Run(d, null, new SimulationSettings { seed = 1, allowLaunchFailure = false, forcedEvents = new List<RandomEventSO> { storm } });

            Assert.AreEqual(MissionVerdict.Failure, o.verdict);
            Assert.AreEqual(0f, o.dataRecoveredTB);
            StringAssert.Contains("cápsula no recuperada", o.verdictTitle);
        }

        [Test]
        public void SlsLaunch_LogsTheRealAscentTimeline()
        {
            var d = CrewedDesign(out _);
            d.LaunchVehicle.vehicleId = "LV_SLS_B1";
            var o = MissionSimulator.Run(d, null, new SimulationSettings { seed = 1, allowLaunchFailure = false, forcedEvents = new List<RandomEventSO>() });
            var launch = o.timeline.Where(e => e.phase == MissionPhase.Launch).Select(e => e.message).ToList();
            Assert.That(launch, Has.Some.Contains("Separación de los propulsores sólidos (47,5 km)"));
            Assert.That(launch, Has.Some.Contains("22 279 km"));
            Assert.That(o.timeline.Select(e => e.message), Has.Some.Contains("Inyección translunar"));
        }
    }
}
