using System;
using System.Linq;
using MissionDesign.Core;
using MissionDesign.Data;
using UnityEngine;

namespace MissionDesign.Simulation
{
    /// <summary>
    /// Motor matemático de los 6 recursos críticos. Se suscribe a SpacecraftDesign.Changed
    /// y recalcula cada vez que el equipo añade o quita una pieza; Calculate() es puro y testeable.
    /// </summary>
    public sealed class MissionTelemetry : IDisposable
    {
        // ── Constantes de ingeniería (simplificadas, documentadas para el jurado) ──

        /// <summary>Margen de crecimiento de masa (Mass Growth Allowance) sobre la masa seca.</summary>
        public const float MassGrowthAllowance = 0.10f;
        /// <summary>Meses de integración, pruebas ambientales (I&T) y campaña de lanzamiento.</summary>
        public const float IntegrationAndTestMonths = 6f;
        /// <summary>Por debajo de este margen de presupuesto la reserva se considera insuficiente.</summary>
        public const float BudgetReserveFraction = 0.10f;
        /// <summary>Penalización científica por operar fuera de la órbita óptima del instrumento.</summary>
        public const float OffOrbitScienceFactor = 0.5f;

        /// <summary>Costo del equipo de control de misión ($M/mes). El tiempo de antena lo suma la red terrena.</summary>
        public static float OperationsCostPerMonth(OrbitRegime regime)
        {
            switch (regime)
            {
                case OrbitRegime.LEO: return 0.3f;
                case OrbitRegime.MEO:
                case OrbitRegime.GEO: return 0.4f;
                case OrbitRegime.Lunar: return 0.3f;
                case OrbitRegime.Interplanetary: return 0.5f;
                default: return 0.4f;
            }
        }

        /// <summary>
        /// Fracción de ciencia que se logra según el apuntamiento: 1 si el ADCS es suficiente;
        /// 10× peor → 0.5, 100× peor → 0.33. Sin ADCS (dificultad Básica) se asume adecuado.
        /// </summary>
        public static float PointingFactor(InstrumentSO instrument, AttitudeControlSO adcs)
        {
            if (instrument == null || adcs == null || adcs.pointingAccuracyDeg <= instrument.requiredPointingDeg) return 1f;
            return 1f / (1f + Mathf.Log10(adcs.pointingAccuracyDeg / instrument.requiredPointingDeg));
        }

        /// <summary>Valor científico de un instrumento en este diseño (órbita + apuntamiento).</summary>
        public static float InstrumentScience(InstrumentSO i, OrbitSO orbit, AttitudeControlSO adcs) =>
            i.scienceValue
            * (orbit != null && i.IsPreferredOrbit(orbit.regime) ? 1f : OffOrbitScienceFactor)
            * PointingFactor(i, adcs);

        private readonly SpacecraftDesign design;

        public event Action<TelemetryReport> Updated;
        public TelemetryReport Current { get; private set; }

        public MissionTelemetry(SpacecraftDesign design)
        {
            this.design = design ?? throw new ArgumentNullException(nameof(design));
            this.design.Changed += Recalculate;
            Recalculate();
        }

        public void Recalculate()
        {
            Current = Calculate(design);
            Updated?.Invoke(Current);
        }

        public void Dispose() => design.Changed -= Recalculate;

        // ───────────────────────────────────────────────────────────────────────

