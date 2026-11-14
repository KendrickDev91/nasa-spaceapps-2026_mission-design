using MissionDesign.Core;
using UnityEngine;

namespace MissionDesign.Data
{
    /// <summary>Plataforma satelital (estructura, aviónica, control térmico, propulsión).</summary>
    [CreateAssetMenu(menuName = "NASA SpaceApps/Spacecraft Bus", fileName = "BUS_New")]
    public class SpacecraftBusSO : SpacecraftComponentSO
    {
        [Header("Bus")]
        [Tooltip("Masa máxima de instrumentos que la estructura soporta (kg).")]
        [Min(0)] public float maxPayloadMassKg;
        [Tooltip("Potencia máxima que el bus puede distribuir a la carga útil (W).")]
        [Min(0)] public float maxPayloadPowerW;
        [Min(1)] public int payloadSlots = 2;
        [Tooltip("Combustible cargado (kg): forma parte de la masa total al lanzamiento.")]
        [Min(0)] public float propellantMassKg;
        [Tooltip("Impulso específico del sistema de propulsión (s): ~220 hidracina, ~300-320 bipropelente.")]
        [Min(1)] public float propulsionIspSeconds = 300f;
        [Tooltip("Consumo de subsistemas propios: aviónica, térmico, ACS (W).")]
        [Min(0)] public float housekeepingPowerW;
        [Tooltip("Telemetría de salud del satélite (GB/día).")]
        [Min(0)] public float housekeepingDataGBPerDay = 0.05f;
        [Min(0)] public float designLifeYears = 5f;
        [Tooltip("Escala del costo del equipo de operaciones (CubeSat universitario ≈ 0.05).")]
        [Min(0)] public float operationsCostScale = 1f;

        public float WetMassKg => massKg + propellantMassKg;

        public override ComponentCategory Category => ComponentCategory.Bus;
        public override MissionRole OwnerRole => MissionRole.SystemsEngineer;

        private void Reset() => socket = SpacecraftSocket.CoreBus;
    }
}
