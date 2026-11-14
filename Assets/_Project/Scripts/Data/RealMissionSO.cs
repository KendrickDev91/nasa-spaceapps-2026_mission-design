using MissionDesign.Core;
using UnityEngine;

namespace MissionDesign.Data
{
    /// <summary>Misión real de la NASA para el "Tu misión se parece a…" del análisis post-misión.</summary>
    [CreateAssetMenu(menuName = "NASA SpaceApps/Real Mission", fileName = "REAL_New")]
    public class RealMissionSO : ScriptableObject
    {
        public string missionId;
        public string displayName;
        public int launchYear;
        public OrbitRegime regime;
        [Tooltip("componentId de los instrumentos del catálogo que vuela (o equivalentes).")]
        public string[] instrumentIds;
        public string[] tags;
        [Min(0)] public float massKg;
        [TextArea(2, 5)] public string description;
        public string sourceUrl;
    }
}
