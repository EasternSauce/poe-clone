using System.Collections.Generic;
using PoeClone.Enemies;
using UnityEngine;

namespace PoeClone.World
{
    /// <summary>Cosmetic wildlife: no combat components, colliders, drops or network identity.</summary>
    public sealed class AmbientAnimal : MonoBehaviour
    {
        public enum Species { Squirrel, Sheep, Snake, Swan, FrostHare, EmberLizard, Rabbit }
        public Species Kind { get; private set; }
        private AreaShape shape;
        private Transform player, visual, head, tail;
        private Transform[] legs, segments;
        private Vector3 home, target;
        private System.Random random;
        private float nextDecision, nextThreatCheck, fleeUntil, phase, motion;
        private float radius, walkSpeed;
        private Vector3 headRest, visualRest;
        private static readonly List<Transform> enemies = new List<Transform>();
        private static float nextEnemyRefresh;

        public void Initialize(Species species, int area, Transform playerTransform, int seed)
        {
            Kind = species;
            shape = WorldBuilder.Shape(area);
            player = playerTransform;
            random = new System.Random(seed);
            home = target = transform.position;
            radius = species == Species.Sheep ? 0.65f : 0.3f;
            walkSpeed = species == Species.Sheep ? 0.55f : species == Species.Snake ? 0.65f : 1.15f;
            phase = (float)random.NextDouble() * 20;
            visual = transform.Find("Visual");
            head = visual.Find("Head");
            tail = visual.Find("Tail");
            headRest = head.localPosition;
            visualRest = visual.localPosition;
            var limbList = new List<Transform>();
            var segmentList = new List<Transform>();
            foreach (Transform child in visual)
            {
                if (child.name.StartsWith("Leg")) limbList.Add(child);
                if (child.name.StartsWith("Segment")) segmentList.Add(child);
            }
            legs = limbList.ToArray();
            segments = segmentList.ToArray();
            nextDecision = Time.time + Range(0.5f, 4);
        }

        // The floor sits at Y=0. A low probe excludes trees, rocks, walls and buildings;
        // area geometry also keeps animals off water, bridges and outside cave corridors.
        public static bool CanOccupy(AreaShape area, Vector3 point, float clearance)
        {
            return area.Contains(point, clearance + 0.6f) && area.WaterDistance(point) < -clearance - 0.5f &&
                !area.IsBridge(point, clearance + 1) &&
                !Physics.CheckCapsule(point + Vector3.up * (clearance + 0.12f),
                    point + Vector3.up * (clearance + 0.65f), clearance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        private void Update()
        {
            if (shape == null || Time.deltaTime <= 0) return;
            // Distant wildlife sleeps; it is purely local scenery.
            if (player != null && (player.position - transform.position).sqrMagnitude > 85 * 85) return;
            if (Time.time >= nextThreatCheck)
            {
                nextThreatCheck = Time.time + 0.3f;
                Transform threat = NearestThreat();
                if (threat != null)
                {
                    Vector3 away = transform.position - threat.position;
                    away.y = 0;
                    if (away.sqrMagnitude < 0.01f) away = transform.forward;
                    ChooseTarget(away.normalized);
                    fleeUntil = Time.time + 1.6f;
                    nextDecision = fleeUntil + Range(1, 3);
                }
            }
            bool fleeing = Time.time < fleeUntil;
            if (!fleeing && Time.time >= nextDecision)
            {
                Vector3 direction = home - transform.position;
                if (direction.sqrMagnitude < 10 * 10)
                {
                    float angle = Range(0, Mathf.PI * 2);
                    direction = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                }
                ChooseTarget(direction.normalized);
                nextDecision = Time.time + Range(4, 9);
            }

            Vector3 toTarget = target - transform.position;
            toTarget.y = 0;
            bool moving = toTarget.sqrMagnitude > 0.04f;
            float speed = walkSpeed * (fleeing ? 3.5f : 1);
            if (moving)
            {
                Quaternion facing = Quaternion.LookRotation(toTarget);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, (fleeing ? 360 : 180) * Time.deltaTime);
                Vector3 step = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
                // Check ahead of the body as well as its destination to avoid clipping scenery.
                if (CanOccupy(shape, step + toTarget.normalized * 0.35f, radius)) transform.position = step;
                else { target = transform.position; nextDecision = Time.time + Range(0.5f, 1.5f); moving = false; }
            }
            motion = Mathf.MoveTowards(motion, moving ? 1 : 0, Time.deltaTime * 6);
            Animate(motion, fleeing);
        }

        private void ChooseTarget(Vector3 direction)
        {
            // Fan out from the desired direction, accepting only a wholly clear short path.
            for (int i = 0; i < 7; i++)
            {
                float turn = i == 0 ? 0 : (i % 2 == 0 ? 1 : -1) * ((i + 1) / 2) * 35;
                Vector3 heading = Quaternion.Euler(0, turn, 0) * direction;
                float distance = Range(2, 4.5f);
                bool clear = true;
                for (float d = 0.4f; d <= distance + 0.4f; d += 0.4f)
                    if (!CanOccupy(shape, transform.position + heading * d, radius)) { clear = false; break; }
                if (!clear) continue;
                target = transform.position + heading * distance;
                return;
            }
            target = transform.position;
        }

        private Transform NearestThreat()
        {
            if (Time.time >= nextEnemyRefresh || nextEnemyRefresh > Time.time + 1)
            {
                enemies.Clear();
                foreach (var enemy in FindObjectsByType<EnemyController>())
                    enemies.Add(enemy.transform);
                nextEnemyRefresh = Time.time + 0.75f;
            }
            Transform nearest = null;
            float best = 5.5f * 5.5f;
            if (player != null && (player.position - transform.position).sqrMagnitude < best)
            { nearest = player; best = (player.position - transform.position).sqrMagnitude; }
            foreach (Transform enemy in enemies)
            {
                if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
                float d = (enemy.position - transform.position).sqrMagnitude;
                if (d < best) { best = d; nearest = enemy; }
            }
            return nearest;
        }

        private void Animate(float moving, bool fleeing)
        {
            float cycle = Time.time * (fleeing ? 15 : 8) + phase;
            if (Kind == Species.Snake)
            {
                for (int i = 0; i < segments.Length; i++)
                {
                    Vector3 p = segments[i].localPosition;
                    p.x = Mathf.Sin(cycle - i * 0.65f) * 0.10f * moving;
                    segments[i].localPosition = p;
                }
                head.localPosition = headRest + Vector3.right * (Mathf.Sin(cycle + 0.65f) * 0.07f * moving);
                return;
            }
            for (int i = 0; i < legs.Length; i++)
                legs[i].localRotation = Quaternion.Euler(Mathf.Sin(cycle + (i == 0 || i == 3 ? 0 : Mathf.PI)) * 27 * moving, 0, 0);
            float hop = Kind == Species.FrostHare || Kind == Species.Rabbit ? 0.14f : Kind == Species.Squirrel ? 0.07f : 0.02f;
            visual.localPosition = visualRest + Vector3.up * (Mathf.Abs(Mathf.Sin(cycle)) * hop * moving);
            // Sheep lower their heads to graze; squirrels sniff and twitch their bushy tails.
            head.localRotation = Quaternion.Euler(Kind == Species.Sheep ? (1 - moving) * (23 + Mathf.Sin(Time.time * 1.8f + phase) * 8) : Mathf.Sin(Time.time * 2 + phase) * 8 * (1 - moving), 0, 0);
            if (tail != null) tail.localRotation = Quaternion.Euler(-20, Mathf.Sin(Time.time * 3 + phase) * 12, 0);
        }

        private float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
    }
}
