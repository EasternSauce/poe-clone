using UnityEngine;
using PoeClone.Combat;
using PoeClone.Inventory;

namespace PoeClone.Visuals
{
    /// <summary>
    /// Procedural walk cycle for blocky characters built from pivot transforms.
    /// Speed is measured from actual movement, so the stride scales with how fast
    /// the character really moves (enemies at half speed walk at half cadence).
    /// </summary>
    public class CharacterWalkAnimator : MonoBehaviour, PoeClone.Inventory.IPreviewPose
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
        [Tooltip("Wide on purpose: with only a couple of source clips shared by the player and every enemy, narrow variation still reads as identical clicks once several characters are stepping near each other.")]
        [SerializeField] private Vector2 footstepPitchRange = new Vector2(0.75f, 1.3f);
        [SerializeField] private Vector2 footstepVolumeRange = new Vector2(0.7f, 1f);

        [Header("Optional joints")]
        [SerializeField] private Transform upperBody;
        [SerializeField] private Transform leftKnee;
        [SerializeField] private Transform rightKnee;
        [SerializeField] private Transform leftElbow;
        [SerializeField] private Transform rightElbow;

        [Header("Anatomical gait (optional)")]
        [SerializeField] private AnatomicalAnkle leftAnkle;
        [SerializeField] private AnatomicalAnkle rightAnkle;
        private bool NaturalGait => leftAnkle != null && rightAnkle != null;

        public void ConfigureNaturalGait(AnatomicalAnkle left, AnatomicalAnkle right)
        {
            leftAnkle = left;
            rightAnkle = right;
            if (leftLeg != null) leftHipRest = leftLeg.localPosition;
            if (rightLeg != null) rightHipRest = rightLeg.localPosition;
            if (leftArm != null) leftShoulderRest = leftArm.localPosition;
            if (rightArm != null) rightShoulderRest = rightArm.localPosition;
        }

        private float runBlend;
        private AnimationBudget animationBudget;
        private bool importantAnimation;

        private Vector3 lastPosition;
        private Vector3 baseLocalPosition;
        private bool basePoseCaptured;
        private bool isTownNpc;
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
        private float nextFootstepAt;

        // Idle: a slow breath and a little sway, out of step from one character to the next.
        private float idleSeed;
        private const float BreathRate = 1.9f;

        // Standing (anatomical gait only), the weight now and then settles onto one leg and stays
        // there a while. Both feet stay exactly where they stand: the pelvis slides a little over the
        // standing leg, the other hip drops so its knee eases, and the torso turns and leans into a
        // slightly lazy posture that it holds. (Feet sliding or lifting read as floating or hanging.)
        private const float WeightShiftPeriod = 24f, WeightShiftDistance = .025f, HipDrop = .02f;
        private const float ShiftTorsoTurn = 6f, ShiftTorsoLean = 3.5f;
        private float weightShift; // -1 on the left leg, 1 on the right
        // Hip joints' rest places, stored when the rig is built: the portrait copies the model mid-shift.
        [SerializeField] private Vector3 leftHipRest, rightHipRest;

        /// <summary>-1 standing on the left leg, 1 on the right, 0 evenly or moving.</summary>
        public float WeightShift => weightShift;

        // Standing a while, the hero fidgets now and then, never the same way twice running: a stretch
        // of each arm in turn (a small shoulder roll with the hand clear of the ribs),
        // scratching the weapon arm's forearm with the off hand
        // in front of him, or reaching across with the off hand to scratch the other shoulder.
        private enum Fidget { None, ArmStretch, ForearmScratch, ShoulderScratch }
        private Fidget fidget, lastFidget;
        private float fidgetBlend, fidgetStart, fidgetUntil, nextFidgetAt;
        private bool stretchLeftFirst;
        private const float StretchRise = .95f, StretchHold = .4f, StretchFall = 1.1f, StretchDelay = .65f;
        private const float ArmStretchDuration = StretchDelay + StretchRise + StretchHold + StretchFall;
        private const float NeckStretchDuration = 2.4f;

        /// <summary>The gentle body stretch shared with the head, independent of update order.</summary>
        public float StretchAmount => fidget == Fidget.ArmStretch && !RightArmSuppressed && !LeftArmSuppressed
            ? Mathf.Max(StretchReach(Time.time - fidgetStart), StretchReach(Time.time - fidgetStart - StretchDelay))
                * Smooth(fidgetBlend) * Stillness : 0f;

        /// <summary>The chin-forward finish after both arms have lowered.</summary>
        public float NeckStretchAmount
        {
            get
            {
                if (fidget != Fidget.ArmStretch || RightArmSuppressed || LeftArmSuppressed) return 0f;
                float t = Time.time - fidgetStart - ArmStretchDuration;
                if (t <= 0f || t >= NeckStretchDuration) return 0f;
                return Smooth(Mathf.Clamp01(t / .35f)) * Smooth(Mathf.Clamp01((NeckStretchDuration - t) / .4f))
                    * Smooth(fidgetBlend) * Stillness;
            }
        }

