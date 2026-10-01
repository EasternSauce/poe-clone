using System.Collections;
using UnityEngine;
using PoeClone.Visuals;

namespace PoeClone.Skills
{
    /// <summary>
    /// Short-lived visuals for skills, built from coloured primitives (no particle systems or
    /// special shaders, so they render in every build): an expanding ground ring, a lightning
    /// arc between two points, and a rising glow around a character.
    /// </summary>
    public class SkillEffects : MonoBehaviour
    {
        /// <summary>A flat ring that grows out to the radius and sinks away.</summary>
        public static void Shockwave(Vector3 center, float radius, Color color, float seconds = 0.35f)
        {
            var go = new GameObject("Shockwave");
            go.transform.position = new Vector3(center.x, GroundY(center) + 0.05f, center.z);
            GameObject disc = RuntimePrimitives.Create(PrimitiveType.Cylinder, go.transform, color);
            disc.transform.localScale = new Vector3(0.1f, 0.02f, 0.1f);
            go.AddComponent<SkillEffects>().StartCoroutine(Grow(go, disc.transform, radius, seconds));
        }

        /// <summary>A jagged bolt from one point to another, gone in a blink.</summary>
        public static void Arc(Vector3 from, Vector3 to, Color color, float seconds = 0.2f)
        {
            var go = new GameObject("Arc");
            const int segments = 5;
            Vector3 previous = from;
            for (int k = 1; k <= segments; k++)
            {
                Vector3 next = Vector3.Lerp(from, to, k / (float)segments);
                if (k < segments)
                    next += Random.insideUnitSphere * 0.35f;

                Vector3 mid = (previous + next) * 0.5f;
                Vector3 dir = next - previous;
                GameObject seg = RuntimePrimitives.Create(PrimitiveType.Cube, go.transform, color);
                seg.transform.position = mid;
                seg.transform.rotation = Quaternion.LookRotation(dir.sqrMagnitude > 0.0001f ? dir : Vector3.forward);
                seg.transform.localScale = new Vector3(0.07f, 0.07f, dir.magnitude);
                previous = next;
            }
            Destroy(go, seconds);
        }

        /// <summary>A ring of small orbs rising around a character (healing, level-ups).</summary>
        public static void Rise(Transform around, Color color, float seconds = 0.9f)
        {
            var go = new GameObject("Rise");
            go.transform.SetParent(around, false);
            for (int k = 0; k < 8; k++)
            {
                float a = k / 8f * Mathf.PI * 2f;
                GameObject orb = RuntimePrimitives.Create(PrimitiveType.Sphere, go.transform, color);
                orb.transform.localScale = Vector3.one * 0.14f;
                orb.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.6f, -0.9f, Mathf.Sin(a) * 0.6f);
            }
            go.AddComponent<SkillEffects>().StartCoroutine(Climb(go, seconds));
        }

        private static IEnumerator Grow(GameObject root, Transform disc, float radius, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float f = t / seconds;
                float d = Mathf.Lerp(0.2f, radius * 2f, Mathf.Sqrt(f));
                disc.localScale = new Vector3(d, 0.02f, d);
                disc.localPosition = Vector3.down * (0.1f * f);
                yield return null;
            }
            Destroy(root);
        }

        private static IEnumerator Climb(GameObject root, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                root.transform.localPosition = Vector3.up * (1.8f * t / seconds);
                root.transform.localRotation = Quaternion.Euler(0f, 240f * t, 0f);
                yield return null;
            }
            Destroy(root);
        }

        // The lowest surface under the point: the ground, not the character standing on it.
        private static float GroundY(Vector3 p)
        {
            RaycastHit[] hits = Physics.RaycastAll(p + Vector3.up * 1f, Vector3.down, 6f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float y = float.MaxValue;
            foreach (RaycastHit hit in hits)
                y = Mathf.Min(y, hit.point.y);
            return y < float.MaxValue ? y : p.y - 1f;
        }
    }
}
