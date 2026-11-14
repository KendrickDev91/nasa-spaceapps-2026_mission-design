using MissionDesign.Core;
using UnityEngine;

namespace MissionDesign.Data
{
    /// <summary>
    /// Base de toda pieza que el jugador puede añadir al ensamblador 3D.
    /// MissionTelemetry suma masa, costo, potencia y tiempo a partir de estos campos.
    /// </summary>
    public abstract class SpacecraftComponentSO : ScriptableObject
    {
        [Header("Identidad")]
        public string componentId;
        public string displayName;
        [TextArea(2, 5)] public string description;
        [Tooltip("Misión/plataforma real de referencia (herencia de vuelo).")]
        public string heritage;

        [Header("Recursos")]
        [Min(0)] public float massKg;
        [Min(0)] public float costMUSD;
        [Tooltip("Consumo pico en W (generación si es un panel: ver SolarArraySO).")]
        [Min(0)] public float peakPowerW;
        [Tooltip("Meses de desarrollo + integración que añade la pieza.")]
        [Min(0)] public float developmentMonths;
        [Range(1, 9)] public int technologyReadinessLevel = 9;

        [Header("Arte 3D (Blender)")]
        public GameObject prefab;
        public SpacecraftSocket socket = SpacecraftSocket.InternalBay;
        public Sprite icon;

        [Header("Fuente de datos")]
        public DataConfidence dataConfidence = DataConfidence.GameEstimate;
        public string sourceUrl;
        [TextArea(2, 4)] public string sourceNotes;

        public abstract ComponentCategory Category { get; }

        /// <summary>Rol cooperativo responsable de elegir esta pieza.</summary>
        public abstract MissionRole OwnerRole { get; }
    }
}
