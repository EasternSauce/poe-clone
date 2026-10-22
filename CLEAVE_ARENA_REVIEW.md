# Level 34 Cleave arena review - 2026-10-22

Removed Cleave's built-in combo damage reduction before the run. The shared
combo multiplier now remains 1.0 for Cleave, including its specializations,
instead of falling to 0.7 while locked or braced. Gear defenses still apply.

## Live run

Selected the highest estimated DPS entry from all live level 34 Cleave routes:
`Cleave/1`, Explosive Finale, approximately 472.50 estimated DPS. Twin Rhythm was
second at 304.78. Used the arena's normal equipment and passive allocation,
fingerprint `a582f29a`, encounter seed `731386796`.

Started through DevTest, dismissed startup overlays, and restored normal base
life and mana after its automatic god-mode setup, before equipping the build.
Verified arrival protection was off and the Cleave damage multiplier was 1.0.
Enemies moved, attacked and used their abilities. An Editor-only input callback
steered toward the nearest enemy and held Cleave in range through virtual touch
inputs. It used no potions, support skills, deliberate dodging or kiting. Ran at
3x time scale; measurements below use game time. This was one live encounter
seed, not a multi-seed survival study or a mouse-control comparison.

| Measurement | Result |
|---|---:|
| Waves / bosses completed | 20 / 3 |
| Kills / deaths | 192 / 0 |
| Health / mana potions used | 0 / 0 |
| Maximum life | 831 |
| Lowest observed life | 104.9 (12.6%), during wave 15 |
| Recorded damage taken | 6,507.6 |
| Combat time | 199.3 seconds |
| Encounter clear DPS, including movement | 424.0 |
| Cleave uses / Dash uses | 321 / 0 |
| Average mana | 96.6% |

Damage taken is the recorder's hit/direct-damage counter; poison ticks currently
bypass that event, so it is not a complete total of all life lost. Minimum life
was sampled each frame and immediately before recorded damage was applied.

| Boss | First hit to death | Recorded player damage taken |
|---|---:|---:|
| Vex, the Tunnel King | 5.83 s | 178.5 |
| Gravelord Mortis | 6.32 s | 231.4 |
| Bramblesow | 7.40 s | 52.9 |

The build survived without potions, but the claim that waves cannot dent its life
did not reproduce after removing the reduction. Wave 15 brought it close to death.
Removing 30% less damage means approximately 43% more incoming damage during the
previously protected portions of a combo. There was no pre-removal paired run.

## Remaining sources of strength

1. **Damage also prevents enemy attacks.** Every surviving non-boss enemy flinches
   on an ordinary hit, regardless of the fraction of life lost. Flinch lasts
   0.4 seconds and interrupts attacks. This build's base Cleave interval was
   0.808 seconds; the ordinary combo follow-up speed multiplier of 2.25 puts
   nominal follow-up spacing around 0.359 seconds. Repeated hits can therefore
   renew flinch before recovery. Explosive Finale additionally stuns surviving
   non-bosses for two seconds across its circular area. This is strong defensive
   value even after the explicit damage reduction is gone.
2. **Recovery scales with enemies hit.** The build has 6 instant life per enemy
   hit and 2% life leech. An explosion hitting twelve targets can grant 72 instant
   life (8.7% of this life pool), plus independent leech effects. Instant on-hit
   life bypasses the leech rate cap. Passive regeneration contributes about
   9.5 life per second. These are available mechanics, not measured recovery
   attribution: this run did not separately count healing from each source.
3. **Overkill contributes to leech.** `EnemyHealth.TakeDamage` returns mitigated
   hit damage without clamping it to remaining enemy life; `HitEffects` uses that
   value for leech. The report logged about 146,644 damage against 85,077 total
   enemy maximum life. That does not equal actual leech recovery, because the
   per-effect cap, rate cap and full-life clearing still apply, but excess killing
   damage can generate additional leech. Large explosions benefit particularly.
4. **The strongest route also ends encounters quickly.** It was about 55% above
   Twin Rhythm in picker DPS, one-shot 82 of 150 normal enemies, and killed these
   three bosses in under eight seconds each. Its damage, area and control arrive
   together. Mana was not a meaningful constraint in this run.

## Suggested next balance steps

Keep the damage-reduction removal and repeat several seeds before applying a
second broad nerf: a 13% life minimum already shows real danger.

If sustained attacking still feels too safe, first trial a shorter Explosive
Finale stun (0.5-0.75 seconds). Separately trial a damage threshold or short
immunity window for ordinary enemy flinch so rapid light hits cannot repeatedly
cancel an enemy's attack. Ordinary flinch is shared by other skills, so evaluate
that change across melee rather than changing it globally without comparison.

Next investigate basing leech on actual life removed, excluding overkill. If
large packs still act as healing sources, trial diminishing instant life gain
after the first few targets per Cleave use. Do not simultaneously cut the global
leech cap, flat recovery, stun and damage; their individual effects would be hard
to assess, and single-target melee would also suffer.

Only then consider lowering Explosive Finale's 550% damage or adding a longer
finisher recovery. The route currently leads both damage and control; reducing
its control first preserves the heavy finisher while opening enemy attack windows.
These are proposed experiments; no additional combat nerfs were applied.

## Artifacts and validation

- Raw completed report: `GauntletResults/20261022-cleave-level34-review.json`.
- Life observations: `GauntletResults/20261022-cleave-level34-observations.txt`.
- Fixed a result-export failure discovered at completion: arena IDs such as
  `Cleave/1` were treated as nested paths. Export filenames now replace invalid
  filename characters; the report retains its original build ID. Re-exported
  this completed report successfully through the corrected writer.
- Confirmed a locked Cleave took 10 life from 10 direct damage; a locked
  Pulverize still took 7. All damage paths use the same combo multiplier.
- Stopped Play to restore saved preferences, refreshed all Unity assets, waited
  for compilation, and checked the console: no errors. Deleted the temporary
  screenshot used to inspect live combat. Results are local and gitignored;
  this review is committed with the code and patch notes.
