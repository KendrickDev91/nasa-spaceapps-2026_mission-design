using System.Text;
using MissionDesign.Simulation;

namespace MissionDesign.Exploration
{
    /// <summary>
    /// Textos educativos del centro espacial (un solo lugar para editarlos). Cada entrada es {título, cuerpo}.
    /// Los datos de Artemis II provienen de la información pública de la NASA sobre la misión.
    /// </summary>
    public static class SpaceCenterContent
    {
        public sealed class PlanSheet
        {
            public readonly string file, title, caption;
            public PlanSheet(string file, string title, string caption) { this.file = file; this.title = title; this.caption = caption; }
        }

        public static readonly PlanSheet[] Plans =
        {
            new PlanSheet("Lamina_01_Plano_de_conjunto", "LÁMINA 1 · PLANO DE CONJUNTO",
                "Recorrido: Centro de Control → carretera de servicio → Edificio de Ensamblaje → camino de orugas → plataforma de lanzamiento."),
            new PlanSheet("Lamina_02_Centro_de_Control", "LÁMINA 2 · CENTRO DE CONTROL DE MISIÓN",
                "Planta con corte a 3,5 m: vestíbulo con mesa de planos y recepción; sala de control con 3 gradas de consolas frente a las pantallas."),
            new PlanSheet("Lamina_03_Ensamblaje_y_sala_limpia", "LÁMINA 3 · EDIFICIO DE ENSAMBLAJE Y SALA LIMPIA",
                "Planta con corte a 12 m: el cohete se apila sobre el lanzador móvil; la cápsula se prepara en la sala limpia (aire filtrado, trajes y control de partículas)."),
            new PlanSheet("Lamina_04_Plataforma_de_lanzamiento", "LÁMINA 4 · PLATAFORMA DE LANZAMIENTO",
                "Losa de 90 m, montaje del cohete, torre de servicio de 110 m con brazo de acceso de la tripulación, foso de llamas y rampa del camino de orugas."),
            new PlanSheet("Lamina_05_Sitio_lunar", "LÁMINA 5 · SITIO LUNAR (MODELADO)",
                "Relieve del regolito con los cráteres principales y el módulo de aterrizaje. Escenario de visualización, no caminable."),
        };

        public const string Controls =
            "<b>WASD</b> / flechas: caminar · <b>Shift</b>: correr · <b>Espacio</b>: saltar\n" +
            "<b>Clic derecho + ratón</b>: girar la cámara · <b>Rueda</b>: acercar/alejar\n" +
            "<b>E</b>: interactuar · <b>M</b>: planos · <b>N</b>: mapa · <b>V</b>: cámara en 1.ª / 3.ª persona\n" +
            "<b>Esc</b>: menú (viaje rápido entre edificios)";

        public static readonly string[][] CrewArm =
        {
            new[] { "BRAZO DE ACCESO DE LA TRIPULACIÓN",
                "Desde este brazo, a la altura de la cápsula, los astronautas cruzan a la <b>sala blanca</b> y entran en Orion " +
                "unas horas antes del despegue. Técnicos con trajes limpios los ayudan a sentarse y cierran la escotilla.\n\n" +
                "Antes del lanzamiento el brazo se retrae. Si hay una emergencia en la plataforma, la tripulación evacúa por un " +
                "sistema de cestas que bajan deslizándose por cables hasta un búnker.\n\n" +
                "<size=85%>En el juego el brazo está a 75 m; en la plataforma real de Artemis está aún más alto.</size>" },
        };

        public static readonly string[][] Reception =
        {
            new[] { "BIENVENIDA AL CENTRO ESPACIAL",
                "Este es el lugar donde una misión pasa del papel al cielo. Recorre los tres edificios en el orden real del trabajo:\n\n" +
                "1. <b>Centro de Control</b> (aquí): el equipo diseña la misión y la sigue en vivo.\n" +
                "2. <b>Edificio de Ensamblaje</b> (160 m al sur por la carretera de servicio): el cohete se apila y la cápsula se prepara en la sala limpia.\n" +
                "3. <b>Plataforma de lanzamiento</b> (por el camino de orugas): desde aquí parte la misión.\n\n" +
                "Cuando estés listo, siéntate en la <b>consola de diseño</b> de la sala de control." },
            new[] { "CONTROLES", Controls },
        };

