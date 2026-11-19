using System;
using System.Collections.Generic;
using System.Linq;
using MissionDesign.Core;
using MissionDesign.Data;
using UnityEngine;

namespace MissionDesign.Simulation
{
    public sealed class SimulationSettings
    {
        /// <summary>Misma semilla + mismo diseño = mismo resultado (útil para comparar equipos).</summary>
        public int seed = Environment.TickCount;
        /// <summary>Permite que el cohete falle según su confiabilidad histórica.</summary>
        public bool allowLaunchFailure = true;
        public int minEvents = 1;
        public int maxEvents = 2;
        /// <summary>Si se indica, se usan estos eventos en lugar de sortearlos (modo demo / pruebas).</summary>
        public List<RandomEventSO> forcedEvents;
        /// <summary>Catálogo de misiones reales para el "Tu misión se parece a…".</summary>
        public IEnumerable<RealMissionSO> realMissions;
    }

    /// <summary>
    /// Simulación acelerada por fases: Lanzamiento → Inserción orbital → Operaciones científicas →
    /// Transmisión de datos. Es una función pura: calcula todo el resultado de una vez; el
    /// MissionSimulationRunner lo reproduce después en pantalla.
    /// </summary>
    public static class MissionSimulator
    {
        public const float DaysPerMonth = 30.44f;

        /// <summary>
        /// Duración real de Artemis II: despegue el 1 de abril de 2026 (22:35 UTC) y amerizaje el 10 de abril, ~9 días y 1,5 h.
        /// Es la duración planificada del escenario tripulado (y por tanto de la demo 1, que reproduce Artemis II).
        /// </summary>
        public const float ArtemisIIMissionDays = 9.06f;
        /// <summary>Por debajo de esta fracción de la ciencia objetivo la misión es un fallo.</summary>
        public const float PartialThreshold = 0.4f;
        /// <summary>Días de datos que debe guardar la memoria para absorber un corte de enlace.</summary>
        public const float StorageDaysToBufferOutage = 2f;

        /// <summary>Hitos del ascenso del SLS Block 1 (infografía de la misión Artemis; tiempos desde el despegue).</summary>
        public static (string time, string text)[] SlsAscent => new[]
        {
            ("T+1:11", Loc.L("Max Q: máxima presión aerodinámica (12,8 km)", "Max Q: maximum aerodynamic pressure (12.8 km)")),
            ("T+2:09", Loc.L("Separación de los propulsores sólidos (47,5 km)", "Solid rocket booster separation (47.5 km)")),
            ("T+3:18", Loc.L("Expulsión del sistema de aborto de lanzamiento (87 km)", "Launch abort system jettison (87 km)")),
            ("T+8:15", Loc.L("Apagado y separación de la etapa central (162,5 km)", "Core stage cutoff and separation (162.5 km)")),
            ("T+49 min", Loc.L("La etapa superior (ICPS) eleva el perigeo", "The upper stage (ICPS) raises the perigee")),
            ("T+1 h 48 min", Loc.L("La ICPS eleva el apogeo: órbita terrestre alta", "The ICPS raises the apogee: high Earth orbit")),
            ("T+3 h 24 min", Loc.L("Orion se separa de la ICPS a 22 279 km", "Orion separates from the ICPS at 22,279 km"))
        };

