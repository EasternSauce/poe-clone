using UnityEngine;

namespace PoeClone.Visuals
{
    /// <summary>Small canopy sway; trunks and ground-level collision stay fixed.</summary>
    public sealed class WindSway : MonoBehaviour
    {
        private Quaternion rest;
        private float phase;
        private void Awake() { rest = transform.localRotation; phase = transform.position.x + transform.position.z; }
        private void Update()
        {
            Vector3 wind = transform.InverseTransformDirection(AmbientWind.At(transform.position));
            float flutter = Mathf.Sin(Time.time * 1.7f + phase) * 0.6f;
            transform.localRotation = rest * Quaternion.Euler(wind.z * 1.4f + flutter, 0, -wind.x * 1.4f);
        }
    }
}
