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
                        Loc.L("Órbita fuera del objetivo", "Orbit outside the goal"),
                        Loc.L($"El escenario \"{scenario.displayName}\" requiere {string.Join("/", scenario.allowedOrbits)}, pero se eligió {orbit.regime}.",
                              $"The \"{scenario.displayName}\" scenario requires {string.Join("/", scenario.allowedOrbits)}, but {orbit.regime} was chosen."));
                }
            }

            // ── Lanzador vs masa total vs órbita ──
            if (orbit != null && lv != null)
            {
                if (!lv.TryGetCapacity(orbit.regime, out var cap))
                {
                    Add(issues, IssueSeverity.Blocking, ResourceKind.Mass, MissionRole.TelecomLaunchEngineer,
                        Loc.L($"{lv.displayName} no llega a {orbit.regime}", $"{lv.displayName} cannot reach {orbit.regime}"),
                        Loc.L($"{orbit.displayName} exige {orbit.requiredDeltaVKmS:F1} km/s de Δv y {lv.displayName} no ofrece ese perfil de misión. Elige otro lanzador.",
                              $"{orbit.displayName} needs {orbit.requiredDeltaVKmS:F1} km/s of Δv and {lv.displayName} does not offer that mission profile. Choose another launcher."));
                }
                else
                {
                    res.capacityMarginFraction = (cap.payloadKg - t.totalMassKg) / cap.payloadKg;
                    if (t.totalMassKg > cap.payloadKg)
                    {
                        float excess = t.totalMassKg - cap.payloadKg;
                        var heaviest = design.Instruments.OrderByDescending(i => i.massKg).FirstOrDefault();
                        string hint = heaviest != null
                            ? Loc.L($" El instrumento más pesado es {heaviest.displayName} ({heaviest.massKg:N0} kg).", $" The heaviest instrument is {heaviest.displayName} ({heaviest.massKg:N0} kg).")
                            : string.Empty;
                        Add(issues, IssueSeverity.Blocking, ResourceKind.Mass, MissionRole.TelecomLaunchEngineer,
                            Loc.L("Masa excede la capacidad del lanzador", "Mass exceeds launcher capacity"),
                            Loc.L($"La nave pesa {t.totalMassKg:N0} kg y {lv.displayName} solo coloca {cap.payloadKg:N0} kg en {orbit.regime} " +
                                  $"(Δv {orbit.requiredDeltaVKmS:F1} km/s): sobran {excess:N0} kg ({excess / cap.payloadKg:P0} de exceso).{hint} " +
                                  "Opciones: lanzador mayor, bus más ligero o quitar instrumentos.",
                                  $"The spacecraft weighs {t.totalMassKg:N0} kg and {lv.displayName} only puts {cap.payloadKg:N0} kg into {orbit.regime} " +
                                  $"(Δv {orbit.requiredDeltaVKmS:F1} km/s): {excess:N0} kg too many ({excess / cap.payloadKg:P0} over).{hint} " +
                                  "Options: a bigger launcher, a lighter bus or fewer instruments."));
                    }
                    else if (t.massWithMarginKg > cap.payloadKg || res.capacityMarginFraction < RecommendedLaunchMargin)
                    {
                        Add(issues, IssueSeverity.Warning, ResourceKind.Mass, MissionRole.SystemsEngineer,
                            Loc.L("Margen de masa insuficiente", "Insufficient mass margin"),
                            Loc.L($"Margen de {res.capacityMarginFraction:P0} frente a la capacidad del lanzador. La práctica de NASA reserva " +
                                  $"≥{RecommendedLaunchMargin:P0}: la masa real suele crecer durante el desarrollo.",
                                  $"{res.capacityMarginFraction:P0} margin against the launcher capacity. NASA practice keeps " +
                                  $"≥{RecommendedLaunchMargin:P0}: real mass usually grows during development."));
                    }

                    if (cap.confidence == DataConfidence.GameEstimate)
                    {
                        Add(issues, IssueSeverity.Info, ResourceKind.Mass, MissionRole.TelecomLaunchEngineer,
                            Loc.L("Capacidad estimada", "Estimated capacity"),
                            Loc.L($"La capacidad de {lv.displayName} a {orbit.regime} es una estimación de juego: {cap.note}",
                                  $"The capacity of {lv.displayName} to {orbit.regime} is a game estimate."));
                    }
                }
            }

            // ── Límites del bus ──
            if (bus != null)
            {
                if (orbit != null && t.spacecraftDeltaVKmS < t.requiredOnboardDeltaVKmS)
                {
                    Add(issues, IssueSeverity.Blocking, ResourceKind.Mass, MissionRole.SystemsEngineer,
                        Loc.L("Combustible insuficiente para la inserción", "Not enough propellant for orbit insertion"),
                        Loc.L($"El lanzador deja la nave en trayectoria de transferencia; para llegar a {orbit.regime} su propio motor debe aportar " +
                              $"{t.requiredOnboardDeltaVKmS:F2} km/s. Con {bus.propellantMassKg:N0} kg de combustible (Isp {bus.propulsionIspSeconds:N0} s) " +
                              $"y {t.totalMassKg:N0} kg de masa total solo logra {t.spacecraftDeltaVKmS:F2} km/s. Usa un bus con más combustible o aligera la nave.",
                              $"The launcher leaves the spacecraft on a transfer trajectory; to reach {orbit.regime} its own engine must provide " +
                              $"{t.requiredOnboardDeltaVKmS:F2} km/s. With {bus.propellantMassKg:N0} kg of propellant (Isp {bus.propulsionIspSeconds:N0} s) " +
                              $"and {t.totalMassKg:N0} kg total mass it only achieves {t.spacecraftDeltaVKmS:F2} km/s. Use a bus with more propellant or lighten the spacecraft."));
                }
                if (t.instrumentMassKg > bus.maxPayloadMassKg)
                {
                    Add(issues, IssueSeverity.Blocking, ResourceKind.Mass, MissionRole.SystemsEngineer,
                        Loc.L("Estructura del bus sobrecargada", "Bus structure overloaded"),
                        Loc.L($"Los instrumentos suman {t.instrumentMassKg:N0} kg y {bus.displayName} soporta {bus.maxPayloadMassKg:N0} kg de carga útil.",
                              $"The instruments add up to {t.instrumentMassKg:N0} kg and {bus.displayName} supports {bus.maxPayloadMassKg:N0} kg of payload."));
                }
                if (design.Instruments.Count > bus.payloadSlots)
                {
                    Add(issues, IssueSeverity.Blocking, ResourceKind.Mass, MissionRole.PrincipalScientist,
                        Loc.L("Sin espacio para instrumentos", "No room for the instruments"),
                        Loc.L($"{design.Instruments.Count} instrumentos para {bus.payloadSlots} posiciones de montaje en {bus.displayName}.",
                              $"{design.Instruments.Count} instruments for {bus.payloadSlots} mounting slots on {bus.displayName}."));
                }
                if (t.instrumentPeakPowerW > bus.maxPayloadPowerW)
                {
                    Add(issues, IssueSeverity.Blocking, ResourceKind.Power, MissionRole.SystemsEngineer,
                        Loc.L("Distribución de potencia excedida", "Power distribution exceeded"),
                        Loc.L($"Los instrumentos piden {t.instrumentPeakPowerW:N0} W pico; la electrónica de {bus.displayName} distribuye como máximo {bus.maxPayloadPowerW:N0} W.",
                              $"The instruments draw {t.instrumentPeakPowerW:N0} W peak; the electronics of {bus.displayName} deliver at most {bus.maxPayloadPowerW:N0} W."));
                }
            }

            // ── Energía ──
            if (design.SolarArray != null && t.powerBalanceW < 0f)
            {
                Add(issues, IssueSeverity.Warning, ResourceKind.Power, MissionRole.SystemsEngineer,
                    Loc.L("Déficit energético", "Power deficit"),
                    Loc.L($"Generación media {t.powerGeneratedAvgW:N0} W (con {(orbit != null ? orbit.eclipseFraction : 0f):P0} del tiempo en eclipse) " +
                          $"frente a {t.peakLoadW:N0} W de consumo pico. Los instrumentos operarán al {t.powerAvailabilityFraction:P0}.",
                          $"Average generation {t.powerGeneratedAvgW:N0} W (with {(orbit != null ? orbit.eclipseFraction : 0f):P0} of the time in eclipse) " +
                          $"against a {t.peakLoadW:N0} W peak load. The instruments will run at {t.powerAvailabilityFraction:P0}."));
            }
            if (design.Battery != null && t.eclipseEnergyRequiredWh > t.batteryUsableWh)
            {
                Add(issues, IssueSeverity.Warning, ResourceKind.Power, MissionRole.SystemsEngineer,
                    Loc.L("Batería pequeña para el eclipse", "Battery too small for the eclipse"),
                    Loc.L($"El eclipse más largo ({(orbit != null ? orbit.maxEclipseMinutes : 0f):N0} min) consume {t.eclipseEnergyRequiredWh:N0} Wh; " +
                          $"la batería entrega {t.batteryUsableWh:N0} Wh útiles.",
                          $"The longest eclipse ({(orbit != null ? orbit.maxEclipseMinutes : 0f):N0} min) uses {t.eclipseEnergyRequiredWh:N0} Wh; " +
                          $"the battery delivers {t.batteryUsableWh:N0} Wh usable."));
            }

            // ── Datos vs comunicaciones ──
            if (design.Antenna != null && t.dataLostGBPerDay > 0f)
            {
                Add(issues, IssueSeverity.Warning, ResourceKind.Comms, MissionRole.TelecomLaunchEngineer,
                    Loc.L("Enlace de bajada saturado", "Downlink saturated"),
                    Loc.L($"Se generan {t.dataGeneratedGBPerDay:N1} GB/día, pero {design.Antenna.displayName} en {orbit?.regime} solo transmite " +
                          $"{t.downlinkCapacityGBPerDay:N1} GB/día: se perdería el {1f - t.dataReturnFraction:P0} de la ciencia.",
                          $"{t.dataGeneratedGBPerDay:N1} GB/day are generated, but {design.Antenna.displayName} in {orbit?.regime} only transmits " +
                          $"{t.downlinkCapacityGBPerDay:N1} GB/day: {1f - t.dataReturnFraction:P0} of the science would be lost."));
            }
            bool laserLink = design.Antenna != null && design.Antenna.band == CommBand.Optical;
            bool opticalGround = design.GroundNetwork != null && design.GroundNetwork.networkId == OpticalNetworkId;
            if (laserLink != opticalGround && design.Antenna != null && design.GroundNetwork != null && (laserLink || opticalGround))
            {
                Add(issues, IssueSeverity.Blocking, ResourceKind.Comms, MissionRole.TelecomLaunchEngineer,
                    laserLink ? Loc.L("El enlace láser necesita estaciones ópticas", "The laser link needs optical stations")
                              : Loc.L("Las estaciones ópticas solo reciben láser", "Optical stations only receive laser"),
                    laserLink
                        ? Loc.L($"{design.Antenna.displayName} emite luz láser: elige las estaciones ópticas terrestres como segmento terreno.",
                                $"{design.Antenna.displayName} transmits laser light: choose the optical ground stations as the ground segment.")
                        : Loc.L($"{design.GroundNetwork.displayName} usa telescopios; {design.Antenna.displayName} transmite por radio. Usa una antena láser o la DSN.",
                                $"{design.GroundNetwork.displayName} uses telescopes; {design.Antenna.displayName} transmits by radio. Use a laser antenna or the DSN."));
            }
            else if (orbit != null && design.Antenna != null && orbit.regime >= OrbitRegime.Lunar && !design.Antenna.dsnCompatible && !laserLink)
            {
                Add(issues, IssueSeverity.Blocking, ResourceKind.Comms, MissionRole.TelecomLaunchEngineer,
                    Loc.L("Antena incompatible con la DSN", "Antenna not DSN-compatible"),
                    Loc.L($"Más allá de la órbita terrestre solo la Red del Espacio Profundo puede seguir la nave; {design.Antenna.displayName} no es compatible.",
                          $"Beyond Earth orbit only the Deep Space Network can track the spacecraft; {design.Antenna.displayName} is not compatible."));
            }

            // ── Segmento terreno ──
            var network = design.GroundNetwork;
            if (orbit != null && network != null && !network.Supports(orbit.regime))
            {
                Add(issues, IssueSeverity.Blocking, ResourceKind.Comms, MissionRole.TelecomLaunchEngineer,
                    Loc.L($"{network.displayName} no cubre {orbit.regime}", $"{network.displayName} does not cover {orbit.regime}"),
                    Loc.L($"Esa red atiende {string.Join("/", network.supportedRegimes ?? Array.Empty<OrbitRegime>())}. " +
                          $"A {orbit.groundStationRangeKm:N0} km hace falta una red con antenas adecuadas para esa distancia.",
                          $"That network serves {string.Join("/", network.supportedRegimes ?? Array.Empty<OrbitRegime>())}. " +
                          $"At {orbit.groundStationRangeKm:N0} km you need a network with antennas suited to that distance."));
            }

            // ── Subsistemas de dificultad Avanzada ──
            if (design.AttitudeControl != null)
            {
                var poorly = design.Instruments.Where(i => MissionTelemetry.PointingFactor(i, design.AttitudeControl) < 0.99f).ToList();
                if (poorly.Count > 0)
                {
                    Add(issues, IssueSeverity.Warning, ResourceKind.Data, MissionRole.SystemsEngineer,
                        Loc.L("Apuntamiento insuficiente", "Insufficient pointing"),
                        Loc.L($"{design.AttitudeControl.displayName} apunta con ±{design.AttitudeControl.pointingAccuracyDeg:0.###}°, pero ",
                              $"{design.AttitudeControl.displayName} points to ±{design.AttitudeControl.pointingAccuracyDeg:0.###}°, but ") +
                        string.Join(", ", poorly.Select(i => Loc.L($"{i.displayName} necesita ±{i.requiredPointingDeg:0.###}° ({MissionTelemetry.PointingFactor(i, design.AttitudeControl):P0} de su ciencia)",
                                                                   $"{i.displayName} needs ±{i.requiredPointingDeg:0.###}° ({MissionTelemetry.PointingFactor(i, design.AttitudeControl):P0} of its science)"))) + ".");
                }
            }
            if (design.Computer != null && t.dataGeneratedGBPerDay > 0f && t.storageDays < 1f)
            {
                Add(issues, IssueSeverity.Warning, ResourceKind.Data, MissionRole.SystemsEngineer,
                    Loc.L("Memoria a bordo pequeña", "Small onboard memory"),
                    Loc.L($"{design.Computer.displayName} guarda {t.onboardStorageGB:N0} GB: menos de un día de datos ({t.dataGeneratedGBPerDay:N0} GB/día). " +
                          "Cualquier corte de enlace hará perder ciencia.",
                          $"{design.Computer.displayName} stores {t.onboardStorageGB:N0} GB: less than one day of data ({t.dataGeneratedGBPerDay:N0} GB/day). " +
                          "Any link outage will lose science."));
            }

            // ── Piezas que solo existen como parte de otro vehículo ──
            foreach (var c in design.AllComponents)
            {
                if (c == null || string.IsNullOrEmpty(c.requiredBusId)) continue;
                if (bus != null && bus.componentId == c.requiredBusId) continue;
                string busName = string.IsNullOrEmpty(c.requiredBusName) ? c.requiredBusId : c.requiredBusName;
                Add(issues, IssueSeverity.Blocking, ResourceKind.Mass, MissionRole.SystemsEngineer,
                    Loc.L($"{c.displayName}: pieza exclusiva", $"{c.displayName}: exclusive part"),
                    Loc.L($"{c.displayName} forma parte de {busName} y solo se monta en ese bus. Elige otra pieza o cambia el bus.",
                          $"{c.displayName} is part of {busName} and only mounts on that bus. Choose another part or change the bus."));
            }

            // ── Presupuesto y calendario ──
            if (scenario != null)
            {
                if (scenario.crewed && (bus == null || !bus.crewCapable))
                {
                    Add(issues, IssueSeverity.Blocking, ResourceKind.Mass, MissionRole.SystemsEngineer,
                        Loc.L("Nave sin soporte vital", "Spacecraft without life support"),
                        Loc.L($"El escenario lleva {scenario.crewSize} astronautas: necesita una cápsula con soporte vital, escudo térmico " +
                              "y sistema de aborto (p. ej. Orion con su módulo de servicio).",
                              $"The scenario carries {scenario.crewSize} astronauts: it needs a capsule with life support, a heat shield " +
                              "and a launch abort system (e.g. Orion with its service module)."));
                }
                if (t.budgetLimitMUSD > 0f && t.totalCostMUSD > t.budgetLimitMUSD)
                {
                    Add(issues, IssueSeverity.Blocking, ResourceKind.Budget, MissionRole.MissionDirector,
                        Loc.L("Presupuesto excedido", "Budget exceeded"),
                        Loc.L($"Costo total ${t.totalCostMUSD:N1} M frente a un límite de ${t.budgetLimitMUSD:N0} M (exceso ${t.totalCostMUSD - t.budgetLimitMUSD:N1} M).",
                              $"Total cost ${t.totalCostMUSD:N1} M against a ${t.budgetLimitMUSD:N0} M limit (${t.totalCostMUSD - t.budgetLimitMUSD:N1} M over)."));
                }
                if (t.maxDevelopmentMonths > 0f && t.developmentMonths > t.maxDevelopmentMonths)
                {
                    Add(issues, IssueSeverity.Warning, ResourceKind.Time, MissionRole.MissionDirector,
                        Loc.L("Calendario fuera de plazo", "Schedule overrun"),
                        Loc.L($"Desarrollo estimado {t.developmentMonths:N0} meses frente a {t.maxDevelopmentMonths:N0} disponibles.",
                              $"Estimated development {t.developmentMonths:N0} months against {t.maxDevelopmentMonths:N0} available."));
                }
                if (t.potentialScienceValue < scenario.minScienceValue)
                {
                    Add(issues, IssueSeverity.Info, ResourceKind.Data, MissionRole.PrincipalScientist,
                        Loc.L("Objetivo científico no cubierto", "Science goal not covered"),
                        Loc.L($"Valor científico potencial {t.potentialScienceValue:N0} < {scenario.minScienceValue} requerido: la misión sería, como mucho, un éxito parcial.",
                              $"Potential science value {t.potentialScienceValue:N0} < {scenario.minScienceValue} required: the mission would be a partial success at best."));
                }
            }

            foreach (var i in design.Instruments)
            {
                if (orbit != null && !i.IsPreferredOrbit(orbit.regime))
                {
                    Add(issues, IssueSeverity.Info, ResourceKind.Data, MissionRole.PrincipalScientist,
                        Loc.L($"{i.displayName} fuera de su órbita óptima", $"{i.displayName} outside its optimal orbit"),
                        Loc.L($"Diseñado para {string.Join("/", i.preferredOrbits ?? Array.Empty<OrbitRegime>())}: en {orbit.regime} aporta {MissionTelemetry.OffOrbitScienceFactor:P0} de su valor.",
                              $"Designed for {string.Join("/", i.preferredOrbits ?? Array.Empty<OrbitRegime>())}: in {orbit.regime} it gives {MissionTelemetry.OffOrbitScienceFactor:P0} of its value."));
                }
                if (!MissionTelemetry.MatchesObjective(i, scenario))
                {
                    Add(issues, IssueSeverity.Info, ResourceKind.Data, MissionRole.PrincipalScientist,
                        Loc.L($"{i.displayName} fuera del objetivo", $"{i.displayName} off the goal"),
                        Loc.L($"Estudia {string.Join("/", i.scienceObjectives ?? Array.Empty<string>())}, pero el objetivo de \"{scenario.displayName}\" es " +
                              $"{scenario.scienceObjectiveTag}: aporta {MissionTelemetry.OffObjectiveScienceFactor:P0} de su valor.",
                              $"It studies {string.Join("/", (i.scienceObjectives ?? Array.Empty<string>()).Select(Loc.Tag))}, but the goal of \"{scenario.displayName}\" is " +
                              $"{Loc.Tag(scenario.scienceObjectiveTag)}: it gives {MissionTelemetry.OffObjectiveScienceFactor:P0} of its value."));
                }
            }

            res.canLaunch = !issues.Any(i => i.severity == IssueSeverity.Blocking);
            return res;
        }

        private static void CheckMissingSelections(SpacecraftDesign d, List<ValidationIssue> issues)
        {
            if (d.Scenario == null) Missing(issues, MissionRole.MissionDirector, ResourceKind.Budget, Loc.L("escenario / objetivo científico", "a scenario / science goal"));
            if (d.Orbit == null) Missing(issues, MissionRole.MissionDirector, ResourceKind.Time, Loc.L("órbita", "an orbit"));
            if (d.LaunchVehicle == null) Missing(issues, MissionRole.TelecomLaunchEngineer, ResourceKind.Mass, Loc.L("cohete lanzador", "a launch vehicle"));
            if (d.Bus == null) Missing(issues, MissionRole.SystemsEngineer, ResourceKind.Mass, Loc.L("bus satelital", "a spacecraft bus"));
            if (d.SolarArray == null) Missing(issues, MissionRole.SystemsEngineer, ResourceKind.Power, Loc.L("paneles solares", "solar arrays"));
            if (d.Battery == null) Missing(issues, MissionRole.SystemsEngineer, ResourceKind.Power, Loc.L("batería", "a battery"));
            if (d.Antenna == null) Missing(issues, MissionRole.TelecomLaunchEngineer, ResourceKind.Comms, Loc.L("antena", "an antenna"));
            if (d.GroundNetwork == null) Missing(issues, MissionRole.TelecomLaunchEngineer, ResourceKind.Comms, Loc.L("red de estaciones terrenas (segmento terreno)", "a ground station network (ground segment)"));
            if (d.IsAdvanced && d.Computer == null) Missing(issues, MissionRole.SystemsEngineer, ResourceKind.Data, Loc.L("computadora de a bordo", "an onboard computer"));
            if (d.IsAdvanced && d.AttitudeControl == null) Missing(issues, MissionRole.SystemsEngineer, ResourceKind.Power, Loc.L("control de orientación (ADCS)", "attitude control (ADCS)"));
            if (d.Instruments.Count == 0) Missing(issues, MissionRole.PrincipalScientist, ResourceKind.Data, Loc.L("al menos un instrumento científico", "at least one science instrument"));
        }

        private static void Missing(List<ValidationIssue> issues, MissionRole role, ResourceKind kind, string what) =>
            Add(issues, IssueSeverity.Blocking, kind, role, Loc.L("Selección incompleta", "Incomplete selection"), Loc.L($"Falta elegir {what}.", $"Choose {what}."));

        private static void Add(List<ValidationIssue> issues, IssueSeverity s, ResourceKind kind, MissionRole role, string title, string explanation) =>
            issues.Add(new ValidationIssue { severity = s, resource = kind, responsibleRole = role, title = title, explanation = explanation });
    }
}
