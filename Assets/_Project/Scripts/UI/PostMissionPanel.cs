using System.IO;
using System.Linq;
using System.Text;
using MissionDesign.Core;
using MissionDesign.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionDesign.UI
{
    /// <summary>Análisis post-misión: veredicto, métricas clave y el desglose de POR QUÉ.</summary>
    public sealed class PostMissionPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text verdictText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text metricsText;
        [SerializeField] private TMP_Text explanationsText;
        [SerializeField] private TMP_Text eventsText;
        [SerializeField] private TMP_Text exportText;
        [SerializeField] private Button openReportsButton;
        [SerializeField] private Button backButton;

        private MissionStateManager manager;

        public string VerdictText => verdictText != null ? verdictText.text : string.Empty;
        public string ExplanationsText => explanationsText != null ? explanationsText.text : string.Empty;

        private void Start()
        {
            manager = MissionStateManager.Instance;
            if (manager == null) return;
            manager.MissionCompleted += Show;
            if (backButton != null) backButton.onClick.AddListener(manager.ReturnToDesign);
            if (openReportsButton != null) openReportsButton.onClick.AddListener(OpenReportsFolder);
            if (manager.LastOutcome != null) Show(manager.LastOutcome);
        }

        private void OnDestroy()
        {
            if (manager != null) manager.MissionCompleted -= Show;
        }

        public void Show(MissionOutcomeReport o)
        {
            if (o == null) return;
            Color c = o.verdict == MissionVerdict.Success ? UiTheme.Ok : o.verdict == MissionVerdict.PartialSuccess ? UiTheme.Warning : UiTheme.Critical;

            if (verdictText != null)
                verdictText.text = $"<color={UiTheme.Hex(c)}>{MissionOutcomeReport.VerdictLabel(o.verdict)}</color>\n<size=55%>{o.verdictTitle}</size>";
            if (scoreText != null)
                scoreText.text = $"<size=40%><color={UiTheme.Hex(UiTheme.TextDim)}>PUNTUACIÓN</color></size>\n{o.score}<size=40%>/100</size>";

            if (metricsText != null)
            {
                string dim = UiTheme.Hex(UiTheme.TextDim);
                metricsText.text =
                    $"<color={dim}>Ciencia</color>  {o.scienceAchieved:N0} / {o.scienceTarget:N0}\n" +
                    $"<color={dim}>Datos recibidos</color>  {o.dataReturnedTotalTB:N1} TB ({o.dataReturnFraction:P0})\n" +
                    $"<color={dim}>Datos perdidos</color>  {o.dataLostTotalTB:N1} TB\n" +
                    $"<color={dim}>Operación</color>  {o.achievedOperationsDays:N0} / {o.plannedOperationsDays:N0} días\n" +
                    $"<color={dim}>Energía disponible</color>  {o.powerAvailabilityFraction:P0}\n" +
                    $"<color={dim}>Costo</color>  ${o.totalCostMUSD:N0}M / ${o.budgetLimitMUSD:N0}M\n" +
                    $"<color={dim}>Semilla</color>  {o.seed}";
            }

            if (explanationsText != null)
            {
                var sb = new StringBuilder();
                foreach (var e in o.explanations)
                {
                    Color lc = e.StartsWith("Éxito") || e.StartsWith("A favor") ? UiTheme.Ok
                        : e.StartsWith("Fallo parcial") || e.StartsWith("Atención") || e.StartsWith("En contra") ? UiTheme.Warning
                        : e.StartsWith("Fallo") ? UiTheme.Critical : UiTheme.Text;
                    sb.AppendLine($"<color={UiTheme.Hex(lc)}>•</color> {e}");
                }
                explanationsText.text = sb.ToString();
            }

            if (eventsText != null)
            {
                eventsText.text = o.events.Count == 0
                    ? "Sin eventos aleatorios."
                    : string.Join("\n", o.events.Select(e =>
                        $"<color={UiTheme.Hex(e.positive ? UiTheme.Ok : UiTheme.Warning)}><b>Día {e.missionDay:N0} · {e.displayName}</b></color>\n" +
                        $"{e.impact}\n" +
                        $"<size=90%><color={UiTheme.Hex(UiTheme.TextDim)}>{e.educationalFact}</color></size>"));
            }

            if (exportText != null)
                exportText.text = string.IsNullOrEmpty(manager.LastReportPath)
                    ? "Reporte no exportado."
                    : $"Reporte JSON/TXT: {manager.LastReportPath}";
        }

        private void OpenReportsFolder()
        {
            string folder = MissionReportExporter.ReportsFolder;
            Directory.CreateDirectory(folder);
            Application.OpenURL("file:///" + folder.Replace('\\', '/'));
        }
    }
}
