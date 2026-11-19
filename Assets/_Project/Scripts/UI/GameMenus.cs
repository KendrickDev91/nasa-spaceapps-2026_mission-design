using MissionDesign.Core;
using MissionDesign.Exploration;
using MissionDesign.Multiplayer;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MissionDesign.UI
{
    /// <summary>
    /// Menú principal, pausa (Esc), tutorial y créditos/fuentes, más atajos de teclado:
    /// 1-4 rol activo · Tab siguiente turno · F1 ayuda · Esc pausa.
    /// </summary>
    public sealed class GameMenus : MonoBehaviour
    {
        [SerializeField] private RoleTurnManager roles;
        [SerializeField] private CanvasGroup mainMenu;
        [SerializeField] private CanvasGroup pauseMenu;
        [SerializeField] private CanvasGroup infoPanel;
        [SerializeField] private TMP_Text infoTitle;
        [SerializeField] private TMP_Text infoBody;
        [SerializeField] private TMP_Text infoPager;
        [SerializeField] private Button playButton, tutorialButton, creditsButton, quitButton;
        [SerializeField] private Button resumeButton, pauseTutorialButton, restartButton, mainMenuButton, pauseQuitButton;
        [SerializeField] private Button infoPrevButton, infoNextButton, infoCloseButton;
        [SerializeField] private Button exploreButton, plansButton;
        [SerializeField] private Button optionsButton, pauseOptionsButton;
        [SerializeField] private TMP_Text infoCaption;
        [SerializeField] private RawImage infoImage;
        [SerializeField] private GameObject infoImageFrame;
        [SerializeField] private Texture2D[] planTextures;
        [SerializeField] private bool showMainMenuOnStart = true;

        private string[][] pages;
        private int page;
        private Texture2D[] pageImages;

        public bool IsBlocking => Visible(mainMenu) || Visible(pauseMenu) || Visible(infoPanel) || OptionsMenu.IsOpen;

        public static readonly string[][] TutorialPages =
        {
            new[] { "1 · TU MISIÓN",
                "Diseña una misión espacial completa y llévala a buen término. El <b>Director</b> elige el objetivo (escenario), la dificultad y la órbita.\n\n" +
                "Hay 4 roles: <b>Director de Misión</b>, <b>Ingeniero de Sistemas</b> (bus, energía, computadora), <b>Científico Principal</b> (instrumentos) e " +
                "<b>Ingeniero de Telecom y Lanzamiento</b> (cohete, antena y red de estaciones terrenas).\n\n" +
                "Jueguen de 1 a 4 personas en la misma pantalla: el botón <b>jugadores</b> reparte los roles y <b>Siguiente turno</b> (o Tab) pasa el control." },
            new[] { "2 · LOS 6 RECURSOS",
                "A la derecha verás <b>Masa, Energía, Presupuesto, Datos, Comunicaciones y Tiempo</b>. Cada pieza cambia varios a la vez: " +
                "un instrumento potente da más ciencia, pero pesa, consume, cuesta y genera datos que hay que poder bajar a tierra.\n\n" +
                "Verde = bien · ámbar = sin margen · rojo = problema. <b>Pasa el ratón</b> por una barra para entender qué mide." },
            new[] { "3 · ELEGIR PIEZAS",
                "El catálogo de la izquierda cambia según el rol activo (teclas <b>1-4</b>). Pasa el ratón por cualquier opción: el tooltip te dice qué es, " +
                "de dónde salen sus números y <b>qué le pasaría a tu misión si la eliges</b> (\"Provocaría: Masa excede la capacidad del lanzador…\").\n\n" +
                "En <b>Avanzado</b> también eliges computadora de a bordo y control de orientación." },
            new[] { "4 · NEGOCIAR Y LANZAR",
                "El panel inferior lista los conflictos y el rol que debe resolverlos. Cuando no quede nada en rojo, cada jugador pulsa " +
                "<b>Aprobar diseño</b>. Cualquier cambio borra las aprobaciones: el equipo debe ponerse de acuerdo.\n\n" +
                "Con consenso y sin bloqueos, pulsa <b>LANZAR MISIÓN</b>." },
            new[] { "5 · SIMULACIÓN Y ANÁLISIS",
                "Verás el lanzamiento, la trayectoria y la sala de control. Ocurrirán 1 o 2 eventos reales (tormentas solares, micrometeoroides, cortes de la DSN…).\n\n" +
                "Al final, el <b>análisis post-misión</b> explica qué salió bien, qué falló y por qué, con un rango A-F, la misión real de la NASA más parecida y el ranking. " +
                "El reporte se guarda en JSON y TXT." },
            new[] { "6 · CONSEJOS DE INGENIERÍA",
                "• Deja ~10% de margen en masa y presupuesto: los proyectos reales siempre crecen.\n" +
                "• Más lejos = señal más débil (1/r²): la Luna necesita antenas grandes y la DSN.\n" +
                "• La batería debe aguantar el eclipse y el modo seguro.\n" +
                "• Guarda varias alternativas en <b>Diseños</b> y compáralas: casi nunca hay una única solución perfecta.\n" +
                "• Prueba el escenario <b>Sandbox</b> para experimentar sin límites." }
        };

        public static readonly string[][] CreditsPages =
        {
            new[] { "EQUIPO " + GameInfo.Team,
                "<size=120%><b>" + GameInfo.Title + "</b></size>\n" + GameInfo.Event + "\n\n" +
                "Desarrollado por el equipo <b>" + GameInfo.Team + "</b>:\n\n" +
                "• " + string.Join("\n• ", GameInfo.TeamMembers) + "\n\n" +
                "<size=85%>Gracias por jugar. Cada misión que diseñan es un trade-off: no hay una única respuesta correcta.</size>" },
            new[] { "FUENTES Y CRÉDITOS",
                GameInfo.Title + " — " + GameInfo.Event + ". Proyecto independiente: <b>no es un producto oficial de la NASA</b>.\n\n" +
                "<b>Recursos del reto:</b> Design and Develop Science Missions (NASA Small Spacecraft Systems Virtual Institute), " +
                "Concept Maturity Levels (JPL), CubeSat 101 (NASA CubeSat Launch Initiative), Mission Design Lab (NASA Goddard), GMAT y NASA Mission Viz.\n\n" +
                "<b>Datos:</b> cada pieza indica su fuente y si es dato oficial, aproximado o estimación de juego (pasa el ratón por ella).\n\n" +
                "<b>Arte:</b> modelos 3D propios generados con Blender (ArtSource/Blender) y texturas procedurales propias." },
            new[] { "LUNA, TRAYECTORIA Y ARTEMIS II",
                "<b>Luna:</b> color y relieve reales del Lunar Reconnaissance Orbiter (LROC y LOLA), del <i>CGI Moon Kit</i> de " +
                "NASA's Scientific Visualization Studio (Ernie Wright, NASA/GSFC). Dominio público.\n\n" +
                "<b>Trayectoria:</b> retorno libre calculado con el problema restringido de tres cuerpos y validado con " +
                "<b>NASA GMAT R2026a</b> (docs/validacion/gmat). Esquema de 8 hitos basado en el diagrama de Artemis II de la ESA.\n\n" +
                "<b>Artemis II:</b> despegue el 1 de abril de 2026, sobrevuelo lunar el 6 de abril y amerizaje en el Pacífico " +
                "frente a San Diego el 10 de abril. La cinemática del amerizaje es una recreación propia, no metraje real." }
        };

        private void Start()
        {
            Hook(playButton, CloseAll);
            Hook(tutorialButton, () => OpenInfo(TutorialPages));
            Hook(creditsButton, () => OpenInfo(CreditsPages));
            Hook(quitButton, Quit);
            Hook(exploreButton, SceneFlow.OpenSpaceCenter);
            Hook(plansButton, OpenPlans);
            Hook(optionsButton, OptionsMenu.Open);
            Hook(pauseOptionsButton, OptionsMenu.Open);
            Hook(resumeButton, Resume);
            Hook(pauseTutorialButton, () => OpenInfo(TutorialPages));
            Hook(restartButton, Restart);
            Hook(mainMenuButton, () => { Show(pauseMenu, false); Show(mainMenu, true); });
            Hook(pauseQuitButton, Quit);
            Hook(infoPrevButton, () => Turn(-1));
            Hook(infoNextButton, () => Turn(1));
            Hook(infoCloseButton, () => Show(infoPanel, false));

            Show(pauseMenu, false);
            Show(infoPanel, false);
            Show(mainMenu, showMainMenuOnStart && !SceneFlow.ConsumeSkipMainMenu());
            UpdateTimeScale();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (OptionsMenu.IsOpen) OptionsMenu.Close();
                else if (Visible(infoPanel)) Show(infoPanel, false);
                else if (Visible(pauseMenu)) Resume();
                else if (!Visible(mainMenu)) Show(pauseMenu, true);
            }
            if (kb.f1Key.wasPressedThisFrame) OpenInfo(TutorialPages);
            if (IsBlocking || roles == null) { UpdateTimeScale(); return; }
            if (kb.tabKey.wasPressedThisFrame) roles.NextTurn();
            if (kb.digit1Key.wasPressedThisFrame) roles.SetActiveRole(MissionRole.MissionDirector);
            if (kb.digit2Key.wasPressedThisFrame) roles.SetActiveRole(MissionRole.SystemsEngineer);
            if (kb.digit3Key.wasPressedThisFrame) roles.SetActiveRole(MissionRole.PrincipalScientist);
            if (kb.digit4Key.wasPressedThisFrame) roles.SetActiveRole(MissionRole.TelecomLaunchEngineer);
            UpdateTimeScale();
        }

        public void ShowMainMenu() => Show(mainMenu, true);
        public void OpenTutorial() => OpenInfo(TutorialPages);

        public void CloseAll()
        {
            Show(mainMenu, false);
            Show(pauseMenu, false);
            Show(infoPanel, false);
            UpdateTimeScale();
        }

        /// <summary>Láminas 2D del centro espacial (las mismas que se consultan caminando, tecla M).</summary>
        public void OpenPlans()
        {
            int n = SpaceCenterContent.Plans.Length;
            var p = new string[n][];
            for (int i = 0; i < n; i++) p[i] = new[] { SpaceCenterContent.Plans[i].title, SpaceCenterContent.Plans[i].caption };
            OpenInfo(p, planTextures);
        }

        public void OpenInfo(string[][] content) => OpenInfo(content, null);

        private void OpenInfo(string[][] content, Texture2D[] images)
        {
            pages = content;
            pageImages = images;
            page = 0;
            Show(infoPanel, true);
            Render();
        }

        private void Turn(int delta)
        {
            if (pages == null) return;
            page = Mathf.Clamp(page + delta, 0, pages.Length - 1);
            Render();
        }

        private void Render()
        {
            if (pages == null || pages.Length == 0) return;
            var image = pageImages != null && page < pageImages.Length ? pageImages[page] : null;
            bool hasImage = image != null;
            if (infoTitle != null) infoTitle.text = pages[page][0];
            if (infoBody != null) { infoBody.text = hasImage ? string.Empty : pages[page][1]; infoBody.gameObject.SetActive(!hasImage); }
            if (infoCaption != null) { infoCaption.text = hasImage ? pages[page][1] : string.Empty; infoCaption.gameObject.SetActive(hasImage); }
            if (infoImageFrame != null) infoImageFrame.SetActive(hasImage);
            if (infoImage != null)
            {
                infoImage.texture = image;
                var fitter = infoImage.GetComponent<AspectRatioFitter>();
                if (fitter != null && hasImage) fitter.aspectRatio = (float)image.width / image.height;
            }
            if (infoPager != null) infoPager.text = pages.Length > 1 ? $"{page + 1} / {pages.Length}" : string.Empty;
            if (infoPrevButton != null) infoPrevButton.interactable = page > 0;
            if (infoNextButton != null) infoNextButton.interactable = page < pages.Length - 1;
        }

        private void Resume()
        {
            Show(pauseMenu, false);
            UpdateTimeScale();
        }

        private void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>La pausa congela la simulación; el menú principal y el tutorial no.</summary>
        private void UpdateTimeScale() => Time.timeScale = Visible(pauseMenu) ? 0f : 1f;

        private static bool Visible(CanvasGroup g) => g != null && g.alpha > 0.5f;

        private static void Show(CanvasGroup g, bool visible)
        {
            if (g == null) return;
            g.alpha = visible ? 1f : 0f;
            g.interactable = visible;
            g.blocksRaycasts = visible;
            if (visible) g.transform.SetAsLastSibling();
        }

        private static void Hook(Button b, UnityEngine.Events.UnityAction action)
        {
            if (b != null) b.onClick.AddListener(action);
        }
    }
}
