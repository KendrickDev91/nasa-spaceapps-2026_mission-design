using System.Collections.Generic;
using System.IO;
using MissionDesign.Core;
using MissionDesign.Data;
using MissionDesign.Simulation;
using UnityEditor;
using UnityEngine;

namespace MissionDesign.Editor
{
    /// <summary>
    /// Genera (o actualiza en el sitio, conservando GUIDs y prefabs asignados) la base de datos inicial.
    /// Valores marcados con DataConfidence: Official / Approximate / GameEstimate.
    /// Ejecutable también en batch: -executeMethod MissionDesign.Editor.NasaDatabaseGenerator.Generate
    /// </summary>
    public static class NasaDatabaseGenerator
    {
        private const string Root = "Assets/_Project/Data/Generated";

        [MenuItem("NASA SpaceApps/Generate Initial NASA Database")]
        public static void Generate()
        {
            var db = GetOrCreate<NasaDatabaseSO>($"{Root}/NasaDatabase.asset");

            db.orbits = CreateOrbits();
            db.launchVehicles = CreateLaunchVehicles();
            db.instruments = CreateInstruments();
            db.buses = CreateBuses();
            db.solarArrays = CreateSolarArrays();
            db.batteries = CreateBatteries();
            db.antennas = CreateAntennas();
            db.computers = CreateComputers();
            db.attitudeControls = CreateAttitudeControls();
            db.groundNetworks = CreateGroundNetworks();
            db.randomEvents = CreateRandomEvents();
            db.scenarios = CreateScenarios();
            db.realMissions = CreateRealMissions();
            AddCubeSatAndSandbox(db);
            AddCrewedArtemis(db);
            AddExplorationCameras(db);
            int models = ModelLinker.LinkAll(db);
            Debug.Log($"[NASA DB] Modelos 3D vinculados: {models}");

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[NASA DB] OK → {Root}: {db.launchVehicles.Count} cohetes, {db.orbits.Count} órbitas, " +
                      $"{db.instruments.Count} instrumentos, {db.buses.Count} buses, {db.solarArrays.Count} paneles, " +
                      $"{db.batteries.Count} baterías, {db.antennas.Count} antenas, {db.computers.Count} computadoras, " +
                      $"{db.attitudeControls.Count} ADCS, {db.groundNetworks.Count} redes terrenas, {db.randomEvents.Count} eventos, " +
                      $"{db.scenarios.Count} escenarios, {db.realMissions.Count} misiones reales.");
            LogCommsSanityTable(db);
            Selection.activeObject = db;
        }

        // ───────────────────────────── ÓRBITAS ─────────────────────────────

        private static List<OrbitSO> CreateOrbits()
        {
            var list = new List<OrbitSO>();

            var leo = GetOrCreate<OrbitSO>($"{Root}/Orbits/ORB_LEO_SSO.asset");
            leo.orbitId = "ORB_LEO_SSO"; leo.displayName = "LEO heliosíncrona (705 km)"; leo.regime = OrbitRegime.LEO;
            leo.description = "Órbita baja polar heliosíncrona usada por Terra, Aqua y Landsat: cruza el ecuador a la misma hora solar.";
            leo.altitudeKm = 705; leo.inclinationDeg = 98.2f; leo.orbitalPeriodMinutes = 98.9f; leo.requiredDeltaVKmS = 9.4f; leo.onboardDeltaVKmS = 0.05f;
            leo.sunDistanceAU = 1f; leo.eclipseFraction = 0.35f; leo.maxEclipseMinutes = 35;
            leo.groundStationRangeKm = 2000; leo.dailyContactHours = 2f;
            leo.radiationLevel = 0.25f; leo.debrisRisk = 0.6f;
            leo.educationalNote = "≈9.4 km/s de Δv: ~7.8 km/s orbitales + ~1.5 km/s de pérdidas por gravedad y arrastre. Solo hay contacto con tierra en pasadas de ~10 min.";
            leo.dataConfidence = DataConfidence.Approximate; leo.sourceUrl = "https://landsat.gsfc.nasa.gov/satellites/landsat-9/";
            Save(leo); list.Add(leo);

            var meo = GetOrCreate<OrbitSO>($"{Root}/Orbits/ORB_MEO.asset");
            meo.orbitId = "ORB_MEO"; meo.displayName = "MEO (20 200 km, tipo GPS)"; meo.regime = OrbitRegime.MEO;
            meo.description = "Órbita media usada por constelaciones de navegación (GPS, Galileo). Atraviesa el cinturón de Van Allen exterior.";
            meo.altitudeKm = 20200; meo.inclinationDeg = 55f; meo.orbitalPeriodMinutes = 718f; meo.requiredDeltaVKmS = 11.8f; meo.onboardDeltaVKmS = 0.3f;
            meo.sunDistanceAU = 1f; meo.eclipseFraction = 0.04f; meo.maxEclipseMinutes = 56;
            meo.groundStationRangeKm = 22000; meo.dailyContactHours = 10f;
            meo.radiationLevel = 0.85f; meo.debrisRisk = 0.1f;
            meo.educationalNote = "La radiación del cinturón de Van Allen degrada paneles y provoca Single Event Upsets: requiere electrónica endurecida.";
            meo.dataConfidence = DataConfidence.GameEstimate; meo.sourceUrl = "https://www.gps.gov/systems/gps/space/";
            Save(meo); list.Add(meo);

            var geo = GetOrCreate<OrbitSO>($"{Root}/Orbits/ORB_GEO.asset");
            geo.orbitId = "ORB_GEO"; geo.displayName = "GEO (35 786 km)"; geo.regime = OrbitRegime.GEO;
            geo.description = "Órbita geoestacionaria: el satélite ve siempre el mismo hemisferio (GOES-R, TDRS).";
            geo.altitudeKm = 35786; geo.inclinationDeg = 0f; geo.orbitalPeriodMinutes = 1436f; geo.requiredDeltaVKmS = 13.0f; geo.onboardDeltaVKmS = 1.5f;
            geo.sunDistanceAU = 1f; geo.eclipseFraction = 0.01f; geo.maxEclipseMinutes = 72;
            geo.groundStationRangeKm = 38000; geo.dailyContactHours = 24f;
            geo.radiationLevel = 0.6f; geo.debrisRisk = 0.2f;
            geo.educationalNote = "Contacto 24/7 con una estación fija, pero 19× más lejos que LEO: la señal llega ~360 veces más débil (1/r²).";
            geo.dataConfidence = DataConfidence.Approximate; geo.sourceUrl = "https://www.goes-r.gov/mission/mission.html";
            Save(geo); list.Add(geo);

            var lunar = GetOrCreate<OrbitSO>($"{Root}/Orbits/ORB_LUNAR.asset");
            lunar.orbitId = "ORB_LUNAR"; lunar.displayName = "Órbita lunar polar (50 km)"; lunar.regime = OrbitRegime.Lunar;
            lunar.description = "Órbita polar baja alrededor de la Luna, como Lunar Reconnaissance Orbiter (LRO).";
            lunar.altitudeKm = 50; lunar.inclinationDeg = 90f; lunar.orbitalPeriodMinutes = 113f; lunar.requiredDeltaVKmS = 15.5f; lunar.onboardDeltaVKmS = 0.85f;
            lunar.sunDistanceAU = 1f; lunar.eclipseFraction = 0.30f; lunar.maxEclipseMinutes = 48;
            lunar.groundStationRangeKm = 384400; lunar.dailyContactHours = 8f;
            lunar.radiationLevel = 0.7f; lunar.debrisRisk = 0.05f;
            lunar.educationalNote = "Sin magnetosfera que proteja de partículas solares. Las antenas de 34 m de la DSN compensan los 384 400 km de distancia.";
            lunar.dataConfidence = DataConfidence.Approximate; lunar.sourceUrl = "https://science.nasa.gov/mission/lro/";
            Save(lunar); list.Add(lunar);

            return list;
        }

        // ───────────────────────────── COHETES ─────────────────────────────

