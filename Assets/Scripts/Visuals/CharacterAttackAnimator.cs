using System;
using UnityEngine;
using PoeClone.Inventory;
using PoeClone.Skills;

namespace PoeClone.Visuals
{
    /// <summary>
    /// Procedural attack swing for the same blocky pivot rig <see cref="CharacterWalkAnimator"/>
    /// animates. Drives the weapon (right) arm and its elbow through a windup/strike/recover
    /// arc shaped by the equipped weapon's <see cref="WeaponType"/>, so every weapon type reads
    /// as a distinct attack without needing hand-authored animation clips. Two-handed attacks
    /// (the bow: left arm holds it out at the target while the right draws and releases) also
    /// drive the off (left) arm.
    /// </summary>
    public class CharacterAttackAnimator : MonoBehaviour
    {
        [Tooltip("The shoulder pivot that swings the weapon (e.g. ArmR). Auto-found by name if left empty.")]
        [SerializeField] private Transform weaponArm;

        [Tooltip("The elbow pivot beneath the weapon arm (e.g. Elbow). Auto-found by name if left empty.")]
        [SerializeField] private Transform weaponElbow;

        [Tooltip("The other shoulder pivot (e.g. ArmL), for two-handed attacks like the bow. Auto-found by name if left empty.")]
        [SerializeField] private Transform offArm;

        [Tooltip("The elbow pivot beneath the off arm. Auto-found by name if left empty.")]
        [SerializeField] private Transform offElbow;

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
            public float SwingStart; // optional fraction where the windup turns into a visible strike
            public Pose WindupOffset;
            public Pose StrikeOffset;
            public float BaseAttacksPerSecond;
            public float Range;

            // Absolute: the poses are real angles, not offsets from rest. For a stance that must
            // look the same on every rig (aiming a bow is "arm straight at the target" whether
            // the character's arms rest hanging or raised).
            public bool Absolute;

            // Two-handed: the off arm has its own windup/strike poses.
            public bool UsesOffArm;
            public Pose OffWindup;
            public Pose OffStrike;

            // Both hands on one weapon: the off arm copies the weapon arm, turned in so the hands
            // meet on the grip (see GripPose).
            public bool TwoHandGrip;

            // Half-angle of the cone the blow reaches (0: the attacker's own default).
            public float ConeHalfAngle;

            // An absolute (bow) pose's lean: how far the body tips over the draw (forward +).
            public float Lean;
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

        // A broad axe cut: draw the head across the shoulder, then sweep its edge through the target.
        private static readonly AttackProfile AxeProfile = new AttackProfile
        {
            Duration = 0.65f,
            StrikeTime = 0.5f,
            SwingStart = 0.27f,
            WindupOffset = new Pose(35f, -70f, -35f, 60f),
            StrikeOffset = new Pose(-40f, 65f, 35f, 20f),
            BaseAttacksPerSecond = 1.0f,
            Range = 2.3f
        };

        // A heavy, low diagonal smash: the slowest weapon.
        private static readonly AttackProfile MaceProfile = new AttackProfile
        {
            Duration = 0.7f,
            StrikeTime = 0.55f,
            // Drive the hammer head forward at the strike instead of sweeping its side across.
            WindupOffset = new Pose(70f, 0f, 0f, 60f),
            StrikeOffset = new Pose(-10f, 0f, 0f, 20f),
            BaseAttacksPerSecond = 0.9f,
            Range = 2.2f
        };

        // Quick, short jabs: the fastest weapon, with the least reach.
        private static readonly AttackProfile DaggerProfile = new AttackProfile
        {
            Duration = 0.28f,
            StrikeTime = 0.5f,
            WindupOffset = new Pose(35f, -6f, 0f, 90f),
            StrikeOffset = new Pose(-72f, 3f, 0f, 10f),
            BaseAttacksPerSecond = 2.0f,
            Range = 1.7f
        };

