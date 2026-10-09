using UnityEngine;
using PoeClone.Combat;

namespace PoeClone.Visuals
{
    /// <summary>
    /// Procedural walk cycle for blocky characters built from pivot transforms.
    /// Speed is measured from actual movement, so the stride scales with how fast
    /// the character really moves (enemies at half speed walk at half cadence).
    /// </summary>
    public class CharacterWalkAnimator : MonoBehaviour
    {
        [SerializeField] private Transform leftLeg;
        [SerializeField] private Transform rightLeg;
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;

        [Header("Pose")]
        [SerializeField] private float armRestAngle = 0f;

        [Header("Walk")]
        [SerializeField] private float legSwing = 38f;
        [SerializeField] private float armSwing = 28f;
        [SerializeField] private float strideLength = 3.6f;
        [SerializeField] private float bobHeight = 0.05f;
        [SerializeField] private float blendSpeed = 8f;
        [SerializeField] private float minMoveSpeed = 0.3f;

        [Header("Walk joints")]
        [SerializeField] private float walkKneeBend = 18f;
        [SerializeField] private float walkElbowBend = 12f;

        [Header("Run (blends in between these two speeds)")]
        [SerializeField] private float runStartSpeed = 7.5f;
        [SerializeField] private float runFullSpeed = 9f;
        [SerializeField] private float runBlendSpeed = 6f;
        [SerializeField] private float runLegSwing = 62f;
        [SerializeField] private float runArmSwing = 58f;
        [SerializeField] private float runKneeBend = 80f;
        [SerializeField] private float runElbowBend = 85f;
        [SerializeField] private float runStrideLength = 5.4f;
        [SerializeField] private float runBobHeight = 0.12f;
        [SerializeField] private float runLean = 14f;

        [Header("Stagger")]
        [Tooltip("How far the upper body rocks back when staggered (see Combat.Stagger).")]
        [SerializeField] private float staggerTiltAngle = -22f;

        [Header("Footsteps")]
        [SerializeField] private AudioClip[] footstepClips;
        [SerializeField] private float footstepVolume = 0.6f;
        [Tooltip("Wide on purpose: with only a couple of source clips shared by the player and every enemy, narrow variation still reads as identical clicks once several characters are stepping near each other.")]
        [SerializeField] private Vector2 footstepPitchRange = new Vector2(0.75f, 1.3f);
        [SerializeField] private Vector2 footstepVolumeRange = new Vector2(0.7f, 1f);

        [Header("Optional joints")]
        [SerializeField] private Transform upperBody;
        [SerializeField] private Transform leftKnee;
        [SerializeField] private Transform rightKnee;
        [SerializeField] private Transform leftElbow;
        [SerializeField] private Transform rightElbow;

        private float runBlend;

        private Vector3 lastPosition;
        private Vector3 baseLocalPosition;
        private float speed;
        private float phase;
        private float blend;

        // While an attack is playing, CharacterAttackAnimator owns the right arm/elbow pose;
        // skipping them here (rather than both scripts writing to the same pivot every frame)
        // avoids fighting over control regardless of script execution order.
        private CharacterAttackAnimator attackAnimator;
        private bool RightArmSuppressed => attackAnimator != null && attackAnimator.IsAttacking;
        private bool LeftArmSuppressed => attackAnimator != null && attackAnimator.DrivesOffArm;

        // Read for its RecoilFraction only; Stagger itself lives on the root, not this model.
        private Stagger stagger;

        private AudioSource audioSource;
        private PoeClone.Audio.SoundBoardSettings soundBoard;
        private int lastStepIndex;

        // Idle: a slow breath and a little sway, out of step from one character to the next.
        private float idleSeed;
        private const float BreathRate = 1.9f;

        /// <summary>The right/left arm's rest pitch (0 for the player, more raised for monsters). Shared with CharacterAttackAnimator so its swing offsets land correctly regardless of rig.</summary>
        public float ArmRestAngle => armRestAngle;

