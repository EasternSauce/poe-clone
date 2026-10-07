using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PoeClone.Inventory;
using UnityEditor;
using UnityEngine;

namespace PoeClone.EditorTools
{
    // A separate undo target keeps view navigation and the last saved file out of undo history.
    internal sealed class PassiveTreePositionDraft : ScriptableObject
    {
        public PassiveTreePositions layout;
    }

    public sealed class PassiveTreePositionsEditor : EditorWindow
    {
        [SerializeField] private PassiveTreePositionDraft draft;
        [SerializeField] private string savedJson;
        [SerializeField] private string sourceSnapshot;
        [SerializeField] private string selected;
        [SerializeField] private List<string> selectedIds = new List<string>();
        [SerializeField] private float scale = 45f;
        [SerializeField] private Vector2 pan;
        [SerializeField] private bool snap;
        [SerializeField] private float grid = 0.1f;
        [SerializeField] private bool labels = true;
        private string search = "";
        private string error;
        private Vector2 listScroll;
        private bool framePending = true;
        private bool dragging;
        private bool panning;
        private bool boxSelecting;
        private Vector2 boxStart;
        private Vector2 boxEnd;
        private readonly List<string> boxInitialSelection = new List<string>();
        private readonly Dictionary<string, Vector2> dragPositions = new Dictionary<string, Vector2>();
        private Vector2 dragStartMouse;
        private Vector2 dragStartPosition;
        private int dragUndoGroup;
        private int control;
        private Vector2 hoverMouse;
        private bool mouseInWindow;

        [MenuItem("PoeClone/Passive Tree Positions Editor", priority = 31)]
        public static void Open()
        {
            var window = GetWindow<PassiveTreePositionsEditor>("Passive Positions");
            window.minSize = new Vector2(760, 480);
            window.Show();
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
            wantsMouseEnterLeaveWindow = true;
            saveChangesMessage = "Save passive node positions to the game's JSON layout?";
            Undo.undoRedoPerformed += OnUndo;
            if (draft == null) LoadFile();
            if (selectedIds.Count == 0 && selected != null && Find(selected) != null) selectedIds.Add(selected);
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndo;
            mouseInWindow = false;
        }

        private void OnDestroy()
        {
            if (draft != null) DestroyImmediate(draft);
        }

        private void OnUndo() { UpdateDirty(); Repaint(); }
        private void UpdateDirty()
        {
            hasUnsavedChanges = draft != null && JsonUtility.ToJson(draft.layout) != savedJson;
        }

        private void LoadFile()
        {
            try
            {
                string text = File.ReadAllText(PassiveTreePositions.AssetPath);
                var layout = PassiveTreePositions.Parse(text);
                layout.Validate(PassiveTree.Nodes);
                if (draft == null)
                {
                    draft = CreateInstance<PassiveTreePositionDraft>();
                    draft.hideFlags = HideFlags.HideAndDontSave;
                }
                Undo.ClearUndo(draft);
                draft.layout = layout;
                selectedIds.RemoveAll(id => Find(id) == null);
                selected = selectedIds.Contains(selected) ? selected : selectedIds.Count > 0 ? selectedIds[0] : null;
                savedJson = JsonUtility.ToJson(layout);
                sourceSnapshot = text;
                error = null;
                UpdateDirty();
            }
            catch (Exception ex) { error = ex.Message; }
        }

        public override void SaveChanges()
        {
            if (draft == null) return;
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Exit Play mode before saving positions.");
                draft.layout.Validate(PassiveTree.Nodes);
                if (File.ReadAllText(PassiveTreePositions.AssetPath) != sourceSnapshot)
                    throw new InvalidOperationException("The JSON changed outside this editor. Reload it before saving.");
                string json = JsonUtility.ToJson(draft.layout, true) + "\n";
                File.WriteAllText(PassiveTreePositions.AssetPath, json, new UTF8Encoding(false));
                sourceSnapshot = json;
                savedJson = JsonUtility.ToJson(draft.layout);
                // Also update the cached graph for the next Play session when domain reload is off.
                foreach (var entry in draft.layout.nodes)
                {
                    var node = PassiveTree.Get(entry.id);
                    node.X = entry.x;
                    node.Y = entry.y;
                }
                AssetDatabase.ImportAsset(PassiveTreePositions.AssetPath);
                error = null;
                base.SaveChanges();
                UpdateDirty();
                ShowNotification(new GUIContent("Positions saved"));
            }
            catch (Exception ex) { error = ex.Message; Repaint(); }
        }

