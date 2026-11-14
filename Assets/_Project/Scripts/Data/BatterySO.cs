using MissionDesign.Core;
using UnityEngine;

namespace MissionDesign.Data
{
    [CreateAssetMenu(menuName = "NASA SpaceApps/Battery", fileName = "BAT_New")]
    public class BatterySO : SpacecraftComponentSO
    {
        [Header("Almacenamiento")]
        [Min(0)] public float capacityWh;
        [Tooltip("Profundidad de descarga permitida (vida útil de celdas Li-ion).")]
        [Range(0.1f, 1f)] public float maxDepthOfDischarge = 0.6f;

        public float UsableWh => capacityWh * maxDepthOfDischarge;

        public override ComponentCategory Category => ComponentCategory.Battery;
        public override MissionRole OwnerRole => MissionRole.SystemsEngineer;
    }
}
