using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.Inventory
{
    /// <summary>Bends procedural boot shapes at the ankle. Meshes are prepared once per equip.</summary>
    public sealed class AnatomicalFootwear : MonoBehaviour
    {
        private readonly List<Mesh> ownedMeshes = new List<Mesh>();

        public void Bind(Transform knee, Transform ankle)
        {
            foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.TryGetComponent<MeshRenderer>(out var original) || filter.sharedMesh == null)
                    continue;
                Mesh mesh = Instantiate(filter.sharedMesh);
                mesh.name = filter.sharedMesh.name + "_Ankle";
                ownedMeshes.Add(mesh);
                Matrix4x4 toKnee = knee.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Vector3[] vertices = mesh.vertices;
                var weights = new BoneWeight[vertices.Length];
                for (int i = 0; i < weights.Length; i++)
                {
                    float y = toKnee.MultiplyPoint3x4(vertices[i]).y;
                    float weight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.22f, -.38f, y));
                    weights[i] = weight > .5f
                        ? new BoneWeight { boneIndex0 = 1, weight0 = weight, boneIndex1 = 0, weight1 = 1 - weight }
                        : new BoneWeight { boneIndex0 = 0, weight0 = 1 - weight, boneIndex1 = 1, weight1 = weight };
                }
                mesh.boneWeights = weights;
                mesh.bindposes = new[] { toKnee, Matrix4x4.Translate(-ankle.localPosition) * toKnee };
                var properties = new MaterialPropertyBlock();
                original.GetPropertyBlock(properties);
                Material[] materials = original.sharedMaterials;
                var shadows = original.shadowCastingMode;
                bool receiveShadows = original.receiveShadows;
                uint layers = original.renderingLayerMask;
                original.enabled = false;
                // Unity replaces the MeshRenderer when adding another renderer to this object.
                var skin = filter.gameObject.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMaterials = materials;
                skin.SetPropertyBlock(properties);
                skin.bones = new[] { knee, ankle };
                skin.sharedMesh = mesh;
                skin.rootBone = knee;
                skin.quality = SkinQuality.Bone2;
                skin.shadowCastingMode = shadows;
                skin.receiveShadows = receiveShadows;
                skin.renderingLayerMask = layers;
                // Keep the small buckles and trim visible throughout ankle flexion.
                Vector3 scale = filter.transform.lossyScale;
                float minimumScale = Mathf.Max(.001f, Mathf.Min(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                skin.localBounds = new Bounds(mesh.bounds.center, mesh.bounds.size + Vector3.one * (.35f / minimumScale));
            }
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in ownedMeshes)
                if (mesh != null) Destroy(mesh);
        }
    }
}
