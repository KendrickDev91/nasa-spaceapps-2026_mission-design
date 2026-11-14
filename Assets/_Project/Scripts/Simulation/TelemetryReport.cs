using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MissionDesign.Simulation
{
    public enum ResourceKind
    {
        Mass,
        Power,
        Budget,
        Data,
        Comms,
        Time
    }

    public enum ResourceState
    {
        Ok,
        Warning,
        Critical
    }

    /// <summary>Lectura lista para pintar una barra en la UI: valor frente a su límite.</summary>
    [Serializable]
    public struct ResourceReading
    {
        public ResourceKind kind;
        public float value;
        public float limit;
        public string unit;
        public ResourceState state;
        public string summary;

        /// <summary>value / limit (0 si no hay límite definido).</summary>
        public float Ratio => limit > 0f ? value / limit : 0f;
    }

    /// <summary>
    /// Resultado completo del motor matemático. Campos públicos para que JsonUtility
    /// pueda exportarlo tal cual en el reporte final de misión.
    /// </summary>
    [Serializable]
    public sealed class TelemetryReport
    {
        [Header("1. Masa (kg)")]
        public float dryMassKg;
        public float propellantMassKg;
        public float instrumentMassKg;
        public float totalMassKg;
        public float massWithMarginKg;
        public float launchCapacityKg;
        [Tooltip("Δv que el combustible del bus puede dar a la nave completa (Tsiolkovsky).")]
        public float spacecraftDeltaVKmS;
        public float requiredOnboardDeltaVKmS;

        [Header("2. Energía (W)")]
        public float solarPowerSunlitW;
        public float powerGeneratedAvgW;
        public float powerGeneratedEolW;
        public float instrumentPeakPowerW;
        public float peakLoadW;
        public float averageLoadW;
        public float powerBalanceW;
        public float eclipseEnergyRequiredWh;
        public float batteryUsableWh;
        public float safeModeAutonomyHours;

        [Header("3. Presupuesto ($M USD)")]
        public float hardwareCostMUSD;
        public float launchCostMUSD;
        public float operationsCostMUSD;
        public float totalCostMUSD;
        public float budgetLimitMUSD;

        [Header("4-5. Datos y comunicaciones")]
        public float dataGeneratedGBPerDay;
        public float downlinkRateMbps;
        public float downlinkCapacityGBPerDay;
        public float dataLostGBPerDay;
        public float dataReturnFraction = 1f;
        public float contactHoursPerDay;
        [Tooltip("Memoria a bordo (dificultad Avanzada) y cuántos días de datos puede guardar.")]
        public float onboardStorageGB;
        public float storageDays;

        [Header("6. Tiempo (meses)")]
        public float developmentMonths;
        public float maxDevelopmentMonths;
        public float estimatedLifeMonths;
        public float plannedOperationsMonths;

        [Header("Ciencia")]
        public float potentialScienceValue;
        public float expectedScienceValue;
        public float powerAvailabilityFraction = 1f;

        public List<ResourceReading> resources = new List<ResourceReading>();
        public List<string> alerts = new List<string>();

        public ResourceReading Get(ResourceKind kind)
        {
            foreach (var r in resources)
                if (r.kind == kind) return r;
            return default;
        }

        public bool HasCritical
        {
            get
            {
                foreach (var r in resources)
                    if (r.state == ResourceState.Critical) return true;
                return false;
            }
        }

        public string ToConsoleString()
        {
            var sb = new StringBuilder();
            foreach (var r in resources)
            {
                string icon = r.state == ResourceState.Ok ? "OK  " : r.state == ResourceState.Warning ? "WARN" : "CRIT";
                sb.AppendLine($"  [{icon}] {r.kind,-6} {r.summary}");
            }
            sb.AppendLine($"  Ciencia: potencial {potentialScienceValue:F0} → esperada {expectedScienceValue:F0} " +
                          $"(retorno de datos {dataReturnFraction:P0}, energía {powerAvailabilityFraction:P0})");
            foreach (var a in alerts) sb.AppendLine($"  ⚠ {a}");
            return sb.ToString();
        }
    }
}
