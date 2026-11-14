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
        }

        private void OnDestroy()
        {
            if (manager == null) return;
            if (manager.Design != null) manager.Design.Changed -= OnDesignChanged;
            manager.DesignEvaluated -= OnDesignEvaluated;
        }

        private void OnDesignChanged()
        {
            if (manager.Phase == MissionPhase.Design && roles != null) Log.RecordChange(roles.ActiveRole);
        }

        private void OnDesignEvaluated(TelemetryReport t, LaunchValidationResult v) => Log.RecordEvaluation(v);
    }
}
