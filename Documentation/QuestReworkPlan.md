# Quest rework — plan and session handoff

Status: CHECKPOINT 2 COMPLETE / AWAITING USER REVIEW. Checkpoint 1 is committed as 7418dec. Checkpoint 2 parent: 0de1fcb (Lost Hollows crossroads restructure). Checkpoints 3-4 require separate approval.

## Live checkpoint 2 handoff (update during work)

- User approved checkpoint 2 and emphasized keeping these notes current because usage may run out soon.
- Correction to checkpoint 1: its 6-8 enemy packs spawned together, including Greenwood. Greenwood must instead spawn three at a time in multiple waves, clearing one group before the next.
- New requirement: a quest sends the player to a blockade; accepting the quest places a strong stationed group there immediately. Kill the assigned guards, THEN click to dismantle the blockade, THEN unlock its gateway. Teleporting past the physical wall must not bypass the disabled gateway.
- World now branches: Haven -> Greenwood -> Lost Hollows; Hollows -> Haunted Graveyard (dead end) and Hollows -> Ashen Ruins. A bone wall currently auto-opens after Mortis. Convert that wall into the explicit guarded/dismantle quest, offered by Tobias after Mortis and before the warning to Haven.
- Other checkpoint 2 scope remains: attackable totems; sequential patrol clues ending in raiders; stationed, distinct reliquary guardians; wreck/trail/carrier caravan hunt; Bell-Ringer main-story quest before relics. Vex/rescue remains checkpoint 3.
- Inspection complete: reviewed updated layouts, bone wall, AreaGate lock checks, QuestBarrier, quest/save data, basic attack/projectile targeting, enemy area parking and boss lairs. Working tree was clean at checkpoint 2 start. Unity is in Edit mode.
- Implementation now wired: QuestProp supports three-enemy Greenwood waves, sequential clues, guarded props that spawn on acceptance and require a final interaction, and attackable totems. QuestBook/world sites include Bell-Ringer and deep_road. Existing characters beyond new steps are grandfathered without resetting the quest book. EnemyHealth supports quest scenery without loot/kill credit; network capture/handoff skips these local scenery targets. First forced Unity refresh/compilation passed with zero console errors. Final forced refresh corrected a missing World namespace import in EnemySpawner; verifying the final console, then committing/pushing. Structural data check passed: all 26 quests have valid, acyclic prerequisites and areas; Bell-Ringer and deep_road are in the main chain; all new encounter enemy kinds exist and do not split.
- Completion condition: preserve the full playable main story at checkpoint 2. Do not report completion until required asset refresh, compilation, console checks and commit/push succeed. Gameplay checks remain with the user unless requested.
Planning baseline: master, aaba6ca. Current checkpoint parent: 0ae77a7 (an unrelated Dash adjustment was committed during this work and is preserved).

## Working agreement

- User explicitly requests final documentation cleanup: once the ENTIRE main-questline rework is finished, remove this temporary plan/handoff and every other temporary Markdown file created for this rework. Update any permanent documentation that describes changed quests/world routes; do not leave obsolete plans behind. Keep the handoff until all checkpoints are completed and reviewed so interrupted sessions remain resumable.
- Approve the plan first. Implement only the next approved checkpoint, finish it, and stop for user review before starting another.
- The entire main questline must remain playable after EVERY checkpoint. Keep each checkpoint playable and independently resumable. Avoid leaving half-wired quest mechanics between checkpoints.
- At each completed checkpoint: update user-facing patch notes, force Unity asset refresh and compilation through MCP, wait for readiness, inspect console errors, commit only related changes, and push the current branch. Report any blocker accurately.
- Leave gameplay testing to the user unless requested. Any requested gameplay checks must use the AGENTS.md DevTest workflow.
- Update this document before each checkpoint commit with changes, checks, commit reference when available, user feedback, and the exact next step. Update the handoff at an interruption too.
- No large unrelated refactors. Clean up obsolete quest code locally as mechanics are replaced.

## Confirmed user requirements

- Replace the repeated pattern of clicking an object and spawning two or three trivial enemies with varied quest activities.
- Always show the destination location in the current objective, including talk, intermediate, and return-to-NPC objectives.
- Existing survival/wave quests must spawn enemies much more frequently.
- Whenever a quest-object interaction summons enemies, its objective must not count until those specific enemies are dead. A timed defence also requires clearing its surviving summons.
- Summoned encounters must be meaningful: larger packs and/or stronger enemies appropriate to the area's level, rather than two ordinary wolves.
- Rewrite main-story material to fit the changed quests and the cave's place in the route.
- Add Vex and the Bell-Ringer to the MAIN questline: six of nine bosses become main-story encounters; Bramblesow, the Sunforged Idol and Hrimgar remain optional.
- Add a rescue NPC quest: kill the captors, free the captive, then speak to them. Proposed placement: The Lost Hollows.
- Work iteratively with explicit user approval between checkpoints; preserve a handoff for a new session or usage interruption.

