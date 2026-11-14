using MissionDesign.Core;
using UnityEngine;

namespace MissionDesign.Data
{
    /// <summary>Control de orientación (ADCS): qué tan bien apunta la nave. Solo en dificultad Avanzada.</summary>
    [CreateAssetMenu(menuName = "NASA SpaceApps/Attitude Control", fileName = "ADCS_New")]
    public class AttitudeControlSO : SpacecraftComponentSO
    {
        [Header("Apuntamiento")]
        [Tooltip("Error de apuntamiento típico (grados). Menor es mejor.")]
        [Min(0.0001f)] public float pointingAccuracyDeg = 1f;

        public override ComponentCategory Category => ComponentCategory.AttitudeControl;
        public override MissionRole OwnerRole => MissionRole.SystemsEngineer;

        private void Reset()
        {
            socket = SpacecraftSocket.InternalBay;
            advancedOnly = true;
        }
    }
}
