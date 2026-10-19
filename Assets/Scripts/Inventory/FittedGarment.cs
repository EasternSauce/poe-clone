using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.Inventory
{
    /// <summary>Weights armour skirts to the anatomical rig instead of dragging them with the chest.</summary>
    public sealed class FittedGarment : MonoBehaviour
    {
        private readonly List<Mesh> meshes = new List<Mesh>();

        public void Bind(Transform model, Transform socket)
        {
            Transform left = model.Find("LegL"), right = model.Find("LegR");
            if (left == null || right == null) return;
            Transform[] bones = { socket, left, right, left.Find("Knee"), right.Find("Knee") };
            // Bind in the rig's authored rest space, even if equipped during a stride or fidget.
            // The torso pivots at .9; UpperOffset cancels that translation in the neutral pose.
            Vector3[] rest = { new Vector3(0, 1.225f, 0), new Vector3(-.15f, .85f, 0),
                new Vector3(.15f, .85f, 0), new Vector3(-.15f, .43f, 0), new Vector3(.15f, .43f, 0) };
            foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
            {
                if (!IsLowerGarment(filter.transform)) continue;
                if (!filter.TryGetComponent<MeshRenderer>(out var original) || !original.enabled || filter.sharedMesh == null) continue;
                Mesh mesh = Instantiate(filter.sharedMesh);
                mesh.name += "_Fitted";
                meshes.Add(mesh);
                Matrix4x4 toModel = Matrix4x4.Translate(rest[0]) * socket.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Vector3[] vertices = mesh.vertices;
                var weights = new BoneWeight[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 p = toModel.MultiplyPoint3x4(vertices[i]);
                    float pelvis = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.0f, .68f, p.y));
                    float knee = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.53f, .20f, p.y));
                    float rightSide = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.10f, .10f, p.x));
                    var influences = new List<KeyValuePair<int, float>>(5);
                    influences.Add(new KeyValuePair<int, float>(0, 1 - pelvis));
                    influences.Add(new KeyValuePair<int, float>(1, pelvis * (1 - knee) * (1 - rightSide)));
                    influences.Add(new KeyValuePair<int, float>(2, pelvis * (1 - knee) * rightSide));
                    influences.Add(new KeyValuePair<int, float>(3, pelvis * knee * (1 - rightSide)));
                    influences.Add(new KeyValuePair<int, float>(4, pelvis * knee * rightSide));
                    influences.Sort((a, b) => b.Value.CompareTo(a.Value));
                    weights[i] = new BoneWeight { boneIndex0 = influences[0].Key, weight0 = influences[0].Value,
                        boneIndex1 = influences[1].Key, weight1 = influences[1].Value,
                        boneIndex2 = influences[2].Key, weight2 = influences[2].Value,
                        boneIndex3 = influences[3].Key, weight3 = influences[3].Value };
                }
                mesh.boneWeights = weights;
                var bindposes = new Matrix4x4[bones.Length];
                for (int i = 0; i < bones.Length; i++) bindposes[i] = Matrix4x4.Translate(-rest[i]) * toModel;
                mesh.bindposes = bindposes;
                var properties = new MaterialPropertyBlock();
                original.GetPropertyBlock(properties);
                Material[] materials = original.sharedMaterials;
                var shadows = original.shadowCastingMode;
                bool receiveShadows = original.receiveShadows;
                uint layers = original.renderingLayerMask;
                original.enabled = false;
                var skin = filter.gameObject.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh = mesh;
                skin.sharedMaterials = materials;
                skin.SetPropertyBlock(properties);
                skin.bones = bones;
                skin.rootBone = model;
                skin.quality = SkinQuality.Bone4;
                skin.shadowCastingMode = shadows;
                skin.receiveShadows = receiveShadows;
                skin.renderingLayerMask = layers;
                Vector3 scale = filter.transform.lossyScale;
                float minimum = Mathf.Max(.001f, Mathf.Min(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                skin.localBounds = new Bounds(mesh.bounds.center, mesh.bounds.size + Vector3.one * (.8f / minimum));
            }
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in meshes)
                if (mesh != null)
                {
                    if (Application.isPlaying) Destroy(mesh);
                    else DestroyImmediate(mesh);
                }
        }

        private bool IsLowerGarment(Transform part)
        {
            // Seams, pleats and tassets follow the same bones as the garment underneath them.
            for (Transform p = part; p != null && p != transform; p = p.parent)
                if (p.name == "Hem" || p.name == "Skirt" || p.name == "Fauld" || p.name == "FauldTrim") return true;
            return false;
        }
    }
}
