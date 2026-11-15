using System;
using System.Collections.Generic;

namespace MissionDesign.Simulation
{
    [Serializable]
    public struct TrajectorySample
    {
        /// <summary>Días desde la inyección translunar (TLI).</summary>
        public float tDays;
        /// <summary>Posición en el marco rotatorio Tierra-Luna (km), origen en el baricentro, Luna en +X.</summary>
        public float xKm;
        public float yKm;
        /// <summary>-1 = órbitas terrestres previas a la TLI, 0 = ida (hasta el sobrevuelo), 1 = vuelta.</summary>
        public int leg;
    }

    [Serializable]
    public struct TrajectoryMilestone
    {
        public int number;
        public string title;
        public string detail;
        public float tDays;
        public float xKm;
        public float yKm;
    }

    [Serializable]
    public sealed class FreeReturnSolution
    {
        public bool converged;
        public float parkingAltitudeKm;
        public float injectionAngleDeg;
        public float injectionSpeedKmS;
        public float tliDeltaVKmS;
        public float flybyAltitudeKm;
        public float flybyTimeDays;
        public float returnPerigeeAltitudeKm;
        public float totalDays;
        public bool passesFarSide;
        public List<TrajectorySample> samples = new List<TrajectorySample>();
        /// <summary>Despegue, órbita de estacionamiento y órbita terrestre alta (~1 día) hasta la TLI; tDays negativos.</summary>
        public List<TrajectorySample> preTliSamples = new List<TrajectorySample>();
        public float launchDays;
        public float highOrbitApogeeKm;
        public List<TrajectoryMilestone> milestones = new List<TrajectoryMilestone>();
    }

    /// <summary>
    /// Trayectoria de retorno libre (tipo Apolo 13 / Artemis II) calculada con el problema restringido
    /// circular de tres cuerpos (CR3BP) Tierra-Luna, integrado con RK4 de paso adaptativo en el marco rotatorio.
    /// La nave se inyecta desde una órbita circular de estacionamiento; se buscan el ángulo y la velocidad de
    /// inyección que dan el sobrevuelo lunar deseado y un regreso a la atmósfera sin encender motores
    /// (salvo pequeñas correcciones de rumbo). En el marco rotatorio el recorrido dibuja el "8" del diagrama de Artemis II.
    /// </summary>
    public static class FreeReturnTrajectory
    {
        public const double Mu = 0.012150585609624;          // M_Luna / (M_Tierra + M_Luna)
        public const double LengthKm = 384400.0;              // distancia media Tierra-Luna
        public const double TimeUnitS = 375190.26;            // sqrt(L³ / G(M1+M2))
        public const double VelocityUnitKmS = LengthKm / TimeUnitS;
        public const double EarthRadiusKm = 6378.137;
        public const double MoonRadiusKm = 1737.4;
        public const double EarthMuKm3S2 = 398600.4418;
        public const double EntryInterfaceKm = 122.0;

        private const double MaxDays = 14.0;

        private static FreeReturnSolution cached;
        private static readonly object Gate = new object();

        /// <summary>Solución de referencia (185 km → sobrevuelo a 7 400 km → perigeo 50 km), calculada una vez (~0,5 s).</summary>
        public static FreeReturnSolution Default
        {
            get
            {
                lock (Gate) return cached ?? (cached = Solve());
            }
        }

        public static bool IsReady => cached != null;

        /// <summary>Calcula la solución en segundo plano para que no se note al entrar en la vista orbital.</summary>
        public static void WarmUpAsync() => System.Threading.Tasks.Task.Run(() => { _ = Default; });

