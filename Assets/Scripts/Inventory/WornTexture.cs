using UnityEngine;

namespace PoeClone.Inventory
{
    /// <summary>
    /// ToonLit projects its texture in world space, which suits terrain and buildings but makes
    /// clothing and gear swim as the character moves. Marks a model's renderers (per renderer,
    /// so materials shared with the world are left alone) to project in their own space instead.
    /// </summary>
    public static class WornTexture
    {
        private static readonly int ObjectTexture = Shader.PropertyToID("_ObjectTexture");
        private static MaterialPropertyBlock block;

        public static void Anchor(GameObject model)
        {
            if (block == null) block = new MaterialPropertyBlock();
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                Material m = r.sharedMaterial;
                if (m == null || !m.HasProperty(ObjectTexture))
                    continue;
                r.GetPropertyBlock(block);
                block.SetFloat(ObjectTexture, 1f);
                r.SetPropertyBlock(block);
            }
        }
    }
}
