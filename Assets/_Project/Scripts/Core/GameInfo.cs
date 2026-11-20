namespace MissionDesign.Core
{
    /// <summary>
    /// Identidad del juego en un único lugar. El título es provisional: evita nombres de
    /// instalaciones reales de la NASA (p. ej. el "Mission Design Lab" de Goddard) y no uses sus logos.
    /// Los textos con versión en inglés tienen la pareja Es/En; el constructor de escenas escribe la Es
    /// y <see cref="Loc.StaticLabels"/> la cambia por la En al cargar la escena.
    /// </summary>
    public static class GameInfo
    {
        /// <summary>Título corto (barra superior, centro espacial).</summary>
        public const string Title = "ASTROBYTE";

        /// <summary>Nombre completo del juego (créditos, reportes, ventana, itch.io). Antes se llamaba «Mission Trade-Off».</summary>
        public const string FullTitle = "AstroByte: Space Mission Simulator";

        /// <summary>Título grande de la pantalla inicial.</summary>
        public const string StartScreenTitle = "AstroByte";

        public const string TaglineEs = "Diseña, negocia y vuela tu misión espacial";
        public const string TaglineEn = "Design, negotiate and fly your space mission";
        public static string Tagline => Loc.L(TaglineEs, TaglineEn);

        public const string EventEs = "Proyecto para NASA Space Apps Challenge 2026";
        public const string EventEn = "Project for the NASA Space Apps Challenge 2026";
        public static string Event => Loc.L(EventEs, EventEn);

        /// <summary>Equipo autor del proyecto (créditos, menú principal y README).</summary>
        public const string Team = "ASTROBYTE";

        /// <summary>Integrantes del equipo, en el orden indicado por el equipo.</summary>
        public static readonly string[] TeamMembers =
        {
            "Alejandro Marco Toranzo Cazorla",
            "Keni Breython Ramos Ramirez",
            "Jhamil Obando Requis",
            "Tony Cahuaya"
        };

        /// <summary>Autor del juego: diseño y desarrollo (programación, arte 3D, datos).</summary>
        public const string Developer = "Keni Breython Ramos Ramirez";
        public const string DeveloperHandle = "KendrickDev91";

        /// <summary>Resto del equipo del evento: aportaron el concepto y las ideas.</summary>
        public static readonly string[] IdeaContributors =
        {
            "Alejandro Marco Toranzo Cazorla",
            "Jhamil Obando Requis",
            "Tony Cahuaya"
        };

        /// <summary>Línea corta para el menú principal.</summary>
        public static string TeamLine => Loc.L(TeamLineEs, TeamLineEn);
        public const string TeamLineEs = "Equipo " + Team + " · NASA Space Apps Challenge 2026";
        public const string TeamLineEn = "Team " + Team + " · NASA Space Apps Challenge 2026";

        // Textos fijos del menú principal y la barra superior (el constructor de escenas los escribe tal cual).
        public const string RolesLineEs = "1 a 4 jugadores · Director de Misión · Ingeniero de Sistemas · Científico Principal · Ingeniero de Telecom y Lanzamiento";
        public const string RolesLineEn = "1 to 4 players · Mission Director · Systems Engineer · Principal Scientist · Telecom & Launch Engineer";
        public const string FooterEs = EventEs + " · Proyecto independiente, no es un producto oficial de la NASA · F1 ayuda · Esc pausa";
        public const string FooterEn = EventEn + " · Independent project, not an official NASA product · F1 help · Esc pause";
        public const string TopBarEs = "<b>" + Title + "</b>\n<size=60%><color=#9FB3D1>" + TaglineEs + "</color></size>";
        public const string TopBarEn = "<b>" + Title + "</b>\n<size=60%><color=#9FB3D1>" + TaglineEn + "</color></size>";
    }
}
