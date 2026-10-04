# Fast testing in the Unity Editor

Read `.claude/skills/unity-playtest/SKILL.md` before Unity MCP work. The project has two different testing paths:

- **Unity EditMode tests:** run with Unity MCP `run_tests` (`mode: EditMode`) and one `get_test_job`. These check code without entering the game.
- **Interactive Play tests:** use the Editor-only `PoeClone.EditorTools.DevTest` helpers in `Assets/Editor/DevTest.cs`. These connect to a local session server; they do not consume a live server slot.

## One-call Play setup

1. Start the local server in a terminal: from `server/`, run `PORT=8099 node server.js` (PowerShell: `$env:PORT=8099; node server.js`). Note that process's PID and stop only that PID afterward.
2. In Edit mode, run `return PoeClone.EditorTools.DevTest.QuickStart();` through Unity MCP `execute_code`, or choose **PoeClone > Test > Quick Start (Sandbox)** in the Editor.
3. Wait for the console line `DevTest QuickStart: ... sandbox ready`, or read `return PoeClone.EditorTools.DevTest.QuickStatus();` once. QuickStart makes a temporary character, enters Play, handles Choose Character and patch notes, waits for the local server, grants 100,000 life and 10,000 mana, and warps to a flat floor at (500, 500). No setup calls are needed while it waits.
4. Use short one-line helpers such as `DevTest.Equip(...)`, `Spawn(...)`, `Use(...)`, `Status()`, `Clear()`, `Quest(...)`, `Area(...)`, `Warp(...)`, `Talk(...)`, or `BossArena()`. See their XML comments for arguments. `QuickStart(false)` uses the normal starting area instead of the floor.
5. Stop Editor Play mode. QuickStart then calls `End()` automatically to restore the user's character and patch-notes preference and clear `PoeClone.DevQuery`. Stop the server by its PID. If Play ended abnormally, call `return PoeClone.EditorTools.DevTest.End();` in Edit mode and check `QuickStatus()`.

Example after the sandbox is ready:

```csharp
return PoeClone.EditorTools.DevTest.Equip("bone_sceptre", "GrantRaiseSkeletons=5, MinionLife=40") + " || " + PoeClone.EditorTools.DevTest.Spawn("Zombie", 3, 3, 5, 1);
```

The sandbox floor exists only during Play. Use `Area(index)` when you need actual world terrain or quests (Greenwood 0, Haven 1, Graveyard 2, Ruins 3, Frozen Hollow 4). Use the older `Begin()` / Play / `Ready()` / `End()` sequence when testing startup UI itself, spectator flow, or a non-default server.

Never edit scripts during Play or make a Web build just to test. Once a Play session is handed to the user, make no further Unity MCP calls until they finish.

## Saved combat balance fixtures

`Assets/Editor/DevBalance.cs` contains twelve fixed loadouts: `melee`, `bow`, `caster`, and `minion` at `early` (level 4), `mid` (level 9), and `late` (level 15). Each has a legal connected passive path and deterministic ordinary gear. `DevBalance.ValidateLoadouts()` checks the paths in Edit mode. Loadouts reset worn gear, passives, skill slots, and minions in the temporary test character. Move through tiers in increasing order within one Play session; start a fresh QuickStart to return to an earlier tier.

From a running QuickStart sandbox, use one call to run a timed sample:

```csharp
return PoeClone.EditorTools.DevBalance.Benchmark("minion", "early", "neutral");
// After about 12 seconds (summons are prepared first):
return PoeClone.EditorTools.DevBalance.BenchmarkStatus();
```

Use `neutral` for an unarmoured Zombie-kind dummy and `boss` for the Shepherd's current 1,200 armour and 30% elemental resistances. The boss dummy has a large life pool so it survives the sample; its result also estimates free-attack seconds for phases 1, 2, and 3 using the real level 12 boss life. `Benchmark` counts post-defence damage for eight game seconds by default. It holds basic attacks for melee/bow, Fire Bolt for caster, and Death Mark for minions. `Benchmark("minion", "late", "boss", false)` measures minions without marking. The dummy is stationary and cannot attack; the test character receives god mode and abundant mana from QuickStart.

For manual scenarios, use `DevBalance.Loadout(build, tier)`, `Army()` (summoner), `Dummy("neutral" or "boss", distance)`, `Start(seconds)`, `DevTest.Attack(seconds)` if desired, then `Report()`. `DevBalance.ArmyStatus()` reports the prepared army and cap. Every summon in `Army()` goes through the real skill cast path. The dummy counters count damage after the enemy's armour or resistance calculation.

The current measurements and limits are in `BALANCE_DPS.md`. When changing early-game summon balance, verify both an early sample and a late boss sample; a low-level buff must not silently increase late damage. Check the level 10+ summon cost or damage formula too.
