using System.Text;
using MissionDesign.Core;
using MissionDesign.Simulation;

namespace MissionDesign.Exploration
{
    /// <summary>
    /// Textos educativos del centro espacial (un solo lugar para editarlos). Cada entrada es {título, cuerpo}.
    /// Los datos de Artemis II provienen de la información pública de la NASA sobre la misión.
    /// Cada texto va en pareja español / inglés (<see cref="Loc.L"/>).
    /// </summary>
    public static class SpaceCenterContent
    {
        public sealed class PlanSheet
        {
            public readonly string file, title, caption;
            public PlanSheet(string file, string title, string caption) { this.file = file; this.title = title; this.caption = caption; }
        }

        public static PlanSheet[] Plans => new[]
        {
            new PlanSheet("Lamina_01_Plano_de_conjunto", Loc.L("LÁMINA 1 · PLANO DE CONJUNTO", "SHEET 1 · SITE PLAN"),
                Loc.L("Recorrido: Centro de Control → carretera de servicio → Edificio de Ensamblaje → camino de orugas → plataforma de lanzamiento.",
                      "Route: Control Center → service road → Assembly Building → crawlerway → launch pad.")),
            new PlanSheet("Lamina_02_Centro_de_Control", Loc.L("LÁMINA 2 · CENTRO DE CONTROL DE MISIÓN", "SHEET 2 · MISSION CONTROL CENTER"),
                Loc.L("Planta con corte a 3,5 m: vestíbulo con mesa de planos y recepción; sala de control con 3 gradas de consolas frente a las pantallas.",
                      "Floor plan cut at 3.5 m: lobby with a blueprint table and reception; control room with 3 tiers of consoles facing the screens.")),
            new PlanSheet("Lamina_03_Ensamblaje_y_sala_limpia", Loc.L("LÁMINA 3 · EDIFICIO DE ENSAMBLAJE Y SALA LIMPIA", "SHEET 3 · ASSEMBLY BUILDING AND CLEAN ROOM"),
                Loc.L("Planta con corte a 12 m: el cohete se apila sobre el lanzador móvil; la cápsula se prepara en la sala limpia (aire filtrado, trajes y control de partículas).",
                      "Floor plan cut at 12 m: the rocket is stacked on the mobile launcher; the capsule is prepared in the clean room (filtered air, suits and particle control).")),
            new PlanSheet("Lamina_04_Plataforma_de_lanzamiento", Loc.L("LÁMINA 4 · PLATAFORMA DE LANZAMIENTO", "SHEET 4 · LAUNCH PAD"),
                Loc.L("Losa de 90 m, montaje del cohete, torre de servicio de 110 m con brazo de acceso de la tripulación, foso de llamas y rampa del camino de orugas.",
                      "90 m pad, rocket mount, 110 m service tower with the crew access arm, flame trench and crawlerway ramp.")),
            new PlanSheet("Lamina_05_Sitio_lunar", Loc.L("LÁMINA 5 · SITIO LUNAR (MODELADO)", "SHEET 5 · LUNAR SITE (MODEL)"),
                Loc.L("Relieve del regolito con los cráteres principales y el módulo de aterrizaje. Escenario de visualización, no caminable.",
                      "Regolith relief with the main craters and the lander. A visualization set, not walkable.")),
        };

        public static string Controls => Loc.L(
            "<b>WASD</b> / flechas: caminar · <b>Shift</b>: correr · <b>Espacio</b>: saltar\n" +
            "<b>Clic derecho + ratón</b>: girar la cámara · <b>Rueda</b>: acercar/alejar\n" +
            "<b>E</b>: interactuar · <b>M</b>: planos · <b>N</b>: mapa · <b>V</b>: cámara en 1.ª / 3.ª persona\n" +
            "<b>Esc</b>: menú (viaje rápido entre edificios)",
            "<b>WASD</b> / arrows: walk · <b>Shift</b>: run · <b>Space</b>: jump\n" +
            "<b>Right-click + mouse</b>: turn the camera · <b>Wheel</b>: zoom in/out\n" +
            "<b>E</b>: interact · <b>M</b>: blueprints · <b>N</b>: map · <b>V</b>: 1st / 3rd person camera\n" +
            "<b>Esc</b>: menu (fast travel between buildings)");