        private static List<LaunchVehicleSO> CreateLaunchVehicles()
        {
            var list = new List<LaunchVehicleSO>();

            var f9 = GetOrCreate<LaunchVehicleSO>($"{Root}/LaunchVehicles/LV_Falcon9.asset");
            f9.vehicleId = "LV_FALCON9"; f9.displayName = "Falcon 9 (booster reutilizable)"; f9.operatorName = "SpaceX"; f9.country = "EE.UU.";
            f9.description = "Cohete de dos etapas con primera etapa recuperable. Lanzó misiones NASA como TESS, DART, PACE y SWOT.";
            f9.payloadCapacities = new List<OrbitPayloadCapacity>
            {
                Cap(OrbitRegime.LEO, 17500, DataConfidence.Approximate, "Reutilizable; 22 800 kg en modo desechable."),
                Cap(OrbitRegime.MEO, 4000, DataConfidence.GameEstimate, "Referencia: satélites GPS III (~4 400 kg) a órbita de transferencia."),
                Cap(OrbitRegime.GEO, 5500, DataConfidence.Official, "Valor a GTO (reutilizable); el satélite circulariza con su motor de apogeo."),
                Cap(OrbitRegime.Lunar, 3000, DataConfidence.GameEstimate, "Inyección translunar estimada (ref. IM-1 Nova-C ~1 900 kg).")
            };
            f9.fairingDiameterM = 5.2f; f9.reliability = 0.99f; f9.launchCostMUSD = 69.75f; f9.leadTimeMonths = 12;
            f9.dataConfidence = DataConfidence.Approximate; f9.sourceUrl = "https://www.spacex.com/vehicles/falcon-9/";
            f9.sourceNotes = "Costo: precio de lista publicado por SpaceX (~$69.75 M). Confiabilidad >99% histórica.";
            Save(f9); list.Add(f9);

            var el = GetOrCreate<LaunchVehicleSO>($"{Root}/LaunchVehicles/LV_Electron.asset");
            el.vehicleId = "LV_ELECTRON"; el.displayName = "Electron"; el.operatorName = "Rocket Lab"; el.country = "EE.UU. / Nueva Zelanda";
            el.description = "Lanzador dedicado de pequeños satélites. Lanzó CAPSTONE (NASA) hacia la Luna con la etapa Photon.";
            el.payloadCapacities = new List<OrbitPayloadCapacity>
            {
                Cap(OrbitRegime.LEO, 300, DataConfidence.Official, "300 kg a LEO baja; ~200 kg a SSO 500 km."),
                Cap(OrbitRegime.MEO, 0, DataConfidence.Official, "No ofrecido."),
                Cap(OrbitRegime.GEO, 0, DataConfidence.Official, "No ofrecido."),
                Cap(OrbitRegime.Lunar, 30, DataConfidence.Approximate, "Con etapa Photon; CAPSTONE pesaba ~25 kg.")
            };
            el.fairingDiameterM = 1.2f; el.reliability = 0.92f; el.launchCostMUSD = 7.5f; el.leadTimeMonths = 6;
            el.dataConfidence = DataConfidence.Approximate; el.sourceUrl = "https://www.rocketlabusa.com/launch/electron/";
            el.sourceNotes = "Costo aproximado por lanzamiento dedicado. Confiabilidad redondeada del historial de vuelos.";
            Save(el); list.Add(el);

            var sls = GetOrCreate<LaunchVehicleSO>($"{Root}/LaunchVehicles/LV_SLS_Block1.asset");
            sls.vehicleId = "LV_SLS_B1"; sls.displayName = "SLS Block 1"; sls.operatorName = "NASA"; sls.country = "EE.UU.";
            sls.description = "Space Launch System, cohete superpesado del programa Artemis. Artemis I (2022) envió Orion a la Luna.";
            sls.payloadCapacities = new List<OrbitPayloadCapacity>
            {
                Cap(OrbitRegime.LEO, 95000, DataConfidence.Official, "95 t a LEO."),
                Cap(OrbitRegime.MEO, 40000, DataConfidence.GameEstimate, "No es un perfil de misión de SLS; estimación de juego."),
                Cap(OrbitRegime.GEO, 32000, DataConfidence.GameEstimate, "No es un perfil de misión de SLS; estimación de juego."),
                Cap(OrbitRegime.Lunar, 27000, DataConfidence.Official, ">27 t a inyección translunar (TLI).")
            };
            sls.fairingDiameterM = 8.4f; sls.reliability = 0.95f; sls.launchCostMUSD = 2200f; sls.leadTimeMonths = 36;
            sls.dataConfidence = DataConfidence.Approximate; sls.sourceUrl = "https://www.nasa.gov/humans-in-space/space-launch-system/";
            sls.sourceNotes = "Costo de producción y operación por lanzamiento ~$2.2 mil M según auditoría de NASA OIG (2022). Confiabilidad: 1/1 vuelos, valor de juego.";
            Save(sls); list.Add(sls);

            var a6 = GetOrCreate<LaunchVehicleSO>($"{Root}/LaunchVehicles/LV_Ariane64.asset");
            a6.vehicleId = "LV_ARIANE64"; a6.displayName = "Ariane 6 (A64)"; a6.operatorName = "Arianespace / ESA"; a6.country = "Europa";
            a6.description = "Lanzador europeo con 4 propulsores sólidos P120C y etapa superior Vinci reencendible.";
            a6.payloadCapacities = new List<OrbitPayloadCapacity>
            {
                Cap(OrbitRegime.LEO, 21650, DataConfidence.Official, "A64 a LEO."),
                Cap(OrbitRegime.MEO, 7000, DataConfidence.GameEstimate, "Estimación de juego para inserción directa MEO."),
                Cap(OrbitRegime.GEO, 11500, DataConfidence.Official, "A64 a GTO."),
                Cap(OrbitRegime.Lunar, 8600, DataConfidence.Approximate, "Inyección translunar anunciada por ArianeGroup.")
            };
            a6.fairingDiameterM = 5.4f; a6.reliability = 0.95f; a6.launchCostMUSD = 125f; a6.leadTimeMonths = 18;
            a6.dataConfidence = DataConfidence.Approximate; a6.sourceUrl = "https://www.esa.int/Enabling_Support/Space_Transportation/Ariane";
            a6.sourceNotes = "Costo ≈ €115 M convertido a USD (aprox.). Confiabilidad: valor de juego por historial corto.";
            Save(a6); list.Add(a6);

            return list;
        }

        // ─────────────────────────── INSTRUMENTOS ───────────────────────────

        private static List<InstrumentSO> CreateInstruments()
        {
            var list = new List<InstrumentSO>();

            list.Add(Instrument("INS_MODIS", "MODIS (Terra/Aqua)", InstrumentType.MultispectralImager,
                "Espectrorradiómetro de 36 bandas que cubre toda la Tierra cada 1-2 días: incendios, nubes, color del océano.",
                "Terra (1999), Aqua (2002)", mass: 228.7f, cost: 150f, peakW: 225f, avgW: 162.5f, dataGB: 66f, science: 70, months: 48,
                orbits: new[] { OrbitRegime.LEO }, tags: new[] { "clima", "tierra", "oceano", "incendios" },
                conf: DataConfidence.Approximate, url: "https://modis.gsfc.nasa.gov/about/specifications.php",
                notes: "Masa 228.7 kg y potencia media 162.5 W según ficha MODIS; 6.1 Mbps de media orbital ≈ 66 GB/día. Costo: estimación de juego."));

            list.Add(Instrument("INS_ABI", "ABI (GOES-R)", InstrumentType.MultispectralImager,
                "Advanced Baseline Imager: 16 bandas, imagen del disco completo cada 10 min desde GEO para meteorología.",
                "GOES-16/17/18/19", mass: 338f, cost: 300f, peakW: 500f, avgW: 450f, dataGB: 713f, science: 80, months: 60,
                orbits: new[] { OrbitRegime.GEO }, tags: new[] { "clima", "meteorologia", "tormentas" },
                conf: DataConfidence.Approximate, url: "https://www.goes-r.gov/spacesegment/abi.html",
                notes: "WMO OSCAR: 338 kg, 450 W y 66 Mbps (≈713 GB/día). Costo: estimación de juego."));

            list.Add(Instrument("INS_OCO2", "Espectrómetro OCO-2", InstrumentType.Spectrometer,
                "Tres espectrómetros de rejilla de alta resolución que miden CO2 atmosférico con precisión de 1 ppm.",
                "Orbiting Carbon Observatory-2 (2014)", mass: 131f, cost: 120f, peakW: 125f, avgW: 110f, dataGB: 15f, science: 65, months: 42,
                orbits: new[] { OrbitRegime.LEO }, tags: new[] { "clima", "carbono", "atmosfera" },
                conf: DataConfidence.Approximate, url: "https://www.jpl.nasa.gov/news/press_kits/oco2-launch-press-kit.pdf",
                notes: "Kit de prensa JPL: instrumento de 131 kg; ~125 W (Spaceflight101). Volumen de datos y costo: estimación de juego."));

            list.Add(Instrument("INS_NISAR", "Radar de apertura sintética NISAR", InstrumentType.Radar,
                "Radar SAR banda L (NASA) + S (ISRO) con reflector de 12 m: mide deformación del suelo de cm en todo clima, día y noche.",
                "NISAR (NASA-ISRO, 2025)", mass: 1300f, cost: 600f, peakW: 4000f, avgW: 2500f, dataGB: 3250f, science: 95, months: 72,
                orbits: new[] { OrbitRegime.LEO }, tags: new[] { "tierra", "terremotos", "hielo", "agricultura" },
                conf: DataConfidence.GameEstimate, url: "https://nisar.jpl.nasa.gov/",
                notes: "NASA: hasta 26 Tb/día (≈3 250 GB) bajados en banda Ka a 3.5 Gbps. Masa y potencia del radar: estimación de juego."));

            list.Add(Instrument("INS_LROC", "Cámara LROC (LRO)", InstrumentType.Camera,
                "Dos cámaras de ángulo estrecho (0.5 m/píxel) y una gran angular que cartografían la superficie lunar.",
                "Lunar Reconnaissance Orbiter (2009)", mass: 16.4f, cost: 35f, peakW: 30f, avgW: 22f, dataGB: 40f, science: 75, months: 36,
                orbits: new[] { OrbitRegime.Lunar }, tags: new[] { "luna", "superficie", "artemis" },
                conf: DataConfidence.Approximate, url: "https://www.lroc.asu.edu/about",
                notes: "LRO baja ~460 Gb/día en banda Ka; LROC es el mayor generador. Costo: estimación de juego."));

            list.Add(Instrument("INS_GEDI", "Lidar GEDI", InstrumentType.Lidar,
                "Lidar de onda completa de 3 láseres que mide la estructura 3D de los bosques y la biomasa.",
                "GEDI en la ISS (2018)", mass: 519f, cost: 100f, peakW: 450f, avgW: 400f, dataGB: 25f, science: 70, months: 40,
                orbits: new[] { OrbitRegime.LEO }, tags: new[] { "tierra", "bosques", "carbono" },
                conf: DataConfidence.Approximate, url: "https://ntrs.nasa.gov/api/citations/20190030790/downloads/20190030790.pdf",
                notes: "NASA NTRS: 519 kg y 400 W de media en la ISS. Volumen de datos y costo: estimación de juego."));

            // Precisión de apuntamiento requerida (grados): estimaciones de juego por clase de instrumento.
            var pointing = new Dictionary<string, float>
            {
                { "INS_MODIS", 0.05f }, { "INS_ABI", 0.01f }, { "INS_OCO2", 0.05f },
                { "INS_NISAR", 0.05f }, { "INS_LROC", 0.01f }, { "INS_GEDI", 0.01f }
            };
            foreach (var i in list)
            {
                if (pointing.TryGetValue(i.componentId, out float deg)) i.requiredPointingDeg = deg;
                Save(i);
            }
            return list;
        }

