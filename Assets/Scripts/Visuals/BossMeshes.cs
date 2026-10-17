using UnityEngine;

namespace PoeClone.Visuals
{
    /// <summary>
    /// Detailed meshes the procedural boss bodies borrow from the player's anatomy (made by the
    /// Editor's AnatomyMeshes), loaded from Resources/BossMeshes. Hands: palm along Y with the wrist
    /// at the top, knuckles across Z, closed round a handle.
    /// </summary>
    [CreateAssetMenu(menuName = "PoeClone/Boss Meshes")]
    public class BossMeshes : ScriptableObject
    {
        public Mesh HandLGrip, HandRGrip;

        private static BossMeshes loaded;

        public static BossMeshes Get()
        {
            if (loaded == null)
                loaded = Resources.Load<BossMeshes>("BossMeshes");
            return loaded;
        }
    }
}
