using System;
using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.Inventory
{
    /// <summary>Origin-first spacing of an authored layout; never changes graph connections.</summary>
    public static class PassiveTreeSpacing
    {
        public const float BasicDiameter = 0.22f;
        public const float ConnectionPullDistance = 4 * BasicDiameter;
        public const int DeadEndLookahead = 5;
        private const float Margin = 0.00001f;

        /// <summary>Builds a deterministic layout using connections only, with origin at (0, 0).</summary>
        public static int ArrangeFromScratch(PassiveTreePositions layout, IReadOnlyList<PassiveNode> graph,
            string originId = PassiveTree.OriginId)
        {
            layout.Validate(graph);
            var nodes = new Dictionary<string, PassiveNode>();
            foreach (var node in graph) nodes.Add(node.Id, node);
            if (!nodes.ContainsKey(originId)) throw new InvalidOperationException("Missing tree origin.");
            var order = new List<string> { originId };
            var parents = new Dictionary<string, string> { { originId, null } };
            var children = new Dictionary<string, List<string>>();
            foreach (var node in graph) children.Add(node.Id, new List<string>());
            for (int i = 0; i < order.Count; i++)
            {
                string id = order[i];
                var links = new List<string>(nodes[id].Links);
                links.Sort(StringComparer.Ordinal);
                foreach (string link in links)
                {
                    if (!nodes.ContainsKey(link)) throw new InvalidOperationException("Unknown passive link: " + link);
                    if (parents.ContainsKey(link)) continue;
                    parents.Add(link, id);
                    children[id].Add(link);
                    order.Add(link);
                }
            }
            if (order.Count != graph.Count)
                throw new InvalidOperationException("Every passive must be reachable from the origin.");

            var compactPairs = DeadEndPairs(nodes, originId);
            var weights = new Dictionary<string, int>();
            for (int i = order.Count - 1; i >= 0; i--)
            {
                int weight = 0;
                foreach (string child in children[order[i]]) weight += weights[child];
                weights.Add(order[i], Mathf.Max(1, weight));
            }
            // Give each spanning-tree subtree a sector. These seeds depend on topology, not X/Y.
            var sectors = new Dictionary<string, Vector2> { { originId, new Vector2(0, Mathf.PI * 2) } };
            var seeds = new Dictionary<string, Vector2> { { originId, Vector2.zero } };
            var directions = new Dictionary<string, Vector2>();
            foreach (string id in order)
            {
                Vector2 sector = sectors[id];
                float angle = sector.x;
                foreach (string child in children[id])
                {
                    float width = id == originId ? sector.y / children[id].Count : sector.y * weights[child] / weights[id];
                    sectors.Add(child, new Vector2(angle, width));
                    float heading = angle + width * 0.5f;
                    Vector2 direction = new Vector2(Mathf.Cos(heading), Mathf.Sin(heading));
                    directions.Add(child, direction);
                    seeds.Add(child, seeds[id] + direction * (Minimum(id, child, compactPairs) + Margin));
                    angle += width;
                }
            }
            var edges = new List<KeyValuePair<string, string>>();
            foreach (string id in order)
                foreach (string link in nodes[id].Links)
                    if (string.CompareOrdinal(id, link) < 0) edges.Add(new KeyValuePair<string, string>(id, link));

            var positions = new Dictionary<string, Vector2>(seeds);
            var locked = new List<string> { originId };
            var lockedIds = new HashSet<string> { originId };
            for (int i = 1; i < order.Count; i++)
            {
                string target = order[i], parent = parents[target];
                Vector2 heading = directions[target];
                Vector2 best = Resolve(target, seeds[target], heading, locked, positions, compactPairs);
                positions[target] = best;
                int bestConflicts = ConnectionConflicts(target, positions, nodes, edges, lockedIds);
                float bestCost = ArrangementCost(target, best, seeds[target], nodes, positions, lockedIds, compactPairs);
                Vector2 anchor = Vector2.zero;
                int neighbors = 0;
                foreach (string link in nodes[target].Links)
                    if (lockedIds.Contains(link)) { anchor += positions[link]; neighbors++; }
                anchor /= neighbors; // The BFS parent is always already locked.
                float step = Minimum(parent, target, compactPairs) + Margin;
                float baseAngle = Mathf.Atan2(heading.y, heading.x);
                var candidates = new List<Vector2>();
                // A small bounded search balances short connections with locked obstacles.
                for (int ring = 1; ring <= 2; ring++)
                    for (int turn = 0; turn < 12; turn++)
                    {
                        float angle = baseAngle + turn * (Mathf.PI * 2 / 12);
                        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        float radius = step * ring;
                        candidates.Add(anchor + direction * radius);
                    }
                var linkedLocks = new List<string>();
                foreach (string link in nodes[target].Links) if (lockedIds.Contains(link)) linkedLocks.Add(link);
                linkedLocks.Sort(StringComparer.Ordinal);
                for (int a = 0; a < linkedLocks.Count; a++)
                    for (int b = a + 1; b < linkedLocks.Count; b++)
                        AddCircleIntersections(candidates, positions[linkedLocks[a]], Minimum(target, linkedLocks[a], compactPairs) + Margin,
                            positions[linkedLocks[b]], Minimum(target, linkedLocks[b], compactPairs) + Margin);
                foreach (Vector2 proposed in candidates)
                {
                    Vector2 offset = proposed - anchor;
                    Vector2 direction = offset.sqrMagnitude > 1e-12f ? offset.normalized : heading;
                    Vector2 candidate = Resolve(target, proposed, direction, locked, positions, compactPairs);
                    positions[target] = candidate;
                    int conflicts = ConnectionConflicts(target, positions, nodes, edges, lockedIds);
                    float cost = ArrangementCost(target, candidate, seeds[target], nodes, positions, lockedIds, compactPairs);
                    if (conflicts < bestConflicts || (conflicts == bestConflicts && cost < bestCost))
                    {
                        best = candidate;
                        bestConflicts = conflicts;
                        bestCost = cost;
                    }
                }
                positions[target] = best;
                locked.Add(target);
                lockedIds.Add(target);
            }
            return Commit(layout, positions);
        }

        private static void AddCircleIntersections(List<Vector2> candidates, Vector2 a, float rA, Vector2 b, float rB)
        {
            Vector2 delta = b - a;
            float distance = delta.magnitude;
            if (distance < 1e-6f || distance > rA + rB || distance < Mathf.Abs(rA - rB)) return;
            float along = (rA * rA - rB * rB + distance * distance) / (2 * distance);
            float height = Mathf.Sqrt(Mathf.Max(0, rA * rA - along * along));
            Vector2 direction = delta / distance;
            Vector2 center = a + direction * along;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x) * height;
            candidates.Add(center + perpendicular);
            candidates.Add(center - perpendicular);
        }

        private static float ArrangementCost(string target, Vector2 point, Vector2 seed,
            Dictionary<string, PassiveNode> nodes, Dictionary<string, Vector2> positions,
            HashSet<string> locked, HashSet<string> compactPairs)
        {
            float cost = (point - seed).sqrMagnitude * 0.04f;
            foreach (string link in nodes[target].Links)
            {
                if (!locked.Contains(link)) continue;
                float stretch = Vector2.Distance(point, positions[link]) - Minimum(link, target, compactPairs);
                cost += stretch * stretch;
            }
            return cost;
        }

        public static int Apply(PassiveTreePositions layout, IReadOnlyList<PassiveNode> graph,
            string originId = PassiveTree.OriginId)
        {
            layout.Validate(graph);
            var nodes = new Dictionary<string, PassiveNode>();
            var positions = new Dictionary<string, Vector2>();
            foreach (var node in graph) nodes.Add(node.Id, node);
            foreach (var entry in layout.nodes) positions.Add(entry.id, new Vector2(entry.x, entry.y));
            if (!nodes.ContainsKey(originId)) throw new InvalidOperationException("Missing tree origin.");

            // Stable breadth-first order advances all branches one graph step at a time.
            var order = new List<string>();
            var seen = new HashSet<string> { originId };
            var parents = new Dictionary<string, string>();
            var queue = new Queue<string>();
            queue.Enqueue(originId);
            while (queue.Count > 0)
            {
                string id = queue.Dequeue();
                order.Add(id);
                var links = new List<string>(nodes[id].Links);
                links.Sort(StringComparer.Ordinal);
                foreach (string link in links)
                {
                    if (!nodes.ContainsKey(link)) throw new InvalidOperationException("Unknown passive link: " + link);
                    if (seen.Add(link))
                    {
                        parents.Add(link, id);
                        queue.Enqueue(link);
                    }
                }
            }
            if (order.Count != graph.Count)
                throw new InvalidOperationException("Every passive must be reachable from the origin.");

            var compactPairs = DeadEndPairs(nodes, originId);
            var edges = new List<KeyValuePair<string, string>>();
            foreach (var node in graph)
                foreach (string link in node.Links)
                    if (string.CompareOrdinal(node.Id, link) < 0) edges.Add(new KeyValuePair<string, string>(node.Id, link));
            var locked = new List<string>();
            var lockedIds = new HashSet<string>();
            int targetIndex;
            foreach (string current in order)
            {
                // Pull once when this node is reached, toward its already locked BFS parent.
                // Resolve overlaps before locking it; later pushes may stretch the connection again.
                if (parents.TryGetValue(current, out string parent))
                {
                    Vector2 delta = positions[current] - positions[parent];
                    if (delta.sqrMagnitude > ConnectionPullDistance * ConnectionPullDistance)
                    {
                        Vector2 direction = delta.normalized;
                        Vector2 start = positions[parent] + direction * ConnectionPullDistance;
                        positions[current] = ResolveWithConnectionGuard(current, start, direction,
                            locked, positions, compactPairs, nodes, edges, lockedIds);
                    }
                }
                locked.Add(current);
                lockedIds.Add(current);
                targetIndex = -1;
                foreach (string target in order)
                {
                    targetIndex++;
                    if (lockedIds.Contains(target)) continue;
                    float distance = Minimum(current, target, compactPairs);
                    Vector2 delta = positions[target] - positions[current];
                    if (delta.sqrMagnitude >= distance * distance) continue;
                    Vector2 direction = delta.sqrMagnitude > 1e-12f ? delta.normalized : Direction(targetIndex);
                    positions[target] = ResolveWithConnectionGuard(target, positions[target], direction,
                        locked, positions, compactPairs, nodes, edges, lockedIds);
                }
            }

            return Commit(layout, positions);
        }

        private static Vector2 ResolveWithConnectionGuard(string target, Vector2 start, Vector2 direction,
            List<string> locked, Dictionary<string, Vector2> positions, HashSet<string> compactPairs,
            Dictionary<string, PassiveNode> nodes, List<KeyValuePair<string, string>> edges, HashSet<string> lockedIds)
        {
            Vector2 best = Resolve(target, start, direction, locked, positions, compactPairs);
            positions[target] = best;
            int bestConflicts = ConnectionConflicts(target, positions, nodes, edges, lockedIds);
            if (bestConflicts > 0)
            {
                Vector2 sideways = new Vector2(-direction.y, direction.x);
                float pushLength = (best - start).magnitude;
                foreach (Vector2 alternative in new[] { sideways, -sideways, -direction })
                {
                    Vector2 candidate = Resolve(target, start + alternative * pushLength, alternative, locked, positions, compactPairs);
                    positions[target] = candidate;
                    int conflicts = ConnectionConflicts(target, positions, nodes, edges, lockedIds);
                    if (conflicts < bestConflicts || (conflicts == bestConflicts && (candidate - start).sqrMagnitude < (best - start).sqrMagnitude))
                    {
                        best = candidate;
                        bestConflicts = conflicts;
                    }
                }
            }
            return best;
        }

        private static int Commit(PassiveTreePositions layout, Dictionary<string, Vector2> positions)
        {
            // Commit only after the complete pass succeeds.
            int moved = 0;
            foreach (var entry in layout.nodes)
            {
                Vector2 next = positions[entry.id];
                if (float.IsNaN(next.x) || float.IsInfinity(next.x) || float.IsNaN(next.y) || float.IsInfinity(next.y))
                    throw new InvalidOperationException("Spacing produced a non-finite position.");
            }
            foreach (var entry in layout.nodes)
            {
                Vector2 next = positions[entry.id];
                if (entry.x != next.x || entry.y != next.y) moved++;
                entry.x = next.x;
                entry.y = next.y;
            }
            return moved;
        }

        private static Vector2 Direction(int index)
        {
            float angle = index * 2.39996323f; // Stable separation for coincident centers.
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        // Only score connections affected by this move. Shared endpoints are legitimate joins.
        private static int ConnectionConflicts(string target, Dictionary<string, Vector2> positions,
            Dictionary<string, PassiveNode> nodes, List<KeyValuePair<string, string>> edges, HashSet<string> locked)
        {
            int count = 0;
            foreach (var edge in edges)
            {
                Vector2 a = positions[edge.Key], b = positions[edge.Value];
                if (edge.Key != target && edge.Value != target)
                {
                    if (!locked.Contains(edge.Key) || !locked.Contains(edge.Value)) continue;
                    if (SegmentDistance(positions[target], a, b) < NodeRadius(nodes[target]) + 0.02f) count++;
                    continue;
                }
                string neighbor = edge.Key == target ? edge.Value : edge.Key;
                if (!locked.Contains(neighbor)) continue;
                foreach (var node in nodes.Values)
                    if (locked.Contains(node.Id) && node.Id != edge.Key && node.Id != edge.Value &&
                        SegmentDistance(positions[node.Id], a, b) < NodeRadius(node) + 0.02f) count++;
                foreach (var other in edges)
                {
                    if (!locked.Contains(other.Key) || !locked.Contains(other.Value)) continue;
                    if (other.Key == edge.Key || other.Key == edge.Value || other.Value == edge.Key || other.Value == edge.Value) continue;
                    Vector2 c = positions[other.Key], d = positions[other.Value];
                    if (Crosses(a, b, c, d)) count++;
                }
            }
            return count;
        }

        private static float NodeRadius(PassiveNode node)
        {
            return node.Keystone ? 0.212f : node.Notable ? 0.155f : node.Id == PassiveTree.OriginId ? 0.14f : BasicDiameter / 2;
        }

        private static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 delta = b - a;
            float t = delta.sqrMagnitude > 1e-12f ? Mathf.Clamp01(Vector2.Dot(point - a, delta) / delta.sqrMagnitude) : 0;
            return Vector2.Distance(point, a + delta * t);
        }

        private static float Cross(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }
        private static bool Crosses(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float abC = Cross(b - a, c - a), abD = Cross(b - a, d - a);
            float cdA = Cross(d - c, a - c), cdB = Cross(d - c, b - c);
            if (abC * abD < 0 && cdA * cdB < 0) return true;
            // Also include collinear overlaps and a line touching an unrelated endpoint.
            return SegmentDistance(a, c, d) < Margin || SegmentDistance(b, c, d) < Margin ||
                SegmentDistance(c, a, b) < Margin || SegmentDistance(d, a, b) < Margin;
        }

        private static Vector2 Resolve(string target, Vector2 start, Vector2 direction,
            List<string> locked, Dictionary<string, Vector2> positions, HashSet<string> compactPairs)
        {
            Vector2 point = start;
            // Local projections usually find a nearby valid position, even between several locks.
            for (int pass = 0; pass < 32; pass++)
            {
                bool changed = false;
                foreach (string id in locked)
                {
                    float radius = Minimum(id, target, compactPairs);
                    Vector2 delta = point - positions[id];
                    if (delta.sqrMagnitude >= radius * radius) continue;
                    point = positions[id] + (delta.sqrMagnitude > 1e-12f ? delta.normalized : direction) * (radius + Margin);
                    changed = true;
                }
                if (!changed) return point;
            }

            // Projections can oscillate in a narrow pocket. Exit the union of exclusion circles
            // along the original push ray, without moving any already locked position.
            var intervals = new List<Vector2>();
            foreach (string id in locked)
            {
                Vector2 offset = positions[id] - start;
                float along = Vector2.Dot(offset, direction);
                float radius = Minimum(id, target, compactPairs) + Margin;
                float discriminant = radius * radius - (offset.sqrMagnitude - along * along);
                if (discriminant < 0) continue;
                float extent = Mathf.Sqrt(discriminant);
                if (along + extent >= 0) intervals.Add(new Vector2(along - extent, along + extent));
            }
            intervals.Sort((a, b) => a.x.CompareTo(b.x));
            float travel = 0;
            foreach (Vector2 interval in intervals)
            {
                if (interval.x > travel) break;
                travel = Mathf.Max(travel, interval.y + Margin);
            }
            return start + direction * travel;
        }

        private static string Pair(string a, string b)
        {
            return string.CompareOrdinal(a, b) < 0 ? a + "\n" + b : b + "\n" + a;
        }

        private static float Minimum(string a, string b, HashSet<string> compactPairs)
        {
            return BasicDiameter * (compactPairs.Contains(Pair(a, b)) ? 2f : 3f);
        }

        private static HashSet<string> DeadEndPairs(Dictionary<string, PassiveNode> nodes, string originId)
        {
            var result = new HashSet<string>();
            foreach (var leaf in nodes.Values)
            {
                if (leaf.Id == originId || leaf.Links.Count != 1) continue;
                var tail = new List<string> { leaf.Id };
                string previous = leaf.Id;
                string current = leaf.Links[0];
                while (current != originId && nodes[current].Links.Count == 2 && tail.Count < DeadEndLookahead)
                {
                    tail.Add(current);
                    var links = nodes[current].Links;
                    string next = links[0] == previous ? links[1] : links[0];
                    previous = current;
                    current = next;
                }
                if (current != originId && nodes[current].Links.Count < 3) continue;
                tail.Add(current); // Only this tail and its attachment share the exception.
                for (int i = 0; i < tail.Count; i++)
                    for (int j = i + 1; j < tail.Count; j++) result.Add(Pair(tail[i], tail[j]));
            }
            return result;
        }
    }
}
