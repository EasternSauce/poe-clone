using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PoeClone.Visuals;

namespace PoeClone.World
{
    public partial class WorldBuilder
    {
        private readonly Dictionary<int, Vector3[]> grassClearings = new Dictionary<int, Vector3[]>();
        private Material grassMaterial;
        private readonly Dictionary<int, List<Vector3>> roadClearings = new Dictionary<int, List<Vector3>>();

        private static bool HasGrass(int area) => area == Greenwood || area == Haven || area == Graveyard || area == Orchard;

        private IEnumerator BuildGrass(int area, GameObject ground)
        {
            if (!HasGrass(area) || ground == null) yield break;
            var floor = ground.GetComponent<Collider>();
            if (floor == null) yield break;
            if (grassMaterial == null)
                grassMaterial = new Material(Resources.Load<Shader>("Shaders/WindGrass")) { name = "Wind grass" };
            Vector3[] clearings = grassClearings[area];
            roadClearings.TryGetValue(area, out var roads);
            bool Clear(Vector3 point)
            {
                if (roads != null)
                    foreach (Vector3 site in roads)
                    {
                        float dx = point.x - site.x, dz = point.z - site.y;
                        if (dx * dx + dz * dz < site.z * site.z) return false;
                    }
                foreach (Vector3 site in clearings)
                {
                    float dx = point.x - site.x, dz = point.z - site.y;
                    if (dx * dx + dz * dz < site.z * site.z) return false;
                }
                return !Shape(area).IsBridge(point, 0.5f) &&
                    !Physics.CheckSphere(point + Vector3.up * 0.35f, 0.25f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            }
            yield return WindGrass.Build(floor, grassMaterial, area * 7919, area == Haven, Clear);
        }
    }
}
