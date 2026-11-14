using MissionDesign.Core;
using UnityEngine;

namespace MissionDesign.Data
{
    [CreateAssetMenu(menuName = "NASA SpaceApps/Orbit", fileName = "ORB_New")]
    public class OrbitSO : ScriptableObject
    {
        [Header("Identidad")]
        public string orbitId;
        public string displayName;
        public OrbitRegime regime;
        [TextArea(2, 5)] public string description;

        [Header("Mecánica orbital")]
        [Min(0)] public float altitudeKm;
        public float inclinationDeg;
        [Min(0)] public float orbitalPeriodMinutes;
        [Tooltip("Δv total desde la superficie terrestre, incluye pérdidas gravitatorias y aerodinámicas (km/s).")]
        [Min(0)] public float requiredDeltaVKmS;
        [Tooltip("Parte del Δv que debe aportar el motor del propio satélite (circularización GTO→GEO, inserción lunar...).")]
        [Min(0)] public float onboardDeltaVKmS;

        [Header("Entorno de energía")]
        [Tooltip("Distancia al Sol (UA). 1.0 para órbitas terrestres y lunares.")]
        [Min(0.1f)] public float sunDistanceAU = 1f;
        [Tooltip("Fracción media del tiempo en eclipse (0-1).")]
        [Range(0f, 1f)] public float eclipseFraction;
        [Tooltip("Duración del eclipse más largo (min): dimensiona la batería.")]
        [Min(0)] public float maxEclipseMinutes;

        [Header("Comunicaciones")]
        [Tooltip("Distancia típica satélite-estación terrena (km).")]
        [Min(1)] public float groundStationRangeKm;
        [Tooltip("Horas de contacto al día con una red terrena estándar (la red elegida lo multiplica).")]
        [Range(0f, 24f)] public float dailyContactHours;

        [Header("Riesgos de entorno (0-1)")]
        [Range(0f, 1f)] public float radiationLevel;
        [Range(0f, 1f)] public float debrisRisk;

        [Header("Educación / fuente")]
        [TextArea(2, 5)] public string educationalNote;
        public DataConfidence dataConfidence = DataConfidence.Approximate;
        public string sourceUrl;

        public float GetContactHours(GroundNetworkSO network) =>
            Mathf.Min(24f, dailyContactHours * (network != null ? network.contactHoursMultiplier : 1f));

        public float GetDataRateMbps(AntennaSO antenna, GroundNetworkSO network) =>
            antenna != null ? antenna.GetDataRateMbps(groundStationRangeKm, network != null ? network.gainFactor : 1f) : 0f;

        /// <summary>Capacidad diaria de bajada en GB/día para una antena y red terrena dadas.</summary>
        public float GetDailyDownlinkGB(AntennaSO antenna, GroundNetworkSO network = null)
        {
            if (antenna == null) return 0f;
            // Mbps · s / 8 bits / 1000 MB → GB
            return GetDataRateMbps(antenna, network) * GetContactHours(network) * 3600f / 8f / 1000f;
        }
    }
}
