using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.World
{
    public partial class WorldBuilder
    {
        private void DressCaveEdges(Transform parent, int area, List<(Vector3 a, Vector3 b)> boundary = null)
        {
            AreaShape shape = Shape(area);
            float walked = 0f, gap = R(6f, 20f);
            foreach (var edge in boundary ?? shape.Boundary)
            {
                walked += Vector3.Distance(edge.a, edge.b);
                if (walked < gap) continue;
                walked = 0f;
                // Uneven spacing and occasional bare stretches keep the walls from reading as a pattern.
                gap = R(6f, 20f);
                if (rng.NextDouble() < 0.2) continue;
                Vector3 at = shape.Center + (edge.a + edge.b) * 0.5f;
                Vector3 inward = Vector3.Cross(edge.b - edge.a, Vector3.up).normalized;
                if (!shape.Contains(at + inward * 0.5f)) inward = -inward;
                at += inward * R(2.2f, 3.4f);
                // Wall features stay out of narrow tunnels, gate approaches and quest clearings.
                if (!shape.Contains(at, 2.5f) || shape.IsBridge(at, 6f) ||
                    shape.WaterDistance(at) > -4f || NearGate(area, at, 15f) || !Free(at, 3.5f)) continue;
                bool quest = false;
                if (streamedAreas.TryGetValue(area, out var state))
                    foreach (var clearing in state.Clearances)
                        if ((at - clearing.point).sqrMagnitude < Mathf.Pow(clearing.radius + 4f, 2f)) quest = true;
                foreach (string key in AreaLayouts.QuestGroups(area))
                    foreach (Vector3 local in AreaLayouts.QuestAnchors[key])
                        if ((at - shape.Center - local).sqrMagnitude < 225f) quest = true;
                if (quest) continue;
                Vector3 position = at, direction = inward;
                ScheduleContent(position, () => CaveFormation(parent, position, direction, true));
                Claim(at, 3.5f);
            }
        }

        // Each site rolls its own kind of feature, so no two corners of a cave share one recipe.
        private GameObject CaveFormation(Transform parent, Vector3 at, Vector3 inward, bool wall)
        {
            // Floor features face anywhere; wall features keep their back to the rock face.
            Quaternion facing = wall ? Quaternion.LookRotation(inward) * Quaternion.Euler(0f, R(-25f, 25f), 0f)
                : Quaternion.Euler(0f, R(0f, 360f), 0f);
            Transform group = Holder(parent, wall ? "CaveWallFeature" : "CaveFloorFeature", at, facing);
            bool damp = rng.NextDouble() < (wall ? 0.6 : 0.4);
            var mesh = new PieceMesh();
            var bases = new List<(Vector3 point, float radius)>();
            Vector3? dripTip = null;
            double roll = rng.NextDouble();
            if (wall)
            {
                if (roll < 0.4) dripTip = CaveShelf(mesh, group, damp, bases);
                else if (roll < 0.7) CaveBoulders(group, Vector3.back * R(0.5f, 1.2f), damp, bases, rng.Next(1, 4));
                else CaveSpikeStand(mesh, group, new Vector3(R(-1f, 1f), 0f, -R(0.3f, 1f)), rng.Next(2, 6), R(1.4f, 3f), true, bases);
            }
            else if (roll < 0.35) CaveSpikeStand(mesh, group, Vector3.zero, rng.Next(1, 8), R(1.6f, 3.8f), false, bases);
            else if (roll < 0.65) CaveBoulders(group, Vector3.zero, damp, bases, rng.Next(1, 4));
            else if (roll < 0.85)
            {
                // A boulder with a few mineral growths pushing up beside it.
                CaveBoulders(group, Vector3.zero, damp, bases, 1);
                Vector2 side = Random2(R(1.3f, 2.2f));
                CaveSpikeStand(mesh, group, new Vector3(side.x, 0f, side.y), rng.Next(1, 4), R(1f, 2.4f), false, bases);
            }
            else
            {
                // A low, walkable rubble bed that is mostly moss.
                damp = true;
                CaveRubble(group, rng.Next(3, 7), R(1.2f, 2.4f));
                bases.Add((Vector3.zero, R(1.4f, 2.4f)));
            }
            if (damp) CaveMoss(mesh, bases);
            GameObject formation = mesh.Build(group, "WeatheredCaveStone");
            if (UsesChunks(CurrentArea())) TrackChunkMesh(CurrentArea(), formation.GetComponent<MeshFilter>().sharedMesh);
            if (dripTip.HasValue && damp && rng.NextDouble() < 0.75)
                group.gameObject.AddComponent<CaveDrips>().Initialize(dripTip.Value, R(0.4f, 1.4f));
            return group.gameObject;
        }

        // An overhang running back into the wall, with an uneven fringe of stalactites; returns a drip tip.
        private Vector3 CaveShelf(PieceMesh mesh, Transform group, bool damp, List<(Vector3, float)> bases)
        {
            int stone = mesh.Slot(kit.Mat("RockDark")), mineral = mesh.Slot(kit.Mat("Stone"));
            float ceiling = R(3.8f, 5.3f), width = R(1.4f, 3.6f);
            mesh.Lump(stone, new Vector3(R(-0.6f, 0.6f), ceiling + 0.35f, -1.8f),
                new Vector3(width, R(0.5f, 1f), R(1.5f, 2.4f)), 11, 5, Quaternion.Euler(R(-6f, 6f), R(-20f, 20f), R(-6f, 6f)));
            int count = rng.Next(1, 8);
            Vector3 tip = Vector3.zero;
            float longest = 0f;
            for (int k = 0; k < count; k++)
            {
                // Long formations favour the front lip; short nubs scatter further back.
                float x = R(-width, width) * 0.85f, front = R(0f, 1f);
                float length = Mathf.Lerp(0.35f, R(1.2f, 2.3f), front) * (1f - Mathf.Abs(x) / (width * 1.6f));
                Vector3 ceilingAt = new Vector3(x, ceiling + R(-0.15f, 0.15f), Mathf.Lerp(-1.7f, R(-0.4f, 0.1f), front));
                Vector3 end = CaveSpike(mesh, Coin() ? mineral : stone, ceilingAt, -length, R(0.12f, 0.2f + length * 0.14f));
                if (length > longest) { longest = length; tip = end; }
                if (damp && rng.NextDouble() < 0.35)
                    mesh.Lump(mesh.Slot(kit.Mat("Moss")), ceilingAt + Vector3.up * 0.4f,
                        new Vector3(R(0.3f, 0.8f), 0.1f, R(0.25f, 0.6f)), 7, 3, Quaternion.Euler(0f, R(0f, 360f), 0f));
            }
            // A stub on the floor under the longest stalactite, where its drip has built one up.
            if (rng.NextDouble() < 0.5)
            {
                Vector3 foot = new Vector3(tip.x + R(-0.15f, 0.15f), -0.06f, tip.z + R(-0.15f, 0.15f));
                CaveSpike(mesh, mineral, foot, R(0.3f, 1.1f), R(0.18f, 0.32f));
                bases.Add((foot, 0.45f));
                // The overhang itself is open below; only the floor stub blocks movement.
                AddFootprint(group, foot, 0.5f);
            }
            bases.Add((new Vector3(0f, 0f, -1.6f), width));
            return tip;
        }

        // A dominant stalagmite with smaller satellites falling away from it.
        private void CaveSpikeStand(PieceMesh mesh, Transform group, Vector3 centre, int count, float tallest, bool leanToWall,
            List<(Vector3, float)> bases)
        {
            int stone = mesh.Slot(kit.Mat("RockDark")), mineral = mesh.Slot(kit.Mat("Stone"));
            int primary = Coin() ? mineral : stone;
            float spread = 0.35f + count * R(0.18f, 0.32f);
            for (int k = 0; k < count; k++)
            {
                Vector2 offset = k == 0 ? Vector2.zero : Random2(spread * Mathf.Sqrt(R(0.15f, 1f)));
                Vector3 foot = centre + new Vector3(offset.x, -0.08f, offset.y);
                if (leanToWall) foot.z -= offset.magnitude * 0.3f;
                // Satellites shrink with distance from the main spire, with the odd tall outlier.
                float falloff = k == 0 ? 1f : Mathf.Clamp01(1f - offset.magnitude / (spread * 1.4f)) * R(0.35f, 0.85f);
                if (k > 0 && rng.NextDouble() < 0.12) falloff = R(0.7f, 0.95f);
                float height = Mathf.Max(0.3f, tallest * falloff);
                CaveSpike(mesh, rng.NextDouble() < 0.75 ? primary : (primary == stone ? mineral : stone),
                    foot, height, Mathf.Clamp(height * R(0.11f, 0.17f), 0.12f, 0.55f));
            }
            bases.Add((centre, spread));
            AddFootprint(group, centre, Mathf.Max(0.5f, spread * 0.8f));
        }

        private static void AddFootprint(Transform group, Vector3 local, float radius)
        {
            var collider = group.gameObject.AddComponent<SphereCollider>();
            collider.center = local + Vector3.up * 0.6f;
            collider.radius = radius;
        }

        // One to three boulders of clearly different sizes, leaning into each other.
        private void CaveBoulders(Transform group, Vector3 centre, bool damp, List<(Vector3, float)> bases, int count)
        {
            float size = R(1.1f, 2.4f);
            Vector3 at = centre;
            for (int k = 0; k < count; k++)
            {
                Vector3 scale = new Vector3(size * R(0.8f, 1.2f), size * R(0.5f, 1.05f), size * R(0.6f, 1f));
                GameObject rock = Prefab(kit.rock, group, group.TransformPoint(at), R(0f, 360f), scale);
                rock.name = "CaveBoulder";
                // NaturalRock applies moss in its shader without a separate shell on the rock.
                var natural = rock.GetComponent<PoeClone.Visuals.NaturalRock>();
                if (natural != null) natural.mossCoverage = damp && rng.NextDouble() < 0.8 ? R(0.35f, 0.9f) : 0f;
                bases.Add((at, size * 1.1f));
                Vector2 step = Random2(size * R(0.7f, 1.1f));
                at += new Vector3(step.x, 0f, step.y);
                size *= R(0.4f, 0.7f);
            }
        }

        private void CaveRubble(Transform group, int count, float spread)
        {
            for (int k = 0; k < count; k++)
            {
                Vector2 offset = Random2(spread * Mathf.Sqrt(R(0f, 1f)));
                GameObject stone = Prefab(kit.rockSmall, group, group.TransformPoint(new Vector3(offset.x, 0f, offset.y)),
                    R(0f, 360f), Vector3.one * R(0.25f, 0.7f));
                var natural = stone.GetComponent<PoeClone.Visuals.NaturalRock>();
                if (natural != null) natural.mossCoverage = R(0.3f, 0.95f);
            }
        }

        // Moss gathers in uneven patches hugging the bases of whatever stands at the site.
        private void CaveMoss(PieceMesh mesh, List<(Vector3 point, float radius)> bases)
        {
            int moss = mesh.Slot(kit.Mat("Moss"));
            foreach (var (point, radius) in bases)
            {
                if (rng.NextDouble() < 0.3) continue;
                int patches = rng.Next(1, 6);
                for (int k = 0; k < patches; k++)
                {
                    Vector2 offset = Random2(radius * R(0.5f, 1.2f));
                    mesh.Lump(moss, new Vector3(point.x + offset.x, 0.015f, point.z + offset.y),
                        new Vector3(R(0.2f, 0.9f), R(0.02f, 0.07f), R(0.2f, 0.7f)), 7, 3,
                        Quaternion.Euler(0f, R(0f, 360f), 0f));
                }
            }
        }

        private Vector2 Random2(float radius)
        {
            float angle = R(0f, Mathf.PI * 2f);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private Vector3 CaveSpike(PieceMesh mesh, int slot, Vector3 foot, float height, float radius)
        {
            Vector3 lean = new Vector3(R(-0.16f, 0.16f), 0, R(-0.16f, 0.16f));
            Vector3 previous = foot;
            float previousRadius = radius;
            for (int ring = 1; ring <= 5; ring++)
            {
                float t = ring / 5f;
                Vector3 next = foot + Vector3.up * (height * t) + lean * t * t;
                float nextRadius = ring == 5 ? 0.015f : radius * Mathf.Pow(1f - t, 1.3f) * R(0.92f, 1.08f);
                mesh.Frustum(slot, previous, next, previousRadius, nextRadius, 9, Vector3.right);
                previous = next;
                previousRadius = nextRadius;
            }
            return previous;
        }
    }
}
