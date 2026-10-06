# Act boss: The Shepherd / Carrion Saint - plan and progress

## Boss balance step - 2026-10-06

- The Shepherd's base hit damage is 29 instead of 44; calm movement and attack pace are 20% slower. Ranged and minion hits enrage before damage is applied, restore the old damage and pace, and refresh enrage on every hit. Enraged bosses take 20% less damage from that first hit onward.
- Carrion Saint body attacks use the same calm/enraged scaling. The detached pursuing snake head deliberately keeps fixed movement, bite timing, and damage regardless of enrage.
- Source and diff checks completed. Unity was not used at the user's request. Await player review before further Shepherd balance changes.

Working notes for the act boss, so work can resume in a new session. Last updated 2026-10-06.
Nothing of this is committed yet (all in the working tree).

## Final boss polish - 2026-10-05

- Phase-three melee reach now follows each attack animation. Pursuit cooldown starts after the previous serpent attack ends and lasts 20 seconds; its head clears the floor, its route weaves, and its jaws close before the head tilts back to swallow.
- Two animated shoulder vipers replace the wing-like back plates. Phase-two venom volleys and ground spouts now launch from their actual mouth positions. Damage passed from the pursuing head to the boss is displayed over the head after defenses.
- Carrion Saint death buckles the hind legs and lowers the upright body with shake and dust. Ordinary corpses last twice as long; boss corpses four times as long.
- Unity Editor: zero compile/runtime errors, 166/166 EditMode tests, real arena phases and final quest/loot checked. A forced Venom Volley produced three globs exactly at three mouth positions. A long pursuit recorded 25 route points with 1.01m lateral deviation; the head collider was 1.035m clear of the floor. A head hit removed 471.7 boss life and displayed 472 at the head. Final death pose had both hips bent 55 degrees and a 100-second boss corpse. All ten impact dust pieces appeared 0.20-0.31m above the arena floor. Temporary save restored and Editor stopped. No Web build, commit, or deploy.

## Latest handoff - playable release slice, 2026-10-04
User explicitly prioritized playable access/full fight over appearance and shortened rewards to TWO items. No further visual polish before shipping unless a blocking issue appears.
- Dedicated simple 82m dark-stone sanctuary at (1040,0,0), area index 5. Sparse giant ribs and green perimeter accents, clear centre. Door north of Rimeheart opens after main quest `stag` is handed in to Elder Maren; saved ID retained. Door slab disappears. New `shepherd` quest available from Maren and auto-accepted on arena entry if missed.
- Phase 1 -> grafted phase 2 -> lethal fake death -> Carrion Saint full fresh bar -> real final death. Phase-three brain uses maw bite, rear slam, lash, approach, and 12-second extending serpent pursuit on a 25-second timer. Retained body hurts; head captures, swallows and spits the player out. Head uses angular bevelled meshes instead of spheres. Phase-two snakes unchanged.
- Guaranteed final boss-exclusive unique: Shepherd's Fang (100% attack hit damage as poison over 3s) OR Widow's Choir (piercing shot level 7 and 3-second venom clouds, 1s creation cooldown). Reused existing icons/models; staff/grimoire deferred. No two-hand poison melee weapon. Both excluded from ordinary unique pools and other bosses.
- After final death, five-minute respawn. Shed Heart: 0.8% drop from Act I enemy deaths, inventory click instantly resets defeated boss, consumed only on successful use; never resets a living fight. Saved/reloaded as consumable. Leaving/re-entering cannot bypass cooldown.
- Player death returns to Frozen Hollow outside door and removes live boss; re-entry starts full phase one. Walking out similarly resets an unfinished attempt.
- Smoke verified in Editor: zero errors, real arena entry, phase 1 threshold (1940/2910), phase 2 fake death alive/immune, phase 3 Carrion Saint 2910/2910 immune false, pursuit route growing, final death quest Complete 1/1 and Shepherd's Fang loot, inventory-click heart consumed + full phase-one respawn, living-boss heart retained, dagger hit100 plus poison100, death moved player to area4 doorway (780,1.1,43) and removed boss.
- Small final fixes after smoke: hold health floor across multiple same-frame hits, auto-accept available boss quest on entry, reject repeat hearts when respawn already queued outside arena, clean cloud visuals on owner destruction. Final compile check passed with zero errors; DevTest.End restored user preferences/save; local test server PID 77060 stopped. Editor stopped.
- User will do most testing. Known shortcuts: sparse arena, reused item art, basic phase-three moves, balance/animation rough. Spectators now receive the boss phase, scale, animation clip/timing, phase-three reveal, and phase-two snake strikes; Unity compile check passed with zero errors. No live spectator play-through, Web build, commit, or deploy performed.
- New helpers: DevTest.BossArena() (fresh God test character, quest unlock, two weapons + heart, real arena), BossArenaHit(), BossArenaStatus(). Area(5) now supported. For normal quest access use real character, return to Maren after door inscription/story.

