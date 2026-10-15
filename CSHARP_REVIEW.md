# C# maintenance review — 2026-10-15

## Scope

Reviewed the structure of 276 runtime C# files (about 76,000 lines), existing test
assemblies, and representative implementation paths through inventory, equipment,
stats, saves, skills, combat, quests, world construction, audio and networking.
This is a cross-system maintenance pass, not an exhaustive correctness proof or
a line-by-line audit of every authored world/boss script. No gameplay demo was run.

The development history alone cannot establish that an issue was caused by a
particular model. The relevant risk is many locally reasonable changes without
regular checks of contracts between systems.

## Findings addressed

| Finding | Evidence and change |
| --- | --- |
| Old tests asserted retired design rules | The baseline suite found obsolete assertions for loot counts and eligibility, weapon base-damage tags, automatic mana protection, devotion, keystone approaches, unique effects and tooltip wording. Existing patch notes, the current modifier catalogue and development guide document the newer rules. Updated the assertions without reverting intentional balance changes. Modifier eligibility follows the authored catalogue; modifier counts, uniqueness, connected approaches, fixed rewards and saved values retain separate assertions. |
| Save restoration could silently discard items | `SaveData.RestoreInventory` ignored the item displaced by a duplicate equipment-slot record. It now preserves the first equipped entry and recovers the extra item through the bag/overflow path. `RestoreStash` now returns items from tabs beyond the available tab count for recovery. |
| One inventory operation exposed intermediate state | `InventoryGrid.TryAutoPlace` could notify after filling an existing stack and before placing the remainder. It now emits one notification after the transfer completes. Insufficient total space still leaves all stacks untouched. |
| Repeated co-op prediction could subtract one hit twice | `PredictedHealth.Predict` now ignores a hit sequence already pending, in addition to its existing acknowledgement check. |
| Repeated placement and serialization logic | Inventory placement shares bounds, overlap and first-free-space helpers. Boolean space checks no longer allocate overlap lists per candidate cell. Save capture shares a tab serializer across bag and all stash tabs. Bounds checks also reject integer-overflow coordinates. |

Added regression coverage for stack transfer atomicity and notifications,
multi-stack currency consumption, duplicate equipment recovery, full-bag overflow,
extra stash tabs, all six stash tabs and names, saved stack limits, repeated hit
predictions, and world-construction waits/disposal on completion, cancellation and
failure. World-construction tests drive the iterator directly in Edit mode.

## Prioritized follow-up work

These changes would touch larger ownership boundaries and were deliberately left
for a separate refactor, as required by `AGENTS.md`.

**Confirmed existing test failure: passive-tree layout.**
`PassiveTreeTests.SpacingKeepsItsAverageWithoutLongOutliers` still fails its
1.8-unit maximum edge length. Inspection found two violations:
`b_surge -> travel_b_surge_s_sp5_1` (1.812223) and
`junction_v3 -> travel_v2_junction_v3_1` (2.196867). Mean edge length (0.82734)
and deviation (0.23241) satisfy the other assertions. Runtime coordinates match
the authored positions resource. The test and authored layout were retained;
correct these edges through the positions editor and recheck overlap/crossing
clearance in a dedicated layout change. This is separate from the obsolete rule
assertions corrected above.

1. **Protect unreadable saves before allowing autosave.**
   In `Assets/Scripts/Player/SaveSystem.cs`, `Load` returns after a JSON parse failure;
   `TryLoadSelectedProfile` still marks the session loaded, allowing later `Save`
   calls to overwrite that character key with fresh state. The parse-failure path
   also skips shared-stash restoration. Introduce an explicit load result
   (new/loaded/recoverable failure), preserve the original data, and test failure
   recovery before changing this persistence policy.

2. **Test profile and shared-stash migration independently of PlayerPrefs.**
   Existing `SaveDataTests` exercise records and inventory restoration, but do not
   exercise the profile roster, legacy profile recovery, merging character stashes
   into the shared stash, or load/save ordering during a character switch.
   `SaveSystem` mixes these policies with Unity lifecycle and global storage. A
   small storage adapter plus a plain C# migration service would allow tests with
   isolated in-memory keys and avoid modifying a developer's real character data.