        // The great weapons: both hands on the grip, slower and heavier than their one-handed
        // cousins, with more reach. The greatsword sweeps wide and flat across the front; the
        // greataxe chops down from high over the head; the maul is hauled up behind the head and
        // slammed into the ground (its strike is late: the whole body goes into it).
        // Elbows stay nearly straight: the weapon models already point forward of the hand, so a
        // bent elbow would tip the head down behind the back in the wind-up.
        private static readonly AttackProfile GreatswordProfile = new AttackProfile
        {
            Duration = 0.8f,
            StrikeTime = 0.5f,
            WindupOffset = new Pose(-35f, -95f, -30f, 0f),
            StrikeOffset = new Pose(-45f, 70f, 35f, 0f),
            BaseAttacksPerSecond = 0.85f,
            Range = 2.9f,
            TwoHandGrip = true,
            ConeHalfAngle = 70f
        };

        private static readonly AttackProfile GreatswordBackhandProfile = new AttackProfile
        {
            Duration = GreatswordProfile.Duration,
            StrikeTime = GreatswordProfile.StrikeTime,
            WindupOffset = new Pose(-35f, 80f, 30f, 0f),
            StrikeOffset = new Pose(-45f, -70f, -35f, 0f),
            BaseAttacksPerSecond = GreatswordProfile.BaseAttacksPerSecond,
            Range = GreatswordProfile.Range,
            TwoHandGrip = true,
            ConeHalfAngle = GreatswordProfile.ConeHalfAngle
        };

        private static readonly AttackProfile GreataxeProfile = new AttackProfile
        {
            Duration = 0.85f,
            StrikeTime = 0.55f,
            SwingStart = 0.32f,
            WindupOffset = new Pose(-85f, -75f, -30f, 10f),
            StrikeOffset = new Pose(-35f, 65f, 25f, 0f),
            BaseAttacksPerSecond = 0.8f,
            Range = 2.8f,
            TwoHandGrip = true,
            ConeHalfAngle = 45f
        };

        private static readonly AttackProfile MaulProfile = new AttackProfile
        {
            Duration = 0.95f,
            StrikeTime = 0.62f,
            WindupOffset = new Pose(-160f, 0f, 0f, 0f),
            StrikeOffset = new Pose(-2f, 0f, 0f, 0f),
            BaseAttacksPerSecond = 0.72f,
            Range = 2.7f,
            TwoHandGrip = true,
            ConeHalfAngle = 55f
        };

        // Skill attacks have their own readable silhouettes and slower release frames, separate
        // from the ordinary weapon swing. Skill timing below matches PlayerSkills' windups.
        private static readonly AttackProfile CleaveSkillProfile = new AttackProfile
        {
            Duration = 0.98f, StrikeTime = 0.93f,
            WindupOffset = new Pose(55f, -105f, -25f, 65f),
            StrikeOffset = new Pose(-60f, 100f, 20f, 0f),
            BaseAttacksPerSecond = 1f, Range = 3f
        };

        private static readonly AttackProfile CleaveGreatSkillProfile = new AttackProfile
        {
            Duration = 0.98f, StrikeTime = 0.93f, TwoHandGrip = true,
            WindupOffset = new Pose(-55f, -105f, -25f, 0f),
            StrikeOffset = new Pose(-28f, 100f, 20f, 0f),
            BaseAttacksPerSecond = 0.8f, Range = 3.3f, ConeHalfAngle = 75f
        };

        private static readonly AttackProfile PulverizeMaceProfile = new AttackProfile
        {
            Duration = 0.9f, StrikeTime = 0.8f, SwingStart = 0.58f,
            WindupOffset = new Pose(-145f, -4f, -8f, 25f),
            StrikeOffset = new Pose(-8f, 2f, 0f, 0f),
            BaseAttacksPerSecond = 0.8f, Range = 2.5f
        };

        private static readonly AttackProfile PulverizeMaulProfile = new AttackProfile
        {
            Duration = 0.9f, StrikeTime = 0.8f, SwingStart = 0.58f, TwoHandGrip = true,
            WindupOffset = new Pose(-165f, 0f, 0f, 0f),
            StrikeOffset = new Pose(-2f, 0f, 0f, 0f),
            BaseAttacksPerSecond = 0.72f, Range = 2.7f
        };

