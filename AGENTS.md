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

# Inspiration assets

`Assets/assets_for_inspiration` is temporary reference material and will be deleted. The game must not depend on files in this directory. Before using any inspiration asset, copy it into a permanent directory under `Assets` outside `assets_for_inspiration`, then reference the copied asset. This applies to scenes, prefabs, scripts, Resources paths, soundboards, and other runtime asset references. Never add new game dependencies on the inspiration directory. Existing soundboard references still need migration during the planned soundboard rework.

# Change completion

When finishing a set of project changes, update `Assets/Resources/PatchNotes.txt` to describe the user-facing changes. Keep the patch notes current as part of completing the work.

After finishing every new feature or fix, refresh assets in the Unity Editor through Unity MCP `refresh_unity(scope="all", mode="force", compile="request", wait_for_ready=true)`. Wait for asset importing and script compilation to finish, then check the Unity console for errors before reporting completion.

After completing a feature or fix and the required checks, commit the changes with a descriptive message and push the commit to the current branch's remote before reporting completion. Include only changes related to the completed work. If committing or pushing fails, report the blocker.
