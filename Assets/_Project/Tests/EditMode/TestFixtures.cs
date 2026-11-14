using System.Collections.Generic;
using System.Linq;
using MissionDesign.Core;
using MissionDesign.Data;
using MissionDesign.Simulation;
using UnityEngine;

namespace MissionDesign.Tests
{
    /// <summary>
    /// Piezas creadas en memoria con números redondos, independientes de la base generada,
    /// para que cada prueba verifique la fórmula y no los datos del catálogo.
    /// </summary>
    internal sealed class TestFixtures : System.IDisposable
    {
        private readonly List<Object> created = new List<Object>();

        public T Make<T>() where T : ScriptableObject
        {
            var so = ScriptableObject.CreateInstance<T>();
            created.Add(so);
            return so;
        }

        public OrbitSO Leo()
        {
            var o = Make<OrbitSO>();
            o.displayName = "LEO test"; o.orbitId = "ORB_TEST_LEO"; o.regime = OrbitRegime.LEO; o.requiredDeltaVKmS = 9.4f;
            o.sunDistanceAU = 1f; o.eclipseFraction = 0.35f; o.maxEclipseMinutes = 36;
            o.groundStationRangeKm = 2000; o.dailyContactHours = 2f;
            return o;
        }

        public GroundNetworkSO Network(float gain = 1f, float contact = 1f, float costPerMonth = 0.1f, float outage = 1f,
            params OrbitRegime[] regimes)
        {
            var n = Make<GroundNetworkSO>();
            n.displayName = "Red test"; n.networkId = "GND_TEST"; n.gainFactor = gain; n.contactHoursMultiplier = contact;
            n.costPerMonthMUSD = costPerMonth; n.outageProbabilityMultiplier = outage;
            n.supportedRegimes = regimes.Length > 0 ? regimes : new[] { OrbitRegime.LEO };
            return n;
        }

        public OnboardComputerSO Computer(float storageGB = 256f, bool radHard = true, float mass = 5f, float cost = 3f)
        {
            var c = Make<OnboardComputerSO>();
            c.displayName = "Computadora test"; c.componentId = "CDH_TEST"; c.storageGB = storageGB; c.radiationHardened = radHard;
            c.massKg = mass; c.costMUSD = cost; c.peakPowerW = 10f; c.developmentMonths = 6; c.advancedOnly = true;
            return c;
        }

        public AttitudeControlSO Adcs(float accuracyDeg = 0.01f, float mass = 10f, float cost = 2f)
        {
            var a = Make<AttitudeControlSO>();
            a.displayName = "ADCS test"; a.componentId = "ADCS_TEST"; a.pointingAccuracyDeg = accuracyDeg; a.massKg = mass; a.costMUSD = cost;
            a.peakPowerW = 20f; a.developmentMonths = 8; a.advancedOnly = true;
            return a;
        }

        public RealMissionSO RealMission(string name, OrbitRegime regime, float mass, string[] instrumentIds, params string[] tags)
        {
            var m = Make<RealMissionSO>();
            m.displayName = name; m.regime = regime; m.massKg = mass; m.instrumentIds = instrumentIds; m.tags = tags;
            return m;
        }

        public LaunchVehicleSO Rocket(string name, float leoKg, float cost = 50f, float lead = 12f)
        {
            var lv = Make<LaunchVehicleSO>();
            lv.displayName = name; lv.vehicleId = "LV_" + name; lv.launchCostMUSD = cost; lv.leadTimeMonths = lead;
            lv.payloadCapacities = new List<OrbitPayloadCapacity>
            {
                new OrbitPayloadCapacity { regime = OrbitRegime.LEO, payloadKg = leoKg, confidence = DataConfidence.Official }
            };
            return lv;
        }

        public SpacecraftBusSO Bus(float dry = 400, float prop = 100, float maxPayload = 500, float maxPower = 1000,
            int slots = 3, float hk = 200, float life = 5, float cost = 100, float months = 30)
        {
            var b = Make<SpacecraftBusSO>();
            b.displayName = "Bus test"; b.componentId = "BUS_TEST"; b.massKg = dry; b.propellantMassKg = prop; b.maxPayloadMassKg = maxPayload;
            b.maxPayloadPowerW = maxPower; b.payloadSlots = slots; b.housekeepingPowerW = hk; b.housekeepingDataGBPerDay = 0f;
            b.designLifeYears = life; b.costMUSD = cost; b.developmentMonths = months;
            return b;
        }

        public SolarArraySO Solar(float watts = 2000, float mass = 50, float cost = 10)
        {
            var s = Make<SolarArraySO>();
            s.displayName = "Solar test"; s.componentId = "SOL_TEST"; s.powerAt1AU_W = watts; s.massKg = mass; s.costMUSD = cost;
            s.annualDegradation = 0.02f; s.developmentMonths = 12;
            return s;
        }

