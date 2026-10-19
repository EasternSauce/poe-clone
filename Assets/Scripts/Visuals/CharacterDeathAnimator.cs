using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.Visuals
{
    /// <summary>Shared face-first death: knees give way, hands brace, then the body lands prone.
    /// Only the visual model moves; the gameplay and replication root stays at the death position.</summary>
    public class CharacterDeathAnimator : MonoBehaviour
    {
        [SerializeField] private float duration = 1.15f;

        private struct Joint
        {
            public Transform Pivot;
            public Quaternion From, Buckle, Brace, Land, Rest;
            public Vector3 Position;
        }

        private readonly List<Joint> joints = new List<Joint>();
        private Vector3 modelPosition;
        private Quaternion modelRotation;
        private float timer;
        private bool captured;

        public static void PlayOn(Transform root, bool instant = false)
        {
            Transform model = root.Find("Model");
            if (model == null) return;
            var death = model.GetComponent<CharacterDeathAnimator>();
            if (death != null && death.captured) return;
            var walk = model.GetComponent<CharacterWalkAnimator>();
            var attack = model.GetComponent<CharacterAttackAnimator>();
            if (walk != null) walk.enabled = false;
            if (attack != null) attack.enabled = false;
            if (death == null) death = model.gameObject.AddComponent<CharacterDeathAnimator>();
            death.PlayDeath(instant);
        }

        public static void ResetOn(Transform root)
        {
            Transform model = root.Find("Model");
            if (model == null) return;
            var death = model.GetComponent<CharacterDeathAnimator>();
            if (death != null && death.captured)
            {
                death.enabled = false;
                model.SetLocalPositionAndRotation(death.modelPosition, death.modelRotation);
                foreach (var joint in death.joints)
                {
                    if (joint.Pivot == null) continue;
                    joint.Pivot.localPosition = joint.Position;
                    joint.Pivot.localRotation = joint.From;
                }
                death.captured = false;
            }
            var walk = model.GetComponent<CharacterWalkAnimator>();
            if (walk != null)
            {
                walk.enabled = true;
                walk.ResetAnimatorState();
            }
            var attack = model.GetComponent<CharacterAttackAnimator>();
            if (attack != null) attack.enabled = true;
        }

        public void PlayDeath(bool instant = false)
        {
            if (captured) return;
            modelPosition = transform.localPosition;
            modelRotation = transform.localRotation;
            captured = true;
            joints.Clear();
            // Pitch bends the hip forwards, the knee backwards and the elbow forwards.
            Add("LegL", new Vector3(-32, 0, -4), new Vector3(-48, 0, -6), new Vector3(-4, 0, -8), new Vector3(0, 0, -7));
            Add("LegR", new Vector3(-20, 0, 4), new Vector3(-35, 0, 8), new Vector3(-7, 0, 9), new Vector3(-3, 0, 9));
            Add("LegL/Knee", new Vector3(66, 0, 0), new Vector3(100, 0, 0), new Vector3(28, 0, 0), new Vector3(12, 0, 0));
            Add("LegR/Knee", new Vector3(48, 0, 0), new Vector3(82, 0, 0), new Vector3(35, 0, 0), new Vector3(18, 0, 0));
            Add("UpperBody", new Vector3(22, 0, 0), new Vector3(28, 0, -3), new Vector3(2, 0, 0), Vector3.zero);
            Add("ArmL", new Vector3(-38, 0, -20), new Vector3(-88, 0, -24), new Vector3(-12, 0, -42), new Vector3(-5, 0, -45));
            Add("ArmR", new Vector3(-28, 0, 18), new Vector3(-82, 0, 28), new Vector3(-10, 0, 46), new Vector3(-4, 0, 48));
            Add("ArmL/Elbow", new Vector3(-45, 0, 0), new Vector3(-30, 0, 0), new Vector3(-20, 0, 0), new Vector3(-12, 0, 0));
            Add("ArmR/Elbow", new Vector3(-38, 0, 0), new Vector3(-24, 0, 0), new Vector3(-24, 0, 0), new Vector3(-14, 0, 0));
            // Undo any planted-foot pose so the ankles do not stay flexed after the fall.
            Add("LegL/Knee/Ankle", Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero);
            Add("LegR/Knee/Ankle", Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero);
            timer = instant ? duration : 0f;
            enabled = true;
            Pose(instant ? 1f : 0f);
            if (instant) enabled = false;
        }

        private void Add(string path, Vector3 buckle, Vector3 brace, Vector3 land, Vector3 rest)
        {
            string[] parts = path.Split('/');
            Transform pivot = FindDescendant(transform, parts[0]);
            for (int i = 1; i < parts.Length && pivot != null; i++) pivot = pivot.Find(parts[i]);
            if (pivot == null) return;
            joints.Add(new Joint { Pivot = pivot, Position = pivot.localPosition, From = pivot.localRotation,
                Buckle = Quaternion.Euler(buckle), Brace = Quaternion.Euler(brace),
                Land = Quaternion.Euler(land), Rest = Quaternion.Euler(rest) });
        }

        private void Update()
        {
            timer += Time.deltaTime;
            float f = Mathf.Clamp01(timer / Mathf.Max(0.01f, duration));
            Pose(f);
            if (f >= 1f) enabled = false;
        }

        private void Pose(float f)
        {
            // The feet-origin model drops while kneeling, then rotates around the hips rather
            // than sinking the whole actor. The last phase releases the brace after impact.
            float pitch;
            Vector3 offset;
            if (f < 0.28f)
            {
                float t = Ease(f / 0.28f);
                pitch = Mathf.Lerp(0, 12, t);
                offset = Vector3.Lerp(Vector3.zero, new Vector3(0, -0.12f, 0.12f), t);
            }
            else if (f < 0.72f)
            {
                float t = (f - 0.28f) / 0.44f;
                t *= t; // Accelerates into the ground instead of easing slowly into contact.
                pitch = Mathf.Lerp(12, 90, t);
                offset = Vector3.Lerp(new Vector3(0, -0.12f, 0.12f), new Vector3(0, 0.27f, 0.32f), t);
            }
            else
            {
                float t = Ease((f - 0.72f) / 0.28f);
                pitch = 90;
                offset = Vector3.Lerp(new Vector3(0, 0.27f, 0.32f), new Vector3(0, 0.24f, 0.39f), t);
                offset.y += Mathf.Sin(t * Mathf.PI) * 0.035f;
            }
            transform.localPosition = modelPosition + modelRotation * offset;
            transform.localRotation = modelRotation * Quaternion.Euler(pitch, 0, 0);
            foreach (var j in joints)
            {
                if (j.Pivot == null) continue;
                if (f < 0.28f) j.Pivot.localRotation = Quaternion.Slerp(j.From, j.Buckle, Ease(f / 0.28f));
                else if (f < 0.48f) j.Pivot.localRotation = Quaternion.Slerp(j.Buckle, j.Brace, Ease((f - 0.28f) / 0.20f));
                else if (f < 0.72f) j.Pivot.localRotation = Quaternion.Slerp(j.Brace, j.Land, Ease((f - 0.48f) / 0.24f));
                else j.Pivot.localRotation = Quaternion.Slerp(j.Land, j.Rest, Ease((f - 0.72f) / 0.28f));
            }
        }

        private static float Ease(float t) => Mathf.SmoothStep(0, 1, t);

        private static Transform FindDescendant(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && t.name == name) return t;
            return null;
        }
    }
}
