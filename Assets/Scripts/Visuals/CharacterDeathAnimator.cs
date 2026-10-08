using UnityEngine;

namespace PoeClone.Visuals
{
    /// <summary>
    /// Procedural death collapse for the same blocky pivot rig <see cref="CharacterWalkAnimator"/>
    /// animates: legs straighten, the upper body folds forward and the arms go limp and splay
    /// outward, settling into a heap pose over <see cref="PlayDeath"/>'s duration. Each joint
    /// eases from whatever pose it was actually in when death began (rather than a hardcoded
    /// start), so it never pops even if death lands mid-swing or mid-stride. Purely a limb pose;
    /// the whole-body topple/sink is handled separately (see EnemyHealth.CollapseAndRemove).
    /// </summary>
    public class CharacterDeathAnimator : MonoBehaviour
    {
        [SerializeField] private float duration = 0.7f;

        private Transform leftLeg;
        private Transform rightLeg;
        private Transform leftKnee;
        private Transform rightKnee;
        private Transform upperBody;
        private Transform leftArm;
        private Transform rightArm;
        private Transform leftElbow;
        private Transform rightElbow;

        private struct Joint
        {
            public Transform Pivot;
            public Quaternion From;
            public Quaternion To;
        }

        private Joint[] joints;
        private float timer;
        private bool playing;

        /// <summary>
        /// Starts the collapse on whatever character has this "Model" child, after disabling the
        /// walk/attack animators (both write the same pivots every frame and would otherwise
        /// immediately overwrite the collapse pose). Shared by EnemyHealth and PlayerStats so a
        /// death always looks the same either way. No-ops if there's no "Model" child.
        /// </summary>
        /// <param name="foldUpperBody">
        /// The player folds its torso forward while keeping its legs straight and visible.
        /// Enemies pass false because EnemyHealth.Collapse topples the whole rig instead.
        /// </param>
        public static void PlayOn(Transform root, bool foldUpperBody = true)
        {
            Transform model = root.Find("Model");
            if (model == null)
                return;

            CharacterWalkAnimator walkAnimator = model.GetComponent<CharacterWalkAnimator>();
            if (walkAnimator != null)
                walkAnimator.enabled = false;

            CharacterAttackAnimator attackAnimator = model.GetComponent<CharacterAttackAnimator>();
            if (attackAnimator != null)
                attackAnimator.enabled = false;

            CharacterDeathAnimator deathAnimator = model.GetComponent<CharacterDeathAnimator>();
            if (deathAnimator == null)
                deathAnimator = model.gameObject.AddComponent<CharacterDeathAnimator>();

            deathAnimator.PlayDeath(foldUpperBody);
        }

        /// <summary>
        /// Undoes <see cref="PlayOn"/>: stops the collapse, re-enables the walk/attack animators,
        /// and snaps the rig back to its resting pose via CharacterWalkAnimator.ResetAnimatorState
        /// (the same reset used after a teleport). Used to bring the player back on revive.
        /// </summary>
        public static void ResetOn(Transform root)
        {
            Transform model = root.Find("Model");
            if (model == null)
                return;

            CharacterDeathAnimator deathAnimator = model.GetComponent<CharacterDeathAnimator>();
            if (deathAnimator != null)
                deathAnimator.enabled = false;

            CharacterWalkAnimator walkAnimator = model.GetComponent<CharacterWalkAnimator>();
            if (walkAnimator != null)
            {
                walkAnimator.enabled = true;
                walkAnimator.ResetAnimatorState();
            }

            CharacterAttackAnimator attackAnimator = model.GetComponent<CharacterAttackAnimator>();
            if (attackAnimator != null)
                attackAnimator.enabled = true;
        }

        private void Awake()
        {
            leftLeg = FindDescendant(transform, "LegL");
            rightLeg = FindDescendant(transform, "LegR");
            leftKnee = leftLeg != null ? FindDescendant(leftLeg, "Knee") : null;
            rightKnee = rightLeg != null ? FindDescendant(rightLeg, "Knee") : null;
            upperBody = FindDescendant(transform, "UpperBody");
            leftArm = FindDescendant(transform, "ArmL");
            rightArm = FindDescendant(transform, "ArmR");
            leftElbow = leftArm != null ? FindDescendant(leftArm, "Elbow") : null;
            rightElbow = rightArm != null ? FindDescendant(rightArm, "Elbow") : null;
        }

        public void PlayDeath(bool foldUpperBody = true)
        {
            float upperBodyPitch = foldUpperBody ? 85f : 0f;

            joints = new[]
            {
                MakeJoint(leftLeg, 0f, 0f),
                MakeJoint(rightLeg, 0f, 0f),
                MakeJoint(leftKnee, 0f, 0f),
                MakeJoint(rightKnee, 0f, 0f),
                MakeJoint(upperBody, upperBodyPitch, 0f),
                MakeJoint(leftArm, -15f, -72f),
                MakeJoint(rightArm, -15f, 72f),
                MakeJoint(leftElbow, -32f, 0f),
                MakeJoint(rightElbow, -32f, 0f),
            };

            timer = 0f;
            playing = true;
            enabled = true;
        }

        private static Joint MakeJoint(Transform pivot, float pitch, float yaw)
        {
            return new Joint
            {
                Pivot = pivot,
                From = pivot != null ? pivot.localRotation : Quaternion.identity,
                To = Quaternion.Euler(pitch, yaw, 0f)
            };
        }

        private void Update()
        {
            if (!playing)
                return;

            timer += Time.deltaTime;
            float f = Mathf.Clamp01(timer / duration);
            float eased = 1f - (1f - f) * (1f - f);

            for (int i = 0; i < joints.Length; i++)
            {
                if (joints[i].Pivot != null)
                    joints[i].Pivot.localRotation = Quaternion.Slerp(joints[i].From, joints[i].To, eased);
            }

            if (f >= 1f)
                playing = false;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != root && t.name == name)
                    return t;
            }

            return null;
        }
    }
}