        // ─────────────────── DIFICULTAD AVANZADA: C&DH y ADCS ───────────────────

        private static List<OnboardComputerSO> CreateComputers()
        {
            return new List<OnboardComputerSO>
            {
                Computer("CDH_COTS", "Computadora comercial (clase SmallSat)", "Procesador comercial barato y ligero; sensible a la radiación.",
                    storage: 32, radHard: false, mass: 1.5f, power: 5, cost: 0.5f, months: 6),
                Computer("CDH_RAD", "Computadora endurecida (clase RAD750)", "Procesador tolerante a radiación usado en misiones científicas de la NASA.",
                    storage: 256, radHard: true, mass: 6, power: 20, cost: 4, months: 12),
                Computer("CDH_SSR", "Endurecida + grabador de 4 TB", "Computadora endurecida con grabador de estado sólido de gran capacidad para instrumentos de alto volumen.",
                    storage: 4096, radHard: true, mass: 15, power: 40, cost: 12, months: 18)
            };
        }

        private static OnboardComputerSO Computer(string id, string name, string desc, float storage, bool radHard,
            float mass, float power, float cost, float months)
        {
            var c = GetOrCreate<OnboardComputerSO>($"{Root}/Computers/{id}.asset");
            c.componentId = id; c.displayName = name; c.description = desc;
            c.storageGB = storage; c.radiationHardened = radHard;
            c.massKg = mass; c.peakPowerW = power; c.costMUSD = cost; c.developmentMonths = months;
            c.advancedOnly = true; c.socket = SpacecraftSocket.InternalBay; c.dataConfidence = DataConfidence.GameEstimate;
            c.sourceUrl = "https://www.nasa.gov/smallsat-institute/sst-soa/small-spacecraft-avionics/";
            c.sourceNotes = "Valores representativos de la clase; no de un producto concreto.";
            Save(c);
            return c;
        }

        private static List<AttitudeControlSO> CreateAttitudeControls()
        {
            return new List<AttitudeControlSO>
            {
                Adcs("ADCS_MAG", "Magnetopares + sensores solares", "Control pasivo/ligero que usa el campo magnético terrestre. Barato, pero impreciso.",
                    accuracy: 2f, mass: 1, power: 2, cost: 0.3f, months: 4),
                Adcs("ADCS_RW_SUN", "Ruedas de reacción + sensores solares", "Apuntamiento medio, suficiente para cámaras de baja resolución.",
                    accuracy: 0.1f, mass: 10, power: 30, cost: 2, months: 8),
                Adcs("ADCS_RW_STAR", "Ruedas de reacción + rastreador estelar", "Apuntamiento de precisión usando las estrellas como referencia: necesario para alta resolución.",
                    accuracy: 0.01f, mass: 20, power: 60, cost: 6, months: 12)
            };
        }

        private static AttitudeControlSO Adcs(string id, string name, string desc, float accuracy, float mass, float power, float cost, float months)
        {
            var a = GetOrCreate<AttitudeControlSO>($"{Root}/AttitudeControl/{id}.asset");
            a.componentId = id; a.displayName = name; a.description = desc;
            a.pointingAccuracyDeg = accuracy; a.massKg = mass; a.peakPowerW = power; a.costMUSD = cost; a.developmentMonths = months;
            a.advancedOnly = true; a.socket = SpacecraftSocket.InternalBay; a.dataConfidence = DataConfidence.GameEstimate;
            a.sourceUrl = "https://www.nasa.gov/smallsat-institute/sst-soa/guidance-navigation-and-control/";
            a.sourceNotes = "Precisiones típicas por tecnología (estado del arte de pequeños satélites).";
            Save(a);
            return a;
        }

        // ─────────────────── SEGMENTO TERRENO ───────────────────

        private static List<GroundNetworkSO> CreateGroundNetworks()
        {
            var nsn = GetOrCreate<GroundNetworkSO>($"{Root}/GroundNetworks/GND_NSN.asset");
            nsn.networkId = "GND_NSN"; nsn.displayName = "Near Space Network (NASA)";
            nsn.description = "Estaciones terrenas y satélites de retransmisión de la NASA para misiones cercanas a la Tierra.";
            nsn.supportedRegimes = new[] { OrbitRegime.LEO, OrbitRegime.MEO, OrbitRegime.GEO };
            nsn.gainFactor = 1f; nsn.contactHoursMultiplier = 1f; nsn.outageProbabilityMultiplier = 1f; nsn.costPerMonthMUSD = 0.1f;
            nsn.educationalNote = "Una nave en LEO solo ve cada estación ~10 minutos por pasada: las horas de contacto son el cuello de botella.";
            nsn.dataConfidence = DataConfidence.GameEstimate; nsn.sourceUrl = "https://www.nasa.gov/communicating-with-missions/";
            Save(nsn);

            var commercial = GetOrCreate<GroundNetworkSO>($"{Root}/GroundNetworks/GND_COMMERCIAL.asset");
            commercial.networkId = "GND_COMMERCIAL"; commercial.displayName = "Red comercial (pago por pase)";
            commercial.description = "Proveedores privados con muchas antenas repartidas por el mundo: más pases al día, a mayor costo.";
            commercial.supportedRegimes = new[] { OrbitRegime.LEO, OrbitRegime.MEO, OrbitRegime.GEO };
            commercial.gainFactor = 1f; commercial.contactHoursMultiplier = 2f; commercial.outageProbabilityMultiplier = 0.7f; commercial.costPerMonthMUSD = 0.25f;
            commercial.educationalNote = "Duplicar las horas de contacto duplica los datos que bajan sin cambiar la nave: un trade-off de costo de operaciones.";
            commercial.dataConfidence = DataConfidence.GameEstimate; commercial.sourceUrl = "https://www.nasa.gov/communicating-with-missions/";
            Save(commercial);

            var dsn = GetOrCreate<GroundNetworkSO>($"{Root}/GroundNetworks/GND_DSN.asset");
            dsn.networkId = "GND_DSN"; dsn.displayName = "Deep Space Network (antenas de 34 m)";
            dsn.description = "Tres complejos (Goldstone, Madrid, Canberra) separados ~120° para seguir naves lejanas las 24 h.";
            dsn.supportedRegimes = new[] { OrbitRegime.Lunar, OrbitRegime.Interplanetary };
            dsn.gainFactor = 30f; dsn.contactHoursMultiplier = 1f; dsn.outageProbabilityMultiplier = 1.8f; dsn.costPerMonthMUSD = 0.7f;
            dsn.educationalNote = "Sus antenas enormes compensan la distancia, pero la demanda supera su capacidad: es habitual perder turnos.";
            dsn.dataConfidence = DataConfidence.Approximate; dsn.sourceUrl = "https://www.nasa.gov/communicating-with-missions/dsn/";
            Save(dsn);

            return new List<GroundNetworkSO> { nsn, commercial, dsn };
        }

