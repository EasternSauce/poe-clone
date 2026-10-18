using System;
using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.Visuals
{
    /// <summary>
    /// One key of a field boss's keyframed move: the whole body's pose, relative to the built rest.
    /// Arm and leg angles are written for the right side and mirrored for the left (yaw and roll
    /// outward are positive on both). Arm pitch: 0 hangs down, -90 straight ahead, -180 overhead;
    /// elbows and knees bend from the rest by the given degrees (negative folds an elbow forward).
    /// Leg pitch is from upright in the world (for legs hung from the torso, its lean is taken back out), so a stance
    /// stays planted however far the body bends: negative steps a leg forward.
    /// </summary>
    public sealed class BossPose
    {
        public Vector3 Body, Shift, ArmR, ArmL, LegR, LegL, Head, Tool, Coat, PropAt, PropRot;
        public float ElbowR, ElbowL, KneeR, KneeL, Jaw;
        /// <summary>How firmly the left hand holds the weapon (bodies with an off-hand grip): 1 both hands, 0 the left arm free.</summary>
        public float TwoHands = 1f;
        public float PropGrip;

        public BossPose OneHand() { TwoHands = 0f; return this; }

        public BossPose Torso(float pitch, float yaw = 0f, float roll = 0f) { Body = new Vector3(pitch, yaw, roll); return this; }
        public BossPose Drop(float down, float forward = 0f) { Shift = new Vector3(0f, -down, forward); return this; }
        public BossPose R(float pitch, float yaw = 0f, float roll = 0f, float elbow = 0f) { ArmR = new Vector3(pitch, yaw, roll); ElbowR = elbow; return this; }
        public BossPose L(float pitch, float yaw = 0f, float roll = 0f, float elbow = 0f) { ArmL = new Vector3(pitch, yaw, roll); ElbowL = elbow; return this; }
        public BossPose Stance(float rightPitch, float rightKnee, float leftPitch, float leftKnee, float spread = 0f)
        {
            LegR = new Vector3(rightPitch, 0f, spread);
            LegL = new Vector3(leftPitch, 0f, spread);
            KneeR = rightKnee;
            KneeL = leftKnee;
            return this;
        }
        public BossPose Look(float pitch, float yaw = 0f, float roll = 0f) { Head = new Vector3(pitch, yaw, roll); return this; }
        public BossPose Mouth(float open) { Jaw = open; return this; }
        public BossPose Hold(float pitch, float yaw = 0f, float roll = 0f) { Tool = new Vector3(pitch, yaw, roll); return this; }
        public BossPose Tails(float pitch, float yaw = 0f, float roll = 0f) { Coat = new Vector3(pitch, yaw, roll); return this; }
        public BossPose Carry(Vector3 at, Vector3 euler) { PropAt = at; PropRot = euler; PropGrip = 1f; return this; }

        public static BossPose Lerp(BossPose a, BossPose b, float f)
        {
            return new BossPose
            {
                Body = Vector3.Lerp(a.Body, b.Body, f), Shift = Vector3.Lerp(a.Shift, b.Shift, f),
                ArmR = Vector3.Lerp(a.ArmR, b.ArmR, f), ArmL = Vector3.Lerp(a.ArmL, b.ArmL, f),
                LegR = Vector3.Lerp(a.LegR, b.LegR, f), LegL = Vector3.Lerp(a.LegL, b.LegL, f),
                Head = Vector3.Lerp(a.Head, b.Head, f), Tool = Vector3.Lerp(a.Tool, b.Tool, f),
                Coat = Vector3.Lerp(a.Coat, b.Coat, f),
                PropAt = Vector3.Lerp(a.PropAt, b.PropAt, f), PropRot = Vector3.Lerp(a.PropRot, b.PropRot, f),
                ElbowR = Mathf.Lerp(a.ElbowR, b.ElbowR, f), ElbowL = Mathf.Lerp(a.ElbowL, b.ElbowL, f),
                KneeR = Mathf.Lerp(a.KneeR, b.KneeR, f), KneeL = Mathf.Lerp(a.KneeL, b.KneeL, f),
                Jaw = Mathf.Lerp(a.Jaw, b.Jaw, f), TwoHands = Mathf.Lerp(a.TwoHands, b.TwoHands, f),
                PropGrip = Mathf.Lerp(a.PropGrip, b.PropGrip, f)
            };
        }
    }

    /// <summary>A field boss's keyframed move: poses at times (seconds at tempo 1), eased between.</summary>
    public sealed class BossClip
    {
        public string Name;
        public float[] Times;
        public BossPose[] Keys;
        /// <summary>A plain attack's clip: when its blow lands (lined up with the attack's strike).</summary>
        public float Strike;
        public float Duration => Times[Times.Length - 1];

        public BossClip StrikesAt(float time) { Strike = time; return this; }

        public static BossClip Make(string name, params object[] timesAndPoses)
        {
            var times = new List<float>();
            var keys = new List<BossPose>();
            for (int i = 0; i < timesAndPoses.Length; i += 2)
            {
                times.Add(Convert.ToSingle(timesAndPoses[i]));
                keys.Add((BossPose)timesAndPoses[i + 1]);
            }
            return new BossClip { Name = name, Times = times.ToArray(), Keys = keys.ToArray() };
        }

        public BossPose Sample(float t)
        {
            int k = 1;
            while (k < Times.Length - 1 && t > Times[k])
                k++;
            float t0 = Times[k - 1], t1 = Times[k];
            float f = t1 > t0 ? Mathf.Clamp01((t - t0) / (t1 - t0)) : 1f;
            return BossPose.Lerp(Keys[k - 1], Keys[k], f * f * (3f - 2f * f));
        }
    }

    public partial class CreatureAnimator
    {
        /// <summary>What the boss holds in its right hand (Mortis's shovel), posed by its clips.</summary>
        public Transform Tool;

        private Quaternion toolBase = Quaternion.identity;
        private Vector3 propBasePosition;
        private Quaternion propBaseRotation = Quaternion.identity;

        private BossClip clip;
        private float clipStart;
        private float clipSpeed = 1f;
        private float frozenAt = -1f;
        // How long a clip takes to take over the body, and to hand it back.
        private const float ClipBlendIn = 0.1f;
        private const float ClipBlendOut = 0.18f;

        public bool ClipPlaying => clip != null;

        /// <summary>Plays a keyframed move over the body's own animation, at this speed (the boss's tempo).</summary>
        public void PlayClip(BossClip c, float speed = 1f)
        {
            clip = c;
            clipStart = Time.time;
            clipSpeed = Mathf.Max(0.05f, speed);
            frozenAt = -1f;
        }

        /// <summary>Testing: holds a clip still this many seconds (tempo 1) in; Stop or Play lets go.</summary>
        public void FreezeClip(BossClip c, float at)
        {
            clip = c;
            frozenAt = Mathf.Clamp(at, 0f, c.Duration - 0.01f);
        }

        public void StopClip() => clip = null;

        private void CaptureClipRest()
        {
            if (Tool != null)
                toolBase = Tool.localRotation;
            if (Prop != null)
            {
                propBasePosition = Prop.localPosition;
                propBaseRotation = Prop.localRotation;
            }
        }

        /// <summary>Its plain attacks, one picked per swing (in place of the body's single built-in blow).</summary>
        [NonSerialized] public BossClip[] AttackClips;

        /// <summary>A point on the held weapon the left hand grips (Mortis's spade handle), or null.</summary>
        public Transform OffHand;
        /// <summary>Where, in the left forearm's space, the left hand holds things.</summary>
        public Vector3 OffHandGrip;

        private int attackClipCount = -1;
        private int attackClipIndex;

        // After the body's own animation: a move's clip (or a plain attack's), blended over it at
        // its start and end, then the left hand onto the weapon.
        private void ApplyClip()
        {
            float twoHands = 1f;
            float propGrip = 0f;
            if (clip != null)
            {
                float time = frozenAt >= 0f ? frozenAt : (Time.time - clipStart) * clipSpeed;
                if (time >= clip.Duration)
                {
                    clip = null;
                }
                else
                {
                    BossPose p = clip.Sample(time);
                    float w = frozenAt >= 0f ? 1f : Mathf.Clamp01(Mathf.Min(time / ClipBlendIn, (clip.Duration - time) / ClipBlendOut));
                    ApplyPose(p, w);
                    twoHands = Mathf.Lerp(1f, p.TwoHands, w);
                    propGrip = p.PropGrip * w;
                }
            }
            else if (AttackClips != null && AttackClips.Length > 0 && attack != null && attack.IsAttacking)
            {
                BossClip a = PickAttackClip();
                float f = attack.Progress;
                float strike = Mathf.Clamp(attack.StrikeFraction, 0.1f, 0.9f);
                float time = f < strike ? f / strike * a.Strike : a.Strike + (f - strike) / (1f - strike) * (a.Duration - a.Strike);
                BossPose p = a.Sample(time);
                float w = Mathf.Clamp01(Mathf.Min(f / 0.08f, (1f - f) / 0.12f));
                ApplyPose(p, w);
                twoHands = Mathf.Lerp(1f, p.TwoHands, w);
            }
            GripOffHand(twoHands);
            if (Prop != null && propGrip > 0.001f)
                GripLeftHand(Prop.TransformPoint(new Vector3(-0.22f, 0.12f, 0f)), propGrip);
        }

        // A new swing picks one of its attacks at random from the attack's own count (so a
        // spectator's puppet picks the same), never the same one twice running.
        private BossClip PickAttackClip()
        {
            if (attack.AttackCount != attackClipCount)
            {
                attackClipCount = attack.AttackCount;
                unchecked
                {
                    int h = attackClipCount * 73856093;
                    h ^= h >> 13;
                    h *= 0x5bd1e995;
                    h ^= h >> 15;
                    int next = (h & 0x7fffffff) % AttackClips.Length;
                    attackClipIndex = next == attackClipIndex && AttackClips.Length > 1 ? (next + 1) % AttackClips.Length : next;
                }
            }
            return AttackClips[attackClipIndex];
        }

        // Two-bone reach: bends the left elbow so the hand is as far from the shoulder as the grip
        // point is, then turns the shoulder to bring it there; blended over the animated arm.
        private void GripOffHand(float weight)
        {
            if (OffHand == null || weight <= 0.001f)
                return;
            GripLeftHand(OffHand.position, weight);
        }

        private void GripLeftHand(Vector3 grip, float weight)
        {
            Leg arm = Legs.Find(l => l.Front && l.Side < 0);
            if (arm == null)
                return;
            Quaternion hipWas = arm.Hip.localRotation, kneeWas = arm.Knee.localRotation;

            Vector3 target = arm.Hip.parent.InverseTransformPoint(grip) - arm.Hip.localPosition;
            float upper = arm.Knee.localPosition.magnitude;
            Vector2 g = new Vector2(OffHandGrip.y, OffHandGrip.z);
            float reach = g.magnitude;
            float d = Mathf.Clamp(target.magnitude, Mathf.Abs(upper - reach) + 0.01f, upper + reach - 0.01f);
            // With the elbow at angle e, the hand is d from the shoulder when cos(e + phi) = k.
            float k = Mathf.Clamp((upper * upper + reach * reach - d * d) / (2f * upper * reach), -1f, 1f);
            float phi = Mathf.Atan2(g.y, g.x) * Mathf.Rad2Deg;
            float e = Mathf.DeltaAngle(0f, Mathf.Acos(k) * Mathf.Rad2Deg - phi);
            float e2 = Mathf.DeltaAngle(0f, -Mathf.Acos(k) * Mathf.Rad2Deg - phi);
            if (e2 < e)
                e = e2;
            arm.Knee.localRotation = Quaternion.Euler(e, 0f, 0f);
            Vector3 hand = arm.Knee.TransformPoint(OffHandGrip);
            Vector3 shoulder = arm.Hip.position;
            arm.Hip.rotation = Quaternion.FromToRotation(hand - shoulder, grip - shoulder) * arm.Hip.rotation;

            arm.Hip.localRotation = Quaternion.Slerp(hipWas, arm.Hip.localRotation, weight);
            arm.Knee.localRotation = Quaternion.Slerp(kneeWas, arm.Knee.localRotation, weight);
        }

        private void ApplyPose(BossPose p, float w)
        {
            if (BodyPivot != null)
            {
                BodyPivot.localPosition = Vector3.Lerp(BodyPivot.localPosition, bodyBase + p.Shift, w);
                BodyPivot.localRotation = Quaternion.Slerp(BodyPivot.localRotation, bodyBaseRotation * Quaternion.Euler(p.Body), w);
            }
            foreach (Leg leg in Legs)
            {
                int s = leg.Side;
                Vector3 hip = leg.Front ? (s > 0 ? p.ArmR : p.ArmL) : (s > 0 ? p.LegR : p.LegL);
                float bend = leg.Front ? (s > 0 ? p.ElbowR : p.ElbowL) : (s > 0 ? p.KneeR : p.KneeL);
                if (!leg.Front && BodyPivot != null && leg.Hip.IsChildOf(BodyPivot))
                    hip.x -= p.Body.x;
                leg.Hip.localRotation = Quaternion.Slerp(leg.Hip.localRotation, Quaternion.Euler(hip.x, hip.y * s, leg.Lift + hip.z * s), w);
                leg.Knee.localRotation = Quaternion.Slerp(leg.Knee.localRotation, Quaternion.Euler(leg.Bend + bend, 0f, 0f), w);
            }
            if (Head != null)
                Head.localRotation = Quaternion.Slerp(Head.localRotation, Quaternion.Euler(p.Head), w);
            if (Jaw != null)
                Jaw.localRotation = Quaternion.Slerp(Jaw.localRotation, Quaternion.Euler(p.Jaw, 0f, 0f), w);
            if (Tail != null)
                Tail.localRotation = Quaternion.Slerp(Tail.localRotation, tailBase * Quaternion.Euler(p.Coat), w);
            if (Tool != null)
                Tool.localRotation = Quaternion.Slerp(Tool.localRotation, toolBase * Quaternion.Euler(p.Tool), w);
            if (Prop != null)
            {
                Prop.localPosition = Vector3.Lerp(Prop.localPosition, propBasePosition + p.PropAt, w);
                Prop.localRotation = Quaternion.Slerp(Prop.localRotation, propBaseRotation * Quaternion.Euler(p.PropRot), w);
            }
        }
    }
}
