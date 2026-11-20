using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MissionDesign.Characters;
using MissionDesign.Core;
using MissionDesign.Exploration;
using MissionDesign.UI;
using MissionDesign.UI.Visual3D;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MissionDesign.Editor
{
    /// <summary>
    /// Construye la escena caminable "SpaceCenter": campus, Centro de Control (vestíbulo + sala de control),
    /// Edificio de Ensamblaje con sala limpia y SLS apilado, y plataforma de lanzamiento, todo con colisiones.
    /// Configura el personaje (avatar Avaturn + animaciones Mixamo como Humanoid) y su Animator.
    /// Lee las anclas de los modelos de Blender: Door_X/Y_*, Interact_*, Sign_*, StackMount, SpawnPoint.
    /// Batch: -executeMethod MissionDesign.Editor.MainSceneBuilder.BuildSpaceCenter
    /// </summary>
    public static partial class MainSceneBuilder
    {
        public const string SpaceCenterScenePath = "Assets/_Project/Scenes/SpaceCenter.unity";
        private const string CharactersRoot = "Assets/_Project/Art/Characters";
        private const string PlayerModelPath = CharactersRoot + "/Player_Ken.fbx";
        private const string AnimatorPath = CharactersRoot + "/Player.controller";
        private const int IgnoreRaycastLayer = 2;

        // Misma disposición que la lámina 1 (Blender: Y-160 / Y-420 → Unity: Z+160 / Z+420).
        private static readonly Vector3 AssemblyOffset = new Vector3(0f, 0f, 160f);
        private static readonly Vector3 PadOffset = new Vector3(0f, 2f, 420f);

        private static readonly Dictionary<string, (InteractionKind kind, string prompt, bool offerDesign)> InteractionTable =
            new Dictionary<string, (InteractionKind, string, bool)>
            {
                { "DesignConsole", (InteractionKind.OpenMissionDesign, "Sentarte en la consola de diseño de misión", true) },
                { "Plans", (InteractionKind.PlansViewer, "Ver los planos del centro espacial", false) },
                { "Reception", (InteractionKind.Info, "Recepción: cómo funciona el centro espacial", false) },
                { "AssemblyKiosk", (InteractionKind.Info, "Leer sobre la sala limpia y la cápsula Orion", false) },
                { "StackInfo", (InteractionKind.Info, "Ver cómo se apila el cohete SLS", false) },
                { "LaunchPanel", (InteractionKind.Info, "Consultar la trayectoria y las fotos de Artemis II", true) },
            };

        private static readonly Dictionary<string, float> SignYaw = new Dictionary<string, float>
        {
            { "CentroDeControl", 180f }, { "SalaLimpia", 180f }, { "EdificioDeEnsamblaje", 0f }, { "PlataformaDeLanzamiento", 0f },
        };

        /// <summary>Base de datos + escena de diseño + centro espacial, en un paso (batch).</summary>
        [MenuItem("NASA SpaceApps/Build Everything (DB + scenes)")]
        public static void BuildEverything()
        {
            NasaDatabaseGenerator.Generate();
            DemoDesigns.Generate();
            GmatValidation.Export();
            Build();
            BuildSpaceCenter();
        }

        [MenuItem("NASA SpaceApps/Build Space Center Scene")]
        public static void BuildSpaceCenter()
        {
            if (File.Exists(SpaceCenterScenePath) && !Application.isBatchMode &&
                !EditorUtility.DisplayDialog("Reconstruir escena", $"{SpaceCenterScenePath} se sobrescribirá. ¿Continuar?", "Reconstruir", "Cancelar"))
                return;

            EnsureUrp();
            var controller = SetupCharacter();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Recargar DESPUÉS de NewScene: el modo Single descarga los assets no referenciados.
            controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorPath);

            BuildSky();
            var env = new GameObject("Environment").transform;
            var campus = Instantiate(ModelLinker.Load("Campus", "Campus_Site"), env, Vector3.zero);
            var room = Instantiate(ModelLinker.Load("ControlRoom", "ControlRoom"), env, Vector3.zero);
            var shell = Instantiate(ModelLinker.Load("Campus", "ControlCenter_Shell"), env, Vector3.zero);
            var vab = Instantiate(ModelLinker.Load("Campus", "AssemblyBuilding"), env, AssemblyOffset);
            var pad = Instantiate(ModelLinker.Load("LaunchPad", "LaunchPad"), env, PadOffset);
            if (pad != null)
                foreach (var t in pad.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Ground"))
                    t.gameObject.SetActive(false);   // el terreno lo pone el campus

            var sls = ModelLinker.Load("Rockets", "LV_SLS_Block1");
            var stackMount = Find(vab, "StackMount");
            if (stackMount != null) Instantiate(sls, env, stackMount.position);
            if (pad != null) Instantiate(sls, env, pad.transform.position + Vector3.up * 1.5f);

            foreach (var root in new[] { campus, room, shell, vab, pad }) AddColliders(root);
            foreach (Transform t in env) if (t.name.StartsWith("LV_")) AddColliders(t.gameObject);

            var all = env.GetComponentsInChildren<Transform>(true).ToList();
            var glass = CreateLitMaterial("MAT_DoorGlass", new Color(0.32f, 0.45f, 0.55f));
            var board = CreateLitMaterial("MAT_SignBoard", new Color(0.05f, 0.09f, 0.18f));
            var marker = CreateLitMaterial("MAT_InteractMarker", new Color(0.25f, 0.75f, 1f));
            marker.EnableKeyword("_EMISSION");
            marker.SetColor("_EmissionColor", new Color(0.2f, 0.65f, 1f) * 2.2f);
            EditorUtility.SetDirty(marker);

            var props = new GameObject("Gameplay").transform;
            BuildControlRoomCrew(room, props, controller, all);
            foreach (var t in all.Where(t => t.name.StartsWith("Door_X_") || t.name.StartsWith("Door_Y_"))) BuildDoor(t, props, glass);
            foreach (var t in all.Where(t => t.name.StartsWith("Sign_"))) BuildSign(t, props, board);
            foreach (var t in all.Where(t => t.name.StartsWith("Interact_"))) BuildInteractable(t, props, marker);
            BuildZones(props);
            BuildElevator(props, marker);
            BuildInteriorLights(props);

            var spawn = all.FirstOrDefault(t => t.name == "SpawnPoint");
            Vector3 spawnPos = spawn != null ? spawn.position : new Vector3(0f, 0f, 32f);
            var travel = BuildTravelPoints(props, spawnPos);
            var (player, camera) = BuildPlayer(controller, spawnPos + Vector3.up * 0.1f, 180f);
            BuildExplorationHud(camera, player, travel);
            var hudComp = Object.FindAnyObjectByType<ExplorationHud>();
            BuildMinimap((RectTransform)hudComp.transform, hudComp);
            hudComp.gameObject.AddComponent<AudioSource>();
            hudComp.gameObject.AddComponent<ExplorationSfx>();
            var playerAnimator = player.GetComponentInChildren<Animator>();
            if (playerAnimator != null)
            {
                player.gameObject.AddComponent<CrewPoser>().Configure(playerAnimator, null, false, 0);
                player.gameObject.AddComponent<FootPlanter>().Configure(playerAnimator);
                var outfit = player.gameObject.AddComponent<OutfitController>();
                outfit.Configure(playerAnimator, new Color(0.62f, 0.78f, 1f), Color.white, Color.white, withHeadset: false, Outfit.Engineer);
                Wire(hudComp, "playerOutfit", outfit);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(SpaceCenterScenePath) ?? "Assets");
            EditorSceneManager.SaveScene(scene, SpaceCenterScenePath);
            RegisterScenes();
            AssetDatabase.SaveAssets();
            Debug.Log($"[SpaceCenter] Escena creada: {SpaceCenterScenePath} · {all.Count(t => t.name.StartsWith("Interact_"))} interacciones · " +
                      $"{all.Count(t => t.name.StartsWith("Door_"))} puertas · {all.Count(t => t.name.StartsWith("Sign_"))} rótulos");
        }

        /// <summary>Diagnóstico: qué colisiona con el personaje alrededor del punto de inicio y en el suelo.</summary>
        [MenuItem("NASA SpaceApps/Diagnose Space Center Spawn")]
        public static void DiagnoseSpawn()
        {
            EditorSceneManager.OpenScene(SpaceCenterScenePath);
            Physics.SyncTransforms();
            var player = Object.FindAnyObjectByType<PlayerController>();
            var cc = player.GetComponent<CharacterController>();
            Vector3 p = player.transform.position;
            Vector3 a = p + Vector3.up * cc.radius + Vector3.up * 0.05f, b = p + Vector3.up * (cc.height - cc.radius);
            foreach (var c in Physics.OverlapCapsule(a, b, cc.radius))
                Debug.Log($"[Diag] Solapa en el inicio: {c.name} ({c.GetType().Name}) bounds {c.bounds}");
            foreach (var dir in new[] { Vector3.back, Vector3.forward, Vector3.left, Vector3.right })
                if (Physics.CapsuleCast(a, b, cc.radius * 0.95f, dir, out var hit, 15f, ~(1 << 2)))
                    Debug.Log($"[Diag] {dir}: choca con {hit.collider.name} a {hit.distance:F2} m (bounds {hit.collider.bounds})");
                else Debug.Log($"[Diag] {dir}: libre 15 m");
            if (Physics.Raycast(p + Vector3.up, Vector3.down, out var g, 5f))
                Debug.Log($"[Diag] Suelo: {g.collider.name} a y = {g.point.y:F3}");
        }

        // ───────────────────────────── PERSONAJE ─────────────────────────────

        /// <summary>Avatar Humanoid + clips Mixamo (en el sitio, Idle/Walk/Run en bucle) y Animator Locomotion/Jump.</summary>
        private static RuntimeAnimatorController SetupCharacter()
        {
            if (AssetImporter.GetAtPath(PlayerModelPath) is ModelImporter mi)
            {
                bool dirty = false;
                if (mi.animationType != ModelImporterAnimationType.Human) { mi.animationType = ModelImporterAnimationType.Human; mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; dirty = true; }
                if (mi.importAnimation) { mi.importAnimation = false; dirty = true; }
                if (mi.importCameras || mi.importLights) { mi.importCameras = mi.importLights = false; dirty = true; }
                if (mi.materialImportMode != ModelImporterMaterialImportMode.ImportViaMaterialDescription) { mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription; dirty = true; }
                if (dirty) mi.SaveAndReimport();
            }
            else Debug.LogWarning($"[SpaceCenter] Falta {PlayerModelPath}: se usará una cápsula.");

            var clips = new Dictionary<string, AnimationClip>();
            foreach (var (name, loop) in new[] { ("Idle", true), ("Walk", true), ("Run", true), ("Jump", false) })
            {
                string path = $"{CharactersRoot}/Animations/{name}.fbx";
                if (!(AssetImporter.GetAtPath(path) is ModelImporter ai)) { Debug.LogWarning($"[SpaceCenter] Falta {path}"); continue; }
                if (ai.animationType != ModelImporterAnimationType.Human || !ai.importAnimation)
                {
                    ai.animationType = ModelImporterAnimationType.Human;
                    ai.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    ai.importAnimation = true;
                    ai.importCameras = ai.importLights = false;
                    ai.SaveAndReimport();
                }
                var defaults = ai.defaultClipAnimations;
                if (defaults.Length > 0)
                {
                    var c = defaults[0];
                    c.name = name;
                    c.loopTime = loop;
                    c.lockRootRotation = c.keepOriginalOrientation = true;
                    // Altura de la raíz "basada en los pies": el personaje apoya las suelas en el suelo (no flota).
                    c.lockRootHeightY = true;
                    c.keepOriginalPositionY = false;
                    c.heightFromFeet = true;
                    c.lockRootPositionXZ = c.keepOriginalPositionXZ = true;
                    var existing = ai.clipAnimations;
                    bool same = existing.Length == 1 && existing[0].name == name && existing[0].loopTime == loop && existing[0].lockRootPositionXZ && existing[0].heightFromFeet;
                    if (!same) { ai.clipAnimations = new[] { c }; ai.SaveAndReimport(); }
                }
                var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(x => !x.name.StartsWith("__preview"));
                if (clip != null) clips[name] = clip;
            }

            AssetDatabase.DeleteAsset(AnimatorPath);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(AnimatorPath);
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            var sm = ac.layers[0].stateMachine;
            var loco = ac.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            if (clips.TryGetValue("Idle", out var idle)) tree.AddChild(idle, 0f);
            if (clips.TryGetValue("Walk", out var walk)) tree.AddChild(walk, 1f);
            if (clips.TryGetValue("Run", out var run)) tree.AddChild(run, 2f);
            sm.defaultState = loco;
            if (clips.TryGetValue("Jump", out var jump))
            {
                var js = sm.AddState("Jump");
                js.motion = jump;
                var toJump = sm.AddAnyStateTransition(js);
                toJump.AddCondition(AnimatorConditionMode.If, 0f, "Jump");
                toJump.hasExitTime = false;
                toJump.duration = 0.1f;
                toJump.canTransitionToSelf = false;
                var back = js.AddTransition(loco);
                back.hasExitTime = true;
                back.exitTime = 0.75f;
                back.duration = 0.25f;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[SpaceCenter] Animator con {clips.Count} clips: {string.Join(", ", clips.Keys)}");
            return ac;
        }

        private static (PlayerController player, Camera camera) BuildPlayer(RuntimeAnimatorController controller, Vector3 position, float yaw)
        {
            var go = new GameObject("Player") { layer = IgnoreRaycastLayer };
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.32f;
            cc.center = new Vector3(0f, 0.9f + 0.04f, 0f);   // base de la cápsula = suelo + skinWidth
            cc.stepOffset = 0.45f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.04f;
            cc.minMoveDistance = 0f;   // a muchos FPS el paso por frame es < 1 mm y se descartaría

            Animator animator = null;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModelPath);
            if (model != null)
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, go.transform);
                visual.name = "Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                NormalizeHeight(visual, 1.76f);
                foreach (var t in visual.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = IgnoreRaycastLayer;
                animator = visual.GetComponent<Animator>() ?? visual.AddComponent<Animator>();
                animator.avatar = AssetDatabase.LoadAllAssetsAtPath(PlayerModelPath).OfType<Avatar>().FirstOrDefault();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                foreach (var smr in visual.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = true;
            }
            else
            {
                var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Object.DestroyImmediate(capsule.GetComponent<Collider>());
                capsule.transform.SetParent(go.transform, false);
                capsule.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                capsule.layer = IgnoreRaycastLayer;
            }

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 3000f;
            cam.clearFlags = CameraClearFlags.Skybox;
            camGo.AddComponent<AudioListener>();
            var rig = camGo.AddComponent<ThirdPersonCamera>();
            Wire(rig, "target", go.transform);
            SetFloat(rig, "yaw", yaw);

            var pc = go.AddComponent<PlayerController>();
            Wire(pc, "animator", animator);
            Wire(pc, "cameraTransform", camGo.transform);
            camGo.transform.SetPositionAndRotation(position + Quaternion.Euler(14f, yaw, 0f) * new Vector3(0f, 0f, -4.2f) + Vector3.up * 1.55f,
                Quaternion.Euler(14f, yaw, 0f));
            return (pc, cam);
        }

        /// <summary>Escala el modelo a la altura indicada y apoya los pies en y = 0 (el FBX puede venir en cm).</summary>
        private static void NormalizeHeight(GameObject visual, float height)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            Debug.Log($"[SpaceCenter] Personaje: bounds {b.size} (min y {b.min.y:F2})");
            if (b.size.y < 0.01f) return;
            if (b.size.y < 1.4f || b.size.y > 2.2f)
            {
                float k = height / b.size.y;
                visual.transform.localScale *= k;
                b = new Bounds(visual.transform.parent.position + (b.center - visual.transform.parent.position) * k, b.size * k);
            }
            visual.transform.position += Vector3.up * (visual.transform.parent.position.y - b.min.y);
        }

        // ───────────────────────────── ENTORNO ─────────────────────────────

        private static void BuildSky()
        {
            var sun = new GameObject("Sun (Directional Light)").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            sun.intensity = 1.5f;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.shadows = LightShadows.Soft;

            string skyPath = "Assets/_Project/Art/Materials/MAT_Sky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                sky.SetFloat("_AtmosphereThickness", 0.9f);
                sky.SetFloat("_Exposure", 1.2f);
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.7f, 0.84f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.52f, 0.55f);
            RenderSettings.ambientGroundColor = new Color(0.28f, 0.27f, 0.25f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.72f, 0.78f, 0.86f);
            RenderSettings.fogStartDistance = 350f;
            RenderSettings.fogEndDistance = 1800f;
        }

        private static void AddColliders(GameObject root)
        {
            if (root == null) return;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || mf.GetComponent<Collider>() != null) continue;
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            }
        }

        private static Transform Find(GameObject root, string name) =>
            root == null ? null : root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        private static void BuildDoor(Transform anchor, Transform parent, Material glass)
        {
            bool alongX = anchor.name.StartsWith("Door_X_");
            float width = 3f;
            float height = Mathf.Clamp((anchor.position.y - anchor.root.position.y) * 2f, 2.4f, 4f);
            if (anchor.position.y < 0.5f) height = 3f;
            var door = new GameObject("AutoDoor_" + anchor.name.Substring(7));
            door.transform.SetParent(parent, false);
            door.transform.position = anchor.position;
            Vector3 axis = alongX ? Vector3.right : Vector3.forward;
            Transform Panel(string n, float side)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
                p.name = n;
                p.transform.SetParent(door.transform, false);
                p.transform.localPosition = axis * (side * width * 0.25f);
                p.transform.localScale = alongX ? new Vector3(width * 0.5f, height, 0.1f) : new Vector3(0.1f, height, width * 0.5f);
                p.GetComponent<Renderer>().sharedMaterial = glass;
                return p.transform;
            }
            var left = Panel("Left", -1f);
            var right = Panel("Right", 1f);
            var auto = door.AddComponent<AutoDoor>();
            Wire(auto, "leftPanel", left);
            Wire(auto, "rightPanel", right);
            var so = new SerializedObject(auto);
            so.FindProperty("slideAxis").vector3Value = axis;
            so.FindProperty("slideDistance").floatValue = width * 0.5f - 0.05f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildSign(Transform anchor, Transform parent, Material board)
        {
            string key = anchor.name.Substring(5);
            string label = Regex.Replace(key, "(?<=[a-záéíóú])(?=[A-Z])", " ").ToUpperInvariant();
            float yaw = SignYaw.TryGetValue(key, out var y) ? y : 0f;
            bool big = key.Contains("Ensamblaje") || key.Contains("Lanzamiento");
            var size = big ? new Vector2(26f, 3.2f) : new Vector2(11f, 1.3f);

            var sign = new GameObject("Sign_" + key);
            sign.transform.SetParent(parent, false);
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 towardViewer = rot * Vector3.back;
            sign.transform.SetPositionAndRotation(anchor.position + towardViewer * 0.45f, rot);

            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "Board";
            Object.DestroyImmediate(plate.GetComponent<Collider>());
            plate.transform.SetParent(sign.transform, false);
            plate.transform.localPosition = new Vector3(0f, 0f, 0.12f);
            plate.transform.localScale = new Vector3(size.x + 1f, size.y + 0.6f, 0.15f);
            plate.GetComponent<Renderer>().sharedMaterial = board;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(sign.transform, false);
            var tmp = textGo.AddComponent<TextMeshPro>();
            tmp.text = label;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.92f, 0.96f, 1f);
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 1f;
            tmp.fontSizeMax = 200f;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = size;

            if (key.Contains("Lanzamiento"))
                foreach (float sx in new[] { -size.x * 0.45f, size.x * 0.45f })
                {
                    var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    post.name = "Post";
                    post.transform.SetParent(sign.transform, false);
                    float h = anchor.position.y - 2f;
                    post.transform.localPosition = new Vector3(sx, -h * 0.5f, 0.12f);
                    post.transform.localScale = new Vector3(0.4f, h, 0.4f);
                    post.GetComponent<Renderer>().sharedMaterial = board;
                }
        }

        private static void BuildInteractable(Transform anchor, Transform parent, Material markerMat)
        {
            string id = anchor.name.Substring(9);
            var go = new GameObject("Interact_" + id);
            go.transform.SetParent(parent, false);
            go.transform.position = anchor.position;
            var it = go.AddComponent<Interactable>();
            it.interactionId = id;
            if (InteractionTable.TryGetValue(id, out var row))
            {
                it.kind = row.kind;
                it.prompt = row.prompt;
                it.offerMissionDesign = row.offerDesign;
            }
            else it.prompt = id;

            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Marker";
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.transform.SetParent(go.transform, false);
            marker.transform.localPosition = new Vector3(0f, 2.3f, 0f);
            marker.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            marker.transform.localScale = Vector3.one * 0.22f;
            marker.GetComponent<Renderer>().sharedMaterial = markerMat;
            marker.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var spin = marker.AddComponent<SlowSpin>();
            var so = new SerializedObject(spin);
            so.FindProperty("degreesPerSecond").vector3Value = new Vector3(0f, 90f, 0f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildZones(Transform parent)
        {
            void Zone(string name, Vector3 center, Vector3 size)
            {
                var go = new GameObject("Zone_" + name);
                go.transform.SetParent(parent, false);
                go.transform.position = center;
                var z = go.AddComponent<ZoneVolume>();
                z.zoneName = name;
                z.size = size;
            }
            Zone("Centro de Control · Vestíbulo", new Vector3(0f, 3f, 15f), new Vector3(28f, 8f, 10f));
            Zone("Centro de Control · Sala de control de misión", new Vector3(0f, 4f, -1f), new Vector3(28f, 10f, 22f));
            Zone("Explanada de acceso", new Vector3(0f, 5f, 32f), new Vector3(60f, 20f, 24f));
            Zone("Carretera de servicio", new Vector3(0f, 5f, 88f), new Vector3(30f, 20f, 88f));
            Zone("Edificio de Ensamblaje", AssemblyOffset + new Vector3(0f, 30f, 0f), new Vector3(64f, 62f, 56f));
            Zone("Edificio de Ensamblaje · Sala limpia", AssemblyOffset + new Vector3(20f, 5f, -16f), new Vector3(15f, 12f, 15f));
            Zone("Camino de orugas", new Vector3(0f, 5f, 260f), new Vector3(44f, 20f, 140f));
            Zone("Plataforma de lanzamiento", PadOffset + new Vector3(0f, 40f, -10f), new Vector3(96f, 100f, 140f));
            Zone("Plataforma · Brazo de acceso de la tripulación", PadOffset + new Vector3(6f, 76f, 1f), new Vector3(14f, 6f, 8f));
        }

        private static void BuildInteriorLights(Transform parent)
        {
            void Point(string name, Vector3 pos, float range, float intensity, Color color)
            {
                var l = new GameObject(name).AddComponent<Light>();
                l.transform.SetParent(parent, false);
                l.transform.position = pos;
                l.type = LightType.Point;
                l.range = range;
                l.intensity = intensity;
                l.color = color;
                l.shadows = LightShadows.None;
            }
            var warm = new Color(1f, 0.93f, 0.82f);
            Point("Light_Lobby", new Vector3(0f, 4.2f, 15f), 18f, 2.2f, warm);
            Point("Light_Room_Front", new Vector3(0f, 6f, -6f), 22f, 2.4f, new Color(0.85f, 0.92f, 1f));
            Point("Light_Room_Back", new Vector3(0f, 6f, 5f), 20f, 2.0f, new Color(0.85f, 0.92f, 1f));
            Point("Light_CleanRoom", AssemblyOffset + new Vector3(20f, 8f, -16f), 18f, 2.5f, new Color(0.9f, 0.95f, 1f));
            Point("Light_Assembly_A", AssemblyOffset + new Vector3(0f, 30f, 6f), 60f, 2.0f, warm);
            Point("Light_Assembly_B", AssemblyOffset + new Vector3(-18f, 12f, 14f), 30f, 1.6f, warm);
        }

        private static Transform[] BuildTravelPoints(Transform parent, Vector3 spawn)
        {
            Transform Point(string name, Vector3 pos, float yaw)
            {
                var t = new GameObject("Travel_" + name).transform;
                t.SetParent(parent, false);
                t.SetPositionAndRotation(pos + Vector3.up * 0.1f, Quaternion.Euler(0f, yaw, 0f));
                return t;
            }
            return new[]
            {
                Point("Entrada", spawn, 180f),
                Point("SalaDeControl", new Vector3(0f, 0f, 8f), 180f),
                Point("Ensamblaje", AssemblyOffset + new Vector3(-10f, 0f, -36f), 0f),
                Point("Plataforma", PadOffset + new Vector3(0f, -2f, -100f), 0f),   // al pie de la rampa
            };
        }

        /// <summary>
        /// Ascensor de la torre de servicio hasta el brazo de acceso de la tripulación (75 m), con pasarela y barandas.
        /// Coordenadas de la plataforma (Blender → Unity): x_U = −x_B, z_U = 420 − y_B, y_U = z_B + 2.
        /// </summary>
        private static void BuildElevator(Transform parent, Material markerMat)
        {
            var steel = CreateLitMaterial("MAT_ElevatorSteel", new Color(0.55f, 0.57f, 0.6f));
            var yellow = CreateLitMaterial("MAT_ElevatorYellow", new Color(0.95f, 0.75f, 0.1f));
            var root = new GameObject("ServiceTowerElevator").transform;
            root.SetParent(parent, false);
            Vector3 shaft = PadOffset + new Vector3(9f, 0f, 5f);          // Blender (−9, −5) junto a la torre
            const float half = 1.6f, top = 77.3f, bottom = 2f;

            GameObject Box(string name, Vector3 center, Vector3 size, Material mat, Transform p = null, bool collider = true)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = name;
                if (!collider) Object.DestroyImmediate(b.GetComponent<Collider>());
                b.transform.SetParent(p != null ? p : root, true);
                b.transform.position = center;
                b.transform.localScale = size;
                b.GetComponent<Renderer>().sharedMaterial = mat;
                return b;
            }

            // Hueco: cuatro columnas y travesaños cada 8 m.
            foreach (var (sx, sz) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
                Box("ShaftColumn", shaft + new Vector3(sx * (half + 0.25f), (top + 2.5f) * 0.5f, sz * (half + 0.25f)), new Vector3(0.25f, top + 2.5f - bottom + 2f, 0.25f), steel);
            for (float y = bottom + 8f; y < top; y += 8f)
            {
                Box("ShaftRing", shaft + new Vector3(0f, y, half + 0.25f), new Vector3(2f * half + 0.5f, 0.18f, 0.18f), steel, null, false);
                Box("ShaftRing", shaft + new Vector3(half + 0.25f, y, 0f), new Vector3(0.18f, 0.18f, 2f * half + 0.5f), steel, null, false);
                Box("ShaftRing", shaft + new Vector3(-half - 0.25f, y, 0f), new Vector3(0.18f, 0.18f, 2f * half + 0.5f), steel, null, false);
            }

            // Cabina: piso, barandas en tres lados (abierta hacia la pasarela, −Z) y luz.
            var platform = Box("ElevatorPlatform", shaft + new Vector3(0f, bottom - 0.15f, 0f), new Vector3(2f * half, 0.3f, 2f * half), yellow);
            Box("RailBack", shaft + new Vector3(0f, bottom + 0.55f, half - 0.05f), new Vector3(2f * half, 1.1f, 0.08f), steel, platform.transform);
            Box("RailLeft", shaft + new Vector3(-half + 0.05f, bottom + 0.55f, 0f), new Vector3(0.08f, 1.1f, 2f * half), steel, platform.transform);
            Box("RailRight", shaft + new Vector3(half - 0.05f, bottom + 0.55f, 0f), new Vector3(0.08f, 1.1f, 2f * half), steel, platform.transform);
            var lamp = new GameObject("CabLight").AddComponent<Light>();
            lamp.transform.SetParent(platform.transform, false);
            lamp.transform.position = shaft + new Vector3(0f, bottom + 2.4f, 0f);
            lamp.type = LightType.Point; lamp.range = 6f; lamp.intensity = 1.5f; lamp.color = new Color(1f, 0.9f, 0.7f);

            // Pasarela desde el ascensor hasta el brazo (x_U 1,35…11,85, z_U 418,8…421,2, superficie 77,3 m) y barandas.
            Box("Walkway", new Vector3(shaft.x, top - 0.15f, (421.2f + shaft.z - half) * 0.5f), new Vector3(2.4f, 0.3f, shaft.z - half - 421.2f + 0.4f), steel);
            Box("WalkwayRailL", new Vector3(shaft.x - 1.2f, top + 0.55f, (421.2f + shaft.z - half) * 0.5f), new Vector3(0.08f, 1.1f, shaft.z - half - 421.2f), steel);
            Box("WalkwayRailR", new Vector3(shaft.x + 1.2f, top + 0.55f, (421.2f + shaft.z - half) * 0.5f), new Vector3(0.08f, 1.1f, shaft.z - half - 421.2f), steel);
            Box("ArmRailNorth", new Vector3(6.6f, top + 0.55f, 418.8f), new Vector3(10.5f, 1.1f, 0.08f), steel);
            Box("ArmRailSouthA", new Vector3((1.35f + shaft.x - 1.2f) * 0.5f, top + 0.55f, 421.2f), new Vector3(shaft.x - 1.2f - 1.35f, 1.1f, 0.08f), steel);
            Box("ArmRailSouthB", new Vector3((shaft.x + 1.2f + 11.85f) * 0.5f, top + 0.55f, 421.2f), new Vector3(11.85f - shaft.x - 1.2f, 1.1f, 0.08f), steel);

            var elevator = root.gameObject.AddComponent<ElevatorController>();
            Wire(elevator, "platform", platform.transform);
            var so = new SerializedObject(elevator);
            so.FindProperty("bottomSurfaceY").floatValue = bottom;
            so.FindProperty("topSurfaceY").floatValue = top;
            so.ApplyModifiedPropertiesWithoutUndo();

            Interactable Point(string id, Vector3 pos, Transform p, InteractionKind kind, string prompt, float radius)
            {
                var go = new GameObject("Interact_" + id);
                go.transform.SetParent(p, true);
                go.transform.position = pos;
                var it = go.AddComponent<Interactable>();
                it.interactionId = id;
                it.kind = kind;
                it.prompt = prompt;
                it.radius = radius;
                if (kind == InteractionKind.Elevator) it.elevator = elevator;
                var m = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(m.GetComponent<Collider>());
                m.name = "Marker";
                m.transform.SetParent(go.transform, false);
                m.transform.localPosition = new Vector3(0f, 2.3f, 0f);
                m.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
                m.transform.localScale = Vector3.one * 0.22f;
                m.GetComponent<Renderer>().sharedMaterial = markerMat;
                return it;
            }
            Point("ElevatorCab", shaft + new Vector3(0f, bottom, 0f), platform.transform, InteractionKind.Elevator, "Ascensor", 1.5f);
            Point("ElevatorCallBottom", shaft + new Vector3(0f, bottom, -half - 1.6f), root, InteractionKind.Elevator, "Llamar al ascensor", 1.6f);
            Point("ElevatorCallTop", new Vector3(shaft.x, top, 422.4f), root, InteractionKind.Elevator, "Llamar al ascensor", 1.3f);
            Point("CrewArm", new Vector3(5f, top, 420f), root, InteractionKind.Info, "Brazo de acceso de la tripulación: cómo entran los astronautas", 2.4f);
        }

        /// <summary>Operadores tecleando en la sala de control del centro espacial y el círculo de entrada al diseño.</summary>
        private static void BuildControlRoomCrew(GameObject room, Transform parent, RuntimeAnimatorController controller, List<Transform> all)
        {
            if (room == null) return;
            var seats = Seats(room.transform);
            var console = all.FirstOrDefault(t => t.name == "Interact_DesignConsole");
            var screen = Find(room, "Screen_Main");
            var back = Find(room, "CameraSpot_Back");
            Vector3 facing = screen != null && back != null ? screen.position - back.position : Vector3.back;
            Transform designSeat = console != null && seats.Count > 0
                ? seats.OrderBy(s => (s.position - console.position).sqrMagnitude).First()
                : seats.FirstOrDefault();
            if (designSeat != null) BuildDesignRing(parent, designSeat.position, facing);
            DecorateControlRoom(room.transform, room.transform, facing);
            int k = 0;
            for (int i = 0; i < seats.Count; i++)
            {
                if (seats[i] == designSeat || i % 3 != 0) continue;
                SpawnCrew($"Operator_{k}", parent, seats[i].position, facing, k, controller, ambient: true);
                k++;
            }
        }

        // ───────────────────────────── HUD ─────────────────────────────

        private static void BuildExplorationHud(Camera camera, PlayerController player, Transform[] travelPoints)
        {
            var canvasGo = new GameObject("ExplorationCanvas", typeof(RectTransform)) { layer = UiLayer };
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 0.2f;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
            var module = es.AddComponent<InputSystemUIInputModule>();
            try { module.AssignDefaultActions(); }
            catch (System.InvalidOperationException) { /* sin asset asignado el módulo crea las acciones por defecto al iniciar */ }
            var root = (RectTransform)canvasGo.transform;
            var hud = canvasGo.AddComponent<ExplorationHud>();

            // Zona actual + subtítulo.
            var zoneBack = Panel("ZoneBar", root, new Color(0.02f, 0.04f, 0.09f, 0.72f));
            Place(zoneBack, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(760f, 86f));
            var sub = Text("Subtitle", zoneBack, "CENTRO ESPACIAL · " + GameInfo.Title, 15, UiTheme.Accent, TextAlignmentOptions.TopLeft, bold: true);
            Place(sub.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(20f, -10f), new Vector2(-40f, 22f));
            var zone = Text("Zone", zoneBack, "Centro espacial", 28, UiTheme.Text, TextAlignmentOptions.TopLeft, bold: true);
            Place(zone.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(20f, -36f), new Vector2(-40f, 40f));

            // Controles (abajo a la izquierda).
            var hint = Text("Controls", root, "WASD caminar · Shift correr · Espacio saltar · Clic derecho + ratón cámara · Rueda zoom · V 1.ª/3.ª persona · E interactuar · M planos · N mapa · Esc menú",
                16, UiTheme.TextDim, TextAlignmentOptions.BottomLeft);
            Place(hint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(24f, 18f), new Vector2(-48f, 26f));

            // Aviso de interacción.
            var prompt = Panel("Prompt", root, new Color(0.02f, 0.05f, 0.11f, 0.85f));
            Place(prompt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(820f, 64f));
            var promptGroup = prompt.gameObject.AddComponent<CanvasGroup>();
            var promptText = Text("Text", prompt, "", 26, UiTheme.Text, TextAlignmentOptions.Center);
            Stretch(promptText.rectTransform);

            // Panel de información (texto o lámina).
            var info = Panel("InfoPanel", root, new Color(0f, 0f, 0f, 0.7f));
            Stretch(info);
            var infoGroup = info.gameObject.AddComponent<CanvasGroup>();
            var card = Panel("Card", info, new Color(0.04f, 0.06f, 0.11f, 1f));
            Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1300f, 820f));
            var title = Text("Title", card, "", 32, UiTheme.Accent, TextAlignmentOptions.TopLeft, bold: true);
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(-96f, 50f));
            var body = Text("Body", card, "", 23, UiTheme.Text, TextAlignmentOptions.TopLeft);
            body.lineSpacing = 8;
            Place(body.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(-96f, -200f));
            var (image, frame, caption) = InfoImageArea(card);
            var pager = Text("Pager", card, "", 18, UiTheme.TextDim, TextAlignmentOptions.MidlineLeft);
            Place(pager.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-640f, 30f), new Vector2(90f, 54f));
            Button CardButton(string n, string label, Color c, float x, float w)
            {
                var b = MakeButton(n, card, label, c, 18, 0);
                Place((RectTransform)b.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(x, 30f), new Vector2(w, 54f));
                return b;
            }
            var prev = CardButton("PrevButton", "« Anterior", UiTheme.AccentDim, -436f, 170f);
            var next = CardButton("NextButton", "Siguiente »", UiTheme.Accent, -246f, 170f);
            var close = CardButton("CloseButton", "Cerrar (Esc)", UiTheme.PanelLight, -48f, 180f);
            var action = MakeButton("DesignButton", card, "<b>Diseñar la misión »</b>", UiTheme.Launch, 18, 0);
            Place((RectTransform)action.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(48f, 30f), new Vector2(260f, 54f));
            // El pie de lámina ocupa la franja inferior izquierda; el botón de diseño solo aparece en paneles de texto.

            // Menú Esc con viaje rápido.
            var pause = Panel("PauseMenu", root, new Color(0f, 0f, 0f, 0.7f));
            Stretch(pause);
            var pauseGroup = pause.gameObject.AddComponent<CanvasGroup>();
            var pcard = Panel("Card", pause, new Color(0.04f, 0.06f, 0.11f, 1f));
            Place(pcard, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 862f));
            var ptitle = Text("Title", pcard, "<b>CENTRO ESPACIAL</b>", 32, UiTheme.Accent, TextAlignmentOptions.Center);
            Place(ptitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(0f, 46f));
            var col = NewRect("Buttons", pcard);
            Place(col, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -24f), new Vector2(-80f, -120f));
            var vlg = col.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10; vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = true;
            var resume = MakeButton("ResumeButton", col, "<b>Continuar</b>", UiTheme.Accent, 21, 0);
            var travelLabel = Text("TravelLabel", col, "Viaje rápido", 17, UiTheme.TextDim, TextAlignmentOptions.Bottom);
            var travelNames = new[] { "Entrada del Centro de Control", "Sala de control de misión", "Edificio de Ensamblaje", "Plataforma de lanzamiento" };
            var travelButtons = travelNames.Select((n, i) => (Object)MakeButton("Travel" + i, col, n, UiTheme.PanelLight, 19, 0)).ToArray();
            var plansBtn = MakeButton("PlansButton", col, "Planos (M)", UiTheme.AccentDim, 20, 0);
            var controlsBtn = MakeButton("ControlsButton", col, "Controles", UiTheme.AccentDim, 20, 0);
            var optionsBtn = MakeButton("OptionsButton", col, "Opciones", UiTheme.AccentDim, 20, 0);
            var designBtn = MakeButton("DesignButton", col, "<b>Diseñar la misión</b>", UiTheme.Launch, 20, 0);
            var quitBtn = MakeButton("MainMenuButton", col, "Menú principal", UiTheme.PanelLight, 20, 0);
            _ = travelLabel;

            Wire(hud, "player", player);
            Wire(hud, "cameraRig", camera.GetComponent<ThirdPersonCamera>());
            Wire(hud, "promptText", promptText);
            Wire(hud, "zoneText", zone);
            Wire(hud, "promptGroup", promptGroup);
            Wire(hud, "infoPanel", infoGroup);
            Wire(hud, "infoTitle", title);
            Wire(hud, "infoBody", body);
            Wire(hud, "infoCaption", caption);
            Wire(hud, "infoPager", pager);
            Wire(hud, "infoImage", image);
            Wire(hud, "infoImageFrame", frame);
            Wire(hud, "infoPrev", prev);
            Wire(hud, "infoNext", next);
            Wire(hud, "infoClose", close);
            Wire(hud, "infoAction", action);
            Wire(hud, "pauseMenu", pauseGroup);
            Wire(hud, "resumeButton", resume);
            Wire(hud, "designButton", designBtn);
            Wire(hud, "plansButton", plansBtn);
            Wire(hud, "controlsButton", controlsBtn);
            Wire(hud, "optionsButton", optionsBtn);
            Wire(hud, "quitButton", quitBtn);
            WireArray(hud, "travelButtons", travelButtons);
            WireArray(hud, "travelPoints", travelPoints.Cast<Object>().ToArray());
            WireArray(hud, "planTextures", LoadPlanTextures());

            // Avisos (cambio de equipo, entrada a la consola) y fundido a negro.
            var toast = Panel("Toast", root, new Color(0.02f, 0.05f, 0.11f, 0.9f));
            Place(toast, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(900f, 54f));
            var toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            var toastText = Text("Text", toast, "", 21, new Color(1f, 0.88f, 0.5f), TextAlignmentOptions.Center);
            Stretch(toastText.rectTransform);
            var fadeRt = NewRect("Fade", root);
            Stretch(fadeRt);
            var fade = fadeRt.gameObject.AddComponent<Image>();
            fade.color = new Color(0f, 0f, 0f, 0f);
            fade.raycastTarget = false;
            Wire(hud, "toastGroup", toastGroup);
            Wire(hud, "toastText", toastText);
            Wire(hud, "fade", fade);
        }

        private static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
