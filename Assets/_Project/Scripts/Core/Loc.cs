using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MissionDesign.Core
{
    public enum GameLanguage
    {
        Spanish,
        English
    }

    /// <summary>
    /// Idioma del juego. El texto de código se escribe en pares: <c>Loc.L("Reanudar", "Resume")</c>, así cada traducción vive
    /// junto a su original. Las etiquetas fijas que el constructor de escenas deja en las escenas se traducen al cargar
    /// con <see cref="StaticLabels"/>. Cambiar de idioma recarga la escena actual.
    /// </summary>
    public static class Loc
    {
        private const string PrefKey = "settings.language";

        public static GameLanguage Current { get; private set; } = GameLanguage.Spanish;
        public static bool English => Current == GameLanguage.English;

        public static event Action Changed;

        /// <summary>Devuelve el texto en el idioma actual.</summary>
        public static string L(string es, string en) => English && !string.IsNullOrEmpty(en) ? en : es;

        /// <summary>Etiqueta de objetivo científico (clima, luna, sol…) en el idioma actual.</summary>
        public static string Tag(string tag)
        {
            if (!English || string.IsNullOrEmpty(tag)) return tag;
            switch (tag)
            {
                case "clima": return "climate";
                case "meteorologia": return "weather";
                case "tormentas": return "storms";
                case "luna": return "Moon";
                case "sol": return "Sun";
                case "clima_espacial": return "space weather";
                case "tierra": return "Earth";
                case "agricultura": return "agriculture";
                case "bosques": return "forests";
                case "carbono": return "carbon";
                case "oceano": return "ocean";
                case "incendios": return "wildfires";
                case "terremotos": return "earthquakes";
                case "hielo": return "ice";
                case "atmosfera": return "atmosphere";
                case "superficie": return "surface";
                case "radiacion": return "radiation";
                case "fotografia": return "photography";
                case "divulgacion": return "outreach";
                case "tripulada": return "crewed";
                default: return tag;
            }
        }

        public static string LanguageName(GameLanguage l) => l == GameLanguage.English ? "English" : "Español";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            // Primera vez: el idioma del sistema (español si Windows está en español; si no, inglés).
            int saved = PlayerPrefs.GetInt(PrefKey, -1);
            Current = saved >= 0 ? (GameLanguage)saved
                : Application.systemLanguage == SystemLanguage.Spanish ? GameLanguage.Spanish : GameLanguage.English;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        /// <summary>Cambia el idioma, lo guarda y recarga la escena para que todo se vuelva a escribir.</summary>
        public static void SetLanguage(GameLanguage language, bool reloadScene = true)
        {
            if (language == Current) return;
            Current = language;
            PlayerPrefs.SetInt(PrefKey, (int)language);
            PlayerPrefs.Save();
            Changed?.Invoke();
            if (reloadScene && Application.isPlaying)
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }

        /// <summary>Solo para pruebas: cambia el idioma sin guardar ni recargar.</summary>
        public static void SetForTests(GameLanguage language)
        {
            Current = language;
            Changed?.Invoke();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TranslateStaticLabels(scene);

        /// <summary>Traduce las etiquetas fijas de la escena (las que escribió el constructor de escenas).</summary>
        public static void TranslateStaticLabels(Scene scene)
        {
            if (!English) return;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
                if (t.text != null && StaticLabels.TryGetValue(t.text, out var en))
                    t.text = en;
            // Los campos de texto (TMP_InputField) muestran su marcador aparte.
        }

        /// <summary>Traduce un texto fijo si está en <see cref="StaticLabels"/> (p. ej. tooltips guardados en la escena).</summary>
        public static string Static(string es) => English && es != null && StaticLabels.TryGetValue(es, out var en) ? en : es;

        /// <summary>Etiquetas fijas de las escenas (texto exacto en español → inglés).</summary>
        public static readonly Dictionary<string, string> StaticLabels = new Dictionary<string, string>
        {
            // Menú principal y pausa
            { "<b>JUGAR · DISEÑAR MISIÓN</b>", "<b>PLAY · DESIGN A MISSION</b>" },
            { "<b>Explorar el centro espacial</b>", "<b>Explore the space center</b>" },
            { "Planos del centro espacial", "Space center blueprints" },
            { "Cómo se juega", "How to play" },
            { "Opciones", "Options" },
            { "Fuentes y créditos", "Sources & credits" },
            { "Salir", "Quit" },
            { "<b>PAUSA</b>", "<b>PAUSED</b>" },
            { "<b>Reanudar</b>", "<b>Resume</b>" },
            { "Reiniciar misión", "Restart mission" },
            { "Menú principal", "Main menu" },
            { "« Anterior", "« Previous" },
            { "Siguiente »", "Next »" },
            { "Cerrar", "Close" },
            { "<b>Cerrar</b>", "<b>Close</b>" },
            { "Cerrar (Esc)", "Close (Esc)" },
            { "Abrir carpeta de fotos", "Open photo folder" },
            { GameInfo.TaglineEs, GameInfo.TaglineEn },
            { GameInfo.RolesLineEs, GameInfo.RolesLineEn },
            { GameInfo.FooterEs, GameInfo.FooterEn },
            { "<b>" + GameInfo.TeamLineEs + "</b>", "<b>" + GameInfo.TeamLineEn + "</b>" },
            { GameInfo.TopBarEs, GameInfo.TopBarEn },

            // Barra superior, paneles y diseño
            { "Siguiente turno »", "Next turn »" },
            { "Turno:", "Turn:" },
            { "4 jugadores", "4 players" },
            { "Diseños", "Designs" },
            { "TELEMETRÍA DE MISIÓN", "MISSION TELEMETRY" },
            { "VALOR CIENTÍFICO", "SCIENCE VALUE" },
            { "CONSENSO DEL EQUIPO", "TEAM CONSENSUS" },
            { "Aprobar diseño", "Approve design" },
            { "<b>LANZAR MISIÓN</b>", "<b>LAUNCH MISSION</b>" },
            { "SIMULACIÓN DE MISIÓN", "MISSION SIMULATION" },
            { "Saltar al resultado »", "Skip to result »" },
            { "LANZAMIENTO", "LAUNCH" },
            { "ANÁLISIS POST-MISIÓN", "POST-MISSION ANALYSIS" },
            { "<b>Volver al diseño</b>", "<b>Back to design</b>" },
            { "Abrir reportes", "Open reports" },
            { "Informe de la exploración", "Exploration report" },
            { "INFORME DE LA EXPLORACIÓN", "EXPLORATION REPORT" },
            { "Estadísticas", "Statistics" },
            { "Equipo", "Team" },
            { "Diseño", "Design" },
            { "Centro espacial", "Space center" },
            { "ÉXITO", "SUCCESS" },
            { "PUNTUACIÓN", "SCORE" },
            { "ESTADO", "STATUS" },
            { "ROL", "ROLE" },
            { "SECCIÓN", "SECTION" },
            { "DISEÑOS: GUARDAR, CARGAR Y COMPARAR", "DESIGNS: SAVE, LOAD AND COMPARE" },
            { "Guarda alternativas para el mismo objetivo y compáralas: casi nunca hay una única solución perfecta.",
              "Save alternatives for the same goal and compare them: there is almost never a single perfect solution." },
            { "<b>Guardar diseño actual</b>", "<b>Save current design</b>" },
            { "Nombre del diseño", "Design name" },
            { "Nombre", "Name" },
            { "GUARDADOS", "SAVED" },
            { "Cargar", "Load" },
            { "Comparar", "Compare" },
            { "Nombre del equipo", "Team name" },
            { "p. ej. Opción barata LEO", "e.g. Cheap LEO option" },
            { "LO QUE SALIÓ BIEN", "WHAT WENT WELL" },
            { "LO QUE FALLÓ", "WHAT WENT WRONG" },
            { "Ciencia", "Science" },
            { "Datos recibidos", "Data received" },
            { "Energía disponible", "Power available" },
            { "Presupuesto usado", "Budget used" },
            { "Trabajo en equipo", "Teamwork" },
            { "Director de Misión", "Mission Director" },
            { "Ing. de Sistemas", "Systems Engineer" },
            { "Científico Principal", "Principal Scientist" },
            { "Ing. Telecom y Lanzamiento", "Telecom & Launch Eng." },
            { "Director de Misión: en verde cuando aprueba el diseño.", "Mission Director: green when they approve the design." },
            { "Ing. de Sistemas: en verde cuando aprueba el diseño.", "Systems Engineer: green when they approve the design." },
            { "Científico Principal: en verde cuando aprueba el diseño.", "Principal Scientist: green when they approve the design." },
            { "Ing. Telecom y Lanzamiento: en verde cuando aprueba el diseño.", "Telecom & Launch Eng.: green when they approve the design." },

            // Relojes de la sala de control
            { "TIEMPO DE MISIÓN", "MISSION TIME" },
            { "CUENTA", "COUNTDOWN" },
            { "DISTANCIA", "DISTANCE" },
            { "RED DSN", "DSN NETWORK" },

            // Centro espacial
            { "<b>CENTRO ESPACIAL</b>", "<b>SPACE CENTER</b>" },
            { "<b>Continuar</b>", "<b>Continue</b>" },
            { "Viaje rápido", "Fast travel" },
            { "Planos (M)", "Blueprints (M)" },
            { "Controles", "Controls" },
            { "<b>Diseñar la misión</b>", "<b>Design the mission</b>" },
            { "<b>Diseñar la misión »</b>", "<b>Design the mission »</b>" },
            { "CENTRO ESPACIAL · " + GameInfo.Title, "SPACE CENTER · " + GameInfo.Title },
            { "Centro espacial · exteriores", "Space center · outdoors" },
            { "<b>DISEÑAR MISIÓN</b>\n<size=70%>pisa el círculo</size>", "<b>DESIGN MISSION</b>\n<size=70%>step into the circle</size>" },
            { "CENTRO DE CONTROL", "CONTROL CENTER" },
            { "EDIFICIO DE ENSAMBLAJE", "ASSEMBLY BUILDING" },
            { "PLATAFORMA DE LANZAMIENTO", "LAUNCH PAD" },
            { "SALA LIMPIA", "CLEAN ROOM" },
            { "Centro de Control · Vestíbulo", "Control Center · Lobby" },
            { "Centro de Control · Sala de control de misión", "Control Center · Mission control room" },
            { "Explanada de acceso", "Entrance plaza" },
            { "Carretera de servicio", "Service road" },
            { "Edificio de Ensamblaje · Sala limpia", "Assembly Building · Clean room" },
            { "Camino de orugas", "Crawlerway" },
            { "Plataforma · Brazo de acceso de la tripulación", "Launch pad · Crew access arm" },
            { "<b>Centro de Control</b>", "<b>Control Center</b>" },
            { "<b>Edificio de Ensamblaje</b>", "<b>Assembly Building</b>" },
            { "<b>Plataforma de lanzamiento</b>", "<b>Launch pad</b>" },
            { "<b>Camino de orugas</b>", "<b>Crawlerway</b>" },
            { "Interactuar", "Interact" },
            { "Ascensor", "Elevator" },
            { "Llamar al ascensor", "Call the elevator" },
            { "Sentarte en la consola de diseño de misión", "Sit at the mission design console" },
            { "Ver los planos del centro espacial", "View the space center blueprints" },
            { "Recepción: cómo funciona el centro espacial", "Reception: how the space center works" },
            { "Leer sobre la sala limpia y la cápsula Orion", "Read about the clean room and the Orion capsule" },
            { "Ver cómo se apila el cohete SLS", "See how the SLS rocket is stacked" },
            { "Consultar la trayectoria y las fotos de Artemis II", "Check the Artemis II trajectory and photos" },
            { "Brazo de acceso de la tripulación: cómo entran los astronautas", "Crew access arm: how the astronauts board" },
            { "Entrada del Centro de Control", "Control Center entrance" },
            { "Sala de control de misión", "Mission control room" },
            { "Edificio de Ensamblaje", "Assembly Building" },
            { "Plataforma de lanzamiento", "Launch pad" },
            { "WASD caminar · Shift correr · Espacio saltar · Clic derecho + ratón cámara · Rueda zoom · V 1.ª/3.ª persona · E interactuar · M planos · N mapa · Esc menú",
              "WASD walk · Shift run · Space jump · Right-click + mouse camera · Wheel zoom · V 1st/3rd person · E interact · M blueprints · N map · Esc menu" },
        };
    }
}
