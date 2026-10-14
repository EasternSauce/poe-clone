# Weapon tree redesign and balance audit

All 24 trees contain 24 passives. Three bent trunks each divide into two destinations; their two dead-end arms repeat related small bonuses before the final passive. Strong destinations cost five or six connected points. Utility travel passives generally provide 5% increased damage alongside their utility; direct damage travel points provide 10%.

Allocations from the previous topology are refunded on loading. Level and experience are preserved. The gauntlet's default node IDs and fingerprint now refer to this topology.

## Modifier rules

Increased/reduced damage, attack/cast speed, area, and minion life join the relevant stat increases. Each more/less damage source multiplies independently. Minion resistance increases multiply the minion's existing resistance before its cap. Flat values use a sign; probabilities and damage shares state their actual percentage. Full elemental conversions are mutually exclusive.

Tree bonuses belong to the equipped weapon's skill and to projectiles/minions spawned from that skill. Summon duration does not affect ailments, permanent minions do not gain an expiry, and movement speed does not change attack speed. Additional mage bolts share an enemy-hit set, preventing same-volley shotgunning.

## Identity and limits

- Forks: Ice Shard and Split Shot only. No spell-repeat destinations.
- Explode-on-kill: one 15% Fire Bolt destination. Minion death explosion: warrior skeletons only, on combat death rather than dismissal.
- Chain Lightning starts at zero chains; jumps retain 60% damage before retention passives.
- Solar Wheel fires eight sequential radial fireballs every fifth Fire Bolt use. Each deals 40% normal damage; the radial sequence shares a hit set.
- Piercing Shot charges while rooted. Base damage rises from 120% to 360% weapon damage, with a full-charge sound; its tree adds draw-time, penetration, opening-shot and range choices.
- Rain starts with six arrows at 30% weapon damage apiece, one salvo. Additional arrows add one-for-one. Impact area and landing spread are independent. The two salvo passives give two salvos at 75% damage each, then three at 50% each; all hits must land to realize that total. No conversion, ignite or explosion path.
- Warrior skeletons: numbers, bigger champions with half the cap, or Final Service. Mages: full elemental schools, coverage and ranged scaling. Wolves: proximity to packmates, pursuit and Blood Scent. Golem: one only, defence and delayed slams without speed penalties. Vipers: poison, longevity and brood size.

## Allocation audit

Exported the compiled tree definitions from Unity, verified parent connectivity and unique IDs, enumerated rooted subsets and compared all 195 legal ten-point sets per skill (4,680 total). Also asserted defensive/movement options on every summon tree and the rarity limits above.

The following are zero-gear direct-throughput ceilings relative to that skill's unallocated baseline. They include independently multiplied damage penalties, attack rate, minion count, pack proximity, charged-shot cycle time, golem aftershock, and rain arrows/salvos. Rain assumes every arrow lands; real small-target damage depends on spread and impact overlap. Ordinary projectile fans cannot shotgun. Coverage, ailment damage, kill bursts, conditional opening/low-life bonuses and sustain are assessed separately rather than counted as permanent single-target DPS. These figures are a mathematical audit, not gameplay results or proof of encounter balance.

| Skill | Maximum direct throughput / base |
| --- | ---: |
| Cleave | 1.864x |
| FireBolt | 1.74x |
| ChainLightning | 1.995x |
| IceShard | 1.947x |
| RaiseSkeletons | 3.328x |
| SkeletonMages | 3.156x |
| SpiritWolves | 3.8x |
| BoneGolem | 2.31x |
| SplitShot | 2.013x |
| PiercingShot | 2.694x |
| RainOfArrows | 4.042x |
| BurningArrow | 1.848x |
| FangStrike | 1.824x |
| VenomArrow | 1.68x |
| VenomSpout | 1.947x |
| SummonViper | 3.24x |
| Pulverize | 1.888x |
| ReapingArc | 1.864x |
| LungingThrust | 1.829x |
| Whirlwind | 1.994x |
| Fissure | 1.7x |
| Aftershock | 1.9x |
| CrossSlash | 1.824x |
| VenomSlash | 1.98x |

The most important reductions were to extra-minion routes and rain stacking. Original weapon spell, bow and minion baselines were reduced to leave room for ten allocated points. Spells outside the weapon-tree system retain their previous base damage.

Targeted Edit-mode checks passed for old-save refunds with preserved XP/level, prerequisite enforcement, duplicate rejection, independently stacked salvo penalties, and the fixed golem cap. No gauntlet or gameplay runs were performed; encounter balance, charge feel, audio, mobile input and multiplayer visuals still need hands-on review.
