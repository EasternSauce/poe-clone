using UnityEngine;

namespace PoeClone.Visuals
{
    /// <summary>A common, slowly turning breeze with smaller travelling gusts.</summary>
    public static class AmbientWind
    {
        public static Vector3 At(Vector3 position)
        {
            float t = Time.time;
            float angle = 0.65f + Mathf.Sin(t * 0.037f) * 0.4f;
            float gust = 0.45f + Mathf.PerlinNoise(position.x * 0.013f + t * 0.09f, position.z * 0.013f) * 0.9f;
            return new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * gust;
        }
    }
}