        // ─────────────────── CUBESAT (NASA CubeSat 101) Y SANDBOX ───────────────────

        /// <summary>
        /// Escenario educativo CubeSat con datos de la guía "CubeSat 101" (NASA CSLI): 1U ≈ 1-1.33 kg,
        /// la NASA cubre hasta ~$300 000 de lanzamiento compartido a LEO, operación con estaciones universitarias.
        /// </summary>
        private static void AddCubeSatAndSandbox(NasaDatabaseSO db)
        {
            const string cubesat101 = "https://www.nasa.gov/smallsat-institute/cubesat-101/";

            var b6 = Bus("BUS_CUBESAT_6U", "CubeSat 6U (estructura + aviónica)", "Estándar de 6 unidades (2 × 3U): ~12 kg máximo. Muy barato, muy limitado.",
                "Clase Dellingr / MarCO", dry: 6, prop: 0.5f, isp: 70, maxPayload: 4, maxPower: 30, slots: 1, hk: 8, life: 2, cost: 1.5f, months: 12);
            b6.operationsCostScale = 0.05f; b6.sourceUrl = cubesat101; Save(b6);
            var b12 = Bus("BUS_CUBESAT_12U", "CubeSat 12U (estructura + aviónica)", "Estándar de 12 unidades: ~24 kg máximo, dos ranuras de carga útil.",
                "Clase Lunar Flashlight / CAPSTONE", dry: 11, prop: 1, isp: 220, maxPayload: 9, maxPower: 60, slots: 2, hk: 15, life: 3, cost: 3, months: 15);
            b12.operationsCostScale = 0.08f; b12.sourceUrl = cubesat101; Save(b12);
            db.buses.AddRange(new[] { b6, b12 });

            db.solarArrays.Add(Solar("SOL_CUBESAT", "Alas desplegables CubeSat", "Paneles de triple unión que se despliegan tras la eyección del dispensador.",
                60, 0.6f, 0.15f, 0.03f, 4, SpacecraftSocket.SolarWingLeft));
            db.batteries.Add(Battery("BAT_CUBESAT", "Li-ion 80 Wh (CubeSat)", 80, 0.6f, 0.05f));
            db.antennas.Add(Antenna("ANT_S_CUBESAT", "Banda S CubeSat (parche)", CommBand.S,
                "Transmisor compacto de banda S para CubeSats.", 2f, 2000, 4f, false, 0.2f, 0.08f, 6, 3));

            var cam = Instrument("INS_CUBECAM", "Cámara multiespectral CubeSat", InstrumentType.MultispectralImager,
                "Cámara de 4 bandas tipo constelación comercial: imágenes de cultivos y bosques a ~3-5 m.",
                "Clase CubeSat de observación terrestre", mass: 1.2f, cost: 0.5f, peakW: 12, avgW: 8, dataGB: 0.5f, science: 20, months: 9,
                orbits: new[] { OrbitRegime.LEO }, tags: new[] { "tierra", "agricultura" },
                conf: DataConfidence.GameEstimate, url: "https://www.nasa.gov/smallsat-institute/sst-soa/", notes: "Valores representativos de la clase.");
            cam.requiredPointingDeg = 0.5f; Save(cam);
            var minxss = Instrument("INS_MINXSS", "Espectrómetro de rayos X solares (clase MinXSS)", InstrumentType.Spectrometer,
                "Mide los rayos X suaves de las fulguraciones solares y su efecto en la alta atmósfera.",
                "MinXSS (Univ. de Colorado, 2016)", mass: 1f, cost: 0.8f, peakW: 5, avgW: 4, dataGB: 0.05f, science: 25, months: 12,
                orbits: new[] { OrbitRegime.LEO }, tags: new[] { "sol", "clima_espacial" },
                conf: DataConfidence.GameEstimate, url: "https://lasp.colorado.edu/minxss/", notes: "CubeSat citado como ejemplo en CubeSat 101; valores aproximados.");
            minxss.requiredPointingDeg = 1f; Save(minxss);
            db.instruments.AddRange(new[] { cam, minxss });

            var ride = GetOrCreate<LaunchVehicleSO>($"{Root}/LaunchVehicles/LV_Rideshare.asset");
            ride.vehicleId = "LV_RIDESHARE"; ride.displayName = "Lanzamiento compartido CubeSat (rideshare)"; ride.operatorName = "NASA CSLI / proveedor";
            ride.country = "EE.UU."; ride.description = "Tu CubeSat viaja en un dispensador junto a la carga principal de otro cohete. Barato, pero sin elegir fecha ni órbita.";
            ride.payloadCapacities = new List<OrbitPayloadCapacity> { Cap(OrbitRegime.LEO, 24, DataConfidence.Approximate, "Un dispensador de 12U (~24 kg).") };
            ride.fairingDiameterM = 0f; ride.reliability = 0.95f; ride.launchCostMUSD = 0.3f; ride.leadTimeMonths = 18;
            ride.dataConfidence = DataConfidence.Approximate; ride.sourceUrl = cubesat101;
            ride.sourceNotes = "CubeSat 101: la NASA cubre hasta ~$300 000 de lanzamiento, típicamente suficiente para un 3U a LEO; el manifiesto puede tardar de 1 a 36 meses.";
            Save(ride);
            db.launchVehicles.Add(ride);

            var uni = GetOrCreate<GroundNetworkSO>($"{Root}/GroundNetworks/GND_UNIVERSITY.asset");
            uni.networkId = "GND_UNIVERSITY"; uni.displayName = "Estación universitaria (radioaficionados)";
            uni.description = "Antena propia en el campus, con apoyo de la comunidad de radioaficionados.";
            uni.supportedRegimes = new[] { OrbitRegime.LEO };
            uni.gainFactor = 0.3f; uni.contactHoursMultiplier = 0.6f; uni.outageProbabilityMultiplier = 1.3f; uni.costPerMonthMUSD = 0.005f;
            uni.educationalNote = "CubeSat 101: \"lanzar con una estación terrena inadecuada es un asesino de misiones\". Pruébala temprano y a menudo.";
            uni.dataConfidence = DataConfidence.GameEstimate; uni.sourceUrl = cubesat101;
            Save(uni);
            db.groundNetworks.Add(uni);

            db.scenarios.Add(Scenario("SCN_CUBESAT_EDU", "CubeSat universitario",
                "Tu universidad tiene un hueco en un lanzamiento compartido. Con muy poco presupuesto, ¿cuánta ciencia puedes devolver? Ojo con el enlace de datos.",
                "sol", budget: 9, devMonths: 30, lifeMonths: 12, minScience: 20, orbits: new[] { OrbitRegime.LEO }));
            var sandbox = Scenario("SCN_SANDBOX", "Sandbox: laboratorio libre",
                "Sin límites de presupuesto, calendario ni órbita. Experimenta con cualquier combinación y compara resultados.",
                "", budget: 0, devMonths: 0, lifeMonths: 60, minScience: 0, orbits: new OrbitRegime[0]);
            sandbox.isSandbox = true; Save(sandbox);
            db.scenarios.Add(sandbox);

            db.realMissions.Add(Real("REAL_MINXSS", "MinXSS-1", 2016, OrbitRegime.LEO, new[] { "INS_MINXSS" }, new[] { "sol", "clima_espacial" }, 4,
                "CubeSat 3U de la Universidad de Colorado desplegado desde la ISS para estudiar los rayos X del Sol.", "https://lasp.colorado.edu/minxss/"));
        }

        // ─────────────────── MISIÓN TRIPULADA: SOBREVUELO LUNAR (tipo Artemis II) ───────────────────