## Previous handoff - phase-three animation review, 2026-10-04

Phase-three look approved. Animation/reveal step built and Editor tested; waiting for user approval before playable phase three. No commit, Web build or deploy.

### Review correction
User found the colossal snake fling unreadable and the camera switches confusing. Revised SkyBite: one snake at a time, vertical drop from above the marked target, 3s clip, 1.25s impact, 0.4s impact hold, 0.9s recovery. SnakeLimb.StrikeFromAbove is separate; phase-two Strike unchanged. Camera changes ease over 0.5s; isolated sky-bite review avoids switching to unrelated animations. This still needs the user's renewed animation approval.

### Current Editor handoff
DevTest.BossReview3() repeats fake death/reveal and all seven clips every 24s on an isolated floor. Current review uses BossReview3(true): isolates the revised sky bite after one reveal, keeps its wide camera, and loops the attack. Once handed over, MAKE NO UNITY CALLS until the user says done or asks for intervention. Stop Play automatically calls DevTest.End to restore the original save and clear DevQuery. Local server PID 77060 (exec session 10448) remains running for this review; stop only that PID after the session ends. Backup stays in Library/DevTestPrefsBackup.txt until cleanup.

### Built and checked
- CarrionSaintReveal: old health bar empties, body falls, bar hides during silence, shudder, four robe panels tear away, chimera unfolds, low growl, fresh full bar named Carrion Saint. Pauses/resumes non-spatial looping audio if present; no new music asset.
- Fake death had zero Died events and zero new loot. EnemyHealth remains alive and immune. Actual lethal phase-two trigger is deliberately not wired until the playable phase-three step.
- CarrionSaintAnimator: idle motion and gallop; Charge, RearSlam, MawBite, TentacleLash, SkyBite, Burrow poses. Hit events ready for combat. All clips ran repeatedly without errors, all six attack event names observed. RootMotion is optional/off in the review.
- Phase-three review endpoint stays held/immune. No phase-three damage/AI/real victory yet. BossBarUI now uses EnemyHealth.DisplayName and HideBossBar.
- DevTest.BossReveal3(), BossAnim3(), BossState3(), BossReview3() are the short testing/review helpers.

### Final user scope and budget direction
- Focus on shipping, practical shortcuts, no extra polish. User most recently reported 36% remaining five-hour usage.
- Five Shepherd-only uniques: bow, dagger, staff, summoner grimoire and poison-focused amulet. No two-handed poison weapon; dagger covers melee.
- Distinct procedural serpent sanctuary arena connected to main quest. Boss door stays locked until preceding questline fulfilled. Replace old Hollow Stag quest text with Shepherd story.
- Death returns player outside entrance and fully resets boss.
- Boss respawns five minutes after defeat. Somewhat rare Act I drop from any enemy, clicked in inventory, instantly respawns dead act boss; consume only when successful, cannot reset an active living fight.

### Next
Get animation/reveal review, then build minimal playable phase three. Afterwards arena/quest/reset integration and four poison uniques + respawn consumable. Spectator boss phase, reveal, and snake animation replication is now integrated. Update release patch notes as the fight becomes production-accessible. User must authorize deploy/commit.

