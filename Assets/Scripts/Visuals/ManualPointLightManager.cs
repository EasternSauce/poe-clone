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
    ///
    /// The scene can hold more lights than the shader takes (every area lives in one scene), so
    /// each frame the ones nearest to where the camera looks are sent, and lights out of reach
    /// of the view are skipped entirely.
    /// </summary>
    public class ManualPointLightManager : MonoBehaviour
    {
        public const int MaxLights = 24;

        // Phones: every light is a loop step in the shader for every pixel, and town especially
        // clusters several lamps close together, where none of them are cheaply out of range -
        // mobile is a "runs fine, looks plainer" tier, not a pretty one, so this stays low: the
        // player's own light (always nearest, always included) plus one or two nearby torches.
        public const int MaxLightsOnTouch = 2;

        private static int Limit => Inventory.TouchMode.Active || Application.isMobilePlatform ? MaxLightsOnTouch : MaxLights;

        // Lights whose reach ends further than this from the point the camera looks at are off
        // screen (the view is roughly 40 m across).
        private const float ViewRadius = 32f;

        private static ManualPointLightManager instance;

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

        private readonly List<Light> _nearest = new List<Light>();
        private readonly List<float> _nearestDistance = new List<float>();

        private void Awake()
        {
            instance = this;
            CollectLights();
        }

        /// <summary>Re-scans the scene, for lights added at runtime (e.g. by the WorldBuilder).</summary>
        public static void Refresh()
        {
            if (instance != null)
                instance.CollectLights();
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
                if (l.type == LightType.Point || l.type == LightType.Spot)
                    _lights.Add(l);
            }
        }

        // LateUpdate so it reads each torch's flicker-updated intensity
        // (set in TorchFlicker.Update) before pushing this frame's values.
        private void LateUpdate()
        {
            SelectNearest(ViewFocus());

            int count = 0;
            for (int i = 0; i < _nearest.Count; i++)
            {
                var l = _nearest[i];
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

        // Where the camera's view centre meets the ground (y = 0), or the camera itself.
        private static Vector3 ViewFocus()
        {
            Camera cam = Camera.main;
            if (cam == null)
                return Vector3.zero;
            Vector3 origin = cam.transform.position;
            Vector3 forward = cam.transform.forward;
            if (forward.y < -0.01f)
                return origin + forward * (-origin.y / forward.y);
            return origin;
        }

        // Fills _nearest with up to MaxLights active lights that can reach the view, closest first.
        private void SelectNearest(Vector3 focus)
        {
            _nearest.Clear();
            _nearestDistance.Clear();
            for (int i = 0; i < _lights.Count; i++)
            {
                var l = _lights[i];
                if (l == null || !l.isActiveAndEnabled) continue;

                Vector3 d = l.transform.position - focus;
                d.y = 0f;
                float distance = d.magnitude - l.range;
                if (distance > ViewRadius) continue;

                // Insertion into a short sorted list.
                int limit = Limit;
                int at = _nearestDistance.Count;
                while (at > 0 && _nearestDistance[at - 1] > distance) at--;
                if (at >= limit) continue;
                _nearest.Insert(at, l);
                _nearestDistance.Insert(at, distance);
                if (_nearest.Count > limit)
                {
                    _nearest.RemoveAt(limit);
                    _nearestDistance.RemoveAt(limit);
                }
            }
        }
    }
}
