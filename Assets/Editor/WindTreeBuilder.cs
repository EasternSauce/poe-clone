using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using PoeClone.Visuals;

namespace PoeClone.EditorTools
{
    /// <summary>Bakes reusable branching trees; never generates per-instance geometry at runtime.</summary>
    public static class WindTreeBuilder
    {
        private const string MeshDir = "Assets/Meshes/Stylized";
        private const string MatDir = "Assets/Materials/Stylized";
        private const string PrefabDir = "Assets/Prefabs/Environment";

        public static readonly string[] Species = { "Oak", "Pine", "Birch", "Spruce", "Willow", "Cypress", "Acacia", "Palm", "OrchardSnag", "CharredSnag", "FrostPine", "FrostSpruce" };

        public static void AssignSpecies(PoeClone.World.AreaKit kit)
        {
            kit.trees = new GameObject[Species.Length];
            for (int i = 0; i < Species.Length; i++)
                kit.trees[i] = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{Species[i]}.prefab");
        }

        [MenuItem("PoeClone/Rebuild Wind Trees")]
        public static void BuildAll()
        {
            foreach (string species in Species)
                BuildSpecies(species, species == "Pine" || species == "Spruce" || species.StartsWith("Frost"));
            var kit = AssetDatabase.LoadAssetAtPath<PoeClone.World.AreaKit>("Assets/Resources/AreaKit.asset");
            if (kit != null) { AssignSpecies(kit); EditorUtility.SetDirty(kit); }
            AssetDatabase.SaveAssets();
        }

        public static GameObject BuildSpecies(string name, bool pine)
        {
            var variants = new Mesh[6];
            var offeringTips = new Vector3[variants.Length];
            for (int i = 0; i < variants.Length; i++)
            {
                var generator = new TreeMesh(907 + i * 317 + (pine ? 71 : 0), i, name.Replace("Frost", ""));
                Mesh mesh = generator.Generate(pine);
                offeringTips[i] = generator.OfferingTip;
                string path = $"{MeshDir}/{name.Replace("Frost", "")}Wind{i}.asset";
                variants[i] = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (variants[i] == null) { AssetDatabase.CreateAsset(mesh, path); variants[i] = mesh; }
                else { EditorUtility.CopySerialized(mesh, variants[i]); Object.DestroyImmediate(mesh); }
            }
            var materials = new Material[3];
            string[] sources = { "Bark", pine ? "PineA" : "OakA", pine ? "PineB" : "OakB" };
            for (int i = 0; i < sources.Length; i++)
            {
                string path = $"{MatDir}/{name}Wind{sources[i]}.mat";
                var source = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{sources[i]}.mat");
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(source); AssetDatabase.CreateAsset(material, path); }
                else material.CopyPropertiesFromMaterial(source);
                material.shader = Shader.Find("PoeClone/ToonLit");
                material.SetFloat("_ObjectTexture", 2);
                material.SetFloat("_TreeWind", 1);
                material.SetFloat("_RimIntensity", 0.06f);
                if (name == "Birch") material.SetColor("_BaseColor", i == 0 ? new Color(0.74f,0.71f,0.61f) : new Color(0.49f,0.59f,0.22f));
                if (name == "Willow") material.SetColor("_BaseColor", i == 0 ? new Color(0.29f,0.25f,0.17f) : new Color(0.29f,0.43f,0.22f));
                if (name == "Cypress" || name == "Spruce") material.SetColor("_BaseColor", i == 0 ? new Color(0.26f,0.22f,0.18f) : new Color(0.14f,0.28f,0.23f));
                if (name == "Acacia" || name == "Palm") material.SetColor("_BaseColor", i == 0 ? new Color(0.39f,0.29f,0.17f) : new Color(0.38f,0.43f,0.19f));
                if (name == "OrchardSnag") material.SetColor("_BaseColor", new Color(0.30f,0.29f,0.18f));
                if (name == "CharredSnag") material.SetColor("_BaseColor", new Color(0.12f,0.105f,0.09f));
                if (name.StartsWith("Frost") && i > 0) material.SetColor("_BaseColor", i == 1 ? new Color(0.63f,0.75f,0.73f) : new Color(0.86f,0.91f,0.9f));
                materials[i] = material;
                EditorUtility.SetDirty(material);
            }
            // Edit the existing prefab to retain its root IDs and all authored placements.
            string prefabPath = $"{PrefabDir}/{name}.prefab";
            bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;
            GameObject root = existing ? PrefabUtility.LoadPrefabContents(prefabPath) : new GameObject(name);
            try
            {
                while (root.transform.childCount > 0) Object.DestroyImmediate(root.transform.GetChild(0).gameObject);
                var trunk = new GameObject("Trunk");
                trunk.transform.SetParent(root.transform, false);
                trunk.AddComponent<MeshFilter>().sharedMesh = variants[0];
                trunk.AddComponent<MeshRenderer>().sharedMaterials = materials;
                var tree = trunk.AddComponent<WindTree>();
                tree.variants = variants;
                tree.offeringTips = offeringTips;
                tree.offeringSocket = new GameObject("OfferingSocket").transform;
                tree.offeringSocket.SetParent(trunk.transform, false);
                tree.offeringSocket.localPosition = offeringTips[0];
                // The lower trunk is deliberately near the origin in every silhouette.
                var collider = root.GetComponent<CapsuleCollider>();
                if (collider == null) collider = root.AddComponent<CapsuleCollider>();
                collider.center = Vector3.up * 1.1f;
                collider.height = 2.2f;
                collider.radius = pine ? 0.32f : 0.4f;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { if (existing) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root); }
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        private sealed class TreeMesh
        {
            private readonly System.Random random;
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector2> motion = new List<Vector2>();
            private readonly List<Color32> colors = new List<Color32>();
            private readonly List<int>[] indices = { new List<int>(), new List<int>(), new List<int>() };
            public Vector3 OfferingTip { get; private set; }
            private float phase;
            // Broad, narrow, forked, wind-bent, sparse and low-spreading growth habits.
            private readonly int form;
            private readonly string species;
            private bool Bare => species == "OrchardSnag" || species == "CharredSnag";
            public TreeMesh(int seed, int form, string species) { random = new System.Random(seed); this.form = form; this.species = species; }
            private float R(float a, float b) => Mathf.Lerp(a, b, (float)random.NextDouble());
            private Vector3 Radial(float angle) => new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));

