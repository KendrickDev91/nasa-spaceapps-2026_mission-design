using MissionDesign.Core;
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
            Time.timeScale = 1f;
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
                if (Visible(infoPanel))
                {
                    if (kb.rightArrowKey.wasPressedThisFrame) Turn(1);
                    if (kb.leftArrowKey.wasPressedThisFrame) Turn(-1);
                }
            }

            bool locked = IsBlocking;
            if (player != null) player.InputLocked = locked;
            if (cameraRig != null) cameraRig.InputLocked = locked;
            Time.timeScale = Visible(pauseMenu) ? 0f : 1f;

            current = locked ? null : FindNearest();
            Show(promptGroup, current != null);
            if (current != null && promptText != null) promptText.text = $"<b>[E]</b>  {current.prompt}";
            if (current != null && kb != null && kb.eKey.wasPressedThisFrame) Interact(current);

            if (zoneText != null && player != null) zoneText.text = ZoneAt(player.transform.position);
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
                if (Mathf.Abs(d.y) > 2.5f) continue;
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
