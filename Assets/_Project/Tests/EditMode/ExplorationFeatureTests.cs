using System.Collections.Generic;
using System.Linq;
using MissionDesign.Characters;
using MissionDesign.Core;
using MissionDesign.Data;
using MissionDesign.Exploration;
using MissionDesign.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace MissionDesign.Tests
{
    /// <summary>Trayectoria tipo diagrama ESA, cámaras/fotografías, informe de exploración, vestuario y geolocalización.</summary>
    public class ExplorationFeatureTests
    {
        private TestFixtures fx;

        [SetUp] public void SetUp() => fx = new TestFixtures();
        [TearDown] public void TearDown() => fx.Dispose();

        // ───── Trayectoria ─────

        [Test]
        public void Trajectory_HasEarthOrbitsBeforeTli_EndingAtInjectionPoint()
        {
            var s = FreeReturnTrajectory.Default;
            Assert.Greater(s.preTliSamples.Count, 100);
            Assert.Less(s.launchDays, -0.9f, "~1 día en órbita terrestre alta antes de la TLI (Artemis II)");
            var last = s.preTliSamples[s.preTliSamples.Count - 1];
            var tli = s.samples[0];
            Assert.AreEqual(0f, last.tDays, 0.01f);
            Assert.Less(Vector2.Distance(new Vector2(last.xKm, last.yKm), new Vector2(tli.xKm, tli.yKm)), 50f, "la órbita alta termina en el punto de la TLI");
            double ex = -FreeReturnTrajectory.Mu * FreeReturnTrajectory.LengthKm;
            float apogee = s.preTliSamples.Max(p => Mathf.Sqrt((float)((p.xKm - ex) * (p.xKm - ex)) + p.yKm * p.yKm)) - (float)FreeReturnTrajectory.EarthRadiusKm;
            Assert.AreEqual(70000f, apogee, 1500f);
        }

        [Test]
        public void Milestones_FollowTheEsaDiagram()
        {
            var m = FreeReturnTrajectory.Default.milestones;
            CollectionAssert.AreEqual(Enumerable.Range(1, 8), m.Select(x => x.number));
            StringAssert.Contains("Despegue", m[0].title);
            StringAssert.Contains("alta", m[1].title);
            StringAssert.Contains("separa del lanzador", m[2].title);
            StringAssert.Contains("TLI", m[3].title);
            StringAssert.Contains("Regreso", m[5].title);
            StringAssert.Contains("Amerizaje", m[7].title);
            Assert.Less(m[2].tDays, 0f, "la separación ocurre antes de la TLI");
        }

        // ───── Cámaras y fotografías ─────

        private SpacecraftDesign Flyby(bool camera, out InstrumentSO cam)
        {
            var d = fx.ValidLeoDesign(out _);
            d.Orbit.regime = OrbitRegime.LunarFlyby;
            var scn = fx.Scenario(minScience: 10, life: 0.31f);
            scn.crewed = true; scn.crewSize = 4;
            scn.allowedOrbits = new[] { OrbitRegime.LunarFlyby };
            d.SetScenario(scn);
            d.SetGroundNetwork(fx.Network(regimes: OrbitRegime.LunarFlyby));
            d.LaunchVehicle.payloadCapacities.Add(new OrbitPayloadCapacity { regime = OrbitRegime.LunarFlyby, payloadKg = 5000, confidence = DataConfidence.Official });
            var capsule = fx.Bus(cost: 100);
            capsule.crewCapable = true; capsule.recoverableStorageGB = 1000f; capsule.designLifeYears = 0.06f;
            d.SetBus(capsule);
            cam = null;
            if (camera)
            {
                cam = fx.Instrument("Camaras", mass: 10, peakW: 40, dataGB: 540);
                cam.isCamera = true; cam.photosPerDay = 8000;
                d.AddInstrument(cam);
            }
            return d;
        }

        private static MissionOutcomeReport Run(SpacecraftDesign d, params RandomEventSO[] events) =>
            MissionSimulator.Run(d, null, new SimulationSettings { seed = 1, allowLaunchFailure = false, forcedEvents = events.ToList() });

        [Test]
        public void CamerasOnLunarFlyby_TakePhotos_AndSdCardsBringTheRest()
        {
            var o = Run(Flyby(true, out _));
            Assert.IsTrue(o.reachedMoon);
            Assert.AreEqual(9600, o.photosTaken, "8 000 fotos/día × 1,2 días de sobrevuelo");
            Assert.Greater(o.photosTransmitted, 0);
            Assert.Greater(o.photosRecovered, 0, "lo que el enlace no envió vuelve en las tarjetas SD");
            Assert.AreEqual(o.photosTaken, o.photosTransmitted + o.photosRecovered);
            Assert.AreEqual(6, o.photoShots, "4 + 2 por cámara");
            Assert.AreEqual(o.photoShots, o.photoShotsAvailable);
            Assert.That(o.wentWell, Has.Some.Contains("Informe de la exploración"));
        }

        [Test]
        public void WithoutCameras_NoPhotos_AndReportSuggestsAddingOne()
        {
            var o = Run(Flyby(false, out _));
            Assert.AreEqual(0, o.photosTaken);
            Assert.AreEqual(0, o.photoShots);
            StringAssert.Contains("no llevaba cámaras", ExplorationReport.Build(o).body);
        }

        [Test]
        public void LostCapsule_KeepsOnlyTransmittedPhotos()
        {
            var d = Flyby(true, out _);
            d.SetBattery(fx.Battery(wh: 10));
            var o = Run(d, fx.Event("Tormenta", safeMode: true, safeHours: 24));
            Assert.IsFalse(o.survivedOperations);
            Assert.AreEqual(0, o.photosRecovered);
            Assert.Less(o.photoShotsAvailable, o.photoShots);
        }

        [Test]
        public void CamerasOnEarthOrbit_PhotographTheEarth()
        {
            var d = fx.ValidLeoDesign(out _);
            var cam = fx.Instrument("Camara", mass: 3, peakW: 20, dataGB: 10);
            cam.isCamera = true; cam.photosPerDay = 1000;
            d.AddInstrument(cam);
            var o = Run(d);
            Assert.IsFalse(o.reachedMoon);
            Assert.Greater(o.photosTaken, 0, "en órbita terrestre las cámaras fotografían la Tierra");
            Assert.AreEqual("Tierra", o.photoTarget);
            Assert.That(o.explanations, Has.Some.Contains("de la Tierra desde órbita"));
        }

        [Test]
        public void ExplorationReport_IsWrittenByTheCrew_WithGeolocation()
        {
            var o = Run(Flyby(true, out _));
            var r = ExplorationReport.Build(o, "Equipo Halcón");
            StringAssert.Contains("TRIPULACIÓN", r.title);
            StringAssert.Contains("Equipo Halcón", r.signature);
            StringAssert.Contains("cara oculta", r.body);
            StringAssert.Contains(MissionGeo.Format(MissionGeo.LaunchLat, MissionGeo.LaunchLon), r.body);
            StringAssert.Contains(MissionGeo.Format(MissionGeo.SplashLat, MissionGeo.SplashLon), r.body);
            StringAssert.Contains("tarjetas SD", r.body);
            string txt = ExplorationReport.ToPlainText(r, new[] { "Foto A", "Foto B" });
            StringAssert.Contains("02. Foto B", txt);
            StringAssert.DoesNotContain("<b>", txt);
        }

        // ───── Centro espacial: vestuario y mapa ─────

        [Test]
        public void Outfit_FollowsTheEnvironment()
        {
            Assert.AreEqual(Outfit.CleanRoom, ExplorationHud.OutfitFor("Edificio de Ensamblaje · Sala limpia"));
            Assert.AreEqual(Outfit.AssemblyEngineer, ExplorationHud.OutfitFor("Edificio de Ensamblaje"));
            Assert.AreEqual(Outfit.Astronaut, ExplorationHud.OutfitFor("Plataforma de lanzamiento"));
            Assert.AreEqual(Outfit.Engineer, ExplorationHud.OutfitFor("Centro de Control · Vestíbulo"));
        }

        [Test]
        public void Geolocator_PlacesThePadAtTheLaunchSite_AndNorthIsUp()
        {
            var (lat, lon) = ExplorationHud.GeoAt(new Vector3(0f, 0f, 420f));
            Assert.AreEqual(MissionGeo.LaunchLat, lat, 1e-6);
            Assert.AreEqual(MissionGeo.LaunchLon, lon, 1e-6);
            var (latCc, _) = ExplorationHud.GeoAt(Vector3.zero);
            Assert.Greater(latCc, lat, "el Centro de Control está al norte de la plataforma");
            Assert.Greater(ExplorationHud.MapUv(Vector3.zero).y, ExplorationHud.MapUv(new Vector3(0f, 0f, 420f)).y, "norte arriba en el mapa");
        }
    }
}
