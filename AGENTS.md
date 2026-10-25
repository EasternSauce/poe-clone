# Deployment

For deployment requests, start with `DEPLOYMENT.md` and `git status`. It documents
the target, fresh-build requirement, reusable Pages checkout, and timed live
verification. Do not begin with a generic repository discovery scan.

# Unity gameplay demos and visual checks

Do not run complex Unity tests unless the user specifically asks. Leave gameplay checks to the user unless requested; routine asset refresh, compilation, and console checks remain required.

**Always start gameplay checks through the temporary DevTest session in `Assets/Editor/DevTest.cs`. Do not start ordinary Play mode and click through startup menus. Gameplay checks and screenshots must begin only after all startup overlays, including patch notes, are dismissed through the helpers.**

1. Check Unity editor readiness and whether Play mode is already running. Stop an existing temporary demo before starting another.
2. Ensure the local session server is listening on port 8099. If it is absent, set `$env:PORT = '8099'` and start `node server.js` in the `server` directory. On Windows launch it with `Start-Process -WindowStyle Hidden`. Verify the listener before calling `QuickStart`; `server.js` otherwise defaults to port 8080.
3. Through Unity MCP `execute_code(action="execute", code=...)`, call:
   ```csharp
   return PoeClone.EditorTools.DevTest.QuickStart();
   ```
   This backs up the user's character preferences, enters Play, automatically handles startup UI, selects a temporary character, grants god mode and Dash, and moves to a quiet sandbox. Startup is asynchronous; check `DevTest.QuickStatus()` until it reports `running` and `sandbox ready`. Do not click startup menus while it is progressing.
   **`running` alone does not prove that startup overlays are gone.** Patch notes can appear after the initial ready check. Before changing demo state or taking screenshots, call `DevTest.Ready()` again and check that `PoeClone.UI.PatchNotesUI.IsShowing` is false. If an overlay remains, keep advancing startup with `DevTest.Ready()` until it is dismissed. Do not capture or accept a gameplay screenshot covered by the name prompt, character selection, or patch notes.
4. For a demo starting with a bow equipped, call:
   ```csharp
   return PoeClone.EditorTools.DevTest.QuickStart(weaponBaseId: "short_bow");
   ```
   Any supported weapon base ID can be supplied to `QuickStart`.
5. Change inventory and scene state directly through code during Play:
   ```csharp
   return PoeClone.EditorTools.DevTest.Equip("short_bow");
   ```
   `Equip` also accepts extra stat modifiers and an item level. Other existing helpers include `God`, `Warp`, `Spawn`, `Status`, and `Sandbox`; inspect their signatures before calling them. Use `QuickStart(sandbox: false, weaponBaseId: "short_bow")` when checking the actual starting area or minimap.
6. For comparisons across facing directions, pause Play after setup, rotate the player root, and capture each view from the same camera position using Unity MCP `manage_camera(action="screenshot", ...)`. Inspect the saved images. Also check the Unity console for errors after changes compile.
7. Stop Play when finished to restore the user's character preferences automatically. If a demo is deliberately left paused for inspection, report that to the user; stopping it will restore preferences. `DevTest.End()` is available in Edit mode for manual cleanup if needed.

Keep demo equipment changes inside the temporary session. Do not change new-character loadouts just to prepare a visual check.

Plain Editor Play (no DevTest session) is the dev build: it skips patch notes and starts the user's first saved character in Greenwood at the Haven gate arrival, or shows the main menu when no characters exist. It plays the user's real character, so gameplay checks still go through `QuickStart`. Loading takes several seconds after Play begins, and screenshots taken earlier only show the loading screen. If you do use plain Play, do not screenshot or query gameplay on a fixed delay. Repeat this check until it returns `ready`:
```csharp
return PoeClone.Player.SaveSystem.CharacterLoaded && Time.timeScale > 0f ? "ready" : "loading";
```

When a check is finished, delete every screenshot or capture you took only for your own testing (for example under `Captures/` or `Assets/Screenshots/`, including their `.meta` files). Keep only images you show to the user or that they asked for, and tell them where those are. Never delete screenshots you did not create.

