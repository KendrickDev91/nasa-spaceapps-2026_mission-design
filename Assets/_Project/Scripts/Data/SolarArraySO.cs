using MissionDesign.Core;
using UnityEngine;

namespace MissionDesign.Data
{
    [CreateAssetMenu(menuName = "NASA SpaceApps/Solar Array", fileName = "SOL_New")]
    public class SolarArraySO : SpacecraftComponentSO
    {
        [Header("Generación solar")]
        [Tooltip("Potencia al inicio de vida (BOL) a 1 UA, incidencia normal (W).")]
        [Min(0)] public float powerAt1AU_W;
        [Tooltip("Degradación anual por radiación (fracción, ej. 0.025 = 2.5%/año).")]
        [Range(0f, 0.2f)] public float annualDegradation = 0.025f;

        public override ComponentCategory Category => ComponentCategory.SolarArray;
        public override MissionRole OwnerRole => MissionRole.SystemsEngineer;

        /// <summary>
        /// Potencia disponible en iluminación: ley del inverso del cuadrado con la distancia
        /// al Sol y degradación compuesta: P = P0 / d² · (1 - k)^años.
        /// </summary>
        public float GetPowerW(float sunDistanceAU, float yearsInOrbit = 0f)
        {
            float d = Mathf.Max(0.1f, sunDistanceAU);
            return powerAt1AU_W / (d * d) * Mathf.Pow(1f - annualDegradation, Mathf.Max(0f, yearsInOrbit));
        }

        private void Reset() => socket = SpacecraftSocket.SolarWingLeft;
    }
}
