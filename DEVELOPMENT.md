# Development guide

This guide consolidates the weapon, passive-tree, fixed-area, equipment and Editor-arena notes. Historical implementation audits and copied item tables have been retired; current data lives in the source files linked below.

- [Project instructions and required checks](AGENTS.md)
- [Deployment workflow](DEPLOYMENT.md)
- [Balance testing and planned boss-DPS benchmark](BALANCE_TESTING.md)
- [Sound Library](tools/sound-library/README.md) and [Sound Board](tools/sound-board/README.md)
- [Equipment icon source prompts](Assets/Editor/ItemIconSources/EquipmentIconPrompts.md)

## C# ownership boundaries

- `InventoryUI` owns input, held items and panel visibility. `InventoryUI.Layout.cs` builds and lays out its uGUI views. `ItemTooltip` owns shared item text and tooltip sizing; the public `InventoryUI` tooltip methods forward to it for existing callers.
- `PlayerSkills` owns casting and gameplay coordination. `PlayerSkills.Loadout.cs` resolves equipped grants and binding eligibility; `SkillBindings` owns action-bar entries, moves and cooldown state. Binding reads return values, so callers cannot mutate stored entries. `PlayerSkills.Presentation.cs` contains descriptions and harmless replay effects. Keep potion binding changes and `Changed` notifications in the component, outside the binding store.
- `SpectatorReplica` owns the playback clock, scene puppets and discrete world events. `SpectatorMenuReplica` owns gear-message buffering, inventory, equipment, passives and action-bar mirroring. Only apply pending gear to its matching session ID. Preserve pending gear across reset because it may arrive before that session's first motion snapshot. Ground-item details return to the scene replica through a callback.

The extracted helpers are ordinary C# objects rather than new scene components. Existing component scripts and public entry points retain their Unity references. `ResponsibilityBoundaryTests` covers binding cooldown moves and replica session ordering without entering Play mode.

## Weapon skill specializations

The equipped main weapon supplies one main ability. Melee weapons use their family-specific pools; bows use bow attacks; staves use Fire Bolt, Ice Shard, Chain Lightning or Venom Spout; sceptres use one of the five summons. Grimoires provide support abilities and minion stats, without a main skill or weapon tree. Death Mark is an optional grimoire support modifier, not a main skill.

Movement, buff and curse abilities remain secondary abilities with fixed baselines. The specialization window and weapon XP apply to main weapon skills.

Press **T**, click the main attack icon, or click **Weapon tree** to open the current weapon's tree. Unequipping the weapon closes or switches the tree. Every main skill has 24 paid nodes and a free origin. Three connected trunks each split into two destinations; terminal routes require five or six connected points. All nodes cost one point. Selecting a node previews its exact effects; allocating is a separate button.

### Progression

- Progress belongs to the individual item, including XP and allocated nodes.
- A skill rank of 6 grants six total points, immediately available on a fresh drop.
- Combat XP awards also train the actively equipped main weapon. There is no passive time-based training and no training for bag, stash, inactive or secondary weapons.
- The XP needed for the next rank is `250 * current rank^2`. Excess XP carries over. Rank 10 is the cap.
- Rank itself never changes damage, mana cost, cooldown, duration, projectile count or minion stats. Only allocated nodes and ordinary character/equipment stats do that.
- XP progress is visible in the tree, without a second bar on the combat HUD.
- An Ice Shard weapon begins with one shard. Frozen Fan grants two additional shards after paying its connected travel path; it is not adjacent to the origin.

### Reset service

**Mentor Ilyra** stands beside Smith Bram in Haven. She resets the currently equipped main weapon for `75 + 50 * skill rank^2` gold. She shows the price before payment and asks the player to confirm. A successful reset refunds every allocated point and preserves rank and XP. Empty trees cannot be charged for a reset. The service checks that the player is still nearby, in town, wearing the same weapon, and has enough gold.

### Combat and persistence

Bow skills permanently replace left click. Minion main attacks fill empty summon slots; at the cap they command the existing army toward the aimed enemy without replacing healthy minions or charging a summoning cost. Changing the summoning weapon dismisses its army. Changing its allocation also dismisses old minions so their previous stat snapshots cannot be retained.

Skill-local effects are captured on projectiles and summoned minions. Forked volleys share their hit record so extra projectiles do not multiply damage against the same target. Secondary splash, shatter and explosion damage cannot recursively trigger itself. Poison/cloud kills retain their originating tree's kill effects.

Saves, stash items and multiplayer item messages carry the item's rank, XP and node IDs. Old weapons retain one compatible main ability, capped at rank 10, with points available to allocate. Old bow toggles are ignored. Removed stat IDs remain reserved for save compatibility. Character passives are refunded once at allocation version 3 because their skill mechanics have moved. Broad stats such as spell damage, minion life, attack speed and defences remain on gear and the character tree.

### Authoring

`WeaponSkillTrees.cs` contains the individual routes, notables and keystones. `WeaponSkillTree.cs` contains item progression, validation and immutable bonus snapshots. Node IDs are serialized; preserve them when changing text or balance. Incompatible tree changes should increment the progression version and provide an allocation migration.

Gauntlet builds define `WeaponNodes` allocation order and include it in their fingerprint. Generated weapons spend their actual rank budget. See the class gauntlet instructions in [AGENTS.md](AGENTS.md).

## Passive tree positions editor

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

## Authoring fixed areas

The permanent layout source is `Assets/Scripts/World/AreaLayouts.cs`. Each area has one authored shape; it never rerolls when entering an area or starting another session. Decoration still uses fixed seeds, and enemy spawns retain their existing behavior.

