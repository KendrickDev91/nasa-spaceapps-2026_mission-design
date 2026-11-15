using System.Collections.Generic;
using System.Text;
using MissionDesign.Core;
using MissionDesign.Simulation;
using TMPro;
using UnityEngine;

namespace MissionDesign.UI.Visual3D
{
    /// <summary>
    /// Pantallas laterales de la sala de control con datos en tiempo real durante la simulación:
    /// izquierda = telemetría (día de misión, distancia, velocidad, datos recibidos, energía, fotos);
    /// derecha = despliegue de la nave paso a paso y bitácora con los últimos eventos.
    /// La pantalla central ya muestra la trayectoria en vivo (cámara de la vista orbital).
    /// </summary>
    public sealed class ControlRoomScreens : MonoBehaviour
    {
        [SerializeField] private TMP_Text leftScreen;
        [SerializeField] private TMP_Text rightScreen;
        [SerializeField] private MissionSimulationRunner runner;
        [SerializeField] private OrbitViewController orbit;
        [SerializeField] private MoonPhotoStudio studio;

        private const float EarthMu = 398600.4418f, EarthRadiusKm = 6378.137f;
        private MissionStateManager manager;
        private readonly Queue<string> log = new Queue<string>();
        private float progress;
        private float nextRefresh;

        private void Start()
        {
            manager = MissionStateManager.Instance;
            if (runner != null)
            {
                runner.EntryEmitted += OnEntry;
                runner.ProgressChanged += p => progress = p;
            }
            if (manager != null) manager.PhaseChanged += OnPhase;
            Refresh();
        }

        private void OnDestroy()
        {
            if (runner != null) runner.EntryEmitted -= OnEntry;
            if (manager != null) manager.PhaseChanged -= OnPhase;
        }

        private void OnPhase(MissionPhase phase)
        {
            if (phase == MissionPhase.Launch) { log.Clear(); progress = 0f; }
            Refresh();
        }

        private void OnEntry(SimulationLogEntry e)
        {
            string color = e.tone == LogTone.Critical ? "#FF5A50" : e.tone == LogTone.Warning ? "#FFC233" : e.tone == LogTone.Positive ? "#4CD975" : "#C8D6EA";
            string msg = e.message.Length > 70 ? e.message.Substring(0, 69) + "…" : e.message;
            log.Enqueue($"<color={color}>D{e.missionDay:F1}  {msg}</color>");
            while (log.Count > 6) log.Dequeue();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.2f;
            Refresh();
        }

        private void Refresh()
        {
            if (manager == null) return;
            var phase = manager.Phase;
            var o = runner != null ? runner.Current : null;
            bool flying = phase >= MissionPhase.Launch && phase <= MissionPhase.ReturnAndRecovery && o != null;
            if (leftScreen != null) leftScreen.text = flying ? Telemetry(phase, o) : Idle();
            if (rightScreen != null) rightScreen.text = flying || phase == MissionPhase.PostMission ? Deployment(phase, o) : Checklist();
        }

        private string Idle()
        {
            var d = manager.Design;
            return "<b><color=#3FA0FF>CONTROL DE MISIÓN</color></b>\n" +
                   $"Misión: {(d.Scenario != null ? d.Scenario.displayName : "—")}\n" +
                   $"Órbita: {(d.Orbit != null ? d.Orbit.displayName : "—")}\n" +
                   $"Cohete: {(d.LaunchVehicle != null ? d.LaunchVehicle.displayName : "—")}\n" +
                   $"Instrumentos: {d.Instruments.Count}\n" +
                   $"Estado: {(manager.CurrentValidation != null && manager.CurrentValidation.canLaunch ? "<color=#4CD975>LISTO PARA LANZAR</color>" : "<color=#FFC233>EN DISEÑO</color>")}";
        }

        private string Checklist()
        {
            var v = manager.CurrentValidation;
            int blocking = v != null ? System.Linq.Enumerable.Count(v.Blocking) : 0;
            return "<b><color=#3FA0FF>REVISIÓN DE VUELO</color></b>\n" +
                   $"Problemas bloqueantes: {(blocking == 0 ? "<color=#4CD975>0</color>" : $"<color=#FF5A50>{blocking}</color>")}\n" +
                   "Pendiente: aprobación del equipo\n" +
                   "Ventana de lanzamiento: abierta";
        }

