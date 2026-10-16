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
            // 0: fixed anatomy, 1: left hand, 2: right hand.
            public int Hand;
        }

        [SerializeField] private Part[] parts = Array.Empty<Part>();
        private bool leftGrip, rightGrip;

        public void Configure(Part[] value) => parts = value;

        private void OnEnable() => RenderPipelineManager.beginCameraRendering += BeforeCamera;
        private void OnDisable() => RenderPipelineManager.beginCameraRendering -= BeforeCamera;

        public void SetEquipment(EquipmentSet equipment)
        {
            ItemData main = equipment?.Get(EquipSlot.MainHand);
            rightGrip = main != null;
            leftGrip = equipment?.Get(EquipSlot.OffHand) != null ||
                (main != null && (SlotRules.IsTwoHandedMelee(main.WeaponType) || main.WeaponType == WeaponType.Bow || main.WeaponType == WeaponType.Staff));
            Apply(null);
        }

        private void BeforeCamera(ScriptableRenderContext context, Camera camera) => Apply(camera);

        private void Apply(Camera camera)
        {
            // Use projected size, so orthographic gameplay and perspective inventory cameras
            // select independently. No mesh generation, material instances or searches per frame.
            foreach (Part part in parts)
            {
                if (part.Filter == null) continue;
                bool near = true;
                if (camera != null)
                {
                    Vector3 scale = part.Filter.transform.lossyScale;
                    Vector3 extent = Vector3.Scale(part.Near.bounds.size, scale);
                    float size = Mathf.Max(Mathf.Abs(extent.x), Mathf.Abs(extent.y), Mathf.Abs(extent.z));
                    float depth = Vector3.Dot(part.Filter.transform.position - camera.transform.position, camera.transform.forward);
                    float viewHeight = camera.orthographic ? 2 * camera.orthographicSize :
                        2 * Mathf.Max(.01f, depth) * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f);
                    near = depth > 0 && size * camera.pixelHeight / Mathf.Max(.01f, viewHeight) >= 48;
                }
                bool grip = part.Hand == 1 ? leftGrip : part.Hand == 2 && rightGrip;
                Mesh mesh = grip && part.GripNear != null ? (near ? part.GripNear : part.GripFar) : (near ? part.Near : part.Far);
                if (mesh != null && part.Filter.sharedMesh != mesh) part.Filter.sharedMesh = mesh;
            }
        }
    }
}