        public static MissionOutcomeReport Run(SpacecraftDesign d, IReadOnlyList<RandomEventSO> eventPool, SimulationSettings settings = null)
        {
            if (d == null) throw new ArgumentNullException(nameof(d));
            settings = settings ?? new SimulationSettings();
            var rng = new System.Random(settings.seed);
            var t = MissionTelemetry.Calculate(d);
            var o = NewReport(d, t, settings.seed);

            var orbit = d.Orbit;
            var lv = d.LaunchVehicle;
            string regime = orbit != null ? orbit.regime.ToString() : "?";
            float days = o.plannedOperationsDays;

            var similar = FindSimilarMission(d, t, settings.realMissions);
            if (similar != null)
            {
                o.similarMission = $"{similar.displayName} ({similar.launchYear})";
                o.similarMissionDescription = similar.description;
            }

            // ───── 1. LANZAMIENTO ─────
            float reliability = lv != null ? lv.reliability : 0f;
            o.launchSucceeded = lv != null && (!settings.allowLaunchFailure || rng.NextDouble() < reliability);
            if (!o.launchSucceeded)
            {
                Log(o, MissionPhase.Launch, 0, LogTone.Critical, Loc.L($"Anomalía en el ascenso: {Name(lv)} no alcanzó la órbita.", $"Ascent anomaly: {Name(lv)} did not reach orbit."));
                o.explanations.Add(Loc.L($"Fallo: el lanzador {Name(lv)} falló durante el ascenso (confiabilidad histórica {reliability:P0}). " +
                                         "Ningún diseño elimina este riesgo; por eso se eligen lanzadores con historial y se contratan seguros.",
                                         $"Failure: the {Name(lv)} launcher failed during ascent (historical reliability {reliability:P0}). " +
                                         "No design removes this risk; that is why proven launchers are chosen and insurance is bought."));
                return Finish(o, MissionVerdict.Failure, Loc.L("Pérdida en el lanzamiento", "Lost at launch"), 0f, t);
            }
            Log(o, MissionPhase.Launch, 0, LogTone.Positive,
                Loc.L($"Despegue nominal de {Name(lv)}: {t.totalMassKg:N0} kg hacia {regime} (Δv {orbit?.requiredDeltaVKmS:F1} km/s, margen de carga {Margin(t):P0}).", $"Nominal liftoff of {Name(lv)}: {t.totalMassKg:N0} kg to {regime} (Δv {orbit?.requiredDeltaVKmS:F1} km/s, payload margin {Margin(t):P0})."));
            bool flyby = orbit != null && orbit.regime == OrbitRegime.LunarFlyby;
            o.returnsToEarth = flyby;
            o.crewed = d.Scenario != null && d.Scenario.crewed;
            if (lv.vehicleId == "LV_SLS_B1")
                foreach (var (time, text) in SlsAscent) Log(o, MissionPhase.Launch, 0, LogTone.Info, $"{time} · {text}");
            var freeReturn = flyby ? FreeReturnTrajectory.Default : null;

            // ───── 2. INSERCIÓN ORBITAL ─────
            o.orbitInsertionSucceeded = t.spacecraftDeltaVKmS >= t.requiredOnboardDeltaVKmS;
            if (!o.orbitInsertionSucceeded)
            {
                Log(o, MissionPhase.OrbitInsertion, 1, LogTone.Critical, Loc.L("Combustible agotado antes de completar la inserción.", "Propellant ran out before completing orbit insertion."));
                o.explanations.Add(Loc.L($"Fallo: para entrar en {regime} el motor del bus debía aportar {t.requiredOnboardDeltaVKmS:F2} km/s, " +
                                         $"pero con {o.bus} solo alcanzó {t.spacecraftDeltaVKmS:F2} km/s.",
                                         $"Failure: to enter {regime} the bus engine had to provide {t.requiredOnboardDeltaVKmS:F2} km/s, " +
                                         $"but with {o.bus} it only reached {t.spacecraftDeltaVKmS:F2} km/s."));
                return Finish(o, MissionVerdict.Failure, Loc.L("Inserción orbital fallida", "Orbit insertion failed"), 0f, t);
            }
            Log(o, MissionPhase.OrbitInsertion, 1, LogTone.Positive, t.requiredOnboardDeltaVKmS > 0.1f
                ? Loc.L($"Inserción en {regime} completada: el motor aportó {t.requiredOnboardDeltaVKmS:F2} de {t.spacecraftDeltaVKmS:F2} km/s disponibles.",
                        $"Insertion into {regime} complete: the engine provided {t.requiredOnboardDeltaVKmS:F2} of {t.spacecraftDeltaVKmS:F2} km/s available.")
                : Loc.L($"Separación e inserción en {regime} completadas. Paneles desplegados.", $"Separation and insertion into {regime} complete. Panels deployed."));
            if (freeReturn != null)
            {
                Log(o, MissionPhase.OrbitInsertion, 0.5f, LogTone.Info,
                    Loc.L("Órbita terrestre alta: ~24 h de verificación de sistemas y pruebas de proximidad (como en Artemis II).", "High Earth orbit: ~24 h of systems checkout and proximity operations (as on Artemis II)."));
                Log(o, MissionPhase.OrbitInsertion, 1, LogTone.Positive,
                    Loc.L($"Inyección translunar (TLI): Δv {freeReturn.tliDeltaVKmS:F2} km/s → {freeReturn.injectionSpeedKmS:F2} km/s. Comienza la ida en retorno libre.", $"Trans-lunar injection (TLI): Δv {freeReturn.tliDeltaVKmS:F2} km/s → {freeReturn.injectionSpeedKmS:F2} km/s. The free-return outbound leg begins."));
            }

            // ───── 3. OPERACIONES CIENTÍFICAS (con eventos) ─────
            Log(o, MissionPhase.ScienceOperations, 2, LogTone.Info,
                Loc.L($"Inicio de operaciones: {d.Instruments.Count} instrumento(s) generando {t.dataGeneratedGBPerDay:N1} GB/día.", $"Operations begin: {d.Instruments.Count} instrument(s) generating {t.dataGeneratedGBPerDay:N1} GB/day."));
            if (freeReturn != null)
            {
                Log(o, MissionPhase.ScienceOperations, 1f, LogTone.Info, Loc.L("Corrección de rumbo 1: encendido de pocos m/s para afinar la ruta.", "Trajectory correction 1: a burn of a few m/s to fine-tune the path."));
                Log(o, MissionPhase.ScienceOperations, Mathf.Max(1.2f, freeReturn.flybyTimeDays - 1f), LogTone.Info, Loc.L("Corrección de rumbo 2: ajuste final antes de la Luna.", "Trajectory correction 2: final trim before the Moon."));
                Log(o, MissionPhase.ScienceOperations, freeReturn.flybyTimeDays, LogTone.Positive,
                    Loc.L($"Sobrevuelo lunar a {freeReturn.flybyAltitudeKm:N0} km sobre la cara oculta (día {freeReturn.flybyTimeDays:F1}): campaña fotográfica de la tripulación.", $"Lunar flyby {freeReturn.flybyAltitudeKm:N0} km above the far side (day {freeReturn.flybyTimeDays:F1}): the crew photo campaign."));
            }

            var selected = settings.forcedEvents ?? PickEvents(eventPool, orbit, rng, settings.minEvents, settings.maxEvents, d.GroundNetwork);
            var scheduled = selected.Where(e => e != null)
                .Select(e => (evt: e, day: Mathf.Round(days * (0.1f + 0.8f * (float)rng.NextDouble()))))
                .OrderBy(x => x.day).ToList();

            float midLifeGen = (t.powerGeneratedAvgW + t.powerGeneratedEolW) * 0.5f;
            float powerLoss = 0f, sciMult = 1f, dataMult = 1f, commsMult = 1f;
            float lossDay = days;
            o.survivedOperations = true;
            var instrumentFailureDay = new Dictionary<InstrumentSO, float>();
            var failureCause = new Dictionary<InstrumentSO, string>();

            foreach (var (evt, day) in scheduled)
            {
                var impact = new List<string>();
                bool radHardened = evt.eventType == RandomEventType.SingleEventUpset && d.Computer != null && d.Computer.radiationHardened;
                bool buffered = evt.commsMultiplier < 1f && d.Computer != null && t.storageDays >= StorageDaysToBufferOutage;

                if (radHardened)
                {
                    impact.Add(Loc.L($"{d.Computer.displayName} (endurecida contra radiación) corrigió el error sin reiniciar: sin efecto en la ciencia", $"{d.Computer.displayName} (radiation-hardened) corrected the error without rebooting: no effect on science"));
                }
                else
                {
                    sciMult *= evt.scienceMultiplier;
                    dataMult *= evt.dataGenerationMultiplier;
                    commsMult *= buffered ? 1f : evt.commsMultiplier;
                    powerLoss = 1f - (1f - powerLoss) * (1f - evt.permanentPowerLossFraction);
                    if (buffered)
                        impact.Add(Loc.L($"la memoria a bordo ({t.onboardStorageGB:N0} GB ≈ {t.storageDays:N0} días de datos) guardó la ciencia hasta recuperar el enlace", $"the onboard memory ({t.onboardStorageGB:N0} GB ≈ {t.storageDays:N0} days of data) kept the science until the link came back"));

                    if (evt.requiresSafeMode)
                    {
                        float hk = d.Bus != null ? d.Bus.housekeepingPowerW : 0f;
                        float genNow = midLifeGen * (1f - powerLoss);
                        float usable = d.Battery != null ? d.Battery.UsableWh : 0f;
                        float needWh = genNow >= hk
                            ? hk * (orbit != null ? orbit.maxEclipseMinutes : 0f) / 60f
                            : (hk - genNow) * evt.safeModeHours;
                        if (needWh > usable)
                        {
                            o.survivedOperations = false;
                            lossDay = day;
                            impact.Add(Loc.L($"la batería ({usable:N0} Wh útiles) no sostuvo {evt.safeModeHours:N0} h de modo seguro: se necesitaban {needWh:N0} Wh. Se perdió la nave", $"the battery ({usable:N0} Wh usable) could not sustain {evt.safeModeHours:N0} h of safe mode: {needWh:N0} Wh were needed. The spacecraft was lost"));
                        }
                        else
                        {
                            impact.Add(Loc.L($"modo seguro de {evt.safeModeHours:N0} h superado ({needWh:N0} de {usable:N0} Wh de batería)", $"{evt.safeModeHours:N0} h safe mode survived ({needWh:N0} of {usable:N0} Wh of battery)"));
                        }
                    }

                    if (o.survivedOperations && evt.componentFailureChance > 0f && rng.NextDouble() < evt.componentFailureChance)
                    {
                        var alive = d.Instruments.Where(i => !instrumentFailureDay.ContainsKey(i)).Distinct().ToList();
                        if (alive.Count > 0)
                        {
                            var victim = alive[rng.Next(alive.Count)];
                            instrumentFailureDay[victim] = day;
                            failureCause[victim] = Loc.L($"{evt.displayName} (día {day:N0})", $"{evt.displayName} (day {day:N0})");
                            impact.Add(Loc.L($"daño permanente en {victim.displayName}", $"permanent damage to {victim.displayName}"));
                        }
                        else
                        {
                            powerLoss = 1f - (1f - powerLoss) * 0.7f;
                            impact.Add(Loc.L("daño en los paneles solares (−30% de potencia)", "solar panel damage (−30% power)"));
                        }
                    }

                    impact.AddRange(DescribeMultipliers(evt, skipComms: buffered));
                }

                string text = impact.Count > 0 ? Capitalize(string.Join("; ", impact)) + "." : Loc.L("Sin efectos medibles.", "No measurable effects.");
                var phase = evt.affectedPhase == MissionPhase.DataTransmission ? MissionPhase.DataTransmission : MissionPhase.ScienceOperations;
                bool positive = evt.isPositive || radHardened || buffered;
                o.events.Add(new EventOutcome
                {
                    eventId = evt.eventId, displayName = evt.displayName, phase = phase, missionDay = day, positive = evt.isPositive,
                    mitigated = radHardened || buffered,
                    commsOnly = evt.commsMultiplier < 1f && !evt.requiresSafeMode && evt.componentFailureChance <= 0f
                                && evt.permanentPowerLossFraction <= 0f && Mathf.Approximately(evt.scienceMultiplier, 1f)
                                && Mathf.Approximately(evt.dataGenerationMultiplier, 1f),
                    impact = text, mitigation = evt.mitigationAdvice, educationalFact = evt.educationalFact
                });
                Log(o, phase, day, !o.survivedOperations ? LogTone.Critical : positive ? LogTone.Positive : LogTone.Warning,
                    $"{evt.displayName}: {text}");

                if (!o.survivedOperations) break;
            }

            // ───── Resultados de operación ─────
            float peak = t.peakLoadW;
            float genW = midLifeGen * (1f - powerLoss);
            float powerFrac = peak > 0f ? Mathf.Clamp01(genW / peak) : 1f;
            float lifeFrac = t.estimatedLifeMonths > 0f && t.plannedOperationsMonths > 0f
                ? Mathf.Clamp01(t.estimatedLifeMonths / t.plannedOperationsMonths) : 1f;
            float opsDays = Mathf.Min(lossDay, days * lifeFrac);
            o.achievedOperationsDays = opsDays;

            float Alive(InstrumentSO i) =>
                instrumentFailureDay.TryGetValue(i, out float fd) ? Mathf.Clamp01(fd / Mathf.Max(1f, opsDays)) : 1f;

            float instrumentData = d.Instruments.Sum(i => i.dataVolumeGBPerDay * i.dutyCycle * Alive(i)) * dataMult * powerFrac;
            o.dataGeneratedGBPerDay = instrumentData + (d.Bus != null ? d.Bus.housekeepingDataGBPerDay : 0f);
            o.downlinkGBPerDay = t.downlinkCapacityGBPerDay * commsMult;
            o.dataReturnFraction = o.dataGeneratedGBPerDay > 0f ? Mathf.Clamp01(o.downlinkGBPerDay / o.dataGeneratedGBPerDay) : 1f;
            o.powerAvailabilityFraction = powerFrac;
            o.dataReturnedTotalTB = Mathf.Min(o.dataGeneratedGBPerDay, o.downlinkGBPerDay) * opsDays / 1000f;
            o.dataLostTotalTB = Mathf.Max(0f, o.dataGeneratedGBPerDay - o.downlinkGBPerDay) * opsDays / 1000f;

            float potential = d.Instruments.Sum(i => MissionTelemetry.InstrumentScience(i, orbit, d.AttitudeControl, d.Scenario) * Alive(i));
            float timeFrac = days > 0f ? opsDays / days : 0f;
            float science = potential * sciMult * o.dataReturnFraction * powerFrac * timeFrac;

            // ───── 4. TRANSMISIÓN DE DATOS ─────
            Log(o, MissionPhase.DataTransmission, opsDays, o.dataReturnFraction >= 0.98f ? LogTone.Positive : LogTone.Warning,
                Loc.L($"{Name(d.Antenna)} vía {NetworkName(d)} entregó {o.downlinkGBPerDay:N1} GB/día frente a {o.dataGeneratedGBPerDay:N1} GB/día generados: " +
                      $"{o.dataReturnedTotalTB:N1} TB recibidos en tierra ({o.dataReturnFraction:P0}).",
                      $"{Name(d.Antenna)} via {NetworkName(d)} delivered {o.downlinkGBPerDay:N1} GB/day against {o.dataGeneratedGBPerDay:N1} GB/day generated: " +
                      $"{o.dataReturnedTotalTB:N1} TB received on the ground ({o.dataReturnFraction:P0})."));

            float linkFraction = o.dataReturnFraction;   // lo que bajó por enlace antes de recuperar memorias

            // ───── 5. REGRESO Y RECUPERACIÓN (misiones con retorno libre) ─────
            if (flyby)
            {
                Log(o, MissionPhase.ReturnAndRecovery, Mathf.Max(0f, opsDays - 0.3f), LogTone.Info,
                    Loc.L("Corrección de rumbo 3 y separación del módulo de servicio: solo la cápsula entra en la atmósfera.", "Trajectory correction 3 and service module separation: only the capsule enters the atmosphere."));
                if (o.survivedOperations)
                {
                    float generatedTB = o.dataGeneratedGBPerDay * opsDays / 1000f;
                    float storageTB = ((d.Bus != null ? d.Bus.recoverableStorageGB : 0f) + (d.Computer != null ? d.Computer.storageGB : 0f)) / 1000f;
                    o.dataRecoveredTB = Mathf.Min(o.dataLostTotalTB, storageTB);
                    o.dataLostTotalTB -= o.dataRecoveredTB;
                    o.dataReturnedTotalTB += o.dataRecoveredTB;
                    o.dataReturnFraction = generatedTB > 0f ? Mathf.Clamp01(o.dataReturnedTotalTB / generatedTB) : 1f;
                    science = potential * sciMult * o.dataReturnFraction * powerFrac * timeFrac;
                    Log(o, MissionPhase.ReturnAndRecovery, opsDays, LogTone.Positive,
                        Loc.L($"Interfaz de reentrada a {FreeReturnTrajectory.EntryInterfaceKm:N0} km y amerizaje en el Pacífico (día {opsDays:F1}). " +
                              $"Se recuperan {o.dataRecoveredTB:N2} TB de tarjetas SD y memoria a bordo.",
                              $"Entry interface at {FreeReturnTrajectory.EntryInterfaceKm:N0} km and splashdown in the Pacific (day {opsDays:F1}). " +
                              $"{o.dataRecoveredTB:N2} TB are recovered from SD cards and onboard memory."));
                }
                else
                {
                    Log(o, MissionPhase.ReturnAndRecovery, opsDays, LogTone.Critical, Loc.L("La cápsula no regresa: los datos guardados a bordo se pierden.", "The capsule does not return: the data stored on board is lost."));
                }
            }

            ComputePhotos(d, o, flyby, opsDays, linkFraction, powerFrac);

            // ───── Veredicto y explicación ─────
            float target = o.scienceTarget > 0f ? o.scienceTarget : Mathf.Max(1f, potential);
            MissionVerdict verdict;
            string title;
            if (!o.survivedOperations) { verdict = MissionVerdict.Failure; title = o.crewed ? Loc.L("Misión abortada: cápsula no recuperada", "Mission aborted: capsule not recovered") : Loc.L("Nave perdida en operación", "Spacecraft lost in operation"); }
            else if (science >= target) { verdict = MissionVerdict.Success; title = Loc.L("Objetivos científicos cumplidos", "Science goals achieved"); }
            else if (science >= target * PartialThreshold) { verdict = MissionVerdict.PartialSuccess; title = Loc.L("Objetivos cumplidos parcialmente", "Goals partially achieved"); }
            else { verdict = MissionVerdict.Failure; title = Loc.L("Retorno científico insuficiente", "Insufficient science return"); }

            Explain(o, d, t, verdict, science, target, powerFrac, genW, lifeFrac, failureCause);
            return Finish(o, verdict, title, science, t);
        }

