using System.Globalization;
using System.IO;
using MissionDesign.Simulation;
using UnityEditor;
using UnityEngine;

namespace MissionDesign.Editor
{
    /// <summary>
    /// Validación externa con NASA GMAT (General Mission Analysis Tool): exporta el estado justo después de la TLI calculado
    /// por el modelo CR3BP del juego a un script de GMAT que lo propaga con efemérides reales (DE405: Tierra con campo 4×4,
    /// Sol y Luna como masas puntuales) y, con un corrector diferencial, calcula la corrección de rumbo (TCM) necesaria para
    /// lograr el sobrevuelo de 7 400 km y el perigeo de regreso de 50 km. Ejecutar: GmatConsole.exe docs/validacion/gmat/free_return_artemis2.script
    /// </summary>
    public static class GmatValidation
    {
        public const string Folder = "docs/validacion/gmat";
        public const string ScriptName = "free_return_artemis2.script";
        public const string ResultsName = "resultados_gmat.txt";
        /// <summary>Época ilustrativa cercana a la TLI de Artemis II (abril de 2026).</summary>
        public const string Epoch = "02 Apr 2026 23:30:00.000";

        /// <summary>Quita tildes y símbolos (GMAT rechaza cualquier carácter fuera de ASCII).</summary>
        public static string Ascii(string text)
        {
            text = text.Replace("°", " deg").Replace("·", "-").Replace("×", "x").Replace("→", "->").Replace("−", "-");
            var sb = new System.Text.StringBuilder();
            foreach (char c in text.Normalize(System.Text.NormalizationForm.FormD))
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && c < 128) sb.Append(c);
            return sb.ToString();
        }

