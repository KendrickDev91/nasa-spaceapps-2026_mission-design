using System.Collections.Generic;
using MissionDesign.Core;
using OrbitRegime = MissionDesign.Core.OrbitRegime;
using MissionDesign.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionDesign.UI
{
    /// <summary>Consola de misión durante la simulación: fase actual, progreso y cronología en vivo.</summary>
    public sealed class SimulationPanel : MonoBehaviour
    {
        [SerializeField] private MissionSimulationRunner runner;
        [SerializeField] private TMP_Text phaseText;
        [SerializeField] private RectTransform progressFill;
        [SerializeField] private TMP_Text logText;
        [SerializeField] private Button skipButton;
        [SerializeField] private int maxLines = 9;

        private readonly List<string> lines = new List<string>();
        private MissionStateManager manager;
        private Vector2 homeAnchorMin, homeAnchorMax, homePivot, homeOffsetMin, homeOffsetMax;
        private bool homeSaved;

        private static (MissionPhase phase, string label)[] Steps => new[]
        {
            (MissionPhase.Launch, Loc.L("LANZAMIENTO", "LAUNCH")),
            (MissionPhase.OrbitInsertion, Loc.L("INSERCIÓN", "ORBIT INSERTION")),
            (MissionPhase.ScienceOperations, Loc.L("CIENCIA", "SCIENCE")),
            (MissionPhase.DataTransmission, Loc.L("TRANSMISIÓN", "DOWNLINK")),
            (MissionPhase.ReturnAndRecovery, Loc.L("REGRESO", "RETURN"))
        };

        private void Start()
        {
            manager = MissionStateManager.Instance;
            if (manager == null || runner == null) return;
            runner.EntryEmitted += AddLine;
            runner.ProgressChanged += SetProgress;
            manager.PhaseChanged += OnPhaseChanged;
            if (skipButton != null) skipButton.onClick.AddListener(runner.SkipToEnd);
            SetProgress(0f);
        }

        private void OnDestroy()
        {
            if (runner != null)
            {
                runner.EntryEmitted -= AddLine;
                runner.ProgressChanged -= SetProgress;
            }
            if (manager != null) manager.PhaseChanged -= OnPhaseChanged;
        }

        private void OnPhaseChanged(MissionPhase phase)
        {
            if (phase == MissionPhase.Launch)
            {
                lines.Clear();
                if (logText != null) logText.text = string.Empty;
                SetProgress(0f);
            }

            bool returns = manager.Design.Orbit != null && manager.Design.Orbit.regime == OrbitRegime.LunarFlyby;
            if (phase == MissionPhase.Launch) Dock(returns);
            if (phaseText == null) return;
            var parts = new List<string>();
            foreach (var (p, label) in Steps)
            {
                if (p == MissionPhase.ReturnAndRecovery && !returns) continue;
                string color = p == phase ? UiTheme.Hex(UiTheme.Accent) : p < phase ? UiTheme.Hex(UiTheme.Ok) : UiTheme.Hex(UiTheme.TextDim);
                parts.Add(p == phase ? $"<color={color}><b>{label}</b></color>" : $"<color={color}>{label}</color>");
            }
            // Con 5 fases (misión con regreso) el texto se ajusta en una sola línea.
            phaseText.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            if (!phaseText.enableAutoSizing)
            {
                phaseText.fontSizeMax = phaseText.fontSize;
                phaseText.fontSizeMin = phaseText.fontSize * 0.6f;
                phaseText.enableAutoSizing = true;
            }
            phaseText.text = string.Join(parts.Count > 4 ? " » " : "  »  ", parts);
        }

        /// <summary>En el sobrevuelo lunar el panel pasa a la columna derecha para no tapar la Luna del diagrama.</summary>
        private void Dock(bool rightColumn)
        {
            var rt = (RectTransform)transform;
            if (!homeSaved)
            {
                homeAnchorMin = rt.anchorMin; homeAnchorMax = rt.anchorMax; homePivot = rt.pivot;
                homeOffsetMin = rt.offsetMin; homeOffsetMax = rt.offsetMax;
                homeSaved = true;
            }
            if (rightColumn)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(1f, 0f);
                rt.sizeDelta = new Vector2(560f, 372f);
                rt.anchoredPosition = new Vector2(-20f, 16f);
            }
            else
            {
                rt.anchorMin = homeAnchorMin; rt.anchorMax = homeAnchorMax; rt.pivot = homePivot;
                rt.offsetMin = homeOffsetMin; rt.offsetMax = homeOffsetMax;
            }
        }

        private void AddLine(SimulationLogEntry entry)
        {
            Color c = entry.tone == LogTone.Critical ? UiTheme.Critical
                : entry.tone == LogTone.Warning ? UiTheme.Warning
                : entry.tone == LogTone.Positive ? UiTheme.Ok : UiTheme.Text;
            lines.Add($"<color={UiTheme.Hex(UiTheme.TextDim)}>{Loc.L("Día", "Day")} {entry.missionDay,4:N0}</color>  <color={UiTheme.Hex(c)}>{entry.message}</color>");
            while (lines.Count > maxLines) lines.RemoveAt(0);
            if (logText != null) logText.text = string.Join("\n", lines);
        }

        private void SetProgress(float value)
        {
            if (progressFill == null) return;
            progressFill.anchorMin = Vector2.zero;
            progressFill.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
            progressFill.offsetMin = progressFill.offsetMax = Vector2.zero;
        }
    }
}
