using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MissionDesign.Core;
using MissionDesign.Data;
using UnityEngine;

namespace MissionDesign.Simulation
{
    public enum IssueSeverity
    {
        Info,
        Warning,
        Blocking
    }

    [Serializable]
    public struct ValidationIssue
    {
        public IssueSeverity severity;
        public ResourceKind resource;
        /// <summary>Rol que debe resolverlo: alimenta la fase de Negociación de Trade-offs.</summary>
        public MissionRole responsibleRole;
        public string title;
        public string explanation;

        public override string ToString() => $"[{severity}] ({responsibleRole}) {title}: {explanation}";
    }

    [Serializable]
    public sealed class LaunchValidationResult
    {
        public bool canLaunch;
        public float requiredDeltaVKmS;
        public float idealPropellantFraction;
        public float payloadCapacityKg;
        public float totalMassKg;
        /// <summary>(capacidad − masa) / capacidad. Negativo = sobrepeso.</summary>
        public float capacityMarginFraction;
        public List<ValidationIssue> issues = new List<ValidationIssue>();

        public IEnumerable<ValidationIssue> Blocking => issues.Where(i => i.severity == IssueSeverity.Blocking);
        public IEnumerable<ValidationIssue> Warnings => issues.Where(i => i.severity == IssueSeverity.Warning);

