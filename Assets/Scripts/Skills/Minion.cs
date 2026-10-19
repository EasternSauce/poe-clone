using System.Collections.Generic;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.UI;
using PoeClone.Visuals;

namespace PoeClone.Skills
{
    /// <summary>The kinds of minion the summon skills raise.</summary>
    public enum MinionKind
    {
        Warrior,    // Raise Skeletons: permanent, capped, melee
        Mage,       // Skeleton Mages: permanent, capped, hangs back and casts bolts
        Wolf,       // Spirit Wolves: a temporary pack, fast, enemies can't target them
        Golem,
        Viper,
        Effigy
    }

    /// <summary>
    /// A summoner's minion. It wears an enemy body (the enemy prefab with its enemy scripts
    /// stripped off, dressed as one of the minion kinds at the end of <see cref="EnemyKinds"/>),
    /// follows its owner, and fights the nearest enemy close by - or the one under Death Mark.
    /// <para>
    /// Enemies fight back: they go for a minion that is nearer than the player (and always for a
    /// Bone Golem close to them), so minions take the heat off the player only while they last.
    /// Minions take full enemy hits, mitigated by their own armour and resistances. Life grows
    /// modestly with summon level; a durable army needs minion defence from gear and passives.
    /// </para>
    /// </summary>
    public class Minion : MonoBehaviour
    {
        public static readonly List<Minion> All = new List<Minion>();

        /// <summary>Spectator stream ids for minions start here, clear of the enemies' small ids.</summary>
        public const int ReplicationIdBase = 1000000;
        private static int nextId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            All.Clear();
            nextId = 0;
            marked = null;
        }

        /// <summary>Stable for this minion's life (its spectator copy is keyed by it).</summary>
        public int Id { get; private set; }

        public MinionKind Kind { get; private set; }
        public float Life { get; private set; }
        public float MaxLife { get; private set; }
        public bool IsDead => dead;

        /// <summary>Enemies can attack it (everything but the spirit wolves).</summary>
        public bool Targetable => !dead && Kind != MinionKind.Wolf;

        /// <summary>The golem: enemies close to it go for it, whoever else is near.</summary>
        public bool Taunts => !dead && Kind == MinionKind.Golem;

        /// <summary>Which enemy kind (index) it looks like, for the spectator stream.</summary>
        public int LookIndex { get; private set; }

        public const float BaseArmour = 60f;
        public const float BaseResistance = 10f;

        private const float FollowRadius = 2.5f;
        private const float FollowStopDistance = 0.6f;
        private const float FollowArrivalTime = 0.25f;
        // Far Command doubles the engage radius and leash; Bone Tether shortens the teleport.
        private const float EngageRadius = 6f;
        private const float MageEngageRadius = 9f;
        private const float Leash = 24f;            // only enemies this close to the owner are fought...
        private const float MarkLeash = 64f;        // ...or the marked one, a good deal further off
        private const float TeleportDistance = 32f;
        private const float TetherTeleportDistance = 20f;

        private Transform owner;
        public Transform Owner => owner;
        private CharacterController body;
        private CharacterAttackAnimator attack;
        private float damage;
        private int extraProjectiles;
        private float expiresAt;
        private float speed;
        private float attackCooldown;
        private float attackRange;
        private float cooldown;
        private float verticalVelocity;
        private EnemyHealth target;
        private EnemyHealth striking;
        private Vector3 slot;
        private float retargetAt;
        private float teleportRetryAt;
        private float damagedAt = float.NegativeInfinity;
        private float mageRallyUntil;
        private bool dead;
        private float soulBond;
        private float commandRange = 1f;
        private bool tether;
        // Marching Horde: % increased attack and movement speed for each of the owner's minions.
        private float hordeSpeed, baseSpeed, baseCooldown, moveIncrease, attackIncrease;
        private WeaponSkillBonuses tree;

        // ------------------------------------------------------------------ numbers

        private struct Profile
        {
            public float Life;          // at level 1
            public float Damage;        // per hit at level 1
            public float DamageGrowth;  // fraction of base damage per owner character level
            public float Cooldown;      // seconds between attacks
            public float Range;
            public float Speed;
            public string Look;         // the EnemyKind it wears
        }

        private static Profile ProfileOf(MinionKind kind)
        {
            switch (kind)
            {
                case MinionKind.Mage:
                    return new Profile { Life = 369f, Damage = 18f, DamageGrowth = .035f, Cooldown = 1.6f, Range = 12f, Speed = 6f, Look = "Skeleton Mage" };
                case MinionKind.Wolf:
                    return new Profile { Life = 88f, Damage = 7.25f, DamageGrowth = .035f, Cooldown = 0.8f, Range = 2f, Speed = 9f, Look = "Spirit Wolf" };
                case MinionKind.Golem:
                    return new Profile { Life = 248f, Damage = 24f, DamageGrowth = .045f, Cooldown = 1.5f, Range = 2.6f, Speed = 5.5f, Look = "Bone Golem" };
                case MinionKind.Viper:
                    return new Profile { Life = 93f, Damage = 16f, DamageGrowth = .0122f, Cooldown = 1.15f, Range = 2.2f, Speed = 8f, Look = "Giant Spider" };
                default:
                    return new Profile { Life = 70f, Damage = 10f, DamageGrowth = .035f, Cooldown = 1.1f, Range = 2.1f, Speed = 6.5f, Look = "Skeleton Warrior" };
            }
        }

        /// <summary>Fixed base life before equipment and weapon tree bonuses.</summary>
        public static float LifeAt(MinionKind kind, int level) => ProfileOf(kind).Life;

        /// <summary>Fixed base damage before minion stats and weapon tree bonuses.</summary>
        public static float DamageAt(MinionKind kind, int level)
        {
            // Scale the previous 1.3x baseline target down to 1.2x for every summon.
            return ProfileOf(kind).Damage * (2f * 1.2f / 1.3f);
        }

        /// <summary>Stays until destroyed (warriors and mages); only one such kind can be up at a time.</summary>
        public static bool IsPermanent(MinionKind kind) => kind == MinionKind.Warrior || kind == MinionKind.Mage;

        /// <summary>How long a temporary minion lasts (0: until destroyed).</summary>
        public static float Duration(MinionKind kind, int level, StatSheet sheet)
        {
            float more = 1f + (sheet != null ? Mathf.Max(0f, sheet.Total(StatType.MinionDuration)) : 0f) / 100f;
            switch (kind)
            {
                case MinionKind.Wolf: return 12f * more;
                case MinionKind.Golem: return 18f * more;
                case MinionKind.Viper: return 16f * more;
                default: return 0f;
            }
        }

