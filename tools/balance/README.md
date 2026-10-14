# Offline single-target balance survey

Run `OfflineBalanceTests.FullSurvey_ExportsEveryArchetypeAtLevelsOneThrough34` in the Unity Edit-mode test runner, or use **PoeClone → Balance → Run offline single-target survey**. Neither enters gameplay or changes a character. `OfflineBalance.Status()` reports progress; `Cancel()` stops a menu run. The test also validates coverage and reconciles the exported observations.

The catalogue comes first: all 30 skills, including the six support/utility skills, and all six weapon-tree destinations plus every compatible combination that fits ten points. Character passive allocations are separate random dimensions within each route. Paths pay their travel costs and use the actual allocation APIs. Blood Magic and Mind over Matter paths are excluded from this version because they need coupled resource simulation. Clearing benefits are outside the model.

The default run uses 20 seeded builds per available archetype and character level, at every level from 1 to 34. `Start(samples: 100, seed: 20261014)` produces a larger survey. Each build rolls 20 rares for every equipment slot, with no uniques. Slot selection maximizes 30-second DPS including mana, with a penalty until melee survival thresholds are met. This is a greedy slot-by-slot search, not a global optimization. Invalid gear requirements and hand combinations are rejected. The newest wearable base in each item line is eligible.

After changing a particular skill, `OfflineBalance.RefreshSkills("PiercingShot,VenomSlash")` retests only its routes using the saved settings and seeds, then rebuilds all peer baselines. Support skills have mana references for an unequipped character; they are not ranked for DPS.

The weapon-rank scenario grows from rank 1 at character level 1 to rank 10 at 34. Destinations are marked unavailable until their required points fit; this is a scenario assumption, not an actual equip restriction. Baseline routes spend points without choosing destinations. Hybrid names identify their required destinations; remaining points vary legally without adding a third destination.

DPS is single-target damage after normal-enemy defences, using the game's stat and defence functions wherever available. Poison stacks, ignite refreshes rather than stacking, extra bolts cannot shotgun, and rain uses expected impact overlap. The report documents the remaining approximations. The headline is the arithmetic mean of the best `ceil(viable builds / 5)` builds by 30-second sustained DPS. Melee results divide by 1.5 and minion results divide by 1.3 before comparison with the median of each skill's best archetype at that level. Review flags use a ±25% band. Low damage on a utility route does not automatically imply a buff.

Melee must survive at least four average unavoided hits and six seconds of one freely attacking average normal enemy. Expected evasion/block, armour, resistances, regeneration and on-hit recovery/leech are included. Minion replacement demand includes expiration, deaths, summon batches, the main-skill cast cooldown, and mana. Wolves are untargetable. Death Mark is refreshed every six seconds only when granted by the actual selected grimoire; its mana cost and damage bonus are included. Optional other support rotations and kill-triggered recovery are not assumed.

Results are local and ignored by Git:

- `BalanceResults/survey.json`: settings, catalogue, aggregate rows and every raw build.
- `BalanceResults/skill-balance.xlsx`: readable report with separate DPS, mana, survival and catalogue sheets.
- `BalanceResults/sample-builds.csv`: complete raw build records, including item modifiers and passive allocations.

After the survey, build the workbook with `node tools/balance/report.mjs` from the project root. The builder requires `@oai/artifact-tool`. In the Codex environment, connect this folder's ignored `node_modules` junction to the provided artifact-tool runtime dependencies. You can pass an alternative results folder as the first argument.

Mana values in the workbook describe an actual representative build nearest its top cohort's mean, so the costs, pool and regeneration belong to the same character. The sheet distinguishes full-speed spam, the rotation's demand, support costs, net drain, time to empty, infinite sustain and 30-second sustain. For summons it also shows the initial army cost and the replacement demand of a full army. Exact equipment and all other sampled builds remain in the CSV.