## Findings from the existing code

- QuestBook.cs has story and side quests; most interactions use QuestGoal.Use and the shared QuestProp component.
- QuestProp.Use currently awards progress BEFORE spawning its ambush. This is the first behaviour to replace.
- The Ashen Shrine is the existing timed rite: 30 seconds, waves every 6 seconds, 2–3 enemies per wave, spawning stops at 90% of the timer. Its success currently ignores surviving summons.
- EnemySpawner.SpawnAmbush returns void. Encounter ownership and death tracking need a small extension rather than counting arbitrary area kills.
- World route (changed after checkpoint 1): Haven → Greenwood → The Lost Hollows, the crossroads. The Haunted Graveyard is a dead-end branch off the Hollows; the Hollows' deep road to the Ashen Ruins is a QuestBarrier (bone wall) opened by completing `gravelord`. Ruins → Frozen Hollow → The Shed Sanctuary. Gate spots live in AreaLayouts.Links/GateLocal.
- The Lost Hollows has native cave monsters and access to The Warren / Vex, the Tunnel King, but no quest chain or quest NPCs.
- QuestBook.Whereabouts says only 'at the Emberwatch camp' for Varek/Ysolde, omitting Ashen Ruins. Several active objectives omit their area too.
- SaveSystem discards all quest progress if QuestBook.Version changes. Do not casually bump the version to introduce this rework.
- Main files: Assets/Scripts/Quests/{QuestBook,QuestLog,QuestProp,QuestBarrier}.cs; Assets/Scripts/UI/{QuestTrackerUI,NpcDialogues}.cs; Assets/Scripts/World/{WorldBuilder.QuestSites,WorldBuilder.BossLairs,AreaLayouts,Npc}.cs; Assets/Scripts/Enemies/EnemySpawner.cs; Assets/Scripts/Player/SaveSystem.cs.

## Design direction

Keep the existing Shepherd story backbone: Mortis, the Warlord and Rimeheart are the priest, king and queen whose deaths free him. Rewrite the journey, clues and character motivations around the new gameplay. Dialogue should describe the actual mechanics and locations, and acknowledge completed stages.

Alternate exploration, deliberate combat encounters, rescues, retrieval and defence. Avoid adjacent quests that both amount to 'click three props and fight the same pack'. Keep a few quiet discoveries; not every interaction needs an ambush.

Use clear stage-specific objectives, for example:

- 'Defeat the guide’s captors — The Lost Hollows (0/6)'
- 'Free the captive guide — The Lost Hollows'
- 'Speak to the rescued guide — The Lost Hollows'
- 'Return to Tobias — Haunted Graveyard'

Numbers below are initial tuning proposals, subject to player feedback. Prefer area's existing enemy types and levels; use explicit encounter composition rather than multiplying all world enemy stats.

## Checkpoint 1 — encounter rules, objective locations and wave pace

- Add ownership/tracking for enemies spawned by quest interactions. Show the combat stage and remaining encounter enemies; award prop progress only when the required encounter is cleared.
- Prevent repeat-click spawning. Handle death, leaving/re-entering areas, and saving/reloading without softlocks or awarding unfinished encounters. Persist completed objectives; unfinished encounters must restore or restart consistently.
- Apply the rule to ALL existing quest prop ambushes, not just new quests. Start with roughly 5–8 enemies for regular encounters, mixing melee and ranged or including a tougher area-appropriate threat where suitable. Tune per quest rather than giving every prop identical packs.
- Change the Ashen Shrine to one wave every 2 seconds, beginning promptly. Use an encounter-specific living-enemy cap (initial proposal: 12) so pressure remains high without unlimited accumulation. After the timer, stop spawning and require clearing survivors.
- Ensure every tracker/dialogue objective gives its destination area and useful landmark, including completed quests and NPC conversations. Use one shared formatting source so future stages cannot silently omit locations.
- Update affected reminders and interaction text for the new completion rules.
- Preserve finished saved quests. Establish safe handling of active encounter stages before later content changes.

Acceptance: ignoring a summoned pack cannot complete its objective; repeating interactions cannot duplicate it; shrine defence has sustained pressure and a final clear stage; all objective states name their destination. User reviews encounter difficulty before checkpoint 2.

## Checkpoint 2 — Greenwood and graveyard variety

Proposed quest changes:

