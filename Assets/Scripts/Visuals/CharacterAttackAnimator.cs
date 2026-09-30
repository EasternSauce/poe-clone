using System;
using UnityEngine;
using PoeClone.Inventory;

namespace PoeClone.Visuals
{
    /// <summary>
    /// Procedural attack swing for the same blocky pivot rig <see cref="CharacterWalkAnimator"/>
    /// animates. Drives the weapon (right) arm and its elbow through a windup/strike/recover
    /// arc shaped by the equipped weapon's <see cref="WeaponType"/>, so every weapon type reads
    /// as a distinct attack without needing hand-authored animation clips.
    /// </summary>
    public class CharacterAttackAnimator : MonoBehaviour
    {
        [Tooltip("The shoulder pivot that swings the weapon (e.g. ArmR). Auto-found by name if left empty.")]
        [SerializeField] private Transform weaponArm;

        [Tooltip("The elbow pivot beneath the weapon arm (e.g. Elbow). Auto-found by name if left empty.")]
        [SerializeField] private Transform weaponElbow;

        private struct Pose
        {
            public float ArmPitch; // X: forward(+)/back(-) swing
            public float ArmYaw;   // Y: sideways sweep
            public float ArmRoll;  // Z: twist
            public float ElbowBend;

            public Pose(float pitch, float yaw, float roll, float elbow)
            {
                ArmPitch = pitch;
                ArmYaw = yaw;
                ArmRoll = roll;
                ElbowBend = elbow;
            }
        }

        private class AttackProfile
        {
            public float Duration;
            public float StrikeTime; // fraction of Duration where the hit lands
            public Pose Rest;
            public Pose Windup;
            public Pose Strike;
            public float BaseAttacksPerSecond;
        }

        private static readonly Pose ArmRest = new Pose(0f, 0f, 0f, 0f);

        private static readonly AttackProfile UnarmedProfile = new AttackProfile
        {
            Duration = 0.35f,
            StrikeTime = 0.4f,
            Rest = ArmRest,
            Windup = new Pose(-25f, -10f, 0f, 110f),
            Strike = new Pose(95f, 5f, 0f, 15f),
            BaseAttacksPerSecond = 1.8f
        };

        private static readonly AttackProfile SwordProfile = new AttackProfile
        {
            Duration = 0.55f,
            StrikeTime = 0.45f,
            Rest = ArmRest,
            Windup = new Pose(-60f, -35f, -20f, 60f),
            Strike = new Pose(70f, 45f, 25f, 35f),
            BaseAttacksPerSecond = 1.2f
        };

        private AttackProfile activeProfile;
        private float timer;
        private bool strikeFired;

        public bool IsAttacking { get; private set; }

        /// <summary>Fires once per swing, at the moment the weapon reaches its target.</summary>
        public event Action StrikeFrame;

        private void Awake()
        {
            if (weaponArm == null)
                weaponArm = FindDescendant(transform, "ArmR");

            if (weaponElbow == null && weaponArm != null)
                weaponElbow = FindDescendant(weaponArm, "Elbow");
        }

        /// <summary>Base attacks-per-second for a weapon type, before the AttackSpeed stat is applied.</summary>
        public static float BaseAttackSpeed(WeaponType weaponType)
        {
            return ProfileFor(weaponType).BaseAttacksPerSecond;
        }

        public void PlayAttack(WeaponType weaponType)
        {
            activeProfile = ProfileFor(weaponType);
            timer = 0f;
            strikeFired = false;
            IsAttacking = true;
        }

        private static AttackProfile ProfileFor(WeaponType weaponType)
        {
            switch (weaponType)
            {
                case WeaponType.Sword: return SwordProfile;
                default: return UnarmedProfile;
            }
        }

        private void Update()
        {
            if (!IsAttacking)
                return;

            timer += Time.deltaTime;
            float f = Mathf.Clamp01(timer / activeProfile.Duration);

            Pose pose;
            if (f < activeProfile.StrikeTime)
            {
                float t = activeProfile.StrikeTime > 0f ? f / activeProfile.StrikeTime : 1f;
                pose = Lerp(activeProfile.Rest, activeProfile.Windup, EaseOut(t));
            }
            else
            {
                float recoverSpan = 1f - activeProfile.StrikeTime;
                float t = recoverSpan > 0f ? (f - activeProfile.StrikeTime) / recoverSpan : 1f;
                pose = Lerp(activeProfile.Strike, activeProfile.Rest, t);

                if (!strikeFired)
                {
                    strikeFired = true;
                    StrikeFrame?.Invoke();
                }
            }

            Apply(pose);

            if (f >= 1f)
                IsAttacking = false;
        }

        private static float EaseOut(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }

        private static Pose Lerp(Pose a, Pose b, float t)
        {
            return new Pose(
                Mathf.LerpAngle(a.ArmPitch, b.ArmPitch, t),
                Mathf.LerpAngle(a.ArmYaw, b.ArmYaw, t),
                Mathf.LerpAngle(a.ArmRoll, b.ArmRoll, t),
                Mathf.Lerp(a.ElbowBend, b.ElbowBend, t));
        }

        private void Apply(Pose pose)
        {
            if (weaponArm != null)
                weaponArm.localRotation = Quaternion.Euler(pose.ArmPitch, pose.ArmYaw, pose.ArmRoll);

            if (weaponElbow != null)
                weaponElbow.localRotation = Quaternion.Euler(-pose.ElbowBend, 0f, 0f);
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
