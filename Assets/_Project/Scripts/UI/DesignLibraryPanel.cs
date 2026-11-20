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
        [SerializeField] private int maxItems = 9;

        private readonly List<GameObject> rows = new List<GameObject>();
        private MissionStateManager manager;
        /// <summary>Filtro de la lista: escenario (índice en la base de datos, -1 = todos) y dificultad (-1 = ambas).</summary>
        private int scenarioFilter = -1, difficultyFilter = -1;
        private GameObject filterBar;
        private TMP_Text scenarioLabel, difficultyLabel;

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
            // La lista ahora mezcla ejemplos por escenario y los diseños del equipo.
            var title = group != null ? group.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.name == "ListTitle") : null;
            if (title != null) title.text = Loc.L("EJEMPLOS Y DISEÑOS GUARDADOS", "EXAMPLES AND SAVED DESIGNS");
            SetVisible(false);
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            // Empieza mostrando los ejemplos del escenario y la dificultad que el equipo tiene elegidos.
            if (manager != null && manager.Database != null && manager.Design.Scenario != null)
            {
                scenarioFilter = manager.Database.scenarios.IndexOf(manager.Design.Scenario);
                difficultyFilter = (int)manager.Design.Difficulty;
            }
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
            string note = snapshot.IsDemo && !string.IsNullOrEmpty(snapshot.demoNote) ? $"\n<size=85%>{snapshot.DisplayNote}</size>" : string.Empty;
            SetStatus((missing.Count == 0
                ? $"{Loc.L("Cargado", "Loaded")}: {snapshot.DisplayName}"
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
            sb.AppendLine($"<b><color={dim}>{Loc.L("INDICADOR", "METRIC")}</color><pos=46%>{Loc.L("ACTUAL", "CURRENT")}<pos=73%>{Trim(snapshot.DisplayName, 18)}</b>");
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
                        $"The {(winsA > winsB ? "CURRENT" : snapshot.DisplayName)} design wins on {Mathf.Max(winsA, winsB)} of {winsA + winsB} comparable metrics."));
            comparisonText.text = sb.ToString();
        }

        private void Refresh()
        {
            foreach (var r in rows) Destroy(r);
            rows.Clear();
            if (itemTemplate == null || listContent == null) return;
            BuildFilterBar();

            var db = manager != null ? manager.Database : null;
            string scenarioId = db != null && scenarioFilter >= 0 && scenarioFilter < db.scenarios.Count ? db.scenarios[scenarioFilter].scenarioId : null;
            bool Match(DesignSnapshot s) =>
                (scenarioId == null || s.scenarioId == scenarioId) && (difficultyFilter < 0 || (int)s.difficulty == difficultyFilter);

            // Primero los ejemplos (por dificultad: ganador y luego las lecciones), después los diseños del equipo.
            var demos = DesignLibrary.Demos().Where(d => Match(d.snapshot))
                .OrderBy(d => ScenarioIndex(d.snapshot.scenarioId)).ThenBy(d => (int)d.snapshot.difficulty).ThenBy(d => d.snapshot.demoOrder);
            var saved = demos.Concat(DesignLibrary.List().Where(d => Match(d.snapshot))).Take(maxItems).ToList();
            if (saved.Count == 0 && comparisonText != null)
                comparisonText.text = $"<color={UiTheme.Hex(UiTheme.TextDim)}>{Loc.L("No hay diseños para este filtro. Guarda el actual y prueba alternativas: después compáralos aquí.", "No designs for this filter. Save the current one and try alternatives: then compare them here.")}</color>";

            foreach (var (_, snapshot) in saved)
            {
                var row = Instantiate(itemTemplate, listContent);
                row.gameObject.SetActive(true);
                var texts = row.GetComponentsInChildren<TMP_Text>(true).Where(t => t.GetComponentInParent<Button>() == null).ToArray();
                string level = snapshot.difficulty == DifficultyLevel.Advanced ? Loc.L("Avanzado", "Advanced") : Loc.L("Básico", "Basic");
                if (texts.Length > 0)
                {
                    // Una sola línea con «…»: así el nombre no tapa la nota de abajo.
                    texts[0].textWrappingMode = TextWrappingModes.NoWrap;
                    texts[0].overflowMode = TextOverflowModes.Ellipsis;
                }
                if (texts.Length > 0)
                    texts[0].text = snapshot.IsDemo
                        ? $"{Badge(snapshot)} · <b>{snapshot.DisplayName}</b>"
                        : $"<b>{snapshot.designName}</b> <size=80%><color={UiTheme.Hex(UiTheme.TextDim)}>· {snapshot.teamName} · {level}</color></size>";
                if (texts.Length > 1)
                    texts[1].text = snapshot.IsDemo && !string.IsNullOrEmpty(snapshot.demoNote)
                        ? snapshot.DisplayNote
                        : $"{ScenarioName(snapshot.scenarioId)} · {snapshot.orbitId} · {snapshot.instrumentIds.Count} instr. · {snapshot.savedAtUtc}";
                var s = snapshot;
                foreach (var b in row.GetComponentsInChildren<Button>(true))
                {
                    if (b.name == "LoadButton") b.onClick.AddListener(() => Load(s));
                    else if (b.name == "CompareButton") b.onClick.AddListener(() => CompareWithCurrent(s));
                }
                rows.Add(row.gameObject);
            }
        }

        /// <summary>Etiqueta de color de los ejemplos: ganador (verde) o lección (ámbar).</summary>
        private static string Badge(DesignSnapshot s) => s.demoOrder == 0
            ? $"<color={UiTheme.Hex(UiTheme.Ok)}><b>{Loc.L("GANADOR", "WINNER")}</b></color>"
            : $"<color={UiTheme.Hex(UiTheme.Warning)}><b>{Loc.L("LECCIÓN", "LESSON")}</b></color>";

        private int ScenarioIndex(string id)
        {
            var db = manager != null ? manager.Database : null;
            if (db == null) return 0;
            int i = db.scenarios.FindIndex(x => x != null && x.scenarioId == id);
            return i < 0 ? int.MaxValue : i;
        }

        private string ScenarioName(string id)
        {
            var db = manager != null ? manager.Database : null;
            var scn = db != null ? db.scenarios.FirstOrDefault(x => x != null && x.scenarioId == id) : null;
            return scn != null ? scn.displayName : id;
        }

        /// <summary>Barra de filtros sobre la lista: ‹ escenario › y dificultad (Básico / Avanzado / Ambas).</summary>
        private void BuildFilterBar()
        {
            var db = manager != null ? manager.Database : null;
            if (filterBar == null)
            {
                filterBar = new GameObject("FilterBar", typeof(RectTransform));
                filterBar.transform.SetParent(listContent, false);
                filterBar.AddComponent<LayoutElement>().preferredHeight = 40;
                var bar = (RectTransform)filterBar.transform;

                MakeButton(bar, "‹", new Vector2(0f, 0.5f), new Vector2(0f, 0f), 40f, () => StepScenario(-1));
                scenarioLabel = MakeLabel(bar, new Vector2(46f, 0f), 330f);
                MakeButton(bar, "›", new Vector2(0f, 0.5f), new Vector2(382f, 0f), 40f, () => StepScenario(1));
                var diff = MakeButton(bar, "", new Vector2(1f, 0.5f), new Vector2(0f, 0f), 180f, StepDifficulty, rightAnchored: true);
                difficultyLabel = diff.GetComponentInChildren<TMP_Text>();
            }
            filterBar.transform.SetAsFirstSibling();
            string scenario = db != null && scenarioFilter >= 0 && scenarioFilter < db.scenarios.Count
                ? db.scenarios[scenarioFilter].displayName : Loc.L("Todos los escenarios", "All scenarios");
            if (scenarioLabel != null) scenarioLabel.text = $"<b>{scenario}</b>";
            if (difficultyLabel != null)
                difficultyLabel.text = difficultyFilter < 0 ? Loc.L("Ambas dificultades", "Both difficulties")
                    : difficultyFilter == (int)DifficultyLevel.Advanced ? Loc.L("Avanzado", "Advanced") : Loc.L("Básico", "Basic");
        }

        private void StepScenario(int delta)
        {
            var db = manager != null ? manager.Database : null;
            int n = db != null ? db.scenarios.Count : 0;
            // -1 (todos) y luego cada escenario en el orden del catálogo.
            scenarioFilter = ((scenarioFilter + 1 + delta) % (n + 1) + (n + 1)) % (n + 1) - 1;
            Refresh();
        }

        private void StepDifficulty()
        {
            difficultyFilter = difficultyFilter == (int)DifficultyLevel.Basic ? (int)DifficultyLevel.Advanced
                : difficultyFilter == (int)DifficultyLevel.Advanced ? -1 : (int)DifficultyLevel.Basic;
            Refresh();
        }

        private static Button MakeButton(RectTransform parent, string label, Vector2 anchor, Vector2 pos, float width,
            UnityEngine.Events.UnityAction onClick, bool rightAnchored = false)
        {
            var go = new GameObject("FilterButton", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(rightAnchored ? 1f : 0f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, 36f);
            var img = go.AddComponent<Image>();
            img.color = UiTheme.AccentDim;
            var b = go.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(onClick);
            var t = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            t.transform.SetParent(go.transform, false);
            var trt = t.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            t.text = label; t.fontSize = 17; t.color = UiTheme.Text; t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            return b;
        }

        private static TMP_Text MakeLabel(RectTransform parent, Vector2 pos, float width)
        {
            // Fondo y texto como hermanos: el fondo es el padre (se dibuja primero) y el texto su hijo.
            var bg = new GameObject("ScenarioLabel", typeof(RectTransform)).AddComponent<Image>();
            bg.transform.SetParent(parent, false);
            var rt = bg.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, 36f);
            bg.color = UiTheme.PanelLight;
            bg.raycastTarget = false;
            var t = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            t.transform.SetParent(bg.transform, false);
            var trt = t.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = new Vector2(6f, 0f); trt.offsetMax = new Vector2(-6f, 0f);
            t.fontSize = 16; t.color = UiTheme.Text; t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.enableAutoSizing = true; t.fontSizeMin = 11; t.fontSizeMax = 16;
            t.raycastTarget = false;
            return t;
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