- **Bone Totems:** make them combat targets that must be destroyed, with linked defenders; complete only after both the totem and its encounter are cleared. This replaces a dialogue-button destruction with fighting a defended objective.
- **The Lost Patrol:** follow the patrol's trail between distinct search sites and find what happened, ending in a deliberate raider encounter. Quiet clue interactions do not all summon enemies. Keep Hale and Bram's memorial payoff, rewriting it to fit.
- **Bones of the Gravelord:** defeat each reliquary's assigned guardian before breaking it; use distinct encounter compositions rather than three identical surprise spawns.
- **The Lost Caravan:** investigate the wreck and hunt the enemy carrying Oda's goods, replacing three interchangeable crate ambushes. Keep the letter-to-Varek connection.
- **The Bell-Ringer:** add a main-story quest from Tobias leading into The Drowned Belfry. Connect its bell to the gathering dead before Mortis; retain compatibility for characters already beyond that part of the story.
- Revise Restless Woods / The Unquiet Dead dialogue and nearby objectives as needed to avoid repeated kill quotas dominating the opening.
- Preserve completed quest IDs and rewards where practical; map or safely restart only affected unfinished stages instead of resetting the whole quest book.

Acceptance: the opening offers combat objectives, investigation, guardian fights and a retrieval hunt with consistent save behaviour. Story text matches all changed events. User reviews before checkpoint 3.

## Checkpoint 3 — cave rescue and main-story connection

- Add one captive route-guide NPC in a readable cave encounter site, with a safe post-rescue conversation position.
- Create a staged rescue: defeat the named group of captors → free the captive → speak to them. Nearby unrelated kills must not count; reaching the NPC or clicking the cage cannot bypass the captors.
- Place this lead naturally after Bone Totems, on the route to Tobias. Rewrite Maren's handoff and Tobias's arrival dialogue so the cave discoveries advance the Hollow Call.
- The rescued guide reveals evidence of the dead being moved/called beneath the graveyard, and directs the player onward. After rescue they remain available as a cave contact with updated dialogue and map/NPC markers.
- Add a required main-story follow-up targeting Vex in The Warren, tied to the rescue and using the existing boss and den. Distinguish the objective location (The Warren) from the route through The Lost Hollows.
- Existing characters who already passed this story section retain their progress and can still access the rescue as available content; do not retroactively block later quests.

Acceptance: rescue cannot finish before the captors are dead and the conversation occurs; the NPC remains usable; the main journey acknowledges the cave; Vex is part of the main story, with clear motivation and a compatible handoff to Tobias. User reviews before checkpoint 4.

## Checkpoint 4 — ruins, frozen area and story continuity

- **Lost Supplies:** recover distinct supply types from different situations (guarded cache, wreck, enemy carrier), rather than repeating one click encounter three times.
- **The Frozen Gate:** turn the sunstones into a linked activation sequence with immediate visual feedback and explicit next-stone objectives. State the sequence clearly through Ysolde/clues; avoid a blind guessing puzzle. Remove automatic trivial beetle ambushes.
- **Lost in the Snow:** follow the scouts' journals as a route of discoveries, with a climactic fight at the final site rather than a small ambush at every body.
- Keep the Ashen Shrine as the dedicated defence quest, adjusted by checkpoint 1 feedback, rather than adding more copies of it.
- Rewrite Varek/Ysolde/Maren dialogue, boss handoffs, door inscription references and completion lines to match the revised journey and preserve the warden reveal. Resolve stale late-game dialogue around the open Shepherd door.
- Review quest dependencies, markers, destinations, reward levels and progress compatibility across the finished route. Rewards should fit current world levels, rather than retaining obsolete low-level rewards unquestioningly.

Acceptance: the later route alternates retrieval, defence, activation, investigation and bosses; dialogue and objective stages agree; the full story route includes the cave without resetting old characters. Stop for final user review.

## Checkpoint log / resume instructions

- Planning baseline: master, aaba6ca. Inspected quest definitions, prop encounters, tracker, save version, cave route and boss lairs.
- Confirmed scope added during checkpoint 1: every checkpoint must preserve a playable main questline; Vex and the Bell-Ringer must join the MAIN story (six of nine bosses). Bell-Ringer belongs to checkpoint 2, Vex to checkpoint 3.
- Checkpoint 1: committed/pushed as 7418dec. User reviewed and approved checkpoint 2, requesting three-enemy Greenwood waves.
- Checkpoint 2: COMPLETE / AWAITING REVIEW (see live handoff at the top). Checkpoints 3-4: NOT STARTED / NOT YET APPROVED.
- Checkpoint 2 final checks: forced Unity refresh and compilation finished; editor idle, not compiling/updating, Edit mode, zero console errors. Structural validation passed for all 26 quests, including valid areas and acyclic prerequisites. git diff --check passed. No gameplay checks were run; encounter feel and placement remain for user review.
- Checkpoint 2 commit title: "Rework opening quests and add guarded deep-road blockade". Find its hash with git log -- Documentation/QuestReworkPlan.md; verify remote tracking state when resuming.
- Commit: the commit containing this document, titled "Require quest encounter clears and improve objective guidance". Look it up with git log -- Documentation/QuestReworkPlan.md. Push outcome is reported in the session's final response; verify remote tracking state when resuming.

