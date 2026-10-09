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
        public Vector2 flickerInterval = new Vector2(8f, 20f);
        public Vector2 flickerDuration = new Vector2(0.2f, 0.55f);

        private float seed;
        private float nextFlickerTime;
        private float flickerEndsAt;
        private bool isFlickering;

        private void Awake()
        {
            if (torchLight == null) torchLight = GetComponent<Light>();
            seed = Random.Range(0f, 1000f);
            ScheduleFlicker();
            if (flameVisual != null)
            {
                LivingFlame.Attach(flameVisual.gameObject);
            }
        }

        private void Update()
        {
            if (torchLight == null) return;

            float flicker = 0f;
            if (!isFlickering && Time.time >= nextFlickerTime)
            {
                isFlickering = true;
                flickerEndsAt = Time.time + Random.Range(flickerDuration.x, flickerDuration.y);
            }

            if (isFlickering)
            {
                float n = Mathf.PerlinNoise(seed, Time.time * flickerSpeed);
                flicker = (n - 0.5f) * 2f * flickerAmount;

                if (Time.time >= flickerEndsAt)
                {
                    isFlickering = false;
                    ScheduleFlicker();
                }
            }

            torchLight.intensity = baseIntensity + flicker;
        }

        private void ScheduleFlicker()
        {
            nextFlickerTime = Time.time + Random.Range(flickerInterval.x, flickerInterval.y);
        }
    }
}
