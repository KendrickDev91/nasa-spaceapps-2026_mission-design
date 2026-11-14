using MissionDesign.Core;
using UnityEngine;

namespace MissionDesign.Data
{
    [CreateAssetMenu(menuName = "NASA SpaceApps/Instrument", fileName = "INS_New")]
    public class InstrumentSO : SpacecraftComponentSO
    {
        [Header("Instrumento científico")]
        public InstrumentType instrumentType;
        [Tooltip("Volumen de datos generado por día de operación (GB/día).")]
        [Min(0)] public float dataVolumeGBPerDay;
        [Tooltip("Potencia media en operación (W). peakPowerW es el pico.")]
        [Min(0)] public float averagePowerW;
        [Range(0f, 1f)] public float dutyCycle = 1f;
        [Tooltip("Valor científico de diseño de juego (1-100).")]
        [Range(1, 100)] public int scienceValue = 50;
        [Tooltip("Órbitas donde el instrumento rinde el 100% de su valor científico.")]
        public OrbitRegime[] preferredOrbits;
        [Tooltip("Etiquetas de objetivo científico (clima, océano, superficie lunar...).")]
        public string[] scienceObjectives;

        public override ComponentCategory Category => ComponentCategory.Instrument;
        public override MissionRole OwnerRole => MissionRole.PrincipalScientist;

        public bool IsPreferredOrbit(OrbitRegime regime)
        {
            if (preferredOrbits == null) return false;
            foreach (var r in preferredOrbits)
                if (r == regime) return true;
            return false;
        }

        private void Reset() => socket = SpacecraftSocket.TopPayloadSocket;
    }
}
