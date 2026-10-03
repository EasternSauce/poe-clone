# Working on this project

What to do next is in `todo.txt` (repo root); read it first. This file is only how to work.
Deploying: see `DEPLOYMENT.md`.

## Branches
- `master` is live: the Web build and the Render session server both deploy from it.
- Commit only when the user asks; push and deploy only when the user asks.

## Testing: in the Editor, not in Web builds
- **Start with the `unity-playtest` skill** (`.claude/skills/unity-playtest/SKILL.md`): the cheap way to
  compile, test and play-test, built on the one-line helpers in `Assets/Editor/DevTest.cs`
  (Begin/Ready/God/Equip/Spawn/Use/Status/Clear/End). Extend DevTest instead of re-pasting setup C#.
- Iterate in Editor Play mode. A Web build takes ~5 min and blocks the Editor; make one only
  when deploying (or for something web-only: the page template, browser quirks).
- **Driving the Editor:** the Unity MCP bridge listens on 127.0.0.1:6400. Protocol: on connect
  it sends a one-line `WELCOME ...`; then each message is an 8-byte big-endian length + JSON,
  e.g. `{"type":"execute_code","params":{"action":"execute","code":"...C# body returning a value..."}}`.
  Also: `manage_editor` (play/stop), `manage_scene` (`screenshot`, saved under the gitignored
  `Assets/Screenshots`), `read_console` (use `types` incl. `"log"`; `include_stacktrace`),
  `refresh_unity` (`compile: request`), `run_tests` / `get_test_job` (EditMode),
  `execute_menu_item`. A small Python helper that wraps this is worth writing first.
  `execute_code` uses an old C# compiler (no local functions).
- Domain reload is off: statics persist between plays (reset them with
  `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`). After a recompile, start Play
  again before trusting what you see.
- **Local session server**, so the live slot isn't needed: `cd server && PORT=8099 node server.js`,
  and in the Editor set PlayerPrefs `PoeClone.DevQuery` = `?server=ws://localhost:8099`
  (add `&role=spectator` to watch). On startup a name prompt appears: confirm it through
  `NamePromptUI`'s private `Confirm` method.
- **Leave the user's Editor as you found it:** back up PlayerPrefs `PoeClone.Save.v1` before
  testing and restore it after (play sessions overwrite it); delete `PoeClone.DevQuery`; stop
  the local server; restore InputSystem settings if changed (see below).
- Simulated input (keyboard, touch via `InputSystem.QueueStateEvent`) needs InputSystem
  `backgroundBehavior = IgnoreFocus` and `editorInputBehaviorInPlayMode =
  AllDeviceInputAlwaysGoesToGameView`; the originals are `ResetAndDisableNonBackgroundDevices`
  and `PointersAndKeyboardsRespectGameViewFocus`. Queue a press and its release in separate calls.
  Prefer triggering gameplay through real paths (`PoeClone.Player.VirtualInput`, queued input)
  over calling internals.
- Tests: Unity EditMode tests (`run_tests`), server tests `cd server && npm test`.

## Patch notes
Players see `Assets/Resources/PatchNotes.txt` once per release (`UI/PatchNotesUI.cs`). Add a line
there for every player-visible change since the last deploy; when deploying, give the first line
(`version: ...`) a new value, and start the next release's notes from scratch after that.

## Before every commit
Revert Unity noise: after Web builds `Assets/Settings/Build Profiles/Web - Desktop - Release.asset`
(if the diff is only a settings snapshot) and `Data/Plugins/lib_burst_generated.wasm`; delete
`Assets/Resources/PerformanceTestRun*.json`. The project is unlinked from Unity Cloud (2026-10-03),
so `ProjectSettings.asset` no longer flips `projectName` to `my-project`; don't relink it.
Never use the user's inspiration directory directly; copy what is needed into real assets.
Many files have CRLF line endings: edit them keeping their endings.

## Orientation
- World built at runtime by `Assets/Scripts/World/WorldBuilder.cs` (+ `WorldBuilder.Borders.cs`,
  `AreaShape.cs`): five areas (Haven town, Greenwood, Graveyard, Ruins, Frozen Hollow), each an
  irregular outline with dressed borders; Greenwood's core is the scene's original forest.
- Enemies: kinds in `Enemies/EnemyKind.cs` (referred to by index: add new ones at the end),
  per-area weights in `WorldBuilder.KindWeights`, skills in `Enemies/EnemySkills.cs`, bosses in
  `Enemies/BossAbilities.cs` + `World/BossLair.cs`.
- Network: server `server/room.js` (up to 10 players, each in their own game; spectators pick
  who to watch); client `Network/GameSessionController.cs`; spectator stream sender
  `Network/PlayerStateBroadcaster.cs`, wire format `Network/Replication/`, receiver
  `Network/SpectatorReplica.cs`.
- UI is built at runtime (`Inventory/UiKit.cs` helpers); web page template
  `Assets/WebGLTemplates/FullWindow/`.
- Lighting: point lights reach the ToonLit shader through `Visuals/ManualPointLightManager.cs`
  (nearest 24 to the view). Runtime primitives use `Resources/RuntimePrimitive.mat` (URP Lit
  is stripped from Web builds and renders pink); the outline shader must stay in Always
  Included Shaders.
