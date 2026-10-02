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

    public enum BossStyle
    {
        None,
        Gravelord,   // ground slam, raises zombies
        Warlord,     // ground slam, rains fire on the player
        FrostQueen   // ground slam, calls down ice that chills
    }

    /// <summary>
    /// One type of enemy: its numbers, how it fights, and how it looks. Every type is the same
    /// rigged enemy prefab, told apart by size, recoloured clothes/skin/eyes, and gear on its
    /// sockets (the player's equipment models, or a caster's staff).
    /// </summary>
    public sealed class EnemyKind
    {
        public string Name;
        public float SpawnWeight;

        public float MaxHealth;
        public int Experience;

        public EnemyAttackStyle Style;
        public DamageType DamageType;
        public float Damage;
        public float AttackCooldown;
        public float AttackRange;
        public float ProjectileSpeed;

        public float SpeedRatio;
        public float Scale = 1f;

        // Replacement colours for the prefab's own materials; clear keeps the original (the zombie).
        public Color Cloth = Color.clear;
        public Color Skin = Color.clear;
        public Color Pants = Color.clear;
        public Color Eyes = Color.clear;

        public bool HideHorns;
        public string[] Gear = new string[0];
        public Color StaffOrb;   // casters: the orb on the staff and the colour of their bolts
        public bool Bow;         // archers: shoots arrows instead of bolts (the bow itself is in Gear)

        public float DropChance;
        public float RareBonus;

        // Bosses: never spawn at random (weight 0), placed by a BossLair; drop several items,
        // shrug off stagger, and get a BossAbilities set of their own.
        public bool IsBoss;
        public int Drops = 1;
        public BossStyle Boss;

        public bool IsRanged => Style == EnemyAttackStyle.Ranged;
    }

    /// <summary>
    /// The enemy roster: three melee types, an archer and three elemental casters found
    /// everywhere; skeletons, wraiths and ember knights native to the deeper areas; and the bosses.
    /// </summary>
    public static class EnemyKinds
    {
        public static readonly EnemyKind[] All =
        {
            new EnemyKind
            {
                Name = "Zombie", SpawnWeight = 30f,
                MaxHealth = 30f, Experience = 20,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Physical,
                Damage = 6f, AttackCooldown = 1.4f, AttackRange = 2.0f,
                SpeedRatio = 0.5f,
                DropChance = 0.25f
            },
            new EnemyKind
            {
                Name = "Raider", SpawnWeight = 18f,
                MaxHealth = 42f, Experience = 30,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Physical,
                Damage = 5f, AttackCooldown = 0.9f, AttackRange = 2.0f,
                SpeedRatio = 0.62f, Scale = 0.95f, 
                Cloth = new Color(0.62f, 0.42f, 0.25f), Skin = new Color(0.78f, 0.60f, 0.45f), Eyes = new Color(0.15f, 0.10f, 0.08f), HideHorns = true,
                Gear = new[] { "bronze_helmet", "rusty_sword", "wooden_shield" },
                DropChance = 0.35f
            },
            new EnemyKind
            {
                Name = "Brute", SpawnWeight = 12f,
                MaxHealth = 90f, Experience = 55,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Physical,
                Damage = 15f, AttackCooldown = 2.2f, AttackRange = 2.6f,
                SpeedRatio = 0.42f, Scale = 1.35f, 
                Cloth = new Color(0.22f, 0.22f, 0.25f), Skin = new Color(0.55f, 0.42f, 0.38f), Eyes = new Color(1.0f, 0.2f, 0.1f),
                Gear = new[] { "studded_vest" },
                DropChance = 0.7f, RareBonus = 0.15f
            },
            new EnemyKind
            {
                Name = "Archer", SpawnWeight = 14f,
                MaxHealth = 28f, Experience = 28,
                Style = EnemyAttackStyle.Ranged, DamageType = DamageType.Physical,
                Damage = 7f, AttackCooldown = 1.8f, AttackRange = 12f, ProjectileSpeed = 20f,
                SpeedRatio = 0.55f, Scale = 0.95f, 
                Cloth = new Color(0.28f, 0.55f, 0.24f), Skin = new Color(0.75f, 0.62f, 0.48f), Pants = new Color(0.42f, 0.33f, 0.20f), Eyes = new Color(0.15f, 0.10f, 0.08f), HideHorns = true,
                Gear = new[] { "leather_gloves", "leather_boots", "short_bow" },
                Bow = true,
                DropChance = 0.35f
            },
            new EnemyKind
            {
                Name = "Fire Caster", SpawnWeight = 11f,
                MaxHealth = 26f, Experience = 30,
                Style = EnemyAttackStyle.Ranged, DamageType = DamageType.Fire,
                Damage = 9f, AttackCooldown = 2.0f, AttackRange = 10f, ProjectileSpeed = 11f,
                SpeedRatio = 0.45f, Scale = 0.95f, 
                Cloth = new Color(0.80f, 0.25f, 0.05f), Skin = new Color(0.55f, 0.35f, 0.30f), Eyes = new Color(1.0f, 0.6f, 0.1f), HideHorns = true,
                StaffOrb = new Color(1.0f, 0.45f, 0.10f),
                DropChance = 0.35f
            },
            new EnemyKind
            {
                Name = "Frost Caster", SpawnWeight = 10f,
                MaxHealth = 26f, Experience = 30,
                Style = EnemyAttackStyle.Ranged, DamageType = DamageType.Cold,
                Damage = 7f, AttackCooldown = 2.0f, AttackRange = 10f, ProjectileSpeed = 10f,
                SpeedRatio = 0.45f, Scale = 0.95f, 
                Cloth = new Color(0.15f, 0.35f, 0.80f), Skin = new Color(0.75f, 0.85f, 0.95f), Eyes = new Color(0.5f, 0.95f, 1.0f), HideHorns = true,
                StaffOrb = new Color(0.55f, 0.85f, 1.0f),
                DropChance = 0.35f
            },
            new EnemyKind
            {
                Name = "Storm Caster", SpawnWeight = 10f,
                MaxHealth = 22f, Experience = 30,
                Style = EnemyAttackStyle.Ranged, DamageType = DamageType.Lightning,
                Damage = 8f, AttackCooldown = 1.6f, AttackRange = 11f, ProjectileSpeed = 16f,
                SpeedRatio = 0.5f, Scale = 0.9f, 
                Cloth = new Color(0.38f, 0.20f, 0.60f), Skin = new Color(0.70f, 0.70f, 0.60f), Eyes = new Color(1.0f, 1.0f, 0.4f), HideHorns = true,
                StaffOrb = new Color(1.0f, 0.95f, 0.35f),
                DropChance = 0.35f
            },

            // Area natives (weight 0 by default: only areas that list them spawn them).
            new EnemyKind
            {
                Name = "Skeleton", SpawnWeight = 0f,
                MaxHealth = 24f, Experience = 26,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Physical,
                Damage = 6f, AttackCooldown = 1.0f, AttackRange = 2.0f,
                SpeedRatio = 0.68f, Scale = 0.95f,
                Cloth = new Color(0.82f, 0.80f, 0.72f), Skin = new Color(0.90f, 0.88f, 0.80f), Pants = new Color(0.32f, 0.30f, 0.27f), Eyes = new Color(1.0f, 0.25f, 0.1f), HideHorns = true,
                Gear = new[] { "rusty_sword" },
                DropChance = 0.3f
            },
            new EnemyKind
            {
                Name = "Wraith", SpawnWeight = 0f,
                MaxHealth = 34f, Experience = 34,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Cold,
                Damage = 8f, AttackCooldown = 1.5f, AttackRange = 2.2f,
                SpeedRatio = 0.75f, Scale = 1.05f,
                Cloth = new Color(0.58f, 0.70f, 0.64f), Skin = new Color(0.80f, 0.95f, 0.90f), Pants = new Color(0.40f, 0.50f, 0.47f), Eyes = new Color(0.4f, 1.0f, 0.7f), HideHorns = true,
                DropChance = 0.35f
            },
            new EnemyKind
            {
                Name = "Ember Knight", SpawnWeight = 0f,
                MaxHealth = 70f, Experience = 50,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Fire,
                Damage = 12f, AttackCooldown = 1.8f, AttackRange = 2.4f,
                SpeedRatio = 0.5f, Scale = 1.2f,
                Cloth = new Color(0.36f, 0.12f, 0.06f), Skin = new Color(0.26f, 0.20f, 0.18f), Pants = new Color(0.16f, 0.12f, 0.10f), Eyes = new Color(1.0f, 0.55f, 0.1f), HideHorns = true,
                Gear = new[] { "iron_helmet", "iron_mace", "wooden_shield" },
                DropChance = 0.55f, RareBonus = 0.1f
            },

            // Bosses: weight 0, so only their lairs place them.
            new EnemyKind
            {
                Name = "Gravelord Mortis", SpawnWeight = 0f,
                MaxHealth = 340f, Experience = 400,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Physical,
                Damage = 13f, AttackCooldown = 1.9f, AttackRange = 3.4f,
                SpeedRatio = 0.5f, Scale = 1.9f,
                Cloth = new Color(0.16f, 0.18f, 0.16f), Skin = new Color(0.62f, 0.68f, 0.58f), Pants = new Color(0.12f, 0.12f, 0.12f), Eyes = new Color(0.3f, 1.0f, 0.4f),
                Gear = new[] { "iron_helmet", "hand_axe" },
                DropChance = 1f, RareBonus = 0.6f, Drops = 3,
                IsBoss = true, Boss = BossStyle.Gravelord
            },
            new EnemyKind
            {
                Name = "Ashen Warlord", SpawnWeight = 0f,
                MaxHealth = 420f, Experience = 700,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Fire,
                Damage = 16f, AttackCooldown = 1.8f, AttackRange = 3.6f,
                SpeedRatio = 0.55f, Scale = 2.1f,
                Cloth = new Color(0.30f, 0.06f, 0.04f), Skin = new Color(0.22f, 0.18f, 0.17f), Pants = new Color(0.10f, 0.08f, 0.08f), Eyes = new Color(1.0f, 0.55f, 0.1f),
                Gear = new[] { "studded_vest", "iron_mace", "wooden_shield" },
                DropChance = 1f, RareBonus = 0.8f, Drops = 4,
                IsBoss = true, Boss = BossStyle.Warlord
            },
            new EnemyKind
            {
                Name = "Rimeheart", SpawnWeight = 0f,
                MaxHealth = 460f, Experience = 1000,
                Style = EnemyAttackStyle.Melee, DamageType = DamageType.Cold,
                Damage = 15f, AttackCooldown = 1.6f, AttackRange = 3.4f,
                SpeedRatio = 0.6f, Scale = 2.0f,
                Cloth = new Color(0.62f, 0.80f, 0.95f), Skin = new Color(0.82f, 0.92f, 1.0f), Pants = new Color(0.35f, 0.45f, 0.62f), Eyes = new Color(0.3f, 0.9f, 1.0f),
                Gear = new[] { "iron_helmet", "steel_dagger" },
                DropChance = 1f, RareBonus = 0.9f, Drops = 4,
                IsBoss = true, Boss = BossStyle.FrostQueen
            },
        };

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
            return weights != null && k < weights.Length ? weights[k] : All[k].SpawnWeight;
        }

        /// <summary>
        /// How much tougher each monster level makes an enemy: life, damage and the experience it's worth.
        /// </summary>
        public static float LifeScale(int level) => 1f + 0.35f * (Mathf.Max(1, level) - 1);
        public static float DamageScale(int level) => 1f + 0.22f * (Mathf.Max(1, level) - 1);
        public static float ExperienceScale(int level) => 1f + 0.4f * (Mathf.Max(1, level) - 1);

        /// <summary>Makes a freshly spawned enemy this kind and level: its look and all its gameplay numbers.</summary>
        public static void Apply(GameObject enemy, int index, int level = 1)
        {
            EnemyKind kind = Get(index);
            ApplyLook(enemy, kind);

            enemy.GetComponent<EnemyHealth>()?.Configure(index, kind, level);
            enemy.GetComponent<EnemyController>()?.Configure(kind);
            enemy.GetComponent<EnemyCombat>()?.Configure(kind, level);
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
        private static void AddStaff(Transform model, Color orb)
        {
            Transform hand = FindChild(model, "Socket_MainHand") ?? FindChild(model, "Socket_HandR");
            if (hand == null)
                return;

            var staff = new GameObject("Staff");
            staff.transform.SetParent(hand, false);

            GameObject shaft = RuntimePrimitives.Create(PrimitiveType.Cylinder, staff.transform, new Color(0.35f, 0.24f, 0.14f));
            shaft.transform.localScale = new Vector3(0.09f, 0.8f, 0.09f);
            shaft.transform.localPosition = new Vector3(0f, 0.25f, 0f);

            GameObject top = RuntimePrimitives.Create(PrimitiveType.Sphere, staff.transform, orb);
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
