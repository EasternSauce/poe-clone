using UnityEngine;
using PoeClone.Combat;
using PoeClone.Inventory;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    public enum EnemyAttackStyle
    {
        Melee,
        Ranged
    }

    /// <summary>What a (non-boss) enemy does now and then besides its plain attack; see <see cref="EnemySkills"/>.</summary>
    public enum EnemySkill
    {
        None,
        Slam,     // a blast all round itself, after a glowing wind-up
        Charge,   // dashes at the player and hits
        Volley,   // three arrows in a spread
        Strike,   // a blast where the player stands, after a glowing wind-up (fire, ice or lightning)
        Blink,    // vanishes and reappears beside the player
        WarCry,   // heals the enemies round it
        Summon,   // raises skeletons
        Leap,     // springs through the air and lands on the player (a glow marks where)
        ThornGarden, // seeds lingering thorn patches in a line
        Wail,        // a scream across a ring: stay inside or escape outside
        FrostFissures, // crossed frost fissures with safe diagonal gaps
        LastOffering,
        DraggingBreath,
        Graveward
    }

    public enum BossStyle
    {
        None,
        Gravelord,   // greataxe: leaps onto the player, spins its axe all round, raises zombies
        Warlord,     // maul: slams a line of eruptions at the player, quake-leaps, rains fire
        FrostQueen,  // a giant frost spider: pounces, hatches ice crawlers, bursts frost, calls down ice
        Shepherd     // the act boss: a hooded shepherd with a snake crook and a lantern (ShepherdLook); three phases
    }

    /// <summary>
    /// One type of enemy: its numbers, how it fights, and how it looks. Every type is the same
    /// enemy prefab. Humanoids are its rig, told apart by size, recoloured clothes/skin/eyes, and
    /// gear on its sockets (the player's equipment models, or a caster's staff); creatures (spiders,
    /// wolves, slimes, bats, beetles) swap the rig for a body of their own (<see cref="CreatureBuilder"/>),
    /// coloured Skin (body), Cloth (abdomen/shell/mane/wings), Pants (markings) and Eyes.
    /// </summary>
    public sealed class EnemyKind
    {
        public string Name;
        public float SpawnWeight;

        public float MaxHealth;
        public float Armour;
        public float FireResistance, ColdResistance, LightningResistance, PoisonResistance;
        public int Experience;

        public EnemyAttackStyle Style;
        public DamageType DamageType;
        public float Damage;
        public float AttackCooldown;
        public float AttackRange;
        public float ProjectileSpeed;

        public float SpeedRatio;
        public float Scale = 1f;

        /// <summary>
        /// How fast everything about it runs: walking, attack rate and swing animations, and (for a
        /// boss) every move's wind-up and recovery. Bosses run at double.
        /// </summary>
        public float Tempo = 1f;

        // Replacement colours for the prefab's own materials; clear keeps the original (the zombie).
        public Color Cloth = Color.clear;
        public Color Skin = Color.clear;
        public Color Pants = Color.clear;
        public Color Eyes = Color.clear;

        public bool HideHorns;
        public string[] Gear = new string[0];
        public Color StaffOrb;   // casters: the orb on the staff and the colour of their bolts
        public bool Bow;         // archers: shoots arrows instead of bolts (the bow itself is in Gear)

        /// <summary>
        /// A humanoid that swings a real weapon (the one in its Gear) with that weapon's own
        /// animation, instead of clawing. Unarmed: the claw swipe.
        /// </summary>
        public WeaponType Weapon = WeaponType.Unarmed;

        public float DropChance;
        public float RareBonus;

        // Bosses: never spawn at random (weight 0), placed by a BossLair; drop several items,
        // shrug off stagger, and get a BossAbilities set of their own.
        public bool IsBoss;
        public bool Undead;
        public int Drops = 1;
        public BossStyle Boss;
        // Restores this boss's original hit damage when ranged or minion damage enrages it.
        public float BossEnrageDamage = 1f;
        // Applies on the same ranged/minion hit that starts enrage, preventing a free opening burst.
        public float BossEnrageDamageTaken = 1f;

        public EnemySkill Skill;
        public float SkillCooldown = 9f;

        public CreatureBody Body = CreatureBody.Humanoid;
        public EnemySounds.Set Sounds = EnemySounds.Set.Default;

        /// <summary>Slimes: the kind (index) it bursts into when killed, two of them; -1 for none.</summary>
        public int SplitInto = -1;

        /// <summary>Pack animals: up to this many more of the same kind spawn round it.</summary>
        public int PackSize;

        public bool IsRanged => Style == EnemyAttackStyle.Ranged;
        public bool IsCreature => Body != CreatureBody.Humanoid;

        /// <summary>How high above its pivot (at scale 1) the floating health bar sits.</summary>
        public float BarHeight
        {
            get
            {
                switch (Body)
                {
                    case CreatureBody.Spider: return 0.6f;
                    case CreatureBody.Wolf: return 0.9f;
                    case CreatureBody.Slime: return 0.7f;
                    case CreatureBody.Bat: return 1.4f;
                    case CreatureBody.Beetle: return 0.6f;
                    case CreatureBody.Briarbound: return 2.6f;
                    case CreatureBody.GraveSiren: return 2.8f;
                    case CreatureBody.RimeStalker: return 2.5f;
                    case CreatureBody.CinderPenitent: return 1.8f;
                    case CreatureBody.Hollowmaw: return 2.5f;
                    case CreatureBody.BarrowCastellan: return 2.7f;
                    default: return 2.3f;
                }
            }
        }
    }

    /// <summary>
    /// The enemy roster: three melee types, an archer and three elemental casters; skeletons,
    /// wraiths and ember knights native to the deeper areas; the bosses; then later additions
    /// (shaman, necromancer, skeleton archer, frost giant); then the creatures (spiders, wolves,
    /// slimes, bats, beetles). Each area picks its own mix (the
    /// WorldBuilder's per-area weights). Kinds are referred to by index (saves don't, but the
    /// spectator stream and the weights do), so new ones go on the end.
    /// </summary>
    public static class EnemyKinds
    {
        public static readonly EnemyKind[] All =
        {
            new EnemyKind
            {
                Name = "Zombie", SpawnWeight = 30f, Undead = true,
                MaxHealth = 30f, Experience = 20,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Physical,
                Damage = 6f, AttackCooldown = 1.4f, AttackRange = 2.0f,
                SpeedRatio = 0.58f,
                DropChance = 0.25f
            },
            new EnemyKind
            {
                Name = "Raider", SpawnWeight = 18f,
                MaxHealth = 42f, Experience = 30,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Physical,
                Damage = 5f, AttackCooldown = 0.9f, AttackRange = 2.0f,
                SpeedRatio = 0.85f, Scale = 0.95f, 
                Cloth = new Color(0.62f, 0.42f, 0.25f), Skin = new Color(0.78f, 0.60f, 0.45f), Eyes = new Color(0.15f, 0.10f, 0.08f), HideHorns = true,
                Gear = new[] { "bronze_helmet", "rusty_sword", "wooden_shield" },
                Skill = EnemySkill.Charge, SkillCooldown = 7f,
                DropChance = 0.35f
            },
            new EnemyKind
            {
                Name = "Brute", SpawnWeight = 12f,
                MaxHealth = 90f, Experience = 55,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Physical,
                Damage = 15f, AttackCooldown = 2.2f, AttackRange = 2.6f,
                SpeedRatio = 0.62f, Scale = 1.35f, 
                Cloth = new Color(0.22f, 0.22f, 0.25f), Skin = new Color(0.55f, 0.42f, 0.38f), Eyes = new Color(1.0f, 0.2f, 0.1f),
                Gear = new[] { "studded_vest" },
                Skill = EnemySkill.Slam, SkillCooldown = 8f,
                DropChance = 0.7f, RareBonus = 0.15f
            },
            new EnemyKind
            {
                Name = "Archer", SpawnWeight = 14f,
                MaxHealth = 28f, Experience = 28,
                Style = EnemyAttackStyle.Ranged, DamageType = DamageType.Physical,
                Damage = 7f, AttackCooldown = 1.8f, AttackRange = 12f, ProjectileSpeed = 20f,
                SpeedRatio = 0.72f, Scale = 0.95f, 
                Cloth = new Color(0.28f, 0.55f, 0.24f), Skin = new Color(0.75f, 0.62f, 0.48f), Pants = new Color(0.42f, 0.33f, 0.20f), Eyes = new Color(0.15f, 0.10f, 0.08f), HideHorns = true,
                Gear = new[] { "leather_gloves", "leather_boots", "short_bow" },
                Bow = true,
                Skill = EnemySkill.Volley, SkillCooldown = 8f,
                DropChance = 0.35f
            },
            new EnemyKind
            {
                Name = "Fire Caster", SpawnWeight = 11f,
                MaxHealth = 26f, Experience = 30,
                Style = EnemyAttackStyle.Ranged, DamageType = DamageType.Fire,
                Damage = 9f, AttackCooldown = 2.0f, AttackRange = 10f, ProjectileSpeed = 11f,
                SpeedRatio = 0.62f, Scale = 0.95f, 
                Cloth = new Color(0.80f, 0.25f, 0.05f), Skin = new Color(0.55f, 0.35f, 0.30f), Eyes = new Color(1.0f, 0.6f, 0.1f), HideHorns = true,
                StaffOrb = new Color(1.0f, 0.45f, 0.10f),
                Skill = EnemySkill.Strike, SkillCooldown = 9f,
                DropChance = 0.35f
            },
            new EnemyKind
            {
                Name = "Frost Caster", SpawnWeight = 10f,
                MaxHealth = 26f, Experience = 30,
                Style = EnemyAttackStyle.Ranged, DamageType = DamageType.Cold,
                Damage = 7f, AttackCooldown = 2.0f, AttackRange = 10f, ProjectileSpeed = 10f,
                SpeedRatio = 0.62f, Scale = 0.95f, 
                Cloth = new Color(0.15f, 0.35f, 0.80f), Skin = new Color(0.75f, 0.85f, 0.95f), Eyes = new Color(0.5f, 0.95f, 1.0f), HideHorns = true,
                StaffOrb = new Color(0.55f, 0.85f, 1.0f),
                Skill = EnemySkill.Strike, SkillCooldown = 9f,
                DropChance = 0.35f
            },
            new EnemyKind
            {
                Name = "Storm Caster", SpawnWeight = 10f,
                MaxHealth = 22f, Experience = 30,
                Style = EnemyAttackStyle.Ranged, DamageType = DamageType.Lightning,
                Damage = 8f, AttackCooldown = 1.6f, AttackRange = 11f, ProjectileSpeed = 16f,
                SpeedRatio = 0.68f, Scale = 0.9f, 
                Cloth = new Color(0.38f, 0.20f, 0.60f), Skin = new Color(0.70f, 0.70f, 0.60f), Eyes = new Color(1.0f, 1.0f, 0.4f), HideHorns = true,
                StaffOrb = new Color(1.0f, 0.95f, 0.35f),
                Skill = EnemySkill.Strike, SkillCooldown = 7f,
                DropChance = 0.35f
            },

            // Area natives (weight 0 by default: only areas that list them spawn them).
            new EnemyKind
            {
                Name = "Skeleton", SpawnWeight = 0f, Undead = true,
                MaxHealth = 24f, Experience = 26,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Physical,
                Damage = 6f, AttackCooldown = 1.0f, AttackRange = 2.0f,
                SpeedRatio = 0.88f, Scale = 0.95f,
                Cloth = new Color(0.82f, 0.80f, 0.72f), Skin = new Color(0.90f, 0.88f, 0.80f), Pants = new Color(0.32f, 0.30f, 0.27f), Eyes = new Color(1.0f, 0.25f, 0.1f), HideHorns = true,
                Gear = new[] { "rusty_sword" },
                DropChance = 0.3f
            },
            new EnemyKind
            {
                Name = "Wraith", SpawnWeight = 0f, Undead = true,
                MaxHealth = 34f, Experience = 34,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Cold,
                Damage = 8f, AttackCooldown = 1.5f, AttackRange = 2.2f,
                SpeedRatio = 0.95f, Scale = 1.05f,
                Cloth = new Color(0.58f, 0.70f, 0.64f), Skin = new Color(0.80f, 0.95f, 0.90f), Pants = new Color(0.40f, 0.50f, 0.47f), Eyes = new Color(0.4f, 1.0f, 0.7f), HideHorns = true,
                Skill = EnemySkill.Blink, SkillCooldown = 9f,
                DropChance = 0.35f
            },
            new EnemyKind
            {
                Name = "Ember Knight", SpawnWeight = 0f,
                MaxHealth = 70f, Experience = 50,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Fire,
                Damage = 12f, AttackCooldown = 1.8f, AttackRange = 2.4f,
                SpeedRatio = 0.7f, Scale = 1.2f,
                Cloth = new Color(0.36f, 0.12f, 0.06f), Skin = new Color(0.26f, 0.20f, 0.18f), Pants = new Color(0.16f, 0.12f, 0.10f), Eyes = new Color(1.0f, 0.55f, 0.1f), HideHorns = true,
                Gear = new[] { "iron_helmet", "iron_mace", "wooden_shield" },
                Skill = EnemySkill.Slam, SkillCooldown = 8f,
                DropChance = 0.55f, RareBonus = 0.1f
            },

            // Bosses: weight 0, so only their lairs place them.
            new EnemyKind
            {
                Name = "Gravelord Mortis", SpawnWeight = 0f, Tempo = 0.8f, BossEnrageDamage = 1.5f, BossEnrageDamageTaken = 0.8f,
                MaxHealth = 340f, Armour = 500f, FireResistance = 20f, ColdResistance = 20f, LightningResistance = 20f, Experience = 150,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Physical,
                Damage = 14f, AttackCooldown = 1.4f, AttackRange = 4.2f,
                SpeedRatio = 1.05f, Scale = 1.9f,
                Cloth = new Color(0.16f, 0.18f, 0.16f), Skin = new Color(0.62f, 0.68f, 0.58f), Pants = new Color(0.12f, 0.12f, 0.12f), Eyes = new Color(0.3f, 1.0f, 0.4f),
                Gear = new[] { "great_helm", "executioner_axe" }, Weapon = WeaponType.Greataxe,
                DropChance = 1f, RareBonus = 0.6f, Drops = 3,
                IsBoss = true, Boss = BossStyle.Gravelord
            },
            new EnemyKind
            {
                Name = "Ashen Warlord", SpawnWeight = 0f, Tempo = 0.8f, BossEnrageDamage = 1.5625f, BossEnrageDamageTaken = 0.8f,
                MaxHealth = 420f, Armour = 500f, FireResistance = 20f, ColdResistance = 20f, LightningResistance = 20f, Experience = 220,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Fire,
                Damage = 16f, AttackCooldown = 1.5f, AttackRange = 4.4f,
                SpeedRatio = 1.05f, Scale = 2.1f,
                Cloth = new Color(0.30f, 0.06f, 0.04f), Skin = new Color(0.22f, 0.18f, 0.17f), Pants = new Color(0.10f, 0.08f, 0.08f), Eyes = new Color(1.0f, 0.55f, 0.1f),
                Gear = new[] { "warlord_plate", "warlord_helm", "earthbreaker" }, Weapon = WeaponType.Maul,
                DropChance = 1f, RareBonus = 0.8f, Drops = 4,
                IsBoss = true, Boss = BossStyle.Warlord
            },
            new EnemyKind
            {
                // The brood-queen of the Hollow's ice crawlers: a spider the size of a house.
                Name = "Rimeheart", SpawnWeight = 0f, Tempo = 0.8f, BossEnrageDamage = 1.5f, BossEnrageDamageTaken = 0.8f,
                MaxHealth = 460f, Armour = 500f, FireResistance = 20f, ColdResistance = 20f, LightningResistance = 20f, Experience = 300,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Cold,
                Damage = 14f, AttackCooldown = 1.0f, AttackRange = 4.0f,
                SpeedRatio = 1.2f, Scale = 2.6f,
                Body = CreatureBody.Spider, Sounds = EnemySounds.Set.Spider,
                Skin = new Color(0.62f, 0.80f, 0.96f), Cloth = new Color(0.86f, 0.95f, 1.0f), Pants = new Color(0.20f, 0.50f, 0.95f), Eyes = new Color(0.4f, 1.0f, 1.0f),
                DropChance = 1f, RareBonus = 0.9f, Drops = 4,
                IsBoss = true, Boss = BossStyle.FrostQueen
            },

            // Later additions (weight 0: only the areas that list them spawn them).
            new EnemyKind
            {
                Name = "Forest Shaman", SpawnWeight = 0f,
                MaxHealth = 30f, Experience = 34,
                Style = EnemyAttackStyle.Ranged, DamageType = DamageType.Physical,
                Damage = 6f, AttackCooldown = 2.2f, AttackRange = 9f, ProjectileSpeed = 10f,
                SpeedRatio = 0.62f, Scale = 0.95f,
                Cloth = new Color(0.30f, 0.45f, 0.18f), Skin = new Color(0.62f, 0.50f, 0.36f), Pants = new Color(0.36f, 0.26f, 0.14f), Eyes = new Color(0.6f, 1.0f, 0.4f),
                StaffOrb = new Color(0.45f, 1.0f, 0.45f),
                Skill = EnemySkill.WarCry, SkillCooldown = 10f,
                DropChance = 0.4f
            },
            new EnemyKind
            {
                Name = "Necromancer", SpawnWeight = 0f, Undead = true,
                MaxHealth = 32f, Experience = 40,
                Style = EnemyAttackStyle.Ranged, DamageType = DamageType.Cold,
                Damage = 8f, AttackCooldown = 2.2f, AttackRange = 10f, ProjectileSpeed = 10f,
                SpeedRatio = 0.6f, Scale = 1.0f,
                Cloth = new Color(0.18f, 0.12f, 0.22f), Skin = new Color(0.70f, 0.72f, 0.66f), Pants = new Color(0.10f, 0.08f, 0.12f), Eyes = new Color(0.55f, 1.0f, 0.5f), HideHorns = true,
                StaffOrb = new Color(0.55f, 1.0f, 0.55f),
                Skill = EnemySkill.Summon, SkillCooldown = 12f,
                DropChance = 0.45f, RareBonus = 0.05f
            },
            new EnemyKind
            {
                Name = "Skeleton Archer", SpawnWeight = 0f, Undead = true,
                MaxHealth = 22f, Experience = 28,
                Style = EnemyAttackStyle.Ranged, DamageType = DamageType.Physical,
                Damage = 7f, AttackCooldown = 1.9f, AttackRange = 12f, ProjectileSpeed = 20f,
                SpeedRatio = 0.75f, Scale = 0.95f,
                Cloth = new Color(0.82f, 0.80f, 0.72f), Skin = new Color(0.90f, 0.88f, 0.80f), Pants = new Color(0.32f, 0.30f, 0.27f), Eyes = new Color(1.0f, 0.25f, 0.1f), HideHorns = true,
                Gear = new[] { "short_bow" },
                Bow = true,
                Skill = EnemySkill.Volley, SkillCooldown = 9f,
                DropChance = 0.3f
            },
            new EnemyKind
            {
                Name = "Frost Giant", SpawnWeight = 0f,
                MaxHealth = 120f, Experience = 70,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Cold,
                Damage = 16f, AttackCooldown = 2.4f, AttackRange = 3.0f,
                SpeedRatio = 0.6f, Scale = 1.6f,
                Cloth = new Color(0.55f, 0.70f, 0.85f), Skin = new Color(0.78f, 0.88f, 0.96f), Pants = new Color(0.30f, 0.38f, 0.50f), Eyes = new Color(0.4f, 0.9f, 1.0f),
                Gear = new[] { "iron_mace" },
                Skill = EnemySkill.Slam, SkillCooldown = 7f,
                DropChance = 0.8f, RareBonus = 0.2f
            },

            // Creatures: bodies of their own instead of the humanoid rig (weight 0: the areas pick them).
            new EnemyKind
            {
                Name = "Giant Spider", SpawnWeight = 0f,
                MaxHealth = 26f, Experience = 26,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Physical,
                Damage = 5f, AttackCooldown = 1.0f, AttackRange = 1.9f,
                SpeedRatio = 1.0f, Scale = 0.85f,
                Body = CreatureBody.Spider, Sounds = EnemySounds.Set.Spider,
                Skin = new Color(0.24f, 0.19f, 0.15f), Cloth = new Color(0.33f, 0.24f, 0.17f), Pants = new Color(0.85f, 0.48f, 0.12f), Eyes = new Color(1.0f, 0.25f, 0.15f),
                Skill = EnemySkill.Leap, SkillCooldown = 7f,
                DropChance = 0.3f
            },
            new EnemyKind
            {
                Name = "Dire Wolf", SpawnWeight = 0f,
                MaxHealth = 30f, Experience = 28,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Physical,
                Damage = 6f, AttackCooldown = 1.1f, AttackRange = 2.0f,
                SpeedRatio = 1.05f, Scale = 0.95f,
                Body = CreatureBody.Wolf, Sounds = EnemySounds.Set.Wolf, PackSize = 2,
                Skin = new Color(0.46f, 0.43f, 0.40f), Cloth = new Color(0.30f, 0.28f, 0.27f), Pants = new Color(0.80f, 0.77f, 0.72f), Eyes = new Color(1.0f, 0.8f, 0.2f),
                Skill = EnemySkill.Leap, SkillCooldown = 9f,
                DropChance = 0.3f
            },
            new EnemyKind
            {
                Name = "Bog Slime", SpawnWeight = 0f,
                MaxHealth = 38f, Experience = 26,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Physical,
                Damage = 7f, AttackCooldown = 1.6f, AttackRange = 1.9f,
                SpeedRatio = 0.55f, Scale = 1.0f,
                Body = CreatureBody.Slime, Sounds = EnemySounds.Set.Slime, SplitInto = SlimelingIndex,
                Skin = new Color(0.46f, 0.74f, 0.26f), Cloth = new Color(0.28f, 0.48f, 0.16f), Pants = new Color(0.60f, 0.45f, 0.25f), Eyes = new Color(0.08f, 0.10f, 0.05f),
                Skill = EnemySkill.Leap, SkillCooldown = 10f,
                DropChance = 0.3f
            },
            new EnemyKind
            {
                Name = "Slimeling", SpawnWeight = 0f,
                MaxHealth = 11f, Experience = 6,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Physical,
                Damage = 3f, AttackCooldown = 1.2f, AttackRange = 1.5f,
                SpeedRatio = 0.8f, Scale = 0.5f,
                Body = CreatureBody.Slime, Sounds = EnemySounds.Set.Slime,
                Skin = new Color(0.46f, 0.74f, 0.26f), Cloth = new Color(0.28f, 0.48f, 0.16f), Pants = new Color(0.60f, 0.45f, 0.25f), Eyes = new Color(0.08f, 0.10f, 0.05f),
                DropChance = 0.06f
            },
            new EnemyKind
            {
                Name = "Grave Bat", SpawnWeight = 0f,
                MaxHealth = 16f, Experience = 16,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Physical,
                Damage = 4f, AttackCooldown = 0.9f, AttackRange = 1.9f,
                SpeedRatio = 1.1f, Scale = 0.8f,
                Body = CreatureBody.Bat, Sounds = EnemySounds.Set.Bat, PackSize = 2,
                Skin = new Color(0.22f, 0.18f, 0.20f), Cloth = new Color(0.36f, 0.22f, 0.27f), Pants = new Color(0.55f, 0.42f, 0.44f), Eyes = new Color(1.0f, 0.2f, 0.2f),
                Skill = EnemySkill.Charge, SkillCooldown = 6f,
                DropChance = 0.18f
            },
            new EnemyKind
            {
                Name = "Corpse Ooze", SpawnWeight = 0f,
                MaxHealth = 44f, Experience = 32,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Cold,
                Damage = 8f, AttackCooldown = 1.6f, AttackRange = 1.9f,
                SpeedRatio = 0.55f, Scale = 1.05f,
                Body = CreatureBody.Slime, Sounds = EnemySounds.Set.Slime, SplitInto = OozelingIndex,
                Skin = new Color(0.52f, 0.36f, 0.60f), Cloth = new Color(0.30f, 0.20f, 0.36f), Pants = new Color(0.90f, 0.88f, 0.80f), Eyes = new Color(0.5f, 1.0f, 0.6f),
                Skill = EnemySkill.Leap, SkillCooldown = 10f,
                DropChance = 0.35f
            },
            new EnemyKind
            {
                Name = "Oozeling", SpawnWeight = 0f,
                MaxHealth = 13f, Experience = 8,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Cold,
                Damage = 3f, AttackCooldown = 1.2f, AttackRange = 1.5f,
                SpeedRatio = 0.8f, Scale = 0.52f,
                Body = CreatureBody.Slime, Sounds = EnemySounds.Set.Slime,
                Skin = new Color(0.52f, 0.36f, 0.60f), Cloth = new Color(0.30f, 0.20f, 0.36f), Pants = new Color(0.90f, 0.88f, 0.80f), Eyes = new Color(0.5f, 1.0f, 0.6f),
                DropChance = 0.06f
            },
            new EnemyKind
            {
                Name = "Crypt Spider", SpawnWeight = 0f,
                MaxHealth = 32f, Experience = 32,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Cold,
                Damage = 7f, AttackCooldown = 1.1f, AttackRange = 2.0f,
                SpeedRatio = 0.95f, Scale = 1.0f,
                Body = CreatureBody.Spider, Sounds = EnemySounds.Set.Spider,
                Skin = new Color(0.76f, 0.73f, 0.66f), Cloth = new Color(0.30f, 0.27f, 0.36f), Pants = new Color(0.45f, 1.0f, 0.6f), Eyes = new Color(0.45f, 1.0f, 0.6f),
                Skill = EnemySkill.Leap, SkillCooldown = 7f,
                DropChance = 0.35f
            },
            new EnemyKind
            {
                Name = "Magma Beetle", SpawnWeight = 0f,
                MaxHealth = 55f, Experience = 45,
                Style = EnemyAttackStyle.Ranged, DamageType = DamageType.Fire,
                Damage = 9f, AttackCooldown = 2.4f, AttackRange = 9f, ProjectileSpeed = 9f,
                SpeedRatio = 0.5f, Scale = 1.1f,
                Body = CreatureBody.Beetle, Sounds = EnemySounds.Set.Beetle,
                Skin = new Color(0.20f, 0.14f, 0.12f), Cloth = new Color(0.28f, 0.19f, 0.15f), Pants = new Color(1.0f, 0.5f, 0.1f), Eyes = new Color(1.0f, 0.65f, 0.15f),
                StaffOrb = new Color(1.0f, 0.45f, 0.10f),
                Skill = EnemySkill.Strike, SkillCooldown = 9f,
                DropChance = 0.45f, RareBonus = 0.05f
            },
            new EnemyKind
            {
                Name = "Hellhound", SpawnWeight = 0f,
                MaxHealth = 46f, Experience = 40,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Fire,
                Damage = 9f, AttackCooldown = 1.1f, AttackRange = 2.1f,
                SpeedRatio = 1.1f, Scale = 1.05f,
                Body = CreatureBody.Wolf, Sounds = EnemySounds.Set.Hound, PackSize = 2,
                Skin = new Color(0.17f, 0.13f, 0.12f), Cloth = new Color(0.38f, 0.12f, 0.06f), Pants = new Color(1.0f, 0.45f, 0.1f), Eyes = new Color(1.0f, 0.5f, 0.1f),
                Skill = EnemySkill.Leap, SkillCooldown = 8f,
                DropChance = 0.4f
            },
            new EnemyKind
            {
                Name = "Frost Wolf", SpawnWeight = 0f,
                MaxHealth = 50f, Experience = 44,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Cold,
                Damage = 9f, AttackCooldown = 1.1f, AttackRange = 2.1f,
                SpeedRatio = 1.05f, Scale = 1.05f,
                Body = CreatureBody.Wolf, Sounds = EnemySounds.Set.Wolf, PackSize = 2,
                Skin = new Color(0.70f, 0.78f, 0.88f), Cloth = new Color(0.42f, 0.52f, 0.66f), Pants = new Color(0.95f, 0.97f, 1.0f), Eyes = new Color(0.4f, 0.9f, 1.0f),
                Skill = EnemySkill.Leap, SkillCooldown = 8f,
                DropChance = 0.4f
            },
            new EnemyKind
            {
                Name = "Ice Crawler", SpawnWeight = 0f,
                MaxHealth = 44f, Experience = 40,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Cold,
                Damage = 8f, AttackCooldown = 1.0f, AttackRange = 2.0f,
                SpeedRatio = 0.95f, Scale = 1.05f,
                Body = CreatureBody.Spider, Sounds = EnemySounds.Set.Spider,
                Skin = new Color(0.58f, 0.74f, 0.90f), Cloth = new Color(0.82f, 0.92f, 1.0f), Pants = new Color(0.25f, 0.55f, 0.90f), Eyes = new Color(0.5f, 0.95f, 1.0f),
                Skill = EnemySkill.Leap, SkillCooldown = 7f,
                DropChance = 0.4f
            },

            // The player's minions (see Skills.Minion): never spawned as enemies. Only their look
            // is used here (on the player's side and for spectators); their numbers are the minion's.
            new EnemyKind
            {
                Name = "Skeleton Warrior", SpawnWeight = 0f,
                MaxHealth = 24f, Style = EnemyAttackStyle.Melee, AttackRange = 2.1f, SpeedRatio = 0.9f, Scale = 0.95f,
                Cloth = new Color(0.62f, 0.78f, 0.62f), Skin = new Color(0.92f, 0.90f, 0.82f), Pants = new Color(0.22f, 0.30f, 0.24f), Eyes = new Color(0.45f, 1.0f, 0.5f), HideHorns = true,
                Gear = new[] { "rusty_sword", "wooden_shield" }
            },
            new EnemyKind
            {
                Name = "Skeleton Mage", SpawnWeight = 0f,
                MaxHealth = 14f, Style = EnemyAttackStyle.Ranged, AttackRange = 8.5f, ProjectileSpeed = 14f, SpeedRatio = 0.85f, Scale = 0.92f,
                Cloth = new Color(0.25f, 0.32f, 0.55f), Skin = new Color(0.92f, 0.90f, 0.82f), Pants = new Color(0.16f, 0.18f, 0.30f), Eyes = new Color(0.5f, 0.85f, 1.0f), HideHorns = true,
                StaffOrb = new Color(0.5f, 0.85f, 1.0f)
            },
            new EnemyKind
            {
                Name = "Spirit Wolf", SpawnWeight = 0f,
                MaxHealth = 30f, Style = EnemyAttackStyle.Melee, AttackRange = 2f, SpeedRatio = 1.2f, Scale = 0.9f,
                Body = CreatureBody.Wolf, Sounds = EnemySounds.Set.Wolf,
                Skin = new Color(0.62f, 0.85f, 1.0f), Cloth = new Color(0.82f, 0.95f, 1.0f), Pants = new Color(0.40f, 0.65f, 0.95f), Eyes = new Color(0.85f, 1.0f, 1.0f)
            },
            new EnemyKind
            {
                Name = "Bone Golem", SpawnWeight = 0f,
                MaxHealth = 85f, Style = EnemyAttackStyle.Melee, AttackRange = 2.6f, SpeedRatio = 0.7f, Scale = 1.55f,
                Cloth = new Color(0.78f, 0.74f, 0.62f), Skin = new Color(0.94f, 0.91f, 0.80f), Pants = new Color(0.55f, 0.50f, 0.40f), Eyes = new Color(0.45f, 1.0f, 0.5f)
            },
            new EnemyKind
            {
                // The act boss, in its first phase: a stooped, hooded old man with a crook and a
                // lantern. Numbers are placeholders until the fight itself is in.
                Name = "The Shepherd", SpawnWeight = 0f, Tempo = 1.6f, BossEnrageDamage = 44f / 29f, BossEnrageDamageTaken = 0.8f,
                MaxHealth = 1600f, Armour = 600f,
                FireResistance = 30f, ColdResistance = 30f, LightningResistance = 30f, Experience = 400,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Physical,
                Damage = 7.5f, AttackCooldown = 1.4f, AttackRange = 3.6f,
                SpeedRatio = 1.05f, Scale = 1.5f,
                Cloth = new Color(0.36f, 0.35f, 0.33f), Skin = new Color(0.55f, 0.58f, 0.50f), Pants = new Color(0.30f, 0.29f, 0.27f), Eyes = new Color(0.9f, 0.8f, 0.4f),
                HideHorns = true,
                DropChance = 1f, RareBonus = 1f, Drops = 5,
                IsBoss = true, Boss = BossStyle.Shepherd
            },
            // Append only: kind indices are also used by spectator snapshots.
            new EnemyKind
            {
                Name = "Briarbound", SpawnWeight = 0f,
                MaxHealth = 52f, Armour = 80f, PoisonResistance = 35f, Experience = 42,
                Style = EnemyAttackStyle.Ranged, DamageType = DamageType.Physical,
                Damage = 12f, AttackCooldown = 2.4f, AttackRange = 10f, ProjectileSpeed = 12f,
                SpeedRatio = 0.48f, Body = CreatureBody.Briarbound,
                StaffOrb = new Color(0.42f, 0.56f, 0.22f),
                Skin = new Color(0.20f, 0.12f, 0.10f), Cloth = new Color(0.40f, 0.30f, 0.18f),
                Pants = new Color(0.42f, 0.65f, 0.23f), Eyes = new Color(0.85f, 1f, 0.32f),
                Skill = EnemySkill.ThornGarden, SkillCooldown = 5f, DropChance = 0.45f
            },
            new EnemyKind
            {
                Name = "Grave Siren", SpawnWeight = 0f, Undead = true,
                MaxHealth = 65f, Armour = 120f, ColdResistance = 25f, Experience = 48,
                Style = EnemyAttackStyle.Ranged, DamageType = DamageType.Cold,
                Damage = 14f, AttackCooldown = 2.5f, AttackRange = 9f, ProjectileSpeed = 10f,
                SpeedRatio = 0.45f, Body = CreatureBody.GraveSiren,
                StaffOrb = new Color(0.45f, 0.75f, 0.68f),
                Skin = new Color(0.26f, 0.30f, 0.30f), Cloth = new Color(0.38f, 0.40f, 0.38f),
                Pants = new Color(0.12f, 0.16f, 0.18f), Eyes = new Color(0.45f, 1f, 0.85f),
                Skill = EnemySkill.Wail, SkillCooldown = 4.5f, DropChance = 0.5f
            },
            new EnemyKind
            {
                Name = "Rime Stalker", SpawnWeight = 0f,
                MaxHealth = 48f, ColdResistance = 55f, Experience = 48,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Cold,
                Damage = 14f, AttackCooldown = 1.2f, AttackRange = 2.3f,
                SpeedRatio = 0.95f, Body = CreatureBody.RimeStalker,
                Skin = new Color(0.18f, 0.34f, 0.52f), Cloth = new Color(0.48f, 0.78f, 0.94f),
                Pants = new Color(0.36f, 0.52f, 0.62f), Eyes = new Color(0.85f, 1f, 1f),
                Skill = EnemySkill.FrostFissures, SkillCooldown = 4f, DropChance = 0.45f
            },
            new EnemyKind
            {
                Name = "Cinder Penitent", SpawnWeight = 0f,
                MaxHealth = 62f, FireResistance = 50f, Experience = 48,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Fire,
                Damage = 12f, AttackCooldown = 1.8f, AttackRange = 2.2f,
                SpeedRatio = 0.48f, Body = CreatureBody.CinderPenitent,
                Skin = new Color(0.17f, 0.13f, 0.12f), Cloth = new Color(0.32f, 0.23f, 0.18f),
                Pants = new Color(0.48f, 0.17f, 0.08f), Eyes = new Color(1f, 0.47f, 0.12f),
                Skill = EnemySkill.LastOffering, SkillCooldown = 4f, DropChance = 0.45f
            },
            new EnemyKind
            {
                Name = "Hollowmaw", SpawnWeight = 0f,
                MaxHealth = 75f, Armour = 60f, Experience = 48,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Physical,
                Damage = 16f, AttackCooldown = 1.7f, AttackRange = 2.4f,
                SpeedRatio = 0.67f, Body = CreatureBody.Hollowmaw,
                Skin = new Color(0.63f, 0.60f, 0.51f), Cloth = new Color(0.38f, 0.35f, 0.29f),
                Pants = new Color(0.22f, 0.12f, 0.12f), Eyes = new Color(0.12f, 0.07f, 0.06f),
                Skill = EnemySkill.DraggingBreath, SkillCooldown = 4.5f, DropChance = 0.45f
            },
            new EnemyKind
            {
                Name = "Barrow Castellan", SpawnWeight = 0f, Undead = true,
                MaxHealth = 90f, Armour = 220f, ColdResistance = 20f, Experience = 58,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Physical,
                Damage = 18f, AttackCooldown = 1.8f, AttackRange = 3f,
                SpeedRatio = 0.60f, Body = CreatureBody.BarrowCastellan,
                Skin = new Color(0.31f, 0.33f, 0.29f), Cloth = new Color(0.24f, 0.29f, 0.28f),
                Pants = new Color(0.25f, 0.20f, 0.16f), Eyes = new Color(0.56f, 0.77f, 0.66f),
                Skill = EnemySkill.Graveward, SkillCooldown = 6f, DropChance = 0.55f, RareBonus = 0.1f
            },
        };

        public const int SlimelingIndex = 20;
        public const int OozelingIndex = 23;
        public const int ShamanIndex = 13;
        public const int NecromancerIndex = 14;
        public const int SkeletonArcherIndex = 15;
        public const int FrostGiantIndex = 16;
        public const int SkeletonIndex = 7;

        /// <summary>The index of the kind with this name, or -1.</summary>
        public static int IndexOf(string name)
        {
            for (int k = 0; k < All.Length; k++)
            {
                if (All[k].Name == name)
                    return k;
            }
            return -1;
        }

        public static EnemyKind Get(int index)
        {
            return All[Mathf.Clamp(index, 0, All.Length - 1)];
        }

        /// <summary>A random kind index, by spawn weight (or by an area's own weights, one per kind).</summary>
        public static int PickIndex(float[] weights = null)
        {
            float total = 0f;
            for (int k = 0; k < All.Length; k++)
                total += Weight(k, weights);

            float roll = Random.value * total;
            for (int k = 0; k < All.Length; k++)
            {
                roll -= Weight(k, weights);
                if (roll <= 0f && Weight(k, weights) > 0f)
                    return k;
            }
            return 0;
        }

        private static float Weight(int k, float[] weights)
        {
            return weights != null ? (k < weights.Length ? weights[k] : 0f) : All[k].SpawnWeight;
        }

        /// <summary>
        /// How much tougher each monster level makes an enemy: life, damage and the experience it's worth.
        /// </summary>
        public static float LifeScale(int level) => 1f + 0.35f * (Mathf.Max(1, level) - 1);
        public static float DamageScale(int level, EnemyKind kind) =>
            (kind != null && kind.IsBoss ? BossDamageMultiplier : RegularDamageMultiplier) * (1f + 0.22f * (Mathf.Max(1, level) - 1));

        /// <summary>Every enemy hit (2026-10-03, with much weaker armour/evasion): ordinary enemies x3, bosses x2.</summary>
        public const float RegularDamageMultiplier = 3f;
        public const float BossDamageMultiplier = 2f;
        public static float ExperienceScale(int level) => 1f + 0.4f * (Mathf.Max(1, level) - 1);

        /// <summary>Makes a freshly spawned enemy this kind and level: its look and all its gameplay numbers.</summary>
        public static void Apply(GameObject enemy, int index, int level = 1)
        {
            EnemyKind kind = Get(index);
            ApplyLook(enemy, kind);

            enemy.GetComponent<EnemyHealth>()?.Configure(index, kind, level);
            enemy.GetComponent<EnemyController>()?.Configure(kind);
            enemy.GetComponent<EnemyCombat>()?.Configure(kind, level);

            EnemySkills skills = enemy.GetComponent<EnemySkills>();
            if (kind.Skill != EnemySkill.None && !kind.IsBoss)
            {
                if (skills == null)
                    skills = enemy.AddComponent<EnemySkills>();
                skills.Configure(kind, level);
            }
            else if (skills != null)
            {
                skills.enabled = false;
            }
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>Just the look (spectator puppets get this; their numbers come over the wire).</summary>
        public static void ApplyLook(GameObject enemy, EnemyKind kind)
        {
            enemy.transform.localScale = Vector3.one * kind.Scale;

            Transform model = enemy.transform.Find("Model");
            if (model == null)
                model = enemy.transform;

            if (kind.IsCreature)
            {
                BuildCreature(enemy, model, kind);
                return;
            }

            // Recolour first, so the gear added below keeps its own colours.
            Recolour(model, kind);

            if (kind.HideHorns)
            {
                foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "HornL" || t.name == "HornR")
                        t.gameObject.SetActive(false);
                }
            }

            if (kind.Boss == BossStyle.Shepherd)
                ShepherdLook.Build(model);

            if (kind.Gear.Length > 0)
            {
                var gear = new EquipmentSet();
                foreach (string id in kind.Gear)
                {
                    ItemData item = ItemGenerator.Display(id, null, ItemRarity.Normal);
                    if (item == null)
                        continue;
                    foreach (EquipSlot slot in SlotRules.AllSlots)
                    {
                        if (gear.Get(slot) == null && gear.TryEquip(slot, item, out _))
                            break;
                    }
                }

                EquipmentVisuals visuals = model.gameObject.AddComponent<EquipmentVisuals>();
                visuals.Bind(gear);
            }

            // Archers carry the Short Bow from their gear list; casters get a staff.
            if (kind.IsRanged && !kind.Bow)
                AddStaff(model, kind.StaffOrb);
        }

        // The smallest a creature's collider gets, in metres (see BuildCreature).
        private const float MinHitHeight = 1.8f;
        private const float MinHitRadius = 0.4f;

        // Replaces the humanoid rig under the model with the kind's own body. The rig's stylised
        // meshes (the faceted head, the horn cone) are kept for the new body's parts.
        private static void BuildCreature(GameObject enemy, Transform model, EnemyKind kind)
        {
            var meshes = new System.Collections.Generic.Dictionary<string, Mesh>();
            foreach (MeshFilter mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh != null && !meshes.ContainsKey(mf.sharedMesh.name))
                    meshes[mf.sharedMesh.name] = mf.sharedMesh;
            }

            // Out of the hierarchy at once (Destroy only happens at the end of the frame), so
            // nothing built this frame - an outline, a renderer list - picks up the old parts.
            for (int i = model.childCount - 1; i >= 0; i--)
            {
                Transform child = model.GetChild(i);
                child.gameObject.SetActive(false);
                child.SetParent(null, false);
                Object.Destroy(child.gameObject);
            }

            CharacterWalkAnimator walk = model.GetComponent<CharacterWalkAnimator>();
            if (walk != null)
                walk.enabled = false;

            CreatureBuilder.Build(model, kind.Body, new CreatureBuilder.Palette
            {
                Main = kind.Skin,
                Second = kind.Cloth,
                Accent = kind.Pants,
                Eyes = kind.Eyes
            }, meshes);

            // Low bodies get a wider collider than the humanoid's (its bottom stays at the feet),
            // but never a short one: arrows and bolts fly at chest height and melee sweeps from
            // there too, so even a slimeling's collider stands as tall as a person's chest. A bat
            // keeps the prefab's, since it hangs in the air where the player swings.
            CharacterController cc = enemy.GetComponent<CharacterController>();
            if (cc != null && kind.Body != CreatureBody.Bat)
            {
                float scale = Mathf.Max(0.1f, kind.Scale);
                float height = Mathf.Max(kind.Body == CreatureBody.Wolf ? 1.3f : 1.1f, MinHitHeight / scale);
                cc.height = height;
                cc.radius = Mathf.Max(kind.Body == CreatureBody.Slime ? 0.55f : 0.6f, MinHitRadius / scale);
                cc.center = new Vector3(0f, -1f + height * 0.5f, 0f);
                // A low, round body would otherwise step right up onto whatever it bumps into.
                cc.stepOffset = 0.15f;
            }
        }

        // Swaps the colour of each of the rig's own materials (cloth, skin, pants, eyes) for the
        // kind's, through property blocks so the shared materials themselves stay untouched.
        private static void Recolour(Transform root, EnemyKind kind)
        {
            var block = new MaterialPropertyBlock();
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                Material material = r.sharedMaterial;
                if (material == null)
                    continue;

                Color color = ColourFor(material.name, kind);
                if (color.a <= 0f)
                    continue;

                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, color);
                block.SetColor(ColorId, color);
                r.SetPropertyBlock(block);
            }
        }

        private static Color ColourFor(string materialName, EnemyKind kind)
        {
            if (materialName.StartsWith("EnemyCloth"))
                return kind.Cloth;
            if (materialName.StartsWith("EnemySkin"))
                return kind.Skin;
            if (materialName.StartsWith("EnemyPants"))
                return kind.Pants;
            if (materialName.StartsWith("Eye"))
                return kind.Eyes;
            return Color.clear;
        }

        // A plain shaft with a glowing orb on top, in the caster's element colour, held in the
        // weapon hand. Built from primitives (colliders removed: it must not block hits or movement).
        /// <summary>Name of the glowing orb on a caster's staff (bolts start there).</summary>
        public const string StaffOrbName = "StaffOrb";

        private static void AddStaff(Transform model, Color orb)
        {
            Transform hand = FindChild(model, "Socket_MainHand") ?? FindChild(model, "Socket_HandR");
            if (hand == null)
                return;

            var staff = new GameObject("Staff");
            staff.transform.SetParent(hand, false);
            staff.AddComponent<UprightStaff>().Bind(model.parent != null ? model.parent : model);

            GameObject shaft = RuntimePrimitives.Create(PrimitiveType.Cylinder, staff.transform, new Color(0.35f, 0.24f, 0.14f));
            shaft.transform.localScale = new Vector3(0.09f, 0.8f, 0.09f);
            shaft.transform.localPosition = new Vector3(0f, 0.25f, 0f);

            GameObject top = RuntimePrimitives.Create(PrimitiveType.Sphere, staff.transform, orb);
            top.name = StaffOrbName;
            top.transform.localScale = Vector3.one * 0.32f;
            top.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                    return t;
            }
            return null;
        }
    }
}