### Implemented in checkpoint 1

- QuestProp owns and subscribes to the deaths of its exact spawned enemies. Progress is awarded once the entire pack dies, never on the initial click. Ambient kills cannot replace them. Reassembling enemies must actually die, not just collapse.
- Quest-object interactions are guarded against duplicate activation. Their labels and minimap markers remain visible during combat. Tracker and reminder dialogue show encounter-specific remaining counts and destination areas.
- Regular pack sizes: totems 6 (zombies, skeletons, archer); fallen scouts 6 wolves; reliquaries 8 (including Bone Warden and Bone Reaver); caravan crates 8 (bats, spiders, Bone Reaver); supply caches 8 (hounds, knights, casters); sunstones 6 (beetles, knight, caster); frozen scouts 8 (wolves, crawlers, Rime Stalker, caster).
- Encounter compositions deliberately avoid minion summoners and splitting oozes, so every spawned combatant is owned and tracked by the objective. Later new compositions must preserve this rule or extend tracking to descendants.
- Shrine: immediate first wave; three enemies every 2 seconds, capped at 12 living/pending summons; 30 seconds inside the circle, followed by clearing all surviving summons. The circle restriction ends when the timer does. Death or leaving the area still resets an unfinished clear stage.
- Blocked spawn placements stay pending and retry each second. The rite clock starts only when combat can actually spawn. A missing enemy without a death event resets the encounter instead of granting credit or permanently blocking progress.
- Death or area departure removes living owned enemies and restores the unfinished prop for retry. Saving writes only successfully cleared props; reload restarts unfinished encounters. Finished quests and previously secured props retain their progress. QuestBook.Version stays 2.
- Shared objective formatting supplies destination areas, including talk quests, bosses, encounter stages and return trips. Emberwatch return lines now explicitly say Ashen Ruins. Old duplicated progress formatter removed.
- Updated offers/objectives, interaction warnings and patch notes to explain the completion rule.
- Main-story prerequisite IDs, objective counts, gate unlock requirements and save version remain compatible. No story content is left awaiting a later checkpoint to make the current chain usable.

### Checks and limits

- Forced Unity MCP asset refresh and requested compilation; waited for idle/readiness. No console errors. Existing obsolete Unity API warnings remain outside the changed code.
- Editor-only data validation passed for all 24 quests: unique IDs, valid prerequisites, no dependency cycles, valid destination areas, all quest-NPC return locations include an area, unchanged quest-book version.
- Small preview-scene data check passed: displaying an unfinished encounter awards/saves zero progress; prop credit is idempotent; export/import preserves a secured prop and completed predecessor; temporary encounter state does not restore; all 24 rendered objective lines include their destination area. Preview objects were removed.
- No Play-mode gameplay checks, screenshots, gauntlet or complex tests were run. Encounter feel, positioning and combat difficulty remain for the user's gameplay review, per AGENTS.md.
- Encounter composition validation passed: all eight packs use valid enemy kinds, regular packs contain 6-8 enemies, and no pack contains a boss or splitting enemy.
- git diff --check passed. Final commit and push are the completion step.
- Plan moved from ignored docs/ to tracked Documentation/ so a new session can read it from the repository.

### Exact next step

STOP for checkpoint 2 review. Do not start checkpoint 3 without user approval. No gameplay checks were requested or run. Keep these notes current. The final rework must remove temporary rework Markdown files, as explicitly requested.

Historical checkpoint 1 review guidance: Suggested player review: activate a totem and leave enemies alive; clear a pack; retry after death or area travel; reload mid-encounter; hold the shrine and clear survivors; inspect talk/boss/return objective locations. Fix any reported checkpoint 1 issue before progressing.

After checkpoint 2 review, fix any reported issues first. Only after explicit approval begin checkpoint 3: the Lost Hollows captive rescue (kill captors, free captive, talk), cave story integration and Vex main-story quest. Preserve the whole playable main route and existing save progress. Checkpoint 4 remains unapproved.