        /// <summary>One gentle tilt to each side, returning to center.</summary>
        public float NeckStretchTilt => 8f * Mathf.Sin((Time.time - fidgetStart - ArmStretchDuration)
            * 2f * Mathf.PI / NeckStretchDuration) * NeckStretchAmount;
        // Right side; the left mirrors it. A low, outward reach keeps the hand off the torso.
        private static readonly Vector3 StretchHandHigh = new Vector3(.43f, .95f, .16f);
        private static readonly Vector3 StretchElbowBend = new Vector3(.25f, -.45f, -.5f);
        private static readonly Vector3 StretchShrug = new Vector3(0f, .012f, -.01f);
        private static readonly Vector3 ScratchedHand = new Vector3(-.02f, 1.1f, .3f);
        // The off hand's wrist below the far shoulder and clear of the shoulder cape (which reaches
        // z .24), so the fingers land over the front of the shoulder instead of inside the cape.
        private static readonly Vector3 ShoulderScratchHand = new Vector3(.15f, 1.4f, .29f);
        private static readonly Vector3 ShoulderScratchBend = new Vector3(-.2f, -1f, .4f);
        // Shoulder joints' rest places, stored with the rig like the hips.
        [SerializeField] private Vector3 leftShoulderRest, rightShoulderRest;

        /// <summary>1 standing still, 0 walking or running.</summary>
        public float Stillness => 1f - blend;

        // Braced for a held channel (see CharacterAttackAnimator.IsHolding): 0 striding, 1 planted.
        private float plant;
        private const float PlantCrouch = 0.35f;
        private const float PlantSpread = 10f; // each leg out to its side, degrees

        // A great weapon is carried at the ready in both hands: the weapon arm brings the grip in
        // front of the belly with the head raised ahead, and the off hand closes on the grip
        // (solved onto the weapon itself, so it stays on the haft whatever the weapon's length).
        // Real angles, like the great-weapon swings, so every rig holds it the same way.
        public const float GuardPitch = -30f;
        public const float GuardYaw = 5f;
        public const float GuardRoll = -50f; // across the body, so the hands meet in front of it
        public const float GuardElbow = 20f;
        private const float OffGripAlong = -0.22f; // the off hand's place on the weapon, below the main hand
        private PoeClone.Inventory.EquipmentVisuals equipmentVisuals;
        private Transform offHand, mainHand;
        private Transform mountedWeapon;
        private Vector3 mountPosition;
        private Quaternion mountRotation;
        private float holdBlend;

        /// <summary>True while this character carries a great weapon in both hands (see <see cref="GuardPitch"/>).</summary>
        public bool HoldsTwoHanded => equipmentVisuals != null && equipmentVisuals.TwoHandedWeapon != null;

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
        public float WorkSweep { get; set; }
        public float WorkSweepPhase { get; set; }
        private Transform sweepingBroom, sweepLeftHand, sweepRightHand;

        public void BindSweepingBroom(Transform broom)
        {
            sweepingBroom = broom;
            sweepLeftHand = leftElbow != null ? leftElbow.Find("Hand") : null;
            sweepRightHand = rightElbow != null ? rightElbow.Find("Hand") : null;
        }

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

        /// <summary>The inventory portrait's copy stands far from the player but animates every frame.</summary>
        public void PrepareForPreview() => importantAnimation = true;

        private void Start()
        {
            var owner = GetComponentInParent<PoeClone.Enemies.EnemyHealth>();
            importantAnimation = importantAnimation || GetComponentInParent<PoeClone.Player.PlayerController>() != null ||
                (owner != null && (owner.IsAlly || PoeClone.Enemies.EnemyKinds.Get(owner.KindIndex).IsBoss));
            soundBoard = PoeClone.Audio.SoundBoardSettings.Load();
            lastPosition = transform.position;
            CaptureBasePose();
            isTownNpc = GetComponentInParent<PoeClone.World.Npc>() != null;
            idleSeed = Random.value * 10f;
            attackAnimator = GetComponent<CharacterAttackAnimator>();
            stagger = GetComponentInParent<Stagger>();
            equipmentVisuals = GetComponent<PoeClone.Inventory.EquipmentVisuals>();
            offHand = leftElbow != null ? leftElbow.Find("Hand") : null;
            mainHand = rightElbow != null ? rightElbow.Find("Hand") : null;
            WornTexture.Anchor(gameObject);

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.panStereo = 0f;
        }

