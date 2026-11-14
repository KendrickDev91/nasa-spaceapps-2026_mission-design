using UnityEngine;

namespace MissionDesign.Exploration
{
    /// <summary>Volumen con nombre para mostrar en qué zona del centro espacial está el jugador.</summary>
    public sealed class ZoneVolume : MonoBehaviour
    {
        public string zoneName;
        public Vector3 size = new Vector3(10f, 10f, 10f);

        public float Volume => size.x * size.y * size.z;

        public bool Contains(Vector3 worldPoint)
        {
            Vector3 p = worldPoint - transform.position;
            return Mathf.Abs(p.x) <= size.x * 0.5f && Mathf.Abs(p.y) <= size.y * 0.5f && Mathf.Abs(p.z) <= size.z * 0.5f;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.4f);
            Gizmos.DrawWireCube(transform.position, size);
        }
    }
}