        public BatterySO Battery(float wh = 2000, float mass = 15, float cost = 2)
        {
            var b = Make<BatterySO>();
            b.displayName = "Battery test"; b.componentId = "BAT_TEST"; b.capacityWh = wh; b.maxDepthOfDischarge = 0.5f; b.massKg = mass; b.costMUSD = cost;
            b.developmentMonths = 4;
            return b;
        }

        public AntennaSO Antenna(float refMbps = 400, float refRange = 2000, float maxMbps = 800, float power = 50, float mass = 10, float cost = 5)
        {
            var a = Make<AntennaSO>();
            a.displayName = "Antena test"; a.componentId = "ANT_TEST"; a.referenceDataRateMbps = refMbps; a.referenceRangeKm = refRange; a.maxDataRateMbps = maxMbps;
            a.peakPowerW = power; a.massKg = mass; a.costMUSD = cost; a.developmentMonths = 8; a.dsnCompatible = true;
            return a;
        }

        public InstrumentSO Instrument(string name, float mass, float peakW, float dataGB, float cost = 50, int science = 50, float months = 36)
        {
            var i = Make<InstrumentSO>();
            i.displayName = name; i.componentId = "INS_" + name; i.massKg = mass; i.peakPowerW = peakW; i.averagePowerW = peakW * 0.8f; i.dutyCycle = 1f;
            i.dataVolumeGBPerDay = dataGB; i.costMUSD = cost; i.scienceValue = science; i.developmentMonths = months;
            i.preferredOrbits = new[] { OrbitRegime.LEO };
            return i;
        }

        public MissionScenarioSO Scenario(float budget = 1000, float maxDev = 60, float life = 60, int minScience = 0)
        {
            var s = Make<MissionScenarioSO>();
            s.displayName = "Escenario test"; s.scenarioId = "SCN_TEST"; s.budgetLimitMUSD = budget; s.maxDevelopmentMonths = maxDev;
            s.targetMissionLifeMonths = life; s.minScienceValue = minScience; s.allowedOrbits = new[] { OrbitRegime.LEO };
            return s;
        }

        public RandomEventSO Event(string id, float probability = 0.3f, float science = 1f, float data = 1f, float comms = 1f,
            float powerLoss = 0f, float failureChance = 0f, bool safeMode = false, float safeHours = 0f, bool positive = false)
        {
            var e = Make<RandomEventSO>();
            e.eventId = id; e.displayName = id; e.baseProbability = probability; e.isPositive = positive;
            e.scienceMultiplier = science; e.dataGenerationMultiplier = data; e.commsMultiplier = comms;
            e.permanentPowerLossFraction = powerLoss; e.componentFailureChance = failureChance;
            e.requiresSafeMode = safeMode; e.safeModeHours = safeHours;
            e.affectedPhase = MissionPhase.ScienceOperations;
            return e;
        }

        /// <summary>Diseño completo y válido en LEO; cada prueba modifica solo lo que examina.</summary>
        public SpacecraftDesign ValidLeoDesign(out InstrumentSO instrument)
        {
            var d = new SpacecraftDesign();
            d.SetScenario(Scenario());
            d.SetOrbit(Leo());
            d.SetLaunchVehicle(Rocket("Rocket test", 5000));
            d.SetBus(Bus());
            d.SetSolarArray(Solar());
            d.SetBattery(Battery());
            d.SetAntenna(Antenna());
            d.SetGroundNetwork(Network());
            instrument = Instrument("Imager", mass: 200, peakW: 250, dataGB: 60);
            instrument.componentId = "INS_IMAGER";
            d.AddInstrument(instrument);
            return d;
        }

        /// <summary>Catálogo mínimo con las piezas de un diseño (para guardar/cargar/comparar).</summary>
        public NasaDatabaseSO DatabaseFrom(SpacecraftDesign d, params ScriptableObject[] extra)
        {
            var db = Make<NasaDatabaseSO>();
            if (d.Scenario != null) db.scenarios.Add(d.Scenario);
            if (d.Orbit != null) db.orbits.Add(d.Orbit);
            if (d.LaunchVehicle != null) db.launchVehicles.Add(d.LaunchVehicle);
            if (d.GroundNetwork != null) db.groundNetworks.Add(d.GroundNetwork);
            foreach (var so in d.AllComponents.Cast<ScriptableObject>().Concat(extra))
            {
                switch (so)
                {
                    case SpacecraftBusSO b: db.buses.Add(b); break;
                    case SolarArraySO s: db.solarArrays.Add(s); break;
                    case BatterySO bt: db.batteries.Add(bt); break;
                    case AntennaSO a: db.antennas.Add(a); break;
                    case InstrumentSO i: db.instruments.Add(i); break;
                    case OnboardComputerSO c: db.computers.Add(c); break;
                    case AttitudeControlSO ad: db.attitudeControls.Add(ad); break;
                }
            }
            return db;
        }

        public void Dispose()
        {
            foreach (var o in created)
                if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }
    }
}
