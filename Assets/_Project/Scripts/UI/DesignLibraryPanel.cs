using System.Collections.Generic;
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
    /// Ventana "Diseños": nombre del equipo, guardar el diseño actual, cargar uno guardado y
    /// compararlo lado a lado con el actual (no hay una única solución perfecta).
    /// </summary>
    public sealed class DesignLibraryPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RoleTurnManager roles;
        [SerializeField] private TMP_InputField teamInput;
        [SerializeField] private TMP_InputField designNameInput;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private RectTransform listContent;
        [Tooltip("Fila desactivada con: TMP nombre, TMP detalle, botón 'LoadButton' y botón 'CompareButton'.")]
        [SerializeField] private RectTransform itemTemplate;
        [SerializeField] private TMP_Text comparisonText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private int maxItems = 10;

        private readonly List<GameObject> rows = new List<GameObject>();
        private MissionStateManager manager;

        public bool IsOpen => group != null && group.alpha > 0.5f;
        public string ComparisonText => comparisonText != null ? comparisonText.text : string.Empty;

        private void Start()
        {
            manager = MissionStateManager.Instance;
            if (itemTemplate != null) itemTemplate.gameObject.SetActive(false);
            if (saveButton != null) saveButton.onClick.AddListener(() => SaveCurrent());
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (teamInput != null && roles != null)
            {
                teamInput.text = roles.TeamName;
                teamInput.onEndEdit.AddListener(v => roles.TeamName = v);
            }
            SetVisible(false);
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            Refresh();
            SetStatus(manager != null && manager.Phase != MissionPhase.Design ? Loc.L("Solo se pueden cargar diseños en la fase de diseño.", "Designs can only be loaded during the design phase.") : string.Empty);
            SetVisible(true);
        }

        public void Close() => SetVisible(false);

        public string SaveCurrent()
        {
            if (manager == null) return null;
            if (roles != null && teamInput != null) roles.TeamName = teamInput.text;
            string name = designNameInput != null && !string.IsNullOrWhiteSpace(designNameInput.text)
                ? designNameInput.text
                : Loc.L($"Diseño {DesignLibrary.List().Count + 1}", $"Design {DesignLibrary.List().Count + 1}");
            var snapshot = DesignSnapshot.Capture(manager.Design, name, roles != null ? roles.TeamName : null);
            string path = DesignLibrary.Save(snapshot);
            SetStatus($"{Loc.L("Guardado", "Saved")}: {snapshot.designName}");
            Refresh();
            return path;
        }

        public void Load(DesignSnapshot snapshot)
        {
            if (manager == null || snapshot == null) return;
            var missing = manager.LoadSnapshot(snapshot);
            // Las demos usan una semilla fija para que la presentación salga igual cada vez; los demás diseños, aleatoria.
            var runner = FindAnyObjectByType<MissionSimulationRunner>();
            if (runner != null) runner.FixedSeed = snapshot.demoSeed;
            string note = snapshot.IsDemo && !string.IsNullOrEmpty(snapshot.demoNote) ? $"\n<size=85%>{snapshot.demoNote}</size>" : string.Empty;
            SetStatus((missing.Count == 0
                ? $"{Loc.L("Cargado", "Loaded")}: {DemoText(snapshot.designName)}"
                : Loc.L($"Cargado con {missing.Count} pieza(s) que ya no existen en el catálogo: {string.Join(", ", missing)}",
                        $"Loaded with {missing.Count} part(s) that no longer exist in the catalog: {string.Join(", ", missing)}")) + note);
        }

        public void CompareWithCurrent(DesignSnapshot snapshot)
        {
            if (manager == null || snapshot == null || comparisonText == null) return;
            var other = new SpacecraftDesign();
            snapshot.ApplyTo(other, manager.Database);
            var table = DesignComparison.Compare(manager.Design, other);
            var (winsA, winsB) = DesignComparison.Tally(table);

            string ok = UiTheme.Hex(UiTheme.Ok), dim = UiTheme.Hex(UiTheme.TextDim);
            var sb = new StringBuilder();
            sb.AppendLine($"<b><color={dim}>{Loc.L("INDICADOR", "METRIC")}</color><pos=46%>{Loc.L("ACTUAL", "CURRENT")}<pos=73%>{Trim(DemoText(snapshot.designName), 18)}</b>");
            foreach (var r in table)
            {
                string a = r.better < 0 ? $"<color={ok}><b>{r.valueA}</b></color>" : r.valueA;
                string b = r.better > 0 ? $"<color={ok}><b>{r.valueB}</b></color>" : r.valueB;
                sb.AppendLine($"{r.label}<pos=46%>{Trim(a, 60)}<pos=73%>{Trim(b, 60)}");
            }
            sb.AppendLine();
            sb.Append(winsA == winsB
                ? Loc.L("Empate técnico: distintos compromisos, ninguna solución es perfecta.", "Technical tie: different trade-offs, no solution is perfect.")
                : Loc.L($"El diseño {(winsA > winsB ? "ACTUAL" : snapshot.designName)} gana en {Mathf.Max(winsA, winsB)} de {winsA + winsB} indicadores comparables.",
                        $"The {(winsA > winsB ? "CURRENT" : DemoText(snapshot.designName))} design wins on {Mathf.Max(winsA, winsB)} of {winsA + winsB} comparable metrics."));
            comparisonText.text = sb.ToString();
        }

        private void Refresh()
        {
            foreach (var r in rows) Destroy(r);
            rows.Clear();
            if (itemTemplate == null || listContent == null) return;

            var saved = DesignLibrary.Demos().Concat(DesignLibrary.List()).Take(maxItems).ToList();
            if (saved.Count == 0 && comparisonText != null)
                comparisonText.text = $"<color={UiTheme.Hex(UiTheme.TextDim)}>{Loc.L("Aún no hay diseños guardados. Guarda el actual y prueba alternativas: después compáralos aquí.", "No saved designs yet. Save the current one and try alternatives: then compare them here.")}</color>";

            foreach (var (_, snapshot) in saved)
            {
                var row = Instantiate(itemTemplate, listContent);
                row.gameObject.SetActive(true);
                var texts = row.GetComponentsInChildren<TMP_Text>(true).Where(t => t.GetComponentInParent<Button>() == null).ToArray();
                if (texts.Length > 0)
                    texts[0].text = snapshot.IsDemo
                        ? $"<color={UiTheme.Hex(UiTheme.Warning)}><b>DEMO</b></color> · <b>{DemoText(snapshot.designName)}</b>"
                        : $"<b>{snapshot.designName}</b> <size=80%><color={UiTheme.Hex(UiTheme.TextDim)}>· {snapshot.teamName}</color></size>";
                if (texts.Length > 1)
                    texts[1].text = snapshot.IsDemo && !string.IsNullOrEmpty(snapshot.demoNote)
                        ? DemoText(snapshot.demoNote)
                        : $"{snapshot.scenarioId} · {snapshot.orbitId} · {snapshot.instrumentIds.Count} instr. · {snapshot.savedAtUtc}";
                var s = snapshot;
                foreach (var b in row.GetComponentsInChildren<Button>(true))
                {
                    if (b.name == "LoadButton") b.onClick.AddListener(() => Load(s));
                    else if (b.name == "CompareButton") b.onClick.AddListener(() => CompareWithCurrent(s));
                }
                rows.Add(row.gameObject);
            }
        }

        private static string Trim(string s, int max) => s != null && s.Length > max ? s.Substring(0, max - 1) + "…" : s;

        private void SetStatus(string text)
        {
            if (statusText != null) statusText.text = text;
        }

        private void SetVisible(bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        /// <summary>Nombres y notas de los diseños de demostración (los .json están en español).</summary>
        private static string DemoText(string es)
        {
            if (!Loc.English || string.IsNullOrEmpty(es)) return es;
            switch (es)
            {
                case "1 · Artemis II con fotos (éxito 100 %)": return "1 · Artemis II with photos (100% success)";
                case "2 · Sobrevuelo económico (parcial ~80 %)": return "2 · Budget flyby (partial ~80%)";
                case "3 · Batería insuficiente (falla)": return "3 · Undersized battery (failure)";
                case "Éxito total: 32 cámaras de Orion + cámaras de la tripulación, enlace láser y fotos de la Luna en el informe.":
                    return "Full success: Orion's 32 cameras + the crew cameras, laser link and Moon photos in the report.";
                case "Menos cámaras y menos ciencia: se cumple solo una parte del objetivo. Compáralo con la demo 1.":
                    return "Fewer cameras and less science: only part of the goal is met. Compare it with demo 1.";
                case "La batería no aguanta un evento: la cápsula no vuelve y solo quedan las fotos transmitidas. Lección: margen de energía.":
                    return "The battery cannot ride out an event: the capsule does not return and only the transmitted photos remain. Lesson: power margin.";
                default: return es;
            }
        }
    }
}