        private static readonly AttackProfile ReapingAxeProfile = new AttackProfile
        {
            Duration = 0.82f, StrikeTime = 0.71f, SwingStart = 0.46f,
            WindupOffset = new Pose(22f, -78f, -35f, 60f),
            StrikeOffset = new Pose(-28f, 82f, 35f, 20f),
            BaseAttacksPerSecond = 0.85f, Range = 3f
        };

        private static readonly AttackProfile ReapingGreataxeProfile = new AttackProfile
        {
            Duration = 0.82f, StrikeTime = 0.71f, SwingStart = 0.46f, TwoHandGrip = true,
            WindupOffset = new Pose(-45f, -85f, -22f, 0f),
            StrikeOffset = new Pose(-35f, 88f, 22f, 0f),
            BaseAttacksPerSecond = 0.8f, Range = 3.2f, ConeHalfAngle = 72f
        };

        private static readonly AttackProfile LungingSwordProfile = new AttackProfile
        {
            Duration = 0.75f, StrikeTime = 0.67f,
            WindupOffset = new Pose(55f, -7f, -4f, 100f),
            StrikeOffset = new Pose(-105f, 2f, 0f, -43f),
            BaseAttacksPerSecond = 0.9f, Range = 3.2f
        };

        private static readonly AttackProfile LungingGreatswordProfile = new AttackProfile
        {
            Duration = 0.75f, StrikeTime = 0.67f, TwoHandGrip = true,
            WindupOffset = new Pose(-100f, -12f, -5f, 0f),
            StrikeOffset = new Pose(-112f, 4f, 0f, 0f),
            BaseAttacksPerSecond = 0.82f, Range = 3.6f
        };

        private static readonly AttackProfile FangStrikeProfile = new AttackProfile
        {
            Duration = 0.3f, StrikeTime = 0.4f,
            WindupOffset = new Pose(22f, -2f, -2f, 65f),
            StrikeOffset = new Pose(-105f, 1f, 0f, -43f),
            BaseAttacksPerSecond = 2.2f, Range = 1.9f
        };

        // Aim and loose: the off (left) arm raises the bow straight at the target, while the
        // weapon (right) arm comes up across the body and draws the string back to the chin with
        // the elbow sharply bent; at the release the drawing hand snaps back. The "strike" is the
        // moment the arrow leaves; Range is how far arrows fly.
        private static readonly AttackProfile BowProfile = new AttackProfile
        {
            Duration = 0.6f,
            StrikeTime = 0.65f,
            Absolute = true,
            WindupOffset = new Pose(-80f, -28f, 0f, 125f),
            StrikeOffset = new Pose(-78f, -12f, 0f, 95f),
            UsesOffArm = true,
            OffWindup = new Pose(-90f, -4f, 0f, 0f),
            OffStrike = new Pose(-90f, -4f, 0f, 0f),
            BaseAttacksPerSecond = 1.25f,
            Range = 14f
        };

        // Bow skills' draws (see BowStyle): each replaces the plain shot while its skill is on.
        // Split Shot sweeps the bow across the body as it looses, so the fan reads as thrown wide.
        private static readonly AttackProfile BowFanProfile = new AttackProfile
        {
            Duration = 0.6f,
            StrikeTime = 0.62f,
            Absolute = true,
            WindupOffset = new Pose(-80f, -55f, 0f, 125f),
            StrikeOffset = new Pose(-80f, 20f, 0f, 95f),
            UsesOffArm = true,
            OffWindup = new Pose(-90f, -38f, 0f, 0f),
            OffStrike = new Pose(-90f, 30f, 0f, 0f),
            BaseAttacksPerSecond = BowProfile.BaseAttacksPerSecond,
            Range = BowProfile.Range
        };

        // Rain of Arrows: both arms high, bow pointed at the sky, leaning back into the draw.
        private static readonly AttackProfile BowSkyProfile = new AttackProfile
        {
            Duration = 0.65f,
            StrikeTime = 0.62f,
            Absolute = true,
            WindupOffset = new Pose(-150f, -24f, 0f, 125f),
            StrikeOffset = new Pose(-152f, -8f, 0f, 85f),
            UsesOffArm = true,
            OffWindup = new Pose(-158f, -4f, 0f, 0f),
            OffStrike = new Pose(-160f, -4f, 0f, 0f),
            Lean = -14f,
            BaseAttacksPerSecond = BowProfile.BaseAttacksPerSecond,
            Range = BowProfile.Range
        };

