using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Profiling;
using Unity.Profiling;
using PoeClone.Inventory;
using PoeClone.Visuals;
using PoeClone.World;

namespace PoeClone.UI
{
    [DefaultExecutionOrder(-10000)]
    public sealed class PerformanceDebugUI : MonoBehaviour
    {
        private readonly double[] frames = new double[180];
        private readonly float[] ages = new float[180];
        private readonly double[] sorted = new double[180];
        private readonly FrameTiming[] timings = new FrameTiming[1];
        private readonly StringBuilder report = new StringBuilder(1800);
        private int cursor, gcCount, gcChanges;
        private double lastTime, worstFrame, worstWork;
        private string worstStage = "unmeasured";
        private float nextReport;
        private Text text, toggleText;
        private RectTransform safeRoot, panel, resetButton;
        private CanvasScaler scaler;
        private bool visible;
        private WorldBuilder world;
        private ProfilerRecorder draws, triangles;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            var go = new GameObject("Performance debug");
            DontDestroyOnLoad(go);
            go.AddComponent<PerformanceDebugUI>();
        }

        private void Awake()
        {
            lastTime = Time.realtimeSinceStartupAsDouble;
            gcCount = GC.CollectionCount(0);
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            gameObject.AddComponent<GraphicRaycaster>();
            safeRoot = UiKit.NewRect("Safe area", transform);
            var button = UiKit.NewImage("Debug toggle", safeRoot, new Color(0.07f, 0.07f, 0.08f, 0.94f));
            var br = button.rectTransform;
            br.anchorMin = br.anchorMax = br.pivot = new Vector2(0.5f, 1f);
            br.anchoredPosition = new Vector2(0, -6);
            br.sizeDelta = new Vector2(120, 44);
            toggleText = UiKit.NewText("Label", br, "DEBUG", 20, UiKit.Gold, TextAnchor.MiddleCenter);
            UiKit.Stretch(toggleText.rectTransform, 0);
            button.raycastTarget = true;
            button.gameObject.AddComponent<Button>().onClick.AddListener(() =>
            {
                visible = !visible;
                panel.gameObject.SetActive(visible);
                resetButton.gameObject.SetActive(visible);
                toggleText.text = visible ? "CLOSE DEBUG" : "DEBUG";
                if (visible) StartCounters(); else StopCounters();
                nextReport = 0;
                UiKit.ClickConsumedFrame = Time.frameCount;
            });
            TouchMode.AddBlocker(br);
            TouchMode.AddMenuBlocker(br);
            var reset = UiKit.NewImage("Reset measurements", safeRoot, new Color(0.07f, 0.07f, 0.08f, 0.94f));
            resetButton = reset.rectTransform;
            resetButton.anchorMin = resetButton.anchorMax = resetButton.pivot = new Vector2(0.5f, 1f);
            resetButton.anchoredPosition = new Vector2(108, -6);
            resetButton.sizeDelta = new Vector2(86, 44);
            var resetLabel = UiKit.NewText("Label", resetButton, "RESET", 18, UiKit.Gold, TextAnchor.MiddleCenter);
            UiKit.Stretch(resetLabel.rectTransform, 0);
            reset.raycastTarget = true;
            reset.gameObject.AddComponent<Button>().onClick.AddListener(() =>
            {
                Array.Clear(frames, 0, frames.Length);
                worstFrame = worstWork = 0; worstStage = "unmeasured"; gcChanges = 0;
                PerformanceDiagnostics.ResetWork(); nextReport = 0;
                UiKit.ClickConsumedFrame = Time.frameCount;
            });
            TouchMode.AddBlocker(resetButton);
            TouchMode.AddMenuBlocker(resetButton);
            resetButton.gameObject.SetActive(false);
            var background = UiKit.NewImage("Performance readout", safeRoot, new Color(0.025f, 0.03f, 0.04f, 0.87f));
            panel = background.rectTransform;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 1f);
            panel.anchoredPosition = new Vector2(0, -54);
            panel.sizeDelta = new Vector2(490, 385);
            text = UiKit.NewText("Readout", panel, "Collecting performance data…", 18, Color.white, TextAnchor.UpperLeft);
            text.supportRichText = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 14;
            text.resizeTextMaxSize = 18;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            UiKit.Stretch(text.rectTransform, 10);
            // The panel and text deliberately have no raycasts or menu blockers. Gameplay continues.
            panel.gameObject.SetActive(false);
        }

        private void StartCounters()
        {
            draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1);
            triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", 1);
        }
        private void StopCounters() { draws.Dispose(); triangles.Dispose(); }
        private void OnDestroy() => StopCounters();

        private void Update()
        {
            PerformanceDiagnostics.NextFrame();
            double now = Time.realtimeSinceStartupAsDouble;
            double ms = (now - lastTime) * 1000d;
            lastTime = now;
            // Ignore background-tab suspension, but retain genuine long foreground stalls.
            if (Application.isFocused && ms > 0)
            {
                frames[cursor] = ms; ages[cursor] = Time.unscaledTime;
                cursor = (cursor + 1) % frames.Length;
                if (ms > worstFrame)
                {
                    worstFrame = ms; worstWork = PerformanceDiagnostics.PreviousWork;
                    worstStage = PerformanceDiagnostics.PreviousStage;
                }
            }
            int collections = GC.CollectionCount(0);
            gcChanges += Math.Max(0, collections - gcCount); gcCount = collections;
            if (Time.unscaledTime < nextReport) return;
            nextReport = Time.unscaledTime + 0.5f;
            scaler.referenceResolution = TouchMode.Active ? new Vector2(540, 540) : new Vector2(1100, 700);
            Rect safe = Screen.safeArea;
            safeRoot.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            safeRoot.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
            panel.sizeDelta = new Vector2(Mathf.Min(490, safeRoot.rect.width - 12), Mathf.Min(470, safeRoot.rect.height - 64));
            if (!visible) return;
            BuildReport();
            text.text = report.ToString();
        }

        private void BuildReport()
        {
            int count = 0, stalls = 0; double sum = 0;
            for (int i = 0; i < frames.Length; i++)
                if (frames[i] > 0 && Time.unscaledTime - ages[i] <= 5f)
                {
                    sorted[count++] = frames[i]; sum += frames[i];
                    if (frames[i] > 50) stalls++;
                }
            Array.Sort(sorted, 0, count);
            double mean = count > 0 ? sum / count : 0;
            report.Clear();
            report.AppendLine($"PERFORMANCE · {Screen.width}×{Screen.height} · last ≤5s");
            report.AppendLine($"FPS {(mean > 0 ? 1000 / mean : 0):0} | avg {mean:0.0} ms | p95 {(count > 0 ? sorted[Math.Min(count - 1, (int)(count * 0.95))] : 0):0.0} ms");
            report.AppendLine($"Peak {(count > 0 ? sorted[count - 1] : 0):0.0} ms | frames >50 ms: {stalls}");
            report.AppendLine($"Session worst {worstFrame:0} ms; build work {worstWork:0.0} ms");
            report.AppendLine($"Largest measured step: {worstStage}");
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].cpuFrameTime > 0)
                report.AppendLine($"CPU {timings[0].cpuFrameTime:0.0} ms | GPU {(timings[0].gpuFrameTime > 0 ? timings[0].gpuFrameTime.ToString("0.0") + " ms" : "unavailable")}");
            else report.AppendLine("CPU / GPU timings unavailable in this browser");
            report.AppendLine($"Draws {(draws.Valid ? draws.LastValue.ToString() : "n/a")} | triangles {(triangles.Valid ? triangles.LastValue.ToString() : "n/a")}");
            report.AppendLine($"Managed {GC.GetTotalMemory(false) / 1048576d:0} MB | Unity {Profiler.GetTotalAllocatedMemoryLong() / 1048576d:0} MB | GC +{gcChanges}");
            report.AppendLine($"Quality reductions: detail {AdaptiveQuality.DetailLevel}/5, render {AdaptiveQuality.RenderLevel}/5");
            if (world == null) world = FindAnyObjectByType<WorldBuilder>();
            report.AppendLine(world != null ? world.StreamingDebugStatus() : "World not loaded");
            PerformanceDiagnostics.AppendWork(report);
            report.AppendLine("Loading now: " + (LoadingStatus.Step.Length > 0 ? LoadingStatus.Step : "none"));
            report.AppendLine("Last: " + PerformanceDiagnostics.LastLoading);
            report.AppendLine("Slowest: " + PerformanceDiagnostics.SlowestLoading);
            report.Append("Work timers exclude waits; other frame cost is unmeasured.");
        }
    }
}
