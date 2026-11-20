using UnityEngine;
using UnityEngine.InputSystem;

namespace MissionDesign.Exploration
{
    /// <summary>
    /// Personaje en tercera persona con CharacterController: WASD/flechas relativo a la cámara, Shift corre, Espacio salta.
    /// Alimenta el Animator con Speed (0 reposo · 1 caminar · 2 correr), Grounded y el trigger Jump.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float walkSpeed = 2.75f;   // +25 %
        [SerializeField] private float runSpeed = 7.5f;     // +25 %
        [SerializeField] private float jumpHeight = 1.0f;
        [SerializeField] private float gravity = -19.6f;
        [SerializeField] private float turnSpeed = 12f;
        [SerializeField] private float fallResetY = -25f;

        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int GroundedId = Animator.StringToHash("Grounded");
        private static readonly int JumpId = Animator.StringToHash("Jump");

        private CharacterController controller;
        private Vector3 velocity;
        private float animSpeed;
        private Vector3 safePosition;
        private float safeYaw;
        private bool grounded;

        /// <summary>Bloquea el movimiento (paneles abiertos, menú).</summary>
        public bool InputLocked { get; set; }
        /// <summary>Entrada simulada (pruebas automáticas); se suma al teclado.</summary>
        public Vector2 SimulatedMove { get; set; }
        public bool SimulatedRun { get; set; }
        public bool IsGrounded => grounded;
        public float AnimatorSpeed => animSpeed;
        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;
        /// <summary>Congelado (sentado en una silla): sin física ni movimiento; el CrewPoser coloca el cuerpo.</summary>
        public bool Frozen { get; private set; }

        /// <summary>Desplaza al personaje junto con una plataforma móvil (ascensor).</summary>
        public void Carry(Vector3 delta)
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            bool was = controller.enabled;
            controller.enabled = false;
            transform.position += delta;
            controller.enabled = was;
            if (velocity.y < 0f) velocity.y = -2f;
        }

        public void SetFrozen(bool frozen)
        {
            Frozen = frozen;
            if (controller == null) controller = GetComponent<CharacterController>();
            controller.enabled = !frozen;
            velocity = Vector3.zero;
            animSpeed = 0f;
            if (animator != null) animator.SetFloat(SpeedId, 0f);
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            controller.minMoveDistance = 0f;
            safePosition = transform.position;
            safeYaw = transform.eulerAngles.y;
            if (animator != null) animator.applyRootMotion = false;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || Frozen) return;
            Vector2 input = SimulatedMove;
            bool run = SimulatedRun, jump = false;
            var kb = Keyboard.current;
            if (!InputLocked && kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
                run |= kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                jump = kb.spaceKey.wasPressedThisFrame;
            }
            if (!InputLocked && TouchControls.Active)
            {
                input += TouchControls.Move;
                run |= TouchControls.Run;
                jump |= TouchControls.ConsumeJump();
            }
            if (InputLocked) { input = Vector2.zero; jump = false; }
            input = Vector2.ClampMagnitude(input, 1f);

            Vector3 forward = cameraTransform != null ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up) : transform.forward;
            if (forward.sqrMagnitude < 1e-4f) forward = transform.forward;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 move = forward * input.y + right * input.x;

            if (move.sqrMagnitude > 1e-4f)
            {
                var target = Quaternion.LookRotation(move.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-turnSpeed * dt));
            }

            grounded = CheckGrounded();
            if (grounded && velocity.y < 0f) velocity.y = -2f;
            if (grounded && jump)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                if (animator != null) animator.SetTrigger(JumpId);
            }
            velocity.y += gravity * dt;
            float speed = run ? runSpeed : walkSpeed;
            var flags = controller.Move((move * speed + Vector3.up * velocity.y) * dt);
            grounded = (flags & CollisionFlags.Below) != 0 || CheckGrounded();

            float targetAnim = move.magnitude * (run ? 2f : 1f);
            animSpeed = Mathf.MoveTowards(animSpeed, targetAnim, dt * 5f);
            if (animator != null)
            {
                animator.SetFloat(SpeedId, animSpeed);
                animator.SetBool(GroundedId, grounded);
            }

            if (transform.position.y < fallResetY) Teleport(safePosition, safeYaw);
        }

        /// <summary>isGrounded del CharacterController parpadea en reposo: se confirma con un SphereCast corto hacia abajo.</summary>
        private bool CheckGrounded()
        {
            if (controller.isGrounded) return true;
            float r = controller.radius * 0.9f;
            Vector3 origin = transform.position + Vector3.up * (r + 0.1f);
            return velocity.y <= 0.1f && Physics.SphereCast(origin, r, Vector3.down, out _, 0.2f, ~(1 << 2), QueryTriggerInteraction.Ignore);
        }

        /// <summary>Mueve al personaje sin que el CharacterController lo impida (viaje rápido, reaparición).</summary>
        public void Teleport(Vector3 position, float yaw)
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            controller.enabled = true;
            velocity = Vector3.zero;
            var cam = cameraTransform != null ? cameraTransform.GetComponent<ThirdPersonCamera>() : null;
            if (cam != null) cam.SnapBehind(yaw);
        }
    }
}
