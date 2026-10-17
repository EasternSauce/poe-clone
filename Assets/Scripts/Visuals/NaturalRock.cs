using UnityEngine;
using PoeClone.World;

namespace PoeClone.Visuals
{
    /// <summary>Position-seeded dressing; shares baked meshes and never consumes world RNG.</summary>
    public sealed class NaturalRock : MonoBehaviour
    {
        public Mesh[] variants;

        private void Start() => Dress();

        public void Dress()
        {
            if (variants == null || variants.Length == 0) return;
            Vector3 p = transform.position;
            int seed = unchecked(Mathf.RoundToInt(p.x * 100f) * 73856093 ^
                Mathf.RoundToInt(p.z * 100f) * 19349663 ^ Mathf.RoundToInt(p.y * 100f) * 83492791);
            // System.Random treats +seed and -seed identically; keep mirrored placements distinct.
            var random = new System.Random(seed & int.MaxValue);
            float Range(float a, float b) => Mathf.Lerp(a, b, (float)random.NextDouble());
            int area = PoeClone.Network.CoopSession.AreaAt(p);
            bool green = area == WorldBuilder.Greenwood || area == WorldBuilder.Haven || area == WorldBuilder.Graveyard;
            bool dry = WorldBuilder.IsActTwo(area);
            float moss = green && random.NextDouble() < 0.65 ? Range(0.38f, 0.86f) : 0f;
            Color cool = new Color(0.43f, 0.46f, 0.47f);
            Color warm = new Color(0.53f, 0.49f, 0.42f);
            Color tint = Color.Lerp(cool, warm, Range(0f, 1f));
            if (dry) tint = Color.Lerp(new Color(0.52f, 0.42f, 0.31f), new Color(0.68f, 0.60f, 0.46f), Range(0f, 1f));
            if (area == WorldBuilder.Frozen) tint = Color.Lerp(cool, new Color(0.64f, 0.69f, 0.71f), Range(0.35f, 0.8f));
            if (area == WorldBuilder.Ruins) tint *= 0.78f;
            var block = new MaterialPropertyBlock();
            foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>())
            {
                filter.sharedMesh = variants[random.Next(variants.Length)];
                // Keep variations within the authored footprint and retain collision clearance.
                bool main = filter.name == "RockMain";
                filter.transform.localRotation = Quaternion.Euler(Range(-9f, 9f), Range(0f, 360f), Range(-9f, 9f));
                filter.transform.localScale = main
                    ? new Vector3(Range(1.35f, 1.8f), Range(0.83f, 1.1f), Range(1.15f, 1.5f))
                    : new Vector3(Range(0.35f, 0.85f), Range(0.3f, 0.62f), Range(0.4f, 0.8f));
                if (!main)
                    filter.transform.localPosition = new Vector3(Range(-0.65f, 0.9f), 0.1f, Range(-0.45f, 0.45f));
                // Seat tilted slabs as well as boulders into the ground rather than floating.
                float bottom = float.PositiveInfinity;
                Bounds bounds = filter.sharedMesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 v = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                        (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                    bottom = Mathf.Min(bottom, (filter.transform.localRotation * Vector3.Scale(v, filter.transform.localScale)).y);
                }
                Vector3 seated = filter.transform.localPosition;
                seated.y = -bottom - 0.06f;
                filter.transform.localPosition = seated;
                var renderer = filter.GetComponent<Renderer>();
                if (renderer == null) continue;
                renderer.GetPropertyBlock(block);
                // Preserve explicit icy/sandstone tints placed by WorldBuilder.
                Color existing = block.GetColor("_BaseColor");
                Color color = existing.a > 0f ? existing : tint;
                color *= Range(0.9f, 1.08f); color.a = 1f;
                block.SetColor("_BaseColor", color);
                block.SetFloat("_RockSurface", 1f);
                block.SetFloat("_RockMoss", moss * Range(0.75f, 1f));
                block.SetFloat("_TexInfluence", 0.24f);
                block.SetFloat("_RimIntensity", 0.035f);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