        // ───────────────────────────── Eventos ─────────────────────────────

        public static List<RandomEventSO> PickEvents(IReadOnlyList<RandomEventSO> pool, OrbitSO orbit, System.Random rng, int min, int max,
            GroundNetworkSO network = null)
        {
            var result = new List<RandomEventSO>();
            if (pool == null || orbit == null) return result;

            float Weight(RandomEventSO e) =>
                e.GetProbability(orbit.regime) *
                (e.eventType == RandomEventType.GroundStationOutage && network != null ? network.outageProbabilityMultiplier : 1f);

            var candidates = pool.Where(e => e != null && Weight(e) > 0f).ToList();
            int count = Mathf.Min(candidates.Count, rng.Next(min, max + 1));

            for (int n = 0; n < count; n++)
            {
                float total = candidates.Sum(Weight);
                double pick = rng.NextDouble() * total;
                for (int i = 0; i < candidates.Count; i++)
                {
                    pick -= Weight(candidates[i]);
                    if (pick <= 0 || i == candidates.Count - 1)
                    {
                        result.Add(candidates[i]);
                        candidates.RemoveAt(i);
                        break;
                    }
                }
            }
            return result;
        }

        private static IEnumerable<string> DescribeMultipliers(RandomEventSO e, bool skipComms)
        {
            if (!Mathf.Approximately(e.scienceMultiplier, 1f)) yield return $"{Loc.L("ciencia", "science")} ×{e.scienceMultiplier:0.##}";
            if (!Mathf.Approximately(e.dataGenerationMultiplier, 1f)) yield return $"{Loc.L("datos generados", "data generated")} ×{e.dataGenerationMultiplier:0.##}";
            if (!skipComms && !Mathf.Approximately(e.commsMultiplier, 1f)) yield return $"{Loc.L("capacidad de enlace", "link capacity")} ×{e.commsMultiplier:0.##}";
            if (e.permanentPowerLossFraction > 0f) yield return Loc.L($"−{e.permanentPowerLossFraction:P0} de potencia solar permanente", $"−{e.permanentPowerLossFraction:P0} permanent solar power");
        }

