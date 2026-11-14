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
    }

    /// <summary>
    /// Simulación acelerada por fases: Lanzamiento → Inserción orbital → Operaciones científicas →
    /// Transmisión de datos. Es una función pura: calcula todo el resultado de una vez; el
    /// MissionSimulationRunner lo reproduce después en pantalla.
    /// </summary>
    public static class MissionSimulator
    {
        public const float DaysPerMonth = 30.44f;
        /// <summary>Por debajo de esta fracción de la ciencia objetivo la misión es un fallo.</summary>
        public const float PartialThreshold = 0.4f;

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

            // ───── 3. OPERACIONES CIENTÍFICAS (con eventos) ─────
            Log(o, MissionPhase.ScienceOperations, 2, LogTone.Info,
                $"Inicio de operaciones: {d.Instruments.Count} instrumento(s) generando {t.dataGeneratedGBPerDay:N1} GB/día.");

            var selected = settings.forcedEvents ?? PickEvents(eventPool, orbit, rng, settings.minEvents, settings.maxEvents);
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
                sciMult *= evt.scienceMultiplier;
                dataMult *= evt.dataGenerationMultiplier;
                commsMult *= evt.commsMultiplier;
                powerLoss = 1f - (1f - powerLoss) * (1f - evt.permanentPowerLossFraction);
                var impact = new List<string>();

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

                impact.AddRange(DescribeMultipliers(evt));
                string text = impact.Count > 0 ? Capitalize(string.Join("; ", impact)) + "." : "Sin efectos medibles.";
                var phase = evt.affectedPhase == MissionPhase.DataTransmission ? MissionPhase.DataTransmission : MissionPhase.ScienceOperations;
                o.events.Add(new EventOutcome
                {
                    eventId = evt.eventId, displayName = evt.displayName, phase = phase, missionDay = day, positive = evt.isPositive,
                    impact = text, mitigation = evt.mitigationAdvice, educationalFact = evt.educationalFact
                });
                Log(o, phase, day, !o.survivedOperations ? LogTone.Critical : evt.isPositive ? LogTone.Positive : LogTone.Warning,
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

            float potential = d.Instruments.Sum(i => i.scienceValue * Alive(i) *
                (orbit != null && i.IsPreferredOrbit(orbit.regime) ? 1f : MissionTelemetry.OffOrbitScienceFactor));
            float timeFrac = days > 0f ? opsDays / days : 0f;
            float science = potential * sciMult * o.dataReturnFraction * powerFrac * timeFrac;

            // ───── 4. TRANSMISIÓN DE DATOS ─────
            Log(o, MissionPhase.DataTransmission, opsDays, o.dataReturnFraction >= 0.98f ? LogTone.Positive : LogTone.Warning,
                $"{Name(d.Antenna)} entregó {o.downlinkGBPerDay:N1} GB/día frente a {o.dataGeneratedGBPerDay:N1} GB/día generados: " +
                $"{o.dataReturnedTotalTB:N1} TB recibidos en tierra ({o.dataReturnFraction:P0}).");

            // ───── Veredicto y explicación ─────
            float target = o.scienceTarget > 0f ? o.scienceTarget : Mathf.Max(1f, potential);
            MissionVerdict verdict;
            string title;
            if (!o.survivedOperations) { verdict = MissionVerdict.Failure; title = "Nave perdida en operación"; }
            else if (science >= target) { verdict = MissionVerdict.Success; title = "Objetivos científicos cumplidos"; }
            else if (science >= target * PartialThreshold) { verdict = MissionVerdict.PartialSuccess; title = "Objetivos cumplidos parcialmente"; }
            else { verdict = MissionVerdict.Failure; title = "Retorno científico insuficiente"; }

            Explain(o, d, t, verdict, science, target, powerFrac, genW, lifeFrac, failureCause);
            return Finish(o, verdict, title, science, t);
        }

        // ───────────────────────────── Eventos ─────────────────────────────

        public static List<RandomEventSO> PickEvents(IReadOnlyList<RandomEventSO> pool, OrbitSO orbit, System.Random rng, int min, int max)
        {
            var result = new List<RandomEventSO>();
            if (pool == null || orbit == null) return result;
            var candidates = pool.Where(e => e != null && e.GetProbability(orbit.regime) > 0f).ToList();
            int count = Mathf.Min(candidates.Count, rng.Next(min, max + 1));

            for (int n = 0; n < count; n++)
            {
                float total = candidates.Sum(e => e.GetProbability(orbit.regime));
                double pick = rng.NextDouble() * total;
                for (int i = 0; i < candidates.Count; i++)
                {
                    pick -= candidates[i].GetProbability(orbit.regime);
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

        private static IEnumerable<string> DescribeMultipliers(RandomEventSO e)
        {
            if (!Mathf.Approximately(e.scienceMultiplier, 1f)) yield return $"ciencia ×{e.scienceMultiplier:0.##}";
            if (!Mathf.Approximately(e.dataGenerationMultiplier, 1f)) yield return $"datos generados ×{e.dataGenerationMultiplier:0.##}";
            if (!Mathf.Approximately(e.commsMultiplier, 1f)) yield return $"capacidad de enlace ×{e.commsMultiplier:0.##}";
            if (e.permanentPowerLossFraction > 0f) yield return $"−{e.permanentPowerLossFraction:P0} de potencia solar permanente";
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

            if (powerFrac < 0.98f)
                o.explanations.Add($"{prefix}: los paneles dieron {genW:N0} W de media (eclipse y degradación incluidos) frente a {t.peakLoadW:N0} W de consumo pico; " +
                                   $"los instrumentos solo operaron el {powerFrac:P0} del tiempo.");

            if (lifeFrac < 1f)
                o.explanations.Add($"{prefix}: el bus está diseñado para {t.estimatedLifeMonths:N0} meses y la misión planificaba {t.plannedOperationsMonths:N0}.");

            foreach (var kv in failed)
                o.explanations.Add($"{prefix}: {kv.Key.displayName} quedó fuera de servicio por {kv.Value}; su ciencia se cortó a partir de ese día.");

            foreach (var e in o.events.Where(e => e.positive))
                o.explanations.Add($"A favor: {e.displayName} — {e.impact}");

            // Eventos negativos superados: su coste también explica el resultado.
            if (o.survivedOperations)
                foreach (var e in o.events.Where(e => !e.positive))
                    o.explanations.Add($"En contra: {e.displayName} — {e.impact}");

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

        // ───────────────────────────── Utilidades ─────────────────────────────

        private static MissionOutcomeReport NewReport(SpacecraftDesign d, TelemetryReport t, int seed) => new MissionOutcomeReport
        {
            generatedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            seed = seed,
            scenarioId = d.Scenario != null ? d.Scenario.scenarioId : string.Empty,
            scenarioName = d.Scenario != null ? d.Scenario.displayName : "(sin escenario)",
            orbit = d.Orbit != null ? d.Orbit.displayName : "(sin órbita)",
            launchVehicle = Name(d.LaunchVehicle),
            bus = Name(d.Bus),
            solarArray = Name(d.SolarArray),
            battery = Name(d.Battery),
            antenna = Name(d.Antenna),
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
            float budget = t.budgetLimitMUSD > 0f ? Mathf.Clamp01((t.budgetLimitMUSD - t.totalCostMUSD) / (t.budgetLimitMUSD * 0.3f)) * 20f : 10f;
            float data = o.dataReturnFraction * 10f;
            float time = t.maxDevelopmentMonths > 0f && t.developmentMonths <= t.maxDevelopmentMonths ? 10f : 0f;
            return Mathf.RoundToInt(science + budget + data + time);
        }

        private static void Log(MissionOutcomeReport o, MissionPhase phase, float day, LogTone tone, string message) =>
            o.timeline.Add(new SimulationLogEntry { phase = phase, missionDay = day, tone = tone, message = message });

        private static float Margin(TelemetryReport t) =>
            t.launchCapacityKg > 0f ? (t.launchCapacityKg - t.totalMassKg) / t.launchCapacityKg : 0f;

        private static string Name(LaunchVehicleSO lv) => lv != null ? lv.displayName : "(sin lanzador)";
        private static string Name(SpacecraftComponentSO c) => c != null ? c.displayName : "(ninguno)";

        private static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
