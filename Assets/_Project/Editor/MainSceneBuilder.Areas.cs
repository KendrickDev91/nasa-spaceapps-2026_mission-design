using System.Collections.Generic;
using MissionDesign.UI;
using MissionDesign.Simulation;
using MissionDesign.UI.Visual3D;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MissionDesign.Editor
{
    /// <summary>
    /// "Sets" 3D de la escena, separados en el espacio para que la cámara salte entre ellos:
    /// plataforma de lanzamiento, vista orbital, sala de control y superficie lunar.
    /// Usa los modelos de ArtSource/Blender (vía ModelLinker) y texturas de TextureFactory.
    /// </summary>
    public static partial class MainSceneBuilder
    {
        private const string MaterialFolder = "Assets/_Project/Art/Materials";
        private static readonly Vector3 LaunchAreaPos = new Vector3(0f, 0f, 5000f);
        private static readonly Vector3 OrbitAreaPos = new Vector3(0f, 0f, 10000f);
        private static readonly Vector3 ControlRoomPos = new Vector3(0f, 0f, 15000f);
        private static readonly Vector3 LunarAreaPos = new Vector3(0f, 0f, 20000f);

        private static void BuildAreas(GameObject systems, Camera camera, ModularSpacecraftAssembler assembler, GameObject room,
            MissionSimulationRunner runner)
        {
            var sky = SaveMaterial("MAT_Skybox_Stars", new Material(Shader.Find("Skybox/Panoramic")));
            sky.SetTexture("_MainTex", TextureFactory.Stars());
            sky.SetFloat("_Exposure", 1.2f);
            RenderSettings.skybox = sky;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.farClipPlane = 3000f;

            var earthMat = TexturedLit("MAT_Earth", TextureFactory.Earth(), 0.3f);
            var moonMat = TexturedLit("MAT_Moon", TextureFactory.Moon(), 0.02f);
            if (TextureFactory.NasaMoonNormal() is Texture2D moonRelief)
            {
                // Relieve real de NASA (LOLA): los cráteres se sombrean con el Sol al acercarse en el sobrevuelo.
                moonMat.SetTexture("_BumpMap", moonRelief);
                moonMat.SetFloat("_BumpScale", 1f);
                moonMat.EnableKeyword("_NORMALMAP");
                EditorUtility.SetDirty(moonMat);
            }
            var designEarth = GameObject.Find("Earth_Placeholder");
            if (designEarth != null)
            {
                designEarth.name = "Earth";
                designEarth.GetComponent<Renderer>().sharedMaterial = earthMat;
            }

            var launch = BuildLaunchArea();
            var screenRt = SaveRenderTexture("RT_OrbitScreen", 1600, 640);   // misma proporción que la pantalla (10 × 4 m)
            var orbit = BuildOrbitArea(earthMat, moonMat, screenRt);
            var roomPose = PlaceControlRoom(room, screenRt);
            var lunar = BuildLunarArea(assembler, earthMat);
            var studio = BuildPhotoStudio(runner, earthMat, lunar != null ? lunar.transform : null);
            BuildSplashdownCinematic(runner);
            var flightHud = Object.FindAnyObjectByType<FlightHudView>();
            if (flightHud != null) { Wire(flightHud, "orbit", orbit); Wire(flightHud, "studio", studio); }
            var report = Object.FindAnyObjectByType<ExplorationReportPanel>();
            if (report != null) Wire(report, "studio", studio);
            Wire(orbit, "runner", runner);
            var screens = Object.FindAnyObjectByType<ControlRoomScreens>();
            if (screens != null) { Wire(screens, "runner", runner); Wire(screens, "orbit", orbit); Wire(screens, "studio", studio); }

            var director = systems.AddComponent<MissionCameraDirector>();
            Wire(director, "mainCamera", camera);
            Wire(director, "launchSite", launch);
            Wire(director, "orbitView", orbit);
            Wire(director, "lunarFlyover", lunar);
            Wire(director, "controlRoomPose", roomPose);
            Wire(director, "reactionPose", room.transform.Find("ControlRoomReactionPose"));
            Wire(director, "wideRoomPose", room.transform.Find("ControlRoomWidePose"));

            var so = new SerializedObject(runner);
            so.FindProperty("secondsPerPhase").floatValue = 4f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ───────────── Plataforma de lanzamiento ─────────────

        private static LaunchSiteController BuildLaunchArea()
        {
            var area = new GameObject("LaunchArea");
            area.transform.position = LaunchAreaPos;
            Instantiate(ModelLinker.Load("LaunchPad", "LaunchPad"), area.transform, Vector3.zero);
            var mount = new GameObject("RocketMount").transform;
            mount.SetParent(area.transform, false);
            mount.localPosition = new Vector3(0f, 1.5f, 0f);

            var flood = new GameObject("PadFloodlight").AddComponent<Light>();
            flood.transform.SetParent(area.transform, false);
            flood.transform.localPosition = new Vector3(-60f, 60f, -90f);
            flood.transform.LookAt(area.transform.position + Vector3.up * 40f);
            flood.type = LightType.Spot;
            flood.range = 400f;
            flood.spotAngle = 50f;
            flood.intensity = 40f;
            flood.color = new Color(1f, 0.95f, 0.85f);

            var site = area.AddComponent<LaunchSiteController>();
            Wire(site, "rocketMount", mount);
            Wire(site, "fallbackRocket", ModelLinker.Load("Rockets", "LV_Falcon9"));
            Wire(site, "exhaustMaterial", ParticleMaterial("MAT_FX_Exhaust", additive: true));
            Wire(site, "smokeMaterial", ParticleMaterial("MAT_FX_Smoke", additive: false));
            return site;
        }

        // ───────────── Vista orbital ─────────────

        private static OrbitViewController BuildOrbitArea(Material earthMat, Material moonMat, RenderTexture screenRt)
        {
            var area = new GameObject("OrbitArea");
            area.transform.position = OrbitAreaPos;
            var view = area.AddComponent<OrbitViewController>();

            var earth = Sphere("Earth", area.transform, Vector3.zero, OrbitViewController.EarthRadius * 2f, earthMat);
            var moon = Sphere("Moon", area.transform, new Vector3(-OrbitViewController.MoonDisplayDistance, 0f, 0f), OrbitViewController.MoonDisplayRadius * 2f, moonMat);
            var marker = Sphere("SpacecraftMarker", area.transform, new Vector3(OrbitViewController.ParkingRadius, 0f, 0f), 0.7f,
                UnlitMaterial("MAT_Orbit_Marker", new Color(1f, 0.95f, 0.6f)));
            var markerLight = marker.gameObject.AddComponent<Light>();
            markerLight.type = LightType.Point; markerLight.range = 6f; markerLight.intensity = 3f; markerLight.color = new Color(1f, 0.9f, 0.5f);

            var sun = new GameObject("OrbitSunLight").AddComponent<Light>();
            sun.transform.SetParent(area.transform, false);
            sun.type = LightType.Point; sun.range = 400f; sun.intensity = 2f;
            sun.transform.localPosition = new Vector3(60f, 40f, -60f);

            var captionGo = new GameObject("Caption");
            captionGo.transform.SetParent(area.transform, false);
            captionGo.transform.localPosition = new Vector3(0f, 9f, 0f);
            var caption = captionGo.AddComponent<TextMeshPro>();
            caption.fontSize = 4.5f;
            caption.alignment = TextAlignmentOptions.Center;
            caption.color = new Color(0.85f, 0.92f, 1f);
            caption.rectTransform.sizeDelta = new Vector2(60f, 6f);
            captionGo.AddComponent<FaceCamera>();

            var screenCamGo = new GameObject("OrbitScreenCamera");
            screenCamGo.transform.SetParent(area.transform, false);
            var screenCam = screenCamGo.AddComponent<Camera>();
            screenCam.targetTexture = screenRt;
            screenCam.clearFlags = CameraClearFlags.Skybox;
            screenCam.fieldOfView = 40f;
            screenCam.farClipPlane = 1500f;

            Wire(view, "earth", earth);
            Wire(view, "moon", moon);
            Wire(view, "spacecraftPrefab", ModelLinker.Load("Spacecraft", "BUS_ORION"));
            Wire(view, "trailMaterial", ParticleMaterial("MAT_FX_Trail", additive: true));
            Wire(view, "parkingLine", Line("ParkingOrbit", area.transform, new Color(0.3f, 0.75f, 1f), 0.12f));
            Wire(view, "transferLine", Line("TransferOrbit", area.transform, new Color(1f, 0.55f, 0.15f), 0.25f));
            Wire(view, "targetLine", Line("TargetOrbit", area.transform, new Color(0.35f, 1f, 0.5f), 0.22f));
            Wire(view, "linkLine", Line("DataLink", area.transform, new Color(1f, 0.95f, 0.3f), 0.15f));
            // Sobrevuelo tripulado: colores del diagrama de Artemis II (ida morado, vuelta naranja).
            var outbound = Line("FreeReturnOutbound", area.transform, new Color(0.62f, 0.45f, 1f), 0.35f);
            var inbound = Line("FreeReturnReturn", area.transform, new Color(1f, 0.62f, 0.15f), 0.35f);
            Wire(view, "outboundLine", outbound);
            Wire(view, "returnLine", inbound);
            Wire(view, "outboundMaterial", outbound.sharedMaterial);
            Wire(view, "returnMaterial", inbound.sharedMaterial);
            Wire(view, "milestoneMaterial", UnlitMaterial("MAT_Orbit_Milestone", Color.white));
            Wire(view, "marker", marker);
            Wire(view, "label", caption);
            Wire(view, "screenCamera", screenCam);
            return view;
        }

        // ───────────── Sala de control ─────────────

        private static Transform PlaceControlRoom(GameObject room, RenderTexture screenRt)
        {
            room.transform.position = ControlRoomPos;
            var model = Instantiate(ModelLinker.Load("ControlRoom", "ControlRoom"), room.transform, Vector3.zero);
            if (model == null) return room.transform;

            var screen = FindDeep(model.transform, "Screen_Main");
            var spotForScreen = FindDeep(model.transform, "CameraSpot_Back");
            if (screen != null && spotForScreen != null)
            {
                // Quad de Unity (UV estándar) delante de la pantalla del modelo: la imagen no sale rotada.
                var sr = screen.GetComponent<Renderer>();
                var b = sr.bounds;
                sr.enabled = false;
                Vector3 toScreen = b.center - spotForScreen.position;
                toScreen.y = 0f;
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "MainScreen_Display";
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.transform.SetParent(room.transform, true);
                quad.transform.rotation = Quaternion.LookRotation(toScreen.normalized, Vector3.up);
                quad.transform.position = b.center - toScreen.normalized * 0.05f;
                quad.transform.localScale = new Vector3(Mathf.Max(b.size.x, b.size.z), b.size.y, 1f);
                quad.GetComponent<Renderer>().sharedMaterial = UnlitMaterial("MAT_ControlRoom_MainScreen", Color.white, screenRt);
            }
            // Pantallas laterales: fondo oscuro con texto en vivo (telemetría · despliegue y bitácora).
            var sideMat = UnlitMaterial("MAT_ControlRoom_SideScreen", new Color(0.02f, 0.05f, 0.11f));
            var screensView = room.AddComponent<ControlRoomScreens>();
            Vector3 screenNormal = screen != null && spotForScreen != null ? screen.position - spotForScreen.position : Vector3.forward;
            screenNormal.y = 0f;
            screenNormal.Normalize();
            foreach (var side in new[] { "Screen_Left", "Screen_Right" })
            {
                var t = FindDeep(model.transform, side);
                if (t == null) continue;
                var r = t.GetComponent<Renderer>();
                r.sharedMaterial = sideMat;
                var bounds = r.bounds;
                var textGo = new GameObject(side + "_Live", typeof(RectTransform));
                textGo.transform.SetParent(room.transform, true);
                textGo.transform.SetPositionAndRotation(bounds.center - screenNormal * 0.08f, Quaternion.LookRotation(screenNormal, Vector3.up));
                var tmp = textGo.AddComponent<TextMeshPro>();
                tmp.rectTransform.sizeDelta = new Vector2(Mathf.Max(bounds.size.x, bounds.size.z) * 0.9f, bounds.size.y * 0.88f);
                tmp.alignment = TextAlignmentOptions.TopLeft;
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 0.05f;
                tmp.fontSizeMax = bounds.size.y * 1.1f;
                tmp.color = new Color(0.86f, 0.92f, 1f);
                tmp.richText = true;
                tmp.text = "";
                Wire(screensView, side == "Screen_Left" ? "leftScreen" : "rightScreen", tmp);
            }

            // Personal de la sala: los 4 roles en la primera grada y más operadores en las otras (avatar con poses procedurales).
            var bridge = room.GetComponent<MissionControlAnimatorBridge>();
            var controller = CrewController();
            Vector3 facing = screen != null && spotForScreen != null ? screen.position - spotForScreen.position : Vector3.forward;
            var bso = new SerializedObject(bridge);
            var opsProp = bso.FindProperty("operators");
            int i = 0;
            var anchors = new List<Transform>();
            foreach (Transform op in room.transform) if (op.name.StartsWith("Operator_")) anchors.Add(op);
            foreach (var op in anchors)
            {
                var seat = FindDeep(model.transform, $"Seat_0_{1 + i * 2}");
                if (seat != null) op.position = seat.position;
                var body = op.Find("Placeholder_Body");
                if (body != null) Object.DestroyImmediate(body.gameObject);
                var oldAnimator = op.GetComponent<Animator>();
                if (oldAnimator != null) Object.DestroyImmediate(oldAnimator);
                var crewAnimator = seat != null ? SpawnCrew($"Crew_{op.name}", op, seat.position, facing, i, controller, ambient: false) : null;
                if (i < opsProp.arraySize) opsProp.GetArrayElementAtIndex(i).FindPropertyRelative("animator").objectReferenceValue = crewAnimator;
                i++;
            }
            var crowd = new List<Object>();
            int crewIndex = 4;
            foreach (var name in new[] { "Seat_1_0", "Seat_1_3", "Seat_1_5", "Seat_1_6", "Seat_2_1", "Seat_2_6" })
            {
                var seat = FindDeep(model.transform, name);
                if (seat == null) continue;
                var a = SpawnCrew($"Crew_{name}", room.transform, seat.position, facing, crewIndex++, controller, ambient: false);
                if (a != null) crowd.Add(a);
            }
            bso.ApplyModifiedPropertiesWithoutUndo();
            WireArray(bridge, "crowd", crowd.ToArray());

            DecorateControlRoom(room.transform, model.transform, facing);

            // Cámara de reacción: frente a las pantallas, mirando a la primera grada.
            var firstRow = FindDeep(model.transform, "Seat_0_3");
            var reaction = new GameObject("ControlRoomReactionPose").transform;
            reaction.SetParent(room.transform, false);
            Vector3 dir = facing; dir.y = 0f; dir.Normalize();
            Vector3 look = (firstRow != null ? firstRow.position : room.transform.position) + Vector3.up * 0.7f + Vector3.Cross(Vector3.up, dir) * 1.2f;
            reaction.position = look + dir * 5.2f + Vector3.up * 1.4f;
            reaction.LookAt(look + Vector3.up * 0.2f);

            for (int k = 0; k < 3; k++)
            {
                var l = new GameObject($"RoomLight{k}").AddComponent<Light>();
                l.transform.SetParent(room.transform, false);
                l.transform.localPosition = new Vector3(-8f + k * 8f, 6f, 0f);
                l.type = LightType.Point; l.range = 22f; l.intensity = 6f; l.color = new Color(0.85f, 0.9f, 1f);
            }

            // Plano general alto desde el fondo: operadores de espaldas y la pantalla grande con la trayectoria en vivo.
            var wide = new GameObject("ControlRoomWidePose").transform;
            wide.SetParent(room.transform, false);
            Vector3 back = spotForScreen != null ? spotForScreen.position : room.transform.position - dir * 9f + Vector3.up * 3f;
            wide.position = back + Vector3.up * 3.2f - dir * 0.6f;
            wide.LookAt((screen != null ? screen.position : room.transform.position + dir * 10f) + Vector3.down * 2.2f);

            // Toma de la sala (transmisión y análisis): desde el costado, para ver a los operadores trabajando
            // y, al fondo, las pantallas con la trayectoria y los datos en vivo.
            var pose = new GameObject("ControlRoomCameraPose").transform;
            pose.SetParent(room.transform, false);
            Vector3 row = firstRow != null ? firstRow.position : room.transform.position;
            Vector3 sideDir = Vector3.Cross(Vector3.up, dir);
            pose.position = row + sideDir * 6.5f + dir * 1.6f + Vector3.up * 2.2f;
            pose.LookAt(row - sideDir * 1.2f + dir * 0.2f + Vector3.up * 1.2f);
            return pose;
        }

        // ───────────── Superficie lunar ─────────────

        private static LunarFlyover BuildLunarArea(ModularSpacecraftAssembler assembler, Material earthMat)
        {
            var area = new GameObject("LunarArea");
            area.transform.position = LunarAreaPos;
            Instantiate(ModelLinker.Load("Moon", "LunarSurface"), area.transform, Vector3.zero);
            Sphere("EarthInSky", area.transform, new Vector3(-260f, 140f, 520f), 70f, earthMat);

            Transform Point(string name, Vector3 local)
            {
                var t = new GameObject(name).transform;
                t.SetParent(area.transform, false);
                t.localPosition = local;
                return t;
            }

            var flyover = area.AddComponent<LunarFlyover>();
            Wire(flyover, "assembler", assembler);
            Wire(flyover, "cameraPose", Point("CameraPose", new Vector3(14f, 3.5f, -38f)));
            Wire(flyover, "flyStart", Point("FlyStart", new Vector3(-90f, 38f, 70f)));
            Wire(flyover, "flyEnd", Point("FlyEnd", new Vector3(90f, 46f, 70f)));
            return flyover;
        }

        // ───────────── Utilidades de materiales y objetos ─────────────

        private static GameObject Instantiate(GameObject prefab, Transform parent, Vector3 localPosition)
        {
            if (prefab == null)
            {
                Debug.LogWarning("[SceneBuilder] Falta un modelo: ejecuta ArtSource/Blender/build_all.ps1 y el generador de la base de datos.");
                return null;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = localPosition;
            return go;
        }

        private static Transform Sphere(string name, Transform parent, Vector3 localPos, float diameter, Material mat)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.name = name;
            Object.DestroyImmediate(s.GetComponent<Collider>());
            s.transform.SetParent(parent, false);
            s.transform.localPosition = localPos;
            s.transform.localScale = Vector3.one * diameter;
            s.GetComponent<Renderer>().sharedMaterial = mat;
            return s.transform;
        }

        private static LineRenderer Line(string name, Transform parent, Color color, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = UnlitMaterial($"MAT_Line_{name}", color);
            lr.widthMultiplier = width;
            lr.useWorldSpace = false;
            lr.loop = false;
            lr.positionCount = 0;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            return lr;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        private static Material TexturedLit(string name, Texture texture, float smoothness)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetTexture("_BaseMap", texture);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", smoothness);
            return SaveMaterial(name, m);
        }

        private static Material UnlitMaterial(string name, Color color, Texture texture = null)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", color);
            if (texture != null) m.SetTexture("_BaseMap", texture);
            return SaveMaterial(name, m);
        }

        private static Material ParticleMaterial(string name, bool additive)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            m.SetTexture("_BaseMap", TextureFactory.SoftDot());
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            return SaveMaterial(name, m);
        }

        /// <summary>Crea o actualiza (conservando el GUID) un material en Assets/_Project/Art/Materials.</summary>
        private static Material SaveMaterial(string name, Material m)
        {
            System.IO.Directory.CreateDirectory(MaterialFolder);
            string path = $"{MaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(m, path);
                return m;
            }
            existing.shader = m.shader;
            existing.CopyPropertiesFromMaterial(m);
            existing.shaderKeywords = m.shaderKeywords;
            existing.renderQueue = m.renderQueue;
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static RenderTexture SaveRenderTexture(string name, int w, int h)
        {
            string path = $"Assets/_Project/Art/Textures/{name}.renderTexture";
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
            if (rt != null)
            {
                if (rt.width != w || rt.height != h)
                {
                    rt.Release();
                    rt.width = w;
                    rt.height = h;
                    EditorUtility.SetDirty(rt);
                }
                return rt;
            }
            System.IO.Directory.CreateDirectory("Assets/_Project/Art/Textures");
            rt = new RenderTexture(w, h, 24) { name = name };
            AssetDatabase.CreateAsset(rt, path);
            return rt;
        }
    }
}
