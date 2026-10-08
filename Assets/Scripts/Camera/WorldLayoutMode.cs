using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using PoeClone.World;

namespace PoeClone.CameraSystem
{
    /// <summary>Inspect the generated world using the real game camera, with gameplay frozen.</summary>
    public sealed class WorldLayoutMode : MonoBehaviour
    {
        private Camera view;
        private Vector3 focus;
        private float zoom;
        private bool angled;
        private bool active;
        private Vector3 savedPosition;
        private Quaternion savedRotation;
        private bool savedOrthographic;
        private float savedSize, savedNear, savedFar, savedTimeScale;
        private bool savedFog, savedOcclusion;
        private readonly List<Behaviour> suspended = new List<Behaviour>();
        private readonly List<Canvas> hiddenCanvases = new List<Canvas>();
        private Rect toolbar;
        public bool IsActive => active;
        public Vector3 Focus => focus;

        public static WorldLayoutMode Open()
        {
            if (!Application.isPlaying || Camera.main == null || WorldBuilder.Instance == null)
                return null;
            var mode = Camera.main.GetComponent<WorldLayoutMode>();
            if (mode == null) mode = Camera.main.gameObject.AddComponent<WorldLayoutMode>();
            mode.Enter();
            return mode;
        }

        private void Enter()
        {
            if (active || WorldBuilder.Instance == null || Time.timeScale <= 0 || PoeClone.UI.PatchNotesUI.IsShowing) return;
            view = GetComponent<Camera>();
            savedPosition = transform.position;
            savedRotation = transform.rotation;
            savedOrthographic = view.orthographic;
            savedSize = view.orthographicSize;
            savedNear = view.nearClipPlane;
            savedFar = view.farClipPlane;
            savedOcclusion = view.useOcclusionCulling;
            savedTimeScale = Time.timeScale;
            savedFog = RenderSettings.fog;
            Suspend(GetComponent<CameraFollow>());
            // Disable gameplay input while keeping the world and all its renderers present.
            var player = FindAnyObjectByType<PoeClone.Player.PlayerController>();
            if (player != null)
                foreach (var behaviour in player.GetComponents<MonoBehaviour>()) Suspend(behaviour);
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                string ns = behaviour.GetType().Namespace ?? "";
                if (ns == "PoeClone.UI" || (ns == "PoeClone.Inventory" && behaviour.GetType().Name.EndsWith("UI")))
                    Suspend(behaviour);
            }
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (canvas.enabled && canvas.renderMode != RenderMode.WorldSpace)
                { hiddenCanvases.Add(canvas); canvas.enabled = false; }
            Time.timeScale = 0;
            RenderSettings.fog = false;
            view.orthographic = true;
            view.nearClipPlane = 0.1f;
            view.farClipPlane = 10000;
            view.useOcclusionCulling = false;
            active = true;
            FitWorld();
        }

        private void Suspend(Behaviour behaviour)
        {
            if (behaviour == null || !behaviour.enabled || suspended.Contains(behaviour)) return;
            suspended.Add(behaviour);
            behaviour.enabled = false;
        }

        private void Close()
        {
            if (!active) return;
            active = false;
            if (view != null)
            {
                view.orthographic = savedOrthographic;
                view.orthographicSize = savedSize;
                view.nearClipPlane = savedNear;
                view.farClipPlane = savedFar;
                view.useOcclusionCulling = savedOcclusion;
                transform.SetPositionAndRotation(savedPosition, savedRotation);
            }
            Time.timeScale = savedTimeScale;
            RenderSettings.fog = savedFog;
            foreach (var behaviour in suspended) if (behaviour != null) behaviour.enabled = true;
            foreach (var canvas in hiddenCanvases) if (canvas != null) canvas.enabled = true;
            suspended.Clear();
            hiddenCanvases.Clear();
        }

        private void OnDisable() => Close();

        private void QuitPreview()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void FocusArea(int area)
        {
            area = Mathf.Clamp(area, 0, WorldBuilder.AreaNames.Length - 1);
            var shape = AreaLayouts.Create(area, WorldBuilder.Center(area));
            Fit(new Bounds(shape.Center + Vector3.up * 5, new Vector3(shape.Size.x + 32, 35, shape.Size.y + 32)));
        }

        public void FitWorld()
        {
            var bounds = new Bounds(WorldBuilder.Center(0), Vector3.zero);
            for (int area = 0; area < WorldBuilder.AreaNames.Length; area++)
            {
                var shape = AreaLayouts.Create(area, WorldBuilder.Center(area));
                bounds.Encapsulate(new Bounds(shape.Center + Vector3.up * 5,
                    new Vector3(shape.Size.x + 32, 35, shape.Size.y + 32)));
            }
            Fit(bounds);
        }

