# Combat dummy baseline — 2026-10-04

The DPS samples below predate the minion defence change and the melee-skill and bow-passive buffs. They measure basic attacks, so the melee-skill changes do not affect the listed melee samples; the bow samples predate the new late Grace path and must be rerun before use as current values. Minion outgoing damage formulas were left intact. The early Zombie survival observations are historical; minions now take full hits with their own armour and fire/cold/lightning/poison resistance, have higher starting life with 5% life growth per summon level, and can invest in Grave Ward and stronger Necromancy defences. Rerun the survival encounters before treating the old hit counts as current.

Reproduce these numbers with `DevBalance.Benchmark(...)` in `TESTING.md`. These are single eight-game-second Editor Play samples with deterministic ordinary gear and connected passives. Levels are 4 / 9 / 15 for early / mid / late. The neutral target has no armour or resistance. The boss target uses the current Shepherd's 1,200 armour and 30% fire/cold/lightning resistance. Damage is counted after defences. Samples vary slightly with hit timing and random damage.

| Build | Early neutral DPS | Mid neutral DPS | Late neutral DPS | Late boss DPS |
| --- | ---: | ---: | ---: | ---: |
| Melee, greatsword basic attack | 13.5 | 43.5 | 76.4 | 56.6 |
| Bow basic attack | 11.8 | 51.3 | 88.7 | 59.0 |
| Fire Bolt staff attack | 15.4 | 54.1 | 114.5 | 81.8 |
| Minions alone | 6.0 | 24.2 | 123.4 | 57.7 |
| Minions with Death Mark attack | 15.6 | 45.5 | 204.9 | 105.7 |

The early summon fixture has two level 2 warriors. The mid fixture has four minions at summon level 6; the late fixture has six at summon level 12. Death Mark is the summoner's grimoire attack. It materially changes single-target damage, so the marked line is the fairer comparison for an actively played summoner. Minions alone show the army's output while the player does something else. The mid and late army mixes use warriors and mages.

## Early playability

Marked early damage is comparable with the other fixtures. Without a mark, the two warriors deal less than half the melee or caster baseline. Against one level 4 Zombie, they killed it and both survived (one at 19/31 life). Against three level 4 Zombies, both died within about 13 game seconds while the pack remained almost intact. This makes early pack play fragile even though single-target damage is viable. The dummy test does not account for targeting mistakes, travel, resummon time, or incoming damage, which all matter for a summoner.

Raise Skeletons level 2 used to cost 49 mana per cast. The new low-level summon discount makes it 31.6; it fades by skill level 10 (Raise Skeletons remains 161 mana at level 10). The early loadout's life and outgoing damage were not changed. A late marked boss sample was 105.7 DPS before this mana change and 107.5 after, a 1.7% difference within ordinary hit variation. Level 12 summons receive no discount.

## Shepherd phases

At level 12, the Shepherd's initial life is 11,640. Phase 1 consumes the first third (3,880); phase 2 consumes the remaining 7,760; the Carrion Saint reveal restores a full 11,640 for phase 3. This is 23,280 damage across the fight. Estimated uninterrupted damage times from the boss dummy:

| Late build | Phase 1 | Phase 2 | Phase 3 | Total |
| --- | ---: | ---: | ---: | ---: |
| Melee basic attack | 69 s | 137 s | 206 s | 6 m 51 s |
| Bow basic attack | 66 s | 132 s | 197 s | 6 m 34 s |
| Fire Bolt staff attack | 48 s | 95 s | 142 s | 4 m 45 s |
| Minions with Death Mark | 37 s | 73 s | 110 s | 3 m 40 s |
| Minions alone | 67 s | 135 s | 202 s | 6 m 44 s |

The boss's defences cut late neutral DPS to about 74% for melee, 67% for bow, 71% for Fire Bolt, and 52% for marked minions. Armour particularly punishes numerous smaller minion hits. Despite that, the active late summoner is the fastest of these fixtures.

These phase times assume stationary targets, continuous contact, abundant mana, a prepared army, and no phase transition pauses. The melee and bow fixtures use basic attacks; Cleave and bow skills could shorten their times. Real dodging and resummoning could lengthen every time. The full-health phase-three reset contributes as much life as the first two phases together. If the intended uninterrupted baseline is around two to three minutes, the current combined life and defences are likely too high. Measure representative active melee and bow rotations before changing the boss; then test a smaller life pool or armour reduction separately so the cause of the pacing change is clear.