        private static float Stat(StatSheet sheet, StatType stat) => sheet != null ? sheet.Total(stat) : 0f;

        /// <summary>Life with the owner's stats and the summoned minion's weapon-tree snapshot.</summary>
        public static float LifeFor(MinionKind kind, int level, StatSheet sheet, WeaponSkillBonuses tree = null) =>
            Mathf.Max(1f, LifeAt(kind, level) * Mathf.Max(0.1f, 1f +
                (Stat(sheet, StatType.MinionLife) + (tree?.Get(WeaponSkillEffect.MinionLife) ?? 0f)) / 100f));

        /// <summary>Mage bolts are projectile spells as well as minion hits.</summary>
        public static bool UsesProjectileSpell(MinionKind kind) => kind == MinionKind.Mage;

        /// <summary>Mages inherit cast speed; attack-speed bonuses only affect attacking minions.</summary>
        public static float ActionSpeedIncreaseFor(MinionKind kind, StatSheet sheet, WeaponSkillBonuses tree = null) =>
            Stat(sheet, UsesProjectileSpell(kind) ? StatType.CastSpeed : StatType.MinionSpeed) +
            (tree?.Get(WeaponSkillEffect.Speed) ?? 0f);

        /// <summary>Minion damage stacks additively with spell damage for mage bolts.</summary>
        public static float DamageFor(MinionKind kind, int level, StatSheet sheet, WeaponSkillBonuses tree = null) =>
            DamageAt(kind, level) * (1f + ProfileOf(kind).DamageGrowth * Mathf.Max(0, (sheet?.Level ?? 1) - 1)) * Mathf.Max(0f, 1f +
                (Stat(sheet, StatType.Intelligence) * 0.5f + Stat(sheet, StatType.MinionDamage) +
                 (UsesProjectileSpell(kind) ? Stat(sheet, StatType.SpellDamage) : 0f) + (tree?.Get(WeaponSkillEffect.Damage) ?? 0f)) / 100f) * (tree?.MoreDamageMultiplier ?? 1f) *
            (sheet != null ? sheet.Multiplier(StatType.MoreMinionDamage) * sheet.Multiplier(StatType.MinionDamagePenalty) *
                (UsesProjectileSpell(kind) ? sheet.Multiplier(StatType.MoreSpellDamage) : 1f) : 1f);

        public static float ArmourFor(StatSheet sheet, WeaponSkillBonuses tree = null) =>
            Mathf.Max(0f, BaseArmour + Stat(sheet, StatType.MinionArmour)) + (tree?.Get(WeaponSkillEffect.MinionArmour) ?? 0f);

        public static float ResistanceFor(StatSheet sheet, WeaponSkillBonuses tree = null) =>
            Mathf.Min(StatSheet.ResistanceCap,
                Mathf.Min(StatSheet.ResistanceCap, BaseResistance + Stat(sheet, StatType.MinionResistances)) *
                (1f + (tree?.Get(WeaponSkillEffect.MinionResists) ?? 0f) / 100f));

        /// <summary>Damage after the minion's own armour or resistance and Bone Armour.</summary>
        public static float DamageTaken(float amount, DamageType type, StatSheet sheet, WeaponSkillBonuses tree = null)
        {
            if (type == DamageType.Physical)
                amount *= 1f - DefenceMath.ArmourReduction(ArmourFor(sheet, tree), amount);
            else
                amount = DefenceMath.AfterResistance(amount, ResistanceFor(sheet, tree));
            return amount * (sheet != null ? sheet.Multiplier(StatType.BoneArmour) : 1f);
        }

        // ------------------------------------------------------------------ summoning

        public static SkillId SkillFor(MinionKind kind)
        {
            switch (kind)
            {
                case MinionKind.Mage: return SkillId.SkeletonMages;
                case MinionKind.Wolf: return SkillId.SpiritWolves;
                case MinionKind.Golem: return SkillId.BoneGolem;
                case MinionKind.Viper: return SkillId.SummonViper;
                default: return SkillId.RaiseSkeletons;
            }
        }
        private static WeaponSkillBonuses TreeFor(Transform owner, MinionKind kind) =>
            owner != null ? owner.GetComponent<PlayerSkills>()?.BonusesFor(SkillFor(kind)) ?? new WeaponSkillBonuses(null) : new WeaponSkillBonuses(null);
        public static int CapFor(Transform owner, MinionKind kind)
            => CapFor(kind, TreeFor(owner, kind), owner != null ? owner.GetComponent<PlayerInventory>()?.Stats : null);

        /// <summary>The same cap for gameplay, tooltips and simulations without a player object.</summary>
        public static int CapFor(MinionKind kind, WeaponSkillBonuses tree) => CapFor(kind, tree, null);

        public static int CapFor(MinionKind kind, WeaponSkillBonuses tree, StatSheet sheet)
        {
            int cap = (kind == MinionKind.Warrior || kind == MinionKind.Wolf ? 2 : 1) +
                (tree?.ExtraMinions ?? 0) + Mathf.RoundToInt(Stat(sheet, StatType.AdditionalMinions));
            if (IsPermanent(kind)) cap += Mathf.RoundToInt(Stat(sheet, StatType.AdditionalSkeletons));
            return tree != null && tree.Get(WeaponSkillEffect.HalfMinions) > 0 ? cap / 2 : cap;
        }
        public static bool AtCap(Transform owner, MinionKind kind) =>
            LivingCount(owner, kind) >= CapFor(owner, kind);