        // Piercing Shot: a long, deep draw past the chin, leaning into it, then a hard snap back.
        private static readonly AttackProfile BowHeavyProfile = new AttackProfile
        {
            Duration = 0.72f,
            StrikeTime = 0.72f,
            Absolute = true,
            WindupOffset = new Pose(-84f, -40f, 0f, 150f),
            StrikeOffset = new Pose(-70f, -2f, 0f, 70f),
            UsesOffArm = true,
            OffWindup = new Pose(-92f, -6f, 0f, 0f),
            OffStrike = new Pose(-88f, -2f, 0f, 0f),
            Lean = 8f,
            BaseAttacksPerSecond = BowProfile.BaseAttacksPerSecond,
            Range = BowProfile.Range
        };

        // Burning Arrow: the bow canted on its side, drawn low and loosed with a flick upward.
        private static readonly AttackProfile BowCantedProfile = new AttackProfile
        {
            Duration = 0.6f,
            StrikeTime = 0.65f,
            Absolute = true,
            WindupOffset = new Pose(-68f, -30f, 40f, 125f),
            StrikeOffset = new Pose(-88f, -10f, 30f, 95f),
            UsesOffArm = true,
            OffWindup = new Pose(-78f, -4f, 45f, 0f),
            OffStrike = new Pose(-96f, -4f, 35f, 0f),
            BaseAttacksPerSecond = BowProfile.BaseAttacksPerSecond,
            Range = BowProfile.Range
        };

        /// <summary>The draw a bow skill replaces the plain shot with.</summary>
        public enum BowStyle
        {
            Plain,
            Fan,
            Sky,
            Heavy,
            Canted
        }

        // Creatures (see CreatureAnimator): no arms to pose, just the timing of a bite or a spit,
        // which the creature's own animator turns into a lunge.
        private static readonly AttackProfile BiteProfile = new AttackProfile
        {
            Duration = 0.6f,
            StrikeTime = 0.55f,
            BaseAttacksPerSecond = 1.0f,
            Range = 1.8f
        };

        private static readonly AttackProfile SpitProfile = new AttackProfile
        {
            Duration = 0.75f,
            StrikeTime = 0.6f,
            BaseAttacksPerSecond = 0.8f,
            Range = 9f
        };

        // Stable ids for every profile, so a spectator replica can replay exactly the swing the player
        // made (including which random sword variant) - see PlayReplicated. New profiles go at the end.
        private static readonly AttackProfile[] ProfilesById =
        {
            UnarmedProfile,
            SwordProfile,
            SwordStabProfile,
            SwordSlashMirroredProfile,
            ClawProfile,
            AxeProfile,
            MaceProfile,
            DaggerProfile,
            BowProfile,
            BiteProfile,
            SpitProfile,
            GreatswordProfile,
            GreatswordBackhandProfile,
            GreataxeProfile,
            MaulProfile,
            BowFanProfile,
            BowSkyProfile,
            BowHeavyProfile,
            BowCantedProfile,
            CleaveSkillProfile,
            CleaveGreatSkillProfile,
            PulverizeMaceProfile,
            PulverizeMaulProfile,
            ReapingAxeProfile,
            ReapingGreataxeProfile,
            LungingSwordProfile,
            LungingGreatswordProfile,
            FangStrikeProfile
        };

        /// <summary>Whether the profile with this id shoots (an arrow) rather than hits.</summary>
        public static bool IsRangedProfile(int profileId)
        {
            return profileId >= 0 && profileId < ProfilesById.Length && ProfilesById[profileId] == BowProfile;
        }

        /// <summary>Whether a weapon type shoots instead of hitting what's in reach.</summary>
        /// <summary>Seconds from the start of a swing with this weapon to its strike, at normal speed.</summary>
        public static float StrikeSeconds(WeaponType weaponType)
        {
            AttackProfile p = ProfileFor(weaponType);
            return p.Duration * p.StrikeTime;
        }

