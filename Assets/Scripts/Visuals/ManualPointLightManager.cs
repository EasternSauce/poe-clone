using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.Visuals
{
    /// <summary>
    /// Feeds a small set of point lights (e.g. torches) into global shader
    /// arrays that custom shaders like ToonLit read directly in their
    /// fragment shader.
    ///
    /// Why this exists: ToonLit already contains a standard URP
    /// GetAdditionalLightsCount()/GetAdditionalLight() loop guarded by the
    /// correct (current) Forward+ keyword, but in practice those lights were
    /// not reaching the shader at runtime in this project - the torches were
    /// fully enabled, correctly positioned/wired, and the pipeline's own Lit
    /// shader picked them up perfectly on a test object right next to them,
    /// but ToonLit stayed completely unlit even at extreme intensity/range.
    /// Rather than keep chasing the exact URP keyword/variant mismatch, this
    /// manager pushes the light data manually as plain global shader
    /// uniforms once per frame, and the shader does its own simple
    /// windowed-inverse-square point light calculation with it. This is the
    /// same "manual forward-additive lighting" approach used long before
    /// SRPs existed, so it does not depend on any Forward+/clustered-light
    /// keyword matching at all.
    /// </summary>
    public class ManualPointLightManager : MonoBehaviour
    {
        public const int MaxLights = 24;

        [Tooltip("Legacy scoping option, kept only for backwards compatibility with existing scenes. Lights are now collected from the whole active scene (see CollectLights), so this is no longer used to restrict the search.")]
        public Transform lightsRoot;

        [Tooltip("Only Point and Spot lights found anywhere in the scene are used, up to MaxLights.")]
        public bool includeInactiveAtStart = true;

        private static readonly int PosRangeId = Shader.PropertyToID("_ManualLightPosRange");
        private static readonly int ColorIntensityId = Shader.PropertyToID("_ManualLightColorIntensity");
        private static readonly int CountId = Shader.PropertyToID("_ManualLightCount");

        private readonly List<Light> _lights = new List<Light>();
        private readonly Vector4[] _posRange = new Vector4[MaxLights];
        private readonly Vector4[] _colorIntensity = new Vector4[MaxLights];

        private void Awake()
        {
            CollectLights();
        }

        // Scans the whole scene rather than a single subtree: torches (and any other point/spot
        // lights) get added in various places in this project - some under a shared "Torches"
        // container, some as children of their own structure (e.g. gateway pillars) - and a
        // subtree-scoped search silently missed whichever ones lived outside lightsRoot, leaving
        // them fully unlit with no error.
        private void CollectLights()
        {
            _lights.Clear();
            var found = FindObjectsByType<Light>(
                includeInactiveAtStart ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            foreach (var l in found)
            {
                if (_lights.Count >= MaxLights) break;
                if (l.type == LightType.Point || l.type == LightType.Spot)
                    _lights.Add(l);
            }
        }

        // LateUpdate so it reads each torch's flicker-updated intensity
        // (set in TorchFlicker.Update) before pushing this frame's values.
        private void LateUpdate()
        {
            int count = 0;
            for (int i = 0; i < _lights.Count && count < MaxLights; i++)
            {
                var l = _lights[i];
                if (l == null || !l.isActiveAndEnabled) continue;

                Vector3 pos = l.transform.position;
                _posRange[count] = new Vector4(pos.x, pos.y, pos.z, l.range);
                _colorIntensity[count] = new Vector4(l.color.r, l.color.g, l.color.b, l.intensity);
                count++;
            }

            // Zero out any remaining slots so stale data from a light that
            // just got disabled doesn't keep contributing.
            for (int i = count; i < MaxLights; i++)
            {
                _posRange[i] = Vector4.zero;
                _colorIntensity[i] = Vector4.zero;
            }

            Shader.SetGlobalVectorArray(PosRangeId, _posRange);
            Shader.SetGlobalVectorArray(ColorIntensityId, _colorIntensity);
            Shader.SetGlobalInt(CountId, count);
        }
    }
}