## Previous handoff - Codex continuation, 2026-10-04

All work is still uncommitted. No Web build or deploy. Editor is stopped; DevTest.End restored the original save and cleared DevQuery. Local test server PID 61308 was stopped. Quality was restored to PC (1).

### User steering
- Phase three changes the displayed boss name from The Shepherd to Carrion Saint, with a fresh health bar after the fake death/reveal. Explicitly reconfirmed by the user; implementation belongs to the reveal/fight integration, not the visual-only preview.
- Keep the snake theme visible on the phase-three body, not only during attacks. Added cobra mantle, green dorsal scales, amber slit eyes, and a curved serpent tail. Retained chimera legs/arms/belly maw.
- User has 43% of the five-hour usage window left and wants to ship before it runs out. Take practical shortcuts, skip extra features/polish, make a minimal complete boss after the review gates.
- User approved proceeding from the phase-three look. Animation/reveal step in progress; stop for that review before playable phase three.

### Verified
- New visual rig and helpers compiled; zero console errors.
- Corrected phase-one charge: full 12.60m travel in clear space; actual warning (4.50, 0.01, 13.80). Stops at a test wall (3.92m travel).
- Real forced charge damage: at 10m along and 2.1m lateral, 149.69 damage; at 3.2m lateral, zero; at 14.5m along, zero. God-mode regeneration had to be disabled during measurement so it did not erase damage.
- Normal bite movement still respects its assigned MinGap: test starting 3.2m away traveled 0.45m and stopped at 2.75m (the helper assigned phase-one MinGap before instant phase two; production phase two assigns 3m). Do not interpret this as a production phase-two gap change.
- Phase-three feet grounded by visible bounds (bottom approximately 0). Body and serpent previews visually inspected; Mobile quality 0 also checked.
- Giant serpents remain hidden in the body preview and belong only to eventual phase-three attacks. Two preview rigs, each 48m long / 7.2m root diameter. Phase-two snakes unchanged.

### Files and review artifacts
- CarrionSaintLook.cs: visual draft plus snake motifs, grounded feet, more separated giant serpent poses.
- DevTest.BossMotionCheck("clear"/"wall"/"inside"/"outside"/"beyond"/"bite") and BossMotionResult(): repeatable isolated-floor checks. BossLook3View hides loot clutter for review.
- Body: Assets/Screenshots/screenshot-20261004-133143.png (Mobile quality).
- Giant scale preview: Assets/Screenshots/screenshot-20261004-133030.png.
- Patch notes already contain the verified phase-one charge change. Visual-only phase-three draft is not a production player-visible feature yet.

### Next
Implement the approved minimal animation/reveal step and stop for that review. Preserve the occasional double bite and the phase-two snake proportions. Keep the remaining arena/fight integration focused on shipping.

## Shipping scope addition - 2026-10-04

User explicitly wants a visually distinct boss arena and boss-exclusive poison rewards covering the classes before shipping. Keep the existing phase-three step gates; this is added shipping scope, not approval of the phase-three look.

### Arena direction
Main-quest integration required: the door stays locked until the preceding questline is fulfilled. Act boss respawns five minutes after defeat. A somewhat rare Act I drop from any enemy can be clicked in the inventory to respawn the dead act boss immediately; consume only when that reset succeeds.
A ruined serpent sanctuary beyond the existing story door: circular cracked dark-stone floor, colossal fossil ribs/coiled serpent remains framing the perimeter, broken shrine at the far end, sickly green pools outside the fighting floor, restrained hanging lanterns. Clear playable centre, readable telegraphs; procedural geometry and existing materials. Quest-gated entrance, death returns outside and resets the complete fight. Update the old Hollow Stag story/door text to match the Shepherd.

