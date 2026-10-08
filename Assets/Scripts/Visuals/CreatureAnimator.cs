using System.Collections.Generic;
using UnityEngine;
using PoeClone.Combat;

namespace PoeClone.Visuals
{
    /// <summary>
    /// Procedural animation for the bodies <see cref="CreatureBuilder"/> makes: a gait for each
    /// body (an insect's alternating tripods, a wolf's trot, a slime's hops, a bat's flapping
    /// hover), idle life (breathing, looking round, twitching fangs, a wagging tail), the attack
    /// (a wind-up, a lunge, a snap of the jaws), a recoil when staggered, a pose in the air during a
    /// leap, and a death of its own (bugs flip onto their backs, wolves fall on their side, slimes
    /// splat, bats drop out of the air).
    ///
    /// Everything is read from what the enemy does rather than told: speed and vertical speed from
    /// how the body moves, the attack from the <see cref="CharacterAttackAnimator"/> beside it (which
    /// times the strike), the recoil from <see cref="Stagger"/>. So a spectator's puppet, which only
    /// gets positions and attack/stagger counters, animates the same way.
    /// </summary>
    public class CreatureAnimator : MonoBehaviour
    {
        /// <summary>Where a creature's spit leaves its body (see EnemyCombat.BoltOrigin).</summary>
        public const string MouthName = "Mouth";

        public class Leg
        {
            public Transform Hip;
            public Transform Knee;
            public float Yaw;    // insect legs: how far the hip turns the leg out (rest)
            public float Lift;   // ...how far the femur rises (rest)
            public float Bend;   // ...how far the knee folds down (rest)
            public int Side;     // +1 right, -1 left
            public int Group;    // which half of the gait it steps with
            public bool Front;   // wolves: a foreleg
        }

        // Set by CreatureBuilder.
        public CreatureBody Body;
        public Transform BodyPivot;
        public Transform Abdomen;
        public Transform Head;
        public Transform Jaw;
        public Transform Tail;
        public Transform FangL;
        public Transform FangR;
        public Transform WingL;
        public Transform WingR;
        public Transform WristL;
        public Transform WristR;
        public Transform Mouth;
        public readonly List<Leg> Legs = new List<Leg>();

        private CharacterAttackAnimator attack;
        private Stagger stagger;

        private Vector3 lastPosition;
        private float speed;
        private float verticalSpeed;
        private float moveBlend;
        private float airBlend;
        private float crouchBlend;
        private float crouchUntil = -1f;
        private float phase;
        private float seed;

        private Vector3 bodyBase;
        private Quaternion bodyBaseRotation;
        private Vector3 headBase;
        private Quaternion tailBase;
        private Quaternion wingLBase;
        private Quaternion wingRBase;
        private Vector3 jawBaseScale;

        // Death: every animated part eases from where it was to where it lies.
        private struct Settle
        {
            public Transform T;
            public Vector3 FromPos, ToPos;
            public Quaternion FromRot, ToRot;
            public Vector3 FromScale, ToScale;
        }

        private readonly List<Settle> settles = new List<Settle>();
        private bool dead;
        private float deathTimer;
        private const float DeathSeconds = 0.75f;

        public bool IsDead => dead;

        private void Start()
        {
            attack = GetComponent<CharacterAttackAnimator>();
            stagger = GetComponentInParent<Stagger>();
            lastPosition = transform.position;
        }

        /// <summary>Records the built rest pose (called by the builder once the body is complete).</summary>
        public void CaptureRest()
        {
            seed = Random.value * 100f;
            lastPosition = transform.position;

            if (BodyPivot != null)
            {
                bodyBase = BodyPivot.localPosition;
                bodyBaseRotation = BodyPivot.localRotation;
            }
            if (Head != null)
                headBase = Head.localPosition;
            if (Tail != null)
                tailBase = Tail.localRotation;
            if (WingL != null)
                wingLBase = WingL.localRotation;
            if (WingR != null)
                wingRBase = WingR.localRotation;
            if (Jaw != null)
                jawBaseScale = Jaw.localScale;
        }

        /// <summary>Gathers itself to jump (a leap's wind-up) for this long.</summary>
        public void Crouch(float seconds)
        {
            crouchUntil = Time.time + seconds;
        }

