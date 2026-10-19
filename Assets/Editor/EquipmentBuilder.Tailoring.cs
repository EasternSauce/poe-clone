using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PoeClone.EditorTools
{
    public static partial class EquipmentBuilder
    {
        private const string WearDir = "Assets/Materials/Equipment";
        private const string WearTextures = "Assets/Textures/Equipment";
        private static readonly Dictionary<string, Material> wear = new Dictionary<string, Material>();

        private static Mesh SaveWornMesh(Mesh mesh)
        {
            // Replacing the native buffer, as for anatomical meshes, also updates existing previews
            // when a rebuild changes the vertex/index layout. The existing .meta preserves its GUID.
            string path = LowPolyMeshes.MeshDir + "/" + mesh.name + ".asset";
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static void WearMaterials()
        {
            if (wear.Count > 0 && wear["Leather"] != null) return;
            EnsureFolder(WearDir);
            EnsureFolder(WearTextures);
            foreach (string surface in new[] { "Hide", "Weave", "Mail", "Forged" })
            {
                string path = WearTextures + "/" + surface + ".png";
                if (File.Exists(path)) continue;
                const int size = 256;
                var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Tileable grain, woven warp/weft, interlocking mail, and hammered metal.
                    float grain = Mathf.PerlinNoise(x * .125f, y * .125f);
                    float value;
                    if (surface == "Weave")
                        value = .72f + .12f * Mathf.Sin(x * Mathf.PI / 2) + .09f * Mathf.Sin(y * Mathf.PI / 2) + grain * .08f;
                    else if (surface == "Mail")
                    {
                        float row = Mathf.Floor(y / 16f);
                        float px = Mathf.Repeat(x + (row % 2) * 8f, 16f) / 8f - 1f;
                        float py = Mathf.Repeat(y, 16f) / 8f - 1f;
                        float ring = Mathf.Abs(Mathf.Sqrt(px * px + py * py) - .67f);
                        value = Mathf.Lerp(.24f, .85f, 1f - Mathf.SmoothStep(.07f, .22f, ring));
                    }
                    else value = (surface == "Hide" ? .68f : .76f) + grain * .22f;
                    texture.SetPixel(x, y, new Color(value, value, value));
                }
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.maxTextureSize = size;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            void Material(string key, string template, string hex, string texture, float tile, float rim)
            {
                string path = WearDir + "/Worn" + key + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/" + template + ".mat"));
                    AssetDatabase.CreateAsset(material, path);
                }
                Color color = H(hex);
                material.name = "Worn" + key;
                material.SetColor("_BaseColor", color);
                material.SetColor("_ShadowColor", color * new Color(.54f, .55f, .58f, 1f));
                material.SetColor("_RimColor", Color.Lerp(color, Color.white, .2f));
                material.SetFloat("_RimIntensity", rim);
                material.SetFloat("_RimPower", 5f);
                material.SetFloat("_Smoothness", .08f);
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_ObjectTexture", 1f);
                material.SetFloat("_TriplanarTileSize", tile);
                material.SetFloat("_TexInfluence", .65f);
                material.SetFloat("_ShadowBandSmooth", .18f);
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(WearTextures + "/" + texture + ".png"));
                EditorUtility.SetDirty(material);
                wear[key] = material;
            }
            Material("Leather", "TanLight", "#b79a72", "Hide", .55f, .025f);
            Material("LeatherDark", "Leather", "#786044", "Hide", .55f, .025f);
            Material("Linen", "GearCloth", "#d4ccb6", "Weave", .35f, .015f);
            Material("Teal", "GearClothTrim", "#405b56", "Weave", .35f, .015f);
            Material("Steel", "Steel", "#c0c4ba", "Forged", .45f, .065f);
            Material("Edge", "SteelDark", "#7a817a", "Forged", .45f, .04f);
            Material("Mail", "SteelDark", "#6e7673", "Mail", .32f, .025f);
            Material("Brass", "Gold", "#9d895c", "Forged", .4f, .045f);
            Material("Thread", "Rope", "#bca57c", "Weave", .2f, .015f);
        }

        private static void WearPalette(GameObject root, string id)
        {
            bool cloth = id.Contains("silk") || id == "hunter_hood";
            bool leather = id.Contains("leather") || id == "studded_vest";
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                string name = renderer.sharedMaterial.name;
                string key = name == "Silver" || name == "Steel" ? "Steel" :
                    name == "SteelDark" || name == "RustSteel" ? "Edge" :
                    name == "Gold" || name == "BronzeLight" || name == "Bronze" ? "Brass" :
                    name == "GearClothTrim" ? "Teal" :
                    name == "GearCloth" || name == "HoodCloth" ? "Linen" :
                    name == "TanLight" || name == "Tan" ? (cloth ? "Linen" : "Leather") :
                    name == "Leather" || name == "TanDark" ? (leather ? "LeatherDark" : "Edge") : null;
                if (key != null) renderer.sharedMaterial = wear[key];
            }
        }

        private static void TailorWearable(GameObject root, string id)
        {
            bool body = id == "studded_vest" || id == "chain_hauberk" || id == "silk_robe";
            bool boots = id == "leather_boots" || id == "iron_greaves" || id == "silk_slippers";
            bool head = id == "iron_helmet" || id == "bronze_helmet" || id == "hunter_hood" || id == "sage_circlet";
            if (!body && !boots && !head) return;
            WearMaterials();
            WearPalette(root, id);
            if (body) TailorBody(root, id);
            if (boots) TailorBoots(root, id);
            if (head) TailorHead(root, id);
        }

        // Details are baked once per material and socket, not dozens of runtime primitives.
        private sealed class WearDetail
        {
            private readonly string id;
            private readonly Transform parent;
            private readonly Dictionary<string, List<CombineInstance>> pieces = new Dictionary<string, List<CombineInstance>>();
            private readonly List<Mesh> temporary = new List<Mesh>();
            public WearDetail(string id, Transform parent) { this.id = id; this.parent = parent; }

            public void Patch(string material, Func<float, float, Vector3> point, int columns = 8, int rows = 6)
            {
                var vertices = new List<Vector3>();
                var triangles = new List<int>();
                for (int y = 0; y <= rows; y++)
                for (int x = 0; x <= columns; x++) vertices.Add(point(x / (float)columns, y / (float)rows));
                for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                {
                    int a = y * (columns + 1) + x, b = a + 1, c = a + columns + 1, d = c + 1;
                    triangles.AddRange(new[] { a, c, b, b, c, d });
                }
                var mesh = new Mesh();
                mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals();
                // Back faces need separate vertices; shared reversed triangles cancel the normals.
                var normals = new List<Vector3>(mesh.normals);
                int count = vertices.Count, faces = triangles.Count;
                for (int i = 0; i < count; i++) { vertices.Add(vertices[i]); normals.Add(-normals[i]); }
                for (int i = 0; i < faces; i += 3)
                    triangles.AddRange(new[] { triangles[i]+count, triangles[i+2]+count, triangles[i+1]+count });
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(triangles, 0);
                Add(material, mesh, Matrix4x4.identity);
            }

            public void Line(string material, Func<float, Vector3> point, float width, int steps = 12)
            {
                // One continuous six-sided cord, with shared rings instead of a primitive per segment.
                const int sides = 6;
                var vertices = new List<Vector3>();
                var triangles = new List<int>();
                for (int j = 0; j <= steps; j++)
                {
                    Vector3 center = point(j / (float)steps);
                    Vector3 tangent = (point(Mathf.Min(steps,j+1)/(float)steps) - point(Mathf.Max(0,j-1)/(float)steps)).normalized;
                    Vector3 normal = Vector3.Cross(tangent, Mathf.Abs(tangent.y) < .9f ? Vector3.up : Vector3.forward).normalized;
                    Vector3 binormal = Vector3.Cross(tangent, normal);
                    for (int i = 0; i < sides; i++)
                    {
                        float a = i * Mathf.PI * 2 / sides;
                        vertices.Add(center + (normal*Mathf.Cos(a)+binormal*Mathf.Sin(a))*width*.5f);
                        if (j == 0) continue;
                        int current = j*sides+i, next = j*sides+(i+1)%sides;
                        triangles.AddRange(new[] { current-sides,next-sides,current, current,next-sides,next });
                    }
                }
                var mesh = new Mesh();
                mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals();
                Add(material,mesh,Matrix4x4.identity);
            }

            private void Add(string material, Mesh mesh, Matrix4x4 matrix)
            {
                temporary.Add(mesh);
                if (!pieces.TryGetValue(material, out var list)) pieces[material] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mesh, transform = matrix });
            }

            public void Save()
            {
                string socket = parent.name;
                for (Transform p = parent.parent; p != null && p.parent != null; p = p.parent) socket = p.name + "_" + socket;
                foreach (var pair in pieces)
                {
                    string name = "Tailoring_" + pair.Key;
                    Transform old = parent.Find(name);
                    if (old != null) Object.DestroyImmediate(old.gameObject);
                    var mesh = new Mesh();
                    mesh.CombineMeshes(pair.Value.ToArray(), true, true);
                    mesh.name = "Wear_" + id + "_" + socket + "_" + pair.Key;
                    Mesh asset = SaveWornMesh(mesh);
                    var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(parent, false);
                    go.GetComponent<MeshFilter>().sharedMesh = asset;
                    var renderer = go.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = wear[pair.Key];
                    // Sub-centimetre seams cannot cast stable shadow-map silhouettes.
                    // The underlying garment already supplies the character's broad shadows.
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
                foreach (Mesh mesh in temporary) Object.DestroyImmediate(mesh);
            }
        }

        private static readonly LowPolyMeshes.Ring[] CoatRings = {
            new LowPolyMeshes.Ring(.5f,.2f,.24f), new LowPolyMeshes.Ring(.46f,.36f,.4f),
            new LowPolyMeshes.Ring(.36f,.48f,.46f), new LowPolyMeshes.Ring(.22f,.5f,.49f,.02f),
            new LowPolyMeshes.Ring(.02f,.44f,.48f,.02f), new LowPolyMeshes.Ring(-.2f,.36f,.42f),
            new LowPolyMeshes.Ring(-.4f,.39f,.41f), new LowPolyMeshes.Ring(-.48f,.34f,.35f), new LowPolyMeshes.Ring(-.5f,0,0) };

        private static Vector3 CoatPoint(Transform body, string id, float y, float angle, float lift = .004f)
        {
            int row = 0;
            while (row < CoatRings.Length - 2 && y < CoatRings[row + 1].Y) row++;
            var a = CoatRings[row]; var b = CoatRings[row + 1];
            float t = Mathf.InverseLerp(a.Y, b.Y, y), radians = angle * Mathf.Deg2Rad;
            float c = Mathf.Cos(radians), s = Mathf.Sin(radians);
            float exponent = id == "chain_hauberk" ? 2.7f : 2.3f;
            float fold = id == "silk_robe" ? 1f + .035f * Mathf.Cos(radians * 10f) : 1f;
            Vector3 local = new Vector3(Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 2f / exponent) * Mathf.Lerp(a.X,b.X,t) * fold,
                y, Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 2f / exponent) * Mathf.Lerp(a.Z,b.Z,t) * fold + Mathf.Lerp(a.Forward,b.Forward,t));
            return body.localPosition + Vector3.Scale(local, body.localScale) + new Vector3(c,0,s) * lift;
        }

        private static void TailorBody(GameObject root, string id)
        {
            bool plate = id == "chain_hauberk", robe = id == "silk_robe";
            Transform chest = root.transform.Find("Socket_Chest"), body = chest.Find("Body");
            // Replace the old floating decorative leaves and studs with garment construction.
            for (int i = chest.childCount - 1; i >= 0; i--)
            {
                Transform child = chest.GetChild(i);
                if (child.name == "Toggle" || child.name == "Stitch" || child.name == "Stud" ||
                    child.name == "Gem" || child.name == "Ridge" || child.name == "Engraving") Object.DestroyImmediate(child.gameObject);
            }
            // Authored shapes have many rings, with soft folds for cloth and a squarer plate section.
            Mesh torso = LowPolyMeshes.Loft("TailoredTorso", 40, plate ? 2.7f : 2.3f, false, CoatRings);
            if (robe)
            {
                var vertices = torso.vertices;
                for (int i = 0; i < vertices.Length; i++)
                {
                    float fold = 1 + .035f * Mathf.Cos(Mathf.Atan2(vertices[i].z, vertices[i].x) * 10);
                    vertices[i].x *= fold; vertices[i].z *= fold;
                }
                torso.vertices = vertices; torso.RecalculateNormals();
            }
            torso.name = "Wear_" + id + "_Torso";
            body.GetComponent<MeshFilter>().sharedMesh = SaveWornMesh(torso);
            var detail = new WearDetail(id, chest);
            Vector3 P(float y, float angle, float lift = .018f) => CoatPoint(body, id, y, angle, lift);
            foreach (float facing in new[] { 90f, 270f })
            {
                if (plate)
                {
                    // Overlapping abdominal lames, a raised breastplate keel, and a separate backplate.
                    for (int i = 0; i < 3; i++)
                    {
                        float y = -.37f + i * .13f;
                        detail.Patch("Steel", (u,v) => P(y + v * .15f, facing - 67f + u * 134f, .018f));
                        detail.Line("Edge", t => P(y + .15f, facing - 67f + t * 134f, .025f), .009f);
                    }
                    detail.Line("Edge", t => P(.02f + t * .36f, facing, .017f), .014f);
                    foreach (float s in new[] { -1f, 1f })
                        detail.Line("Edge", t => P(.15f + t * .22f, facing + s * (50f - t * 35f), .014f), .01f);
                }
                else
                {
                    // Broad sewn panels and darts, visible at gameplay distance as changes in construction.
                    foreach (float s in new[] { -1f, 1f })
                    {
                        detail.Patch(robe ? "Teal" : "LeatherDark", (u,v) => P(-.34f + v * .69f,
                            facing + s * (24f + u * (robe ? 10f : 6f) + v * 8f), .019f));
                        detail.Line("Thread", t => P(-.34f + t * .67f, facing + s * (25f + t * 8f), .025f), .004f);
                    }
                    if (!robe)
                    {
                        // Diamond quilting is cut across fitted leather panels, rather than a few studs.
                        for (int row = 0; row < 4; row++)
                        foreach (float s in new[] { -1f, 1f })
                        {
                            float y = -.24f + row * .13f;
                            detail.Line("Thread", t => P(y + t * .12f, facing + s * (-48f + t * 96f)), .0045f);
                        }
                    }
                }
            }
            // Wide side straps cross the body; the reverse is as constructed as the front.
            if (!robe)
                foreach (float side in new[] { 0f, 180f })
                foreach (float y in new[] { -.20f, .17f })
                    detail.Patch("LeatherDark", (u,v) => P(y + v * .055f, side - 30f + u * 60f, .015f));
            else
                detail.Patch("Teal", (u,v) => P(-.3f + v * .55f, 55f + u * 10f + v * 42f, .018f));
            detail.Save();
            TailorSkirt(id, chest.Find(plate ? "Fauld" : robe ? "Skirt" : "Hem"));
            foreach (string side in new[] { "L", "R" }) TailorShoulder(id, root.transform.Find("Socket_Shoulder" + side), plate, robe);
        }

        private static void TailorSkirt(string id, Transform skirt)
        {
            bool plate = id == "chain_hauberk", robe = id == "silk_robe";
            Vector3 Point(float y, float angle, float lift = 0f)
            {
                float t = .5f - y, a = angle * Mathf.Deg2Rad;
                float radius = Mathf.Lerp(.39f,.52f,Mathf.Min(1,t*4));
                float pleat = robe ? .025f * Mathf.Cos(a * 14f) * Mathf.SmoothStep(0,1,t) : 0f;
                return new Vector3(Mathf.Cos(a) * (radius+pleat+lift), y, Mathf.Sin(a) * (radius+pleat+lift));
            }
            // Replace the smooth tube with a pleated skirt or overlapping open-front fauld.
            var shell = new WearDetail(id, skirt);
            shell.Patch(plate ? "Mail" : robe ? "Linen" : "Leather", (u,v) => Point(.5f-v, 98f+u*344f), 56, 16);
            skirt.GetComponent<MeshRenderer>().enabled = false;
            int panels = plate ? 10 : robe ? 14 : 8;
            for (int i = 0; i < panels; i++)
            {
                float angle = 102f + i * (336f / panels);
                if (plate)
                {
                    shell.Patch("Steel", (u,v) => Point(.22f-v*.68f, angle + u * 27f, .035f+v*.012f), 5, 4);
                    shell.Line("Edge", t => Point(-.46f,angle+t*27f,.05f), .012f);
                }
                else
                    shell.Line(robe ? "Teal" : "LeatherDark", t => Point(.35f-t*.82f,angle,.01f), robe ? .012f : .009f);
            }
            shell.Line(robe ? "Teal" : "LeatherDark", t => Point(-.48f,98f+t*344f,.015f), .018f, 48);
            // A split front and unequal overlapping edges break the bucket silhouette.
            foreach (float a in new[] { 98f,442f })
                shell.Line(robe ? "Teal" : "LeatherDark", t => Point(.5f-t,a,.012f), .018f);
            shell.Save();
        }

        private static void TailorShoulder(string id, Transform socket, bool plate, bool robe)
        {
            var d = new WearDetail(id, socket);
            if (plate)
            {
                foreach (string name in new[] { "ShoulderPlate", "ShoulderTrim", "ShoulderLame" })
                    if (socket.Find(name) != null) Object.DestroyImmediate(socket.Find(name).gameObject);
                float side = socket.name.EndsWith("L") ? -1f : 1f;
                for (int i = 0; i < 3; i++)
                {
                    int layer = i;
                    d.Patch("Steel", (u,v) => {
                        float a = u * Mathf.PI * 2, r = .025f + v * .975f;
                        return new Vector3(Mathf.Cos(a)*r*(.14f-layer*.015f)+side*layer*.025f,
                            .025f-layer*.055f+Mathf.Sqrt(1-r*r)*.095f, Mathf.Sin(a)*r*(.15f-layer*.01f));
                    }, 24, 6);
                    d.Line("Edge", t => new Vector3(Mathf.Cos(t*Mathf.PI*2)*(.14f-layer*.015f)+side*layer*.025f,
                        .025f-layer*.055f, Mathf.Sin(t*Mathf.PI*2)*(.15f-layer*.01f)), .009f, 24);
                }
            }
            else if (!robe)
            {
                d.Line("Thread", t => new Vector3(0,-.04f+Mathf.Sin(t*Mathf.PI)*.13f,Mathf.Cos(t*Mathf.PI)*.108f), .005f);
            }
            d.Save();
        }

        private static void TailorBoots(GameObject root, string id)
        {
            bool plate = id == "iron_greaves", cloth = id == "silk_slippers";
            foreach (string side in new[] { "L", "R" })
            {
                Transform foot = root.transform.Find("Socket_Foot" + side);
                var d = new WearDetail(id,foot);
                if (plate)
                    for (int i = 0; i < 3; i++)
                    {
                        float y = -.09f+i*.075f;
                        d.Patch("Steel", (u,v) => new Vector3((u-.5f)*.145f,y+v*.085f,
                            .084f + Mathf.Sin(u*Mathf.PI)*.025f),6,3);
                        d.Line("Edge", t => new Vector3((t-.5f)*.145f,y+.085f,.087f+Mathf.Sin(t*Mathf.PI)*.025f), .005f);
                    }
                else
                {
                    d.Patch(cloth ? "Teal" : "LeatherDark", (u,v) => new Vector3((u-.5f)*.085f,-.10f+v*.27f,
                        .102f+Mathf.Sin(u*Mathf.PI)*.009f),4,6);
                    for(int i=0;i<5;i++)
                    {
                        float y=-.08f+i*.047f;
                        foreach(float s in new[]{-1f,1f})
                            d.Line("Thread",t=>new Vector3(Mathf.Lerp(-.036f,.036f,t),y+s*(t-.5f)*.026f,.115f),.004f,1);
                    }
                }
                // Separate welt and sole, with a stitched toe instead of a rounded tube ending.
                foreach(float y in new[]{-.218f,-.235f})
                    d.Line("LeatherDark",t=>{
                        float a=t*Mathf.PI*2;
                        return new Vector3(Mathf.Cos(a)*.084f,y,.05f+Mathf.Sin(a)*.17f);
                    },.008f,24);
                d.Line(cloth ? "Teal" : "Thread", t=> new Vector3((t-.5f)*.13f,-.185f,.17f+Mathf.Sin(t*Mathf.PI)*.033f),.004f);
                d.Save();
            }
        }

        internal static void TailorGloves(GameObject root, string id)
        {
            WearMaterials();
            WearPalette(root,id);
            foreach(string side in new[]{"L","R"})
            {
                Transform hand=root.transform.Find("Socket_Hand"+side);
                var d=new WearDetail(id,hand);
                float s=side=="L"?-1f:1f;
                bool plate=id=="iron_gauntlets";
                foreach(float y in new[]{.035f,.12f})
                    d.Line(plate ? "Edge" : "LeatherDark",t=>{
                        float a=t*Mathf.PI*2;
                        return new Vector3(Mathf.Cos(a)*.060f,y,Mathf.Sin(a)*.063f);
                    },.006f,18);
                for(int i=0;i<3;i++)
                {
                    float y=.045f+i*.035f;
                    d.Patch(plate ? "Steel" : id=="silk_gloves"?"Teal":"LeatherDark",
                        (u,v)=>new Vector3(s*(.058f+Mathf.Sin(u*Mathf.PI)*.008f),y+v*.028f,(u-.5f)*.095f),4,2);
                }
                d.Save();
            }
        }

        private static void TailorHead(GameObject root,string id)
        {
            Transform head=root.transform.Find("Socket_Head");
            var d=new WearDetail(id,head);
            if(id=="hunter_hood")
            {
                d.Line("LeatherDark",t=>new Vector3(0,.27f-t*.42f,-.04f-Mathf.Sin(t*Mathf.PI*.65f)*.245f),.009f);
                foreach(float s in new[]{-1f,1f})
                    d.Line("Thread",t=>new Vector3(s*(.12f+t*.065f),.24f-t*.4f,-.12f-t*.09f),.004f);
            }
            else if (id != "sage_circlet")
            {
                foreach(float s in new[]{-1f,0f,1f})
                    d.Line("Edge",t=>{
                        float a=t*Mathf.PI;
                        return new Vector3(s*.12f,.055f+Mathf.Sin(a)*(.19f-Mathf.Abs(s)*.025f),-.02f+Mathf.Cos(a)*.233f);
                    },.009f,18);
                d.Line("Brass",t=>{
                    float a=t*Mathf.PI*2;
                    return new Vector3(Mathf.Cos(a)*.24f,.048f,-.02f+Mathf.Sin(a)*.25f);
                },.009f,28);
            }
            d.Save();
        }
    }
}
