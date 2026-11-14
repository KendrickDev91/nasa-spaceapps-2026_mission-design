using System.Linq;
using MissionDesign.Core;
using UnityEngine;

namespace MissionDesign.UI
{
    /// <summary>
    /// Muestra el panel solo en las fases indicadas. Usa CanvasGroup (no SetActive) para que
    /// los scripts del panel sigan suscritos a los eventos mientras está oculto.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class PhaseVisibility : MonoBehaviour
    {
        [SerializeField] private MissionPhase[] visibleIn = { MissionPhase.Design };

        private CanvasGroup group;
        private MissionStateManager manager;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            Apply(MissionPhase.Design);
        }

        private void Start()
        {
            manager = MissionStateManager.Instance;
            if (manager == null) return;
            manager.PhaseChanged += Apply;
            Apply(manager.Phase);
        }

        private void OnDestroy()
        {
            if (manager != null) manager.PhaseChanged -= Apply;
        }

        private void Apply(MissionPhase phase)
        {
            bool visible = visibleIn != null && visibleIn.Contains(phase);
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
