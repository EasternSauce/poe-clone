# Iterative boss balance results — 2026-10-15

Three complete boss surveys: an unchanged-gameplay baseline followed by two tuning passes. Each uses 20 seeded builds per available archetype/level, 20 rare rolls per equipment slot, all 318 catalogue entries and every character level 1–34. Each survey evaluates 78,240 builds and exports 10,812 archetype/level rows.

The headline comparison is each of the 24 main skills’ strongest available viable archetype at each level (816 comparisons). The top fifth of viable builds is averaged, then melee DPS is divided by 1.5 and minion DPS by 1.3. The peer baseline is the median across skills at that level. Utility branches are retained and are not required to match damage branches in stationary DPS.

## Pass results

| Survey | Within 75–125% | Below 50% | Above 200% | Largest same-level strongest/weakest gap |
|---|---:|---:|---:|---:|
| boss-baseline | 171/816 (21.0%) | 325 | 152 | 156.98× |
| pass1 | 677/816 (83.0%) | 0 | 0 | 2.64× |
| pass2 | 789/816 (96.7%) | 0 | 0 | 2.20× |

The before/after figures use the same boss model and sampling budgets. The earlier normal-enemy run is archived separately and is not mixed into this comparison. Fresh equipment optimization is rerun after every pass; the same seeds identify sampling scenarios, not necessarily identical selected gear.

## Changes by pass

- Pass one raised weak spell/minion base damage, added character-level spell/minion progression, reduced extreme bow/dagger attacks and poisons, and improved Aftershock. Runtime combat and the evaluator now share intrinsic poison constants.
- Pass two improved early mages/vipers and Rain of Arrows, redistributed Burning Arrow/Split Shot damage into their weapon passives, reduced Fang Strike/Fissure route spikes and improved Cross Slash’s Bladedance. Fissure’s full physical penetration route now reaches exactly 100% instead of wasting penetration above the cap.
- No third tuning pass was made, as requested. The remaining 27 skill/level review flags are shown below; the largest final high outlier is Rain of Arrows at level 34 (151%), and the lowest result is Burning Arrow (63% at its weakest level).

## Coverage at progression checkpoints

| Level | Baseline skills within band | Final skills within band | Baseline gap | Final gap |
|---|---:|---:|---:|---:|
| 1 | 5/24 | 24/24 | 100.15× | 1.47× |
| 7 | 5/24 | 24/24 | 108.66× | 1.46× |
| 14 | 6/24 | 22/24 | 149.73× | 1.85× |
| 21 | 5/24 | 22/24 | 65.18× | 1.71× |
| 28 | 5/24 | 24/24 | 59.95× | 1.45× |
| 34 | 5/24 | 23/24 | 58.33× | 1.88× |

## Final damage relative to the peer baseline

100% means the per-level peer baseline after the family damage premiums.

| Skill | L1 | L7 | L14 | L21 | L28 | L34 | All-level range |
|---|---:|---:|---:|---:|---:|---:|---:|
| Aftershock | 95% | 97% | 112% | 87% | 86% | 84% | 78–112% |
| BoneGolem | 95% | 102% | 100% | 93% | 93% | 99% | 82–105% |
| BurningArrow | 115% | 95% | 69% | 72% | 91% | 90% | 63–115% |
| ChainLightning | 84% | 100% | 113% | 101% | 110% | 113% | 84–120% |
| Cleave | 102% | 115% | 122% | 99% | 100% | 104% | 85–122% |
| CrossSlash | 122% | 105% | 113% | 83% | 91% | 83% | 75–122% |
| FangStrike | 120% | 98% | 90% | 122% | 98% | 100% | 89–123% |
| FireBolt | 94% | 100% | 93% | 82% | 92% | 98% | 80–109% |
| Fissure | 94% | 110% | 100% | 113% | 119% | 117% | 82–128% |
| IceShard | 89% | 108% | 110% | 90% | 96% | 99% | 87–113% |
| LungingThrust | 106% | 107% | 129% | 109% | 105% | 109% | 94–139% |
| PiercingShot | 120% | 105% | 113% | 102% | 100% | 100% | 90–125% |
| Pulverize | 100% | 107% | 103% | 96% | 92% | 92% | 89–118% |
| RainOfArrows | 106% | 85% | 86% | 116% | 108% | 151% | 75–151% |
| RaiseSkeletons | 91% | 99% | 114% | 101% | 107% | 112% | 90–122% |
| ReapingArc | 114% | 101% | 124% | 101% | 103% | 95% | 95–124% |
| SkeletonMages | 85% | 89% | 79% | 90% | 107% | 109% | 75–119% |
| SpiritWolves | 84% | 88% | 89% | 94% | 103% | 118% | 80–118% |
| SplitShot | 119% | 98% | 86% | 106% | 100% | 87% | 76–119% |
| SummonViper | 83% | 80% | 77% | 98% | 120% | 111% | 70–120% |
| VenomArrow | 114% | 92% | 79% | 72% | 83% | 80% | 66–114% |
| VenomSlash | 119% | 104% | 84% | 104% | 96% | 99% | 83–123% |
| VenomSpout | 92% | 94% | 92% | 105% | 112% | 117% | 81–127% |
| Whirlwind | 100% | 117% | 122% | 102% | 104% | 104% | 90–124% |

