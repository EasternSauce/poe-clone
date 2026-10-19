using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using PoeClone.UI;
using PoeClone.World;

namespace PoeClone.Visuals
{
    /// <summary>
    /// On touch devices, reduces CPU detail and GPU cost independently, with slow recovery and a
    /// 30 FPS floor target. Desktop (PC and desktop browsers) always keeps full quality.
    /// </summary>
    public sealed class AdaptiveQuality : MonoBehaviour
    {
        public static int DetailLevel { get; private set; }
        public static int RenderLevel { get; private set; }
        public static float CosmeticScale => DetailLevel == 0 ? 1f : DetailLevel == 1 ? 0.75f : DetailLevel == 2 ? 0.5f : DetailLevel == 3 ? 0.3f : DetailLevel == 4 ? 0.15f : 0f;
        public static int TextBudget => Mathf.Max(8, 40 - DetailLevel * 6);
        public static int LightBudget => DetailLevel == 0 ? 24 : DetailLevel == 1 ? 12 : DetailLevel == 2 ? 8 : DetailLevel == 3 ? 4 : 2;
        public static float CorpseScale => DetailLevel >= 4 ? 0.15f : DetailLevel >= 2 ? 0.4f : 1f;
        public static float FrameMilliseconds { get; private set; }
        public static float LastChangeGainMilliseconds { get; private set; }

        private readonly FrameTiming[] timings = new FrameTiming[1];
        private RenderPipelineAsset originalOverride;
        private UniversalRenderPipelineAsset original, runtime, runtimeSource;
        private UniversalRenderPipelineAsset lowCost;
        private Camera view;
        private UniversalAdditionalCameraData cameraData;
        private bool originalPost;
        private int originalTarget;
        private float windowSeconds, windowTotal, slowSeconds, fastSeconds, ignoreUntil;
        private int windowFrames, lastChanged;
        private float beforeChange;
        private bool assessing, preferRendering;
        private static int effects;
        private static int effectFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            DetailLevel = RenderLevel = effects = 0;
            FrameMilliseconds = 0;
            LastChangeGainMilliseconds = 0;
            effectFrame = -1;
        }

        private void Start()
        {
            originalTarget = Application.targetFrameRate;
            ApplyFrameCap();
            Inventory.TouchMode.Changed += ApplyFrameCap;
            originalOverride = QualitySettings.renderPipeline;
            original = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            ignoreUntil = Time.unscaledTime + 8f;
            if (Inventory.TouchMode.Active) ApplyRendering();
        }

        // Desktop is uncapped (browsers still pace to the display); touch devices cap at 60 for battery.
        private static void ApplyFrameCap() => Application.targetFrameRate = Inventory.TouchMode.Active ? 60 : -1;

        /// <summary>Only optional effects call this. Damage callbacks, warnings and projectiles bypass it.</summary>
        public static bool AllowEffect(Vector3 position)
        {
            if (!WorldActivity.NearView(position, 1.5f)) return false;
            if (effectFrame != Time.frameCount) { effectFrame = Time.frameCount; effects = 0; }
            int limit = DetailLevel == 0 ? 48 : DetailLevel == 1 ? 32 : DetailLevel == 2 ? 20 : DetailLevel == 3 ? 12 : DetailLevel == 4 ? 6 : 3;
            return effects++ < limit;
        }

        private void Update()
        {
            if (!Inventory.TouchMode.Active)
            {
                // Touch mode can be switched off at runtime (editor simulation); drop any reductions.
                if (runtime != null) { DetailLevel = RenderLevel = lastChanged = 0; assessing = false; RestoreRendering(); }
                ResetWindow();
                ignoreUntil = Time.unscaledTime + 3f;
                return;
            }
            var area = AreaManager.Instance;
            if (!Application.isFocused || Time.timeScale <= 0f || area == null || area.CurrentAreaIndex < 0 ||
                area.IsSwitching || (area.loadingScreen != null && area.loadingScreen.IsShowing) ||
                PatchNotesUI.IsShowing || LoadingStatus.Step.Length > 0)
            {
                ResetWindow();
                ignoreUntil = Time.unscaledTime + 3f;
                return;
            }
            if (Time.unscaledTime < ignoreUntil) return;
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;
            FrameTimingManager.CaptureFrameTimings();
            windowSeconds += dt;
            // Limit one stall's influence without ignoring devices that consistently run below 4 FPS.
            windowTotal += Mathf.Min(dt, 0.1f);
            windowFrames++;
            if (windowSeconds < 2f) return;
            float seconds = windowSeconds;
            FrameMilliseconds = windowTotal / windowFrames * 1000f;
            if (assessing)
            {
                LastChangeGainMilliseconds = beforeChange - FrameMilliseconds;
                // Without GPU timings, use the measured response to choose the next budget.
                bool helped = LastChangeGainMilliseconds > beforeChange * 0.08f;
                preferRendering = lastChanged == 2 ? helped : !helped;
                assessing = false;
            }
            windowSeconds = windowTotal = 0f;
            windowFrames = 0;
            slowSeconds = FrameMilliseconds > 25f ? slowSeconds + seconds : 0f;
            fastSeconds = FrameMilliseconds < 18.5f ? fastSeconds + seconds : 0f;
            if (slowSeconds >= 4f || FrameMilliseconds > 35f)
            {
                bool rendering = preferRendering;
                if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
                {
                    var timing = timings[0];
                    double cpuWork = System.Math.Max(timing.cpuMainThreadFrameTime - timing.cpuMainThreadPresentWaitTime, timing.cpuRenderThreadFrameTime);
                    if (timing.gpuFrameTime > 0 && cpuWork > 0) rendering = timing.gpuFrameTime > cpuWork * 1.1;
                }
                if (DetailLevel >= 5) rendering = true;
                if (rendering && RenderLevel < 5) { RenderLevel++; lastChanged = 2; }
                else if (DetailLevel < 5) { DetailLevel++; lastChanged = 1; }
                else return;
                Changed(true);
            }
            else if (fastSeconds >= 16f && (DetailLevel > 0 || RenderLevel > 0))
            {
                if (lastChanged == 2 && RenderLevel > 0) RenderLevel--;
                else if (DetailLevel > 0) DetailLevel--;
                else RenderLevel--;
                lastChanged = lastChanged == 1 ? 2 : 1;
                Changed(false);
            }
        }

