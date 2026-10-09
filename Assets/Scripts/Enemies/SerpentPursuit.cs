using System.Collections.Generic;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Player;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>A serpent grows along its chase route. Its retained body is a damaging escape-route hazard.</summary>
    public class SerpentPursuit : MonoBehaviour, IDamageable
    {
        // One fixed physical hit at area level 12: about seven Carrion Saint auto-attacks.
        // Tune this directly; enrage does not change it.
        private const float SwallowDamage = 400f;
        private const float Radius = 1.5f;
        private const float StartSpeed = 10f;
        private const float EndSpeed = 13.5f;
        private const float SpeedRampSeconds = 5f;
        private const float TurnDegreesPerSecond = 75f;
        private const float EdgePassTurnDegreesPerSecond = 35f;
        private const float UTurnSeconds = 1.75f;
        private const float UTurnSpeed = 5.5f;
        private const float ChaseSeconds = 12f;
        private const float RearTellSeconds = 0.3f;
        private const float PursuitOpenDegrees = 26f;
        private readonly List<Vector3> route = new List<Vector3>();
        private EnemyHealth owner;
        private PlayerStats player;
        private Transform head, skull, jaw;
        private Renderer[] headRenderers;
        private GameObject emergenceWarning;
        private Mesh mesh;
        private Vector3 nose, direction, rearDirection;
        private float turnSign;
        private float age, endedAt = -1f, capturedAt = -1f, nextSkinHit, damage;
        private float headReach;
        private float floorY;
        private Vector3 headScale;
        private PlayerController controls;
        private CharacterController playerBody;
        private bool controlsEnabled, bodyEnabled;
        private Transform victimModel;
        private Vector3 modelScale;
        private bool modelVisible;
        private bool swallowHit;
        private bool previousImmunity;
        private bool edgePass, missed, retained;
        private World.AreaShape passArena;
        private Vector3 lastPassVelocity;
        private float passSpeed;
        public bool ReachedEdge => retained;
        public bool IsFinished => endedAt >= 0f;
        public bool Captured => capturedAt >= 0f;
        public int RoutePoints => route.Count;
        public float LastSharedDamage { get; private set; }
        public Vector3 MouthPosition => head != null ? head.position + Vector3.up * 1.5f : nose + Vector3.up * 1.5f;

        public static SerpentPursuit Spawn(EnemyHealth boss, PlayerStats target, float hitDamage,
            Vector3? edgeStart = null, World.AreaShape edgeArena = null)
        {
            SnakeLimb source = boss.GetComponentInChildren<SnakeLimb>(true);
            foreach (SnakeLimb snake in boss.GetComponentsInChildren<SnakeLimb>(true))
                if (snake.name.StartsWith("ColossalSerpent")) { source = snake; break; }
            if (source == null || target == null) return null;
            var go = new GameObject("Pursuing colossal serpent");
            var pursuit = go.AddComponent<SerpentPursuit>();
            pursuit.owner = boss; pursuit.player = target; pursuit.damage = hitDamage;
            pursuit.previousImmunity = boss.Immune;
            pursuit.edgePass = edgeStart.HasValue && edgeArena != null;
            if (!pursuit.edgePass) boss.Immune = true;
            // Emerge from the very rear of the chimera, facing away from the player.
            pursuit.rearDirection = -boss.transform.forward;
            pursuit.direction = pursuit.rearDirection;
            Vector3 anchor = boss.transform.TransformPoint(new Vector3(0f, 1.35f, -2.15f));
            Vector3 towardPlayer = target.transform.position - anchor; towardPlayer.y = 0f;
            pursuit.turnSign = Vector3.SignedAngle(pursuit.rearDirection, towardPlayer, Vector3.up) >= 0f ? 1f : -1f;
            pursuit.floorY = Debris.GroundBelow(anchor + Vector3.up * 5f);
            Vector3 ground = anchor; ground.y = pursuit.floorY + Radius + 0.15f;
            pursuit.route.Add(anchor); pursuit.route.Add(ground);
            pursuit.nose = ground;
            if (pursuit.edgePass)
            {
                ground = edgeStart.Value;
                pursuit.floorY = Debris.GroundBelow(ground + Vector3.up * 5f);
                ground.y = pursuit.floorY + Radius + 0.15f;
                pursuit.nose = ground;
                pursuit.passArena = edgeArena;
                pursuit.direction = Horizontal(target.transform.position - ground).normalized;
                if (pursuit.direction.sqrMagnitude < 0.01f)
                    pursuit.direction = Horizontal(edgeArena.Center - ground).normalized;
                pursuit.passSpeed = EndSpeed;
                pursuit.lastPassVelocity = pursuit.direction * EndSpeed;
                pursuit.route.Clear(); pursuit.route.Add(ground);
            }
            pursuit.head = Instantiate(source.MouthTransform.gameObject, go.transform).transform;
            pursuit.head.name = "PursuitHead";
            pursuit.headScale = source.MouthTransform.lossyScale;
            SphereCollider hitbox = pursuit.head.gameObject.AddComponent<SphereCollider>();
            hitbox.center = new Vector3(0f, 0.55f, 0f);
            hitbox.radius = 0.58f;
            pursuit.headReach = CarrionSaintLook.SerpentThickness * 0.75f * 1.7f * pursuit.headScale.y * 0.8f;
            pursuit.skull = pursuit.head.Find("Skull"); pursuit.jaw = pursuit.head.Find("Jaw");
            pursuit.headRenderers = pursuit.head.GetComponentsInChildren<Renderer>(true);
            Renderer[] originals = source.MouthTransform.GetComponentsInChildren<Renderer>(true);
            Renderer[] copies = pursuit.head.GetComponentsInChildren<Renderer>(true);
            var colour = new MaterialPropertyBlock();
            for (int i = 0; i < originals.Length && i < copies.Length; i++) { originals[i].GetPropertyBlock(colour); copies[i].SetPropertyBlock(colour); }
            GameObject skin = RuntimePrimitives.Create(PrimitiveType.Cube, go.transform, new Color(0.22f, 0.29f, 0.16f));
            skin.name = "Extended serpent skin";
            pursuit.mesh = new Mesh { name = "Growing serpent route" };
            pursuit.mesh.MarkDynamic();
            skin.GetComponent<MeshFilter>().sharedMesh = pursuit.mesh;
            pursuit.emergenceWarning = RuntimePrimitives.Create(PrimitiveType.Cylinder, null, new Color(0.78f, 0.58f, 0.20f));
            pursuit.emergenceWarning.name = "Serpent emergence warning";
            pursuit.emergenceWarning.transform.position = new Vector3(ground.x, pursuit.floorY + 0.08f, ground.z);
            pursuit.emergenceWarning.transform.localScale = new Vector3(4.2f, 0.02f, 4.2f);
            pursuit.UpdateHead(pursuit.edgePass ? 1f : 0.01f);
            CameraSystem.CameraFollow.SustainedShake = 0.12f;
            return pursuit;
        }

        /// <summary>Player attacks can strike the exposed head for 150% damage to the boss.</summary>
        public void TakeDamage(float amount)
        {
            if (owner != null && !owner.IsDead && amount > 0f)
            {
                LastSharedDamage = owner.TakeDamage(amount * 1.5f, Combat.DamageType.Physical, 0f, 0f, true);
            }
        }

        public void TakeArrowHit(Transform attacker, float amount, bool attack, Combat.DamageType type, Color color, float igniteBonus,
            float projectileDistance = -1f, bool melee = false, int venomArrowLevel = 0)
        {
            if (owner == null || owner.IsDead || amount <= 0f)
                return;
            LastSharedDamage = amount * 1.5f;
            HitEffects.Deal(attacker, owner, LastSharedDamage, attack, color, type, igniteBonus: igniteBonus,
                displayAt: MouthPosition, throughExposedHead: true, melee: melee, projectileDistance: projectileDistance,
                venomArrowLevel: venomArrowLevel);
        }

        private Vector3 Mouth => head != null ? head.position + head.up * headReach : nose + direction * headReach;

        private void Update()
        {
            if (owner == null || owner.IsDead || player == null || player.IsDead)
            {
                Release(); Destroy(gameObject); return;
            }
            age += Time.deltaTime;
            float growth = Mathf.SmoothStep(0.02f, 1f, Mathf.Clamp01(age / 0.45f));
            if (endedAt >= 0f)
            {
                float left = 1f - (Time.time - endedAt) / 0.6f;
                UpdateHead(Mathf.Max(0.01f, left)); BuildSkin(Mathf.Max(0.01f, left));
                if (left <= 0f) Destroy(gameObject);
                return;
            }
            if (Captured)
            {
                Swallow(); UpdateHead(1f); BuildSkin(1f); return;
            }
            if (edgePass)
            {
                UpdateEdgePass(); return;
            }
            if (age < RearTellSeconds)
            {
                if (emergenceWarning != null)
                    emergenceWarning.transform.localScale = new Vector3(4.2f + Mathf.Sin(age * 12f) * 0.3f, 0.02f,
                        4.2f + Mathf.Sin(age * 12f) * 0.3f);
            }
            else
            {
                if (emergenceWarning != null) { Destroy(emergenceWarning); emergenceWarning = null; }
                float turnAge = age - RearTellSeconds;
                bool turning = turnAge < UTurnSeconds;
                float speed;
                if (turning)
                {
                    // Move through the full arc rather than yawing in place. At 5.5 m/s,
                    // the 180-degree turn has a roughly 3 m radius and remains readable.
                    direction = Quaternion.AngleAxis(180f * turnSign * turnAge / UTurnSeconds, Vector3.up) * rearDirection;
                    speed = UTurnSpeed;
                }
                else
                {
                    Vector3 to = player.transform.position - Mouth; to.y = 0f;
                    if (to.sqrMagnitude > 0.01f)
                        direction = Vector3.RotateTowards(direction, to.normalized,
                            TurnDegreesPerSecond * Mathf.Deg2Rad * Time.deltaTime, 0f).normalized;
                    float chaseAge = turnAge - UTurnSeconds;
                    speed = Mathf.Lerp(StartSpeed, EndSpeed, Mathf.Clamp01(chaseAge / SpeedRampSeconds));
                }
                // A travelling lateral wave makes the head weave, and the retained route
                // records the same curves instead of drawing a straight tube.
                Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
                nose += (direction * speed + (turning ? Vector3.zero : side * Mathf.Sin(age * 5.5f) * 3.2f)) * Time.deltaTime;
                nose.y = floorY + Radius + 0.15f;
                if ((nose - route[route.Count - 1]).sqrMagnitude >= 0.8f * 0.8f) route.Add(nose);
                if (!turning && Horizontal(player.transform.position - Mouth).magnitude < 2.3f) Capture();
                else if (turnAge > UTurnSeconds + ChaseSeconds) endedAt = Time.time;
                if (!Captured && Time.time >= nextSkinHit)
                {
                    for (int i = 1; i < route.Count; i++)
                    {
                        if (DistanceToSegment(player.transform.position, route[i - 1], route[i]) > Radius + 0.3f) continue;
                        nextSkinHit = Time.time + 0.5f;
                        if (player.TakeHit(damage * 1.2f, Combat.DamageType.Physical)) player.Poison(damage * 0.33333334f, 2f);
                        break;
                    }
                }
            }
            UpdateHead(growth); BuildSkin(growth);
        }

        private void UpdateEdgePass()
        {
            if (retained) { DamageTail(); return; }
            // The emergence tell does not ramp movement speed: launch at the old chase's maximum.
            if (age < RearTellSeconds) { UpdateHead(1f); BuildSkin(1f); return; }
            if (emergenceWarning != null) { Destroy(emergenceWarning); emergenceWarning = null; }
            Vector3 oldMouth = Mouth;
            Vector3 velocity;
            if (!missed)
            {
                // Track the player's live position with the pursuit's limited turn rate and weave.
                // Once the jaws pass them, commit to the actual travelling direction, including
                // the current sideways weave, so the exit never snaps back toward the player.
                Vector3 to = Horizontal(player.transform.position - oldMouth);
                if (Vector3.Dot(to, direction) <= 0f)
                {
                    missed = true;
                    direction = lastPassVelocity.normalized;
                }
                else if (to.sqrMagnitude > 0.01f)
                    direction = Vector3.RotateTowards(direction, to.normalized,
                        EdgePassTurnDegreesPerSecond * Mathf.Deg2Rad * Time.deltaTime, 0f).normalized;
            }
            // Double the approach speed in 0.2 seconds: a distinct burst after the dodge.
            if (missed) passSpeed = Mathf.MoveTowards(passSpeed, EndSpeed * 2f, EndSpeed / 0.2f * Time.deltaTime);
            Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
            velocity = direction * passSpeed + (missed ? Vector3.zero : side * Mathf.Sin(age * 5.5f) * 3.2f);
            lastPassVelocity = velocity;
            Vector3 next = nose + velocity * Time.deltaTime;
            bool atEdge = !passArena.Contains(next, 1.8f);
            if (atEdge)
            {
                // Clip the final step to the authored arena rather than the original aim line:
                // steering can send a missed pass toward any point on the boundary.
                float low = 0f, high = 1f;
                for (int i = 0; i < 8; i++)
                {
                    float middle = (low + high) * 0.5f;
                    if (passArena.Contains(Vector3.Lerp(nose, next, middle), 1.8f)) low = middle;
                    else high = middle;
                }
                next = Vector3.Lerp(nose, next, low);
            }
            nose = next;
            if ((nose - route[route.Count - 1]).sqrMagnitude >= 0.8f * 0.8f) route.Add(nose);
            UpdateHead(1f);
            // Sweep the jaws so fast exit movement cannot skip a player between frames.
            if (DistanceToSegment(player.transform.position, oldMouth, Mouth) < 2.3f) Capture();
            else if (!missed && Vector3.Dot(Horizontal(player.transform.position - Mouth), direction) <= 0f)
            {
                missed = true;
                direction = velocity.normalized;
            }
            if (!Captured && atEdge)
            {
                retained = true;
                head.gameObject.SetActive(false);
            }
            BuildSkin(1f);
            if (!Captured) DamageTail();
        }

        private void DamageTail()
        {
            if (Time.time < nextSkinHit) return;
            for (int i = 0; i < route.Count; i++)
            {
                Vector3 end = i + 1 < route.Count ? route[i + 1] : nose;
                if (DistanceToSegment(player.transform.position, route[i], end) > Radius + 0.3f) continue;
                nextSkinHit = Time.time + 0.5f;
                if (player.TakeHit(damage * 1.2f, Combat.DamageType.Physical)) player.Poison(damage * 0.33333334f, 2f);
                break;
            }
        }

        public void Withdraw()
        {
            if (endedAt < 0f) endedAt = Time.time;
            if (emergenceWarning != null) { Destroy(emergenceWarning); emergenceWarning = null; }
        }

        private void UpdateHead(float growth)
        {
            float caught = Captured ? Time.time - capturedAt : -1f;
            // The skull has a deeper lower jaw than the body: lift its pivot clear of
            // the floor while the body itself remains in contact with the ground.
            Vector3 headAt = nose;
            headAt.y = Mathf.Max(nose.y, floorY + Radius * 1.85f);
            if (caught >= 0f) headAt.y += Mathf.SmoothStep(0f, 2.4f, Mathf.Clamp01((caught - 0.35f) / 0.55f));
            head.SetPositionAndRotation(headAt, Quaternion.LookRotation(Vector3.up, direction));
            if (caught >= 0f)
            {
                float tilt = 48f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((caught - 0.38f) / 0.38f))
                    * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((caught - 0.95f) / 0.35f)));
                float shake = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(caught / 0.12f))
                    * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((caught - 0.95f) / 0.48f)));
                head.localRotation *= Quaternion.Euler(tilt + Mathf.Sin(caught * 17f) * 4f * shake,
                    Mathf.Sin(caught * 23f) * 9f * shake, Mathf.Sin(caught * 19f + 0.6f) * 6f * shake);
            }
            else head.localRotation *= Quaternion.Euler(Mathf.Sin(age * 6f) * 2.5f,
                Mathf.Sin(age * 4f) * 3f, Mathf.Sin(age * 5f) * 2f);
            head.localScale = headScale * growth;
            // On this giant head even a modest hinge angle creates a wide mouth.
            // Divide the opening between both halves so the lower jaw does not
            // swing several metres beneath the floor.
            float open = caught < 0f ? PursuitOpenDegrees : Mathf.Lerp(PursuitOpenDegrees + 4f, 0f, Mathf.Clamp01((caught - 0.16f) / 0.25f));
            skull.localRotation = Quaternion.Euler(open * 0.7f, 0f, 0f);
            jaw.localRotation = Quaternion.Euler(-open * 0.65f, 0f, 0f);
            // Use the actual jaw/skull geometry: the mouth can extend well below its
            // damage collider, especially while it pitches and shakes during swallowing.
            float lowest = float.MaxValue;
            foreach (Renderer part in headRenderers)
                if (part != null && part.enabled && part.gameObject.activeInHierarchy)
                    lowest = Mathf.Min(lowest, part.bounds.min.y);
            if (lowest < floorY + 0.18f)
                head.position += Vector3.up * (floorY + 0.18f - lowest);
        }

        private void Capture()
        {
            capturedAt = Time.time;
            controls = player.GetComponent<PlayerController>(); playerBody = player.GetComponent<CharacterController>();
            controlsEnabled = controls != null && controls.enabled; bodyEnabled = playerBody != null && playerBody.enabled;
            if (controls != null) controls.enabled = false;
            if (playerBody != null) playerBody.enabled = false;
            victimModel = player.transform.Find("Model");
            if (victimModel != null) { modelScale = victimModel.localScale; modelVisible = victimModel.gameObject.activeSelf; }
            CameraSystem.CameraFollow.Shake(0.25f, 0.2f);
        }

        private void Swallow()
        {
            float t = Time.time - capturedAt;
            // The jaws snap over the player first; the head then tips back to swallow.
            player.transform.position = Mouth + Vector3.down * 0.7f;
            if (victimModel != null)
            {
                victimModel.localScale = modelScale * Mathf.Lerp(1f, 0.05f, Mathf.Clamp01((t - 0.12f) / 0.18f));
                if (t >= 0.30f) victimModel.gameObject.SetActive(false);
            }
            if (!swallowHit && t >= 0.35f)
            {
                swallowHit = true;
                player.TakeHit(SwallowDamage, Combat.DamageType.Physical);
            }
            if (t >= 1.65f)
            {
                Vector3 outAt = Mouth + direction * 3f; outAt.y = Debris.GroundBelow(outAt + Vector3.up * 5f) + 1.1f;
                player.transform.position = outAt;
                Release(); endedAt = Time.time;
                Physics.SyncTransforms();
            }
        }

        private void Release()
        {
            if (victimModel != null) { victimModel.localScale = modelScale; victimModel.gameObject.SetActive(modelVisible); victimModel = null; }
            if (controls != null) { controls.enabled = controlsEnabled && player != null && !player.IsDead; controls = null; }
            if (playerBody != null) { playerBody.enabled = bodyEnabled && player != null && !player.IsDead; playerBody = null; }
        }

        private void BuildSkin(float growth)
        {
            int count = route.Count + 1;
            const int sides = 8;
            var vertices = new Vector3[count * sides];
            var indices = new int[(count - 1) * sides * 6];
            for (int i = 0; i < count; i++)
            {
                Vector3 center = i < route.Count ? route[i] : nose;
                Vector3 tangent = i == count - 1 ? direction : (i + 1 < route.Count ? route[i + 1] - center : nose - center);
                if (tangent.sqrMagnitude < 0.0001f) tangent = direction;
                Vector3 across = Vector3.Cross(Vector3.up, tangent.normalized).normalized;
                if (across.sqrMagnitude < 0.01f) across = Vector3.right;
                for (int j = 0; j < sides; j++)
                {
                    float a = j * Mathf.PI * 2f / sides;
                    vertices[i * sides + j] = center + (across * Mathf.Cos(a) + Vector3.up * Mathf.Sin(a)) * Radius * growth;
                    if (i == count - 1) continue;
                    int at = (i * sides + j) * 6, v = i * sides + j, next = i * sides + (j + 1) % sides;
                    indices[at] = v; indices[at + 1] = next; indices[at + 2] = v + sides;
                    indices[at + 3] = next; indices[at + 4] = next + sides; indices[at + 5] = v + sides;
                }
            }
            mesh.Clear(); mesh.vertices = vertices; mesh.triangles = indices; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        }

        private static Vector3 Horizontal(Vector3 v) { v.y = 0f; return v; }
        private static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 segment = Horizontal(b - a), offset = Horizontal(point - a);
            float t = segment.sqrMagnitude > 0.001f ? Mathf.Clamp01(Vector3.Dot(offset, segment) / segment.sqrMagnitude) : 0f;
            return (offset - segment * t).magnitude;
        }
        private void OnDestroy() { CameraSystem.CameraFollow.SustainedShake = 0f; Release(); if (!edgePass && owner != null && !owner.IsDead) owner.Immune = previousImmunity; if (mesh != null) Destroy(mesh); if (emergenceWarning != null) Destroy(emergenceWarning); }
    }
}