        // ───────────────────────────── Misión real parecida ─────────────────────────────

        /// <summary>
        /// Puntúa cada misión real: órbita igual (+3), instrumento compartido (+4 c/u),
        /// objetivo compartido (+1 c/u) y masa parecida (hasta +2).
        /// </summary>
        public static RealMissionSO FindSimilarMission(SpacecraftDesign d, TelemetryReport t, IEnumerable<RealMissionSO> missions)
        {
            if (missions == null) return null;
            var ids = new HashSet<string>(d.Instruments.Select(i => i.componentId));
            var tags = new HashSet<string>(d.Instruments.SelectMany(i => i.scienceObjectives ?? Array.Empty<string>()));
            if (d.Scenario != null && !string.IsNullOrEmpty(d.Scenario.scienceObjectiveTag)) tags.Add(d.Scenario.scienceObjectiveTag);

            RealMissionSO best = null;
            float bestScore = 0f;
            foreach (var m in missions.Where(m => m != null))
            {
                float score = 0f;
                if (d.Orbit != null && d.Orbit.regime == m.regime) score += 3f;
                score += 4f * (m.instrumentIds ?? Array.Empty<string>()).Count(ids.Contains);
                score += (m.tags ?? Array.Empty<string>()).Count(tags.Contains);
                if (m.massKg > 0f && t.totalMassKg > 0f)
                    score += 2f * Mathf.Max(0f, 1f - Mathf.Abs(Mathf.Log10(t.totalMassKg / m.massKg)) / 2f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = m;
                }
            }
            return best;
        }