        private void ResetWindow() { windowSeconds = windowTotal = slowSeconds = fastSeconds = 0f; windowFrames = 0; }
        private void Changed(bool reducing)
        {
            beforeChange = FrameMilliseconds;
            assessing = reducing;
            ResetWindow();
            ignoreUntil = Time.unscaledTime + 3f;
            ApplyRendering();
        }

        private void ApplyRendering()
        {
            if (original == null) return;
            UniversalRenderPipelineAsset source = original;
            if (RenderLevel >= 3)
            {
                // A Resources reference keeps this renderer available in PC builds that exclude Mobile quality.
                if (lowCost == null) lowCost = Resources.Load<UniversalRenderPipelineAsset>("Performance/LowCostPipeline");
                if (lowCost != null) source = lowCost;
            }
            if (runtime == null || runtimeSource != source)
            {
                var previous = runtime;
                runtime = Instantiate(source);
                runtime.name = "Adaptive quality (runtime only)";
                runtime.hideFlags = HideFlags.DontSave;
                runtimeSource = source;
                QualitySettings.renderPipeline = runtime;
                if (previous != null) Destroy(previous);
            }
            float scale = RenderLevel == 0 ? 1f : RenderLevel == 1 ? 0.9f : RenderLevel == 2 ? 0.8f : RenderLevel == 3 ? 0.7f : RenderLevel == 4 ? 0.6f : 0.55f;
            runtime.renderScale = Mathf.Min(original.renderScale, Mathf.Max(0.4f, original.renderScale * scale));
            runtime.shadowDistance = RenderLevel == 0 ? original.shadowDistance : RenderLevel == 1 ? Mathf.Min(32f, original.shadowDistance) : RenderLevel == 2 ? Mathf.Min(20f, original.shadowDistance) : 0f;
            // URP 17 exposes soft-shadow support with an internal setter. Change only our
            // runtime clone's serialized setting; saved pipeline assets remain untouched.
            JsonUtility.FromJsonOverwrite(original.supportsSoftShadows && RenderLevel == 0
                ? "{\"m_SoftShadowsSupported\":true}" : "{\"m_SoftShadowsSupported\":false}", runtime);
            runtime.shadowCascadeCount = RenderLevel == 0 ? original.shadowCascadeCount : 1;
            runtime.mainLightShadowmapResolution = RenderLevel == 0 ? original.mainLightShadowmapResolution : Mathf.Min(1024, original.mainLightShadowmapResolution);
            runtime.msaaSampleCount = RenderLevel >= 2 ? 1 : original.msaaSampleCount;
            runtime.supportsHDR = original.supportsHDR && RenderLevel < 2;
            ApplyCamera();
        }

        private void LateUpdate()
        {
            if (view != Camera.main) { RestoreCamera(); view = Camera.main; cameraData = null; ApplyCamera(); }
        }
        private void ApplyCamera()
        {
            if (view == null) view = Camera.main;
            if (view == null) return;
            if (cameraData == null && view.TryGetComponent<UniversalAdditionalCameraData>(out cameraData)) originalPost = cameraData.renderPostProcessing;
            if (cameraData != null) cameraData.renderPostProcessing = originalPost && RenderLevel < 2;
        }
        private void RestoreCamera() { if (cameraData != null) cameraData.renderPostProcessing = originalPost; }
        private void RestoreRendering()
        {
            RestoreCamera();
            if (runtime == null) return;
            if (QualitySettings.renderPipeline == runtime) QualitySettings.renderPipeline = originalOverride;
            Destroy(runtime);
            runtime = runtimeSource = null;
        }
        private void OnDestroy()
        {
            RestoreRendering();
            Inventory.TouchMode.Changed -= ApplyFrameCap;
            Application.targetFrameRate = originalTarget;
            DetailLevel = RenderLevel = 0;
        }
    }
}