        /// <summary>
        /// Sobrevuelo lunar tripulado con retorno libre. La trayectoria se calcula con FreeReturnTrajectory (CR3BP).
        /// Datos de la campaña fotográfica: ~10 000 fotos, 32 cámaras en Orion, cámaras Nikon D5/Z9 (80-400 mm y 35 mm),
        /// parte enviada por enlace láser y el resto recuperado en tarjetas SD tras el amerizaje (material del equipo).
        /// </summary>
        private static void AddCrewedArtemis(NasaDatabaseSO db)
        {
            const string artemis = "https://www.nasa.gov/mission/artemis-ii/";

            var fr = GetOrCreate<OrbitSO>($"{Root}/Orbits/ORB_FREE_RETURN.asset");
            fr.orbitId = "ORB_FREE_RETURN"; fr.displayName = "Sobrevuelo lunar con retorno libre (tipo Artemis II)"; fr.regime = OrbitRegime.LunarFlyby;
            fr.description = "Trayectoria en forma de 8: tras la inyección translunar la nave rodea la cara oculta de la Luna y su gravedad la devuelve a la Tierra sin motor.";
            fr.altitudeKm = 7400; fr.inclinationDeg = 0; fr.orbitalPeriodMinutes = 0; fr.requiredDeltaVKmS = 12.6f; fr.onboardDeltaVKmS = 0.1f;
            fr.sunDistanceAU = 1f; fr.eclipseFraction = 0.02f; fr.maxEclipseMinutes = 60;
            fr.groundStationRangeKm = 300000; fr.dailyContactHours = 20f;
            fr.radiationLevel = 0.85f; fr.debrisRisk = 0.03f;
            fr.educationalNote = "El retorno libre es una red de seguridad: si falla el motor, la gravedad lunar trae a la tripulación de vuelta (así se salvó Apolo 13). La trayectoria del juego se calcula con el problema de tres cuerpos Tierra-Luna.";
            fr.dataConfidence = DataConfidence.Approximate; fr.sourceUrl = artemis;
            Save(fr);
            if (!db.orbits.Contains(fr)) db.orbits.Add(fr);

            // Capacidad de inyección translunar de cada lanzador (la misma que hacia la órbita lunar).
            foreach (var lv in db.launchVehicles)
            {
                if (lv.payloadCapacities.Exists(c => c.regime == OrbitRegime.LunarFlyby)) continue;
                var lunar = lv.payloadCapacities.Find(c => c.regime == OrbitRegime.Lunar);
                lv.payloadCapacities.Add(Cap(OrbitRegime.LunarFlyby, lunar.payloadKg, lunar.confidence, "Inyección translunar (TLI), igual que hacia la órbita lunar."));
                Save(lv);
            }

            var orion = Bus("BUS_ORION", "Orion (cápsula + módulo de servicio europeo)",
                "Cápsula para 4 astronautas con soporte vital, escudo térmico y sistema de aborto; el módulo de servicio de la ESA aporta motor, energía y agua.",
                "Orion / ESM (Artemis I-II)", dry: 17900, prop: 8600, isp: 316, maxPayload: 300, maxPower: 3000, slots: 4, hk: 5500, life: 0.06f, cost: 1000, months: 48);
            orion.crewCapable = true; orion.recoverableStorageGB = 1000; orion.operationsCostScale = 20f;
            orion.dataConfidence = DataConfidence.Approximate; orion.sourceUrl = artemis;
            orion.sourceNotes = "Masa en TLI ~26,5 t (aprox.). Costo de producción ~$1 000 M (orden de magnitud de auditorías de la NASA). 1 TB de tarjetas SD y memoria recuperable: estimación de juego.";
            Save(orion);
            if (!db.buses.Contains(orion)) db.buses.Add(orion);

            var esm = Solar("SOL_ESM", "Alas del módulo de servicio europeo (4 alas)", "Cuatro alas solares de ~7 m que entregan unos 11 kW; incluidas en la masa del módulo de servicio.",
                11200, 1, 1, 0.02f, 0, SpacecraftSocket.SolarWingLeft);
            esm.dataConfidence = DataConfidence.Approximate; esm.sourceUrl = "https://www.esa.int/Science_Exploration/Human_and_Robotic_Exploration/Orion";
            // Masa y costo incluidos en Orion: fuera de Orion serían 11 kW por 1 kg y $1 M.
            esm.requiredBusId = orion.componentId; esm.requiredBusName = orion.displayName; Save(esm);
            if (!db.solarArrays.Contains(esm)) db.solarArrays.Add(esm);

            var o2o = Antenna("ANT_O2O", "Enlace láser O2O (comunicación óptica)", CommBand.Optical,
                "Terminal láser de Orion: video y fotos en alta definición casi en tiempo real hacia estaciones ópticas terrestres.", 260f, 384400, 260f, false, 30, 40, 100, 24);
            o2o.dataConfidence = DataConfidence.Approximate; o2o.sourceUrl = "https://www.nasa.gov/communicating-with-missions/";
            o2o.sourceNotes = "Hasta ~260 Mbps de bajada (aprox.). Las nubes sobre la estación cortan el enlace.";
            Save(o2o);
            var orionS = Antenna("ANT_ORION_S", "Banda S de Orion (vía DSN)", CommBand.S,
                "Enlace de voz, telemetría y comandos con la Red del Espacio Profundo.", 0.1f, 384400, 6f, true, 10, 5, 60, 12);
            foreach (var a in new[] { o2o, orionS }) if (!db.antennas.Contains(a)) db.antennas.Add(a);

            var optical = GetOrCreate<GroundNetworkSO>($"{Root}/GroundNetworks/GND_OPTICAL.asset");
            optical.networkId = "GND_OPTICAL"; optical.displayName = "Estaciones ópticas terrestres (láser)";
            optical.description = "Telescopios en tierra que reciben el enlace láser de Orion; necesitan cielo despejado.";
            optical.supportedRegimes = new[] { OrbitRegime.Lunar, OrbitRegime.LunarFlyby };
            optical.gainFactor = 1f; optical.contactHoursMultiplier = 0.2f; optical.outageProbabilityMultiplier = 1.5f; optical.costPerMonthMUSD = 0.3f;
            optical.educationalNote = "La luz láser lleva muchísimos más datos que la radio, pero las nubes la bloquean: por eso parte de las fotos de Artemis II volvió en tarjetas SD.";
            optical.dataConfidence = DataConfidence.GameEstimate; optical.sourceUrl = "https://www.nasa.gov/communicating-with-missions/";
            Save(optical);
            if (!db.groundNetworks.Contains(optical)) db.groundNetworks.Add(optical);
            foreach (var n in db.groundNetworks)
                if (n.networkId == "GND_DSN" && !n.Supports(OrbitRegime.LunarFlyby))
                { n.supportedRegimes = new[] { OrbitRegime.Lunar, OrbitRegime.Interplanetary, OrbitRegime.LunarFlyby }; Save(n); }

            var cams = Instrument("INS_ORION_CAMERAS", "32 cámaras de Orion (exteriores y cabina)", InstrumentType.Camera,
                "Cámaras fijas en el exterior, en las alas solares y en la cabina: documentan la nave y el sobrevuelo en foto y video.",
                "Orion, Artemis II", mass: 40, cost: 15, peakW: 300, avgW: 250, dataGB: 600, science: 60, months: 24,
                orbits: new[] { OrbitRegime.LunarFlyby }, tags: new[] { "luna", "artemis", "tripulada" },
                conf: DataConfidence.Approximate, url: artemis, notes: "32 cámaras según el material de la misión; volumen de datos y masa: estimación de juego.");
            cams.requiredPointingDeg = 0.5f; Save(cams);
            var nikon = Instrument("INS_CREW_CAMERAS", "Cámaras de la tripulación (Nikon D5/Z9)", InstrumentType.Camera,
                "Cámaras manuales con lentes de 80-400 mm y 35 mm: la tripulación tomó ~10 000 fotos durante el sobrevuelo.",
                "Artemis II", mass: 6, cost: 0.1f, peakW: 20, avgW: 10, dataGB: 120, science: 40, months: 6,
                orbits: new[] { OrbitRegime.LunarFlyby }, tags: new[] { "luna", "artemis", "tripulada" },
                conf: DataConfidence.Approximate, url: artemis, notes: "~10 000 fotos × ~50 MB en unos 4 días ≈ 120 GB/día (aprox.).");
            nikon.requiredPointingDeg = 1f; Save(nikon);
            var atenea = Instrument("INS_ATENEA", "CubeSat ATENEA (CONAE, carga secundaria)", InstrumentType.Radiometer,
                "Microsatélite argentino que viajó en el adaptador de etapa de Orion: mide radiación y prueba comunicaciones en órbita alta.",
                "Carga útil secundaria de Artemis II (Argentina)", mass: 12, cost: 2, peakW: 0, avgW: 0, dataGB: 0.5f, science: 15, months: 24,
                orbits: new[] { OrbitRegime.LunarFlyby }, tags: new[] { "luna", "radiacion", "cubesat" },
                conf: DataConfidence.GameEstimate, url: "https://www.argentina.gob.ar/ciencia/conae", notes: "Viaja en el adaptador (no consume energía de Orion). Valores aproximados.");
            atenea.requiredPointingDeg = 2f; Save(atenea);
            // Las cámaras de Orion y las de la tripulación viajan en la cápsula: no se montan en satélites.
            foreach (var i in new[] { cams, nikon }) { i.requiredBusId = orion.componentId; i.requiredBusName = orion.displayName; Save(i); }
            foreach (var i in new[] { cams, nikon, atenea }) if (!db.instruments.Contains(i)) db.instruments.Add(i);

            var cdh = Computer("CDH_ORION", "Computadoras de vuelo y memoria de Orion", "Computadoras redundantes endurecidas y memoria para guardar fotos y video que vuelven a la Tierra.",
                storage: 4000, radHard: true, mass: 50, power: 200, cost: 20, months: 36);
            if (!db.computers.Contains(cdh)) db.computers.Add(cdh);

            var scn = Scenario("SCN_ARTEMIS_FLYBY", "Sobrevuelo lunar tripulado (tipo Artemis II)",
                "Lleva a 4 astronautas alrededor de la Luna y de vuelta a casa. Diseña el lanzamiento, la nave y las comunicaciones: " +
                "las fotos que el enlace no alcance a enviar volverán en las tarjetas SD... si la cápsula regresa. " +
                "Duración como la real: ~9 días.",
                "luna", budget: 4500, devMonths: 72, lifeMonths: MissionSimulator.ArtemisIIMissionDays / MissionSimulator.DaysPerMonth,
                minScience: 80, orbits: new[] { OrbitRegime.LunarFlyby });
            scn.crewed = true; scn.crewSize = 4; Save(scn);
            if (!db.scenarios.Contains(scn)) db.scenarios.Add(scn);

            var real = Real("REAL_ARTEMIS2", "Artemis II", 2026, OrbitRegime.LunarFlyby, new[] { "INS_ORION_CAMERAS", "INS_CREW_CAMERAS" },
                new[] { "luna", "artemis", "tripulada" }, 26500,
                "Primera misión tripulada del programa Artemis: 4 astronautas rodearon la Luna en Orion y amerizaron en el Pacífico.", artemis);
            if (!db.realMissions.Contains(real)) db.realMissions.Add(real);

            // Eventos: fuera de la magnetosfera la tripulación está más expuesta a tormentas solares.
            foreach (var e in db.randomEvents)
            {
                if (e.orbitModifiers.Exists(m => m.regime == OrbitRegime.LunarFlyby)) continue;
                float mult = e.eventType == RandomEventType.SolarStorm ? 1.5f : e.eventType == RandomEventType.MicrometeoroidImpact ? 0.6f
                    : e.eventType == RandomEventType.SingleEventUpset ? 1.3f : 1f;
                e.orbitModifiers.Add(new OrbitProbabilityModifier { regime = OrbitRegime.LunarFlyby, multiplier = mult });
                Save(e);
            }
        }

