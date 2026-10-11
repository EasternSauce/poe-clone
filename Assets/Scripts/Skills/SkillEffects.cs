using System.Collections;
using UnityEngine;
using PoeClone.Visuals;

namespace PoeClone.Skills
{
    /// <summary>
    /// Short-lived visuals for skills, built from coloured primitives (no particle systems or
    /// special shaders, so they render in every build): an expanding ground ring, a lightning
    /// arc between two points, a fiery explosion, a rising glow around a character, a cut along the
    /// ground, a running fissure and a chain of tremors.
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

        /// <summary>
        /// An arrow dropping out of the sky onto a spot (Rain of Arrows): it appears after the delay,
        /// plunges, kicks up a small ring where it lands (then <paramref name="landed"/> runs, for
        /// the damage) and stays stuck in the ground for a moment.
        /// </summary>
        public static void FallingArrow(Vector3 at, float delay, float radius, Color color, System.Action<Vector3> landed = null)
        {
            var go = new GameObject("Falling Arrow");
            at.y = GroundY(at);
            go.transform.SetPositionAndRotation(at + new Vector3(-1.2f, 9f, 0f), Quaternion.LookRotation(new Vector3(0.13f, -1f, 0f)));
            RuntimePrimitives.BuildArrow(go.transform);
            go.transform.localScale = Vector3.one * 1.3f;
            go.SetActive(false);
            // The coroutine runs on a host that is never hidden.
            var host = new GameObject("Falling Arrow Host").AddComponent<SkillEffects>();
            host.StartCoroutine(Fall(host.gameObject, go, at, delay, radius, color, landed));
        }

        private static IEnumerator Fall(GameObject host, GameObject arrow, Vector3 at, float delay, float radius, Color color, System.Action<Vector3> landed)
        {
            yield return new WaitForSeconds(delay);
            arrow.SetActive(true);
            Vector3 from = arrow.transform.position;
            Vector3 to = at + new Vector3(0f, 0.35f, 0f);
            const float fall = 0.16f;
            for (float t = 0f; t < fall; t += Time.deltaTime)
            {
                arrow.transform.position = Vector3.Lerp(from, to, t / fall);
                yield return null;
            }
            arrow.transform.position = to;
            Shockwave(at, radius, color, 0.22f);
            landed?.Invoke(at);
            Destroy(arrow, 1.2f);
            Destroy(host, 1.2f);
        }

        /// <summary>A ball that bursts out to the radius and collapses again (an explosion).</summary>
        public static void Blast(Vector3 center, float radius, Color color, float seconds = 0.3f)
        {
            var go = new GameObject("Blast");
            go.transform.position = center;
            GameObject ball = RuntimePrimitives.Create(PrimitiveType.Sphere, go.transform, color);
            ball.transform.localScale = Vector3.zero;
            go.AddComponent<SkillEffects>().StartCoroutine(Pop(go, ball.transform, radius, seconds));
        }

        /// <summary>
        /// An explosion built to be seen in a crowd: a white-hot ball that swallows the bodies around
        /// it for an instant, and a bright ring floating above their heads that sweeps out to the
        /// radius. Over in about a quarter of a second, so a chain of them never hides the fight.
        /// </summary>
        public static void Explosion(Vector3 center, float radius, Color color)
        {
            var go = new GameObject("Explosion");
            go.transform.position = new Vector3(center.x, GroundY(center), center.z);
            go.AddComponent<SkillEffects>().StartCoroutine(Explode(go, radius, color));
        }

        private static readonly int ShadowColorId = Shader.PropertyToID("_ShadowColor");
        private static readonly int AmbientBoostId = Shader.PropertyToID("_AmbientBoost");

        private const float RingHeight = 2.3f;     // just above a character's head
        private const int RingSegments = 32;