        public static int LivingCount(Transform owner, MinionKind kind)
        {
            int count = 0;
            foreach (Minion minion in All)
                if (minion != null && minion.owner == owner && minion.Kind == kind && !minion.dead)
                    count++;
            return count;
        }
        /// <summary>
        /// Raises minions toward a chosen point, at most six metres from the owner.
        /// Replaces the oldest summons when their kind's limit would be exceeded.
        /// Raising a permanent kind dismisses the other permanent kind.
        /// </summary>
        public static void Summon(Transform owner, MinionKind kind, int level, Vector3 target)
        {
            EnemySpawner spawner = FindAnyObjectByType<EnemySpawner>();
            if (spawner == null || spawner.EnemyPrefab == null || owner == null)
                return;

            PlayerInventory inventory = owner.GetComponent<PlayerInventory>();
            StatSheet sheet = inventory != null ? inventory.Stats : null;
            int cap = CapFor(owner, kind);

            All.RemoveAll(m => m == null);
            // Only one kind of permanent minion at a time: raising one dismisses the others.
            if (IsPermanent(kind))
            {
                foreach (Minion other in All.FindAll(m => m.owner == owner && m.Kind != kind && IsPermanent(m.Kind)))
                    other.Crumble();
            }
            int count = SummonCount(kind, TreeFor(owner, kind), cap);
            // All keeps creation order; dismiss only this owner's oldest matching summons.
            var existing = All.FindAll(m => m.owner == owner && m.Kind == kind && !m.dead);
            int replacements = Mathf.Max(0, existing.Count + count - cap);
            for (int k = 0; k < replacements; k++)
                existing[k].Crumble();
            for (int k = 0; k < count; k++)
            {
                Vector3 forward = target - owner.position;
                forward.y = 0f;
                float distance = Mathf.Clamp(forward.magnitude, 1.6f, 6f);
                forward = forward.sqrMagnitude > 0.001f ? forward.normalized : owner.forward;
                Vector3 side = new Vector3(-forward.z, 0f, forward.x);
                float offset = count == 1 ? 0f : (k - (count - 1) * 0.5f) * 1.1f;
                Vector3 desired = owner.position + Vector3.ClampMagnitude(forward * distance + side * offset, 6f);
                Vector3 at = ClearSummonPoint(owner, desired) + Vector3.up * 0.3f;
                Create(spawner.EnemyPrefab, owner, kind, at, level, sheet);
            }
            // Any successful summon rallies this owner's mages; restoring saved minions does not.
            if (count > 0)
                foreach (Minion minion in All)
                    if (minion != null && minion.owner == owner && minion.Kind == MinionKind.Mage && !minion.dead)
                        minion.mageRallyUntil = Time.time + 4f;
        }

        /// <summary>Per-cast batch, capped before replacing any existing minions.</summary>
        public static int SummonCount(MinionKind kind, WeaponSkillBonuses tree, int cap)
        {
            int baseline = kind == MinionKind.Warrior || kind == MinionKind.Wolf ? 2 : 1;
            return Mathf.Clamp(Mathf.Max(baseline, Mathf.RoundToInt(tree?.Get(WeaponSkillEffect.SummonsPerCast) ?? 0f)), 0, cap);
        }

        /// <summary>Brings back saved permanent minions around the owner, up to their current cap.</summary>
        public static void Restore(Transform owner, MinionKind kind, int count)
        {
            EnemySpawner spawner = FindAnyObjectByType<EnemySpawner>();
            if (spawner == null || spawner.EnemyPrefab == null || owner == null || !IsPermanent(kind))
                return;
            StatSheet sheet = owner.GetComponent<PlayerInventory>()?.Stats;
            count = Mathf.Min(count, CapFor(owner, kind) - LivingCount(owner, kind));
            for (int k = 0; k < count; k++)
            {
                float angle = k * Mathf.PI * 2f / count;
                Vector3 desired = owner.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * FollowRadius;
                Create(spawner.EnemyPrefab, owner, kind, ClearSummonPoint(owner, desired) + Vector3.up * 0.3f, 1, sheet);
            }
        }

        private static Vector3 ClearSummonPoint(Transform owner, Vector3 desired)
        {
            Vector3 from = owner.position + Vector3.up;
            Vector3 to = new Vector3(desired.x, owner.position.y + 1f, desired.z);
            Vector3 path = to - from;
            float distance = path.magnitude;
            float clear = distance;
            foreach (RaycastHit hit in Physics.SphereCastAll(from, 0.35f, path.normalized, distance,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(owner) || hit.collider.GetComponentInParent<EnemyHealth>() != null ||
                    hit.collider.GetComponentInParent<Minion>() != null || hit.normal.y > 0.6f)
                    continue;
                clear = Mathf.Min(clear, Mathf.Max(0.8f, hit.distance - 0.6f));
            }
            Vector3 at = from + path.normalized * clear;
            at.y = owner.position.y;
            return at;
        }

        /// <summary>Dismiss this owner's living summons, for example when their summoning weapon is removed.</summary>
        public static void Desummon(Transform owner)
        {
            if (owner == null)
                return;
            foreach (Minion minion in All.FindAll(m => m != null && m.owner == owner))
                minion.Crumble();
        }

        /// <summary>Recall this owner's mobile summons; effigies remain where they were placed.</summary>
        public static void Recall(Transform owner)
        {
            if (owner == null) return;
            foreach (Minion minion in All)
                if (minion != null && minion.owner == owner && !minion.dead && minion.Kind != MinionKind.Effigy)
                    minion.TeleportToOwner();
        }

        private bool TeleportToOwner()
        {
            // Try the formation slot first, then nearby alternatives toward the owner.
            // Never use an unchecked fallback when the bank or scenery leaves no room.
            Vector3 desired = owner.position + slot;
            Vector3 at;
            bool found = TryRecallPoint(desired, out at);
            for (int ring = 0; ring < 3 && !found; ring++)
                for (int i = 0; i < 12 && !found; i++)
                {
                    float angle = i * Mathf.PI / 6f;
                    float radius = 1f + ring;
                    found = TryRecallPoint(owner.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius, out at);
                }
            if (!found) found = TryRecallPoint(owner.position, out at);
            if (!found) return false;

            SkillEffects.Shockwave(transform.position, 1f, new Color(0.5f, 1f, 0.6f), 0.3f);
            body.enabled = false;
            transform.position = at;
            body.enabled = true;
            verticalVelocity = 0f;
            target = null;
            striking = null;
            retargetAt = Time.time + 0.3f;
            SkillEffects.Shockwave(at, 1f, new Color(0.5f, 1f, 0.6f), 0.3f);
            return true;
        }

        private bool TryRecallPoint(Vector3 candidate, out Vector3 point)
        {
            float scale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
            float kindScale = Mathf.Max(1f, EnemyKinds.Get(LookIndex).Scale);
            float clearance = (body.radius * scale + body.skinWidth) / kindScale;
            if (!EnemySpawner.TryPrepareSpawnPoint(candidate, LookIndex, out point,
                    clearanceRadius: clearance, spawnHeight: 0.1f, ignoreCharacters: true))
                return false;
            // Reject lower riverbeds, other floors and a path crossing an area boundary.
            if (Mathf.Abs(point.y - owner.position.y) > 2f) return false;
            Vector3 reachable = PoeClone.World.GroundObstacleMotion.ClampToArea(body, owner.position, point);
            return Flat(reachable - point).sqrMagnitude < 0.001f;
        }