        public static TelemetryReport Calculate(SpacecraftDesign d)
        {
            var r = new TelemetryReport();
            var orbit = d.Orbit;
            var bus = d.Bus;
            var solar = d.SolarArray;
            var battery = d.Battery;
            var antenna = d.Antenna;
            var lv = d.LaunchVehicle;
            var scenario = d.Scenario;

            // 6. TIEMPO (primero: la vida planificada afecta energía EOL y costo de operaciones)
            r.estimatedLifeMonths = bus != null ? bus.designLifeYears * 12f : 0f;
            r.plannedOperationsMonths = scenario != null && scenario.targetMissionLifeMonths > 0f
                ? scenario.targetMissionLifeMonths
                : r.estimatedLifeMonths;
            float longestComponent = d.AllComponents.Select(c => c.developmentMonths).DefaultIfEmpty(0f).Max();
            float launchLead = lv != null ? lv.leadTimeMonths : 0f;
            // Los subsistemas se desarrollan en paralelo; el camino crítico es el más lento.
            r.developmentMonths = Mathf.Max(longestComponent, launchLead) + IntegrationAndTestMonths;
            bool sandbox = scenario != null && scenario.isSandbox;
            r.maxDevelopmentMonths = scenario != null && !sandbox ? scenario.maxDevelopmentMonths : 0f;

            // 1. MASA
            r.instrumentMassKg = d.Instruments.Sum(i => i.massKg);
            r.propellantMassKg = bus != null ? bus.propellantMassKg : 0f;
            r.dryMassKg = d.AllComponents.Sum(c => c.massKg);
            r.totalMassKg = r.dryMassKg + r.propellantMassKg;
            r.massWithMarginKg = r.dryMassKg * (1f + MassGrowthAllowance) + r.propellantMassKg;
            r.launchCapacityKg = lv != null && orbit != null ? lv.GetCapacityKg(orbit.regime) : 0f;
            r.requiredOnboardDeltaVKmS = orbit != null ? orbit.onboardDeltaVKmS : 0f;
            r.spacecraftDeltaVKmS = bus != null ? SpacecraftDeltaVKmS(r.totalMassKg, r.propellantMassKg, bus.propulsionIspSeconds) : 0f;

            // 2. ENERGÍA
            float sunAU = orbit != null ? orbit.sunDistanceAU : 1f;
            float eclipse = orbit != null ? orbit.eclipseFraction : 0f;
            var network = d.GroundNetwork;
            float contactHours = orbit != null ? orbit.GetContactHours(network) : 0f;
            float housekeeping = bus != null ? bus.housekeepingPowerW : 0f;
            float antennaPower = antenna != null ? antenna.peakPowerW : 0f;

            r.solarPowerSunlitW = solar != null ? solar.GetPowerW(sunAU) : 0f;
            r.powerGeneratedAvgW = r.solarPowerSunlitW * (1f - eclipse);
            r.powerGeneratedEolW = solar != null
                ? solar.GetPowerW(sunAU, r.plannedOperationsMonths / 12f) * (1f - eclipse)
                : 0f;
            r.instrumentPeakPowerW = d.Instruments.Sum(i => i.peakPowerW);
            r.peakLoadW = housekeeping + antennaPower + r.instrumentPeakPowerW;
            r.averageLoadW = housekeeping
                             + antennaPower * (contactHours / 24f)
                             + d.Instruments.Sum(i => i.averagePowerW * i.dutyCycle);
            r.powerBalanceW = r.powerGeneratedAvgW - r.peakLoadW;
            r.eclipseEnergyRequiredWh = orbit != null ? r.peakLoadW * orbit.maxEclipseMinutes / 60f : 0f;
            r.batteryUsableWh = battery != null ? battery.UsableWh : 0f;
            r.safeModeAutonomyHours = housekeeping > 0f ? r.batteryUsableWh / housekeeping : 0f;
            r.powerAvailabilityFraction = r.powerBalanceW < 0f && r.peakLoadW > 0f
                ? Mathf.Clamp01(r.powerGeneratedAvgW / r.peakLoadW)
                : 1f;

            // 3. PRESUPUESTO
            r.hardwareCostMUSD = d.AllComponents.Sum(c => c.costMUSD);
            r.launchCostMUSD = lv != null ? lv.launchCostMUSD : 0f;
            r.operationsCostMUSD = orbit != null
                ? r.plannedOperationsMonths * (OperationsCostPerMonth(orbit.regime) * (bus != null ? bus.operationsCostScale : 1f)
                                               + (network != null ? network.costPerMonthMUSD : 0f))
                : 0f;
            r.totalCostMUSD = r.hardwareCostMUSD + r.launchCostMUSD + r.operationsCostMUSD;
            r.budgetLimitMUSD = scenario != null && !sandbox ? scenario.budgetLimitMUSD : 0f;

            // 4. DATOS  /  5. COMUNICACIONES
            r.dataGeneratedGBPerDay = d.Instruments.Sum(i => i.dataVolumeGBPerDay * i.dutyCycle)
                                      * r.powerAvailabilityFraction
                                      + (bus != null ? bus.housekeepingDataGBPerDay : 0f);
            r.downlinkRateMbps = orbit != null ? orbit.GetDataRateMbps(antenna, network) : 0f;
            r.downlinkCapacityGBPerDay = orbit != null ? orbit.GetDailyDownlinkGB(antenna, network) : 0f;
            r.contactHoursPerDay = contactHours;
            r.onboardStorageGB = d.Computer != null ? d.Computer.storageGB : 0f;
            r.storageDays = r.dataGeneratedGBPerDay > 0f ? r.onboardStorageGB / r.dataGeneratedGBPerDay : 0f;
            r.dataLostGBPerDay = Mathf.Max(0f, r.dataGeneratedGBPerDay - r.downlinkCapacityGBPerDay);
            r.dataReturnFraction = r.dataGeneratedGBPerDay > 0f
                ? Mathf.Clamp01(r.downlinkCapacityGBPerDay / r.dataGeneratedGBPerDay)
                : 1f;

            // CIENCIA
            r.potentialScienceValue = d.Instruments.Sum(i => InstrumentScience(i, orbit, d.AttitudeControl));
            r.expectedScienceValue = r.potentialScienceValue * r.dataReturnFraction * r.powerAvailabilityFraction;

            BuildReadings(r, d);
            return r;
        }