        [MenuItem("NASA SpaceApps/Export GMAT Validation Script")]
        public static string Export()
        {
            var s = FreeReturnTrajectory.Default;
            double th = s.injectionAngleDeg * System.Math.PI / 180.0;
            double r0 = FreeReturnTrajectory.EarthRadiusKm + s.parkingAltitudeKm;
            double v = s.injectionSpeedKmS;
            double w = 2.0 * System.Math.PI / (27.321661 * 86400.0);          // rad/s del marco Tierra-Luna
            // Estado relativo a la Tierra en el marco rotatorio (X hacia la Luna, Z normal a su órbita).
            double x = r0 * System.Math.Cos(th), y = r0 * System.Math.Sin(th);
            double vx = -v * System.Math.Sin(th) + w * y;                          // v_rot = v_inercial − ω × r
            double vy = v * System.Math.Cos(th) - w * x;

            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string folder = Path.Combine(root, Folder);
            Directory.CreateDirectory(folder);
            string results = Path.Combine(folder, ResultsName).Replace('\\', '/');
            string F(double d) => d.ToString("R", CultureInfo.InvariantCulture);

            string script = $@"%  MISSION TRADE-OFF · validación de la trayectoria de retorno libre (tipo Artemis II) con NASA GMAT
%  Estado inicial = salida del modelo CR3BP del juego justo después de la TLI:
%    parking {s.parkingAltitudeKm:F0} km · ángulo {s.injectionAngleDeg.ToString("F3", CultureInfo.InvariantCulture)}° · {s.injectionSpeedKmS.ToString("F4", CultureInfo.InvariantCulture)} km/s
%    CR3BP: sobrevuelo {s.flybyAltitudeKm:F0} km (día {s.flybyTimeDays.ToString("F2", CultureInfo.InvariantCulture)}) · perigeo de regreso {s.returnPerigeeAltitudeKm:F0} km (día {s.totalDays.ToString("F2", CultureInfo.InvariantCulture)})
%  Generado automáticamente por Unity (NASA SpaceApps → Export GMAT Validation Script).

Create CoordinateSystem EarthMoonRot;
EarthMoonRot.Origin = Earth;
EarthMoonRot.Axes = ObjectReferenced;
EarthMoonRot.XAxis = R;
EarthMoonRot.ZAxis = N;
EarthMoonRot.Primary = Earth;
EarthMoonRot.Secondary = Luna;

Create Spacecraft Orion;
Orion.DateFormat = UTCGregorian;
Orion.Epoch = '{Epoch}';
Orion.CoordinateSystem = EarthMoonRot;
Orion.DisplayStateType = Cartesian;
Orion.X = {F(x)};
Orion.Y = {F(y)};
Orion.Z = 0;
Orion.VX = {F(vx)};
Orion.VY = {F(vy)};
Orion.VZ = 0;
Orion.DryMass = 26500;

Create Spacecraft OrionTCM;
OrionTCM.DateFormat = UTCGregorian;
OrionTCM.Epoch = '{Epoch}';
OrionTCM.CoordinateSystem = EarthMoonRot;
OrionTCM.DisplayStateType = Cartesian;
OrionTCM.X = {F(x)};
OrionTCM.Y = {F(y)};
OrionTCM.Z = 0;
OrionTCM.VX = {F(vx)};
OrionTCM.VY = {F(vy)};
OrionTCM.VZ = 0;
OrionTCM.DryMass = 26500;

Create ForceModel CislunarFM;
CislunarFM.CentralBody = Earth;
CislunarFM.PrimaryBodies = {{Earth}};
CislunarFM.PointMasses = {{Sun, Luna}};
CislunarFM.Drag = None;
CislunarFM.SRP = Off;
CislunarFM.GravityField.Earth.Degree = 4;
CislunarFM.GravityField.Earth.Order = 4;

Create Propagator CislunarProp;
CislunarProp.FM = CislunarFM;
CislunarProp.Type = RungeKutta89;
CislunarProp.InitialStepSize = 30;
CislunarProp.Accuracy = 1e-11;
CislunarProp.MinStep = 0.001;
CislunarProp.MaxStep = 3600;
CislunarProp.MaxStepAttempts = 50;

Create ImpulsiveBurn TCM;
TCM.CoordinateSystem = Local;
TCM.Origin = Earth;
TCM.Axes = VNB;
TCM.Element1 = 0;
TCM.Element2 = 0;
TCM.Element3 = 0;

Create DifferentialCorrector DC;
DC.MaximumIterations = 60;

Create Variable t0 nomFlybyAlt nomFlybyDay nomPerigeeAlt nomReturnDay corrFlybyAlt corrFlybyDay corrPerigeeAlt corrReturnDay tcmMag;

Create ReportFile Results;
Results.Filename = '{results}';
Results.WriteHeaders = false;
Results.Precision = 10;

BeginMissionSequence;
t0 = Orion.A1ModJulian;

% 1) Estado nominal del juego propagado con efemérides reales (sin correcciones)
Propagate 'Nominal a periselenio' CislunarProp(Orion) {{Orion.Luna.Periapsis, Orion.ElapsedDays = 7}};
nomFlybyAlt = Orion.Luna.Altitude;
nomFlybyDay = Orion.A1ModJulian - t0;
Propagate 'Nominal a perigeo' CislunarProp(Orion) {{Orion.Earth.Periapsis, Orion.ElapsedDays = 9}};
nomPerigeeAlt = Orion.Earth.Altitude;
nomReturnDay = Orion.A1ModJulian - t0;
Report Results nomFlybyAlt nomFlybyDay nomPerigeeAlt nomReturnDay;

% 2) Corrección de rumbo (TCM) justo después de la TLI: sobrevuelo de 7 400 km y perigeo de 50 km
Target 'Corregir retorno libre' DC {{SolveMode = Solve, ExitMode = SaveAndContinue}};
   Vary 'TCM velocidad' DC(TCM.Element1 = 0, {{Perturbation = 1e-6, Lower = -0.3, Upper = 0.3, MaxStep = 0.004}});
   Vary 'TCM radial' DC(TCM.Element3 = 0, {{Perturbation = 1e-6, Lower = -0.3, Upper = 0.3, MaxStep = 0.004}});
   Maneuver TCM(OrionTCM);
   Propagate 'A periselenio' CislunarProp(OrionTCM) {{OrionTCM.Luna.Periapsis, OrionTCM.ElapsedDays = 7}};
   corrFlybyAlt = OrionTCM.Luna.Altitude;
   corrFlybyDay = OrionTCM.A1ModJulian - t0;
   Achieve 'Sobrevuelo 7400 km' DC(OrionTCM.Luna.Altitude = {s.flybyAltitudeKm.ToString("F0", CultureInfo.InvariantCulture)}, {{Tolerance = 25}});
   Propagate 'A perigeo' CislunarProp(OrionTCM) {{OrionTCM.Earth.Periapsis, OrionTCM.ElapsedDays = 9}};
   Achieve 'Perigeo 50 km' DC(OrionTCM.Earth.Altitude = {s.returnPerigeeAltitudeKm.ToString("F0", CultureInfo.InvariantCulture)}, {{Tolerance = 10}});
EndTarget;
corrPerigeeAlt = OrionTCM.Earth.Altitude;
corrReturnDay = OrionTCM.A1ModJulian - t0;
tcmMag = sqrt(TCM.Element1^2 + TCM.Element3^2) * 1000;
Report Results corrFlybyAlt corrFlybyDay corrPerigeeAlt corrReturnDay tcmMag;
";
            string path = Path.Combine(folder, ScriptName);
            File.WriteAllText(path, Ascii(script), System.Text.Encoding.ASCII);   // GMAT solo acepta ASCII
            Debug.Log($"[GMAT] Script exportado: {path}");
            return path;
        }
    }
}
