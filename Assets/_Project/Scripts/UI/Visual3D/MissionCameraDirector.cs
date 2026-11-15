using MissionDesign.Core;
using UnityEngine;

namespace MissionDesign.UI.Visual3D
{
    /// <summary>
    /// Mueve la cámara principal entre los "sets" según la fase: mesa de diseño → plataforma de
    /// lanzamiento → vista orbital (o sobrevuelo lunar) → sala de control para la bajada de datos y el informe.
    /// El HUD (ScreenSpace-Camera) viaja con la cámara.
    /// </summary>
    public sealed class MissionCameraDirector : MonoBehaviour
    {
        public enum Shot { Design, Launch, Orbit, Lunar, ControlRoom }

        [SerializeField] private Camera mainCamera;
        [SerializeField] private LaunchSiteController launchSite;
        [SerializeField] private OrbitViewController orbitView;
        [SerializeField] private LunarFlyover lunarFlyover;
        [SerializeField] private Transform controlRoomPose;
        [SerializeField] private float followSharpness = 6f;
        [Tooltip("Pose frente a los operadores para ver su reacción cuando el lanzamiento es exitoso.")]
        [SerializeField] private Transform reactionPose;
        [SerializeField] private float reactionSeconds = 1.8f;
        private float reactionUntil;
        [Tooltip("Plano general alto: la sala con los operadores y la pantalla grande al fondo.")]
        [SerializeField] private Transform wideRoomPose;
        [SerializeField] private float wideSeconds = 2.6f;
        private float phaseStart;
        private bool inReaction;

        private MissionStateManager manager;
        private Pose designPose;
        private bool cut = true;

        public Shot Current { get; private set; } = Shot.Design;
        public bool ShowingReaction => Time.time < reactionUntil;

        private void Start()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            designPose = new Pose(mainCamera.transform.position, mainCamera.transform.rotation);
            manager = MissionStateManager.Instance;
            if (manager != null) manager.PhaseChanged += OnPhaseChanged;
        }

        private void OnDestroy()
        {
            if (manager != null) manager.PhaseChanged -= OnPhaseChanged;
        }

        private void OnPhaseChanged(MissionPhase phase)
        {
            bool lunar = manager.Design.Orbit != null && manager.Design.Orbit.regime == OrbitRegime.Lunar;
            Shot next;
            switch (phase)
            {
                case MissionPhase.Launch: next = Shot.Launch; break;
                case MissionPhase.OrbitInsertion: next = Shot.Orbit; break;
                case MissionPhase.ScienceOperations: next = lunar && lunarFlyover != null ? Shot.Lunar : Shot.Orbit; break;
                case MissionPhase.ReturnAndRecovery: next = Shot.Orbit; break;
                case MissionPhase.DataTransmission:
                case MissionPhase.PostMission: next = Shot.ControlRoom; break;
                default: next = Shot.Design; break;
            }
            if (next != Current && !(Current == Shot.ControlRoom && next == Shot.ControlRoom)) cut = true;
            Current = next;
            phaseStart = Time.time;
            if (phase == MissionPhase.OrbitInsertion && reactionPose != null)
            {
                reactionUntil = Time.time + reactionSeconds;
                cut = true;
            }
            else reactionUntil = 0f;   // la reacción no tapa la trayectoria de la fase siguiente
        }

        private void LateUpdate()
        {
            if (mainCamera == null) return;
            bool reacting = ShowingReaction;
            if (reacting != inReaction) { cut = true; inReaction = reacting; }
            Pose target = reacting ? new Pose(reactionPose.position, reactionPose.rotation) : TargetPose();
            if (cut)
            {
                mainCamera.transform.SetPositionAndRotation(target.position, target.rotation);
                cut = false;
                return;
            }
            float k = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            mainCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(mainCamera.transform.position, target.position, k),
                Quaternion.Slerp(mainCamera.transform.rotation, target.rotation, k));
        }

        private Pose TargetPose()
        {
            switch (Current)
            {
                case Shot.Launch when launchSite != null: return launchSite.GetCameraPose();
                case Shot.Orbit when orbitView != null: return orbitView.GetCameraPose();
                case Shot.Lunar when lunarFlyover != null: return lunarFlyover.GetCameraPose();
                case Shot.ControlRoom when controlRoomPose != null:
                {
                    // Primero el plano general (sala + pantalla); luego la cámara se desliza al costado, con los operadores.
                    bool wide = wideRoomPose != null && (manager.Phase == MissionPhase.PostMission || Time.time - phaseStart < wideSeconds);
                    if (wide) return new Pose(wideRoomPose.position, wideRoomPose.rotation);
                    float drift = Mathf.Sin((Time.time - phaseStart) * 0.35f) * 0.8f;
                    return new Pose(controlRoomPose.position + controlRoomPose.right * drift, controlRoomPose.rotation);
                }
                default: return designPose;
            }
        }
    }
}