        /// <summary>Tsiolkovsky para la nave: Δv = Isp · g0 · ln(m0 / (m0 − m_prop)), en km/s.</summary>
        public static float SpacecraftDeltaVKmS(float wetMassKg, float propellantKg, float ispSeconds)
        {
            float dryKg = wetMassKg - propellantKg;
            if (wetMassKg <= 0f || dryKg <= 0f || propellantKg <= 0f) return 0f;
            return ispSeconds * OrbitAndLaunchValidator.StandardGravity * Mathf.Log(wetMassKg / dryKg) / 1000f;
        }

        private static void BuildReadings(TelemetryReport r, SpacecraftDesign d)
        {
            string orbitName = d.Orbit != null ? d.Orbit.regime.ToString() : "—";
            string lvName = d.LaunchVehicle != null ? d.LaunchVehicle.displayName : "sin cohete";

            // 1. Masa
            var mass = new ResourceReading
            {
                kind = ResourceKind.Mass, value = r.totalMassKg, limit = r.launchCapacityKg, unit = "kg",
                summary = $"{r.totalMassKg:N0} kg ({r.massWithMarginKg:N0} kg con margen {MassGrowthAllowance:P0}) / capacidad {lvName} a {orbitName}: {r.launchCapacityKg:N0} kg"
            };
            if (d.LaunchVehicle != null && d.Orbit != null && (r.launchCapacityKg <= 0f || r.totalMassKg > r.launchCapacityKg))
                mass.state = ResourceState.Critical;
            else if (r.launchCapacityKg > 0f && r.massWithMarginKg > r.launchCapacityKg)
                mass.state = ResourceState.Warning;
            r.resources.Add(mass);

            // 2. Energía
            var power = new ResourceReading
            {
                kind = ResourceKind.Power, value = r.peakLoadW, limit = r.powerGeneratedAvgW, unit = "W",
                summary = $"Consumo pico {r.peakLoadW:N0} W / generación media {r.powerGeneratedAvgW:N0} W (balance {r.powerBalanceW:+#,0;-#,0;0} W, EOL {r.powerGeneratedEolW:N0} W)"
            };
            if (r.powerBalanceW < 0f)
            {
                power.state = ResourceState.Critical;
                r.alerts.Add($"DÉFICIT ENERGÉTICO: faltan {-r.powerBalanceW:N0} W. Los instrumentos solo podrán operar el {r.powerAvailabilityFraction:P0} del tiempo.");
            }
            else if (d.Battery != null && r.eclipseEnergyRequiredWh > r.batteryUsableWh)
            {
                power.state = ResourceState.Warning;
                r.alerts.Add($"Batería insuficiente para el eclipse: se necesitan {r.eclipseEnergyRequiredWh:N0} Wh y hay {r.batteryUsableWh:N0} Wh útiles.");
            }
            else if (r.powerGeneratedEolW < r.peakLoadW)
            {
                power.state = ResourceState.Warning;
                r.alerts.Add($"Al final de vida los paneles degradados darán {r.powerGeneratedEolW:N0} W, menos que el consumo pico ({r.peakLoadW:N0} W).");
            }
            r.resources.Add(power);

            // 3. Presupuesto
            var budget = new ResourceReading
            {
                kind = ResourceKind.Budget, value = r.totalCostMUSD, limit = r.budgetLimitMUSD, unit = "$M",
                summary = $"${r.totalCostMUSD:N1} M (hardware {r.hardwareCostMUSD:N1} + lanzamiento {r.launchCostMUSD:N1} + operaciones {r.operationsCostMUSD:N1}) / límite ${r.budgetLimitMUSD:N0} M"
            };
            if (r.budgetLimitMUSD > 0f)
            {
                if (r.totalCostMUSD > r.budgetLimitMUSD) budget.state = ResourceState.Critical;
                else if (r.totalCostMUSD > r.budgetLimitMUSD * (1f - BudgetReserveFraction)) budget.state = ResourceState.Warning;
            }
            r.resources.Add(budget);

            // 4. Datos
            var data = new ResourceReading
            {
                kind = ResourceKind.Data, value = r.dataGeneratedGBPerDay, limit = r.downlinkCapacityGBPerDay, unit = "GB/día",
                summary = $"Generados {r.dataGeneratedGBPerDay:N1} GB/día; se pierden {r.dataLostGBPerDay:N1} GB/día"
            };
            // 5. Comunicaciones
            string band = d.Antenna != null ? d.Antenna.displayName : "sin antena";
            string net = d.GroundNetwork != null ? d.GroundNetwork.displayName : "sin red terrena";
            var comms = new ResourceReading
            {
                kind = ResourceKind.Comms, value = r.downlinkCapacityGBPerDay, limit = r.dataGeneratedGBPerDay, unit = "GB/día",
                summary = $"{band} + {net} en {orbitName}: {r.downlinkRateMbps:N2} Mbps × {r.contactHoursPerDay:N1} h/día = {r.downlinkCapacityGBPerDay:N1} GB/día"
            };
            if (r.dataLostGBPerDay > 0f)
            {
                data.state = comms.state = ResourceState.Critical;
                r.alerts.Add($"Pérdida de ciencia: los instrumentos generan {r.dataGeneratedGBPerDay:N1} GB/día, pero {band} en {orbitName} " +
                             $"solo transmite {r.downlinkCapacityGBPerDay:N1} GB/día ({r.dataReturnFraction:P0} de retorno).");
            }
            else if (r.dataGeneratedGBPerDay > 0.8f * r.downlinkCapacityGBPerDay)
            {
                data.state = comms.state = ResourceState.Warning;
            }
            r.resources.Add(data);
            r.resources.Add(comms);

            // 6. Tiempo
            var time = new ResourceReading
            {
                kind = ResourceKind.Time, value = r.developmentMonths, limit = r.maxDevelopmentMonths, unit = "meses",
                summary = $"Desarrollo {r.developmentMonths:N0} / {r.maxDevelopmentMonths:N0} meses; vida útil {r.estimatedLifeMonths:N0} meses (plan {r.plannedOperationsMonths:N0})"
            };
            if (r.maxDevelopmentMonths > 0f && r.developmentMonths > r.maxDevelopmentMonths)
                time.state = ResourceState.Critical;
            else if (r.estimatedLifeMonths > 0f && r.estimatedLifeMonths < r.plannedOperationsMonths)
            {
                time.state = ResourceState.Warning;
                r.alerts.Add($"La vida de diseño del bus ({r.estimatedLifeMonths:N0} meses) es menor que la misión planificada ({r.plannedOperationsMonths:N0}).");
            }
            r.resources.Add(time);
        }
    }
}