        private void LateUpdate()
        {
            if (!animationBudget.Sample(transform, importantAnimation, out float dt)) return;
            // Townsfolk share a close, quiet step sound. Distant neighborhoods must not
            // pile their footsteps into the mix at the player's own footstep volume.
            if (isTownNpc && audioSource != null)
                audioSource.volume = PoeClone.Audio.AudioManager.Instance != null
                    ? 0.25f * PoeClone.Audio.AudioManager.Instance.WorldSfxVolume(transform.position) : 0f;
            if (dt <= 0f)
                return;

            Vector3 delta = transform.position - lastPosition;
            delta.y = 0f;
            lastPosition = transform.position;

            // Placement, warps and network corrections are not walking. Do not let a
            // position jump feed a huge speed into the stride and footstep cadence.
            if (delta.magnitude > Mathf.Max(2f, runFullSpeed * dt * 3f))
            {
                ResetAnimatorState();
                return;
            }

            float instantSpeed = delta.magnitude / dt;
            speed = Mathf.Lerp(speed, instantSpeed, 1f - Mathf.Exp(-12f * dt));

            bool moving = speed > minMoveSpeed;
            blend = Mathf.MoveTowards(blend, moving ? 1f : 0f, blendSpeed * dt);

            // 0 = walk, 1 = run. Driven by real speed, so it kicks in when sprinting.
            float runTarget = Mathf.InverseLerp(runStartSpeed, runFullSpeed, speed);
            runBlend = Mathf.MoveTowards(runBlend, runTarget, runBlendSpeed * dt);

            // Faster running lengthens the stride as well as quickening it, as a sprint does.
            float stride = Mathf.Lerp(strideLength, runStrideLength * Mathf.Sqrt(Mathf.Max(1f, speed / runFullSpeed)), runBlend);
            // Quicken the shared arm/leg cycle by 50% at full sprint, easing in from walking.
            float cadence = Mathf.Lerp(1f, 1.5f, runBlend);
            phase += speed * dt / stride * Mathf.PI * 2f * cadence;
            float s = Mathf.Sin(phase);
            float c = Mathf.Cos(phase);

            // One foot lands every half-cycle (PI radians of phase); fire a step each
            // time we cross into a new half-cycle while actually moving.
            // A held channel (Whirlwind) plants the feet wide, knees bent, and glides instead of striding.
            plant = Mathf.MoveTowards(plant, attackAnimator != null && attackAnimator.IsHolding ? 1f : 0f, 8f * dt);
            float stepping = 1f - plant;

            int stepIndex = Mathf.FloorToInt(phase / Mathf.PI);
            if (moving && plant < 0.5f && stepIndex != lastStepIndex)
                PlayFootstep();
            lastStepIndex = stepIndex;

            float legAmp = Mathf.Lerp(legSwing, runLegSwing, runBlend) * blend * stepping;
            float armAmp = Mathf.Lerp(armSwing, runArmSwing, runBlend) * blend;

            // Standing still: breathing lifts the chest and the arms drift with it.
            float still = 1f - blend;
            bool simple = AdaptiveQuality.DetailLevel >= 3;
            float breath = simple ? 0f : Mathf.Sin((Time.time + idleSeed) * BreathRate) * still;
            float sway = simple ? 0f : Mathf.Sin((Time.time + idleSeed) * BreathRate * 0.5f + 1f) * still;

            bool rightArmSuppressed = RightArmSuppressed;
            bool leftArmSuppressed = LeftArmSuppressed;

            float crouch = Mathf.Clamp01(Crouch + PlantCrouch * plant);
            if (!NaturalGait)
            {
                SetPivot(leftLeg, s * legAmp - 35f * crouch, -PlantSpread * plant);
                SetPivot(rightLeg, -s * legAmp - 35f * crouch, PlantSpread * plant);
            }
            // Each arm swings with the opposite leg. The anatomical gait has its feet furthest apart
            // at phase 0 and PI (cosine), the blocky one at the sine peaks; out of step by a quarter
            // cycle, the arms reach forward as the legs pass, like planting walking poles.
            float armWave = NaturalGait ? -c : s;
            float leftSwing = -armWave * armAmp, rightSwing = armWave * armAmp;
            float armArc = NaturalGait ? blend * (1f - WorkBlend) * Mathf.Lerp(.35f, 1f, runBlend) : 0f;
            if (!leftArmSuppressed)
                SetArm(leftArm, Mathf.Lerp(armRestAngle + leftSwing + breath * 2.5f + sway * 1.5f, WorkArms.x, WorkBlend), -1f, armWave, armArc);
            if (!rightArmSuppressed)
                SetArm(rightArm, Mathf.Lerp(armRestAngle + rightSwing + breath * 2.5f - sway * 1.5f, WorkArms.y, WorkBlend), 1f, -armWave, armArc);

            // Knees bend while the leg swings forward (more so when running).
            float kneeMax = Mathf.Lerp(walkKneeBend, runKneeBend, runBlend) * blend * stepping;
            if (!NaturalGait)
            {
                SetPivot(leftKnee, Mathf.Max(0f, -c) * kneeMax + 70f * crouch);
                SetPivot(rightKnee, Mathf.Max(0f, c) * kneeMax + 70f * crouch);
            }

            // Elbows bend forward; running holds the arms sharply bent. Walking, the forearm folds
            // up as the arm swings forward and hangs loose behind, so the arms never read as poles.
            float elbow = -Mathf.Lerp(walkElbowBend, runElbowBend, runBlend) * blend;
            float elbowFold = WalkElbowFold * (1f - runBlend);
            if (!leftArmSuppressed)
                SetPivot(leftElbow, Mathf.Lerp(elbow + Mathf.Min(0f, leftSwing) * elbowFold, WorkElbows.x, WorkBlend));
            if (!rightArmSuppressed)
                SetPivot(rightElbow, Mathf.Lerp(elbow + Mathf.Min(0f, rightSwing) * elbowFold, WorkElbows.y, WorkBlend));

            float staggerTilt = stagger != null ? stagger.RecoilFraction * staggerTiltAngle : 0f;
            float torsoPitch = attackAnimator != null ? attackAnimator.TorsoPitch : 0f;
            float torsoYaw = attackAnimator != null ? attackAnimator.TorsoYaw : 0f;
            // Walking, the shoulders counter-rotate against the hips a little, following the arms.
            float walkTwist = simple ? 0f : armWave * 5f * blend;
            if (upperBody != null)
                upperBody.localRotation = Quaternion.Euler(
                    runLean * runBlend * blend + staggerTilt + torsoPitch - breath * 1.2f + 25f * crouch + Hunch + WorkLean * WorkBlend,
                    torsoYaw + walkTwist + sway * 2f, 0f);

            float bobAmount = Mathf.Lerp(bobHeight, runBobHeight, runBlend);
            float bob = simple ? 0f : Mathf.Abs(c) * bobAmount * blend * stepping;
            if (NaturalGait)
            {
                // Walking: the body sinks just after each heel strike as the leg takes the weight
                // (bending the knee), then rises over the straightened leg at mid-stance.
                // Running sinks onto the planted leg and rises through the flight.
                float walkBob = -.025f+.015f*Mathf.Cos(2f*(phase-WalkStance*Mathf.PI));
                float runBob = -.03f-.03f*Mathf.Cos(2f*(phase-RunStance*Mathf.PI));
                bob = Mathf.Lerp(walkBob,runBob,runBlend)*blend*stepping;
            }
            float lunge = attackAnimator != null ? attackAnimator.Lunge : 0f;
            transform.localPosition = baseLocalPosition + Vector3.up * (bob - 0.28f * crouch) + Vector3.forward * lunge;
            if (NaturalGait)
            {
                // Held on each leg for a while, easing across in between.
                float hold = Mathf.Clamp(Mathf.Sin((Time.time + idleSeed * 7f) * 2f * Mathf.PI / WeightShiftPeriod) * 1.8f, -1f, 1f);
                weightShift = simple ? 0f : (1.5f * hold - .5f * hold * hold * hold) * still * stepping * (1f - crouch);
                // Move weight over the supporting leg; the torso keeps its existing counter-twist.
                transform.localPosition += Vector3.right*(s*.018f*blend*stepping + weightShift*WeightShiftDistance);
                // Against the pelvis, the shoulders dip on the standing side and turn a little away from it.
                if (upperBody != null) upperBody.localRotation *= Quaternion.Euler(0f, -ShiftTorsoTurn * weightShift, -ShiftTorsoLean * weightShift);
                UpdateFidget(dt, still > .98f && !simple && plant <= 0f && crouch <= 0f && WorkBlend <= 0f &&
                    !HoldsTwoHanded && !rightArmSuppressed && !leftArmSuppressed);
                if (upperBody != null) upperBody.localRotation *= Quaternion.Euler(6f * StretchAmount, 0f, 0f);
                PoseNaturalLeg(leftLeg,leftKnee,leftAnkle,phase,-1f,bob-.28f*crouch,crouch);
                PoseNaturalLeg(rightLeg,rightKnee,rightAnkle,phase+Mathf.PI,1f,bob-.28f*crouch,crouch);
                RestShoulders();
                if (fidgetBlend > 0f) PoseFidget();
            }
            if (sweepingBroom != null && WorkSweep > 0f)
                PoseSweepingBroom(!leftArmSuppressed, !rightArmSuppressed);

            // The grip lets go only for a one-armed gesture (a spell cast); a great weapon's own
            // swing poses both arms and starts and ends in this same guard.
            bool oneArmed = attackAnimator != null && attackAnimator.IsAttacking && !attackAnimator.DrivesOffArm;
            holdBlend = HoldsTwoHanded ? Mathf.MoveTowards(holdBlend, oneArmed ? 0f : 1f, 6f * dt) : 0f;
            if (HoldsTwoHanded)
                PoseTwoHandedHold(rightArmSuppressed, leftArmSuppressed, s * armAmp, breath);
        }

