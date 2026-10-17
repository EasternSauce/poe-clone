using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PoeClone.Visuals;

namespace PoeClone.World
{
    public partial class WorldBuilder
    {
        // Unlike the scattering pass's claimed list, these survive every Begin() call.
        private readonly Dictionary<int, List<Vector3>> grassClearings = new Dictionary<int, List<Vector3>>();
        private Material grassMaterial;

        private static bool HasGrass(int area) => area == Greenwood || area == Haven || area == Graveyard || area == Orchard;

        private IEnumerator BuildGrass(int area, GameObject ground)
        {
            if (!HasGrass(area) || ground == null) yield break;
            var floor = ground.GetComponent<Collider>();
            if (floor == null) yield break;
            if (grassMaterial == null)
                grassMaterial = new Material(Resources.Load<Shader>("Shaders/WindGrass")) { name = "Wind grass" };
            grassClearings.TryGetValue(area, out var clearings);
            AreaShape shape = Shape(area);
            bool Clear(Vector3 point)
            {
                const float margin = 0.4f; // Leave room for scattered blade roots and wind-bent tips.
                if (shape.IsBridge(point, margin) || shape.IsShore(point, margin)) return false;
                if (clearings != null)
                    foreach (Vector3 site in clearings)
                    {
                        float dx = point.x - site.x, dz = point.z - site.y;
                        float radius = site.z + margin;
                        if (dx * dx + dz * dz < radius * radius) return false;
                    }
                return !Physics.CheckSphere(point + Vector3.up * 0.35f, 0.25f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            }
            yield return WindGrass.Build(floor, grassMaterial, area * 7919, area == Haven, Clear);
        }
    }
}