        // ------------------------------------------------------------------ per frame

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            if (dead)
            {
                UpdateDeath(dt);
                return;
            }

            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;
            float vy = delta.y / dt;
            delta.y = 0f;

            // A teleport (blink, gate, a spectator's first snapshot) is not a burst of speed.
            float instant = delta.magnitude / dt;
            if (instant > 40f)
                instant = 0f;
            speed = Mathf.Lerp(speed, instant, 1f - Mathf.Exp(-10f * dt));
            verticalSpeed = Mathf.Lerp(verticalSpeed, Mathf.Abs(vy) > 40f ? 0f : vy, 1f - Mathf.Exp(-14f * dt));

            moveBlend = Mathf.MoveTowards(moveBlend, speed > 0.3f ? 1f : 0f, 5f * dt);
            airBlend = Mathf.MoveTowards(airBlend, Mathf.Abs(verticalSpeed) > 1.5f ? 1f : 0f, 6f * dt);
            crouchBlend = Mathf.MoveTowards(crouchBlend, Time.time < crouchUntil ? 1f : 0f, 6f * dt);

            AttackCurves(out float windUp, out float lunge, out float bite);
            float recoil = stagger != null ? stagger.RecoilFraction : 0f;
            float t = Time.time + seed;

            switch (Body)
            {
                case CreatureBody.Spider:
                case CreatureBody.Beetle:
                    AnimateInsect(dt, t, windUp, lunge, bite, recoil);
                    break;
                case CreatureBody.Wolf:
                    AnimateWolf(dt, t, windUp, lunge, bite, recoil);
                    break;
                case CreatureBody.Slime:
                    AnimateSlime(dt, t, windUp, lunge, recoil);
                    break;
                case CreatureBody.Bat:
                    AnimateBat(dt, t, windUp, lunge, bite, recoil);
                    break;
                case CreatureBody.Briarbound:
                case CreatureBody.GraveSiren:
                case CreatureBody.RimeStalker:
                case CreatureBody.Hollowmaw:
                case CreatureBody.BarrowCastellan:
                    AnimateRevenant(dt, t, windUp, lunge, bite, recoil);
                    break;
                case CreatureBody.CinderPenitent:
                    AnimatePenitent(dt, t, windUp, lunge, recoil);
                    break;
            }
        }

        // The attack, as three curves over the swing the attack animator is timing:
        // windUp rises to 1 as it gathers itself, lunge takes over just before the strike frame and
        // fades through the recovery, and bite holds the jaws open until they snap shut on the strike.
        private void AttackCurves(out float windUp, out float lunge, out float bite)
        {
            windUp = lunge = bite = 0f;
            if (attack == null || !attack.IsAttacking)
                return;

            float f = attack.Progress;
            float strike = Mathf.Clamp(attack.StrikeFraction, 0.1f, 0.9f);
            float gather = strike * 0.7f;
            if (f < gather)
            {
                windUp = Smooth(f / gather);
            }
            else if (f < strike)
            {
                float k = (f - gather) / (strike - gather);
                windUp = 1f - k;
                lunge = Smooth(k);
            }
            else
            {
                lunge = 1f - Smooth((f - strike) / (1f - strike));
            }

            bite = f < strike ? Smooth(f / strike) : Mathf.Max(0f, 1f - (f - strike) / 0.08f);
        }

        // ------------------------------------------------------------------ spider / beetle

