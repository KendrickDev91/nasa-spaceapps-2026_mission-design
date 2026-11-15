using UnityEngine;

namespace MissionDesign.Characters
{
    /// <summary>
    /// Apoya las suelas en el suelo: después del Animator mide el pie más bajo, lanza un rayo hacia el suelo y desplaza
    /// suavemente el modelo visual (no la física) para que no flote ni se hunda en rampas, escalones o losas.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class FootPlanter : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private float soleBelowAnkle = 0.085f;
        [SerializeField] private float soleBelowToes = 0.025f;
        [SerializeField] private float maxCorrection = 0.35f;
        [SerializeField] private float sharpness = 14f;
        [SerializeField] private LayerMask groundMask = ~(1 << 2);

        private Transform visual, lFoot, rFoot, lToes, rToes;
        private Vector3 baseLocal;
        private float offset;
        private CrewPoser poser;
        private Exploration.PlayerController player;

        /// <summary>Distancia (m) entre la suela más baja y el suelo tras la corrección; ~0 = bien apoyado.</summary>
        public float LastGap { get; private set; }
        public float Offset => offset;

        public void Configure(Animator a) => animator = a;

        private void Start()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) { enabled = false; return; }
            visual = animator.transform;
            baseLocal = visual.localPosition;
            lFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            lToes = animator.GetBoneTransform(HumanBodyBones.LeftToes);
            rToes = animator.GetBoneTransform(HumanBodyBones.RightToes);
            poser = GetComponent<CrewPoser>();
            player = GetComponent<Exploration.PlayerController>();
        }

        private void LateUpdate()
        {
            if (visual == null || lFoot == null || rFoot == null) return;
            bool seated = poser != null && poser.SitAmount > 0.05f;
            bool airborne = player != null && (!player.IsGrounded || player.Frozen);
            float k = 1f - Mathf.Exp(-sharpness * Time.deltaTime);

            if (seated || airborne)
            {
                offset = Mathf.Lerp(offset, 0f, k);
            }
            else
            {
                float sole = Mathf.Min(lFoot.position.y - soleBelowAnkle, rFoot.position.y - soleBelowAnkle);
                if (lToes != null) sole = Mathf.Min(sole, lToes.position.y - soleBelowToes);
                if (rToes != null) sole = Mathf.Min(sole, rToes.position.y - soleBelowToes);
                Vector3 origin = transform.position + Vector3.up * 0.8f;
                if (Physics.Raycast(origin, Vector3.down, out var hit, 2f, groundMask, QueryTriggerInteraction.Ignore))
                {
                    float error = sole - hit.point.y;
                    LastGap = error;
                    if (Mathf.Abs(error) < maxCorrection * 1.5f)
                        offset = Mathf.Clamp(offset - error * k, -maxCorrection, maxCorrection);
                }
            }
            visual.localPosition = baseLocal + Vector3.up * offset;
        }
    }
}
