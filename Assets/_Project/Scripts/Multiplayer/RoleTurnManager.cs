using System;
using System.Collections.Generic;
using System.Linq;
using MissionDesign.Core;
using MissionDesign.Simulation;
using UnityEngine;

namespace MissionDesign.Multiplayer
{
    /// <summary>
    /// Modo cooperativo hot-seat de 1 a 4 jugadores en la misma pantalla.
    /// Cada rol pertenece a un jugador (rol % jugadores). Antes de lanzar, cada jugador debe aprobar
    /// el diseño; cualquier cambio de piezas anula las aprobaciones (negociación de trade-offs).
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public sealed class RoleTurnManager : MonoBehaviour
    {
        public static readonly MissionRole[] AllRoles =
        {
            MissionRole.MissionDirector, MissionRole.SystemsEngineer,
            MissionRole.PrincipalScientist, MissionRole.TelecomLaunchEngineer
        };

        [Range(1, 4)] [SerializeField] private int playerCount = 4;
        [SerializeField] private string[] playerNames = { "Jugador 1", "Jugador 2", "Jugador 3", "Jugador 4" };
        [SerializeField] private MissionRole startingRole = MissionRole.MissionDirector;

        private readonly HashSet<MissionRole> approvals = new HashSet<MissionRole>();
        private MissionStateManager manager;

        public MissionRole ActiveRole { get; private set; }
        public int PlayerCount => playerCount;

        /// <summary>(rol activo, índice de jugador que lo controla).</summary>
        public event Action<MissionRole, int> ActiveRoleChanged;
        public event Action ApprovalsChanged;

        public static string RoleDisplayName(MissionRole role)
        {
            switch (role)
            {
                case MissionRole.MissionDirector: return "Director de Misión";
                case MissionRole.SystemsEngineer: return "Ing. de Sistemas";
                case MissionRole.PrincipalScientist: return "Científico Principal";
                case MissionRole.TelecomLaunchEngineer: return "Ing. Telecom y Lanzamiento";
                default: return role.ToString();
            }
        }

        private void Awake() => ActiveRole = startingRole;

        private void Start()
        {
            manager = MissionStateManager.Instance;
            if (manager != null)
            {
                manager.Design.Changed += ResetApprovals;
                manager.PhaseChanged += OnPhaseChanged;
            }
            ActiveRoleChanged?.Invoke(ActiveRole, PlayerForRole(ActiveRole));
        }

        private void OnDestroy()
        {
            if (manager == null) return;
            if (manager.Design != null) manager.Design.Changed -= ResetApprovals;
            manager.PhaseChanged -= OnPhaseChanged;
        }

        /// <summary>Al volver a la mesa de diseño tras una misión, el consenso se negocia de nuevo.</summary>
        private void OnPhaseChanged(MissionPhase phase)
        {
            if (phase == MissionPhase.Design) ResetApprovals();
        }

        public void SetPlayerCount(int count)
        {
            playerCount = Mathf.Clamp(count, 1, 4);
            ResetApprovals();
            ActiveRoleChanged?.Invoke(ActiveRole, PlayerForRole(ActiveRole));
        }

        public int PlayerForRole(MissionRole role) => (int)role % Mathf.Max(1, playerCount);

        public string PlayerName(int index) =>
            playerNames != null && index >= 0 && index < playerNames.Length && !string.IsNullOrEmpty(playerNames[index])
                ? playerNames[index]
                : $"Jugador {index + 1}";

        public void SetActiveRole(MissionRole role)
        {
            ActiveRole = role;
            ActiveRoleChanged?.Invoke(role, PlayerForRole(role));
        }

        /// <summary>Pasa el control al siguiente rol (el dispositivo cambia de manos).</summary>
        public void NextTurn() => SetActiveRole(AllRoles[((int)ActiveRole + 1) % AllRoles.Length]);

        // ───────────── Negociación y consenso ─────────────

        public bool IsApproved(MissionRole role) => approvals.Contains(role);
        public bool AllApproved => AllRoles.All(approvals.Contains);

        /// <summary>El jugador del rol activo aprueba el diseño por todos los roles que controla.</summary>
        public void ApproveAsActivePlayer()
        {
            int player = PlayerForRole(ActiveRole);
            foreach (var role in AllRoles)
                if (PlayerForRole(role) == player) approvals.Add(role);
            ApprovalsChanged?.Invoke();
        }

        public void ResetApprovals()
        {
            if (approvals.Count == 0) return;
            approvals.Clear();
            ApprovalsChanged?.Invoke();
        }

        /// <summary>Problemas que bloquean y que este rol debe resolver.</summary>
        public static IEnumerable<ValidationIssue> ConflictsFor(MissionRole role, LaunchValidationResult validation) =>
            validation == null
                ? Enumerable.Empty<ValidationIssue>()
                : validation.issues.Where(i => i.responsibleRole == role && i.severity != IssueSeverity.Info);

        /// <summary>Lanza solo si hay consenso de todos los jugadores y el validador lo permite.</summary>
        public bool TryRequestLaunch(out string message)
        {
            if (manager == null)
            {
                message = "No hay MissionStateManager en la escena.";
                return false;
            }
            if (!AllApproved)
            {
                var pending = AllRoles.Where(r => !approvals.Contains(r)).Select(RoleDisplayName);
                message = "Falta consenso del equipo: " + string.Join(", ", pending) + ".";
                return false;
            }
            if (!manager.TryLaunch())
            {
                int blocking = manager.CurrentValidation?.Blocking.Count() ?? 0;
                message = $"Lanzamiento bloqueado: {blocking} problema(s) crítico(s) por resolver.";
                return false;
            }
            message = "¡Lanzamiento autorizado! Cuenta regresiva iniciada.";
            return true;
        }
    }
}