        private void AnimateInsect(float dt, float t, float windUp, float lunge, float bite, float recoil)
        {
            bool beetle = Body == CreatureBody.Beetle;
            float stride = beetle ? 0.9f : 1.25f;
            phase += speed * dt / stride * Mathf.PI * 2f;

            for (int i = 0; i < Legs.Count; i++)
            {
                Leg leg = Legs[i];
                float gp = phase + (leg.Group == 0 ? 0f : Mathf.PI) + i * 0.25f;
                float swing = Mathf.Sin(gp) * 18f * moveBlend;
                float lift = Mathf.Max(0f, Mathf.Cos(gp)) * 26f * moveBlend;

                // Idle: legs shift their weight now and then.
                swing += Mathf.Sin(t * 1.3f + i * 1.7f) * 3f * (1f - moveBlend);

                bool front = i < 2; // the first pair built is the front pair
                if (front)
                {
                    lift += windUp * 55f + lunge * 25f;
                    swing += lunge * 20f;
                }

                // A leap: legs thrown out wide, knees straightened; gathering: knees folded.
                lift += airBlend * 14f - crouchBlend * 12f;
                float kneeOpen = airBlend * 35f - crouchBlend * 15f - lift * 0.55f;

                // Forward is decreasing yaw on the right, increasing on the left.
                float yaw = leg.Yaw - leg.Side * swing;
                leg.Hip.localRotation = Quaternion.Euler(0f, yaw, leg.Lift + lift);
                leg.Knee.localRotation = Quaternion.Euler(0f, 0f, leg.Bend + kneeOpen);
            }

            if (BodyPivot != null)
            {
                float bob = Mathf.Abs(Mathf.Sin(phase * 2f)) * 0.035f * moveBlend + Mathf.Sin(t * 2f) * 0.01f;
                Vector3 pos = bodyBase + new Vector3(0f,
                    bob - crouchBlend * 0.16f + windUp * 0.08f - lunge * 0.04f,
                    lunge * (beetle ? 0.2f : 0.38f) - windUp * 0.08f - recoil * 0.18f);
                float pitch = -windUp * (beetle ? 14f : 20f) + lunge * 14f - recoil * 16f
                              + Mathf.Clamp(-verticalSpeed * 3f, -22f, 22f) * airBlend;
                float sway = Mathf.Sin(phase) * 3f * moveBlend;
                BodyPivot.localPosition = pos;
                BodyPivot.localRotation = bodyBaseRotation * Quaternion.Euler(pitch, sway, -sway * 0.6f);
            }

            if (Abdomen != null)
            {
                // Breathing, and the abdomen bouncing along behind the legs.
                float breathe = Mathf.Sin(t * 2.1f) * (beetle ? 2f : 4f);
                float jounce = Mathf.Sin(phase * 2f + 1f) * 5f * moveBlend;
                Abdomen.localRotation = Quaternion.Euler(breathe + jounce + windUp * (beetle ? -10f : 8f), 0f, 0f);
                float puff = 1f + Mathf.Sin(t * 2.1f) * 0.03f;
                Abdomen.localScale = new Vector3(puff, puff, 1f);
            }

            if (Head != null)
            {
                float look = Mathf.Sin(t * 0.7f) * 8f * (1f - moveBlend);
                Head.localRotation = Quaternion.Euler(-windUp * 18f + lunge * 22f, look, 0f);
                Head.localPosition = headBase + new Vector3(0f, 0f, lunge * 0.08f);
            }

            // Fangs chatter a little at rest, spread on the wind-up and snap shut on the bite.
            float open = Mathf.Max(bite, 0.25f * Mathf.Max(0f, Mathf.Sin(t * 1.1f)) * (1f + Mathf.Sin(t * 14f)) * 0.5f);
            if (FangR != null)
                FangR.localRotation = Quaternion.Euler(-open * 25f, open * 30f, 0f);
            if (FangL != null)
                FangL.localRotation = Quaternion.Euler(-open * 25f, -open * 30f, 0f);
        }

        // ------------------------------------------------------------------ wolf

