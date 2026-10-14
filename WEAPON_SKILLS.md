# Weapon skill specializations

Each equipped weapon supplies one main ability. Melee weapons use their family-specific pools; bows use bow attacks; staves use Fire Bolt, Ice Shard, Chain Lightning or Venom Spout; sceptres and grimoires use one of the five summons. The main hand takes priority over an off-hand grimoire.

Movement, buff and curse abilities remain secondary abilities with fixed baselines. The specialization window and weapon XP apply to main weapon skills.

Press **T**, click the main attack icon, or click **Weapon tree** to open the current weapon's tree. Unequipping the weapon closes or switches the tree. Every main skill has 24 paid nodes in four authored branches, with a free origin. Each branch has a notable at its fourth node and a keystone at its sixth. All nodes cost one point. Selecting a node previews its exact effects; allocating is a separate button.

## Progression

- Progress belongs to the individual item, including XP and allocated nodes.
- A skill rank of 6 grants six total points, immediately available on a fresh drop.
- Combat XP awards also train the actively equipped main weapon. There is no passive time-based training and no training for bag, stash, inactive or secondary weapons.
- The XP needed for the next rank is `250 × current rank²`. Excess XP carries over. Rank 10 is the cap.
- Rank itself never changes damage, mana cost, cooldown, duration, projectile count or minion stats. Only allocated nodes and ordinary character/equipment stats do that.
- XP progress is visible in the tree, without a second bar on the combat HUD.
- An Ice Shard weapon begins with one shard. Frozen Fan is adjacent to the origin and grants two additional shards for one point.

## Reset service

**Mentor Ilyra** stands beside Smith Bram in Haven. She resets the currently equipped main weapon for `75 + 50 × skill rank²` gold. She shows the price before payment and asks the player to confirm. A successful reset refunds every allocated point and preserves rank and XP. Empty trees cannot be charged for a reset. The service checks that the player is still nearby, in town, wearing the same weapon, and has enough gold.

## Combat and persistence

Bow skills permanently replace left click. Minion main attacks fill empty summon slots; at the cap they command the existing army toward the aimed enemy without replacing healthy minions or charging a summoning cost. Changing the summoning weapon dismisses its army. Changing its allocation also dismisses old minions so their previous stat snapshots cannot be retained.

Skill-local effects are captured on projectiles and summoned minions. Forked volleys share their hit record so extra projectiles do not multiply damage against the same target. Secondary splash, shatter and explosion damage cannot recursively trigger itself. Poison/cloud kills retain their originating tree's kill effects.

Saves, stash items and multiplayer item messages carry the item's rank, XP and node IDs. Old weapons retain one compatible main ability, capped at rank 10, with points available to allocate. Old bow toggles are ignored. Removed stat IDs remain reserved for save compatibility. Character passives are refunded once at allocation version 3 because their skill mechanics have moved. Broad stats such as spell damage, minion life, attack speed and defences remain on gear and the character tree.

## Authoring

`WeaponSkillTrees.cs` contains the individual routes, notables and keystones. `WeaponSkillTree.cs` contains item progression, validation and immutable bonus snapshots. Node IDs are serialized; preserve them when changing text or balance. Incompatible tree changes should increment the progression version and provide an allocation migration.

Gauntlet builds have a `WeaponNodes` allocation order and include it in their fingerprint. Their generated weapons spend their actual rank budget. No gauntlet or gameplay run was required for the implementation.
