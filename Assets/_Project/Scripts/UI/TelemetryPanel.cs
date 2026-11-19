using System.Linq;
using System.Text;
using MissionDesign.Core;
using MissionDesign.Simulation;
using TMPro;
using UnityEngine;

namespace MissionDesign.UI
{
    /// <summary>Panel de los 6 recursos críticos + valor científico, actualizado en tiempo real.</summary>
    public sealed class TelemetryPanel : MonoBehaviour
    {
        [SerializeField] private ResourceBarView[] bars;
        [SerializeField] private TMP_Text scienceText;
        [SerializeField] private TMP_Text alertsText;
        [SerializeField] private int maxAlerts = 3;

        private MissionStateManager manager;

        private void Start()
        {
            manager = MissionStateManager.Instance;
            if (manager == null) return;
            manager.DesignEvaluated += OnDesignEvaluated;
            OnDesignEvaluated(manager.CurrentTelemetry, manager.CurrentValidation);
        }

        private void OnDestroy()
        {
            if (manager != null) manager.DesignEvaluated -= OnDesignEvaluated;
        }

        private void OnDesignEvaluated(TelemetryReport report, LaunchValidationResult _)
        {
            if (report == null) return;
            foreach (var bar in bars.Where(b => b != null))
                bar.Show(report.Get(bar.Kind));

            if (scienceText != null)
            {
                scienceText.text =
                    Loc.L($"VALOR CIENTÍFICO  <b>{report.expectedScienceValue:N0}</b> <color={UiTheme.Hex(UiTheme.TextDim)}>/ {report.potentialScienceValue:N0} potencial · " +
                          $"datos {report.dataReturnFraction:P0} · energía {report.powerAvailabilityFraction:P0}</color>",
                          $"SCIENCE VALUE  <b>{report.expectedScienceValue:N0}</b> <color={UiTheme.Hex(UiTheme.TextDim)}>/ {report.potentialScienceValue:N0} potential · " +
                          $"data {report.dataReturnFraction:P0} · power {report.powerAvailabilityFraction:P0}</color>");
            }

            if (alertsText != null)
            {
                var sb = new StringBuilder();
                foreach (var a in report.alerts.Take(maxAlerts))
                    sb.AppendLine($"<color={UiTheme.Hex(UiTheme.Warning)}>!</color> {a}");
                alertsText.text = sb.ToString();
            }
        }
    }
}
