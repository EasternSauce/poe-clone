using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PoeClone.Visuals;
using PoeClone.UI;

namespace PoeClone.World
{
    public enum VillageActivity { Chat, Laundry, Garden, Sweep, Produce, WarmHands }

    /// <summary>Unimportant townsfolk travel safely between authored chore and social stops.</summary>
    [DefaultExecutionOrder(-20)]
    public sealed class VillageRoutine : MonoBehaviour
    {
        private const float WalkSpeed = 3.2f;
        private Npc npc;
        private CharacterController body;
        private CharacterWalkAnimator walk;
        private AreaShape shape;
        private Vector3[] stops;
        private Vector3 workFacing;
        private VillageActivity activity;
        private readonly List<Vector3> path = new List<Vector3>();
        private int stop, waypoint;
        private float wait, workBlend, workTime, blockedTime, seed;
        private bool planning, arrived;
        private GameObject tool;
        private Coroutine routeJob;
        private WindCloth[] laundry;
        private Vector3[] hangingPositions;
        private Vector3 basket;
        public void BindLaundry(WindCloth[] clothes, Vector3 basketPosition)
        {
            laundry = clothes; basket = basketPosition;
            hangingPositions = new Vector3[clothes.Length];
            for (int i = 0; i < clothes.Length; i++) hangingPositions[i] = clothes[i].transform.position;
        }

        private void AnimateLaundry(bool working)
        {
            if (laundry == null) return;
            if (laundry.Length == 0) return;
            int selected = Mathf.FloorToInt(workTime / 6) % laundry.Length;
            float cycle = Mathf.Repeat(workTime, 6);
            for (int i = 0; i < laundry.Length; i++)
            {
                float lift = Mathf.SmoothStep(0, 1, Mathf.Clamp01((cycle - 1.2f) / 1.6f));
                laundry[i].transform.position = working && i == selected
                    ? Vector3.Lerp(basket, hangingPositions[i], lift) : hangingPositions[i];
            }
        }
        public bool OwnsFacing { get; private set; }

        public void Configure(AreaShape area, VillageActivity chore, Vector3 facing, GameObject workTool, params Vector3[] destinations)
        {
            shape = area; activity = chore; stops = destinations; workFacing = facing; tool = workTool;
            npc = GetComponent<Npc>(); body = GetComponent<CharacterController>();
            walk = GetComponentInChildren<CharacterWalkAnimator>();
            seed = Mathf.Abs(transform.position.x * 0.37f + transform.position.z * 0.61f);
            Vector3 offset = transform.position - stops[0]; offset.y = 0;
            arrived = offset.sqrMagnitude < 0.1f;
            stop = arrived ? 0 : stops.Length - 1;
            wait = arrived ? 12 + seed % 8 : 1 + seed % 5;
        }

        private void Update()
        {
            if (stops == null || stops.Length == 0 || Time.deltaTime <= 0) return;
            bool talking = (DialogueUI.IsOpen && DialogueUI.Speaker == npc) || Player.NpcInteractor.RequestedNpc == npc;
            bool working = arrived && !talking && !planning && path.Count == 0 && wait > 0 && stop == 0;
            OwnsFacing = !talking && (working || path.Count > 0);
            workBlend = Mathf.MoveTowards(workBlend, working ? 1 : 0, Time.deltaTime * 2);
            if (working) workTime += Time.deltaTime;
            Pose();
            AnimateLaundry(working);
            if (tool != null) tool.SetActive(workBlend > 0.15f);
            if (body != null && body.enabled) body.Move(Vector3.down * (2 * Time.deltaTime));
            if (talking) return;
            if (working && workFacing.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(workFacing), 150 * Time.deltaTime);
            if (planning) return;
            if (wait > 0)
            {
                wait -= Time.deltaTime;
                if (wait <= 0)
                {
                    // A failed or obstructed route retries its destination instead of skipping the chore.
                    if (arrived) stop = (stop + 1) % stops.Length;
                    BeginRoute();
                }
                return;
            }
            if (path.Count == 0) { BeginRoute(); return; }
            Vector3 target = path[waypoint]; target.y = transform.position.y;
            Vector3 offset = target - transform.position;
            if (offset.sqrMagnitude < 0.10f)
            {
                if (++waypoint >= path.Count)
                {
                    arrived = true; path.Clear(); wait = stop == 0 ? 12 + seed % 8 : 5 + seed % 5;
                    workTime = 0;
                }
                return;
            }
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(offset), 180 * Time.deltaTime);
            Vector3 before = transform.position;
            float step = Mathf.Min(offset.magnitude, WalkSpeed * Time.deltaTime);
            Vector3 next = GroundObstacleMotion.Slide(body, before, before + offset.normalized * step);
            if (shape.Contains(next, 0.6f) && body != null && body.enabled) body.Move(next - before);
            blockedTime = (transform.position - before).sqrMagnitude < 0.00001f ? blockedTime + Time.deltaTime : 0;
            if (blockedTime > 2) { path.Clear(); wait = 2; blockedTime = 0; }
        }

