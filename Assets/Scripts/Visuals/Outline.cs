using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PoeClone.Visuals
{
    /// <summary>
    /// White "currently aimed at" outline for a character, built from the classic inverted-hull
    /// trick: one slightly inflated, backface-only clone per mesh renderer. Clones are parented as
    /// siblings of the renderer they copy, so they automatically follow whatever pivot animates
    /// that part (walk, attack, stagger) with no extra code. Built lazily on first use and
    /// self-provisioned by whoever wants to highlight this character (GetComponent-or-AddComponent),
    /// so no prefab wiring is required. A clone only shows while the part it copies is itself shown
    /// (hidden horns, a dropped disguise); a character that changes shape calls <see cref="Rebuild"/>.
    /// </summary>
    public class Outline : MonoBehaviour
    {
        [SerializeField] private float scaleInflation = 1.06f;

        private static Material sharedMaterial;

        private readonly List<GameObject> clones = new List<GameObject>();
        private readonly List<Renderer> sources = new List<Renderer>();
        private bool built;
        private bool highlighted;

        public void SetHighlighted(bool on)
        {
            if (on && !built)
                Build();
            highlighted = on;
            Sync();
        }

        /// <summary>The body changed (parts added or swapped): the clones are made again on next use.</summary>
        public void Rebuild()
        {
            foreach (GameObject clone in clones)
            {
                if (clone != null)
                    Destroy(clone);
            }
            clones.Clear();
            sources.Clear();
            built = false;
            if (highlighted)
                Build();
            Sync();
        }

        private void LateUpdate()
        {
            if (highlighted)
                Sync();
        }

        // Each clone shows only while highlighted and while the part it copies is shown.
        private void Sync()
        {
            for (int i = 0; i < clones.Count; i++)
            {
                if (clones[i] == null)
                    continue;
                Renderer source = sources[i];
                bool show = highlighted && source != null && source.enabled && source.gameObject.activeInHierarchy;
                if (clones[i].activeSelf != show)
                    clones[i].SetActive(show);
            }
        }

        private void Build()
        {
            built = true;
            EnsureMaterial();

            foreach (MeshRenderer sourceRenderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (sourceRenderer.sharedMaterial == sharedMaterial)
                    continue;
                MeshFilter sourceFilter = sourceRenderer.GetComponent<MeshFilter>();
                if (sourceFilter == null || sourceFilter.sharedMesh == null)
                    continue;

                Transform source = sourceRenderer.transform;
                GameObject clone = new GameObject("Outline_" + source.name);
                clone.transform.SetParent(source.parent, false);
                clone.transform.localPosition = source.localPosition;
                clone.transform.localRotation = source.localRotation;
                clone.transform.localScale = source.localScale * scaleInflation;

                MeshFilter cloneFilter = clone.AddComponent<MeshFilter>();
                cloneFilter.sharedMesh = sourceFilter.sharedMesh;

                MeshRenderer cloneRenderer = clone.AddComponent<MeshRenderer>();
                cloneRenderer.sharedMaterial = sharedMaterial;
                cloneRenderer.shadowCastingMode = ShadowCastingMode.Off;
                cloneRenderer.receiveShadows = false;
                cloneRenderer.lightProbeUsage = LightProbeUsage.Off;

                clone.SetActive(false);
                clones.Add(clone);
                sources.Add(sourceRenderer);
            }
        }

        private static void EnsureMaterial()
        {
            if (sharedMaterial != null)
                return;

            Shader shader = Shader.Find("PoeClone/Outline");
            sharedMaterial = new Material(shader) { color = Color.white };
        }
    }
}