        public override void DiscardChanges()
        {
            LoadFile();
            base.DiscardChanges();
        }

        private PassiveTreePositions.Entry Find(string id)
        {
            return draft.layout.nodes.Find(entry => entry.id == id);
        }

        private void OnGUI()
        {
            // Capture window coordinates before any IMGUI groups or controls handle the event.
            // Keep the last pointer input so repaint can draw the tooltip independently.
            Event input = Event.current;
            if (input.type == EventType.MouseLeaveWindow)
            {
                mouseInWindow = false;
                Repaint();
            }
            else if (input.isMouse || input.type == EventType.MouseEnterWindow || input.type == EventType.ScrollWheel)
            {
                hoverMouse = input.mousePosition;
                mouseInWindow = new Rect(Vector2.zero, position.size).Contains(hoverMouse);
                Repaint();
            }
            control = GUIUtility.GetControlID(FocusType.Passive);
            UpdateDirty();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(!hasUnsavedChanges || EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button("Save positions", EditorStyles.toolbarButton, GUILayout.Width(100))) SaveChanges();
                if (GUILayout.Button("Reload file", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    if (!hasUnsavedChanges || EditorUtility.DisplayDialog("Reload positions", "Discard unsaved position changes?", "Reload", "Cancel")) LoadFile();
                }
                if (GUILayout.Button("Fit tree", EditorStyles.toolbarButton, GUILayout.Width(65))) framePending = true;
                using (new EditorGUI.DisabledScope(draft == null || draft.layout == null || EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if (GUILayout.Button(new GUIContent("Space tree", "Pull connections to the visited parent down to 4 basic diameters before pushing: 3 basic diameters apart, or 2 within short dead ends."), EditorStyles.toolbarButton, GUILayout.Width(80))) SpaceTree();
                    if (GUILayout.Button(new GUIContent("Arrange from scratch", "Rebuild all positions from connections, ignoring the current layout. Supports Undo."), EditorStyles.toolbarButton, GUILayout.Width(135))) ArrangeTree();
                }
                if (GUILayout.Button("Undo", EditorStyles.toolbarButton, GUILayout.Width(45))) Undo.PerformUndo();
                if (GUILayout.Button("Redo", EditorStyles.toolbarButton, GUILayout.Width(45))) Undo.PerformRedo();
                GUILayout.FlexibleSpace();
                GUILayout.Label(hasUnsavedChanges ? "Unsaved changes" : "Saved", EditorStyles.miniLabel);
            }
            if (draft == null || draft.layout == null)
            {
                EditorGUILayout.HelpBox(error ?? "No layout loaded.", MessageType.Error);
                return;
            }
            Rect canvas = new Rect(0, 22, position.width - 260, position.height - 44);
            if (framePending && Event.current.type == EventType.Repaint) { Fit(canvas); framePending = false; }
            HandleInput(canvas);
            DrawTree(canvas);
            DrawSidebar(new Rect(canvas.xMax + 10, 28, 240, position.height - 55));
            GUI.Label(new Rect(8, position.height - 20, position.width - 16, 20),
                "Empty drag: box select  |  Ctrl-click: toggle  |  Node drag: move selection  |  Right/middle drag: pan  |  Scroll: zoom", EditorStyles.miniLabel);
            DrawNodeTooltip(canvas);
        }

        private void SpaceTree()
        {
            ApplyAutomaticLayout(false);
        }

        private void ArrangeTree()
        {
            ApplyAutomaticLayout(true);
        }

        private void ApplyAutomaticLayout(bool fromScratch)
        {
            try
            {
                var next = PassiveTreePositions.Parse(JsonUtility.ToJson(draft.layout));
                int moved = fromScratch ? PassiveTreeSpacing.ArrangeFromScratch(next, PassiveTree.Nodes)
                    : PassiveTreeSpacing.Apply(next, PassiveTree.Nodes);
                if (moved > 0)
                {
                    Undo.IncrementCurrentGroup();
                    Undo.RecordObject(draft, fromScratch ? "Arrange passive tree from scratch" : "Space entire passive tree");
                    draft.layout = next;
                    Undo.FlushUndoRecordObjects();
                    Undo.IncrementCurrentGroup();
                }
                error = null;
                UpdateDirty();
                framePending = true;
                ShowNotification(new GUIContent((fromScratch ? "Arranged tree: " : "Spaced tree: ") + moved + " nodes moved"));
                Repaint();
            }
            catch (Exception ex) { error = ex.Message; Repaint(); }
        }

        private Vector2 ScreenPoint(PassiveTreePositions.Entry entry, Rect canvas)
        {
            return canvas.size * 0.5f + pan + new Vector2(entry.x, -entry.y) * scale;
        }

        private void Fit(Rect canvas)
        {
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
            foreach (var entry in draft.layout.nodes)
            {
                var p = new Vector2(entry.x, -entry.y);
                min = Vector2.Min(min, p); max = Vector2.Max(max, p);
            }
            Vector2 size = max - min + Vector2.one * 1.4f;
            scale = Mathf.Clamp(Mathf.Min(canvas.width / size.x, canvas.height / size.y), 12, 260);
            pan = -(min + max) * 0.5f * scale;
        }

        private float Radius(PassiveNode node)
        {
            return (node.Keystone ? 0.212f : node.Notable ? 0.155f : node.Id == PassiveTree.OriginId ? 0.14f : 0.11f) * scale;
        }

        private bool Matches(PassiveNode node)
        {
            return string.IsNullOrEmpty(search) || node.Id.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                node.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Color BranchColor(PassiveBranch branch)
        {
            switch (branch)
            {
                case PassiveBranch.Might: return new Color(0.9f, 0.35f, 0.3f);
                case PassiveBranch.Grace: return new Color(0.35f, 0.8f, 0.45f);
                case PassiveBranch.Wisdom: return new Color(0.35f, 0.6f, 1f);
                case PassiveBranch.Fury: return new Color(1f, 0.65f, 0.25f);
                case PassiveBranch.Storm: return new Color(0.35f, 0.85f, 0.9f);
                case PassiveBranch.Zeal: return new Color(0.9f, 0.45f, 0.8f);
                case PassiveBranch.Necromancy: return new Color(0.7f, 0.6f, 0.95f);
                default: return new Color(1f, 0.85f, 0.4f);
            }
        }

        private struct Connection
        {
            public PassiveTreePositions.Entry from, to;
            public Vector2 a, b;
            public bool colliding;
        }

        private List<Connection> FindConnectionCollisions()
        {
            var entries = new Dictionary<string, PassiveTreePositions.Entry>();
            foreach (var entry in draft.layout.nodes) entries.Add(entry.id, entry);
            var connections = new List<Connection>();
            foreach (var entry in draft.layout.nodes)
                foreach (string link in PassiveTree.Get(entry.id).Links)
                {
                    if (string.CompareOrdinal(entry.id, link) >= 0) continue;
                    var end = entries[link];
                    connections.Add(new Connection
                    {
                        from = entry, to = end,
                        a = new Vector2(entry.x, entry.y), b = new Vector2(end.x, end.y)
                    });
                }

            for (int i = 0; i < connections.Count; i++)
                for (int j = i + 1; j < connections.Count; j++)
                {
                    var first = connections[i];
                    var second = connections[j];
                    // Meeting at the same graph node is an intentional connection.
                    if (first.from.id == second.from.id || first.from.id == second.to.id ||
                        first.to.id == second.from.id || first.to.id == second.to.id) continue;
                    if (!SegmentsIntersect(first.a, first.b, second.a, second.b)) continue;
                    first.colliding = second.colliding = true;
                    connections[i] = first;
                    connections[j] = second;
                }
            return connections;
        }

        private static double Cross(Vector2 a, Vector2 b, Vector2 c)
        {
            return ((double)b.x - a.x) * ((double)c.y - a.y) -
                ((double)b.y - a.y) * ((double)c.x - a.x);
        }

        private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            // Inclusive bounds also detect collinear overlaps and unrelated endpoint touches.
            if (Mathf.Max(a.x, b.x) < Mathf.Min(c.x, d.x) ||
                Mathf.Max(c.x, d.x) < Mathf.Min(a.x, b.x) ||
                Mathf.Max(a.y, b.y) < Mathf.Min(c.y, d.y) ||
                Mathf.Max(c.y, d.y) < Mathf.Min(a.y, b.y)) return false;
            double abC = Cross(a, b, c), abD = Cross(a, b, d);
            double cdA = Cross(c, d, a), cdB = Cross(c, d, b);
            return ((abC <= 0 && abD >= 0) || (abC >= 0 && abD <= 0)) &&
                ((cdA <= 0 && cdB >= 0) || (cdA >= 0 && cdB <= 0));
        }

        private void DrawTree(Rect canvas)
        {
            EditorGUI.DrawRect(canvas, new Color(0.065f, 0.075f, 0.09f));
            GUI.BeginGroup(canvas);
            Handles.BeginGUI();
            if (Event.current.type == EventType.Repaint)
            {
                // Major grid lines mark one tree unit; snapping can use finer increments.
                Vector2 origin = canvas.size * 0.5f + pan;
                Handles.color = new Color(1, 1, 1, 0.06f);
                for (float x = Mathf.Repeat(origin.x, scale); x < canvas.width; x += scale)
                    Handles.DrawLine(new Vector3(x, 0), new Vector3(x, canvas.height));
                for (float y = Mathf.Repeat(origin.y, scale); y < canvas.height; y += scale)
                    Handles.DrawLine(new Vector3(0, y), new Vector3(canvas.width, y));
                var connections = FindConnectionCollisions();
                foreach (var connection in connections)
                {
                    if (connection.colliding) continue;
                    Handles.color = selectedIds.Contains(connection.from.id) || selectedIds.Contains(connection.to.id)
                        ? new Color(1, 0.85f, 0.4f) : new Color(0.4f, 0.43f, 0.5f);
                    Handles.DrawAAPolyLine(2, ScreenPoint(connection.from, canvas), ScreenPoint(connection.to, canvas));
                }
                // Draw conflicts last so selection and other connections cannot hide the warning.
                Handles.color = Color.red;
                foreach (var connection in connections)
                    if (connection.colliding)
                        Handles.DrawAAPolyLine(4, ScreenPoint(connection.from, canvas), ScreenPoint(connection.to, canvas));
                foreach (var entry in draft.layout.nodes)
                {
                    var node = PassiveTree.Get(entry.id);
                    Vector2 p = ScreenPoint(entry, canvas);
                    float radius = Radius(node);
                    Color color = BranchColor(node.Branch);
                    if (!Matches(node)) color *= 0.3f;
                    Handles.color = color;
                    if (node.Keystone)
                        Handles.DrawAAConvexPolygon(p + Vector2.up * radius, p + Vector2.right * radius, p + Vector2.down * radius, p + Vector2.left * radius);
                    else Handles.DrawSolidDisc(p, Vector3.forward, radius);
                    bool isSelected = selectedIds.Contains(node.Id);
                    Handles.color = isSelected ? Color.white : new Color(0, 0, 0, 0.6f);
                    Handles.DrawWireDisc(p, Vector3.forward, radius + (isSelected ? 3 : 0));
                }
            }
            Handles.EndGUI();
            if (labels)
                foreach (var entry in draft.layout.nodes)
                {
                    var node = PassiveTree.Get(entry.id);
                    if ((!node.Notable && !selectedIds.Contains(node.Id)) || !Matches(node)) continue;
                    Vector2 p = ScreenPoint(entry, canvas);
                    var style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.UpperCenter };
                    style.normal.textColor = selectedIds.Contains(node.Id) ? Color.white : new Color(0.8f, 0.82f, 0.86f);
                    GUI.Label(new Rect(p.x - 90, p.y + Radius(node) + 3, 180, 20), node.Name, style);
                }
            if (boxSelecting)
            {
                Rect box = SelectionRect(boxStart, boxEnd);
                EditorGUI.DrawRect(box, new Color(0.35f, 0.65f, 1f, 0.15f));
                GUI.Box(box, GUIContent.none);
            }
            GUI.EndGroup();
        }