## Remaining review flags

| Skill | Below 75% levels | Above 125% levels |
|---|---|---|
| BurningArrow | 12–15, 21 | — |
| CrossSlash | 31–32 | — |
| Fissure | — | 24–25, 30 |
| LungingThrust | — | 11, 14–15 |
| PiercingShot | — | 11 |
| RainOfArrows | 8 | 33–34 |
| SummonViper | 3–4, 9, 11–13, 15 | — |
| VenomArrow | 18, 21 | — |
| VenomSpout | — | 25 |

## Benchmark and validation

- Equal-weight Gravelord Mortis, Ashen Warlord and Rimeheart, continuously scaled to character level. Actual armour, resistances and basic attacks are exported in the workbook’s Boss targets sheet.
- Solo ranged/spell/minion hits enrage the boss. Solo melee keeps it calm. Enrage damage-taken changes apply before armour/resistance; pressure includes boss hit scaling, Tempo and the 25% enrage attack-rate increase.
- One boss’s basic attacks are shared across the targetable minion army for expected replacement demand; wolves remain untargetable. Mana setup, replacement costs, sustain and melee survival thresholds stay active.
- Full balance unit tests were rerun after each pass: 15/15 baseline checks and 17/17 checks after each tuning pass. Matched passive checks also compared pass-one/pass-two effects on 18 identical gear/character-passive sets, with two allocations per set; all temporary node overrides were restored. Focused tests cover boss mitigation order, penetration caps, reference targets, shared army pressure, character versus weapon-rank progression, report coverage and resource reconciliation.
- This measures stationary single-target performance, not a full boss fight. Special moves, phases, wards, movement, clearing and co-op enrage spillover are excluded. Ailment ramp, secondary effects and expected proc rates remain approximate. Twenty samples do not establish that every possible build is balanced.

Broader regression checks: **65/75 passed; 10 failed**. The failures concern existing mana absorption, passive-tree topology/rewards/spacing, cast-speed eligibility and gear-grant expectations. The relevant implementations and test files have no substantive changes in this work (`git diff --ignore-space-at-eol` is empty for them). They were left unchanged when stopping after pass two:

- `DefenceMathTests.Mana_SoaksAShareOfHits_OnlyWhileThereIsMana`
- `PassiveTreeTests.DevotionGrowsWithEachPassiveOfItsSector`
- `PassiveTreeTests.EveryKeystoneRequiresThreeTravelPassivesOnItsOnlyApproach`
- `PassiveTreeTests.KeystonesAreDeadEndsThatChangeTheRules`
- `PassiveTreeTests.LongApproachesKeepOneSmallRewardThroughout`
- `PassiveTreeTests.PassiveStatsCountLikeGear`
- `PassiveTreeTests.SpacingKeepsItsAverageWithoutLongOutliers`
- `SkillGearTests.CastSpeed_RollsOnSuitableGear("rope_belt", False)`
- `SkillGearTests.EveryStaff_CarriesAnAttackSpell_AtEveryRarity`
- `SkillGearTests.StarterBow_CarriesSplitShot_AndBowSkillsAreNotTheAttack`

Local artifacts: `BalanceResults/survey.json`, `BalanceResults/skill-balance.xlsx`, `BalanceResults/sample-builds.csv`, `BalanceResults/recalculation-pool.json`, `BalanceResults/matched-route-checks.csv`, and the three `*-rows.json` snapshots used by this comparison.
