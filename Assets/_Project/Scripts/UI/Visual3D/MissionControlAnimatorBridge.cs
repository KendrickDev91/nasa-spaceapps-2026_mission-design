using System;
using System.Collections.Generic;
using System.Linq;
using MissionDesign.Core;
using MissionDesign.Simulation;
using UnityEngine;

namespace MissionDesign.UI.Visual3D
{
    /// <summary>
    /// Conecta el estado de la misión con los personajes de Mixamo de la sala de control.
    /// El Animator Controller de cada personaje debe tener los triggers Celebrate, Stress, Type y PointScreen;
    /// si falta alguno, se ignora sin errores (útil mientras se importan los modelos).
    /// </summary>
    public sealed class MissionControlAnimatorBridge : MonoBehaviour
    {
        public static readonly int Celebrate = Animator.StringToHash("Celebrate");
        public static readonly int Stress = Animator.StringToHash("Stress");
        public static readonly int Type = Animator.StringToHash("Type");
        public static readonly int PointScreen = Animator.StringToHash("PointScreen");

        [Serializable]
        public sealed class Operator
        {
            public MissionRole role;
            public Animator animator;
        }

        [SerializeField] private List<Operator> operators = new List<Operator>();
        [Tooltip("Personajes de ambiente que reaccionan a lanzamiento y resultado.")]
        [SerializeField] private Animator[] crowd = Array.Empty<Animator>();
        [SerializeField] private bool logTriggers;

        private MissionStateManager manager;
        private readonly HashSet<MissionRole> stressedRoles = new HashSet<MissionRole>();

        private void Start()
        {
            manager = MissionStateManager.Instance;
            if (manager == null) return;
            manager.DesignEvaluated += OnDesignEvaluated;
            manager.LaunchApproved += OnLaunchApproved;
            manager.LaunchRejected += OnLaunchRejected;
            manager.PhaseChanged += OnPhaseChanged;
            manager.MissionCompleted += OnMissionCompleted;
        }

        private void OnDestroy()
        {
            if (manager == null) return;
            manager.DesignEvaluated -= OnDesignEvaluated;
            manager.LaunchApproved -= OnLaunchApproved;
            manager.LaunchRejected -= OnLaunchRejected;
            manager.PhaseChanged -= OnPhaseChanged;
            manager.MissionCompleted -= OnMissionCompleted;
        }

        private void OnPhaseChanged(MissionPhase phase)
        {
            // Durante operaciones el equipo trabaja en sus consolas.
            if (phase == MissionPhase.ScienceOperations || phase == MissionPhase.DataTransmission) TriggerAll(Type);
        }

        private void OnMissionCompleted(MissionOutcomeReport outcome) =>
            ReportMissionOutcome(outcome != null && outcome.verdict != MissionVerdict.Failure);

        // ───────────── API pública (también para el simulador y el post-misión) ─────────────

        public void TriggerRole(MissionRole role, int trigger)
        {
            foreach (var op in operators.Where(o => o.role == role))
                Fire(op.animator, trigger);
        }

        public void TriggerAll(int trigger)
        {
            foreach (var op in operators) Fire(op.animator, trigger);
            foreach (var a in crowd) Fire(a, trigger);
        }

        /// <summary>Llamar al terminar la simulación.</summary>
        public void ReportMissionOutcome(bool success) => TriggerAll(success ? Celebrate : Stress);

        // ───────────── Reacciones automáticas ─────────────

        private void OnDesignEvaluated(TelemetryReport report, LaunchValidationResult validation)
        {
            if (validation == null) return;
            var nowStressed = new HashSet<MissionRole>(validation.Blocking.Select(i => i.responsibleRole));

            // Solo se dispara al cambiar el estado, para no reiniciar la animación en cada clic.
            foreach (var role in nowStressed.Where(r => !stressedRoles.Contains(r)))
                TriggerRole(role, Stress);
            foreach (var role in stressedRoles.Where(r => !nowStressed.Contains(r)))
                TriggerRole(role, Type);

            stressedRoles.Clear();
            stressedRoles.UnionWith(nowStressed);
        }

        private void OnLaunchApproved() => TriggerAll(PointScreen);

        private void OnLaunchRejected(LaunchValidationResult validation)
        {
            if (validation == null) return;
            foreach (var role in validation.Blocking.Select(i => i.responsibleRole).Distinct())
                TriggerRole(role, Stress);
        }

        private void Fire(Animator animator, int trigger)
        {
            if (animator == null || animator.runtimeAnimatorController == null || !animator.isActiveAndEnabled) return;
            foreach (var p in animator.parameters)
            {
                if (p.nameHash == trigger && p.type == AnimatorControllerParameterType.Trigger)
                {
                    animator.SetTrigger(trigger);
                    if (logTriggers) Debug.Log($"[AnimatorBridge] {animator.name} → {p.name}");
                    return;
                }
            }
        }
    }
}
