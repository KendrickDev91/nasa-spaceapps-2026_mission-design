using MissionDesign.Core;
using MissionDesign.Simulation;
using UnityEngine;

namespace MissionDesign.Multiplayer
{
    /// <summary>Alimenta el CollaborationLog con los eventos de la partida.</summary>
    public sealed class CollaborationTracker : MonoBehaviour
    {
        [SerializeField] private RoleTurnManager roles;

        private MissionStateManager manager;
        public CollaborationLog Log { get; } = new CollaborationLog();

        private void Start()
        {
            manager = MissionStateManager.Instance;
            if (manager == null) return;
            manager.Design.Changed += OnDesignChanged;
            manager.DesignEvaluated += OnDesignEvaluated;
            if (roles != null) roles.ApprovalsChanged += OnApprovalsChanged;
        }

        private void OnDestroy()
        {
            if (manager == null) return;
            if (manager.Design != null) manager.Design.Changed -= OnDesignChanged;
            manager.DesignEvaluated -= OnDesignEvaluated;
            if (roles != null) roles.ApprovalsChanged -= OnApprovalsChanged;
        }

        private void OnApprovalsChanged()
        {
            if (manager == null || manager.Phase != MissionPhase.Design) return;
            // Solo cuenta el rol que pulsó "Aprobar" (el rol activo). Con 1 jugador, un clic aprueba los 4 roles
            // a la vez, pero eso es una sola persona revisando, no cuatro.
            if (roles.IsApproved(roles.ActiveRole)) Log.RecordApproval(roles.ActiveRole);
        }

        private void OnDesignChanged()
        {
            if (manager.Phase == MissionPhase.Design && roles != null) Log.RecordChange(roles.ActiveRole);
        }

        private void OnDesignEvaluated(TelemetryReport t, LaunchValidationResult v) => Log.RecordEvaluation(v);
    }
}
