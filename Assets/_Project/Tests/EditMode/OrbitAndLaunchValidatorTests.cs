using System.Linq;
using MissionDesign.Core;
using MissionDesign.Simulation;
using NUnit.Framework;

namespace MissionDesign.Tests
{
    public class OrbitAndLaunchValidatorTests
    {
        private TestFixtures fx;

        [SetUp] public void SetUp() => fx = new TestFixtures();
        [TearDown] public void TearDown() => fx.Dispose();

        [Test]
        public void ValidDesign_CanLaunch()
        {
            var r = OrbitAndLaunchValidator.Validate(fx.ValidLeoDesign(out _));
            Assert.IsTrue(r.canLaunch, r.ToConsoleString());
            Assert.IsEmpty(r.Blocking);
            Assert.AreEqual(9.4f, r.requiredDeltaVKmS, 0.001f);
        }

        [Test]
        public void Tsiolkovsky_LeoNeedsAbout93PercentPropellant()
        {
            // 1 − e^(−9400 / (350·9.80665)) ≈ 0.935
            Assert.AreEqual(0.935f, OrbitAndLaunchValidator.IdealPropellantFraction(9.4f), 0.002f);
            Assert.Greater(OrbitAndLaunchValidator.IdealPropellantFraction(15.5f),
                           OrbitAndLaunchValidator.IdealPropellantFraction(13.0f));
        }

        [Test]
        public void MassAboveRocketCapacity_BlocksWithTechnicalExplanation()
        {
            var d = fx.ValidLeoDesign(out _);
            d.SetLaunchVehicle(fx.Rocket("Mini", leoKg: 300));
            var r = OrbitAndLaunchValidator.Validate(d);

            Assert.IsFalse(r.canLaunch);
            var issue = r.Blocking.Single(i => i.title.StartsWith("Masa excede"));
            Assert.AreEqual(MissionRole.TelecomLaunchEngineer, issue.responsibleRole);
            StringAssert.Contains("Mini", issue.explanation);
            StringAssert.Contains("9.4 km/s", issue.explanation.Replace(',', '.'));
            StringAssert.Contains("Imager", issue.explanation);      // sugiere el instrumento más pesado
            Assert.Less(r.capacityMarginFraction, 0f);
        }

        [Test]
        public void ThinMargin_IsWarningNotBlocking()
        {
            var d = fx.ValidLeoDesign(out _);
            d.SetLaunchVehicle(fx.Rocket("Justo", leoKg: 800));   // 775 kg → 3% de margen
            var r = OrbitAndLaunchValidator.Validate(d);

            Assert.IsTrue(r.canLaunch);
            Assert.That(r.Warnings.Select(w => w.title), Has.Member("Margen de masa insuficiente"));
        }

        [Test]
        public void RocketWithoutOrbitProfile_Blocks()
        {
            var d = fx.ValidLeoDesign(out _);
            var lv = fx.Rocket("Solo GEO", leoKg: 0);
            d.SetLaunchVehicle(lv);
            var r = OrbitAndLaunchValidator.Validate(d);

            Assert.IsFalse(r.canLaunch);
            Assert.That(r.Blocking.Select(i => i.title), Has.Some.Contains("no llega a LEO"));
        }

        [Test]
        public void MissingSelections_AreBlockingAndAssignedToRoles()
        {
            var r = OrbitAndLaunchValidator.Validate(new SpacecraftDesign());
            Assert.IsFalse(r.canLaunch);
            var roles = r.Blocking.Select(i => i.responsibleRole).Distinct().ToList();
            CollectionAssert.AreEquivalent(
                new[] { MissionRole.MissionDirector, MissionRole.SystemsEngineer, MissionRole.PrincipalScientist, MissionRole.TelecomLaunchEngineer },
                roles);
        }

        [Test]
        public void BusLimits_SlotsMassAndPower()
        {
            var d = fx.ValidLeoDesign(out _);
            d.SetBus(fx.Bus(maxPayload: 150, maxPower: 100, slots: 1));
            d.AddInstrument(fx.Instrument("Extra", mass: 10, peakW: 10, dataGB: 1));
            var titles = OrbitAndLaunchValidator.Validate(d).Blocking.Select(i => i.title).ToList();

            CollectionAssert.IsSubsetOf(
                new[] { "Estructura del bus sobrecargada", "Sin espacio para instrumentos", "Distribución de potencia excedida" },
                titles);
        }

        [Test]
        public void OverBudget_BlocksForMissionDirector()
        {
            var d = fx.ValidLeoDesign(out _);
            d.SetScenario(fx.Scenario(budget: 100));
            var issue = OrbitAndLaunchValidator.Validate(d).Blocking.Single(i => i.title == "Presupuesto excedido");
            Assert.AreEqual(MissionRole.MissionDirector, issue.responsibleRole);
        }

        [Test]
        public void EnergyDeficitAndDataLoss_AreWarnings()
        {
            var d = fx.ValidLeoDesign(out _);
            d.SetSolarArray(fx.Solar(watts: 400));
            d.AddInstrument(fx.Instrument("Radar", mass: 50, peakW: 50, dataGB: 2000));
            var r = OrbitAndLaunchValidator.Validate(d);

            var warnings = r.Warnings.Select(w => w.title).ToList();
            Assert.Contains("Déficit energético", warnings);
            Assert.Contains("Enlace de bajada saturado", warnings);
        }

        [Test]
        public void OnboardDeltaV_TsiolkovskyAndInsertionCheck()
        {
            // 300 s · 9.80665 · ln(775 / 675) ≈ 0.406 km/s con 100 kg de combustible
            var d = fx.ValidLeoDesign(out _);
            var t = MissionTelemetry.Calculate(d);
            Assert.AreEqual(0.406f, t.spacecraftDeltaVKmS, 0.002f);

            d.Orbit.onboardDeltaVKmS = 0.85f;   // inserción lunar
            var r = OrbitAndLaunchValidator.Validate(d);
            var issue = r.Blocking.Single(i => i.title == "Combustible insuficiente para la inserción");
            Assert.AreEqual(MissionRole.SystemsEngineer, issue.responsibleRole);
        }

        [Test]
        public void OrbitNotAllowedByScenario_Blocks()
        {
            var d = fx.ValidLeoDesign(out _);
            var scn = fx.Scenario();
            scn.allowedOrbits = new[] { OrbitRegime.GEO };
            d.SetScenario(scn);
            Assert.That(OrbitAndLaunchValidator.Validate(d).Blocking.Select(i => i.title), Has.Member("Órbita fuera del objetivo"));
        }
    }
}
