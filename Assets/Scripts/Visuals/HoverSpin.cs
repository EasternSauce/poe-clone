using UnityEngine;

namespace PoeClone.Visuals
{
    /// <summary>
    /// Keeps a floating thing (a waystone's crystal and its shards) turning slowly and bobbing
    /// about where it was placed, and breathes an optional light with it.
    /// </summary>
    public sealed class HoverSpin : MonoBehaviour
    {
        private Vector3 rest;
        private float spin, bob, phase;
        private Light pulse;
        private float baseIntensity;

        public static HoverSpin Attach(GameObject go, float spinDegrees, float bobHeight, Light light = null)
        {
            var hover = go.AddComponent<HoverSpin>();
            hover.rest = go.transform.localPosition;
            hover.spin = spinDegrees;
            hover.bob = bobHeight;
            hover.phase = go.transform.position.x * 0.37f + go.transform.position.z * 0.61f;
            hover.pulse = light;
            if (light != null) hover.baseIntensity = light.intensity;
            return hover;
        }

        private void Update()
        {
            float wave = Mathf.Sin(Time.time * 1.3f + phase);
            transform.localPosition = rest + Vector3.up * (wave * bob);
            transform.Rotate(0f, spin * Time.deltaTime, 0f, Space.Self);
            if (pulse != null)
                pulse.intensity = baseIntensity * (1f + 0.12f * wave);
        }
    }
}
