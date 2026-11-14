using System;
using System.Linq;
using System.Text;
using MissionDesign.Core;
using MissionDesign.Data;
using MissionDesign.Simulation;
using UnityEngine;

namespace MissionDesign.UI
{
    /// <summary>
    /// Textos educativos de los tooltips: qué es la pieza, sus números, de dónde salen
    /// y — lo más importante — qué le pasaría a TU misión si la eliges.
    /// </summary>
    public static class EducationalTips
    {
        public static string ConfidenceLabel(DataConfidence c) =>
            c == DataConfidence.Official ? "dato oficial" : c == DataConfidence.Approximate ? "dato aproximado" : "estimación de juego";

        public static string For(ScriptableObject data, SpacecraftDesign design, NasaDatabaseSO db = null)
        {
            var sb = new StringBuilder();
            switch (data)
            {
                case InstrumentSO i:
                {
                    Header(sb, i.displayName, i.dataConfidence);
                    Body(sb, i.description, i.heritage);
                    Stats(sb, $"{i.massKg:N0} kg · {i.peakPowerW:N0} W pico · {i.dataVolumeGBPerDay:N0} GB/día · ciencia {i.scienceValue} · " +
                              $"apuntamiento ±{i.requiredPointingDeg:0.###}° · mejor en {string.Join("/", i.preferredOrbits ?? Array.Empty<OrbitRegime>())}");
                    bool mounted = design != null && design.Instruments.Contains(i);
                    Preview(sb, design, db, mounted ? "Si lo quitas:" : "Si lo añades:", d =>
                    {
                        if (mounted) d.RemoveInstrument(i);
                        else d.AddInstrument(i);
                    });
                    Source(sb, i.sourceUrl, i.sourceNotes);
                    break;
                }
                case SpacecraftComponentSO c:
                {
                    Header(sb, c.displayName, c.dataConfidence);
                    Body(sb, c.description, c.heritage);
                    Stats(sb, ComponentStats(c));
                    bool mounted = design != null && design.AllComponents.Contains(c);
                    if (mounted) sb.AppendLine($"<color={UiTheme.Hex(UiTheme.Ok)}>Ya está montado en la nave.</color>");
                    else Preview(sb, design, db, "Si lo eliges:", d => d.SetComponent(c));
                    Source(sb, c.sourceUrl, c.sourceNotes);
                    break;
                }
                case OrbitSO o:
                    Header(sb, o.displayName, o.dataConfidence);
                    Body(sb, o.description, null);
                    Stats(sb, $"{o.altitudeKm:N0} km · Δv {o.requiredDeltaVKmS:F1} km/s · eclipse {o.eclipseFraction:P0} · contacto {o.dailyContactHours:N0} h/día · radiación {o.radiationLevel:P0}");
                    if (!string.IsNullOrEmpty(o.educationalNote)) sb.AppendLine($"<i>{o.educationalNote}</i>");
                    if (design == null || design.Orbit != o) Preview(sb, design, db, "Si la eliges:", d => d.SetOrbit(o));
                    Source(sb, o.sourceUrl, null);
                    break;
                case LaunchVehicleSO lv:
                    Header(sb, lv.displayName, lv.dataConfidence);
                    Body(sb, lv.description, $"{lv.operatorName} · {lv.country}");
                    foreach (var cap in lv.payloadCapacities)
                        sb.AppendLine(cap.payloadKg > 0f
                            ? $"• {cap.regime}: {cap.payloadKg:N0} kg <size=85%><color={UiTheme.Hex(UiTheme.TextDim)}>({ConfidenceLabel(cap.confidence)})</color></size>"
                            : $"• {cap.regime}: no ofrecido");
                    Stats(sb, $"${lv.launchCostMUSD:N1}M · éxito histórico {lv.reliability:P0} · reserva {lv.leadTimeMonths:N0} meses antes");
                    if (design == null || design.LaunchVehicle != lv) Preview(sb, design, db, "Si lo eliges:", d => d.SetLaunchVehicle(lv));
                    Source(sb, lv.sourceUrl, lv.sourceNotes);
                    break;
                case GroundNetworkSO n:
                    Header(sb, n.displayName, n.dataConfidence);
                    Body(sb, n.description, null);
                    Stats(sb, $"ganancia ×{n.gainFactor:0.#} · contacto ×{n.contactHoursMultiplier:0.#} · ${n.costPerMonthMUSD:0.##}M/mes · cubre {string.Join("/", n.supportedRegimes ?? Array.Empty<OrbitRegime>())}");
                    if (!string.IsNullOrEmpty(n.educationalNote)) sb.AppendLine($"<i>{n.educationalNote}</i>");
                    if (design == null || design.GroundNetwork != n) Preview(sb, design, db, "Si la eliges:", d => d.SetGroundNetwork(n));
                    Source(sb, n.sourceUrl, null);
                    break;
                case MissionScenarioSO s:
                    sb.AppendLine($"<b>{s.displayName}</b>");
                    sb.AppendLine(s.briefing);
                    Stats(sb, $"Límite ${s.budgetLimitMUSD:N0}M · desarrollo máx. {s.maxDevelopmentMonths:N0} meses · operación {s.targetMissionLifeMonths:N0} meses · ciencia mínima {s.minScienceValue}");
                    break;
            }
            return sb.ToString().TrimEnd();
        }

