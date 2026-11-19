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
            return $"<b><color=#3FA0FF>{Loc.L("CONTROL DE MISIÓN", "MISSION CONTROL")}</color></b>\n" +
                   $"{Loc.L("Misión", "Mission")}: {(d.Scenario != null ? d.Scenario.displayName : "—")}\n" +
                   $"{Loc.L("Órbita", "Orbit")}: {(d.Orbit != null ? d.Orbit.displayName : "—")}\n" +
                   $"{Loc.L("Cohete", "Rocket")}: {(d.LaunchVehicle != null ? d.LaunchVehicle.displayName : "—")}\n" +
                   $"{Loc.L("Instrumentos", "Instruments")}: {d.Instruments.Count}\n" +
                   $"{Loc.L("Estado", "Status")}: {(manager.CurrentValidation != null && manager.CurrentValidation.canLaunch ? Loc.L("<color=#4CD975>LISTO PARA LANZAR</color>", "<color=#4CD975>READY TO LAUNCH</color>") : Loc.L("<color=#FFC233>EN DISEÑO</color>", "<color=#FFC233>IN DESIGN</color>"))}";
        }

        private string Checklist()
        {
            var v = manager.CurrentValidation;
            int blocking = v != null ? System.Linq.Enumerable.Count(v.Blocking) : 0;
            return $"<b><color=#3FA0FF>{Loc.L("REVISIÓN DE VUELO", "FLIGHT READINESS")}</color></b>\n" +
                   $"{Loc.L("Problemas bloqueantes", "Blocking issues")}: {(blocking == 0 ? "<color=#4CD975>0</color>" : $"<color=#FF5A50>{blocking}</color>")}\n" +
                   Loc.L("Pendiente: aprobación del equipo\n", "Pending: team approval\n") +
                   Loc.L("Ventana de lanzamiento: abierta", "Launch window: open");
        }

        private string Telemetry(MissionPhase phase, MissionOutcomeReport o)
        {
            var sb = new StringBuilder($"<b><color=#3FA0FF>{Loc.L("TELEMETRÍA EN VIVO", "LIVE TELEMETRY")}</color></b>\n");
            sb.Append($"{Loc.L("Fase", "Phase")}: <b>{PhaseName(phase)}</b>\n");
            if (orbit != null && orbit.ShowsFreeReturn)
            {
                sb.Append($"{Loc.L("Día de misión", "Mission day")}: <b>{orbit.CurrentDay:F1}</b>\n");
                sb.Append($"{Loc.L("A la Tierra", "To Earth")}: <b>{orbit.DistanceEarthKm:N0} km</b>\n");
                sb.Append($"{Loc.L("A la Luna", "To the Moon")}: <b>{orbit.DistanceMoonKm:N0} km</b>\n");
                sb.Append($"{Loc.L("Velocidad", "Speed")}: <b>{orbit.SpeedKmS:F2} km/s</b>\n");
            }
            else
            {
                var orb = manager.Design.Orbit;
                float alt = orb != null ? orb.altitudeKm : 400f;
                float v = Mathf.Sqrt(EarthMu / (EarthRadiusKm + Mathf.Max(150f, alt)));
                sb.Append($"{Loc.L("Día de misión", "Mission day")}: <b>{progress * Mathf.Max(1f, o.plannedOperationsDays):F0}</b>\n");
                sb.Append($"{Loc.L("Altitud", "Altitude")}: <b>{alt:N0} km</b>\n");
                sb.Append($"{Loc.L("Velocidad orbital", "Orbital speed")}: <b>{v:F2} km/s</b>\n");
                sb.Append($"{Loc.L("Contactos con tierra", "Ground contacts")}: <b>{Mathf.RoundToInt(progress * 40f)}</b>\n");
            }
            float tb = phase >= MissionPhase.DataTransmission ? o.dataReturnedTotalTB * Mathf.InverseLerp(0.55f, 1f, progress) : 0f;
            sb.Append($"{Loc.L("Datos recibidos", "Data received")}: <b>{tb:N2} TB</b>  {Bar(tb / Mathf.Max(0.001f, o.dataReturnedTotalTB))}\n");
            sb.Append($"{Loc.L("Energía", "Power")}: <b>{o.powerAvailabilityFraction:P0}</b>  {Bar(o.powerAvailabilityFraction)}\n");
            int photos = studio != null ? studio.Photos.Count : 0;
            sb.Append($"{Loc.L("Fotos", "Photos")}: <b>{photos}/{o.photoShots}</b>{(o.photosTaken > 0 ? Loc.L($"  (~{o.photosTaken:N0} a bordo)", $"  (~{o.photosTaken:N0} on board)") : "")}");
            return sb.ToString();
        }

        private string Deployment(MissionPhase phase, MissionOutcomeReport o)
        {
            string Step(bool done, string label) => done ? $"<color=#4CD975>[OK]</color> {label}" : $"<color=#6F7F99>[··]</color> <color=#8FA0BA>{label}</color>";
            bool inSpace = phase >= MissionPhase.OrbitInsertion;
            var sb = new StringBuilder("<b><color=#3FA0FF>DESPLIEGUE DE LA NAVE</color></b>\n");
            sb.AppendLine(Step(inSpace, Loc.L("Separación del cohete", "Rocket separation")));
            sb.AppendLine(Step(inSpace && (phase > MissionPhase.OrbitInsertion || progress > 0.3f), Loc.L("Paneles solares desplegados", "Solar panels deployed")));
            sb.AppendLine(Step(phase >= MissionPhase.ScienceOperations, Loc.L("Antena apuntando a tierra", "Antenna pointed at Earth")));
            sb.AppendLine(Step(phase >= MissionPhase.ScienceOperations, o != null && o.photoShots > 0 ? Loc.L("Cámaras e instrumentos activos", "Cameras and instruments active") : Loc.L("Instrumentos activos", "Instruments active")));
            sb.AppendLine(Step(phase >= MissionPhase.DataTransmission, Loc.L("Enlace de bajada de datos", "Data downlink")));
            if (o != null && o.returnsToEarth) sb.AppendLine(Step(phase >= MissionPhase.ReturnAndRecovery, Loc.L("Reentrada y amerizaje", "Reentry and splashdown")));
            sb.Append($"<b><color=#3FA0FF>{Loc.L("BITÁCORA", "LOG")}</color></b>\n");
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
            MissionPhase.Launch => Loc.L("LANZAMIENTO", "LAUNCH"),
            MissionPhase.OrbitInsertion => Loc.L("INSERCIÓN", "INSERTION"),
            MissionPhase.ScienceOperations => Loc.L("OPERACIONES", "OPERATIONS"),
            MissionPhase.DataTransmission => Loc.L("TRANSMISIÓN", "DOWNLINK"),
            MissionPhase.ReturnAndRecovery => Loc.L("REGRESO", "RETURN"),
            MissionPhase.PostMission => Loc.L("ANÁLISIS", "ANALYSIS"),
            _ => Loc.L("DISEÑO", "DESIGN")
        };
    }
}
