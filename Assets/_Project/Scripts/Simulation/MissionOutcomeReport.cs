using System;
using System.Collections.Generic;
using System.Text;
using MissionDesign.Core;
using UnityEngine;

namespace MissionDesign.Simulation
{
    public enum MissionVerdict
    {
        Success,
        PartialSuccess,
        Failure
    }

    public enum LogTone
    {
        Info,
        Positive,
        Warning,
        Critical
    }

    [Serializable]
    public struct SimulationLogEntry
    {
        public MissionPhase phase;
        public float missionDay;
        public LogTone tone;
        public string message;
    }

    [Serializable]
    public struct EventOutcome
    {
        public string eventId;
        public string displayName;
        public MissionPhase phase;
        public float missionDay;
        public bool positive;
        public string impact;
        public string mitigation;
        public string educationalFact;
    }

    /// <summary>
    /// Resultado completo de una simulación. Solo contiene datos serializables (ids y nombres,
    /// no referencias a assets) para exportarse tal cual a JSON y compararse entre equipos.
    /// </summary>
    [Serializable]
    public sealed class MissionOutcomeReport
    {
        public string generatedAtUtc;
        public int seed;

        [Header("Diseño")]
        public string scenarioId;
        public string scenarioName;
        public string orbit;
        public string launchVehicle;
        public string bus;
        public string solarArray;
        public string battery;
        public string antenna;
        public string[] instruments = Array.Empty<string>();

        [Header("Veredicto")]
        public MissionVerdict verdict;
        public string verdictTitle;
        public int score;

        [Header("Fases")]
        public bool launchSucceeded;
        public bool orbitInsertionSucceeded;
        public bool survivedOperations;
        public float onboardDeltaVKmS;
        public float requiredOnboardDeltaVKmS;

        [Header("Operaciones")]
        public float plannedOperationsDays;
        public float achievedOperationsDays;
        public float dataGeneratedGBPerDay;
        public float downlinkGBPerDay;
        public float dataReturnFraction;
        public float powerAvailabilityFraction;
        public float dataReturnedTotalTB;
        public float dataLostTotalTB;

        [Header("Ciencia y programa")]
        public float scienceAchieved;
        public float scienceTarget;
        public float totalCostMUSD;
        public float budgetLimitMUSD;
        public float developmentMonths;

        public List<EventOutcome> events = new List<EventOutcome>();
        public List<SimulationLogEntry> timeline = new List<SimulationLogEntry>();
        /// <summary>El "por qué" del resultado, en lenguaje claro y con los números exactos.</summary>
        public List<string> explanations = new List<string>();
        public TelemetryReport designTelemetry;

        public string ToJson() => JsonUtility.ToJson(this, true);

        public static string VerdictLabel(MissionVerdict v) =>
            v == MissionVerdict.Success ? "ÉXITO" : v == MissionVerdict.PartialSuccess ? "ÉXITO PARCIAL" : "FALLO";

        public string ToText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== REPORTE DE MISIÓN — Mission Design Lab (NASA Space Apps 2026) ===");
            sb.AppendLine($"Generado (UTC): {generatedAtUtc}   Semilla: {seed}");
            sb.AppendLine($"Escenario: {scenarioName} ({scenarioId})");
            sb.AppendLine($"Órbita: {orbit}   Lanzador: {launchVehicle}");
            sb.AppendLine($"Bus: {bus}   Paneles: {solarArray}   Batería: {battery}   Antena: {antenna}");
            sb.AppendLine($"Instrumentos: {string.Join(", ", instruments)}");
            sb.AppendLine();
            sb.AppendLine($"VEREDICTO: {VerdictLabel(verdict)} — {verdictTitle}   Puntuación: {score}/100");
            sb.AppendLine($"Ciencia lograda: {scienceAchieved:N0} / {scienceTarget:N0} requerida");
            sb.AppendLine($"Datos: {dataGeneratedGBPerDay:N1} GB/día generados, {downlinkGBPerDay:N1} GB/día de enlace " +
                          $"({dataReturnFraction:P0} de retorno) → {dataReturnedTotalTB:N1} TB recibidos, {dataLostTotalTB:N1} TB perdidos");
            sb.AppendLine($"Operación: {achievedOperationsDays:N0} de {plannedOperationsDays:N0} días · energía disponible {powerAvailabilityFraction:P0}");
            sb.AppendLine($"Programa: ${totalCostMUSD:N1} M de ${budgetLimitMUSD:N0} M · desarrollo {developmentMonths:N0} meses");
            sb.AppendLine();
            sb.AppendLine("POR QUÉ:");
            foreach (var e in explanations) sb.AppendLine($" - {e}");
            sb.AppendLine();
            sb.AppendLine("EVENTOS:");
            if (events.Count == 0) sb.AppendLine(" - (ninguno)");
            foreach (var e in events)
            {
                sb.AppendLine($" - Día {e.missionDay:N0} · {e.displayName}: {e.impact}");
                if (!string.IsNullOrEmpty(e.mitigation)) sb.AppendLine($"     Mitigación: {e.mitigation}");
                if (!string.IsNullOrEmpty(e.educationalFact)) sb.AppendLine($"     Dato: {e.educationalFact}");
            }
            sb.AppendLine();
            sb.AppendLine("CRONOLOGÍA:");
            foreach (var t in timeline) sb.AppendLine($" [{t.phase}] día {t.missionDay:N0}: {t.message}");
            return sb.ToString();
        }
    }
}
