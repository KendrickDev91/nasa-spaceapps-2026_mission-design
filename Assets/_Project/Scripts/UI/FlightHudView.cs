using System.Collections;
using System.Collections.Generic;
using MissionDesign.Core;
using MissionDesign.Simulation;
using MissionDesign.UI.Visual3D;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionDesign.UI
{
    /// <summary>
    /// HUD dinámico durante la simulación: telemetría de vuelo en vivo (día, distancias, velocidad), cartel de cada
    /// hito del diagrama (1-8) y, si hay cámaras, destello de cámara + miniaturas de las fotos que se van tomando.
    /// </summary>
    public sealed class FlightHudView : MonoBehaviour
    {
        [SerializeField] private OrbitViewController orbit;
        [SerializeField] private MoonPhotoStudio studio;
        [SerializeField] private CanvasGroup telemetryGroup;
        [SerializeField] private TMP_Text telemetryText;
        [SerializeField] private CanvasGroup bannerGroup;
        [SerializeField] private TMP_Text bannerText;
        [SerializeField] private CanvasGroup photosGroup;
        [SerializeField] private TMP_Text photoCounter;
        [SerializeField] private RectTransform thumbStrip;
        [SerializeField] private Image flash;

        private MissionStateManager manager;
        private Coroutine bannerRoutine;
        private readonly List<GameObject> thumbs = new List<GameObject>();

        private void Start()
        {
            manager = MissionStateManager.Instance;
            if (orbit != null) orbit.MilestoneReached += OnMilestone;
            if (studio != null) studio.PhotoTaken += OnPhoto;
            if (manager != null) manager.PhaseChanged += OnPhaseChanged;
            SetAlpha(bannerGroup, 0f);
            SetAlpha(photosGroup, 0f);
            if (flash != null) flash.color = new Color(1f, 1f, 1f, 0f);
        }

        private void OnDestroy()
        {
            if (orbit != null) orbit.MilestoneReached -= OnMilestone;
            if (studio != null) studio.PhotoTaken -= OnPhoto;
            if (manager != null) manager.PhaseChanged -= OnPhaseChanged;
        }

        private void OnPhaseChanged(MissionPhase phase)
        {
            if (phase == MissionPhase.OrbitInsertion)
                ShowBanner($"<color={UiTheme.Hex(UiTheme.Ok)}><size=130%><b>{Loc.L("¡DESPEGUE EXITOSO!", "SUCCESSFUL LIFTOFF!")}</b></size></color>\n<size=80%>{Loc.L("La sala de control celebra: el cohete llegó al espacio", "Mission control celebrates: the rocket reached space")}</size>");
            if (phase != MissionPhase.Launch) return;
            foreach (var t in thumbs) Destroy(t);
            thumbs.Clear();
            SetAlpha(photosGroup, 0f);
        }

        private void Update()
        {
            bool flying = orbit != null && orbit.ShowsFreeReturn && manager != null &&
                          manager.Phase >= MissionPhase.Launch && manager.Phase <= MissionPhase.ReturnAndRecovery;
            SetAlpha(telemetryGroup, flying ? 1f : 0f);
            if (!flying || telemetryText == null) return;
            string dim = UiTheme.Hex(UiTheme.TextDim);
            float day = orbit.CurrentDay;
            // Reloj de misión desde el despegue, como la NASA (la trayectoria guarda los días desde la TLI).
            float sinceLaunch = Mathf.Max(0f, day - orbit.Solution.launchDays);
            float total = orbit.Solution.totalDays - orbit.Solution.launchDays;
            string clock = Loc.L($"Día {sinceLaunch:F1} de {total:F1}", $"Day {sinceLaunch:F1} of {total:F1}")
                           + (day < 0f ? Loc.L("  <size=70%>(órbita terrestre, antes de la TLI)</size>", "  <size=70%>(Earth orbit, before TLI)</size>") : "");
            string where = day < 0f ? Loc.L("Órbita terrestre", "Earth orbit") : orbit.DistanceMoonKm < 20000f ? Loc.L("Sobrevuelo lunar", "Lunar flyby") : Loc.L("Espacio cislunar", "Cislunar space");
            telemetryText.text =
                $"<color={UiTheme.Hex(UiTheme.Accent)}><b>{Loc.L("TELEMETRÍA DE VUELO", "FLIGHT TELEMETRY")}</b></color>   <color={dim}>{where}</color>\n" +
                $"<size=130%><b>{clock}</b></size>\n" +
                $"<color={dim}>{Loc.L("Distancia a la Tierra", "Distance to Earth")}</color>  <b>{orbit.DistanceEarthKm:N0} km</b>\n" +
                $"<color={dim}>{Loc.L("Distancia a la Luna", "Distance to the Moon")}</color>  <b>{orbit.DistanceMoonKm:N0} km</b>\n" +
                $"<color={dim}>{Loc.L("Velocidad", "Speed")}</color>  <b>{orbit.SpeedKmS:F2} km/s</b>  <color={dim}>({orbit.SpeedKmS * 3600f:N0} km/h)</color>";
        }

        private void OnMilestone(TrajectoryMilestone m)
        {
            if (bannerText == null) return;
            string color = m.number <= 5 ? "#B9A3FF" : "#FFB347";
            ShowBanner($"<color={color}><size=150%><b>{m.number}</b></size></color>  <b>{m.Title}</b>\n<size=80%>{m.Detail}</size>");
        }

        public void ShowBanner(string text)
        {
            if (bannerText == null || bannerGroup == null) return;
            bannerText.text = text;
            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            bannerRoutine = StartCoroutine(Banner());
        }

        private IEnumerator Banner()
        {
            var rt = (RectTransform)bannerGroup.transform;
            for (float t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                SetAlpha(bannerGroup, t / 0.25f);
                rt.localScale = Vector3.one * Mathf.Lerp(1.15f, 1f, t / 0.25f);
                yield return null;
            }
            SetAlpha(bannerGroup, 1f);
            rt.localScale = Vector3.one;
            yield return new WaitForSeconds(2.2f);
            for (float t = 0f; t < 0.5f; t += Time.deltaTime) { SetAlpha(bannerGroup, 1f - t / 0.5f); yield return null; }
            SetAlpha(bannerGroup, 0f);
            bannerRoutine = null;
        }

        private void OnPhoto(MoonPhotoStudio.Photo photo, int number, int total)
        {
            SetAlpha(photosGroup, 1f);
            var o = manager != null ? manager.LastOutcome : null;
            if (photoCounter != null)
                photoCounter.text = $"<b>{Loc.L("CÁMARAS", "CAMERAS")}</b>  {Loc.L($"foto {number} de {total}", $"photo {number} of {total}")}  <color={UiTheme.Hex(UiTheme.TextDim)}>· {photo.title}</color>";
            if (thumbStrip != null)
            {
                var go = new GameObject($"Thumb_{number}", typeof(RectTransform));
                go.transform.SetParent(thumbStrip, false);
                var img = go.AddComponent<RawImage>();
                img.texture = photo.texture;
                img.raycastTarget = false;
                var le = go.AddComponent<LayoutElement>();
                le.preferredWidth = 160; le.preferredHeight = 90;
                thumbs.Add(go);
                StartCoroutine(Pop((RectTransform)go.transform));
                while (thumbs.Count > 6) { Destroy(thumbs[0]); thumbs.RemoveAt(0); }
            }
            if (flash != null) StartCoroutine(Flash());
            _ = o;
        }

        private IEnumerator Flash()
        {
            // Destello sutil: las 8 fotos caen en pocos segundos y un flash fuerte tapaba el sobrevuelo (y molesta a
            // jugadores fotosensibles). El «clic» lo dan el sonido y la miniatura que aparece.
            const float peak = 0.14f, duration = 0.18f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                flash.color = new Color(1f, 1f, 1f, peak * (1f - t / duration));
                yield return null;
            }
            flash.color = new Color(1f, 1f, 1f, 0f);
        }

        private static IEnumerator Pop(RectTransform rt)
        {
            for (float t = 0f; t < 0.3f && rt != null; t += Time.deltaTime)
            {
                float k = t / 0.3f;
                rt.localScale = Vector3.one * (1f + 0.6f * Mathf.Sin(k * Mathf.PI) * (1f - k));
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one;
        }

        private static void SetAlpha(CanvasGroup g, float a)
        {
            if (g == null) return;
            g.alpha = a;
            g.blocksRaycasts = false;
            g.interactable = false;
        }
    }
}