        private static IEnumerator Explode(GameObject root, float radius, Color color)
        {
            var glow = new MaterialPropertyBlock();
            Transform Glowing(PrimitiveType type, Color c)
            {
                GameObject g = RuntimePrimitives.Create(type, root.transform, c);
                // Fire gives off light: no toon shadow side, so it reads as glowing rather than solid.
                Renderer r = g.GetComponent<Renderer>();
                r.GetPropertyBlock(glow);
                glow.SetColor(ShadowColorId, Color.white);
                glow.SetFloat(AmbientBoostId, 1f);
                r.SetPropertyBlock(glow);
                g.transform.localScale = Vector3.zero;
                return g.transform;
            }

            Transform flash = Glowing(PrimitiveType.Sphere, Color.Lerp(color, new Color(1f, 0.97f, 0.8f), 0.8f));
            flash.localPosition = Vector3.up * 1f;
            var ring = new Transform[RingSegments];
            for (int k = 0; k < RingSegments; k++)
                ring[k] = Glowing(PrimitiveType.Cube, Color.Lerp(color, new Color(1f, 0.9f, 0.35f), 0.6f));

            const float flashTime = 0.15f, ringTime = 0.25f;
            for (float t = 0f; t < ringTime; t += Time.deltaTime)
            {
                // The white-hot ball is out almost at once, swallowing the bodies around it, and gone again.
                float f = t / flashTime;
                flash.localScale = Vector3.one * (f >= 1f ? 0f : radius * 1.5f * (f < 0.3f ? Mathf.Sqrt(f / 0.3f) : 1f - (f - 0.3f) / 0.7f));

                // The ring sweeps out fast, slows as it reaches the blast radius, and thins away there.
                f = t / ringTime;
                float r = Mathf.Lerp(0.3f, radius, 1f - (1f - f) * (1f - f));
                float width = 0.45f * (f < 0.6f ? 1f : 1f - (f - 0.6f) / 0.4f);
                float length = 2f * Mathf.PI * r / RingSegments * 1.15f;
                for (int k = 0; k < RingSegments; k++)
                {
                    float a = k / (float)RingSegments * Mathf.PI * 2f;
                    ring[k].localPosition = new Vector3(Mathf.Cos(a) * r, RingHeight, Mathf.Sin(a) * r);
                    ring[k].localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
                    ring[k].localScale = new Vector3(width, 0.08f, length);
                }
                yield return null;
            }
            Destroy(root);
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

        private static IEnumerator Pop(GameObject root, Transform ball, float radius, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float f = t / seconds;
                // Out fast, then shrinks away.
                float d = radius * 2f * (f < 0.35f ? Mathf.Sqrt(f / 0.35f) : 1f - (f - 0.35f) / 0.65f);
                ball.localScale = Vector3.one * d;
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

        /// <summary>A bright cut along the ground from one point to another that thins away (Cross Slash).</summary>
        public static void Slash(Vector3 from, Vector3 to, float width, Color color, float seconds = 0.25f)
        {
            Vector3 line = to - from;
            line.y = 0f;
            Vector3 mid = (from + to) * 0.5f;
            var go = new GameObject("Slash");
            go.transform.SetPositionAndRotation(new Vector3(mid.x, GroundY(mid) + 0.08f, mid.z),
                Quaternion.LookRotation(line.sqrMagnitude > 0.0001f ? line : Vector3.forward));
            GameObject blade = RuntimePrimitives.Create(PrimitiveType.Cube, go.transform, color);
            blade.transform.localScale = new Vector3(width, 0.03f, 0.1f);
            go.AddComponent<SkillEffects>().StartCoroutine(Thin(go, blade.transform, width, line.magnitude, seconds));
        }

        private static IEnumerator Thin(GameObject root, Transform blade, float width, float length, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float f = t / seconds;
                // Drawn out to its full length at once, then narrows away.
                blade.localScale = new Vector3(width * (1f - f) * 0.5f, 0.03f, length * Mathf.Min(1f, f * 5f));
                yield return null;
            }
            Destroy(root);
        }

        private const float FissureSpeed = 22f;
        private const float FissureStep = 0.7f;

        /// <summary>
        /// A crack that runs along the ground from <paramref name="start"/>, throwing up shards of
        /// rock as it goes, which sink back once it's done. <paramref name="reached"/> runs for each
        /// stretch it opens (from, to), for the damage.
        /// </summary>
        public static void Fissure(Vector3 start, Vector3 direction, float length, float halfWidth, Color color,
            System.Action<Vector3, Vector3> reached = null)
        {
            var host = new GameObject("Fissure").AddComponent<SkillEffects>();
            host.StartCoroutine(Crack(host.gameObject, start, direction, length, halfWidth, color, reached));
        }

        private static IEnumerator Crack(GameObject root, Vector3 start, Vector3 direction, float length, float halfWidth, Color color,
            System.Action<Vector3, Vector3> reached)
        {
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            Vector3 side = new Vector3(direction.z, 0f, -direction.x);
            Color dark = Color.Lerp(color, Color.black, 0.6f);
            Vector3 from = start;
            for (float d = FissureStep; d <= length + 0.001f; d += FissureStep)
            {
                Vector3 to = start + direction * d;
                float ground = GroundY(to);
                GameObject crack = RuntimePrimitives.Create(PrimitiveType.Cube, root.transform, dark);
                crack.transform.SetPositionAndRotation(new Vector3((from.x + to.x) * 0.5f, ground + 0.03f, (from.z + to.z) * 0.5f),
                    Quaternion.LookRotation(direction) * Quaternion.Euler(0f, Random.Range(-12f, 12f), 0f));
                crack.transform.localScale = new Vector3(halfWidth * 0.35f, 0.04f, FissureStep * 1.1f);

                // A shard of rock thrust up beside the crack, tilted away from it.
                float lean = Random.value < 0.5f ? -1f : 1f;
                GameObject shard = RuntimePrimitives.Create(PrimitiveType.Cube, root.transform, color);
                float height = Random.Range(0.5f, 0.9f) * Mathf.Max(0.8f, halfWidth);
                shard.transform.SetPositionAndRotation(
                    new Vector3(to.x, ground, to.z) + side * lean * Random.Range(0.15f, 0.45f) * halfWidth + Vector3.up * height * 0.3f,
                    Quaternion.LookRotation(direction) * Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 90f), lean * Random.Range(10f, 30f)));
                shard.transform.localScale = new Vector3(Random.Range(0.25f, 0.4f), height, Random.Range(0.25f, 0.4f));

                reached?.Invoke(from, to);
                from = to;
                yield return new WaitForSeconds(FissureStep / FissureSpeed);
            }