        public string ToConsoleString()
        {
            var sb = new StringBuilder();
            sb.AppendLine(canLaunch ? "  ✅ LANZAMIENTO AUTORIZADO" : "  ⛔ LANZAMIENTO BLOQUEADO");
            sb.AppendLine($"  Δv requerido {requiredDeltaVKmS:F1} km/s → un cohete ideal de una etapa (Isp {OrbitAndLaunchValidator.ReferenceIspSeconds:F0} s) " +
                          $"debería ser {idealPropellantFraction:P1} propelente. Margen de carga útil: {capacityMarginFraction:P0}.");
            foreach (var i in issues.OrderByDescending(i => i.severity))
                sb.AppendLine($"   - {i}");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Valida física orbital, capacidad del lanzador y coherencia del diseño.
    /// Cualquier problema Blocking impide pulsar "Lanzar Misión" y explica por qué.
    /// </summary>
    public static class OrbitAndLaunchValidator
    {
        public const string OpticalNetworkId = "GND_OPTICAL";

        public const float StandardGravity = 9.80665f;
        /// <summary>Impulso específico de referencia (motor LOX/RP-1 de vacío) para la explicación educativa.</summary>
        public const float ReferenceIspSeconds = 350f;
        /// <summary>Margen mínimo recomendado entre masa y capacidad del lanzador.</summary>
        public const float RecommendedLaunchMargin = 0.10f;

        /// <summary>
        /// Ecuación de Tsiolkovsky: Δv = Isp·g0·ln(m0/mf) → fracción de propelente = 1 − e^(−Δv / (Isp·g0)).
        /// </summary>
        public static float IdealPropellantFraction(float deltaVKmS, float ispSeconds = ReferenceIspSeconds)
        {
            float ve = ispSeconds * StandardGravity;          // m/s
            return 1f - Mathf.Exp(-deltaVKmS * 1000f / ve);
        }

        public static LaunchValidationResult Validate(SpacecraftDesign design, TelemetryReport telemetry = null)
        {
            if (design == null) throw new ArgumentNullException(nameof(design));
            var t = telemetry ?? MissionTelemetry.Calculate(design);
            var res = new LaunchValidationResult { totalMassKg = t.totalMassKg, payloadCapacityKg = t.launchCapacityKg };
            var issues = res.issues;

            CheckMissingSelections(design, issues);

            var orbit = design.Orbit;
            var lv = design.LaunchVehicle;
            var bus = design.Bus;
            var scenario = design.Scenario;

            // ── Órbita y escenario ──
            if (orbit != null)
            {
                res.requiredDeltaVKmS = orbit.requiredDeltaVKmS;
                res.idealPropellantFraction = IdealPropellantFraction(orbit.requiredDeltaVKmS);

                if (scenario != null && scenario.allowedOrbits != null && scenario.allowedOrbits.Length > 0
                    && !scenario.allowedOrbits.Contains(orbit.regime))
                {
                    Add(issues, IssueSeverity.Blocking, ResourceKind.Time, MissionRole.MissionDirector,
                        "Órbita fuera del objetivo",
                        $"El escenario \"{scenario.displayName}\" requiere {string.Join("/", scenario.allowedOrbits)}, pero se eligió {orbit.regime}.");
                }
            }

            // ── Lanzador vs masa total vs órbita ──
            if (orbit != null && lv != null)
            {
                if (!lv.TryGetCapacity(orbit.regime, out var cap))
                {
                    Add(issues, IssueSeverity.Blocking, ResourceKind.Mass, MissionRole.TelecomLaunchEngineer,
                        $"{lv.displayName} no llega a {orbit.regime}",
                        $"{orbit.displayName} exige {orbit.requiredDeltaVKmS:F1} km/s de Δv y {lv.displayName} no ofrece ese perfil de misión. Elige otro lanzador.");
                }
                else
                {
                    res.capacityMarginFraction = (cap.payloadKg - t.totalMassKg) / cap.payloadKg;
                    if (t.totalMassKg > cap.payloadKg)
                    {
                        float excess = t.totalMassKg - cap.payloadKg;
                        var heaviest = design.Instruments.OrderByDescending(i => i.massKg).FirstOrDefault();
                        string hint = heaviest != null
                            ? $" El instrumento más pesado es {heaviest.displayName} ({heaviest.massKg:N0} kg)."
                            : string.Empty;
                        Add(issues, IssueSeverity.Blocking, ResourceKind.Mass, MissionRole.TelecomLaunchEngineer,
                            "Masa excede la capacidad del lanzador",
                            $"La nave pesa {t.totalMassKg:N0} kg y {lv.displayName} solo coloca {cap.payloadKg:N0} kg en {orbit.regime} " +
                            $"(Δv {orbit.requiredDeltaVKmS:F1} km/s): sobran {excess:N0} kg ({excess / cap.payloadKg:P0} de exceso).{hint} " +
                            "Opciones: lanzador mayor, bus más ligero o quitar instrumentos.");
                    }
                    else if (t.massWithMarginKg > cap.payloadKg || res.capacityMarginFraction < RecommendedLaunchMargin)
                    {
                        Add(issues, IssueSeverity.Warning, ResourceKind.Mass, MissionRole.SystemsEngineer,
                            "Margen de masa insuficiente",
                            $"Margen de {res.capacityMarginFraction:P0} frente a la capacidad del lanzador. La práctica de NASA reserva " +
                            $"≥{RecommendedLaunchMargin:P0}: la masa real suele crecer durante el desarrollo.");
                    }

                    if (cap.confidence == DataConfidence.GameEstimate)
                    {
                        Add(issues, IssueSeverity.Info, ResourceKind.Mass, MissionRole.TelecomLaunchEngineer,
                            "Capacidad estimada", $"La capacidad de {lv.displayName} a {orbit.regime} es una estimación de juego: {cap.note}");
                    }
                }
            }

            // ── Límites del bus ──
            if (bus != null)
            {
                if (orbit != null && t.spacecraftDeltaVKmS < t.requiredOnboardDeltaVKmS)
                {
                    Add(issues, IssueSeverity.Blocking, ResourceKind.Mass, MissionRole.SystemsEngineer,
                        "Combustible insuficiente para la inserción",
                        $"El lanzador deja la nave en trayectoria de transferencia; para llegar a {orbit.regime} su propio motor debe aportar " +
                        $"{t.requiredOnboardDeltaVKmS:F2} km/s. Con {bus.propellantMassKg:N0} kg de combustible (Isp {bus.propulsionIspSeconds:N0} s) " +
                        $"y {t.totalMassKg:N0} kg de masa total solo logra {t.spacecraftDeltaVKmS:F2} km/s. Usa un bus con más combustible o aligera la nave.");
                }
                if (t.instrumentMassKg > bus.maxPayloadMassKg)
                {
                    Add(issues, IssueSeverity.Blocking, ResourceKind.Mass, MissionRole.SystemsEngineer,
                        "Estructura del bus sobrecargada",
                        $"Los instrumentos suman {t.instrumentMassKg:N0} kg y {bus.displayName} soporta {bus.maxPayloadMassKg:N0} kg de carga útil.");
                }
                if (design.Instruments.Count > bus.payloadSlots)
                {
                    Add(issues, IssueSeverity.Blocking, ResourceKind.Mass, MissionRole.PrincipalScientist,
                        "Sin espacio para instrumentos",
                        $"{design.Instruments.Count} instrumentos para {bus.payloadSlots} posiciones de montaje en {bus.displayName}.");
                }
                if (t.instrumentPeakPowerW > bus.maxPayloadPowerW)
                {
                    Add(issues, IssueSeverity.Blocking, ResourceKind.Power, MissionRole.SystemsEngineer,
                        "Distribución de potencia excedida",
                        $"Los instrumentos piden {t.instrumentPeakPowerW:N0} W pico; la electrónica de {bus.displayName} distribuye como máximo {bus.maxPayloadPowerW:N0} W.");
                }
            }

            // ── Energía ──
            if (design.SolarArray != null && t.powerBalanceW < 0f)
            {
                Add(issues, IssueSeverity.Warning, ResourceKind.Power, MissionRole.SystemsEngineer,
                    "Déficit energético",
                    $"Generación media {t.powerGeneratedAvgW:N0} W (con {(orbit != null ? orbit.eclipseFraction : 0f):P0} del tiempo en eclipse) " +
                    $"frente a {t.peakLoadW:N0} W de consumo pico. Los instrumentos operarán al {t.powerAvailabilityFraction:P0}.");
            }
            if (design.Battery != null && t.eclipseEnergyRequiredWh > t.batteryUsableWh)
            {
                Add(issues, IssueSeverity.Warning, ResourceKind.Power, MissionRole.SystemsEngineer,
                    "Batería pequeña para el eclipse",
                    $"El eclipse más largo ({(orbit != null ? orbit.maxEclipseMinutes : 0f):N0} min) consume {t.eclipseEnergyRequiredWh:N0} Wh; " +
                    $"la batería entrega {t.batteryUsableWh:N0} Wh útiles.");
            }

            // ── Datos vs comunicaciones ──
            if (design.Antenna != null && t.dataLostGBPerDay > 0f)
            {
                Add(issues, IssueSeverity.Warning, ResourceKind.Comms, MissionRole.TelecomLaunchEngineer,
                    "Enlace de bajada saturado",
                    $"Se generan {t.dataGeneratedGBPerDay:N1} GB/día, pero {design.Antenna.displayName} en {orbit?.regime} solo transmite " +
                    $"{t.downlinkCapacityGBPerDay:N1} GB/día: se perdería el {1f - t.dataReturnFraction:P0} de la ciencia.");
            }
            bool laserLink = design.Antenna != null && design.Antenna.band == CommBand.Optical;
            bool opticalGround = design.GroundNetwork != null && design.GroundNetwork.networkId == OpticalNetworkId;
            if (laserLink != opticalGround && design.Antenna != null && design.GroundNetwork != null && (laserLink || opticalGround))
            {
                Add(issues, IssueSeverity.Blocking, ResourceKind.Comms, MissionRole.TelecomLaunchEngineer,
                    laserLink ? "El enlace láser necesita estaciones ópticas" : "Las estaciones ópticas solo reciben láser",
                    laserLink
                        ? $"{design.Antenna.displayName} emite luz láser: elige las estaciones ópticas terrestres como segmento terreno."
                        : $"{design.GroundNetwork.displayName} usa telescopios; {design.Antenna.displayName} transmite por radio. Usa una antena láser o la DSN.");
            }
            else if (orbit != null && design.Antenna != null && orbit.regime >= OrbitRegime.Lunar && !design.Antenna.dsnCompatible && !laserLink)
            {
                Add(issues, IssueSeverity.Blocking, ResourceKind.Comms, MissionRole.TelecomLaunchEngineer,
                    "Antena incompatible con la DSN",
                    $"Más allá de la órbita terrestre solo la Red del Espacio Profundo puede seguir la nave; {design.Antenna.displayName} no es compatible.");
            }

            // ── Segmento terreno ──
            var network = design.GroundNetwork;
            if (orbit != null && network != null && !network.Supports(orbit.regime))
            {
                Add(issues, IssueSeverity.Blocking, ResourceKind.Comms, MissionRole.TelecomLaunchEngineer,
                    $"{network.displayName} no cubre {orbit.regime}",
                    $"Esa red atiende {string.Join("/", network.supportedRegimes ?? Array.Empty<OrbitRegime>())}. " +
                    $"A {orbit.groundStationRangeKm:N0} km hace falta una red con antenas adecuadas para esa distancia.");
            }

            // ── Subsistemas de dificultad Avanzada ──
            if (design.AttitudeControl != null)
            {
                var poorly = design.Instruments.Where(i => MissionTelemetry.PointingFactor(i, design.AttitudeControl) < 0.99f).ToList();
                if (poorly.Count > 0)
                {
                    Add(issues, IssueSeverity.Warning, ResourceKind.Data, MissionRole.SystemsEngineer,
                        "Apuntamiento insuficiente",
                        $"{design.AttitudeControl.displayName} apunta con ±{design.AttitudeControl.pointingAccuracyDeg:0.###}°, pero " +
                        string.Join(", ", poorly.Select(i => $"{i.displayName} necesita ±{i.requiredPointingDeg:0.###}° ({MissionTelemetry.PointingFactor(i, design.AttitudeControl):P0} de su ciencia)")) + ".");
                }
            }
            if (design.Computer != null && t.dataGeneratedGBPerDay > 0f && t.storageDays < 1f)
            {
                Add(issues, IssueSeverity.Warning, ResourceKind.Data, MissionRole.SystemsEngineer,
                    "Memoria a bordo pequeña",
                    $"{design.Computer.displayName} guarda {t.onboardStorageGB:N0} GB: menos de un día de datos ({t.dataGeneratedGBPerDay:N0} GB/día). " +
                    "Cualquier corte de enlace hará perder ciencia.");
            }

            // ── Piezas que solo existen como parte de otro vehículo ──
            foreach (var c in design.AllComponents)
            {
                if (c == null || string.IsNullOrEmpty(c.requiredBusId)) continue;
                if (bus != null && bus.componentId == c.requiredBusId) continue;
                string busName = string.IsNullOrEmpty(c.requiredBusName) ? c.requiredBusId : c.requiredBusName;
                Add(issues, IssueSeverity.Blocking, ResourceKind.Mass, MissionRole.SystemsEngineer,
                    $"{c.displayName}: pieza exclusiva",
                    $"{c.displayName} forma parte de {busName} y solo se monta en ese bus. Elige otra pieza o cambia el bus.");
            }

            // ── Presupuesto y calendario ──
            if (scenario != null)
            {
                if (scenario.crewed && (bus == null || !bus.crewCapable))
                {
                    Add(issues, IssueSeverity.Blocking, ResourceKind.Mass, MissionRole.SystemsEngineer,
                        "Nave sin soporte vital",
                        $"El escenario lleva {scenario.crewSize} astronautas: necesita una cápsula con soporte vital, escudo térmico " +
                        "y sistema de aborto (p. ej. Orion con su módulo de servicio).");
                }
                if (t.budgetLimitMUSD > 0f && t.totalCostMUSD > t.budgetLimitMUSD)
                {
                    Add(issues, IssueSeverity.Blocking, ResourceKind.Budget, MissionRole.MissionDirector,
                        "Presupuesto excedido",
                        $"Costo total ${t.totalCostMUSD:N1} M frente a un límite de ${t.budgetLimitMUSD:N0} M (exceso ${t.totalCostMUSD - t.budgetLimitMUSD:N1} M).");
                }
                if (t.maxDevelopmentMonths > 0f && t.developmentMonths > t.maxDevelopmentMonths)
                {
                    Add(issues, IssueSeverity.Warning, ResourceKind.Time, MissionRole.MissionDirector,
                        "Calendario fuera de plazo",
                        $"Desarrollo estimado {t.developmentMonths:N0} meses frente a {t.maxDevelopmentMonths:N0} disponibles.");
                }
                if (t.potentialScienceValue < scenario.minScienceValue)
                {
                    Add(issues, IssueSeverity.Info, ResourceKind.Data, MissionRole.PrincipalScientist,
                        "Objetivo científico no cubierto",
                        $"Valor científico potencial {t.potentialScienceValue:N0} < {scenario.minScienceValue} requerido: la misión sería, como mucho, un éxito parcial.");
                }
            }

            foreach (var i in design.Instruments)
            {
                if (orbit != null && !i.IsPreferredOrbit(orbit.regime))
                {
                    Add(issues, IssueSeverity.Info, ResourceKind.Data, MissionRole.PrincipalScientist,
                        $"{i.displayName} fuera de su órbita óptima",
                        $"Diseñado para {string.Join("/", i.preferredOrbits ?? Array.Empty<OrbitRegime>())}: en {orbit.regime} aporta {MissionTelemetry.OffOrbitScienceFactor:P0} de su valor.");
                }
                if (!MissionTelemetry.MatchesObjective(i, scenario))
                {
                    Add(issues, IssueSeverity.Info, ResourceKind.Data, MissionRole.PrincipalScientist,
                        $"{i.displayName} fuera del objetivo",
                        $"Estudia {string.Join("/", i.scienceObjectives ?? Array.Empty<string>())}, pero el objetivo de \"{scenario.displayName}\" es " +
                        $"{scenario.scienceObjectiveTag}: aporta {MissionTelemetry.OffObjectiveScienceFactor:P0} de su valor.");
                }
            }

            res.canLaunch = !issues.Any(i => i.severity == IssueSeverity.Blocking);
            return res;
        }

        private static void CheckMissingSelections(SpacecraftDesign d, List<ValidationIssue> issues)
        {
            if (d.Scenario == null) Missing(issues, MissionRole.MissionDirector, ResourceKind.Budget, "escenario / objetivo científico");
            if (d.Orbit == null) Missing(issues, MissionRole.MissionDirector, ResourceKind.Time, "órbita");
            if (d.LaunchVehicle == null) Missing(issues, MissionRole.TelecomLaunchEngineer, ResourceKind.Mass, "cohete lanzador");
            if (d.Bus == null) Missing(issues, MissionRole.SystemsEngineer, ResourceKind.Mass, "bus satelital");
            if (d.SolarArray == null) Missing(issues, MissionRole.SystemsEngineer, ResourceKind.Power, "paneles solares");
            if (d.Battery == null) Missing(issues, MissionRole.SystemsEngineer, ResourceKind.Power, "batería");
            if (d.Antenna == null) Missing(issues, MissionRole.TelecomLaunchEngineer, ResourceKind.Comms, "antena");
            if (d.GroundNetwork == null) Missing(issues, MissionRole.TelecomLaunchEngineer, ResourceKind.Comms, "red de estaciones terrenas (segmento terreno)");
            if (d.IsAdvanced && d.Computer == null) Missing(issues, MissionRole.SystemsEngineer, ResourceKind.Data, "computadora de a bordo");
            if (d.IsAdvanced && d.AttitudeControl == null) Missing(issues, MissionRole.SystemsEngineer, ResourceKind.Power, "control de orientación (ADCS)");
            if (d.Instruments.Count == 0) Missing(issues, MissionRole.PrincipalScientist, ResourceKind.Data, "al menos un instrumento científico");
        }

        private static void Missing(List<ValidationIssue> issues, MissionRole role, ResourceKind kind, string what) =>
            Add(issues, IssueSeverity.Blocking, kind, role, "Selección incompleta", $"Falta elegir {what}.");

        private static void Add(List<ValidationIssue> issues, IssueSeverity s, ResourceKind kind, MissionRole role, string title, string explanation) =>
            issues.Add(new ValidationIssue { severity = s, resource = kind, responsibleRole = role, title = title, explanation = explanation });
    }
}
