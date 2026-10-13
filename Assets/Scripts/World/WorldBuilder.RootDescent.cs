using UnityEngine;
using PoeClone.Enemies;

namespace PoeClone.World
{
    public partial class WorldBuilder
    {
        private void BuildRootDescent(int area)
        {
            PrepareWaterRoute(area, 19013 + area * 47, new Vector3(0, 0, -116));
            Transform ward = Group(AreaNames[area]);
            Vector3 c = Center(area);
            AreaShape shape = Shape(area);
            bool roots = area == FirstRoot;
            var stone = new Material(desertStone) { name = roots ? "RootFlesh" : "BuriedWardStone" };
            stone.SetColor("_BaseColor", roots ? new Color(0.40f, 0.23f, 0.28f) : new Color(0.37f, 0.48f, 0.44f));
            Material wood = kit.Mat("DeadWood");

            // Scenery follows chamber edges, leaving routes, objectives and boss courts open.
            for (int z = -146; z <= 146; z += 14)
                for (int x = -136; x <= 136; x += 14)
                {
                    Vector3 at = c + new Vector3(x + R(-2, 2), 0, z + R(-2, 2));
                    if (!shape.Contains(at, -5) || shape.Contains(at, 5) || RootDescentLandmark(area, at, 13)) continue;
                    if (roots)
                    {
                        for (int strand = 0; strand < 3; strand++)
                            PlanBox(ward, at + new Vector3(strand * 0.7f, 0.7f, 0), new Vector3(0.6f, R(2, 4), 0.7f),
                                wood, false, new Vector3(0, R(0, 360), R(-40, 40)));
                        PlanCyl(ward, at + Vector3.up * 0.8f, 0.65f, 1.2f, stone, false);
                    }
                    else
                    {
                        PlanBox(ward, at + Vector3.up, new Vector3(4, 2, 1), stone, false, new Vector3(0, R(0, 180), 0));
                        for (int bar = -2; bar <= 2; bar++)
                            PlanBox(ward, at + new Vector3(bar * 0.5f, 2.4f, 0), new Vector3(0.09f, 1.3f, 0.09f), kit.Mat("Iron"), false);
                    }
                }

            foreach (string group in AreaLayouts.QuestGroups(area))
                foreach (Vector3 local in AreaLayouts.QuestAnchors[group])
                {
                    Vector3 at = c + local;
                    PlanGlow(ward, at + new Vector3(5, 2, 4), roots ? new Color(0.85f, 0.30f, 0.39f) : new Color(0.36f, 0.78f, 0.72f), 18, 5);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 edge = at + new Vector3(side * 10, 0, 6);
                        PlanCyl(ward, edge + Vector3.up, roots ? 1.3f : 0.8f, 2, roots ? wood : stone, false);
                        PlanBones(ward, edge + Vector3.forward * 2);
                        if (!roots) SahretJar(ward, edge + Vector3.right * side * 2, 0.7f);
                    }
                }

            Vector3 boss = c + AreaLayouts.BossLocal(area);
            for (int i = 0; i < 10; i++)
            {
                float angle = i * 36;
                Vector3 edge = boss + Quaternion.Euler(0, angle, 0) * Vector3.forward * 25;
                if (RootDescentLandmark(area, edge, 8)) continue;
                PlanCyl(ward, edge + Vector3.up * 1.5f, roots ? 1.4f : 1, 3, roots ? wood : stone, false);
            }
            PlanGlow(ward, boss + Vector3.up * 4, roots ? new Color(0.9f, 0.3f, 0.4f) : new Color(0.3f, 0.7f, 0.68f), 28, 7);
            if (roots)
            {
                // The dead river enters a root-grown altar beyond the arena, below the heart objective.
                Vector3 altar = boss + new Vector3(0, 0, 28);
                for (int i = -3; i <= 3; i++)
                    PlanBox(ward, altar + new Vector3(i * 1.4f, 3, 2), new Vector3(1, 6 + Mathf.Abs(i), 1), wood, false,
                        new Vector3(0, 0, i * 9));
            }
            else
                DryChannel(ward, c, stone, new[] { new Vector3(0, 0, -116), new Vector3(-66, 0, -45),
                    new Vector3(-65, 0, 42), new Vector3(0, 0, 92) });
        }

        private bool RootDescentLandmark(int area, Vector3 at, float radius)
        {
            Vector3 c = Center(area);
            if ((at - (c + new Vector3(0, 0, -116))).sqrMagnitude < radius * radius ||
                (at - WaystoneSpot(area)).sqrMagnitude < radius * radius ||
                (at - (c + AreaLayouts.BossLocal(area))).sqrMagnitude < 22 * 22) return true;
            foreach (var link in AreaLayouts.Links)
            {
                int other = link.a == area ? link.b : link.b == area ? link.a : -1;
                if (other >= 0 && (at - (c + AreaLayouts.GateLocal(area, other))).sqrMagnitude < radius * radius) return true;
            }
            foreach (string group in AreaLayouts.QuestGroups(area))
                foreach (Vector3 local in AreaLayouts.QuestAnchors[group])
                    if ((at - (c + local)).sqrMagnitude < radius * radius) return true;
            return false;
        }

        private void ConfigureRootPopulation(EnemySpawner spawner, int area)
        {
            if (area != BuriedWard && area != FirstRoot) return;
            float[] entrance = area == BuriedWard
                ? Roster(("Drowned Fugitive", 4), ("Cistern Leech", 3), ("Ward Gaoler", 2))
                : Roster(("Grafted Stalker", 4), ("Marrow Tick", 3), ("Root Goliath", 2));
            float[] inner = area == BuriedWard
                ? Roster(("Ward Gaoler", 4), ("Memory Scribe", 3), ("Cistern Leech", 2))
                : Roster(("Root Goliath", 3), ("Sap Cantor", 3), ("Grafted Stalker", 2), ("Marrow Tick", 2));
            spawner.SetKindSelector(p => EnemyKinds.PickIndex((p - Center(area)).z < -25 ? entrance : inner));
        }
    }
}
