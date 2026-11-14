using MissionDesign.Core;
using UnityEngine;

namespace MissionDesign.Data
{
    [CreateAssetMenu(menuName = "NASA SpaceApps/Antenna", fileName = "ANT_New")]
    public class AntennaSO : SpacecraftComponentSO
    {
        [Header("Enlace de comunicaciones")]
        public CommBand band;
        [Tooltip("Tasa de bajada a la distancia de referencia (Mbps).")]
        [Min(0)] public float referenceDataRateMbps;
        [Tooltip("Distancia de diseño a la que se logra la tasa de referencia (km).")]
        [Min(1)] public float referenceRangeKm = 2000f;
        [Tooltip("Límite del módem/transmisor (Mbps): la tasa nunca lo supera.")]
        [Min(0)] public float maxDataRateMbps;
        [Tooltip("Compatible con la Red del Espacio Profundo (DSN).")]
        public bool dsnCompatible;

        public override ComponentCategory Category => ComponentCategory.Antenna;
        public override MissionRole OwnerRole => MissionRole.TelecomLaunchEngineer;

        /// <summary>
        /// Presupuesto de enlace simplificado: la potencia recibida cae con 1/r²,
        /// y la tasa alcanzable es proporcional a ella (Shannon en régimen de baja SNR).
        /// groundGainFactor modela antenas terrenas más grandes (ej. DSN 34 m).
        /// </summary>
        public float GetDataRateMbps(float rangeKm, float groundGainFactor = 1f)
        {
            float r = Mathf.Max(1f, rangeKm);
            float ratio = referenceRangeKm / r;
            float rate = referenceDataRateMbps * ratio * ratio * Mathf.Max(0f, groundGainFactor);
            return Mathf.Min(rate, maxDataRateMbps);
        }

        private void Reset() => socket = SpacecraftSocket.AntennaSocket;
    }
}
