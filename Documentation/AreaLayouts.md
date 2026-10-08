# Authoring fixed areas

The permanent layout source is `Assets/Scripts/World/AreaLayouts.cs`. Each area has one authored shape; it never rerolls when entering an area or starting another session. Decoration still uses fixed seeds, and enemy spawns retain their existing behavior.

Open **PoeClone > World > Area Layout Preview** in Edit mode to inspect each layout, check connected walkable space and gate/boss clearance, or export plan images to `Library/AreaLayoutPreviews`. This does not start gameplay or change the scene.

## Geometry helpers

Coordinates are local `(x, z)` metres relative to the area's center. The enclosing rectangle is `Size`, with independently chosen width and depth. Most campaign rectangles now cover approximately twice the previous 220 × 220 metre bounding area; the final encounter retains a smaller combat footprint with an uneven perimeter.

```csharp
new AreaShape(center, new Vector2(340, 284), cave: true)
    .Room(-100, -60, 28, 25) // elliptical chamber: center and two radii
    .Room(70, 50, 35, 30)
    .Route(14, -100, -60, -40, -60, -40, 10, 70, 50) // full corridor width and polyline
    .Route(12, -40, 10, -80, 70) // blind branch
    .Exclude(70, 50, 8, 7); // inaccessible elliptical region
```

Rooms and routes form a union; exclusions cut holes from it. Use overlapping rooms and routes to keep every required location reachable. End a route inside rock to make a dead end. Caves use 12–18 metre passages and larger chambers for gates and existing content. The current player's diameter is 1 metre, so the narrowest authored cave tunnel comfortably exceeds eight diameters. Avoid putting solid props in narrow corridors or cutting exclusions through the only connecting route.

`AreaShape` clips a two-metre triangulation to the outline and generates collision around every edge, including holes. Cliff areas extrude downward to an inaccessible lava backdrop; caves use raised stone banks and reduced daylight with local light sources. The backdrop has no collider. `Contains` supplies the same geometry to spawning, teleport destinations and minimap baking. `NearestOpen` finds a location with the requested clearance; use a large clearance for NPC compounds and bosses and smaller clearances for quest props.

## Content and area registration

Keep fixed gate and boss anchors in `AreaLayouts.GateLocal` and `BossLocal`, and existing quest anchors in `AreaLayouts.QuestAnchors`. `WorldBuilder.QuestSites.cs` builds those objects with their original IDs and quest logic. Place NPC camps and large props in chambers. Keep arrivals, waystones, bosses and quest sites clear of decoration.

To add another area, append a stable area ID, name, level, color and distant center to `WorldBuilder`; register normal travel areas in `WorldAreas`, add a layout case, build its themed scenery, connect its gates, and configure its spawner. Never renumber existing IDs: the final arena remains 5 and The Lost Hollows is 6. Leave at least the map's half-width plus border dressing between neighboring bounds. Add cave lighting bounds to `ConfigureCaveLighting` and the ToonLit shader if the new area is underground.

Run the preview's connectivity and placement check, refresh Unity assets, wait for compilation, and inspect the console. Gameplay checks remain a manual step unless requested; use the temporary `DevTest` session described in `AGENTS.md` when they are requested.
