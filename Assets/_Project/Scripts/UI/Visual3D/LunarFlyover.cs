using MissionDesign.Core;
using UnityEngine;

namespace MissionDesign.UI.Visual3D
{
    /// <summary>
    /// Vista desde la superficie lunar durante las operaciones de una misión lunar: una copia de la nave
    /// ensamblada cruza el cielo sobre el terreno y el módulo de aterrizaje, con la Tierra en el horizonte.
    /// </summary>
    public sealed class LunarFlyover : MonoBehaviour
    {
        [SerializeField] private ModularSpacecraftAssembler assembler;
        [SerializeField] private Transform cameraPose;
        [SerializeField] private Transform flyStart;
        [SerializeField] private Transform flyEnd;
        [SerializeField] private float passSeconds = 6f;
        [SerializeField] private float displayScale = 2.5f;

        private MissionStateManager manager;
        private GameObject copy;
        private float t;

        public bool IsActive => copy != null;

        private void Start()
        {
            manager = MissionStateManager.Instance;
            if (manager != null) manager.PhaseChanged += OnPhaseChanged;
        }

        private void OnDestroy()
        {
            if (manager != null) manager.PhaseChanged -= OnPhaseChanged;
        }

        public Pose GetCameraPose()
        {
            Vector3 look = copy != null ? copy.transform.position : flyStart.position;
            return new Pose(cameraPose.position, Quaternion.LookRotation(look - cameraPose.position, Vector3.up));
        }

        private void OnPhaseChanged(MissionPhase phase)
        {
            bool lunar = manager.Design.Orbit != null && manager.Design.Orbit.regime == OrbitRegime.Lunar;
            if (phase == MissionPhase.ScienceOperations && lunar && assembler != null)
            {
                if (copy != null) Destroy(copy);
                copy = assembler.CreateVisualCopy(transform);
                copy.transform.localScale = Vector3.one * displayScale;
                t = 0f;
            }
            else if (phase != MissionPhase.ScienceOperations && copy != null)
            {
                Destroy(copy);
                copy = null;
            }
        }

        private void Update()
        {
            if (copy == null) return;
            t = Mathf.Repeat(t + Time.deltaTime / passSeconds, 1f);
            copy.transform.position = Vector3.Lerp(flyStart.position, flyEnd.position, t);
            copy.transform.Rotate(Vector3.up, 15f * Time.deltaTime, Space.World);
        }
    }
}
