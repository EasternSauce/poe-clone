using UnityEngine;
using PoeClone.Skills;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>
    /// A snake that bursts up out of the ground, rears, strikes toward a point, and sinks back:
    /// the Shepherd's Serpent's Call. Only a look - whoever spawns it deals the damage. Its body is
    /// a row of spheres along a curve from the hole: straight up as it rises, then arching over
    /// toward where it bites, swaying all the while.
    /// </summary>
    public class GroundSnake : MonoBehaviour
    {
        private static readonly Color Hide = new Color(0.36f, 0.33f, 0.20f);
        private static readonly Color Belly = new Color(0.55f, 0.50f, 0.32f);
        private static readonly Color Dirt = new Color(0.30f, 0.24f, 0.16f);

        private const int Segments = 22;
        private const float Rise = 0.18f;
        private const float Strike = 0.15f;
        private const float Sink = 0.45f;

        private Transform[] body;
        private Transform head;
        private Vector3 hole;
        private Vector3 toward;
        private float height;
        private float hold;
        private float age;
        private float seed;

        /// <param name="toward">The flat direction it strikes in.</param>
        /// <param name="hold">How long it stays up after striking before sinking.</param>
        public static GroundSnake Spawn(Vector3 at, Vector3 toward, float height = 2.2f, float hold = 0.6f)
        {
            var go = new GameObject("GroundSnake");
            go.transform.position = at;
            GroundSnake s = go.AddComponent<GroundSnake>();
            toward.y = 0f;
            s.toward = toward.sqrMagnitude > 0.001f ? toward.normalized : Vector3.forward;
            s.hole = at;
            s.height = height;
            s.hold = hold;
            s.seed = Random.Range(0f, 10f);
            s.Build();
            SkillEffects.Shockwave(at, 0.45f * height / 2.2f + 0.5f, Dirt, 0.3f);
            return s;
        }

        private void Build()
        {
            body = new Transform[Segments];
            for (int i = 0; i < Segments; i++)
            {
                float size = Mathf.Lerp(0.75f, 0.45f, i / (float)(Segments - 1)) * height / 2.2f;
                GameObject seg = RuntimePrimitives.Create(PrimitiveType.Sphere, transform, i % 4 == 2 ? Belly : Hide);
                seg.transform.localScale = Vector3.one * size;
                body[i] = seg.transform;
            }
            head = new GameObject("Head").transform;
            head.SetParent(transform, false);
            float k = height / 2.2f * 1.5f;
            Part(PrimitiveType.Sphere, head, Hide, new Vector3(0f, 0f, 0.10f) * k, new Vector3(0.40f, 0.28f, 0.54f) * k);
            Part(PrimitiveType.Sphere, head, Belly, new Vector3(0f, -0.08f, 0.26f) * k, new Vector3(0.26f, 0.12f, 0.30f) * k);
            Part(PrimitiveType.Sphere, head, Belly, new Vector3(0.15f, 0.08f, 0.16f) * k, Vector3.one * 0.08f * k);
            Part(PrimitiveType.Sphere, head, Belly, new Vector3(-0.15f, 0.08f, 0.16f) * k, Vector3.one * 0.08f * k);
            Pose();
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age > Rise + Strike + hold + Sink)
            {
                Destroy(gameObject);
                return;
            }
            Pose();
        }

        // Up out of the hole, arch over and strike, hold, then slide back down.
        private void Pose()
        {
            float up = Mathf.Clamp01(age / Rise);
            float strike = Mathf.Clamp01((age - Rise) / Strike);
            float sink = Mathf.Clamp01((age - Rise - Strike - hold) / Sink);
            float shown = up * (1f - sink);
            float arch = Mathf.SmoothStep(0f, 1f, strike) * (1f - sink);

            Vector3 side = Vector3.Cross(Vector3.up, toward);
            Vector3 previous = hole;
            for (int i = 0; i <= Segments; i++)
            {
                float s = i / (float)Segments;
                // The body's length along the curve is fixed; how much of it is above ground grows.
                float along = s * height - (1f - shown) * height;
                float u = Mathf.Max(0f, along / height);
                float sway = Mathf.Sin(Time.time * 7f + seed - s * 4f) * 0.18f * u * (1f - arch * 0.7f);
                Vector3 p = hole
                    + Vector3.up * (along * (1f - 0.45f * arch * u))
                    + toward * (arch * 0.6f * height * u * u)
                    + side * sway;
                if (i < Segments)
                {
                    body[i].position = p;
                    body[i].gameObject.SetActive(along > -0.1f);
                }
                else
                {
                    head.position = p;
                    Vector3 dir = p - previous;
                    if (dir.sqrMagnitude > 0.0001f)
                    {
                        dir.Normalize();
                        // Straight up, "up" for the head is away from where it will strike.
                        head.rotation = Quaternion.LookRotation(dir, Mathf.Abs(dir.y) > 0.95f ? -toward : Vector3.up);
                    }
                    head.gameObject.SetActive(along > -0.1f);
                }
                previous = p;
            }
        }

        private static void Part(PrimitiveType type, Transform parent, Color color, Vector3 position, Vector3 scale)
        {
            GameObject go = RuntimePrimitives.Create(type, parent, color);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
        }
    }
}
