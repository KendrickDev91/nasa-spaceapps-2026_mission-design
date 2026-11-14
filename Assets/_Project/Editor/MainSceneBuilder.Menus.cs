using MissionDesign.Core;
using MissionDesign.Multiplayer;
using MissionDesign.Simulation;
using MissionDesign.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionDesign.Editor
{
    /// <summary>Menú principal, pausa, panel de tutorial/créditos y sonidos.</summary>
    public static partial class MainSceneBuilder
    {
        private static void BuildMenus(Transform root, RoleTurnManager roles, MissionSimulationRunner runner)
        {
            var container = NewRect("Menus", root);
            Stretch(container);
            var menus = container.gameObject.AddComponent<GameMenus>();

            // ── Menú principal ──
            var main = Panel("MainMenu", container, new Color(0.01f, 0.02f, 0.05f, 0.93f));
            Stretch(main);
            var mainGroup = main.gameObject.AddComponent<CanvasGroup>();
            var title = Text("Title", main, $"<b>{GameInfo.Title}</b>", 96, UiTheme.Text, TextAlignmentOptions.Center);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(1600f, 120f));
            var tagline = Text("Tagline", main, GameInfo.Tagline, 30, UiTheme.Accent, TextAlignmentOptions.Center);
            Place(tagline.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -295f), new Vector2(1600f, 44f));
            var roleLine = Text("Roles", main,
                "1 a 4 jugadores · Director de Misión · Ingeniero de Sistemas · Científico Principal · Ingeniero de Telecom y Lanzamiento",
                18, UiTheme.TextDim, TextAlignmentOptions.Center);
            Place(roleLine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -345f), new Vector2(1600f, 30f));

            var column = NewRect("Buttons", main);
            Place(column, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 60f), new Vector2(420f, 330f));
            var vlg = column.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 16; vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = true;
            var play = MakeButton("PlayButton", column, "<b>JUGAR</b>", UiTheme.Launch, 26, 0);
            var tutorial = MakeButton("TutorialButton", column, "Cómo se juega", UiTheme.AccentDim, 22, 0);
            var credits = MakeButton("CreditsButton", column, "Fuentes y créditos", UiTheme.AccentDim, 22, 0);
            var quit = MakeButton("QuitButton", column, "Salir", UiTheme.PanelLight, 22, 0);
            var footer = Text("Footer", main, $"{GameInfo.Event} · Proyecto independiente, no es un producto oficial de la NASA · F1 ayuda · Esc pausa",
                15, UiTheme.TextDim, TextAlignmentOptions.Center);
            Place(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1600f, 26f));

            // ── Pausa ──
            var pause = Panel("PauseMenu", container, new Color(0f, 0f, 0f, 0.72f));
            Stretch(pause);
            var pauseGroup = pause.gameObject.AddComponent<CanvasGroup>();
            var pauseCard = Panel("Card", pause, new Color(0.04f, 0.06f, 0.11f, 1f));
            Place(pauseCard, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(480f, 520f));
            var pauseTitle = Text("Title", pauseCard, "<b>PAUSA</b>", 36, UiTheme.Accent, TextAlignmentOptions.Center);
            Place(pauseTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(0f, 50f));
            var pauseCol = NewRect("Buttons", pauseCard);
            Place(pauseCol, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(-80f, -150f));
            var pv = pauseCol.gameObject.AddComponent<VerticalLayoutGroup>();
            pv.spacing = 12; pv.childControlWidth = pv.childControlHeight = true;
            pv.childForceExpandWidth = true; pv.childForceExpandHeight = true;
            var resume = MakeButton("ResumeButton", pauseCol, "<b>Reanudar</b>", UiTheme.Accent, 22, 0);
            var pauseTutorial = MakeButton("PauseTutorialButton", pauseCol, "Cómo se juega", UiTheme.AccentDim, 20, 0);
            var restart = MakeButton("RestartButton", pauseCol, "Reiniciar misión", UiTheme.AccentDim, 20, 0);
            var mainMenuBtn = MakeButton("MainMenuButton", pauseCol, "Menú principal", UiTheme.AccentDim, 20, 0);
            var pauseQuit = MakeButton("PauseQuitButton", pauseCol, "Salir", UiTheme.PanelLight, 20, 0);

            // ── Tutorial / créditos ──
            var info = Panel("InfoPanel", container, new Color(0f, 0f, 0f, 0.75f));
            Stretch(info);
            var infoGroup = info.gameObject.AddComponent<CanvasGroup>();
            var infoCard = Panel("Card", info, new Color(0.04f, 0.06f, 0.11f, 1f));
            Place(infoCard, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 680f));
            var infoTitle = Text("Title", infoCard, "TÍTULO", 34, UiTheme.Accent, TextAlignmentOptions.TopLeft, bold: true);
            Place(infoTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(-96f, 50f));
            var infoBody = Text("Body", infoCard, "", 22, UiTheme.Text, TextAlignmentOptions.TopLeft);
            infoBody.lineSpacing = 8;
            Place(infoBody.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -106f), new Vector2(-96f, -210f));
            var pager = Text("Pager", infoCard, "", 18, UiTheme.TextDim, TextAlignmentOptions.MidlineLeft);
            Place(pager.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(48f, 34f), new Vector2(200f, 50f));
            var prev = MakeButton("PrevButton", infoCard, "« Anterior", UiTheme.AccentDim, 18, 0);
            Place((RectTransform)prev.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-436f, 30f), new Vector2(170f, 54f));
            var next = MakeButton("NextButton", infoCard, "Siguiente »", UiTheme.Accent, 18, 0);
            Place((RectTransform)next.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-246f, 30f), new Vector2(170f, 54f));
            var close = MakeButton("CloseButton", infoCard, "Cerrar", UiTheme.PanelLight, 18, 0);
            Place((RectTransform)close.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-48f, 30f), new Vector2(180f, 54f));

            Wire(menus, "roles", roles);
            Wire(menus, "mainMenu", mainGroup);
            Wire(menus, "pauseMenu", pauseGroup);
            Wire(menus, "infoPanel", infoGroup);
            Wire(menus, "infoTitle", infoTitle);
            Wire(menus, "infoBody", infoBody);
            Wire(menus, "infoPager", pager);
            Wire(menus, "playButton", play);
            Wire(menus, "tutorialButton", tutorial);
            Wire(menus, "creditsButton", credits);
            Wire(menus, "quitButton", quit);
            Wire(menus, "resumeButton", resume);
            Wire(menus, "pauseTutorialButton", pauseTutorial);
            Wire(menus, "restartButton", restart);
            Wire(menus, "mainMenuButton", mainMenuBtn);
            Wire(menus, "pauseQuitButton", pauseQuit);
            Wire(menus, "infoPrevButton", prev);
            Wire(menus, "infoNextButton", next);
            Wire(menus, "infoCloseButton", close);

            // Sonidos sintetizados.
            var sfxGo = new GameObject("Sfx");
            sfxGo.AddComponent<AudioSource>();
            Wire(sfxGo.AddComponent<SfxPlayer>(), "runner", runner);
        }
    }
}