            yield return new WaitForSeconds(0.3f);
            // The shards sink back into the ground.
            const float sink = 0.4f;
            Vector3 top = root.transform.position;
            for (float t = 0f; t < sink; t += Time.deltaTime)
            {
                root.transform.position = top + Vector3.down * (1.2f * t / sink);
                yield return null;
            }
            Destroy(root);
        }

        /// <summary>
        /// Tremors one after another, <paramref name="step"/> seconds apart (Aftershock): a ring and a
        /// burst at each point, at its radius. <paramref name="landed"/> runs with each one's index.
        /// </summary>
        public static void Ripple(Vector3[] centers, float[] radii, float step, Color color, System.Action<int> landed = null)
        {
            var host = new GameObject("Ripple").AddComponent<SkillEffects>();
            host.StartCoroutine(Tremors(host.gameObject, centers, radii, step, color, landed));
        }

        private static IEnumerator Tremors(GameObject root, Vector3[] centers, float[] radii, float step, Color color, System.Action<int> landed)
        {
            for (int k = 0; k < centers.Length && k < radii.Length; k++)
            {
                if (k > 0)
                    yield return new WaitForSeconds(step);
                Shockwave(centers[k], radii[k], color, 0.32f);
                Blast(new Vector3(centers[k].x, GroundY(centers[k]), centers[k].z), radii[k] * 0.35f, color, 0.22f);
                landed?.Invoke(k);
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