        public static FreeReturnSolution Solve(double parkingAltKm = 185.0, double targetFlybyAltKm = 7400.0,
            double targetReturnPerigeeKm = 50.0)
        {
            // 1) Búsqueda gruesa: ángulo de inyección × velocidad de inyección.
            double vCirc = Math.Sqrt(EarthMuKm3S2 / (EarthRadiusKm + parkingAltKm));
            double vEsc = vCirc * Math.Sqrt(2.0);
            double bestCost = double.MaxValue, bestTheta = 0, bestV = 0;
            for (double th = 0; th < 360; th += 3.0)
            {
                for (double v = vEsc - 0.12; v <= vEsc + 0.02; v += 0.004)
                {
                    var r = Propagate(parkingAltKm, th, v, null);
                    double cost = Cost(r, targetFlybyAltKm, targetReturnPerigeeKm);
                    if (cost < bestCost) { bestCost = cost; bestTheta = th; bestV = v; }
                }
            }

            // 2) Refinamiento por descenso de coordenadas con pasos decrecientes.
            double stepTh = 1.5, stepV = 0.002;
            for (int iter = 0; iter < 60 && (stepTh > 1e-4 || stepV > 1e-7); iter++)
            {
                bool improved = false;
                foreach (var (dTh, dV) in new[] { (stepTh, 0.0), (-stepTh, 0.0), (0.0, stepV), (0.0, -stepV) })
                {
                    var r = Propagate(parkingAltKm, bestTheta + dTh, bestV + dV, null);
                    double cost = Cost(r, targetFlybyAltKm, targetReturnPerigeeKm);
                    if (cost < bestCost) { bestCost = cost; bestTheta += dTh; bestV += dV; improved = true; }
                }
                if (!improved) { stepTh *= 0.5; stepV *= 0.5; }
            }

            // 3) Trayectoria final muestreada + hitos.
            var samples = new List<TrajectorySample>();
            var final = Propagate(parkingAltKm, bestTheta, bestV, samples);
            var sol = new FreeReturnSolution
            {
                parkingAltitudeKm = (float)parkingAltKm,
                injectionAngleDeg = (float)bestTheta,
                injectionSpeedKmS = (float)bestV,
                tliDeltaVKmS = (float)(bestV - vCirc),
                flybyAltitudeKm = (float)final.flybyAltKm,
                flybyTimeDays = (float)final.flybyDays,
                returnPerigeeAltitudeKm = (float)final.returnPerigeeAltKm,
                totalDays = (float)final.endDays,
                passesFarSide = final.farSide,
                samples = samples
            };
            sol.converged = final.returned && final.farSide
                            && Math.Abs(final.flybyAltKm - targetFlybyAltKm) < 0.05 * targetFlybyAltKm
                            && final.returnPerigeeAltKm < EntryInterfaceKm;
            BuildPreTli(sol);
            BuildMilestones(sol);
            return sol;
        }

        private struct Run
        {
            public double flybyAltKm, flybyDays, returnPerigeeAltKm, endDays;
            public bool returned, farSide, hitMoon;
        }

        private static double Cost(Run r, double targetFlyby, double targetPerigee)
        {
            if (r.hitMoon) return 1e9;
            double c = Math.Abs(r.flybyAltKm - targetFlyby) / 500.0;
            c += Math.Abs(r.returnPerigeeAltKm - targetPerigee) / 50.0;
            if (!r.farSide) c += 50.0;
            if (!r.returned) c += 100.0;
            return c;
        }

        /// <summary>Integra desde la inyección (TLI) en el marco rotatorio adimensional.</summary>
        private static Run Propagate(double parkingAltKm, double thetaDeg, double vInjKmS, List<TrajectorySample> output)
        {
            double r0 = (EarthRadiusKm + parkingAltKm) / LengthKm;
            double th = thetaDeg * Math.PI / 180.0;
            double x = -Mu + r0 * Math.Cos(th), y = r0 * Math.Sin(th);
            double vin = vInjKmS / VelocityUnitKmS;
            // Velocidad inercial prograda (sentido antihorario) → marco rotatorio: v_rot = v_in − ω×r.
            double vx = -vin * Math.Sin(th) + y;
            double vy = vin * Math.Cos(th) - x;

            var run = new Run { flybyAltKm = double.MaxValue, returnPerigeeAltKm = double.MaxValue };
            double t = 0, tMax = MaxDays * 86400.0 / TimeUnitS;
            double minMoon = double.MaxValue;
            bool pastFlyby = false, leftEarth = false;
            double lastSampleT = -1;
            double flybyX = 0;

            while (t < tMax)
            {
                double r1 = Math.Sqrt((x + Mu) * (x + Mu) + y * y);
                double r2 = Math.Sqrt((x - 1 + Mu) * (x - 1 + Mu) + y * y);
                double rMin = Math.Min(r1, r2);
                double dt = Math.Max(2e-6, Math.Min(2e-3, 0.02 * Math.Pow(rMin, 1.5)));

                if (output != null && (t - lastSampleT) * TimeUnitS > 1800.0)   // una muestra cada ~30 min
                {
                    output.Add(new TrajectorySample
                    {
                        tDays = (float)(t * TimeUnitS / 86400.0),
                        xKm = (float)(x * LengthKm), yKm = (float)(y * LengthKm), leg = pastFlyby ? 1 : 0
                    });
                    lastSampleT = t;
                }

                if (r1 > 0.2) leftEarth = true;
                double moonAlt = r2 * LengthKm - MoonRadiusKm;
                if (moonAlt < 0) { run.hitMoon = true; break; }
                if (!pastFlyby)
                {
                    if (r2 < minMoon) { minMoon = r2; run.flybyDays = t * TimeUnitS / 86400.0; flybyX = x; }
                    else if (minMoon < 0.2 && r2 > minMoon * 1.05) pastFlyby = true;
                }
                if (pastFlyby && leftEarth)
                {
                    double earthAlt = r1 * LengthKm - EarthRadiusKm;
                    if (earthAlt < run.returnPerigeeAltKm) run.returnPerigeeAltKm = earthAlt;
                    if (earthAlt < EntryInterfaceKm) { run.returned = true; break; }
                    if (r1 < 0.3 && earthAlt > run.returnPerigeeAltKm + 2000) break;   // perigeo pasado sin reentrar
                }

                Rk4(ref x, ref y, ref vx, ref vy, dt);
                t += dt;
            }

            run.flybyAltKm = minMoon * LengthKm - MoonRadiusKm;
            run.farSide = flybyX > 1 - Mu;              // pasa por detrás de la Luna (lado opuesto a la Tierra)
            run.endDays = t * TimeUnitS / 86400.0;
            if (output != null)
                output.Add(new TrajectorySample
                {
                    tDays = (float)run.endDays, xKm = (float)(x * LengthKm), yKm = (float)(y * LengthKm), leg = 1
                });
            return run;
        }

