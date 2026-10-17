using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PoeClone.Visuals
{
    /// <summary>Shares a planar scene capture per water type between visible, coplanar chunks.</summary>
    [DefaultExecutionOrder(1000)]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class ReflectiveWaterSurface : MonoBehaviour
    {
        private static readonly List<ReflectiveWaterSurface> surfaces = new();
        private static readonly Plane[] frustum = new Plane[6];
        private static readonly UniversalRenderPipeline.SingleCameraRequest request = new();
        private static Camera reflection, source;
        private static UniversalAdditionalCameraData sourceData;
        private static CameraOverrideOption originalDepth, originalColor;
        private static RenderTexture texture, puddleTexture;
        private static int lastFrame = -1;
        private MeshRenderer surface;
        private bool planar;
        internal bool IsPuddle { get; set; }

        private void OnEnable()
        {
            surface = GetComponent<MeshRenderer>();
            planar = surface.sharedMaterial.GetFloat("_Waterfall") < 0.5f;
            gameObject.layer = 4; // Built-in Water layer; never capture water recursively.
            surfaces.Add(this);
        }

        private void LateUpdate()
        {
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            Camera view = Camera.main;
            Shader.SetGlobalFloat("_WaterReflectionValid", 0f);
            Shader.SetGlobalFloat("_PuddleReflectionValid", 0f);
            if (view == null || !view.isActiveAndEnabled) { RestoreSource(); return; }
            GeometryUtility.CalculateFrustumPlanes(view, frustum);
            ReflectiveWaterSurface nearest = null;
            ReflectiveWaterSurface nearestPuddle = null;
            bool visible = false;
            float distance = float.MaxValue;
            float puddleDistance = float.MaxValue;
            foreach (var candidate in surfaces)
            {
                if (!candidate.surface.enabled || (view.cullingMask & (1 << candidate.gameObject.layer)) == 0 ||
                    !GeometryUtility.TestPlanesAABB(frustum, candidate.surface.bounds)) continue;
                visible = true;
                if (!candidate.planar) continue;
                float d = candidate.surface.bounds.SqrDistance(view.transform.position);
                // Puddles sit above rivers and lakes, so each height needs its own capture.
                if (candidate.IsPuddle)
                {
                    if (d < puddleDistance) { nearestPuddle = candidate; puddleDistance = d; }
                }
                else if (d < distance) { nearest = candidate; distance = d; }
            }
            if (!visible) { RestoreSource(); return; }
            if (source != view)
            {
                RestoreSource();
                source = view;
                sourceData = view.GetUniversalAdditionalCameraData();
                originalDepth = sourceData.requiresDepthOption;
                originalColor = sourceData.requiresColorOption;
            }
            sourceData.requiresDepthOption = CameraOverrideOption.On;
            sourceData.requiresColorOption = CameraOverrideOption.On;
            if (nearest != null) RenderSurface(view, nearest, ref texture);
            if (nearestPuddle != null) RenderSurface(view, nearestPuddle, ref puddleTexture);
        }

        private static void RenderSurface(Camera view, ReflectiveWaterSurface selected, ref RenderTexture target)
        {
            bool economical = Application.isMobilePlatform || Shader.IsKeywordEnabled("_MOBILE_LITE") || AdaptiveQuality.RenderLevel >= 2;
            int width = Mathf.Clamp(view.pixelWidth / (economical ? 4 : 2), 128, economical ? 384 : 768);
            int height = Mathf.Max(64, Mathf.RoundToInt(width / view.aspect));
            if (target == null || target.width != width || target.height != height)
            {
                ReleaseTexture(ref target);
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                {
                    name = selected.IsPuddle ? "Puddle scene reflection" : "Water scene reflection", hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
                };
                target.Create();
            }
            // Lower resolution saves work without letting reflections lag behind a moving camera.
            Capture(view, selected.transform.position.y, target, selected.IsPuddle);
        }

        private static void Capture(Camera view, float level, RenderTexture target, bool puddle)
        {
            if (reflection == null)
            {
                var go = new GameObject("Water reflection camera") { hideFlags = HideFlags.HideAndDontSave };
                reflection = go.AddComponent<Camera>();
                reflection.enabled = false;
            }
            reflection.CopyFrom(view);
            reflection.enabled = false;
            reflection.cameraType = CameraType.Reflection;
            reflection.cullingMask = view.cullingMask & ~(1 << 4) & ~(1 << 5);
            reflection.useOcclusionCulling = false; // Main-camera occlusion is invalid for a mirrored view.
            reflection.allowMSAA = false;
            reflection.targetTexture = target;
            reflection.rect = new Rect(0, 0, 1, 1);
            var data = reflection.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.requiresDepthOption = CameraOverrideOption.Off;
            data.requiresColorOption = CameraOverrideOption.Off;
            var mirror = Matrix4x4.identity;
            mirror.m11 = -1f;
            mirror.m13 = 2f * level;
            reflection.transform.SetPositionAndRotation(mirror.MultiplyPoint(view.transform.position),
                Quaternion.LookRotation(mirror.MultiplyVector(view.transform.forward), mirror.MultiplyVector(view.transform.up)));
            reflection.worldToCameraMatrix = view.worldToCameraMatrix * mirror;
            // Discard everything below the water, including the submerged terrain.
            Vector4 plane = reflection.worldToCameraMatrix.inverse.transpose * new Vector4(0, 1, 0, -level - 0.04f);
            reflection.projectionMatrix = reflection.CalculateObliqueMatrix(plane);
            request.destination = target;
            if (!RenderPipeline.SupportsRenderRequest(reflection, request)) return;
            bool inverted = GL.invertCulling;
            try
            {
                GL.invertCulling = !inverted;
                RenderPipeline.SubmitRenderRequest(reflection, request);
            }
            finally { GL.invertCulling = inverted; }
            string prefix = puddle ? "_PuddleReflection" : "_WaterReflection";
            Shader.SetGlobalTexture(prefix, target);
            // URP's camera request already handles render-target orientation. Applying the target
            // flip here again mirrors sampling vertically and makes reflections slide with the view.
            Shader.SetGlobalMatrix(prefix + "VP", GL.GetGPUProjectionMatrix(reflection.projectionMatrix, false) * reflection.worldToCameraMatrix);
            Shader.SetGlobalFloat(prefix + "Height", level);
            Shader.SetGlobalFloat(prefix + "Valid", 1f);
        }

        private static void RestoreSource()
        {
            if (sourceData != null)
            {
                sourceData.requiresDepthOption = originalDepth;
                sourceData.requiresColorOption = originalColor;
            }
            sourceData = null;
            source = null;
        }

        private static void ReleaseTexture(ref RenderTexture target)
        {
            if (target == null) return;
            target.Release();
            Destroy(target);
            target = null;
        }

        private void OnDisable()
        {
            surfaces.Remove(this);
            if (surfaces.Count != 0) return;
            RestoreSource();
            Shader.SetGlobalFloat("_WaterReflectionValid", 0f);
            Shader.SetGlobalFloat("_PuddleReflectionValid", 0f);
            ReleaseTexture(ref texture);
            ReleaseTexture(ref puddleTexture);
            if (reflection != null) Destroy(reflection.gameObject);
            reflection = null;
            lastFrame = -1;
        }
    }
}
