using UnityEngine;

namespace PoeClone.Visuals
{
    /// <summary>Shared baked silhouettes selected without changing the world's random stream.</summary>
    [RequireComponent(typeof(MeshFilter))]
    public sealed class WindTree : MonoBehaviour
    {
        public Mesh[] variants;
        public Vector3[] offeringTips;
        public Transform offeringSocket;

        private void Awake()
        {
            if (variants == null || variants.Length == 0) return;
            Vector3 p = transform.position;
            int seed = unchecked(Mathf.RoundToInt(p.x * 100) * 73856093 ^ Mathf.RoundToInt(p.z * 100) * 19349663);
            int index = (seed & int.MaxValue) % variants.Length;
            GetComponent<MeshFilter>().sharedMesh = variants[index];
            if (offeringSocket != null && offeringTips != null && index < offeringTips.Length)
                offeringSocket.localPosition = offeringTips[index];
        }
    }
}
