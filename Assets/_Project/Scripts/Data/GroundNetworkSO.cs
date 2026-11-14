using System.Linq;
using MissionDesign.Core;
using UnityEngine;

namespace MissionDesign.Data
{
    /// <summary>
    /// Segmento terreno: la red de antenas que recibe los datos. Determina la ganancia del enlace,
    /// cuántas horas de contacto hay al día, su costo y su disponibilidad.
    /// </summary>
    [CreateAssetMenu(menuName = "NASA SpaceApps/Ground Network", fileName = "GND_New")]
    public class GroundNetworkSO : ScriptableObject
    {
        public string networkId;
        public string displayName;
        [TextArea(2, 5)] public string description;

        [Header("Cobertura y rendimiento")]
        public OrbitRegime[] supportedRegimes;
        [Tooltip("Ganancia relativa de las antenas terrenas (1 = ~11 m; DSN 34 m ≈ 30).")]
        [Min(0)] public float gainFactor = 1f;
        [Tooltip("Multiplica las horas de contacto base de la órbita (más estaciones = más pases).")]
        [Min(0)] public float contactHoursMultiplier = 1f;
        [Tooltip("Multiplica la probabilidad de cortes/conflictos de agenda.")]
        [Min(0)] public float outageProbabilityMultiplier = 1f;

        [Header("Costo")]
        [Min(0)] public float costPerMonthMUSD = 0.1f;

        [Header("Educación / fuente")]
        [TextArea(2, 5)] public string educationalNote;
        public DataConfidence dataConfidence = DataConfidence.GameEstimate;
        public string sourceUrl;

        public bool Supports(OrbitRegime regime) => supportedRegimes != null && supportedRegimes.Contains(regime);
    }
}
