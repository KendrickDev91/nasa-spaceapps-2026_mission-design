using UnityEngine;

namespace MissionDesign.UI.Visual3D
{
    /// <summary>Rotación lenta de exhibición para la nave en el ensamblador.</summary>
    public sealed class SlowSpin : MonoBehaviour
    {
        [SerializeField] private Vector3 degreesPerSecond = new Vector3(0f, 12f, 0f);

        private void Update() => transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
