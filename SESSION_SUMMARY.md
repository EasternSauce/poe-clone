# Session summary / hand-off (updated 2026-10-02)

Where this project stands, so a fresh Claude Code session can pick it up. Hosting setup lives
in `DEPLOYMENT.md`; this file is the current state, the branches, what the user asked for last,
and how to work on it.

## Live links

| What | URL |
|---|---|
| Play | https://easternsauce.github.io/poe-clone-web/ |
| Spectate | https://easternsauce.github.io/poe-clone-web/?role=spectator |
| Server status | https://poe-clone-session-server.onrender.com/status |
| Game source repo | https://github.com/EasternSauce/poe-clone (branch `master`) |
| Web build repo (GitHub Pages) | https://github.com/EasternSauce/poe-clone-web (branch `master`) |

- Render's free server sleeps after ~15 min idle; the first visit then takes up to a minute.
- Browsers pause hidden/minimized tabs, so the player's tab must stay visible.

## Branches and what is where

### `master` = what is live (pushed and deployed)
`master` is pushed to `origin/master`; the live Web build (`poe-clone-web`, `203d543`) was
built from `dabea46`. Live features:
- Browser play with a one-player lock + waiting queue, live spectator view (10 Hz state stream),
  chat (Enter sends), optional player-name prompt.
- Mobile touch controls (joystick, attack/run buttons, BAG/CHAR/CHAT menu; `?touch=1` forces them).
- Combat: armour/evasion/block, elemental resistances, mana absorbs 30% of hits (sprint is free),
  stagger with attack-cooldown refund.
- Enemies (one area, Greenwood): Zombie, Raider, Brute (melee), Archer, Fire/Frost/Storm Casters
  (ranged; retreat only every few attacks); respawn when the player is far away.
- Loot: random drops (Normal/Magic/Rare), weapons (sword, hand axe, mace, dagger, short bow with
  a proper draw animation), quivers (bow only; no shield with a bow).
- Pickup is click/tap with an outline (enemies take priority), no auto-pickup; items can be
  thrown away by putting them down outside the inventory.
- New game: empty bag, 3-4 starter items on the ground.
- Editor menu **PoeClone > Build Web** (and a command-line build).

