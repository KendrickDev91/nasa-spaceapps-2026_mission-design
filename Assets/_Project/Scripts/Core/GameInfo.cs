namespace MissionDesign.Core
{
    /// <summary>
    /// Identidad del juego en un único lugar. El título es provisional: evita nombres de
    /// instalaciones reales de la NASA (p. ej. el "Mission Design Lab" de Goddard) y no uses sus logos.
    /// </summary>
    public static class GameInfo
    {
        public const string Title = "MISSION TRADE-OFF";

        /// <summary>Título grande de la pantalla inicial (el nombre del equipo); el resto del juego sigue usando <see cref="Title"/>.</summary>
        public const string StartScreenTitle = "AstroByte";
        public const string Tagline = "Diseña, negocia y vuela tu misión espacial";
        public const string Event = "Proyecto para NASA Space Apps Challenge 2026";

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

        /// <summary>Línea corta para el menú principal.</summary>
        public static string TeamLine => $"Equipo {Team} · NASA Space Apps Challenge 2026";
    }
}
