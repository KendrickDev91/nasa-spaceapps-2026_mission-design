using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MissionDesign.Simulation
{
    /// <summary>
    /// "¿Qué pasaría si…?": aplica un cambio a una copia del diseño y describe sus consecuencias
    /// antes de que el jugador lo confirme. Alimenta los tooltips educativos del catálogo.
    /// </summary>
    public static class DesignPreview
    {
        public static List<string> Describe(SpacecraftDesign current, Action<SpacecraftDesign> change)
        {
            var lines = new List<string>();
            if (current == null || change == null) return lines;

            var after = current.Clone();
            change(after);

            var t0 = MissionTelemetry.Calculate(current);
            var t1 = MissionTelemetry.Calculate(after);
            var v0 = OrbitAndLaunchValidator.Validate(current, t0);
            var v1 = OrbitAndLaunchValidator.Validate(after, t1);

            if (Changed(t0.totalMassKg, t1.totalMassKg, 0.5f))
                lines.Add($"Masa: {t0.totalMassKg:N0} → {t1.totalMassKg:N0} kg" +
                          (t1.launchCapacityKg > 0f ? $" (el lanzador admite {t1.launchCapacityKg:N0} kg)" : ""));
            if (Changed(t0.powerBalanceW, t1.powerBalanceW, 0.5f))
                lines.Add($"Balance energético: {t0.powerBalanceW:+#,0;-#,0;0} → {t1.powerBalanceW:+#,0;-#,0;0} W");
            if (Changed(t0.totalCostMUSD, t1.totalCostMUSD, 0.05f))
                lines.Add($"Costo: ${t0.totalCostMUSD:N1}M → ${t1.totalCostMUSD:N1}M" +
                          (t1.budgetLimitMUSD > 0f ? $" (límite ${t1.budgetLimitMUSD:N0}M)" : ""));
            if (Changed(t0.dataReturnFraction, t1.dataReturnFraction, 0.005f) || Changed(t0.dataGeneratedGBPerDay, t1.dataGeneratedGBPerDay, 0.05f))
                lines.Add($"Datos que llegan a tierra: {t0.dataReturnFraction:P0} → {t1.dataReturnFraction:P0} " +
                          $"({t1.dataGeneratedGBPerDay:N0} GB/día generados, {t1.downlinkCapacityGBPerDay:N0} de enlace)");
            if (Changed(t0.expectedScienceValue, t1.expectedScienceValue, 0.5f))
                lines.Add($"Ciencia esperada: {t0.expectedScienceValue:N0} → {t1.expectedScienceValue:N0}");
            if (Changed(t0.developmentMonths, t1.developmentMonths, 0.5f))
                lines.Add($"Desarrollo: {t0.developmentMonths:N0} → {t1.developmentMonths:N0} meses");

            var before = new HashSet<string>(v0.Blocking.Select(i => i.title));
            var afterTitles = new HashSet<string>(v1.Blocking.Select(i => i.title));
            foreach (var issue in v1.Blocking.Where(i => !before.Contains(i.title)))
                lines.Add($"Provocaría: {issue.title}. {issue.explanation}");
            foreach (var title in before.Where(b => !afterTitles.Contains(b)))
                lines.Add($"Resolvería: {title}.");

            return lines;
        }

        private static bool Changed(float a, float b, float epsilon) => Mathf.Abs(a - b) > epsilon;
    }
}
