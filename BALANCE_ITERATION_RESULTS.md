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

## Cloudburst follow-up — 2026-10-15

Cloudburst now keeps its four additional arrows but applies 35% less damage,
previously 15%. The connected six-point requirement, impact area, arrow count and
other weapon nodes are unchanged. Existing allocations remain valid.

Before tuning, inspection found a survey mismatch: gameplay guarantees one arrow
at the aim point in every salvo, while the old estimate treated all arrows as
scattered. The evaluator now shares the runtime arrow-count calculation and rain
geometry, and adds one guaranteed central hit per salvo before estimating overlap
from the remaining arrows. This estimates a stationary point target, not collider
extent or a moving boss. It changes the estimate, not runtime arrow placement.

Using that corrected model, Rain of Arrows was refreshed before and after the
Cloudburst change with the same seed, 20 sampled builds per available route/level,
and 20 rare candidates per gear slot. Each refresh covers all 13 Rain archetypes
and levels 1–34: 3,260 builds. Other skills retain the preceding full-survey samples;
peer baselines are rebuilt for every level. The complete report validates.

| Cloudburst level | Corrected baseline | After tuning |
| --- | ---: | ---: |
| 20 | 128.0% | 92.7% |
| 25 | 139.5% | 100.8% |
| 29 | 112.3% | 81.1% |
| 34 | 169.5% | 121.7% |

Cloudburst alone is now inside 75–125% at every available level (20–34). Level-34
cohort DPS falls from 1,216.6 to 873.3 with identical modeling assumptions. The
earlier 150.4% figure used the old overlap estimate and is not a valid direct
before/after comparison for the gameplay adjustment.

Remaining work: the Cloudburst + Pinning Rain hybrid falls from 90.1% to 65.8% at
level 34; its spread and guaranteed chill are not valued as utility by this DPS
metric. The corrected model also exposes a strong flexible route (135.8% at level
34) and Death Zone + Overlapping Impacts outliers (129.7% at level 31 and 138.8% at
33). None of their raw DPS values changed between the corrected baseline and the
Cloudburst tuning unless the allocation includes Cloudburst. The follow-up should
consider those routes together before declaring Rain of Arrows balanced.

Local comparison snapshots: `BalanceResults/rain-cloudburst-before.json` (old
model), `BalanceResults/rain-cloudburst-corrected-baseline.json`, and
`BalanceResults/rain-cloudburst-after.json`. `survey.json` and
`recalculation-pool.json` contain the refreshed results. The historical workbook
and CSV above were not regenerated and should not be used for this follow-up.

Regression coverage adds five overlap cases (centered hits, extra arrows,
multiple salvos, full overlap and zero spread), plus two cases checking
Cloudburst's allocation budget, projectile bonus and separate damage penalty.
No gameplay session or character save was used.

Validation: 306/307 non-survey EditMode cases passed, including all seven added
cases. The sole failure remains the previously documented passive-tree edge
spacing violation. The full 318-archetype test was not rerun for this targeted
change; all 13 Rain routes were freshly sampled and the combined report validated.
Forced Unity refresh and compilation completed with no console errors.

## Burning Arrow progression follow-up — 2026-10-15

The level 12–15 dip was primarily damage rather than mana: the representative
builds sustain their attacks throughout the modeled 30-second encounter. Damage
was concentrated in terminal bonuses unavailable before weapon ranks five/six.
Level 21 also has some resource pressure, but changing mana costs alone would
not address the preceding damage gap.

Each Burning Arrow small node now adds 7% more damage alongside its existing
utility and 5% increased damage. Slow Burn's more-damage bonus falls from 45% to
20%, and Hot Coals' from 25% to 10%. This moves damage into developing allocations
while keeping the completed eight-point ignition route's combined more-damage
multiplier within about 2.2% of its former value. Mana costs, base skill damage,
ignite chances, durations, node IDs and connected allocation costs are unchanged.
Weapon rank still grants points rather than automatic damage.

All 13 Burning Arrow archetypes were refreshed at levels 1–34 using the existing
seed 20261014, 20 builds per available route/level and 20 rare candidates per gear
slot: 3,260 builds. Gear is freshly optimized after the redistribution. The other
skills retain their preceding samples, and per-level peer baselines are rebuilt.

| Level | Best available route before | After | Second-seed check |
| --- | ---: | ---: | ---: |
| 12 | 69.7% | 90.0% | 93.5% |
| 13 | 67.5% | 85.6% | 96.3% |
| 14 | 68.7% | 87.2% | 85.1% |
| 15 | 62.2% | 77.5% | 87.2% |
| 21 | 72.5% | 79.6% | 83.3% |

The strongest available Burning Arrow route is now within 75–125% at every level,
ranging from 77.5% to 122.9%. No individual Burning Arrow route is flagged high.
Level 1 rises from 114.9% to 122.9%; level 34 rises from 90.5% to 105.7%, where the
best sampled route changes from Slow Burn + Hot Coals to Slow Burn. These results
describe sampled routes, not a guarantee for every allocation or gameplay fight.

The second-seed check uses seed 20261015 and 20 fresh builds for every available
Burning Arrow route at each of the five requested levels. It compares against
the existing per-level peer baselines; it is not a second full all-skill survey.
All five strongest-route results remain in band.

Local before/after snapshots, including Burning Arrow sample builds:
`BalanceResults/burning-arrow-before.json` and
`BalanceResults/burning-arrow-after.json`. `survey.json` and
`recalculation-pool.json` contain the refreshed main-seed results. Historical
workbook/CSV exports were not regenerated.

