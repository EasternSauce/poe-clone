using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.World
{
    public partial class WorldBuilder
    {
        private void DressCaveEdges(Transform parent, int area, List<(Vector3 a, Vector3 b)> boundary = null)
        {
            AreaShape shape = Shape(area);
            float walked = 0f;
            foreach (var edge in boundary ?? shape.Boundary)
            {
                walked += Vector3.Distance(edge.a, edge.b);
                if (walked < 12f) continue;
                walked = 0f;
                Vector3 at = shape.Center + (edge.a + edge.b) * 0.5f;
                Vector3 inward = Vector3.Cross(edge.b - edge.a, Vector3.up).normalized;
                if (!shape.Contains(at + inward * 0.5f)) inward = -inward;
                at += inward * 2.8f;
                // Wall shelves stay out of narrow tunnels, gate approaches and quest clearings.
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

        private GameObject CaveFormation(Transform parent, Vector3 at, Vector3 inward, bool wall)
        {
            Transform group = Holder(parent, wall ? "CaveDripShelf" : "CaveRockfall", at,
                Quaternion.LookRotation(inward));
            bool damp = rng.NextDouble() < (wall ? 0.7 : 0.45);
            var mesh = new PieceMesh();
            int stone = mesh.Slot(kit.Mat("RockDark"));
            int mineral = mesh.Slot(kit.Mat("Stone"));
            int moss = mesh.Slot(kit.Mat("Moss"));
            // Broad, low rockfall silhouettes alternate with slender mineral growths.
            float size = R(1.25f, 2.1f);
            GameObject rock = Prefab(kit.rock, group, at - (wall ? inward * 0.7f : Vector3.zero), R(0f, 360f),
                new Vector3(size, size * R(0.6f, 1.05f), size * R(0.65f, 0.95f)));
            rock.name = "CaveBoulder";
            // NaturalRock applies moss in its shader without a separate shell on the rock.
            var natural = rock.GetComponent<PoeClone.Visuals.NaturalRock>();
            if (natural != null) natural.mossCoverage = damp ? R(0.58f, 0.9f) : 0f;
            int spikes = rng.Next(3, 7);
            for (int k = 0; k < spikes; k++)
            {
                Vector3 foot = new Vector3(R(-2.2f, 2.2f), -0.08f, R(-1.2f, 1.5f));
                float height = R(0.65f, wall ? 2.6f : 3.4f);
                CaveSpike(mesh, k % 3 == 0 ? mineral : stone, foot, height, R(0.22f, 0.5f));
            }
            if (damp)
                for (int k = 0; k < 9; k++)
                    mesh.Lump(moss, new Vector3(R(-2.5f, 2.5f), 0.015f, R(-1.8f, 2f)),
                        new Vector3(R(0.3f, 0.9f), R(0.025f, 0.07f), R(0.25f, 0.7f)), 7, 3, Quaternion.identity);
            Vector3 dripTip = Vector3.zero;
            if (wall)
            {
                float ceiling = R(4.1f, 5.1f);
                // The shelf runs back into the wall, giving the hanging formations an anchor.
                mesh.Lump(stone, new Vector3(0f, ceiling + 0.35f, -1.8f),
                    new Vector3(3f, 0.85f, 2.1f), 11, 5, Quaternion.Euler(0f, R(-12, 12), 4f));
                for (int k = 0; k < 6; k++)
                {
                    Vector3 ceilingAt = new Vector3(R(-2f, 2f), ceiling, R(-1.6f, -0.3f));
                    if (k == 0) ceilingAt = new Vector3(2.1f, ceiling, 0.1f);
                    Vector3 tip = CaveSpike(mesh, mineral, ceilingAt, -R(0.8f, 1.9f), R(0.2f, 0.45f));
                    if (k == 0) dripTip = tip;
                    if (damp)
                        mesh.Lump(moss, ceilingAt + Vector3.up * 0.45f,
                            new Vector3(0.7f, 0.12f, 0.6f), 7, 3, Quaternion.identity);
                }
            }
            GameObject formation = mesh.Build(group, "WeatheredCaveStone");
            if (UsesChunks(CurrentArea())) TrackChunkMesh(CurrentArea(), formation.GetComponent<MeshFilter>().sharedMesh);
            // Only the low footprints block movement; overhead shelves do not snag projectiles.
            for (int k = 0; k < 2; k++)
            {
                var collider = group.gameObject.AddComponent<SphereCollider>();
                collider.center = new Vector3(k == 0 ? -1f : 1f, 0.6f, 0f);
                collider.radius = 1.1f;
            }
            if (wall && damp)
                group.gameObject.AddComponent<CaveDrips>().Initialize(dripTip, R(0.5f, 1.3f));
            return group.gameObject;
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