# Inspiration assets

`Assets/assets_for_inspiration` is temporary reference material and will be deleted. The game must not depend on files in this directory. Before using any inspiration asset, copy it into a permanent directory under `Assets` outside `assets_for_inspiration`, then reference the copied asset. This applies to scenes, prefabs, scripts, Resources paths, soundboards, and other runtime asset references. Never add new game dependencies on the inspiration directory. Existing soundboard references still need migration during the planned soundboard rework.

# Audio levels

Sound loudness lives in the audio files. To make a sound louder or quieter, re-render the file (for example ffmpeg `-af volume=X`, written as wav) and keep its `.meta` so the GUID is preserved. Do not add per-sound volume multipliers in code or on the sound board; code only applies the SFX/UI/ambience category gains, distance fade and the player's master volume. If one recording needs two levels, add a quieter copy with its own sound board effect.

# Cleanup around changes

Only create Markdown (`.md`) files with the user's explicit permission. Keep
analysis and reports in the conversation unless the user explicitly authorizes
a Markdown file.

Weapon skill trees must start with small nodes. Place at least two small nodes
before each notable; never put a notable at or next to the start. Never link a
keystone directly to a notable or another keystone; travel nodes always sit in
between. Weapon trees should not duplicate main passive tree stats (attack speed,
generic damage, area, armour penetration, life on hit and similar); prefer
skill-specific mechanics, or move a modifier off the main tree on purpose.
Basic (travel) nodes only carry plain "increased" stats such as increased
damage, physical damage, minion damage, minion life, area or attack speed.
Exotic stats (reduced cost, range, projectile speed, ailment duration, armour
penetration, minion movement speed, life on hit and the like) belong on notables
and keystones.
Lead with damage on early nodes so levelling feels stronger. Almost never put
"more" damage, conditional, finisher-only or keystone-specific modifiers on
basic nodes; those belong to notables and keystones.

When making a specific code change, spend some time looking for improvements in and around the code you are touching: remove old, dead, or unreachable code. Do small cleanups alongside the change. If a spot looks like it needs a bigger refactor, ask the user first instead of doing it unprompted.

# Change completion

When finishing a set of project changes, update `Assets/Resources/PatchNotes.txt` to describe the user-facing changes. Keep the patch notes current as part of completing the work.

After finishing every new feature or fix, refresh assets in the Unity Editor through Unity MCP `refresh_unity(scope="all", mode="force", compile="request", wait_for_ready=true)`. Wait for asset importing and script compilation to finish, then check the Unity console for errors before reporting completion.

**If Unity is in Play mode, stop Play yourself (`manage_editor(action="stop")`) without asking, then refresh.** Never compile into a running Play session, and never treat Play mode as a reason to postpone the checks or the commit.

**Every finished feature or fix MUST be committed and pushed before you report completion. A turn that ends with uncommitted finished work is incomplete.** Commit with a descriptive message and push to the current branch's remote. Include only changes related to the completed work. Do not wait to be asked and do not ask for permission. Only a failed commit or push is a valid reason to stop, and then report the blocker.

# Class gauntlet (balance testing)

The Editor-only class gauntlet compares builds at levels 1, 7, 14, 21, 28 and 34. Builds are data in `Assets/Scripts/World/Gauntlet/GauntletBuilds.cs` (weapons, armour attribute, skills, stat weights, passive targets, uniques). Start it from the PoeClone menu or through Unity MCP: `PoeClone.EditorTools.GauntletLauncher.Manual()`, or automated runs against frozen enemies with `GauntletLauncher.Auto("pyromancer,necromancer", "1,7,14")` (check `AutoStatus()`, cancel with `StopAuto()`). It runs in the minimal arena, needs no session server and never saves the character. Results go to `GauntletResults/` (gitignored). To retest a build, edit its entry and rerun `Auto` for it; each result row carries the build's fingerprint. Keep melee builds defensive rather than full damage.