        public static string[][] CrewArm => new[]
        {
            new[] { Loc.L("BRAZO DE ACCESO DE LA TRIPULACIÓN", "CREW ACCESS ARM"),
                Loc.L("Desde este brazo, a la altura de la cápsula, los astronautas cruzan a la <b>sala blanca</b> y entran en Orion " +
                      "unas horas antes del despegue. Técnicos con trajes limpios los ayudan a sentarse y cierran la escotilla.\n\n" +
                      "Antes del lanzamiento el brazo se retrae. Si hay una emergencia en la plataforma, la tripulación evacúa por un " +
                      "sistema de cestas que bajan deslizándose por cables hasta un búnker.\n\n" +
                      "<size=85%>En el juego el brazo está a 75 m; en la plataforma real de Artemis está aún más alto.</size>",
                      "From this arm, level with the capsule, the astronauts cross into the <b>white room</b> and board Orion " +
                      "a few hours before liftoff. Technicians in clean suits help them strap in and close the hatch.\n\n" +
                      "Before launch the arm swings away. If there is an emergency on the pad, the crew escapes in " +
                      "baskets that slide down cables to a bunker.\n\n" +
                      "<size=85%>In the game the arm is at 75 m; on the real Artemis pad it is even higher.</size>") },
        };

        public static string[][] Reception => new[]
        {
            new[] { Loc.L("BIENVENIDA AL CENTRO ESPACIAL", "WELCOME TO THE SPACE CENTER"),
                Loc.L("Este es el lugar donde una misión pasa del papel al cielo. Recorre los tres edificios en el orden real del trabajo:\n\n" +
                      "1. <b>Centro de Control</b> (aquí): el equipo diseña la misión y la sigue en vivo.\n" +
                      "2. <b>Edificio de Ensamblaje</b> (160 m al sur por la carretera de servicio): el cohete se apila y la cápsula se prepara en la sala limpia.\n" +
                      "3. <b>Plataforma de lanzamiento</b> (por el camino de orugas): desde aquí parte la misión.\n\n" +
                      "Cuando estés listo, siéntate en la <b>consola de diseño</b> de la sala de control.",
                      "This is where a mission goes from paper to the sky. Visit the three buildings in the real order of the work:\n\n" +
                      "1. <b>Control Center</b> (here): the team designs the mission and follows it live.\n" +
                      "2. <b>Assembly Building</b> (160 m south along the service road): the rocket is stacked and the capsule is prepared in the clean room.\n" +
                      "3. <b>Launch pad</b> (along the crawlerway): the mission departs from here.\n\n" +
                      "When you are ready, sit at the <b>design console</b> in the control room.") },
            new[] { Loc.L("CONTROLES", "CONTROLS"), Controls },
        };

        public static string[][] CleanRoom => new[]
        {
            new[] { Loc.L("SALA LIMPIA", "CLEAN ROOM"),
                Loc.L("Las naves se integran en salas limpias: el aire pasa por filtros HEPA y se cuentan las partículas por metro cúbico " +
                      "(norma ISO 14644; para naves suelen usarse clases ISO 7–8). El personal usa trajes, guantes y cofias.\n\n" +
                      "¿Por qué? Una mota de polvo puede rayar una lente, ensuciar un sensor o contaminar una muestra científica. " +
                      "En misiones que buscan vida o estudian otros mundos, además se controla la contaminación biológica (protección planetaria).",
                      "Spacecraft are integrated in clean rooms: the air goes through HEPA filters and particles are counted per cubic meter " +
                      "(ISO 14644 standard; spacecraft usually use ISO classes 7–8). Staff wear suits, gloves and hoods.\n\n" +
                      "Why? A speck of dust can scratch a lens, foul a sensor or contaminate a science sample. " +
                      "On missions that search for life or study other worlds, biological contamination is also controlled (planetary protection).") },
            new[] { Loc.L("LA CÁPSULA ORION", "THE ORION CAPSULE"),
                Loc.L("Orion es la nave tripulada del programa Artemis: un módulo de tripulación para 4 astronautas con soporte vital, " +
                      "escudo térmico para volver a ~11 km/s y un <b>módulo de servicio europeo (ESA)</b> que aporta propulsión, agua, oxígeno y energía solar.\n\n" +
                      "Tripulación de Artemis II: Reid Wiseman (comandante), Victor Glover (piloto), Christina Koch y Jeremy Hansen (Agencia Espacial Canadiense).\n\n" +
                      "En el juego, solo un bus <b>con soporte vital</b> puede llevar tripulación: el validador lo exige.",
                      "Orion is the crewed spacecraft of the Artemis program: a crew module for 4 astronauts with life support, " +
                      "a heat shield to come home at ~11 km/s and a <b>European Service Module (ESA)</b> that provides propulsion, water, oxygen and solar power.\n\n" +
                      "Artemis II crew: Reid Wiseman (commander), Victor Glover (pilot), Christina Koch and Jeremy Hansen (Canadian Space Agency).\n\n" +
                      "In the game, only a bus <b>with life support</b> can carry a crew: the validator requires it.") },
        };

