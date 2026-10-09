using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.Visuals
{
    /// <summary>
    /// The field bosses' bodies: their idle life, walk, plain blow and death, plus the poses their
    /// big moves play (<see cref="Perform"/>), told by the boss's move code (BossAbilities).
    /// </summary>
    public partial class CreatureAnimator
    {
        /// <summary>A pose a boss's move holds for a while; each body reads it its own way.</summary>
        public enum BossAct
        {
            None,
            Raise,  // arms (or weapon) up over the head
            Slam,   // up, then down hard at the end
            Throw,  // the right arm back, then thrown forward at the end
            Spin,   // arms out to the sides
            Cast,   // arms forward and up, leaning back
            Shake,  // shaking itself (the boar's thicket)
            Paw,    // head low, pawing the ground (a charge's wind-up)
            Charge, // head down, running
            Toll,   // the hammer swung back into the bell
            Roar,   // head up, arms spread
            Thrust  // a spear thrust forward
        }

        /// <summary>The thing it carries that a move may use (a coffin, a bell, a spear, the sun orb).</summary>
        public Transform Prop;

        /// <summary>Small parts that circle the body (Rimeheart's shards).</summary>
        public readonly List<Transform> Orbit = new List<Transform>();

        /// <summary>How far it has sunk into the ground, 0 to 1 (burrowing, an idol sinking).</summary>
        public float Sunk { get; set; }

        private BossAct act;
        private float actStart, actEnd = -1f;
        private float actBlend;
        private Vector3 modelBase;
        private Vector3 propBaseScale = Vector3.one;
        private Vector3 coreBaseScale = Vector3.one;

        public BossAct CurrentAct => Time.time < actEnd ? act : BossAct.None;

        /// <summary>Holds a move's pose for this long.</summary>
        public void Perform(BossAct pose, float seconds)
        {
            act = pose;
            actStart = Time.time;
            actEnd = Time.time + Mathf.Max(0.05f, seconds);
        }

        public void EndAct()
        {
            actEnd = Mathf.Min(actEnd, Time.time);
        }

        private float ActProgress => act == BossAct.None ? 0f : Mathf.Clamp01((Time.time - actStart) / Mathf.Max(0.01f, actEnd - actStart));

        private bool IsBossBody => Body >= CreatureBody.Gravedigger;

        private void CaptureBossRest()
        {
            modelBase = transform.localPosition;
            if (Prop != null)
                propBaseScale = Prop.localScale;
            if (Jaw != null)
                coreBaseScale = Jaw.localScale;
        }

        private void UpdateBossAct(float dt)
        {
            if (!IsBossBody)
                return;
            actBlend = Mathf.MoveTowards(actBlend, Time.time < actEnd && act != BossAct.None ? 1f : 0f, (Time.time < actEnd ? 8f : 4f) * dt);
            // Sinking moves the whole model (its plinth too) down into the ground.
            transform.localPosition = modelBase + Vector3.down * Sunk * (Body == CreatureBody.SunIdol ? 3.2f : 2.6f);
        }

        private float ActWeight(BossAct pose) => act == pose ? actBlend : 0f;

        private void AnimateBoss(float dt, float t, float windUp, float lunge, float bite, float recoil)
        {
            switch (Body)
            {
                case CreatureBody.FrostHeart:
                    AnimateHeart(dt, t, windUp, lunge, recoil);
                    break;
                case CreatureBody.Boar:
                    AnimateWolf(dt, t, windUp, lunge, bite, recoil);
                    AnimateBoarExtras(t);
                    break;
                case CreatureBody.SunIdol:
                    AnimateIdol(dt, t, windUp, lunge, recoil);
                    break;
                default:
                    AnimateBiped(dt, t, windUp, lunge, bite, recoil);
                    break;
            }
        }

        // ------------------------------------------------------------------ two legs, two arms

        private void AnimateBiped(float dt, float t, float windUp, float lunge, float bite, float recoil)
        {
            windUp = Mathf.Max(windUp, crouchBlend);
            bool goblin = Body == CreatureBody.TunnelKing;
            bool ringer = Body == CreatureBody.BellRinger;
            bool armour = Body == CreatureBody.HollowArmor;
            float hunch = Body == CreatureBody.Gravedigger ? 16f : ringer ? 26f : goblin ? 14f : 3f;
            phase += speed * dt * (goblin ? 5.2f : armour ? 2.1f : 2.8f);

            float p = ActProgress;
            float raise = ActWeight(BossAct.Raise);
            float slam = ActWeight(BossAct.Slam);
            float slamUp = slam * (p < 0.7f ? Mathf.SmoothStep(0f, 1f, p / 0.7f) : 1f - Mathf.SmoothStep(0f, 1f, (p - 0.7f) / 0.15f));
            float slamDown = slam * (p < 0.7f ? 0f : Mathf.SmoothStep(0f, 1f, (p - 0.7f) / 0.15f));
            float thr = ActWeight(BossAct.Throw);
            float throwBack = thr * (p < 0.75f ? Mathf.SmoothStep(0f, 1f, p / 0.5f) : 0f);
            float throwOut = thr * (p < 0.75f ? 0f : 1f);
            float spin = ActWeight(BossAct.Spin);
            float cast = ActWeight(BossAct.Cast);
            float roar = ActWeight(BossAct.Roar);
            float toll = ActWeight(BossAct.Toll);
            float tollBack = toll * (p < 0.7f ? Mathf.SmoothStep(0f, 1f, p / 0.6f) : 1f - Mathf.SmoothStep(0f, 1f, (p - 0.7f) / 0.12f));
            float tollHit = toll * (p < 0.7f ? 0f : 1f);
            float thrust = ActWeight(BossAct.Thrust);
            float thrustBack = thrust * (p < 0.6f ? Mathf.SmoothStep(0f, 1f, p / 0.6f) : 0f);
            float thrustOut = thrust * (p < 0.6f ? 0f : 1f);

            foreach (Leg leg in Legs)
            {
                float stride = Mathf.Sin(phase + (leg.Group == 0 ? 0f : Mathf.PI)) * moveBlend;
                if (!leg.Front)
                {
                    float legPitch = -10f + stride * (goblin ? 40f : 26f) - crouchBlend * 30f - slamDown * 20f;
                    leg.Hip.localRotation = Quaternion.Euler(legPitch, 0f, leg.Lift);
                    leg.Knee.localRotation = Quaternion.Euler(leg.Bend + Mathf.Max(0f, -stride) * 30f + crouchBlend * 45f + slamDown * 35f, 0f, 0f);
                    continue;
                }

                bool right = leg.Side > 0;
                float pitch = -12f - stride * 14f + Mathf.Sin(t * 1.6f + leg.Side) * 3f;
                float roll = leg.Lift;
                float elbow = leg.Bend;

                // The plain blow: the right arm (both, for the armour's greatsword) winds up and swings.
                if (right || armour)
                {
                    pitch += -windUp * 120f + lunge * 70f;
                    elbow += -windUp * 30f;
                }
                else
                {
                    pitch += -windUp * 25f + lunge * 10f;
                }
                pitch += -raise * 150f;
                pitch += -slamUp * 165f + slamDown * -25f;
                if (right)
                {
                    pitch += throwBack * 70f - throwOut * 120f;
                    pitch += -tollBack * 160f + tollHit * 50f;
                    pitch += thrustBack * 35f - thrustOut * 85f;
                    elbow += -thrustBack * 50f;
                }
                roll += leg.Side * (spin * 70f + roar * 55f);
                pitch += -cast * 85f - roar * 35f;
                pitch -= recoil * 20f;
                leg.Hip.localRotation = Quaternion.Euler(pitch, 0f, roll);
                leg.Knee.localRotation = Quaternion.Euler(elbow, 0f, 0f);
            }

            if (BodyPivot != null)
            {
                float bob = Mathf.Abs(Mathf.Sin(phase)) * (goblin ? 0.07f : 0.045f) * moveBlend + Mathf.Sin(t * 1.7f) * 0.012f;
                if (armour)
                    bob += Mathf.Sin(t * 2.3f) * 0.03f;
                BodyPivot.localPosition = bodyBase + new Vector3(0f, bob - windUp * 0.05f - crouchBlend * 0.2f - slamDown * 0.12f,
                    lunge * 0.2f - recoil * 0.15f);
                float lean = hunch + moveBlend * 6f + lunge * 14f - windUp * 6f + slamDown * 22f - cast * 10f - roar * (hunch + 12f) + thrustOut * 12f;
                BodyPivot.localRotation = bodyBaseRotation * Quaternion.Euler(lean, Mathf.Sin(phase) * moveBlend * 5f + tollBack * 25f, Mathf.Sin(t * 1.3f) * 2f);
            }
            if (Head != null)
            {
                float look = Mathf.Sin(t * 0.8f) * 10f * (1f - moveBlend);
                Head.localRotation = Quaternion.Euler(-windUp * 12f + lunge * 10f - roar * 35f - hunch * 0.5f, look, goblin ? Mathf.Sin(t * 2.3f) * 6f : 0f);
                // The armour's flame (its "head") flickers.
                if (armour)
                    Head.localScale = new Vector3(1f + Mathf.Sin(t * 13f) * 0.06f, 1f + Mathf.Sin(t * 9f) * 0.12f + roar * 0.4f, 1f);
            }
            if (Jaw != null)
                Jaw.localRotation = Quaternion.Euler(bite * 22f + roar * 30f, 0f, 0f);
            if (Tail != null)
                Tail.localRotation = tailBase * Quaternion.Euler(Mathf.Sin(t * 1.7f) * 6f + moveBlend * 14f + crouchBlend * 10f, 0f, Mathf.Sin(t * 1.2f) * 5f);
            // The bell sways on its yoke, and jumps when struck.
            if (ringer && Prop != null)
                Prop.rotation = transform.rotation * Quaternion.Euler(Mathf.Sin(t * 1.4f) * 4f + Mathf.Sin(phase) * moveBlend * 6f - tollHit * 10f * Mathf.Sin(t * 40f), 0f,
                    Mathf.Sin(t * 1.1f) * 3f);
        }

        // ------------------------------------------------------------------ the heart of ice

        private void AnimateHeart(float dt, float t, float windUp, float lunge, float recoil)
        {
            float cast = ActWeight(BossAct.Cast);
            float roar = ActWeight(BossAct.Roar);
            float slam = ActWeight(BossAct.Slam);
            float p = ActProgress;
            phase += dt * (0.6f + cast * 2.5f + roar * 3f);
            if (BodyPivot != null)
            {
                float dip = slam * (p < 0.7f ? -Mathf.SmoothStep(0f, 0.35f, p / 0.7f) : Mathf.Lerp(-0.35f, 0.2f, (p - 0.7f) / 0.3f));
                BodyPivot.localPosition = bodyBase + new Vector3(0f, Mathf.Sin(t * 1.3f) * 0.12f + windUp * 0.15f - lunge * 0.1f + dip + cast * 0.15f,
                    lunge * 0.3f - recoil * 0.2f);
                BodyPivot.localRotation = bodyBaseRotation * Quaternion.Euler(moveBlend * 10f + lunge * 15f - windUp * 10f, 0f, Mathf.Sin(t * 0.9f) * 4f);
            }
            if (Abdomen != null)
                Abdomen.localRotation = Quaternion.Euler(0f, phase * 40f, 0f);
            if (Jaw != null)
            {
                // The inner core beats.
                float beat = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 2.4f)), 8f) * 0.18f + cast * 0.25f + windUp * 0.2f + roar * 0.35f;
                Jaw.localScale = coreBaseScale * (1f + beat);
            }
            if (Tail != null)
                Tail.localRotation = tailBase * Quaternion.Euler(Mathf.Sin(t * 1.1f) * 8f, 0f, Mathf.Sin(t * 0.8f) * 8f);
            for (int i = 0; i < Orbit.Count; i++)
            {
                Transform s = Orbit[i];
                if (s == null)
                    continue;
                float a = t * (0.9f + cast * 2f) + i * Mathf.PI * 2f / Orbit.Count;
                float r = 1.05f + roar * 0.6f + cast * 0.25f;
                s.localPosition = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(t * 1.7f + i) * 0.25f, Mathf.Sin(a) * r);
                s.localRotation = Quaternion.Euler(20f, -a * Mathf.Rad2Deg, 0f);
            }
        }

        // ------------------------------------------------------------------ the boar

        private void AnimateBoarExtras(float t)
        {
            float paw = ActWeight(BossAct.Paw);
            float shake = ActWeight(BossAct.Shake);
            float charge = ActWeight(BossAct.Charge);
            float roar = ActWeight(BossAct.Roar);
            if (BodyPivot != null)
                BodyPivot.localRotation = BodyPivot.localRotation * Quaternion.Euler(charge * 8f + paw * 6f - roar * 12f, 0f, shake * Mathf.Sin(t * 32f) * 14f);
            if (Head != null)
                Head.localRotation = Head.localRotation * Quaternion.Euler(paw * 22f + charge * 18f - roar * 30f + paw * Mathf.Sin(t * 7f) * 6f, shake * Mathf.Sin(t * 26f) * 12f, 0f);
            if (paw > 0f)
            {
                // The right foreleg scrapes the ground.
                foreach (Leg leg in Legs)
                {
                    if (!leg.Front || leg.Side < 0)
                        continue;
                    float scrape = Mathf.Sin(t * 9f);
                    leg.Hip.localRotation = Quaternion.Slerp(leg.Hip.localRotation, Quaternion.Euler(-25f + scrape * 35f, 0f, 0f), paw);
                    leg.Knee.localRotation = Quaternion.Slerp(leg.Knee.localRotation, Quaternion.Euler(30f + Mathf.Max(0f, scrape) * 30f, 0f, 0f), paw);
                }
            }
            if (Jaw != null)
                Jaw.localRotation = Jaw.localRotation * Quaternion.Euler(roar * 35f, 0f, 0f);
        }

        // ------------------------------------------------------------------ the idol

        private void AnimateIdol(float dt, float t, float windUp, float lunge, float recoil)
        {
            float cast = ActWeight(BossAct.Cast);
            float slam = ActWeight(BossAct.Slam);
            float roar = ActWeight(BossAct.Roar);
            float p = ActProgress;
            float slamUp = slam * (p < 0.7f ? Mathf.SmoothStep(0f, 1f, p / 0.7f) : 1f - Mathf.SmoothStep(0f, 1f, (p - 0.7f) / 0.15f));
            float slamDown = slam * (p < 0.7f ? 0f : 1f);

            if (BodyPivot != null)
            {
                BodyPivot.localPosition = bodyBase + new Vector3(0f, Mathf.Sin(t * 0.7f) * 0.01f - slamDown * 0.06f, lunge * 0.08f - recoil * 0.04f);
                BodyPivot.localRotation = bodyBaseRotation * Quaternion.Euler(lunge * 6f + slamDown * 8f - cast * 5f - roar * 6f, 0f, 0f);
            }
            int arm = 0;
            foreach (Leg leg in Legs)
            {
                // The first pair built holds the sun up; the second pair are the striking hands.
                bool upper = arm < 2;
                arm++;
                float pitch, elbow;
                if (upper)
                {
                    pitch = -150f + Mathf.Sin(t * 0.8f + leg.Side) * 3f - cast * 15f;
                    elbow = leg.Bend - cast * 20f;
                }
                else
                {
                    pitch = -20f - windUp * 100f + lunge * 60f - slamUp * 140f + slamDown * -10f - roar * 50f + Mathf.Sin(t * 0.9f + leg.Side) * 3f;
                    elbow = leg.Bend - windUp * 20f;
                }
                leg.Hip.localRotation = Quaternion.Euler(pitch, 0f, leg.Lift + leg.Side * roar * 30f);
                leg.Knee.localRotation = Quaternion.Euler(elbow, 0f, 0f);
            }
            if (Tail != null)
                Tail.localRotation = tailBase * Quaternion.Euler(0f, 0f, t * (12f + cast * 60f + roar * 90f));
            if (Prop != null)
            {
                Prop.localScale = propBaseScale * (1f + Mathf.Sin(t * 3f) * 0.05f + cast * 0.45f + roar * 0.3f);
                Prop.localRotation = Quaternion.Euler(t * 30f, t * 45f, 0f);
            }
            if (Head != null)
                Head.localRotation = Quaternion.Euler(-windUp * 6f + slamDown * 10f - roar * 15f, Mathf.Sin(t * 0.3f) * 4f, 0f);
        }

        // ------------------------------------------------------------------ death

        private void BossDeath()
        {
            Sunk = 0f;
            transform.localPosition = modelBase;
            switch (Body)
            {
                case CreatureBody.Boar:
                {
                    float side = Random.value < 0.5f ? 1f : -1f;
                    AddSettle(BodyPivot, new Vector3(bodyBase.x, bodyBase.y * 0.45f, bodyBase.z), Quaternion.Euler(0f, 0f, 85f * side));
                    foreach (Leg leg in Legs)
                    {
                        AddSettle(leg.Hip, leg.Hip.localPosition, Quaternion.Euler(leg.Front ? -28f : 30f, 0f, 0f));
                        AddSettle(leg.Knee, leg.Knee.localPosition, Quaternion.Euler(leg.Front ? 15f : -10f, 0f, 0f));
                    }
                    if (Head != null) AddSettle(Head, headBase, Quaternion.Euler(12f, 0f, 0f));
                    if (Jaw != null) AddSettle(Jaw, Jaw.localPosition, Quaternion.Euler(22f, 0f, 0f));
                    break;
                }
                case CreatureBody.FrostHeart:
                    // The heart drops out of the air and cracks; its shards fall round it.
                    AddSettle(BodyPivot, new Vector3(bodyBase.x, 0.45f, bodyBase.z), Quaternion.Euler(70f, 30f, 20f));
                    for (int i = 0; i < Orbit.Count; i++)
                    {
                        float a = i * Mathf.PI * 2f / Mathf.Max(1, Orbit.Count);
                        if (Orbit[i] != null)
                            AddSettle(Orbit[i], new Vector3(Mathf.Cos(a) * 1.3f, -1.6f, Mathf.Sin(a) * 1.3f), Quaternion.Euler(90f, a * Mathf.Rad2Deg, 0f));
                    }
                    break;
                case CreatureBody.SunIdol:
                    // The statue cracks and slumps forward on its plinth, the sun going out.
                    AddSettle(BodyPivot, bodyBase + new Vector3(0f, -0.35f, 0.1f), Quaternion.Euler(14f, 0f, 6f));
                    if (Head != null) AddSettle(Head, headBase + new Vector3(0.15f, -0.12f, 0.1f), Quaternion.Euler(35f, 20f, 15f));
                    foreach (Leg leg in Legs)
                        AddSettle(leg.Hip, leg.Hip.localPosition, Quaternion.Euler(-10f, 0f, leg.Side * 35f));
                    if (Prop != null) AddSettle(Prop, Prop.localPosition + Vector3.down * 0.6f, Prop.localRotation, Vector3.one * 0.05f);
                    break;
                default:
                    // Falls on its back (the bell-ringer onto its bell, forwards).
                    bool forward = Body == CreatureBody.BellRinger || Body == CreatureBody.TunnelKing;
                    AddSettle(BodyPivot, new Vector3(bodyBase.x, 0.3f, bodyBase.z), Quaternion.Euler(forward ? 80f : -78f, 15f, 10f));
                    foreach (Leg leg in Legs)
                    {
                        AddSettle(leg.Hip, leg.Hip.localPosition, Quaternion.Euler(leg.Front ? -35f : 20f, 0f, leg.Side * 25f));
                        AddSettle(leg.Knee, leg.Knee.localPosition, Quaternion.Euler(leg.Front ? -60f : 55f, 0f, 0f));
                    }
                    if (Head != null) AddSettle(Head, headBase, Quaternion.Euler(25f, 25f, 0f));
                    break;
            }
        }
    }
}