        private static void Derivs(double x, double y, double vx, double vy, out double ax, out double ay)
        {
            double dx1 = x + Mu, dx2 = x - 1 + Mu;
            double r1 = Math.Sqrt(dx1 * dx1 + y * y), r2 = Math.Sqrt(dx2 * dx2 + y * y);
            double r13 = r1 * r1 * r1, r23 = r2 * r2 * r2;
            ax = 2 * vy + x - (1 - Mu) * dx1 / r13 - Mu * dx2 / r23;
            ay = -2 * vx + y - (1 - Mu) * y / r13 - Mu * y / r23;
        }

        private static void Rk4(ref double x, ref double y, ref double vx, ref double vy, double h)
        {
            Derivs(x, y, vx, vy, out double a1x, out double a1y);
            double x2 = x + 0.5 * h * vx, y2 = y + 0.5 * h * vy, vx2 = vx + 0.5 * h * a1x, vy2 = vy + 0.5 * h * a1y;
            Derivs(x2, y2, vx2, vy2, out double a2x, out double a2y);
            double x3 = x + 0.5 * h * vx2, y3 = y + 0.5 * h * vy2, vx3 = vx + 0.5 * h * a2x, vy3 = vy + 0.5 * h * a2y;
            Derivs(x3, y3, vx3, vy3, out double a3x, out double a3y);
            double x4 = x + h * vx3, y4 = y + h * vy3, vx4 = vx + h * a3x, vy4 = vy + h * a3y;
            Derivs(x4, y4, vx4, vy4, out double a4x, out double a4y);
            x += h / 6 * (vx + 2 * vx2 + 2 * vx3 + vx4);
            y += h / 6 * (vy + 2 * vy2 + 2 * vy3 + vy4);
            vx += h / 6 * (a1x + 2 * a2x + 2 * a3x + a4x);
            vy += h / 6 * (a1y + 2 * a2y + 2 * a3y + a4y);
        }

        /// <summary>Hitos numerados como en el diagrama de Artemis II (ida 1-5, vuelta 6-8).</summary>
        public const double HighOrbitApogeeKm = 70000.0;     // Artemis II: órbita alta de ~24 h para verificar Orion