### Proposed rewards (names/details provisional)
Guarantee one random item from a five-item Shepherd-only pool on full final-phase victory. Exclude these from ordinary unique rolls and other bosses; no rewards for fake death. The completed pool consists of skill-granting bow, dagger, staff and grimoire rewards plus a poison-focused amulet.
- Bow, Widow's Choir: exclusive Venom Arrow, piercing shot leaving a small poison cloud.
- Dagger, Shepherd's Fang: exclusive Fang Strike, quick melee strike with stronger poison.
- No two-handed poison weapon: user explicitly removed it; dagger covers melee.
- Staff, Crook of the Last Shepherd: exclusive Venom Spout, targeted poison eruption using the boss's existing visual language.
- Grimoire, Book of Shed Skin: exclusive Summon Viper, small serpent minion applying poison; compatible with existing minion investment.
- Amulet, Widow's Brood: poison damage, damage over time, poison resistance and penetration, plus venom clouds.

Boss poison currently targets PlayerStats. Player poison against enemies and boss-only unique pooling still need implementation; these are not already supported. Balance/item names are not finalized. Reuse gear art and green tint for first ship; custom icons/models can wait.

## Previous handoff - Codex, 2026-10-04

User is opening a new session. All work remains uncommitted. No Web build or deploy was made.

### User decisions in this session
- User watched phase two and liked the occasional double bite. It is intentional: 40% choice unless the preceding move was DoubleBite. Preserve it.
- User requested a much wider, longer charge and explicitly clarified this means **The Shepherd himself lunging in phase one**, NOT phase two's diving snake.
- User then authorized moving on to **phase-three look** after finishing the charge.
- **Absurdly big snakes ONLY in phase three, in some attacks.** The existing phase-two ground-jumping snake should look skinny by comparison; do not enlarge phase-two snakes.
- Keep the original step gates: finish/show phase-three look, get user review, then animations (including fake death/reveal), then playable phase three. No phase-three attack implementation yet.

### Charge changes made, verification incomplete
- `ShepherdFight.cs`: LungeLength 6.3 -> 12.6 (now internal const shared by animation); LungeWidth 1.5 -> 3. Phase-one actual warning width 4.5m and length 13.8m including crook reach. AI activation distance follows new reach.
- `ShepherdAnimator.cs`: CobraLunge keyframes travel 12.6m, same quick timings. Ordinary attack movement still stops at MinGap; CobraLunge now bypasses that artificial stop.
- Compiled and checked initial version: no console errors, runtime warning measured (4.50, 0.01, 13.80), compiled final keyframe Advance=12.6.
- Runtime travel check initially measured only 7.25m from a 10m starting distance. Found an editing mistake: the MinGap conditional had been removed while its body remained unconditional. Fixed it to `if (advance > 0f && Target != null && Root != null && clip.Name != "CobraLunge")`.
- **The corrected movement has NOT been retested.** Verify unobstructed full travel, collision behavior, warning/damage alignment, and normal bites retaining their gap. Latest compile result was not collected.
- `Assets/Resources/PatchNotes.txt` has a charge note; lowercase `version:` preserved. Do not mark task DONE until checks pass.

### Phase-three visual draft added, NOT reviewed or tested
- New `Assets/Scripts/Enemies/CarrionSaintLook.cs` (Unity generated its .meta).
- Standalone procedural visual rig: stretched beast hindquarters, backward hocks/claws, four grafted human arms used as legs with fingers, vertical toothed belly maw, split skull, pale seam tentacles/stitches, exposed ribs/spine.
- Hides old model children; disables old walk/Shepherd animator; uses scale 3. Named joints intended for later animation work. No phase-three combat, fake death, transition, new health bar, or production fight entry yet.
- Two separate colossal serpent rigs built using SnakeLimb, hidden by default for eventual attack use. Draft dimensions at scale 3: 48m length and 7.2m root diameter each (4x phase-two diving snake thickness). These are draft proportions, not visually validated.
- `DevTest.BossPhase(3)` now holds the boss, makes it immune, builds the visual draft, and calls new `DevTest.BossLook3View()`.
- `BossLook3View(false)` frames body; `BossLook3View(true)` shows and frames giant serpent scale preview. Disables CameraFollow while previewing; ending Play resets it. The preview does NOT set ShepherdFight.Phase=3 or enable combat.
- Check compile, model grounding, silhouette, serpent poses and camera framing in Editor before showing user. The giant rigs start hidden; bounds may need a frame to settle after enabling before framing again.
- Last tool call applied DevTest edits successfully, then called refresh_unity, but user interrupted before completion/result. Do not assume compilation succeeded.

