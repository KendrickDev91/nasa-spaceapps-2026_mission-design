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
        /// <summary>El diseño neutralizó el evento (memoria a bordo, electrónica endurecida…).</summary>
        public bool mitigated;
        /// <summary>Solo redujo la capacidad de enlace (sin daño, ciencia ni energía): el margen puede absorberlo.</summary>
        public bool commsOnly;
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
        public DifficultyLevel difficulty;

        [Header("Diseño")]
        public string scenarioId;
        public string scenarioName;
        public string orbit;
        public string launchVehicle;
        public string bus;
        public string solarArray;
        public string battery;
        public string antenna;
        public string groundNetwork;
        public string computer;
        public string attitudeControl;
        public string[] instruments = Array.Empty<string>();

        [Header("Veredicto")]
        public MissionVerdict verdict;
        public string verdictTitle;
        public int score;
        /// <summary>A ≥ 90 · B ≥ 75 · C ≥ 60 · D ≥ 40 · F.</summary>
        public string rank;
        public string similarMission;
        public string similarMissionDescription;
        /// <summary>Trabajo en equipo 0-10 (roles que aportaron + conflictos resueltos).</summary>
        public int collaborationScore;
        public string collaborationNotes;

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
        [Tooltip("Misiones con retorno: datos guardados a bordo y recuperados tras el amerizaje (TB).")]
        public float dataRecoveredTB;
        public bool returnsToEarth;
        public bool crewed;

        [Header("Exploración fotográfica")]
        public bool reachedMoon;
        [Tooltip("Qué fotografiaron las cámaras: \"Luna\" (destino lunar) o \"Tierra\" (órbita terrestre).")]
        public string photoTarget;
        public string[] cameras = Array.Empty<string>();
        [Tooltip("Fotografías tomadas (estimación a escala real).")]
        public int photosTaken;
        public int photosTransmitted;
        public int photosRecovered;
        [Tooltip("Imágenes que el juego renderiza para el informe de exploración.")]
        public int photoShots;
        [Tooltip("De esas imágenes, las que llegaron a tierra (enlace o tarjetas SD).")]
        public int photoShotsAvailable;

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
        /// <summary>Tarjeta "Lo que salió bien".</summary>
        public List<string> wentWell = new List<string>();
        /// <summary>Tarjeta "Lo que falló".</summary>
        public List<string> wentWrong = new List<string>();
        public TelemetryReport designTelemetry;

        public string ToJson() => JsonUtility.ToJson(this, true);

        public static string VerdictLabel(MissionVerdict v) =>
            v == MissionVerdict.Success ? Loc.L("ÉXITO", "SUCCESS") : v == MissionVerdict.PartialSuccess ? Loc.L("ÉXITO PARCIAL", "PARTIAL SUCCESS") : Loc.L("FALLO", "FAILURE");

        public string ToText()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== {Loc.L("REPORTE DE MISIÓN", "MISSION REPORT")} — {GameInfo.FullTitle} ({GameInfo.Event}) ===");
            sb.AppendLine(Loc.L($"Generado (UTC): {generatedAtUtc}   Semilla: {seed}   Dificultad: {difficulty}", $"Generated (UTC): {generatedAtUtc}   Seed: {seed}   Difficulty: {difficulty}"));
            sb.AppendLine($"{Loc.L("Escenario", "Scenario")}: {scenarioName} ({scenarioId})");
            sb.AppendLine(Loc.L($"Órbita: {orbit}   Lanzador: {launchVehicle}   Red terrena: {groundNetwork}", $"Orbit: {orbit}   Launcher: {launchVehicle}   Ground network: {groundNetwork}"));
            sb.AppendLine(Loc.L($"Bus: {bus}   Paneles: {solarArray}   Batería: {battery}   Antena: {antenna}", $"Bus: {bus}   Panels: {solarArray}   Battery: {battery}   Antenna: {antenna}"));
            if (difficulty == DifficultyLevel.Advanced)
                sb.AppendLine(Loc.L($"Computadora: {computer}   Control de orientación: {attitudeControl}", $"Computer: {computer}   Attitude control: {attitudeControl}"));
            sb.AppendLine($"{Loc.L("Instrumentos", "Instruments")}: {string.Join(", ", instruments)}");
            sb.AppendLine();
            sb.AppendLine(Loc.L($"VEREDICTO: {VerdictLabel(verdict)} — {verdictTitle}   Puntuación: {score}/100 (rango {rank})", $"VERDICT: {VerdictLabel(verdict)} — {verdictTitle}   Score: {score}/100 (rank {rank})"));
            if (!string.IsNullOrEmpty(similarMission)) sb.AppendLine($"{Loc.L("Tu misión se parece a", "Your mission resembles")}: {similarMission} — {similarMissionDescription}");
            sb.AppendLine(Loc.L($"Ciencia lograda: {scienceAchieved:N0} / {scienceTarget:N0} requerida", $"Science achieved: {scienceAchieved:N0} / {scienceTarget:N0} required"));
            sb.AppendLine(Loc.L($"Datos: {dataGeneratedGBPerDay:N1} GB/día generados, {downlinkGBPerDay:N1} GB/día de enlace " +
                                $"({dataReturnFraction:P0} de retorno) → {dataReturnedTotalTB:N1} TB recibidos, {dataLostTotalTB:N1} TB perdidos",
                                $"Data: {dataGeneratedGBPerDay:N1} GB/day generated, {downlinkGBPerDay:N1} GB/day of link " +
                                $"({dataReturnFraction:P0} returned) → {dataReturnedTotalTB:N1} TB received, {dataLostTotalTB:N1} TB lost"));
            sb.AppendLine(Loc.L($"Operación: {achievedOperationsDays:N0} de {plannedOperationsDays:N0} días · energía disponible {powerAvailabilityFraction:P0}", $"Operations: {achievedOperationsDays:N0} of {plannedOperationsDays:N0} days · power available {powerAvailabilityFraction:P0}"));
            sb.AppendLine(Loc.L($"Programa: ${totalCostMUSD:N1} M de ${budgetLimitMUSD:N0} M · desarrollo {developmentMonths:N0} meses", $"Program: ${totalCostMUSD:N1} M of ${budgetLimitMUSD:N0} M · development {developmentMonths:N0} months"));
            sb.AppendLine();
            sb.AppendLine(Loc.L("LO QUE SALIÓ BIEN:", "WHAT WENT WELL:"));
            foreach (var e in wentWell) sb.AppendLine($" + {e}");
            sb.AppendLine(Loc.L("LO QUE FALLÓ:", "WHAT WENT WRONG:"));
            foreach (var e in wentWrong) sb.AppendLine($" - {e}");
            sb.AppendLine();
            sb.AppendLine(Loc.L("POR QUÉ (todas las explicaciones):", "WHY (all explanations):"));
            foreach (var e in explanations) sb.AppendLine($" · {e}");
            sb.AppendLine();
            sb.AppendLine(Loc.L("EVENTOS:", "EVENTS:"));
            if (events.Count == 0) sb.AppendLine(Loc.L(" - (ninguno)", " - (none)"));
            foreach (var e in events)
            {
                sb.AppendLine($" - {Loc.L("Día", "Day")} {e.missionDay:N0} · {e.displayName}: {e.impact}");
                if (!string.IsNullOrEmpty(e.mitigation)) sb.AppendLine($"     {Loc.L("Mitigación", "Mitigation")}: {e.mitigation}");
                if (!string.IsNullOrEmpty(e.educationalFact)) sb.AppendLine($"     {Loc.L("Dato", "Fact")}: {e.educationalFact}");
            }
            sb.AppendLine();
            sb.AppendLine(Loc.L("CRONOLOGÍA:", "TIMELINE:"));
            foreach (var t in timeline) sb.AppendLine($" [{t.phase}] {Loc.L("día", "day")} {t.missionDay:N0}: {t.message}");
            return sb.ToString();
        }
    }
}
