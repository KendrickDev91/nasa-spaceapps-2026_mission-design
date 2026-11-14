using UnityEngine;
using UnityEngine.InputSystem;

namespace MissionDesign.Exploration
{
    /// <summary>
    /// Cámara orbital en tercera persona: clic derecho + ratón gira, rueda acerca/aleja (también Q/Z para girar con teclado).
    /// Un SphereCast evita que atraviese paredes y techos.
    /// </summary>
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float distance = 4.2f;
        [SerializeField] private float minDistance = 1.5f;
        [SerializeField] private float maxDistance = 14f;
        [SerializeField] private float pivotHeight = 1.55f;
        [SerializeField] private float mouseSensitivity = 0.18f;
        [SerializeField] private LayerMask collisionMask = ~(1 << 2);
        [SerializeField] private float yaw;
        [SerializeField] private float pitch = 14f;

        private float currentDistance;

        public bool InputLocked { get; set; }
        public float Yaw => yaw;

        private void Start() => currentDistance = distance;

        public void SnapBehind(float newYaw)
        {
            yaw = newYaw;
            currentDistance = distance;
            LateUpdate();
        }

        private void LateUpdate()
        {
            if (target == null) return;
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            float dt = Time.unscaledDeltaTime;
            if (!InputLocked)
            {
                if (mouse != null)
                {
                    if (mouse.rightButton.isPressed)
                    {
                        Vector2 d = mouse.delta.ReadValue();
                        yaw += d.x * mouseSensitivity;
                        pitch = Mathf.Clamp(pitch - d.y * mouseSensitivity, -25f, 70f);
                    }
                    float scroll = mouse.scroll.ReadValue().y;
                    if (Mathf.Abs(scroll) > 0.01f)
                        distance = Mathf.Clamp(distance - Mathf.Sign(scroll) * 0.6f, minDistance, maxDistance);
                }
                if (kb != null)
                {
                    if (kb.qKey.isPressed) yaw -= 90f * dt;
                    if (kb.zKey.isPressed) yaw += 90f * dt;
                }
            }

            Vector3 pivot = target.position + Vector3.up * pivotHeight;
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 dir = rot * Vector3.back;
            float wanted = distance;
            if (Physics.SphereCast(pivot, 0.25f, dir, out var hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
                wanted = Mathf.Max(0.4f, hit.distance - 0.1f);
            // Acercar al instante (no atravesar paredes); alejar suave.
            currentDistance = wanted < currentDistance ? wanted : Mathf.Lerp(currentDistance, wanted, 1f - Mathf.Exp(-6f * dt));
            transform.SetPositionAndRotation(pivot + dir * currentDistance, rot);
        }
    }
}