        // ───────────────────────────── Explicación ─────────────────────────────

        private static void Explain(MissionOutcomeReport o, SpacecraftDesign d, TelemetryReport t, MissionVerdict verdict,
            float science, float target, float powerFrac, float genW, float lifeFrac, Dictionary<InstrumentSO, string> failed)
        {
            string prefix = verdict == MissionVerdict.Success ? Loc.L("Atención", "Note") : verdict == MissionVerdict.PartialSuccess ? Loc.L("Fallo parcial", "Partial failure") : Loc.L("Fallo", "Failure");
            string regime = d.Orbit != null ? d.Orbit.regime.ToString() : "?";

            if (verdict == MissionVerdict.Success)
                o.explanations.Add(Loc.L($"Éxito: se lograron {science:N0} puntos de ciencia de {target:N0} requeridos, con {o.dataReturnedTotalTB:N1} TB entregados a tierra.",
                                         $"Success: {science:N0} science points achieved of {target:N0} required, with {o.dataReturnedTotalTB:N1} TB delivered to the ground."));

            if (o.dataReturnFraction < 0.98f)
            {
                var top = d.Instruments.OrderByDescending(i => i.dataVolumeGBPerDay).FirstOrDefault();
                string who = top != null && d.Instruments.Count > 1
                    ? Loc.L($"los instrumentos generaron {o.dataGeneratedGBPerDay:N0} GB/día ({top.displayName} aportó {top.dataVolumeGBPerDay:N0})",
                            $"the instruments generated {o.dataGeneratedGBPerDay:N0} GB/day ({top.displayName} contributed {top.dataVolumeGBPerDay:N0})")
                    : Loc.L($"{(top != null ? "el " + top.displayName : "la carga útil")} generó {o.dataGeneratedGBPerDay:N0} GB/día",
                            $"{(top != null ? top.displayName : "the payload")} generated {o.dataGeneratedGBPerDay:N0} GB/day");
                o.explanations.Add(Loc.L($"{prefix}: {who}, pero la antena {Name(d.Antenna)} en órbita {regime} solo transmitió " +
                                         $"{o.downlinkGBPerDay:N0} GB/día. Se perdió el {1f - o.dataReturnFraction:P0} de los datos ({o.dataLostTotalTB:N1} TB).",
                                         $"{prefix}: {who}, but the {Name(d.Antenna)} antenna in {regime} orbit only transmitted " +
                                         $"{o.downlinkGBPerDay:N0} GB/day. {1f - o.dataReturnFraction:P0} of the data was lost ({o.dataLostTotalTB:N1} TB)."));
            }
            else if (o.survivedOperations && o.dataRecoveredTB <= 0f)
            {
                o.explanations.Add(Loc.L($"Bien: el enlace alcanzó — {Name(d.Antenna)} vía {NetworkName(d)} entregó el {o.dataReturnFraction:P0} de los datos ({o.dataReturnedTotalTB:N1} TB).",
                                         $"Good: the link was enough — {Name(d.Antenna)} via {NetworkName(d)} delivered {o.dataReturnFraction:P0} of the data ({o.dataReturnedTotalTB:N1} TB)."));
            }

            if (o.dataRecoveredTB > 0f)
                o.explanations.Add(Loc.L($"Bien: tras el amerizaje se recuperaron {o.dataRecoveredTB:N2} TB guardados a bordo que el enlace no alcanzó a enviar. " +
                                         "Así ocurrió en Artemis II: la tripulación tomó ~10 000 fotos con las 32 cámaras de Orion y cámaras Nikon D5/Z9; " +
                                         "parte se envió por enlace láser y el resto volvió en tarjetas SD.",
                                         $"Good: after splashdown {o.dataRecoveredTB:N2} TB stored on board, which the link could not send, were recovered. " +
                                         "That is what happened on Artemis II: the crew took ~10,000 photos with Orion's 32 cameras and Nikon D5/Z9 cameras; " +
                                         "part was sent over the laser link and the rest came back on SD cards."));

            ExplainPhotos(o);

            if (powerFrac < 0.98f)
                o.explanations.Add(Loc.L($"{prefix}: los paneles dieron {genW:N0} W de media (eclipse y degradación incluidos) frente a {t.peakLoadW:N0} W de consumo pico; " +
                                         $"los instrumentos solo operaron el {powerFrac:P0} del tiempo.",
                                         $"{prefix}: the panels gave {genW:N0} W on average (eclipse and degradation included) against a {t.peakLoadW:N0} W peak load; " +
                                         $"the instruments only operated {powerFrac:P0} of the time."));
            else if (o.survivedOperations)
                o.explanations.Add(Loc.L($"Bien: energía con margen — {genW:N0} W de media para {t.peakLoadW:N0} W de consumo pico.",
                                         $"Good: power with margin — {genW:N0} W average for a {t.peakLoadW:N0} W peak load."));

            float massMargin = Margin(t);
            if (massMargin >= 0.1f)
                o.explanations.Add(Loc.L($"Bien: margen de masa del {massMargin:P0} frente a la capacidad de {Name(d.LaunchVehicle)}.",
                                         $"Good: {massMargin:P0} mass margin against the capacity of {Name(d.LaunchVehicle)}."));

            var poorlyPointed = d.Instruments.Where(i => MissionTelemetry.PointingFactor(i, d.AttitudeControl) < 0.99f).ToList();
            foreach (var i in poorlyPointed)
                o.explanations.Add(Loc.L($"{prefix}: {Name(d.AttitudeControl)} no apunta con la precisión que pide {i.displayName} " +
                                         $"(±{i.requiredPointingDeg:0.###}°): obtuvo el {MissionTelemetry.PointingFactor(i, d.AttitudeControl):P0} de su ciencia.",
                                         $"{prefix}: {Name(d.AttitudeControl)} does not point as precisely as {i.displayName} needs " +
                                         $"(±{i.requiredPointingDeg:0.###}°): it got {MissionTelemetry.PointingFactor(i, d.AttitudeControl):P0} of its science."));

            if (lifeFrac < 1f)
                o.explanations.Add(Loc.L($"{prefix}: el bus está diseñado para {t.estimatedLifeMonths:N0} meses y la misión planificaba {t.plannedOperationsMonths:N0}.",
                                         $"{prefix}: the bus is designed for {t.estimatedLifeMonths:N0} months and the mission planned {t.plannedOperationsMonths:N0}."));

            foreach (var kv in failed)
                o.explanations.Add(Loc.L($"{prefix}: {kv.Key.displayName} quedó fuera de servicio por {kv.Value}; su ciencia se cortó a partir de ese día.",
                                         $"{prefix}: {kv.Key.displayName} went out of service due to {kv.Value}; its science stopped from that day."));

            foreach (var e in o.events.Where(e => e.positive))
                o.explanations.Add(Loc.L($"A favor: {e.displayName} — {e.impact}", $"Plus: {e.displayName} — {e.impact}"));
            foreach (var e in o.events.Where(e => e.mitigated && !e.positive))
                o.explanations.Add(Loc.L($"A favor: tu diseño resistió {e.displayName} — {e.impact}", $"Plus: your design withstood {e.displayName} — {e.impact}"));

            // Eventos negativos no mitigados: su coste también explica el resultado. Si solo recortaron
            // el enlace y aun así llegó todo, el margen de diseño los absorbió (y eso es mérito del equipo).
            if (o.survivedOperations)
                foreach (var e in o.events.Where(e => !e.positive && !e.mitigated))
                    o.explanations.Add(e.commsOnly && o.dataReturnFraction >= 0.98f
                        ? Loc.L($"A favor: el margen del enlace absorbió {e.displayName} — aun con la capacidad recortada llegó el {o.dataReturnFraction:P0} de los datos.",
                                $"Plus: the link margin absorbed {e.displayName} — even with reduced capacity {o.dataReturnFraction:P0} of the data arrived.")
                        : Loc.L($"En contra: {e.displayName} — {e.impact}", $"Minus: {e.displayName} — {e.impact}"));

            if (!o.survivedOperations)
            {
                var fatal = o.events.LastOrDefault();
                o.explanations.Add(Loc.L($"Fallo: {fatal.displayName} el día {fatal.missionDay:N0}. {fatal.impact} {fatal.mitigation}",
                                         $"Failure: {fatal.displayName} on day {fatal.missionDay:N0}. {fatal.impact} {fatal.mitigation}"));
            }

            if (verdict != MissionVerdict.Success && o.survivedOperations)
                o.explanations.Add(Loc.L($"Ciencia lograda {science:N0} de {target:N0} requerida ({science / Mathf.Max(1f, target):P0}).",
                                         $"Science achieved {science:N0} of {target:N0} required ({science / Mathf.Max(1f, target):P0})."));

            if (t.budgetLimitMUSD > 0f)
                o.explanations.Add(Loc.L($"Programa: ${t.totalCostMUSD:N1} M de ${t.budgetLimitMUSD:N0} M " +
                                         $"({(t.budgetLimitMUSD - t.totalCostMUSD) / t.budgetLimitMUSD:P0} de reserva) y {t.developmentMonths:N0} meses de desarrollo.",
                                         $"Program: ${t.totalCostMUSD:N1} M of ${t.budgetLimitMUSD:N0} M " +
                                         $"({(t.budgetLimitMUSD - t.totalCostMUSD) / t.budgetLimitMUSD:P0} reserve) and {t.developmentMonths:N0} months of development."));
        }

