using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace PoeClone.Inventory
{
    /// <summary>Shares baked procedural meshes, selecting detail per camera and hand poses per equipment.</summary>
    public sealed class ProceduralAnatomy : MonoBehaviour
    {
        [Serializable]
        public sealed class Part
        {
            public MeshFilter Filter;
            public Mesh Near, Far, GripNear, GripFar;
            public SkinnedMeshRenderer Skin;
            public Mesh Covered;
            // 0: fixed anatomy, 1: left hand, 2: right hand.
            public int Hand;
            [NonSerialized] internal Renderer Renderer;
        }

        [SerializeField] private Part[] parts = Array.Empty<Part>();
        private bool leftGrip, rightGrip, gloves;
        private Camera skinCamera;

        public void Configure(Part[] value)
        {
            parts = value;
            SynchronizeBounds();
        }

        private void OnEnable()
        {
            SynchronizeBounds();
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
        }
        private void OnDisable() => RenderPipelineManager.beginCameraRendering -= BeforeCamera;

        private void SynchronizeBounds()
        {
            // Include fixed anatomy (hair and limbs), not just the meshes with detail variants.
            // Explicit local bounds also survive stale native renderer bounds after an Editor rebuild.
            foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null && filter.TryGetComponent<Renderer>(out var renderer))
                    renderer.localBounds = filter.sharedMesh.bounds;

            foreach (Part part in parts)
                if (part.Filter != null)
                    part.Renderer = part.Filter.GetComponent<Renderer>();
        }

        public void SetEquipment(EquipmentSet equipment)
        {
            gloves = equipment?.Get(EquipSlot.Gloves) != null;
            ItemData main = equipment?.Get(EquipSlot.MainHand);
            rightGrip = main != null;
            leftGrip = equipment?.Get(EquipSlot.OffHand) != null ||
                (main != null && (SlotRules.IsTwoHandedMelee(main.WeaponType) || main.WeaponType == WeaponType.Bow || main.WeaponType == WeaponType.Staff));
            Apply(null);
            ApplyParts(null,true);
        }

        private void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            if ((camera.cullingMask & (1 << gameObject.layer)) != 0) skinCamera = camera;
            Apply(camera);
        }

        // Skinning buffers are prepared before camera rendering. Swap skinned variants in the
        // player loop, using the last camera that actually rendered this model's layer.
        private void LateUpdate() => ApplyParts(skinCamera != null ? skinCamera : Camera.main,true);
        private void Apply(Camera camera) => ApplyParts(camera,false);

        private void ApplyParts(Camera camera,bool skinParts)
        {
            // Use projected size, so orthographic gameplay and perspective inventory cameras
            // select independently. No mesh generation, material instances or searches per frame.
            foreach (Part part in parts)
            {
                if (part.Filter == null || (part.Skin != null) != skinParts) continue;
                bool near = true;
                if (camera != null)
                {
                    Vector3 scale = part.Filter.transform.lossyScale;
                    Vector3 extent = part.Skin != null ? Vector3.one*.14f : Vector3.Scale(part.Near.bounds.size, scale);
                    float size = Mathf.Max(Mathf.Abs(extent.x), Mathf.Abs(extent.y), Mathf.Abs(extent.z));
                    float depth = Vector3.Dot(part.Filter.transform.position - camera.transform.position, camera.transform.forward);
                    float viewHeight = camera.orthographic ? 2 * camera.orthographicSize :
                        2 * Mathf.Max(.01f, depth) * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f);
                    near = depth > 0 && size * camera.pixelHeight / Mathf.Max(.01f, viewHeight) >= 48;
                }
                bool grip = part.Hand == 1 ? leftGrip : part.Hand == 2 && rightGrip;
                Mesh mesh = grip && part.GripNear != null ? (near ? part.GripNear : part.GripFar) : (near ? part.Near : part.Far);
                if (part.Skin != null)
                {
                    if (gloves && part.Covered != null) mesh = part.Covered;
                    if (mesh != null && part.Skin.sharedMesh != mesh)
                    {
                        // A skinned renderer that has drawn keeps buffers sized for its old mesh and
                        // stops rendering after a swap ("does not match the expected mesh data size");
                        // re-enabling it around the swap rebuilds them.
                        part.Skin.enabled = false;
                        part.Skin.sharedMesh = mesh;
                        part.Skin.enabled = true;
                        part.Skin.localBounds = new Bounds(new Vector3(0,-.4f,0),Vector3.one*1.9f);
                    }
                    continue;
                }
                if (mesh != null && part.Filter.sharedMesh != mesh)
                {
                    part.Filter.sharedMesh = mesh;
                    if (part.Renderer != null) part.Renderer.localBounds = mesh.bounds;
                }
            }
        }
    }
}