        /// <summary>
        /// Fase previa a la TLI, como en el diagrama de la ESA: despegue → media vuelta en la órbita de estacionamiento →
        /// una revolución en órbita terrestre alta (Kepler) que termina en el perigeo donde se enciende el motor (TLI).
        /// </summary>
        private static void BuildPreTli(FreeReturnSolution s)
        {
            double th = s.injectionAngleDeg * Math.PI / 180.0;
            double ex = -Mu * LengthKm;
            double rp = EarthRadiusKm + s.parkingAltitudeKm, ra = EarthRadiusKm + HighOrbitApogeeKm;
            double a = (rp + ra) / 2, e = (ra - rp) / (ra + rp);
            double leoPeriod = 2 * Math.PI * Math.Sqrt(rp * rp * rp / EarthMuKm3S2) / 86400.0;
            double heoPeriod = 2 * Math.PI * Math.Sqrt(a * a * a / EarthMuKm3S2) / 86400.0;
            double leoDays = leoPeriod * 0.5;
            s.launchDays = (float)-(leoDays + heoPeriod);
            s.highOrbitApogeeKm = (float)HighOrbitApogeeKm;
            s.preTliSamples.Clear();

            // Ascenso + media órbita baja: del lado opuesto al perigeo (θ+π) hasta θ, prograda.
            const int leoSteps = 40;
            for (int k = 0; k <= leoSteps; k++)
            {
                double f = (double)k / leoSteps;
                double ang = th + Math.PI + Math.PI * f;
                double r = EarthRadiusKm + s.parkingAltitudeKm * Math.Min(1.0, f * 4.0);   // el ascenso dura ~1/4 del tramo
                s.preTliSamples.Add(new TrajectorySample { tDays = (float)(s.launchDays + leoDays * f), xKm = (float)(ex + r * Math.Cos(ang)), yKm = (float)(r * Math.Sin(ang)), leg = -1 });
            }
            // Órbita alta: una revolución completa desde el perigeo θ (ecuación de Kepler).
            const int heoSteps = 160;
            for (int k = 1; k <= heoSteps; k++)
            {
                double M = 2 * Math.PI * k / heoSteps, E = M;
                for (int it = 0; it < 12; it++) E -= (E - e * Math.Sin(E) - M) / (1 - e * Math.Cos(E));
                double nu = 2 * Math.Atan2(Math.Sqrt(1 + e) * Math.Sin(E / 2), Math.Sqrt(1 - e) * Math.Cos(E / 2));
                double r = a * (1 - e * Math.Cos(E));
                s.preTliSamples.Add(new TrajectorySample
                {
                    tDays = (float)(s.launchDays + leoDays + heoPeriod * k / heoSteps),
                    xKm = (float)(ex + r * Math.Cos(th + nu)), yKm = (float)(r * Math.Sin(th + nu)), leg = -1
                });
            }
        }

        /// <summary>Los 8 hitos del diagrama de Artemis II publicado por la ESA (ida 1-5, vuelta 6-8).</summary>
        private static void BuildMilestones(FreeReturnSolution s)
        {
            if (s.samples.Count == 0) return;
            TrajectorySample At(List<TrajectorySample> list, float days)
            {
                TrajectorySample best = list[0];
                foreach (var p in list) if (Math.Abs(p.tDays - days) < Math.Abs(best.tDays - days)) best = p;
                return best;
            }

            void Add(int n, string title, string detail, TrajectorySample p) =>
                s.milestones.Add(new TrajectoryMilestone { number = n, title = title, detail = detail, tDays = p.tDays, xKm = p.xKm, yKm = p.yKm });

            var pre = s.preTliSamples.Count > 0 ? s.preTliSamples : s.samples;
            var last = s.samples[s.samples.Count - 1];
            float heoStart = pre.Count > 41 ? pre[40].tDays : s.launchDays;
            Add(1, "Despegue: Florida", "El cohete SLS despega y deja la nave en una órbita baja de estacionamiento", pre[0]);
            Add(2, "Hacia la órbita alta", $"La etapa superior eleva el apogeo a ~{s.highOrbitApogeeKm:N0} km (~1 día de pruebas)", At(pre, heoStart));
            Add(3, "Se separa del lanzador", "Orion se separa de la etapa superior y practica maniobras de proximidad", At(pre, heoStart + 0.14f));
            Add(4, "Motor rumbo a la Luna (TLI)", $"El motor principal se enciende e impulsa la cápsula a la Luna: Δv ≈ {s.tliDeltaVKmS:F2} km/s", s.samples[0]);
            Add(5, "Sobrevuelo lunar", $"{s.flybyAltitudeKm:N0} km sobre la cara oculta, día {s.flybyTimeDays:F1}", At(s.samples, s.flybyTimeDays));
            Add(6, "Regreso a la Tierra", "Retorno libre: la gravedad lunar devuelve la nave sin encender el motor", At(s.samples, (s.flybyTimeDays + s.totalDays) * 0.55f));
            Add(7, "El módulo de tripulación se separa", "Se suelta el módulo de servicio; solo la cápsula entra en la atmósfera", At(s.samples, s.totalDays - 0.15f));
            Add(8, "Amerizaje: océano Pacífico", $"Reentrada a {EntryInterfaceKm:N0} km y paracaídas; día {s.totalDays:F1}", last);
        }
    }
}