        /// <summary>Reparte las explicaciones en las tarjetas "Lo que salió bien" / "Lo que falló".</summary>
        private static void Classify(MissionOutcomeReport o)
        {
            o.wentWell.Clear();
            o.wentWrong.Clear();
            float reserve = o.budgetLimitMUSD > 0f ? (o.budgetLimitMUSD - o.totalCostMUSD) / o.budgetLimitMUSD : 0f;
            foreach (var e in o.explanations)
            {
                bool good = e.StartsWith("Éxito") || e.StartsWith("A favor") || e.StartsWith("Bien")
                            || e.StartsWith("Success") || e.StartsWith("Plus") || e.StartsWith("Good")
                            || ((e.StartsWith("Programa:") || e.StartsWith("Program:")) && reserve >= MissionTelemetry.BudgetReserveFraction);
                (good ? o.wentWell : o.wentWrong).Add(e);
            }
        }

        /// <summary>Añade la evaluación del trabajo en equipo (la mide el CollaborationTracker) al reporte.</summary>
        public static void ApplyCollaboration(MissionOutcomeReport o, int score, string notes)
        {
            o.collaborationScore = Mathf.Clamp(score, 0, 10);
            o.collaborationNotes = notes;
            o.explanations.Add(Loc.L($"{(o.collaborationScore >= 6 ? "Bien" : "Atención")}: trabajo en equipo {o.collaborationScore}/10 — {notes}.",
                                     $"{(o.collaborationScore >= 6 ? "Good" : "Note")}: teamwork {o.collaborationScore}/10 — {notes}."));
            Classify(o);
        }

