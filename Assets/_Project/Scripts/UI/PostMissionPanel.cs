using System.Collections;
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
    /// Análisis post-misión, pensado para leerse de un vistazo: veredicto grande, dos tarjetas con viñetas cortas
    /// ("Lo que salió bien" / "Lo que falló"), rango A-F con la puntuación, 5 barras de métricas y la misión real parecida.
    /// Todo aparece animado: sello del veredicto, viñetas que se escriben, barras que se llenan, puntuación que cuenta y confeti.
    /// El texto completo del "por qué" sigue en el reporte TXT/JSON.
    /// </summary>
    public sealed class PostMissionPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text verdictText;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text wentWellText;
        [SerializeField] private TMP_Text wentWrongText;
        [SerializeField] private TMP_Text rankText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text similarText;
        [SerializeField] private TMP_Text exportText;
        [SerializeField] private TMP_Text leaderboardText;
        [SerializeField] private RectTransform[] barFills;
        [SerializeField] private TMP_Text[] barValues;
        [SerializeField] private Button openReportsButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Button explorationButton;
        [SerializeField] private Button menuButton;
        [SerializeField] private ExplorationReportPanel explorationPanel;
        [Tooltip("Contenedor donde cae el confeti en un éxito.")]
        [SerializeField] private RectTransform confettiLayer;
        [SerializeField] private int maxBulletsPerCard = 4;

        private MissionStateManager manager;
        private Coroutine reveal;
        private readonly List<GameObject> confetti = new List<GameObject>();
        private float[] barTargets = new float[0];

        public string VerdictText => (verdictText != null ? verdictText.text : string.Empty) + " " + (titleText != null ? titleText.text : string.Empty);
        public string WentWellText => wentWellText != null ? wentWellText.text : string.Empty;
        public string WentWrongText => wentWrongText != null ? wentWrongText.text : string.Empty;
        public string RankText => (rankText != null ? rankText.text : string.Empty) + " " + (scoreText != null ? scoreText.text : string.Empty);
        public bool Revealing => reveal != null;

        private void Start()
        {
            manager = MissionStateManager.Instance;
            if (manager == null) return;
            manager.MissionCompleted += Show;
            if (backButton != null) backButton.onClick.AddListener(manager.ReturnToDesign);
            if (openReportsButton != null) openReportsButton.onClick.AddListener(OpenReportsFolder);
            if (openReportsButton != null && GameSettings.IsWeb) openReportsButton.gameObject.SetActive(false);
            if (explorationButton != null && explorationPanel != null) explorationButton.onClick.AddListener(explorationPanel.Open);
            if (menuButton != null) menuButton.onClick.AddListener(GameMenus.BackToMainMenu);
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
            if (verdictText != null) verdictText.text = $"<color={UiTheme.Hex(verdictColor)}>{MissionOutcomeReport.VerdictLabel(o.verdict)}</color>";
            if (titleText != null) titleText.text = o.verdictTitle;

            if (wentWellText != null) wentWellText.text = Bullets(o.wentWell, Loc.L("Nada destacable esta vez: revisa la tarjeta de fallos.", "Nothing notable this time: check the failures card."));
            if (wentWrongText != null) wentWrongText.text = Bullets(o.wentWrong, Loc.L("¡Nada falló! Diseño sólido.", "Nothing failed! Solid design."));

            Color rankColor = RankColor(o.rank);
            if (rankText != null) rankText.text = $"<color={UiTheme.Hex(rankColor)}>{o.rank}</color>";
            if (scoreText != null) scoreText.text = $"{o.score}<size=55%>/100</size>";

            if (similarText != null)
                similarText.text = string.IsNullOrEmpty(o.similarMission)
                    ? string.Empty
                    : $"<color={UiTheme.Hex(UiTheme.TextDim)}>{Loc.L("Tu misión se parece a", "Your mission resembles")}</color>  <b>{o.similarMission}</b>\n" +
                      $"<size=88%>{Trim(o.similarMissionDescription, 120)}</size>";

            // Barras: ciencia, datos, energía, presupuesto usado y trabajo en equipo.
            float science = o.scienceTarget > 0f ? o.scienceAchieved / o.scienceTarget : 0f;
            float budget = o.budgetLimitMUSD > 0f ? o.totalCostMUSD / o.budgetLimitMUSD : 0f;
            barTargets = new[] { Mathf.Clamp01(science), Mathf.Clamp01(o.dataReturnFraction), Mathf.Clamp01(o.powerAvailabilityFraction),
                                 Mathf.Clamp01(budget), Mathf.Clamp01(o.collaborationScore / 10f) };
            string[] values =
            {
                $"{o.scienceAchieved:N0} {Loc.L("de", "of")} {o.scienceTarget:N0} ({science:P0})",
                $"{o.dataReturnedTotalTB:N1} TB ({o.dataReturnFraction:P0})",
                $"{o.powerAvailabilityFraction:P0}",
                o.budgetLimitMUSD > 0f ? $"${o.totalCostMUSD:N0}M {Loc.L("de", "of")} ${o.budgetLimitMUSD:N0}M" : Loc.L("sin límite", "no limit"),
                $"{o.collaborationScore} {Loc.L("de", "of")} 10"
            };
            Color[] colors = { science >= 1f ? UiTheme.Ok : science >= 0.6f ? UiTheme.Warning : UiTheme.Critical,
                               o.dataReturnFraction >= 0.9f ? UiTheme.Ok : UiTheme.Warning,
                               o.powerAvailabilityFraction >= 0.95f ? UiTheme.Ok : UiTheme.Warning,
                               budget <= 0.9f ? UiTheme.Ok : budget <= 1f ? UiTheme.Warning : UiTheme.Critical,
                               o.collaborationScore >= 7 ? UiTheme.Ok : o.collaborationScore >= 4 ? UiTheme.Warning : UiTheme.Critical };
            for (int i = 0; barFills != null && i < barFills.Length && i < barTargets.Length; i++)
            {
                if (barValues != null && i < barValues.Length && barValues[i] != null) barValues[i].text = values[i];
                var img = barFills[i] != null ? barFills[i].GetComponent<Image>() : null;
                if (img != null) img.color = colors[i];
                SetBar(i, barTargets[i]);
            }

            if (leaderboardText != null)
            {
                var top = Leaderboard.Top(o.scenarioId, 3);
                var sb = new StringBuilder($"<color={UiTheme.Hex(UiTheme.TextDim)}>{Loc.L("RANKING DEL ESCENARIO", "SCENARIO LEADERBOARD")}</color>\n");
                for (int i = 0; i < top.Count; i++)
                {
                    var e = top[i];
                    bool mine = e.seed == o.seed && e.dateUtc == o.generatedAtUtc;
                    string line = $"{i + 1}. {Trim(e.team == "Equipo 1" ? Loc.L("Equipo 1", "Team 1") : e.team, 18)}<pos=55%>{e.rank} · {e.score}<pos=80%>{Loc.L("equipo", "team")} {e.collaboration}";
                    sb.AppendLine(mine ? $"<color={UiTheme.Hex(UiTheme.Ok)}><b>{line}</b></color>" : line);
                }
                leaderboardText.text = sb.ToString();
            }

            if (exportText != null)
                exportText.text = string.IsNullOrEmpty(manager.LastReportPath)
                    ? Loc.L("Reporte no exportado.", "Report not exported.")
                    : $"{Loc.L("Reporte completo (el «por qué» detallado)", "Full report (the detailed \"why\")")}: {Path.GetFileName(manager.LastReportPath)}";

            if (explorationButton != null)
            {
                var label = explorationButton.GetComponentInChildren<TMP_Text>();
                if (label != null)
                    label.text = o.photoShotsAvailable > 0 ? Loc.L($"<b>Informe de la exploración</b> · {o.photoShotsAvailable} fotos", $"<b>Exploration report</b> · {o.photoShotsAvailable} photos") : Loc.L("Informe de la exploración", "Exploration report");
            }

            if (reveal != null) StopCoroutine(reveal);
            if (isActiveAndEnabled) reveal = StartCoroutine(Reveal(o));
        }

        // ───────────── Texto corto y claro ─────────────

        private string Bullets(List<string> lines, string empty)
        {
            if (lines == null || lines.Count == 0) return $"<color={UiTheme.Hex(UiTheme.TextDim)}>{empty}</color>";
            var sb = new StringBuilder();
            foreach (var line in lines.Take(maxBulletsPerCard)) sb.AppendLine($"•  {Short(line)}");
            if (lines.Count > maxBulletsPerCard)
                sb.Append($"<size=80%><color={UiTheme.Hex(UiTheme.TextDim)}>+{lines.Count - maxBulletsPerCard} {Loc.L("más en el reporte", "more in the report")}</color></size>");
            return sb.ToString().TrimEnd();
        }

        /// <summary>Primera idea de cada explicación, sin prefijo y en una frase corta (≤ 110 caracteres).</summary>
        public static string Short(string line, int max = 110)
        {
            line = StripPrefix(line ?? string.Empty).Trim();
            int cut = line.Length;
            foreach (var sep in new[] { ". ", "; ", " — " })
            {
                int i = line.IndexOf(sep, System.StringComparison.Ordinal);
                if (i > 25 && i < cut) cut = i;
            }
            line = line.Substring(0, cut).TrimEnd('.', ';', ' ');
            if (line.Length > max) line = line.Substring(0, max - 1).TrimEnd(' ', ',') + "…";
            return line.EndsWith("…") ? line : line + ".";
        }

        /// <summary>Quita "Fallo parcial:", "A favor:", etc.: la tarjeta ya indica el tono.</summary>
        private static string StripPrefix(string line)
        {
            int colon = line.IndexOf(':');
            if (colon <= 0 || colon > 16) return line;
            string rest = line.Substring(colon + 1).TrimStart();
            return rest.Length > 0 ? char.ToUpperInvariant(rest[0]) + rest.Substring(1) : line;
        }

        private static string Trim(string s, int max) => string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max - 1) + "…";

        private static Color RankColor(string rank) =>
            rank == "A" || rank == "B" ? UiTheme.Ok : rank == "C" || rank == "D" ? UiTheme.Warning : UiTheme.Critical;

        private void SetBar(int i, float fraction)
        {
            if (barFills == null || i >= barFills.Length || barFills[i] == null) return;
            barFills[i].anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
        }

        // ───────────── Presentación animada ─────────────

        private IEnumerator Reveal(MissionOutcomeReport o)
        {
            var cards = new List<CanvasGroup>();
            foreach (var t in new[] { wentWellText, wentWrongText, rankText })
            {
                if (t == null || t.transform.parent == null) continue;
                var g = t.transform.parent.GetComponent<CanvasGroup>();
                if (g == null) g = t.transform.parent.gameObject.AddComponent<CanvasGroup>();
                g.alpha = 0f;
                cards.Add(g);
            }
            foreach (var t in new[] { wentWellText, wentWrongText }) if (t != null) t.maxVisibleCharacters = 0;
            for (int i = 0; i < barTargets.Length; i++) SetBar(i, 0f);
            if (scoreText != null) scoreText.text = "0<size=55%>/100</size>";

            // 1. El veredicto cae como un sello.
            if (verdictText != null)
            {
                for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
                {
                    verdictText.transform.localScale = Vector3.one * Mathf.Lerp(1.8f, 1f, EaseOutBack(t / 0.35f));
                    yield return null;
                }
                verdictText.transform.localScale = Vector3.one;
            }
            if (o.verdict == MissionVerdict.Success) SpawnConfetti();

            // 2. Las tarjetas entran deslizándose y las viñetas se escriben.
            foreach (var g in cards)
            {
                var rt = (RectTransform)g.transform;
                Vector2 home = rt.anchoredPosition;
                for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
                {
                    float k = t / 0.25f;
                    g.alpha = k;
                    rt.anchoredPosition = home + new Vector2(0f, -40f * (1f - EaseOutBack(k)));
                    yield return null;
                }
                g.alpha = 1f;
                rt.anchoredPosition = home;
            }
            foreach (var text in new[] { wentWellText, wentWrongText })
            {
                if (text == null) continue;
                text.ForceMeshUpdate();
                int total = text.textInfo.characterCount;
                for (float t = 0f; t < 0.7f; t += Time.unscaledDeltaTime)
                {
                    text.maxVisibleCharacters = Mathf.RoundToInt(total * (t / 0.7f));
                    yield return null;
                }
                text.maxVisibleCharacters = 99999;
            }

            // 3. Las barras se llenan y la puntuación cuenta hasta el valor final.
            for (float t = 0f; t < 1.1f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 1.1f);
                for (int i = 0; i < barTargets.Length; i++) SetBar(i, barTargets[i] * k);
                if (scoreText != null) scoreText.text = $"{Mathf.RoundToInt(o.score * k)}<size=55%>/100</size>";
                yield return null;
            }
            for (int i = 0; i < barTargets.Length; i++) SetBar(i, barTargets[i]);
            if (scoreText != null) scoreText.text = $"{o.score}<size=55%>/100</size>";

            // 4. El rango "late".
            if (rankText != null)
            {
                for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
                {
                    rankText.transform.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(t / 0.3f * Mathf.PI));
                    yield return null;
                }
                rankText.transform.localScale = Vector3.one;
            }
            reveal = null;
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            x = Mathf.Clamp01(x);
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        private void SpawnConfetti()
        {
            if (confettiLayer == null) return;
            foreach (var c in confetti) if (c != null) Destroy(c);
            confetti.Clear();
            var colors = new[] { UiTheme.Ok, UiTheme.Accent, UiTheme.Warning, new Color(0.73f, 0.64f, 1f), Color.white, UiTheme.Launch };
            var rect = confettiLayer.rect;
            for (int i = 0; i < 90; i++)
            {
                var go = new GameObject("Confetti", typeof(RectTransform));
                go.transform.SetParent(confettiLayer, false);
                var img = go.AddComponent<Image>();
                img.color = colors[i % colors.Length];
                img.raycastTarget = false;
                var rt = (RectTransform)go.transform;
                rt.sizeDelta = new Vector2(Random.Range(8f, 16f), Random.Range(14f, 26f));
                rt.anchoredPosition = new Vector2(Random.Range(-rect.width * 0.5f, rect.width * 0.5f), rect.height * 0.5f + Random.Range(0f, 300f));
                confetti.Add(go);
                StartCoroutine(Fall(rt, Random.Range(260f, 520f), Random.Range(-260f, 260f), Random.Range(-360f, 360f), rect.height));
            }
        }

        private static IEnumerator Fall(RectTransform rt, float speed, float spin, float drift, float height)
        {
            float t = 0f;
            while (rt != null && rt.anchoredPosition.y > -height * 0.6f)
            {
                t += Time.unscaledDeltaTime;
                rt.anchoredPosition += new Vector2(Mathf.Sin(t * 3f) * drift * 0.2f, -speed) * Time.unscaledDeltaTime;
                rt.Rotate(0f, 0f, spin * Time.unscaledDeltaTime);
                yield return null;
            }
            if (rt != null) Destroy(rt.gameObject);
        }

        private void OpenReportsFolder()
        {
            string folder = MissionReportExporter.ReportsFolder;
            Directory.CreateDirectory(folder);
            Application.OpenURL("file:///" + folder.Replace('\\', '/'));
        }
    }
}