        public static string ForDifficulty(DifficultyLevel level) => level == DifficultyLevel.Basic
            ? "<b>Básico</b>\nPiezas esenciales: bus, energía, antena, red terrena e instrumentos. Un solo evento aleatorio y el cohete nunca falla.\n" +
              "Ideal para aprender los 6 recursos sin abrumarse."
            : "<b>Avanzado</b>\nAñade la <b>computadora de a bordo</b> (memoria para cortes de enlace y protección contra radiación) y el " +
              "<b>control de orientación</b> (sin buen apuntamiento, los instrumentos de alta resolución pierden ciencia).\n" +
              "1-2 eventos y el cohete puede fallar según su historial real.";

        private static string ComponentStats(SpacecraftComponentSO c)
        {
            string common = $"{c.massKg:N0} kg · ${c.costMUSD:N1}M · {c.developmentMonths:N0} meses";
            switch (c)
            {
                case SpacecraftBusSO b:
                    return $"{b.WetMassKg:N0} kg con {b.propellantMassKg:N0} kg de combustible (Isp {b.propulsionIspSeconds:N0} s) · carga útil {b.maxPayloadMassKg:N0} kg / {b.maxPayloadPowerW:N0} W · " +
                           $"{b.payloadSlots} ranuras · consumo propio {b.housekeepingPowerW:N0} W · vida {b.designLifeYears:N0} años · ${b.costMUSD:N0}M";
                case SolarArraySO s: return $"{s.powerAt1AU_W:N0} W a 1 UA · pierde {s.annualDegradation:P1}/año · {common}";
                case BatterySO b: return $"{b.capacityWh:N0} Wh ({b.UsableWh:N0} útiles al {b.maxDepthOfDischarge:P0} de descarga) · {common}";
                case AntennaSO a: return $"Banda {a.band} · {a.referenceDataRateMbps:N1} Mbps a {a.referenceRangeKm:N0} km (máx. {a.maxDataRateMbps:N0}) · {a.peakPowerW:N0} W · {common}{(a.dsnCompatible ? " · compatible DSN" : "")}";
                case OnboardComputerSO cdh: return $"{cdh.storageGB:N0} GB de memoria · {(cdh.radiationHardened ? "endurecida contra radiación" : "comercial (sensible a radiación)")} · {cdh.peakPowerW:N0} W · {common}";
                case AttitudeControlSO adcs: return $"apuntamiento ±{adcs.pointingAccuracyDeg:0.###}° · {adcs.peakPowerW:N0} W · {common}";
                default: return common;
            }
        }

        private static void Header(StringBuilder sb, string title, DataConfidence confidence) =>
            sb.AppendLine($"<b>{title}</b>  <size=75%><color={UiTheme.Hex(UiTheme.TextDim)}>[{ConfidenceLabel(confidence)}]</color></size>");

        private static void Body(StringBuilder sb, string description, string heritage)
        {
            if (!string.IsNullOrEmpty(description)) sb.AppendLine(description);
            if (!string.IsNullOrEmpty(heritage)) sb.AppendLine($"<size=85%><color={UiTheme.Hex(UiTheme.TextDim)}>Referencia real: {heritage}</color></size>");
        }

        private static void Stats(StringBuilder sb, string stats) =>
            sb.AppendLine($"<color={UiTheme.Hex(UiTheme.Accent)}>{stats}</color>");

