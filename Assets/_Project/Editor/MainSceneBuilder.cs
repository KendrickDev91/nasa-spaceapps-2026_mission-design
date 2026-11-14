using System.IO;
using MissionDesign.Core;
using MissionDesign.Data;
using MissionDesign.Multiplayer;
using MissionDesign.Simulation;
using MissionDesign.UI;
using MissionDesign.UI.Visual3D;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MissionDesign.Editor
{
    /// <summary>
    /// Construye la escena principal completa: URP, cámara, nave con sockets, sala de control
    /// con anclas para Mixamo y el HUD de los 4 roles, todo cableado. Tras generarla, la escena es
    /// tuya: re-estiliza, cambia placeholders por tus prefabs de Blender, etc.
    /// Batch: -executeMethod MissionDesign.Editor.MainSceneBuilder.Build
    /// </summary>
    public static class MainSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/MissionDesign_Main.unity";
        private const string DatabasePath = "Assets/_Project/Data/Generated/NasaDatabase.asset";
        private const string RendererPath = "Assets/_Project/Settings/URP_Renderer.asset";
        private const string PipelinePath = "Assets/_Project/Settings/URP_Pipeline.asset";
        private const int UiLayer = 5;

        [MenuItem("NASA SpaceApps/Build Main Scene")]
        public static void Build()
        {
            if (File.Exists(ScenePath) && !Application.isBatchMode &&
                !EditorUtility.DisplayDialog("Reconstruir escena",
                    $"{ScenePath} ya existe y se sobrescribirá (los cambios manuales se pierden). ¿Continuar?",
                    "Reconstruir", "Cancelar"))
                return;

            EnsureUrp();
            if (AssetDatabase.LoadAssetAtPath<NasaDatabaseSO>(DatabasePath) == null)
                NasaDatabaseGenerator.Generate();
            if (TMP_Settings.defaultFontAsset == null)
                Debug.LogError("[SceneBuilder] Falta TMP Essential Resources: Window → TextMeshPro → Import TMP Essential Resources.");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Cargar DESPUÉS de NewScene: el modo Single descarga los assets no referenciados.
            var db = AssetDatabase.LoadAssetAtPath<NasaDatabaseSO>(DatabasePath);
            if (db == null) Debug.LogError($"[SceneBuilder] No se pudo cargar {DatabasePath}.");

            var camera = BuildEnvironment();
            var systems = new GameObject("MissionSystems");
            var manager = systems.AddComponent<MissionStateManager>();
            Wire(manager, "database", db);
            var roles = systems.AddComponent<RoleTurnManager>();
            var runner = systems.AddComponent<MissionSimulationRunner>();

            BuildSpacecraft();
            BuildControlRoom();
            BuildUi(camera, roles, runner);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath) ?? "Assets");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"[SceneBuilder] Escena creada: {ScenePath}");
        }

        // ───────────────────────────── URP ─────────────────────────────

        private static void EnsureUrp()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PipelinePath) ?? "Assets");
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
                AssetDatabase.SaveAssets();
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);
        }

        // ───────────────────────────── ENTORNO ─────────────────────────────

        private static Camera BuildEnvironment()
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetPositionAndRotation(new Vector3(0f, 2.2f, -10f), Quaternion.Euler(6f, 0f, 0f));
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = UiTheme.Background;
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            camGo.AddComponent<AudioListener>();

            var lightGo = new GameObject("Sun (Directional Light)");
            lightGo.transform.rotation = Quaternion.Euler(35f, -40f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.color = new Color(1f, 0.97f, 0.92f);
            light.shadows = LightShadows.Soft;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.19f, 0.28f);

            // Tierra de fondo (placeholder; reemplazar por modelo/texturas NASA Blue Marble).
            var earth = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            earth.name = "Earth_Placeholder";
            Object.DestroyImmediate(earth.GetComponent<Collider>());
            earth.transform.position = new Vector3(-4f, -9f, 14f);
            earth.transform.localScale = Vector3.one * 16f;
            earth.GetComponent<Renderer>().sharedMaterial = CreateLitMaterial("MAT_Earth_Placeholder", new Color(0.12f, 0.35f, 0.75f));
            return cam;
        }

        private static Material CreateLitMaterial(string name, Color color)
        {
            string path = $"Assets/_Project/Art/Materials/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "Assets");
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.35f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ───────────────────────────── NAVE ─────────────────────────────

        private static void BuildSpacecraft()
        {
            var root = new GameObject("Spacecraft");
            root.transform.position = new Vector3(0f, 2.1f, 0f);
            root.transform.rotation = Quaternion.Euler(0f, -25f, 0f);
            var assembler = root.AddComponent<ModularSpacecraftAssembler>();
            root.AddComponent<SlowSpin>();

            Wire(assembler, "coreBusSocket", Socket(root, "CoreBus", Vector3.zero, Vector3.zero));
            Wire(assembler, "topPayloadSocket", Socket(root, "TopPayloadSocket", new Vector3(0f, 0.75f, 0f), Vector3.zero));
            Wire(assembler, "solarWingLeft", Socket(root, "SolarWingLeft", new Vector3(-0.75f, 0f, 0f), Vector3.zero));
            Wire(assembler, "solarWingRight", Socket(root, "SolarWingRight", new Vector3(0.75f, 0f, 0f), Vector3.zero));
            Wire(assembler, "antennaSocket", Socket(root, "AntennaSocket", new Vector3(0f, 0.1f, -0.75f), Vector3.zero));
            Wire(assembler, "thrusterSocket", Socket(root, "ThrusterSocket", new Vector3(0f, -0.8f, 0f), new Vector3(180f, 0f, 0f)));
            Wire(assembler, "internalBay", Socket(root, "InternalBay", new Vector3(0f, -0.2f, 0.45f), Vector3.zero));
        }

        private static Transform Socket(GameObject root, string name, Vector3 pos, Vector3 euler)
        {
            var s = new GameObject(name).transform;
            s.SetParent(root.transform, false);
            s.localPosition = pos;
            s.localRotation = Quaternion.Euler(euler);
            return s;
        }

        // ───────────────────────── SALA DE CONTROL ─────────────────────────

        private static void BuildControlRoom()
        {
            var room = new GameObject("ControlRoom");
            room.transform.position = new Vector3(40f, 0f, 0f);   // fuera de cámara: escena 3D de la sala (Mixamo)
            var bridge = room.AddComponent<MissionControlAnimatorBridge>();

            var so = new SerializedObject(bridge);
            var ops = so.FindProperty("operators");
            ops.arraySize = RoleTurnManager.AllRoles.Length;
            for (int i = 0; i < RoleTurnManager.AllRoles.Length; i++)
            {
                var role = RoleTurnManager.AllRoles[i];
                var anchor = new GameObject($"Operator_{role}");
                anchor.transform.SetParent(room.transform, false);
                anchor.transform.localPosition = new Vector3(i * 1.6f - 2.4f, 0f, 0f);
                var animator = anchor.AddComponent<Animator>();   // asigna aquí tu Avatar + Animator Controller de Mixamo

                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Placeholder_Body";
                Object.DestroyImmediate(body.GetComponent<Collider>());
                body.transform.SetParent(anchor.transform, false);
                body.transform.localPosition = new Vector3(0f, 1f, 0f);

                var element = ops.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("role").enumValueIndex = (int)role;
                element.FindPropertyRelative("animator").objectReferenceValue = animator;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ───────────────────────────── HUD ─────────────────────────────

        private static void BuildUi(Camera camera, RoleTurnManager roles, MissionSimulationRunner runner)
        {
            var canvasGo = new GameObject("HUD_Canvas", typeof(RectTransform)) { layer = UiLayer };
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
            es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();

            var root = canvasGo.transform;
            BuildTopBar(root, roles);
            BuildCatalog(root, roles);
            BuildTelemetry(root);
            BuildValidation(root, roles);
            BuildSimulation(root, runner);
            BuildPostMission(root);
            BuildTooltip(root);
        }

        private static void BuildSimulation(Transform root, MissionSimulationRunner runner)
        {
            var panel = Panel("SimulationPanel", root, UiTheme.Panel);
            panel.anchorMin = new Vector2(0, 0); panel.anchorMax = new Vector2(1, 0); panel.pivot = new Vector2(0.5f, 0);
            panel.offsetMin = new Vector2(562, 16); panel.offsetMax = new Vector2(-612, 400);
            SetPhases(panel.gameObject, MissionPhase.Launch, MissionPhase.OrbitInsertion, MissionPhase.ScienceOperations, MissionPhase.DataTransmission);

            var title = Text("Title", panel, "SIMULACIÓN DE MISIÓN", 15, UiTheme.TextDim, TextAlignmentOptions.TopLeft, bold: true);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(-32, 22));
            var phase = Text("PhaseText", panel, "LANZAMIENTO", 21, UiTheme.Text, TextAlignmentOptions.TopLeft);
            Place(phase.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -34), new Vector2(-32, 30));

            var track = NewRect("ProgressTrack", panel);
            track.gameObject.AddComponent<Image>().color = UiTheme.PanelLight;
            Place(track, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -72), new Vector2(-32, 10));
            var fill = NewRect("Fill", track);
            Stretch(fill);
            fill.gameObject.AddComponent<Image>().color = UiTheme.Accent;

            var log = Text("LogText", panel, "", 15, UiTheme.Text, TextAlignmentOptions.TopLeft);
            log.overflowMode = TextOverflowModes.Truncate;
            Place(log.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -94), new Vector2(-32, -164));

            var skip = MakeButton("SkipButton", panel, "Saltar al resultado »", UiTheme.AccentDim, 17, 0);
            Place((RectTransform)skip.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16, 12), new Vector2(240, 46));

            var view = panel.gameObject.AddComponent<SimulationPanel>();
            Wire(view, "runner", runner);
            Wire(view, "phaseText", phase);
            Wire(view, "progressFill", fill);
            Wire(view, "logText", log);
            Wire(view, "skipButton", skip);
        }

        private static void BuildPostMission(Transform root)
        {
            var overlay = Panel("PostMissionOverlay", root, new Color(0f, 0f, 0f, 0.78f));
            Stretch(overlay);
            SetPhases(overlay.gameObject, MissionPhase.PostMission);

            var card = Panel("Card", overlay, new Color(0.04f, 0.06f, 0.11f, 1f));
            Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500, 900));

            var header = Text("Header", card, "ANÁLISIS POST-MISIÓN", 18, UiTheme.Accent, TextAlignmentOptions.TopLeft, bold: true);
            Place(header.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(-64, 26));
            var verdict = Text("VerdictText", card, "VEREDICTO", 60, UiTheme.Text, TextAlignmentOptions.TopLeft, bold: true);
            Place(verdict.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -54), new Vector2(-64, 90));

            // Tres tarjetas: bien · falló · puntuación (card 1500 × 900; columnas de 32 + 455 + 20 + 455 + 20 + 486 + 32).
            var wentWell = MakeCard(card, "WentWellCard", "LO QUE SALIÓ BIEN", UiTheme.Ok, new Color(0.06f, 0.16f, 0.11f, 1f), 32f, 455f);
            var wentWrong = MakeCard(card, "WentWrongCard", "LO QUE FALLÓ", UiTheme.Critical, new Color(0.18f, 0.07f, 0.07f, 1f), 507f, 455f);

            var scoreCard = Panel("ScoreCard", card, new Color(0.08f, 0.11f, 0.19f, 1f));
            Place(scoreCard, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 1), new Vector2(982, -160), new Vector2(486, -270));
            var scoreTitle = Text("Title", scoreCard, "PUNTUACIÓN", 18, UiTheme.Accent, TextAlignmentOptions.TopLeft, bold: true);
            Place(scoreTitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(-40, 26));
            var rank = Text("RankText", scoreCard, "A", 110, UiTheme.Text, TextAlignmentOptions.TopLeft, bold: true);
            Place(rank.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -42), new Vector2(-40, 120));
            var similar = Text("SimilarText", scoreCard, "", 16, UiTheme.Text, TextAlignmentOptions.TopLeft);
            Place(similar.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(-40, 130));
            var metrics = Text("MetricsText", scoreCard, "", 16, UiTheme.Text, TextAlignmentOptions.TopLeft);
            metrics.lineSpacing = 12;
            Place(metrics.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -310), new Vector2(-40, -330));

            var export = Text("ExportText", card, "", 13, UiTheme.TextDim, TextAlignmentOptions.BottomLeft);
            Place(export.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(-250, 34), new Vector2(-564, 44));
            var open = MakeButton("OpenReportsButton", card, "Abrir reportes", UiTheme.AccentDim, 18, 0);
            Place((RectTransform)open.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-292, 28), new Vector2(220, 54));
            var back = MakeButton("BackButton", card, "<b>Volver al diseño</b>", UiTheme.Accent, 19, 0);
            Place((RectTransform)back.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-32, 28), new Vector2(240, 54));

            var view = overlay.gameObject.AddComponent<PostMissionPanel>();
            Wire(view, "verdictText", verdict);
            Wire(view, "wentWellText", wentWell);
            Wire(view, "wentWrongText", wentWrong);
            Wire(view, "rankText", rank);
            Wire(view, "similarText", similar);
            Wire(view, "metricsText", metrics);
            Wire(view, "exportText", export);
            Wire(view, "openReportsButton", open);
            Wire(view, "backButton", back);
        }

        /// <summary>Tarjeta con encabezado coloreado; devuelve el texto del cuerpo.</summary>
        private static TextMeshProUGUI MakeCard(RectTransform parent, string name, string title, Color accent, Color background, float x, float width)
        {
            var cardRt = Panel(name, parent, background);
            Place(cardRt, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -160), new Vector2(width, -270));
            var stripe = NewRect("Stripe", cardRt);
            stripe.gameObject.AddComponent<Image>().color = accent;
            Place(stripe, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 5));
            var head = Text("Title", cardRt, title, 18, accent, TextAlignmentOptions.TopLeft, bold: true);
            Place(head.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(-40, 26));
            var body = Text("Body", cardRt, "", 16, UiTheme.Text, TextAlignmentOptions.TopLeft);
            body.paragraphSpacing = 10;
            body.overflowMode = TextOverflowModes.Ellipsis;
            Place(body.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -56), new Vector2(-40, -72));
            return body;
        }

        /// <summary>Tooltip único, último hijo del canvas para quedar por encima de todo.</summary>
        private static void BuildTooltip(Transform root)
        {
            var tooltipRoot = NewRect("Tooltip", root);
            Stretch(tooltipRoot);
            var panel = Panel("Panel", tooltipRoot, new Color(0.03f, 0.05f, 0.10f, 0.98f));
            panel.GetComponent<Image>().raycastTarget = false;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0f, 1f);
            panel.sizeDelta = new Vector2(500f, 100f);
            var vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(16, 16, 12, 14);
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var text = Text("Text", panel, "", 15, UiTheme.Text, TextAlignmentOptions.TopLeft);
            text.paragraphSpacing = 4;

            var view = tooltipRoot.gameObject.AddComponent<TooltipView>();
            Wire(view, "panel", panel);
            Wire(view, "text", text);
        }

        private static void SetPhases(GameObject go, params MissionPhase[] phases)
        {
            go.AddComponent<CanvasGroup>();
            var visibility = go.AddComponent<PhaseVisibility>();
            var so = new SerializedObject(visibility);
            var prop = so.FindProperty("visibleIn");
            prop.arraySize = phases.Length;
            for (int i = 0; i < phases.Length; i++) prop.GetArrayElementAtIndex(i).enumValueIndex = (int)phases[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildTopBar(Transform root, RoleTurnManager roles)
        {
            var bar = Panel("TopBar", root, UiTheme.Panel);
            Place(bar, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 96));

            var title = Text("Title", bar, $"<b>{GameInfo.Title}</b>\n<size=60%><color=#9FB3D1>{GameInfo.Tagline}</color></size>", 30, UiTheme.Text, TextAlignmentOptions.MidlineLeft);
            Place(title.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(420, 0));

            var row = NewRect("RoleTabs", bar);
            Place(row, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(450, 0), new Vector2(-466, -20));
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10; hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;

            var view = bar.gameObject.AddComponent<RoleTabsView>();
            var roleButtons = new Object[RoleTurnManager.AllRoles.Length];
            for (int i = 0; i < roleButtons.Length; i++)
                roleButtons[i] = MakeButton($"Role_{RoleTurnManager.AllRoles[i]}", row, RoleTurnManager.RoleDisplayName(RoleTurnManager.AllRoles[i]), UiTheme.PanelLight, 18, 250);

            var players = MakeButton("PlayersButton", row, "4 jugadores", UiTheme.AccentDim, 18, 150);
            var next = MakeButton("NextTurnButton", row, "Siguiente turno »", UiTheme.AccentDim, 18, 190);
            var turn = Text("TurnText", row, "Turno:", 18, UiTheme.TextDim, TextAlignmentOptions.MidlineLeft);
            turn.gameObject.AddComponent<LayoutElement>().preferredWidth = 200;

            Wire(view, "roles", roles);
            WireArray(view, "roleButtons", roleButtons);
            Wire(view, "nextTurnButton", next);
            Wire(view, "playersButton", players);
            Wire(view, "playersText", players.GetComponentInChildren<TMP_Text>());
            Wire(view, "turnText", turn);
        }

        private static void BuildCatalog(Transform root, RoleTurnManager roles)
        {
            var panel = Panel("CatalogPanel", root, UiTheme.Panel);
            Place(panel, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(16, -40), new Vector2(530, -128));

            var title = Text("Title", panel, "ROL", 24, UiTheme.Accent, TextAlignmentOptions.TopLeft, bold: true);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(-32, 34));
            var hint = Text("Hint", panel, "", 15, UiTheme.TextDim, TextAlignmentOptions.TopLeft);
            Place(hint.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(-32, 44));

            var scroll = NewRect("Scroll", panel);
            Place(scroll, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -50), new Vector2(-24, -124));
            var viewport = NewRect("Viewport", scroll);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6; vlg.padding = new RectOffset(4, 4, 4, 12);
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var sr = scroll.gameObject.AddComponent<ScrollRect>();
            sr.content = content; sr.viewport = viewport; sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 30f;
            scroll.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.001f);   // recibe el scroll

            // Plantillas (desactivadas): re-estilízalas en el editor.
            var section = Text("SectionTemplate", content, "SECCIÓN", 15, UiTheme.Accent, TextAlignmentOptions.BottomLeft, bold: true);
            section.margin = new Vector4(2, 12, 0, 2);

            var item = NewRect("ItemTemplate", content);
            var itemImage = item.gameObject.AddComponent<Image>();
            itemImage.color = UiTheme.PanelLight;
            var itemButton = item.gameObject.AddComponent<Button>();
            itemButton.targetGraphic = itemImage;
            var itemVlg = item.gameObject.AddComponent<VerticalLayoutGroup>();
            itemVlg.padding = new RectOffset(14, 14, 10, 10); itemVlg.spacing = 2;
            itemVlg.childControlWidth = itemVlg.childControlHeight = true;
            itemVlg.childForceExpandWidth = true; itemVlg.childForceExpandHeight = false;
            Text("Name", item, "Nombre", 19, UiTheme.Text, TextAlignmentOptions.TopLeft, bold: true);
            Text("Stats", item, "Estadísticas", 14, UiTheme.TextDim, TextAlignmentOptions.TopLeft);

            var catalog = panel.gameObject.AddComponent<CatalogPanel>();
            Wire(catalog, "roles", roles);
            Wire(catalog, "titleText", title);
            Wire(catalog, "hintText", hint);
            Wire(catalog, "content", content);
            Wire(catalog, "itemTemplate", itemButton);
            Wire(catalog, "sectionTemplate", section);
        }

        private static void BuildTelemetry(Transform root)
        {
            var panel = Panel("TelemetryPanel", root, UiTheme.Panel);
            Place(panel, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-16, -40), new Vector2(580, -128));

            var title = Text("Title", panel, "TELEMETRÍA DE MISIÓN", 24, UiTheme.Accent, TextAlignmentOptions.TopLeft, bold: true);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(-32, 34));

            var list = NewRect("Resources", panel);
            Place(list, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -56), new Vector2(-32, -70));
            var vlg = list.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10;
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            var kinds = new[] { ResourceKind.Mass, ResourceKind.Power, ResourceKind.Budget, ResourceKind.Data, ResourceKind.Comms, ResourceKind.Time };
            var bars = new Object[kinds.Length];
            for (int i = 0; i < kinds.Length; i++) bars[i] = MakeResourceBar(list, kinds[i]);

            var science = Text("ScienceText", list, "VALOR CIENTÍFICO", 18, UiTheme.Text, TextAlignmentOptions.TopLeft);
            science.margin = new Vector4(0, 8, 0, 0);
            var alerts = Text("AlertsText", list, "", 14, UiTheme.Text, TextAlignmentOptions.TopLeft);

            var telemetry = panel.gameObject.AddComponent<TelemetryPanel>();
            WireArray(telemetry, "bars", bars);
            Wire(telemetry, "scienceText", science);
            Wire(telemetry, "alertsText", alerts);
        }

        private static ResourceBarView MakeResourceBar(Transform parent, ResourceKind kind)
        {
            var bar = NewRect($"Bar_{kind}", parent);
            bar.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);   // recibe el hover para el tooltip
            var vlg = bar.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            var header = NewRect("Header", bar);
            var hlg = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = false;
            var label = Text("Label", header, ResourceBarView.Title(kind), 16, UiTheme.TextDim, TextAlignmentOptions.MidlineLeft, bold: true);
            var value = Text("Value", header, "—", 18, UiTheme.Text, TextAlignmentOptions.MidlineRight, bold: true);

            var track = NewRect("Track", bar);
            track.gameObject.AddComponent<Image>().color = UiTheme.PanelLight;
            track.gameObject.AddComponent<LayoutElement>().preferredHeight = 12;
            var fillRt = NewRect("Fill", track);
            Stretch(fillRt);
            var fill = fillRt.gameObject.AddComponent<Image>();
            fill.color = UiTheme.Ok;

            var detail = Text("Detail", bar, "", 12, UiTheme.TextDim, TextAlignmentOptions.TopLeft);

            var view = bar.gameObject.AddComponent<ResourceBarView>();
            var so = new SerializedObject(view);
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("valueText").objectReferenceValue = value;
            so.FindProperty("detailText").objectReferenceValue = detail;
            so.FindProperty("fill").objectReferenceValue = fill;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static void BuildValidation(Transform root, RoleTurnManager roles)
        {
            var panel = Panel("ValidationPanel", root, UiTheme.Panel);
            panel.anchorMin = new Vector2(0, 0); panel.anchorMax = new Vector2(1, 0); panel.pivot = new Vector2(0.5f, 0);
            panel.offsetMin = new Vector2(562, 16); panel.offsetMax = new Vector2(-612, 400);
            SetPhases(panel.gameObject, MissionPhase.Design);

            var status = Text("StatusText", panel, "ESTADO", 24, UiTheme.Text, TextAlignmentOptions.TopLeft, bold: true);
            Place(status.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(-32, 34));

            var issues = Text("IssuesText", panel, "", 14, UiTheme.Text, TextAlignmentOptions.TopLeft);
            issues.overflowMode = TextOverflowModes.Ellipsis;
            Place(issues.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -52), new Vector2(-32, -150));

            var message = Text("MessageText", panel, "", 16, UiTheme.Warning, TextAlignmentOptions.BottomLeft);
            Place(message.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 66), new Vector2(-32, 26));

            var consensus = Text("ConsensusText", panel, "CONSENSO DEL EQUIPO", 15, UiTheme.Text, TextAlignmentOptions.MidlineLeft);
            consensus.textWrappingMode = TextWrappingModes.NoWrap;
            Place(consensus.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(16, 10), new Vector2(310, 52));

            var approve = MakeButton("ApproveButton", panel, "Aprobar diseño", UiTheme.AccentDim, 18, 0);
            Place((RectTransform)approve.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-236, 12), new Vector2(170, 48));
            var launch = MakeButton("LaunchButton", panel, "<b>LANZAR MISIÓN</b>", UiTheme.Launch, 20, 0);
            Place((RectTransform)launch.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16, 12), new Vector2(210, 48));

            var view = panel.gameObject.AddComponent<ValidationPanel>();
            Wire(view, "roles", roles);
            Wire(view, "statusText", status);
            Wire(view, "issuesText", issues);
            Wire(view, "consensusText", consensus);
            Wire(view, "approveButton", approve);
            Wire(view, "launchButton", launch);
            Wire(view, "messageText", message);
        }

        // ───────────────────────────── UTILIDADES UI ─────────────────────────────

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = UiLayer };
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static RectTransform Panel(string name, Transform parent, Color color)
        {
            var rt = NewRect(name, parent);
            rt.gameObject.AddComponent<Image>().color = color;
            return rt;
        }

        private static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Color color,
            TextAlignmentOptions align, bool bold = false)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.richText = true;
            t.raycastTarget = false;
            if (bold) t.fontStyle = FontStyles.Bold;
            return t;
        }

        private static Button MakeButton(string name, Transform parent, string label, Color color, float fontSize, float preferredWidth)
        {
            var rt = NewRect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (preferredWidth > 0f) rt.gameObject.AddComponent<LayoutElement>().preferredWidth = preferredWidth;
            var text = Text("Label", rt, label, fontSize, UiTheme.Text, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
            text.margin = new Vector4(8, 4, 8, 4);
            return button;
        }

        private static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void Wire(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[SceneBuilder] {target.GetType().Name} no tiene el campo '{field}'.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
