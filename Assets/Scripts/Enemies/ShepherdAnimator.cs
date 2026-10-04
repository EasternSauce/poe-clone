using System;
using System.Collections.Generic;
using UnityEngine;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>
    /// The Shepherd's moves, as keyframed poses played over the walk animator's pose. A pose
    /// sets the upper body, both arms and elbows, the crook (its angle from upright, relative to
    /// the body, and how curled the snake at its top is: 1 the hook, 0 straight out like a
    /// striking snake), how far the upper body sinks into the robe, and how far the boss has moved
    /// forward (root motion). Between moves the snake head twitches now and then.
    /// Runs after the walk/attack animators and UprightStaff, and blends in and out of their pose.
    /// </summary>
    [DefaultExecutionOrder(1001)]
    public class ShepherdAnimator : MonoBehaviour
    {
        // ------------------------------------------------------------------ poses and clips

        public sealed class Pose
        {
            public Vector3 BodyRot = new Vector3(ShepherdLook.Stoop, 0f, 0f);
            public Vector3 ArmR = new Vector3(-20f, 0f, 0f);
            public float ElbowR = -20f;
            public Vector3 ArmL = new Vector3(-8f, 0f, 0f);
            public float ElbowL = -15f;
            public Vector3 CrookRot = new Vector3(20f, 0f, 0f);
            public float Curl = 1f;
            public float Drop;
            public float Advance;

            public Pose Body(float pitch, float yaw = 0f, float roll = 0f) { BodyRot = new Vector3(pitch, yaw, roll); return this; }
            public Pose R(float pitch, float yaw = 0f, float roll = 0f, float elbow = -20f) { ArmR = new Vector3(pitch, yaw, roll); ElbowR = elbow; return this; }
            public Pose L(float pitch, float yaw = 0f, float roll = 0f, float elbow = -15f) { ArmL = new Vector3(pitch, yaw, roll); ElbowL = elbow; return this; }
            public Pose Crook(float pitch, float yaw = 0f, float roll = 0f, float curl = 1f) { CrookRot = new Vector3(pitch, yaw, roll); Curl = curl; return this; }
            public Pose Sink(float drop) { Drop = drop; return this; }
            public Pose Move(float advance) { Advance = advance; return this; }

            public static Pose Lerp(Pose a, Pose b, float f)
            {
                return new Pose
                {
                    BodyRot = Vector3.Lerp(a.BodyRot, b.BodyRot, f),
                    ArmR = Vector3.Lerp(a.ArmR, b.ArmR, f),
                    ElbowR = Mathf.Lerp(a.ElbowR, b.ElbowR, f),
                    ArmL = Vector3.Lerp(a.ArmL, b.ArmL, f),
                    ElbowL = Mathf.Lerp(a.ElbowL, b.ElbowL, f),
                    CrookRot = Vector3.Lerp(a.CrookRot, b.CrookRot, f),
                    Curl = Mathf.Lerp(a.Curl, b.Curl, f),
                    Drop = Mathf.Lerp(a.Drop, b.Drop, f),
                    Advance = Mathf.Lerp(a.Advance, b.Advance, f)
                };
            }
        }

        private static Pose N() { return new Pose(); }

        public sealed class Clip
        {
            public string Name;
            public float[] Times;
            public Pose[] Keys;
            /// <summary>When the blow lands, in seconds from the start (for the fight to deal damage on).</summary>
            public float[] Hits = new float[0];
            public float Duration => Times[Times.Length - 1];
        }

        private static Clip Make(string name, float[] hits, params object[] timesAndPoses)
        {
            var times = new List<float>();
            var keys = new List<Pose>();
            for (int i = 0; i < timesAndPoses.Length; i += 2)
            {
                times.Add(Convert.ToSingle(timesAndPoses[i]));
                keys.Add((Pose)timesAndPoses[i + 1]);
            }
            return new Clip { Name = name, Times = times.ToArray(), Keys = keys.ToArray(), Hits = hits };
        }

        // Arm pitch: 0 hangs down, -90 straight ahead, -180 overhead. Elbow: negative bends forward.
        // Crook pitch: 0 upright, 90 pointing ahead, 180 pointing down. Yaw: + to his right.
        public static readonly Clip[] Clips =
        {
            // A wide sweep from his right across to his left, low, the hook leading.
            Make("Sweep", new[] { 0.5f },
                0f, N(),
                0.35f, N().Body(32f, 40f).R(-70f, 65f, 0f, -30f).Crook(80f, 75f).Sink(-0.08f),
                0.50f, N().Body(36f, -30f).R(-80f, -45f, 0f, -10f).Crook(85f, -55f).Sink(-0.12f),
                0.62f, N().Body(36f, -45f).R(-78f, -70f, 0f, -10f).Crook(85f, -85f).Sink(-0.12f),
                0.95f, N()),

            // Coils back like a snake about to strike, then lunges the crook straight out: the
            // snake at its top snaps straight for a moment.
            Make("Jab", new[] { 0.5f },
                0f, N(),
                0.40f, N().Body(4f, 18f).R(-55f, 10f, 0f, -115f).Crook(88f, 0f, 0f, 0.55f).Sink(-0.04f).Move(-0.25f),
                0.50f, N().Body(46f, -8f).R(-96f, 0f, 0f, 0f).Crook(92f, 0f, 0f, 0f).Sink(-0.2f).Move(0.8f),
                0.64f, N().Body(46f, -8f).R(-96f, 0f, 0f, 0f).Crook(92f, 0f, 0f, 0.1f).Sink(-0.2f).Move(0.8f),
                0.95f, N().Move(0.8f)),

            // Rears up, crook high overhead, and brings the hook down on the ground ahead.
            Make("Slam", new[] { 0.6f },
                0f, N(),
                0.45f, N().Body(-6f).R(-172f, 0f, 0f, -20f).L(-40f).Crook(-25f).Sink(0.12f),
                0.60f, N().Body(56f).R(-62f, 0f, 0f, -5f).L(-20f).Crook(125f).Sink(-0.3f).Move(0.3f),
                0.82f, N().Body(56f).R(-62f, 0f, 0f, -5f).L(-20f).Crook(125f).Sink(-0.3f).Move(0.3f),
                1.15f, N().Move(0.3f)),

            // Reaches the hook out, catches, and yanks it back to his chest.
            Make("HookPull", new[] { 0.32f, 0.75f },
                0f, N(),
                0.30f, N().Body(38f).R(-100f, 0f, 0f, 0f).Crook(95f).Move(0.3f),
                0.55f, N().Body(40f).R(-102f, 0f, 0f, 0f).Crook(97f).Move(0.3f),
                0.75f, N().Body(2f, 25f).R(-35f, 30f, 0f, -120f).Crook(40f, 20f).Move(0.3f),
                1.10f, N().Move(0.3f)),

            // Sinks into the robe, head held up, and sways side to side like a cobra; rears back,
            // then launches himself along the ground, arms and crook out ahead, the snake straight.
            Make("CobraLunge", new[] { 0.55f },
                0f, N(),
                0.15f, N().Body(28f).R(-55f, 0f, 0f, -50f).L(-50f, 0f, 0f, -40f).Crook(95f, 0f, 0f, 0.5f).Sink(-0.55f),
                0.28f, N().Body(26f, 28f, 12f).R(-55f, 0f, 0f, -50f).L(-50f, 0f, 0f, -40f).Crook(95f, 25f, 0f, 0.5f).Sink(-0.5f),
                0.40f, N().Body(14f, 0f, 0f).R(-45f, 0f, 0f, -80f).L(-45f, 0f, 0f, -70f).Crook(90f, 0f, 0f, 0.4f).Sink(-0.6f).Move(-0.3f),
                0.55f, N().Body(78f).R(-112f, 0f, 0f, 0f).L(-112f, 0f, 0f, 0f).Crook(95f, 0f, 0f, 0f).Sink(-0.4f).Move(ShepherdFight.LungeLength - 0.3f),
                0.75f, N().Body(70f).R(-100f, 0f, 0f, -10f).L(-100f, 0f, 0f, -10f).Crook(95f, 0f, 0f, 0.2f).Sink(-0.4f).Move(ShepherdFight.LungeLength),
                1.05f, N().Move(ShepherdFight.LungeLength)),

            // Raises the crook and drives it into the ground; the snake at its top writhes while
            // he holds it there (what answers from below comes with the fight).
            Make("SerpentCall", new[] { 0.32f },
                0f, N(),
                0.20f, N().Body(-10f).R(-160f, 0f, 0f, -10f).L(-30f).Crook(0f).Sink(0.2f),
                0.32f, N().Body(40f).R(-48f, 0f, 0f, -10f).L(-25f).Crook(0f).Sink(-0.3f),
                0.48f, N().Body(44f, 4f).R(-48f, 0f, 0f, -10f).L(-25f).Crook(0f, 0f, 0f, 0.3f).Sink(-0.3f),
                0.64f, N().Body(44f, -4f).R(-48f, 0f, 0f, -10f).L(-25f).Crook(0f, 0f, 0f, 0.9f).Sink(-0.3f),
                0.80f, N().Body(44f, 4f).R(-48f, 0f, 0f, -10f).L(-25f).Crook(0f, 0f, 0f, 0.2f).Sink(-0.3f),
                0.96f, N().Body(44f, -4f).R(-48f, 0f, 0f, -10f).L(-25f).Crook(0f, 0f, 0f, 0.8f).Sink(-0.3f),
                1.25f, N()),

            // Phase 1 -> 2, played at speed 1 (ShepherdFight.GraftTransition does what happens at each
            // event): shudders; plants the crook (0.85); reaches across, grips the lantern arm and
            // tears it off (1.55); takes the crook back up (2.15); drives it into the stump (2.65);
            // convulses as it grows into the snake arm and the disguise bursts (2.9); roars (3.6).
            Make("Graft", new[] { 0.85f, 1.55f, 2.15f, 2.65f, 2.9f, 3.6f },
                0f, N(),
                0.35f, N().Body(45f, 0f, 6f).R(-20f).L(-10f).Sink(-0.2f),
                0.50f, N().Body(45f, 0f, -6f).R(-20f).L(-14f).Sink(-0.2f),
                0.65f, N().Body(45f, 0f, 6f).R(-20f).L(-8f).Sink(-0.2f),
                0.85f, N().Body(30f).R(-40f, 0f, 0f, -20f).Crook(0f).Sink(-0.1f),
                1.15f, N().Body(20f, -25f).R(-100f, -70f, 0f, -110f).L(-20f).Crook(0f),
                1.40f, N().Body(10f, -35f, -10f).R(-95f, -80f, 0f, -120f).L(-30f, 0f, 20f).Crook(0f),
                1.55f, N().Body(-5f, 30f, 10f).R(-60f, 60f, 0f, -20f).Crook(0f),
                1.85f, N().Body(20f, 15f).R(-30f, 30f, 0f, -20f).Crook(0f),
                2.15f, N().Body(40f, 10f).R(-70f, 10f, 0f, -10f).Crook(0f),
                2.45f, N().Body(10f, -20f).R(-150f, -40f, 0f, -60f).Crook(160f, -60f),
                2.65f, N().Body(25f, -30f).R(-110f, -70f, 0f, -100f).Crook(150f, -80f),
                3.05f, N().Body(-10f, 0f, 10f).R(-20f).Sink(0.1f),
                3.30f, N().Body(-10f, 0f, -10f).R(-25f).Sink(0.1f),
                3.60f, N().Body(-25f).R(-60f, 40f, 0f, -40f).Sink(0.25f),
                4.10f, N()),

            // Phase 2, Venom Volley: rears back, then throws his chest forward twice - each time the
            // snakes on his back strike out and spit (ShepherdFight.VenomWave).
            Make("Spit", new[] { 0.45f, 0.75f },
                0f, N(),
                0.30f, N().Body(-15f).R(-30f).Sink(0.15f),
                0.45f, N().Body(30f).R(-20f),
                0.62f, N().Body(-5f).R(-30f).Sink(0.1f),
                0.75f, N().Body(32f).R(-20f),
                1.10f, N()),

            // Phase 2, Burrow Snatch: twists the snake shoulder back and plunges the arm into the
            // ground (0.40), then holds while it tunnels to the player (ShepherdFight.Snatch).
            Make("Burrow", new[] { 0.40f },
                0f, N(),
                0.25f, N().Body(-5f, -20f).R(-30f),
                0.40f, N().Body(45f, 25f).R(-20f).Sink(-0.2f),
                2.20f, N().Body(40f, 20f).R(-20f).Sink(-0.2f),
                2.50f, N()),

            // Phase 2: the snake arm does the biting (SnakeLimb.Strike, timed by ShepherdFight);
            // the body twists the left shoulder back as it coils, then throws it forward into the
            // strike.
            Make("Bite", new[] { 0.42f },
                0f, N(),
                0.30f, N().Body(18f, -28f).R(-25f, 0f, 0f, -40f),
                0.42f, N().Body(34f, 32f).R(-10f, 0f, 0f, -30f).Move(0.4f),
                0.70f, N().Body(32f, 28f).R(-10f, 0f, 0f, -30f).Move(0.4f),
                1.00f, N().Move(0.4f)),

            Make("DoubleBite", new[] { 0.36f, 0.66f },
                0f, N(),
                0.25f, N().Body(18f, -28f).R(-25f, 0f, 0f, -40f),
                0.36f, N().Body(34f, 32f).R(-10f, 0f, 0f, -30f).Move(0.3f),
                0.55f, N().Body(20f, -20f).R(-25f, 0f, 0f, -40f).Move(0.3f),
                0.66f, N().Body(36f, 34f).R(-10f, 0f, 0f, -30f).Move(0.6f),
                0.92f, N().Body(32f, 28f).R(-10f, 0f, 0f, -30f).Move(0.6f),
                1.20f, N().Move(0.6f)),
        };

        public static Clip Find(string name)
        {
            foreach (Clip c in Clips)
            {
                if (string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
                    return c;
            }
            return null;
        }

        // ------------------------------------------------------------------ rig

        public Transform Root;
        public Transform UpperBody;
        public Transform ArmR, ElbowR, ArmL, ElbowL;
        public Transform Crook, Lantern;
        /// <summary>The joints of the snake's curl at the crook's top, base first.</summary>
        public Transform[] CurlJoints = new Transform[0];
        /// <summary>Each curl joint's bend at full curl, in degrees.</summary>
        public float CurlStep;

        /// <summary>
        /// Root motion forward stops this far (flat metres, centre to centre) short of
        /// <see cref="Target"/>: ordinary attacks stop short; Cobra Lunge commits to its full path.
        /// </summary>
        public Transform Target;
        public float MinGap;

        /// <summary>Added to every pose's body pitch: -Stoop stands him up straight (phase 2).</summary>
        public float BodyPitchOffset;

        /// <summary>Fires each time a playing clip reaches one of its <see cref="Clip.Hits"/>.</summary>
        public event Action<Clip, int> Hit;

        /// <summary>Testing: plays every clip in turn, a second apart, its name floating overhead, each from where the demo started.</summary>
        public bool Demo;

        public bool IsPlaying => clip != null;
        public Clip Current => clip;

        private const float BlendIn = 0.12f;
        private const float BlendOut = 0.15f;

        private Clip clip;
        private float time;
        private float speed = 1f;
        private float lastAdvance;
        private CharacterController body;
        private Vector3 upperRest;
        private float nextTwitch;
        private float twitchUntil;
        private int demoIndex;
        private float demoNext;
        private Vector3? demoHome;

        public void Play(Clip c, float playbackSpeed = 1f)
        {
            if (c == null)
                return;
            clip = c;
            time = 0f;
            speed = Mathf.Max(0.05f, playbackSpeed);
            lastAdvance = 0f;
        }

        /// <summary>Testing: holds a clip still at <paramref name="at"/> seconds in (Stop or Play to let go).</summary>
        public void Freeze(Clip c, float at)
        {
            if (c == null)
                return;
            clip = c;
            time = Mathf.Clamp(at, 0f, c.Duration - 0.01f);
            speed = 0f;
            lastAdvance = Sample(c, time).Advance;
        }

        public void Stop()
        {
            clip = null;
        }

        private void Start()
        {
            body = Root != null ? Root.GetComponent<CharacterController>() : null;
            if (UpperBody != null)
                upperRest = UpperBody.localPosition;
            nextTwitch = Time.time + UnityEngine.Random.Range(3f, 6f);
        }

        private EnemyHealth owner;

        private void LateUpdate()
        {
            if (owner == null && Root != null)
                owner = Root.GetComponent<EnemyHealth>();
            // Dead: the crook's snake stops stirring and no clip plays on the corpse.
            if (owner != null && owner.IsDead)
            {
                clip = null;
                return;
            }

            if (!Demo)
                demoHome = null;
            if (Demo && clip == null && Time.time >= demoNext && Root != null)
            {
                if (demoHome == null)
                    demoHome = Root.position;
                Root.position = demoHome.Value;
                Physics.SyncTransforms();
                Clip next = Clips[demoIndex++ % Clips.Length];
                Play(next);
                demoNext = Time.time + next.Duration + 1f;
                UI.CombatText.Show(Root.position + Vector3.up * 3.6f, next.Name, new Color(1f, 0.85f, 0.4f), 1.3f);
            }

            float curl = 1f;
            float drop = 0f;
            if (clip != null)
            {
                float previous = time;
                time += Time.deltaTime * speed;
                for (int i = 0; i < clip.Hits.Length; i++)
                {
                    if (previous < clip.Hits[i] && time >= clip.Hits[i])
                        Hit?.Invoke(clip, i);
                }

                if (time >= clip.Duration)
                {
                    clip = null;
                }
                else
                {
                    Pose pose = Sample(clip, time);
                    float w = speed == 0f ? 1f : Mathf.Clamp01(Mathf.Min(time / BlendIn, (clip.Duration - time) / BlendOut));
                    Apply(pose, w);
                    curl = Mathf.Lerp(1f, pose.Curl, w);
                    drop = pose.Drop * w;

                    // Root motion is authored at his phase-1 size and grows with him.
                    float advance = (pose.Advance - lastAdvance) * (Root != null ? Root.localScale.x / ShepherdLook.BaseScale : 1f);
                    lastAdvance = pose.Advance;
                    // The charge commits to its full path; ordinary attacks stop short of the player.
                    if (advance > 0f && Target != null && Root != null && clip.Name != "CobraLunge")
                    {
                        Vector3 to = Target.position - Root.position;
                        to.y = 0f;
                        float ahead = Vector3.Dot(to, Root.forward);
                        if (ahead > 0f)
                            advance = Mathf.Min(advance, Mathf.Max(0f, ahead - MinGap));
                    }
                    if (Root != null && Mathf.Abs(advance) > 0f)
                    {
                        Vector3 step = Root.forward * advance;
                        if (body != null && body.enabled)
                            body.Move(step);
                        else
                            Root.position += step;
                    }
                }
            }

            if (UpperBody != null)
                UpperBody.localPosition = upperRest + Vector3.up * drop;
            BendCurl(curl, Twitch());
        }

        // Every few seconds between moves the snake head stirs: a short ripple down the curl.
        private float Twitch()
        {
            if (clip != null)
                return 0f;
            if (Time.time >= nextTwitch)
            {
                twitchUntil = Time.time + 0.6f;
                nextTwitch = Time.time + UnityEngine.Random.Range(4f, 8f);
            }
            if (Time.time >= twitchUntil)
                return 0f;
            return Mathf.Sin((twitchUntil - Time.time) / 0.6f * Mathf.PI);
        }

        private static Pose Sample(Clip c, float t)
        {
            int k = 1;
            while (k < c.Times.Length - 1 && t > c.Times[k])
                k++;
            float t0 = c.Times[k - 1], t1 = c.Times[k];
            float f = t1 > t0 ? Mathf.Clamp01((t - t0) / (t1 - t0)) : 1f;
            return Pose.Lerp(c.Keys[k - 1], c.Keys[k], f * f * (3f - 2f * f));
        }

        private void Apply(Pose p, float w)
        {
            Blend(UpperBody, p.BodyRot + new Vector3(BodyPitchOffset, 0f, 0f), w);
            Blend(ArmR, p.ArmR, w);
            Blend(ElbowR, new Vector3(p.ElbowR, 0f, 0f), w);
            Blend(ArmL, p.ArmL, w);
            Blend(ElbowL, new Vector3(p.ElbowL, 0f, 0f), w);

            if (Root != null)
            {
                if (Crook != null)
                    Crook.rotation = Quaternion.Slerp(Crook.rotation, Root.rotation * Quaternion.Euler(p.CrookRot), w);
                // The lantern keeps hanging straight down from the hand whatever the arm does.
                if (Lantern != null)
                    Lantern.rotation = Quaternion.Slerp(Lantern.rotation, Root.rotation, w);
            }
        }

        private static void Blend(Transform t, Vector3 euler, float w)
        {
            if (t != null)
                t.localRotation = Quaternion.Slerp(t.localRotation, Quaternion.Euler(euler), w);
        }

        private void BendCurl(float curl, float twitch)
        {
            for (int i = 0; i < CurlJoints.Length; i++)
            {
                if (CurlJoints[i] == null)
                    continue;
                float sway = twitch * 14f * Mathf.Sin(Time.time * 22f - i * 0.9f);
                CurlJoints[i].localRotation = Quaternion.Euler(CurlStep * curl, sway, 0f);
            }
        }
    }
}