        public static Minion PlaceEffigy(Transform owner, SkillId skill, int level, Vector3 at)
        {
            var sheet = owner.GetComponent<PlayerInventory>().Stats;
            var existing = All.FindAll(m => m != null && m.owner == owner && m.Kind == MinionKind.Effigy && !m.dead);
            while (existing.Count >= EffigyRules.Limit(sheet)) { existing[0].Crumble(); existing.RemoveAt(0); }
            var go = new GameObject(SkillBook.Get(skill).Name + " Effigy");
            go.transform.SetPositionAndRotation(at, owner.rotation);
            go.layer = 2;
            var model = new GameObject("Model").transform;
            model.SetParent(go.transform, false);
            SpellEffigy.BuildVisual(model, SkillBook.Get(skill).Color);
            var minion = go.AddComponent<Minion>();
            minion.owner = owner;
            minion.Kind = MinionKind.Effigy;
            minion.LookIndex = EnemyKinds.IndexOf("Spell Effigy");
            minion.Id = ++nextId;
            minion.MaxLife = EffigyRules.Life(sheet);
            minion.Life = minion.MaxLife;
            go.AddComponent<SpellEffigy>().Initialize(owner, minion, skill, level);
            All.Add(minion);
            SkillEffects.Shockwave(at, 1.1f, SkillBook.Get(skill).Color, 0.35f);
            return minion;
        }

        public void RegenerateEffigy(float percent)
        {
            if (!dead && Kind == MinionKind.Effigy && percent > 0f) Life = Mathf.Min(MaxLife, Life + MaxLife * percent / 100f);
        }

        private static void Create(GameObject prefab, Transform owner, MinionKind kind, Vector3 at, int level, StatSheet sheet)
        {
            Profile profile = ProfileOf(kind);
            int look = EnemyKinds.IndexOf(profile.Look);
            if (look < 0)
                look = EnemyKinds.SkeletonIndex;

            // Built under an inactive holder so none of the enemy scripts wake up before they go.
            var holder = new GameObject("MinionHolder");
            holder.SetActive(false);
            GameObject go = Instantiate(prefab, at, owner.rotation, holder.transform);
            go.name = profile.Look + " (Minion)";
            foreach (System.Type t in new[] { typeof(EnemyHealthBarUI), typeof(EnemySkills), typeof(EnemyCombat), typeof(EnemyController), typeof(EnemyHealth) })
            {
                Component c = go.GetComponent(t);
                if (c != null)
                    DestroyImmediate(c);
            }
            EnemyKinds.ApplyLook(go, EnemyKinds.Get(look));

            // Out of the way of the player's shots and clicks, and of the player's own body.
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = 2; // Ignore Raycast
            Transform model = go.transform.Find("Model") ?? go.transform;
            var minion = go.AddComponent<Minion>();
            minion.owner = owner;
            minion.tree = TreeFor(owner, kind);
            minion.Kind = kind;
            minion.LookIndex = look;
            minion.Id = ++nextId;
            minion.body = go.GetComponent<CharacterController>();
            minion.attack = model.GetComponent<CharacterAttackAnimator>() ?? model.gameObject.AddComponent<CharacterAttackAnimator>();

            minion.moveIncrease = Stat(sheet, StatType.MinionSpeed) + minion.tree.Get(WeaponSkillEffect.MinionMoveSpeed);
            minion.attackIncrease = ActionSpeedIncreaseFor(kind, sheet, minion.tree);
            minion.extraProjectiles = UsesProjectileSpell(kind) ? sheet?.ExtraSkillProjectiles(spell: true) ?? 0 : 0;
            minion.hordeSpeed = Mathf.Max(0f, Stat(sheet, StatType.MinionHordeSpeed));
            minion.baseSpeed = profile.Speed;
            minion.baseCooldown = profile.Cooldown;
            go.transform.localScale *= 1f + minion.tree.Get(WeaponSkillEffect.MinionSize) / 100f;
            minion.MaxLife = LifeFor(kind, 1, sheet, minion.tree);
            minion.Life = minion.MaxLife;
            minion.damage = DamageFor(kind, 1, sheet, minion.tree);
            minion.attackRange = profile.Range * minion.tree.RangeMultiplier * (kind == MinionKind.Mage ? DefenceMath.RadiusMultiplier(Stat(sheet, StatType.AreaOfEffect) + minion.tree.Get(WeaponSkillEffect.Area)) : 1f);
            minion.ApplySpeed(0);
            minion.soulBond = Mathf.Max(0f, Stat(sheet, StatType.SoulBond));
            minion.commandRange = Stat(sheet, StatType.MinionCommandRange) > 0f ? 2f : 1f;
            minion.tether = Stat(sheet, StatType.MinionTether) > 0f;
            float duration = Duration(kind, 1, sheet);
            duration *= minion.tree.MinionDurationMultiplier;
            minion.expiresAt = duration > 0f ? Time.time + duration : float.PositiveInfinity;

            CharacterController ownerBody = owner.GetComponent<CharacterController>();
            if (ownerBody != null && minion.body != null)
                Physics.IgnoreCollision(ownerBody, minion.body);
            // Minions don't jostle each other off their feet either.
            foreach (Minion other in All)
            {
                if (other != null && other.body != null && minion.body != null)
                    Physics.IgnoreCollision(other.body, minion.body);
            }

            go.transform.SetParent(null, true);
            Destroy(holder);
            go.SetActive(true);
            All.Add(minion);
            SkillEffects.Shockwave(at, kind == MinionKind.Golem ? 2f : 1.3f, new Color(0.5f, 1f, 0.6f), 0.4f);
        }

        // ------------------------------------------------------------------ what enemies see

        /// <summary>How close a minion has to be to draw an enemy away from the player.</summary>
        private const float DrawRange = 9f;

        /// <summary>A golem pulls every enemy this close to it onto itself.</summary>
        private const float TauntRadius = 6.5f;

        /// <summary>
        /// The minion an enemy standing here should attack instead of the player, or null for the
        /// player: a Bone Golem close by, else a minion clearly nearer than the player is.
        /// </summary>
        public static Minion TargetFor(Vector3 enemy, Vector3 player)
        {
            if (All.Count == 0)
                return null;

            float toPlayer = Flat(player - enemy).magnitude;
            Minion best = null;
            float bestDistance = DrawRange;
            foreach (Minion m in All)
            {
                if (m == null || !m.Targetable)
                    continue;
                float d = Flat(m.transform.position - enemy).magnitude;
                if (m.Taunts && d < TauntRadius)
                    return m;
                if (d < bestDistance && d + 0.75f < toPlayer)
                {
                    bestDistance = d;
                    best = m;
                }
            }
            return best;
        }

