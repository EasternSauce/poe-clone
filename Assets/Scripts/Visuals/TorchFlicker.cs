using UnityEngine;

namespace PoeClone.Visuals
{
    /// <summary>
    /// Very simple point-light flicker for torches: perturbs intensity (and a
    /// touch of flame scale) using layered Perlin noise, no textures or
    /// coroutines needed.
    /// </summary>
    public class TorchFlicker : MonoBehaviour
    {
        public Light torchLight;
        public Transform flameVisual;

        public float baseIntensity = 2.4f;
        public float flickerAmount = 0.45f;
        public float flickerSpeed = 9f;
        public float flameScaleJitter = 0.15f;

        private float seed;
        private Vector3 baseFlameScale = Vector3.one;

        private void Awake()
        {
            if (torchLight == null) torchLight = GetComponent<Light>();
            seed = Random.Range(0f, 1000f);
            if (flameVisual != null) baseFlameScale = flameVisual.localScale;
        }

        private void Update()
        {
            float n = Mathf.PerlinNoise(seed, Time.time * flickerSpeed);
            float flicker = (n - 0.5f) * 2f * flickerAmount;

            if (torchLight != null)
                torchLight.intensity = baseIntensity + flicker;

            if (flameVisual != null)
            {
                float s = 1f + (n - 0.5f) * flameScaleJitter;
                flameVisual.localScale = new Vector3(baseFlameScale.x, baseFlameScale.y * s, baseFlameScale.z);
            }
        }
    }
}
