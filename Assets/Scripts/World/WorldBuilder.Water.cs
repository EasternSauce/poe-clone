using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PoeClone.World
{
    public partial class WorldBuilder
    {
        private void BuildWater(int area, float floorY)
        {
            AreaShape shape = Shape(area);
            if (!shape.HasWater) return;
            Transform group = Group("Water_" + AreaNames[area]);
            group.position = shape.Center + Vector3.up * floorY;
            bool frozen = area == Frozen;
            var water = new Material(kit.Mat("Water")) { name = "Water_" + AreaNames[area] };
            Color color = frozen ? new Color(0.40f, 0.67f, 0.78f) :
                area == Graveyard ? new Color(0.13f, 0.29f, 0.28f) :
                shape.HasOcean ? new Color(0.10f, 0.38f, 0.49f) : new Color(0.16f, 0.42f, 0.48f);
            water.SetColor("_BaseColor", color);
            water.SetColor("_ShadowColor", color * 0.6f);
            water.SetFloat("_TriplanarTileSize", frozen ? 5f : 8f);
            water.SetFloat("_RimIntensity", 0.08f);
            water.SetFloat("_TexInfluence", frozen ? 0.24f : 0.12f);
            Mesh surface = shape.BuildWaterMesh();
            if (!frozen)
            {
                var motion = new System.Collections.Generic.List<Vector4>();
                foreach (Vector3 vertex in surface.vertices) motion.Add(shape.WaterMotion(vertex));
                surface.SetUVs(1, motion);
                // GPU waves need an expanded bound, but never change collision or shoreline logic.
                Bounds bounds = surface.bounds; bounds.Expand(Vector3.up * 0.2f); surface.bounds = bounds;
                water.SetFloat("_LivingWater", 1);
                water.SetFloat("_WaterOcean", shape.HasOcean ? 1 : 0);
            }
            WaterPart(group, frozen ? "FrozenRiverAndPools" : "RiversAndLakes", surface, water, -0.45f);
            var shore = new Material(kit.Mat(frozen ? "Snow" : shape.HasOcean ? "Tan" : "TanDark"));
            shore.SetFloat("_TriplanarTileSize", 3f);
            shore.SetFloat("_TexInfluence", 0.35f);
            if (!frozen && !shape.HasOcean)
            {
                // Wet mud strewn with gravel, rather than a flat brown band.
                shore.SetTexture("_BaseMap", GroundTextures.Dirt(17 + area, new Color(0.17f, 0.14f, 0.10f), new Color(0.33f, 0.27f, 0.19f),
                    new Color(0.46f, 0.45f, 0.41f), 260));
                shore.SetColor("_BaseColor", Color.white);
                shore.SetFloat("_TexInfluence", 1f);
            }
            WaterPart(group, shape.HasOcean ? "SandyBeach" : "Shore", shape.BuildShoreMesh(), shore, 0.012f);
            var timber = new Material(kit.Mat("Wood")) { name = "BridgeTimber" };
            timber.SetColor("_BaseColor", new Color(0.50f, 0.34f, 0.20f));
            timber.SetFloat("_TexInfluence", 0.28f);
            timber.SetFloat("_TriplanarTileSize", 2f);
            foreach (var bridge in shape.Bridges) BuildTimberBridge(group, shape, bridge.center, bridge.size, bridge.yaw, timber);
            Begin(area, 700 + area);
            foreach (var source in shape.RiverSources())
                BuildWaterfall(group, shape, source.point, source.downstream, source.halfWidth, water, frozen, floorY);
            if (!frozen && !shape.IsCave) DressLakeShores(group, shape, area, floorY);
        }

        // Where a river enters the area it pours over a rock ledge into its channel.
        private void BuildWaterfall(Transform parent, AreaShape shape, Vector2 source, Vector2 downstream, float halfWidth,
            Material riverWater, bool frozen, float floorY)
        {
            Transform fall = new GameObject("Waterfall").transform;
            fall.SetParent(parent, false);
            fall.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Vector3 down = new Vector3(downstream.x, 0f, downstream.y), side = Vector3.Cross(Vector3.up, down);
            Vector3 foot = shape.Center + new Vector3(source.x, floorY, source.y);
            float height = frozen ? 5f : 6.5f, reach = 3f, channel = 6f;
            Vector3 lip = foot - down * reach + Vector3.up * height;
            float Width(float u) => halfWidth * Mathf.Lerp(0.65f, 0.9f, u);

            // The sheet curls out from the lip, fed by a short channel across the ledge top.
            // UV1 carries flow (direction, distance travelled, strength) for the water shader.
            var vertices = new List<Vector3>();
            var motion = new List<Vector4>();
            var triangles = new List<int>();
            const int columns = 10, channelRows = 4, fallRows = 14;
            for (int i = 0; i <= channelRows + fallRows; i++)
            {
                bool falling = i > channelRows;
                float u = falling ? (i - channelRows) / (float)fallRows : 0f;
                float back = falling ? 0f : channel * (1f - i / (float)channelRows);
                for (int j = 0; j <= columns; j++)
                {
                    float v = j / (float)columns - 0.5f;
                    float bulge = falling ? 0.15f * Mathf.Sin(j * 1.9f + i * 0.7f) * Mathf.Sin(u * Mathf.PI) : 0f;
                    vertices.Add(lip + down * (reach * u * u + bulge - back) + side * (v * 2f * Width(u)) +
                        Vector3.up * (falling ? Mathf.Lerp(0f, -height - 0.5f, u) : -0.02f));
                    motion.Add(new Vector4(down.x, down.z, falling ? u * height * 1.4f : -back, frozen ? 0f : 1f));
                }
            }
            for (int i = 0; i < channelRows + fallRows; i++)
                for (int j = 0; j < columns; j++)
                {
                    int a = i * (columns + 1) + j, b = a + 1, c = a + columns + 1, d = c + 1;
                    // Both windings: the sheet is seen from the front and, on the ledge, from above.
                    triangles.AddRange(new[] { a, b, c, b, d, c, a, c, b, b, c, d });
                }
            var mesh = new Mesh { name = "WaterfallSheet" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(1, motion);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var sheetMaterial = new Material(riverWater) { name = "Waterfall" };
            Color tone = sheetMaterial.GetColor("_BaseColor");
            sheetMaterial.SetColor("_BaseColor", Color.Lerp(tone, Color.white, frozen ? 0.15f : 0.22f));
            var sheet = new GameObject("Sheet");
            sheet.transform.SetParent(fall, false);
            sheet.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = sheet.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = sheetMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            // The ledge: rock under the channel, taller crags either side, boulders stepping down to the banks.
            float Scale(float top) => top / 0.89f; // the rock mesh rises 0.89 above its pivot
            void Rock(Vector3 at, float top, float girth)
            {
                GameObject rock = Prefab(kit.rock, fall, at, R(0f, 360f), new Vector3(girth * R(0.9f, 1.15f), Scale(top), girth * R(1.2f, 1.6f)));
                if (frozen) Tint(rock, new Color(0.65f, 0.78f, 0.9f));
            }
            float topWidth = Width(0f);
            for (float back = 1.3f; back < channel + 2.5f; back += 2.2f)
                for (float across = -topWidth - 1f; across <= topWidth + 1f; across += 1.9f)
                    Rock(foot - down * (reach + back) + side * across, back > channel ? height + R(0.3f, 1.5f) : height - 0.12f, 1.5f);
            foreach (float sign in new[] { -1f, 1f })
            {
                for (float across = topWidth + 1.2f; across < topWidth + 13f; across += R(1.8f, 2.6f))
                    Rock(foot - down * (reach + R(0f, 6f)) + side * (sign * across), height * R(1.05f, 1.4f), R(1.4f, 2f));
                for (float across = Width(1f) + 0.6f; across < Width(1f) + 5f; across += R(1.6f, 2.2f))
                    Rock(foot - down * R(0.5f, reach) + side * (sign * across), height * R(0.25f, 0.6f), R(0.9f, 1.3f));
            }
            Claim(foot - down * reach, halfWidth + 6f);
            if (frozen) return;

            // Churned foam and drifting spray where the water lands.
            var foam = new Material(riverWater) { name = "WaterfallFoam" };
            foam.SetFloat("_LivingWater", 0);
            foam.SetColor("_BaseColor", new Color(0.82f, 0.9f, 0.92f));
            foam.SetColor("_ShadowColor", new Color(0.55f, 0.66f, 0.72f));
            for (float across = -Width(1f); across <= Width(1f); across += R(0.7f, 1.2f))
                Ball(fall, foot - down * R(0f, 0.8f) + side * across + Vector3.up * -0.38f, R(0.5f, 0.9f), foam, flatten: 0.3f, solid: false);
            WaterfallSpray(fall, foot + Vector3.up * 0.2f, down, Width(1f));
        }

        private static Material sprayMaterial;

        private static void WaterfallSpray(Transform parent, Vector3 at, Vector3 down, float halfWidth)
        {
            var go = new GameObject("Spray");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(down));
            var spray = go.AddComponent<ParticleSystem>();
            spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = spray.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            main.startColor = new Color(0.88f, 0.94f, 1f, 0.3f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;
            main.prewarm = true;
            main.cullingMode = ParticleSystemCullingMode.PauseAndCatchup;
            var emission = spray.emission;
            emission.rateOverTime = 4f + halfWidth * 2f;
            var shape = spray.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(halfWidth * 2f, 0.3f, 1.2f);
            var velocity = spray.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.5f, 1.3f);
            velocity.z = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
            var color = spray.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            var growth = spray.sizeOverLifetime;
            growth.enabled = true;
            growth.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.8f));
            if (sprayMaterial == null) sprayMaterial = new Material(Shader.Find("PoeClone/AmbientParticle"));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = sprayMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            spray.Play();
        }

        // Reeds in the shallows, pebbles on the bank and, in the green areas, lily pads.
        private void DressLakeShores(Transform parent, AreaShape shape, int area, float floorY)
        {
            bool green = area == Greenwood || area == Haven;
            Material reed = new Material(kit.Mat("Moss")) { name = "Reed" };
            reed.SetColor("_BaseColor", green ? new Color(0.42f, 0.5f, 0.22f) : new Color(0.33f, 0.31f, 0.24f));
            Material head = new Material(kit.Mat("Wood")) { name = "ReedHead" };
            head.SetColor("_BaseColor", new Color(0.3f, 0.19f, 0.11f));
            Material pad = new Material(kit.Mat("Moss")) { name = "LilyPad" };
            pad.SetColor("_BaseColor", new Color(0.24f, 0.42f, 0.16f));
            Material bloom = new Material(kit.Mat("Cloth")) { name = "LilyBloom" };
            bloom.SetColor("_BaseColor", new Color(0.95f, 0.82f, 0.88f));
            var batch = new PrimitiveBatch();
            float water = floorY - 0.45f;
            foreach (var shore in shape.LakeShores(3.2f))
            {
                Vector3 edge = shape.Center + new Vector3(shore.point.x, floorY, shore.point.y);
                Vector3 outward = new Vector3(shore.outward.x, 0f, shore.outward.y);
                if (!shape.Contains(edge + outward * 2f) || shape.IsBridge(edge, 3f)) continue;
                double roll = rng.NextDouble();
                if (roll < 0.4)
                {
                    Vector3 clump = edge - outward * R(0.3f, 1.4f);
                    int stalks = rng.Next(10, 18);
                    for (int k = 0; k < stalks; k++)
                    {
                        float tall = R(1f, 2.1f);
                        Quaternion lean = Quaternion.Euler(R(-12f, 12f), R(0f, 360f), R(-12f, 12f));
                        Vector3 root = clump + Flat(R(-0.9f, 0.9f), R(-0.9f, 0.9f));
                        root.y = water;
                        batch.Add(PrimitiveType.Cube, reed, root + lean * Vector3.up * (tall * 0.5f), lean, new Vector3(0.08f, tall, 0.08f));
                        if (k % 3 == 0)
                            batch.Add(PrimitiveType.Cube, head, root + lean * Vector3.up * (tall - 0.12f), lean, new Vector3(0.13f, 0.32f, 0.13f));
                    }
                }
                else if (roll < 0.6)
                {
                    for (int k = rng.Next(1, 4); k > 0; k--)
                        NoShadows(Prefab(kit.rockSmall, parent, edge + outward * R(-0.4f, 0.9f) + Flat(R(-0.8f, 0.8f), R(-0.8f, 0.8f)) +
                            Vector3.down * 0.15f, R(0f, 360f), Vector3.one * R(0.4f, 0.85f)));
                }
                else if (roll < 0.85 && green)
                {
                    Vector3 cluster = edge - outward * R(2f, 5f);
                    for (int k = rng.Next(3, 7); k > 0; k--)
                    {
                        Vector3 at = cluster + Flat(R(-1.2f, 1.2f), R(-1.2f, 1.2f));
                        at.y = water + 0.08f;
                        float size = R(0.5f, 0.85f);
                        batch.Add(PrimitiveType.Cylinder, pad, at, Quaternion.Euler(0f, R(0f, 360f), 0f), new Vector3(size * 2f, 0.01f, size * 2f));
                        if (rng.NextDouble() < 0.25)
                            batch.Add(PrimitiveType.Sphere, bloom, at + Vector3.up * 0.06f, Quaternion.identity, new Vector3(0.32f, 0.2f, 0.32f));
                    }
                }
            }
            batch.Build(parent, "LakeShoreDressing", shadows: false);
        }

        private void BuildTimberBridge(Transform parent, AreaShape shape, Vector2 center, Vector2 size, float yaw, Material timber)
        {
            // Find both banks across the full deck width, including oblique river crossings.
            Quaternion rotation = Quaternion.Euler(0, yaw, 0);
            float left = 0, right = 0;
            for (float x = -size.x * 0.5f; x <= size.x * 0.5f; x += 0.15f)
                for (int side = -1; side <= 1; side++)
                    if (shape.WaterDistance(shape.Center + new Vector3(center.x, 0, center.y) + rotation * new Vector3(x, 0, side * size.y * 0.5f)) >= 0)
                    { left = Mathf.Min(left, x); right = Mathf.Max(right, x); }
            left -= 1.1f; right += 1.1f;
            float length = right - left;
            Transform deck = new GameObject("TimberBridge").transform;
            deck.SetParent(parent, false);
            deck.localPosition = new Vector3(center.x, 0, center.y);
            deck.localRotation = rotation;
            float Height(float x) => 0.065f + 0.32f * Mathf.Sin(Mathf.Clamp01((x - left) / length) * Mathf.PI);
            int count = Mathf.CeilToInt(length / 0.52f);
            float step = length / count;
            for (int i = 0; i < count; i++)
            {
                float x = left + (i + 0.5f) * step;
                float slope = Mathf.Atan2(Height(x + 0.05f) - Height(x - 0.05f), 0.1f) * Mathf.Rad2Deg;
                float variation = Mathf.Sin(i * 2.71f + center.y) * 0.07f;
                LocalBox(deck, new Vector3(x, Height(x) - 0.055f, variation * 0.35f),
                    new Vector3(step - 0.018f, 0.11f, size.y + variation), timber, false, new Vector3(0, 0, slope));
            }
            // A continuous curved top collider lets feet follow the arch without board seams.
            var points = new Vector3[(count + 1) * 2];
            var indices = new int[count * 6];
            for (int i = 0; i <= count; i++)
            {
                float x = left + i * step;
                points[i * 2] = new Vector3(x, Height(x), -size.y * 0.5f);
                points[i * 2 + 1] = new Vector3(x, Height(x), size.y * 0.5f);
                if (i == count) continue;
                int v = i * 2, t = i * 6;
                indices[t] = v; indices[t + 1] = v + 1; indices[t + 2] = v + 2;
                indices[t + 3] = v + 2; indices[t + 4] = v + 1; indices[t + 5] = v + 3;
            }
            var collision = new Mesh { name = "ArchedBridgeDeck", vertices = points, triangles = indices };
            collision.RecalculateBounds();
            deck.gameObject.AddComponent<MeshCollider>().sharedMesh = collision;
            foreach (float side in new[] { -1f, 1f })
            {
                float z = side * (size.y * 0.5f + 0.14f);
                int sections = Mathf.CeilToInt(length / 2.8f);
                for (int i = 0; i <= sections; i++)
                {
                    float x = Mathf.Lerp(left + 0.2f, right - 0.2f, (float)i / sections);
                    LocalBox(deck, new Vector3(x, Height(x) + 0.48f, z), new Vector3(0.20f, 1.1f, 0.20f), timber);
                    if (i == sections) continue;
                    float next = Mathf.Lerp(left + 0.2f, right - 0.2f, (float)(i + 1) / sections);
                    Vector3 a = new Vector3(x, Height(x), z), b = new Vector3(next, Height(next), z);
                    float tilt = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
                    LocalBox(deck, (a + b) * 0.5f + Vector3.up * 0.9f,
                        new Vector3(Vector3.Distance(a, b) + 0.1f, 0.12f, 0.14f), timber, true, new Vector3(0, 0, tilt));
                    LocalBox(deck, (a + b) * 0.5f - Vector3.up * 0.19f,
                        new Vector3(Vector3.Distance(a, b) + 0.1f, 0.25f, 0.22f), timber, false, new Vector3(0, 0, tilt));
                }
                // Piles and diagonal braces seat the bridge into each bank.
                foreach (float x in new[] { left + 1.3f, right - 1.3f })
                {
                    LocalBox(deck, new Vector3(x, -0.35f, z), new Vector3(0.36f, 1.3f, 0.36f), timber, false);
                    LocalBox(deck, new Vector3(x + (x < 0 ? 0.4f : -0.4f), -0.18f, z),
                        new Vector3(1.25f, 0.16f, 0.18f), timber, false, new Vector3(0, 0, x < 0 ? 35 : -35));
                }
            }
        }

        private static void WaterPart(Transform parent, string name, Mesh mesh, Material material, float y)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * y;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
