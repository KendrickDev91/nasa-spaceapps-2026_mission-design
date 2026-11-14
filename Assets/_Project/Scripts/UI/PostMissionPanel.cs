using System.Collections.Generic;
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
    /// <summary>
    /// Análisis post-misión en 3 tarjetas: "Lo que salió bien", "Lo que falló" y "Puntuación"
    /// (rango A-F, misión real parecida y métricas clave).
    /// </summary>
    public sealed class PostMissionPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text verdictText;
        [SerializeField] private TMP_Text wentWellText;
        [SerializeField] private TMP_Text wentWrongText;
        [SerializeField] private TMP_Text rankText;
        [SerializeField] private TMP_Text similarText;
        [SerializeField] private TMP_Text metricsText;
        [SerializeField] private TMP_Text exportText;
        [SerializeField] private Button openReportsButton;
        [SerializeField] private Button backButton;
        [SerializeField] private int maxLinesPerCard = 6;

        private MissionStateManager manager;

        public string VerdictText => verdictText != null ? verdictText.text : string.Empty;
        public string WentWellText => wentWellText != null ? wentWellText.text : string.Empty;
        public string WentWrongText => wentWrongText != null ? wentWrongText.text : string.Empty;
        public string RankText => rankText != null ? rankText.text : string.Empty;

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
            Color verdictColor = o.verdict == MissionVerdict.Success ? UiTheme.Ok : o.verdict == MissionVerdict.PartialSuccess ? UiTheme.Warning : UiTheme.Critical;

            if (verdictText != null)
                verdictText.text = $"<color={UiTheme.Hex(verdictColor)}>{MissionOutcomeReport.VerdictLabel(o.verdict)}</color>  " +
                                   $"<size=45%>{o.verdictTitle}</size>";

            if (wentWellText != null) wentWellText.text = Card(o.wentWell, "Nada destacable esta vez: revisa la tarjeta de fallos.");
            if (wentWrongText != null) wentWrongText.text = Card(o.wentWrong, "¡Nada falló! Diseño sólido.");

            if (rankText != null)
            {
                Color rankColor = o.rank == "A" || o.rank == "B" ? UiTheme.Ok : o.rank == "C" || o.rank == "D" ? UiTheme.Warning : UiTheme.Critical;
                rankText.text = $"<color={UiTheme.Hex(rankColor)}>{o.rank}</color>  <size=50%>{o.score}/100</size>";
            }

            if (similarText != null)
                similarText.text = string.IsNullOrEmpty(o.similarMission)
                    ? string.Empty
                    : $"<color={UiTheme.Hex(UiTheme.TextDim)}>TU MISIÓN SE PARECE A</color>\n<b>{o.similarMission}</b>\n<size=85%>{o.similarMissionDescription}</size>";

            if (metricsText != null)
            {
                string dim = UiTheme.Hex(UiTheme.TextDim);
                metricsText.text =
                    $"<color={dim}>Ciencia</color>  {o.scienceAchieved:N0} / {o.scienceTarget:N0}\n" +
                    $"<color={dim}>Datos recibidos</color>  {o.dataReturnedTotalTB:N1} TB ({o.dataReturnFraction:P0})\n" +
                    $"<color={dim}>Operación</color>  {o.achievedOperationsDays:N0} / {o.plannedOperationsDays:N0} días\n" +
                    $"<color={dim}>Energía disponible</color>  {o.powerAvailabilityFraction:P0}\n" +
                    $"<color={dim}>Costo</color>  ${o.totalCostMUSD:N0}M / ${o.budgetLimitMUSD:N0}M\n" +
                    $"<color={dim}>Dificultad · semilla</color>  {(o.difficulty == DifficultyLevel.Basic ? "Básico" : "Avanzado")} · {o.seed}";
            }

            if (exportText != null)
                exportText.text = string.IsNullOrEmpty(manager.LastReportPath)
                    ? "Reporte no exportado."
                    : $"Reporte JSON/TXT: {manager.LastReportPath}";
        }

        private string Card(List<string> lines, string empty)
        {
            if (lines == null || lines.Count == 0) return $"<color={UiTheme.Hex(UiTheme.TextDim)}>{empty}</color>";
            var sb = new StringBuilder();
            foreach (var line in lines.Take(maxLinesPerCard)) sb.AppendLine($"• {StripPrefix(line)}");
            if (lines.Count > maxLinesPerCard) sb.Append($"<size=80%>… y {lines.Count - maxLinesPerCard} más en el reporte</size>");
            return sb.ToString();
        }

        /// <summary>Quita "Fallo parcial:", "A favor:", etc.: la tarjeta ya indica el tono.</summary>
        private static string StripPrefix(string line)
        {
            int colon = line.IndexOf(':');
            if (colon <= 0 || colon > 16) return line;
            string rest = line.Substring(colon + 1).TrimStart();
            return rest.Length > 0 ? char.ToUpperInvariant(rest[0]) + rest.Substring(1) : line;
        }

        private void OpenReportsFolder()
        {
            string folder = MissionReportExporter.ReportsFolder;
            Directory.CreateDirectory(folder);
            Application.OpenURL("file:///" + folder.Replace('\\', '/'));
        }
    }
}