        /// <summary>Enemy ability damage uses the same footprint for every vulnerable summon.
        /// A shared set limits a travelling wave or overlapping patches to one hit per minion.
        /// Iterate backwards because a lethal hit removes the minion from All immediately.</summary>
        public static bool HitInArea(float damage, DamageType type, System.Func<Vector3, bool> inside,
            HashSet<Minion> hitOnce = null, bool stopAfterFirst = false,
            Dictionary<Minion, float> nextHits = null, float interval = 0f)
        {
            if (damage <= 0f) return false; // spectator effects carry zero damage
            bool hit = false;
            for (int i = All.Count - 1; i >= 0; i--)
            {
                Minion minion = All[i];
                if (minion == null || !minion.Targetable || !minion.isActiveAndEnabled ||
                    (hitOnce != null && hitOnce.Contains(minion)) || !inside(minion.transform.position)) continue;
                if (nextHits != null && nextHits.TryGetValue(minion, out float nextHit) && Time.time < nextHit) continue;
                if (nextHits != null) nextHits[minion] = Time.time + interval;
                hitOnce?.Add(minion);
                minion.TakeHit(damage, type);
                hit = true;
                if (stopAfterFirst) break;
            }
            return hit;
        }

        /// <summary>An enemy's blow (or bolt) lands on this minion.</summary>
        public void TakeHit(float amount, DamageType type)
        {
            if (dead || amount <= 0f)
                return;
            PlayerInventory inventory = owner != null ? owner.GetComponent<PlayerInventory>() : null;
            StatSheet sheet = inventory != null ? inventory.Stats : null;
            amount = Kind == MinionKind.Effigy
                ? EffigyRules.DamageTaken(amount, type == DamageType.Physical, sheet)
                : DamageTaken(amount, type, sheet, tree);
            Life -= amount;
            damagedAt = Time.time;
            Color color = type == DamageType.Physical ? new Color(1f, 0.55f, 0.45f) : CombatText.ColorFor(type);
            CombatText.Show(transform.position + Vector3.up * 1.4f * transform.localScale.y,
                Mathf.Max(1, Mathf.RoundToInt(amount)).ToString(), color, 0.7f);
            if (Life <= 0f)
                Crumble(true);
        }

        // ------------------------------------------------------------------ Death Mark

        private static EnemyHealth marked;
        private static float markUntil;
        private static GameObject markVisual;
        private static readonly Color MarkColor = new Color(0.55f, 1f, 0.45f);

        public const float MarkSeconds = 6f;

        /// <summary>The enemy under Death Mark right now, or null.</summary>
        public static EnemyHealth Marked => marked != null && !marked.IsDead && Time.time < markUntil ? marked : null;

        /// <summary>
        /// Puts Death Mark on an enemy (a grimoire's bolt, a sceptre's blow): the minions go for it
        /// and hit it harder. One mark at a time; a new one moves it.
        /// </summary>
        public static void Mark(EnemyHealth enemy)
        {
            if (enemy == null || enemy.IsDead)
                return;
            bool fresh = Marked != enemy;
            if (fresh)
            {
                Unmark();
                marked = enemy;
                enemy.Died += OnMarkedDied;
                markVisual = BuildMarkVisual(enemy);
                SkillEffects.Shockwave(enemy.transform.position, 1.2f * enemy.transform.localScale.x, MarkColor, 0.3f);
            }
            markUntil = Time.time + MarkSeconds;
        }

        private static void Unmark()
        {
            if (marked != null)
                marked.Died -= OnMarkedDied;
            marked = null;
            if (markVisual != null)
                Destroy(markVisual);
            markVisual = null;
        }

        /// <summary>How much harder minions hit the marked enemy: 30% more, raised by Death Mark Effect.</summary>
        public static float MarkMultiplier(Transform owner)
        {
            PlayerInventory inventory = owner != null ? owner.GetComponent<PlayerInventory>() : null;
            float effect = 1f + Mathf.Max(-100f, Stat(inventory != null ? inventory.Stats : null, StatType.MarkEffect)) / 100f;
            return 1f + 0.3f * effect;
        }

        private static void OnMarkedDied() => Unmark();

        // A slowly turning ring of grave-light over the marked enemy's head.
        private static GameObject BuildMarkVisual(EnemyHealth enemy)
        {
            var root = new GameObject("DeathMark");
            root.transform.SetParent(enemy.transform, false);
            root.transform.localPosition = Vector3.up * (enemy.BarHeight + 0.25f);
            for (int k = 0; k < 6; k++)
            {
                float a = k * Mathf.PI * 2f / 6f;
                GameObject bead = RuntimePrimitives.Create(PrimitiveType.Sphere, root.transform, MarkColor);
                bead.transform.localScale = Vector3.one * 0.13f;
                bead.transform.localPosition = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.38f;
            }
            GameObject core = RuntimePrimitives.Create(PrimitiveType.Sphere, root.transform, new Color(0.2f, 0.5f, 0.2f));
            core.transform.localScale = new Vector3(0.22f, 0.3f, 0.22f);
            root.AddComponent<Spin>();
            return root;
        }

        private class Spin : MonoBehaviour
        {
            private void Update()
            {
                transform.Rotate(0f, 120f * Time.deltaTime, 0f, Space.Self);
                if (Marked == null)
                    Destroy(gameObject);
            }
        }

        // ------------------------------------------------------------------ life

        private void Start()
        {
            if (Kind == MinionKind.Effigy) return;
            attack.StrikeFrame += OnStrike;
            float a = Random.value * Mathf.PI * 2f;
            // Mages keep a step further back than the melee minions.
            slot = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (Kind == MinionKind.Mage ? FollowRadius + 1f : FollowRadius);
        }

        private void OnDestroy()
        {
            All.Remove(this);
            if (attack != null)
                attack.StrikeFrame -= OnStrike;
        }