        public static string RankFor(int score) =>
            score >= 90 ? "A" : score >= 75 ? "B" : score >= 60 ? "C" : score >= 40 ? "D" : "F";

        // ───────────────────────────── Utilidades ─────────────────────────────

        private static MissionOutcomeReport NewReport(SpacecraftDesign d, TelemetryReport t, int seed) => new MissionOutcomeReport
        {
            generatedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            seed = seed,
            difficulty = d.Difficulty,
            scenarioId = d.Scenario != null ? d.Scenario.scenarioId : string.Empty,
            scenarioName = d.Scenario != null ? d.Scenario.displayName : Loc.L("(sin escenario)", "(no scenario)"),
            orbit = d.Orbit != null ? d.Orbit.displayName : Loc.L("(sin órbita)", "(no orbit)"),
            launchVehicle = Name(d.LaunchVehicle),
            bus = Name(d.Bus),
            solarArray = Name(d.SolarArray),
            battery = Name(d.Battery),
            antenna = Name(d.Antenna),
            groundNetwork = NetworkName(d),
            computer = Name(d.Computer),
            attitudeControl = Name(d.AttitudeControl),
            instruments = d.Instruments.Select(i => i.displayName).ToArray(),
            onboardDeltaVKmS = t.spacecraftDeltaVKmS,
            requiredOnboardDeltaVKmS = t.requiredOnboardDeltaVKmS,
            plannedOperationsDays = t.plannedOperationsMonths * DaysPerMonth,
            scienceTarget = d.Scenario != null ? d.Scenario.minScienceValue : 0f,
            totalCostMUSD = t.totalCostMUSD,
            budgetLimitMUSD = t.budgetLimitMUSD,
            developmentMonths = t.developmentMonths,
            designTelemetry = t
        };

        private static MissionOutcomeReport Finish(MissionOutcomeReport o, MissionVerdict verdict, string title, float science, TelemetryReport t)
        {
            o.verdict = verdict;
            o.verdictTitle = title;
            o.scienceAchieved = science;
            o.score = Score(o, t);
            o.rank = RankFor(o.score);
            Classify(o);
            Log(o, MissionPhase.PostMission, o.achievedOperationsDays, verdict == MissionVerdict.Success ? LogTone.Positive
                : verdict == MissionVerdict.PartialSuccess ? LogTone.Warning : LogTone.Critical,
                $"{MissionOutcomeReport.VerdictLabel(verdict)}: {title}.");
            return o;
        }

        /// <summary>0-100: ciencia (60) + reserva de presupuesto (20) + retorno de datos (10) + calendario (10).</summary>
        private static int Score(MissionOutcomeReport o, TelemetryReport t)
        {
            if (!o.launchSucceeded || !o.orbitInsertionSucceeded) return 0;
            float target = o.scienceTarget > 0f ? o.scienceTarget : Mathf.Max(1f, o.scienceAchieved);
            float science = Mathf.Clamp01(o.scienceAchieved / target) * 60f;
            // Sin límite (Sandbox) no hay nada que incumplir: puntos completos. Antes el Sandbox no podía pasar de 80 (B).
            float budget = t.budgetLimitMUSD > 0f ? Mathf.Clamp01((t.budgetLimitMUSD - t.totalCostMUSD) / (t.budgetLimitMUSD * 0.3f)) * 20f : 20f;
            float data = o.dataReturnFraction * 10f;
            float time = t.maxDevelopmentMonths <= 0f || t.developmentMonths <= t.maxDevelopmentMonths ? 10f : 0f;
            return Mathf.RoundToInt(science + budget + data + time);
        }

