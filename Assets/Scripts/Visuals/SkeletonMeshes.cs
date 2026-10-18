using System;
using UnityEngine;

namespace PoeClone.Visuals
{
    /// <summary>
    /// The skeleton enemies' and summons' sculpted bones, baked by the Editor's SkeletonMeshBuilder
    /// and loaded from Resources/SkeletonMeshes. Each piece belongs to one joint of the humanoid
    /// template and is centred on itself (the hover outline scales a part about its pivot);
    /// <see cref="Piece.Centre"/> puts it back in the joint's space. Hands come open or closed
    /// round a held weapon (Grip), sharing the fleshed hand's finger paths.
    /// </summary>
    public class SkeletonMeshes : ScriptableObject
    {
        [Serializable]
        public struct Piece
        {
            public Mesh Mesh;
            public Vector3 Centre;
        }

        public Material Bone;
        public Piece Skull, Jaw, Spine, Ribcage, Pelvis, Loincloth;
        public Piece HumerusL, HumerusR, FemurL, FemurR, ShinL, ShinR;
        public Piece ForearmL, ForearmR, ForearmLGrip, ForearmRGrip;

        private static SkeletonMeshes loaded;

        public static SkeletonMeshes Get()
        {
            if (loaded == null)
                loaded = Resources.Load<SkeletonMeshes>("SkeletonMeshes");
            return loaded;
        }
    }
}
