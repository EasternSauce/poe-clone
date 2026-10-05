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
        Viper
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
            markOwner = null;
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
        private const float EngageRadius = 8f;
        private const float Leash = 9f;             // only enemies this close to the owner are fought...
        private const float MarkLeash = 16f;        // ...or the marked one, a good deal further off
        private const float TeleportDistance = 10f; // about half the screen: past this it blinks back to the owner

        private Transform owner;
        private CharacterController body;
        private CharacterAttackAnimator attack;
        private float damage;
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
        private float damagedAt = float.NegativeInfinity;
        private bool dead;
        private float soulBond;

        // ------------------------------------------------------------------ numbers

        private struct Profile
        {
            public float Life;          // at level 1
            public float Damage;        // per hit at level 1
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
                    return new Profile { Life = 41f, Damage = 2.5f, Cooldown = 1.6f, Range = 8.5f, Speed = 6f, Look = "Skeleton Mage" };
                case MinionKind.Wolf:
                    return new Profile { Life = 88f, Damage = 2.75f, Cooldown = 0.8f, Range = 2f, Speed = 9f, Look = "Spirit Wolf" };
                case MinionKind.Golem:
                    return new Profile { Life = 248f, Damage = 4.5f, Cooldown = 1.5f, Range = 2.6f, Speed = 5.5f, Look = "Bone Golem" };
                case MinionKind.Viper:
                    return new Profile { Life = 93f, Damage = 3.5f, Cooldown = 1.15f, Range = 2.2f, Speed = 8f, Look = "Giant Spider" };
                default:
                    return new Profile { Life = 70f, Damage = 2.5f, Cooldown = 1.1f, Range = 2.1f, Speed = 6.5f, Look = "Skeleton Warrior" };
            }
        }

        /// <summary>Life at a summon level, before Minion Life: 5% more each level.</summary>
        public static float LifeAt(MinionKind kind, int level) => ProfileOf(kind).Life * Mathf.Pow(1.05f, Mathf.Max(1, level) - 1);

        /// <summary>Damage per hit at a summon level, before Minion Damage: about 16% more each level.</summary>
        public static float DamageAt(MinionKind kind, int level) => ProfileOf(kind).Damage * Mathf.Pow(1.16f, Mathf.Max(1, level) - 1);

        /// <summary>A soft per-kind cap; the owner's global minion limit is the real army limit.</summary>
        public static int KindCap(MinionKind kind, StatSheet sheet)
        {
            int extra = sheet != null ? Mathf.RoundToInt(Mathf.Max(0f, sheet.Total(StatType.AdditionalSkeletons))) : 0;
            switch (kind)
            {
                case MinionKind.Warrior: return 4 + extra;
                case MinionKind.Mage: return 3 + extra;
                case MinionKind.Wolf: return 3;
                default: return 1;
            }
        }

        /// <summary>How long a temporary minion lasts (0: until destroyed).</summary>
        public static float Duration(MinionKind kind, int level, StatSheet sheet)
        {
            float more = 1f + (sheet != null ? Mathf.Max(0f, sheet.Total(StatType.MinionDuration)) : 0f) / 100f;
            switch (kind)
            {
                case MinionKind.Wolf: return (12f + 0.5f * (level - 1)) * more;
                case MinionKind.Golem: return (18f + 1f * (level - 1)) * more;
                case MinionKind.Viper: return (16f + 0.5f * (level - 1)) * more;
                default: return 0f;
            }
        }

        private static float Stat(StatSheet sheet, StatType stat) => sheet != null ? sheet.Total(stat) : 0f;

        /// <summary>Life with the owner's Minion Life, for the skills panel.</summary>
        public static float LifeFor(MinionKind kind, int level, StatSheet sheet) =>
            LifeAt(kind, level) * (1f + Mathf.Max(-50f, Stat(sheet, StatType.MinionLife)) / 100f);

        /// <summary>Damage per hit with the owner's Minion Damage, for the skills panel.</summary>
        public static float DamageFor(MinionKind kind, int level, StatSheet sheet) =>
            DamageAt(kind, level) * (1f + Mathf.Max(0f, Stat(sheet, StatType.Intelligence)) * 0.005f) *
            (1f + Mathf.Max(-50f, Stat(sheet, StatType.MinionDamage)) / 100f);

        /// <summary>The shared living-minion limit. Capacity comes from the tree and gear, not per-skill caps.</summary>
        public const int BaseGlobalCap = 2;

        public static int GlobalCap(Transform owner)
        {
            PlayerInventory inventory = owner != null ? owner.GetComponent<PlayerInventory>() : null;
            StatSheet sheet = inventory != null ? inventory.Stats : null;
            return Mathf.Max(1, BaseGlobalCap + (sheet != null ? Mathf.RoundToInt(sheet.Total(StatType.AdditionalMinions)) : 0));
        }

        public static float ArmourFor(StatSheet sheet) =>
            Mathf.Max(0f, BaseArmour + Stat(sheet, StatType.MinionArmour));

        public static float ResistanceFor(StatSheet sheet) =>
            Mathf.Min(StatSheet.ResistanceCap, BaseResistance + Stat(sheet, StatType.MinionResistances));

        /// <summary>Damage after the minion's own armour or resistance and Bone Armour.</summary>
        public static float DamageTaken(float amount, DamageType type, StatSheet sheet)
        {
            if (type == DamageType.Physical)
                amount *= 1f - DefenceMath.ArmourReduction(ArmourFor(sheet), amount);
            else
                amount = DefenceMath.AfterResistance(amount, ResistanceFor(sheet));
            return amount * (1f - Mathf.Clamp(Stat(sheet, StatType.BoneArmour), 0f, 60f) / 100f);
        }

        // ------------------------------------------------------------------ summoning

        /// <summary>
        /// Raises minions toward a chosen point, at most six metres from the owner.
        /// Warriors and mages add to their type's limit
        /// until the shared army limit is reached; then the oldest minion is replaced.
        /// Wolves, the golem and viper replace their previous summon when recast.
        /// </summary>
        public static void Summon(Transform owner, MinionKind kind, int level, Vector3 target)
        {
            EnemySpawner spawner = FindAnyObjectByType<EnemySpawner>();
            if (spawner == null || spawner.EnemyPrefab == null || owner == null)
                return;

            PlayerInventory inventory = owner.GetComponent<PlayerInventory>();
            StatSheet sheet = inventory != null ? inventory.Stats : null;
            int cap = KindCap(kind, sheet);
            int globalCap = GlobalCap(owner);

            All.RemoveAll(m => m == null);
            if (kind == MinionKind.Wolf || kind == MinionKind.Golem || kind == MinionKind.Viper)
            {
                foreach (Minion old in All.FindAll(m => m.owner == owner && m.Kind == kind))
                    old.Crumble();
            }

            int count = kind == MinionKind.Warrior ? 2 : kind == MinionKind.Wolf ? cap : 1;
            count = Mathf.Min(count, cap);
            for (int k = 0; k < count; k++)
            {
                List<Minion> same = All.FindAll(m => m.owner == owner && m.Kind == kind && !m.dead);
                if (same.Count >= cap)
                {
                    Oldest(same).Crumble();
                }
                List<Minion> owned = All.FindAll(m => m.owner == owner && !m.dead);
                if (owned.Count >= globalCap)
                    Oldest(owned).Crumble();

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

        private static Minion Oldest(List<Minion> minions)
        {
            Minion oldest = minions[0];
            foreach (Minion minion in minions)
                if (minion.Id < oldest.Id) oldest = minion;
            return oldest;
        }

        /// <summary>Dismiss this owner's living summons, for example when their summoning weapon is removed.</summary>
        public static void Desummon(Transform owner)
        {
            if (owner == null)
                return;
            foreach (Minion minion in All.FindAll(m => m != null && m.owner == owner))
                minion.Crumble();
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
            minion.Kind = kind;
            minion.LookIndex = look;
            minion.Id = ++nextId;
            minion.body = go.GetComponent<CharacterController>();
            minion.attack = model.GetComponent<CharacterAttackAnimator>() ?? model.gameObject.AddComponent<CharacterAttackAnimator>();

            float haste = 1f + Mathf.Clamp(Stat(sheet, StatType.MinionSpeed), -50f, 100f) / 100f;
            float attackHaste = haste * (kind == MinionKind.Mage
                ? 1f + Mathf.Min(0.3f, 0.02f * (Mathf.Max(1, level) - 1)) : 1f);
            minion.MaxLife = Mathf.Max(1f, LifeFor(kind, level, sheet));
            minion.Life = minion.MaxLife;
            minion.damage = DamageFor(kind, level, sheet);
            minion.attackCooldown = profile.Cooldown / attackHaste;
            minion.attackRange = profile.Range;
            minion.speed = profile.Speed * haste;
            minion.attack.PlaybackSpeed = attackHaste;
            minion.soulBond = Mathf.Max(0f, Stat(sheet, StatType.SoulBond));
            float duration = Duration(kind, level, sheet);
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

        /// <summary>An enemy's blow (or bolt) lands on this minion.</summary>
        public void TakeHit(float amount, DamageType type)
        {
            if (dead || amount <= 0f)
                return;
            PlayerInventory inventory = owner != null ? owner.GetComponent<PlayerInventory>() : null;
            amount = DamageTaken(amount, type, inventory != null ? inventory.Stats : null);
            Life -= amount;
            damagedAt = Time.time;
            Color color = type == DamageType.Physical ? new Color(1f, 0.55f, 0.45f) : CombatText.ColorFor(type);
            CombatText.Show(transform.position + Vector3.up * 1.4f * transform.localScale.y,
                Mathf.Max(1, Mathf.RoundToInt(amount)).ToString(), color, 0.7f);
            if (Life <= 0f)
                Crumble();
        }

        // ------------------------------------------------------------------ Death Mark

        private static EnemyHealth marked;
        private static Transform markOwner;
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
        public static void Mark(EnemyHealth enemy, Transform owner)
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
            markOwner = owner;
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

        // Death's Herald: a marked enemy that dies bursts for a fifth of its life round it, and the
        // mark leaps on to the nearest enemy.
        private static void OnMarkedDied()
        {
            EnemyHealth dying = marked;
            Transform owner = markOwner;
            Unmark();
            if (dying == null || owner == null)
                return;
            PlayerInventory inventory = owner.GetComponent<PlayerInventory>();
            if (Stat(inventory != null ? inventory.Stats : null, StatType.DeathsHerald) <= 0f)
                return;

            Vector3 at = dying.transform.position;
            SkillEffects.Shockwave(at, 3.5f, MarkColor, 0.45f);
            float burst = dying.MaxHealth * 0.2f;
            EnemyHealth next = null;
            float nextDistance = 8f;
            foreach (EnemyHealth e in EnemyHealth.Active.ToArray())
            {
                if (e == null || e.IsDead || e == dying)
                    continue;
                float d = Flat(e.transform.position - at).magnitude;
                if (d <= 3.5f)
                {
                    e.TakeDamage(burst);
                    CombatText.Show(e.transform.position + Vector3.up * 1.6f * e.transform.localScale.y,
                        Mathf.Max(1, Mathf.RoundToInt(burst)).ToString(), MarkColor, 0.9f);
                }
                if (!e.IsDead && d < nextDistance)
                {
                    nextDistance = d;
                    next = e;
                }
            }
            if (next != null)
                Mark(next, owner);
        }

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
        public void Crumble()
        {
            if (dead)
                return;
            dead = true;
            All.Remove(this);
            Color dust = Kind == MinionKind.Wolf ? new Color(0.6f, 0.9f, 1f) : new Color(0.85f, 0.83f, 0.75f);
            SkillEffects.Shockwave(transform.position, Kind == MinionKind.Golem ? 2f : 1.2f, dust, 0.35f);
            Destroy(gameObject);
        }

        private void Update()
        {
            Player.PlayerStats ownerStats = owner != null ? owner.GetComponent<Player.PlayerStats>() : null;
            if (owner == null || Time.time >= expiresAt || (ownerStats != null && ownerStats.IsDead))
            {
                Crumble();
                return;
            }

            Vector3 toOwner = Flat(owner.position - transform.position);
            if (toOwner.magnitude > TeleportDistance && (target == null || Flat(target.transform.position - owner.position).magnitude > MarkLeash))
            {
                SkillEffects.Shockwave(transform.position, 1f, new Color(0.5f, 1f, 0.6f), 0.3f);
                body.enabled = false;
                transform.position = owner.position + slot;
                body.enabled = true;
                target = null;
                SkillEffects.Shockwave(transform.position, 1f, new Color(0.5f, 1f, 0.6f), 0.3f);
                return;
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
                if (toSlot.magnitude > 0.6f)
                {
                    // Catching up: the further behind, the faster (up to twice the pace).
                    move = toSlot.normalized * Mathf.Lerp(1f, 2f, Mathf.InverseLerp(2f, 7f, toSlot.magnitude));
                    face = toSlot;
                }
            }

            cooldown -= Time.deltaTime;
            if (face.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), 10f * Time.deltaTime);

            if (body.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;
            verticalVelocity -= 20f * Time.deltaTime;
            Vector3 velocity = move * speed;
            velocity.y = verticalVelocity;
            body.Move(velocity * Time.deltaTime);
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
                    attack.PlayAttack(WeaponType.Unarmed);
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
            float bestDistance = Kind == MinionKind.Mage ? EngageRadius + 2f : EngageRadius;
            foreach (EnemyHealth e in EnemyHealth.Active)
            {
                if (e == null || e.IsDead)
                    continue;
                if (Flat(e.transform.position - owner.position).magnitude > Leash)
                    continue;
                float d = Flat(e.transform.position - transform.position).magnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = e;
                }
            }
            return best;
        }

        private void OnStrike()
        {
            EnemyHealth hit = striking;
            striking = null;
            if (hit == null || hit.IsDead || dead)
                return;

            if (Kind == MinionKind.Mage)
            {
                MinionBolt.Launch(this, hit);
                return;
            }

            if (Flat(hit.transform.position - transform.position).magnitude > Reach(hit) * 1.2f)
                return;

            if (Kind == MinionKind.Golem)
            {
                // A two-fisted slam: the target and whatever stands right by it.
                Vector3 at = hit.transform.position;
                SkillEffects.Shockwave(at, 2f, new Color(0.85f, 0.8f, 0.65f), 0.3f);
                foreach (EnemyHealth e in EnemyHealth.Active.ToArray())
                {
                    if (e != null && !e.IsDead && (e == hit || Flat(e.transform.position - at).magnitude <= 2f))
                        Deal(e, e == hit ? 1f : 0.6f);
                }
                return;
            }
            Deal(hit, 1f);
        }

        /// <summary>One of this minion's hits landing (the mage's bolts call this when they arrive).</summary>
        public void Deal(EnemyHealth enemy, float share)
        {
            if (enemy == null || enemy.IsDead)
                return;
            float amount = damage * share * Random.Range(0.85f, 1.15f);
            if (enemy == Marked)
                amount *= MarkMultiplier(owner);
            if (enemy.IsShocked)
                amount *= HitEffects.ShockedMore;
            amount *= Curse.TakenMultiplier(enemy);
            PlayerInventory ownerInventory = owner != null ? owner.GetComponent<PlayerInventory>() : null;
            StatSheet ownerSheet = ownerInventory != null ? ownerInventory.Stats : null;
            float lessDamage = Mathf.Clamp(Stat(ownerSheet, StatType.MinionDamagePenalty), 0f, 100f);
            amount *= 1f - lessDamage / 100f;
            float dealt = enemy.TakeDamage(amount, PoeClone.Combat.DamageType.Physical, 0f, 0f);
            if (dealt <= 0f)
                return;
            if (Kind == MinionKind.Viper && owner != null)
            {
                WeaponVenom.Apply(owner, enemy, dealt, 45f, 0f, ownerSheet);
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

        /// <summary>A skeleton mage's bolt: flies to its target and lands the mage's hit.</summary>
        private class MinionBolt : MonoBehaviour
        {
            private Minion caster;
            private EnemyHealth target;
            private float life = 2f;
            private const float Speed = 14f;

            public static void Launch(Minion caster, EnemyHealth target)
            {
                var root = new GameObject("Minion Bolt");
                root.transform.position = caster.transform.position + Vector3.up * 1.1f * caster.transform.localScale.y + caster.transform.forward * 0.5f;
                GameObject core = RuntimePrimitives.Create(PrimitiveType.Sphere, root.transform, new Color(0.5f, 0.85f, 1f));
                core.transform.localScale = Vector3.one * 0.26f;
                GameObject spark = RuntimePrimitives.Create(PrimitiveType.Sphere, root.transform, new Color(0.85f, 0.95f, 1f));
                spark.transform.localScale = Vector3.one * 0.12f;
                spark.transform.localPosition = new Vector3(0.16f, 0f, 0f);
                var bolt = root.AddComponent<MinionBolt>();
                bolt.caster = caster;
                bolt.target = target;
            }

            private void Update()
            {
                life -= Time.deltaTime;
                if (target == null || target.IsDead || life <= 0f)
                {
                    Destroy(gameObject);
                    return;
                }
                Vector3 aim = target.transform.position + Vector3.up * 0.4f * target.transform.localScale.y;
                Vector3 to = aim - transform.position;
                float step = Speed * Time.deltaTime;
                transform.Rotate(400f * Time.deltaTime, 280f * Time.deltaTime, 0f);
                if (to.magnitude <= step + 0.3f)
                {
                    if (caster != null)
                        caster.Deal(target, 1f);
                    SkillEffects.Shockwave(aim, 0.7f, new Color(0.5f, 0.85f, 1f), 0.2f);
                    Destroy(gameObject);
                    return;
                }
                transform.position += to.normalized * step;
            }
        }
    }
}
