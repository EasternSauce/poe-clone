# Offline skill balance testing

The recorded survey figures predate the current uniform boss combat model; regenerate the survey and workbook before using them for new balance decisions.

Current status: the sampler now benchmarks **boss single-target DPS**. Fresh full surveys optimize equipment against the boss target model; old normal-enemy results cannot be refreshed into this model. Iterative tuning results and remaining outliers are recorded in `BALANCE_ITERATION_RESULTS.md`.

## Current sampler

- Catalogue: 24 main skills, each with a flexible tree, six destination routes and six compatible hybrids; six additional support/utility entries. Definitions come from the live weapon trees in `Assets/Editor/BalanceArchetypes.cs`.
- Levels: every character level from 1 through 34. Weapon rank is `1 + (level - 1) * 9 / 33`, using integer division. A specialized route is unavailable until its required connected nodes fit. Flexible routes appear from level 1 because they require no particular destination. Early developing versions of specialized routes are not measured separately.
- Sampling: 20 deterministic builds per available archetype/level, default seed `20261014`. Random connected character passives and legal weapon allocations pay travel costs. Blood Magic and Mind over Matter are excluded until coupled life/mana spending is modeled.
- Equipment: no uniques; roll 20 rares per slot and greedily choose for 30-second sustained DPS, including mana and a penalty for insufficient melee survival. Respect attributes, levels and hand compatibility. This naturally values mana on hungry builds and defences on melee builds; it is not a global gear optimization.
- Encounter: stationary single target, ranged distance 6, 30 seconds. Gravelord Mortis, Ashen Warlord and Rimeheart have equal one-third weight at every level 1–34, using actual level scaling, armour and resistances. These are continuous scaled references, not campaign appearance levels. No clearing, movement, phases, special moves, kill effects or potions. All builds face the same boss defences, basic-hit damage and attack rate.
- Mechanics: damage/defence functions are reused where available. Poison stacks, ignite refreshes, ordinary projectile fans cannot shotgun, and rain uses one guaranteed centered hit per salvo plus expected overlap from the remaining scattered arrows. Rain targets are stationary points; collider size and movement are not simulated. These remain approximations rather than a gameplay simulation.
- Supports: Death Mark contributes its damage and six-second refresh mana only when actually rolled on the equipped grimoire. Summon main skills roll exclusively on sceptres; grimoires supply support abilities and stats. Other optional support rotations are not automatically assumed.

## Reading the workbook

The headline DPS is the arithmetic mean of the best `ceil(viable builds * 0.2)` builds by 30-second sustained DPS. Melee and Piercing Shot DPS are divided by 1.5 and minion DPS by 1.2 to allow their requested damage premiums. Each level's baseline is the median of each main skill's best available adjusted archetype.

**100% means equal to that baseline; 200% means twice it; 50% means half.** These percentages are relative balance comparisons, not percentage increases or probabilities. Review flags use a 75%-125% band. A low utility-route result does not automatically justify a buff. The baseline is recalculated at every level, so also compare absolute DPS.

The workbook has Skill overview, Archetype comparison, DPS by level, Mana consumption, Survival, Archetype catalogue, Method and Boss targets sheets. Boss targets exports each reference’s scaled life, armour, resistances, basic-hit damage and attack rate.

Mana consumption shows cost per payment, maximum spam demand, actual main/support rotation demand, pool, regeneration, net drain, time to empty, infinite sustain and 30-second use fraction. Summons also show initial army cost and replacement demand. Mana columns belong to one real sampled build nearest the top cohort's mean; they are not averages assembled from different characters. When no build is viable, the strongest rejected build is shown for diagnosis. Support-only entries use an unequipped character reference and are not DPS-ranked.

Melee viability currently requires at least four average unavoided hits and six seconds against one freely attacking average reference boss. Recovery includes regeneration, on-hit recovery and leech; mitigation includes armour, resistances, expected evasion and block. Rejected builds do not enter the top cohort. Minions use estimated lifetimes and replacement uptime, including deaths, expiry, summon batches, cast rate and mana. One boss’s basic-attack pressure is shared evenly across the targetable army for expected attrition. Wolves are untargetable. No boss splash, changing aggro, positioning or special attacks are simulated. Boss pressure uses actual Tempo/AttackCooldown; it is a stationary stress scenario, not a full encounter prediction.

## Boss benchmark and limitations

Model: `boss-single-target-v2`. Each boss has one-third weight independent of its zero normal spawn weight. Physical penetration is clamped to 0–100%; elemental/poison penetration uses runtime resistance math. Shock changes the hit before armour. Culling, flinching, first-strike bursts and on-kill benefits receive no sustained boss DPS credit. The benchmark excludes phase-specific wards.

