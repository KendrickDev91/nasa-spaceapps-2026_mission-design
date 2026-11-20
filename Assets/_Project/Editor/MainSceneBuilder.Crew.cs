using System.Collections.Generic;
using System.Linq;
using MissionDesign.Characters;
using MissionDesign.Core;
using MissionDesign.Exploration;
using MissionDesign.Simulation;
using MissionDesign.UI;
using MissionDesign.UI.Visual3D;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace MissionDesign.Editor
{
    /// <summary>
    /// Personal de la sala de control (avatar con poses procedurales y vestuario), estudio de fotografía lunar,
    /// HUD de vuelo, informe de exploración, círculo de entrada al diseño y mapa geolocalizador.
    /// </summary>
    public static partial class MainSceneBuilder
    {
        private static readonly Vector3 PhotoStudioPos = new Vector3(0f, 0f, 25000f);

        private static readonly Color[] ShirtPalette =
        {
            Color.white, new Color(0.62f, 0.78f, 1f), new Color(0.36f, 0.44f, 0.72f), new Color(0.78f, 0.78f, 0.8f),
            new Color(0.55f, 0.85f, 0.8f), new Color(1f, 0.9f, 0.72f), new Color(0.85f, 0.6f, 0.6f), new Color(0.7f, 0.9f, 0.6f)
        };
        private static readonly Color[] SkinPalette =
            { Color.white, new Color(0.86f, 0.76f, 0.68f), new Color(0.68f, 0.54f, 0.45f), new Color(1f, 0.94f, 0.9f) };
        private static readonly Color[] HairPalette =
            { Color.white, new Color(0.75f, 0.55f, 0.35f), new Color(0.6f, 0.6f, 0.6f), new Color(1f, 0.85f, 0.6f) };

        private static RuntimeAnimatorController CrewController()
        {
            var c = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorPath);
            return c != null ? c : SetupCharacter();
        }

        /// <summary>Operador sentado en <paramref name="seat"/> mirando hacia <paramref name="facing"/>.</summary>
        private static Animator SpawnCrew(string name, Transform parent, Vector3 seatPoint, Vector3 facing, int index,
                                          RuntimeAnimatorController controller, bool ambient)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModelPath);
            if (model == null) return null;
            facing.y = 0f;
            if (facing.sqrMagnitude < 1e-4f) facing = Vector3.forward;
            var root = new GameObject(name);
            root.transform.SetParent(parent, true);
            root.transform.SetPositionAndRotation(new Vector3(seatPoint.x, seatPoint.y - 0.5f, seatPoint.z), Quaternion.LookRotation(facing.normalized));
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            var animator = visual.GetComponent<Animator>() ?? visual.AddComponent<Animator>();
            animator.avatar = AssetDatabase.LoadAllAssetsAtPath(PlayerModelPath).OfType<Avatar>().FirstOrDefault();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var seat = new GameObject("SeatPoint").transform;
            seat.SetParent(root.transform, true);
            seat.position = seatPoint;
            var poser = root.AddComponent<CrewPoser>();
            poser.Configure(animator, seat, true, index);
            if (ambient) SetBool(poser, "ambient", true);
            var outfit = root.AddComponent<OutfitController>();
            outfit.Configure(animator, ShirtPalette[index % ShirtPalette.Length], HairPalette[(index / 2) % HairPalette.Length],
                SkinPalette[(index * 3 + 1) % SkinPalette.Length], withHeadset: true, Outfit.Engineer);
            return animator;
        }

        private static void SetBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) return;
            p.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Viste la sala de control: un panel (telemetría, traza terrestre, estado o trayectoria) en cada monitor de los
        /// operadores y en las pantallas de las paredes, y la fila de relojes de misión con texto en vivo.
        /// </summary>
        private static void DecorateControlRoom(Transform room, Transform model, Vector3 toScreen)
        {
            toScreen.y = 0f;
            toScreen.Normalize();
            var dash = new Material[4];
            for (int v = 0; v < 4; v++) dash[v] = UnlitMaterial($"MAT_Dashboard{v}", Color.white, TextureFactory.Dashboard(v));
            Quaternion facing = Quaternion.LookRotation(toScreen, Vector3.up);

            GameObject Screen(string name, Bounds b, Quaternion rot, Vector3 offset, Material mat)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = name;
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.transform.SetParent(room, true);
                q.transform.SetPositionAndRotation(b.center + offset, rot);
                Vector3 local = Quaternion.Inverse(rot) * b.size;
                q.transform.localScale = new Vector3(Mathf.Abs(local.x) * 0.94f, Mathf.Abs(local.y) * 0.9f, 1f);
                var r = q.GetComponent<Renderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                return q;
            }

            int k = 0;
            foreach (var t in model.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Monitor_")).OrderBy(t => t.name))
            {
                var rend = t.GetComponent<Renderer>();
                if (rend == null) continue;
                Screen(t.name + "_Display", rend.bounds, facing, -toScreen * 0.035f, dash[(k * 7 + 3) % 4]);
                k++;
            }
            foreach (var (name, v) in new[] { ("Screen_Wall_L", 1), ("Screen_Wall_R", 0) })
            {
                var t = FindDeep(model, name);
                if (t == null) continue;
                var b = t.GetComponent<Renderer>().bounds;
                Vector3 inward = room.position - b.center; inward.y = 0f; inward.Normalize();
                Screen(name + "_Display", b, Quaternion.LookRotation(-inward, Vector3.up), inward * 0.05f, dash[v]);
            }

            var clockFace = UnlitMaterial("MAT_ClockFace", new Color(0.03f, 0.02f, 0.02f));   // LED: cara oscura, dígitos ámbar
            var clocks = new List<Object>();
            string[] labels = { "UTC", "FLORIDA", "TIEMPO DE MISIÓN", "CUENTA", "DISTANCIA", "RED DSN" };
            for (int c = 0; c < 6; c++)
            {
                var t = FindDeep(model, $"MissionClock{c}");
                if (t == null) continue;
                var clockRenderer = t.GetComponent<Renderer>();
                clockRenderer.sharedMaterial = clockFace;
                var b = clockRenderer.bounds;
                var go = new GameObject($"MissionClock{c}_Text", typeof(RectTransform));
                go.transform.SetParent(room, true);
                go.transform.SetPositionAndRotation(b.center - toScreen * 0.08f, facing);
                var tmp = go.AddComponent<TextMeshPro>();
                tmp.rectTransform.sizeDelta = new Vector2(Mathf.Max(b.size.x, b.size.z) * 0.96f, b.size.y * 0.9f);
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.enableAutoSizing = true; tmp.fontSizeMin = 0.05f; tmp.fontSizeMax = 3f;
                tmp.color = new Color(1f, 0.62f, 0.18f);
                tmp.fontStyle = FontStyles.Bold;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.text = "00:00:00";
                clocks.Add(tmp);
                var label = new GameObject($"MissionClock{c}_Label", typeof(RectTransform));
                label.transform.SetParent(room, true);
                label.transform.SetPositionAndRotation(b.center - toScreen * 0.08f + Vector3.up * 0.34f, facing);
                var lt = label.AddComponent<TextMeshPro>();
                lt.rectTransform.sizeDelta = new Vector2(Mathf.Max(b.size.x, b.size.z), 0.2f);
                lt.alignment = TextAlignmentOptions.Center;
                lt.enableAutoSizing = true; lt.fontSizeMin = 0.05f; lt.fontSizeMax = 1.6f;
                lt.color = new Color(0.65f, 0.75f, 0.9f);
                lt.text = labels[c];
            }
            var ambience = room.gameObject.AddComponent<ControlRoomAmbience>();
            WireArray(ambience, "scrollingScreens", new Object[] { dash[0], dash[1] });
            WireArray(ambience, "clocks", clocks.ToArray());
        }

        /// <summary>Asientos Seat_r_n del modelo de la sala, ordenados por fila y columna.</summary>
        private static List<Transform> Seats(Transform model) =>
            model.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Seat_")).OrderBy(t => t.name).ToList();

        // ───────────── Cinemática del amerizaje (Artemis II) ─────────────

        /// <summary>Lugar apartado del resto de áreas: el set del océano se dibuja solo con su propia cámara.</summary>
        private static readonly Vector3 SplashdownSetPos = new Vector3(0f, 0f, -9000f);

        private static void BuildSplashdownCinematic(MissionSimulationRunner runner)
        {
            var go = new GameObject("SplashdownCinematic");
            go.transform.position = SplashdownSetPos;
            var cine = go.AddComponent<SplashdownCinematic>();

            Material Lit(string name, Color color, float smooth, float metal = 0f, Texture tex = null, bool doubleSided = false)
            {
                var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.SetColor("_BaseColor", color);
                if (tex != null) m.SetTexture("_BaseMap", tex);
                m.SetFloat("_Smoothness", smooth);
                m.SetFloat("_Metallic", metal);
                if (doubleSided) { m.SetFloat("_Cull", 0f); m.doubleSidedGI = true; }
                return SaveMaterial(name, m);
            }

            // Paracaídas principales de Orion: gajos naranja y blanco alternados; los de frenado, con franjas más finas.
            var mainStripes = TextureFactory.ChuteStripes("Chute_Main_Stripes", 32, new Color(0.86f, 0.3f, 0.04f), new Color(0.93f, 0.92f, 0.89f));
            var drogueStripes = TextureFactory.ChuteStripes("Chute_Drogue_Stripes", 16, new Color(0.86f, 0.3f, 0.04f), new Color(0.93f, 0.92f, 0.89f));
            Wire(cine, "capsuleMaterial", Lit("MAT_Cine_OrionCapsule", new Color(0.66f, 0.67f, 0.7f), 0.55f, 0.55f));
            Wire(cine, "heatShieldMaterial", Lit("MAT_Cine_HeatShield", new Color(0.14f, 0.11f, 0.09f), 0.12f));
            Wire(cine, "chuteMaterial", Lit("MAT_Cine_ChuteMain", Color.white, 0.15f, 0f, mainStripes, true));
            Wire(cine, "drogueMaterial", Lit("MAT_Cine_ChuteDrogue", Color.white, 0.15f, 0f, drogueStripes, true));
            Wire(cine, "lineMaterial", UnlitMaterial("MAT_Cine_ChuteLines", new Color(0.86f, 0.85f, 0.8f)));
            Wire(cine, "oceanMaterial", Lit("MAT_Cine_Ocean", new Color(0.03f, 0.15f, 0.23f), 0.9f, 0.05f));
            Wire(cine, "airbagMaterial", Lit("MAT_Cine_Airbag", new Color(1f, 0.5f, 0.08f), 0.35f));
            Wire(cine, "shipMaterial", Lit("MAT_Cine_ShipGrey", new Color(0.46f, 0.49f, 0.52f), 0.3f, 0.2f));
            Wire(cine, "skyMaterial", UnlitMaterial("MAT_Cine_Sky", Color.white, TextureFactory.SkyGradient()));
            var plasmaMat = ParticleMaterial("MAT_Cine_Plasma", true);
            plasmaMat.SetFloat("_Cull", 0f);       // la capa de choque y la estela se ven por ambos lados
            EditorUtility.SetDirty(plasmaMat);
            Wire(cine, "plasmaMaterial", plasmaMat);
            Wire(cine, "sprayMaterial", ParticleMaterial("MAT_Cine_Spray", false));
            Wire(cine, "cloudMaterial", ParticleMaterial("MAT_Cine_Cloud", false));
            WireArray(runner, "cinematics", new Object[] { cine });
        }

        // ───────────── Estudio de fotografía lunar (escena de diseño) ─────────────

        private static MoonPhotoStudio BuildPhotoStudio(MissionSimulationRunner runner, Material earthMat, Transform lunarArea)
        {
            var area = new GameObject("MoonPhotoStudio");
            area.transform.position = PhotoStudioPos;
            var moonMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            moonMat.SetTexture("_BaseMap", TextureFactory.MoonPhoto());
            moonMat.SetTexture("_BumpMap", TextureFactory.MoonPhotoNormal());
            moonMat.SetFloat("_BumpScale", 1f);
            moonMat.EnableKeyword("_NORMALMAP");
            moonMat.SetColor("_BaseColor", Color.white);
            moonMat.SetFloat("_Smoothness", 0.02f);
            moonMat = SaveMaterial("MAT_MoonPhoto", moonMat);
            var moon = Sphere("PhotoMoon", area.transform, Vector3.zero, 80f, moonMat);
            var earthPhotoMat = SaveMaterial("MAT_EarthPhoto", new Material(Shader.Find("Universal Render Pipeline/Lit")));
            earthPhotoMat.SetTexture("_BaseMap", TextureFactory.EarthPhoto());
            earthPhotoMat.SetColor("_BaseColor", Color.white);
            earthPhotoMat.SetFloat("_Smoothness", 0.35f);
            EditorUtility.SetDirty(earthPhotoMat);
            var earth = Sphere("PhotoEarth", area.transform, new Vector3(0f, 50f, 320f), 60f, earthPhotoMat);
            earth.gameObject.SetActive(false);

            var sun = new GameObject("StudioSun").AddComponent<Light>();
            sun.transform.SetParent(area.transform, false);
            sun.type = LightType.Directional;
            sun.intensity = 2.2f;
            sun.color = new Color(1f, 0.98f, 0.94f);
            sun.shadows = LightShadows.Soft;
            sun.enabled = false;

            Transform orion = null;
            var orionPrefab = ModelLinker.Load("Spacecraft", "BUS_ORION");
            var wingPrefab = ModelLinker.Load("Spacecraft", "SOL_ESM");
            if (orionPrefab != null)
            {
                var o = new GameObject("PhotoOrion").transform;
                o.SetParent(area.transform, false);
                Instantiate(orionPrefab, o, Vector3.zero);
                if (wingPrefab != null)
                {
                    var w = Instantiate(wingPrefab, o, new Vector3(-1.4f, 0.6f, 0f));
                    if (w != null) w.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
                }
                var rends = o.GetComponentsInChildren<Renderer>();
                if (rends.Length > 0)
                {
                    var b = rends[0].bounds;
                    foreach (var r in rends) b.Encapsulate(r.bounds);
                    o.localScale *= 7f / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.y, b.size.z));
                }
                o.localPosition = new Vector3(-3.5f, 1.2f, -184f + 9f);
                o.localRotation = Quaternion.Euler(10f, 140f, -12f);
                orion = o;
            }

            var camGo = new GameObject("PhotoCamera");
            camGo.transform.SetParent(area.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.3f;
            cam.enabled = false;

            Transform surface = null;
            if (lunarArea != null)
            {
                surface = new GameObject("PhotoSurfacePose").transform;
                surface.SetParent(lunarArea, false);
                surface.localPosition = new Vector3(-40f, 18f, -75f);
                surface.LookAt(lunarArea.position + new Vector3(10f, 0f, 30f));
            }

            var studio = area.AddComponent<MoonPhotoStudio>();
            Wire(studio, "photoCamera", cam);
            Wire(studio, "moon", moon);
            Wire(studio, "earth", earth);
            Wire(studio, "studioSun", sun);
            Wire(studio, "orion", orion);
            Wire(studio, "surfacePose", surface);
            Wire(studio, "runner", runner);
            return studio;
        }

        // ───────────── HUD de vuelo (simulación) ─────────────

        private static FlightHudView BuildFlightHud(Transform root)
        {
            var hudRoot = NewRect("FlightHud", root);
            Stretch(hudRoot);
            SetPhases(hudRoot.gameObject, MissionPhase.Launch, MissionPhase.OrbitInsertion, MissionPhase.ScienceOperations,
                MissionPhase.DataTransmission, MissionPhase.ReturnAndRecovery);

            var flash = NewRect("CameraFlash", hudRoot);
            Stretch(flash);
            var flashImg = flash.gameObject.AddComponent<Image>();
            flashImg.color = new Color(1f, 1f, 1f, 0f);
            flashImg.raycastTarget = false;

            var banner = Panel("MilestoneBanner", hudRoot, new Color(0.03f, 0.05f, 0.1f, 0.88f));
            Place(banner, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -110f), new Vector2(560f, 96f));
            var bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            var bannerText = Text("Text", banner, "", 21, UiTheme.Text, TextAlignmentOptions.MidlineLeft);
            Stretch(bannerText.rectTransform);
            bannerText.margin = new Vector4(18, 6, 12, 6);

            var tele = Panel("FlightTelemetry", hudRoot, new Color(0.03f, 0.05f, 0.1f, 0.82f));
            Place(tele, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -216f), new Vector2(560f, 190f));
            var teleGroup = tele.gameObject.AddComponent<CanvasGroup>();
            var teleText = Text("Text", tele, "", 19, UiTheme.Text, TextAlignmentOptions.TopLeft);
            teleText.lineSpacing = 6;
            Stretch(teleText.rectTransform);
            teleText.margin = new Vector4(18, 12, 12, 8);

            var photos = Panel("PhotoFeed", hudRoot, new Color(0.03f, 0.05f, 0.1f, 0.82f));
            Place(photos, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -416f), new Vector2(560f, 268f));
            var photosGroup = photos.gameObject.AddComponent<CanvasGroup>();
            var counter = Text("Counter", photos, "", 16, UiTheme.Text, TextAlignmentOptions.TopLeft);
            Place(counter.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(-32f, 26f));
            var strip = NewRect("Thumbs", photos);
            Place(strip, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -16f), new Vector2(-32f, -56f));
            var grid = strip.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(166f, 94f);
            grid.spacing = new Vector2(8f, 8f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;

            var view = hudRoot.gameObject.AddComponent<FlightHudView>();
            Wire(view, "telemetryGroup", teleGroup);
            Wire(view, "telemetryText", teleText);
            Wire(view, "bannerGroup", bannerGroup);
            Wire(view, "bannerText", bannerText);
            Wire(view, "photosGroup", photosGroup);
            Wire(view, "photoCounter", counter);
            Wire(view, "thumbStrip", strip);
            Wire(view, "flash", flashImg);
            return view;
        }

        // ───────────── Informe de la exploración ─────────────

        private static ExplorationReportPanel BuildExplorationReport(Transform root)
        {
            var overlay = Panel("ExplorationReport", root, new Color(0f, 0f, 0f, 0.86f));
            Stretch(overlay);
            var group = overlay.gameObject.AddComponent<CanvasGroup>();   // sin PhaseVisibility: el panel se abre a demanda
            group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false;
            var card = Panel("Card", overlay, new Color(0.04f, 0.06f, 0.11f, 1f));
            Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1680f, 940f));

            var header = Text("Header", card, "INFORME DE LA EXPLORACIÓN", 18, UiTheme.Accent, TextAlignmentOptions.TopLeft, bold: true);
            Place(header.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(-64f, 26f));
            var title = Text("Title", card, "", 36, UiTheme.Text, TextAlignmentOptions.TopLeft, bold: true);
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -52f), new Vector2(760f, 48f));
            // Una sola línea: los títulos largos («INFORME DEL EQUIPO DE EXPLORACIÓN») se achican en vez de pisar la firma.
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.enableAutoSizing = true;
            title.fontSizeMin = 22;
            title.fontSizeMax = 36;
            var signature = Text("Signature", card, "", 18, UiTheme.TextDim, TextAlignmentOptions.TopLeft);
            Place(signature.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -104f), new Vector2(760f, 30f));
            var body = Text("Body", card, "", 20, UiTheme.Text, TextAlignmentOptions.TopLeft);
            body.lineSpacing = 4;
            body.paragraphSpacing = 14;
            body.overflowMode = TextOverflowModes.Ellipsis;
            Place(body.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -150f), new Vector2(760f, -250f));

            var frame = Panel("PhotoFrame", card, Color.black);
            Place(frame, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-32f, -52f), new Vector2(820f, 461f));
            var photoRt = NewRect("Photo", frame);
            Stretch(photoRt);
            var photo = photoRt.gameObject.AddComponent<RawImage>();
            photo.raycastTarget = false;
            var none = Text("NoPhotos", frame, "<b>Sin fotografías</b>\nLa nave no llevaba cámaras o no llegó a la Luna.", 24, UiTheme.TextDim, TextAlignmentOptions.Center);
            Stretch(none.rectTransform);
            var caption = Text("Caption", card, "", 19, UiTheme.Text, TextAlignmentOptions.TopLeft);
            Place(caption.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-32f, -524f), new Vector2(820f, 56f));
            var thumbs = NewRect("Thumbs", card);
            Place(thumbs, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-32f, -592f), new Vector2(820f, 84f));
            var hl = thumbs.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 8; hl.childControlWidth = hl.childControlHeight = true; hl.childForceExpandWidth = hl.childForceExpandHeight = false;

            Button B(string n, string label, Color c, float x, float w)
            {
                var b = MakeButton(n, card, label, c, 18, 0);
                Place((RectTransform)b.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(x, 28f), new Vector2(w, 54f));
                return b;
            }
            var prev = B("PrevButton", "« Anterior", UiTheme.AccentDim, -646f, 170f);
            var next = B("NextButton", "Siguiente »", UiTheme.Accent, -466f, 170f);
            var folder = B("FolderButton", "Abrir carpeta de fotos", UiTheme.AccentDim, -228f, 228f);
            var close = B("CloseButton", "<b>Cerrar</b>", UiTheme.PanelLight, -32f, 186f);
            // Fuera de pantalla hasta que se abra: el CanvasGroup controla la visibilidad.
            close.transform.SetAsLastSibling();
            var menu = MakeButton("MainMenuButton", card, "Menú principal", UiTheme.PanelLight, 18, 0);
            Place((RectTransform)menu.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(32f, 28f), new Vector2(220f, 54f));

            var panel = overlay.gameObject.AddComponent<ExplorationReportPanel>();
            Wire(panel, "group", group);
            Wire(panel, "titleText", title);
            Wire(panel, "signatureText", signature);
            Wire(panel, "bodyText", body);
            Wire(panel, "mainPhoto", photo);
            Wire(panel, "noPhotos", none.gameObject);
            Wire(panel, "captionText", caption);
            Wire(panel, "thumbGrid", thumbs);
            Wire(panel, "prevButton", prev);
            Wire(panel, "nextButton", next);
            Wire(panel, "closeButton", close);
            Wire(panel, "folderButton", folder);
            Wire(panel, "menuButton", menu);
            return panel;
        }

        // ───────────── Centro espacial: círculo de entrada y mapa ─────────────

        private static void BuildDesignRing(Transform parent, Vector3 seatPoint, Vector3 facing)
        {
            facing.y = 0f;
            facing.Normalize();
            float floorY = seatPoint.y - 0.5f;
            var ring = new GameObject("DesignEntryRing");
            ring.transform.SetParent(parent, false);
            ring.transform.position = new Vector3(seatPoint.x, floorY + 0.03f, seatPoint.z) - facing * 0.9f;

            var glowMat = ParticleMaterial("MAT_FX_RingGlow", additive: true);
            var disc = GameObject.CreatePrimitive(PrimitiveType.Quad);
            disc.name = "Disc";
            Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.transform.SetParent(ring.transform, false);
            disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            disc.transform.localScale = new Vector3(1.9f, 1.9f, 1f);
            var discMat = ParticleMaterial("MAT_FX_RingDisc", additive: true);
            discMat.SetColor("_BaseColor", new Color(1f, 0.55f, 0.1f, 0.9f));
            disc.GetComponent<Renderer>().sharedMaterial = discMat;
            disc.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            var line = ring.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.widthMultiplier = 0.09f;
            line.positionCount = 48;
            for (int i = 0; i < 48; i++)
            {
                float a = 2f * Mathf.PI * i / 48;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * 0.95f, 0.02f, Mathf.Sin(a) * 0.95f));
            }
            line.sharedMaterial = UnlitMaterial("MAT_FX_RingLine", new Color(1f, 0.85f, 0.25f));
            line.shadowCastingMode = ShadowCastingMode.Off;

            var fxGo = new GameObject("Flames");
            fxGo.transform.SetParent(ring.transform, false);
            fxGo.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = fxGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.45f, 0.08f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            var emission = ps.emission;
            emission.rateOverTime = 70f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.95f;
            shape.radiusThickness = 0.15f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new[] { new GradientColorKey(new Color(1f, 0.9f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.35f, 0.05f), 1f) },
                alphaKeys = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) }
            });
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            var psr = fxGo.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = glowMat;
            psr.shadowCastingMode = ShadowCastingMode.Off;

            var light = new GameObject("Glow").AddComponent<Light>();
            light.transform.SetParent(ring.transform, false);
            light.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            light.type = LightType.Point;
            light.range = 4f;
            light.intensity = 2.5f;
            light.color = new Color(1f, 0.6f, 0.2f);

            var label = new GameObject("Label", typeof(RectTransform));
            label.transform.SetParent(ring.transform, false);
            label.transform.localPosition = new Vector3(0f, 2.3f, 0f);
            var tmp = label.AddComponent<TextMeshPro>();
            tmp.text = "<b>DISEÑAR MISIÓN</b>\n<size=70%>pisa el círculo</size>";
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 2.2f;
            tmp.color = new Color(1f, 0.85f, 0.4f);
            tmp.rectTransform.sizeDelta = new Vector2(4f, 1.2f);
            label.AddComponent<FaceCamera>();

            var seat = new GameObject("RingSeat").transform;
            seat.SetParent(ring.transform, true);
            seat.SetPositionAndRotation(seatPoint, Quaternion.LookRotation(facing));

            var entry = ring.AddComponent<DesignEntryRing>();
            Wire(entry, "seat", seat);
            Wire(entry, "disc", disc.transform);
            Wire(entry, "glow", light);
            Wire(entry, "label", label);
        }

        /// <summary>Flecha blanca (textura generada y guardada) para el minimapa.</summary>
        private static Texture2D ArrowTexture()
        {
            const string path = TextureFactory.Folder + "/UI_MapArrow.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float u = (x + 0.5f) / 64f, v = (y + 0.5f) / 64f;
                float half = 0.42f * (1f - v);                         // triángulo con punta arriba
                bool inside = v > 0.08f && v < 0.95f && Mathf.Abs(u - 0.5f) < half && !(v < 0.3f && Mathf.Abs(u - 0.5f) < 0.18f * (0.3f - v) / 0.3f * 2f);
                tex.SetPixel(x, y, inside ? Color.white : new Color(1f, 1f, 1f, 0f));
            }
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is TextureImporter ti) { ti.alphaIsTransparency = true; ti.mipmapEnabled = false; ti.SaveAndReimport(); }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void BuildMinimap(RectTransform root, ExplorationHud hud)
        {
            var frame = Panel("Minimap", root, new Color(0.02f, 0.04f, 0.08f, 0.9f));
            Place(frame, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(280f, 340f));
            var mapRt = NewRect("Map", frame);
            Place(mapRt, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(-16f, -76f));
            var map = mapRt.gameObject.AddComponent<RawImage>();
            map.raycastTarget = false;
            mapRt.gameObject.AddComponent<RectMask2D>();
            var arrowRt = NewRect("PlayerArrow", mapRt);
            Place(arrowRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 26f));
            var arrow = arrowRt.gameObject.AddComponent<RawImage>();
            arrow.texture = ArrowTexture();
            arrow.color = new Color(1f, 0.82f, 0.2f);
            arrow.raycastTarget = false;
            var north = Text("North", mapRt, "<b>N</b>", 16, UiTheme.Text, TextAlignmentOptions.Center);
            Place(north.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(30f, 22f));
            var geo = Text("Geo", frame, "", 13, UiTheme.Text, TextAlignmentOptions.TopLeft);
            Place(geo.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(-20f, 60f));

            // Rótulos del mapa grande (N): posiciones en coordenadas del mapa.
            var labels = NewRect("BigMapLabels", mapRt);
            Stretch(labels);
            foreach (var (name, pos) in new[] { ("Centro de Control", new Vector3(0f, 0f, 0f)), ("Edificio de Ensamblaje", new Vector3(0f, 0f, 160f)),
                                                  ("Plataforma de lanzamiento", new Vector3(0f, 0f, 420f)), ("Camino de orugas", new Vector3(0f, 0f, 290f)) })
            {
                Vector2 uv = ExplorationHud.MapUv(pos);
                var t = Text("Label_" + name, labels, $"<b>{name}</b>", 15, new Color(1f, 0.92f, 0.6f), TextAlignmentOptions.MidlineLeft);
                Place(t.rectTransform, uv, uv, new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(260f, 24f));
            }

            Wire(hud, "minimap", map);
            Wire(hud, "minimapFrame", frame);
            Wire(hud, "playerArrow", arrowRt);
            Wire(hud, "geoText", geo);
            Wire(hud, "bigMapLabels", labels.gameObject);
        }
    }
}