        private Quaternion ViewRotation => angled ? Quaternion.Euler(55, 0, 0) : Quaternion.Euler(90, 0, 0);

        private void Fit(Bounds bounds)
        {
            focus = bounds.center;
            Vector3 e = bounds.extents;
            Quaternion inverse = Quaternion.Inverse(ViewRotation);
            float extentX = 0, extentY = 0;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = inverse * Vector3.Scale(e, new Vector3(x, y, z));
                        extentX = Mathf.Max(extentX, Mathf.Abs(corner.x));
                        extentY = Mathf.Max(extentY, Mathf.Abs(corner.y));
                    }
            zoom = Mathf.Clamp(Mathf.Max(extentY, extentX / view.aspect) * 1.15f, 4, 2500);
            ApplyCamera();
        }

        private void ApplyCamera()
        {
            view.orthographicSize = zoom;
            Quaternion rotation = ViewRotation;
            // Stay close enough for ordinary detail/shadow rendering when zoomed in.
            float distance = Mathf.Max(100, zoom * 3);
            transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
        }

        private bool GroundPoint(Vector2 screen, out Vector3 point)
        {
            Ray ray = view.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, new Vector3(0, focus.y, 0));
            if (plane.Raycast(ray, out float distance)) { point = ray.GetPoint(distance); return true; }
            point = focus;
            return false;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (!active) return;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) { QuitPreview(); return; }
                if (keyboard.fKey.wasPressedThisFrame) FitWorld();
                if (keyboard.tKey.wasPressedThisFrame) { angled = !angled; ApplyCamera(); }
                Vector3 movement = new Vector3(
                    (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) -
                    (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0), 0,
                    (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) -
                    (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0));
                focus += movement.normalized * zoom * Time.unscaledDeltaTime;
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 at = mouse.position.ReadValue();
                bool overToolbar = toolbar.Contains(new Vector2(at.x, Screen.height - at.y));
                if (!overToolbar)
                {
                    float scroll = mouse.scroll.ReadValue().y;
                    if (scroll != 0 && GroundPoint(at, out Vector3 before))
                    {
                        zoom = Mathf.Clamp(zoom * Mathf.Exp(-scroll * 0.0015f), 4, 2500);
                        ApplyCamera();
                        if (GroundPoint(at, out Vector3 after)) focus += before - after;
                    }
                    if (mouse.leftButton.isPressed || mouse.middleButton.isPressed || mouse.rightButton.isPressed)
                    {
                        if (GroundPoint(at - mouse.delta.ReadValue(), out Vector3 previous) && GroundPoint(at, out Vector3 current))
                            focus += previous - current;
                    }
                }
            }
            ApplyCamera();
        }

        private void OnGUI()
        {
            if (!active) return;
            for (int i = 0; i < WorldBuilder.AreaNames.Length; i++)
            {
                Vector3 origin = WorldBuilder.Center(i);
                Vector2 size = AreaLayouts.Create(i, origin).Size;
                Vector3 screen = view.WorldToScreenPoint(origin + new Vector3(0, 12, size.y * 0.5f));
                if (screen.z > 0 && screen.x >= 0 && screen.x <= Screen.width && screen.y >= 0 && screen.y <= Screen.height)
                    GUI.Box(new Rect(screen.x - 90, Screen.height - screen.y - 22, 180, 22), WorldBuilder.AreaNames[i]);
            }
            float width = Mathf.Min(Screen.width - 16, 940);
            int columns = Mathf.Max(1, Mathf.FloorToInt((width - 16) / 145));
            int rows = Mathf.CeilToInt(WorldBuilder.AreaNames.Length / (float)columns);
            toolbar = new Rect(8, 8, width, 98 + rows * 28);
            GUI.Box(toolbar, "Rendered World Layout Preview");
            GUILayout.BeginArea(new Rect(16, 34, width - 16, toolbar.height - 26));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Fit World (F)")) FitWorld();
            if (GUILayout.Button(angled ? "Top-down (T)" : "Angled view (T)")) { angled = !angled; ApplyCamera(); }
            if (GUILayout.Button("−")) { zoom = Mathf.Min(2500, zoom * 1.25f); ApplyCamera(); }
            if (GUILayout.Button("+")) { zoom = Mathf.Max(4, zoom / 1.25f); ApplyCamera(); }
            if (GUILayout.Button("Close preview (Esc)")) QuitPreview();
            GUILayout.EndHorizontal();
            int area = GUILayout.SelectionGrid(-1, WorldBuilder.AreaNames, columns, GUILayout.Height(rows * 28));
            if (area >= 0) FocusArea(area);
            GUILayout.Label("Scroll: zoom at cursor  |  Drag / WASD / arrows: pan  |  Area buttons: fit layout");
            GUILayout.EndArea();
        }
    }
}