        private static void Log(MissionOutcomeReport o, MissionPhase phase, float day, LogTone tone, string message) =>
            o.timeline.Add(new SimulationLogEntry { phase = phase, missionDay = day, tone = tone, message = message });

        private static float Margin(TelemetryReport t) =>
            t.launchCapacityKg > 0f ? (t.launchCapacityKg - t.totalMassKg) / t.launchCapacityKg : 0f;

        private static string Name(LaunchVehicleSO lv) => lv != null ? lv.displayName : Loc.L("(sin lanzador)", "(no launcher)");
        private static string Name(SpacecraftComponentSO c) => c != null ? c.displayName : Loc.L("(ninguno)", "(none)");
        private static string NetworkName(SpacecraftDesign d) => d.GroundNetwork != null ? d.GroundNetwork.displayName : Loc.L("(sin red terrena)", "(no ground network)");

        private static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        // ───── Fotografía de exploración ─────

        /// <summary>
        /// Cámaras + destino lunar → fotografías. Con retorno libre, lo que el enlace no envió vuelve en las tarjetas SD
        /// (como en Artemis II); si la cápsula se pierde, solo queda lo transmitido.
        /// </summary>
        public static void ComputePhotos(SpacecraftDesign d, MissionOutcomeReport o, bool flyby, float opsDays, float linkFraction, float powerFrac)
        {
            var cams = d.Instruments.Where(i => i != null && i.isCamera).ToList();
            o.cameras = cams.Select(c => c.displayName).ToArray();
            var orbit = d.Orbit;
            bool lunarTarget = orbit != null && (orbit.regime == OrbitRegime.Lunar || flyby);
            bool inSpace = o.launchSucceeded && o.orbitInsertionSucceeded;
            o.reachedMoon = lunarTarget && inSpace;
            o.photoTarget = lunarTarget ? Loc.L("Luna", "Moon") : Loc.L("Tierra", "Earth");
            o.photosTaken = o.photosTransmitted = o.photosRecovered = o.photoShots = o.photoShotsAvailable = 0;
            // En órbita terrestre las cámaras fotografían la Tierra; en destino lunar, la Luna.
            if (cams.Count == 0 || !inSpace) return;

            float window = flyby ? 1.2f : Mathf.Clamp(opsDays, 0f, lunarTarget ? 30f : 10f);
            if (!o.survivedOperations && !flyby) window *= o.plannedOperationsDays > 0f ? Mathf.Clamp01(o.achievedOperationsDays / o.plannedOperationsDays) : 0f;
            o.photosTaken = Mathf.RoundToInt(cams.Sum(c => c.photosPerDay) * window * Mathf.Clamp01(powerFrac));
            o.photoShots = Mathf.Clamp(4 + 2 * cams.Count, 4, 8);
            float sent = Mathf.Clamp01(linkFraction);
            o.photosTransmitted = Mathf.RoundToInt(o.photosTaken * sent);
            bool capsuleBack = flyby && o.survivedOperations;
            o.photosRecovered = capsuleBack ? o.photosTaken - o.photosTransmitted : 0;
            o.photoShotsAvailable = capsuleBack ? o.photoShots : Mathf.Clamp(Mathf.RoundToInt(o.photoShots * sent), o.photosTransmitted > 0 ? 1 : 0, o.photoShots);
        }

        private static void ExplainPhotos(MissionOutcomeReport o)
        {
            if (o.cameras == null || o.cameras.Length == 0) return;
            string cams = string.Join(", ", o.cameras);
            if (o.photosTaken <= 0)
            {
                o.explanations.Add(Loc.L($"Falló: las cámaras ({cams}) no tomaron fotografías porque la nave no llegó a su órbita.",
                                         $"Failed: the cameras ({cams}) took no photos because the spacecraft did not reach its orbit."));
                return;
            }
            string recovered = o.photosRecovered > 0 ? Loc.L($" y {o.photosRecovered:N0} en tarjetas SD", $" and {o.photosRecovered:N0} on SD cards") : string.Empty;
            string target = o.reachedMoon ? Loc.L("de la Luna", "of the Moon") : Loc.L("de la Tierra desde órbita", "of Earth from orbit");
            o.explanations.Add(Loc.L($"Bien: las cámaras tomaron ~{o.photosTaken:N0} fotos {target} ({o.photosTransmitted:N0} por el enlace{recovered}). " +
                                     $"Cámaras: {cams}. Revísalas en el «Informe de la exploración»." +
                                     (o.reachedMoon ? string.Empty : " Para fotografiar la Luna elige un destino lunar."),
                                     $"Good: the cameras took ~{o.photosTaken:N0} photos {target} ({o.photosTransmitted:N0} over the link{recovered}). " +
                                     $"Cameras: {cams}. See them in the \"Exploration report\"." +
                                     (o.reachedMoon ? string.Empty : " To photograph the Moon, choose a lunar destination.")));
            if (o.photoShotsAvailable < o.photoShots)
                o.explanations.Add(Loc.L($"Falló: se perdieron fotografías guardadas a bordo; solo llegaron a tierra las transmitidas ({o.photosTransmitted:N0}).",
                                     $"Failed: photos stored on board were lost; only the transmitted ones reached the ground ({o.photosTransmitted:N0})."));
        }
    }
}
