using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MissionDesign.UI
{
    /// <summary>
    /// Tooltip único del HUD: sigue al ratón y se mantiene dentro de la pantalla.
    /// Lo abren los TooltipTrigger (catálogo) y las barras de recursos.
    /// </summary>
    public sealed class TooltipView : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private TMP_Text text;
        [SerializeField] private Vector2 offset = new Vector2(20f, -20f);

        private Canvas canvas;
        private RectTransform canvasRect;
        private Vector2? fixedScreenPosition;

        public static TooltipView Instance { get; private set; }
        public bool IsVisible => panel != null && panel.gameObject.activeSelf;
        public string CurrentText => text != null ? text.text : string.Empty;

        private void Awake()
        {
            Instance = this;
            canvas = GetComponentInParent<Canvas>();
            canvasRect = canvas != null ? (RectTransform)canvas.transform : null;
            Hide();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <param name="screenPosition">Posición fija (p. ej. para capturas); si es null sigue al ratón.</param>
        public void Show(string content, Vector2? screenPosition = null)
        {
            if (string.IsNullOrEmpty(content) || panel == null || text == null)
            {
                Hide();
                return;
            }
            text.text = content;
            panel.gameObject.SetActive(true);
            transform.SetAsLastSibling();
            fixedScreenPosition = screenPosition;
            Position(screenPosition ?? MousePosition());
        }

        public void Hide()
        {
            if (panel != null) panel.gameObject.SetActive(false);
            fixedScreenPosition = null;
        }

        private void LateUpdate()
        {
            if (IsVisible && fixedScreenPosition == null) Position(MousePosition());
        }

        private static Vector2 MousePosition() => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        private void Position(Vector2 screen)
        {
            if (canvasRect == null) return;
            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, cam, out var local);
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);

            Vector2 size = panel.rect.size;
            Vector2 half = canvasRect.rect.size * 0.5f;
            var pos = local + offset;                                  // pivote del panel: esquina superior izquierda
            if (pos.x + size.x > half.x) pos.x = local.x - Mathf.Abs(offset.x) - size.x;
            if (pos.y - size.y < -half.y) pos.y = local.y + Mathf.Abs(offset.y) + size.y;
            pos.x = Mathf.Clamp(pos.x, -half.x, Mathf.Max(-half.x, half.x - size.x));
            pos.y = Mathf.Clamp(pos.y, Mathf.Min(half.y, -half.y + size.y), half.y);
            panel.anchoredPosition = pos;
        }
    }
}
