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
            $"{System.Math.Abs(lat):F3}° {(lat >= 0 ? "N" : "S")}, {System.Math.Abs(lon):F3}° {(lon >= 0 ? "E" : Loc.L("O", "W"))}";
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
        public static string HowToAddCamera => Loc.L(
            "Para traer fotos: rol <b>Científico Principal</b> (tecla 3) → catálogo de instrumentos → «Kit de cámaras de exploración» " +
            "(o las cámaras de Orion / de la tripulación). En órbita terrestre fotografía la Tierra; con destino lunar, la Luna.",
            "To bring back photos: <b>Principal Scientist</b> role (key 3) → instrument catalog → \"Exploration camera kit\" " +
            "(or the Orion / crew cameras). In Earth orbit it photographs the Earth; with a lunar destination, the Moon.");

        public static Data Build(MissionOutcomeReport o, string teamName = null)
        {
            string team = string.IsNullOrWhiteSpace(teamName) ? Loc.L("tu equipo", "your team") : teamName;
            var r = new Data
            {
                title = o.crewed ? Loc.L("INFORME DE LA TRIPULACIÓN", "CREW REPORT") : Loc.L("INFORME DEL EQUIPO DE EXPLORACIÓN", "EXPLORATION TEAM REPORT"),
                signature = o.crewed
                    ? Loc.L($"Comandante, piloto y especialistas de misión · tripulación de {team}", $"Commander, pilot and mission specialists · crew of {team}")
                    : Loc.L($"Equipo de operaciones científicas · {team}", $"Science operations team · {team}")
            };
            // Párrafos cortos: una idea por línea, con la etiqueta del momento en color.
            var sb = new StringBuilder();
            void P(string label, string text) => sb.Append(sb.Length > 0 ? "\n" : string.Empty).Append($"<color=#5FB4FF><b>{label}</b></color>  {text}");
            string geoLaunch = $"<size=85%><color=#9FB0C8>({MissionGeo.Format(MissionGeo.LaunchLat, MissionGeo.LaunchLon)})</color></size>";

            P(Loc.L("Día 0", "Day 0"), Loc.L($"{(o.crewed ? "Despegamos" : "La nave despegó")} desde Florida a bordo de {o.launchVehicle}. {geoLaunch}",
                                         $"{(o.crewed ? "We lifted off" : "The spacecraft lifted off")} from Florida aboard {o.launchVehicle}. {geoLaunch}"));
            if (!o.launchSucceeded)
            {
                P(Loc.L("Ascenso", "Ascent"), Loc.L("El lanzador falló: no hubo exploración. La lección está en el análisis post-misión.", "The launcher failed: there was no exploration. The lesson is in the post-mission analysis."));
                r.body = sb.ToString();
                return r;
            }
            if (!o.orbitInsertionSucceeded)
            {
                P(Loc.L("Inserción", "Insertion"), Loc.L("Llegamos al espacio, pero no alcanzamos la órbita prevista.", "We reached space, but not the planned orbit."));
                r.body = sb.ToString();
                return r;
            }

            bool flyby = o.returnsToEarth;
            if (flyby)
            {
                var fr = FreeReturnTrajectory.Default;
                P(Loc.L("Día 1", "Day 1"), o.crewed
                    ? Loc.L("Un día en órbita alta probando el soporte vital. Luego el motor nos lanzó hacia la Luna (TLI).", "A day in high orbit testing life support. Then the engine sent us toward the Moon (TLI).")
                    : Loc.L("Sistemas verificados en órbita alta. El motor impulsó la nave hacia la Luna (TLI).", "Systems checked out in high orbit. The engine sent the spacecraft toward the Moon (TLI)."));
                P(Loc.L($"Día {fr.flybyTimeDays:F1}", $"Day {fr.flybyTimeDays:F1}"), Loc.L($"{(o.crewed ? "Pasamos" : "La nave pasó")} a {fr.flybyAltitudeKm:N0} km sobre la cara oculta.",
                                                                     $"{(o.crewed ? "We passed" : "The spacecraft passed")} {fr.flybyAltitudeKm:N0} km above the far side."));
                P(Loc.L("Vista", "View"), o.crewed
                    ? Loc.L("Vimos la cuenca Oriental, cráteres brillantes y la Tierra ocultarse tras el borde lunar.", "We saw the Orientale basin, bright craters and the Earth setting behind the lunar limb.")
                    : Loc.L("Las cámaras registraron la cuenca Oriental y la Tierra ocultándose tras el borde lunar.", "The cameras captured the Orientale basin and the Earth setting behind the lunar limb."));
            }
            else
            {
                P(Loc.L("Llegada", "Arrival"), Loc.L($"Inserción en la órbita {o.orbit}.", $"Insertion into {o.orbit}."));
                P(Loc.L("Trabajo", "Work"), o.reachedMoon ? Loc.L("Empezó el mapeo de cráteres, mares y zonas de aterrizaje.", "Mapping of craters, maria and landing zones began.") : Loc.L("Empezaron las operaciones científicas.", "Science operations began."));
            }

            if (o.photosTaken > 0)
                P(Loc.L("Fotos", "Photos"), Loc.L($"~{o.photosTaken:N0} fotos {(o.reachedMoon ? "de la Luna" : "de la Tierra")}: {o.photosTransmitted:N0} por el enlace" +
                                                  (o.photosRecovered > 0 ? $" y {o.photosRecovered:N0} en las tarjetas SD." : "."),
                                                  $"~{o.photosTaken:N0} photos {(o.reachedMoon ? "of the Moon" : "of Earth")}: {o.photosTransmitted:N0} over the link" +
                                                  (o.photosRecovered > 0 ? $" and {o.photosRecovered:N0} on the SD cards." : ".")));
            else if (o.cameras != null && o.cameras.Length > 0)
                P(Loc.L("Fotos", "Photos"), Loc.L("Las cámaras no llegaron a operar: la nave no alcanzó su órbita.", "The cameras never operated: the spacecraft did not reach its orbit."));
            else
                P(Loc.L("Fotos", "Photos"), Loc.L("La nave no llevaba cámaras. ", "The spacecraft carried no cameras. ") + HowToAddCamera);

            var incidents = o.events.Where(e => !e.positive).Select(e => e.mitigated ? $"{e.displayName} ({Loc.L("mitigado", "mitigated")})" : e.displayName).ToList();
            if (incidents.Count > 0) P(Loc.L("Incidencias", "Incidents"), string.Join("; ", incidents) + ".");

            if (flyby)
            {
                if (o.survivedOperations)
                    P(Loc.L($"Día {o.achievedOperationsDays:F1}", $"Day {o.achievedOperationsDays:F1}"),
                        Loc.L($"{(o.crewed ? "Amerizamos" : "La cápsula amerizó")} en el Pacífico ", $"{(o.crewed ? "We splashed down" : "The capsule splashed down")} in the Pacific ") +
                        $"<size=85%><color=#9FB0C8>({MissionGeo.Format(MissionGeo.SplashLat, MissionGeo.SplashLon)})</color></size>." +
                        (o.photosRecovered > 0 ? Loc.L(" Se recuperaron las tarjetas SD con el archivo completo.", " The SD cards with the full archive were recovered.") : string.Empty));
                else
                    P(Loc.L("Regreso", "Return"), Loc.L("La cápsula no se recuperó: solo quedan las imágenes transmitidas.", "The capsule was not recovered: only the transmitted images remain."));
            }

            P(Loc.L("Conclusión", "Conclusion"), Loc.L($"{MissionOutcomeReport.VerdictLabel(o.verdict)} · {o.scienceAchieved:N0} de {o.scienceTarget:N0} puntos de ciencia · rango {o.rank}.",
                                                $"{MissionOutcomeReport.VerdictLabel(o.verdict)} · {o.scienceAchieved:N0} of {o.scienceTarget:N0} science points · rank {o.rank}."));
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
                sb.AppendLine().AppendLine(Loc.L("Fotografías:", "Photographs:"));
                for (int i = 0; i < captions.Length; i++) sb.AppendLine($"  {i + 1:00}. {captions[i]}");
            }
            return sb.ToString();
        }
    }
}