Four added regression cases check developing-route damage before destinations,
unchanged mana-cost modifiers, and the completed ignition route's damage budget
and ailment bonuses. The non-survey EditMode run passed 310/311 cases; the sole
failure is the previously documented passive-tree spacing violation. The full
318-archetype test was not rerun for this targeted change. The combined report
validates; no gameplay session or character save was used.

## Venom Arrow progression follow-up — 2026-10-15

Deep Venom now adds poison equal to 55% of the hit, previously 25%. Black Blood
adds another 35%, previously 40%, so their combined tree bonus is 90% rather than
65%. More of that investment is available at the three-point notable before the
six-point terminal. These bonuses are additional to the skill's innate poison;
the change does not double the skill's entire poison output. Base attack damage,
mana costs, duration, cloud bonuses, node IDs and allocation costs are unchanged.

The strongest level-21 build sustained about 99% of its modeled attack demand;
its shortfall was primarily damage. Level 18 also had some mana pressure (about
86% encounter use on its representative build). Strengthening the poison branch
improves both points without a global cost reduction or early base-damage buff.

All 13 Venom Arrow archetypes were freshly sampled at levels 1–34 with seed
20261014, 20 builds per available route/level and 20 rare candidates per gear
slot: 3,260 builds. Other skills retain the preceding survey samples; per-level
peer baselines are recalculated. The combined report validates.

| Level | Best available route before | After | Second-seed check |
| --- | ---: | ---: | ---: |
| 18 | 64.9% | 78.8% | 76.1% |
| 21 | 72.3% | 82.5% | 88.8% |

The best sampled route is now in the 75–125% band at every level, ranging from
77.1% to 121.6%. No Venom Arrow route is flagged high. The level-1 result remains
113.8%, and level 34 improves from 78.2% to 91.6%. The second seed, 20261015,
resamples every available Venom Arrow route at the two requested levels with
20 builds each, comparing against the existing peer baselines. Level 18 remains
close to the lower boundary; two seeds are evidence of improvement, not a
guarantee that every random build or encounter meets the band.

Local comparison snapshots: `BalanceResults/venom-arrow-before.json` and
`BalanceResults/venom-arrow-after.json`, including their sample builds.
`survey.json` and `recalculation-pool.json` contain the refreshed main-seed results.
Historical workbook/CSV exports were not regenerated.

Two added regression cases check connected access to Deep Venom, its three-point
bonus and the completed six-point branch, unchanged duration/cost modifiers, and
the absence of a new general damage multiplier. The non-survey EditMode run
passed 312/313 cases; the sole failure is the previously documented passive-tree
spacing violation. The full 318-archetype test was not rerun for this targeted
change. No gameplay session or character save was used.

## Summon Viper early-level follow-up — 2026-10-15

Early representative builds have full modeled army and resource uptime, despite
short minion lifetimes and replacement demand. Their shortfall is principally
single-viper damage before Brood becomes available, rather than initial army cost
or a lack of mana. The benchmark assumes prompt replacement; gameplay survival
and replacement input still need player evaluation.

The viper profile's base hit rises from 13 to 16 before the shared summon damage
factor. Character-level growth falls from 2.2% to 1.22% of base per level. At level
34 the unmodified profile damage is 22.4416 versus the old 22.438, a difference
under 0.02%. The shared summon factor applies equally to both. This improves
early damage without adding weapon-rank scaling or raising the level-34 damage
budget. Life, defences, army caps, durations, attack intervals, summon costs and
weapon-tree bonuses are unchanged. Per-kind growth now lives beside base damage
in the existing minion profiles; the other four kinds retain their rates.

All 13 Summon Viper archetypes were refreshed across levels 1–34 with seed
20261014, 20 builds per available route/level and 20 rare candidates per gear
slot: 3,260 builds. Other skills retain their preceding samples; peer baselines
are rebuilt. The combined report validates.

| Level | Best available route before | After | Second-seed check |
| --- | ---: | ---: | ---: |
| 3 | 73.0% | 92.0% | 111.2% |
| 4 | 70.0% | 88.2% | 112.3% |
| 9 | 71.5% | 84.7% | 98.0% |
| 11 | 71.6% | 83.3% | 98.7% |
| 12 | 69.3% | 79.9% | 92.8% |
| 13 | 69.6% | 79.5% | 100.0% |
| 14 | 74.4% | 84.3% | 95.3% |
| 15 | 68.4% | 76.9% | 95.7% |

The main seed's best route is now in band at every level, ranging from 76.9% to
122.3%, with no high-route flags. Level 1 improves from 81.1% to 103.4%; level 34
remains 104.7%. The second-seed check uses seed 20261015 and 20 fresh builds per
available route at the listed levels, against the existing main-seed peer
baselines. All previously weak early levels pass that check.

**Remaining issue: Brood's level-16 unlock.** The main seed rises from 109.3% to
122.3%, but the second seed produces 156.8%. Repeating that second-seed measurement
with the original viper damage curve produces 140.3%, confirming a pre-existing
unlock spike that the early-damage increase amplifies. Brood's extra viper and
damage tradeoff need a separate check across more seeds; this pass does not
establish that the unlock is balanced. The original curve was restored only for
that measurement; the final code and report use the new curve.

Local comparison snapshots: `BalanceResults/summon-viper-before.json` and
`BalanceResults/summon-viper-after.json`, including their sample builds.
`survey.json` and `recalculation-pool.json` contain the refreshed main-seed results.
Historical workbook/CSV exports were not regenerated.

Five added regression cases verify the stronger early viper curve, monotonic
character progression, preserved level-34 damage, and unchanged growth for the
other minion kinds. The non-survey EditMode run passed 317/318 cases; the only
failure remains the documented passive-tree spacing violation. The full
318-archetype test was not rerun for this targeted change. No gameplay session or
character save was used.
