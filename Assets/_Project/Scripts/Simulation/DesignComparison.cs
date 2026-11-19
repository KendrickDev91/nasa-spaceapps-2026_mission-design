using MissionDesign.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MissionDesign.Simulation
{
    [Serializable]
    public struct ComparisonRow
    {
        public string label;
        public string valueA;
        public string valueB;
        /// <summary>-1 gana A, 1 gana B, 0 empate o no comparable.</summary>
        public int better;
    }

    /// <summary>
    /// Compara dos diseños lado a lado: demuestra que "no hay una única solución perfecta"
    /// (distintas combinaciones pueden cumplir el mismo objetivo con otros compromisos).
    /// </summary>
    public static class DesignComparison
    {
        public static List<ComparisonRow> Compare(SpacecraftDesign a, SpacecraftDesign b)
        {
            var ta = MissionTelemetry.Calculate(a);
            var tb = MissionTelemetry.Calculate(b);
            var va = OrbitAndLaunchValidator.Validate(a, ta);
            var vb = OrbitAndLaunchValidator.Validate(b, tb);
            float marginA = ta.launchCapacityKg > 0f ? (ta.launchCapacityKg - ta.totalMassKg) / ta.launchCapacityKg : -1f;
            float marginB = tb.launchCapacityKg > 0f ? (tb.launchCapacityKg - tb.totalMassKg) / tb.launchCapacityKg : -1f;

            return new List<ComparisonRow>
            {
                Text(Loc.L("Órbita", "Orbit"), a.Orbit != null ? a.Orbit.regime.ToString() : "—", b.Orbit != null ? b.Orbit.regime.ToString() : "—"),
                Text(Loc.L("Lanzador", "Launcher"), a.LaunchVehicle != null ? a.LaunchVehicle.displayName : "—", b.LaunchVehicle != null ? b.LaunchVehicle.displayName : "—"),
                Text(Loc.L("Instrumentos", "Instruments"), a.Instruments.Count.ToString(), b.Instruments.Count.ToString()),
                Row(Loc.L("Ciencia esperada", "Expected science"), ta.expectedScienceValue, tb.expectedScienceValue, "N0", higherIsBetter: true),
                Row(Loc.L("Masa total (kg)", "Total mass (kg)"), ta.totalMassKg, tb.totalMassKg, "N0", higherIsBetter: false),
                Row(Loc.L("Margen de lanzamiento", "Launch margin"), marginA, marginB, "P0", higherIsBetter: true),
                Row(Loc.L("Balance energético (W)", "Power balance (W)"), ta.powerBalanceW, tb.powerBalanceW, "N0", higherIsBetter: true),
                Row(Loc.L("Costo total ($M)", "Total cost ($M)"), ta.totalCostMUSD, tb.totalCostMUSD, "N1", higherIsBetter: false),
                Row(Loc.L("Datos que llegan a tierra", "Data reaching the ground"), ta.dataReturnFraction, tb.dataReturnFraction, "P0", higherIsBetter: true),
                Row(Loc.L("Desarrollo (meses)", "Development (months)"), ta.developmentMonths, tb.developmentMonths, "N0", higherIsBetter: false),
                Row(Loc.L("Problemas bloqueantes", "Blocking issues"), va.Blocking.Count(), vb.Blocking.Count(), "N0", higherIsBetter: false)
            };
        }

        /// <summary>Cuántas filas gana cada diseño (para el resumen de la comparación).</summary>
        public static (int winsA, int winsB) Tally(IEnumerable<ComparisonRow> rows)
        {
            var list = rows.ToList();
            return (list.Count(r => r.better < 0), list.Count(r => r.better > 0));
        }

        private static ComparisonRow Row(string label, float a, float b, string format, bool higherIsBetter)
        {
            int better = Math.Abs(a - b) < 1e-4f ? 0 : (a > b) == higherIsBetter ? -1 : 1;
            return new ComparisonRow { label = label, valueA = a.ToString(format), valueB = b.ToString(format), better = better };
        }

        private static ComparisonRow Text(string label, string a, string b) =>
            new ComparisonRow { label = label, valueA = a, valueB = b, better = 0 };
    }
}
