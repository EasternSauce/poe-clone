using UnityEngine;

namespace PoeClone.Visuals
{
    /// <summary>Loops a precomputed downstream route without per-frame terrain queries or physics.</summary>
    public sealed class DriftingWaterBranch : MonoBehaviour
    {
        private Vector3[] route;
        private float phase, speed;
        private Mesh ownedMesh;

        public void Initialize(Vector3[] points, float offset, float metresPerSecond)
        {
            route = points;
            phase = offset * (points.Length - 1);
            speed = metresPerSecond / 0.6f;
            ownedMesh = GetComponentInChildren<MeshFilter>().sharedMesh;
            UpdatePose();
        }

        private void Update()
        {
            phase = Mathf.Repeat(phase + Time.deltaTime * speed, route.Length - 1);
            UpdatePose();
        }

        private void UpdatePose()
        {
            int index = Mathf.Min((int)phase, route.Length - 2);
            Vector3 direction = route[index + 1] - route[index];
            Vector3 at = Vector3.Lerp(route[index], route[index + 1], phase - index);
            float bob = Time.time * 1.1f + route[0].x;
            at.y += Mathf.Sin(bob) * 0.025f;
            transform.SetPositionAndRotation(at, Quaternion.LookRotation(direction) *
                Quaternion.Euler(Mathf.Sin(bob * 0.7f) * 3f, Mathf.Sin(bob * 0.43f) * 14f, Mathf.Cos(bob) * 4f));
            // Submerge gradually at the ends instead of visibly teleporting to the start.
            float visibility = Mathf.SmoothStep(0, 1, Mathf.Min(phase, route.Length - 1 - phase) / 2f);
            transform.position += Vector3.down * (1f - visibility) * 0.4f;
            transform.localScale = Vector3.one * visibility;
        }

        private void OnDestroy()
        {
            if (ownedMesh != null) Destroy(ownedMesh);
        }
    }
}
