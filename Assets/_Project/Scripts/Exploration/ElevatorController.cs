using UnityEngine;

namespace MissionDesign.Exploration
{
    /// <summary>
    /// Ascensor de la torre de servicio: plataforma con dos paradas (losa y brazo de acceso de la tripulación).
    /// Si el personaje está sobre la plataforma, viaja con ella (se le aplica el mismo desplazamiento cada fotograma).
    /// Se maneja con E desde la cabina o con los botones de llamada de cada parada.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class ElevatorController : MonoBehaviour
    {
        [SerializeField] private Transform platform;
        [Tooltip("Altura (mundo) de la superficie de la plataforma en cada parada.")]
        [SerializeField] private float bottomSurfaceY = 2f;
        [SerializeField] private float topSurfaceY = 77.3f;
        [SerializeField] private float speed = 8f;
        [SerializeField] private Vector2 halfSize = new Vector2(1.55f, 1.55f);
        [SerializeField] private float thickness = 0.3f;

        private PlayerController player;
        private float targetSurface;

        public bool Moving => platform != null && Mathf.Abs(Surface - targetSurface) > 0.001f;
        public bool AtTop => Mathf.Abs(Surface - topSurfaceY) < 0.05f;
        public float Surface => platform != null ? platform.position.y + thickness * 0.5f : 0f;
        public float TopSurfaceY => topSurfaceY;
        public float BottomSurfaceY => bottomSurfaceY;

        private void Start()
        {
            player = FindAnyObjectByType<PlayerController>();
            targetSurface = Surface;
        }

        /// <summary>Desde la cabina: viaja a la otra parada. Desde un rellano: llama la cabina a esa parada.</summary>
        public void Activate(Transform caller)
        {
            if (Moving) return;
            if (caller != null && IsOnPlatform(caller.position)) targetSurface = AtTop ? bottomSurfaceY : topSurfaceY;
            else targetSurface = caller != null && Mathf.Abs(caller.position.y - topSurfaceY) < Mathf.Abs(caller.position.y - bottomSurfaceY)
                ? topSurfaceY : bottomSurfaceY;
        }

        public string PromptFor(Transform caller)
        {
            if (caller != null && IsOnPlatform(caller.position))
                return AtTop ? "Bajar en el ascensor a la losa" : "Subir en el ascensor al brazo de acceso (75 m)";
            return "Llamar al ascensor";
        }

        public bool IsOnPlatform(Vector3 p)
        {
            if (platform == null) return false;
            Vector3 local = p - platform.position;
            return Mathf.Abs(local.x) <= halfSize.x && Mathf.Abs(local.z) <= halfSize.y && p.y > Surface - 0.4f && p.y < Surface + 1.2f;
        }

        private void Update()
        {
            if (!Moving) return;
            float next = Mathf.MoveTowards(Surface, targetSurface, speed * Time.deltaTime);
            float delta = next - Surface;
            bool carry = player != null && IsOnPlatform(player.transform.position);
            platform.position += Vector3.up * delta;
            if (carry) player.Carry(Vector3.up * delta);
        }
    }
}
