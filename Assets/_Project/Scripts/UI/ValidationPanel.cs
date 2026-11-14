using System.Linq;
using System.Text;
using MissionDesign.Core;
using MissionDesign.Multiplayer;
using MissionDesign.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionDesign.UI
{
    /// <summary>
    /// Mesa de negociación de trade-offs: lista los conflictos por rol, recoge el consenso
    /// del equipo y dispara "Lanzar Misión".
    /// </summary>
    public sealed class ValidationPanel : MonoBehaviour
    {
        [SerializeField] private RoleTurnManager roles;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text issuesText;
        [SerializeField] private TMP_Text consensusText;
        [SerializeField] private Button approveButton;
        [SerializeField] private Button launchButton;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private int maxIssues = 7;

        private MissionStateManager manager;

        private void Start()
        {
            manager = MissionStateManager.Instance;
            if (manager == null || roles == null) return;

            if (approveButton != null) approveButton.onClick.AddListener(Approve);
            if (launchButton != null) launchButton.onClick.AddListener(Launch);

            manager.DesignEvaluated += OnDesignEvaluated;
            manager.PhaseChanged += OnPhaseChanged;
            roles.ApprovalsChanged += RefreshConsensus;
            roles.ActiveRoleChanged += OnRoleChanged;

            OnDesignEvaluated(manager.CurrentTelemetry, manager.CurrentValidation);
            SetMessage(string.Empty);
        }

        private void OnDestroy()
        {
            if (manager != null)
            {
                manager.DesignEvaluated -= OnDesignEvaluated;
                manager.PhaseChanged -= OnPhaseChanged;
            }
            if (roles != null)
            {
                roles.ApprovalsChanged -= RefreshConsensus;
                roles.ActiveRoleChanged -= OnRoleChanged;
            }
        }

        public void Approve()
        {
            roles.ApproveAsActivePlayer();
            SetMessage($"{roles.PlayerName(roles.PlayerForRole(roles.ActiveRole))} aprobó el diseño.");
        }

        public void Launch()
        {
            roles.TryRequestLaunch(out string message);
            SetMessage(message);
        }

        private void OnRoleChanged(MissionRole role, int player) => RefreshConsensus();

        private void OnPhaseChanged(MissionPhase phase)
        {
            if (statusText != null && phase != MissionPhase.Design)
                statusText.text = $"<color={UiTheme.Hex(UiTheme.Accent)}>FASE: {phase.ToString().ToUpperInvariant()}</color>";
            RefreshConsensus();
        }

        private void OnDesignEvaluated(TelemetryReport report, LaunchValidationResult v)
        {
            if (v == null) return;
            int blocking = v.Blocking.Count();
            int warnings = v.Warnings.Count();

            if (statusText != null)
            {
                statusText.text = v.canLaunch
                    ? $"<color={UiTheme.Hex(UiTheme.Ok)}>LISTO PARA LANZAR</color>  <size=70%>{warnings} advertencia(s) · Δv {v.requiredDeltaVKmS:F1} km/s · margen {v.capacityMarginFraction:P0}</size>"
                    : $"<color={UiTheme.Hex(UiTheme.Critical)}>LANZAMIENTO BLOQUEADO</color>  <size=70%>{blocking} conflicto(s) · {warnings} advertencia(s)</size>";
            }

            if (issuesText != null)
            {
                var sb = new StringBuilder();
                foreach (var issue in v.issues.OrderByDescending(i => i.severity).Take(maxIssues))
                {
                    string color = UiTheme.Hex(UiTheme.SeverityColor(issue.severity));
                    sb.AppendLine($"<color={color}><b>[{UiTheme.RoleShort(issue.responsibleRole)}] {issue.title}</b></color>  " +
                                  $"<size=85%><color={UiTheme.Hex(UiTheme.TextDim)}>{issue.explanation}</color></size>");
                }
                int hidden = v.issues.Count - maxIssues;
                if (hidden > 0) sb.Append($"<size=80%>… y {hidden} más</size>");
                issuesText.text = v.issues.Count == 0 ? "Sin conflictos: el diseño es consistente." : sb.ToString();
            }

            RefreshConsensus();
        }

        private void RefreshConsensus()
        {
            if (consensusText != null)
            {
                var parts = RoleTurnManager.AllRoles.Select(r =>
                    roles.IsApproved(r)
                        ? $"<color={UiTheme.Hex(UiTheme.Ok)}>[OK] {UiTheme.RoleShort(r)}</color>"
                        : $"<color={UiTheme.Hex(UiTheme.TextDim)}>[  ] {UiTheme.RoleShort(r)}</color>");
                consensusText.text = $"<size=80%><color={UiTheme.Hex(UiTheme.TextDim)}>CONSENSO DEL EQUIPO</color></size>\n" + string.Join("  ", parts);
            }

            bool designPhase = manager.Phase == MissionPhase.Design;
            if (approveButton != null) approveButton.interactable = designPhase && !roles.IsApproved(roles.ActiveRole);
            if (launchButton != null) launchButton.interactable = designPhase;
        }

        private void SetMessage(string text)
        {
            if (messageText != null) messageText.text = text;
        }
    }
}
