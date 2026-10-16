using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.Visuals
{
    /// <summary>
    /// Cheap procedural cloth for the back cape: no simulation, just a generated sheet whose rows
    /// bend progressively further back towards the hem as the wearer moves, with soft vertical folds,
    /// a ripple travelling down the cloth and a sideways flutter. The sheet hangs from a hinge at the
    /// shoulders and is kept behind the body, so it never swings through the legs.
    /// </summary>
    public class SimpleCapePhysics : MonoBehaviour
    {
        [Header("Target")]
        public Transform cape; // the back panel; its mesh is replaced by the generated cloth

        [Header("Movement Response")]
        public float pitchPerSpeed = 3f;      // trail backward when moving forward, degrees per m/s
        public float maxPitchForward = 35f;
        public float restPitch = 4f;          // hangs slightly away from the body even when still
        public float rollPerStrafeSpeed = 14f;
        public float maxRoll = 25f;

        [Header("Spring")]
        public float springSpeed = 8f;
        public float damping = 0.85f;

        [Header("Cloth")]
        public float rippleAmplitude = 0.025f; // at the hem, scaled up while moving
        public float rippleSpeed = 4f;
        public float foldDepth = 0.07f;

        private const int Columns = 10, Rows = 16;
        private const float Thickness = 0.012f;
        private static readonly int ObjectTexture = Shader.PropertyToID("_ObjectTexture");

        private CharacterController controller;
        private Vector3 lastPosition;
        private float currentPitch, pitchVel;
        private float currentRoll, rollVel;
        private float motion; // 0 still .. 1 moving fast, for ripple strength

        private Mesh mesh;
        private Vector3[] vertices;
        private float topHalfWidth, hemHalfWidth, length;

        private void Start()
        {
            WorldActivity.TrackCosmetic(this);
            controller = GetComponentInParent<CharacterController>();
            lastPosition = transform.position;
            if (cape != null && cape.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null)
                Build(filter);
        }

        // Measure the rigid panel the rig was built with, then replace it with a cloth sheet of the
        // same size hanging from its top edge (the transform becomes the hinge, at unit scale).
        private void Build(MeshFilter filter)
        {
            Bounds bounds = filter.sharedMesh.bounds;
            Vector3 scale = cape.localScale;
            length = bounds.size.y * scale.y;
            hemHalfWidth = bounds.extents.x * scale.x;
            topHalfWidth = hemHalfWidth * 0.76f;
            cape.localPosition += cape.localRotation * (Vector3.up * bounds.max.y * scale.y);
            cape.localScale = Vector3.one;

            int perSide = (Columns + 1) * (Rows + 1);
            vertices = new Vector3[perSide * 2];
            var rest = new List<Vector3>(perSide * 2);
            var triangles = new List<int>();
            for (int side = 0; side < 2; side++)
                for (int j = 0; j <= Rows; j++)
                    for (int i = 0; i <= Columns; i++)
                        rest.Add(Sheet(i, j, 0f, 0f, 0f, 0f) + Vector3.back * (side * Thickness));
            for (int side = 0; side < 2; side++)
                for (int j = 0; j < Rows; j++)
                    for (int i = 0; i < Columns; i++)
                    {
                        int a = side * perSide + j * (Columns + 1) + i, b = a + 1, c = a + Columns + 1, d = c + 1;
                        // Front layer faces the body (+z), back layer faces away.
                        if (side == 0) { triangles.AddRange(new[] { a, c, b, b, c, d }); }
                        else { triangles.AddRange(new[] { a, b, c, b, d, c }); }
                    }

            mesh = new Mesh { name = "CapeCloth" };
            mesh.MarkDynamic();
            mesh.SetVertices(rest);
            mesh.SetUVs(1, rest); // rest pose: the texture stays on the cloth as it moves (ToonLit)
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            filter.sharedMesh = mesh;

            var renderer = cape.GetComponent<Renderer>();
            renderer.localBounds = new Bounds(new Vector3(0f, -length * 0.5f, -length * 0.3f), new Vector3(hemHalfWidth * 3f, length * 1.6f, length * 1.4f));
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetFloat(ObjectTexture, 2f);
            renderer.SetPropertyBlock(block);
        }

        // A point of the sheet: column i across, row j down. Rows bend further back towards the hem,
        // so the cloth curves like fabric rather than tipping as a plank.
        private Vector3 Sheet(int i, int j, float pitch, float roll, float time, float strength)
        {
            float u = i / (float)Columns * 2f - 1f;
            Vector3 p = Vector3.zero;
            float step = length / Rows;
            for (int k = 0; k < j; k++)
            {
                float s = (k + 0.5f) / Rows;
                float angle = pitch * Mathf.Deg2Rad * (0.45f + 1.1f * s);
                p += new Vector3(0f, -Mathf.Cos(angle), -Mathf.Sin(angle)) * step;
            }
            float depth = j / (float)Rows;
            float half = Mathf.Lerp(topHalfWidth, hemHalfWidth, depth);
            p.x = u * half;
            // Wrapped round the shoulders at the top, opening out flatter towards the hem.
            p.z += Mathf.Lerp(0.07f, 0.025f, depth) * u * u;
            // Vertical folds, gathered at the shoulders and deepening towards the hem, where they
            // also scallop its outline.
            float fold = 0.5f + 0.5f * Mathf.Cos(u * Mathf.PI * 3.5f + 0.6f);
            p.z -= foldDepth * (0.25f + 0.75f * Mathf.Sqrt(depth)) * fold;
            p.y += 0.03f * depth * depth * depth * fold;
            if (strength > 0f)
            {
                float ripple = Mathf.Sin(time * rippleSpeed - depth * 7f + u * 1.7f) + 0.4f * Mathf.Sin(time * rippleSpeed * 1.7f - depth * 11f - u * 2.3f);
                p.z -= ripple * rippleAmplitude * depth * depth * strength;
                p.x += Mathf.Sin(time * rippleSpeed * 0.6f - depth * 4f) * 0.02f * depth * depth * strength;
            }
            // Lateral movement swings the hem sideways about the hinge.
            p.x += Mathf.Sin(roll * Mathf.Deg2Rad) * -p.y;
            // Never forward of the hinge plane, where the back and the legs are.
            p.z = Mathf.Min(p.z, Mathf.Lerp(0.07f, -0.02f, depth) * u * u);
            return p;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || mesh == null || !cape.gameObject.activeInHierarchy) return;

            Vector3 worldVel = controller != null && controller.velocity.sqrMagnitude > 0.0001f
                ? controller.velocity : (transform.position - lastPosition) / dt;
            lastPosition = transform.position;
            Vector3 localVel = transform.InverseTransformDirection(worldVel);
            localVel.y = 0f;

            float targetPitch = restPitch + Mathf.Clamp(localVel.z * pitchPerSpeed, 0f, maxPitchForward);
            float targetRoll = Mathf.Clamp(-localVel.x * rollPerStrafeSpeed, -maxRoll, maxRoll);
            currentPitch = SpringTowards(currentPitch, targetPitch, ref pitchVel, dt);
            currentRoll = SpringTowards(currentRoll, targetRoll, ref rollVel, dt);
            motion = Mathf.MoveTowards(motion, Mathf.Clamp01(localVel.magnitude / 8f), 2f * dt);

            // Even standing still a little air moves the hem.
            float strength = 0.35f + motion;
            float time = Time.time;
            int perSide = (Columns + 1) * (Rows + 1);
            for (int j = 0; j <= Rows; j++)
                for (int i = 0; i <= Columns; i++)
                {
                    Vector3 p = Sheet(i, j, Mathf.Max(0f, currentPitch), currentRoll, time, strength);
                    int index = j * (Columns + 1) + i;
                    vertices[index] = p;
                    vertices[perSide + index] = p + Vector3.back * Thickness;
                }
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
        }

        private float SpringTowards(float current, float target, ref float velocity, float dt)
        {
            float accel = (target - current) * springSpeed;
            velocity = (velocity + accel * dt) * damping;
            return current + velocity * dt;
        }
    }
}
