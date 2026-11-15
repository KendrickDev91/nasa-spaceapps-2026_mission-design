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
    public static partial class MainSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/MissionDesign_Main.unity";
        private const string DatabasePath = "Assets/_Project/Data/Generated/NasaDatabase.asset";
        private const string RendererPath = "Assets/_Project/Settings/URP_Renderer.asset";
        private const string PipelinePath = "Assets/_Project/Settings/URP_Pipeline.asset";
        private const int UiLayer = 5;
        private const string PlansFolder = "Assets/_Project/Art/Plans";

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

            CrewController();   // avatar Humanoid + Animator del personal de la sala de control
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
            Wire(systems.AddComponent<CollaborationTracker>(), "roles", roles);

            var assembler = BuildSpacecraft();
            var room = BuildControlRoom();
            BuildUi(camera, roles, runner);
            BuildAreas(systems, camera, assembler, room, runner);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath) ?? "Assets");
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterScenes();
            AssetDatabase.SaveAssets();
            Debug.Log($"[SceneBuilder] Escena creada: {ScenePath}");
        }

        /// <summary>Escenas del juego en Build Settings: diseño de misión (índice 0) y centro espacial si ya existe.</summary>
        public static void RegisterScenes()
        {
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            if (File.Exists(SpaceCenterScenePath)) list.Add(new EditorBuildSettingsScene(SpaceCenterScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
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

        private static ModularSpacecraftAssembler BuildSpacecraft()
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
            return assembler;
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

        private static GameObject BuildControlRoom()
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
            return room;
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
            var library = BuildDesignLibrary(root, roles);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(designsButton.onClick, library.Toggle);
            BuildMenus(root, roles, runner);
            BuildTooltip(root);
        }

        private static Button designsButton;

        /// <summary>Ventana modal "Diseños": equipo, guardar, lista de guardados y comparación.</summary>
        private static DesignLibraryPanel BuildDesignLibrary(Transform root, RoleTurnManager roles)
        {
            var overlay = Panel("DesignLibraryOverlay", root, new Color(0f, 0f, 0f, 0.7f));
            Stretch(overlay);
            var group = overlay.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false;

            var card = Panel("Card", overlay, new Color(0.04f, 0.06f, 0.11f, 1f));
            Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 820));

            var title = Text("Title", card, "DISEÑOS: GUARDAR, CARGAR Y COMPARAR", 24, UiTheme.Accent, TextAlignmentOptions.TopLeft, bold: true);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(-64, 34));
            var hint = Text("Hint", card, "Guarda alternativas para el mismo objetivo y compáralas: casi nunca hay una única solución perfecta.", 15, UiTheme.TextDim, TextAlignmentOptions.TopLeft);
            Place(hint.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -62), new Vector2(-64, 24));

            var teamLabel = Text("TeamLabel", card, "Equipo", 15, UiTheme.TextDim, TextAlignmentOptions.MidlineLeft);
            Place(teamLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -100), new Vector2(200, 24));
            var team = MakeInput("TeamInput", card, "Nombre del equipo");
            Place((RectTransform)team.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -126), new Vector2(300, 44));
            var nameLabel = Text("NameLabel", card, "Nombre del diseño", 15, UiTheme.TextDim, TextAlignmentOptions.MidlineLeft);
            Place(nameLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(352, -100), new Vector2(300, 24));
            var designName = MakeInput("DesignNameInput", card, "p. ej. Opción barata LEO");
            Place((RectTransform)designName.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(352, -126), new Vector2(330, 44));
            var save = MakeButton("SaveButton", card, "<b>Guardar diseño actual</b>", UiTheme.Accent, 17, 0);
            Place((RectTransform)save.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(702, -126), new Vector2(240, 44));
            var close = MakeButton("CloseButton", card, "Cerrar", UiTheme.AccentDim, 17, 0);
            Place((RectTransform)close.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-32, -20), new Vector2(130, 44));
            var status = Text("StatusText", card, "", 15, UiTheme.Warning, TextAlignmentOptions.MidlineLeft);
            Place(status.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -178), new Vector2(-64, 24));

            // Lista de guardados (izquierda) y comparación (derecha).
            var listTitle = Text("ListTitle", card, "GUARDADOS", 16, UiTheme.Accent, TextAlignmentOptions.TopLeft, bold: true);
            Place(listTitle.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -212), new Vector2(600, 24));
            var list = NewRect("List", card);
            Place(list, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -242), new Vector2(620, -274));
            var vlg = list.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6; vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            var item = Panel("ItemTemplate", list, UiTheme.PanelLight);
            item.gameObject.AddComponent<LayoutElement>().preferredHeight = 52;
            var info = NewRect("Info", item);
            Place(info, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(-250, -8));
            var itemName = Text("Name", info, "Diseño", 16, UiTheme.Text, TextAlignmentOptions.TopLeft);
            Place(itemName.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 22));
            var itemDetail = Text("Detail", info, "", 12, UiTheme.TextDim, TextAlignmentOptions.TopLeft);
            Place(itemDetail.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 18));
            var load = MakeButton("LoadButton", item, "Cargar", UiTheme.AccentDim, 15, 0);
            Place((RectTransform)load.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-124, 0), new Vector2(100, 38));
            var compare = MakeButton("CompareButton", item, "Comparar", UiTheme.Accent, 15, 0);
            Place((RectTransform)compare.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(106, 38));

            var compareCard = Panel("ComparisonCard", card, new Color(0.08f, 0.11f, 0.19f, 1f));
            Place(compareCard, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 1), new Vector2(672, -212), new Vector2(696, -244));
            var comparison = Text("ComparisonText", compareCard, "", 15, UiTheme.Text, TextAlignmentOptions.TopLeft);
            comparison.lineSpacing = 6;
            Place(comparison.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-32, -28));

            var view = overlay.gameObject.AddComponent<DesignLibraryPanel>();
            Wire(view, "group", group);
            Wire(view, "roles", roles);
            Wire(view, "teamInput", team);
            Wire(view, "designNameInput", designName);
            Wire(view, "saveButton", save);
            Wire(view, "closeButton", close);
            Wire(view, "listContent", list);
            Wire(view, "itemTemplate", item);
            Wire(view, "comparisonText", comparison);
            Wire(view, "statusText", status);
            return view;
        }

        private static TMP_InputField MakeInput(string name, Transform parent, string placeholder)
        {
            var go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            go.name = name;
            go.layer = UiLayer;
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = UiLayer;
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = UiTheme.PanelLight;
            var input = go.GetComponent<TMP_InputField>();
            input.pointSize = 17;
            input.textComponent.color = UiTheme.Text;
            if (input.placeholder is TMP_Text ph)
            {
                ph.text = placeholder;
                ph.color = UiTheme.TextDim;
            }
            return input;
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

            BuildFlightHud(root);

            var view = panel.gameObject.AddComponent<SimulationPanel>();
            Wire(view, "runner", runner);
            Wire(view, "phaseText", phase);
            Wire(view, "progressFill", fill);
            Wire(view, "logText", log);
            Wire(view, "skipButton", skip);
        }

        private static void BuildPostMission(Transform root)
        {
            var overlay = Panel("PostMissionOverlay", root, new Color(0f, 0f, 0f, 0.8f));
            Stretch(overlay);
            SetPhases(overlay.gameObject, MissionPhase.PostMission);

            var card = Panel("Card", overlay, new Color(0.035f, 0.05f, 0.1f, 0.98f));
            Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1640, 960));

            var header = Text("Header", card, "ANÁLISIS POST-MISIÓN", 20, UiTheme.Accent, TextAlignmentOptions.TopLeft, bold: true);
            Place(header.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(-72, 28));
            var verdict = Text("VerdictText", card, "ÉXITO", 72, UiTheme.Text, TextAlignmentOptions.MidlineLeft, bold: true);
            Place(verdict.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(36, -100), new Vector2(640, 90));
            // "ÉXITO PARCIAL" debe caber en una línea: sin salto y con tamaño automático.
            verdict.textWrappingMode = TextWrappingModes.NoWrap;
            verdict.enableAutoSizing = true;
            verdict.fontSizeMin = 36;
            verdict.fontSizeMax = 64;
            var title = Text("TitleText", card, "", 30, UiTheme.Text, TextAlignmentOptions.MidlineLeft);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(700, -102), new Vector2(-740, 60));

            // Dos tarjetas de viñetas cortas (texto grande) y la tarjeta de puntuación con barras.
            TextMeshProUGUI BulletCard(string name, string head, Color accent, Color bg, float x)
            {
                var c = Panel(name, card, bg);
                Place(c, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -168), new Vector2(520, -278));
                var stripe = NewRect("Stripe", c);
                stripe.gameObject.AddComponent<Image>().color = accent;
                Place(stripe, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 6));
                var h = Text("Title", c, head, 22, accent, TextAlignmentOptions.TopLeft, bold: true);
                Place(h.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(-48, 30));
                var body = Text("Body", c, "", 21, UiTheme.Text, TextAlignmentOptions.TopLeft);
                body.paragraphSpacing = 16;
                body.lineSpacing = 4;
                body.overflowMode = TextOverflowModes.Ellipsis;
                Place(body.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -66), new Vector2(-48, -86));
                return body;
            }
            var wentWell = BulletCard("WentWellCard", "LO QUE SALIÓ BIEN", UiTheme.Ok, new Color(0.05f, 0.14f, 0.1f, 1f), 36f);
            var wentWrong = BulletCard("WentWrongCard", "LO QUE FALLÓ", UiTheme.Critical, new Color(0.16f, 0.06f, 0.07f, 1f), 572f);

            var scoreCard = Panel("ScoreCard", card, new Color(0.07f, 0.1f, 0.18f, 1f));
            Place(scoreCard, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 1), new Vector2(1108, -168), new Vector2(496, -278));
            var scoreTitle = Text("Title", scoreCard, "PUNTUACIÓN", 22, UiTheme.Accent, TextAlignmentOptions.TopLeft, bold: true);
            Place(scoreTitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(-48, 30));
            var rank = Text("RankText", scoreCard, "A", 120, UiTheme.Text, TextAlignmentOptions.MidlineLeft, bold: true);
            Place(rank.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(28, -118), new Vector2(150, 130));
            var score = Text("ScoreText", scoreCard, "0/100", 64, UiTheme.Text, TextAlignmentOptions.MidlineLeft, bold: true);
            Place(score.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(190, -118), new Vector2(290, 90));

            string[] barNames = { "Ciencia", "Datos recibidos", "Energía disponible", "Presupuesto usado", "Trabajo en equipo" };
            var fills = new Object[barNames.Length];
            var values = new Object[barNames.Length];
            for (int b = 0; b < barNames.Length; b++)
            {
                float y = -200 - b * 58;
                var label = Text("BarLabel" + b, scoreCard, barNames[b], 18, UiTheme.TextDim, TextAlignmentOptions.BottomLeft);
                Place(label.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(-48, 24));
                var value = Text("BarValue" + b, scoreCard, "", 18, UiTheme.Text, TextAlignmentOptions.BottomRight, bold: true);
                Place(value.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(-48, 24));
                var track = Panel("BarTrack" + b, scoreCard, new Color(0.14f, 0.18f, 0.28f, 1f));
                Place(track, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, y - 28), new Vector2(-48, 12));
                var fill = Panel("Fill", track, UiTheme.Accent);
                fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0.5f, 1f);
                fill.offsetMin = fill.offsetMax = Vector2.zero;
                fills[b] = fill;
                values[b] = value;
            }
            var similar = Text("SimilarText", scoreCard, "", 17, UiTheme.Text, TextAlignmentOptions.TopLeft);
            Place(similar.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -500), new Vector2(-48, 70));
            var leaderboard = Text("LeaderboardText", scoreCard, "", 16, UiTheme.Text, TextAlignmentOptions.TopLeft);
            Place(leaderboard.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(-48, 100));

            var export = Text("ExportText", card, "", 15, UiTheme.TextDim, TextAlignmentOptions.MidlineLeft);
            Place(export.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(36, 34), new Vector2(560, 50));
            var exploration = MakeButton("ExplorationButton", card, "Informe de la exploración", UiTheme.Launch, 20, 0);
            Place((RectTransform)exploration.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-568, 30), new Vector2(380, 60));
            var open = MakeButton("OpenReportsButton", card, "Abrir reportes", UiTheme.AccentDim, 19, 0);
            Place((RectTransform)open.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-312, 30), new Vector2(240, 60));
            var back = MakeButton("BackButton", card, "<b>Volver al diseño</b>", UiTheme.Accent, 20, 0);
            Place((RectTransform)back.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-36, 30), new Vector2(260, 60));

            var view = overlay.gameObject.AddComponent<PostMissionPanel>();
            Wire(view, "verdictText", verdict);
            Wire(view, "titleText", title);
            Wire(view, "wentWellText", wentWell);
            Wire(view, "wentWrongText", wentWrong);
            Wire(view, "rankText", rank);
            Wire(view, "scoreText", score);
            Wire(view, "similarText", similar);
            Wire(view, "leaderboardText", leaderboard);
            Wire(view, "exportText", export);
            WireArray(view, "barFills", fills);
            WireArray(view, "barValues", values);
            Wire(view, "openReportsButton", open);
            Wire(view, "backButton", back);
            Wire(view, "explorationButton", exploration);

            var confetti = NewRect("Confetti", overlay);
            Stretch(confetti);
            Wire(view, "confettiLayer", confetti);
            Wire(view, "explorationPanel", BuildExplorationReport(root));
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
            Place(row, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(430, 0), new Vector2(-446, -20));
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8; hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;

            var view = bar.gameObject.AddComponent<RoleTabsView>();
            var roleButtons = new Object[RoleTurnManager.AllRoles.Length];
            for (int i = 0; i < roleButtons.Length; i++)
                roleButtons[i] = MakeButton($"Role_{RoleTurnManager.AllRoles[i]}", row, RoleTurnManager.RoleDisplayName(RoleTurnManager.AllRoles[i]), UiTheme.PanelLight, 17, 210);

            var designs = MakeButton("DesignsButton", row, "Diseños", UiTheme.AccentDim, 17, 120);
            var players = MakeButton("PlayersButton", row, "4 jugadores", UiTheme.AccentDim, 17, 130);
            var next = MakeButton("NextTurnButton", row, "Siguiente turno »", UiTheme.AccentDim, 17, 170);
            var turn = Text("TurnText", row, "Turno:", 18, UiTheme.TextDim, TextAlignmentOptions.MidlineLeft);
            turn.gameObject.AddComponent<LayoutElement>().preferredWidth = 150;
            designsButton = designs;

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
            SetPhases(panel.gameObject, MissionPhase.Design);
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
            SetPhases(panel.gameObject, MissionPhase.Design);
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
            Place(issues.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -52), new Vector2(-32, -192));

            // Fila inferior: botones. Encima, una fila propia de consenso que ocupa todo el ancho del panel
            // (las píldoras se reparten en cuartos), así nunca se montan sobre los botones aunque la ventana sea angosta.
            // Más arriba: título del consenso a la izquierda y mensaje a la derecha.
            var consensus = Text("ConsensusText", panel, "CONSENSO DEL EQUIPO", 14, UiTheme.TextDim, TextAlignmentOptions.BottomLeft, bold: true);
            consensus.textWrappingMode = TextWrappingModes.NoWrap;
            Place(consensus.rectTransform, new Vector2(0, 0), new Vector2(0.4f, 0), new Vector2(0, 0), new Vector2(16, 106), new Vector2(-16, 20));

            var message = Text("MessageText", panel, "", 15, UiTheme.Warning, TextAlignmentOptions.BottomRight);
            message.enableAutoSizing = true; message.fontSizeMin = 11; message.fontSizeMax = 15;
            Place(message.rectTransform, new Vector2(0.4f, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16, 104), new Vector2(-24, 34));

            var chipRow = Panel("ConsensusRow", panel, new Color(0, 0, 0, 0));
            Place(chipRow, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 68), new Vector2(-32, 30));
            var chips = new Object[RoleTurnManager.AllRoles.Length];
            var chipLabels = new Object[RoleTurnManager.AllRoles.Length];
            for (int i = 0; i < chips.Length; i++)
            {
                var role = RoleTurnManager.AllRoles[i];
                var chip = Panel("Chip_" + role, chipRow, UiTheme.PanelLight);
                float a = (float)i / chips.Length, b = (float)(i + 1) / chips.Length;
                chip.anchorMin = new Vector2(a, 0); chip.anchorMax = new Vector2(b, 1); chip.pivot = new Vector2(0.5f, 0.5f);
                chip.offsetMin = new Vector2(i == 0 ? 0 : 4, 0); chip.offsetMax = new Vector2(i == chips.Length - 1 ? 0 : -4, 0);
                chip.gameObject.AddComponent<TooltipTrigger>().staticText = $"{RoleTurnManager.RoleDisplayName(role)}: en verde cuando aprueba el diseño.";
                var label = Text("Label", chip, UiTheme.RoleShort(role), 14, UiTheme.Text, TextAlignmentOptions.Center, bold: true);
                Stretch(label.rectTransform);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                chips[i] = chip.GetComponent<Image>();
                chipLabels[i] = label;
            }

            var approve = MakeButton("ApproveButton", panel, "Aprobar diseño", UiTheme.AccentDim, 18, 0);
            Place((RectTransform)approve.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-236, 12), new Vector2(170, 48));
            var launch = MakeButton("LaunchButton", panel, "<b>LANZAR MISIÓN</b>", UiTheme.Launch, 20, 0);
            Place((RectTransform)launch.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16, 12), new Vector2(210, 48));

            var view = panel.gameObject.AddComponent<ValidationPanel>();
            Wire(view, "roles", roles);
            Wire(view, "statusText", status);
            Wire(view, "issuesText", issues);
            Wire(view, "consensusText", consensus);
            WireArray(view, "consensusChips", chips);
            WireArray(view, "consensusLabels", chipLabels);
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
