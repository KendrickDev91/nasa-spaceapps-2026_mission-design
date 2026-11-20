using System.Collections.Generic;
using MissionDesign.Core;
using MissionDesign.Simulation;
using MissionDesign.UI.Visual3D;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionDesign.UI
{
    /// <summary>
    /// "Informe de la exploración": bitácora escrita por la tripulación (o el equipo de operaciones) y galería con las
    /// fotografías de la Luna tomadas por las cámaras de la nave. Se abre desde el análisis post-misión.
    /// </summary>
    public sealed class ExplorationReportPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private MoonPhotoStudio studio;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text signatureText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private RawImage mainPhoto;
        [SerializeField] private GameObject noPhotos;
        [SerializeField] private TMP_Text captionText;
        [SerializeField] private RectTransform thumbGrid;
        [SerializeField] private Button prevButton, nextButton, closeButton, folderButton;
        [SerializeField] private Button menuButton;

        private readonly List<GameObject> thumbs = new List<GameObject>();
        private int index;
        private Coroutine typing;

        public bool IsOpen => group != null && group.alpha > 0.5f;
        public int PhotoCount => studio != null ? studio.Photos.Count : 0;
        public string Body => bodyText != null ? bodyText.text : string.Empty;

        private void Start()
        {
            Show(false);
            if (prevButton != null) prevButton.onClick.AddListener(() => Select(index - 1));
            if (nextButton != null) nextButton.onClick.AddListener(() => Select(index + 1));
            if (closeButton != null) closeButton.onClick.AddListener(() => Show(false));
            if (menuButton != null) menuButton.onClick.AddListener(() => { Show(false); GameMenus.BackToMainMenu(); });
            if (folderButton != null) folderButton.onClick.AddListener(OpenFolder);
            if (folderButton != null && GameSettings.IsWeb) folderButton.gameObject.SetActive(false);
            var manager = MissionStateManager.Instance;
            if (manager != null) manager.PhaseChanged += OnPhaseChanged;
        }

        private void OnDestroy()
        {
            var manager = MissionStateManager.Instance;
            if (manager != null) manager.PhaseChanged -= OnPhaseChanged;
        }

        private void OnPhaseChanged(MissionPhase phase)
        {
            if (phase != MissionPhase.PostMission) Show(false);
        }

        private void Update()
        {
            if (!IsOpen) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame) Show(false);
            if (kb.rightArrowKey.wasPressedThisFrame) Select(index + 1);
            if (kb.leftArrowKey.wasPressedThisFrame) Select(index - 1);
        }

        public void Open()
        {
            var manager = MissionStateManager.Instance;
            var o = manager != null ? manager.LastOutcome : null;
            if (o == null) return;
            var roles = FindAnyObjectByType<Multiplayer.RoleTurnManager>();
            var report = ExplorationReport.Build(o, roles != null ? roles.TeamName : null);
            if (titleText != null) titleText.text = report.title;
            if (signatureText != null) signatureText.text = report.signature;
            if (bodyText != null) bodyText.text = report.body;

            foreach (var t in thumbs) Destroy(t);
            thumbs.Clear();
            int n = PhotoCount;
            for (int i = 0; i < n; i++)
            {
                int k = i;
                var go = new GameObject($"Thumb_{i + 1}", typeof(RectTransform));
                go.transform.SetParent(thumbGrid, false);
                var img = go.AddComponent<RawImage>();
                img.texture = studio.Photos[i].texture;
                var b = go.AddComponent<Button>();
                b.targetGraphic = img;
                b.onClick.AddListener(() => Select(k));
                var le = go.AddComponent<LayoutElement>();
                le.preferredWidth = 92; le.preferredHeight = 52;
                thumbs.Add(go);
            }
            if (noPhotos != null) noPhotos.SetActive(n == 0);
            if (mainPhoto != null) mainPhoto.gameObject.SetActive(n > 0);
            if (folderButton != null) folderButton.interactable = studio != null && !string.IsNullOrEmpty(studio.SavedFolder);
            Show(true);
            Select(0);
            if (typing != null) StopCoroutine(typing);
            typing = StartCoroutine(TypeBody());
        }

        /// <summary>La bitácora aparece como si se estuviera escribiendo.</summary>
        private System.Collections.IEnumerator TypeBody()
        {
            if (bodyText == null) yield break;
            bodyText.maxVisibleCharacters = 0;
            bodyText.ForceMeshUpdate();
            int total = bodyText.textInfo.characterCount;
            for (float t = 0f; t < 1.6f; t += Time.unscaledDeltaTime)
            {
                bodyText.maxVisibleCharacters = Mathf.RoundToInt(total * (t / 1.6f));
                yield return null;
            }
            bodyText.maxVisibleCharacters = 99999;
            typing = null;
        }

        public void Select(int i)
        {
            int n = PhotoCount;
            if (n == 0)
            {
                if (captionText != null)
                    captionText.text = ExplorationReport.HowToAddCamera;
                if (prevButton != null) prevButton.interactable = false;
                if (nextButton != null) nextButton.interactable = false;
                return;
            }
            index = Mathf.Clamp(i, 0, n - 1);
            var p = studio.Photos[index];
            if (mainPhoto != null) mainPhoto.texture = p.texture;
            if (captionText != null) captionText.text = $"<b>{Loc.L($"Foto {index + 1} de {n}", $"Photo {index + 1} of {n}")}</b> · {p.caption}";
            for (int k = 0; k < thumbs.Count; k++)
                thumbs[k].transform.localScale = Vector3.one * (k == index ? 1.12f : 1f);
            if (prevButton != null) prevButton.interactable = index > 0;
            if (nextButton != null) nextButton.interactable = index < n - 1;
        }

        public void Show(bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
            if (visible) transform.SetAsLastSibling();
        }

        private void OpenFolder()
        {
            if (studio == null || string.IsNullOrEmpty(studio.SavedFolder)) return;
            Application.OpenURL("file:///" + studio.SavedFolder.Replace('\\', '/'));
        }
    }
}
