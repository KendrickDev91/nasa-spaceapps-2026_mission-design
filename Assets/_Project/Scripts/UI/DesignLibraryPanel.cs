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
            SetStatus(manager != null && manager.Phase != MissionPhase.Design ? "Solo se pueden cargar diseños en la fase de diseño." : string.Empty);
            SetVisible(true);
        }

        public void Close() => SetVisible(false);

        public string SaveCurrent()
        {
            if (manager == null) return null;
            if (roles != null && teamInput != null) roles.TeamName = teamInput.text;
            string name = designNameInput != null && !string.IsNullOrWhiteSpace(designNameInput.text)
                ? designNameInput.text
                : $"Diseño {DesignLibrary.List().Count + 1}";
            var snapshot = DesignSnapshot.Capture(manager.Design, name, roles != null ? roles.TeamName : null);
            string path = DesignLibrary.Save(snapshot);
            SetStatus($"Guardado: {snapshot.designName}");
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
                ? $"Cargado: {snapshot.designName}"
                : $"Cargado con {missing.Count} pieza(s) que ya no existen en el catálogo: {string.Join(", ", missing)}") + note);
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
            sb.AppendLine($"<b><color={dim}>INDICADOR</color><pos=46%>ACTUAL<pos=73%>{Trim(snapshot.designName, 18)}</b>");
            foreach (var r in table)
            {
                string a = r.better < 0 ? $"<color={ok}><b>{r.valueA}</b></color>" : r.valueA;
                string b = r.better > 0 ? $"<color={ok}><b>{r.valueB}</b></color>" : r.valueB;
                sb.AppendLine($"{r.label}<pos=46%>{Trim(a, 60)}<pos=73%>{Trim(b, 60)}");
            }
            sb.AppendLine();
            sb.Append(winsA == winsB
                ? "Empate técnico: distintos compromisos, ninguna solución es perfecta."
                : $"El diseño {(winsA > winsB ? "ACTUAL" : snapshot.designName)} gana en {Mathf.Max(winsA, winsB)} de {winsA + winsB} indicadores comparables.");
            comparisonText.text = sb.ToString();
        }

        private void Refresh()
        {
            foreach (var r in rows) Destroy(r);
            rows.Clear();
            if (itemTemplate == null || listContent == null) return;

            var saved = DesignLibrary.Demos().Concat(DesignLibrary.List()).Take(maxItems).ToList();
            if (saved.Count == 0 && comparisonText != null)
                comparisonText.text = $"<color={UiTheme.Hex(UiTheme.TextDim)}>Aún no hay diseños guardados. Guarda el actual y prueba alternativas: después compáralos aquí.</color>";

            foreach (var (_, snapshot) in saved)
            {
                var row = Instantiate(itemTemplate, listContent);
                row.gameObject.SetActive(true);
                var texts = row.GetComponentsInChildren<TMP_Text>(true).Where(t => t.GetComponentInParent<Button>() == null).ToArray();
                if (texts.Length > 0)
                    texts[0].text = snapshot.IsDemo
                        ? $"<color={UiTheme.Hex(UiTheme.Warning)}><b>DEMO</b></color> · <b>{snapshot.designName}</b>"
                        : $"<b>{snapshot.designName}</b> <size=80%><color={UiTheme.Hex(UiTheme.TextDim)}>· {snapshot.teamName}</color></size>";
                if (texts.Length > 1)
                    texts[1].text = snapshot.IsDemo && !string.IsNullOrEmpty(snapshot.demoNote)
                        ? snapshot.demoNote
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
    }
}
