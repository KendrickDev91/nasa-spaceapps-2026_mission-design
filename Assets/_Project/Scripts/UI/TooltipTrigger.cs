using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MissionDesign.UI
{
    /// <summary>Muestra un tooltip al pasar el ratón. El contenido se calcula al entrar (siempre actualizado).</summary>
    public sealed class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [TextArea(2, 6)] public string staticText;
        public Func<string> Content;

        private bool hovered;

        public string Resolve() => Content != null ? Content() : MissionDesign.Core.Loc.Static(staticText);

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            if (TooltipView.Instance != null) TooltipView.Instance.Show(Resolve());
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            if (TooltipView.Instance != null) TooltipView.Instance.Hide();
        }

        private void OnDisable()
        {
            if (hovered && TooltipView.Instance != null) TooltipView.Instance.Hide();
            hovered = false;
        }
    }
}