        // ─────────────────── MISIONES REALES DE REFERENCIA ───────────────────

        private static List<RealMissionSO> CreateRealMissions()
        {
            return new List<RealMissionSO>
            {
                Real("REAL_TERRA", "Terra", 1999, OrbitRegime.LEO, new[] { "INS_MODIS" }, new[] { "clima", "tierra", "incendios" }, 4864,
                    "Buque insignia del Earth Observing System: 5 instrumentos, entre ellos MODIS, observando toda la Tierra.", "https://terra.nasa.gov/"),
                Real("REAL_OCO2", "OCO-2", 2014, OrbitRegime.LEO, new[] { "INS_OCO2" }, new[] { "carbono", "clima", "atmosfera" }, 454,
                    "Mide el CO2 atmosférico desde órbita heliosíncrona con tres espectrómetros.", "https://ocov2.jpl.nasa.gov/"),
                Real("REAL_NISAR", "NISAR", 2025, OrbitRegime.LEO, new[] { "INS_NISAR" }, new[] { "terremotos", "hielo", "tierra" }, 2800,
                    "Radar NASA-ISRO que mide deformaciones del suelo de centímetros y genera terabytes al día.", "https://nisar.jpl.nasa.gov/"),
                Real("REAL_GEDI", "GEDI (en la ISS)", 2018, OrbitRegime.LEO, new[] { "INS_GEDI" }, new[] { "bosques", "carbono" }, 520,
                    "Lidar instalado en la Estación Espacial que mide la estructura 3D de los bosques.", "https://gedi.umd.edu/"),
                Real("REAL_GOES16", "GOES-16", 2016, OrbitRegime.GEO, new[] { "INS_ABI" }, new[] { "meteorologia", "tormentas", "clima" }, 5192,
                    "Satélite meteorológico geoestacionario NOAA/NASA: imágenes del hemisferio cada 10 minutos.", "https://www.goes-r.gov/"),
                Real("REAL_LRO", "Lunar Reconnaissance Orbiter", 2009, OrbitRegime.Lunar, new[] { "INS_LROC" }, new[] { "luna", "superficie", "artemis" }, 1916,
                    "Cartografía la Luna desde 2009; sus mapas eligen los sitios de alunizaje de Artemis.", "https://science.nasa.gov/mission/lro/"),
                Real("REAL_CAPSTONE", "CAPSTONE", 2022, OrbitRegime.Lunar, new string[0], new[] { "luna", "artemis" }, 25,
                    "CubeSat de 25 kg lanzado por Electron que probó la órbita lunar de la futura estación Gateway.", "https://www.nasa.gov/mission/capstone/")
            };
        }

        private static RealMissionSO Real(string id, string name, int year, OrbitRegime regime, string[] instruments, string[] tags,
            float mass, string desc, string url)
        {
            var m = GetOrCreate<RealMissionSO>($"{Root}/RealMissions/{id}.asset");
            m.missionId = id; m.displayName = name; m.launchYear = year; m.regime = regime;
            m.instrumentIds = instruments; m.tags = tags; m.massKg = mass; m.description = desc; m.sourceUrl = url;
            Save(m);
            return m;
        }

        /// <summary>
        /// Cámaras de exploración: si la misión llega a la Luna toman fotografías que aparecen en el informe de exploración.
        /// Incluye un kit genérico y ligero que cabe en cualquier nave (también CubeSats).
        /// </summary>
        private static void AddExplorationCameras(NasaDatabaseSO db)
        {
            var kit = Instrument("INS_EXPLORER_CAMS", "Kit de cámaras de exploración (4 cámaras)", InstrumentType.Camera,
                "Cuatro cámaras compactas (gran angular, 35 mm, teleobjetivo y cámara de ingeniería) para documentar el viaje. " +
                "Si la nave llega a la Luna, sus fotografías aparecen en el informe de la exploración.",
                "Clase cámaras de ingeniería de naves pequeñas", mass: 3, cost: 1.5f, peakW: 30, avgW: 18, dataGB: 40, science: 20, months: 6,
                orbits: new[] { OrbitRegime.Lunar, OrbitRegime.LunarFlyby }, tags: new[] { "luna", "fotografia", "divulgacion" },
                conf: DataConfidence.GameEstimate, url: "https://www.nasa.gov/smallsat-institute/sst-soa/",
                notes: "Valores de juego representativos de cámaras compactas para naves pequeñas.");
            kit.requiredPointingDeg = 2f;
            Save(kit);
            if (!db.instruments.Contains(kit)) db.instruments.Add(kit);

            var perDay = new Dictionary<string, int>
            {
                { "INS_ORION_CAMERAS", 6500 },   // ~10 000 fotos en total junto con las de la tripulación (Artemis II)
                { "INS_CREW_CAMERAS", 3500 },
                { "INS_EXPLORER_CAMS", 1800 },
                { "INS_LROC", 450 },
                { "INS_CUBECAM", 300 },
            };
            foreach (var i in db.instruments)
            {
                bool camera = perDay.TryGetValue(i.componentId, out int n);
                if (i.isCamera == camera && i.photosPerDay == n) continue;
                i.isCamera = camera;
                i.photosPerDay = n;
                Save(i);
            }
        }

        private static InstrumentSO Instrument(string id, string name, InstrumentType type, string desc, string heritage,
            float mass, float cost, float peakW, float avgW, float dataGB, int science, float months,
            OrbitRegime[] orbits, string[] tags, DataConfidence conf, string url, string notes)
        {
            var i = GetOrCreate<InstrumentSO>($"{Root}/Instruments/{id}.asset");
            i.componentId = id; i.displayName = name; i.instrumentType = type; i.description = desc; i.heritage = heritage;
            i.massKg = mass; i.costMUSD = cost; i.peakPowerW = peakW; i.averagePowerW = avgW; i.dutyCycle = 1f;
            i.dataVolumeGBPerDay = dataGB; i.scienceValue = science; i.developmentMonths = months; i.technologyReadinessLevel = 9;
            i.preferredOrbits = orbits; i.scienceObjectives = tags; i.socket = SpacecraftSocket.TopPayloadSocket;
            i.dataConfidence = conf; i.sourceUrl = url; i.sourceNotes = notes;
            Save(i);
            return i;
        }

        // ─────────────────────── HARDWARE GENÉRICO (clase) ───────────────────────