        /// <summary>Falls apart (destroyed, time's up, the owner died, or a fresh one took its place).</summary>
        public void Crumble(bool combatOrExpiry = false)
        {
            if (dead)
                return;
            dead = true;
            if (combatOrExpiry && owner != null && tree != null && tree.Get(WeaponSkillEffect.MinionDeathBurst) > 0)
            {
                float radius = 3f * tree.AreaMultiplier;
                float amount = MaxLife * tree.Get(WeaponSkillEffect.MinionDeathBurst) / 100f;
                foreach (EnemyHealth e in EnemyHealth.Active.ToArray())
                    if (e != null && !e.IsDead && Flat(e.transform.position - transform.position).magnitude <= radius)
                        e.TakeDamage(amount, DamageType.Physical, 0f, 0f, hitOrigin: transform.position);
                SkillEffects.Shockwave(transform.position, radius, new Color(0.6f, 1f, 0.65f), 0.4f);
            }
            All.Remove(this);
            Color dust = Kind == MinionKind.Wolf ? new Color(0.6f, 0.9f, 1f) : new Color(0.85f, 0.83f, 0.75f);
            SkillEffects.Shockwave(transform.position, Kind == MinionKind.Golem ? 2f : 1.2f, dust, 0.35f);
            Destroy(gameObject);
        }

        private void ApplySpeed(int horde)
        {
            float bonus = hordeSpeed * horde;
            speed = baseSpeed * Mathf.Max(0.2f, 1f + (moveIncrease + bonus) / 100f);
            float attackHaste = Mathf.Max(0.2f, 1f + (attackIncrease + (UsesProjectileSpell(Kind) ? 0f : bonus)) / 100f);
            if (Kind == MinionKind.Mage && Time.time < mageRallyUntil)
                attackHaste *= 1f + tree.Get(WeaponSkillEffect.MageCastSpeedAfterSummon) / 100f;
            attackCooldown = baseCooldown / attackHaste;
            attack.PlaybackSpeed = attackHaste;
        }

        /// <summary>The owner's living minions that Marching Horde counts (effigies are not summoned servants).</summary>
        private static int HordeCount(Transform owner)
        {
            int count = 0;
            foreach (Minion minion in All)
                if (minion != null && minion.owner == owner && minion.Kind != MinionKind.Effigy && !minion.dead)
                    count++;
            return count;
        }

        private void Update()
        {
            if (Kind == MinionKind.Effigy) return;
            if (hordeSpeed > 0f || (Kind == MinionKind.Mage && tree.Get(WeaponSkillEffect.MageCastSpeedAfterSummon) > 0f))
                ApplySpeed(hordeSpeed > 0f ? HordeCount(owner) : 0);
            Player.PlayerStats ownerStats = owner != null ? owner.GetComponent<Player.PlayerStats>() : null;
            if (owner == null || Time.time >= expiresAt || (ownerStats != null && ownerStats.IsDead))
            {
                Crumble(owner != null && ownerStats != null && !ownerStats.IsDead && Time.time >= expiresAt);
                return;
            }

            Vector3 toOwner = Flat(owner.position - transform.position);
            if (Time.time >= teleportRetryAt && toOwner.magnitude > (tether ? TetherTeleportDistance : TeleportDistance) &&
                (target == null || Flat(target.transform.position - owner.position).magnitude > MarkLeash))
            {
                if (TeleportToOwner()) return;
                teleportRetryAt = Time.time + 0.5f;
            }

            if (Time.time >= retargetAt || (target != null && target.IsDead))
            {
                retargetAt = Time.time + 0.3f;
                target = PickTarget();
            }

            Vector3 move = Vector3.zero;
            Vector3 face = Vector3.zero;
            if (target != null)
            {
                Vector3 toTarget = Flat(target.transform.position - transform.position);
                face = toTarget;
                float reach = Reach(target);
                // A mage closes to a little inside its range; the rest walk up to swing.
                if (toTarget.magnitude > reach * 0.9f)
                {
                    if (!attack.IsAttacking)
                        move = toTarget.normalized;
                }
                else if (cooldown <= 0f && !attack.IsAttacking)
                {
                    cooldown = attackCooldown;
                    striking = target;
                    PlaySwing();
                }
            }
            else
            {
                Vector3 toSlot = Flat(owner.position + slot - transform.position);
                float distance = toSlot.magnitude;
                if (distance > FollowStopDistance)
                {
                    // Bone Tether: the further behind, the faster (up to twice the pace).
                    float catchUp = tether ? Mathf.Lerp(1f, 2f, Mathf.InverseLerp(2f, 7f, distance)) : 1f;
                    // Ease into the slot instead of alternating full speed and a dead stop when
                    // the owner walks more slowly than us. Cap the step on long frames too.
                    float arrivalSpeed = (distance - FollowStopDistance) / Mathf.Max(FollowArrivalTime, Time.deltaTime);
                    move = toSlot / distance * (Mathf.Min(speed * catchUp, arrivalSpeed) / speed);
                    face = toSlot;
                }
            }

            Life = Mathf.Min(MaxLife, Life + MaxLife * tree.Get(WeaponSkillEffect.MinionRegen) / 100f * Time.deltaTime);
            cooldown -= Time.deltaTime;
            if (face.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), 10f * Time.deltaTime);

            if (body.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;
            verticalVelocity -= 20f * Time.deltaTime;
            Vector3 velocity = move * speed;
            velocity.y = verticalVelocity;
            Vector3 from = transform.position;
            Vector3 destination = PoeClone.World.GroundObstacleMotion.ClampToArea(body, from, from + velocity * Time.deltaTime);
            body.Move(destination - from);
        }

        private float Reach(EnemyHealth enemy)
        {
            return attackRange + 0.4f * enemy.transform.localScale.x;
        }

        private void PlaySwing()
        {
            switch (Kind)
            {
                case MinionKind.Wolf:
                    attack.PlayCreatureAttack(false);
                    break;
                case MinionKind.Mage:
                    attack.PlayStaffBolt();
                    break;
                case MinionKind.Golem:
                    attack.PlayClawAttack();
                    break;
                default:
                    attack.PlayAttack(WeaponType.Sword);
                    break;
            }
        }

