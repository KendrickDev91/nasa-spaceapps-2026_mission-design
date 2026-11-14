using System.Collections.Generic;
using MissionDesign.Core;
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

        private static readonly (MissionPhase phase, string label)[] Steps =
        {
            (MissionPhase.Launch, "LANZAMIENTO"),
            (MissionPhase.OrbitInsertion, "INSERCIÓN"),
            (MissionPhase.ScienceOperations, "CIENCIA"),
            (MissionPhase.DataTransmission, "TRANSMISIÓN")
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

            if (phaseText == null) return;
            var parts = new List<string>();
            foreach (var (p, label) in Steps)
            {
                string color = p == phase ? UiTheme.Hex(UiTheme.Accent) : p < phase ? UiTheme.Hex(UiTheme.Ok) : UiTheme.Hex(UiTheme.TextDim);
                parts.Add(p == phase ? $"<color={color}><b>{label}</b></color>" : $"<color={color}>{label}</color>");
            }
            phaseText.text = string.Join("  »  ", parts);
        }

        private void AddLine(SimulationLogEntry entry)
        {
            Color c = entry.tone == LogTone.Critical ? UiTheme.Critical
                : entry.tone == LogTone.Warning ? UiTheme.Warning
                : entry.tone == LogTone.Positive ? UiTheme.Ok : UiTheme.Text;
            lines.Add($"<color={UiTheme.Hex(UiTheme.TextDim)}>Día {entry.missionDay,4:N0}</color>  <color={UiTheme.Hex(c)}>{entry.message}</color>");
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
