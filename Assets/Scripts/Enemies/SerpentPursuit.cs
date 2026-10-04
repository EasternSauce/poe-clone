using System.Collections.Generic;
using UnityEngine;
using PoeClone.Player;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>A serpent grows along its chase route. Its retained body is a damaging escape-route hazard.</summary>
    public class SerpentPursuit : MonoBehaviour
    {
        private const float Radius = 1.5f;
        private const float Speed = 10f;
        private const float ChaseSeconds = 12f;
        private readonly List<Vector3> route = new List<Vector3>();
        private EnemyHealth owner;
        private PlayerStats player;
        private Transform head, skull, jaw;
        private Mesh mesh;
        private Vector3 nose, direction;
        private float age, endedAt = -1f, capturedAt = -1f, nextSkinHit, damage;
        private float headReach;
        private Vector3 headScale;
        private PlayerController controls;
        private CharacterController playerBody;
        private bool controlsEnabled, bodyEnabled;
        private Transform victimModel;
        private Vector3 modelScale;
        private bool modelVisible;
        private bool firstBite, secondBite;
        public bool IsFinished => endedAt >= 0f;
        public bool Captured => capturedAt >= 0f;
        public int RoutePoints => route.Count;

        public static SerpentPursuit Spawn(EnemyHealth boss, PlayerStats target, float hitDamage)
        {
            SnakeLimb source = boss.GetComponentInChildren<SnakeLimb>(true);
            foreach (SnakeLimb snake in boss.GetComponentsInChildren<SnakeLimb>(true))
                if (snake.name.StartsWith("ColossalSerpent")) { source = snake; break; }
            if (source == null || target == null) return null;
            var go = new GameObject("Pursuing colossal serpent");
            var pursuit = go.AddComponent<SerpentPursuit>();
            pursuit.owner = boss; pursuit.player = target; pursuit.damage = hitDamage;
            pursuit.direction = boss.transform.forward;
            Vector3 anchor = source.transform.position;
            Vector3 ground = anchor; ground.y = Debris.GroundBelow(anchor + Vector3.up * 5f) + Radius;
            pursuit.route.Add(anchor); pursuit.route.Add(ground);
            pursuit.nose = ground;
            pursuit.head = Instantiate(source.MouthTransform.gameObject, go.transform).transform;
            pursuit.head.name = "PursuitHead";
            pursuit.headScale = source.MouthTransform.lossyScale;
            pursuit.headReach = CarrionSaintLook.SerpentThickness * 0.75f * 1.7f * pursuit.headScale.y * 0.8f;
            pursuit.skull = pursuit.head.Find("Skull"); pursuit.jaw = pursuit.head.Find("Jaw");
            Renderer[] originals = source.MouthTransform.GetComponentsInChildren<Renderer>(true);
            Renderer[] copies = pursuit.head.GetComponentsInChildren<Renderer>(true);
            var colour = new MaterialPropertyBlock();
            for (int i = 0; i < originals.Length && i < copies.Length; i++) { originals[i].GetPropertyBlock(colour); copies[i].SetPropertyBlock(colour); }
            GameObject skin = RuntimePrimitives.Create(PrimitiveType.Cube, go.transform, new Color(0.22f, 0.29f, 0.16f));
            skin.name = "Extended serpent skin";
            pursuit.mesh = new Mesh { name = "Growing serpent route" };
            pursuit.mesh.MarkDynamic();
            skin.GetComponent<MeshFilter>().sharedMesh = pursuit.mesh;
            pursuit.UpdateHead(0.01f);
            return pursuit;
        }

        private Vector3 Mouth => nose + direction * headReach;

        private void Update()
        {
            if (owner == null || owner.IsDead || player == null || player.IsDead)
            {
                Release(); Destroy(gameObject); return;
            }
            age += Time.deltaTime;
            float growth = Mathf.SmoothStep(0.02f, 1f, Mathf.Clamp01(age / 0.65f));
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
            if (age >= 0.65f)
            {
                Vector3 to = player.transform.position - Mouth; to.y = 0f;
                if (to.sqrMagnitude > 0.01f)
                    direction = Vector3.RotateTowards(direction, to.normalized, 2.5f * Time.deltaTime, 0f).normalized;
                nose += direction * Speed * Time.deltaTime;
                if ((nose - route[route.Count - 1]).sqrMagnitude >= 0.8f * 0.8f) route.Add(nose);
                if (Horizontal(player.transform.position - Mouth).magnitude < 2.3f) Capture();
                else if (age > ChaseSeconds) endedAt = Time.time;
                if (!Captured && Time.time >= nextSkinHit)
                {
                    for (int i = 1; i < route.Count; i++)
                    {
                        if (DistanceToSegment(player.transform.position, route[i - 1], route[i]) > Radius + 0.3f) continue;
                        nextSkinHit = Time.time + 0.5f;
                        if (player.TakeHit(damage * 1.2f, Combat.DamageType.Physical)) player.Poison(damage * 0.5f, 2f);
                        break;
                    }
                }
            }
            UpdateHead(growth); BuildSkin(growth);
        }

        private void UpdateHead(float growth)
        {
            head.SetPositionAndRotation(nose, Quaternion.LookRotation(Vector3.up, direction));
            head.localScale = headScale * growth;
            float caught = Captured ? Time.time - capturedAt : -1f;
            float open = caught < 0f ? 32f : Mathf.Lerp(35f, 0f, Mathf.Clamp01((caught - 0.2f) / 0.35f));
            skull.localRotation = Quaternion.Euler(open * 0.4f, 0f, 0f);
            jaw.localRotation = Quaternion.Euler(-open, 0f, 0f);
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
            // Lift the captive, clamp the jaws, draw them down the throat, then spit them out.
            nose.y = Radius + Mathf.SmoothStep(0f, 5f, Mathf.Clamp01(t / 0.6f));
            player.transform.position = Mouth + Vector3.down * 0.7f - direction * Mathf.SmoothStep(0f, headReach * 0.65f, Mathf.Clamp01((t - 0.3f) / 0.4f));
            if (victimModel != null)
            {
                victimModel.localScale = modelScale * Mathf.Lerp(1f, 0.08f, Mathf.Clamp01((t - 0.35f) / 0.35f));
                if (t >= 0.7f) victimModel.gameObject.SetActive(false);
            }
            if (!firstBite && t >= 0.55f) { firstBite = true; player.TakeHit(damage * 2f, Combat.DamageType.Physical); }
            if (!secondBite && t >= 1f) { secondBite = true; player.TakeHit(damage * 2f, Combat.DamageType.Physical); }
            if (t >= 1.45f)
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
        private void OnDestroy() { Release(); if (mesh != null) Destroy(mesh); }
    }
}