Open **PoeClone > World > Area Layout Preview** in Edit mode to inspect each layout, check connected walkable space and gate/boss clearance, or export plan images to `Library/AreaLayoutPreviews`. This does not start gameplay or change the scene.

### Geometry helpers

Coordinates are local `(x, z)` metres relative to the area's center. The enclosing rectangle is `Size`, with independently chosen width and depth. Use each layout's current dimensions in `AreaLayouts.cs` rather than historical map-size estimates.

```csharp
new AreaShape(center, new Vector2(340, 284), cave: true)
    .Room(-100, -60, 28, 25) // elliptical chamber: center and two radii
    .Room(70, 50, 35, 30)
    .Route(14, -100, -60, -40, -60, -40, 10, 70, 50) // full corridor width and polyline
    .Route(12, -40, 10, -80, 70) // blind branch
    .Exclude(70, 50, 8, 7); // inaccessible elliptical region
```

Rooms and routes form a union; exclusions cut holes from it. Use overlapping rooms and routes to keep every required location reachable. End a route inside rock to make a dead end. Caves use 12-18 metre passages and larger chambers for gates and existing content. The current player's diameter is 1 metre, so the narrowest authored cave tunnel comfortably exceeds eight diameters. Avoid putting solid props in narrow corridors or cutting exclusions through the only connecting route.

`AreaShape` clips a two-metre triangulation to the outline and generates collision around every edge, including holes. Cliff areas extrude downward to an inaccessible lava backdrop; caves use raised stone banks and reduced daylight with local light sources. The backdrop has no collider. `Contains` supplies the same geometry to spawning, teleport destinations and minimap baking. `NearestOpen` finds a location with the requested clearance; use a large clearance for NPC compounds and bosses and smaller clearances for quest props.

### Content and area registration

Area connections are listed in `AreaLayouts.Links`; the Lost Hollows are the crossroads (Greenwood, the dead-end Haunted Graveyard, and the Ashen Ruins behind the deep-road blockade). After Mortis, Tobias's `deep_road` quest stations guards there; defeat them and explicitly dismantle the blockade to unlock the gateway. The gateway itself stays locked even if a movement spell crosses the physical wall. Place gates in side passages rather than straight across from the entrance, so the way on has to be found. Keep fixed gate anchors (per destination) in `AreaLayouts.GateLocal`/`GateApproachLocal` and boss anchors in `BossLocal`, and existing quest anchors in `AreaLayouts.QuestAnchors`. `WorldBuilder.QuestSites.cs` builds those objects with their original IDs and quest logic. Place NPC camps and large props in chambers. Keep arrivals, waystones, bosses and quest sites clear of decoration.

To add another area, append a stable area ID, name, level, color and distant center to `WorldBuilder`; register normal travel areas in `WorldAreas`, add a layout case, build its themed scenery, add its gates to `Links`, and configure its spawner. Never renumber existing IDs: the final arena remains 5 and The Lost Hollows is 6. Leave at least the map's half-width plus border dressing between neighboring bounds. Add cave lighting bounds to `ConfigureCaveLighting` and the ToonLit shader if the new area is underground.

Run the preview's connectivity and placement check, refresh Unity assets, wait for compilation, and inspect the console. Gameplay checks remain a manual step unless requested; use the temporary `DevTest` session described in `AGENTS.md` when they are requested.

## Equipment data

Use `Assets/Scripts/Inventory/ItemGenerator.cs` as the base catalogue (`BaseIds`, `RequirementsOf` and `AttributeRequirement`), and `UniqueItems.cs` for unique definitions. Do not maintain a second item/requirement table here.

Attribute requirements are `round((10 + 2 * requiredLevel) * (1 + 0.5 * min(requiredLevel, 10) / 10))`: base levels 0, 7, 14 and 21 require 10, 32, 57 and 78 of their matching attribute. Hybrid armour requires STR and DEX. Neutral jewellery has no attribute requirement. Unique level requirements can exceed the underlying base's level. The item's own attributes count; unmet worn gear stays equipped but inactive. Use the game's requirement checks when generating test equipment.

## Temporary Editor combat arena

Public diagnostic URL switches are disabled. Published builds start the normal game. For an Editor diagnostic arena, use **PoeClone > Test > Minimal Combat Arena** or:

```csharp
return PoeClone.EditorTools.DevTest.QuickStartMinimal();
```

Mobile controls and individual systems can be configured with:

```csharp
return PoeClone.EditorTools.DevTest.QuickStartMinimal(
    touch: true, options: "features=0&skills=1");
```

Options include `weapon=short_bow`, `enemy=Raider`, `drops=0`, `quests=0`, `menus=0`, `inventory=1`, `character=1`, `passives=1`, `preview=0`, `audio=0`, and `guaranteedGear=0`. Without options, arena additions are enabled. These are local Editor preferences, not web URL features.

Follow [AGENTS.md](AGENTS.md): verify the session server on port 8099, start through DevTest, wait for `QuickStatus()` and call `Ready()` again to dismiss all startup overlays before inspection. Stop Play to restore preferences. The minimal arena does not load or save a character; its enemy respawns after three seconds. Run gameplay checks only when requested.

## Loading architecture

Areas are built on first demand under the loading cover and visited areas remain cached. Destination policy belongs to `SaveSystem.ResolveStartingArea`. Runtime SFX use deferred path catalogues; ambience loads per area, and music downloads track bundles on demand. `Resources.LoadAsync` defers runtime loading, not the initial WebGL data download.

Relevant source: `WorldBuilder`, `AreaManager`, `SoundBoardSettings`, `WorldAmbience` and `DeferredAudioCatalogs`. Profile current startup before planning further loading changes. The removed loading audit was a historical source review, not measured performance evidence or an active task list.