        // Share of the cycle a foot is down. Walking keeps one foot down at all times (both briefly);
        // running is under half, so both feet leave the ground between steps.
        private const float WalkStance = .64f;
        private const float RunStance = .38f;

        // Extra elbow bend per degree of forward arm swing while walking.
        private const float WalkElbowFold = .3f;

        private void PoseNaturalLeg(Transform hip,Transform knee,AnatomicalAnkle ankle,float cycle,float side,float bodyOffset,float crouch)
        {
            float t=Mathf.Repeat(cycle/(2*Mathf.PI),1);
            float stance=Mathf.Lerp(WalkStance,RunStance,runBlend);
            float travel=Mathf.Lerp(.42f,.70f,runBlend);
            float forward,lift,footPitch;
            if(t<stance)
            {
                float contact=t/stance;
                forward=Mathf.Lerp(travel*.5f,-travel*.5f,contact);
                lift=0;
                footPitch=-8f*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.18f,contact)))
                    +12f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.72f,1,contact));
            }
            else
            {
                float swing=(t-stance)/(1-stance);
                forward=Mathf.Lerp(-travel*.5f,travel*.5f,Mathf.SmoothStep(0,1,swing));
                // A running leg kicks the heel up behind early in the swing, then reaches forward.
                float walkLift=Mathf.Sin(swing*Mathf.PI)*.045f;
                float runLift=Mathf.Sin(Mathf.Pow(swing,.6f)*Mathf.PI)*.30f;
                if(runBlend>0) forward-=Mathf.Sin(swing*Mathf.PI)*Mathf.Max(0,.6f-swing)*.25f*runBlend;
                lift=Mathf.Lerp(walkLift,runLift,runBlend);
                footPitch=Mathf.Lerp(-6f*Mathf.Sin(swing*Mathf.PI),25f*Mathf.Sin(swing*Mathf.PI)*(1-swing)-10f*swing,runBlend);
            }
            float weight=blend*(1-plant);
            // Off the weight, the hip drops and the knee eases over a foot that stays planted.
            float relax=Mathf.Max(0,-side*weightShift);
            Vector3 rest=side<0?leftHipRest:rightHipRest;
            if(rest!=Vector3.zero) hip.localPosition=rest+Vector3.down*(HipDrop*relax);
            // The feet stay put in the world while the body slides over them with the weight.
            Vector3 target=new Vector3(hip.localPosition.x-weightShift*WeightShiftDistance,.05f-bodyOffset+lift*weight,forward*weight);
            Vector3 reach=target-hip.localPosition;
            const float thigh=.42f,shin=.38f;
            float distance=Mathf.Clamp(reach.magnitude,.12f,(thigh+shin)*.995f);
            float vertical=Mathf.Sqrt(reach.x*reach.x+reach.y*reach.y);
            float aim=Mathf.Atan2(-reach.z,vertical)*Mathf.Rad2Deg;
            float bend=Mathf.Acos(Mathf.Clamp((thigh*thigh+distance*distance-shin*shin)/(2*thigh*distance),-1,1))*Mathf.Rad2Deg;
            float hipPitch=Mathf.Lerp(aim-bend,-35f,crouch);
            float kneePitch=Mathf.Lerp(180-Mathf.Acos(Mathf.Clamp((thigh*thigh+shin*shin-distance*distance)/(2*thigh*shin),-1,1))*Mathf.Rad2Deg,70f,crouch);
            // Each leg leans out to its planted foot.
            float lean=Mathf.Atan2(reach.x,-reach.y)*Mathf.Rad2Deg;
            hip.localRotation=Quaternion.Euler(hipPitch,Mathf.Sin(cycle)*2f*weight,side*PlantSpread*plant+lean);
            knee.localRotation=Quaternion.Euler(kneePitch,0,0);
            ankle.Pose(Mathf.Clamp(footPitch*weight-hipPitch-kneePitch,-38f,30f));
        }

        private static float Smooth(float t) => t*t*(3f-2f*t);

        private void UpdateFidget(float dt, bool allowed)
        {
            float now = Time.time;
            if (!allowed)
            {
                fidgetUntil = 0f;
                nextFidgetAt = Mathf.Max(nextFidgetAt, now + 3f);
            }
            else if (fidget == Fidget.None && now >= nextFidgetAt)
            {
                // The scratch needs a free off hand; the weapon hand may keep its weapon.
                bool left = false, right = false;
                equipmentVisuals?.HeldHands(out left, out right);
                var choices = new System.Collections.Generic.List<Fidget> { Fidget.ArmStretch };
                if (!left) { choices.Add(Fidget.ForearmScratch); choices.Add(Fidget.ShoulderScratch); }
                if (choices.Count > 1) choices.Remove(lastFidget);
                fidget = lastFidget = choices[Random.Range(0, choices.Count)];
                stretchLeftFirst = Random.value < .5f;
                fidgetStart = now;
                fidgetUntil = now + (fidget == Fidget.ArmStretch ? ArmStretchDuration + NeckStretchDuration : Random.Range(3f, 4.5f));
            }
            bool on = fidget != Fidget.None && now < fidgetUntil;
            fidgetBlend = Mathf.MoveTowards(fidgetBlend, on ? 1f : 0f, dt / .45f);
            if (!on && fidgetBlend <= 0f && fidget != Fidget.None)
            {
                fidget = Fidget.None;
                nextFidgetAt = now + Random.Range(10f, 20f);
            }
        }

        private void PoseFidget()
        {
            float w = Smooth(fidgetBlend), t = Time.time;
            bool left = false;
            equipmentVisuals?.HeldHands(out left, out _);
            // An attack takes its arm at once, even while the gesture is still easing out.
            left |= LeftArmSuppressed;
            float elapsed = t - fidgetStart;
            switch (fidget)
            {
                case Fidget.ArmStretch:
                    // One arm, then the other a beat later; a held weapon or shield comes along.
                    if (!LeftArmSuppressed) StretchArm(leftArm, leftElbow, offHand, leftShoulderRest, -1f, elapsed - (stretchLeftFirst ? 0f : StretchDelay), w);
                    if (!RightArmSuppressed) StretchArm(rightArm, rightElbow, mainHand, rightShoulderRest, 1f, elapsed - (stretchLeftFirst ? StretchDelay : 0f), w);
                    break;
                case Fidget.ShoulderScratch:
                    if (left || offHand == null) break;
                    // Across the chest, elbow low in front, fingers working over the far shoulder.
                    Vector3 shoulder = ShoulderScratchHand + new Vector3(Mathf.Sin(elapsed * 8f) * .012f, Mathf.Sin(elapsed * 16f) * .015f, 0f);
                    PoseGrip(leftArm, leftElbow, offHand, transform.TransformPoint(shoulder), -1f, w, ShoulderScratchBend);
                    break;
                case Fidget.ForearmScratch:
                    if (left || RightArmSuppressed || mainHand == null || offHand == null) break;
                    // The weapon arm (weapon and all) folds across the belly; the off hand meets it in
                    // the middle and scratches back and forth along the top of its forearm.
                    PoseGrip(rightArm, rightElbow, mainHand, transform.TransformPoint(ScratchedHand), 1f, w);
                    HangHeldWeapon(w);
                    Vector3 along = Vector3.Lerp(rightElbow.position, mainHand.position, .5f + .12f * Mathf.Sin(elapsed * 9f));
                    Vector3 scratch = along + transform.TransformDirection(new Vector3(-.07f, .05f, .02f));
                    PoseGrip(leftArm, leftElbow, offHand, scratch, -1f, w);
                    break;
            }
        }

        // Folded across the belly, the fist would hold its weapon straight out sideways; turn the
        // forearm about its own axis so the blade hangs down and a little forward instead.
        private void HangHeldWeapon(float weight)
        {
            Transform socket = rightElbow.Find("Socket_MainHand");
            if (socket == null || socket.childCount == 0) return;
            Vector3 axis = (mainHand.position - rightElbow.position).normalized;
            Vector3 blade = Vector3.ProjectOnPlane(socket.GetChild(0).up, axis);
            Vector3 wanted = Vector3.ProjectOnPlane(transform.TransformDirection(new Vector3(0f, -1f, .35f)), axis);
            if (blade.sqrMagnitude < 1e-4f || wanted.sqrMagnitude < 1e-4f) return;
            float twist = Vector3.SignedAngle(blade, wanted, axis) * weight;
            rightElbow.rotation = Quaternion.AngleAxis(twist, axis) * rightElbow.rotation;
        }

        // Slowly ease from the hanging pose into a small outward stretch and back. Solving the
        // peak pose then blending by reach avoids a separate fast lift onto a flank-scratching path.
        // side: -1 left, 1 right.
        private void StretchArm(Transform arm, Transform elbow, Transform hand, Vector3 shoulderRest, float side, float time, float weight)
        {
            float reach = StretchReach(time);
            if (arm == null || hand == null || reach <= 0f) return;
            Vector3 mirror = new Vector3(side, 1f, 1f);
            if (shoulderRest != Vector3.zero) arm.localPosition = shoulderRest + Vector3.Scale(StretchShrug, mirror) * reach * weight;
            Vector3 target = Vector3.Scale(StretchHandHigh, mirror);
            PoseGrip(arm, elbow, hand, transform.TransformPoint(target), side, reach * weight,
                Vector3.Scale(StretchElbowBend, mirror));
        }

        private static float StretchReach(float time)
        {
            if (time <= 0f || time >= StretchRise + StretchHold + StretchFall) return 0f;
            return time < StretchRise ? Smooth(time / StretchRise)
                : time < StretchRise + StretchHold ? 1f : Smooth(1f - (time - StretchRise - StretchHold) / StretchFall);
        }

        // The stretch's shrug moves the shoulder joints; every frame starts from their rest.
        private void RestShoulders()
        {
            if (leftArm != null && leftShoulderRest != Vector3.zero) leftArm.localPosition = leftShoulderRest;
            if (rightArm != null && rightShoulderRest != Vector3.zero) rightArm.localPosition = rightShoulderRest;
        }

        private void PoseTwoHandedHold(bool rightArmSuppressed, bool leftArmSuppressed, float swing, float breath)
        {
            // The weapon arm keeps the guard throughout (every swing and cast starts and ends in it).
            // Walking rocks the weapon a little instead of swinging the arm.
            if (!rightArmSuppressed && rightArm != null)
            {
                rightArm.localRotation = Quaternion.Euler(GuardPitch + swing * 0.15f + breath * 2f, GuardYaw, GuardRoll);
                if (rightElbow != null)
                    rightElbow.localRotation = Quaternion.Euler(-GuardElbow, 0f, 0f);
            }

            // A great weapon's swing keeps the off hand solved onto the haft the whole way through
            // (the swing's own off-arm pose only seeds the solve); other off-arm poses let go.
            float weight = holdBlend;
            if (leftArmSuppressed)
                weight = attackAnimator.SwingsTwoHanded ? 1f : 0f;

            Transform weapon = equipmentVisuals.TwoHandedWeapon;
            if (weapon != mountedWeapon)
            {
                mountedWeapon = weapon;
                mountPosition = weapon.localPosition;
                mountRotation = weapon.localRotation;
            }
            weapon.localPosition = mountPosition;
            weapon.localRotation = mountRotation;

            if (weight > 0f)
            {
                if (weight >= 1f)
                    BringGripInReach(weapon);
                PoseGrip(leftArm, leftElbow, offHand, weapon.TransformPoint(Vector3.up * OffGripAlong), -1f, weight);
            }
        }

        // A swing poses the weapon arm at full stretch, where the off hand can't reach the haft.
        // Draw the weapon hand in towards the chest until both hands can hold it, keeping the
        // weapon pointing the way the swing aims it: the arms bend, the arc stays.
        private void BringGripInReach(Transform weapon)
        {
            if (mainHand == null || offHand == null || leftArm == null || rightArm == null || rightElbow == null)
                return;

            float reach = 0.97f * (Vector3.Distance(leftArm.position, leftElbow.position) + Vector3.Distance(leftElbow.position, offHand.position));
            Vector3 hand = mainHand.position;
            Vector3 toGrip = weapon.TransformPoint(Vector3.up * OffGripAlong) - hand;
            Vector3 anchor = (leftArm.position + rightArm.position) * 0.5f + transform.forward * 0.3f;
            Vector3 target = hand;
            for (int i = 0; i < 12 && (Vector3.Distance(target, rightArm.position) > reach || Vector3.Distance(target + toGrip, leftArm.position) > reach); i++)
                target = Vector3.Lerp(target, anchor, 0.25f);
            if (target == hand)
                return;

            Quaternion weaponRotation = weapon.rotation;
            Vector3 offset = weapon.position - hand;
            PoseGrip(rightArm, rightElbow, mainHand, target, 1f, 1f);
            weapon.SetPositionAndRotation(mainHand.position + offset, weaponRotation);
        }

        private void PoseSweepingBroom(bool poseLeft, bool poseRight)
        {
            float stroke = Mathf.Sin(WorkSweepPhase);
            // The bristles travel across the doorstep. Only the return stroke lifts
            // slightly; the handle tilts around the upper grip instead of bobbing.
            float lift = Mathf.Max(0f, -Mathf.Cos(WorkSweepPhase)) * 0.035f;
            sweepingBroom.localPosition = new Vector3(stroke * 0.45f, 0.02f + lift, 0.62f);
            sweepingBroom.localRotation = Quaternion.FromToRotation(Vector3.up,
                new Vector3(-stroke * 0.45f, 1f, -0.3f).normalized);
            float weight = Mathf.Clamp01(WorkBlend * WorkSweep);
            if (poseLeft)
                PoseGrip(leftArm, leftElbow, sweepLeftHand, sweepingBroom.TransformPoint(Vector3.up * 1.5f), -1f, weight);
            if (poseRight)
                PoseGrip(rightArm, rightElbow, sweepRightHand, sweepingBroom.TransformPoint(Vector3.up * 1.25f), 1f, weight);
        }

        // Solve the two arm segments so both visible hands stay on the shaft as
        // the broom moves. Shoulder/elbow rotations alone preserve the rig lengths.
        // elbowBend, in this model's space, is where the elbow points (by default out, down and back).
        private void PoseGrip(Transform arm, Transform elbow, Transform hand, Vector3 grip, float side, float weight, Vector3 elbowBend = default)
        {
            if (elbowBend == Vector3.zero) elbowBend = new Vector3(side, -0.6f, -0.2f);
            if (arm == null || elbow == null || hand == null) return;
            Quaternion armRest = arm.localRotation, elbowRest = elbow.localRotation;
            float upperLength = Vector3.Distance(arm.position, elbow.position);
            float lowerLength = Vector3.Distance(elbow.position, hand.position);
            Vector3 reach = grip - arm.position;
            if (upperLength < 0.001f || lowerLength < 0.001f || reach.sqrMagnitude < 0.000001f) return;
            float distance = Mathf.Clamp(reach.magnitude, Mathf.Abs(upperLength - lowerLength) + 0.0001f,
                upperLength + lowerLength - 0.0001f);
            Vector3 direction = reach.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(transform.TransformDirection(elbowBend), direction).normalized;
            float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2f * distance);
            float outward = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            Vector3 elbowTarget = arm.position + direction * along + bend * outward;
            arm.rotation = Quaternion.FromToRotation(elbow.position - arm.position, elbowTarget - arm.position) * arm.rotation;
            elbow.rotation = Quaternion.FromToRotation(hand.position - elbow.position, grip - elbow.position) * elbow.rotation;
            Quaternion armPose = arm.localRotation, elbowPose = elbow.localRotation;
            arm.localRotation = Quaternion.Slerp(armRest, armPose, weight);
            elbow.localRotation = Quaternion.Slerp(elbowRest, elbowPose, weight);
        }

        // Called right after a teleport (e.g. an area gate) so the next LateUpdate
        // doesn't see a huge instantaneous position delta and misread it as a burst
        // of movement speed, which jerks the pose for a frame or two.
        public void ResetAnimatorState()
        {
            CaptureBasePose();
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
            if (NaturalGait) { leftAnkle.Pose(0); rightAnkle.Pose(0); }
            transform.localPosition = baseLocalPosition;
            lastStepIndex = Mathf.FloorToInt(phase / Mathf.PI);
            nextFootstepAt = Time.time;
        }

        private void CaptureBasePose()
        {
            if (basePoseCaptured) return;
            baseLocalPosition = transform.localPosition;
            basePoseCaptured = true;
        }

        private void PlayFootstep()
        {
            if (Time.time < nextFootstepAt || audioSource == null || footstepClips == null || footstepClips.Length == 0)
                return;

            AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];
            if (clip == null)
                return;

            audioSource.pitch = Random.Range(footstepPitchRange.x, footstepPitchRange.y);
            float volume = Random.Range(footstepVolumeRange.x, footstepVolumeRange.y);
            soundBoard?.Resolve(ref clip);
            if (clip == null) return;
            // A single villager must not stack rustling recordings on top of itself.
            // Also cap rapid retriggers for all characters after unusually large movement.
            nextFootstepAt = Time.time + (isTownNpc
                ? Mathf.Max(0.22f, clip.length / Mathf.Max(0.1f, audioSource.pitch)) : 0.22f);
            audioSource.PlayOneShot(clip, volume);
        }

        // A real arm does not swing on a pin. The elbow is carried a little out from the ribs and
        // flares further out as it drives back; swinging forward, the upper arm turns in so the
        // bent forearm crosses towards the front of the chest. side: -1 left, 1 right.
        // forward: -1 arm fully back, 1 fully forward. weight 0 swings on the pitch axis alone.
        private const float ArmCarry = 10f, ArmFlareBack = 12f, ArmTuckFront = 8f;
        private const float ArmTurnIn = 12f, ArmTurnInFront = 30f;

        private static void SetArm(Transform arm, float pitch, float side, float forward, float weight)
        {
            if (arm == null) return;
            float front = Mathf.Max(0f, forward), back = Mathf.Max(0f, -forward);
            float abduct = (ArmCarry + ArmFlareBack * back - ArmTuckFront * front) * weight;
            float turnIn = (ArmTurnIn + ArmTurnInFront * front) * weight;
            arm.localRotation = Quaternion.Euler(pitch, 0f, 0f) * Quaternion.Euler(0f, 0f, side * abduct)
                * Quaternion.Euler(0f, -side * turnIn, 0f);
        }

        private static void SetPivot(Transform pivot, float angle, float roll = 0f)
        {
            if (pivot != null)
                pivot.localRotation = Quaternion.Euler(angle, 0f, roll);
        }
    }
}
