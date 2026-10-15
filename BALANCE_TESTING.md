# Offline skill balance testing

Current status: the sampler and spreadsheet export are implemented and validated. The existing report uses normal enemies. The next change is to benchmark **boss DPS**; boss calculations have not been implemented yet. Do not apply balance changes from the old report before rerunning against bosses.

## Current sampler

- Catalogue: 24 main skills, each with a flexible tree, six destination routes and six compatible hybrids; six additional support/utility entries. Definitions come from the live weapon trees in `Assets/Editor/BalanceArchetypes.cs`.
- Levels: every character level from 1 through 34. Weapon rank is `1 + (level - 1) * 9 / 33`, using integer division. A specialized route is unavailable until its required connected nodes fit. Flexible routes appear from level 1 because they require no particular destination. Early developing versions of specialized routes are not measured separately.
- Sampling: 20 deterministic builds per available archetype/level, default seed `20261014`. Random connected character passives and legal weapon allocations pay travel costs. Blood Magic and Mind over Matter are excluded until coupled life/mana spending is modeled.
- Equipment: no uniques; roll 20 rares per slot and greedily choose for 30-second sustained DPS, including mana and a penalty for insufficient melee survival. Respect attributes, levels and hand compatibility. This naturally values mana on hungry builds and defences on melee builds; it is not a global gear optimization.
- Encounter: stationary single target, ranged distance 6, 30 seconds. Current enemy reference is spawn-weighted **normal enemies**, excluding bosses. No clearing, movement, phases, kill effects, potions or enrage.
- Mechanics: damage/defence functions are reused where available. Poison stacks, ignite refreshes, ordinary projectile fans cannot shotgun, and rain uses expected impact overlap. These remain approximations rather than a gameplay simulation.
- Supports: Death Mark contributes its damage and six-second refresh mana only when actually rolled on the equipped grimoire. Summon main skills roll exclusively on sceptres; grimoires supply support abilities and stats. Other optional support rotations are not automatically assumed.

## Reading the workbook

The headline DPS is the arithmetic mean of the best `ceil(viable builds * 0.2)` builds by 30-second sustained DPS. Melee DPS is divided by 1.5 and minion DPS by 1.3 to allow their requested damage premiums. Each level's baseline is the median of each main skill's best available adjusted archetype.

**100% means equal to that baseline; 200% means twice it; 50% means half.** These percentages are relative balance comparisons, not percentage increases or probabilities. Review flags use a 75%-125% band. A low utility-route result does not automatically justify a buff. The baseline is recalculated at every level, so also compare absolute DPS.

The workbook has Skill overview, Archetype comparison, DPS by level, Mana consumption, Survival, Archetype catalogue and Method sheets.

Mana consumption shows cost per payment, maximum spam demand, actual main/support rotation demand, pool, regeneration, net drain, time to empty, infinite sustain and 30-second use fraction. Summons also show initial army cost and replacement demand. Mana columns belong to one real sampled build nearest the top cohort's mean; they are not averages assembled from different characters. When no build is viable, the strongest rejected build is shown for diagnosis. Support-only entries use an unequipped character reference and are not DPS-ranked.

Melee viability currently requires at least four average unavoided hits and six seconds against one freely attacking average normal enemy. Recovery includes regeneration, on-hit recovery and leech; mitigation includes armour, resistances, expected evasion and block. Rejected builds do not enter the top cohort. Minions use estimated lifetimes and replacement uptime, including deaths, expiry, summon batches, cast rate and mana. Wolves are untargetable. This does not simulate enemy target selection or positioning.

## Next step: boss DPS

Replace the normal-enemy damage reference with an explicit, reproducible boss benchmark while keeping the model simple. This is the implementation plan, not a description of the current report:

1. Define the boss selection and weights explicitly. Bosses have zero normal spawn weight, so the existing spawn-weighted average cannot be reused. Use actual boss definitions and level scaling from `EnemyKind.cs`; record boss identity and state in the report. A scaled reference boss across levels 1-34 can provide a continuous curve, with campaign-boss checkpoints as separate comparisons.
2. Apply boss armour, resistances, penetration and damage-taken rules before ranking builds. Respect boss-specific restrictions such as culling immunity. Handle enrage explicitly: eligible ranged/minion hits trigger it, and boss damage-taken modifiers can change outgoing DPS. Do not apply a blanket multiplier to every archetype.
3. Define a consistent boss-pressure scenario for melee survival and minion replacement. Use actual scaled attacks, attack tempo, mitigation and recovery, with explicit enrage state. Show these assumptions separately from damage so freely attacking boss pressure is not mistaken for a prediction of a complete fight.
4. Keep the existing mana sheet, 30-second window, rare-item budget, top-20% calculation and melee/minion premiums. Continue to exclude clearing, pathfinding and detailed phase simulation.
5. Version the model and export target assumptions into the workbook. Re-sample all builds: boss defences change which items and allocations win. Do not mix old normal-enemy rows with new boss rows or merely rescale the old spreadsheet.
6. Validate boss defence/enrage handling, mana reconciliation, survival exclusions and archetype coverage with focused Edit-mode tests. Run a fresh full survey and regenerate the workbook before recommending buffs or nerfs.

Still to decide during implementation: reference boss versus a small weighted boss set, campaign checkpoint selection, and which explicit boss attacks represent survival pressure. Keep those choices visible in Method rather than hiding them behind an unexplained average. Any HP/DPS kill-time estimate must be labelled stationary and omit phase downtime.

## Running and exporting

In Unity Edit mode, run `OfflineBalanceTests.FullSurvey_ExportsEveryArchetypeAtLevelsOneThrough34`, or choose **PoeClone > Balance > Run offline single-target survey**. This does not enter gameplay or change a character. The full survey can take roughly 18 minutes on this workstation.

`PoeClone.EditorTools.OfflineBalance` exposes `Start(samples: 20, seed: 20261014)`, `Status()`, `Cancel()` and `ValidateReportFile()`. `RefreshSkills("PiercingShot,VenomSlash")` retests selected skills using saved settings and seeds, then rebuilds peer baselines; use it only when the target model is unchanged.

Run `node tools/balance/report.mjs` from the project root to export the spreadsheet and CSV. It requires `@oai/artifact-tool`; in the Codex environment the tool's ignored `node_modules` junction points to the supplied runtime dependencies. An alternative results folder can be passed as the first argument.

Outputs are local and Git-ignored:

- `BalanceResults/survey.json`: settings, catalogue, aggregates and raw samples.
- `BalanceResults/skill-balance.xlsx`: readable comparison and mana sheets.
- `BalanceResults/sample-builds.csv`: full equipment, allocations and observations.

Implementation: `Assets/Editor/OfflineBalance.cs`, `Assets/Editor/BalanceArchetypes.cs`, `Assets/Tests/EditMode/OfflineBalanceTests.cs`, and `tools/balance/report.mjs`. No balance buffs or nerfs have been applied from this survey.
