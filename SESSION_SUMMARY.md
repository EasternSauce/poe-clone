# Session summary: browser play + live spectating (2026-10-01)

What was built and fixed in the session that put this game in the browser with a one-player
lock, a live spectator view and chat. Written so a fresh Claude Code session (or you, later) can
pick up from here. Hosting setup steps live in `DEPLOYMENT.md`.

## Live links

| What | URL |
|---|---|
| Play | https://easternsauce.github.io/poe-clone-web/ |
| Spectate | https://easternsauce.github.io/poe-clone-web/?role=spectator |
| Server status | https://poe-clone-session-server.onrender.com/status |
| Game source repo | https://github.com/EasternSauce/poe-clone (branch `master`) |
| Web build repo (GitHub Pages) | https://github.com/EasternSauce/poe-clone-web (branch `master`) |

- Render's free server sleeps after ~15 min idle; the first visit then takes up to a minute.
- Browsers pause hidden/minimized tabs, so the player's tab must stay visible (alt-tab to
  another window is fine - Run In Background is on).

## Status (2026-10-01)

- Everything is pushed: `poe-clone` up to `7b27078` (Render redeploys the server from it; no
  server code changed) and `poe-clone-web` up to `f3eb8a1` (LibreWolf black-screen fix, live on
  GitHub Pages).
- The live web build still shows "my-project" on the loading screen/tab; that only changes
  with the next Web build (the user asked not to rebuild after the rename).
- Unity keeps reverting `ProjectSettings.asset` -> `projectName` to `my-project` when the
  project is reopened. That's the Unity Cloud link name, not what players see
  (`productName` is `poe-clone`); leave it uncommitted or discard it.

## Architecture

```
Player's browser (Unity Web build)  --WebSocket-->  Render: Node server  --WebSocket-->  Spectator's browser
PlayerStateBroadcaster (10 Hz)                       server/room.js                       SpectatorReplica + SnapshotTimeline
```

### Session server (`server/`, Node + `ws`, hosted on Render free tier)
- One player slot. A second "player" gets "someone is already playing" and waits in a
  **queue**; when the player leaves (or their connection dies - heartbeat), the longest-waiting
  one is promoted automatically.
- Relays `state` snapshots from the player to spectators: rate-capped (~20/s), max 32 KB,
  stamped with a server-side player-session id (`pid`), skipped for spectators whose socket
  is backed up; the latest one is kept for spectators who join mid-game.
- Chat for everyone, with a short history.
- Tests: `cd server && npm test` (24 tests). Probe a running server's stream:
  `node server/test/smoke-spectator.js ws://localhost:8099`.

### Spectator live view (replaced the old 2-fps JPEG slideshow)
- **Sender** - `Assets/Scripts/Network/PlayerStateBroadcaster.cs`: every 0.1 s sends player
  pose/HUD/gear, area, loading-fade flag, and every enemy within 40 units (pos, yaw, hp,
  dead, chasing). Swings and hits are *counters* (`atk`, `stg`) so nothing between snapshots is
  lost; `ap` = which attack animation. ~0.8 KB per snapshot, ~8 KB/s.
- **Wire format + playback** - `Assets/Scripts/Network/Replication/` (own assembly
  `PoeClone.Replication`, unit tested): `StateSnapshot.cs`, `SnapshotCodec.cs` (compact JSON),
  `SnapshotTimeline.cs` (jitter buffer: plays ~0.15-0.2 s behind, interpolates between
  snapshots, adapts delay to jitter, extrapolates up to 0.25 s then holds, snaps teleports).
- **Receiver** - `Assets/Scripts/Network/SpectatorReplica.cs`: the spectator runs the same
  scene with input/AI/combat/spawning off; the player and enemy puppets are posed every frame;
  swings, staggers, deaths, area changes, fades, HUD and gear are applied when playback
  reaches them, with the game's own animations and sounds. `SpectatorView.cs` = waiting
  screen, LIVE badge, "waiting for the player's game" notice on stalls.
- Small hooks added to game code for this: attack/stagger counters
  (`CharacterAttackAnimator`, `Stagger`), replica-only setters (`EnemyHealth`, `PlayerStats`),
  `AreaManager.ApplyAreaImmediate`, `LoadingScreenUI.IsShowing`, `PlayerHUD.SpectatorMode`.

### Web page and build
- Template `Assets/WebGLTemplates/FullWindow/index.html` (selected in the Web build profile):
  full-window canvas that follows window resizes, no Unity branding, own fullscreen button
  (top-right; F11 also works), loading bar. It caps the render pixel ratio to the GPU's
  maximum render size - needed for LibreWolf/hardened Firefox, which report a 2048 px limit with
  a spoofed devicePixelRatio of 2 (that combination was the black screen).
- In Unity 6 the platform is called **Web**; settings live in the build profile
  `Assets/Settings/Build Profiles/Web - Desktop - Release.asset` (it carries its own copy of
  Player Settings - set things there, not only in Project Settings).
- Splash screen off, Run In Background on, product name `poe-clone`.
- Web uses the **Mobile** quality level (`Assets/Settings/Mobile_RPAsset.asset`), tuned to:
  render scale 1, 4x MSAA, 2048 shadow map, 30 m shadow distance, 2 cascades, soft shadows.
- Build into `Builds/WebGL` (that folder is the `poe-clone-web` git clone), then from it:
  `git add -A && git commit -m "Update build" && git push`.

## Bugs fixed along the way
- **Attacking broken on the web near enemies** (also the real cause of "sprint doesn't work in
  Firefox"): the enemy-hover outline shader (`PoeClone/Outline`, loaded via `Shader.Find`)
  was stripped from builds, so it threw every frame once an enemy was in range. Now in
  Always Included Shaders (`ProjectSettings/GraphicsSettings.asset`).
- **Blinking shoulder pad**: coarse hard shadows on the web quality level made the hood's
  shadow edge crawl across the pauldron; with toon shading it flipped light/dark. Fixed by
  the shadow tuning above plus the pauldron and vest shoulder pads not receiving shadows (also
  in the editor builders `StylizedWorldBuilder.cs` / `EquipmentBuilder.cs`).
- "Someone is already playing" never turned into the game -> server-side waiting queue.
- Wrong server URL in the deployed config (`poeclone-...` vs `poe-clone-...`).
- HUD controls hint was clipped.

## Testing tips for next time
- Unity tests: EditMode, 102 total (includes ~30 replication tests in
  `Assets/Tests/EditMode/ReplicationTests.cs`).
- Editor as spectator/player against a local server (no browser URL in the Editor): set
  PlayerPrefs key `PoeClone.DevQuery` to `?role=spectator&server=ws://localhost:8099` (or just
  `?server=...` to play); delete the key afterwards. In a web build the same values are URL
  parameters.
- Local server: `cd server && PORT=8099 node server.js`; serve `Builds/WebGL` with any static
  file server and open `http://localhost:<port>/?server=ws://localhost:8099`.
- Browser testing that worked: headless Chrome driven over the DevTools protocol, and headless
  Firefox over WebDriver BiDi (for LibreWolf behaviour, a profile `user.js` with
  `user_pref("privacy.resistFingerprinting", true);`). Measure player speed from the state
  stream (walk = 6.0 units/s, sprint = 10.2).
