using UnityEngine;

namespace PoeClone.Inventory
{
    /// <summary>An ankle pivot shared by the bare foot, trouser skin and equipped footwear.</summary>
    public sealed class AnatomicalAnkle : MonoBehaviour, IPreviewPose
    {
        // The portrait copy keeps its ankles, so it stands with the same natural gait and idle.
        public void PrepareForPreview() { }

        [SerializeField] private Transform joint;
        [SerializeField] private Transform foot;
        [SerializeField] private Vector3 footRestPosition;
        [SerializeField] private Quaternion footRestRotation = Quaternion.identity;

        public void Configure(Transform joint, Transform foot = null)
        {
            this.joint = joint;
            this.foot = foot;
            if (foot != null)
            {
                footRestPosition = foot.localPosition;
                footRestRotation = foot.localRotation;
            }
        }

        public void Pose(float angle)
        {
            Quaternion rotation = Quaternion.Euler(angle, 0, 0);
            joint.localRotation = rotation;
            // Booted NPCs use skinned footwear instead of a separate bare foot.
            if (foot == null) return;
            foot.localPosition = joint.localPosition + rotation * (footRestPosition - joint.localPosition);
            foot.localRotation = rotation * footRestRotation;
        }

        public void FitFootwear(GameObject equipment)
        {
            equipment.AddComponent<AnatomicalFootwear>().Bind(transform, joint);
        }
    }
}