        private static List<SpacecraftBusSO> CreateBuses()
        {
            return new List<SpacecraftBusSO>
            {
                Bus("BUS_SMALL", "Bus pequeño (clase ESPA ~180 kg)", "Plataforma tipo ESPA para misiones de bajo costo.", "Clase ESPA / SmallSat",
                    dry: 90, prop: 20, isp: 220, maxPayload: 60, maxPower: 150, slots: 2, hk: 60, life: 3, cost: 25, months: 18),
                Bus("BUS_MEDIUM", "Bus mediano (clase LEOStar-2)", "Plataforma probada en misiones científicas medianas de NASA.", "Clase LEOStar-2 (OCO-2, SORCE)",
                    dry: 400, prop: 200, isp: 300, maxPayload: 400, maxPower: 800, slots: 3, hk: 250, life: 5, cost: 90, months: 30),
                Bus("BUS_LARGE", "Bus pesado (clase A2100 / LS-1300)", "Plataforma de gran potencia para GEO o cargas pesadas.", "Clase A2100 (GOES-R)",
                    dry: 1800, prop: 2000, isp: 315, maxPayload: 1500, maxPower: 5000, slots: 4, hk: 1200, life: 15, cost: 250, months: 36)
            };
        }

        private static SpacecraftBusSO Bus(string id, string name, string desc, string heritage, float dry, float prop, float isp,
            float maxPayload, float maxPower, int slots, float hk, float life, float cost, float months)
        {
            var b = GetOrCreate<SpacecraftBusSO>($"{Root}/Buses/{id}.asset");
            b.componentId = id; b.displayName = name; b.description = desc; b.heritage = heritage;
            b.massKg = dry; b.propellantMassKg = prop; b.propulsionIspSeconds = isp; b.maxPayloadMassKg = maxPayload; b.maxPayloadPowerW = maxPower;
            b.payloadSlots = slots; b.housekeepingPowerW = hk; b.peakPowerW = hk; b.housekeepingDataGBPerDay = 0.05f;
            b.designLifeYears = life; b.costMUSD = cost; b.developmentMonths = months; b.technologyReadinessLevel = 9;
            b.socket = SpacecraftSocket.CoreBus; b.dataConfidence = DataConfidence.GameEstimate;
            b.sourceNotes = "Valores representativos de la clase de plataforma, no de un modelo comercial específico.";
            Save(b);
            return b;
        }

        private static List<SolarArraySO> CreateSolarArrays()
        {
            return new List<SolarArraySO>
            {
                Solar("SOL_BODY", "Paneles fijos al cuerpo", "Celdas de triple unión montadas sobre la estructura: simples y baratas.", 300, 15, 2, 0.03f, 6, SpacecraftSocket.InternalBay),
                Solar("SOL_WINGS", "Alas desplegables rígidas (par)", "Dos alas con orientación al Sol (SADA), estándar en misiones medianas.", 2500, 60, 12, 0.025f, 12, SpacecraftSocket.SolarWingLeft),
                Solar("SOL_ROSA", "ROSA enrollables (par)", "Roll-Out Solar Array: alas flexibles de alta potencia por kg, probadas en la ISS (iROSA).", 8000, 110, 35, 0.02f, 18, SpacecraftSocket.SolarWingLeft)
            };
        }

        private static SolarArraySO Solar(string id, string name, string desc, float p1au, float mass, float cost, float deg, float months, SpacecraftSocket socket)
        {
            var s = GetOrCreate<SolarArraySO>($"{Root}/SolarArrays/{id}.asset");
            s.componentId = id; s.displayName = name; s.description = desc;
            s.powerAt1AU_W = p1au; s.massKg = mass; s.costMUSD = cost; s.annualDegradation = deg; s.developmentMonths = months;
            s.peakPowerW = 0; s.socket = socket; s.dataConfidence = DataConfidence.GameEstimate;
            s.sourceUrl = "https://www.nasa.gov/smallsat-institute/sst-soa/power-subsystems/";
            s.sourceNotes = "Densidades de potencia coherentes con el estado del arte (NASA SmallSat SOA, Power).";
            Save(s);
            return s;
        }

        private static List<BatterySO> CreateBatteries()
        {
            return new List<BatterySO>
            {
                Battery("BAT_500", "Li-ion 500 Wh", 500, 5, 1),
                Battery("BAT_2000", "Li-ion 2 000 Wh", 2000, 16, 3),
                Battery("BAT_8000", "Li-ion 8 000 Wh", 8000, 60, 8)
            };
        }

        private static BatterySO Battery(string id, string name, float wh, float mass, float cost)
        {
            var b = GetOrCreate<BatterySO>($"{Root}/Batteries/{id}.asset");
            b.componentId = id; b.displayName = name; b.description = "Paquete de celdas Li-ion con electrónica de control (~130-150 Wh/kg a nivel de paquete).";
            b.capacityWh = wh; b.maxDepthOfDischarge = 0.6f; b.massKg = mass; b.costMUSD = cost; b.developmentMonths = 4;
            b.socket = SpacecraftSocket.InternalBay; b.dataConfidence = DataConfidence.GameEstimate;
            b.sourceUrl = "https://www.nasa.gov/smallsat-institute/sst-soa/power-subsystems/";
            Save(b);
            return b;
        }

        private static List<AntennaSO> CreateAntennas()
        {
            return new List<AntennaSO>
            {
                Antenna("ANT_UHF", "UHF omnidireccional", CommBand.UHF, "Antena de látigo para telemetría básica.", 0.0096f, 1000, 0.0096f, false, 1, 0.3f, 5, 2),
                Antenna("ANT_S", "Banda S (parche)", CommBand.S, "Enlace de telemetría y comandos de baja tasa, robusto y omnidireccional.", 2f, 2000, 2f, true, 2, 1.5f, 15, 4),
                Antenna("ANT_X", "Banda X de alta tasa", CommBand.X, "Enlace de ciencia tipo Landsat hacia estaciones de la Near Space Network.", 384f, 2000, 800f, true, 10, 8, 60, 8),
                Antenna("ANT_KA", "Banda Ka HGA orientable 1 m", CommBand.Ka, "Antena de alta ganancia con cardán, tipo LRO/GOES-R: muy alta tasa a gran distancia.", 150f, 40000, 3500f, true, 35, 25, 150, 14)
            };
        }

        private static AntennaSO Antenna(string id, string name, CommBand band, string desc, float refRate, float refRange,
            float maxRate, bool dsn, float mass, float cost, float power, float months)
        {
            var a = GetOrCreate<AntennaSO>($"{Root}/Antennas/{id}.asset");
            a.componentId = id; a.displayName = name; a.band = band; a.description = desc;
            a.referenceDataRateMbps = refRate; a.referenceRangeKm = refRange; a.maxDataRateMbps = maxRate; a.dsnCompatible = dsn;
            a.massKg = mass; a.costMUSD = cost; a.peakPowerW = power; a.developmentMonths = months;
            a.socket = SpacecraftSocket.AntennaSocket; a.dataConfidence = DataConfidence.GameEstimate;
            a.sourceUrl = "https://www.nasa.gov/smallsat-institute/sst-soa/soa-communications/";
            a.sourceNotes = "Modelo simplificado: tasa ∝ 1/r² desde la distancia de referencia, limitada por el módem.";
            Save(a);
            return a;
        }

        // ───────────────────────── EVENTOS ALEATORIOS ─────────────────────────

