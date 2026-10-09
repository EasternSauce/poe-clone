using UnityEngine;
using PoeClone.Visuals;

namespace PoeClone.World
{
    public partial class WorldBuilder
    {
        private GameObject Flame(Transform parent, Vector3 position, float radius, Material material,
            float flatten = 1.5f, float shelter = 1)
        {
            GameObject flame = Ball(parent, position, radius, material, flatten, false);
            flame.name = "LivingFlame";
            LivingFlame.Attach(flame, shelter);
            return flame;
        }

        private GameObject LocalFlame(Transform parent, Vector3 position, float radius, Material material)
        {
            GameObject flame = LocalBall(parent, position, radius, material);
            flame.transform.localScale = new Vector3(radius * 2, radius * 3.5f, radius * 2);
            flame.name = "LivingCandleFlame";
            LivingFlame.Attach(flame, currentArea == Cave || currentArea == ActArena ? 0.15f : 0.65f);
            return flame;
        }

        private void BuildAmbientDetails()
        {
            Transform details = Group("WindblownDetails");
            // Small tied offerings on actual branches, rather than floating cloth near trees.
            GameObject trees = GameObject.Find("Trees");
            int count = 0;
            if (trees != null)
                foreach (Transform tree in trees.transform)
                {
                    if (!tree.gameObject.activeInHierarchy) continue;
                    var branch = new GameObject("BranchWithTiedRag").transform;
                    branch.SetParent(tree, false);
                    branch.localPosition = new Vector3(0, 1.8f, 0);
                    LocalBox(branch, new Vector3(0.55f, 0, 0), new Vector3(1.2f, 0.08f, 0.08f), kit.Mat("Bark"), false);
                    WindCloth.Create(branch, new Vector3(0.85f, 0, 0), 0.32f, 0.8f,
                        kit.Mat(count % 2 == 0 ? "ClothRed" : "ClothYellow"), true);
                    if (++count >= 4) break;
                }
            foreach (var renderer in FindObjectsByType<MeshRenderer>())
            {
                if ((renderer.name.StartsWith("Crown") || renderer.name.StartsWith("Cone")) && renderer.GetComponent<WindSway>() == null)
                    renderer.gameObject.AddComponent<WindSway>();
            }
            // A weather-beaten standard beside the road; no collision in the travel lane.
            WindBanner(details, Haven, new Vector3(111, 0, 13), 18, "ClothBlue", false);
            WindBanner(details, Ruins, new Vector3(-36, 0, -12), -22, "ClothRed", true);
            WindBanner(details, Graveyard, new Vector3(-18, 0, 18), 35, "ClothYellow", true);
        }

        private void WindBanner(Transform parent, int area, Vector3 local, float yaw, string cloth, bool torn)
        {
            AreaShape shape = Shape(area);
            Vector3 desired = Center(area) + local;
            if (!shape.Contains(desired, 1)) return;
            var pole = new GameObject(torn ? "AbandonedTornStandard" : "HavenRoadBanner").transform;
            pole.SetParent(parent, false);
            pole.SetPositionAndRotation(desired, Quaternion.Euler(0, yaw, 0));
            LocalCyl(pole, Vector3.up * 2.1f, 0.08f, 4.2f, kit.Mat("Wood"), false);
            LocalBox(pole, new Vector3(0.7f, 3.8f, 0), new Vector3(1.65f, 0.07f, 0.07f), kit.Mat("Wood"), false);
            WindCloth.Create(pole, new Vector3(0.8f, 3.76f, 0), 1.35f, 2.1f, kit.Mat(cloth), torn);
        }
    }
}