        public static bool IsRanged(WeaponType weaponType)
        {
            return weaponType == WeaponType.Bow;
        }

        private AttackProfile activeProfile;
        private Pose rest;
        private float timer;
        private bool strikeFired;

        public bool IsAttacking { get; private set; }

        /// <summary>How fast swings play (an enraged enemy swings faster). 1 is normal.</summary>
        public float PlaybackSpeed { get; set; } = 1f;

        /// <summary>How far through the current swing (0 to 1); 0 when not attacking.</summary>
        public float Progress => IsAttacking && activeProfile != null ? Mathf.Clamp01(timer / activeProfile.Duration) : 0f;

        /// <summary>The fraction of the current swing at which it strikes.</summary>
        public float StrikeFraction => activeProfile != null ? activeProfile.StrikeTime : 0.5f;

        /// <summary>
        /// The upper body's share of the swing (pitch: lean forward +, yaw: twist), and how far the
        /// body steps into it, for <see cref="CharacterWalkAnimator"/> to add to its own pose.
        /// </summary>
        public float TorsoPitch { get; private set; }
        public float TorsoYaw { get; private set; }
        public float Lunge { get; private set; }

        /// <summary>True while a two-handed attack is posing the off arm (the walk cycle leaves it alone).</summary>
        public bool DrivesOffArm => IsAttacking && activeProfile != null && (activeProfile.UsesOffArm || activeProfile.TwoHandGrip);

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

            if (offArm == null)
                offArm = FindDescendant(transform, "ArmL");

            if (offElbow == null && offArm != null)
                offElbow = FindDescendant(offArm, "Elbow");

            CharacterWalkAnimator walkAnimator = GetComponent<CharacterWalkAnimator>();
            float restPitch = walkAnimator != null ? walkAnimator.ArmRestAngle : 0f;
            rest = new Pose(restPitch, 0f, 0f, 0f);
        }

        /// <summary>Base attacks-per-second for a weapon type, before the AttackSpeed stat is applied.</summary>
        public static float BaseAttackSpeed(WeaponType weaponType)
        {
            return ProfileFor(weaponType).BaseAttacksPerSecond;
        }

