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
        public static readonly (string time, string text)[] SlsAscent =
        {
            ("T+1:11", "Max Q: máxima presión aerodinámica (12,8 km)"),
            ("T+2:09", "Separación de los propulsores sólidos (47,5 km)"),
            ("T+3:18", "Expulsión del sistema de aborto de lanzamiento (87 km)"),
            ("T+8:15", "Apagado y separación de la etapa central (162,5 km)"),
            ("T+49 min", "La etapa superior (ICPS) eleva el perigeo"),
            ("T+1 h 48 min", "La ICPS eleva el apogeo: órbita terrestre alta"),
            ("T+3 h 24 min", "Orion se separa de la ICPS a 22 279 km")
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
                Log(o, MissionPhase.Launch, 0, LogTone.Critical, $"Anomalía en el ascenso: {Name(lv)} no alcanzó la órbita.");
                o.explanations.Add($"Fallo: el lanzador {Name(lv)} falló durante el ascenso (confiabilidad histórica {reliability:P0}). " +
                                   "Ningún diseño elimina este riesgo; por eso se eligen lanzadores con historial y se contratan seguros.");
                return Finish(o, MissionVerdict.Failure, "Pérdida en el lanzamiento", 0f, t);
            }
            Log(o, MissionPhase.Launch, 0, LogTone.Positive,
                $"Despegue nominal de {Name(lv)}: {t.totalMassKg:N0} kg hacia {regime} (Δv {orbit?.requiredDeltaVKmS:F1} km/s, margen de carga {Margin(t):P0}).");
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
                Log(o, MissionPhase.OrbitInsertion, 1, LogTone.Critical, "Combustible agotado antes de completar la inserción.");
                o.explanations.Add($"Fallo: para entrar en {regime} el motor del bus debía aportar {t.requiredOnboardDeltaVKmS:F2} km/s, " +
                                   $"pero con {o.bus} solo alcanzó {t.spacecraftDeltaVKmS:F2} km/s.");
                return Finish(o, MissionVerdict.Failure, "Inserción orbital fallida", 0f, t);
            }
            Log(o, MissionPhase.OrbitInsertion, 1, LogTone.Positive, t.requiredOnboardDeltaVKmS > 0.1f
                ? $"Inserción en {regime} completada: el motor aportó {t.requiredOnboardDeltaVKmS:F2} de {t.spacecraftDeltaVKmS:F2} km/s disponibles."
                : $"Separación e inserción en {regime} completadas. Paneles desplegados.");
            if (freeReturn != null)
            {
                Log(o, MissionPhase.OrbitInsertion, 0.5f, LogTone.Info,
                    "Órbita terrestre alta: ~24 h de verificación de sistemas y pruebas de proximidad (como en Artemis II).");
                Log(o, MissionPhase.OrbitInsertion, 1, LogTone.Positive,
                    $"Inyección translunar (TLI): Δv {freeReturn.tliDeltaVKmS:F2} km/s → {freeReturn.injectionSpeedKmS:F2} km/s. Comienza la ida en retorno libre.");
            }

            // ───── 3. OPERACIONES CIENTÍFICAS (con eventos) ─────
            Log(o, MissionPhase.ScienceOperations, 2, LogTone.Info,
                $"Inicio de operaciones: {d.Instruments.Count} instrumento(s) generando {t.dataGeneratedGBPerDay:N1} GB/día.");
            if (freeReturn != null)
            {
                Log(o, MissionPhase.ScienceOperations, 1f, LogTone.Info, "Corrección de rumbo 1: encendido de pocos m/s para afinar la ruta.");
                Log(o, MissionPhase.ScienceOperations, Mathf.Max(1.2f, freeReturn.flybyTimeDays - 1f), LogTone.Info, "Corrección de rumbo 2: ajuste final antes de la Luna.");
                Log(o, MissionPhase.ScienceOperations, freeReturn.flybyTimeDays, LogTone.Positive,
                    $"Sobrevuelo lunar a {freeReturn.flybyAltitudeKm:N0} km sobre la cara oculta (día {freeReturn.flybyTimeDays:F1}): campaña fotográfica de la tripulación.");
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
                    impact.Add($"{d.Computer.displayName} (endurecida contra radiación) corrigió el error sin reiniciar: sin efecto en la ciencia");
                }
                else
                {
                    sciMult *= evt.scienceMultiplier;
                    dataMult *= evt.dataGenerationMultiplier;
                    commsMult *= buffered ? 1f : evt.commsMultiplier;
                    powerLoss = 1f - (1f - powerLoss) * (1f - evt.permanentPowerLossFraction);
                    if (buffered)
                        impact.Add($"la memoria a bordo ({t.onboardStorageGB:N0} GB ≈ {t.storageDays:N0} días de datos) guardó la ciencia hasta recuperar el enlace");

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
                            impact.Add($"la batería ({usable:N0} Wh útiles) no sostuvo {evt.safeModeHours:N0} h de modo seguro: se necesitaban {needWh:N0} Wh. Se perdió la nave");
                        }
                        else
                        {
                            impact.Add($"modo seguro de {evt.safeModeHours:N0} h superado ({needWh:N0} de {usable:N0} Wh de batería)");
                        }
                    }

                    if (o.survivedOperations && evt.componentFailureChance > 0f && rng.NextDouble() < evt.componentFailureChance)
                    {
                        var alive = d.Instruments.Where(i => !instrumentFailureDay.ContainsKey(i)).Distinct().ToList();
                        if (alive.Count > 0)
                        {
                            var victim = alive[rng.Next(alive.Count)];
                            instrumentFailureDay[victim] = day;
                            failureCause[victim] = $"{evt.displayName} (día {day:N0})";
                            impact.Add($"daño permanente en {victim.displayName}");
                        }
                        else
                        {
                            powerLoss = 1f - (1f - powerLoss) * 0.7f;
                            impact.Add("daño en los paneles solares (−30% de potencia)");
                        }
                    }

                    impact.AddRange(DescribeMultipliers(evt, skipComms: buffered));
                }

                string text = impact.Count > 0 ? Capitalize(string.Join("; ", impact)) + "." : "Sin efectos medibles.";
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
                $"{Name(d.Antenna)} vía {NetworkName(d)} entregó {o.downlinkGBPerDay:N1} GB/día frente a {o.dataGeneratedGBPerDay:N1} GB/día generados: " +
                $"{o.dataReturnedTotalTB:N1} TB recibidos en tierra ({o.dataReturnFraction:P0}).");

            float linkFraction = o.dataReturnFraction;   // lo que bajó por enlace antes de recuperar memorias

            // ───── 5. REGRESO Y RECUPERACIÓN (misiones con retorno libre) ─────
            if (flyby)
            {
                Log(o, MissionPhase.ReturnAndRecovery, Mathf.Max(0f, opsDays - 0.3f), LogTone.Info,
                    "Corrección de rumbo 3 y separación del módulo de servicio: solo la cápsula entra en la atmósfera.");
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
                        $"Interfaz de reentrada a {FreeReturnTrajectory.EntryInterfaceKm:N0} km y amerizaje en el Pacífico (día {opsDays:F1}). " +
                        $"Se recuperan {o.dataRecoveredTB:N2} TB de tarjetas SD y memoria a bordo.");
                }
                else
                {
                    Log(o, MissionPhase.ReturnAndRecovery, opsDays, LogTone.Critical, "La cápsula no regresa: los datos guardados a bordo se pierden.");
                }
            }

            ComputePhotos(d, o, flyby, opsDays, linkFraction, powerFrac);

            // ───── Veredicto y explicación ─────
            float target = o.scienceTarget > 0f ? o.scienceTarget : Mathf.Max(1f, potential);
            MissionVerdict verdict;
            string title;
            if (!o.survivedOperations) { verdict = MissionVerdict.Failure; title = o.crewed ? "Misión abortada: cápsula no recuperada" : "Nave perdida en operación"; }
            else if (science >= target) { verdict = MissionVerdict.Success; title = "Objetivos científicos cumplidos"; }
            else if (science >= target * PartialThreshold) { verdict = MissionVerdict.PartialSuccess; title = "Objetivos cumplidos parcialmente"; }
            else { verdict = MissionVerdict.Failure; title = "Retorno científico insuficiente"; }

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
            if (!Mathf.Approximately(e.scienceMultiplier, 1f)) yield return $"ciencia ×{e.scienceMultiplier:0.##}";
            if (!Mathf.Approximately(e.dataGenerationMultiplier, 1f)) yield return $"datos generados ×{e.dataGenerationMultiplier:0.##}";
            if (!skipComms && !Mathf.Approximately(e.commsMultiplier, 1f)) yield return $"capacidad de enlace ×{e.commsMultiplier:0.##}";
            if (e.permanentPowerLossFraction > 0f) yield return $"−{e.permanentPowerLossFraction:P0} de potencia solar permanente";
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
            string prefix = verdict == MissionVerdict.Success ? "Atención" : verdict == MissionVerdict.PartialSuccess ? "Fallo parcial" : "Fallo";
            string regime = d.Orbit != null ? d.Orbit.regime.ToString() : "?";

            if (verdict == MissionVerdict.Success)
                o.explanations.Add($"Éxito: se lograron {science:N0} puntos de ciencia de {target:N0} requeridos, con {o.dataReturnedTotalTB:N1} TB entregados a tierra.");

            if (o.dataReturnFraction < 0.98f)
            {
                var top = d.Instruments.OrderByDescending(i => i.dataVolumeGBPerDay).FirstOrDefault();
                string who = top != null && d.Instruments.Count > 1
                    ? $"los instrumentos generaron {o.dataGeneratedGBPerDay:N0} GB/día ({top.displayName} aportó {top.dataVolumeGBPerDay:N0})"
                    : $"{(top != null ? "el " + top.displayName : "la carga útil")} generó {o.dataGeneratedGBPerDay:N0} GB/día";
                o.explanations.Add($"{prefix}: {who}, pero la antena {Name(d.Antenna)} en órbita {regime} solo transmitió " +
                                   $"{o.downlinkGBPerDay:N0} GB/día. Se perdió el {1f - o.dataReturnFraction:P0} de los datos ({o.dataLostTotalTB:N1} TB).");
            }
            else if (o.survivedOperations && o.dataRecoveredTB <= 0f)
            {
                o.explanations.Add($"Bien: el enlace alcanzó — {Name(d.Antenna)} vía {NetworkName(d)} entregó el {o.dataReturnFraction:P0} de los datos ({o.dataReturnedTotalTB:N1} TB).");
            }

            if (o.dataRecoveredTB > 0f)
                o.explanations.Add($"Bien: tras el amerizaje se recuperaron {o.dataRecoveredTB:N2} TB guardados a bordo que el enlace no alcanzó a enviar. " +
                                   "Así ocurrió en Artemis II: la tripulación tomó ~10 000 fotos con las 32 cámaras de Orion y cámaras Nikon D5/Z9; " +
                                   "parte se envió por enlace láser y el resto volvió en tarjetas SD.");

            ExplainPhotos(o);

            if (powerFrac < 0.98f)
                o.explanations.Add($"{prefix}: los paneles dieron {genW:N0} W de media (eclipse y degradación incluidos) frente a {t.peakLoadW:N0} W de consumo pico; " +
                                   $"los instrumentos solo operaron el {powerFrac:P0} del tiempo.");
            else if (o.survivedOperations)
                o.explanations.Add($"Bien: energía con margen — {genW:N0} W de media para {t.peakLoadW:N0} W de consumo pico.");

            float massMargin = Margin(t);
            if (massMargin >= 0.1f)
                o.explanations.Add($"Bien: margen de masa del {massMargin:P0} frente a la capacidad de {Name(d.LaunchVehicle)}.");

            var poorlyPointed = d.Instruments.Where(i => MissionTelemetry.PointingFactor(i, d.AttitudeControl) < 0.99f).ToList();
            foreach (var i in poorlyPointed)
                o.explanations.Add($"{prefix}: {Name(d.AttitudeControl)} no apunta con la precisión que pide {i.displayName} " +
                                   $"(±{i.requiredPointingDeg:0.###}°): obtuvo el {MissionTelemetry.PointingFactor(i, d.AttitudeControl):P0} de su ciencia.");

            if (lifeFrac < 1f)
                o.explanations.Add($"{prefix}: el bus está diseñado para {t.estimatedLifeMonths:N0} meses y la misión planificaba {t.plannedOperationsMonths:N0}.");

            foreach (var kv in failed)
                o.explanations.Add($"{prefix}: {kv.Key.displayName} quedó fuera de servicio por {kv.Value}; su ciencia se cortó a partir de ese día.");

            foreach (var e in o.events.Where(e => e.positive))
                o.explanations.Add($"A favor: {e.displayName} — {e.impact}");
            foreach (var e in o.events.Where(e => e.mitigated && !e.positive))
                o.explanations.Add($"A favor: tu diseño resistió {e.displayName} — {e.impact}");

            // Eventos negativos no mitigados: su coste también explica el resultado. Si solo recortaron
            // el enlace y aun así llegó todo, el margen de diseño los absorbió (y eso es mérito del equipo).
            if (o.survivedOperations)
                foreach (var e in o.events.Where(e => !e.positive && !e.mitigated))
                    o.explanations.Add(e.commsOnly && o.dataReturnFraction >= 0.98f
                        ? $"A favor: el margen del enlace absorbió {e.displayName} — aun con la capacidad recortada llegó el {o.dataReturnFraction:P0} de los datos."
                        : $"En contra: {e.displayName} — {e.impact}");

            if (!o.survivedOperations)
            {
                var fatal = o.events.LastOrDefault();
                o.explanations.Add($"Fallo: {fatal.displayName} el día {fatal.missionDay:N0}. {fatal.impact} {fatal.mitigation}");
            }

            if (verdict != MissionVerdict.Success && o.survivedOperations)
                o.explanations.Add($"Ciencia lograda {science:N0} de {target:N0} requerida ({science / Mathf.Max(1f, target):P0}).");

            if (t.budgetLimitMUSD > 0f)
                o.explanations.Add($"Programa: ${t.totalCostMUSD:N1} M de ${t.budgetLimitMUSD:N0} M " +
                                   $"({(t.budgetLimitMUSD - t.totalCostMUSD) / t.budgetLimitMUSD:P0} de reserva) y {t.developmentMonths:N0} meses de desarrollo.");
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
                            || (e.StartsWith("Programa:") && reserve >= MissionTelemetry.BudgetReserveFraction);
                (good ? o.wentWell : o.wentWrong).Add(e);
            }
        }

        /// <summary>Añade la evaluación del trabajo en equipo (la mide el CollaborationTracker) al reporte.</summary>
        public static void ApplyCollaboration(MissionOutcomeReport o, int score, string notes)
        {
            o.collaborationScore = Mathf.Clamp(score, 0, 10);
            o.collaborationNotes = notes;
            o.explanations.Add($"{(o.collaborationScore >= 6 ? "Bien" : "Atención")}: trabajo en equipo {o.collaborationScore}/10 — {notes}.");
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
            scenarioName = d.Scenario != null ? d.Scenario.displayName : "(sin escenario)",
            orbit = d.Orbit != null ? d.Orbit.displayName : "(sin órbita)",
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

        private static string Name(LaunchVehicleSO lv) => lv != null ? lv.displayName : "(sin lanzador)";
        private static string Name(SpacecraftComponentSO c) => c != null ? c.displayName : "(ninguno)";
        private static string NetworkName(SpacecraftDesign d) => d.GroundNetwork != null ? d.GroundNetwork.displayName : "(sin red terrena)";

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
            o.photoTarget = lunarTarget ? "Luna" : "Tierra";
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
                o.explanations.Add($"Falló: las cámaras ({cams}) no tomaron fotografías porque la nave no llegó a su órbita.");
                return;
            }
            string recovered = o.photosRecovered > 0 ? $" y {o.photosRecovered:N0} en tarjetas SD" : string.Empty;
            string target = o.reachedMoon ? "de la Luna" : "de la Tierra desde órbita";
            o.explanations.Add($"Bien: las cámaras tomaron ~{o.photosTaken:N0} fotos {target} ({o.photosTransmitted:N0} por el enlace{recovered}). " +
                               $"Cámaras: {cams}. Revísalas en el «Informe de la exploración»." +
                               (o.reachedMoon ? string.Empty : " Para fotografiar la Luna elige un destino lunar."));
            if (o.photoShotsAvailable < o.photoShots)
                o.explanations.Add($"Falló: se perdieron fotografías guardadas a bordo; solo llegaron a tierra las transmitidas ({o.photosTransmitted:N0}).");
        }
    }
}
