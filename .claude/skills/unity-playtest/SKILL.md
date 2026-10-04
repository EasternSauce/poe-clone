---
name: unity-playtest
description: Token-efficient workflow for driving the poe-clone Unity Editor through the UnityMCP tools - compiling, running EditMode tests, play-testing against the local session server, spawning enemies, equipping test gear, reading game state. Load before any Unity MCP work in this project (compile checks, tests, Play mode, screenshots, editor menu builders).
---

# Driving Unity cheaply in poe-clone

The expensive things are: re-pasting long C# into `execute_code`, screenshots, verbose console
reads, and fighting the session startup. Follow this and a full play-test costs a few dozen
short calls.

## 1. Load the tools once

All UnityMCP tools are deferred. Load everything needed in ONE ToolSearch call:

`select:mcp__UnityMCP__refresh_unity,mcp__UnityMCP__read_console,mcp__UnityMCP__execute_code,mcp__UnityMCP__manage_editor,mcp__UnityMCP__run_tests,mcp__UnityMCP__get_test_job,mcp__UnityMCP__execute_menu_item,mcp__UnityMCP__manage_camera`

## 2. Compile check (after every batch of edits, never during Play)

1. `manage_editor stop` if playing (a recompile during Play breaks the session).
2. `refresh_unity` with `compile: request, mode: force`.
3. `read_console` with `types: ["error"]`, `count: 20`. **Always filter to errors**: the
   unfiltered console is full of CS0618 obsolete-API warnings.
4. Zero errors can also mean "not compiled yet". Confirm cheaply with a one-liner that touches
   a new symbol, e.g. `return typeof(PoeClone.Skills.Minion).GetMethod("Summon") != null;`.

## 3. Tests

`run_tests` (EditMode, `include_failed_tests: true`), then ONE `get_test_job` with
`wait_timeout: 120` and `include_failed_tests: true`. It takes about 6s. Don't poll in a loop, and
don't ask for `include_details` (it lists all 160+ passing tests).

## 4. Play-testing: use `PoeClone.EditorTools.DevTest` (Assets/Editor/DevTest.cs)

Each step is a one-line `execute_code` body. Read DevTest's doc comments for the parameters.
**Extend DevTest** whenever you catch yourself writing the same setup C# twice.
For ordinary interactive tests, start the local server, then use **one** Edit-mode call:
`return PoeClone.EditorTools.DevTest.QuickStart();`. It enters Play, chooses the temporary
character, closes patch notes, grants god mode, and moves to a flat sandbox. After it reports
`sandbox ready` in the Console (or `QuickStatus()`), use the other helpers below. Stopping Play
automatically calls `End()` for a QuickStart session. `QuickStart(false)` stays in the normal
starting area. See `TESTING.md` for the complete quick path.

Use the manual sequence below only for startup UI, spectator, or alternate-server tests:

For repeatable combat balance checks, use `DevBalance.Benchmark(build, tier, defence)` in
`Assets/Editor/DevBalance.cs` and read `BenchmarkStatus()` after the sample. It has early/mid/late
melee, bow, caster, and minion loadouts plus neutral and Shepherd-defence dummies. See `TESTING.md`.
When changing early balance, rerun a late boss sample to catch unintended late damage changes.

```
# Bash, background: local server, note the PID it prints (stop it by PID later, never by image name)
cd server && PORT=8099 node server.js        (run_in_background, or `( ... &)` then netstat -ano | grep 8099)

return PoeClone.EditorTools.DevTest.Begin();   // edit mode: backs up save + patch-notes prefs, sets DevQuery
manage_editor play;  Bash `sleep 6`
return PoeClone.EditorTools.DevTest.Ready();   // repeat (sleep 5 between) until it says "running"
return PoeClone.EditorTools.DevTest.God();     // 100k life, 10k mana: observe without dying
return PoeClone.EditorTools.DevTest.Equip("bone_sceptre", "GrantRaiseSkeletons=5, MinionLife=40");
return PoeClone.EditorTools.DevTest.Spawn("Zombie", 3, 3, 5, 1);   // kind, level, count, dx, dz
return PoeClone.EditorTools.DevTest.Use("RaiseSkeletons");         // bar skill by SkillId or slot; aims at nearest enemy
return PoeClone.EditorTools.DevTest.Status();  // player / minions / enemies (+ who they target, cursed, marked)
return PoeClone.EditorTools.DevTest.Clear();   // kill nearby enemies between experiments
manage_editor stop
return PoeClone.EditorTools.DevTest.End();     // restores the user's prefs, clears DevQuery
taskkill //PID <pid> //F
```

Chain several calls into one `execute_code` with `+ " || " +`. Fewer round trips.

Gotchas this saves you from:
- The world sits at `Time.timeScale = 0` until the server grants the play slot. The first name
  confirm is often swallowed: that's why `Ready()` is called again. A server that still
  remembers a previous Play session can hold the slot: restart the server (by PID) if `Ready()`
  stays paused for more than ~20s.
- Don't dismiss the patch notes by hiding GameObjects: they share the HUD canvas. `Ready()`
  calls its `Close()`.
- The mouse can't be steered from code (WarpCursorPosition reads back 0,0). Aimed skills go
  through touch aim (`TouchMode.forced` + `VirtualInput.Aim`), which `DevTest.Use` does for you.
- `execute_code` uses an old C# compiler: no local functions, no `$""`, and `Object` is
  ambiguous (write `UnityEngine.Object`). Assembly-CSharp and Editor types are directly usable.
- Domain reload is off: statics persist across plays. Reset them with
  `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`.
- `Bash sleep N` (N ≤ ~20) is the cheap way to let game time pass between checks.

## 5. Screenshots: sparingly

A screenshot is the most expensive read. Prefer `DevTest.Status()` or a targeted one-liner for
numbers and state. Use `manage_camera screenshot` with `include_image: true`, `max_resolution: 960`
only to judge visuals (layout, models, UI), once per thing being judged. To view a generated asset
(an icon PNG), `Read` the file directly instead.

## 6. Editor builders and asset noise

- Menu builders (`execute_menu_item`): prefer the narrow ones, e.g. `PoeClone/Build Summoner
  Equipment` and `PoeClone/Build Summoner Icons`, over `Build Equipment` / `Build Item Icons`.
  When adding new gear, add a narrow menu item for it the same way.
- Any EquipmentBuilder run re-saves `Assets/Materials/Stylized/*.mat` with float noise:
  `git checkout -- Assets/Materials/Stylized/` afterwards (check `git diff` is only noise first).
- Screenshots land in gitignored `Assets/Screenshots`.

## 7. Before finishing

`DevTest.End()` was called. The server is stopped by PID. `git status` shows no stray
`Assets/Resources/PerformanceTestRun*.json`. Also see SESSION_SUMMARY.md "Before every commit".