        private void AnimateWolf(float dt, float t, float windUp, float lunge, float bite, float recoil)
        {
            // Lopes once it's moving fast, so the cadence stays sensible over its speed range.
            float stride = Mathf.Lerp(1.5f, 2.3f, Mathf.InverseLerp(3f, 7f, speed));
            phase += speed * dt / stride * Mathf.PI * 2f;

            foreach (Leg leg in Legs)
            {
                float gp = phase + leg.Group * Mathf.PI;
                float swing = Mathf.Sin(gp) * 30f * moveBlend;
                float fold = Mathf.Max(0f, Mathf.Cos(gp)) * 50f * moveBlend;

                float hip = -swing;
                float knee;
                if (leg.Front)
                {
                    hip -= lunge * 45f - windUp * 10f;
                    hip -= airBlend * 45f;
                    hip += crouchBlend * 15f;
                    knee = fold + windUp * 25f + crouchBlend * 35f - airBlend * 10f;
                }
                else
                {
                    // Hind legs stand angled: thigh forward, hock back.
                    hip += -10f + airBlend * 50f - crouchBlend * 30f - windUp * 12f;
                    knee = 18f - fold + crouchBlend * 50f + windUp * 20f;
                }
                leg.Hip.localRotation = Quaternion.Euler(hip, 0f, 0f);
                leg.Knee.localRotation = Quaternion.Euler(knee, 0f, 0f);
            }

            if (BodyPivot != null)
            {
                float bob = Mathf.Sin(phase * 2f) * 0.035f * moveBlend;
                float breathe = Mathf.Sin(t * 2.4f) * 0.008f * (1f - moveBlend);
                Vector3 pos = bodyBase + new Vector3(0f,
                    bob + breathe - windUp * 0.12f - crouchBlend * 0.2f,
                    lunge * 0.55f - windUp * 0.12f - recoil * 0.15f);
                float pitch = Mathf.Sin(phase * 2f + 0.6f) * 2.5f * moveBlend + windUp * 6f - lunge * 4f - recoil * 9f
                              + Mathf.Clamp(-verticalSpeed * 4f, -25f, 25f) * airBlend + crouchBlend * 4f;
                BodyPivot.localPosition = pos;
                BodyPivot.localRotation = bodyBaseRotation * Quaternion.Euler(pitch, 0f, 0f);
            }

            if (Head != null)
            {
                // Looks round when idle; snarls (head low) while winding up, thrusts in on the bite.
                float look = (Mathf.Sin(t * 0.45f) * 22f + Mathf.Sin(t * 1.3f) * 6f) * (1f - moveBlend) * (1f - windUp);
                float nod = Mathf.Sin(phase * 2f) * 5f * moveBlend;
                float pitch = nod + windUp * 14f - lunge * 6f - recoil * 18f + Mathf.Sin(t * 0.8f) * 4f * (1f - moveBlend);
                Head.localRotation = Quaternion.Euler(pitch, look, 0f);
                Head.localPosition = headBase + new Vector3(0f, -windUp * 0.05f, lunge * 0.12f);
            }

            if (Jaw != null)
            {
                // Panting while it runs, wide open for the bite.
                float pant = (2f + Mathf.Sin(t * 11f) * 3f) * moveBlend;
                Jaw.localRotation = Quaternion.Euler(Mathf.Max(0f, pant) + bite * 42f, 0f, 0f);
            }

            if (Tail != null)
            {
                float wagSpeed = moveBlend > 0.5f ? 9f : 3f;
                float wag = Mathf.Sin(t * wagSpeed) * Mathf.Lerp(16f, 8f, moveBlend);
                float raise = moveBlend * 12f + windUp * 25f - recoil * 20f;
                Tail.localRotation = tailBase * Quaternion.Euler(raise, wag, 0f);
            }
        }

        // ------------------------------------------------------------------ slime

        private void AnimateSlime(float dt, float t, float windUp, float lunge, float recoil)
        {
            if (BodyPivot == null)
                return;

            const float hopLength = 1.3f;
            phase += speed * dt / hopLength;
            float p = phase - Mathf.Floor(phase);
            float arc = Mathf.Sin(p * Mathf.PI);

            float height = arc * 0.32f * moveBlend;
            float squash = (1f - arc) * (1f - arc) * 0.28f * moveBlend;
            float stretch = arc * 0.14f * moveBlend;

            float jiggle = Mathf.Sin(t * 3.1f) * 0.05f + Mathf.Sin(t * 7.3f) * 0.015f;
            float sy = 1f + jiggle - squash + stretch - windUp * 0.38f + lunge * 0.32f - recoil * 0.25f
                       + Mathf.Clamp(Mathf.Abs(verticalSpeed) * 0.04f, 0f, 0.3f) * airBlend - crouchBlend * 0.35f;
            sy = Mathf.Max(0.3f, sy);
            float sxz = 1f / Mathf.Sqrt(sy);

            BodyPivot.localScale = new Vector3(sxz, sy, sxz);
            BodyPivot.localPosition = bodyBase + new Vector3(0f, height + lunge * 0.22f, lunge * 0.55f - recoil * 0.12f);
            BodyPivot.localRotation = bodyBaseRotation * Quaternion.Euler(moveBlend * 6f + lunge * 14f - recoil * 10f,
                Mathf.Sin(t * 1.7f) * 4f, Mathf.Sin(t * 2.3f) * 3f);

            if (Jaw != null)
                Jaw.localScale = new Vector3(jawBaseScale.x * (1f + windUp * 0.3f), jawBaseScale.y * (1f + windUp * 3f + Mathf.Max(0f, Mathf.Sin(t * 2.7f)) * 0.6f), jawBaseScale.z);
        }