            private void Tri(int slot, Vector3 a, Vector3 b, Vector3 c, Vector3 outward, Vector3 anchor)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0) { Vector3 swap = b; b = c; c = swap; }
                float shade = R(0.88f, 1.08f);
                foreach (Vector3 p in new[] { a, b, c })
                {
                    indices[slot].Add(vertices.Count);
                    vertices.Add(p);
                    motion.Add(new Vector2(slot == 0 ? 0 : (p - anchor).magnitude, phase));
                    float ao = slot == 0 ? 1 : Mathf.Lerp(0.68f, 1.12f, Mathf.InverseLerp(1, 4.6f, p.y));
                    colors.Add((Color32)new Color(shade * ao, shade * ao, shade * ao, 1));
                }
            }

            private void Tube(Vector3 a, Vector3 b, float ra, float rb, int sides = 8)
            {
                Vector3 axis = (b - a).normalized;
                Vector3 x = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
                Vector3 z = Vector3.Cross(axis, x);
                for (int i = 0; i < sides; i++)
                {
                    Vector3 u = x * Mathf.Cos(i * Mathf.PI * 2 / sides) + z * Mathf.Sin(i * Mathf.PI * 2 / sides);
                    Vector3 v = x * Mathf.Cos((i + 1) * Mathf.PI * 2 / sides) + z * Mathf.Sin((i + 1) * Mathf.PI * 2 / sides);
                    Tri(0, a + u * ra, a + v * ra, b + v * rb, u + v, a);
                    Tri(0, a + u * ra, b + v * rb, b + u * rb, u + v, a);
                    Tri(0, b, b + u * rb, b + v * rb, axis, a);
                    Tri(0, a, a + v * ra, a + u * ra, -axis, a);
                }
            }

            private void Leaves(Vector3 center, Vector3 radii, Vector3 anchor, float yaw)
            {
                if (Bare) return;
                const int sides = 7, rings = 3;
                var points = new Vector3[rings + 1, sides];
                Quaternion rotation = Quaternion.Euler(R(-18, 18), yaw, R(-18, 18));
                for (int ring = 0; ring <= rings; ring++)
                    for (int side = 0; side < sides; side++)
                    {
                        float polar = ring * Mathf.PI / rings, angle = side * Mathf.PI * 2 / sides;
                        Vector3 unit = new Vector3(Mathf.Sin(polar) * Mathf.Cos(angle), Mathf.Cos(polar), Mathf.Sin(polar) * Mathf.Sin(angle));
                        // Shared ring vertices prevent cracks; lobes break the smooth ball outline.
                        float lobe = ring == 0 || ring == rings ? 1 : R(0.82f, 1.18f);
                        points[ring, side] = center + rotation * Vector3.Scale(unit * lobe, radii);
                    }
                int slot = random.Next(1, 3);
                for (int ring = 0; ring < rings; ring++)
                    for (int side = 0; side < sides; side++)
                    {
                        Vector3 a = points[ring, side], b = points[ring, (side + 1) % sides];
                        Vector3 c = points[ring + 1, (side + 1) % sides], d = points[ring + 1, side];
                        if (ring > 0) Tri(slot, a, b, c, (a + b + c) / 3 - center, anchor);
                        if (ring < rings - 1) Tri(slot, a, c, d, (a + c + d) / 3 - center, anchor);
                    }
            }

            private void DeadLimb(Vector3 from, Vector3 direction, float length, float radius, int depth)
            {
                Vector3 mid = from + direction * length * R(0.4f,0.6f);
                Vector3 bent = (direction + Radial(R(0,6.28f)) * 0.45f + Vector3.up * R(-0.2f,0.4f)).normalized;
                Vector3 tip = mid + bent * length * 0.5f;
                Tube(from,mid,radius,radius*0.6f,6);
                Tube(mid,tip,radius*0.6f,depth == 0 ? 0.004f : radius*0.3f,5);
                if (depth == 0) return;
                DeadLimb(tip,(bent + Radial(R(0,6.28f))*0.6f + Vector3.up*0.2f).normalized,length*0.6f,radius*0.3f,depth-1);
                DeadLimb(mid,(bent + Radial(R(0,6.28f))*0.9f).normalized,length*0.55f,radius*0.4f,depth-1);
            }

            private void Needles(Vector3 center, Vector3 radii, Vector3 anchor, float yaw)
            {
                // Ragged skirts of needles follow individual boughs, leaving air between tiers.
                int slot = random.Next(1,3);
                for (int layer=0; layer<2; layer++)
                {
                    Vector3 apex = center + Vector3.up * radii.y * (1.2f - layer*0.6f);
                    Vector3 baseCenter = center - Vector3.up * radii.y * layer*0.55f;
                    var rim = new Vector3[12];
                    for (int k=0; k<rim.Length; k++)
                    {
                        float angle = k*Mathf.PI*2/rim.Length + yaw;
                        float spread = k%2==0 ? R(0.85f,1.2f) : R(0.55f,0.8f);
                        rim[k] = baseCenter + new Vector3(Mathf.Cos(angle)*radii.x*spread, R(-0.12f,0.02f),Mathf.Sin(angle)*radii.z*spread);
                    }
                    for (int k=0; k<rim.Length; k++)
                    {
                        Vector3 a=rim[k], b=rim[(k+1)%rim.Length];
                        Tri(slot,apex,a,b,(a+b)*0.5f-center+Vector3.up*radii.y,anchor);
                        Tri(slot,baseCenter,a,b,Vector3.down,anchor);
                    }
                }
            }

            private void Frond(Vector3 start, Vector3 mid, Vector3 end)
            {
                Vector3 prev = start;
                for (int i = 1; i <= 8; i++)
                {
                    float t = i / 8f;
                    Vector3 point = (1-t)*(1-t)*start + 2*(1-t)*t*mid + t*t*end;
                    Tube(prev, point, 0.022f*(1-t)+0.006f,0.015f*(1-t)+0.003f,5);
                    Vector3 side = Vector3.Cross((point-prev).normalized,Vector3.up).normalized;
                    float width = Mathf.Sin(t*Mathf.PI)*0.4f;
                    foreach (int sign in new[] {-1,1})
                    {
                        Vector3 tip = point + side * width * sign + (point-prev)*0.7f - Vector3.up*0.12f;
                        Tri(1,prev,point,tip,Vector3.up,start);
                        Tri(1,point,prev,tip,Vector3.down,start);
                    }
                    prev=point;
                }
            }

            public Mesh Generate(bool pine)
            {
                float height = pine ? R(4.1f, 5.4f) : R(3.0f, 4.1f);
                if (species == "Birch" || species == "Cypress") height *= 1.35f;
                if (species == "OrchardSnag" || species == "Acacia") height *= 0.8f;
                if (form == 1) height *= 1.15f;
                if (form == 5) height *= 0.8f;
                float radius = pine || species == "Birch" || species == "Palm" ? 0.18f : 0.29f;
                Vector3 lean = Radial(R(0, 6.28f)) * (form == 3 ? 1.5f : R(0.25f, 0.65f));
                var spine = new Vector3[7];
                for (int i = 0; i < spine.Length; i++)
                {
                    float t = i / 6f;
                    spine[i] = Vector3.up * height * t + lean * t * t + Radial(t * 5 + phase) * Mathf.Sin(t * Mathf.PI) * 0.18f;
                    if (i > 0) Tube(spine[i - 1], spine[i], radius * Mathf.Pow(1 - (i - 1) / 7f, 1.3f), radius * Mathf.Pow(1 - i / 7f, 1.3f));
                }
                for (int i = 0; i < 5; i++)
                    Tube(Vector3.up * 0.35f, Radial(i * 1.256f + R(-0.3f, 0.3f)) * R(0.65f, 1.05f) + Vector3.down * 0.06f, radius * 0.7f, 0.025f, 6);

                if (species == "Oak" || species == "Pine" || species == "Birch")
                {
                    OfferingTip = spine[2] + new Vector3(0.85f, 0.1f, 0);
                    Tube(spine[2], OfferingTip, 0.065f, 0.025f, 6);
                }
                int branches = pine ? 17 : 9;
                if (species == "Palm") branches = 10;
                if (species == "Cypress") branches = 16;
                if (form == 4) branches = pine ? 10 : 6;
                if (Bare) branches = 5;
                if (species == "Willow") branches = 7;
                if (species == "Birch" || form == 2)
                {
                    Vector3 fork = spine[2], tip = spine[6] + Radial(2.1f) * 0.9f - Vector3.up * 0.2f;
                    Vector3 elbow = Vector3.Lerp(fork, tip, 0.5f) + Radial(2.1f) * 0.25f;
                    Tube(fork, elbow, radius * 0.7f, radius * 0.45f);
                    Tube(elbow, tip, radius * 0.45f, 0.025f);
                    Leaves(tip, new Vector3(0.7f,1.0f,0.65f), fork, 30);
                }
                for (int i = 0; i < branches; i++)
                {
                    phase = R(0, 6.28f);
                    float t = pine ? Mathf.Lerp(0.31f, 0.94f, i / (float)branches) : R(0.4f, 0.92f);
                    if (Bare) t = R(0.35f, 0.88f);
                    if (species == "Palm") t = R(0.94f, 0.99f);
                    if (species == "Acacia") t = R(0.55f, 0.86f);
                    if (species == "Cypress") t = Mathf.Lerp(0.25f,0.94f,i/(float)branches);
                    if (form == 5) t *= 0.85f;
                    float f = t * 6;
                    Vector3 start = Vector3.Lerp(spine[Mathf.FloorToInt(f)], spine[Mathf.CeilToInt(f)], f % 1);
                    float angle = i * 2.39996f + R(-0.4f, 0.4f);
                    Vector3 outward = Radial(angle);
                    float length = pine ? (1 - t) * R(1.9f, 2.7f) : R(1.05f, 1.8f);
                    if (form == 1) length *= 0.65f;
                    if (form == 5 || species == "Acacia") length *= 1.3f;
                    if (species == "Cypress") length = (1 - t) * 0.7f + 0.12f;
                    if (species == "Birch") length *= 0.65f;
                    if (species == "Palm") length = R(1.5f, 2.2f);
                    if (Bare)
                    {
                        DeadLimb(start, (outward * R(0.6f,1f) + Vector3.up * R(0.4f,1f)).normalized, length, radius * R(0.3f,0.5f), 2);
                        continue;
                    }
                    Vector3 mid = start + outward * length * 0.55f + Vector3.up * (pine ? -0.08f : 0.35f);
                    Vector3 end = start + outward * length + Vector3.up * (pine ? R(0.05f, 0.3f) : R(0.65f, 1.1f));
                    if (species == "Acacia") end.y = height + R(-0.1f, 0.25f);
                    if (species == "Palm") { mid = start + outward * length * 0.5f + Vector3.up * 0.5f; end = start + outward * length - Vector3.up * 0.4f; }
                    float thickness = pine ? 0.065f * (1 - t) + 0.018f : R(0.085f, 0.13f);
                    Tube(start, mid, thickness, thickness * 0.65f, 6);
                    Tube(mid, end, thickness * 0.65f, 0.018f, 6);
                    if (species == "Palm") { Frond(start, mid, end); continue; }
                    if (species == "Willow")
                    {
                        for (int strand = 0; strand < 3; strand++)
                        {
                            Vector3 top = end + Radial(angle + strand * 0.4f) * 0.45f;
                            Vector3 bottom = top + outward * 0.35f - Vector3.up * R(1.1f,2.1f);
                            Tube(end,top,0.03f,0.012f,5);
                            for (int k = 0; k < 3; k++) Leaves(Vector3.Lerp(top,bottom,k/2f),new Vector3(0.24f,0.45f,0.22f),start,angle*Mathf.Rad2Deg);
                        }
                    }
                    int forks = form == 4 ? 1 : 2;
                    for (int fork = -1; fork < forks; fork++)
                    {
                        Vector3 tip = end + Radial(angle + fork * 0.7f) * (pine ? 0.18f : 0.48f) + Vector3.up * R(0.05f, 0.3f);
                        Tube(mid, tip, thickness * 0.5f, 0.009f, 5);
                        Vector3 radii = pine ? new Vector3(R(0.43f, 0.64f), R(0.24f, 0.4f), R(0.32f, 0.5f))
                            : new Vector3(R(0.6f, 0.88f), R(0.45f, 0.68f), R(0.55f, 0.85f));
                        if (species == "Acacia") radii = new Vector3(0.9f,0.25f,0.75f);
                        if (species == "Cypress") radii = new Vector3(0.32f,0.7f,0.32f);
                        if (species == "Birch") radii = new Vector3(0.45f,0.65f,0.42f);
                        if (species == "Spruce") radii = new Vector3(0.5f,0.28f,0.35f);
                        if (species == "Willow") radii *= 0.75f;
                        if (pine) Needles(tip, radii, start, angle);
                        else Leaves(tip, radii, start, angle * Mathf.Rad2Deg);

                    }
                }
                if (pine) Needles(spine[6], new Vector3(0.3f,0.65f,0.3f), spine[5], 0);
                else if (species != "Palm") Leaves(spine[6] + Vector3.up * 0.1f, new Vector3(0.8f, 0.65f, 0.7f), spine[5], 0);
                var mesh = new Mesh { name = species + " growth form " + form };
                mesh.SetVertices(vertices); mesh.SetUVs(1, motion); mesh.SetUVs(2, vertices); mesh.SetColors(colors);
                mesh.subMeshCount = 3;
                for (int i = 0; i < 3; i++) mesh.SetTriangles(indices[i], i);
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                Bounds bounds = mesh.bounds; bounds.Expand(2f); mesh.bounds = bounds;
                return mesh;
            }
        }
    }
}
