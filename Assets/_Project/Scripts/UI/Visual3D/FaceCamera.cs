using UnityEngine;

namespace MissionDesign.UI.Visual3D
{
    /// <summary>Orienta un texto 3D hacia la cámara principal (rótulos de la vista orbital).</summary>
    public sealed class FaceCamera : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, cam.transform.up);
        }
    }
}