        private static void Source(StringBuilder sb, string url, string notes)
        {
            if (!string.IsNullOrEmpty(notes)) sb.AppendLine($"<size=75%><color={UiTheme.Hex(UiTheme.TextDim)}>{notes}</color></size>");
            if (!string.IsNullOrEmpty(url)) sb.AppendLine($"<size=75%><color={UiTheme.Hex(UiTheme.TextDim)}>Fuente: {url}</color></size>");
        }

        private static void Preview(StringBuilder sb, SpacecraftDesign design, NasaDatabaseSO db, string label, Action<SpacecraftDesign> change)
        {
            if (design == null) return;
            var lines = DesignPreview.Describe(design, change);
            sb.AppendLine($"<b>{label}</b>");
            if (lines.Count == 0) sb.AppendLine("• Sin cambios en los recursos.");
            foreach (var line in lines)
            {
                Color c = line.StartsWith("Provocaría") ? UiTheme.Critical : line.StartsWith("Resolvería") ? UiTheme.Ok : UiTheme.Text;
                sb.AppendLine($"<color={UiTheme.Hex(c)}>• {line}</color>");
            }

            // Qué cohetes ya no podrían llevar la nave resultante a la órbita elegida.
            if (db != null && design.Orbit != null)
            {
                var after = design.Clone();
                change(after);
                float mass = MissionTelemetry.Calculate(after).totalMassKg;
                var tooSmall = db.launchVehicles.Where(v => v != null && v.GetCapacityKg(design.Orbit.regime) < mass)
                    .Select(v => $"{v.displayName} ({v.GetCapacityKg(design.Orbit.regime):N0} kg)").ToList();
                if (tooSmall.Count > 0 && mass > 0f)
                    sb.AppendLine($"<color={UiTheme.Hex(UiTheme.Warning)}>• Con {mass:N0} kg no la pueden llevar a {design.Orbit.regime}: {string.Join(", ", tooSmall)}</color>");
            }
        }
    }

    /// <summary>Qué significa cada recurso, en lenguaje sencillo (tooltip de las barras).</summary>
    public static class ResourceExplanations
    {
        public static string For(ResourceKind kind)
        {
            switch (kind)
            {
                case ResourceKind.Mass:
                    return "<b>MASA</b>\nTodo lo que lleva la nave: estructura, instrumentos y combustible. Cada kilo extra exige un cohete más potente: " +
                           "para llegar a órbita baja hace falta ~9.4 km/s, y por eso ~93% de un cohete es combustible.\n" +
                           "<color=#9FB3D1>La NASA reserva un margen de crecimiento (aquí 10%) porque la masa real casi siempre aumenta durante el desarrollo.</color>";
                case ResourceKind.Power:
                    return "<b>ENERGÍA</b>\nLos paneles generan según la distancia al Sol (ley 1/r²) y no producen en eclipse; la batería cubre la sombra.\n" +
                           "Si el consumo pico supera la generación media, los instrumentos deben apagarse parte del tiempo y se pierde ciencia. " +
                           "Los paneles además se degradan cada año por radiación.";
                case ResourceKind.Budget:
                    return "<b>PRESUPUESTO</b>\nHardware + lanzamiento + operaciones (equipo de control y tiempo de antenas). " +
                           "Los proyectos reales guardan ~10% de reserva para imprevistos (lo recomienda incluso la guía CubeSat 101 de la NASA).";
                case ResourceKind.Data:
                    return "<b>DATOS</b>\nGigabytes de ciencia que generan los instrumentos cada día. Más instrumentos o más resolución = más datos… " +
                           "pero solo sirven los que logras bajar a tierra.";
                case ResourceKind.Comms:
                    return "<b>COMUNICACIONES</b>\nCapacidad de enviar datos a tierra: depende de la antena, la distancia (la señal cae con 1/r²), " +
                           "la red de estaciones terrenas (antenas grandes como las de 34 m de la DSN) y las horas de contacto al día.\n" +
                           "Si se generan más datos de los que se pueden transmitir, se pierde ciencia.";
                case ResourceKind.Time:
                    return "<b>TIEMPO</b>\nMeses de desarrollo (el subsistema más lento marca el ritmo, más integración y pruebas) y vida útil " +
                           "de la misión en órbita, limitada por el diseño del bus.";
                default:
                    return kind.ToString();
            }
        }
    }
}