        public static readonly string[][] CleanRoom =
        {
            new[] { "SALA LIMPIA",
                "Las naves se integran en salas limpias: el aire pasa por filtros HEPA y se cuentan las partículas por metro cúbico " +
                "(norma ISO 14644; para naves suelen usarse clases ISO 7–8). El personal usa trajes, guantes y cofias.\n\n" +
                "¿Por qué? Una mota de polvo puede rayar una lente, ensuciar un sensor o contaminar una muestra científica. " +
                "En misiones que buscan vida o estudian otros mundos, además se controla la contaminación biológica (protección planetaria)." },
            new[] { "LA CÁPSULA ORION",
                "Orion es la nave tripulada del programa Artemis: un módulo de tripulación para 4 astronautas con soporte vital, " +
                "escudo térmico para volver a ~11 km/s y un <b>módulo de servicio europeo (ESA)</b> que aporta propulsión, agua, oxígeno y energía solar.\n\n" +
                "Tripulación de Artemis II: Reid Wiseman (comandante), Victor Glover (piloto), Christina Koch y Jeremy Hansen (Agencia Espacial Canadiense).\n\n" +
                "En el juego, solo un bus <b>con soporte vital</b> puede llevar tripulación: el validador lo exige." },
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
                    new[] { "APILADO DEL COHETE SLS",
                        "El cohete se arma en vertical sobre el <b>lanzador móvil</b>, de abajo arriba:\n\n" +
                        "1. Segmentos de los dos propulsores sólidos.\n" +
                        "2. Etapa central (tanques de hidrógeno y oxígeno líquidos, 4 motores RS-25), entre los propulsores.\n" +
                        "3. Adaptador y etapa superior ICPS, que hará la inyección translunar (TLI).\n" +
                        "4. Orion con su torre de escape.\n\n" +
                        "Luego el transportador de orugas lleva todo, a menos de 1,5 km/h, hasta la plataforma." },
                    new[] { "CRONOLOGÍA DEL ASCENSO (ARTEMIS II)", sb.ToString() },
                };
            }
        }

        public static readonly string[][] LaunchPad =
        {
            new[] { "PLATAFORMA DE LANZAMIENTO",
                "La torre de servicio da acceso a la tripulación por el brazo y la <b>sala blanca</b>. Los mástiles pararrayos protegen el cohete " +
                "y el tanque de agua alimenta el sistema que inunda la plataforma durante el despegue para amortiguar el sonido y el calor. " +
                "El foso de llamas desvía el chorro de los motores." },
            new[] { "TRAYECTORIA DE RETORNO LIBRE",
                "Artemis II no orbita la Luna: usa una trayectoria de <b>retorno libre</b>, la misma idea de seguridad del Apolo 13.\n\n" +
                "1. Órbita de estacionamiento y órbita alta de comprobación (~24 h).\n" +
                "2. <b>TLI</b>: la etapa superior acelera la nave hacia la Luna.\n" +
                "3. Correcciones de rumbo pequeñas.\n" +
                "4. <b>Sobrevuelo</b> por la cara oculta a ~7 400 km de altura.\n" +
                "5. La gravedad lunar curva la trayectoria y la devuelve a la Tierra <b>sin encender el motor principal</b>.\n" +
                "6. Reentrada y amerizaje tras ~10 días.\n\n" +
                "En el juego la trayectoria se calcula con un modelo de 3 cuerpos Tierra-Luna (órbita “Sobrevuelo lunar con retorno libre”) " +
                "y se validó con <b>NASA GMAT</b>: con efemérides reales, una corrección de rumbo de ~140 m/s tras la TLI reproduce el mismo " +
                "sobrevuelo (7 283 km) y el perigeo de regreso de 50 km." },
            new[] { "LAS FOTOS DE ARTEMIS II",
                "Durante el sobrevuelo se tomaron unas <b>10 000 fotos</b>. Orion lleva <b>32 cámaras</b> (muchas externas) y la tripulación usó " +
                "cámaras Nikon D5 y Z9 con objetivos de 80–400 mm y 35 mm.\n\n" +
                "Parte de las imágenes se envió casi en tiempo real por un <b>enlace láser</b> (O2O). El resto quedó en tarjetas SD a bordo y " +
                "se recuperó tras el amerizaje, cuando la NASA accedió al archivo completo.\n\n" +
                "Lección de diseño: si el enlace no alcanza, el almacenamiento recuperable salva la ciencia — en el juego se modela en la fase REGRESO." },
        };

        public static readonly string[][] DesignConsole =
        {
            new[] { "CONSOLA DE DISEÑO DE MISIÓN",
                "Aquí el equipo (1 a 4 jugadores) diseña la misión: objetivo, órbita, cohete, nave, instrumentos y comunicaciones, negociando los 6 recursos.\n\n" +
                "Prueba el escenario <b>Sobrevuelo lunar tripulado (tipo Artemis II)</b> para volar la trayectoria de retorno libre." },
        };
    }
}
