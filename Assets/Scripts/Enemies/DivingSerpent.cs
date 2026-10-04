using UnityEngine;
using PoeClone.Skills;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>
    /// A huge snake that bursts out of the ground at one point, arcs high over, and dives back in at
    /// another, its body following its head along the arc (the Shepherd's Serpent Dive). Only a
    /// look: whoever launches it deals the damage.
    /// </summary>
    public class DivingSerpent : MonoBehaviour
    {
        private static readonly Color Hide = new Color(0.36f, 0.33f, 0.20f);
        private static readonly Color Belly = new Color(0.55f, 0.50f, 0.32f);
        private static readonly Color Eyes = new Color(1f, 0.82f, 0.18f);
        private static readonly Color Dirt = new Color(0.30f, 0.24f, 0.16f);

        private const int Segments = 40;
        // How much of the path the body covers, behind the head.
        private const float BodyShare = 0.55f;

        private Vector3 from, to;
        private float height, seconds, age;
        private Transform[] body;
        private Transform head;
        private bool burst, dived;

        public static DivingSerpent Launch(Vector3 from, Vector3 to, float height, float thickness, float seconds)
        {
            var go = new GameObject("DivingSerpent");
            DivingSerpent s = go.AddComponent<DivingSerpent>();
            s.from = from + Vector3.down * thickness;
            s.to = to + Vector3.down * thickness;
            s.height = height;
            s.seconds = seconds;
            s.body = new Transform[Segments];
            for (int i = 0; i < Segments; i++)
            {
                float size = thickness * Mathf.Lerp(1f, 0.55f, i / (float)(Segments - 1));
                GameObject seg = RuntimePrimitives.Create(PrimitiveType.Sphere, go.transform, i % 5 == 2 ? Belly : Hide);
                seg.transform.localScale = Vector3.one * size;
                s.body[i] = seg.transform;
            }
            s.head = new GameObject("Head").transform;
            s.head.SetParent(go.transform, false);
            Part(s.head, Hide, new Vector3(0f, 0f, 0.35f) * thickness, new Vector3(1.1f, 0.7f, 1.5f) * thickness);
            Part(s.head, Belly, new Vector3(0f, -0.25f, 0.45f) * thickness, new Vector3(0.9f, 0.35f, 1.2f) * thickness);
            Part(s.head, Eyes, new Vector3(0.45f, 0.2f, 0.6f) * thickness, new Vector3(0.12f, 0.12f, 0.3f) * thickness);
            Part(s.head, Eyes, new Vector3(-0.45f, 0.2f, 0.6f) * thickness, new Vector3(0.12f, 0.12f, 0.3f) * thickness);
            s.Pose();
            return s;
        }

        private void Update()
        {
            age += Time.deltaTime;
            Pose();
            if (age > seconds * (1f + BodyShare) + 0.1f)
                Destroy(gameObject);
        }

        // The head runs the path from 0 to 1 (and on, under the ground), the body trailing it.
        private void Pose()
        {
            float u = age / seconds;
            float spacing = BodyShare / Segments;
            for (int i = 0; i < Segments; i++)
                Place(body[i], u - (i + 1) * spacing);
            Place(head, u);
            Vector3 ahead = At(u + 0.01f) - At(u);
            if (ahead.sqrMagnitude > 1e-6f)
                head.rotation = Quaternion.LookRotation(ahead.normalized, Vector3.up);

            if (!burst && u > 0f)
            {
                burst = true;
                SkillEffects.Shockwave(from, height * 0.25f, Dirt, 0.4f);
                CameraSystem.CameraFollow.Shake(0.25f, 0.3f);
            }
            if (!dived && u >= 1f)
            {
                dived = true;
                SkillEffects.Shockwave(to, height * 0.25f, Dirt, 0.4f);
                CameraSystem.CameraFollow.Shake(0.25f, 0.3f);
            }
        }

        private void Place(Transform t, float u)
        {
            bool shown = u > 0f && u < 1f;
            t.gameObject.SetActive(shown);
            if (shown)
                t.position = At(u);
        }

        private Vector3 At(float u)
        {
            return Vector3.Lerp(from, to, u) + Vector3.up * height * 4f * u * (1f - u);
        }

        private static void Part(Transform parent, Color color, Vector3 position, Vector3 scale)
        {
            GameObject go = RuntimePrimitives.Create(PrimitiveType.Sphere, parent, color);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
        }
    }
}
