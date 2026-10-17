using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PoeClone.Visuals
{
    /// <summary>Shares one planar scene capture between visible, coplanar water chunks.</summary>
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
        private static RenderTexture texture;
        private static int lastFrame = -1, capturedFrame = -1;
        private static float capturedHeight;
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
            if (view == null || !view.isActiveAndEnabled) { RestoreSource(); return; }
            GeometryUtility.CalculateFrustumPlanes(view, frustum);
            ReflectiveWaterSurface nearest = null;
            bool visible = false;
            float distance = float.MaxValue;
            foreach (var candidate in surfaces)
            {
                if (!candidate.surface.enabled || (view.cullingMask & (1 << candidate.gameObject.layer)) == 0 ||
                    !GeometryUtility.TestPlanesAABB(frustum, candidate.surface.bounds)) continue;
                visible = true;
                if (!candidate.planar) continue;
                float d = candidate.surface.bounds.SqrDistance(view.transform.position);
                // Preserve the river/lake capture when both are visible. Small puddles can use
                // environment reflections; they must not make the larger water lose its reflection.
                bool preferred = nearest != null && nearest.IsPuddle && !candidate.IsPuddle;
                bool sameKind = nearest == null || nearest.IsPuddle == candidate.IsPuddle;
                if (preferred || sameKind && d < distance) { nearest = candidate; distance = d; }
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
            if (nearest == null) return;

            bool economical = Application.isMobilePlatform || Shader.IsKeywordEnabled("_MOBILE_LITE") || AdaptiveQuality.RenderLevel >= 2;
            int width = Mathf.Clamp(view.pixelWidth / (economical ? 4 : 2), 128, economical ? 384 : 768);
            int height = Mathf.Max(64, Mathf.RoundToInt(width / view.aspect));
            float level = nearest.transform.position.y;
            bool resized = texture == null || texture.width != width || texture.height != height;
            if (resized)
            {
                ReleaseTexture();
                texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "Water scene reflection", hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
                };
                texture.Create();
            }
            // Keep the captured projection with its texture when reducing update frequency.
            if (!resized && economical && Time.frameCount - capturedFrame < 2 && Mathf.Abs(level - capturedHeight) < 0.01f)
            {
                Shader.SetGlobalFloat("_WaterReflectionValid", 1f);
                return;
            }
            Capture(view, level);
        }

        private static void Capture(Camera view, float level)
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
            reflection.targetTexture = texture;
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
            request.destination = texture;
            if (!RenderPipeline.SupportsRenderRequest(reflection, request)) return;
            bool inverted = GL.invertCulling;
            try
            {
                GL.invertCulling = !inverted;
                RenderPipeline.SubmitRenderRequest(reflection, request);
            }
            finally { GL.invertCulling = inverted; }
            Shader.SetGlobalTexture("_WaterReflection", texture);
            Shader.SetGlobalMatrix("_WaterReflectionVP", GL.GetGPUProjectionMatrix(reflection.projectionMatrix, true) * reflection.worldToCameraMatrix);
            Shader.SetGlobalFloat("_WaterReflectionHeight", level);
            Shader.SetGlobalFloat("_WaterReflectionValid", 1f);
            capturedHeight = level;
            capturedFrame = Time.frameCount;
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
            capturedFrame = -1;
        }

        private static void ReleaseTexture()
        {
            if (texture == null) return;
            texture.Release();
            Destroy(texture);
            texture = null;
        }

        private void OnDisable()
        {
            surfaces.Remove(this);
            if (surfaces.Count != 0) return;
            RestoreSource();
            Shader.SetGlobalFloat("_WaterReflectionValid", 0f);
            ReleaseTexture();
            if (reflection != null) Destroy(reflection.gameObject);
            reflection = null;
            lastFrame = capturedFrame = -1;
        }
    }
}