### `overnight-features` = experimental, NOT pushed, NOT deployed
On 2026-10-01/02 the user asked for autonomous feature work ("I will choose which features to
keep in the morning"). It sits on top of `master`, one commit per feature, oldest first:

| # | Commit subject | What it adds |
|---|---|---|
| 1 | Add skills | Cleave, Fire Bolt, Dash, Frost Nova, Rejuvenate, Chain Lightning on Q/E/R/F (round buttons on touch), unlock by level, skill list on K |
| 2 | Add health/mana potions and gold | potions on 1/2 (HP/MP buttons on touch), gold from kills, potion drops |
| 3 | Add real areas | Haven (town, start), Haunted Graveyard (lv 4), Ashen Ruins (lv 7), procedural ground textures, themed props, gates, area title banner |
| 4 | Add Haven's townsfolk, dialogue, quests, merchant and smith | Elder (main quests), Guard (repeating bounties), Merchant (potions, sell bag), Smith (random gear); click/tap to talk; quest tracker |
| 5 | Add bosses | Gravelord Mortis (crypt) and Ashen Warlord (altar), telegraphed slams/summons/fire rain, boss bar, boss quests |
| 6 | Add a simple passive tree | P / TREE button, 1 point per level, three branches |
| 7 | Add unique items | 9 orange uniques with lore; every boss drops one |
| 8 | Add Skeletons, Wraiths and Ember Knights | area-native enemies, skeleton bounty |
| 9 | Add waystones and a town portal | waystone per area (travel to visited areas), T / TOWN portal home |
| 10 | Save the character between visits | PlayerPrefs (browser storage) save/load; Elder "Start a new life" erases |
| 11 | Add the Frozen Hollow and Rimeheart | level-10 area, frost boss, final quest; HUD gets a dark backing |
| 12 | Add a minimap and first-quest guidance | M toggles; tracker points new players to the Elder |
| 13 | Fit the new buttons and panels on phones | second touch-menu column (TREE, TOWN) |
| 14 | Add higher-tier item bases | 21 tiered bases reusing art via `ItemData.ArtId`/`ArtTint` |
| 15-17 | Fix commits | gates blocked by scenery; enemy-over-NPC click priority; boss XP; per-frame scene searches; identical reward items (TickCount seeds); skills panel vs passive tree stacking |
| 18-24 | Feedback quick fixes (2026-10-02) | bolts from the staff orb; pink bows/arrows (URP Lit -> ToonLit); chat newest line visible; slate bag grid; textured town grass; light sources in every area + torches out of pillars; textured UI panels and inset slots |
| 25 | Up to 10 players (2026-10-02) | each plays their own game; 11th is queued; spectators watch one player and switch with left/right arrows or the < > buttons by the LIVE badge; server `room.js` + `SpectatorView` |
| 26-27 | Tap to attack on touch; coloured chat names (2026-10-02) | a finger on the world aims/attacks there (snaps to an enemy within 2.5 m, hold to keep attacking, any finger so the joystick stays usable); chat names coloured per name, role tags tinted, user text escaped |

State: 137/137 EditMode tests pass on the branch (120 on master). A local Web build of the
branch compiled and was then discarded (the deploy folder was restored to the live build).
**Not verified:** a live spectator watching the new content (needs a second client).

**Shipping the multiplayer change:** the server (`server/`, auto-deployed by Render from
`master`) and the Web build should go out together. Either way round still works (old clients
on the new server just watch the first player; new clients on the old server can't switch).

**Decided 2026-10-02:** the user keeps all of it. All work, including the feedback below,
happens on `overnight-features` until the user says to merge it into `master`. Don't merge,
push or deploy before then.

## Latest feedback from the user (2026-10-02)

The user played the overnight build and reported (their words, lightly condensed):
1. **Bow aim on mobile** is super hard - "you should be able to attack by clicking on screen?
   or something"; positioning the character to aim accurately is too fiddly.
2. **Spectator:** arrows and bows look pink (missing material/shader).
3. **Chat:** the last message is often (not always) covered by the input text box.
4. **Fullscreen on Safari** doesn't work - what can we do?
5. **Mage enemies shoot from the base** (bolts start too low).
6. **Lighting:** want more light sources than torches; rethink torch placement - some are
   inside random columns.
7. **Areas are extremely small and always rectangular.** Want PoE-like variety: sometimes an
   open layout, sometimes a series of rooms, sometimes a cave with a "terrarium-like" layout -
   including the town.
8. **Inventory grid** needs a more distinct colour; it blends in too much.
9. **Zero enemy diversity** - every area seems to have the same enemies. Add more enemies,
   some that use a skill from time to time, and cool enemy-only skills.
10. **Town grass** looks too plastic.

Done (one commit each, tested in Editor Play mode, 137/137 EditMode tests):
- (5) Bolts start at the caster's staff orb (`EnemyKinds.StaffOrbName`).
- (2) The bow's Silver/BowString materials, PaleWood, and every runtime primitive used URP Lit,
  whose variants the Web build strips. They now use ToonLit (`Resources/RuntimePrimitive.mat`).
  **Still to confirm in a Web build** (player and spectator).
- (3) Chat log grows upwards in a `RectMask2D` viewport; Text's Truncate had cut the newest line.
- (8) Bag grid: slate cells with lighter grid lines and a frame.
- (10) Generated ground textures are 512 px with grain; Haven grass has blade strokes.
- (6) `ManualPointLightManager` re-scans after `WorldBuilder` and sends the 24 lights nearest the
  view each frame. `WorldBuilder.Glow` adds lights: Haven lanterns, graveyard candles + green
  crypt flames, ruin braziers/altar/lava, ice crystals, Greenwood glowshrooms, waystones. The
  Greenwood ruin torches were moved off the pillars onto posts (scene edit).
- Extra (asked mid-session): UI backgrounds textured: `UiKit.Grain` (tiled panel grain) and
  `UiKit.Inset` (sliced recessed slot) on panels, slots, bag cells, skill slots and buttons.

- (1) Touch: tap/hold on the game world to attack there (`PlayerCombat.ReadWorldTouch`), with
  snapping to a nearby enemy; the attack button keeps its nearest-enemy auto-aim.
- Extra: chat names are coloured (`ChatUI.FormatLine`).

Still open (the user wants to confirm before each of these is started):
- (4) iPhone Safari has no Fullscreen API for non-video elements (iPad is partial). Practical
  fix: a web app manifest + `apple-mobile-web-app-capable` meta so "Add to Home Screen" opens
  the game fullscreen, plus an in-page hint on iOS. The page template is
  `Assets/WebGLTemplates/FullWindow/index.html`.
- (9) Each area mixes the same 7 base kinds by weight (`WorldBuilder.KindWeights`); only the
  Graveyard/Ruins/Frozen natives differ. Per-area rosters + a few skill-using enemies would fix it.
- (7) is a rewrite of `WorldBuilder` (layout generators: open field / rooms+corridors / cave),
  and gates, waystones, spawners, safe spots and the minimap all depend on area bounds
  (currently a 100x100 m square per area, `WorldBuilder.HalfSize`, walls at +-49.5).

Next, in the proposed order: enemy variety (9) -> Safari home-screen support (4) -> area layout
overhaul (7) as its own task. Confirm with the user before starting each.

## Architecture notes

```
Player's browser (Unity Web build)  --WebSocket-->  Render: Node server  --WebSocket-->  Spectator's browser
PlayerStateBroadcaster (10 Hz)                       server/room.js                       SpectatorReplica + SnapshotTimeline
```

- **Session server** (`server/`, Node + `ws`, Render free tier): one player slot + queue,
  relays `state` snapshots (rate/size capped) and chat. Tests: `cd server && npm test`.
- **Spectator view:** sender `Assets/Scripts/Network/PlayerStateBroadcaster.cs`; wire format and
  jitter-buffered playback in `Assets/Scripts/Network/Replication/` (own assembly, unit tested);
  receiver `Assets/Scripts/Network/SpectatorReplica.cs` (runs the same scene with input, AI,
  combat and spawning off; enemies are posed from snapshots by kind index).
- **Web page:** `Assets/WebGLTemplates/FullWindow/index.html` (full-window, own fullscreen
  button, caps pixel ratio to the GPU limit for LibreWolf). Settings live in the build profile
  `Assets/Settings/Build Profiles/Web - Desktop - Release.asset`. Web uses the Mobile quality
  level (`Assets/Settings/Mobile_RPAsset.asset`).
- The outline shader (`PoeClone/Outline`, `Shader.Find`) must stay in Always Included Shaders.
- Overnight branch structure (for orientation): world built at runtime by
  `Assets/Scripts/World/WorldBuilder.cs` (areas, props from `Resources/AreaKit.asset`, built by
  menu **PoeClone > Build Area Kit**); quests in `Assets/Scripts/Quests/`; dialogue/UI in
  `Assets/Scripts/UI/`; boss logic `Enemies/BossAbilities.cs` + `World/BossLair.cs`; save
  `Player/SaveSystem.cs` + `Inventory/SaveData.cs`.

## How to work on it (important)

- **Test in Editor Play mode, not in Web builds.** A Web build takes ~5 min and blocks the
  editor; do one at the end before deploying. Deploy = build via **PoeClone > Build Web** into
  `Builds/WebGL` (a clone of `poe-clone-web`), commit there with `git add -A`, push both repos.
- **Driving the editor:** the Unity MCP bridge listens on 127.0.0.1:6400 (8-byte big-endian
  length + JSON; `execute_code`, `manage_editor` play/stop, `manage_scene` screenshot,
  `read_console`, `refresh_unity`, `run_tests`/`get_test_job`, `execute_menu_item`). Domain reload
  is off, so statics persist between plays (reset them with
  `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`).
- **Local session server** so the live slot isn't needed: `cd server && PORT=8099 node server.js`,
  and in the Editor set PlayerPrefs `PoeClone.DevQuery` = `?server=ws://localhost:8099` (add
  `&role=spectator` to watch). **Delete that key afterwards**, and on the overnight branch also
  delete PlayerPrefs `PoeClone.Save.v1` after test sessions (otherwise the user's Editor loads
  a test character). In a Web build these are URL parameters.
- Simulated input via the bridge needs InputSystem `backgroundBehavior = IgnoreFocus` and
  `editorInputBehaviorInPlayMode = AllDeviceInputAlwaysGoesToGameView`; restore the originals
  (`ResetAndDisableNonBackgroundDevices`, `PointersAndKeyboardsRespectGameViewFocus`) afterwards.
  Code run through `execute_code` runs in the editor loop (Time values differ); trigger gameplay
  through `PoeClone.Player.VirtualInput` or queued input events instead.
- **Before every commit** revert Unity noise: `ProjectSettings/ProjectSettings.asset`
  (`projectName` flips to `my-project` from the cloud link), after Web builds also the build
  profile asset and `Data/Plugins/lib_burst_generated.wasm`; delete
  `Assets/Resources/PerformanceTestRun*.json`.
- Never use the user's inspiration directory directly; copy what is needed into real assets.
- Commit/push only when the user asks. Hotfixes for the live game go on `master` (build +
  deploy), then `git rebase master` on `overnight-features`.
