using System;
using System.Collections.Generic;
using MissionDesign.Core;
using UnityEngine;

namespace MissionDesign.Data
{
    [Serializable]
    public struct OrbitProbabilityModifier
    {
        public OrbitRegime regime;
        [Min(0)] public float multiplier;
    }

    /// <summary>
    /// Evento aleatorio basado en fenómenos espaciales reales. Los multiplicadores se aplican
    /// sobre los resultados de la fase afectada; 1 = sin efecto.
    /// </summary>
    [CreateAssetMenu(menuName = "NASA SpaceApps/Random Event", fileName = "EVT_New")]
    public class RandomEventSO : ScriptableObject
    {
        [Header("Identidad")]
        public string eventId;
        public string displayName;
        public RandomEventType eventType;
        [TextArea(2, 5)] public string description;
        public bool isPositive;

        [Header("Probabilidad")]
        [Range(0f, 1f)] public float baseProbability = 0.2f;
        public List<OrbitProbabilityModifier> orbitModifiers = new List<OrbitProbabilityModifier>();
        public MissionPhase affectedPhase = MissionPhase.ScienceOperations;

        [Header("Efectos (multiplicadores, 1 = sin efecto)")]
        [Min(0)] public float scienceMultiplier = 1f;
        [Min(0)] public float dataGenerationMultiplier = 1f;
        [Min(0)] public float commsMultiplier = 1f;
        [Tooltip("Pérdida permanente de generación solar (fracción).")]
        [Range(0f, 1f)] public float permanentPowerLossFraction;
        [Tooltip("Probabilidad de que una pieza aleatoria quede dañada.")]
        [Range(0f, 1f)] public float componentFailureChance;

        [Header("Modo seguro")]
        public bool requiresSafeMode;
        [Tooltip("Horas que el satélite debe sobrevivir solo con batería y consumo mínimo.")]
        [Min(0)] public float safeModeHours;

        [Header("Educación / fuente")]
        [TextArea(2, 5)] public string mitigationAdvice;
        [TextArea(2, 5)] public string educationalFact;
        public DataConfidence dataConfidence = DataConfidence.Approximate;
        public string sourceUrl;

        public float GetProbability(OrbitRegime regime)
        {
            float m = 1f;
            foreach (var mod in orbitModifiers)
                if (mod.regime == regime) { m = mod.multiplier; break; }
            return Mathf.Clamp01(baseProbability * m);
        }
    }
}