### Environment and session state
- Native UnityMCP tools worked through `tools.mcp__UnityMCP__execute_code`, manage_editor, read_console, refresh_unity. Verified project C:/Users/admin/poe-clone/Assets, Unity 6000.6.3f1.
- Read `.claude/skills/unity-playtest/SKILL.md` before using Unity. Use DevTest helpers. No Unity calls after handing a Play session to user until they finish/request intervention.
- Editor was explicitly STOPPED after the charge test, before phase-three edits. No play session has been handed over since.
- Local test server was started on port 8099 using exec session 83531; it may still be running. PID not reliably identified. Do not kill all node processes. Reuse if listening or start a new local server and record its PID.
- DevTest.Begin was called; `Library/DevTestPrefsBackup.txt` may still exist, DevQuery points to ws://localhost:8099. DevTest.End has NOT been called. Finish test cleanup according to project instructions; do not overwrite the original backup on resumption.
- User requested and approved editing C:/Users/admin/.codex/config.toml: `[mcp_servers.UnityMCP]` now has `default_tools_approval_mode = "approve"`; user restarted Codex.
- Python command is unavailable in this shell. PowerShell UTF8Encoding(false) ReadAllText/WriteAllText was used to preserve existing content. Some apply_patch edits introduced LF amid CRLF; avoid needless whole-file formatting changes.

### Next actions
1. Check compilation and retest the corrected phase-one charge in Editor.
2. Test/refine phase-three visual draft and inspect screenshots; only then show user body and colossal-serpent scale preview.
3. Update patch notes if needed and mark/move completed todo items only after actual verification. Stop for phase-three look approval before animations.

## How we work (user's rules)
- Strictly in order, and **stop for the user's confirmation after each step**:
  phase N look -> phase N animations -> playable (ugly) phase N; then the next phase. Polish/looks last.
- Avoid feature creep. Cheap procedural shapes (RuntimePrimitives) are fine until the polish pass.
- The user wants him **fast** (player has dash and teleport): short wind-ups, quick animations.
- **Never make Unity MCP calls while the user is playing a handed-over Play session**: each call stalls
  the Editor ~1.3s and reads as a game freeze. Verify everything *before* handing over.
- Test in the Editor (see `.claude/skills/unity-playtest`), with `DevTest` one-liners (below).

## The fight (design, agreed)
- Own arena, unlocked only after the storyline quests (story text still says "Hollow Stag" - rewrite to the
  Shepherd later). Player death -> back to the waypoint, boss fully healed and reset. Intro: the boss says a
  few lines in speech bubbles. Boss immune during the intro and phase transitions. Total length about like
  current bosses (aim ~30-40s a phase).
- **Phase 1 - The Shepherd** (modest): stooped hooded old man, long robe to the floor (no legs: he glides),
  faceless shadow in the hood, crook that is a dried snake (its head is the hook; it twitches now and then),
  lantern in the left hand. Inspiration: Maliketh's Beast Clergyman.
- **Transition 1 (at 66% life)** - Godrick graft + Maliketh dropping the disguise. Must be clear and cool:
  shudder, plant the crook, tear off his own lantern arm (arm + burning lantern fly off, blood, shake),
  pull the crook out and drive it into the stump, hood/mantle burst off in pieces, crook grows into the
  living snake arm, back snakes sprout, he grows to 2x size, roars. ~4s, immune.
- **Phase 2** (2x size of phase 1 = Scale 3; camera pulls back 1.5x): bald seamed head, slit eyes, cobra
  hood, grafted snake arm (bites), three snakes on his back (Messmer), robe still on below the waist.
  **Poison on every hit from phase 2 on.** All AoEs/reaches 2x.