        private static Rect SelectionRect(Vector2 a, Vector2 b)
        {
            Vector2 min = Vector2.Min(a, b), max = Vector2.Max(a, b);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private string HitNode(Vector2 mouse, Rect canvas)
        {
            string hit = null;
            float best = float.MaxValue;
            foreach (var entry in draft.layout.nodes)
            {
                float distance = Vector2.Distance(mouse, ScreenPoint(entry, canvas));
                if (distance <= Mathf.Max(8, Radius(PassiveTree.Get(entry.id))) && distance < best)
                {
                    hit = entry.id;
                    best = distance;
                }
            }
            return hit;
        }

        private void DrawNodeTooltip(Rect canvas)
        {
            if (Event.current.type != EventType.Repaint || !mouseInWindow ||
                dragging || panning || boxSelecting || GUIUtility.hotControl != 0) return;
            Vector2 mouse = hoverMouse;
            if (!canvas.Contains(mouse)) return;
            var node = PassiveTree.Get(HitNode(mouse - canvas.position, canvas));
            if (node == null) return;

            var text = new StringBuilder();
            text.Append(node.Name).Append('\n').Append(node.Id).Append("  /  ").Append(node.Branch);
            text.Append("  /  ").Append(node.Keystone ? "Keystone" : node.Notable ? "Notable" :
                node.Id == PassiveTree.OriginId ? "Origin" : "Basic");
            if (node.Mods.Length == 0) text.Append("\nWhere every path starts.");
            foreach (var mod in node.Mods) text.Append('\n').Append(StatFormatter.ItemLine(mod));

            var content = new GUIContent(text.ToString());
            var style = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 12,
                wordWrap = true,
                richText = false,
                padding = new RectOffset(10, 10, 8, 8)
            };
            style.normal.textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black;
            float width = Mathf.Min(340, position.width - 16);
            float height = style.CalcHeight(content, width);
            float x = mouse.x + 16;
            float y = mouse.y + 20;
            if (x + width > position.width - 8) x = mouse.x - width - 16;
            if (y + height > position.height - 8) y = mouse.y - height - 12;
            var rect = new Rect(Mathf.Clamp(x, 8, position.width - width - 8),
                Mathf.Clamp(y, 8, Mathf.Max(8, position.height - height - 8)), width, height);
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.12f, 0.12f, 0.12f) :
                new Color(0.96f, 0.96f, 0.96f));
            GUI.Label(rect, content, style);
        }

        private void SelectNode(string id, bool toggle)
        {
            if (toggle)
            {
                if (selectedIds.Contains(id)) selectedIds.Remove(id);
                else selectedIds.Add(id);
            }
            else if (!selectedIds.Contains(id))
            {
                selectedIds.Clear();
                selectedIds.Add(id);
            }
            selected = selectedIds.Contains(id) ? id : selectedIds.Count > 0 ? selectedIds[0] : null;
        }

        private void SelectInRect(Rect box, Rect canvas)
        {
            selectedIds.Clear();
            selectedIds.AddRange(boxInitialSelection);
            foreach (var entry in draft.layout.nodes)
            {
                Vector2 point = ScreenPoint(entry, canvas);
                if (point.x >= box.xMin && point.x <= box.xMax && point.y >= box.yMin && point.y <= box.yMax &&
                    !selectedIds.Contains(entry.id)) selectedIds.Add(entry.id);
            }
            selected = selectedIds.Count > 0 ? selectedIds[0] : null;
        }

        private void BeginNodeDrag(Vector2 mouse)
        {
            dragPositions.Clear();
            foreach (string id in selectedIds)
            {
                var entry = Find(id);
                dragPositions.Add(id, new Vector2(entry.x, entry.y));
            }
            dragStartPosition = dragPositions[selected];
            dragStartMouse = mouse;
            Undo.IncrementCurrentGroup();
            dragUndoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Move passive selection");
            dragging = true;
        }

        private void DragSelection(Vector2 mouse)
        {
            Vector2 screenDelta = (mouse - dragStartMouse) / scale;
            Vector2 delta = new Vector2(screenDelta.x, -screenDelta.y);
            if (snap)
            {
                Vector2 anchor = dragStartPosition + delta;
                delta = new Vector2(Mathf.Round(anchor.x / grid) * grid,
                    Mathf.Round(anchor.y / grid) * grid) - dragStartPosition;
            }
            // One shared delta preserves all spacing, including with grid snapping enabled.
            ApplySelectionOffset(dragPositions, delta);
        }

        private void ApplySelectionOffset(Dictionary<string, Vector2> starts, Vector2 delta)
        {
            if (float.IsNaN(delta.x) || float.IsInfinity(delta.x) || float.IsNaN(delta.y) || float.IsInfinity(delta.y)) return;
            bool changed = false;
            foreach (var pair in starts)
            {
                var entry = Find(pair.Key);
                Vector2 next = pair.Value + delta;
                if (float.IsNaN(next.x) || float.IsInfinity(next.x) || float.IsNaN(next.y) || float.IsInfinity(next.y)) return;
                changed |= entry.x != next.x || entry.y != next.y;
            }
            if (!changed) return;
            Undo.RecordObject(draft, "Move passive selection");
            foreach (var pair in starts)
            {
                var entry = Find(pair.Key);
                Vector2 next = pair.Value + delta;
                entry.x = next.x; entry.y = next.y;
            }
            UpdateDirty();
        }

        private void HandleInput(Rect canvas)
        {
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.S && (e.control || e.command)) { SaveChanges(); e.Use(); }
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.F && !EditorGUIUtility.editingTextField) { framePending = true; e.Use(); }
            Vector2 mouse = e.mousePosition - canvas.position;
            if (!dragging && !boxSelecting && canvas.Contains(e.mousePosition) && e.type == EventType.ScrollWheel)
            {
                Vector2 anchor = mouse - canvas.size * 0.5f;
                float next = Mathf.Clamp(scale * Mathf.Pow(1.12f, -e.delta.y), 12, 260);
                pan = anchor - (anchor - pan) * (next / scale);
                scale = next; e.Use(); Repaint();
            }
            if (canvas.Contains(e.mousePosition) && e.type == EventType.MouseDown)
            {
                GUI.FocusControl(null);
                if (e.button == 0)
                {
                    string hit = HitNode(mouse, canvas);
                    bool toggle = e.control || e.command;
                    if (hit != null)
                    {
                        SelectNode(hit, toggle);
                        // Ctrl-click only toggles membership; a plain drag moves the group.
                        if (!toggle) BeginNodeDrag(mouse);
                    }
                    else
                    {
                        boxInitialSelection.Clear();
                        if (toggle) boxInitialSelection.AddRange(selectedIds);
                        else { selectedIds.Clear(); selected = null; }
                        boxStart = boxEnd = mouse;
                        boxSelecting = true;
                    }
                }
                else panning = e.button == 1 || e.button == 2;
                if (dragging || panning || boxSelecting) GUIUtility.hotControl = control;
                e.Use(); Repaint();
            }
            if (GUIUtility.hotControl == control && e.type == EventType.MouseDrag)
            {
                if (panning) pan += e.delta;
                if (dragging) DragSelection(mouse);
                if (boxSelecting)
                {
                    boxEnd = mouse;
                    SelectInRect(SelectionRect(boxStart, boxEnd), canvas);
                }
                e.Use(); Repaint();
            }
            if (GUIUtility.hotControl == control && (e.type == EventType.MouseUp || e.type == EventType.Ignore))
            {
                if (dragging) Undo.CollapseUndoOperations(dragUndoGroup);
                if (boxSelecting && e.type == EventType.MouseUp)
                {
                    boxEnd = mouse;
                    // A background click clears selection but should not select a zero-area box.
                    if ((boxEnd - boxStart).sqrMagnitude > 4f) SelectInRect(SelectionRect(boxStart, boxEnd), canvas);
                }
                dragging = panning = boxSelecting = false;
                dragPositions.Clear();
                GUIUtility.hotControl = 0;
                if (e.type == EventType.MouseUp) e.Use();
            }
        }

        private void MoveSelected(Vector2 next)
        {
            if (float.IsNaN(next.x) || float.IsInfinity(next.x) || float.IsNaN(next.y) || float.IsInfinity(next.y)) return;
            var entry = Find(selected);
            var starts = new Dictionary<string, Vector2>();
            foreach (string id in selectedIds)
            {
                var item = Find(id);
                starts.Add(id, new Vector2(item.x, item.y));
            }
            ApplySelectionOffset(starts, next - new Vector2(entry.x, entry.y));
        }

        private void DrawSidebar(Rect rect)
        {
            GUILayout.BeginArea(rect);
            GUILayout.Label("PASSIVE TREE LAYOUT", EditorStyles.boldLabel);
            GUILayout.Label(draft.layout.nodes.Count + " nodes", EditorStyles.miniLabel);
            GUILayout.Label(selectedIds.Count + " selected", EditorStyles.boldLabel);
            if (selectedIds.Count > 1) GUILayout.Label("Drag any selected node to move the group.", EditorStyles.wordWrappedMiniLabel);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                EditorGUILayout.HelpBox("Exit Play mode to save. The game uses the saved layout.", MessageType.Info);
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            labels = EditorGUILayout.Toggle("Show names", labels);
            snap = EditorGUILayout.Toggle("Snap to grid", snap);
            float nextGrid = EditorGUILayout.FloatField("Grid spacing", grid);
            if (!float.IsNaN(nextGrid) && !float.IsInfinity(nextGrid)) grid = Mathf.Clamp(nextGrid, 0.01f, 10);
            GUILayout.Space(8);
            if (selected != null && Find(selected) != null)
            {
                var node = PassiveTree.Get(selected);
                var entry = Find(selected);
                GUILayout.Label(node.Name, EditorStyles.boldLabel);
                GUILayout.Label(node.Id + "  /  " + node.Branch, EditorStyles.wordWrappedMiniLabel);
                EditorGUI.BeginChangeCheck();
                Vector2 next = EditorGUILayout.Vector2Field(selectedIds.Count > 1 ? "Anchor position (moves group)" : "Position (tree units)", new Vector2(entry.x, entry.y));
                if (EditorGUI.EndChangeCheck()) MoveSelected(next);
                if (GUILayout.Button("Focus selected")) pan = -new Vector2(entry.x, -entry.y) * scale;
                GUILayout.Label("Connected to: " + string.Join(", ", node.Links), EditorStyles.wordWrappedMiniLabel);
            }
            else EditorGUILayout.HelpBox("Drag empty space to box-select nodes. Ctrl-click a node to add or remove it.", MessageType.Info);
            GUILayout.Space(8);
            search = EditorGUILayout.TextField("Search", search);
            listScroll = EditorGUILayout.BeginScrollView(listScroll);
            foreach (var entry in draft.layout.nodes)
            {
                var node = PassiveTree.Get(entry.id);
                if (!Matches(node)) continue;
                if (GUILayout.Button(node.Name + "  [" + node.Id + "]", selectedIds.Contains(node.Id) ? EditorStyles.miniButton : EditorStyles.label))
                {
                    SelectNode(node.Id, Event.current.control || Event.current.command);
                    pan = -new Vector2(entry.x, -entry.y) * scale;
                    Repaint();
                }
            }
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
