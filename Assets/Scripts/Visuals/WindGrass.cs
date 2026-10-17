using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PoeClone.Visuals
{
    /// <summary>Small, culled meshes of rooted blades animated by the same breeze as trees and cloth.</summary>
    public sealed class WindGrass : MonoBehaviour
    {
        private Mesh mesh;
        private MeshRenderer view;
        private MaterialPropertyBlock properties;
        private static readonly int WindId = Shader.PropertyToID("_Wind");

        public static IEnumerator Build(Collider floor, Material material, int seed, bool trimmed, Func<Vector3, bool> clear)
        {
            Bounds bounds = floor.bounds;
            const float tile = 12f;
            float spacing = PoeClone.Inventory.TouchMode.Active ? 1.25f : 0.8f;
            var budget = System.Diagnostics.Stopwatch.StartNew();
            for (float z = bounds.min.z; z < bounds.max.z; z += tile)
                for (float x = bounds.min.x; x < bounds.max.x; x += tile)
                {
                    // Tile seeds make placement independent of the order in which areas stream in.
                    var random = new System.Random(unchecked(seed ^ Mathf.RoundToInt(x * 10f) * 73856093 ^ Mathf.RoundToInt(z * 10f) * 19349663));
                    float Rand(float a, float b) => Mathf.Lerp(a, b, (float)random.NextDouble());
                    var vertices = new List<Vector3>();
                    var motion = new List<Vector2>();
                    var colors = new List<Color>();
                    var indices = new List<int>();
                    Vector3 origin = new Vector3(x, bounds.max.y, z);
                    for (float pz = z; pz < Mathf.Min(z + tile, bounds.max.z); pz += spacing)
                        for (float px = x; px < Mathf.Min(x + tile, bounds.max.x); px += spacing)
                        {
                            Vector3 p = new Vector3(Mathf.Min(px + Rand(0f, spacing), bounds.max.x), bounds.max.y + 1f,
                                Mathf.Min(pz + Rand(0f, spacing), bounds.max.z));
                            if (!floor.Raycast(new Ray(p, Vector3.down), out RaycastHit hit, bounds.size.y + 2f) || hit.normal.y < 0.75f) continue;
                            p = hit.point;
                            if (!clear(p) || Mathf.PerlinNoise(p.x * 0.19f + 31f, p.z * 0.19f) < 0.32f) continue;
                            Color tone = Color.Lerp(new Color(0.16f, 0.24f, 0.075f), new Color(0.36f, 0.38f, 0.16f), Rand(0f, 1f));
                            for (int blade = 0; blade < 4; blade++)
                            {
                                Vector3 root = p - origin + new Vector3(Rand(-0.12f, 0.12f), 0.005f, Rand(-0.12f, 0.12f));
                                float height = Rand(0.22f, trimmed ? 0.38f : 0.65f), width = Rand(0.025f, 0.055f);
                                float angle = Rand(0f, Mathf.PI * 2f), phase = Rand(0f, Mathf.PI * 2f);
                                Vector3 side = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                                Vector3 lean = side * Rand(-0.12f, 0.12f);
                                int v = vertices.Count;
                                vertices.Add(root - side * width); vertices.Add(root + side * width);
                                vertices.Add(root + Vector3.up * height * 0.55f + lean * 0.3f - side * width * 0.55f);
                                vertices.Add(root + Vector3.up * height * 0.55f + lean * 0.3f + side * width * 0.55f);
                                vertices.Add(root + Vector3.up * height + lean);
                                for (int k = 0; k < 5; k++)
                                {
                                    float bend = k < 2 ? 0f : k < 4 ? 0.3f : 1f;
                                    motion.Add(new Vector2(bend * height, phase));
                                    colors.Add(tone * (k < 2 ? 0.55f : k < 4 ? 0.9f : 1.2f));
                                }
                                indices.Add(v); indices.Add(v + 2); indices.Add(v + 1);
                                indices.Add(v + 1); indices.Add(v + 2); indices.Add(v + 3);
                                indices.Add(v + 2); indices.Add(v + 4); indices.Add(v + 3);
                            }
                        }
                    if (vertices.Count > 0)
                    {
                        var go = new GameObject("WindGrass");
                        go.transform.SetParent(floor.transform, false);
                        go.transform.position = origin;
                        go.transform.rotation = Quaternion.identity;
                        go.transform.localScale = new Vector3(1f / floor.transform.lossyScale.x, 1f / floor.transform.lossyScale.y, 1f / floor.transform.lossyScale.z);
                        var patch = go.AddComponent<WindGrass>();
                        patch.mesh = new Mesh { name = "Grass blades" };
                        patch.mesh.SetVertices(vertices); patch.mesh.SetUVs(0, motion); patch.mesh.SetColors(colors);
                        patch.mesh.SetTriangles(indices, 0);
                        patch.mesh.RecalculateBounds();
                        Bounds animated = patch.mesh.bounds;
                        animated.Expand(1f); // Include the fully bent tips in frustum culling.
                        patch.mesh.bounds = animated;
                        go.AddComponent<MeshFilter>().sharedMesh = patch.mesh;
                        patch.view = go.AddComponent<MeshRenderer>();
                        patch.view.sharedMaterial = material;
                        patch.view.shadowCastingMode = ShadowCastingMode.Off;
                        patch.properties = new MaterialPropertyBlock();
                        patch.UpdateWind();
                        WorldActivity.TrackCosmetic(patch);
                    }
                    if (budget.Elapsed.TotalMilliseconds >= 2d)
                    {
                        yield return null;
                        budget.Restart();
                    }
                }
        }

        private void Update() { if (view.isVisible) UpdateWind(); }

        private void UpdateWind()
        {
            properties.SetVector(WindId, AmbientWind.At(view.bounds.center));
            view.SetPropertyBlock(properties);
        }

        private void OnDestroy()
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }
    }
}