        public static string[][] Stacking
        {
            get
            {
                var sb = new StringBuilder();
                foreach (var (time, text) in MissionSimulator.SlsAscent)
                    sb.Append("<b>").Append(time).Append("</b> · ").Append(text).Append('\n');
                return new[]
                {
                    new[] { Loc.L("APILADO DEL COHETE SLS", "STACKING THE SLS ROCKET"),
                        Loc.L("El cohete se arma en vertical sobre el <b>lanzador móvil</b>, de abajo arriba:\n\n" +
                              "1. Segmentos de los dos propulsores sólidos.\n" +
                              "2. Etapa central (tanques de hidrógeno y oxígeno líquidos, 4 motores RS-25), entre los propulsores.\n" +
                              "3. Adaptador y etapa superior ICPS, que hará la inyección translunar (TLI).\n" +
                              "4. Orion con su torre de escape.\n\n" +
                              "Luego el transportador de orugas lleva todo, a menos de 1,5 km/h, hasta la plataforma.",
                              "The rocket is assembled vertically on the <b>mobile launcher</b>, from the bottom up:\n\n" +
                              "1. The segments of the two solid rocket boosters.\n" +
                              "2. The core stage (liquid hydrogen and oxygen tanks, 4 RS-25 engines), between the boosters.\n" +
                              "3. The adapter and the ICPS upper stage, which will perform the trans-lunar injection (TLI).\n" +
                              "4. Orion with its launch abort tower.\n\n" +
                              "Then the crawler-transporter carries everything, at under 1.5 km/h, to the pad.") },
                    new[] { Loc.L("CRONOLOGÍA DEL ASCENSO (ARTEMIS II)", "ASCENT TIMELINE (ARTEMIS II)"), sb.ToString() },
                };
            }
        }

