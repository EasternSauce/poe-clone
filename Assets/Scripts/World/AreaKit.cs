using System;
using UnityEngine;

namespace PoeClone.World
{
    /// <summary>
    /// Everything the runtime <see cref="WorldBuilder"/> needs to dress the areas: the stylized
    /// prop prefabs, meshes and materials. Lives in Resources (so it can be loaded at runtime and
    /// everything it references is included in builds); made by PoeClone > Build Area Kit.
    /// </summary>
    [CreateAssetMenu(menuName = "PoeClone/Area Kit")]
    public class AreaKit : ScriptableObject
    {
        [Header("Prefabs")]
        public GameObject pine;
        public GameObject oak;
        public GameObject bush;
        public GameObject bushSmall;
        public GameObject rock;
        public GameObject rockSmall;
        public GameObject house;
        public GameObject pillar;

        [Header("Meshes")]
        public Mesh cone;
        public Mesh cylinder;
        public Mesh icoHead;

        [Header("Materials")]
        public NamedMaterial[] materials = new NamedMaterial[0];

        [Serializable]
        public struct NamedMaterial
        {
            public string name;
            public Material material;
        }

        private static AreaKit loaded;

        public static AreaKit Load()
        {
            if (loaded == null)
                loaded = Resources.Load<AreaKit>("AreaKit");
            return loaded;
        }

        public Material Mat(string name)
        {
            foreach (NamedMaterial m in materials)
            {
                if (m.name == name)
                    return m.material;
            }
            Debug.LogWarning("AreaKit: no material named " + name);
            return materials.Length > 0 ? materials[0].material : null;
        }
    }
}
