using MissionDesign.Core;
using MissionDesign.Multiplayer;
using MissionDesign.Simulation;
using MissionDesign.UI;
using TMPro;
using UnityEditor;
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
            var title = Text("Title", main, $"<b>{GameInfo.StartScreenTitle}</b>", 112, UiTheme.Text, TextAlignmentOptions.Center);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(1600f, 120f));
            var tagline = Text("Tagline", main, GameInfo.Tagline, 30, UiTheme.Accent, TextAlignmentOptions.Center);
            Place(tagline.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -295f), new Vector2(1600f, 44f));
            var roleLine = Text("Roles", main,
                "1 a 4 jugadores · Director de Misión · Ingeniero de Sistemas · Científico Principal · Ingeniero de Telecom y Lanzamiento",
                18, UiTheme.TextDim, TextAlignmentOptions.Center);
            Place(roleLine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -345f), new Vector2(1600f, 30f));

            var column = NewRect("Buttons", main);
            Place(column, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 110f), new Vector2(460f, 470f));
            var vlg = column.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 16; vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = true;
            var play = MakeButton("PlayButton", column, "<b>JUGAR · DISEÑAR MISIÓN</b>", UiTheme.Launch, 26, 0);
            var explore = MakeButton("ExploreButton", column, "<b>Explorar el centro espacial</b>", UiTheme.Accent, 22, 0);
            var plans = MakeButton("PlansButton", column, "Planos del centro espacial", UiTheme.AccentDim, 22, 0);
            var tutorial = MakeButton("TutorialButton", column, "Cómo se juega", UiTheme.AccentDim, 22, 0);
            var credits = MakeButton("CreditsButton", column, "Fuentes y créditos", UiTheme.AccentDim, 22, 0);
            var quit = MakeButton("QuitButton", column, "Salir", UiTheme.PanelLight, 22, 0);
            var footer = Text("Footer", main, $"{GameInfo.Event} · Proyecto independiente, no es un producto oficial de la NASA · F1 ayuda · Esc pausa",
                15, UiTheme.TextDim, TextAlignmentOptions.Center);
            Place(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1600f, 26f));
            var team = Text("TeamLine", main, $"<b>{GameInfo.TeamLine}</b>", 22, UiTheme.Accent, TextAlignmentOptions.Center);
            Place(team.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 76f), new Vector2(1600f, 30f));

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
            Place(infoCard, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1300f, 820f));
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

            var (infoImage, infoFrame, infoCaption) = InfoImageArea(infoCard);
            Wire(menus, "exploreButton", explore);
            Wire(menus, "plansButton", plans);
            Wire(menus, "infoImage", infoImage);
            Wire(menus, "infoImageFrame", infoFrame);
            Wire(menus, "infoCaption", infoCaption);
            WireArray(menus, "planTextures", LoadPlanTextures());
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

        /// <summary>Marco para láminas (RawImage con proporción fija) y pie de imagen dentro de una tarjeta de información.</summary>
        private static (RawImage image, GameObject frame, TextMeshProUGUI caption) InfoImageArea(RectTransform card)
        {
            var frame = Panel("ImageFrame", card, new Color(0.02f, 0.03f, 0.06f, 1f));
            Place(frame, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(-96f, -260f));
            var imgRt = NewRect("Plan", frame);
            Stretch(imgRt);
            var image = imgRt.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            var fitter = imgRt.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 2048f / 1448f;
            var caption = Text("Caption", card, "", 19, UiTheme.Text, TextAlignmentOptions.TopLeft);
            Place(caption.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 92f), new Vector2(-96f, 52f));
            frame.gameObject.SetActive(false);
            return (image, frame.gameObject, caption);
        }

        private static Object[] LoadPlanTextures()
        {
            var list = new System.Collections.Generic.List<Object>();
            foreach (var sheet in MissionDesign.Exploration.SpaceCenterContent.Plans)
            {
                string path = $"{PlansFolder}/{sheet.file}.png";
                if (AssetImporter.GetAtPath(path) is TextureImporter ti &&
                    (ti.mipmapEnabled || ti.npotScale != TextureImporterNPOTScale.None || ti.maxTextureSize < 2048 || ti.textureCompression != TextureImporterCompression.CompressedHQ))
                {
                    ti.textureType = TextureImporterType.Default;
                    ti.mipmapEnabled = false;
                    ti.npotScale = TextureImporterNPOTScale.None;
                    ti.maxTextureSize = 2048;
                    ti.textureCompression = TextureImporterCompression.CompressedHQ;
                    ti.SaveAndReimport();
                }
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) Debug.LogWarning($"[SceneBuilder] Falta la lámina {path} (ejecuta ArtSource/Blender/build_all.ps1).");
                list.Add(tex);
            }
            return list.ToArray();
        }
    }
}
