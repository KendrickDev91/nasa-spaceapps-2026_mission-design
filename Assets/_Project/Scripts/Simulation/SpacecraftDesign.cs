using System;
using System.Collections.Generic;
using MissionDesign.Core;
using MissionDesign.Data;

namespace MissionDesign.Simulation
{
    /// <summary>
    /// Estado del diseño en curso (lo que el equipo ha elegido). No conoce la UI ni el 3D:
    /// emite eventos que escuchan MissionTelemetry, el ensamblador modular y los paneles de rol.
    /// </summary>
    public sealed class SpacecraftDesign
    {
        private MissionScenarioSO scenario;
        private OrbitSO orbit;
        private LaunchVehicleSO launchVehicle;
        private SpacecraftBusSO bus;
        private SolarArraySO solarArray;
        private BatterySO battery;
        private AntennaSO antenna;
        private OnboardComputerSO computer;
        private AttitudeControlSO attitudeControl;
        private GroundNetworkSO groundNetwork;
        private DifficultyLevel difficulty = DifficultyLevel.Basic;
        private readonly List<InstrumentSO> instruments = new List<InstrumentSO>();

        /// <summary>Cualquier cambio del diseño (pieza, órbita, cohete, escenario).</summary>
        public event Action Changed;
        /// <summary>Una pieza física entró al ensamblaje (para instanciar su prefab).</summary>
        public event Action<SpacecraftComponentSO> ComponentAdded;
        /// <summary>Una pieza física salió del ensamblaje (para destruir su prefab).</summary>
        public event Action<SpacecraftComponentSO> ComponentRemoved;

        public MissionScenarioSO Scenario => scenario;
        public OrbitSO Orbit => orbit;
        public LaunchVehicleSO LaunchVehicle => launchVehicle;
        public SpacecraftBusSO Bus => bus;
        public SolarArraySO SolarArray => solarArray;
        public BatterySO Battery => battery;
        public AntennaSO Antenna => antenna;
        public OnboardComputerSO Computer => computer;
        public AttitudeControlSO AttitudeControl => attitudeControl;
        public GroundNetworkSO GroundNetwork => groundNetwork;
        public DifficultyLevel Difficulty => difficulty;
        public bool IsAdvanced => difficulty == DifficultyLevel.Advanced;
        public IReadOnlyList<InstrumentSO> Instruments => instruments;

        /// <summary>Todas las piezas físicas montadas (bus, panel, batería, antena, instrumentos).</summary>
        public IEnumerable<SpacecraftComponentSO> AllComponents
        {
            get
            {
                if (bus != null) yield return bus;
                if (solarArray != null) yield return solarArray;
                if (battery != null) yield return battery;
                if (antenna != null) yield return antenna;
                if (computer != null) yield return computer;
                if (attitudeControl != null) yield return attitudeControl;
                foreach (var i in instruments)
                    if (i != null) yield return i;
            }
        }

        public void SetScenario(MissionScenarioSO value)
        {
            if (scenario == value) return;
            scenario = value;
            Changed?.Invoke();
        }

        public void SetOrbit(OrbitSO value)
        {
            if (orbit == value) return;
            orbit = value;
            Changed?.Invoke();
        }

        public void SetLaunchVehicle(LaunchVehicleSO value)
        {
            if (launchVehicle == value) return;
            launchVehicle = value;
            Changed?.Invoke();
        }

        public void SetGroundNetwork(GroundNetworkSO value)
        {
            if (groundNetwork == value) return;
            groundNetwork = value;
            Changed?.Invoke();
        }

        /// <summary>Al pasar a Básico se retiran las piezas exclusivas de Avanzado.</summary>
        public void SetDifficulty(DifficultyLevel value)
        {
            if (difficulty == value) return;
            difficulty = value;
            if (value == DifficultyLevel.Basic)
            {
                Swap(ref computer, null, notify: false);
                Swap(ref attitudeControl, null, notify: false);
            }
            Changed?.Invoke();
        }

        public void SetBus(SpacecraftBusSO value) => Swap(ref bus, value);
        public void SetSolarArray(SolarArraySO value) => Swap(ref solarArray, value);
        public void SetBattery(BatterySO value) => Swap(ref battery, value);
        public void SetAntenna(AntennaSO value) => Swap(ref antenna, value);
        public void SetComputer(OnboardComputerSO value) => Swap(ref computer, value);
        public void SetAttitudeControl(AttitudeControlSO value) => Swap(ref attitudeControl, value);

        /// <summary>Copia independiente (sin suscriptores) para previsualizar consecuencias.</summary>
        public SpacecraftDesign Clone()
        {
            var c = new SpacecraftDesign
            {
                scenario = scenario, orbit = orbit, launchVehicle = launchVehicle, bus = bus, solarArray = solarArray,
                battery = battery, antenna = antenna, computer = computer, attitudeControl = attitudeControl,
                groundNetwork = groundNetwork, difficulty = difficulty
            };
            c.instruments.AddRange(instruments);
            return c;
        }

        /// <summary>
        /// Añade un instrumento. No se limita por slots a propósito: el exceso lo detecta el
        /// validador y alimenta la fase de "Negociación de Trade-offs".
        /// </summary>
        public void AddInstrument(InstrumentSO instrument)
        {
            if (instrument == null) return;
            instruments.Add(instrument);
            ComponentAdded?.Invoke(instrument);
            Changed?.Invoke();
        }

        public bool RemoveInstrument(InstrumentSO instrument)
        {
            if (!instruments.Remove(instrument)) return false;
            ComponentRemoved?.Invoke(instrument);
            Changed?.Invoke();
            return true;
        }

        public void ClearInstruments()
        {
            if (instruments.Count == 0) return;
            var removed = instruments.ToArray();
            instruments.Clear();
            foreach (var i in removed) ComponentRemoved?.Invoke(i);
            Changed?.Invoke();
        }

        /// <summary>Punto de entrada genérico para la UI: coloca cualquier pieza en su ranura.</summary>
        public void SetComponent(SpacecraftComponentSO component)
        {
            switch (component)
            {
                case SpacecraftBusSO b: SetBus(b); break;
                case SolarArraySO s: SetSolarArray(s); break;
                case BatterySO bt: SetBattery(bt); break;
                case AntennaSO a: SetAntenna(a); break;
                case OnboardComputerSO cdh: SetComputer(cdh); break;
                case AttitudeControlSO adcs: SetAttitudeControl(adcs); break;
                case InstrumentSO i: AddInstrument(i); break;
                case null: break;
                default: throw new ArgumentException($"Tipo de pieza no soportado: {component.GetType().Name}");
            }
        }

        private void Swap<T>(ref T field, T value, bool notify = true) where T : SpacecraftComponentSO
        {
            if (field == value) return;
            var old = field;
            field = value;
            if (old != null) ComponentRemoved?.Invoke(old);
            if (value != null) ComponentAdded?.Invoke(value);
            if (notify) Changed?.Invoke();
        }
    }
}
