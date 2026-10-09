using UnityEngine;

namespace PoeClone.Visuals
{
    /// <summary>
    /// Point-light flicker for torches; the optional flame mesh gets wind motion and smoke.
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

        private void Awake()
        {
            if (torchLight == null) torchLight = GetComponent<Light>();
            seed = Random.Range(0f, 1000f);
            if (flameVisual != null)
            {
                LivingFlame.Attach(flameVisual.gameObject);
            }
        }

        private void Update()
        {
            float n = Mathf.PerlinNoise(seed, Time.time * flickerSpeed);
            float flicker = (n - 0.5f) * 2f * flickerAmount;

            if (torchLight != null)
                torchLight.intensity = baseIntensity + flicker;
        }
    }
}
