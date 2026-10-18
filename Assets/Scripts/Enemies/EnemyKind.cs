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
        Graveward,
        WebSpit,    // lobs a web that sticks where it lands: slows whoever walks through it
        FireBreath, // a cone of flame in front of it, after a short wind-up
        Howl,       // a chilling ring round itself; hastens the pack it runs with
        IceSpikes,  // a line of ice spikes erupting from it towards the player
        Shriek,     // a screech round itself that hurls the player back
        BoneWhirl,  // spins its great weapon and wades at the player, cutting all round it
        BonePrison, // a ring of bone spikes closes round the player: anyone inside can't leave for a while
        DesertCharge,
        ScorpionSting,
        SandBurrow,
        JavelinFan,
        RollingSandstorm,
        Tollkeeper,
        SaltBurst,
        BrineSpit,
        DredgeFault,
        JarShatter,
        SluiceJet
    }

    public enum BossStyle
    {
        None,
        Gravelord,   // Mortis, a gravedigger: burrows, opens graves under the player, throws his coffin full of dead
        Warlord,     // empty burning armour: drags a line of fire, tosses embers, cleaves, its plates fly in phase two
        FrostQueen,  // Rimeheart, a floating heart of ice: pulses rings of frost, spirals shards, raises mirrors
        Shepherd,    // the act boss: a hooded shepherd with a snake crook and a lantern (ShepherdLook); three phases
        Bramblesow,  // a boar with a thicket on its back: charges (stunned if it hits a wall), roots, thorns
        TunnelKing,  // Vex: smoke bombs and backstabs, tripwires, spore mushrooms, knife fans
        BellRinger,  // tolls rings of sound, drops its bell over the player, calls bats
        SunIdol,     // a statue that barely moves: sweeping sunbeams, stone hands, sinks and rises elsewhere
        Huntress,    // Hrimgar: spear throws, nets, pounces, a pack of frost wolves
        SaltMatriarch,
        DryWellKeeper,
        SluiceWarden,
        LastEngineer,
        HarvestMother,
        Ishara,
        Ossan,
        FirstRoot
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

        // Level-one baselines. Elite/boss bonuses are independent of area level.
        public float HealthMultiplier = 1f;
        public float DamageMultiplier = 1f;
        public float MaxHealth;
        public float Armour;
        public float FireResistance, ColdResistance, LightningResistance, PoisonResistance;
        public int Experience;

        public EnemyAttackStyle Style;
        // Specials use DamageType; physical claws/weapons can override only the plain attack.
        public DamageType DamageType;
        public DamageType? BasicDamageType;
        public DamageType AttackDamageType => BasicDamageType ?? DamageType;
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
        public bool Javelin;
        public bool RainOfArrows; // archers: each shot is loosed skywards and rains down on a marked spot
        public bool Skeletal;     // humanoids: bones instead of flesh (SkeletonLook)
        public bool ReachingArms; // humanoids: arms held out straight in front (the zombie shamble), else hanging

        /// <summary>The first killing blow drops it into a bone pile that stands up again unless smashed.</summary>
        public bool Reassembles;

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
        /// <summary>Creatures: extra parts on the shared body (bone plating, ice, horns) so a later area's kind isn't just a recolour.</summary>
        public CreatureVariant Variant = CreatureVariant.Plain;
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
                    case CreatureBody.Scorpion: return 1.5f;
                    case CreatureBody.Sandlurker: return 1.1f;
                    case CreatureBody.SaltCrab: return 1.2f;
                    case CreatureBody.BrineSpitter: return 1.5f;
                    case CreatureBody.Briarbound: return 2.6f;
                    case CreatureBody.GraveSiren: return 2.8f;
                    case CreatureBody.RimeStalker: return 2.5f;
                    case CreatureBody.CinderPenitent: return 1.8f;
                    case CreatureBody.Hollowmaw: return 2.5f;
                    case CreatureBody.BarrowCastellan: return 2.7f;
                    case CreatureBody.Gravedigger: return 2.5f;
                    case CreatureBody.HollowArmor: return 2.5f;
                    case CreatureBody.FrostHeart: return 3.1f;
                    case CreatureBody.Boar: return 1.7f;
                    case CreatureBody.TunnelKing: return 1.6f;
                    case CreatureBody.BellRinger: return 2.9f;
                    case CreatureBody.SunIdol: return 3.3f;
                    case CreatureBody.Huntress: return 2.3f;
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
    public static partial class EnemyKinds
    {
        public static readonly EnemyKind[] All =
        {
            new EnemyKind
            {
                Name = "Zombie", SpawnWeight = 30f, Undead = true, ReachingArms = true,
                MaxHealth = 25.20492f, Experience = 20,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Physical,
                Damage = 15.89362f, AttackCooldown = 1.4f, AttackRange = 2.0f,
                SpeedRatio = 0.58f,
                DropChance = 0.25f
            },
            new EnemyKind
            {
                Name = "Raider", SpawnWeight = 18f,
                MaxHealth = 42f, Experience = 30,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Physical,
                Damage = 15f, AttackCooldown = 0.9f, AttackRange = 2.0f,
                SpeedRatio = 0.85f, Scale = 0.95f,
                Cloth = new Color(0.62f, 0.42f, 0.25f), Skin = new Color(0.78f, 0.60f, 0.45f), Eyes = new Color(0.15f, 0.10f, 0.08f), HideHorns = true,
                Gear = new[] { "bronze_helmet", "rusty_sword", "wooden_shield" },
                Skill = EnemySkill.Charge, SkillCooldown = 7f,
                DropChance = 0.35f
            },
            new EnemyKind
            {
                Name = "Brute", HealthMultiplier = 2.5f, DamageMultiplier = 1f, SpawnWeight = 12f,
                MaxHealth = 35.5814f, Experience = 55,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Physical,
                Damage = 32f, AttackCooldown = 2.2f, AttackRange = 2.6f,
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
                Damage = 21f, AttackCooldown = 1.8f, AttackRange = 12f, ProjectileSpeed = 20f,
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
                MaxHealth = 25.50633f, Experience = 30,
                Style = EnemyAttackStyle.Ranged, DamageType = DamageType.Fire,
                Damage = 27f, AttackCooldown = 2.0f, AttackRange = 10f, ProjectileSpeed = 11f,
                SpeedRatio = 0.62f, Scale = 0.95f,
                Cloth = new Color(0.80f, 0.25f, 0.05f), Skin = new Color(0.55f, 0.35f, 0.30f), Eyes = new Color(1.0f, 0.6f, 0.1f), HideHorns = true,
                StaffOrb = new Color(1.0f, 0.45f, 0.10f),
                Skill = EnemySkill.Strike, SkillCooldown = 9f,
                DropChance = 0.35f
            },
            new EnemyKind
            {
                Name = "Frost Caster", SpawnWeight = 10f,
                MaxHealth = 27.17884f, Experience = 30,
                Style = EnemyAttackStyle.Ranged, DamageType = DamageType.Cold,
                Damage = 22.23091f, AttackCooldown = 2.0f, AttackRange = 10f, ProjectileSpeed = 10f,
                SpeedRatio = 0.62f, Scale = 0.95f,
                Cloth = new Color(0.15f, 0.35f, 0.80f), Skin = new Color(0.75f, 0.85f, 0.95f), Eyes = new Color(0.5f, 0.95f, 1.0f), HideHorns = true,
                StaffOrb = new Color(0.55f, 0.85f, 1.0f),
                Skill = EnemySkill.Strike, SkillCooldown = 9f,
                DropChance = 0.35f
            },
            new EnemyKind
            {
                Name = "Storm Caster", SpawnWeight = 10f,
                MaxHealth = 21.74419f, Experience = 30,
                Style = EnemyAttackStyle.Ranged, DamageType = DamageType.Lightning,
                Damage = 24f, AttackCooldown = 1.6f, AttackRange = 11f, ProjectileSpeed = 16f,
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
                MaxHealth = 20.16393f, Experience = 26,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Physical,
                Damage = 15.89362f, AttackCooldown = 1.0f, AttackRange = 2.0f,
                SpeedRatio = 1.15f, Scale = 0.95f,
                Cloth = new Color(0.82f, 0.80f, 0.72f), Skin = new Color(0.90f, 0.88f, 0.80f), Pants = new Color(0.32f, 0.30f, 0.27f), Eyes = new Color(1.0f, 0.25f, 0.1f), HideHorns = true,
                Gear = new[] { "rusty_sword" }, Weapon = WeaponType.Sword, Skeletal = true,
                DropChance = 0.3f
            },
            new EnemyKind
            {
                Name = "Wraith", SpawnWeight = 0f, Undead = true,
                MaxHealth = 28.56557f, Experience = 34,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Cold,
                Damage = 21.19149f, AttackCooldown = 1.5f, AttackRange = 2.2f,
                SpeedRatio = 0.95f, Scale = 1.05f,
                Cloth = new Color(0.58f, 0.70f, 0.64f), Skin = new Color(0.80f, 0.95f, 0.90f), Pants = new Color(0.40f, 0.50f, 0.47f), Eyes = new Color(0.4f, 1.0f, 0.7f), HideHorns = true,
                Skill = EnemySkill.Blink, SkillCooldown = 9f,
                DropChance = 0.35f
            },
            new EnemyKind
            {
                Name = "Ember Knight", HealthMultiplier = 1.5f, SpawnWeight = 0f,
                MaxHealth = 45.78059f, Experience = 50,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Fire, BasicDamageType = DamageType.Physical,
                Damage = 36f, AttackCooldown = 1.8f, AttackRange = 2.4f,
                SpeedRatio = 0.7f, Scale = 1.2f,
                Cloth = new Color(0.36f, 0.12f, 0.06f), Skin = new Color(0.26f, 0.20f, 0.18f), Pants = new Color(0.16f, 0.12f, 0.10f), Eyes = new Color(1.0f, 0.55f, 0.1f), HideHorns = true,
                Gear = new[] { "iron_helmet", "iron_mace", "wooden_shield" },
                Skill = EnemySkill.Slam, SkillCooldown = 8f,
                DropChance = 0.55f, RareBonus = 0.1f
            },

            // Bosses: weight 0, so only their lairs place them.
            new EnemyKind
            {
                Name = "Gravelord Mortis", HealthMultiplier = 30f, DamageMultiplier = 3f, SpawnWeight = 0f, Tempo = 0.8f, BossEnrageDamage = 1.5f, BossEnrageDamageTaken = 0.8f,
                MaxHealth = 16.72131f, Armour = 255.102f, FireResistance = 20f, ColdResistance = 20f, LightningResistance = 20f, Experience = 150,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Physical,
                Damage = 9.333333f, AttackCooldown = 1.4f, AttackRange = 4.6f,
                SpeedRatio = 0.95f, Scale = 2.3f,
                // The yard's old priest, a gaunt gravedigger twice a man's height: coat, stole, grey skin, grave-light eyes.
                Body = CreatureBody.Gravedigger,
                Skin = new Color(0.20f, 0.24f, 0.21f), Cloth = new Color(0.12f, 0.12f, 0.13f), Pants = new Color(0.62f, 0.67f, 0.58f), Eyes = new Color(0.3f, 1.0f, 0.4f),
                DropChance = 1f, RareBonus = 0.6f, Drops = 3,
                IsBoss = true, Boss = BossStyle.Gravelord
            },
            new EnemyKind
            {
                Name = "Ashen Warlord", HealthMultiplier = 30f, DamageMultiplier = 3f, SpawnWeight = 0f, Tempo = 0.8f, BossEnrageDamage = 1.5625f, BossEnrageDamageTaken = 0.8f,
                MaxHealth = 22.92722f, Armour = 204.918f, FireResistance = 20f, ColdResistance = 20f, LightningResistance = 20f, Experience = 220,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Fire,
                Damage = 11.67816f, AttackCooldown = 1.5f, AttackRange = 4.8f,
                SpeedRatio = 0.9f, Scale = 2.25f,
                // Empty plate armour, embers in its joints and fire where its head should be.
                Body = CreatureBody.HollowArmor,
                Skin = new Color(0.20f, 0.18f, 0.18f), Cloth = new Color(0.32f, 0.07f, 0.04f), Pants = new Color(1.0f, 0.45f, 0.1f), Eyes = new Color(1.0f, 0.85f, 0.35f),
                DropChance = 1f, RareBonus = 0.8f, Drops = 4,
                IsBoss = true, Boss = BossStyle.Warlord
            },
            new EnemyKind
            {
                // The frozen heart of the Hollow: a heart of ice the size of a man, floating in a cage of shards.
                Name = "Rimeheart", HealthMultiplier = 30f, DamageMultiplier = 3f, SpawnWeight = 0f, Tempo = 0.8f, BossEnrageDamage = 1.5f, BossEnrageDamageTaken = 0.8f,
                MaxHealth = 26.07053f, Armour = 167.7852f, FireResistance = 20f, ColdResistance = 20f, LightningResistance = 20f, Experience = 300,
                Style = EnemyAttackStyle.Ranged, DamageType = DamageType.Cold,
                Damage = 10.60983f, AttackCooldown = 1.3f, AttackRange = 11f, ProjectileSpeed = 13f,
                SpeedRatio = 0.75f, Scale = 1.25f,
                Body = CreatureBody.FrostHeart, StaffOrb = new Color(0.6f, 0.9f, 1f),
                Skin = new Color(0.62f, 0.84f, 0.98f), Cloth = new Color(0.88f, 0.96f, 1.0f), Pants = new Color(0.22f, 0.48f, 0.92f), Eyes = new Color(0.55f, 1.0f, 1.0f),
                DropChance = 1f, RareBonus = 0.9f, Drops = 4,
                IsBoss = true, Boss = BossStyle.FrostQueen
            },

            // Later additions (weight 0: only the areas that list them spawn them).
            new EnemyKind
            {
                Name = "Forest Shaman", SpawnWeight = 0f,
                MaxHealth = 30f, Experience = 34,
                Style = EnemyAttackStyle.Ranged, DamageType = DamageType.Lightning,
                Damage = 18f, AttackCooldown = 2.2f, AttackRange = 9f, ProjectileSpeed = 10f,
                SpeedRatio = 0.62f, Scale = 0.95f,
                Cloth = new Color(0.30f, 0.45f, 0.18f), Skin = new Color(0.62f, 0.50f, 0.36f), Pants = new Color(0.36f, 0.26f, 0.14f), Eyes = new Color(0.6f, 1.0f, 0.4f),
                StaffOrb = new Color(0.55f, 0.75f, 1.0f),
                Skill = EnemySkill.WarCry, SkillCooldown = 10f,
                DropChance = 0.4f
            },
            new EnemyKind
            {
                Name = "Necromancer", SpawnWeight = 0f, Undead = true,
                MaxHealth = 26.88525f, Experience = 40,
                Style = EnemyAttackStyle.Ranged, DamageType = DamageType.Cold,
                Damage = 21.19149f, AttackCooldown = 2.2f, AttackRange = 10f, ProjectileSpeed = 10f,
                SpeedRatio = 0.6f, Scale = 1.0f,
                Cloth = new Color(0.18f, 0.12f, 0.22f), Skin = new Color(0.70f, 0.72f, 0.66f), Pants = new Color(0.10f, 0.08f, 0.12f), Eyes = new Color(0.55f, 1.0f, 0.5f), HideHorns = true,
                StaffOrb = new Color(0.55f, 1.0f, 0.55f),
                Skill = EnemySkill.Summon, SkillCooldown = 12f,
                DropChance = 0.45f, RareBonus = 0.05f
            },
            new EnemyKind
            {
                Name = "Skeleton Archer", SpawnWeight = 0f, Undead = true,
                MaxHealth = 18.48361f, Experience = 28,
                Style = EnemyAttackStyle.Ranged, DamageType = DamageType.Physical,
                Damage = 18.54255f, AttackCooldown = 1.46f, AttackRange = 12f, ProjectileSpeed = 20f,
                SpeedRatio = 0.95f, Scale = 0.95f,
                Cloth = new Color(0.82f, 0.80f, 0.72f), Skin = new Color(0.90f, 0.88f, 0.80f), Pants = new Color(0.32f, 0.30f, 0.27f), Eyes = new Color(1.0f, 0.25f, 0.1f), HideHorns = true,
                Gear = new[] { "short_bow" },
                Bow = true, RainOfArrows = true, Skeletal = true,
                Skill = EnemySkill.Volley, SkillCooldown = 9f,
                DropChance = 0.3f
            },
            new EnemyKind
            {
                Name = "Frost Giant", HealthMultiplier = 2.5f, SpawnWeight = 0f,
                MaxHealth = 50.17632f, Experience = 70,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Cold, BasicDamageType = DamageType.Physical,
                Damage = 50.8135f, AttackCooldown = 2.4f, AttackRange = 3.0f,
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
                Damage = 15f, AttackCooldown = 1.0f, AttackRange = 1.9f,
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
                Damage = 18f, AttackCooldown = 1.1f, AttackRange = 2.0f,
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
                Damage = 21f, AttackCooldown = 1.6f, AttackRange = 1.9f,
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
                Damage = 9f, AttackCooldown = 1.2f, AttackRange = 1.5f,
                SpeedRatio = 0.8f, Scale = 0.5f,
                Body = CreatureBody.Slime, Sounds = EnemySounds.Set.Slime,
                Skin = new Color(0.46f, 0.74f, 0.26f), Cloth = new Color(0.28f, 0.48f, 0.16f), Pants = new Color(0.60f, 0.45f, 0.25f), Eyes = new Color(0.08f, 0.10f, 0.05f),
                DropChance = 0.06f
            },
            new EnemyKind
            {
                Name = "Grave Bat", SpawnWeight = 0f,
                MaxHealth = 15.81395f, Experience = 16,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Lightning, BasicDamageType = DamageType.Physical,
                Damage = 12f, AttackCooldown = 0.9f, AttackRange = 1.9f,
                SpeedRatio = 1.1f, Scale = 0.8f,
                Body = CreatureBody.Bat, Sounds = EnemySounds.Set.Bat, PackSize = 2,
                Skin = new Color(0.22f, 0.18f, 0.20f), Cloth = new Color(0.36f, 0.22f, 0.27f), Pants = new Color(0.55f, 0.42f, 0.44f), Eyes = new Color(1.0f, 0.2f, 0.2f),
                Skill = EnemySkill.Shriek, SkillCooldown = 7f,
                Variant = CreatureVariant.Crypt,
                DropChance = 0.18f
            },
            new EnemyKind
            {
                Name = "Corpse Ooze", SpawnWeight = 0f,
                MaxHealth = 43.48837f, Experience = 32,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Cold, BasicDamageType = DamageType.Physical,
                Damage = 24f, AttackCooldown = 1.6f, AttackRange = 1.9f,
                SpeedRatio = 0.55f, Scale = 1.05f,
                Body = CreatureBody.Slime, Sounds = EnemySounds.Set.Slime, SplitInto = OozelingIndex,
                Skin = new Color(0.52f, 0.36f, 0.60f), Cloth = new Color(0.30f, 0.20f, 0.36f), Pants = new Color(0.90f, 0.88f, 0.80f), Eyes = new Color(0.5f, 1.0f, 0.6f),
                Variant = CreatureVariant.Crypt,
                Skill = EnemySkill.Leap, SkillCooldown = 10f,
                DropChance = 0.35f
            },
            new EnemyKind
            {
                Name = "Oozeling", SpawnWeight = 0f,
                MaxHealth = 12.84884f, Experience = 8,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Cold,
                Damage = 9f, AttackCooldown = 1.2f, AttackRange = 1.5f,
                SpeedRatio = 0.8f, Scale = 0.52f,
                Body = CreatureBody.Slime, Sounds = EnemySounds.Set.Slime,
                Skin = new Color(0.52f, 0.36f, 0.60f), Cloth = new Color(0.30f, 0.20f, 0.36f), Pants = new Color(0.90f, 0.88f, 0.80f), Eyes = new Color(0.5f, 1.0f, 0.6f),
                DropChance = 0.06f
            },
            new EnemyKind
            {
                Name = "Crypt Spider", SpawnWeight = 0f,
                MaxHealth = 31.62791f, Experience = 32,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Cold, BasicDamageType = DamageType.Physical,
                Damage = 21f, AttackCooldown = 1.1f, AttackRange = 2.0f,
                SpeedRatio = 0.95f, Scale = 1.0f,
                Body = CreatureBody.Spider, Sounds = EnemySounds.Set.Spider,
                Skin = new Color(0.76f, 0.73f, 0.66f), Cloth = new Color(0.30f, 0.27f, 0.36f), Pants = new Color(0.45f, 1.0f, 0.6f), Eyes = new Color(0.45f, 1.0f, 0.6f),
                Skill = EnemySkill.WebSpit, SkillCooldown = 8f,
                Variant = CreatureVariant.Crypt,
                DropChance = 0.35f
            },
            new EnemyKind
            {
                Name = "Magma Beetle", SpawnWeight = 0f,
                MaxHealth = 53.9557f, Experience = 45,
                Style = EnemyAttackStyle.Ranged, DamageType = DamageType.Fire,
                Damage = 27f, AttackCooldown = 2.4f, AttackRange = 9f, ProjectileSpeed = 9f,
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
                MaxHealth = 45.12658f, Experience = 40,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Fire, BasicDamageType = DamageType.Physical,
                Damage = 27f, AttackCooldown = 1.1f, AttackRange = 2.1f,
                SpeedRatio = 1.1f, Scale = 1.05f,
                Body = CreatureBody.Wolf, Sounds = EnemySounds.Set.Hound, PackSize = 2,
                Skin = new Color(0.17f, 0.13f, 0.12f), Cloth = new Color(0.38f, 0.12f, 0.06f), Pants = new Color(1.0f, 0.45f, 0.1f), Eyes = new Color(1.0f, 0.5f, 0.1f),
                Skill = EnemySkill.FireBreath, SkillCooldown = 7f,
                Variant = CreatureVariant.Infernal,
                DropChance = 0.4f
            },
            new EnemyKind
            {
                Name = "Frost Wolf", SpawnWeight = 0f,
                MaxHealth = 52.267f, Experience = 44,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Cold, BasicDamageType = DamageType.Physical,
                Damage = 28.58259f, AttackCooldown = 1.1f, AttackRange = 2.1f,
                SpeedRatio = 1.05f, Scale = 1.05f,
                Body = CreatureBody.Wolf, Sounds = EnemySounds.Set.Wolf, PackSize = 2,
                Skin = new Color(0.70f, 0.78f, 0.88f), Cloth = new Color(0.42f, 0.52f, 0.66f), Pants = new Color(0.95f, 0.97f, 1.0f), Eyes = new Color(0.4f, 0.9f, 1.0f),
                Skill = EnemySkill.Howl, SkillCooldown = 10f,
                Variant = CreatureVariant.Frost,
                DropChance = 0.4f
            },
            new EnemyKind
            {
                Name = "Ice Crawler", SpawnWeight = 0f,
                MaxHealth = 45.99496f, Experience = 40,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Cold, BasicDamageType = DamageType.Physical,
                Damage = 25.40675f, AttackCooldown = 1.0f, AttackRange = 2.0f,
                SpeedRatio = 0.95f, Scale = 1.05f,
                Body = CreatureBody.Spider, Sounds = EnemySounds.Set.Spider,
                Skin = new Color(0.58f, 0.74f, 0.90f), Cloth = new Color(0.82f, 0.92f, 1.0f), Pants = new Color(0.25f, 0.55f, 0.90f), Eyes = new Color(0.5f, 0.95f, 1.0f),
                Skill = EnemySkill.IceSpikes, SkillCooldown = 7f,
                Variant = CreatureVariant.Frost,
                DropChance = 0.4f
            },

            // The player's minions (see Skills.Minion): never spawned as enemies. Only their look
            // is used here (on the player's side and for spectators); their numbers are the minion's.
            new EnemyKind
            {
                Name = "Skeleton Warrior", SpawnWeight = 0f,
                MaxHealth = 24f, Style = EnemyAttackStyle.Melee, AttackRange = 2.1f, SpeedRatio = 0.9f, Scale = 0.95f,
                Cloth = new Color(0.62f, 0.78f, 0.62f), Skin = new Color(0.92f, 0.90f, 0.82f), Pants = new Color(0.22f, 0.30f, 0.24f), Eyes = new Color(0.45f, 1.0f, 0.5f), HideHorns = true,
                Gear = new[] { "rusty_sword", "wooden_shield" }, Skeletal = true
            },
            new EnemyKind
            {
                Name = "Skeleton Mage", SpawnWeight = 0f,
                MaxHealth = 14f, Style = EnemyAttackStyle.Ranged, AttackRange = 8.5f, ProjectileSpeed = 14f, SpeedRatio = 0.85f, Scale = 0.92f,
                Cloth = new Color(0.25f, 0.32f, 0.55f), Skin = new Color(0.92f, 0.90f, 0.82f), Pants = new Color(0.16f, 0.18f, 0.30f), Eyes = new Color(0.5f, 0.85f, 1.0f), HideHorns = true,
                StaffOrb = new Color(0.5f, 0.85f, 1.0f), Skeletal = true
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
                Name = "The Shepherd", HealthMultiplier = 20f, DamageMultiplier = 3f, SpawnWeight = 0f, Tempo = 1.6f, BossEnrageDamage = 44f / 29f, BossEnrageDamageTaken = 0.8f,
                MaxHealth = 91.50943f, Armour = 189.8734f,
                FireResistance = 30f, ColdResistance = 30f, LightningResistance = 30f, Experience = 400,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Physical,
                Damage = 5.738255f, AttackCooldown = 1.4f, AttackRange = 3.6f,
                SpeedRatio = 1.05f, Scale = 1.5f,
                Cloth = new Color(0.36f, 0.35f, 0.33f), Skin = new Color(0.55f, 0.58f, 0.50f), Pants = new Color(0.30f, 0.29f, 0.27f), Eyes = new Color(0.9f, 0.8f, 0.4f),
                HideHorns = true,
                DropChance = 1f, RareBonus = 1f, Drops = 5,
                IsBoss = true, Boss = BossStyle.Shepherd
            },
            // Append only: kind indices are also used by spectator snapshots.
            new EnemyKind
            {
                Name = "Briarbound", HealthMultiplier = 3f, DamageMultiplier = 2f, SpawnWeight = 0f,
                MaxHealth = 34.66667f, Armour = 80f, PoisonResistance = 35f, Experience = 42,
                Style = EnemyAttackStyle.Ranged, DamageType = DamageType.Physical,
                Damage = 18f, AttackCooldown = 2.4f, AttackRange = 10f, ProjectileSpeed = 12f,
                SpeedRatio = 0.48f, Body = CreatureBody.Briarbound,
                StaffOrb = new Color(0.42f, 0.56f, 0.22f),
                Skin = new Color(0.20f, 0.12f, 0.10f), Cloth = new Color(0.40f, 0.30f, 0.18f),
                Pants = new Color(0.42f, 0.65f, 0.23f), Eyes = new Color(0.85f, 1f, 0.32f),
                Skill = EnemySkill.ThornGarden, SkillCooldown = 5f, DropChance = 0.45f
            },
            new EnemyKind
            {
                Name = "Grave Siren", HealthMultiplier = 3f, DamageMultiplier = 2f, SpawnWeight = 0f, Undead = true,
                MaxHealth = 36.4071f, Armour = 61.22449f, ColdResistance = 25f, Experience = 48,
                Style = EnemyAttackStyle.Ranged, DamageType = DamageType.Cold,
                Damage = 18.54255f, AttackCooldown = 1.6f, AttackRange = 9f, ProjectileSpeed = 10f,
                SpeedRatio = 0.45f, Body = CreatureBody.GraveSiren,
                StaffOrb = new Color(0.45f, 0.75f, 0.68f),
                Skin = new Color(0.26f, 0.30f, 0.30f), Cloth = new Color(0.38f, 0.40f, 0.38f),
                Pants = new Color(0.12f, 0.16f, 0.18f), Eyes = new Color(0.45f, 1f, 0.85f),
                Skill = EnemySkill.Wail, SkillCooldown = 4.5f, DropChance = 0.5f
            },
            new EnemyKind
            {
                Name = "Rime Stalker", HealthMultiplier = 3f, DamageMultiplier = 2f, SpawnWeight = 0f,
                MaxHealth = 33.45088f, ColdResistance = 55f, Experience = 48,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Cold, BasicDamageType = DamageType.Physical,
                Damage = 22.23091f, AttackCooldown = 1.2f, AttackRange = 2.3f,
                SpeedRatio = 0.95f, Body = CreatureBody.RimeStalker,
                Skin = new Color(0.18f, 0.34f, 0.52f), Cloth = new Color(0.48f, 0.78f, 0.94f),
                Pants = new Color(0.36f, 0.52f, 0.62f), Eyes = new Color(0.85f, 1f, 1f),
                Skill = EnemySkill.FrostFissures, SkillCooldown = 4f, DropChance = 0.45f
            },
            new EnemyKind
            {
                Name = "Cinder Penitent", HealthMultiplier = 3f, DamageMultiplier = 2f, SpawnWeight = 0f,
                MaxHealth = 40.54852f, FireResistance = 50f, Experience = 48,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Fire, BasicDamageType = DamageType.Physical,
                Damage = 18f, AttackCooldown = 1.8f, AttackRange = 2.2f,
                SpeedRatio = 0.48f, Body = CreatureBody.CinderPenitent,
                Skin = new Color(0.17f, 0.13f, 0.12f), Cloth = new Color(0.32f, 0.23f, 0.18f),
                Pants = new Color(0.48f, 0.17f, 0.08f), Eyes = new Color(1f, 0.47f, 0.12f),
                Skill = EnemySkill.LastOffering, SkillCooldown = 4f, DropChance = 0.45f
            },
            new EnemyKind
            {
                Name = "Hollowmaw", HealthMultiplier = 3f, DamageMultiplier = 2f, SpawnWeight = 0f,
                MaxHealth = 49.4186f, Armour = 40.54054f, Experience = 48,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Physical,
                Damage = 24f, AttackCooldown = 1.7f, AttackRange = 2.4f,
                SpeedRatio = 0.67f, Body = CreatureBody.Hollowmaw,
                Skin = new Color(0.63f, 0.60f, 0.51f), Cloth = new Color(0.38f, 0.35f, 0.29f),
                Pants = new Color(0.22f, 0.12f, 0.12f), Eyes = new Color(0.12f, 0.07f, 0.06f),
                Skill = EnemySkill.DraggingBreath, SkillCooldown = 4.5f, DropChance = 0.45f
            },
            new EnemyKind
            {
                Name = "Barrow Castellan", HealthMultiplier = 3f, DamageMultiplier = 2f, SpawnWeight = 0f, Undead = true,
                MaxHealth = 50.40984f, Armour = 112.2449f, ColdResistance = 20f, Experience = 58,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Physical,
                Damage = 23.84043f, AttackCooldown = 1.8f, AttackRange = 3f,
                SpeedRatio = 0.60f, Body = CreatureBody.BarrowCastellan,
                Skin = new Color(0.31f, 0.33f, 0.29f), Cloth = new Color(0.24f, 0.29f, 0.28f),
                Pants = new Color(0.25f, 0.20f, 0.16f), Eyes = new Color(0.56f, 0.77f, 0.66f),
                Skill = EnemySkill.Graveward, SkillCooldown = 6f, DropChance = 0.55f, RareBonus = 0.1f
            },

            // Field bosses added later (weight 0: their lairs place them). See BossAbilities.
            new EnemyKind
            {
                // Greenwood's first boss: a boar the size of a cart, a thicket growing on its back.
                Name = "Bramblesow", HealthMultiplier = 27.3f, DamageMultiplier = 2f, SpawnWeight = 0f, Tempo = 0.8f, BossEnrageDamage = 1.5f, BossEnrageDamageTaken = 0.8f,
                MaxHealth = 20f, Armour = 30f, PoisonResistance = 20f, Experience = 90,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Physical,
                Damage = 7f, AttackCooldown = 1.6f, AttackRange = 5.1f,
                SpeedRatio = 0.75f, Scale = 3.0f,
                Body = CreatureBody.Boar, Sounds = EnemySounds.Set.Hound,
                Skin = new Color(0.38f, 0.26f, 0.17f), Cloth = new Color(0.17f, 0.12f, 0.09f), Pants = new Color(0.30f, 0.50f, 0.20f), Eyes = new Color(1.0f, 0.25f, 0.12f),
                DropChance = 1f, RareBonus = 0.4f, Drops = 2,
                IsBoss = true, Boss = BossStyle.Bramblesow
            },
            new EnemyKind
            {
                // King of the tunnels under the Lost Hollows: small, quick and full of tricks.
                Name = "Vex, the Tunnel King", HealthMultiplier = 24f, DamageMultiplier = 2.5f, SpawnWeight = 0f, Tempo = 0.85f, BossEnrageDamage = 1.5f, BossEnrageDamageTaken = 0.8f,
                MaxHealth = 15f, Armour = 60f, PoisonResistance = 40f, Experience = 130,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Physical,
                Damage = 8f, AttackCooldown = 0.8f, AttackRange = 1.9f,
                SpeedRatio = 1.05f, Scale = 0.95f,
                Body = CreatureBody.TunnelKing,
                Skin = new Color(0.44f, 0.50f, 0.30f), Cloth = new Color(0.30f, 0.22f, 0.16f), Pants = new Color(0.82f, 0.55f, 0.24f), Eyes = new Color(1.0f, 0.9f, 0.2f),
                DropChance = 1f, RareBonus = 0.5f, Drops = 3,
                IsBoss = true, Boss = BossStyle.TunnelKing
            },
            new EnemyKind
            {
                // Rings the graveyard's sunken bell for the dead; it carries it on its back.
                Name = "The Bell-Ringer", HealthMultiplier = 30f, DamageMultiplier = 3f, SpawnWeight = 0f, Tempo = 0.8f, BossEnrageDamage = 1.5f, BossEnrageDamageTaken = 0.8f,
                MaxHealth = 16f, Armour = 180f, FireResistance = 20f, ColdResistance = 20f, LightningResistance = 40f, Experience = 160,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Lightning,
                Damage = 9f, AttackCooldown = 1.5f, AttackRange = 2.9f,
                SpeedRatio = 0.55f, Scale = 1.2f, Undead = true,
                Body = CreatureBody.BellRinger,
                Skin = new Color(0.16f, 0.13f, 0.18f), Cloth = new Color(0.62f, 0.44f, 0.20f), Pants = new Color(0.30f, 0.58f, 0.48f), Eyes = new Color(1.0f, 0.92f, 0.5f),
                DropChance = 1f, RareBonus = 0.6f, Drops = 3,
                IsBoss = true, Boss = BossStyle.BellRinger
            },
            new EnemyKind
            {
                // The ruins' old god: a four-armed statue holding up a sun. It hardly moves.
                Name = "The Sunforged Idol", HealthMultiplier = 30f, DamageMultiplier = 3f, SpawnWeight = 0f, Tempo = 0.8f, BossEnrageDamage = 1.5f, BossEnrageDamageTaken = 0.8f,
                MaxHealth = 24f, Armour = 320f, FireResistance = 50f, ColdResistance = 20f, LightningResistance = 20f, Experience = 240,
                Style = EnemyAttackStyle.Ranged, DamageType = DamageType.Fire,
                Damage = 11f, AttackCooldown = 1.8f, AttackRange = 15f, ProjectileSpeed = 12f,
                SpeedRatio = 0.001f, Scale = 2.6f,
                Body = CreatureBody.SunIdol, StaffOrb = new Color(1f, 0.6f, 0.15f),
                Skin = new Color(0.80f, 0.68f, 0.50f), Cloth = new Color(0.42f, 0.36f, 0.30f), Pants = new Color(0.95f, 0.75f, 0.25f), Eyes = new Color(1.0f, 0.62f, 0.15f),
                DropChance = 1f, RareBonus = 0.8f, Drops = 4,
                IsBoss = true, Boss = BossStyle.SunIdol
            },
            new EnemyKind
            {
                // A huntress of the Hollow's ice, with her frost wolves.
                Name = "Hrimgar the Huntress", HealthMultiplier = 30f, DamageMultiplier = 3f, SpawnWeight = 0f, Tempo = 0.85f, BossEnrageDamage = 1.5f, BossEnrageDamageTaken = 0.8f,
                MaxHealth = 24f, Armour = 160f, FireResistance = 10f, ColdResistance = 50f, LightningResistance = 20f, Experience = 300,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Cold,
                Damage = 10f, AttackCooldown = 1.1f, AttackRange = 2.7f,
                SpeedRatio = 0.95f, Scale = 1.0f,
                Body = CreatureBody.Huntress,
                Skin = new Color(0.34f, 0.40f, 0.48f), Cloth = new Color(0.90f, 0.92f, 0.94f), Pants = new Color(0.66f, 0.78f, 0.86f), Eyes = new Color(0.5f, 1.0f, 1.0f),
                DropChance = 1f, RareBonus = 0.9f, Drops = 4,
                IsBoss = true, Boss = BossStyle.Huntress
            },
            // The Haunted Graveyard's heavy dead, built on the skeleton look.
            new EnemyKind
            {
                // A big skeleton with a greataxe: wades in spinning it, and pulls itself back
                // together once unless its bone pile is smashed.
                Name = "Bone Reaver", HealthMultiplier = 2.5f, DamageMultiplier = 1f, SpawnWeight = 0f, Undead = true,
                MaxHealth = 44f, Armour = 60f, Experience = 62,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Physical,
                Damage = 36f, AttackCooldown = 1.9f, AttackRange = 2.8f,
                SpeedRatio = 1.0f, Scale = 1.35f,
                Cloth = new Color(0.80f, 0.77f, 0.68f), Skin = new Color(0.86f, 0.83f, 0.74f), Pants = new Color(0.30f, 0.12f, 0.10f), Eyes = new Color(1.0f, 0.3f, 0.12f), HideHorns = true,
                Gear = new[] { "woodsplitter" }, Weapon = WeaponType.Greataxe, Skeletal = true, Reassembles = true,
                Skill = EnemySkill.BoneWhirl, SkillCooldown = 7f,
                DropChance = 0.7f, RareBonus = 0.15f
            },
            new EnemyKind
            {
                // A shielded skeleton that cages the player in bone for the rest to catch up.
                Name = "Bone Warden", HealthMultiplier = 2.5f, DamageMultiplier = 1f, SpawnWeight = 0f, Undead = true,
                MaxHealth = 40f, Armour = 150f, Experience = 58,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Lightning, BasicDamageType = DamageType.Physical,
                Damage = 27f, AttackCooldown = 1.4f, AttackRange = 2.3f,
                SpeedRatio = 1.05f, Scale = 1.15f,
                Cloth = new Color(0.80f, 0.77f, 0.68f), Skin = new Color(0.84f, 0.84f, 0.78f), Pants = new Color(0.18f, 0.24f, 0.26f), Eyes = new Color(0.45f, 0.9f, 1.0f), HideHorns = true,
                Gear = new[] { "iron_mace", "wooden_shield" }, Weapon = WeaponType.Mace, Skeletal = true,
                Skill = EnemySkill.BonePrison, SkillCooldown = 9f,
                DropChance = 0.65f, RareBonus = 0.1f
            },
            new EnemyKind
            {
                Name = "Dune Skitter", SpawnWeight = 0, MaxHealth = 29, Experience = 30,
                Damage = 15, DamageType = DamageType.Physical, AttackRange = 1.8f, AttackCooldown = 1.4f,
                SpeedRatio = 0.92f, Scale = 0.95f, Body = CreatureBody.Beetle, PackSize = 2,
                Skin = new Color(0.24f, 0.18f, 0.12f), Cloth = new Color(0.33f, 0.25f, 0.16f),
                Pants = new Color(0.78f, 0.52f, 0.22f), Eyes = new Color(1, 0.65f, 0.2f),
                Skill = EnemySkill.DesertCharge, SkillCooldown = 7, DropChance = 0.3f
            },
            new EnemyKind
            {
                Name = "Glassback Scorpion", SpawnWeight = 0, MaxHealth = 48, Armour = 80, Experience = 48,
                Damage = 22, DamageType = DamageType.Physical, PoisonResistance = 35,
                AttackRange = 2.2f, AttackCooldown = 1.8f, SpeedRatio = 0.65f, Scale = 1.3f,
                Body = CreatureBody.Scorpion, Skin = new Color(0.62f, 0.34f, 0.10f), Cloth = new Color(0.88f, 0.61f, 0.24f),
                Pants = new Color(0.32f, 0.18f, 0.08f), Eyes = new Color(0.15f, 0.10f, 0.03f),
                Skill = EnemySkill.ScorpionSting, SkillCooldown = 6, DropChance = 0.5f
            },
            new EnemyKind
            {
                Name = "Sandlurker", SpawnWeight = 0, MaxHealth = 51, Experience = 52,
                Damage = 24, DamageType = DamageType.Physical, AttackRange = 2.3f, AttackCooldown = 1.6f,
                SpeedRatio = 0.74f, Scale = 1.25f, Body = CreatureBody.Sandlurker,
                Skin = new Color(0.72f, 0.60f, 0.40f), Cloth = new Color(0.45f, 0.32f, 0.18f),
                Pants = new Color(0.86f, 0.72f, 0.47f), Eyes = new Color(0.12f, 0.08f, 0.04f),
                Skill = EnemySkill.SandBurrow, SkillCooldown = 8, DropChance = 0.5f
            },
            new EnemyKind
            {
                Name = "Dustroad Reaver", SpawnWeight = 0, MaxHealth = 52, Armour = 60, Experience = 48,
                Damage = 23, DamageType = DamageType.Physical, AttackRange = 2.2f, AttackCooldown = 1.2f,
                SpeedRatio = 0.82f, HideHorns = true, Weapon = WeaponType.Sword,
                Skin = new Color(0.59f, 0.40f, 0.27f), Cloth = new Color(0.56f, 0.14f, 0.10f), Pants = new Color(0.25f, 0.20f, 0.15f),
                Eyes = new Color(0.1f, 0.08f, 0.05f), Gear = new[] { "hunter_hood", "buckler" },
                Skill = EnemySkill.DesertCharge, SkillCooldown = 7, DropChance = 0.5f
            },
            new EnemyKind
            {
                Name = "Dustroad Javelineer", SpawnWeight = 0, MaxHealth = 37, Experience = 44,
                Style = EnemyAttackStyle.Ranged, Damage = 22, DamageType = DamageType.Physical,
                AttackRange = 13, ProjectileSpeed = 19, AttackCooldown = 2.2f, SpeedRatio = 0.73f,
                HideHorns = true, Javelin = true, Skin = new Color(0.62f, 0.43f, 0.29f),
                Cloth = new Color(0.63f, 0.20f, 0.13f), Pants = new Color(0.28f, 0.24f, 0.17f), Eyes = new Color(0.1f, 0.08f, 0.05f),
                Skill = EnemySkill.JavelinFan, SkillCooldown = 8, DropChance = 0.4f
            },
            new EnemyKind
            {
                Name = "Ashwind Caller", SpawnWeight = 0, MaxHealth = 39, Experience = 57,
                Style = EnemyAttackStyle.Ranged, Damage = 21, DamageType = DamageType.Physical,
                AttackRange = 12, ProjectileSpeed = 9, AttackCooldown = 2.6f, SpeedRatio = 0.6f,
                HideHorns = true, Skin = new Color(0.55f, 0.36f, 0.24f), Cloth = new Color(0.39f, 0.10f, 0.08f),
                Pants = new Color(0.24f, 0.18f, 0.12f), Eyes = new Color(0.95f, 0.72f, 0.32f), StaffOrb = new Color(0.77f, 0.52f, 0.23f),
                Skill = EnemySkill.RollingSandstorm, SkillCooldown = 10, DropChance = 0.5f
            },
            new EnemyKind
            {
                Name = "Kharad, the Tollkeeper", SpawnWeight = 0, HealthMultiplier = 7, MaxHealth = 52,
                Armour = 110, Experience = 180, Damage = 29, DamageType = DamageType.Physical,
                AttackRange = 3.1f, AttackCooldown = 1.9f, SpeedRatio = 0.67f, Scale = 1.25f,
                HideHorns = true, Weapon = WeaponType.Greataxe, Gear = new[] { "bronze_helmet" },
                Skin = new Color(0.58f, 0.37f, 0.24f), Cloth = new Color(0.43f, 0.09f, 0.07f),
                Pants = new Color(0.23f, 0.18f, 0.13f), Eyes = new Color(0.12f, 0.08f, 0.04f),
                Skill = EnemySkill.Tollkeeper, SkillCooldown = 5, DropChance = 1, RareBonus = 0.25f, Drops = 2
            },
            new EnemyKind
            {
                Name = "Saltback Crab", SpawnWeight = 0, MaxHealth = 57, Armour = 110, Experience = 58,
                Damage = 23, DamageType = DamageType.Physical, AttackRange = 2.1f, AttackCooldown = 1.9f,
                SpeedRatio = 0.58f, Scale = 1.2f, Body = CreatureBody.SaltCrab, Sounds = EnemySounds.Set.Beetle,
                Skin = new Color(0.42f, 0.26f, 0.20f), Cloth = new Color(0.90f, 0.87f, 0.73f),
                Pants = new Color(0.65f, 0.46f, 0.32f), Eyes = new Color(0.15f, 0.45f, 0.45f),
                Skill = EnemySkill.SaltBurst, SkillCooldown = 7, DropChance = 0.5f
            },
            new EnemyKind
            {
                Name = "Brine Spitter", SpawnWeight = 0, MaxHealth = 40, Experience = 56, PoisonResistance = 40,
                Damage = 19, DamageType = DamageType.Physical, Style = EnemyAttackStyle.Ranged,
                AttackRange = 11, ProjectileSpeed = 10, AttackCooldown = 2.6f, SpeedRatio = 0.56f,
                Scale = 1.1f, Body = CreatureBody.BrineSpitter, Sounds = EnemySounds.Set.Beetle,
                Skin = new Color(0.29f, 0.39f, 0.33f), Cloth = new Color(0.68f, 0.81f, 0.64f),
                Pants = new Color(0.87f, 0.85f, 0.70f), Eyes = new Color(0.80f, 0.98f, 0.45f),
                Skill = EnemySkill.BrineSpit, SkillCooldown = 8, DropChance = 0.45f
            },
            new EnemyKind
            {
                Name = "Saltbound Dredger", SpawnWeight = 0, MaxHealth = 53, Armour = 50, Experience = 61,
                Damage = 24, DamageType = DamageType.Physical, AttackRange = 2.5f, AttackCooldown = 1.9f,
                SpeedRatio = 0.65f, Scale = 1.12f, HideHorns = true, Skeletal = true, Undead = true,
                Skin = new Color(0.81f, 0.77f, 0.63f), Cloth = new Color(0.26f, 0.42f, 0.43f),
                Pants = new Color(0.34f, 0.28f, 0.21f), Eyes = new Color(0.54f, 0.90f, 0.85f),
                Gear = new[] { "iron_mace" }, Weapon = WeaponType.Mace,
                Skill = EnemySkill.DredgeFault, SkillCooldown = 7.5f, DropChance = 0.55f
            },
            new EnemyKind
            {
                Name = "Uruun, the Salt Matriarch", SpawnWeight = 0, IsBoss = true, Boss = BossStyle.SaltMatriarch,
                MaxHealth = 65, HealthMultiplier = 18, Armour = 130, Experience = 420,
                Damage = 27, DamageType = DamageType.Physical, AttackRange = 3.8f, AttackCooldown = 2.1f,
                SpeedRatio = 0.48f, Scale = 2.2f, Body = CreatureBody.SaltCrab, Sounds = EnemySounds.Set.Beetle,
                Skin = new Color(0.45f, 0.21f, 0.16f), Cloth = new Color(0.95f, 0.91f, 0.78f),
                Pants = new Color(0.72f, 0.49f, 0.28f), Eyes = new Color(0.18f, 0.80f, 0.75f),
                DropChance = 1, RareBonus = 0.4f, Drops = 4
            },
            new EnemyKind
            {
                Name = "Namar, Keeper of the Dry Well", SpawnWeight = 0, IsBoss = true, Boss = BossStyle.DryWellKeeper,
                MaxHealth = 58, HealthMultiplier = 20, Armour = 75, Experience = 480,
                Damage = 25, DamageType = DamageType.Physical, Style = EnemyAttackStyle.Ranged,
                AttackRange = 13, ProjectileSpeed = 11, AttackCooldown = 2.5f, SpeedRatio = 0.53f, Scale = 1.45f,
                HideHorns = true, Skeletal = true, Undead = true, Gear = new[] { "hunter_hood" },
                Cloth = new Color(0.14f, 0.35f, 0.38f), Pants = new Color(0.34f, 0.27f, 0.19f),
                Skin = new Color(0.88f, 0.84f, 0.70f), Eyes = new Color(0.55f, 0.98f, 0.90f),
                StaffOrb = new Color(0.63f, 0.88f, 0.80f), DropChance = 1, RareBonus = 0.45f, Drops = 4
            },
            new EnemyKind
            {
                Name = "Channel Reaver", SpawnWeight = 0, MaxHealth = 55, Armour = 65, Experience = 65,
                Damage = 25, DamageType = DamageType.Physical, AttackRange = 2.4f, AttackCooldown = 1.8f,
                SpeedRatio = 0.86f, HideHorns = true, Weapon = WeaponType.Sword, Gear = new[] { "rusty_sword" },
                Skin = new Color(0.57f, 0.36f, 0.23f), Cloth = new Color(0.42f, 0.14f, 0.09f),
                Pants = new Color(0.25f, 0.20f, 0.14f), Skill = EnemySkill.Charge, SkillCooldown = 8, DropChance = 0.55f
            },
            new EnemyKind
            {
                Name = "Clay Bearer", SpawnWeight = 0, MaxHealth = 65, Armour = 100, Experience = 68,
                Damage = 26, DamageType = DamageType.Physical, AttackRange = 2.6f, AttackCooldown = 2.3f,
                SpeedRatio = 0.52f, Scale = 1.25f, HideHorns = true,
                Skin = new Color(0.70f, 0.43f, 0.27f), Cloth = new Color(0.58f, 0.34f, 0.20f),
                Pants = new Color(0.39f, 0.25f, 0.17f), Eyes = new Color(0.45f, 0.85f, 0.82f),
                Skill = EnemySkill.Slam, SkillCooldown = 8, DropChance = 0.55f
            },
            new EnemyKind
            {
                Name = "Drowned Mechanic", SpawnWeight = 0, MaxHealth = 54, Armour = 70, Experience = 64,
                Damage = 24, DamageType = DamageType.Physical, AttackRange = 2.5f, AttackCooldown = 2,
                SpeedRatio = 0.63f, HideHorns = true, Skeletal = true, Undead = true,
                Skin = new Color(0.67f, 0.76f, 0.65f), Cloth = new Color(0.16f, 0.33f, 0.31f),
                Pants = new Color(0.27f, 0.22f, 0.17f), Eyes = new Color(0.5f, 0.95f, 0.85f),
                Gear = new[] { "iron_mace" }, Weapon = WeaponType.Mace,
                Skill = EnemySkill.DredgeFault, SkillCooldown = 8, DropChance = 0.55f
            },
            new EnemyKind
            {
                Name = "The Sluice Warden", SpawnWeight = 0, IsBoss = true, Boss = BossStyle.SluiceWarden,
                MaxHealth = 65, HealthMultiplier = 20, Armour = 125, Experience = 520,
                Damage = 27, DamageType = DamageType.Physical, AttackRange = 3.5f, AttackCooldown = 2.2f,
                SpeedRatio = 0.48f, Scale = 1.6f, Body = CreatureBody.SluiceWarden,
                Skin = new Color(0.42f, 0.23f, 0.16f), Cloth = new Color(0.19f, 0.39f, 0.37f),
                Pants = new Color(0.68f, 0.47f, 0.28f), Eyes = new Color(0.4f, 0.95f, 0.88f),
                DropChance = 1, RareBonus = 0.45f, Drops = 4
            },
            new EnemyKind
            {
                Name = "The Last Engineer", SpawnWeight = 0, IsBoss = true, Boss = BossStyle.LastEngineer,
                MaxHealth = 60, HealthMultiplier = 21, Armour = 100, Experience = 540,
                Damage = 26, DamageType = DamageType.Physical, AttackRange = 3.5f, AttackCooldown = 2.4f,
                SpeedRatio = 0.38f, Scale = 1.6f, Body = CreatureBody.LastEngineer, Undead = true,
                Skin = new Color(0.28f, 0.43f, 0.37f), Cloth = new Color(0.64f, 0.48f, 0.25f),
                Pants = new Color(0.34f, 0.29f, 0.23f), Eyes = new Color(0.63f, 0.98f, 0.80f),
                DropChance = 1, RareBonus = 0.45f, Drops = 4
            },
            new EnemyKind
            {
                Name = "Rootbound Harvester", SpawnWeight = 0, MaxHealth = 60, Armour = 65, Experience = 72,
                Damage = 25, DamageType = DamageType.Physical, AttackRange = 2.8f, AttackCooldown = 2.1f,
                SpeedRatio = 0.58f, Scale = 1.1f, Body = CreatureBody.Briarbound, Undead = true,
                Skin = new Color(0.36f, 0.28f, 0.14f), Cloth = new Color(0.52f, 0.48f, 0.25f),
                Pants = new Color(0.23f, 0.20f, 0.12f), Eyes = new Color(0.72f, 0.85f, 0.28f),
                Skill = EnemySkill.ThornGarden, SkillCooldown = 8, DropChance = 0.55f
            },
            new EnemyKind
            {
                Name = "Jar Swarm", SpawnWeight = 0, MaxHealth = 29, Experience = 42,
                Damage = 16, DamageType = DamageType.Poison, AttackRange = 1.7f, AttackCooldown = 1.6f,
                SpeedRatio = 1.12f, Scale = 0.65f, PackSize = 2, Body = CreatureBody.Beetle, Sounds = EnemySounds.Set.Beetle,
                Skin = new Color(0.30f, 0.25f, 0.11f), Cloth = new Color(0.69f, 0.57f, 0.25f),
                Pants = new Color(0.16f, 0.20f, 0.10f), Eyes = new Color(0.75f, 0.9f, 0.3f),
                DropChance = 0.3f
            },
            new EnemyKind
            {
                Name = "Orchard Hound", SpawnWeight = 0, MaxHealth = 43, Experience = 65,
                Damage = 22, DamageType = DamageType.Physical, AttackRange = 2, AttackCooldown = 1.9f,
                SpeedRatio = 1.06f, Scale = 1.05f, Body = CreatureBody.Wolf, Variant = CreatureVariant.Crypt,
                Sounds = EnemySounds.Set.Wolf, Skin = new Color(0.40f, 0.33f, 0.20f),
                Cloth = new Color(0.58f, 0.56f, 0.35f), Pants = new Color(0.26f, 0.23f, 0.15f),
                Eyes = new Color(0.77f, 0.86f, 0.30f), Skill = EnemySkill.Leap, SkillCooldown = 9, DropChance = 0.45f
            },
            new EnemyKind
            {
                Name = "Jar Keeper", SpawnWeight = 0, MaxHealth = 46, Experience = 75,
                Damage = 22, DamageType = DamageType.Poison, Style = EnemyAttackStyle.Ranged,
                AttackRange = 11, ProjectileSpeed = 10, AttackCooldown = 2.8f, SpeedRatio = 0.56f,
                HideHorns = true, Skeletal = true, Undead = true, Gear = new[] { "hunter_hood" },
                Skin = new Color(0.70f, 0.66f, 0.46f), Cloth = new Color(0.41f, 0.35f, 0.16f),
                Pants = new Color(0.25f, 0.22f, 0.14f), Eyes = new Color(0.8f, 0.9f, 0.35f),
                StaffOrb = new Color(0.72f, 0.82f, 0.28f), Skill = EnemySkill.JarShatter, SkillCooldown = 8, DropChance = 0.55f
            },
            new EnemyKind
            {
                Name = "Bronze Sluiceguard", SpawnWeight = 0, MaxHealth = 66, Armour = 135, Experience = 82,
                Damage = 25, DamageType = DamageType.Physical, AttackRange = 2.6f, AttackCooldown = 2.3f,
                SpeedRatio = 0.55f, Scale = 1.2f, Body = CreatureBody.BarrowCastellan, Undead = true,
                Skin = new Color(0.32f, 0.53f, 0.43f), Cloth = new Color(0.68f, 0.51f, 0.25f),
                Pants = new Color(0.25f, 0.34f, 0.29f), Eyes = new Color(0.46f, 0.95f, 0.82f),
                Skill = EnemySkill.Graveward, SkillCooldown = 9, DropChance = 0.6f
            },
            new EnemyKind
            {
                Name = "Drowned Courtier", SpawnWeight = 0, MaxHealth = 46, ColdResistance = 30, Experience = 79,
                Damage = 23, DamageType = DamageType.Cold, AttackRange = 12, Style = EnemyAttackStyle.Ranged,
                ProjectileSpeed = 11, AttackCooldown = 2.7f, SpeedRatio = 0.64f, Body = CreatureBody.GraveSiren,
                Undead = true, Skin = new Color(0.42f, 0.61f, 0.53f), Cloth = new Color(0.76f, 0.77f, 0.60f),
                Pants = new Color(0.24f, 0.40f, 0.35f), Eyes = new Color(0.52f, 0.98f, 0.9f),
                Skill = EnemySkill.FrostFissures, SkillCooldown = 9, DropChance = 0.55f
            },
            new EnemyKind
            {
                Name = "Tithe Priest", SpawnWeight = 0, MaxHealth = 48, Experience = 85,
                Damage = 20, DamageType = DamageType.Cold, AttackRange = 12, Style = EnemyAttackStyle.Ranged,
                ProjectileSpeed = 10, AttackCooldown = 2.8f, SpeedRatio = 0.62f, HideHorns = true,
                Skin = new Color(0.52f, 0.49f, 0.34f), Cloth = new Color(0.16f, 0.43f, 0.38f),
                Pants = new Color(0.45f, 0.38f, 0.20f), Eyes = new Color(0.62f, 0.95f, 0.85f),
                StaffOrb = new Color(0.37f, 0.83f, 0.72f), Gear = new[] { "hunter_hood" },
                Skill = EnemySkill.WarCry, SkillCooldown = 11, DropChance = 0.6f
            },
            new EnemyKind
            {
                Name = "Reservoir Skimmer", SpawnWeight = 0, MaxHealth = 41, Experience = 71,
                Damage = 21, DamageType = DamageType.Cold, AttackRange = 10, Style = EnemyAttackStyle.Ranged,
                ProjectileSpeed = 13, AttackCooldown = 2.5f, SpeedRatio = 0.86f, Body = CreatureBody.BrineSpitter,
                Sounds = EnemySounds.Set.Beetle, Skin = new Color(0.21f, 0.45f, 0.39f),
                Cloth = new Color(0.62f, 0.79f, 0.63f), Pants = new Color(0.35f, 0.58f, 0.50f),
                Eyes = new Color(0.6f, 0.98f, 0.9f), Skill = EnemySkill.SluiceJet, SkillCooldown = 8, DropChance = 0.5f
            },
            new EnemyKind
            {
                Name = "Mother of the Last Harvest", SpawnWeight = 0, IsBoss = true, Boss = BossStyle.HarvestMother,
                MaxHealth = 65, HealthMultiplier = 22, Armour = 100, PoisonResistance = 40, Experience = 580,
                Damage = 26, DamageType = DamageType.Poison, BasicDamageType = DamageType.Physical,
                AttackRange = 3.8f, AttackCooldown = 2.4f, SpeedRatio = 0.35f, Scale = 1.8f, Body = CreatureBody.HarvestMother,
                Skin = new Color(0.34f, 0.28f, 0.14f), Cloth = new Color(0.55f, 0.44f, 0.22f),
                Pants = new Color(0.29f, 0.38f, 0.17f), Eyes = new Color(0.75f, 0.94f, 0.30f),
                DropChance = 1, RareBonus = 0.45f, Drops = 4
            },
            new EnemyKind
            {
                Name = "Ishara, Keeper of the Borrowed River", SpawnWeight = 0, IsBoss = true, Boss = BossStyle.Ishara,
                MaxHealth = 62, HealthMultiplier = 24, Armour = 85, ColdResistance = 35, Experience = 650,
                Damage = 26, DamageType = DamageType.Cold, AttackRange = 13, Style = EnemyAttackStyle.Ranged,
                ProjectileSpeed = 12, AttackCooldown = 2.6f, SpeedRatio = 0.53f, Scale = 1.7f, Body = CreatureBody.Ishara,
                Skin = new Color(0.62f, 0.74f, 0.63f), Cloth = new Color(0.20f, 0.49f, 0.43f),
                Pants = new Color(0.68f, 0.54f, 0.29f), Eyes = new Color(0.5f, 1, 0.9f),
                DropChance = 1, RareBonus = 0.5f, Drops = 5
            },
            new EnemyKind
            {
                Name = "Ward Gaoler", SpawnWeight = 0, MaxHealth = 72, Experience = 86,
                Damage = 26, DamageType = DamageType.Physical, AttackRange = 2.6f,
                AttackCooldown = 2.2f, SpeedRatio = 0.6f, Scale = 1f,
                Body = CreatureBody.BarrowCastellan, Skill = EnemySkill.BonePrison, SkillCooldown = 9, DropChance = 0.55f,
                Skin = new Color(0.33f, 0.46f, 0.43f), Cloth = new Color(0.51f, 0.46f, 0.30f), Pants = new Color(0.30f, 0.25f, 0.22f),
                Eyes = new Color(0.40f, 0.95f, 0.85f),
                Armour = 140, Undead = true,
            },
            new EnemyKind
            {
                Name = "Cistern Leech", SpawnWeight = 0, MaxHealth = 37, Experience = 65,
                Damage = 20, DamageType = DamageType.Poison, AttackRange = 2.6f,
                AttackCooldown = 2.2f, SpeedRatio = 0.85f, Scale = 0.8f,
                Body = CreatureBody.Slime, Skill = EnemySkill.DraggingBreath, SkillCooldown = 9, DropChance = 0.55f,
                Skin = new Color(0.20f, 0.37f, 0.30f), Cloth = new Color(0.42f, 0.64f, 0.49f), Pants = new Color(0.30f, 0.25f, 0.22f),
                Eyes = new Color(0.63f, 0.95f, 0.38f),
                PackSize = 1, PoisonResistance = 30,
            },
            new EnemyKind
            {
                Name = "Memory Scribe", SpawnWeight = 0, MaxHealth = 49, Experience = 92,
                Damage = 23, DamageType = DamageType.Cold, AttackRange = 12f,
                AttackCooldown = 2.7f, SpeedRatio = 0.6f, Scale = 1f,
                Body = CreatureBody.Humanoid, Skill = EnemySkill.Wail, SkillCooldown = 9, DropChance = 0.55f,
                Skin = new Color(0.66f, 0.70f, 0.55f), Cloth = new Color(0.20f, 0.39f, 0.38f), Pants = new Color(0.30f, 0.25f, 0.22f),
                Eyes = new Color(0.50f, 0.98f, 0.92f), Style = EnemyAttackStyle.Ranged, ProjectileSpeed = 11, StaffOrb = new Color(0.50f, 0.98f, 0.92f),
                HideHorns = true, Skeletal = true, Undead = true, Gear = new[] { "hunter_hood" },
            },
            new EnemyKind
            {
                Name = "Drowned Fugitive", SpawnWeight = 0, MaxHealth = 48, Experience = 78,
                Damage = 23, DamageType = DamageType.Physical, AttackRange = 2.6f,
                AttackCooldown = 2.2f, SpeedRatio = 0.98f, Scale = 1f,
                Body = CreatureBody.RimeStalker, Skill = EnemySkill.Blink, SkillCooldown = 9, DropChance = 0.55f,
                Skin = new Color(0.39f, 0.53f, 0.49f), Cloth = new Color(0.23f, 0.32f, 0.30f), Pants = new Color(0.30f, 0.25f, 0.22f),
                Eyes = new Color(0.60f, 0.90f, 0.85f),
                Undead = true,
            },
            new EnemyKind
            {
                Name = "Root Goliath", SpawnWeight = 0, MaxHealth = 85, Experience = 100,
                Damage = 28, DamageType = DamageType.Physical, AttackRange = 2.6f,
                AttackCooldown = 2.2f, SpeedRatio = 0.45f, Scale = 1.45f,
                Body = CreatureBody.Briarbound, Skill = EnemySkill.DredgeFault, SkillCooldown = 9, DropChance = 0.55f,
                Skin = new Color(0.39f, 0.22f, 0.19f), Cloth = new Color(0.55f, 0.36f, 0.27f), Pants = new Color(0.30f, 0.25f, 0.22f),
                Eyes = new Color(0.95f, 0.37f, 0.48f),
                Armour = 100, PoisonResistance = 25,
            },
            new EnemyKind
            {
                Name = "Marrow Tick", SpawnWeight = 0, MaxHealth = 32, Experience = 66,
                Damage = 18, DamageType = DamageType.Poison, AttackRange = 2.6f,
                AttackCooldown = 2.2f, SpeedRatio = 1.1f, Scale = 0.65f,
                Body = CreatureBody.Spider, Skill = EnemySkill.WebSpit, SkillCooldown = 9, DropChance = 0.55f,
                Skin = new Color(0.59f, 0.42f, 0.31f), Cloth = new Color(0.43f, 0.17f, 0.24f), Pants = new Color(0.30f, 0.25f, 0.22f),
                Eyes = new Color(0.95f, 0.48f, 0.52f),
                PackSize = 2, Sounds = EnemySounds.Set.Spider,
            },
            new EnemyKind
            {
                Name = "Sap Cantor", SpawnWeight = 0, MaxHealth = 52, Experience = 97,
                Damage = 22, DamageType = DamageType.Poison, AttackRange = 12f,
                AttackCooldown = 2.7f, SpeedRatio = 0.6f, Scale = 1f,
                Body = CreatureBody.GraveSiren, Skill = EnemySkill.WarCry, SkillCooldown = 9, DropChance = 0.55f,
                Skin = new Color(0.50f, 0.29f, 0.32f), Cloth = new Color(0.29f, 0.17f, 0.23f), Pants = new Color(0.30f, 0.25f, 0.22f),
                Eyes = new Color(0.95f, 0.45f, 0.57f), Style = EnemyAttackStyle.Ranged, ProjectileSpeed = 11, StaffOrb = new Color(0.95f, 0.45f, 0.57f),
                PoisonResistance = 35,
            },
            new EnemyKind
            {
                Name = "Grafted Stalker", SpawnWeight = 0, MaxHealth = 53, Experience = 90,
                Damage = 25, DamageType = DamageType.Physical, AttackRange = 2.6f,
                AttackCooldown = 2.2f, SpeedRatio = 0.92f, Scale = 1.05f,
                Body = CreatureBody.Hollowmaw, Skill = EnemySkill.Leap, SkillCooldown = 9, DropChance = 0.55f,
                Skin = new Color(0.53f, 0.37f, 0.26f), Cloth = new Color(0.37f, 0.20f, 0.23f), Pants = new Color(0.30f, 0.25f, 0.22f),
                Eyes = new Color(0.98f, 0.36f, 0.48f)
            },
            new EnemyKind
            {
                Name = "Ossan, Warden of the Unremembered", SpawnWeight = 0, IsBoss = true, Boss = BossStyle.Ossan,
                MaxHealth = 66, HealthMultiplier = 25, Armour = 130, Experience = 750,
                Damage = 27, DamageType = DamageType.Cold, BasicDamageType = DamageType.Physical,
                AttackRange = 4, AttackCooldown = 2.6f, SpeedRatio = 0.38f, Scale = 1.9f, Body = CreatureBody.Ossan,
                Skin = new Color(0.35f, 0.48f, 0.43f), Cloth = new Color(0.60f, 0.52f, 0.31f), Pants = new Color(0.44f, 0.36f, 0.25f),
                Eyes = new Color(0.50f, 1f, 0.90f), DropChance = 1, RareBonus = 0.5f, Drops = 5
            },
            new EnemyKind
            {
                Name = "The First Root, Heart of the Tithe", SpawnWeight = 0, IsBoss = true, Boss = BossStyle.FirstRoot,
                MaxHealth = 72, HealthMultiplier = 28, Armour = 105, Experience = 750,
                Damage = 28, DamageType = DamageType.Poison, BasicDamageType = DamageType.Physical,
                AttackRange = 4, AttackCooldown = 2.6f, SpeedRatio = 0.38f, Scale = 1.9f, Body = CreatureBody.FirstRoot,
                Skin = new Color(0.38f, 0.21f, 0.19f), Cloth = new Color(0.57f, 0.31f, 0.36f), Pants = new Color(0.44f, 0.36f, 0.25f),
                Eyes = new Color(1f, 0.40f, 0.53f), DropChance = 1, RareBonus = 0.5f, Drops = 5
            },
            // Append only: effigies share the friendly entity snapshot path.
            new EnemyKind { Name = "Spell Effigy", SpawnWeight = 0f, MaxHealth = 90f, Scale = 1f,
                Style = EnemyAttackStyle.Ranged, AttackRange = 12f },
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

        /// <summary>Shared level growth for every creature, regardless of its original area.</summary>
        public static float LifeScale(int level, EnemyKind kind = null) =>
            (1f + 0.18f * (Mathf.Max(1, level) - 1)) * (kind != null ? kind.HealthMultiplier : 1f);

        public static float DamageScale(int level, EnemyKind kind) =>
            (1f + 0.055f * (Mathf.Max(1, level) - 1)) * (kind != null ? kind.DamageMultiplier : 1f);

        /// <summary>Elemental pressure ramps through Greenwood, reaching +25% in level-nine areas.
        /// At 20% resistance this restores the original hit; 15% leaves it only 6.25% higher.
        /// Physical and poison damage retain their existing scaling.</summary>
        public static float HitScale(int level, EnemyKind kind, DamageType type)
        {
            bool elemental = type == DamageType.Fire || type == DamageType.Cold || type == DamageType.Lightning;
            float pressure = elemental ? 1f + 0.25f * Mathf.Clamp01((level - 1f) / 8f) : 1f;
            return DamageScale(level, kind) * pressure;
        }

        public static float ArmourScale(int level) => 1f + 0.06f * (Mathf.Max(1, level) - 1);

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

            if (kind.Name == "Spell Effigy")
            {
                PoeClone.Skills.SpellEffigy.BuildVisual(model);
                return;
            }

            if (kind.IsCreature)
            {
                BuildCreature(enemy, model, kind);
                return;
            }

            // Recolour first, so the gear added below keeps its own colours.
            Recolour(model, kind);

            EquipmentSet gear = kind.Gear.Length > 0 ? GearOf(kind) : null;

            // Horns would poke through a helmet.
            if (kind.HideHorns || gear?.Get(EquipSlot.Helmet) != null)
            {
                foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "HornL" || t.name == "HornR")
                        t.gameObject.SetActive(false);
                }
            }

            // Archers carry the Short Bow from their gear list; casters get a staff, desert kinds a
            // blade or spear. Whatever is held, the hand closes round it.
            bool desertWeapon = kind.Javelin || kind.Skill == EnemySkill.DesertCharge || kind.Skill == EnemySkill.Tollkeeper;
            bool staff = kind.IsRanged && !kind.Bow && !kind.Javelin;
            SlotRules.HeldHands(gear, out bool gripLeft, out bool gripRight);
            gripRight |= desertWeapon || staff;

            model.GetComponent<CharacterWalkAnimator>()?.SetArmRestAngle(kind.ReachingArms ? CharacterWalkAnimator.ReachingArmAngle : 0f);

            if (kind.Boss == BossStyle.Shepherd)
                ShepherdLook.Build(model);
            if (kind.Skeletal)
                SkeletonLook.Build(model, kind, gripLeft, gripRight);

            if (gear != null)
            {
                EquipmentVisuals visuals = model.gameObject.AddComponent<EquipmentVisuals>();
                visuals.Bind(gear);
            }

            if (desertWeapon)
                AddDesertWeapon(model, kind);
            if (staff)
                AddStaff(model, kind.StaffOrb);
            if (desertWeapon || staff)
                model.GetComponent<ProceduralAnatomy>()?.HoldProp(false, true);
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
                Eyes = kind.Eyes,
                Variant = kind.Variant
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

        // The kind's gear list, each piece in the first free slot that takes it.
        private static EquipmentSet GearOf(EnemyKind kind)
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
            return gear;
        }

        // Swaps the colour of each of the rig's own materials (cloth, skin, pants, eyes) for the
        // kind's, through property blocks so the shared materials themselves stay untouched. A
        // connected arm is sleeve and skin in one renderer, so each material slot gets its own.
        private static void Recolour(Transform root, EnemyKind kind)
        {
            var block = new MaterialPropertyBlock();
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = r.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null)
                        continue;

                    Color color = ColourFor(materials[i].name, kind);
                    if (color.a <= 0f)
                        continue;

                    if (materials.Length == 1)
                        r.GetPropertyBlock(block);
                    else
                        r.GetPropertyBlock(block, i);
                    block.SetColor(BaseColorId, color);
                    block.SetColor(ColorId, color);
                    if (materials.Length == 1)
                        r.SetPropertyBlock(block);
                    else
                        r.SetPropertyBlock(block, i);
                }
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
