using MissionDesign.Simulation;
using TMPro;
using MissionDesign.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MissionDesign.UI
{
    /// <summary>Barra de un recurso crítico: uso frente a límite, coloreada por estado. Al pasar el ratón explica el recurso.</summary>
    public sealed class ResourceBarView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (TooltipView.Instance != null) TooltipView.Instance.Show(ResourceExplanations.For(kind));
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (TooltipView.Instance != null) TooltipView.Instance.Hide();
        }

        [SerializeField] private ResourceKind kind;
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private TMP_Text detailText;
        [SerializeField] private Image fill;

        public ResourceKind Kind => kind;
        public string ValueText => valueText != null ? valueText.text : string.Empty;

        public static string Title(ResourceKind kind)
        {
            switch (kind)
            {
                case ResourceKind.Mass: return Loc.L("MASA", "MASS");
                case ResourceKind.Power: return Loc.L("ENERGÍA", "POWER");
                case ResourceKind.Budget: return Loc.L("PRESUPUESTO", "BUDGET");
                case ResourceKind.Data: return Loc.L("DATOS", "DATA");
                case ResourceKind.Comms: return Loc.L("COMUNICACIONES", "COMMS");
                case ResourceKind.Time: return Loc.L("TIEMPO", "SCHEDULE");
                default: return kind.ToString();
            }
        }

        /// <summary>Fracción de uso: 1 = límite alcanzado. En Comms se mide la saturación del enlace.</summary>
        public static float Utilization(ResourceReading r)
        {
            if (r.kind == ResourceKind.Comms)
                return r.value > 0f ? Mathf.Clamp01(r.limit / r.value) : (r.limit > 0f ? 1f : 0f);
            return r.limit > 0f ? Mathf.Clamp01(r.value / r.limit) : (r.value > 0f ? 1f : 0f);
        }

        public static string ShortValue(ResourceReading r)
        {
            switch (r.kind)
            {
                case ResourceKind.Mass: return $"{r.value:N0} / {r.limit:N0} kg";
                case ResourceKind.Power: return Loc.L($"{r.value:N0} W pico / {r.limit:N0} W", $"{r.value:N0} W peak / {r.limit:N0} W");
                case ResourceKind.Budget: return $"${r.value:N0}M / ${r.limit:N0}M";
                case ResourceKind.Data: return $"{r.value:N1} {Loc.L("GB/día", "GB/day")}";
                case ResourceKind.Comms: return $"{r.value:N1} {Loc.L("GB/día", "GB/day")}";
                case ResourceKind.Time: return $"{r.value:N0} / {r.limit:N0} {Loc.L("meses", "months")}";
                default: return $"{r.value:N1} {r.unit}";
            }
        }

        public void Show(ResourceReading reading)
        {
            if (label != null) label.text = Title(kind);
            if (valueText != null)
            {
                valueText.text = ShortValue(reading);
                valueText.color = UiTheme.StateColor(reading.state);
            }
            if (detailText != null) detailText.text = reading.summary;
            if (fill != null)
            {
                // Relleno por anclajes: no necesita sprite (Image.Filled sí lo exige).
                var rt = fill.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = new Vector2(Utilization(reading), 1f);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                fill.color = UiTheme.StateColor(reading.state);
            }
        }
    }
}