        /// <summary>Half-angle of the cone this weapon type's swing reaches, or 0 for the attacker's default.</summary>
        public static float ConeHalfAngle(WeaponType weaponType)
        {
            return ProfileFor(weaponType).ConeHalfAngle;
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

        /// <summary>Plays the dedicated procedural pose for a committed melee skill.</summary>
        public void PlaySkillAttack(SkillId skill, WeaponType weaponType)
        {
            switch (skill)
            {
                case SkillId.Cleave: Play(SlotRules.IsTwoHandedMelee(weaponType) ? CleaveGreatSkillProfile : CleaveSkillProfile); break;
                case SkillId.Pulverize: Play(weaponType == WeaponType.Maul ? PulverizeMaulProfile : PulverizeMaceProfile); break;
                case SkillId.ReapingArc: Play(weaponType == WeaponType.Greataxe ? ReapingGreataxeProfile : ReapingAxeProfile); break;
                case SkillId.LungingThrust: Play(weaponType == WeaponType.Greatsword ? LungingGreatswordProfile : LungingSwordProfile); break;
                case SkillId.FangStrike: Play(FangStrikeProfile); break;
                default: Play(PickProfile(weaponType)); break;
            }
        }

        /// <summary>A bow shot drawn the way a bow skill draws it (Plain: the ordinary shot).</summary>
        public void PlayBow(BowStyle style)
        {
            switch (style)
            {
                case BowStyle.Fan: Play(BowFanProfile); break;
                case BowStyle.Sky: Play(BowSkyProfile); break;
                case BowStyle.Heavy: Play(BowHeavyProfile); break;
                case BowStyle.Canted: Play(BowCantedProfile); break;
                default: Play(BowProfile); break;
            }
        }

        // Picks which animation actually plays. Independent of ProfileFor, which stays the
        // canonical source for pacing/range (BaseAttackSpeed, AttackRange) so those numbers don't
        // flicker between variants.
        private static AttackProfile PickProfile(WeaponType weaponType)
        {
            // Big sweeps alternate sides at random, like the sword's slashes.
            if (weaponType == WeaponType.Greatsword)
                return UnityEngine.Random.value < 0.5f ? GreatswordProfile : GreatswordBackhandProfile;
            if (weaponType != WeaponType.Sword)
                return ProfileFor(weaponType);

            float roll = UnityEngine.Random.value;
            if (roll < SwordStabChance)
                return SwordStabProfile;
            if (roll < SwordStabChance + SwordMirrorChance)
                return SwordSlashMirroredProfile;

            return SwordProfile;
        }

        /// <summary>A creature's bite (or, with spit, the spit it shoots): timing only, see CreatureAnimator.</summary>
        public void PlayCreatureAttack(bool spit)
        {
            Play(spit ? SpitProfile : BiteProfile);
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

            bool offArmPosed = activeProfile != null && (activeProfile.UsesOffArm || activeProfile.TwoHandGrip);
            IsAttacking = false;
            TorsoPitch = TorsoYaw = Lunge = 0f;
            Apply(weaponArm, weaponElbow, rest);
            if (offArmPosed)
                Apply(offArm, offElbow, rest);

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
                case WeaponType.Axe: return AxeProfile;
                case WeaponType.Mace: return MaceProfile;
                case WeaponType.Dagger: return DaggerProfile;
                case WeaponType.Bow: return BowProfile;
                case WeaponType.Staff: return MaceProfile; // swung two-handed when there's no mana to cast
                case WeaponType.Greatsword: return GreatswordProfile;
                case WeaponType.Greataxe: return GreataxeProfile;
                case WeaponType.Maul: return MaulProfile;
                case WeaponType.Sceptre: return MaceProfile;
                default: return UnarmedProfile;
            }
        }

