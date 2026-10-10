using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.World
{
    public partial class WorldBuilder
    {
        private enum RoofStyle { Thatch, RedShingle, Slate }
        private enum WallFinish { Timbered, Ashlar, Fieldstone, Logs }

        // Flat-shaded blocks gathered per material, so a whole roof or prop is one renderer.
        private sealed class PieceMesh
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<List<int>> triangles = new List<List<int>>();
            private readonly List<Material> materials = new List<Material>();

            public int Slot(Material material)
            {
                int index = materials.IndexOf(material);
                if (index >= 0) return index;
                materials.Add(material);
                triangles.Add(new List<int>());
                return materials.Count - 1;
            }

            public void Tri(int slot, Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) { Vector3 swap = b; b = c; c = swap; }
                int v = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                triangles[slot].Add(v); triangles[slot].Add(v + 1); triangles[slot].Add(v + 2);
            }

            public void Quad(int slot, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) { Vector3 swap = b; b = d; d = swap; }
                int v = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                List<int> t = triangles[slot];
                t.Add(v); t.Add(v + 1); t.Add(v + 2);
                t.Add(v); t.Add(v + 2); t.Add(v + 3);
            }

            // An oriented box: centre plus three half-extent axes.
            public void Block(int slot, Vector3 c, Vector3 x, Vector3 y, Vector3 z)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    Vector3 n = axis == 0 ? x : axis == 1 ? y : z;
                    Vector3 u = axis == 0 ? y : axis == 1 ? z : x;
                    Vector3 w = axis == 0 ? z : axis == 1 ? x : y;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector3 f = c + n * s;
                        Quad(slot, f - u - w, f + u - w, f + u + w, f - u + w, n * s);
                    }
                }
            }

            // A beam between two points, its cross-section spanned by side and depth (half sizes).
            public void Beam(int slot, Vector3 a, Vector3 b, float halfWidth, Vector3 depth)
            {
                Vector3 along = (b - a) * 0.5f;
                Vector3 side = Vector3.Cross(along.normalized, depth.normalized) * halfWidth;
                Block(slot, (a + b) * 0.5f, along, side, depth);
            }

            // A capped, faceted cone section from a (radius ra) to b (radius rb).
            public void Frustum(int slot, Vector3 a, Vector3 b, float ra, float rb, int sides, Vector3 across)
            {
                Vector3 axis = (b - a).normalized;
                Vector3 p = Vector3.Cross(axis, across).normalized;
                Vector3 q = Vector3.Cross(axis, p);
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                    Vector3 o0 = p * Mathf.Cos(a0) + q * Mathf.Sin(a0);
                    Vector3 o1 = p * Mathf.Cos(a1) + q * Mathf.Sin(a1);
                    Quad(slot, a + o0 * ra, a + o1 * ra, b + o1 * rb, b + o0 * rb, o0 + o1);
                    Tri(slot, a, a + o0 * ra, a + o1 * ra, -axis);
                    Tri(slot, b, b + o0 * rb, b + o1 * rb, axis);
                }
            }

            public GameObject Build(Transform parent, string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.subMeshCount = triangles.Count;
                for (int i = 0; i < triangles.Count; i++)
                    mesh.SetTriangles(triangles[i], i);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = materials.ToArray();
                return go;
            }
        }

        private static Transform LocalFrame(Transform parent, string name, Vector3 local, float yaw)
        {
            var frame = new GameObject(name).transform;
            frame.SetParent(parent, false);
            frame.localPosition = local;
            frame.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return frame;
        }

        // ------------------------------------------------------------------ the kit cottage

        // The kit cottage has 4 x 3.4 walls, 2.4 high, with its door on local -Z.
        private const float CottageWallTop = 2.4f, CottageHalfWidth = 2f, CottageHalfDepth = 1.7f;

        // Gives a kit cottage its own walls and roof: a wall finish with a matching gable, a roof
        // pitched to suit its covering, and a capped chimney rising through it.
        private void DressHouse(Transform house, RoofStyle roof, WallFinish walls, int seed)
        {
            const float top = CottageWallTop, hw = CottageHalfWidth, hd = CottageHalfDepth;
            // Thatch sheds rain best steep; tiles sit lower.
            float rise = roof == RoofStyle.Thatch ? 2.3f : roof == RoofStyle.Slate ? 2f : 1.6f;
            rise += (seed % 3 - 1) * 0.12f;

            Transform body = house.Find("Walls");
            Material wallMat = kit.Mat(walls == WallFinish.Timbered ? "Plaster" : walls == WallFinish.Ashlar ? "Wall" :
                walls == WallFinish.Fieldstone ? "Stone" : "Bark");
            if (body != null) body.GetComponent<Renderer>().sharedMaterial = wallMat;

            var mesh = new PieceMesh();
            GableRoof(mesh, roof, new Vector3(0f, top, 0f), hw, rise, 0.55f, hd * 2f + 0.6f, seed);

            int gable = mesh.Slot(walls == WallFinish.Logs ? kit.Mat("Wood") : wallMat);
            int timber = mesh.Slot(kit.Mat("Wood"));
            Vector3 apex = new Vector3(0f, top + rise, 0f);
            for (int end = -1; end <= 1; end += 2)
            {
                Vector3 outward = Vector3.forward * end;
                Vector3 face = outward * hd;
                mesh.Tri(gable, face + new Vector3(-hw, top, 0f), face + new Vector3(hw, top, 0f), face + apex, outward);
                if (walls != WallFinish.Timbered) continue;
                // Tie beam, king post and two braces stand just proud of the plaster.
                Vector3 proud = outward * (hd + 0.035f);
                Vector3 depth = outward * 0.035f;
                mesh.Block(timber, proud + Vector3.up * top, Vector3.right * 1.98f, Vector3.up * 0.07f, depth);
                mesh.Beam(timber, proud + Vector3.up * (top + 0.07f), proud + Vector3.up * (top + rise - 0.12f), 0.07f, depth);
                for (int side = -1; side <= 1; side += 2)
                    mesh.Beam(timber, proud + new Vector3(side * 1.25f, top + 0.07f, 0f),
                        proud + new Vector3(side * 0.07f, top + rise * 0.6f, 0f), 0.06f, depth);
            }

            if (walls == WallFinish.Timbered) TimberFrame(mesh, timber);
            else if (walls == WallFinish.Logs) LogWalls(mesh, rise);
            else Quoins(mesh, kit.Mat(walls == WallFinish.Ashlar ? "Stone" : "Wall"));
            if (walls != WallFinish.Logs)
                mesh.Block(mesh.Slot(kit.Mat("RockDark")), new Vector3(0f, 0.13f, 0f),
                    Vector3.right * (hw + 0.04f), Vector3.up * 0.13f, Vector3.forward * (hd + 0.04f));

            Transform chimney = house.Find("Chimney");
            if (chimney != null)
            {
                Vector3 crown = chimney.localPosition + Vector3.up * chimney.localScale.y * 0.5f;
                mesh.Block(mesh.Slot(kit.Mat("Stone")), crown + Vector3.up * 0.06f,
                    Vector3.right * (chimney.localScale.x * 0.5f + 0.08f), Vector3.up * 0.06f,
                    Vector3.forward * (chimney.localScale.z * 0.5f + 0.08f));
            }
            mesh.Build(house, "Roof");
        }

        // Corner posts, sills, rails and braces over plaster, kept clear of the door and windows.
        private static void TimberFrame(PieceMesh mesh, int timber)
        {
            const float hw = CottageHalfWidth, hd = CottageHalfDepth, top = CottageWallTop;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    mesh.Block(timber, new Vector3(sx * hw, top * 0.5f, sz * hd), Vector3.right * 0.09f,
                        Vector3.up * top * 0.5f, Vector3.forward * 0.09f);
            // Long sides: sill, mid rail, a centre post and a brace in each lower panel.
            for (int sx = -1; sx <= 1; sx += 2)
            {
                Vector3 n = Vector3.right * sx;
                Vector3 face = n * (hw + 0.035f);
                Vector3 depth = n * 0.035f;
                foreach (float y in new[] { 0.32f, 1.3f })
                    mesh.Block(timber, face + Vector3.up * y, Vector3.forward * hd, Vector3.up * 0.065f, depth);
                mesh.Beam(timber, face + Vector3.up * 0.3f, face + Vector3.up * top, 0.065f, depth);
                for (int sz = -1; sz <= 1; sz += 2)
                    mesh.Beam(timber, face + new Vector3(0f, 0.38f, sz * (hd - 0.1f)), face + new Vector3(0f, 1.24f, sz * 0.08f), 0.055f, depth);
            }
            // Back: the same rails, with crossed braces in the upper panels.
            Vector3 back = Vector3.forward * (hd + 0.035f), backDepth = Vector3.forward * 0.035f;
            foreach (float y in new[] { 0.32f, 1.3f })
                mesh.Block(timber, back + Vector3.up * y, Vector3.right * hw, Vector3.up * 0.065f, backDepth);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                mesh.Beam(timber, back + new Vector3(sx * 0.7f, 0.3f, 0f), back + new Vector3(sx * 0.7f, top, 0f), 0.06f, backDepth);
                mesh.Beam(timber, back + new Vector3(sx * 0.1f, 1.37f, 0f), back + new Vector3(sx * (hw - 0.1f), top - 0.08f, 0f), 0.05f, backDepth);
            }
            // Front: posts flank the door and a rail runs over the windows.
            Vector3 front = Vector3.back * (hd + 0.035f), frontDepth = Vector3.back * 0.035f;
            mesh.Block(timber, front + Vector3.up * 1.98f, Vector3.right * hw, Vector3.up * 0.065f, frontDepth);
            for (int sx = -1; sx <= 1; sx += 2)
                mesh.Beam(timber, front + new Vector3(sx * 0.6f, 0.26f, 0f), front + new Vector3(sx * 0.6f, 1.92f, 0f), 0.055f, frontDepth);
        }

        // Dressed corner stones laid long and short in turn.
        private static void Quoins(PieceMesh mesh, Material stone)
        {
            const float hw = CottageHalfWidth, hd = CottageHalfDepth;
            int slot = mesh.Slot(stone);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    for (int k = 0; k < 6; k++)
                    {
                        float x = k % 2 == 0 ? 0.36f : 0.16f, z = k % 2 == 0 ? 0.16f : 0.36f;
                        mesh.Block(slot, new Vector3(sx * (hw - x + 0.04f), 0.38f + k * 0.37f, sz * (hd - z + 0.04f)),
                            Vector3.right * x, Vector3.up * 0.16f, Vector3.forward * z);
                    }
        }

        // Round logs notched across each other, ends left long at the corners. On the front
        // they stop short of the door and windows; up the gables they shorten under the rafters.
        private void LogWalls(PieceMesh mesh, float rise)
        {
            const float hw = CottageHalfWidth, hd = CottageHalfDepth, top = CottageWallTop;
            const float radius = 0.13f, spacing = 0.26f, ends = hw + 0.22f;
            int log = mesh.Slot(kit.Mat("Wood"));
            for (int sx = -1; sx <= 1; sx += 2)
                for (float y = radius; y < top; y += spacing)
                    mesh.Frustum(log, new Vector3(sx * (hw - 0.02f), y, -hd - 0.22f), new Vector3(sx * (hw - 0.02f), y, hd + 0.22f),
                        radius, radius, 6, Vector3.up);
            for (int sz = -1; sz <= 1; sz += 2)
                for (float y = radius + spacing * 0.5f; y < top; y += spacing)
                {
                    float z = sz * (hd - 0.02f);
                    // Openings left to right: window, door, window.
                    var gaps = new List<Vector2>();
                    if (sz < 0 && y > 1.08f && y < 1.92f) gaps.Add(new Vector2(-1.58f, -0.82f));
                    if (sz < 0 && y < 1.62f) gaps.Add(new Vector2(-0.53f, 0.53f));
                    if (sz < 0 && y > 1.08f && y < 1.92f) gaps.Add(new Vector2(0.82f, 1.58f));
                    float from = -ends;
                    foreach (Vector2 gap in gaps)
                    {
                        mesh.Frustum(log, new Vector3(from, y, z), new Vector3(gap.x, y, z), radius, radius, 6, Vector3.up);
                        from = gap.y;
                    }
                    mesh.Frustum(log, new Vector3(from, y, z), new Vector3(ends, y, z), radius, radius, 6, Vector3.up);
                }
            for (int sz = -1; sz <= 1; sz += 2)
                for (float y = radius + spacing * 0.5f; y < top + rise; y += spacing)
                {
                    float half = hw * (1f - (y + radius - top) / rise);
                    if (y < top) continue;
                    if (half < 0.15f) break;
                    float z = sz * (hd - 0.02f);
                    mesh.Frustum(log, new Vector3(-half, y, z), new Vector3(half, y, z), radius, radius, 6, Vector3.up);
                }
        }

        // ------------------------------------------------------------------ roofs

        // Two pitches meeting at a ridge along local Z above centre; the rafters rest on walls
        // halfSpan either side and run on past them by eave.
        private void GableRoof(PieceMesh mesh, RoofStyle style, Vector3 centre, float halfSpan, float rise,
            float eave, float length, int seed)
        {
            var random = new System.Random(seed);
            Vector3 ridge = centre + Vector3.up * rise;
            float drop = eave * rise / halfSpan;
            float thickness = 0f;
            for (int side = -1; side <= 1; side += 2)
                thickness = RoofPitch(mesh, style, ridge, centre + new Vector3(side * (halfSpan + eave), -drop, 0f),
                    length, random, side > 0);

            // The two outer surfaces meet this far above the ridge; the cap covers that seam.
            float cos = halfSpan / Mathf.Sqrt(halfSpan * halfSpan + rise * rise);
            Vector3 crest = ridge + Vector3.up * thickness / cos;
            if (style == RoofStyle.Thatch)
            {
                // Thick verges wrap the stepped course ends at the gables.
                int straw = mesh.Slot(kit.Mat("Thatch"));
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 low = centre + new Vector3(side * (halfSpan + eave), -drop, 0f);
                    Vector3 down = (low - ridge).normalized;
                    Vector3 normal = Outward(down);
                    float slope = Vector3.Distance(ridge, low);
                    for (int end = -1; end <= 1; end += 2)
                        mesh.Block(straw, (ridge + low) * 0.5f + down * 0.05f + normal * thickness * 0.5f + Vector3.forward * end * (length * 0.5f - 0.1f),
                            down * (slope * 0.5f + 0.08f), normal * (thickness * 0.5f + 0.05f), Vector3.forward * 0.16f);
                }
                // A rolled ridge, pinned with hazel pegs.
                Vector3 half = Vector3.forward * (length * 0.5f - 0.02f);
                Vector3 roll = crest - Vector3.up * 0.1f;
                mesh.Frustum(mesh.Slot(kit.Mat("ThatchDark")), roll - half, roll + half, 0.24f, 0.24f, 6, Vector3.up);
                int peg = mesh.Slot(kit.Mat("Wood"));
                for (float z = -length * 0.5f + 0.4f; z < length * 0.5f - 0.2f; z += 0.55f)
                    mesh.Block(peg, crest + new Vector3(0f, 0.12f, z), Vector3.right * 0.28f, Vector3.up * 0.03f, Vector3.forward * 0.03f);
                return;
            }

            // Tiles get bargeboards on the gable edges and a cap of two boards in a V.
            int cap = mesh.Slot(kit.Mat(style == RoofStyle.Slate ? "SlateDark" : "RoofDark"));
            int board = mesh.Slot(kit.Mat("Wood"));
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 low = centre + new Vector3(side * (halfSpan + eave), -drop, 0f);
                Vector3 down = (low - ridge).normalized;
                Vector3 normal = Outward(down);
                mesh.Block(cap, crest + down * 0.13f + normal * 0.03f, down * 0.16f, normal * 0.035f,
                    Vector3.forward * (length * 0.5f + 0.04f));
                float slope = Vector3.Distance(ridge, low);
                for (int end = -1; end <= 1; end += 2)
                    mesh.Block(board, (ridge + low) * 0.5f + normal * (thickness * 0.5f - 0.04f) + Vector3.forward * end * (length * 0.5f + 0.03f),
                        down * (slope * 0.5f + 0.04f), normal * (thickness * 0.5f + 0.08f), Vector3.forward * 0.035f);
            }
        }

        // A single pitch with a board along its raised edge, for lean-tos.
        private void ShedRoof(PieceMesh mesh, RoofStyle style, Vector3 high, Vector3 low, float length, int seed)
        {
            float thickness = RoofPitch(mesh, style, high, low, length, new System.Random(seed), true);
            Vector3 down = (low - high).normalized;
            Vector3 normal = Outward(down);
            mesh.Block(mesh.Slot(kit.Mat("Wood")), high + normal * thickness * 0.5f - down * 0.04f,
                down * 0.05f, normal * (thickness * 0.5f + 0.1f), Vector3.forward * (length * 0.5f + 0.05f));
        }

        // The upward-facing normal of a pitch running down the given direction across local Z.
        private static Vector3 Outward(Vector3 down)
        {
            Vector3 normal = Vector3.Cross(Vector3.forward, down);
            return normal.y < 0f ? -normal : normal;
        }

        // Overlapping courses from the top edge down to the eave, each lapping over the one below,
        // broken into staggered thatch bundles or tiles with ragged lower edges.
        // Returns the depth of the top course above the rafters.
        private float RoofPitch(PieceMesh mesh, RoofStyle style, Vector3 top, Vector3 low, float length,
            System.Random random, bool mirrorStagger)
        {
            float Rand(float min, float max) => min + (float)random.NextDouble() * (max - min);
            Vector3 down = low - top;
            float slope = down.magnitude;
            down /= slope;
            Vector3 normal = Outward(down);

            bool thatch = style == RoofStyle.Thatch, slate = style == RoofStyle.Slate;
            int rows = Mathf.Max(2, Mathf.RoundToInt(slope / (thatch ? 0.62f : slate ? 0.32f : 0.38f)));
            float rowLength = slope / rows;
            float thick = thatch ? 0.18f : 0.055f;
            float step = thatch ? 0.08f : 0.04f;
            float minWidth = thatch ? 0.45f : slate ? 0.3f : 0.36f;
            float maxWidth = thatch ? 0.8f : slate ? 0.42f : 0.5f;
            // Bundles of thatch overlap; tiles keep a hairline between them.
            float gap = thatch ? -0.04f : 0.035f;
            float ragged = thatch ? 0.09f : 0.045f;
            int main = mesh.Slot(kit.Mat(thatch ? "Thatch" : slate ? "Slate" : "Roof"));
            int odd = mesh.Slot(kit.Mat(thatch ? "ThatchDark" : slate ? "SlateDark" : "RoofDark"));
            double oddShare = thatch ? 0.15 : 0.3;

            for (int row = 0; row < rows; row++)
            {
                bool eaveRow = row == rows - 1;
                float rowThick = thatch && eaveRow ? thick * 1.5f : thick;
                float lift = (rows - 1 - row) * step + rowThick * 0.5f;
                float upper = Mathf.Max(-0.04f, row * rowLength - rowLength * 0.5f);
                float z = -length * 0.5f - ((row % 2 == 0) == mirrorStagger ? 0f : maxWidth * 0.5f);
                while (z < length * 0.5f)
                {
                    float width = Rand(minWidth, maxWidth);
                    float z0 = Mathf.Max(z, -length * 0.5f) + gap * 0.5f;
                    float z1 = Mathf.Min(z + width, length * 0.5f) - gap * 0.5f;
                    z += width;
                    if (z1 - z0 < 0.08f) continue;
                    float lower = (row + 1) * rowLength + (eaveRow && thatch ? 0.1f : 0f) + Rand(-ragged, ragged * 0.5f);
                    float pieceThick = rowThick * (thatch ? Rand(0.85f, 1.15f) : 1f);
                    Vector3 c = top + down * ((upper + lower) * 0.5f) + normal * lift + Vector3.forward * ((z0 + z1) * 0.5f);
                    mesh.Block(random.NextDouble() < oddShare ? odd : main, c, down * ((lower - upper) * 0.5f),
                        normal * pieceThick * 0.5f, Vector3.forward * ((z1 - z0) * 0.5f));
                }
            }
            return (rows - 1) * step + thick;
        }
    }
}