All levels use the same three boss identities. Campaign boss checkpoints, other bosses, movement, phases, dodge timing and special attacks remain future validation work. The pressure scenario counts only freely attacking basic hits. Equal-weight aggregate DPS does not establish equal performance against each individual boss, and very low-DPS utility destinations need their coverage/control judged separately. Poison/ignite ramp, secondary effects and average proc rates remain approximations.

Each completed full survey also exports `BalanceResults/recalculation-pool.json`: viable samples with at least 75% of their own archetype/level’s best sustained DPS, excluding invalid/zero observations. The full raw survey stays intact. This is a shortlist for analysis, not a lossless equipment cache; gear descriptions round modifiers. Every tuning pass should reroll the full deterministic population so old optimization or pruning does not hide newly competitive builds.

### Historical normal-enemy flexible-tree review

The earlier normal-enemy survey had 456 skill/level comparisons where both flexible and specialized routes have viable cohorts. Flexible trees beat the best specialized route in 24 cases. The largest lead is Pulverize at level 23 (+12.8%); at level 34 only Split Shot leads (+6.0%, versus Rapid Release + Hunter's Sustenance). Check these again against bosses and show the flexible-versus-best-specialized margin in the next report. Flexible routes avoid terminal destinations but can still allocate notables.

These comparisons use independent gear/passive samples, not matched equipment, so modest leads may be sampling variation. Single-target DPS also omits coverage benefits. Before tuning a keystone, compare allocations on matched equipment/passives and check mana and survival; do not assume every utility keystone must beat flexible damage.

## Running and exporting

In Unity Edit mode, run `OfflineBalanceTests.FullSurvey_ExportsEveryArchetypeAtLevelsOneThrough34`, or choose **PoeClone > Balance > Run offline single-target survey**. This does not enter gameplay or change a character. The full survey can take roughly 18 minutes on this workstation.

`PoeClone.EditorTools.OfflineBalance` exposes `Start(samples: 20, seed: 20261014)`, `Status()`, `Cancel()` and `ValidateReportFile()`. `RefreshSkills("PiercingShot,VenomSlash")` retests selected skills using saved settings and seeds, then rebuilds peer baselines; use it only when the target model is unchanged. It rejects old normal-enemy surveys. Full iterative tuning runs use the balance unit test, not a pruned shortlist.

Run `node tools/balance/report.mjs` from the project root to export the spreadsheet and CSV. It requires `@oai/artifact-tool`; in the Codex environment the tool's ignored `node_modules` junction points to the supplied runtime dependencies. An alternative results folder can be passed as the first argument and a report filename as the second. Add `--no-previews` after those arguments to export only the workbook/CSV without rendering preview PNGs. Use a Node runtime compatible with the installed artifact-tool native renderer (Windows Node for this workstation’s cached dependencies).

Outputs are local and Git-ignored:

- `BalanceResults/survey.json`: model, settings, explicit boss targets/states, catalogue, aggregates and raw samples.
- `BalanceResults/skill-balance.xlsx`: readable comparison and mana sheets.
- `BalanceResults/sample-builds.csv`: full equipment, allocations and observations.
- `BalanceResults/recalculation-pool.json`: retained competitive samples for future recalculations, with cutoff and source metadata.

Implementation: `Assets/Editor/OfflineBalance.cs`, `Assets/Editor/BalanceArchetypes.cs`, `Assets/Tests/EditMode/OfflineBalanceTests.cs`, and `tools/balance/report.mjs`.

Pulverize's tree has since been rebalanced to move early utility/penetration into terminal routes. Early penetration is 50% after the notable, and either full penetration route reaches 100%. Worldshaker's area rises to 65%, Heavy Impact to 35% increased damage, Bonecrusher to 35% splash and Steady Blows to 20% increased speed. Node identities and allocations are preserved. The flexible-tree margins above describe the pre-change survey; refresh Pulverize and regenerate the candidate pool before using these samples for future recalculations. Subsequent boss tuning passes are documented in `BALANCE_ITERATION_RESULTS.md`; use the latest boss survey rather than these older normal-enemy margins.

Rain of Arrows' Broad Coverage destination is now Pinning Rain, preserving its node ID, spread and arrows while adding guaranteed chill (50% movement slow, base duration 2 seconds, refreshed by hits). It still deals physical damage unless another source converts it. Fresh full boss surveys include both of these changes and the later tuning passes. The stationary DPS model does not value movement control as extra damage.
