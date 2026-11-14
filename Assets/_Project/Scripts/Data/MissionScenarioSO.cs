using MissionDesign.Core;
using UnityEngine;

namespace MissionDesign.Data
{
    /// <summary>Escenario que el Director de Misión elige: objetivo y límites del programa.</summary>
    [CreateAssetMenu(menuName = "NASA SpaceApps/Mission Scenario", fileName = "SCN_New")]
    public class MissionScenarioSO : ScriptableObject
    {
        public string scenarioId;
        public string displayName;
        [TextArea(3, 8)] public string briefing;
        public string scienceObjectiveTag;

        [Header("Límites del programa")]
        [Min(0)] public float budgetLimitMUSD;
        [Tooltip("Meses máximos desde aprobación hasta lanzamiento.")]
        [Min(0)] public float maxDevelopmentMonths;
        [Tooltip("Vida útil operativa objetivo (meses).")]
        [Min(0)] public float targetMissionLifeMonths;
        [Tooltip("Valor científico mínimo para declarar éxito total.")]
        [Min(0)] public int minScienceValue;
        public OrbitRegime[] allowedOrbits;

        [Header("Misión tripulada")]
        [Tooltip("Requiere una nave con soporte vital; la seguridad de la tripulación prima sobre la ciencia.")]
        public bool crewed;
        [Min(0)] public int crewSize;

        [Header("Modo sandbox")]
        [Tooltip("Sin límite de presupuesto, calendario ni órbita: para experimentar libremente.")]
        public bool isSandbox;
    }
}
