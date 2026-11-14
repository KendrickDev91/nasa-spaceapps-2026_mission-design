using MissionDesign.Core;
using UnityEngine;

namespace MissionDesign.Data
{
    /// <summary>Computadora de vuelo + memoria (Command &amp; Data Handling). Solo en dificultad Avanzada.</summary>
    [CreateAssetMenu(menuName = "NASA SpaceApps/Onboard Computer", fileName = "CDH_New")]
    public class OnboardComputerSO : SpacecraftComponentSO
    {
        [Header("Datos a bordo")]
        [Tooltip("Memoria para guardar ciencia mientras no hay contacto con tierra (GB).")]
        [Min(0)] public float storageGB = 32f;
        [Tooltip("Electrónica endurecida contra radiación: evita reinicios por Single Event Upsets.")]
        public bool radiationHardened;

        public override ComponentCategory Category => ComponentCategory.OnboardComputer;
        public override MissionRole OwnerRole => MissionRole.SystemsEngineer;

        private void Reset()
        {
            socket = SpacecraftSocket.InternalBay;
            advancedOnly = true;
        }
    }
}
