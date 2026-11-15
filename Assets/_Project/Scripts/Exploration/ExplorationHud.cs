using System.Collections;
using MissionDesign.Characters;
using MissionDesign.Core;
using MissionDesign.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MissionDesign.Exploration
{
    /// <summary>
    /// HUD del centro espacial: aviso "[E] …" del punto cercano, nombre de la zona, panel de información
    /// (texto o lámina con imagen), visor de planos (M) y menú Esc con viaje rápido entre edificios.
    /// </summary>
    public sealed class ExplorationHud : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private ThirdPersonCamera cameraRig;
        [SerializeField] private TMP_Text promptText;
        [SerializeField] private TMP_Text zoneText;
        [SerializeField] private CanvasGroup promptGroup;

        [Header("Panel de información")]
        [SerializeField] private CanvasGroup infoPanel;
        [SerializeField] private TMP_Text infoTitle;
        [SerializeField] private TMP_Text infoBody;
        [SerializeField] private TMP_Text infoCaption;
        [SerializeField] private TMP_Text infoPager;
        [SerializeField] private RawImage infoImage;
        [SerializeField] private GameObject infoImageFrame;
        [SerializeField] private Button infoPrev, infoNext, infoClose, infoAction;

        [Header("Menú")]
        [SerializeField] private CanvasGroup pauseMenu;
        [SerializeField] private Button resumeButton, designButton, plansButton, controlsButton, quitButton;
        [SerializeField] private Button[] travelButtons;
        [SerializeField] private Transform[] travelPoints;

        [Header("Planos")]
        [SerializeField] private Texture2D[] planTextures;

        [Header("Mapa geolocalizador (N lo amplía)")]
        [SerializeField] private RawImage minimap;
        [SerializeField] private RectTransform minimapFrame;
        [SerializeField] private RectTransform playerArrow;
        [SerializeField] private TMP_Text geoText;
        [SerializeField] private GameObject bigMapLabels;

        [Header("Ambiente y transiciones")]
        [SerializeField] private OutfitController playerOutfit;
        [SerializeField] private CanvasGroup toastGroup;
        [SerializeField] private TMP_Text toastText;
        [SerializeField] private Image fade;

        // Región del mapa (m, Unity): x de -110 a 110, z de -40 a 480. Norte = -Z (como en las láminas).
        public const float MapXMin = -110f, MapXMax = 110f, MapZMin = -40f, MapZMax = 480f;
        private const float MapWindowM = 130f;
        private const float PadZ = 420f;
        private RenderTexture mapTexture;
        private bool bigMap;
        private Vector2 smallMapSize, smallMapPos;
        private Coroutine toastRoutine;

        private Interactable[] interactables;
        private ZoneVolume[] zones;
        private Interactable current;
        private string[][] pages;
        private Texture2D[] pageImages;
        private int page;
        private bool offerDesign;

        public Interactable Current => current;
        public bool IsBlocking => Visible(infoPanel) || Visible(pauseMenu);
        public int PageCount => pages?.Length ?? 0;
        public string CurrentTitle => infoTitle != null ? infoTitle.text : string.Empty;
        public string CurrentZone => zoneText != null ? zoneText.text : string.Empty;
        public Transform[] TravelPoints => travelPoints;
        /// <summary>Bloqueo de entrada durante una animación (p. ej. sentarse en la consola).</summary>
        public bool CutsceneLock { get; set; }
        public Outfit CurrentOutfit => playerOutfit != null ? playerOutfit.Current : Outfit.Engineer;
        public string GeoText => geoText != null ? geoText.text : string.Empty;
        public bool MapRendered => mapTexture != null;

        private void Start()
        {
            interactables = FindObjectsByType<Interactable>();
            zones = FindObjectsByType<ZoneVolume>();
            Hook(infoPrev, () => Turn(-1));
            Hook(infoNext, () => Turn(1));
            Hook(infoClose, CloseInfo);
            Hook(infoAction, () => SceneFlow.OpenMissionDesign());
            Hook(resumeButton, () => Show(pauseMenu, false));
            Hook(designButton, () => SceneFlow.OpenMissionDesign());
            Hook(plansButton, () => { Show(pauseMenu, false); OpenPlans(); });
            Hook(controlsButton, () => { Show(pauseMenu, false); OpenInfo(new[] { new[] { "CONTROLES", SpaceCenterContent.Controls } }); });
            Hook(quitButton, () => SceneFlow.OpenMissionDesign(false));
            if (travelButtons != null)
                for (int i = 0; i < travelButtons.Length; i++)
                {
                    int k = i;
                    Hook(travelButtons[i], () => TravelTo(k));
                }
            Show(infoPanel, false);
            Show(pauseMenu, false);
            Show(promptGroup, false);
            Show(toastGroup, false);
            if (fade != null) fade.color = new Color(0f, 0f, 0f, 0f);
            Time.timeScale = 1f;
            if (minimapFrame != null) { smallMapSize = minimapFrame.sizeDelta; smallMapPos = minimapFrame.anchoredPosition; }
            if (bigMapLabels != null) bigMapLabels.SetActive(false);
            RenderMap();
        }

        private void OnDestroy()
        {
            if (mapTexture != null) mapTexture.Release();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame)
                {
                    if (Visible(infoPanel)) CloseInfo();
                    else Show(pauseMenu, !Visible(pauseMenu));
                }
                if (!IsBlocking && kb.mKey.wasPressedThisFrame) OpenPlans();
                if (!IsBlocking && kb.nKey.wasPressedThisFrame) ToggleBigMap();
                if (Visible(infoPanel))
                {
                    if (kb.rightArrowKey.wasPressedThisFrame) Turn(1);
                    if (kb.leftArrowKey.wasPressedThisFrame) Turn(-1);
                }
            }

            bool locked = IsBlocking || CutsceneLock;
            if (player != null) player.InputLocked = locked;
            if (cameraRig != null) cameraRig.InputLocked = locked;
            Time.timeScale = Visible(pauseMenu) ? 0f : 1f;

            current = locked ? null : FindNearest();
            UpdateMap();
            Show(promptGroup, current != null);
            if (current != null && promptText != null)
                promptText.text = $"<b>[E]</b>  {(current.kind == InteractionKind.Elevator && current.elevator != null && player != null ? current.elevator.PromptFor(player.transform) : current.prompt)}";
            if (current != null && kb != null && kb.eKey.wasPressedThisFrame) Interact(current);

            if (zoneText != null && player != null)
            {
                string zone = ZoneAt(player.transform.position);
                zoneText.text = zone;
                UpdateOutfit(zone);
            }
        }

        // ───────────── Vestuario según el ambiente ─────────────

        public static Outfit OutfitFor(string zone)
        {
            if (zone.Contains("Sala limpia")) return Outfit.CleanRoom;
            if (zone.Contains("Ensamblaje")) return Outfit.AssemblyEngineer;
            if (zone.Contains("Plataforma")) return Outfit.Astronaut;
            return Outfit.Engineer;
        }

        private void UpdateOutfit(string zone)
        {
            if (playerOutfit == null || CutsceneLock) return;
            var next = OutfitFor(zone);
            if (next == playerOutfit.Current) return;
            playerOutfit.Set(next);
            ShowToast(next switch
            {
                Outfit.CleanRoom => "Equipo: traje de sala limpia (mono, capucha y mascarilla · ISO 14644)",
                Outfit.AssemblyEngineer => "Equipo: casco de seguridad para la nave de ensamblaje",
                Outfit.Astronaut => "Equipo: traje de lanzamiento y entrada de la tripulación",
                _ => "Equipo: ropa de ingeniero aeroespacial"
            });
        }

        public void ShowToast(string message)
        {
            if (toastText == null) return;
            toastText.text = message;
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(Toast());
        }

        private IEnumerator Toast()
        {
            Show(toastGroup, true);
            toastGroup.blocksRaycasts = false;
            yield return new WaitForSecondsRealtime(2.6f);
            for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime) { toastGroup.alpha = 1f - t / 0.4f; yield return null; }
            Show(toastGroup, false);
            toastRoutine = null;
        }

        public IEnumerator FadeOut(float seconds)
        {
            if (fade == null) yield break;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                fade.color = new Color(0f, 0f, 0f, t / seconds);
                yield return null;
            }
            fade.color = Color.black;
        }

        // ───────────── Mapa geolocalizador ─────────────

        /// <summary>Renderiza una vez el campus visto desde arriba (norte arriba) a una textura para el minimapa.</summary>
        private void RenderMap()
        {
            if (minimap == null) return;
            float w = MapXMax - MapXMin, h = MapZMax - MapZMin;
            mapTexture = new RenderTexture(Mathf.RoundToInt(1400 * w / h), 1400, 24) { name = "MapTexture" };
            var go = new GameObject("MapCamera");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = h * 0.5f;
            cam.aspect = w / h;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 600f;
            cam.cullingMask = ~((1 << 2) | (1 << 5));
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.2f, 0.28f, 0.2f);
            go.transform.SetPositionAndRotation(new Vector3((MapXMin + MapXMax) * 0.5f, 300f, (MapZMin + MapZMax) * 0.5f),
                Quaternion.LookRotation(Vector3.down, Vector3.back));
            cam.targetTexture = mapTexture;
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            cam.Render();
            RenderSettings.fog = fog;
            cam.targetTexture = null;
            Destroy(go);
            minimap.texture = mapTexture;
        }

        /// <summary>Posición normalizada (0-1) en el mapa: derecha = este (-X), arriba = norte (-Z).</summary>
        public static Vector2 MapUv(Vector3 world) =>
            new Vector2((MapXMax - world.x) / (MapXMax - MapXMin), (MapZMax - world.z) / (MapZMax - MapZMin));

        public static (double lat, double lon) GeoAt(Vector3 world)
        {
            double north = -(world.z - PadZ), east = -world.x;
            double lat = MissionGeo.LaunchLat + north / 111320.0;
            double lon = MissionGeo.LaunchLon + east / (111320.0 * System.Math.Cos(MissionGeo.LaunchLat * System.Math.PI / 180.0));
            return (lat, lon);
        }

        private void ToggleBigMap()
        {
            if (minimapFrame == null) return;
            bigMap = !bigMap;
            minimapFrame.sizeDelta = bigMap ? new Vector2(560f, 900f) : smallMapSize;
            minimapFrame.anchoredPosition = bigMap ? new Vector2(-24f, -24f) : smallMapPos;
            if (bigMapLabels != null) bigMapLabels.SetActive(bigMap);
        }

        private void UpdateMap()
        {
            if (player == null || minimap == null) return;
            Vector3 p = player.transform.position;
            Vector2 uv = MapUv(p);
            float w = MapXMax - MapXMin, h = MapZMax - MapZMin;
            Rect view = bigMap ? new Rect(0f, 0f, 1f, 1f)
                               : new Rect(uv.x - MapWindowM / w * 0.5f, uv.y - MapWindowM / h * 0.5f, MapWindowM / w, MapWindowM / h);
            minimap.uvRect = view;
            if (playerArrow != null)
            {
                var area = minimap.rectTransform.rect;
                Vector2 local = new Vector2((uv.x - view.x) / view.width - 0.5f, (uv.y - view.y) / view.height - 0.5f);
                playerArrow.anchoredPosition = new Vector2(local.x * area.width, local.y * area.height);
                Vector3 f = player.transform.forward;
                float ang = Mathf.Atan2(-f.x, -f.z) * Mathf.Rad2Deg;      // en el mapa: derecha = -X, arriba = -Z
                playerArrow.localRotation = Quaternion.Euler(0f, 0f, -ang);
            }
            if (geoText != null)
            {
                var (lat, lon) = GeoAt(p);
                float heading = Mathf.Repeat(Mathf.Atan2(-player.transform.forward.x, -player.transform.forward.z) * Mathf.Rad2Deg, 360f);
                string[] dirs = { "N", "NE", "E", "SE", "S", "SO", "O", "NO" };
                geoText.text = $"<b>{MissionGeo.Format(lat, lon)}</b>\nRumbo {heading:000}° {dirs[Mathf.RoundToInt(heading / 45f) % 8]} · ubicación ilustrativa";
            }
        }

        public void Interact(Interactable target)
        {
            if (target == null) return;
            switch (target.kind)
            {
                case InteractionKind.OpenMissionDesign:
                    if (target.Pages != null) OpenInfo(target.Pages, offerMissionDesign: true);
                    else SceneFlow.OpenMissionDesign();
                    break;
                case InteractionKind.PlansViewer:
                    OpenPlans();
                    break;
                case InteractionKind.Elevator:
                    if (target.elevator != null && player != null) target.elevator.Activate(player.transform);
                    break;
                default:
                    OpenInfo(target.Pages ?? new[] { new[] { target.prompt, "" } }, target.offerMissionDesign);
                    break;
            }
        }

        public void OpenPlans()
        {
            int n = SpaceCenterContent.Plans.Length;
            var p = new string[n][];
            var images = new Texture2D[n];
            for (int i = 0; i < n; i++)
            {
                var sheet = SpaceCenterContent.Plans[i];
                p[i] = new[] { sheet.title, sheet.caption };
                images[i] = planTextures != null && i < planTextures.Length ? planTextures[i] : null;
            }
            OpenInfo(p, false, images);
        }

        public void OpenInfo(string[][] content, bool offerMissionDesign = false, Texture2D[] images = null)
        {
            pages = content;
            pageImages = images;
            offerDesign = offerMissionDesign;
            page = 0;
            Show(infoPanel, true);
            Render();
        }

        public void CloseInfo() => Show(infoPanel, false);

        public void TravelTo(int index)
        {
            if (travelPoints == null || index < 0 || index >= travelPoints.Length || player == null) return;
            var t = travelPoints[index];
            player.Teleport(t.position, t.eulerAngles.y);
            Show(pauseMenu, false);
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
            if (infoPrev != null) infoPrev.interactable = page > 0;
            if (infoNext != null) infoNext.interactable = page < pages.Length - 1;
            if (infoAction != null) infoAction.gameObject.SetActive(offerDesign);
        }

        private Interactable FindNearest()
        {
            if (player == null || interactables == null) return null;
            Vector3 p = player.transform.position;
            Interactable best = null;
            float bestD = float.MaxValue;
            foreach (var it in interactables)
            {
                if (it == null || !it.isActiveAndEnabled) continue;
                Vector3 d = it.transform.position - p;
                if (d.y > 2.5f || d.y < -1.2f) continue;
                float h = new Vector2(d.x, d.z).magnitude;
                if (h <= it.radius && h < bestD) { bestD = h; best = it; }
            }
            return best;
        }

        private string ZoneAt(Vector3 position)
        {
            ZoneVolume best = null;
            if (zones != null)
                foreach (var z in zones)
                    if (z != null && z.Contains(position) && (best == null || z.Volume < best.Volume)) best = z;
            return best != null ? best.zoneName : "Centro espacial · exteriores";
        }

        private static bool Visible(CanvasGroup g) => g != null && g.alpha > 0.5f;

        private static void Show(CanvasGroup g, bool visible)
        {
            if (g == null) return;
            g.alpha = visible ? 1f : 0f;
            g.interactable = visible;
            g.blocksRaycasts = visible;
        }

        private static void Hook(Button b, UnityEngine.Events.UnityAction action)
        {
            if (b != null) b.onClick.AddListener(action);
        }
    }
}
