# Loading audit

Scope: runtime work after character selection. These are source findings, not measured timings. No gameplay or performance run was performed. Unity script compilation and editor validation of all 179 runtime audio references passed.

## Implemented

- `WorldBuilder`: startup builds Haven. Other areas build terrain, scenery, quest sites, NPCs, spawners and boss lairs on first demand. Cheap travel anchors exist in advance so saves and gates do not depend on constructed destination scenery. Visited areas remain cached; this preserves their state but memory still grows as exploration continues.
- `AreaManager`: destination construction runs under the existing loading cover. Removed the simulated load pause.
- Optional wind decoration and wildlife may finish after the loading cover when construction has used five seconds. This is a **soft budget**, not a five-second guarantee. Terrain, collision, quest barriers and combat readiness must remain mandatory. Existing actors and scenery are already procedural low-poly objects; this change does not introduce a separate placeholder/high-detail model system.
- `SoundBoardSettings`: runtime uses a path-only catalog rather than loading the authoring asset with every clip dependency. Individual effect groups request their recordings asynchronously when used; unavailable cues remain silent. Creature fallback sounds use the same asynchronous cache.
- `WorldAmbience`: loads only recordings relevant to a requested area, in the background. It does not hold the loading cover.
- `DeferredAudioCatalogs`: regenerates catalogs before builds, copies non-Resources recordings (including temporary inspiration sources) to permanent Resources assets, and disables audio-data preloading for deferred clips. Authoring libraries remain available to the sound-board tools and now reference the same permanent recordings, avoiding duplicate original/copy dependencies.

## Next candidates

| Priority | Startup work | Deferred trigger | Tradeoff / prerequisite |
| --- | --- | --- | --- |
| High | `InventoryUI.Start`: constructs the inventory/stash UI and invisibly opens it in `Prewarm` | First inventory/stash open, or small idle batches after entry | Show a simple panel immediately; guard input until slots and event subscriptions are ready. Avoid moving prewarm to a single large frame during combat. |
| High | `CharacterPreview.Build`, called by inventory startup: clones equipment/model, camera, lights and a 512 x 768 render texture | First visible character preview | Use a flat portrait/empty preview frame until ready. Destroy or park its camera when hidden. Character-selection previews are needed earlier and should be handled separately. |
| High | `PassiveTreeUI.Awake`: `Build()` constructs a hidden tree; `CreateGameplaySystems` adds it during startup | First passive-tree open | Keep passive calculations and saved allocations available immediately; defer only visual nodes, lines and layout. |
| High (web download) | Main WebGL data file still bundles Resources, including authoring-library dependencies and deferred audio copies | Download per area / recording group | Extend the existing music-bundle pipeline to SFX and area assets, with versioned caches, build-time dependency validation and missing-download handling. `Resources.LoadAsync` defers runtime loading, **not** the initial WebGL data download. Retire authoring-library dependencies from player builds after their consumers have migrated. |
| Medium | `AreaKit.Load`: shared ScriptableObject holds all prop prefab and material references | First area requiring each theme | Split common props from per-theme kits or use path-only catalogs. Keep collision/material fallback assets resident. |
| Medium | Original Greenwood objects live in `Game.unity`, so the scene loads them even before Greenwood is built | First Greenwood visit | Move the original forest into an additive area scene or a streamed prefab; preserve deterministic relocation and authored references. |
| Medium | Broad scene scans for ambient details, lighting and quest clearance | Once per newly built area | Track new area objects directly; avoid repeatedly scanning previously visited scenery. Do not defer scenery that changes collision into active combat. |
| Medium | Visited areas and decoded audio remain cached | Evict areas distant from both players, unload unused sound data | Persist enemy, quest, loot and respawn state first. Co-op authority handovers, return portals and spectators must retain required areas. |
| Medium | Spectator first demand currently builds a missing area synchronously for snapshot consistency | Asynchronous area request while showing loading cover | Buffer events and interpolation until the area's replica roots are ready. Avoid applying poses into absent terrain. |
| Low / inspect first | `CreateGameplaySystems`: spectator, chat, dialogue, minimap, boss UI components | First relevant use | Profile construction before adding complexity. Movement, health, skills and touch controls must be ready for gameplay. Network state itself may be required immediately. |

## Already deferred

- Music downloads a track bundle on first play (`MusicPlayer`), with cache reuse.
- Minimap terrain prepares per visited area, behind the area loading cover.
- Item icon providers and shared UI sprites use caches and construct/load individual assets when requested.
- Ordinary enemy first waves previously waited for area activation; their terrain and spawner construction now also wait for first demand.

## Five-second fallback follow-up

A hard five-second gameplay start needs a deliberate two-stage area format: inexpensive collision terrain and explicit visible combat proxies first, then visual-only meshes/materials in bounded frame slices. Existing area builder methods mix decoration with colliders and quest anchors, so skipping arbitrary construction on a timer would expose missing floors, barriers or invisible attackers. Measure target-device frame time first; keep critical state mandatory and allow sound to arrive independently.