        // ------------------------------------------------------------------ bat

        private void AnimateBat(float dt, float t, float windUp, float lunge, float bite, float recoil)
        {
            float freq = Mathf.Lerp(6f, 8.5f, moveBlend) + windUp * 3f;
            phase += dt * freq * Mathf.PI * 2f;
            float flap = Mathf.Sin(phase);
            float wingAngle = flap * 55f + 12f + windUp * 25f - lunge * 30f;
            float wristAngle = Mathf.Sin(phase - 0.9f) * 38f;

            if (WingL != null)
                WingL.localRotation = wingLBase * Quaternion.Euler(0f, 0f, wingAngle);
            if (WingR != null)
                WingR.localRotation = wingRBase * Quaternion.Euler(0f, 0f, wingAngle);
            if (WristL != null)
                WristL.localRotation = Quaternion.Euler(0f, 0f, wristAngle);
            if (WristR != null)
                WristR.localRotation = Quaternion.Euler(0f, 0f, wristAngle);

            if (BodyPivot != null)
            {
                float hover = Mathf.Sin(t * 2.2f) * 0.14f - flap * 0.05f;
                Vector3 pos = bodyBase + new Vector3(Mathf.Sin(t * 1.3f) * 0.08f * (1f - moveBlend),
                    hover + windUp * 0.4f - lunge * 0.85f + recoil * 0.2f,
                    lunge * 0.7f - recoil * 0.3f);
                float pitch = moveBlend * 18f - windUp * 25f + lunge * 50f - recoil * 30f;
                float roll = Mathf.Sin(t * 1.6f) * 8f * (1f - moveBlend);
                BodyPivot.localPosition = pos;
                BodyPivot.localRotation = bodyBaseRotation * Quaternion.Euler(pitch, 0f, roll);
            }

            if (Head != null)
                Head.localRotation = Quaternion.Euler(-bite * 20f, Mathf.Sin(t * 1.9f) * 15f * (1f - moveBlend), 0f);
        }

        private void AnimatePenitent(float dt, float t, float windUp, float lunge, float recoil)
        {
            windUp = Mathf.Max(windUp, crouchBlend);
            phase += speed * dt * 3.5f;
            foreach (Leg leg in Legs)
            {
                float crawl = Mathf.Sin(phase + (leg.Group == 0 ? 0f : Mathf.PI)) * moveBlend;
                leg.Hip.localRotation = Quaternion.Euler(leg.Front ? -35f + crawl * 24f - windUp * 40f + lunge * 30f : -65f + crawl * 6f,
                    0f, leg.Side * (leg.Front ? 12f : 10f));
                leg.Knee.localRotation = Quaternion.Euler(leg.Front ? -30f - Mathf.Max(0f, crawl) * 25f : 110f, 0f, 0f);
            }
            BodyPivot.localPosition = bodyBase + new Vector3(0f, Mathf.Abs(Mathf.Sin(phase)) * 0.04f * moveBlend, lunge * 0.2f - recoil * 0.12f);
            BodyPivot.localRotation = bodyBaseRotation * Quaternion.Euler(12f - windUp * 15f + lunge * 20f, 0f, Mathf.Sin(phase) * moveBlend * 6f);
            if (Head != null) Head.localRotation = Quaternion.Euler(18f - windUp * 25f, Mathf.Sin(t * 0.7f) * 5f, 0f);
        }

