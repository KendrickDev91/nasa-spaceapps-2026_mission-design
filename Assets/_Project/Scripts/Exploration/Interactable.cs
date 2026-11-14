using UnityEngine;

namespace MissionDesign.Exploration
{
    public enum InteractionKind { Info, OpenMissionDesign, PlansViewer }

    /// <summary>Punto de interacción (tecla E): muestra información, abre los planos o entra al diseño de misión.</summary>
    public sealed class Interactable : MonoBehaviour
    {
        public string interactionId;
        public string prompt = "Interactuar";
        public InteractionKind kind = InteractionKind.Info;
        public float radius = 2.6f;
        /// <summary>Si es true, el panel de información ofrece "Diseñar la misión".</summary>
        public bool offerMissionDesign;

        /// <summary>Páginas {título, cuerpo} (se completan desde <see cref="SpaceCenterContent"/> al iniciar).</summary>
        public string[][] Pages { get; set; }

        private void Awake()
        {
            if (Pages != null) return;
            Pages = interactionId switch
            {
                "Reception" => SpaceCenterContent.Reception,
                "AssemblyKiosk" => SpaceCenterContent.CleanRoom,
                "StackInfo" => SpaceCenterContent.Stacking,
                "LaunchPanel" => SpaceCenterContent.LaunchPad,
                "DesignConsole" => SpaceCenterContent.DesignConsole,
                _ => null
            };
        }
    }
}
