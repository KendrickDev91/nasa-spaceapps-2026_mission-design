using System;
using System.Linq;
using MissionDesign.Data;
using MissionDesign.Simulation;
using UnityEngine;

namespace MissionDesign.Core
{
    /// <summary>
    /// Punto central de la partida. Posee el diseño, el motor de telemetría y la fase actual,
    /// y publica eventos que consumen la UI, el ensamblador 3D y la sala de control.
    /// Ningún consumidor se referencia entre sí: todos escuchan aquí.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class MissionStateManager : MonoBehaviour
    {
        [SerializeField] private NasaDatabaseSO database;
        [Tooltip("Escenario que se carga al iniciar (índice en la base de datos).")]
        [SerializeField] private int defaultScenarioIndex;

        public static MissionStateManager Instance { get; private set; }

        public NasaDatabaseSO Database => database;
        public SpacecraftDesign Design { get; private set; }
        public MissionTelemetry Telemetry { get; private set; }
        public TelemetryReport CurrentTelemetry => Telemetry?.Current;
        public LaunchValidationResult CurrentValidation { get; private set; }
        public MissionPhase Phase { get; private set; } = MissionPhase.Design;

        /// <summary>Se dispara tras cada cambio del diseño con la telemetría y la validación nuevas.</summary>
        public event Action<TelemetryReport, LaunchValidationResult> DesignEvaluated;
        public event Action<MissionPhase> PhaseChanged;
        /// <summary>Se intentó lanzar y el validador lo impidió.</summary>
        public event Action<LaunchValidationResult> LaunchRejected;
        public event Action LaunchApproved;
        /// <summary>La simulación terminó; la fase pasa a PostMission.</summary>
        public event Action<MissionOutcomeReport> MissionCompleted;

        public MissionOutcomeReport LastOutcome { get; private set; }
        public string LastReportPath { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[MissionStateManager] Ya existe una instancia; se destruye la duplicada.", this);
                Destroy(gameObject);
                return;
            }
            Instance = this;

            Design = new SpacecraftDesign();
            Telemetry = new MissionTelemetry(Design);
            Telemetry.Updated += OnTelemetryUpdated;
            OnTelemetryUpdated(Telemetry.Current);

            if (database != null && database.scenarios.Count > 0)
                SelectScenario(database.scenarios[Mathf.Clamp(defaultScenarioIndex, 0, database.scenarios.Count - 1)]);
            else
                Debug.LogWarning("[MissionStateManager] Sin base de datos asignada: ejecuta NASA SpaceApps → Generate Initial NASA Database.", this);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            if (Telemetry != null)
            {
                Telemetry.Updated -= OnTelemetryUpdated;
                Telemetry.Dispose();
            }
            Instance = null;
        }

        private void OnTelemetryUpdated(TelemetryReport report)
        {
            CurrentValidation = OrbitAndLaunchValidator.Validate(Design, report);
            DesignEvaluated?.Invoke(report, CurrentValidation);
        }

        // ───────────── Acciones del jugador (las llama la UI de cada rol) ─────────────

        public void SelectScenario(MissionScenarioSO scenario)
        {
            if (!CanEdit()) return;
            Design.SetScenario(scenario);
            // Si la órbita actual no sirve al nuevo objetivo, se propone la primera permitida.
            if (scenario != null && scenario.allowedOrbits != null && scenario.allowedOrbits.Length > 0 &&
                (Design.Orbit == null || !scenario.allowedOrbits.Contains(Design.Orbit.regime)) && database != null)
            {
                Design.SetOrbit(database.GetOrbit(scenario.allowedOrbits[0]));
            }
        }

        public void SelectOrbit(OrbitSO orbit)
        {
            if (CanEdit()) Design.SetOrbit(orbit);
        }

        public void SelectLaunchVehicle(LaunchVehicleSO vehicle)
        {
            if (CanEdit()) Design.SetLaunchVehicle(vehicle);
        }

        public void SelectGroundNetwork(GroundNetworkSO network)
        {
            if (CanEdit()) Design.SetGroundNetwork(network);
        }

        public void SetDifficulty(DifficultyLevel level)
        {
            if (CanEdit()) Design.SetDifficulty(level);
        }

        /// <summary>Coloca bus/panel/batería/antena en su ranura o añade un instrumento.</summary>
        public void SelectComponent(SpacecraftComponentSO component)
        {
            if (CanEdit()) Design.SetComponent(component);
        }

        public void RemoveInstrument(InstrumentSO instrument)
        {
            if (CanEdit()) Design.RemoveInstrument(instrument);
        }

        /// <summary>Añade el instrumento si no está; si ya está, lo quita.</summary>
        public void ToggleInstrument(InstrumentSO instrument)
        {
            if (!CanEdit() || instrument == null) return;
            if (Design.Instruments.Contains(instrument)) Design.RemoveInstrument(instrument);
            else Design.AddInstrument(instrument);
        }

        /// <summary>Intenta pasar a la fase de lanzamiento. El consenso de roles se comprueba antes (RoleTurnManager).</summary>
        public bool TryLaunch()
        {
            if (Phase != MissionPhase.Design) return false;
            if (CurrentValidation == null || !CurrentValidation.canLaunch)
            {
                LaunchRejected?.Invoke(CurrentValidation);
                return false;
            }
            SetPhase(MissionPhase.Launch);
            LaunchApproved?.Invoke();
            return true;
        }

        public void SetPhase(MissionPhase phase)
        {
            if (Phase == phase) return;
            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }

        /// <summary>Lo llama el MissionSimulationRunner al terminar la reproducción.</summary>
        public void CompleteMission(MissionOutcomeReport outcome, string reportPath)
        {
            LastOutcome = outcome;
            LastReportPath = reportPath;
            SetPhase(MissionPhase.PostMission);
            MissionCompleted?.Invoke(outcome);
        }

        /// <summary>Vuelve a la mesa de diseño conservando las piezas (iterar tras un fallo).</summary>
        public void ReturnToDesign() => SetPhase(MissionPhase.Design);

        private bool CanEdit()
        {
            if (Phase == MissionPhase.Design) return true;
            Debug.LogWarning($"[MissionStateManager] El diseño está congelado durante la fase {Phase}.");
            return false;
        }
    }
}