        private void Update()
        {
            if (!IsAttacking)
            {
                TorsoPitch = TorsoYaw = Lunge = 0f;
                return;
            }

            timer += Time.deltaTime * PlaybackSpeed;
            float f = Mathf.Clamp01(timer / activeProfile.Duration);

            AttackProfile p = activeProfile;
            // A great weapon's swing is the same whatever the arms' rest: the poses are real angles
            // (a big enemy whose arms rest raised still hauls the maul up and over the same way).
            bool absolute = p.Absolute || p.TwoHandGrip;
            Pose windup = absolute ? p.WindupOffset : rest + p.WindupOffset;
            Pose strike = absolute ? p.StrikeOffset : rest + p.StrikeOffset;

            Pose pose;
            Pose offPose = rest;
            if (p.SwingStart > 0f && f < p.StrikeTime)
            {
                if (f < p.SwingStart)
                {
                    float t = EaseOut(f / p.SwingStart);
                    pose = Lerp(rest, windup, t);
                    offPose = Lerp(rest, p.OffWindup, t);
                }
                else
                {
                    float t = (f - p.SwingStart) / (p.StrikeTime - p.SwingStart);
                    pose = Lerp(windup, strike, t);
                    offPose = Lerp(p.OffWindup, p.OffStrike, t);
                }
            }
            else if (f < p.StrikeTime)
            {
                float t = EaseOut(p.StrikeTime > 0f ? f / p.StrikeTime : 1f);
                pose = Lerp(rest, windup, t);
                offPose = Lerp(rest, p.OffWindup, t);
            }
            else
            {
                float recoverSpan = 1f - p.StrikeTime;
                float t = recoverSpan > 0f ? (f - p.StrikeTime) / recoverSpan : 1f;
                pose = Lerp(strike, rest, t);
                offPose = Lerp(p.OffStrike, rest, t);

                if (!strikeFired)
                {
                    strikeFired = true;
                    StrikeFrame?.Invoke();
                }
            }

            Apply(weaponArm, weaponElbow, pose);
            if (p.UsesOffArm)
                Apply(offArm, offElbow, offPose);
            else if (p.TwoHandGrip)
                Apply(offArm, offElbow, GripPose(pose));

            // The body joins in: it twists with the arm's sweep, leans back into the wind-up and
            // forward through the blow, and steps into it. Measured from rest, so a swing that
            // only ever raises the arm (the bow) barely moves the body.
            Pose fromRest = new Pose(pose.ArmPitch - rest.ArmPitch, pose.ArmYaw - rest.ArmYaw, 0f, 0f);
            if (p.Absolute)
            {
                // Leans in over the draw and straightens up through the recovery.
                TorsoPitch = p.Lean * (f < p.StrikeTime ? EaseOut(f / Mathf.Max(0.01f, p.StrikeTime)) : 1f - (f - p.StrikeTime) / Mathf.Max(0.01f, 1f - p.StrikeTime));
                TorsoYaw = fromRest.ArmYaw * 0.25f;
                Lunge = 0f;
            }
            else
            {
                TorsoYaw = fromRest.ArmYaw * 0.35f;
                TorsoPitch = -fromRest.ArmPitch * 0.14f;
                float step;
                if (p.SwingStart > 0f && f < p.StrikeTime)
                    step = f < p.SwingStart
                        ? -0.3f * EaseOut(f / p.SwingStart)
                        : Mathf.Lerp(-0.3f, 1f, (f - p.SwingStart) / (p.StrikeTime - p.SwingStart));
                else
                    step = f < p.StrikeTime ? -0.3f * EaseOut(f / Mathf.Max(0.01f, p.StrikeTime)) : 1f - (f - p.StrikeTime) / Mathf.Max(0.01f, 1f - p.StrikeTime);
                Lunge = f < p.StrikeTime ? step * 0.12f : Mathf.Sin(step * Mathf.PI * 0.5f) * 0.16f;

                // A great weapon carries the whole body: it rears back while hauling the weapon up
                // (whichever way the arm goes) and folds forward over the blow.
                if (p.TwoHandGrip)
                {
                    TorsoYaw = fromRest.ArmYaw * 0.45f;
                    TorsoPitch = p.SwingStart > 0f && f < p.StrikeTime
                        ? (f < p.SwingStart
                            ? -12f * EaseOut(f / p.SwingStart)
                            : Mathf.Lerp(-12f, 22f, (f - p.SwingStart) / (p.StrikeTime - p.SwingStart)))
                        : (f < p.StrikeTime ? -12f * EaseOut(f / Mathf.Max(0.01f, p.StrikeTime)) : 22f * step);
                    Lunge *= 1.6f;
                }
            }

            if (f >= 1f)
            {
                IsAttacking = false;
                TorsoPitch = TorsoYaw = Lunge = 0f;
            }
        }

        // The off arm on a two-handed grip: the weapon arm's pose turned in towards it (yaw and
        // roll) so the two hands meet on the haft, with the elbow a little more bent since the
        // off hand holds higher up the grip.
        private const float GripTurnIn = 38f;

        private Pose GripPose(Pose weapon)
        {
            float raised = Mathf.Clamp01(-(weapon.ArmPitch - rest.ArmPitch) / 60f);
            return new Pose(weapon.ArmPitch, weapon.ArmYaw + GripTurnIn * raised, -weapon.ArmRoll * 0.5f, weapon.ElbowBend + 15f);
        }

        private static float EaseOut(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }

        private static Pose Lerp(Pose a, Pose b, float t)
        {
            return new Pose(
                Mathf.Lerp(a.ArmPitch, b.ArmPitch, t), // plain: overhead wind-ups go past 180 from rest
                Mathf.LerpAngle(a.ArmYaw, b.ArmYaw, t),
                Mathf.LerpAngle(a.ArmRoll, b.ArmRoll, t),
                Mathf.Lerp(a.ElbowBend, b.ElbowBend, t));
        }

        private static void Apply(Transform arm, Transform elbow, Pose pose)
        {
            if (arm != null)
                arm.localRotation = Quaternion.Euler(pose.ArmPitch, pose.ArmYaw, pose.ArmRoll);

            if (elbow != null)
                elbow.localRotation = Quaternion.Euler(-pose.ElbowBend, 0f, 0f);
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