- **Phase 3 - "Carrion Saint"** (surprise): fake death at the end of phase 2 (bar empties, music stops,
  pause), then the robe tears away and a **new health bar** "Carrion Saint" appears. Reveal is a disturbing
  **chimera**, NOT just snakes (user found all-snakes boring): backward-bending beast hindquarters that were
  folded under the robe (why he glided), victims' human arms grafted on his flanks walking as extra legs,
  a vertical toothed maw opening in the belly, pale tentacles from the stitched seams, the seamed head split
  fully open. 2-3 **colossal serpents (3x anything so far, Messmer phase 2)** burst from his back and tunnel
  under the arena: dive arcs across the arena (path shown on the ground), burrow-snatch (ground cracks,
  head erupts under you), sky bite (rears up, shadow grows, slams down). He gallops fast (Ludwig), charges,
  rears and slams, belly maw bites close, tentacles lash. Poison continues.

## Done (in the working tree)
- `Scripts/Enemies/ShepherdLook.cs` - phase-1 look (robe skirt mesh, hood, crook with jointed curl,
  lantern + flickering light), `ToPhase2` (disguise off, head/hood/eyes, snake arm, back snakes, 2x scale,
  lantern dropped). `BaseScale` 1.5, `Phase2Scale` 3.
- `Scripts/Enemies/ShepherdAnimator.cs` - keyframed pose clips (body, arms, crook angle + curl, sink, root
  motion) played over the walk animator. Clips: Sweep, Jab, Slam, HookPull, CobraLunge, SerpentCall, Bite,
  DoubleBite. Root motion scales with size and stops `MinGap` short of the player.
- `Scripts/Enemies/ShepherdFight.cs` - the fight brain. `Pace` 1.6 (all clips/wind-ups). Phase 1: crook
  basics + specials Hook&Pull (yanks player in, then Sweep), Cobra Lunge (line telegraph, dive), Serpent's
  Call (6 telegraphs, fat ground snakes burst up). Phase 2 (`EnterPhase2`): Bite / DoubleBite with the snake
  arm (no telegraph - readable enough), Cobra Lunge + Serpent's Call kept until phase-2 specials exist;
  poison on hits; camera zoom while giant; stand-off distance via `EnemyController.StandOff`.
- `Scripts/Enemies/SnakeLimb.cs` - jointed snake (rest S-coil + ripple), wedge head with hinged jaws, slit
  eyes, fangs; `Strike()` = draw back low, level lunge, body follows by FABRIK (fixed length, no stretch).
  Runs at execution order 1002 (after the body animators).
