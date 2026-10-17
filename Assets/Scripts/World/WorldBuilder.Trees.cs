using UnityEngine;

namespace PoeClone.World
{
    public partial class WorldBuilder
    {
        // Deliberately explicit palettes: tree species never leak into unrelated climates.
        private GameObject BiomeTree(int area, Vector3 p)
        {
            int hash = unchecked(Mathf.RoundToInt(p.x * 17) * 73856093 ^ Mathf.RoundToInt(p.z * 17) * 19349663) & int.MaxValue;
            bool waterside = Shape(area).WaterDistance(p) > -12f;
            switch (area)
            {
                case Greenwood:
                    if (waterside) return kit.Tree("Willow");
                    return kit.Tree(hash % 7 == 0 ? "Spruce" : hash % 5 == 0 ? "Birch" : hash % 2 == 0 ? "Oak" : "Pine");
                case Haven: return kit.Tree(waterside ? "Willow" : hash % 3 == 0 ? "Birch" : "Oak");
                case Frozen: return kit.Tree(hash % 2 == 0 ? "FrostPine" : "FrostSpruce");
                case Ruins: return kit.Tree("CharredSnag");
                case Graveyard: return kit.Tree("Cypress");
                case Desert: return kit.Tree("Acacia");
                case Sahret: return kit.Tree("Palm");
                case Channels: return kit.Tree(waterside ? "Palm" : "Acacia");
                case DrownedWorks: case Reservoir: return kit.Tree(hash % 2 == 0 ? "Palm" : "Cypress");
                case Orchard: return kit.Tree("OrchardSnag");
                default: return null; // Salt flats, caves, buried wards and boss interiors stay treeless.
            }
        }

        private void BuildActTwoTrees(int area)
        {
            if (area != Desert && area != Sahret && area != Channels && area != DrownedWorks && area != Reservoir) return;
            currentArea = area;
            rng = new System.Random(18271 + area);
            Transform trees = Group("BiomeTrees_" + AreaNames[area]);
            AreaShape shape = Shape(area);
            // Dryland trees are sparse. Palms and cypress require water or an inhabited courtyard.
            for (int i = 0; i < 230; i++)
            {
                Vector3 p = shape.Center + Flat(R(-AreaShape.MaxRadius, AreaShape.MaxRadius), R(-AreaShape.MaxRadius, AreaShape.MaxRadius));
                float wet = shape.WaterDistance(p);
                if (!shape.Contains(p, 5) || !Free(p, 4.5f) || NearGate(area, p, 14)) continue;
                if (area == DrownedWorks || area == Reservoir || area == Channels)
                    if (wet < -15 || wet > -3) continue;
                if (area == Desert && i % 6 != 0) continue;
                if (area == Sahret && i % 5 != 0) continue;
                if ((area == Desert || area == Sahret) && shape.Contains(p, 18)) continue;
                if ((p - WaystoneSpot(area)).sqrMagnitude < 18 * 18 ||
                    (p - (shape.Center + AreaLayouts.BossLocal(area))).sqrMagnitude < 26 * 26 ||
                    (p - spawnPoints[area].position).sqrMagnitude < 18 * 18) continue;
                bool objective = false;
                foreach (string quest in AreaLayouts.QuestGroups(area))
                    foreach (Vector3 anchor in AreaLayouts.QuestAnchors[quest])
                        if ((p - shape.Center - anchor).sqrMagnitude < 14 * 14) objective = true;
                if (objective || HitsScenery(p + Vector3.up, 2f)) continue;
                GameObject prefab = BiomeTree(area, p);
                if (prefab == null) continue;
                PlanPrefab(prefab, trees, p, R(0, 360), Vector3.one * R(0.8f, 1.15f));
                Claim(p, 4.5f);
            }
        }
    }
}