        private void AnimateRevenant(float dt, float t, float windUp, float lunge, float bite, float recoil)
        {
            windUp = Mathf.Max(windUp, crouchBlend);
            bite = Mathf.Max(bite, crouchBlend);
            bool siren = Body == CreatureBody.GraveSiren;
            bool stalker = Body == CreatureBody.RimeStalker || Body == CreatureBody.Hollowmaw;
            bool castellan = Body == CreatureBody.BarrowCastellan;
            phase += speed * dt * (stalker ? 4.5f : 2.6f);
            foreach (Leg leg in Legs)
            {
                float stride = Mathf.Sin(phase + (leg.Group == 0 ? 0f : Mathf.PI)) * moveBlend;
                float pitch = leg.Front
                    ? -12f - stride * 12f - windUp * 65f + lunge * 40f - recoil * 20f
                    : -10f + stride * (stalker ? 34f : 22f);
                if (siren) pitch = -35f - windUp * 45f + Mathf.Sin(t * 1.5f + leg.Side) * 8f;
                if (castellan && leg.Front) pitch = -20f - stride * 6f - windUp * 12f + lunge * 40f;
                leg.Hip.localRotation = Quaternion.Euler(pitch, 0f, leg.Lift + (leg.Front ? Mathf.Sin(t * 1.8f) * 5f : 0f));
                leg.Knee.localRotation = Quaternion.Euler(leg.Bend + (leg.Front ? -windUp * 20f : Mathf.Max(0f, -stride) * 28f), 0f, 0f);
            }
            if (BodyPivot != null)
            {
                float bob = siren ? Mathf.Sin(t * 1.8f) * 0.09f : Mathf.Abs(Mathf.Sin(phase)) * 0.045f * moveBlend;
                BodyPivot.localPosition = bodyBase + new Vector3(0f, bob - windUp * 0.05f, lunge * 0.24f - recoil * 0.15f);
                BodyPivot.localRotation = bodyBaseRotation * Quaternion.Euler((stalker ? 18f : 3f) + moveBlend * 5f - windUp * 12f + lunge * 18f,
                    Mathf.Sin(phase) * moveBlend * 5f, Mathf.Sin(t * 1.4f) * (siren ? 5f : 2f));
            }
            if (Head != null)
                Head.localRotation = Quaternion.Euler(-windUp * 20f + lunge * 12f, Mathf.Sin(t * 0.8f) * 8f * (1f - moveBlend), 0f);
            if (Jaw != null)
                Jaw.localRotation = Quaternion.Euler(bite * (siren ? 35f : 20f) + (siren ? 8f + Mathf.Sin(t * 2f) * 5f : 0f), 0f, 0f);
            if (Tail != null)
                Tail.localRotation = tailBase * Quaternion.Euler(Mathf.Sin(t * 1.7f) * 8f + moveBlend * 15f, 0f, Mathf.Sin(t * 1.3f) * 6f);
        }

        // ------------------------------------------------------------------ death