        private string Telemetry(MissionPhase phase, MissionOutcomeReport o)
        {
            var sb = new StringBuilder("<b><color=#3FA0FF>TELEMETRÍA EN VIVO</color></b>\n");
            sb.Append($"Fase: <b>{PhaseName(phase)}</b>\n");
            if (orbit != null && orbit.ShowsFreeReturn)
            {
                sb.Append($"Día de misión: <b>{orbit.CurrentDay:F1}</b>\n");
                sb.Append($"A la Tierra: <b>{orbit.DistanceEarthKm:N0} km</b>\n");
                sb.Append($"A la Luna: <b>{orbit.DistanceMoonKm:N0} km</b>\n");
                sb.Append($"Velocidad: <b>{orbit.SpeedKmS:F2} km/s</b>\n");
            }
            else
            {
                var orb = manager.Design.Orbit;
                float alt = orb != null ? orb.altitudeKm : 400f;
                float v = Mathf.Sqrt(EarthMu / (EarthRadiusKm + Mathf.Max(150f, alt)));
                sb.Append($"Día de misión: <b>{progress * Mathf.Max(1f, o.plannedOperationsDays):F0}</b>\n");
                sb.Append($"Altitud: <b>{alt:N0} km</b>\n");
                sb.Append($"Velocidad orbital: <b>{v:F2} km/s</b>\n");
                sb.Append($"Contactos con tierra: <b>{Mathf.RoundToInt(progress * 40f)}</b>\n");
            }
            float tb = phase >= MissionPhase.DataTransmission ? o.dataReturnedTotalTB * Mathf.InverseLerp(0.55f, 1f, progress) : 0f;
            sb.Append($"Datos recibidos: <b>{tb:N2} TB</b>  {Bar(tb / Mathf.Max(0.001f, o.dataReturnedTotalTB))}\n");
            sb.Append($"Energía: <b>{o.powerAvailabilityFraction:P0}</b>  {Bar(o.powerAvailabilityFraction)}\n");
            int photos = studio != null ? studio.Photos.Count : 0;
            sb.Append($"Fotos: <b>{photos}/{o.photoShots}</b>{(o.photosTaken > 0 ? $"  (~{o.photosTaken:N0} a bordo)" : "")}");
            return sb.ToString();
        }

        private string Deployment(MissionPhase phase, MissionOutcomeReport o)
        {
            string Step(bool done, string label) => done ? $"<color=#4CD975>[OK]</color> {label}" : $"<color=#6F7F99>[··]</color> <color=#8FA0BA>{label}</color>";
            bool inSpace = phase >= MissionPhase.OrbitInsertion;
            var sb = new StringBuilder("<b><color=#3FA0FF>DESPLIEGUE DE LA NAVE</color></b>\n");
            sb.AppendLine(Step(inSpace, "Separación del cohete"));
            sb.AppendLine(Step(inSpace && (phase > MissionPhase.OrbitInsertion || progress > 0.3f), "Paneles solares desplegados"));
            sb.AppendLine(Step(phase >= MissionPhase.ScienceOperations, "Antena apuntando a tierra"));
            sb.AppendLine(Step(phase >= MissionPhase.ScienceOperations, o != null && o.photoShots > 0 ? "Cámaras e instrumentos activos" : "Instrumentos activos"));
            sb.AppendLine(Step(phase >= MissionPhase.DataTransmission, "Enlace de bajada de datos"));
            if (o != null && o.returnsToEarth) sb.AppendLine(Step(phase >= MissionPhase.ReturnAndRecovery, "Reentrada y amerizaje"));
            sb.Append("<b><color=#3FA0FF>BITÁCORA</color></b>\n");
            foreach (var line in log) sb.AppendLine(line);
            return sb.ToString();
        }

        private static string Bar(float fraction)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(fraction) * 10f), 0, 10);
            return $"<color=#3FA0FF>{new string('|', n)}</color><color=#33415A>{new string('|', 10 - n)}</color>";
        }

        private static string PhaseName(MissionPhase p) => p switch
        {
            MissionPhase.Launch => "LANZAMIENTO",
            MissionPhase.OrbitInsertion => "INSERCIÓN",
            MissionPhase.ScienceOperations => "OPERACIONES",
            MissionPhase.DataTransmission => "TRANSMISIÓN",
            MissionPhase.ReturnAndRecovery => "REGRESO",
            MissionPhase.PostMission => "ANÁLISIS",
            _ => "DISEÑO"
        };
    }
}