        /// <summary>
        /// How far the body is crouched (0 standing, 1 deep): knees bent, hips dropped and the chest
        /// leaning in, for gathering into a leap or bracing a slam. Set by whoever drives the move.
        /// </summary>
        public float Crouch { get; set; }

        /// <summary>A standing forward lean of the upper body, in degrees, on top of everything else (a stooped old man).</summary>
        public float Hunch { get; set; }

        // VillageRoutine supplies a chore pose; locomotion still owns the legs and stride.
        public float WorkBlend { get; set; }
        public Vector2 WorkArms { get; set; }
        public Vector2 WorkElbows { get; set; }
        public float WorkLean { get; set; }
        public Transform WorkHand => rightElbow != null ? rightElbow : rightArm;

        /// <summary>Changes the arms' rest pitch (town NPCs built from the monster rig hold theirs down).</summary>
        public void SetArmRestAngle(float angle)
        {
            armRestAngle = angle;
        }

public void Configure(
            Transform leftLegPivot,
            Transform rightLegPivot,
            Transform leftArmPivot,
            Transform rightArmPivot,
            float armRest)
        {
            leftLeg = leftLegPivot;
            rightLeg = rightLegPivot;
            leftArm = leftArmPivot;
            rightArm = rightArmPivot;
            armRestAngle = armRest;
        }

        public void ConfigureJoints(
            Transform upperBodyPivot,
            Transform leftKneePivot,
            Transform rightKneePivot,
            Transform leftElbowPivot,
            Transform rightElbowPivot)
        {
            upperBody = upperBodyPivot;
            leftKnee = leftKneePivot;
            rightKnee = rightKneePivot;
            leftElbow = leftElbowPivot;
            rightElbow = rightElbowPivot;
        }

        private void Start()
        {
            soundBoard = PoeClone.Audio.SoundBoardSettings.Load();
            lastPosition = transform.position;
            baseLocalPosition = transform.localPosition;
            idleSeed = Random.value * 10f;
            attackAnimator = GetComponent<CharacterAttackAnimator>();
            stagger = GetComponentInParent<Stagger>();

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.panStereo = 0f;
        }

private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            Vector3 delta = transform.position - lastPosition;
            delta.y = 0f;
            lastPosition = transform.position;

            float instantSpeed = delta.magnitude / dt;
            speed = Mathf.Lerp(speed, instantSpeed, 1f - Mathf.Exp(-12f * dt));

            bool moving = speed > minMoveSpeed;
            blend = Mathf.MoveTowards(blend, moving ? 1f : 0f, blendSpeed * dt);

            // 0 = walk, 1 = run. Driven by real speed, so it kicks in when sprinting.
            float runTarget = Mathf.InverseLerp(runStartSpeed, runFullSpeed, speed);
            runBlend = Mathf.MoveTowards(runBlend, runTarget, runBlendSpeed * dt);

            float stride = Mathf.Lerp(strideLength, runStrideLength, runBlend);
            phase += speed * dt / stride * Mathf.PI * 2f;
            float s = Mathf.Sin(phase);
            float c = Mathf.Cos(phase);

            // One foot lands every half-cycle (PI radians of phase); fire a step each
            // time we cross into a new half-cycle while actually moving.
            int stepIndex = Mathf.FloorToInt(phase / Mathf.PI);
            if (moving && stepIndex != lastStepIndex)
                PlayFootstep();
            lastStepIndex = stepIndex;

            float legAmp = Mathf.Lerp(legSwing, runLegSwing, runBlend) * blend;
            float armAmp = Mathf.Lerp(armSwing, runArmSwing, runBlend) * blend;

            // Standing still: breathing lifts the chest and the arms drift with it.
            float still = 1f - blend;
            float breath = Mathf.Sin((Time.time + idleSeed) * BreathRate) * still;
            float sway = Mathf.Sin((Time.time + idleSeed) * BreathRate * 0.5f + 1f) * still;

            bool rightArmSuppressed = RightArmSuppressed;
            bool leftArmSuppressed = LeftArmSuppressed;