        private static List<RandomEventSO> CreateRandomEvents()
        {
            var list = new List<RandomEventSO>();

            var storm = GetOrCreate<RandomEventSO>($"{Root}/Events/EVT_SolarStorm.asset");
            storm.eventId = "EVT_SOLAR_STORM"; storm.displayName = "Tormenta solar (CME + evento de protones)"; storm.eventType = RandomEventType.SolarStorm;
            storm.description = "Una eyección de masa coronal alcanza la nave. Los protones energéticos degradan las celdas solares y exigen modo seguro.";
            storm.isPositive = false; storm.baseProbability = 0.35f; storm.affectedPhase = MissionPhase.ScienceOperations;
            storm.orbitModifiers = Mods((OrbitRegime.LEO, 0.6f), (OrbitRegime.MEO, 1.2f), (OrbitRegime.GEO, 1.0f), (OrbitRegime.Lunar, 1.5f));
            storm.scienceMultiplier = 0.85f; storm.dataGenerationMultiplier = 0.8f; storm.commsMultiplier = 0.9f;
            storm.permanentPowerLossFraction = 0.03f; storm.componentFailureChance = 0.05f;
            storm.requiresSafeMode = true; storm.safeModeHours = 24;
            storm.mitigationAdvice = "Dimensiona la batería para sobrevivir 24 h con consumo mínimo y añade margen de potencia del 20-30%.";
            storm.educationalFact = "La NOAA clasifica las tormentas de radiación de S1 a S5. Fuera de la magnetosfera (Luna) el riesgo es mayor.";
            storm.dataConfidence = DataConfidence.Approximate; storm.sourceUrl = "https://www.swpc.noaa.gov/noaa-scales-explanation";
            Save(storm); list.Add(storm);

            var mmod = GetOrCreate<RandomEventSO>($"{Root}/Events/EVT_Micrometeoroid.asset");
            mmod.eventId = "EVT_MMOD"; mmod.displayName = "Impacto de micrometeoroide / desecho orbital"; mmod.eventType = RandomEventType.MicrometeoroidImpact;
            mmod.description = "Una partícula de milímetros a ~10 km/s perfora un panel solar o un radiador.";
            mmod.isPositive = false; mmod.baseProbability = 0.15f; mmod.affectedPhase = MissionPhase.ScienceOperations;
            mmod.orbitModifiers = Mods((OrbitRegime.LEO, 2.0f), (OrbitRegime.MEO, 0.5f), (OrbitRegime.GEO, 0.7f), (OrbitRegime.Lunar, 0.6f));
            mmod.scienceMultiplier = 0.95f; mmod.permanentPowerLossFraction = 0.10f; mmod.componentFailureChance = 0.25f;
            mmod.mitigationAdvice = "Usa blindaje Whipple, redundancia de cadenas de paneles y margen de potencia.";
            mmod.educationalFact = "El modelo ORDEM de la NASA estima el flujo de desechos; LEO entre 700 y 1 000 km es la zona más poblada.";
            mmod.dataConfidence = DataConfidence.Approximate; mmod.sourceUrl = "https://orbitaldebris.jsc.nasa.gov/modeling/ordem.html";
            Save(mmod); list.Add(mmod);

            var window = GetOrCreate<RandomEventSO>($"{Root}/Events/EVT_OptimalWindow.asset");
            window.eventId = "EVT_OPTIMAL_WINDOW"; window.displayName = "Ventana de observación óptima"; window.eventType = RandomEventType.OptimalObservationWindow;
            window.description = "Cielo despejado y geometría solar ideal: los instrumentos capturan datos excepcionales... y muchos más datos.";
            window.isPositive = true; window.baseProbability = 0.30f; window.affectedPhase = MissionPhase.ScienceOperations;
            window.orbitModifiers = Mods((OrbitRegime.LEO, 1.2f), (OrbitRegime.GEO, 1.0f), (OrbitRegime.Lunar, 1.0f), (OrbitRegime.MEO, 0.5f));
            window.scienceMultiplier = 1.25f; window.dataGenerationMultiplier = 1.2f;
            window.mitigationAdvice = "Aprovecharla requiere margen en el enlace de bajada: más datos generados que no se transmiten se pierden.";
            window.educationalFact = "Las misiones heliosíncronas eligen la hora de cruce (ej. 10:30 a. m.) para minimizar nubes y sombras.";
            window.dataConfidence = DataConfidence.GameEstimate; window.sourceUrl = "https://landsat.gsfc.nasa.gov/satellites/landsat-9/";
            Save(window); list.Add(window);

            var dsn = GetOrCreate<RandomEventSO>($"{Root}/Events/EVT_GroundOutage.asset");
            dsn.eventId = "EVT_GROUND_OUTAGE"; dsn.displayName = "Conflicto de agenda DSN / falla de estación terrena"; dsn.eventType = RandomEventType.GroundStationOutage;
            dsn.description = "Una misión prioritaria ocupa las antenas o una estación queda fuera de servicio: se reduce el tiempo de contacto.";
            dsn.isPositive = false; dsn.baseProbability = 0.20f; dsn.affectedPhase = MissionPhase.DataTransmission;
            dsn.orbitModifiers = Mods((OrbitRegime.LEO, 0.8f), (OrbitRegime.MEO, 1.0f), (OrbitRegime.GEO, 0.5f), (OrbitRegime.Lunar, 1.0f));
            dsn.commsMultiplier = 0.6f;
            dsn.mitigationAdvice = "Añade memoria a bordo y margen de enlace; reserva más horas de contacto o una antena de mayor ganancia.";
            dsn.educationalFact = "La DSN tiene complejos en Goldstone, Madrid y Canberra, separados ~120° para cubrir el cielo las 24 h; su demanda supera su capacidad.";
            dsn.dataConfidence = DataConfidence.Approximate; dsn.sourceUrl = "https://www.nasa.gov/communicating-with-missions/dsn/";
            Save(dsn); list.Add(dsn);

            var seu = GetOrCreate<RandomEventSO>($"{Root}/Events/EVT_SingleEventUpset.asset");
            seu.eventId = "EVT_SEU"; seu.displayName = "Single Event Upset (radiación)"; seu.eventType = RandomEventType.SingleEventUpset;
            seu.description = "Un ion pesado invierte un bit en la computadora de vuelo; el satélite se reinicia en modo seguro.";
            seu.isPositive = false; seu.baseProbability = 0.20f; seu.affectedPhase = MissionPhase.ScienceOperations;
            seu.orbitModifiers = Mods((OrbitRegime.LEO, 0.7f), (OrbitRegime.MEO, 2.0f), (OrbitRegime.GEO, 1.2f), (OrbitRegime.Lunar, 1.3f));
            seu.dataGenerationMultiplier = 0.9f; seu.scienceMultiplier = 0.95f;
            seu.requiresSafeMode = true; seu.safeModeHours = 6;
            seu.mitigationAdvice = "Electrónica endurecida, memoria con EDAC y watchdogs. La Anomalía del Atlántico Sur concentra eventos en LEO.";
            seu.educationalFact = "Los cinturones de Van Allen atrapan protones y electrones: MEO los atraviesa de lleno.";
            seu.dataConfidence = DataConfidence.Approximate; seu.sourceUrl = "https://nepp.nasa.gov/";
            Save(seu); list.Add(seu);

            return list;
        }

        // ───────────────────────────── ESCENARIOS ─────────────────────────────

        private static List<MissionScenarioSO> CreateScenarios()
        {
            return new List<MissionScenarioSO>
            {
                Scenario("SCN_EARTH_CLIMATE", "Vigilancia climática de la Tierra",
                    "El Consejo de Ciencias de la Tierra necesita datos globales de nubes, incendios y carbono. Diseña un observatorio en órbita baja.",
                    "clima", budget: 600, devMonths: 60, lifeMonths: 60, minScience: 120, orbits: new[] { OrbitRegime.LEO }),
                Scenario("SCN_GEO_WEATHER", "Centinela meteorológico geoestacionario",
                    "Vigila huracanes en tiempo real sobre las Américas. El enlace de datos 24/7 es crítico.",
                    "meteorologia", budget: 1200, devMonths: 72, lifeMonths: 120, minScience: 80, orbits: new[] { OrbitRegime.GEO }),
                Scenario("SCN_LUNAR_RECON", "Reconocimiento lunar para Artemis",
                    "Cartografía zonas de alunizaje en el polo sur lunar. La distancia exige la DSN.",
                    "luna", budget: 800, devMonths: 48, lifeMonths: 24, minScience: 75, orbits: new[] { OrbitRegime.Lunar })
            };
        }

        private static MissionScenarioSO Scenario(string id, string name, string briefing, string tag, float budget,
            float devMonths, float lifeMonths, int minScience, OrbitRegime[] orbits)
        {
            var s = GetOrCreate<MissionScenarioSO>($"{Root}/Scenarios/{id}.asset");
            s.scenarioId = id; s.displayName = name; s.briefing = briefing; s.scienceObjectiveTag = tag;
            s.budgetLimitMUSD = budget; s.maxDevelopmentMonths = devMonths; s.targetMissionLifeMonths = lifeMonths;
            s.minScienceValue = minScience; s.allowedOrbits = orbits;
            Save(s);
            return s;
        }

        // ───────────────────────────── VERIFICACIÓN ─────────────────────────────

        /// <summary>Imprime la capacidad de bajada de cada antena en cada órbita para calibrar trade-offs.</summary>
        private static void LogCommsSanityTable(NasaDatabaseSO db)
        {
            var sb = new System.Text.StringBuilder("[NASA DB] Capacidad de bajada (GB/día) por antena y órbita:\n");
            foreach (var a in db.antennas)
            {
                sb.Append($"  {a.displayName,-30}");
                foreach (var o in db.orbits)
                    sb.Append($" | {o.regime}: {o.GetDailyDownlinkGB(a, db.DefaultNetworkFor(o.regime)),9:F1}");
                sb.AppendLine();
            }
            Debug.Log(sb.ToString());
        }

        // ───────────────────────────── UTILIDADES ─────────────────────────────

        private static OrbitPayloadCapacity Cap(OrbitRegime r, float kg, DataConfidence c, string note) =>
            new OrbitPayloadCapacity { regime = r, payloadKg = kg, confidence = c, note = note };

        private static List<OrbitProbabilityModifier> Mods(params (OrbitRegime regime, float mult)[] mods)
        {
            var list = new List<OrbitProbabilityModifier>();
            foreach (var m in mods) list.Add(new OrbitProbabilityModifier { regime = m.regime, multiplier = m.mult });
            return list;
        }

        /// <summary>Carga el asset si existe (conserva GUID y referencias a prefabs) o lo crea.</summary>
        private static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static void Save(Object o) => EditorUtility.SetDirty(o);
    }
}
