using UnityEngine;

namespace PoeClone.World
{
    public partial class WorldBuilder
    {
        // These adapters evaluate placement arguments now and construct geometry only
        // when its chunk is requested. Returned objects stay within their owning feature.
        private void PlanFlame(Transform parent, Vector3 position, float radius, Material material,
            float flatten = 1.5f, float shelter = 1)
        {
            if (!DeferContent(position, () => Flame(parent, position, radius, material, flatten, shelter)))
                Flame(parent, position, radius, material, flatten, shelter);
        }

        private void PlanIceSpike(Transform t, Vector3 p, float height, Vector3 euler)
        {
            if (!DeferContent(p, () => IceSpike(t, p, height, euler)))
                IceSpike(t, p, height, euler);
        }

        private void PlanBrazier(Transform t, Vector3 p)
        {
            if (!DeferContent(p, () => Brazier(t, p)))
                Brazier(t, p);
        }

        private void PlanGlow(Transform parent, Vector3 p, Color color, float range, float intensity, bool flicker = false)
        {
            if (!DeferContent(p, () => Glow(parent, p, color, range, intensity, flicker)))
                Glow(parent, p, color, range, intensity, flicker);
        }

        private void PlanPrefab(GameObject prefab, Transform t, Vector3 p, float yaw, Vector3 scale)
        {
            if (!DeferContent(p, () => Prefab(prefab, t, p, yaw, scale)))
                Prefab(prefab, t, p, yaw, scale);
        }

        private void PlanWeatheredSlab(Transform parent, string name, Vector3 p, Vector2 radii,
            float height, Material material, float yaw, bool solid = true, float topScale = 0.96f)
        {
            if (!DeferContent(p, () => WeatheredSlab(parent, name, p, radii, height, material, yaw, solid, topScale)))
                WeatheredSlab(parent, name, p, radii, height, material, yaw, solid, topScale);
        }

        private void PlanBox(Transform t, Vector3 p, Vector3 size, Material mat, bool solid = true, Vector3 euler = default)
        {
            if (!DeferContent(p, () => Box(t, p, size, mat, solid, euler)))
                Box(t, p, size, mat, solid, euler);
        }

        private void PlanLocalBox(Transform t, Vector3 p, Vector3 size, Material mat, bool solid = true, Vector3 euler = default)
        {
            if (!DeferContent(t.TransformPoint(p), () => LocalBox(t, p, size, mat, solid, euler)))
                LocalBox(t, p, size, mat, solid, euler);
        }

        private void PlanCyl(Transform t, Vector3 p, float radius, float height, Material mat, bool solid = true, Vector3 euler = default)
        {
            if (!DeferContent(p, () => Cyl(t, p, radius, height, mat, solid, euler)))
                Cyl(t, p, radius, height, mat, solid, euler);
        }

        private void PlanLocalCyl(Transform t, Vector3 p, float radius, float height, Material mat, bool solid = true)
        {
            if (!DeferContent(t.TransformPoint(p), () => LocalCyl(t, p, radius, height, mat, solid)))
                LocalCyl(t, p, radius, height, mat, solid);
        }

        private void PlanBall(Transform t, Vector3 p, float radius, Material mat, float flatten = 1f, bool solid = true)
        {
            if (!DeferContent(p, () => Ball(t, p, radius, mat, flatten, solid)))
                Ball(t, p, radius, mat, flatten, solid);
        }

        private void PlanCandles(Transform t, Vector3 p)
        {
            if (!DeferContent(p, () => Candles(t, p)))
                Candles(t, p);
        }

        private void PlanDeadTree(Transform t, Vector3 p, Material wood, float size = 1f)
        {
            if (!DeferContent(p, () => DeadTree(t, p, wood, size)))
                DeadTree(t, p, wood, size);
        }

        private void PlanBones(Transform t, Vector3 p)
        {
            if (!DeferContent(p, () => Bones(t, p)))
                Bones(t, p);
        }
    }
}
