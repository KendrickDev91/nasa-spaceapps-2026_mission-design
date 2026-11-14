using System;
using System.Collections.Generic;
using MissionDesign.Core;
using UnityEngine;

namespace MissionDesign.Data
{
    [Serializable]
    public struct OrbitPayloadCapacity
    {
        public OrbitRegime regime;
        [Tooltip("Carga útil máxima a esa órbita (kg). 0 = no ofrecido.")]
        [Min(0)] public float payloadKg;
        public DataConfidence confidence;
        public string note;
    }

    [CreateAssetMenu(menuName = "NASA SpaceApps/Launch Vehicle", fileName = "LV_New")]
    public class LaunchVehicleSO : ScriptableObject
    {
        [Header("Identidad")]
        public string vehicleId;
        public string displayName;
        public string operatorName;
        public string country;
        [TextArea(2, 5)] public string description;

        [Header("Rendimiento")]
        public List<OrbitPayloadCapacity> payloadCapacities = new List<OrbitPayloadCapacity>();
        [Min(0)] public float fairingDiameterM;
        [Tooltip("Tasa de éxito usada por el simulador (0-1).")]
        [Range(0f, 1f)] public float reliability = 0.95f;

        [Header("Costo y calendario")]
        [Min(0)] public float launchCostMUSD;
        [Tooltip("Meses de antelación para reservar el lanzamiento e integrar la carga.")]
        [Min(0)] public float leadTimeMonths = 12f;

        [Header("Arte 3D")]
        public GameObject prefab;
        public Sprite icon;

        [Header("Fuente de datos")]
        public DataConfidence dataConfidence = DataConfidence.Approximate;
        public string sourceUrl;
        [TextArea(2, 4)] public string sourceNotes;

        public bool TryGetCapacity(OrbitRegime regime, out OrbitPayloadCapacity capacity)
        {
            foreach (var c in payloadCapacities)
            {
                if (c.regime == regime && c.payloadKg > 0f)
                {
                    capacity = c;
                    return true;
                }
            }
            capacity = default;
            return false;
        }

        public float GetCapacityKg(OrbitRegime regime) =>
            TryGetCapacity(regime, out var c) ? c.payloadKg : 0f;
    }
}
