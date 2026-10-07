# Passive tree positions editor

Open **PoeClone > Passive Tree Positions Editor** in Unity. No scene setup is needed.

- Hover over any node to see its name, ID, branch, type, and stat bonuses, even with
  **Show names** disabled. Tooltips hide while dragging, panning, or box-selecting.
- Left-click and drag empty space to draw a selection rectangle. Nodes whose centers
  fall inside the rectangle are selected; dragging works in any direction.
- Ctrl-click (Cmd-click on macOS) a node to select or unselect it while keeping the
  other selected nodes. Ctrl-drag a rectangle to add nodes to the selection.
- Drag any selected node to move all selected nodes together. Clicking an unselected
  node without Ctrl selects only that node. Its connections follow it.
- Connections that cross, overlap, or touch unrelated connections turn bright red.
  Highlights update as nodes move; connections sharing a node are excluded.
- Drag with the middle or right mouse button to pan; scroll to zoom around the cursor.
- Use **Fit tree** or **F** to see the whole tree.
- **Space tree** runs an origin-first breadth-first spacing pass on the entire draft.
  Before locking each node, it pulls that node toward its already visited breadth-first
  parent if their connection exceeds four basic-node diameters (0.88 tree units),
  keeping the connection's direction. It then resolves overlaps with visited nodes
  before applying the usual push. The push can make the final connection longer;
  four diameters is a pull threshold, not a final length limit.
  It keeps the origin fixed, pushes unvisited nodes away, and locks each visited node
  for the rest of that pass. Normal minimum center spacing is three basic-node
  diameters (0.66 tree units). Terminal paths of up to five nodes and their attachment
  node use two diameters (0.44 units) only between members of that same path.
  Separate dead ends and unrelated nodes still use normal spacing. This uses the
  basic node size even for notables and keystones. If a push leaves connection
  crossings or connections passing through unrelated locked nodes, it tries three alternate
  directions and prefers fewer conflicts. This is a local guard, not a guarantee of
  zero overlaps or a full rearrangement of branches. Only connections between two
  locked nodes are fixed obstacles; candidate connections to locked neighbors are
  checked against them. Connections between unvisited nodes are ignored. Snapping is ignored
  during the pass to preserve minimum distances. Undo reverts the whole pass;
  **Save positions** commits it to the game.
- Search by node name or ID. Click a result to select and focus it.
- **Arrange from scratch** discards the draft's coordinates and builds a new layout
  from actual connections, starting with the origin at (0, 0). It advances in
  breadth-first order, gives branches space around the origin, and tries nearby
  placements that keep connections short and avoid locked connections. It uses the
  same three-diameter spacing, five-node dead-end exception, and temporary locks
  as **Space tree**. Loops and cross-links are retained; zero crossings are not
  guaranteed. Results are deterministic regardless of the previous coordinates.
  It prefers short connections but imposes no maximum connection length.
  The entire arrangement is one Undo step and is only saved with **Save positions**.
- Edit exact X/Y coordinates in the sidebar, or enable **Snap to grid**. With multiple
  nodes selected, editing the anchor coordinates moves the whole group. Group snapping
  snaps the dragged node and preserves the spacing between all selected nodes.
- Use **Undo / Redo**, or Unity's normal undo shortcuts.
- Click **Save positions** or press **Ctrl/Cmd+S** to save. Exit Play mode before saving.
- **Reload file** reads the saved layout and discards the draft after confirmation.

The saved file is `Assets/Resources/PassiveTreePositions.json`. It is a regular text
file, included in builds, and can also be edited externally. The editor refuses to
overwrite external edits made while a draft is open; reload the file first.
Unfinished edits remain in the window across script reloads, and closing a window
with changes offers Save/Discard/Cancel.

The JSON contains `version: 1` and a `nodes` array of `{ id, x, y }` entries. Keep
exactly one entry per passive node. Coordinates use tree units with positive Y up.
Stats, node IDs and connections are still defined in `PassiveTree.cs`; only final
positions come from this file. Add a position entry when adding a new passive.

Saving updates the cached graph for the next Play session, including projects with
domain reload disabled. An already open in-game tree should be checked in a new
Play session. Position editing permits overlaps and crossings; the existing
`PassiveTreeTests` check spacing and connection clearance for the saved layout.
