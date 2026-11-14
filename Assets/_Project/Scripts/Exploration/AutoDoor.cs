using UnityEngine;

namespace MissionDesign.Exploration
{
    /// <summary>Puerta corrediza de dos hojas que se abre cuando el personaje se acerca.</summary>
    public sealed class AutoDoor : MonoBehaviour
    {
        [SerializeField] private Transform leftPanel;
        [SerializeField] private Transform rightPanel;
        [SerializeField] private Vector3 slideAxis = Vector3.right;
        [SerializeField] private float slideDistance = 1.45f;
        [SerializeField] private float triggerRange = 4f;
        [SerializeField] private float speed = 2.5f;

        private Transform player;
        private Vector3 left0, right0;
        private float openness;

        public bool IsOpen => openness > 0.95f;
        public float Openness => openness;

        private void Start()
        {
            var pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
            if (leftPanel != null) left0 = leftPanel.localPosition;
            if (rightPanel != null) right0 = rightPanel.localPosition;
        }

        private void Update()
        {
            bool near = false;
            if (player != null)
            {
                Vector3 d = player.position - transform.position;
                near = new Vector2(d.x, d.z).sqrMagnitude < triggerRange * triggerRange && Mathf.Abs(d.y + 1.5f) < 3f;
            }
            openness = Mathf.MoveTowards(openness, near ? 1f : 0f, Time.deltaTime * speed);
            float s = Mathf.SmoothStep(0f, 1f, openness) * slideDistance;
            if (leftPanel != null) leftPanel.localPosition = left0 - slideAxis * s;
            if (rightPanel != null) rightPanel.localPosition = right0 + slideAxis * s;
        }
    }
}