        /// <summary>
        /// Plays this body's death and leaves it lying there; <paramref name="instant"/> skips
        /// straight to the end (a corpse a spectator first sees already dead).
        /// </summary>
        public void PlayDeath(bool instant)
        {
            if (dead)
                return;
            dead = true;
            deathTimer = instant ? DeathSeconds : 0f;

            if (attack == null)
                attack = GetComponent<CharacterAttackAnimator>();
            if (attack != null)
                attack.enabled = false;

            settles.Clear();
            switch (Body)
            {
                case CreatureBody.Briarbound:
                case CreatureBody.GraveSiren:
                case CreatureBody.RimeStalker:
                case CreatureBody.CinderPenitent:
                case CreatureBody.Hollowmaw:
                case CreatureBody.BarrowCastellan:
                    AddSettle(BodyPivot, new Vector3(bodyBase.x, 0.22f, bodyBase.z), Quaternion.Euler(78f, 15f, 12f));
                    foreach (Leg leg in Legs)
                    {
                        AddSettle(leg.Hip, leg.Hip.localPosition, Quaternion.Euler(leg.Front ? -35f : 20f, 0f, leg.Side * 25f));
                        AddSettle(leg.Knee, leg.Knee.localPosition, Quaternion.Euler(leg.Front ? -60f : 55f, 0f, 0f));
                    }
                    if (Head != null) AddSettle(Head, headBase, Quaternion.Euler(25f, 25f, 0f));
                    break;
                case CreatureBody.Spider:
                case CreatureBody.Beetle:
                {
                    // Flips onto its back, legs curling up.
                    float rest = Body == CreatureBody.Spider ? 0.52f : 0.46f;
                    AddSettle(BodyPivot, new Vector3(bodyBase.x, rest, bodyBase.z), Quaternion.Euler(0f, Random.Range(-40f, 40f), 180f));
                    foreach (Leg leg in Legs)
                    {
                        AddSettle(leg.Hip, leg.Hip.localPosition, Quaternion.Euler(0f, leg.Yaw + leg.Side * Random.Range(-10f, 10f), -25f + Random.Range(-10f, 10f)));
                        AddSettle(leg.Knee, leg.Knee.localPosition, Quaternion.Euler(0f, 0f, -140f + Random.Range(-15f, 10f)));
                    }
                    if (FangL != null) AddSettle(FangL, FangL.localPosition, Quaternion.Euler(-20f, -25f, 0f));
                    if (FangR != null) AddSettle(FangR, FangR.localPosition, Quaternion.Euler(-20f, 25f, 0f));
                    break;
                }
                case CreatureBody.Wolf:
                {
                    // Falls onto its side, legs stiff, jaw slack.
                    float side = Random.value < 0.5f ? 1f : -1f;
                    AddSettle(BodyPivot, new Vector3(bodyBase.x, 0.26f, bodyBase.z), Quaternion.Euler(0f, 0f, 88f * side));
                    foreach (Leg leg in Legs)
                    {
                        AddSettle(leg.Hip, leg.Hip.localPosition, Quaternion.Euler(leg.Front ? -28f : 30f, 0f, 0f));
                        AddSettle(leg.Knee, leg.Knee.localPosition, Quaternion.Euler(leg.Front ? 15f : -10f, 0f, 0f));
                    }
                    if (Head != null) AddSettle(Head, headBase, Quaternion.Euler(12f, 0f, 0f));
                    if (Jaw != null) AddSettle(Jaw, Jaw.localPosition, Quaternion.Euler(22f, 0f, 0f));
                    if (Tail != null) AddSettle(Tail, Tail.localPosition, tailBase * Quaternion.Euler(15f, 0f, 0f));
                    break;
                }
                case CreatureBody.Slime:
                    // A puddle.
                    AddSettle(BodyPivot, bodyBase, bodyBaseRotation, new Vector3(1.7f, 0.16f, 1.7f));
                    if (Jaw != null) AddSettle(Jaw, Jaw.localPosition, Jaw.localRotation, jawBaseScale);
                    break;
                case CreatureBody.Bat:
                    // Drops out of the air onto its back, wings spread.
                    AddSettle(BodyPivot, new Vector3(bodyBase.x, 0.2f, bodyBase.z + 0.3f), Quaternion.Euler(-10f, Random.Range(-30f, 30f), 175f));
                    if (WingL != null) AddSettle(WingL, WingL.localPosition, wingLBase * Quaternion.Euler(0f, 0f, -8f));
                    if (WingR != null) AddSettle(WingR, WingR.localPosition, wingRBase * Quaternion.Euler(0f, 0f, -8f));
                    if (WristL != null) AddSettle(WristL, WristL.localPosition, Quaternion.Euler(0f, 0f, -15f));
                    if (WristR != null) AddSettle(WristR, WristR.localPosition, Quaternion.Euler(0f, 0f, -15f));
                    break;
            }

            if (instant)
                UpdateDeath(0f);
        }

        private void AddSettle(Transform t, Vector3 toPos, Quaternion toRot, Vector3? toScale = null)
        {
            if (t == null)
                return;
            settles.Add(new Settle
            {
                T = t,
                FromPos = t.localPosition,
                ToPos = toPos,
                FromRot = t.localRotation,
                ToRot = toRot,
                FromScale = t.localScale,
                ToScale = toScale ?? t.localScale
            });
        }

        private void UpdateDeath(float dt)
        {
            if (deathTimer > DeathSeconds && dt > 0f)
                return;
            deathTimer += dt;
            float f = Mathf.Clamp01(deathTimer / DeathSeconds);

            // Bats fall (accelerating); bugs hop as they flip; the rest just slump.
            float fall = Body == CreatureBody.Bat ? f * f : Smooth(f);
            float hop = Body == CreatureBody.Spider || Body == CreatureBody.Beetle ? Mathf.Sin(f * Mathf.PI) * 0.35f : 0f;
            float turn = Smooth(f);

            for (int i = 0; i < settles.Count; i++)
            {
                Settle s = settles[i];
                if (s.T == null)
                    continue;
                Vector3 pos = Vector3.Lerp(s.FromPos, s.ToPos, fall);
                if (s.T == BodyPivot)
                    pos.y += hop;
                s.T.localPosition = pos;
                s.T.localRotation = Quaternion.Slerp(s.FromRot, s.ToRot, turn);
                s.T.localScale = Vector3.Lerp(s.FromScale, s.ToScale, turn);
            }

            if (f >= 1f)
                deathTimer = DeathSeconds + 1f;
        }

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }
    }
}
