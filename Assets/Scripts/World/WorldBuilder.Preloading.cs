using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.World
{
    public partial class WorldBuilder
    {
        private readonly HashSet<int> preparedTerrain = new HashSet<int>();

        /// <summary>Prepare only sliced geometry; authored scenery still builds under the travel cover.</summary>
        public void PreloadTerrain(int area)
        {
            if (area < 0 || area >= Centers.Length || buildingArea ||
                builtAreas.Contains(area) || preparedTerrain.Contains(area)) return;
            StartCoroutine(PreloadTerrainRoutine(area));
        }

        private IEnumerator PreloadTerrainRoutine(int area)
        {
            // Share the build lock: travel and spectator snapshots must never consume half a mesh.
            buildingArea = true;
            try
            {
                yield return null;
                var shape = Shape(area);
                foreach (bool landOnly in new[] { false, true })
                {
                    var steps = shape.PrepareGroundMesh(landOnly);
                    try
                    {
                        while (true)
                        {
                            var manager = AreaManager.Instance;
                            // Once travel starts, finish the pending mesh so EnsureArea can take over.
                            // During play, give struggling frames a rest and respect pauses/overlays.
                            if (manager == null || !manager.IsSwitching)
                            {
                                if (Time.timeScale <= 0f || Time.unscaledDeltaTime > 1f / 30f ||
                                    (manager != null && manager.loadingScreen != null && manager.loadingScreen.IsShowing))
                                {
                                    yield return null;
                                    continue;
                                }
                            }
                            if (!steps.MoveNext()) break;
                            yield return null;
                        }
                    }
                    finally { (steps as System.IDisposable)?.Dispose(); }
                }
                preparedTerrain.Add(area);
            }
            finally { buildingArea = false; }
        }
    }
}
