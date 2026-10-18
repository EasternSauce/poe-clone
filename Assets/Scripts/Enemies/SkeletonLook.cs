using UnityEngine;
using UnityEngine.Rendering;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>
    /// Turns the humanoid template into a skeleton: the flesh is hidden and sculpted bones (baked
    /// by the Editor into <see cref="SkeletonMeshes"/>) go on the same joints, so walking, swings
    /// and deaths still animate them: a skull with a jaw and deep sockets round the kind's glowing
    /// eyes, a spine and ribcage with collarbones and shoulder blades, a pelvis under a rotted
    /// loincloth, and limb bones down to finger and toe bones. A hand holding something closes
    /// round it with the fleshed hand's grip.
    /// </summary>
    public static class SkeletonLook
    {
        private static readonly string[] Flesh = { "ContinuousArm", "ContinuousLeg", "Thigh", "Shin", "Boot", "BootCuff", "Torso", "TunicSkirt", "Belt", "Upper", "Forearm", "Hand", "Neck", "Head" };
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public static void Build(Transform model, EnemyKind kind, bool gripLeft, bool gripRight)
        {
            Transform upper = Find(model, "UpperOffset");
            SkeletonMeshes bones = SkeletonMeshes.Get();
            if (upper == null || bones == null)
                return;

            // Renderers off rather than objects: the walk animator still reads the hands' positions.
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (System.Array.IndexOf(Flesh, r.name) >= 0)
                    r.enabled = false;
            }

            Color bone = kind.Skin;
            foreach (string side in new[] { "L", "R" })
            {
                bool left = side == "L";
                Transform leg = Find(model, "Leg" + side);
                if (leg != null)
                {
                    Attach(leg, "Femur", bones.Bone, bone, left ? bones.FemurL : bones.FemurR);
                    Transform knee = Find(leg, "Knee");
                    if (knee != null)
                        Attach(knee, "Shin", bones.Bone, bone, left ? bones.ShinL : bones.ShinR);
                }

                Transform arm = Find(model, "Arm" + side);
                if (arm != null)
                {
                    Attach(arm, "Humerus", bones.Bone, bone, left ? bones.HumerusL : bones.HumerusR);
                    Transform elbow = Find(arm, "Elbow");
                    bool grip = left ? gripLeft : gripRight;
                    if (elbow != null)
                        Attach(elbow, "Forearm", bones.Bone, bone,
                            left ? (grip ? bones.ForearmLGrip : bones.ForearmL) : (grip ? bones.ForearmRGrip : bones.ForearmR));
                }
            }

            // The pelvis on the model itself (it stays put while the upper body leans), under a rag.
            Attach(model, "Pelvis", bones.Bone, bone, bones.Pelvis);
            GameObject rag = RuntimePrimitives.CreateMesh(bones.Loincloth.Mesh, model, kind.Pants, "BonesLoincloth");
            rag.transform.localPosition = bones.Loincloth.Centre;
            Attach(upper, "Spine", bones.Bone, bone, bones.Spine);
            Attach(upper, "Ribcage", bones.Bone, bone, bones.Ribcage);
            Attach(upper, "Skull", bones.Bone, bone, bones.Skull);
            Attach(upper, "Jaw", bones.Bone, bone, bones.Jaw);

            // The kind's glowing eyes, deep in the sockets.
            foreach (string eye in new[] { "EyeL", "EyeR" })
            {
                Transform t = Find(upper, eye);
                if (t == null) continue;
                t.localPosition = new Vector3(eye == "EyeL" ? -.05f : .05f, 1.876f, .112f);
                t.localScale = new Vector3(.032f, .026f, .02f);
            }
        }

        // One renderer per joint, centred on its own bounds (the hover outline scales each part
        // about its pivot) and placed back in the joint's space.
        private static void Attach(Transform joint, string name, Material material, Color color, SkeletonMeshes.Piece piece)
        {
            if (piece.Mesh == null)
                return;
            var go = new GameObject("Bones" + name);
            go.transform.SetParent(joint, false);
            go.transform.localPosition = piece.Centre;
            go.AddComponent<MeshFilter>().sharedMesh = piece.Mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(block);
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                    return t;
            }
            return null;
        }
    }
}
