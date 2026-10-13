using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.Visuals
{
    /// <summary>
    /// Small props built from Unity primitives at runtime (caster staffs, archer bows, bolts and
    /// arrows), coloured through a property block so they all share one toon material.
    /// (A primitive's own default material is URP Lit, whose variants the Web build strips:
    /// it rendered pink there.)
    /// </summary>
    public static class RuntimePrimitives
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private static Material material;
        private static readonly Dictionary<PrimitiveType, Mesh> primitiveMeshes = new Dictionary<PrimitiveType, Mesh>();
        private static MaterialPropertyBlock colorBlock;

        private static Material Material
        {
            get
            {
                if (material == null)
                    material = Resources.Load<Material>("RuntimePrimitive");
                return material;
            }
        }

        public static GameObject Create(PrimitiveType type, Transform parent, Color color)
        {
            return CreateMesh(MeshFor(type), parent, color, type.ToString());
        }

        public static Mesh MeshFor(PrimitiveType type)
        {
            if (!primitiveMeshes.TryGetValue(type, out var mesh) || mesh == null)
            {
                GameObject template = GameObject.CreatePrimitive(type);
                mesh = template.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(template);
                primitiveMeshes[type] = mesh;
            }
            return mesh;
        }

        /// <summary>Render-only geometry: never create a temporary collider for each body part.</summary>
        public static GameObject CreateMesh(Mesh mesh, Transform parent, Color color, string name = "Part")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>();

            Renderer r = go.GetComponent<Renderer>();
            if (Material != null)
                r.sharedMaterial = Material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (colorBlock == null) colorBlock = new MaterialPropertyBlock();
            colorBlock.SetColor(BaseColorId, color);
            colorBlock.SetColor(ColorId, color);
            r.SetPropertyBlock(colorBlock);
            return go;
        }

        /// <summary>An arrow pointing along the root's +Z; optional tint colors skill arrows.</summary>
        public static void BuildArrow(Transform root, Color? tint = null)
        {
            GameObject shaft = Create(PrimitiveType.Cylinder, root, tint.HasValue ? Color.Lerp(tint.Value, Color.black, 0.35f) : new Color(0.55f, 0.40f, 0.24f));
            shaft.transform.localScale = new Vector3(0.04f, 0.4f, 0.04f);
            shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // a cylinder's long axis is Y

            GameObject head = Create(PrimitiveType.Sphere, root, tint ?? new Color(0.75f, 0.75f, 0.78f));
            head.transform.localScale = new Vector3(0.07f, 0.07f, 0.16f);
            head.transform.localPosition = new Vector3(0f, 0f, 0.42f);

            GameObject fletching = Create(PrimitiveType.Cube, root, tint.HasValue ? Color.Lerp(tint.Value, Color.white, 0.35f) : new Color(0.92f, 0.90f, 0.84f));
            fletching.transform.localScale = new Vector3(0.12f, 0.012f, 0.12f);
            fletching.transform.localPosition = new Vector3(0f, 0f, -0.34f);
        }
    }
}