- `Scripts/Enemies/GroundSnake.cs` - snake bursting from the ground (Serpent's Call).
- `GroundTelegraph.RunLine` (charge strip). `CameraFollow.Zoom`. `PlayerStats.Poison` (stacks, ignores
  mitigation, green numbers) and `TakeHit` now returns whether it landed. `EnemyController.StandOff` and a
  general rule: no enemy walks closer than its own radius + 0.8.
- `CharacterWalkAnimator.Hunch`. `BossStyle.Shepherd`, kind "The Shepherd" (placeholder 600 life).
- `EnemyHealth.Immune` and `EnemyHealth.Floor` (just added, for phase changes).
- `Scripts/Visuals/Debris.cs` (just added): flung pieces with gravity, tumble, land, optional lifetime.

## Transition 1 (the graft) - built, waiting for the user's review
Built as planned below (`ShepherdFight.BeginPhase2` / `GraftTransition` / `GraftStep` / `TearArm` / `Graft` /
`Unfold`, clip "Graft", `Debris`, `EnemyHealth.Immune/Floor`, `DevTest.BossTransition()`). Verified: the
threshold holds at 66%, immune 4.1s, arm torn at 1.55s, growth 2.9-3.8s, phase 2 then bites. Clones copy
their renderers' colour property blocks (else they turn white). Original plan:
- ShepherdAnimator clip "Graft" (~4.1s, played at speed 1) with event times as `Hits`:
  0.85 plant crook (reparent crook to the world, upright), 1.55 rip (hide ArmL, spawn an Instantiate'd copy
  of ArmL - lantern included, UprightStaff removed - thrown as Debris that stays; blood = small dark-red
  spheres as Debris; camera shake), 2.15 take crook back (reparent to Socket_MainHand), 2.65 graft (crook to
  the left shoulder on UpperOffset at (-0.52,1.50,0), pointing out; destroy its UprightStaff; anim.Crook =
  null; shake), 2.9 grow (clone the "Disguise" parts as flying Debris, call ToPhase2, then animate root
  scale 1.5->3, snake arm scale 0.25->1, back snakes 0->1, BodyPitchOffset 0 -> -Stoop; hide the crook),
  3.6 roar (EnemyController.PlayEnrageStart, shake).
- ShepherdFight: phase 1 sets `health.Floor = Max * 2/3`; at the threshold start the transition (Immune on,
  AI busy because the clip plays), afterwards Immune off, Floor 0 (later: phase-3 threshold).
- DevTest: `BossTransition()` to trigger it; keep `BossPhase(2)` as the instant switch.
- Clean up the torn arm etc. when the boss is destroyed.

## Also changed (whole game): enrage
Enemies now enrage when hit from 3m+ away (was 6m), and an enrage spreads: every enemy within 8m is
aggroed and enraged too (not chaining further). `EnemyController.Enrage(spread)`.

## Phase-2 specials - built, all four verified firing in game; waiting for the user's review
(user: phase 2 felt too same-y as phase 1; Cobra Lunge must come out faster)
Phase 2 drops the phase-1 specials (Cobra Lunge, Serpent's Call, Hook&Pull) and gets its own, on timers,
with Bite/DoubleBite as basics:
- Venom Volley (clip "Spit"): the 3 back snakes strike forward and spit venom globs (VenomGlob: arcing
  lob, small warning circle, poison puddle that poisons while you stand in it).
- Venom Spouts (clip "SerpentCall"): 3 GroundSnakes burst up around the player, each spits twice, sinks.
- Burrow Snatch (clip "Burrow"): snake arm dives into the ground; a circle tracks the player ~0.9s,
  locks, then a big snake erupts there (damage + poison).
- Serpent Dive (clip "SerpentCall"): DivingSerpent - a huge snake bursts from the ground on one side of
  the player, arcs over, dives back in on the other; line telegraph first.
- Cobra Lunge clip shortened: launches ~0.35s after start (was ~0.53s).

## Review adjustment (2026-10-04)
- Phase-1 Cobra Lunge: doubled width (4.5m in phase 1), travel 6.3m -> 12.6m; warning and damage reach follow the longer animation. Commits to the full charge instead of stopping short of the player; quick wind-up retained.

## Still to do (in order)
1. Phase-one charge adjustment verified. Transition/phase-two demo was shown; user authorized moving on.
2. Phase-2 polish if asked.
3. Phase 3: finish and review visual draft -> animations (incl. the fake death + robe reveal) -> playable.
4. Arena (own area, gate after the storyline), intro speech bubbles, death -> waypoint + full boss reset,
   phase thresholds/new health bar UI, balance (life, damage; ~30-40s a phase), story text rewrite.
5. Polish pass (models, effects, sounds). Then commit (user decides when).

## DevTest helpers (Assets/Editor/DevTest.cs)
`Boss(dist)` spawn in front of the player; `BossHold(bool)`; `BossMove("Bite", freezeAt)` force a move
(manual mode, `"auto"` / `"resume"`); `BossPhase(2)` instant phase 2; `BossAnim`, `BossPose`,
`BossLineup("Jab@0.5,...")`, `BossDemo()`.

## Previous Claude session ended mid-check (historical)
Outline fix done and verified in phase 1 and phase 2: clones follow their part's visibility, Outline.Rebuild() on
ToPhase2, robe skirt one-sided (outward winding). Poison puddles 1.6x radius. Nothing committed.