        // The marked enemy if it isn't too far from the owner; else the nearest living enemy close
        // to the minion that isn't too far from its owner.
        private EnemyHealth PickTarget()
        {
            EnemyHealth mark = Marked;
            if (mark != null && Flat(mark.transform.position - owner.position).magnitude <= MarkLeash)
                return mark;

            EnemyHealth best = null;
            EnemyHealth training = null;
            float engageRadius = (Kind == MinionKind.Mage ? MageEngageRadius : EngageRadius) * commandRange;
            float bestDistance = engageRadius;
            float trainingDistance = engageRadius;
            foreach (EnemyHealth e in EnemyHealth.Active)
            {
                if (e == null || e.IsDead)
                    continue;
                if (Flat(e.transform.position - owner.position).magnitude > Leash * commandRange)
                    continue;
                float d = Flat(e.transform.position - transform.position).magnitude;
                // Practise automatically while idle, but never abandon an arena enemy for the doll.
                if (e.IsTrainingDummy)
                {
                    if (d < trainingDistance)
                    {
                        trainingDistance = d;
                        training = e;
                    }
                    continue;
                }
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = e;
                }
            }
            return best != null ? best : training;
        }

        private void OnStrike()
        {
            EnemyHealth hit = striking;
            striking = null;
            if (hit == null || hit.IsDead || dead)
                return;

            if (Kind == MinionKind.Mage)
            {
                // Like the player's volleys: extra bolts fan out round the aim, and each enemy is
                // hurt by the first bolt of the volley to reach it only.
                var volley = new HashSet<EnemyHealth>();
                Vector3 origin = EnemyCombat.BoltOrigin(transform);
                Vector3 aim = hit.transform.position + Vector3.up * 0.4f * hit.transform.localScale.y - origin;
                Vector3[] fan = HitEffects.Spread(aim, 1 + tree.ExtraProjectiles + extraProjectiles, PlayerCombat.ArrowSpreadDegrees);
                for (int i = 0; i < fan.Length; i++)
                    MinionBolt.Launch(this, origin, (fan[i] * Flat(aim).magnitude + Vector3.up * aim.y).normalized, volley);

                return;
            }

            if (Flat(hit.transform.position - transform.position).magnitude > Reach(hit) * 1.2f)
                return;

            if (Kind == MinionKind.Golem)
            {
                if (tree.Get(WeaponSkillEffect.GolemAftershock) > 0) StartCoroutine(DelayedSlam(hit.transform.position));
                // A two-fisted slam: the target and whatever stands right by it.
                Vector3 at = hit.transform.position;
                SkillEffects.Shockwave(at, 2f * tree.AreaMultiplier, new Color(0.85f, 0.8f, 0.65f), 0.3f);
                foreach (EnemyHealth e in EnemyHealth.Active.ToArray())
                {
                    if (e != null && !e.IsDead && (e == hit || Flat(e.transform.position - at).magnitude <= 2f * tree.AreaMultiplier))
                        Deal(e, e == hit ? 1f : 0.6f);
                }
                return;
            }
            Deal(hit, 1f);
        }

        private System.Collections.IEnumerator DelayedSlam(Vector3 at)
        {
            yield return new WaitForSeconds(0.6f);
            if (dead || owner == null) yield break;
            SkillEffects.Shockwave(at, 2f * tree.AreaMultiplier, new Color(0.85f, 0.8f, 0.65f), 0.3f);
            foreach (var enemy in EnemyHealth.Active.ToArray())
                if (enemy != null && !enemy.IsDead && Flat(enemy.transform.position - at).magnitude <= 2f * tree.AreaMultiplier) Deal(enemy, 0.4f, at);
        }

        /// <summary>One of this minion's hits landing (the mage's bolts call this when they arrive).</summary>
        public void Deal(EnemyHealth enemy, float share, Vector3? hitOrigin = null)
        {
            if (enemy == null || enemy.IsDead)
                return;
            float amount = damage * share * Random.Range(0.85f, 1.15f);
            if (Kind == MinionKind.Wolf && tree.Get(WeaponSkillEffect.PackHunt) > 0)
            {
                int nearby = 0;
                foreach (var wolf in All)
                    if (wolf != null && wolf != this && !wolf.dead && wolf.owner == owner && wolf.Kind == MinionKind.Wolf &&
                        Flat(wolf.transform.position - transform.position).sqrMagnitude <= 36f) nearby++;
                // Add conditional increases to the same sum used for the minion's ordinary damage.
                var sheet = owner != null ? owner.GetComponent<PlayerInventory>()?.Stats : null;
                float increased = Stat(sheet, StatType.MinionDamage) + Stat(sheet, StatType.Intelligence) * 0.5f + tree.Get(WeaponSkillEffect.Damage);
                amount *= (100f + increased + Mathf.Min(4, nearby) * tree.Get(WeaponSkillEffect.PackHunt)) / Mathf.Max(1f, 100f + increased);
            }
            if (enemy == Marked)
                amount *= MarkMultiplier(owner);
            if (enemy.IsShocked)
                amount *= HitEffects.ShockedMore;
            amount *= Curse.TakenMultiplier(enemy);
            PlayerInventory ownerInventory = owner != null ? owner.GetComponent<PlayerInventory>() : null;
            StatSheet ownerSheet = ownerInventory != null ? ownerInventory.Stats : null;
            var ai = enemy.GetComponent<EnemyController>();
            bool chilled = ai != null && ai.IsChilled;
            DamageType element = WeaponSkillHit.Element(tree, DamageType.Physical);
            float dealt = enemy.TakeDamage(amount, element, 0f, 0f, hitOrigin: hitOrigin ?? transform.position);
            WeaponSkillHit.Apply(owner, enemy, dealt, tree, element, chilled, minion: true);
            if (dealt <= 0f)
                return;
            if (Kind == MinionKind.Viper && owner != null)
            {
                WeaponVenom.Apply(owner, enemy, dealt, 45f, 0f, ownerSheet, minionDamage: true, durationMultiplier: tree?.DurationMultiplier ?? 1f, radiusMultiplier: tree?.AreaMultiplier ?? 1f, tree: tree);
            }
            CombatText.Show(enemy.transform.position + Vector3.up * 1.6f * enemy.transform.localScale.y,
                Mathf.Max(1, Mathf.RoundToInt(dealt)).ToString(), new Color(0.6f, 1f, 0.65f), 0.85f);

            if (soulBond > 0f && owner != null)
            {
                Player.PlayerStats stats = owner.GetComponent<Player.PlayerStats>();
                if (stats != null && !stats.IsDead)
                    stats.Heal(dealt * soulBond / 100f);
            }
        }

        // A small green bar over a minion that has been hurt (like the enemies' red ones).
        private static Texture2D pixel;

        private void OnGUI()
        {
            if (dead || Time.time - damagedAt > 6f || Event.current.type != EventType.Repaint)
                return;
            var session = PoeClone.Network.GameSessionController.Instance;
            if (session != null && session.Role == PoeClone.Network.SessionRole.Spectator &&
                PoeClone.Inventory.SpectatorMirror.Shown(PoeClone.Inventory.SpectatorMirror.Menu.Inventory))
                return;
            Camera cam = Camera.main;
            if (cam == null)
                return;
            float height = Kind == MinionKind.Wolf ? 1.2f : 2.3f;
            Vector3 screen = cam.WorldToScreenPoint(transform.position + Vector3.up * height * transform.localScale.y);
            if (screen.z <= 0f)
                return;
            if (pixel == null)
            {
                pixel = new Texture2D(1, 1);
                pixel.SetPixel(0, 0, Color.white);
                pixel.Apply();
            }
            float scale = TouchMode.GuiScale;
            float w = 44f * scale, h = 5f * scale;
            var rect = new Rect(screen.x - w * 0.5f, Screen.height - screen.y, w, h);
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(rect, pixel);
            GUI.color = new Color(0.3f, 0.85f, 0.35f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(Life / MaxLife), rect.height), pixel);
            GUI.color = previous;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        /// <summary>
        /// A skeleton mage's bolt: a pulsing violet soul with motes circling its flight line and a
        /// fading tail. Flies straight and stops on the first enemy in its path.
        /// </summary>
        private class MinionBolt : MonoBehaviour
        {
            private Minion caster;
            private Vector3 launchPosition;
            private Vector3 direction;
            private float travelLeft;
            private HashSet<EnemyHealth> volley;
            private const float Speed = 21f;

            private static readonly Color Core = new Color(0.78f, 0.42f, 1f);
            private static readonly Color Mote = new Color(0.93f, 0.8f, 1f);
            private static readonly Color TailHead = new Color(0.6f, 0.22f, 0.95f);
            private static readonly Color TailEnd = new Color(0.25f, 0.06f, 0.42f);
            private const int TailLength = 8;
            private const float TailSpacing = 0.015f; // seconds between tail samples

            private Transform core, orbit;
            private readonly Transform[] tail = new Transform[TailLength];
            private readonly Vector3[] trail = new Vector3[TailLength];
            private float age, nextSample;

            public static void Launch(Minion caster, Vector3 origin, Vector3 direction, HashSet<EnemyHealth> volley)
            {
                var root = new GameObject("Minion Bolt");
                root.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction));
                var bolt = root.AddComponent<MinionBolt>();
                bolt.core = RuntimePrimitives.Create(PrimitiveType.Sphere, root.transform, Core).transform;
                // Stretched along the flight, so it reads as speed rather than a ball.
                GameObject sheath = RuntimePrimitives.Create(PrimitiveType.Sphere, root.transform, TailHead);
                sheath.transform.localPosition = new Vector3(0f, 0f, -0.12f);
                sheath.transform.localScale = new Vector3(0.2f, 0.2f, 0.42f);
                bolt.orbit = new GameObject("Motes").transform;
                bolt.orbit.SetParent(root.transform, false);
                for (int i = 0; i < 3; i++)
                {
                    float a = i * Mathf.PI * 2f / 3f;
                    GameObject mote = RuntimePrimitives.Create(PrimitiveType.Sphere, bolt.orbit, Mote);
                    mote.transform.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a), -0.15f * i) * 0.2f;
                    mote.transform.localScale = Vector3.one * 0.075f;
                }
                for (int i = 0; i < TailLength; i++)
                {
                    float t = i / (float)(TailLength - 1);
                    GameObject puff = RuntimePrimitives.Create(PrimitiveType.Sphere, root.transform, Color.Lerp(TailHead, TailEnd, t));
                    bolt.tail[i] = puff.transform;
                    bolt.trail[i] = origin;
                }
                bolt.caster = caster;
                bolt.launchPosition = origin;
                bolt.direction = direction;
                bolt.volley = volley;
                bolt.travelLeft = caster.attackRange + 1f;
                bolt.Animate();
            }

            private void Update()
            {
                if (caster == null || travelLeft <= 0f)
                {
                    Burst(transform.position, 0.45f);
                    return;
                }
                float step = Mathf.Min(travelLeft, Speed * caster.tree.ProjectileSpeedMultiplier * Time.deltaTime);
                travelLeft -= step;
                if (Graveward.Stops(transform.position, transform.position + direction * step, out Vector3 warded))
                {
                    Burst(warded, 0.5f);
                    return;
                }
                EnemyHealth hit = null;
                float nearest = float.MaxValue;
                foreach (EnemyHealth enemy in EnemyHealth.Active)
                {
                    if (enemy == null || enemy.IsDead)
                        continue;
                    Vector3 aim = enemy.transform.position + Vector3.up * 0.4f * enemy.transform.localScale.y;
                    float along = Mathf.Clamp(Vector3.Dot(aim - transform.position, direction), 0f, step);
                    float radius = 0.3f + 0.4f * enemy.transform.localScale.x;
                    if ((aim - (transform.position + direction * along)).sqrMagnitude <= radius * radius && along < nearest)
                    {
                        hit = enemy;
                        nearest = along;
                    }
                }
                if (hit != null)
                {
                    Vector3 at = transform.position + direction * nearest;
                    // A later bolt of the same volley stops on the enemy but does no damage.
                    if (volley.Add(hit))
                        caster.Deal(hit, 1f, launchPosition);
                    Burst(at, 0.8f);
                    return;
                }
                transform.position += direction * step;
                age += Time.deltaTime;
                Animate();
            }

            // The core throbs, the motes wheel round the flight line, and the tail trails the path
            // flown, each puff smaller and darker than the one before.
            private void Animate()
            {
                core.localScale = Vector3.one * (0.27f + 0.05f * Mathf.Sin(age * 38f));
                orbit.localRotation = Quaternion.Euler(0f, 0f, age * 720f);
                if (age >= nextSample)
                {
                    nextSample = age + TailSpacing;
                    for (int i = TailLength - 1; i > 0; i--)
                        trail[i] = trail[i - 1];
                }
                trail[0] = transform.position;
                for (int i = 0; i < TailLength; i++)
                {
                    float t = (i + 1) / (float)(TailLength + 1);
                    tail[i].position = trail[i] - direction * 0.18f;
                    tail[i].localScale = Vector3.one * (0.2f * (1f - t) * (0.85f + 0.15f * Mathf.Sin(age * 30f + i)));
                }
            }

            private void Burst(Vector3 at, float radius)
            {
                SkillEffects.Shockwave(at, radius, Core, 0.25f);
                SkillEffects.Blast(at, radius * 0.5f, TailHead, 0.2f);
                Destroy(gameObject);
            }
        }
    }
}
