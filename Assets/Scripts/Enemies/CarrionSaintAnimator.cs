using System;
using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.Enemies
{
    /// <summary>Cheap procedural phase-three poses. Hit events are available for the combat pass.</summary>
    [DefaultExecutionOrder(1001)]
    public class CarrionSaintAnimator : MonoBehaviour
    {
        public static readonly string[] Clips = { "Gallop", "Charge", "RearSlam", "MawBite", "TentacleLash", "SkyBite", "Burrow" };
        public event Action<string> Hit;
        public bool Demo;
        public bool RootMotion;
        public bool IsPlaying => Current != null;
        public string Current { get; private set; }
        public float Reveal = 1f;

        private struct Rest
        {
            public Transform Joint;
            public Vector3 Position, Scale;
            public Quaternion Rotation;
        }
        private readonly Dictionary<string, Rest> rest = new Dictionary<string, Rest>();
        private Transform owner;
        private EnemyHealth health;
        private CharacterController body;
        private Vector3 lastPosition;
        private float time, speed = 1f, gait, advance, nextDemo;
        private bool hit;
        private int demoIndex;
        private bool skyStarted;
        private int skyIndex;

        public static float Duration(string clip)
        {
            switch (clip)
            {
                case "Gallop": return 1.6f;
                case "Charge": return 0.85f;
                case "RearSlam": return 1f;
                case "MawBite": return 0.65f;
                case "TentacleLash": return 0.8f;
                case "SkyBite": return 3f;
                case "Burrow": return 1.1f;
                default: return 0f;
            }
        }

        public static float HitTime(string clip)
        {
            switch (clip)
            {
                case "Charge": return 0.45f;
                case "RearSlam": return 0.55f;
                case "MawBite": return 0.30f;
                case "TentacleLash": return 0.38f;
                case "SkyBite": return 1.25f;
                case "Burrow": return 0.55f;
                default: return float.MaxValue;
            }
        }

        private void Awake()
        {
            owner = transform.parent.parent;
            health = owner.GetComponent<EnemyHealth>();
            body = owner.GetComponent<CharacterController>();
            lastPosition = owner.position;
            foreach (Transform joint in GetComponentsInChildren<Transform>(true))
            {
                // Named animation pivots only; decorative primitive names need not be unique.
                if (joint == transform || joint.name == "Trunk" || joint.name == "Neck" || joint.name == "BellyMaw"
                    || joint.name == "SerpentTail" || joint.name.StartsWith("SplitSkull")
                    || joint.name.StartsWith("BeastHip") || joint.name.StartsWith("VictimArm")
                    || joint.name.StartsWith("SeamTentacle") || joint.name.StartsWith("ColossalSerpent"))
                    rest[joint.name] = new Rest { Joint = joint, Position = joint.localPosition, Rotation = joint.localRotation, Scale = joint.localScale };
            }
        }

        public bool Play(string clip, float playbackSpeed = 1f)
        {
            if (Duration(clip) == 0f) return false;
            Current = clip;
            time = advance = 0f;
            speed = Mathf.Max(0.05f, playbackSpeed);
            hit = false;
            skyStarted = false;
            CarrionSaintLook.ShowSerpents(owner, clip == "SkyBite");
            if (clip == "SkyBite")
            {
                // One enormous head at a time reads clearly; alternate the two existing rigs.
                int selected = skyIndex++ % 2;
                Transform group = transform.Find(CarrionSaintLook.SerpentsName);
                for (int i = 0; i < group.childCount; i++) group.GetChild(i).gameObject.SetActive(i == selected);
            }
            return true;
        }

        public void Stop()
        {
            Current = null;
            CarrionSaintLook.ShowSerpents(owner, false);
            Transform group = transform.Find(CarrionSaintLook.SerpentsName);
            if (group != null) foreach (Transform snake in group) snake.gameObject.SetActive(true);
        }

        private void Pose(string name, Vector3 rotation, Vector3 offset = default(Vector3), float size = 1f)
        {
            if (!rest.TryGetValue(name, out Rest r)) return;
            r.Joint.localPosition = r.Position + offset;
            r.Joint.localRotation = r.Rotation * Quaternion.Euler(rotation);
            r.Joint.localScale = r.Scale * size;
        }

        private static float Ramp(float t, float a, float b) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t));
        private static float Pulse(float t, float start, float peak, float end) => t < peak ? Ramp(t, start, peak) : 1f - Ramp(t, peak, end);

        private void LateUpdate()
        {
            if (health != null && health.IsDead) { Stop(); return; }
            if (Demo && Current == null && Time.time >= nextDemo)
            {
                Play(Clips[demoIndex++ % Clips.Length]);
                UI.CombatText.Show(owner.position + Vector3.up * 7f, Current, new Color(1f, 0.85f, 0.4f), 1f);
                nextDemo = Time.time + Duration(Current) + 0.7f;
            }
            Vector3 movement = owner.position - lastPosition;
            movement.y = 0f;
            lastPosition = owner.position;
            float moving = Time.deltaTime > 0f ? Mathf.Clamp01(movement.magnitude / Time.deltaTime / 5f) : 0f;
            if (Current == "Gallop" || Current == "Charge") moving = 1f;
            gait += Time.deltaTime * (moving > 0.1f ? 15f : 2f);
            foreach (Rest r in rest.Values)
            {
                r.Joint.localPosition = r.Position;
                r.Joint.localRotation = r.Rotation;
                r.Joint.localScale = r.Scale;
            }
            float breath = Mathf.Sin(Time.time * 2.2f) * 0.018f;
            float rear = 0f, crouch = 0f, snap = 0f, lash = 0f;
            if (Current != null)
            {
                float previous = time;
                time += Time.deltaTime * speed;
                if (Current == "SkyBite" && !skyStarted && time >= 0.10f)
                {
                    skyStarted = true;
                    Vector3 target = owner.position + owner.forward * 12f;
                    target.y = Visuals.Debris.GroundBelow(target + Vector3.up * 5f) + 2f;
                    foreach (SnakeLimb snake in GetComponentsInChildren<SnakeLimb>())
                        snake.StrikeFromAbove(target, 15f, 0.75f / speed, 0.40f / speed, 0.40f / speed, 0.90f / speed,
                            impact => CameraSystem.CameraFollow.Shake(0.3f, 0.25f));
                    StartCoroutine(GroundTelegraph.Run(target, 5f, 1.15f / speed, Combat.DamageType.Physical, null));
                }
                if (!hit && previous < HitTime(Current) && time >= HitTime(Current))
                {
                    hit = true;
                    Hit?.Invoke(Current);
                }
                switch (Current)
                {
                    case "Charge":
                        crouch = Pulse(time, 0f, 0.20f, 0.80f);
                        if (RootMotion)
                        {
                            float next = Ramp(time, 0.22f, 0.48f) * 10f;
                            Vector3 step = owner.forward * (next - advance);
                            if (body != null && body.enabled) body.Move(step); else owner.position += step;
                            advance = next;
                        }
                        break;
                    case "RearSlam":
                        rear = Pulse(time, 0f, 0.38f, 0.55f);
                        crouch = Pulse(time, 0.48f, 0.57f, 1f) * 0.65f;
                        break;
                    case "MawBite": snap = Pulse(time, 0f, 0.22f, 0.33f); crouch = Pulse(time, 0.20f, 0.32f, 0.65f) * 0.5f; break;
                    case "TentacleLash": lash = Pulse(time, 0f, 0.38f, 0.80f); break;
                    case "SkyBite": rear = Pulse(time, 0f, 0.65f, 2.8f) * 0.45f; break;
                    case "Burrow": crouch = Pulse(time, 0f, 0.4f, 1.1f); break;
                }
                if (time >= Duration(Current)) Stop();
            }
            float folded = 1f - Mathf.Clamp01(Reveal);
            Pose("Trunk", new Vector3(-rear * 38f + crouch * 22f + folded * 18f, lash * 18f, Mathf.Sin(gait) * moving * 3f),
                new Vector3(0f, breath + rear * 0.25f - crouch * 0.20f - folded * 0.55f, 0f));
            for (int side = -1; side <= 1; side += 2)
            {
                float cycle = Mathf.Sin(gait + (side < 0 ? Mathf.PI : 0f));
                Pose("BeastHip" + side, new Vector3(cycle * moving * 15f + folded * 48f, 0f, folded * side * 12f), Vector3.up * Mathf.Max(0f, cycle) * moving * 0.12f);
                for (int index = 0; index < 2; index++)
                {
                    float stride = Mathf.Sin(gait + (side < 0 ? Mathf.PI : 0f) + index * Mathf.PI);
                    Pose("VictimArm" + side + "_" + index, new Vector3(stride * moving * 20f - rear * 70f + folded * 65f, lash * side * 12f, 0f),
                        Vector3.up * (Mathf.Max(0f, stride) * moving * 0.16f + rear * 0.15f));
                }
                Pose("SplitSkull" + side, new Vector3(0f, side * snap * 15f, -side * (snap * 20f - folded * 22f)));
                for (int index = 0; index < 3; index++)
                    Pose("SeamTentacle" + side + "_" + index, new Vector3(Mathf.Sin(Time.time * 3f + index) * 5f - lash * 18f,
                        side * (Mathf.Sin(Time.time * 2f + index) * 6f + lash * 65f), 0f), Vector3.zero, 1f - folded * 0.85f);
            }
            Pose("Neck", new Vector3(crouch * -18f + rear * 12f, 0f, 0f));
            Pose("BellyMaw", Vector3.zero, new Vector3(0f, 0f, snap * 0.18f));
            if (rest.TryGetValue("BellyMaw", out Rest maw)) maw.Joint.localScale = new Vector3(1f + snap * 0.65f, 1f - snap * 0.18f, 1f);
            Pose("SerpentTail", new Vector3(0f, Mathf.Sin(Time.time * 2f) * 7f + lash * -22f, 0f), Vector3.zero, 1f - folded * 0.75f);
        }
    }
}