            float crouch = Mathf.Clamp01(Crouch);
            SetPivot(leftLeg, s * legAmp - 35f * crouch);
            SetPivot(rightLeg, -s * legAmp - 35f * crouch);
            if (!leftArmSuppressed)
                SetPivot(leftArm, Mathf.Lerp(armRestAngle - s * armAmp + breath * 2.5f + sway * 1.5f, WorkArms.x, WorkBlend));
            if (!rightArmSuppressed)
                SetPivot(rightArm, Mathf.Lerp(armRestAngle + s * armAmp + breath * 2.5f - sway * 1.5f, WorkArms.y, WorkBlend));

            // Knees bend while the leg swings forward (more so when running).
            float kneeMax = Mathf.Lerp(walkKneeBend, runKneeBend, runBlend) * blend;
            SetPivot(leftKnee, Mathf.Max(0f, -c) * kneeMax + 70f * crouch);
            SetPivot(rightKnee, Mathf.Max(0f, c) * kneeMax + 70f * crouch);

            // Elbows bend forward; running holds the arms sharply bent.
            float elbow = -Mathf.Lerp(walkElbowBend, runElbowBend, runBlend) * blend;
            if (!leftArmSuppressed)
                SetPivot(leftElbow, Mathf.Lerp(elbow, WorkElbows.x, WorkBlend));
            if (!rightArmSuppressed)
                SetPivot(rightElbow, Mathf.Lerp(elbow, WorkElbows.y, WorkBlend));

            float staggerTilt = stagger != null ? stagger.RecoilFraction * staggerTiltAngle : 0f;
            float torsoPitch = attackAnimator != null ? attackAnimator.TorsoPitch : 0f;
            float torsoYaw = attackAnimator != null ? attackAnimator.TorsoYaw : 0f;
            // Walking, the shoulders counter-rotate against the hips a little.
            float walkTwist = s * 5f * blend;
            if (upperBody != null)
                upperBody.localRotation = Quaternion.Euler(
                    runLean * runBlend * blend + staggerTilt + torsoPitch - breath * 1.2f + 25f * crouch + Hunch + WorkLean * WorkBlend,
                    torsoYaw + walkTwist + sway * 2f, 0f);

            float bobAmount = Mathf.Lerp(bobHeight, runBobHeight, runBlend);
            float bob = Mathf.Abs(c) * bobAmount * blend;
            float lunge = attackAnimator != null ? attackAnimator.Lunge : 0f;
            transform.localPosition = baseLocalPosition + Vector3.up * (bob - 0.28f * crouch) + Vector3.forward * lunge;
        }

        // Called right after a teleport (e.g. an area gate) so the next LateUpdate
        // doesn't see a huge instantaneous position delta and misread it as a burst
        // of movement speed, which jerks the pose for a frame or two.
        public void ResetAnimatorState()
        {
            lastPosition = transform.position;
            speed = 0f;
            phase = 0f;
            blend = 0f;
            runBlend = 0f;
            SetPivot(leftLeg, 0f);
            SetPivot(rightLeg, 0f);
            SetPivot(leftArm, armRestAngle);
            SetPivot(rightArm, armRestAngle);
            SetPivot(leftKnee, 0f);
            SetPivot(rightKnee, 0f);
            SetPivot(leftElbow, 0f);
            SetPivot(rightElbow, 0f);
            SetPivot(upperBody, 0f);
            transform.localPosition = baseLocalPosition;
            lastStepIndex = Mathf.FloorToInt(phase / Mathf.PI);
        }

        private void PlayFootstep()
        {
            if (audioSource == null || footstepClips == null || footstepClips.Length == 0)
                return;

            AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];
            if (clip == null)
                return;

            audioSource.pitch = Random.Range(footstepPitchRange.x, footstepPitchRange.y);
            float volume = footstepVolume * Random.Range(footstepVolumeRange.x, footstepVolumeRange.y);
            soundBoard?.Resolve(ref clip, ref volume);
            if (clip == null) return;
            audioSource.PlayOneShot(clip, volume);
        }

        
private static void SetPivot(Transform pivot, float angle)
        {
            if (pivot != null)
                pivot.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }
    }
}
