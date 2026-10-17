using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.World
{
    public partial class WorldBuilder
    {
        // One small mesh per terrain chunk, sharing the river material and a puddle-height capture.
        private void BuildRainPuddles(int area, Transform parent, Material water, Rect? region)
        {
            if (RainWeather.Intensity(area) == 0) return;
            AreaShape shape = Shape(area);
            Rect bounds = region ?? new Rect(-shape.Size * 0.5f, shape.Size);
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            const float spacing = 9f;
            const int sides = 20;
            for (int z = Mathf.FloorToInt(bounds.yMin / spacing); z <= Mathf.CeilToInt(bounds.yMax / spacing); z++)
                for (int x = Mathf.FloorToInt(bounds.xMin / spacing); x <= Mathf.CeilToInt(bounds.xMax / spacing); x++)
                {
                    // Local RNG keeps scenery, loot and neighbouring chunks independent of weather dressing.
                    var random = new System.Random(unchecked(area * 73856093 ^ x * 19349663 ^ z * 83492791));
                    float Next(float a, float b) => Mathf.Lerp(a, b, (float)random.NextDouble());
                    if (random.NextDouble() > 0.38) continue;
                    Vector2 p = new Vector2((x + Next(0.15f, 0.85f)) * spacing, (z + Next(0.15f, 0.85f)) * spacing);
                    if (!bounds.Contains(p)) continue;
                    Vector3 center = shape.Center + new Vector3(p.x, 0, p.y);
                    if (!shape.Contains(center, 3f) || shape.IsBridge(center, 3f) || shape.WaterDistance(center) > -3f) continue;
                    float rx = Next(0.65f, 1.65f), rz = rx * Next(0.45f, 0.8f), seed = Next(0, 6.28f);
                    Quaternion rotation = Quaternion.Euler(0, Next(0, 360), 0);
                    int first = vertices.Count;
                    vertices.Add(new Vector3(p.x, 0, p.y));
                    uvs.Add(Vector2.one * 0.5f);
                    for (int i = 0; i < sides; i++)
                    {
                        float angle = i * Mathf.PI * 2 / sides;
                        float edge = 1 + 0.13f * Mathf.Sin(angle * 3 + seed) + 0.07f * Mathf.Cos(angle * 5 - seed);
                        Vector2 radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        vertices.Add(new Vector3(p.x, 0, p.y) + rotation * new Vector3(radial.x * rx * edge, 0, radial.y * rz * edge));
                        uvs.Add(Vector2.one * 0.5f + radial * 0.5f);
                        triangles.Add(first); triangles.Add(first + (i + 1) % sides + 1); triangles.Add(first + i + 1);
                    }
                }
            if (vertices.Count == 0) return;
            var mesh = new Mesh { name = "Scattered rain puddles" };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            TrackChunkMesh(area, mesh);
            var renderer = WaterPart(parent, "RainPuddles", mesh, water, 0.025f);
            var properties = new MaterialPropertyBlock();
            properties.SetFloat("_Puddle", 1);
            renderer.SetPropertyBlock(properties);
            renderer.gameObject.AddComponent<PoeClone.Visuals.ReflectiveWaterSurface>().IsPuddle = true;
        }
    }
}
