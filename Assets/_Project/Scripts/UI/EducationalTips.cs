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
            c == DataConfidence.Official ? Loc.L("dato oficial", "official data") : c == DataConfidence.Approximate ? Loc.L("dato aproximado", "approximate data") : Loc.L("estimación de juego", "game estimate");

        public static string For(ScriptableObject data, SpacecraftDesign design, NasaDatabaseSO db = null)
        {
            var sb = new StringBuilder();
            switch (data)
            {
                case InstrumentSO i:
                {
                    Header(sb, i.displayName, i.dataConfidence);
                    Body(sb, i.description, i.heritage);
                    Stats(sb, Loc.L($"{i.massKg:N0} kg · {i.peakPowerW:N0} W pico · {i.dataVolumeGBPerDay:N0} GB/día · ciencia {i.scienceValue} · " +
                                    $"apuntamiento ±{i.requiredPointingDeg:0.###}° · mejor en {string.Join("/", i.preferredOrbits ?? Array.Empty<OrbitRegime>())}",
                                    $"{i.massKg:N0} kg · {i.peakPowerW:N0} W peak · {i.dataVolumeGBPerDay:N0} GB/day · science {i.scienceValue} · " +
                                    $"pointing ±{i.requiredPointingDeg:0.###}° · best in {string.Join("/", i.preferredOrbits ?? Array.Empty<OrbitRegime>())}"));
                    bool mounted = design != null && design.Instruments.Contains(i);
                    Preview(sb, design, db, mounted ? Loc.L("Si lo quitas:", "If you remove it:") : Loc.L("Si lo añades:", "If you add it:"), d =>
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
                    if (mounted) sb.AppendLine($"<color={UiTheme.Hex(UiTheme.Ok)}>{Loc.L("Ya está montado en la nave.", "Already mounted on the spacecraft.")}</color>");
                    else Preview(sb, design, db, Loc.L("Si lo eliges:", "If you choose it:"), d => d.SetComponent(c));
                    Source(sb, c.sourceUrl, c.sourceNotes);
                    break;
                }
                case OrbitSO o:
                    Header(sb, o.displayName, o.dataConfidence);
                    Body(sb, o.description, null);
                    Stats(sb, Loc.L($"{o.altitudeKm:N0} km · Δv {o.requiredDeltaVKmS:F1} km/s · eclipse {o.eclipseFraction:P0} · contacto {o.dailyContactHours:N0} h/día · radiación {o.radiationLevel:P0}",
                                   $"{o.altitudeKm:N0} km · Δv {o.requiredDeltaVKmS:F1} km/s · eclipse {o.eclipseFraction:P0} · contact {o.dailyContactHours:N0} h/day · radiation {o.radiationLevel:P0}"));
                    if (!string.IsNullOrEmpty(o.educationalNote)) sb.AppendLine($"<i>{o.educationalNote}</i>");
                    if (design == null || design.Orbit != o) Preview(sb, design, db, Loc.L("Si la eliges:", "If you choose it:"), d => d.SetOrbit(o));
                    Source(sb, o.sourceUrl, null);
                    break;
                case LaunchVehicleSO lv:
                    Header(sb, lv.displayName, lv.dataConfidence);
                    Body(sb, lv.description, $"{lv.operatorName} · {lv.country}");
                    foreach (var cap in lv.payloadCapacities)
                        sb.AppendLine(cap.payloadKg > 0f
                            ? $"• {cap.regime}: {cap.payloadKg:N0} kg <size=85%><color={UiTheme.Hex(UiTheme.TextDim)}>({ConfidenceLabel(cap.confidence)})</color></size>"
                            : $"• {cap.regime}: {Loc.L("no ofrecido", "not offered")}");
                    Stats(sb, Loc.L($"${lv.launchCostMUSD:N1}M · éxito histórico {lv.reliability:P0} · reserva {lv.leadTimeMonths:N0} meses antes",
                                   $"${lv.launchCostMUSD:N1}M · historical success {lv.reliability:P0} · book {lv.leadTimeMonths:N0} months ahead"));
                    if (design == null || design.LaunchVehicle != lv) Preview(sb, design, db, Loc.L("Si lo eliges:", "If you choose it:"), d => d.SetLaunchVehicle(lv));
                    Source(sb, lv.sourceUrl, lv.sourceNotes);
                    break;
                case GroundNetworkSO n:
                    Header(sb, n.displayName, n.dataConfidence);
                    Body(sb, n.description, null);
                    Stats(sb, Loc.L($"ganancia ×{n.gainFactor:0.#} · contacto ×{n.contactHoursMultiplier:0.#} · ${n.costPerMonthMUSD:0.##}M/mes · cubre {string.Join("/", n.supportedRegimes ?? Array.Empty<OrbitRegime>())}",
                                   $"gain ×{n.gainFactor:0.#} · contact ×{n.contactHoursMultiplier:0.#} · ${n.costPerMonthMUSD:0.##}M/month · covers {string.Join("/", n.supportedRegimes ?? Array.Empty<OrbitRegime>())}"));
                    if (!string.IsNullOrEmpty(n.educationalNote)) sb.AppendLine($"<i>{n.educationalNote}</i>");
                    if (design == null || design.GroundNetwork != n) Preview(sb, design, db, Loc.L("Si la eliges:", "If you choose it:"), d => d.SetGroundNetwork(n));
                    Source(sb, n.sourceUrl, null);
                    break;
                case MissionScenarioSO s:
                    sb.AppendLine($"<b>{s.displayName}</b>");
                    sb.AppendLine(s.briefing);
                    Stats(sb, Loc.L($"Límite ${s.budgetLimitMUSD:N0}M · desarrollo máx. {s.maxDevelopmentMonths:N0} meses · operación {s.targetMissionLifeMonths:N0} meses · ciencia mínima {s.minScienceValue}",
                                   $"Limit ${s.budgetLimitMUSD:N0}M · max development {s.maxDevelopmentMonths:N0} months · operations {s.targetMissionLifeMonths:N0} months · minimum science {s.minScienceValue}"));
                    break;
            }
            return sb.ToString().TrimEnd();
        }

        public static string ForDifficulty(DifficultyLevel level) => level == DifficultyLevel.Basic
            ? Loc.L("<b>Básico</b>\nPiezas esenciales: bus, energía, antena, red terrena e instrumentos. Un solo evento aleatorio y el cohete nunca falla.\n" +
                    "Ideal para aprender los 6 recursos sin abrumarse.",
                    "<b>Basic</b>\nEssential parts: bus, power, antenna, ground network and instruments. A single random event and the rocket never fails.\n" +
                    "Ideal for learning the 6 resources without being overwhelmed.")
            : Loc.L("<b>Avanzado</b>\nAñade la <b>computadora de a bordo</b> (memoria para cortes de enlace y protección contra radiación) y el " +
                    "<b>control de orientación</b> (sin buen apuntamiento, los instrumentos de alta resolución pierden ciencia).\n" +
                    "1-2 eventos y el cohete puede fallar según su historial real.",
                    "<b>Advanced</b>\nAdds the <b>onboard computer</b> (memory for link outages and radiation protection) and " +
                    "<b>attitude control</b> (without good pointing, high-resolution instruments lose science).\n" +
                    "1-2 events, and the rocket can fail according to its real track record.");

        private static string ComponentStats(SpacecraftComponentSO c)
        {
            string common = $"{c.massKg:N0} kg · ${c.costMUSD:N1}M · {c.developmentMonths:N0} {Loc.L("meses", "months")}";
            switch (c)
            {
                case SpacecraftBusSO b:
                    return Loc.L($"{b.WetMassKg:N0} kg con {b.propellantMassKg:N0} kg de combustible (Isp {b.propulsionIspSeconds:N0} s) · carga útil {b.maxPayloadMassKg:N0} kg / {b.maxPayloadPowerW:N0} W · " +
                                 $"{b.payloadSlots} ranuras · consumo propio {b.housekeepingPowerW:N0} W · vida {b.designLifeYears:N0} años · ${b.costMUSD:N0}M",
                                 $"{b.WetMassKg:N0} kg with {b.propellantMassKg:N0} kg of propellant (Isp {b.propulsionIspSeconds:N0} s) · payload {b.maxPayloadMassKg:N0} kg / {b.maxPayloadPowerW:N0} W · " +
                                 $"{b.payloadSlots} slots · housekeeping {b.housekeepingPowerW:N0} W · life {b.designLifeYears:N0} years · ${b.costMUSD:N0}M");
                case SolarArraySO s: return Loc.L($"{s.powerAt1AU_W:N0} W a 1 UA · pierde {s.annualDegradation:P1}/año · {common}", $"{s.powerAt1AU_W:N0} W at 1 AU · loses {s.annualDegradation:P1}/year · {common}");
                case BatterySO b: return Loc.L($"{b.capacityWh:N0} Wh ({b.UsableWh:N0} útiles al {b.maxDepthOfDischarge:P0} de descarga) · {common}", $"{b.capacityWh:N0} Wh ({b.UsableWh:N0} usable at {b.maxDepthOfDischarge:P0} depth of discharge) · {common}");
                case AntennaSO a: return Loc.L($"Banda {a.band} · {a.referenceDataRateMbps:N1} Mbps a {a.referenceRangeKm:N0} km (máx. {a.maxDataRateMbps:N0}) · {a.peakPowerW:N0} W · {common}{(a.dsnCompatible ? " · compatible DSN" : "")}",
                                                $"{a.band} band · {a.referenceDataRateMbps:N1} Mbps at {a.referenceRangeKm:N0} km (max {a.maxDataRateMbps:N0}) · {a.peakPowerW:N0} W · {common}{(a.dsnCompatible ? " · DSN compatible" : "")}");
                case OnboardComputerSO cdh: return Loc.L($"{cdh.storageGB:N0} GB de memoria · {(cdh.radiationHardened ? "endurecida contra radiación" : "comercial (sensible a radiación)")} · {cdh.peakPowerW:N0} W · {common}",
                                                         $"{cdh.storageGB:N0} GB of memory · {(cdh.radiationHardened ? "radiation-hardened" : "commercial (radiation-sensitive)")} · {cdh.peakPowerW:N0} W · {common}");
                case AttitudeControlSO adcs: return $"{Loc.L("apuntamiento", "pointing")} ±{adcs.pointingAccuracyDeg:0.###}° · {adcs.peakPowerW:N0} W · {common}";
                default: return common;
            }
        }

        private static void Header(StringBuilder sb, string title, DataConfidence confidence) =>
            sb.AppendLine($"<b>{title}</b>  <size=75%><color={UiTheme.Hex(UiTheme.TextDim)}>[{ConfidenceLabel(confidence)}]</color></size>");

        private static void Body(StringBuilder sb, string description, string heritage)
        {
            if (!string.IsNullOrEmpty(description)) sb.AppendLine(description);
            if (!string.IsNullOrEmpty(heritage)) sb.AppendLine($"<size=85%><color={UiTheme.Hex(UiTheme.TextDim)}>{Loc.L("Referencia real", "Real-world reference")}: {heritage}</color></size>");
        }

        private static void Stats(StringBuilder sb, string stats) =>
            sb.AppendLine($"<color={UiTheme.Hex(UiTheme.Accent)}>{stats}</color>");

        private static void Source(StringBuilder sb, string url, string notes)
        {
            if (!string.IsNullOrEmpty(notes)) sb.AppendLine($"<size=75%><color={UiTheme.Hex(UiTheme.TextDim)}>{notes}</color></size>");
            if (!string.IsNullOrEmpty(url)) sb.AppendLine($"<size=75%><color={UiTheme.Hex(UiTheme.TextDim)}>{Loc.L("Fuente", "Source")}: {url}</color></size>");
        }

        private static void Preview(StringBuilder sb, SpacecraftDesign design, NasaDatabaseSO db, string label, Action<SpacecraftDesign> change)
        {
            if (design == null) return;
            var lines = DesignPreview.Describe(design, change);
            sb.AppendLine($"<b>{label}</b>");
            if (lines.Count == 0) sb.AppendLine(Loc.L("• Sin cambios en los recursos.", "• No change in resources."));
            foreach (var line in lines)
            {
                Color c = line.StartsWith("Provocaría") || line.StartsWith("Would cause") ? UiTheme.Critical
                    : line.StartsWith("Resolvería") || line.StartsWith("Would fix") ? UiTheme.Ok : UiTheme.Text;
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
                    sb.AppendLine($"<color={UiTheme.Hex(UiTheme.Warning)}>• {Loc.L($"Con {mass:N0} kg no la pueden llevar a {design.Orbit.regime}", $"At {mass:N0} kg these cannot take it to {design.Orbit.regime}")}: {string.Join(", ", tooSmall)}</color>");
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
                    return Loc.L("<b>MASA</b>\nTodo lo que lleva la nave: estructura, instrumentos y combustible. Cada kilo extra exige un cohete más potente: " +
                                 "para llegar a órbita baja hace falta ~9.4 km/s, y por eso ~93% de un cohete es combustible.\n" +
                                 "<color=#9FB3D1>La NASA reserva un margen de crecimiento (aquí 10%) porque la masa real casi siempre aumenta durante el desarrollo.</color>",
                                 "<b>MASS</b>\nEverything the spacecraft carries: structure, instruments and propellant. Every extra kilo needs a more powerful rocket: " +
                                 "reaching low orbit takes ~9.4 km/s, which is why ~93% of a rocket is propellant.\n" +
                                 "<color=#9FB3D1>NASA keeps a growth margin (10% here) because real mass almost always increases during development.</color>");
                case ResourceKind.Power:
                    return Loc.L("<b>ENERGÍA</b>\nLos paneles generan según la distancia al Sol (ley 1/r²) y no producen en eclipse; la batería cubre la sombra.\n" +
                                 "Si el consumo pico supera la generación media, los instrumentos deben apagarse parte del tiempo y se pierde ciencia. " +
                                 "Los paneles además se degradan cada año por radiación.",
                                 "<b>POWER</b>\nPanels generate according to the distance from the Sun (1/r² law) and produce nothing in eclipse; the battery covers the shadow.\n" +
                                 "If the peak load exceeds the average generation, the instruments must be switched off part of the time and science is lost. " +
                                 "Panels also degrade every year from radiation.");
                case ResourceKind.Budget:
                    return Loc.L("<b>PRESUPUESTO</b>\nHardware + lanzamiento + operaciones (equipo de control y tiempo de antenas). " +
                                 "Los proyectos reales guardan ~10% de reserva para imprevistos (lo recomienda incluso la guía CubeSat 101 de la NASA).",
                                 "<b>BUDGET</b>\nHardware + launch + operations (control team and antenna time). " +
                                 "Real projects keep a ~10% reserve for surprises (even NASA's CubeSat 101 guide recommends it).");
                case ResourceKind.Data:
                    return Loc.L("<b>DATOS</b>\nGigabytes de ciencia que generan los instrumentos cada día. Más instrumentos o más resolución = más datos… " +
                                 "pero solo sirven los que logras bajar a tierra.",
                                 "<b>DATA</b>\nGigabytes of science the instruments generate every day. More instruments or more resolution = more data… " +
                                 "but only what you manage to bring down to the ground counts.");
                case ResourceKind.Comms:
                    return Loc.L("<b>COMUNICACIONES</b>\nCapacidad de enviar datos a tierra: depende de la antena, la distancia (la señal cae con 1/r²), " +
                                 "la red de estaciones terrenas (antenas grandes como las de 34 m de la DSN) y las horas de contacto al día.\n" +
                                 "Si se generan más datos de los que se pueden transmitir, se pierde ciencia.",
                                 "<b>COMMS</b>\nThe capacity to send data to the ground: it depends on the antenna, the distance (the signal falls with 1/r²), " +
                                 "the ground station network (big antennas like the DSN's 34 m dishes) and the contact hours per day.\n" +
                                 "If more data is generated than can be transmitted, science is lost.");
                case ResourceKind.Time:
                    return Loc.L("<b>TIEMPO</b>\nMeses de desarrollo (el subsistema más lento marca el ritmo, más integración y pruebas) y vida útil " +
                                 "de la misión en órbita, limitada por el diseño del bus.",
                                 "<b>SCHEDULE</b>\nMonths of development (the slowest subsystem sets the pace, plus integration and testing) and the mission's " +
                                 "life in orbit, limited by the bus design.");
                default:
                    return kind.ToString();
            }
        }
    }
}