        private void Pose()
        {
            if (walk == null) return;
            float beat = Mathf.Sin(workTime * 2.8f + seed);
            Vector2 arms = Vector2.zero, elbows = Vector2.zero;
            float lean = 0;
            switch (activity)
            {
                case VillageActivity.Laundry:
                    // Bend to the basket, lift both arms to the line, then peg the cloth.
                    float cycle = Mathf.Repeat(workTime, 6);
                    float reach = Mathf.SmoothStep(0, 1, Mathf.Clamp01((cycle - 1.2f) / 1.4f)) *
                        (1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((cycle - 4.6f) / 1.1f)));
                    arms = new Vector2(-25 - reach * 105, -20 - reach * 115 + beat * 5 * reach);
                    elbows = new Vector2(-20, -25 - reach * 25);
                    lean = cycle < 1.2f ? 23 * Mathf.Sin(cycle / 1.2f * Mathf.PI) : 0;
                    break;
                case VillageActivity.Garden:
                    arms = new Vector2(-25, -52 + beat * 12); elbows = new Vector2(-25, -35); lean = 16 + beat * 4; break;
                case VillageActivity.Sweep:
                    arms = new Vector2(-45 + beat * 13, -55 - beat * 20); elbows = new Vector2(-30, -20); lean = 12 + beat * 4; break;
                case VillageActivity.Produce:
                    arms = new Vector2(-40 - beat * 15, -55 + beat * 18); elbows = new Vector2(-35, -40); lean = 10; break;
                case VillageActivity.WarmHands:
                    arms = new Vector2(-65 + beat * 4, -65 - beat * 4); elbows = new Vector2(-20, -20); lean = 8; break;
                default:
                    arms = new Vector2(-15 - beat * 10, -40 + beat * 22); elbows = new Vector2(-15, -45); break;
            }
            walk.WorkBlend = workBlend; walk.WorkArms = arms; walk.WorkElbows = elbows; walk.WorkLean = lean;
        }

        private void BeginRoute()
        {
            path.Clear(); waypoint = 0; planning = true; arrived = false;
            routeJob = StartCoroutine(FindRoute());
        }

        // Bounded, time-sliced A*: avoids cottages, fences, water and furniture without a baked NavMesh.
        private IEnumerator FindRoute()
        {
            Vector2Int Grid(Vector3 p) => new Vector2Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.z));
            Vector3 World(Vector2Int p) => new Vector3(p.x, transform.position.y, p.y);
            var validity = new Dictionary<Vector2Int, bool>();
            var overlaps = new Collider[32];
            bool Open(Vector2Int p)
            {
                if (validity.TryGetValue(p, out bool cached)) return cached;
                Vector3 world = World(p);
                bool ok = shape.Contains(world, 0.7f);
                if (ok)
                {
                    int count = Physics.OverlapBoxNonAlloc(new Vector3(p.x, 1.05f, p.y), new Vector3(0.62f, 0.86f, 0.62f),
                        overlaps, Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    ok = count < overlaps.Length;
                    for (int i = 0; i < count && ok; i++)
                    {
                        Collider hit = overlaps[i];
                        if (hit is CharacterController || hit.transform.IsChildOf(transform) || hit.bounds.max.y <= 0.16f ||
                            hit.GetComponentInParent<Npc>() != null || hit.GetComponentInParent<Player.PlayerStats>() != null) continue;
                        ok = false;
                    }
                }
                validity[p] = ok; return ok;
            }
            Vector2Int start = Grid(transform.position), goal = Grid(stops[stop]);
            if (!Open(goal))
            {
                bool found = false;
                for (int radius = 1; radius <= 3 && !found; radius++)
                    for (int x = -radius; x <= radius && !found; x++)
                        for (int z = -radius; z <= radius && !found; z++)
                            if (Open(goal + new Vector2Int(x, z))) { goal += new Vector2Int(x, z); found = true; }
                if (!found) { planning = false; wait = 3; yield break; }
            }
            // Vector2Int is not IComparable. Even removing an entry compares it with itself,
            // so the default tuple comparer throws after matching score and order.
            var queue = new SortedSet<(float score, int order, Vector2Int point)>(
                Comparer<(float score, int order, Vector2Int point)>.Create((a, b) =>
                {
                    int score = a.score.CompareTo(b.score);
                    return score != 0 ? score : a.order.CompareTo(b.order);
                }));
            var cost = new Dictionary<Vector2Int, float> { [start] = 0 };
            var previous = new Dictionary<Vector2Int, Vector2Int>();
            var closed = new HashSet<Vector2Int>();
            int order = 0, expanded = 0;
            queue.Add((Vector2Int.Distance(start, goal), order++, start));
            while (queue.Count > 0 && expanded < 6000)
            {
                var entry = queue.Min; queue.Remove(entry);
                Vector2Int current = entry.point;
                if (!closed.Add(current)) continue;
                if (current == goal)
                {
                    for (var p = goal; p != start; p = previous[p]) path.Add(World(p));
                    path.Reverse(); planning = false; routeJob = null;
                    if (path.Count == 0) { arrived = true; wait = 10; }
                    yield break;
                }
                for (int x = -1; x <= 1; x++)
                    for (int z = -1; z <= 1; z++)
                    {
                        if (x == 0 && z == 0) continue;
                        Vector2Int next = current + new Vector2Int(x, z);
                        if (closed.Contains(next) || !Open(next)) continue;
                        if (x != 0 && z != 0 && (!Open(current + new Vector2Int(x, 0)) || !Open(current + new Vector2Int(0, z)))) continue;
                        float price = cost[current] + (x != 0 && z != 0 ? 1.414214f : 1);
                        if (cost.TryGetValue(next, out float old) && old <= price) continue;
                        cost[next] = price; previous[next] = current;
                        queue.Add((price + Vector2Int.Distance(next, goal), order++, next));
                    }
                if (++expanded % 40 == 0) yield return null;
            }
            planning = false; routeJob = null; wait = 3;
        }

        private void OnDisable()
        {
            if (routeJob != null) StopCoroutine(routeJob);
            routeJob = null; planning = false; path.Clear(); OwnsFacing = false;
            if (walk != null) walk.WorkBlend = 0;
            AnimateLaundry(false);
            if (tool != null) tool.SetActive(false);
        }
    }
}