3. **Give each desktop WebSocket connection its own state and test its lifetime.**
   `Assets/Scripts/Network/WebSocketClient.cs` shares `socket`, `cts` and the send
   queue between asynchronous connect, receive, send and graceful-close operations.
   `ConnectDesktop` invokes a receive loop that reads the shared fields after its
   await; a superseded connect can therefore start receiving from the newer socket.
   Old receive callbacks can also report closure after a replacement connection.
   Receive chunks are individually decoded as UTF-8, which can corrupt a multibyte
   character split across chunks. A connection-owned context/generation and a
   decoder or complete-message byte buffer would address these concerns. Test
   reconnect during close, stale callbacks, fragmented Unicode and cancellation.
   Server tests do not cover these C# transport paths.

4. **Extract quest state transitions from scene effects.**
   `Assets/Scripts/Quests/QuestLog.cs` owns prerequisites, progress, prop deduplication,
   migration, rewards, notifications and scene subscriptions. There are no dedicated
   quest-state tests. Start with a plain C# quest-progress object; keep reward
   delivery and world effects in the component. Test prerequisites, repeated prop
   events, repeated hand-ins, previsited areas and imported completion state.

5. **Make the test boundary intentional.**
   `PoeClone.Tests.EditMode.asmdef` references Inventory and Replication, while most
   runtime code remains in `Assembly-CSharp`. Tests for skills, world construction
   and session policy use reflection because named assemblies cannot reference the
   default assembly. Move extracted policy objects into a small explicitly
   referenced assembly as their ownership stabilizes, with internal visibility to
   tests. Avoid a repository-wide assembly migration in the same change.

## Design direction

The existing plain C# helpers (`DefenceMath`, `InventoryGrid`, `SkillBindings`,
`SnapshotTimeline`, `PredictedHealth`) are a useful pattern: components coordinate
Unity objects; ordinary objects own rules and transitions. Continue this pattern
at persistence, connection lifetime and quest boundaries. A repository abstraction
for storage and explicit connection/session states have concrete benefits here;
adding interfaces to every component or a global event bus would add indirection
without resolving those ownership problems.

Large `WorldBuilder`, enemy and skill partial classes include substantial authored
content. File size alone is not evidence of duplication. The duplicate-block scan
found little substantial exact cross-file duplication; the more valuable cleanup
is shared policy and consistent operation boundaries. Before extracting combat
formulas, add tests comparing actual hit/resource behavior to the existing stat
and offline-balance models.

## Keeping future iterations consistent

- When a rule intentionally changes, update its behavior tests in the same commit.
  Use authored data for eligibility, but keep separate assertions for invariants
  such as rarity budgets, uniqueness and conservation of items/resources.
- Keep a fast EditMode run available alongside the complete suite. The existing
  `FullSurvey_ExportsEveryArchetypeAtLevelsOneThrough34` rebuilds the full balance
  survey and is documented to take roughly 18 minutes on this workstation.
- Save regression tests should use isolated storage; component tests should clean
  up their objects and restore any static state they modify.
- Record ownership and migration contracts in `DEVELOPMENT.md`; commits alone do
  not communicate those constraints to the next independent coding session.

## Verification

- Complete original EditMode suite: 285 cases executed, 15 failures. The full
  318-archetype / levels 1–34 balance survey passed and its generated report
  validates successfully.
- After maintenance: 300 non-survey cases executed, 299 passed and the existing
  layout-spacing case failed. Together with the unchanged survey already executed,
  this covers all 301 current cases: 300 passed, one failed. All 16 added cases pass.
  The survey was not rerun after changes to unrelated inventory/replication code
  and obsolete test assertions.
- PlayMode discovery: zero cases. No gameplay demo or character session started.
- Session server: `npm test`, 53 passed, zero failures; no JavaScript changes.
- Forced Unity refresh/import/compilation completed; console contains no errors.
  Eight existing UAC0005 warnings remain in `OfflineBalanceTests` for assembly
  discovery through `AppDomain.GetAssemblies`, consistent with the test-boundary
  issue described above.
- Existing source line endings preserved; whitespace check passes with
  `git -c core.whitespace=cr-at-eol diff --check`.
