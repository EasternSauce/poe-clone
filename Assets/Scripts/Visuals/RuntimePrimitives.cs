using UnityEngine;

namespace PoeClone.Visuals
{
    /// <summary>
    /// Small props built from Unity primitives at runtime (caster staffs, archer bows, bolts and
    /// arrows), coloured through a property block so they share the default material.
    /// </summary>
    public static class RuntimePrimitives
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public static GameObject Create(PrimitiveType type, Transform parent, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            // Immediately: a collider left for even one frame could shove a character or eat a hit test.
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);

            Renderer r = go.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            r.SetPropertyBlock(block);
            return go;
        }

        /// <summary>An arrow pointing along the root's +Z: wooden shaft, grey head, pale fletching.</summary>
        public static void BuildArrow(Transform root)
        {
            GameObject shaft = Create(PrimitiveType.Cylinder, root, new Color(0.55f, 0.40f, 0.24f));
            shaft.transform.localScale = new Vector3(0.04f, 0.4f, 0.04f);
            shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // a cylinder's long axis is Y

            GameObject head = Create(PrimitiveType.Sphere, root, new Color(0.75f, 0.75f, 0.78f));
            head.transform.localScale = new Vector3(0.07f, 0.07f, 0.16f);
            head.transform.localPosition = new Vector3(0f, 0f, 0.42f);

            GameObject fletching = Create(PrimitiveType.Cube, root, new Color(0.92f, 0.90f, 0.84f));
            fletching.transform.localScale = new Vector3(0.12f, 0.012f, 0.12f);
            fletching.transform.localPosition = new Vector3(0f, 0f, -0.34f);
        }
    }
}
