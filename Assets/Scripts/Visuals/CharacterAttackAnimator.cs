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
            public float ArmPitch; // X: forward/at-target(-)  back(+), relative to this rig's rest pose
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

            public static Pose operator +(Pose a, Pose b)
            {
                return new Pose(a.ArmPitch + b.ArmPitch, a.ArmYaw + b.ArmYaw, a.ArmRoll + b.ArmRoll, a.ElbowBend + b.ElbowBend);
            }
        }

        // Windup/Strike are stored as offsets from the character's own rest pose (see ArmRestAngle
        // on CharacterWalkAnimator), not absolute angles. That way the same profile reads correctly
        // whether rest is 0 deg (player, arms hanging) or -65 deg (zombie, arms already raised
        // forward). Pitch offsets are negative to swing forward/at-target and positive to pull back
        // -- proven by the zombie's rest pose, which must use negative pitch to reach forward.
        private class AttackProfile
        {
            public float Duration;
            public float StrikeTime; // fraction of Duration where the hit lands
            public Pose WindupOffset;
            public Pose StrikeOffset;
            public float BaseAttacksPerSecond;
            public float Range;
        }

        private static readonly AttackProfile UnarmedProfile = new AttackProfile
        {
            Duration = 0.35f,
            StrikeTime = 0.4f,
            WindupOffset = new Pose(25f, -10f, 0f, 110f),
            StrikeOffset = new Pose(-75f, 5f, 0f, 15f),
            BaseAttacksPerSecond = 1.8f,
            Range = 1.6f
        };

        // A diagonal cross-body cut: yaw (sideways sweep) dominates pitch (forward/back) so the
        // blade reads as slashing from high on one shoulder down to the opposite hip, not
        // chopping straight down like an overhead axe swing. Roll twists the blade through the cut.
        private static readonly AttackProfile SwordProfile = new AttackProfile
        {
            Duration = 0.55f,
            StrikeTime = 0.45f,
            WindupOffset = new Pose(30f, -55f, -40f, 65f),
            StrikeOffset = new Pose(-35f, 55f, 45f, 30f),
            BaseAttacksPerSecond = 1.2f,
            Range = 2.2f
        };

        // A straight thrust: pitch dominates with almost no yaw/roll, so the arm punches the
        // blade forward and snaps back rather than sweeping across the body. Shares the slash's
        // pace and reach (see PickProfile) so it's a pure visual variant, not a balance change.
        private static readonly AttackProfile SwordStabProfile = new AttackProfile
        {
            Duration = 0.4f,
            StrikeTime = 0.55f,
            WindupOffset = new Pose(45f, -8f, -5f, 95f),
            StrikeOffset = new Pose(-80f, 4f, 0f, 8f),
            BaseAttacksPerSecond = SwordProfile.BaseAttacksPerSecond,
            Range = SwordProfile.Range
        };

        // Mirror of SwordProfile (yaw/roll negated): the same diagonal cut from the opposite
        // shoulder, so consecutive swings don't always cross the body the same way.
        private static readonly AttackProfile SwordSlashMirroredProfile = new AttackProfile
        {
            Duration = SwordProfile.Duration,
            StrikeTime = SwordProfile.StrikeTime,
            WindupOffset = new Pose(SwordProfile.WindupOffset.ArmPitch, -SwordProfile.WindupOffset.ArmYaw, -SwordProfile.WindupOffset.ArmRoll, SwordProfile.WindupOffset.ElbowBend),
            StrikeOffset = new Pose(SwordProfile.StrikeOffset.ArmPitch, -SwordProfile.StrikeOffset.ArmYaw, -SwordProfile.StrikeOffset.ArmRoll, SwordProfile.StrikeOffset.ElbowBend),
            BaseAttacksPerSecond = SwordProfile.BaseAttacksPerSecond,
            Range = SwordProfile.Range
        };

        // Equal odds of a stab, a slash from the right shoulder, or the mirrored slash from the
        // left shoulder, so no variant reads as the "default" attack.
        private const float SwordStabChance = 1f / 3f;
        private const float SwordMirrorChance = 1f / 3f;

        // Tuned for a rest pose that's already raised forward (e.g. the zombie's -65 deg stance),
        // so it doesn't need nearly as much swing as a weapon profile to read as a forward strike.
        private static readonly AttackProfile ClawProfile = new AttackProfile
        {
            Duration = 0.5f,
            StrikeTime = 0.45f,
            WindupOffset = new Pose(15f, -20f, 0f, 70f),
            StrikeOffset = new Pose(-40f, 15f, 0f, 20f),
            BaseAttacksPerSecond = 1.0f,
            Range = 1.8f
        };

        // Stable ids for every profile, so a spectator replica can replay exactly the swing the player
        // made (including which random sword variant) - see PlayReplicated.
        private static readonly AttackProfile[] ProfilesById =
        {
            UnarmedProfile,
            SwordProfile,
            SwordStabProfile,
            SwordSlashMirroredProfile,
            ClawProfile
        };

        private AttackProfile activeProfile;
        private Pose rest;
        private float timer;
        private bool strikeFired;

        public bool IsAttacking { get; private set; }

        /// <summary>Swings started so far. Only ever increases, so an observer sampling it periodically can't miss a swing.</summary>
        public int AttackCount { get; private set; }

        /// <summary>Id of the most recently started swing's profile, for <see cref="PlayReplicated"/>.</summary>
        public int ProfileId { get; private set; }

        /// <summary>Fires once per swing, at the moment the weapon reaches its target.</summary>
        public event Action StrikeFrame;

        /// <summary>Fires when a swing is cut off before its strike (a stagger), so the owner can refund the swing's cooldown.</summary>
        public event Action AttackCancelled;

        private void Awake()
        {
            if (weaponArm == null)
                weaponArm = FindDescendant(transform, "ArmR");

            if (weaponElbow == null && weaponArm != null)
                weaponElbow = FindDescendant(weaponArm, "Elbow");

            CharacterWalkAnimator walkAnimator = GetComponent<CharacterWalkAnimator>();
            float restPitch = walkAnimator != null ? walkAnimator.ArmRestAngle : 0f;
            rest = new Pose(restPitch, 0f, 0f, 0f);
        }

        /// <summary>Base attacks-per-second for a weapon type, before the AttackSpeed stat is applied.</summary>
        public static float BaseAttackSpeed(WeaponType weaponType)
        {
            return ProfileFor(weaponType).BaseAttacksPerSecond;
        }

        /// <summary>How far this weapon type's swing reaches, for both the melee hit check and aim-highlight queries.</summary>
        public static float AttackRange(WeaponType weaponType)
        {
            return ProfileFor(weaponType).Range;
        }

        public void PlayAttack(WeaponType weaponType)
        {
            Play(PickProfile(weaponType));
        }

        // Picks which animation actually plays. Independent of ProfileFor, which stays the
        // canonical source for pacing/range (BaseAttackSpeed, AttackRange) so those numbers don't
        // flicker between variants.
        private static AttackProfile PickProfile(WeaponType weaponType)
        {
            if (weaponType != WeaponType.Sword)
                return ProfileFor(weaponType);

            float roll = UnityEngine.Random.value;
            if (roll < SwordStabChance)
                return SwordStabProfile;
            if (roll < SwordStabChance + SwordMirrorChance)
                return SwordSlashMirroredProfile;

            return SwordProfile;
        }

        /// <summary>Enemy claw swipe: a separate profile from player weapons since it's tuned for a different rest pose.</summary>
        public void PlayClawAttack()
        {
            Play(ClawProfile);
        }

        /// <summary>Immediately cancels an in-progress swing and snaps back to rest. Used when staggered.</summary>
        public void CancelAttack()
        {
            if (!IsAttacking)
                return;

            IsAttacking = false;
            Apply(rest);

            if (!strikeFired)
                AttackCancelled?.Invoke();
        }

        /// <summary>Plays the swing with the given <see cref="ProfileId"/> (spectator replay). Unknown ids are ignored.</summary>
        public void PlayReplicated(int profileId)
        {
            if (profileId < 0 || profileId >= ProfilesById.Length)
                return;
            Play(ProfilesById[profileId]);
        }

        private void Play(AttackProfile profile)
        {
            AttackCount++;
            ProfileId = Array.IndexOf(ProfilesById, profile);
            activeProfile = profile;
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

            Pose windup = rest + activeProfile.WindupOffset;
            Pose strike = rest + activeProfile.StrikeOffset;

            Pose pose;
            if (f < activeProfile.StrikeTime)
            {
                float t = activeProfile.StrikeTime > 0f ? f / activeProfile.StrikeTime : 1f;
                pose = Lerp(rest, windup, EaseOut(t));
            }
            else
            {
                float recoverSpan = 1f - activeProfile.StrikeTime;
                float t = recoverSpan > 0f ? (f - activeProfile.StrikeTime) / recoverSpan : 1f;
                pose = Lerp(strike, rest, t);

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