        public static string[][] LaunchPad => new[]
        {
            new[] { Loc.L("PLATAFORMA DE LANZAMIENTO", "LAUNCH PAD"),
                Loc.L("La torre de servicio da acceso a la tripulación por el brazo y la <b>sala blanca</b>. Los mástiles pararrayos protegen el cohete " +
                      "y el tanque de agua alimenta el sistema que inunda la plataforma durante el despegue para amortiguar el sonido y el calor. " +
                      "El foso de llamas desvía el chorro de los motores.",
                      "The service tower gives the crew access through the arm and the <b>white room</b>. The lightning masts protect the rocket " +
                      "and the water tank feeds the system that floods the pad during liftoff to damp the sound and heat. " +
                      "The flame trench diverts the engine exhaust.") },
            new[] { Loc.L("TRAYECTORIA DE RETORNO LIBRE", "FREE-RETURN TRAJECTORY"),
                Loc.L("Artemis II no orbita la Luna: usa una trayectoria de <b>retorno libre</b>, la misma idea de seguridad del Apolo 13.\n\n" +
                      "1. Órbita de estacionamiento y órbita alta de comprobación (~24 h).\n" +
                      "2. <b>TLI</b>: la etapa superior acelera la nave hacia la Luna.\n" +
                      "3. Correcciones de rumbo pequeñas.\n" +
                      "4. <b>Sobrevuelo</b> por la cara oculta a ~7 400 km de altura.\n" +
                      "5. La gravedad lunar curva la trayectoria y la devuelve a la Tierra <b>sin encender el motor principal</b>.\n" +
                      "6. Reentrada y amerizaje en el Pacífico el 10 de abril, ~9 días después del despegue.\n\n" +
                      "En el juego la trayectoria se calcula con un modelo de 3 cuerpos Tierra-Luna (órbita “Sobrevuelo lunar con retorno libre”) " +
                      "y dibuja el mismo \"8\" que el diagrama de la ESA. Se validó con <b>NASA GMAT</b>: con efemérides reales, una corrección de rumbo " +
                      "de solo ~27 m/s tras la TLI reproduce el sobrevuelo (7 672 km) y el perigeo de regreso de 50 km.",
                      "Artemis II does not orbit the Moon: it uses a <b>free-return</b> trajectory, the same safety idea as Apollo 13.\n\n" +
                      "1. Parking orbit and a high checkout orbit (~24 h).\n" +
                      "2. <b>TLI</b>: the upper stage accelerates the spacecraft toward the Moon.\n" +
                      "3. Small trajectory corrections.\n" +
                      "4. <b>Flyby</b> over the far side at ~7,400 km altitude.\n" +
                      "5. Lunar gravity bends the path and sends it back to Earth <b>without firing the main engine</b>.\n" +
                      "6. Reentry and splashdown in the Pacific on April 10, ~9 days after liftoff.\n\n" +
                      "In the game the trajectory is computed with an Earth-Moon 3-body model (orbit \"Free-return lunar flyby\") " +
                      "and draws the same \"8\" as ESA's diagram. It was validated with <b>NASA GMAT</b>: with real ephemerides, a trajectory correction " +
                      "of only ~27 m/s after TLI reproduces the flyby (7,672 km) and the 50 km return perigee.") },
            new[] { Loc.L("LAS FOTOS DE ARTEMIS II", "THE ARTEMIS II PHOTOS"),
                Loc.L("Durante el sobrevuelo se tomaron unas <b>10 000 fotos</b>. Orion lleva <b>32 cámaras</b> (muchas externas) y la tripulación usó " +
                      "cámaras Nikon D5 y Z9 con objetivos de 80–400 mm y 35 mm.\n\n" +
                      "Parte de las imágenes se envió casi en tiempo real por un <b>enlace láser</b> (O2O). El resto quedó en tarjetas SD a bordo y " +
                      "se recuperó tras el amerizaje, cuando la NASA accedió al archivo completo.\n\n" +
                      "Lección de diseño: si el enlace no alcanza, el almacenamiento recuperable salva la ciencia — en el juego se modela en la fase REGRESO.",
                      "About <b>10,000 photos</b> were taken during the flyby. Orion carries <b>32 cameras</b> (many external) and the crew used " +
                      "Nikon D5 and Z9 cameras with 80–400 mm and 35 mm lenses.\n\n" +
                      "Some images were sent in near real time over a <b>laser link</b> (O2O). The rest stayed on SD cards on board and " +
                      "were recovered after splashdown, when NASA accessed the full archive.\n\n" +
                      "Design lesson: if the link is not enough, recoverable storage saves the science — the game models this in the RETURN phase.") },
        };

        public static string[][] DesignConsole => new[]
        {
            new[] { Loc.L("CONSOLA DE DISEÑO DE MISIÓN", "MISSION DESIGN CONSOLE"),
                Loc.L("Aquí el equipo (1 a 4 jugadores) diseña la misión: objetivo, órbita, cohete, nave, instrumentos y comunicaciones, negociando los 6 recursos.\n\n" +
                      "Prueba el escenario <b>Sobrevuelo lunar tripulado (tipo Artemis II)</b> para volar la trayectoria de retorno libre.",
                      "Here the team (1 to 4 players) designs the mission: goal, orbit, rocket, spacecraft, instruments and comms, negotiating the 6 resources.\n\n" +
                      "Try the <b>Crewed lunar flyby (Artemis II style)</b> scenario to fly the free-return trajectory.") },
        };
    }
}
