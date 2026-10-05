using System;
using UnityEngine;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>
    /// A living snake grown out of a body (the Shepherd's grafted arm, the snakes on his back): a
    /// chain of joints along its local +Y, each bent a little (<see cref="Bend"/>, plus an S-shaped
    /// <see cref="Coil"/>) and rippling with a travelling wave so it never holds still. The head at
    /// the tip has hinged jaws, a mouth and fangs.
    /// It can <see cref="Strike"/> the way a snake does: the head draws back and up while the body
    /// bunches, then flies at the target with the jaws wide, the body's slack straightening out
    /// behind it (it never stretches: its length is fixed), snaps shut there, and draws back into
    /// its coil. The body follows the head through a chain solve (FABRIK) from the shoulder.
    /// Runs after the body's own animators (walk, attack, ShepherdAnimator at 1001), so a strike
    /// aimed at a point stays aimed there however the body is leaning.
    /// </summary>
    [DefaultExecutionOrder(1002)]
    public class SnakeLimb : MonoBehaviour
    {
        /// <summary>Every joint's bend at rest, degrees (x curls toward the limb's local +Z).</summary>
        public Vector3 Bend = new Vector3(8f, 0f, 0f);
        /// <summary>An S-curve along the body at rest, degrees of bend at its peaks.</summary>
        public float Coil;
        public float Sway = 10f;
        public float SwaySpeed = 3f;
        public float JawOpen = 12f;

        private const float StrikeJaw = 38f;
        private const int SolveIterations = 8;

        private Transform[] joints;
        private Transform head, skull, jaw;
        private float step;
        private float headLength;
        private float phase;
        private Vector3[] points;

        // The strike in progress, if any.
        private bool striking;
        private int strikeCount;
        private Vector3 target;
        private float t, windUp, lunge, hold, recover;
        private bool bitten;
        private bool bitePending;
        private Action<Vector3> onBite;
        private Vector3 strikeFrom;
        private float strikeHeight;

        /// <summary>The snake's length, in world metres.</summary>
        public float Length => step * (joints != null ? joints.Length : 0) * transform.lossyScale.y;

        public bool IsStriking => striking;
        public int StrikeCount => strikeCount;
        public Vector3 StrikeTarget => target;
        public float StrikeElapsed => t;
        public float StrikeWindUp => windUp;
        public float StrikeLunge => lunge;
        public float StrikeHold => hold;
        public float StrikeRecover => recover;

        /// <summary>Where its mouth is now (for spitting from).</summary>
        public Vector3 MouthPosition => head != null
            ? head.position + head.up * headLength * transform.lossyScale.y * 0.8f
            : transform.position;

        private static readonly Color Mouth = new Color(0.45f, 0.08f, 0.10f);
        private static readonly Color Fang = new Color(0.95f, 0.93f, 0.85f);
        private static Mesh upperHeadMesh, lowerHeadMesh;

        public static SnakeLimb Build(Transform parent, string name, Vector3 localPosition, Quaternion localRotation,
            int count, float length, float rootThickness, float tipThickness, Color hide, Color belly, Color eyes, bool detailedHead = false)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;
            root.transform.localRotation = localRotation;
            SnakeLimb limb = root.AddComponent<SnakeLimb>();
            limb.phase = UnityEngine.Random.Range(0f, 10f);

            limb.step = length / count;
            limb.joints = new Transform[count];
            limb.points = new Vector3[count + 1];
            Transform at = root.transform;
            for (int i = 0; i < count; i++)
            {
                var joint = new GameObject("Joint" + i).transform;
                joint.SetParent(at, false);
                joint.localPosition = i == 0 ? Vector3.zero : new Vector3(0f, limb.step, 0f);
                float size = Mathf.Lerp(rootThickness, tipThickness, i / (float)(count - 1));
                Part(PrimitiveType.Sphere, joint, i % 4 == 2 ? belly : hide, new Vector3(0f, limb.step * 0.5f, 0f), Vector3.one * size);
                limb.joints[i] = joint;
                at = joint;
            }

            // The head, pointing on along the body (+Y), its top toward +Z. Both jaws hinge at the
            // back of the head: the skull (eyes, fangs) tips up, the lower jaw drops, and the mouth
            // shows between them.
            float h = tipThickness * (detailedHead ? 1.7f : 2.4f);
            limb.headLength = h;
            var head = new GameObject("Head").transform;
            head.SetParent(at, false);
            head.localPosition = new Vector3(0f, limb.step, 0f);
            limb.head = head;
            // The mouth's inside, seen between the jaws when they part.
            if (!detailedHead) Part(PrimitiveType.Sphere, head, Mouth, new Vector3(0f, h * 0.55f, 0f), new Vector3(h * 0.6f, h * 0.9f, h * 0.2f));

            // A wedge: broad over the jaw hinges at the back, tapering to a narrow snout, flat on top.
            limb.skull = new GameObject("Skull").transform;
            limb.skull.SetParent(head, false);
            limb.jaw = new GameObject("Jaw").transform;
            limb.jaw.SetParent(head, false);
            if (detailedHead)
            {
                DetailedHead(limb.skull, limb.jaw, h, hide, belly, eyes);
                return limb;
            }
            Part(PrimitiveType.Sphere, limb.skull, hide, new Vector3(0f, h * 0.30f, h * 0.14f), new Vector3(h * 1.05f, h * 0.70f, h * 0.40f));
            Part(PrimitiveType.Sphere, limb.skull, hide, new Vector3(0f, h * 0.70f, h * 0.12f), new Vector3(h * 0.72f, h * 0.80f, h * 0.30f));
            Part(PrimitiveType.Sphere, limb.skull, hide, new Vector3(0f, h * 1.02f, h * 0.08f), new Vector3(h * 0.40f, h * 0.40f, h * 0.22f));
            // Narrow slit eyes set into the sides, under a heavy brow.
            Color brow = hide * 0.55f;
            brow.a = 1f;
            Part(PrimitiveType.Sphere, limb.skull, brow, new Vector3(h * 0.30f, h * 0.50f, h * 0.27f), new Vector3(h * 0.26f, h * 0.36f, h * 0.10f));
            Part(PrimitiveType.Sphere, limb.skull, brow, new Vector3(-h * 0.30f, h * 0.50f, h * 0.27f), new Vector3(h * 0.26f, h * 0.36f, h * 0.10f));
            Part(PrimitiveType.Sphere, limb.skull, eyes, new Vector3(h * 0.36f, h * 0.52f, h * 0.20f), new Vector3(h * 0.05f, h * 0.16f, h * 0.05f));
            Part(PrimitiveType.Sphere, limb.skull, eyes, new Vector3(-h * 0.36f, h * 0.52f, h * 0.20f), new Vector3(h * 0.05f, h * 0.16f, h * 0.05f));
            // Two fangs under the snout, hidden until the mouth opens.
            Part(PrimitiveType.Sphere, limb.skull, Fang, new Vector3(h * 0.12f, h * 0.98f, -h * 0.06f), new Vector3(h * 0.05f, h * 0.06f, h * 0.20f));
            Part(PrimitiveType.Sphere, limb.skull, Fang, new Vector3(-h * 0.12f, h * 0.98f, -h * 0.06f), new Vector3(h * 0.05f, h * 0.06f, h * 0.20f));

            // The lower jaw, the same wedge shallower, pale underneath.
            Part(PrimitiveType.Sphere, limb.jaw, belly, new Vector3(0f, h * 0.35f, -h * 0.12f), new Vector3(h * 0.95f, h * 0.70f, h * 0.22f));
            Part(PrimitiveType.Sphere, limb.jaw, belly, new Vector3(0f, h * 0.80f, -h * 0.10f), new Vector3(h * 0.55f, h * 0.70f, h * 0.18f));
            return limb;
        }

        // Colossal heads use a bevelled wedge mesh, not overlapping balloon-like spheres.
        private static Mesh HeadMesh(bool lower)
        {
            Mesh cached = lower ? lowerHeadMesh : upperHeadMesh;
            if (cached != null) return cached;
            float[] y = { 0f, 0.22f, 0.55f, 0.92f, 1.12f };
            float[] width = { 0.34f, 0.51f, 0.43f, 0.25f, 0.14f };
            float[] height = { 0.09f, 0.17f, 0.13f, 0.07f, 0.04f };
            var vertices = new Vector3[40];
            var triangles = new System.Collections.Generic.List<int>();
            for (int ring = 0; ring < 5; ring++)
            {
                float w = width[ring] * (lower ? 0.94f : 1f);
                float d = height[ring] * (lower ? 0.65f : 1f);
                float z = lower ? -0.12f : 0.12f;
                Vector2[] bevel = { new Vector2(-0.75f, 1f), new Vector2(0.75f, 1f), new Vector2(1f, 0.45f), new Vector2(1f, -0.45f),
                    new Vector2(0.75f, -1f), new Vector2(-0.75f, -1f), new Vector2(-1f, -0.45f), new Vector2(-1f, 0.45f) };
                for (int side = 0; side < 8; side++) vertices[ring * 8 + side] = new Vector3(bevel[side].x * w, y[ring], z + bevel[side].y * d);
                if (ring == 4) continue;
                for (int side = 0; side < 8; side++)
                {
                    int a = ring * 8 + side, b = ring * 8 + (side + 1) % 8, c = a + 8, e = b + 8;
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    triangles.Add(b); triangles.Add(e); triangles.Add(c);
                }
            }
            for (int side = 1; side < 7; side++)
            {
                triangles.Add(0); triangles.Add(side + 1); triangles.Add(side);
                triangles.Add(32); triangles.Add(32 + side); triangles.Add(32 + side + 1);
            }
            // Separate face vertices preserve the angular silhouette and scale-like planes.
            var flat = new Vector3[triangles.Count];
            var indices = new int[triangles.Count];
            for (int i = 0; i < indices.Length; i++) { flat[i] = vertices[triangles[i]]; indices[i] = i; }
            cached = new Mesh { name = lower ? "Colossal lower jaw" : "Colossal skull", vertices = flat, triangles = indices };
            cached.RecalculateNormals(); cached.RecalculateBounds();
            if (lower) lowerHeadMesh = cached; else upperHeadMesh = cached;
            return cached;
        }

        private static void HeadPart(Transform parent, string name, Mesh mesh, Color color, float size)
        {
            GameObject go = RuntimePrimitives.Create(PrimitiveType.Cube, parent, color);
            go.name = name;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.transform.localScale = Vector3.one * size;
        }

        private static void DetailedHead(Transform skull, Transform jaw, float h, Color hide, Color belly, Color eyes)
        {
            HeadPart(skull, "AngularSkull", HeadMesh(false), hide, h);
            HeadPart(jaw, "AngularJaw", HeadMesh(true), belly, h);
            Color dark = new Color(0.025f, 0.016f, 0.016f);
            Part(PrimitiveType.Cube, skull, dark, new Vector3(0f, h * 0.45f, -h * 0.035f), new Vector3(h * 0.55f, h * 0.70f, h * 0.035f));
            Part(PrimitiveType.Cube, jaw, Mouth, new Vector3(0f, h * 0.5f, -h * 0.045f), new Vector3(h * 0.52f, h * 0.65f, h * 0.045f));
            for (int side = -1; side <= 1; side += 2)
            {
                Part(PrimitiveType.Cube, skull, dark, new Vector3(side * h * 0.41f, h * 0.42f, h * 0.20f), new Vector3(h * 0.18f, h * 0.20f, h * 0.055f));
                Part(PrimitiveType.Cube, skull, eyes, new Vector3(side * h * 0.41f, h * 0.43f, h * 0.235f), new Vector3(h * 0.12f, h * 0.14f, h * 0.035f));
                Part(PrimitiveType.Cube, skull, dark, new Vector3(side * h * 0.41f, h * 0.43f, h * 0.255f), new Vector3(h * 0.023f, h * 0.13f, h * 0.015f));
                Part(PrimitiveType.Cube, skull, dark, new Vector3(side * h * 0.115f, h * 0.97f, h * 0.15f), new Vector3(h * 0.07f, h * 0.075f, h * 0.025f));
                for (int i = 0; i < 7; i++)
                {
                    float along = 0.28f + i * 0.105f;
                    float edge = Mathf.Lerp(0.41f, 0.16f, i / 6f);
                    Part(PrimitiveType.Cube, skull, i % 2 == 0 ? hide * 0.75f : hide * 1.25f,
                        new Vector3(side * h * edge * 0.62f, h * along, h * (0.30f - i * 0.018f)), new Vector3(h * 0.19f, h * 0.09f, h * 0.025f));
                    Part(PrimitiveType.Capsule, skull, Fang, new Vector3(side * h * edge, h * along, -h * 0.08f), new Vector3(h * 0.025f, h * 0.07f, h * 0.025f));
                    Part(PrimitiveType.Capsule, jaw, Fang, new Vector3(side * h * edge * 0.90f, h * along, 0f), new Vector3(h * 0.02f, h * 0.055f, h * 0.02f));
                }
                Part(PrimitiveType.Capsule, skull, Fang, new Vector3(side * h * 0.21f, h * 0.68f, -h * 0.15f), new Vector3(h * 0.04f, h * 0.17f, h * 0.04f));
            }
            Part(PrimitiveType.Cube, jaw, Mouth * 1.4f, new Vector3(0f, h * 0.75f, 0f), new Vector3(h * 0.10f, h * 0.5f, h * 0.03f));
        }

        public Transform MouthTransform => head;
        public void MoveHeldTarget(Vector3 at) { if (striking && bitten) target = at; }

        /// <summary>
        /// Draws back for <paramref name="coil"/> seconds, flies at <paramref name="at"/> over
        /// <paramref name="shoot"/>, snaps its jaws shut - calling <paramref name="bite"/> with where
        /// its mouth is - holds for <paramref name="stay"/>, and draws back over <paramref name="back"/>.
        /// </summary>
        public void Strike(Vector3 at, float coil, float shoot, float stay, float back, Action<Vector3> bite)
        {
            strikeCount++;
            striking = true;
            target = at;
            t = 0f;
            windUp = Mathf.Max(0.01f, coil);
            lunge = Mathf.Max(0.01f, shoot);
            hold = Mathf.Max(0f, stay);
            recover = Mathf.Max(0.01f, back);
            bitten = false;
            bitePending = false;
            onBite = bite;
            strikeFrom = Tip();
            strikeHeight = 0f;
        }

        public void ReplayStrike(Vector3 at, float coil, float shoot, float stay, float back, float elapsed)
        {
            Strike(at, coil, shoot, stay, back, null);
            t = Mathf.Clamp(elapsed, 0f, windUp + lunge + hold + recover);
        }

        /// <summary>Colossal sky bite: rear over the marked target, then drop vertically.</summary>
        public void StrikeFromAbove(Vector3 at, float height, float coil, float shoot, float stay, float back, Action<Vector3> bite)
        {
            Strike(at, coil, shoot, stay, back, bite);
            strikeHeight = height;
        }

        private EnemyHealth owner;

        private void Start()
        {
            owner = GetComponentInParent<EnemyHealth>();
        }

        private void LateUpdate()
        {
            // Dead with its body: no more ripple or strikes, it stays as it was.
            if (joints == null || (owner != null && owner.IsDead))
                return;

            float time = Time.time * SwaySpeed + phase;
            float jawAngle = JawOpen * (0.8f + 0.2f * Mathf.Sin(time * 1.7f));
            PoseAtRest(time, striking ? 0.3f : 1f);

            if (striking)
            {
                Vector3 head = StrikeHead(ref jawAngle);
                Solve(head);
                if (bitePending)
                {
                    bitePending = false;
                    onBite?.Invoke(MouthPosition);
                }
            }

            // Whatever the body is doing, the head keeps its top to the sky (the jaws open up-down).
            if (head != null)
            {
                Vector3 along = head.parent.up;
                Vector3 top = Vector3.ProjectOnPlane(Vector3.up, along);
                if (top.sqrMagnitude > 1e-4f)
                    head.rotation = Quaternion.LookRotation(top.normalized, along);
            }
            if (skull != null)
                skull.localRotation = Quaternion.Euler(jawAngle * 0.45f, 0f, 0f);
            if (jaw != null)
                jaw.localRotation = Quaternion.Euler(-jawAngle, 0f, 0f);
        }

        // The resting shape: a constant curl, an S along the body, and a travelling ripple.
        private void PoseAtRest(float time, float swayScale)
        {
            float sway = Sway * swayScale;
            int n = joints.Length;
            for (int i = 0; i < n; i++)
            {
                float wave = time - i * 0.6f;
                float s = Coil * Mathf.Sin(2f * Mathf.PI * i / n);
                joints[i].localRotation = Quaternion.Euler(
                    Bend.x + s + Mathf.Sin(wave) * sway * 0.5f,
                    Bend.y,
                    Bend.z + Mathf.Cos(wave * 0.8f) * sway);
            }
        }

        // Where the head should be at this moment of the strike (and how open its jaws are).
        private Vector3 StrikeHead(ref float jawAngle)
        {
            t += Time.deltaTime;
            Vector3 root = transform.position;
            Vector3 rest = Tip();
            // Drawn back: the head lowered to the target's height, a short way out in front of
            // the body, so the lunge comes in level - a bite, not a blow from above.
            Vector3 flat = target - root;
            flat.y = 0f;
            Vector3 cocked = root + flat * 0.3f;
            cocked.y = target.y + 0.15f * (root.y - target.y);
            if (strikeHeight > 0f) cocked = target + Vector3.up * strikeHeight;

            if (t < windUp)
            {
                float f = Smooth(t / windUp);
                jawAngle = Mathf.Lerp(jawAngle, StrikeJaw, f);
                return Vector3.Lerp(strikeFrom, cocked, f);
            }
            if (t < windUp + lunge)
            {
                float f = (t - windUp) / lunge;
                jawAngle = StrikeJaw;
                return Vector3.Lerp(cocked, target, f);
            }
            if (!bitten)
            {
                bitten = true;
                bitePending = true;
            }
            if (t < windUp + lunge + hold)
            {
                jawAngle = 0f;
                return target;
            }
            float back = Mathf.Clamp01((t - windUp - lunge - hold) / recover);
            jawAngle = Mathf.Lerp(0f, jawAngle, back);
            if (back >= 1f)
                striking = false;
            return Vector3.Lerp(target, rest, Smooth(back));
        }

        // Where the head is in the current pose.
        private Vector3 Tip()
        {
            Transform last = joints[joints.Length - 1];
            return last.position + last.up * step * transform.lossyScale.y;
        }

        // FABRIK: the head on the goal, the root on the shoulder, every segment its own length,
        // starting from the current pose so the body keeps its shape where it can. Then each joint
        // turned to point along its segment, its top (+Z) kept toward the sky.
        private void Solve(Vector3 goal)
        {
            int n = joints.Length;
            float segment = step * transform.lossyScale.y;
            for (int i = 0; i < n; i++)
                points[i] = joints[i].position;
            points[n] = Tip();

            Vector3 root = points[0];
            // It's the mouth, half a head past the neck, that lands on the goal.
            Vector3 toGoal = goal - root;
            if (toGoal.sqrMagnitude > 1e-6f)
                goal -= toGoal.normalized * headLength * transform.lossyScale.y * 0.5f;
            if ((goal - root).magnitude >= segment * n)
            {
                Vector3 dir = (goal - root).normalized;
                for (int i = 1; i <= n; i++)
                    points[i] = root + dir * segment * i;
            }
            else
            {
                for (int k = 0; k < SolveIterations; k++)
                {
                    points[n] = goal;
                    for (int i = n - 1; i >= 0; i--)
                        points[i] = points[i + 1] + (points[i] - points[i + 1]).normalized * segment;
                    points[0] = root;
                    for (int i = 1; i <= n; i++)
                        points[i] = points[i - 1] + (points[i] - points[i - 1]).normalized * segment;
                }
            }

            for (int i = 0; i < n; i++)
            {
                Vector3 dir = points[i + 1] - points[i];
                if (dir.sqrMagnitude < 1e-8f)
                    continue;
                dir.Normalize();
                Vector3 top = Vector3.ProjectOnPlane(Vector3.up, dir);
                if (top.sqrMagnitude < 1e-4f)
                    top = Vector3.ProjectOnPlane(joints[i].forward, dir);
                joints[i].rotation = Quaternion.LookRotation(top.normalized, dir);
            }
        }

        private static float Smooth(float f)
        {
            f = Mathf.Clamp01(f);
            return f * f * (3f - 2f * f);
        }

        private static void Part(PrimitiveType type, Transform parent, Color color, Vector3 position, Vector3 scale)
        {
            GameObject go = RuntimePrimitives.Create(type, parent, color);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
        }
    }
}
