using System.Linq;
using System.Text;
using MissionDesign.Core;

namespace MissionDesign.Simulation
{
    /// <summary>Coordenadas ilustrativas usadas por el juego (sitio de lanzamiento y amerizaje). Aproximadas.</summary>
    public static class MissionGeo
    {
        public const double LaunchLat = 28.573, LaunchLon = -80.649;          // costa de Florida
        public const double SplashLat = 32.60, SplashLon = -117.90;          // Pacífico, frente a San Diego

        public static string Format(double lat, double lon) =>
            $"{System.Math.Abs(lat):F3}° {(lat >= 0 ? "N" : "S")}, {System.Math.Abs(lon):F3}° {(lon >= 0 ? "E" : "O")}";
    }

    /// <summary>
    /// Informe de la exploración (bitácora en primera persona) que acompaña a las fotografías.
    /// Misiones tripuladas: lo firma la tripulación del equipo (roles, no personas reales); robóticas: el equipo de operaciones.
    /// </summary>
    public static class ExplorationReport
    {
        public sealed class Data
        {
            public string title;
            public string signature;
            public string body;
        }

        /// <summary>Dónde está la cámara en el juego (se muestra cuando no hubo fotos).</summary>
        public const string HowToAddCamera =
            "Para traer fotos: rol <b>Científico Principal</b> (tecla 3) → catálogo de instrumentos → «Kit de cámaras de exploración» " +
            "(o las cámaras de Orion / de la tripulación). En órbita terrestre fotografía la Tierra; con destino lunar, la Luna.";

        public static Data Build(MissionOutcomeReport o, string teamName = null)
        {
            string team = string.IsNullOrWhiteSpace(teamName) ? "tu equipo" : teamName;
            var r = new Data
            {
                title = o.crewed ? "INFORME DE LA TRIPULACIÓN" : "INFORME DEL EQUIPO DE EXPLORACIÓN",
                signature = o.crewed
                    ? $"Comandante, piloto y especialistas de misión · tripulación de {team}"
                    : $"Equipo de operaciones científicas · {team}"
            };
            // Párrafos cortos: una idea por línea, con la etiqueta del momento en color.
            var sb = new StringBuilder();
            void P(string label, string text) => sb.Append(sb.Length > 0 ? "\n" : string.Empty).Append($"<color=#5FB4FF><b>{label}</b></color>  {text}");
            string geoLaunch = $"<size=85%><color=#9FB0C8>({MissionGeo.Format(MissionGeo.LaunchLat, MissionGeo.LaunchLon)})</color></size>";

            P("Día 0", $"{(o.crewed ? "Despegamos" : "La nave despegó")} desde Florida a bordo de {o.launchVehicle}. {geoLaunch}");
            if (!o.launchSucceeded)
            {
                P("Ascenso", "El lanzador falló: no hubo exploración. La lección está en el análisis post-misión.");
                r.body = sb.ToString();
                return r;
            }
            if (!o.orbitInsertionSucceeded)
            {
                P("Inserción", "Llegamos al espacio, pero no alcanzamos la órbita prevista.");
                r.body = sb.ToString();
                return r;
            }

            bool flyby = o.returnsToEarth;
            if (flyby)
            {
                var fr = FreeReturnTrajectory.Default;
                P("Día 1", o.crewed
                    ? "Un día en órbita alta probando el soporte vital. Luego el motor nos lanzó hacia la Luna (TLI)."
                    : "Sistemas verificados en órbita alta. El motor impulsó la nave hacia la Luna (TLI).");
                P($"Día {fr.flybyTimeDays:F1}", $"{(o.crewed ? "Pasamos" : "La nave pasó")} a {fr.flybyAltitudeKm:N0} km sobre la cara oculta.");
                P("Vista", o.crewed
                    ? "Vimos la cuenca Oriental, cráteres brillantes y la Tierra ocultarse tras el borde lunar."
                    : "Las cámaras registraron la cuenca Oriental y la Tierra ocultándose tras el borde lunar.");
            }
            else
            {
                P("Llegada", $"Inserción en la órbita {o.orbit}.");
                P("Trabajo", o.reachedMoon ? "Empezó el mapeo de cráteres, mares y zonas de aterrizaje." : "Empezaron las operaciones científicas.");
            }

            if (o.photosTaken > 0)
                P("Fotos", $"~{o.photosTaken:N0} fotos {(o.reachedMoon ? "de la Luna" : "de la Tierra")}: {o.photosTransmitted:N0} por el enlace" +
                           (o.photosRecovered > 0 ? $" y {o.photosRecovered:N0} en las tarjetas SD." : "."));
            else if (o.cameras != null && o.cameras.Length > 0)
                P("Fotos", "Las cámaras no llegaron a operar: la nave no alcanzó su órbita.");
            else
                P("Fotos", "La nave no llevaba cámaras. " + HowToAddCamera);

            var incidents = o.events.Where(e => !e.positive).Select(e => e.mitigated ? $"{e.displayName} (mitigado)" : e.displayName).ToList();
            if (incidents.Count > 0) P("Incidencias", string.Join("; ", incidents) + ".");

            if (flyby)
            {
                if (o.survivedOperations)
                    P($"Día {o.achievedOperationsDays:F1}", $"{(o.crewed ? "Amerizamos" : "La cápsula amerizó")} en el Pacífico " +
                        $"<size=85%><color=#9FB0C8>({MissionGeo.Format(MissionGeo.SplashLat, MissionGeo.SplashLon)})</color></size>." +
                        (o.photosRecovered > 0 ? " Se recuperaron las tarjetas SD con el archivo completo." : string.Empty));
                else
                    P("Regreso", "La cápsula no se recuperó: solo quedan las imágenes transmitidas.");
            }

            P("Conclusión", $"{MissionOutcomeReport.VerdictLabel(o.verdict)} · {o.scienceAchieved:N0} de {o.scienceTarget:N0} puntos de ciencia · rango {o.rank}.");
            r.body = sb.ToString();
            return r;
        }

        /// <summary>Versión de texto plano para el archivo .txt junto al reporte.</summary>
        public static string ToPlainText(Data d, string[] captions)
        {
            var sb = new StringBuilder();
            sb.AppendLine(d.title).AppendLine(d.signature).AppendLine();
            sb.AppendLine(System.Text.RegularExpressions.Regex.Replace(d.body, "<.*?>", string.Empty));
            if (captions != null && captions.Length > 0)
            {
                sb.AppendLine().AppendLine("Fotografías:");
                for (int i = 0; i < captions.Length; i++) sb.AppendLine($"  {i + 1:00}. {captions[i]}");
            }
            return sb.ToString();
        }
    }
}
