using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using PoeClone.Combat;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.UI;
using PoeClone.Visuals;

namespace PoeClone.Skills
{
    /// <summary>
    /// Gear supplies skills. The equipped weapon owns its rank, XP and specialization points.
    /// Its main skill replaces left click; support abilities occupy action-bar slots.
    /// </summary>
    public partial class PlayerSkills : MonoBehaviour
    {
        private const string DashTowardsCursorKey = "PoeClone.DashTowardsCursor";
        public static bool DashTowardsCursor
        {
            get => PlayerPrefs.GetInt(DashTowardsCursorKey, 1) != 0;
            set { PlayerPrefs.SetInt(DashTowardsCursorKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        private static readonly string[] SlotLabels = { "Q", "E", "R", "F", "1", "2", "3", "4", "RMB", "M4", "M5" };
        private static readonly Key[] SlotKeys = { Key.Q, Key.E, Key.R, Key.F, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4 };

        private readonly SkillBindings bindings = new SkillBindings(SkillBook.SlotCount);
        private readonly Dictionary<SkillId, float> readyAt = new Dictionary<SkillId, float>();
        private readonly Dictionary<SkillId, float> cooldownOf = new Dictionary<SkillId, float>();
        // Roomy: in a cluttered spot (Haven's plaza, a ruin) the scenery alone can fill a small
        // buffer and push the enemies out of it.
        private readonly Collider[] buffer = new Collider[256];

        private PlayerStats stats;
        private PlayerController controller;
        private PlayerInventory inventory;
        private CharacterAttackAnimator attackAnimator;
        private EquipmentSet boundEquipment;
        private ItemData boundMainHand;

        /// <summary>Slots, gear-granted skills or their levels changed (the bars redraw).</summary>
        public event Action Changed;

        /// <summary>
        /// One use of a skill, as spectators need it to draw the same effect (see
        /// <see cref="PlayVisual"/>): where, which way, how big, and Chain Lightning's arc.
        /// </summary>
        public struct CastRecord
        {
            public int Number;
            public SkillId Skill;
            public int Level;
            public Vector3 At;
            public Vector3 Facing;
            public float Size;
            public int Count;
            public Vector3[] Points;
            public float Time;
        }

        private const int RecentCastCount = 8;
        private readonly CastRecord[] recentCasts = new CastRecord[RecentCastCount];

        /// <summary>Casts made so far (counts up; the newest has this number).</summary>
        public int CastCount { get; private set; }

        /// <summary>The latest casts, oldest first, made within the last <paramref name="seconds"/>.</summary>
        public List<CastRecord> RecentCasts(float seconds)
        {
            var list = new List<CastRecord>();
            for (int n = Mathf.Max(1, CastCount - RecentCastCount + 1); n <= CastCount; n++)
            {
                CastRecord r = recentCasts[n % RecentCastCount];
                if (r.Number == n && Time.time - r.Time <= seconds)
                    list.Add(r);
            }
            return list;
        }

        // Not skills, but replayed for spectators through the same log (see HitEffects.Burst).
        public const SkillId CorpseExplosionCast = (SkillId)(-1);
        public const SkillId ShatterCast = (SkillId)(-2);

        /// <summary>Logs a kill burst (a corpse explosion or shatter) where it went off.</summary>
        public void RecordBurst(SkillId burst, Vector3 at, float radius)
        {
            CastCount++;
            recentCasts[CastCount % RecentCastCount] = new CastRecord
            {
                Number = CastCount,
                Skill = burst,
                At = at,
                Facing = transform.forward,
                Size = radius,
                Time = Time.time
            };
        }

        private void Record(SkillDefinition skill, int level, float size = 0f, int count = 0, Vector3[] points = null, Vector3? at = null)
        {
            CastCount++;
            recentCasts[CastCount % RecentCastCount] = new CastRecord
            {
                Number = CastCount,
                Skill = skill.Id,
                Level = level,
                At = at ?? transform.position,
                Facing = transform.forward,
                Size = size,
                Count = count,
                Points = points,
                Time = Time.time
            };
        }

        public static string KeyLabel(int slot)
        {
            return slot >= 0 && slot < SlotLabels.Length ? SlotLabels[slot] : "?";
        }

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            controller = GetComponent<PlayerController>();
            inventory = GetComponent<PlayerInventory>();
            attackAnimator = GetComponentInChildren<CharacterAttackAnimator>();
            if (stats != null)
                stats.Died += OnPlayerDied;
        }

        private void Start()
        {
            if (inventory != null)
            {
                boundEquipment = inventory.Equipment;
                boundMainHand = boundEquipment.Get(EquipSlot.MainHand);
                boundEquipment.Changed += OnGearChanged;
                inventory.StatsChanged += OnStatsChanged;
            }
            FillEmptySlots(announce: false);
        }

        private void OnDestroy()
        {
            if (stats != null)
                stats.Died -= OnPlayerDied;
            if (boundEquipment != null)
                boundEquipment.Changed -= OnGearChanged;
            if (inventory != null)
                inventory.StatsChanged -= OnStatsChanged;
        }

        private void OnDisable()
        {
            EndWhirlwind();
        }

        private void OnPlayerDied()
        {
            CancelActions();
        }

        /// <summary>Cancel pending casts and committed attacks when leaving an area or dying.</summary>
        public void CancelActions()
        {
            EndWhirlwind();
            StopAllCoroutines();
            if (controller != null)
                controller.SetSkillCommit(false);
            if (attackAnimator != null)
                attackAnimator.CancelAttack();
        }

        private void OnGearChanged(EquipSlot slot, ItemData item)
        {
            if (slot == EquipSlot.MainHand || slot == EquipSlot.OffHand)
            {
                CancelActions();
                // Summons are built for their current summoner loadout. A weapon swap must not
                // let the player keep those minions while changing to a different damage setup.
                if (boundEquipment != null && (slot == EquipSlot.OffHand || boundMainHand != item))
                    Minion.Desummon(transform);
            }
            if (slot == EquipSlot.MainHand)
            {
                boundMainHand = item;
            }
            if (item == null)
                ClearUnavailableSlots();
            else
                FillEmptySlots(announce: true);
        }

        private void OnStatsChanged()
        {
            Changed?.Invoke();
        }

        /// <summary>Cast speed, including temporary Onslaught.</summary>
        public float CastRateMultiplier => (inventory != null && inventory.Stats != null ? inventory.Stats.CastRateMultiplier : 1f) *
            (controller != null && controller.HasOnslaught ? PlayerController.OnslaughtMore : 1f);

        /// <summary>Attack speed, including temporary Onslaught.</summary>
        public float AttackRateMultiplier => Mathf.Max(0.5f, 1f + Stat(StatType.AttackSpeed) / 100f) *
            (controller != null && controller.HasOnslaught ? PlayerController.OnslaughtMore : 1f);

        public float SkillArea(SkillId id) => DefenceMath.RadiusMultiplier(Stat(StatType.AreaOfEffect) + BonusesFor(id).Get(WeaponSkillEffect.Area));
        public float SkillProjectileRange(SkillId id) => BonusesFor(id).RangeMultiplier * SkillArea(id);
        public float SkillAttackRate(SkillId id) => Mathf.Max(0.5f, 1f +
            (Stat(StatType.AttackSpeed) + BonusesFor(id).Get(WeaponSkillEffect.Speed)) / 100f) *
            (controller != null && controller.HasOnslaught ? PlayerController.OnslaughtMore : 1f);
        public float SkillCastRate(SkillId id) => Mathf.Max(0.5f, 1f +
            (Stat(StatType.CastSpeed) + BonusesFor(id).Get(WeaponSkillEffect.Speed)) / 100f) *
            (controller != null && controller.HasOnslaught ? PlayerController.OnslaughtMore : 1f);

        /// <summary>Cooldown at the skill's current level, after cast speed. Bow and melee skills have none.</summary>
        public float Cooldown(SkillId id)
        {
            SkillDefinition skill = SkillBook.Get(id);
            if (skill.Summon && WeaponSkill == id)
                return 0.65f / (SkillCastRate(id));
            if (skill.Bow || skill.MeleeAttack)
                return 0f;
            return skill.CooldownAt(1) / (SkillCastRate(id));
        }

        public float CooldownAt(int slot)
        {
            SkillId? id = Slot(slot);
            if (id == null) return 0f;
            SkillDefinition skill = SkillBook.Get(id.Value);
            return skill.CooldownAt(Mathf.Max(1, LevelAt(slot))) / CastRateMultiplier;
        }

        public float CooldownForGrant(SkillGrant grant)
        {
            SkillDefinition skill = SkillBook.Get(grant.Id);
            return skill.CooldownAt(GrantLevelWithBonuses(grant)) / CastRateMultiplier;
        }

        /// <summary>
        /// A skill's cost at a skill level for this character (higher characters pay more). A melee
        /// skill's use costs less with a faster weapon and more with a slower one (in step with its
        /// base attack speed, a sword paying the listed cost), so it costs about the same per second
        /// whatever the weapon.
        /// </summary>
        public float CostOf(SkillId id, int level)
        {
            SkillDefinition skill = SkillBook.Get(id);
            int characterLevel = stats != null ? stats.Level : 1;
            float cost = skill.ManaCostAt(Mathf.Max(1, level)) * CharacterCostMultiplier(skill, characterLevel)
                + DamageSpellManaSurcharge(skill, characterLevel);
            if (skill.Spell)
                cost *= SpellManaCostMultiplier(characterLevel);
            // Staff skills cost half at character level 1, easing back to full cost at level 34.
            if (skill.StaffSkill)
                cost *= Mathf.Lerp(0.5f, 1f, Mathf.SmoothStep(0f, 1f, (characterLevel - 1f) / 33f));
            if (skill.MeleeAttack && !skill.Channelled)
                cost *= CharacterAttackAnimator.BaseAttackSpeed(WeaponType.Sword) /
                        Mathf.Max(0.1f, CharacterAttackAnimator.BaseAttackSpeed(CurrentWeapon()));
            return cost * Mathf.Max(0.1f, 1f + BonusesFor(id).Get(WeaponSkillEffect.Cost) / 100f);
        }

        /// <summary>Keep costs through level 7; ramp to 20% above the previous spell costs at level 34.</summary>
        public static float SpellManaCostMultiplier(int characterLevel)
        {
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((characterLevel - 7f) / 27f));
            return Mathf.Lerp(1f, 0.62f, progress) * Mathf.Lerp(1f, 1.2f, progress);
        }

        /// <summary>
        /// How much more a skill costs at a character level, rising smoothly from 1x at level 1:
        /// to 1.3x at level 34 for most skills, 1.5x for summons and Grave Rot, 2x for melee skills
        /// used as the attack (not Whirlwind). Bow skills and Death Mark barely change cost with their
        /// own level (gear rarely raises it), so they climb steeply with the character instead
        /// (about 8x by level 34). Damage spells continue to 1.9x at level 100, and also pay
        /// DamageSpellManaSurcharge; their costs never flatten out at level 34.
        /// </summary>
        public static float CharacterCostMultiplier(SkillDefinition skill, int characterLevel)
        {
            float l = Mathf.Max(0, characterLevel - 1);
            if (skill.Bow || skill.Id == SkillId.DeathMark)
                return 1f + 0.15f * l + 0.0019f * l * l;
            if (skill.DamageSpell)
                return 1f + 0.3f * l / 33f;
            float late = skill.Summon || skill.Id == SkillId.GraveRot ? 0.5f : skill.MeleeAttack && !skill.Channelled ? 1f : 0.3f;
            return 1f + late * Mathf.Clamp01(l / 33f);
        }

        /// <summary>
        /// Character-level mana pressure for damage spells, independent of their granted rank:
        /// a low-rank staff should not bypass mana management on a higher-level character.
        /// The early ramp adds about 12 mana at level 7, then grows smoothly to 52 at 34 and
        /// 118 at 100. Faster spells pay less per cast; long-cooldown spells cap at the same
        /// surcharge as a one-second spell. Level 1 adds nothing. Death Mark keeps its own curve.
        /// </summary>
        public static float DamageSpellManaSurcharge(SkillDefinition skill, int characterLevel)
        {
            if (!skill.DamageSpell || skill.Id == SkillId.DeathMark) return 0f;
            float l = Mathf.Max(0, characterLevel - 1);
            float earlyProgress = l / 10f;
            float pressure = l + 19f * (1f - Mathf.Exp(-earlyProgress * earlyProgress));
            return pressure * Mathf.Min(1f, skill.Cooldown);
        }

        public float ManaCostForGrant(SkillGrant grant) => CostOf(grant.Id, GrantLevelWithBonuses(grant));

        /// <summary>Seconds until the slot can be used again: 0 while it has a charge left.</summary>
        public float CooldownLeftAt(int slot) => ChargesAt(slot) > 0 ? 0f : RechargeLeftAt(slot);
        /// <summary>Seconds until the slot's next charge returns, even while others remain.</summary>
        public float RechargeLeftAt(int slot) => slot >= 0 && slot < bindings.Count ? bindings.RechargeLeftAt(slot, Time.time) : 0f;
        public int MaxChargesAt(int slot) => Slot(slot) != null ? Mathf.Max(1, SkillBook.Get(Slot(slot).Value).Charges) : 1;
        public int ChargesAt(int slot) => slot >= 0 && slot < bindings.Count ? bindings.ChargesAt(slot, MaxChargesAt(slot), Time.time) : 0;
        public float CooldownTotalAt(int slot) => slot >= 0 && slot < bindings.Count
            ? (bindings[slot].Cooldown > 0f ? bindings[slot].Cooldown : CooldownAt(slot)) : 0f;
        public float ManaCostAt(int slot) => Slot(slot) != null ? CostOf(Slot(slot).Value, LevelAt(slot)) : 0f;
        public bool CanAffordAt(int slot) => stats != null && stats.CanAffordSkill(ManaCostAt(slot));
        public string CostResource => stats != null && stats.UsesBloodMagic ? "life" : "mana";

        public float CooldownLeft(SkillId id)
        {
            return readyAt.TryGetValue(id, out float at) ? Mathf.Max(0f, at - Time.time) : 0f;
        }

        /// <summary>The full length of the cooldown now running (for the bar's sweep).</summary>
        public float CooldownTotal(SkillId id)
        {
            return cooldownOf.TryGetValue(id, out float total) ? total : Cooldown(id);
        }

        public float ManaCost(SkillId id) => CostOf(id, Level(id));

        public bool CanAfford(SkillId id)
        {
            return stats != null && stats.CanAffordSkill(ManaCost(id));
        }

        // ------------------------------------------------------------------ input

        private void Update()
        {
            if (Time.timeScale <= 0f || PoeClone.World.AreaManager.Instance != null && PoeClone.World.AreaManager.Instance.IsSwitching)
            {
                VirtualInput.SkillPressed = -1;
                return;
            }
            if (stats == null || stats.IsDead)
            {
                VirtualInput.SkillPressed = -1;
                return;
            }

            int pressed = VirtualInput.SkillPressed;
            VirtualInput.SkillPressed = -1;

            bool free = !UiKit.IsTypingInTextField() && !PlayerController.IsUiFocused();
            if (IsWhirling)
                UpdateWhirlwind(free);
            Keyboard keyboard = Keyboard.current;
            if (pressed < 0 && keyboard != null && free)
            {
                for (int k = 0; k < SlotKeys.Length; k++)
                {
                    int slot = k;
                    if (bindings[slot].Skill != null && keyboard[SlotKeys[k]].wasPressedThisFrame)
                        pressed = slot;
                }
            }

            // The spare mouse buttons (the left one attacks). Not over a window, and not on touch,
            // where browsers turn taps into mouse clicks.
            Mouse mouse = Mouse.current;
            if (pressed < 0 && mouse != null && free && !TouchMode.Active && !PlayerController.IsPointerOverUi() && !DialogueUI.IsOpen)
            {
                if (bindings[8].Skill != null && mouse.rightButton.wasPressedThisFrame) pressed = 8;
                else if (bindings[9].Skill != null && mouse.backButton.wasPressedThisFrame) pressed = 9;
                else if (bindings[10].Skill != null && mouse.forwardButton.wasPressedThisFrame) pressed = 10;
            }

            if (pressed >= 0)
                TryUse(pressed);
        }

        /// <summary>Uses the skill in a slot if gear grants it and it's ready and affordable.</summary>
        public bool TryUse(int slot)
        {
            if (PoeClone.World.AreaManager.Instance != null && PoeClone.World.AreaManager.Instance.IsSwitching)
                return false;
            SkillId? id = Slot(slot);
            if (id == null)
                return false;

            SkillDefinition skill = SkillBook.Get(id.Value);
            int level = LevelAt(slot);
            if (level <= 0 || MainSkill == skill.Id)
            {
                CombatText.Show(transform.position + Vector3.up * 2f, skill.Name + ": not on your gear", UiKit.DimText, 0.8f);
                return false;
            }
            if (CooldownLeftAt(slot) > 0f || controller.IsDashing || controller.IsSkillCommitted)
                return false;

            // Gestures wait for the current swing (or spin); Dash and War Cry can cut in.
            bool usesArms = skill.Id != SkillId.Dash && skill.Id != SkillId.WarCry;
            if (usesArms && (IsWhirling || (attackAnimator != null && attackAnimator.IsAttacking)))
                return false;

            if (!stats.TrySpendMana(ManaCostAt(slot)))
            {
                CombatText.Show(transform.position + Vector3.up * 2f, "Not enough " + CostResource, CombatText.ColdColor, 0.8f);
                return false;
            }

            float cooldown = CooldownAt(slot);
            bindings.StartCooldown(slot, cooldown, Time.time);
            stats.EndArrivalProtection();
            controller.CancelWalk();
            Cast(skill, level);
            return true;
        }

        private void StartCooldown(SkillId id)
        {
            float cooldown = Cooldown(id);
            cooldownOf[id] = cooldown;
            readyAt[id] = Time.time + cooldown;
        }

        // ------------------------------------------------------------------ the staff's attack

        /// <summary>
        /// Pays for one cast of the staff's attack spell (PlayerCombat starts the cast animation
        /// when this succeeds, and calls <see cref="ReleaseMain"/> at its strike frame). False
        /// without the mana for it: the staff is swung instead.
        /// </summary>
        public bool TrySpendMain()
        {
            SkillId? main = MainSkill;
            if (main == null || stats == null)
                return false;
            if (SkillBook.Get(main.Value).Summon && Minion.CanCommand(transform, MinionKindOf(main.Value))) return true;
            if (!stats.TrySpendMana(ManaCost(main.Value)))
                return false;
            StartCooldown(main.Value);
            return true;
        }

        /// <summary>Seconds between casts of the attack spell (level and Cast Speed shorten it).</summary>
        public float MainInterval()
        {
            SkillId? main = MainSkill;
            return main != null ? Cooldown(main.Value) : 1f;
        }

        /// <summary>The attack spell leaves the staff, the way the player is facing right now.</summary>
        public void ReleaseMain()
        {
            SkillId? main = MainSkill;
            if (main == null)
                return;
            if (SkillBook.Get(main.Value).Summon && Minion.CanCommand(transform, MinionKindOf(main.Value)))
            {
                Minion.Command(transform, AimedEnemy(MarkReach) ?? EnemyInDirection(transform.forward, MarkReach), MinionKindOf(main.Value));
                return;
            }
            Cast(SkillBook.Get(main.Value), Mathf.Max(1, Level(main.Value)), asAttack: true);
        }

        /// <summary>How far the attack spell reaches (for PlayerCombat's aim highlight).</summary>
        public float MainReach()
        {
            SkillId? main = MainSkill;
            if (main == SkillId.ChainLightning)
                return ChainReach * BonusesFor(SkillId.ChainLightning).RangeMultiplier;
            if (main == SkillId.DeathMark)
                return MarkReach;
            if (main == SkillId.VenomSpout)
                return 12f * BonusesFor(SkillId.VenomSpout).RangeMultiplier;
            return (main == SkillId.IceShard ? ShardRange : BoltRange) * (main.HasValue ? SkillProjectileRange(main.Value) : 1f);
        }

        // ------------------------------------------------------------------ the skills

        private ItemData orbitWeapon;
        private int orbitUses;
        private const float BoltRange = 16f;
        private const float ShardRange = 12f;

        // asAttack: cast as the staff's attack, already facing where it should go (PlayerCombat
        // turned the player at the moment of release); from the bar it aims itself.
        private void Cast(SkillDefinition skill, int level, bool asAttack = false)
        {
            PlaySkillSound(skill.Id, transform.position);
            float area = SkillArea(skill.Id);
            float damage = skill.DamageForCharacter(inventory != null ? inventory.Stats.Level : 1);
            var tree = BonusesFor(skill.Id);
            int extraProjectiles = tree.ExtraProjectiles;

            switch (skill.Id)
            {
                case SkillId.FireBolt:
                    Face(ProjectileAimAssist.Direction(transform.position, asAttack ? transform.forward : AimDirection(), BoltRange));
                    if (!asAttack && attackAnimator != null)
                        attackAnimator.PlayAttack(WeaponType.Unarmed);
                    var bolts = PlayerArrow.NewVolley();
                    int boltCount = tree.SingleProjectile ? 1 : 1 + extraProjectiles;
                    foreach (Vector3 direction in HitEffects.Spread(transform.forward, boltCount, 12f))
                        PlayerArrow.LaunchBolt(transform, BoltRange * SkillProjectileRange(skill.Id), damage, skill.Color, 1.6f * area, direction, volley: bolts).WithSkill(tree);
                    var flights = new List<Vector3>();
                    foreach (Vector3 direction in HitEffects.Spread(transform.forward, boltCount, 12f)) flights.Add(direction * BoltRange * SkillProjectileRange(skill.Id));
                    Record(skill, level, 1.6f * area, boltCount, flights.ToArray());
                    if (tree.Get(WeaponSkillEffect.FireOrbit) > 0)
                    {
                        if (orbitWeapon != MainWeapon) { orbitWeapon = MainWeapon; orbitUses = 0; }
                        if (++orbitUses >= tree.Get(WeaponSkillEffect.FireOrbit))
                        {
                            orbitUses = 0;
                            StartCoroutine(FireWheel(skill, damage, area, tree, MainWeapon));
                        }
                    }
                    break;

                case SkillId.IceShard:
                    Face(ProjectileAimAssist.Direction(transform.position, asAttack ? transform.forward : AimDirection(), ShardRange));
                    if (!asAttack && attackAnimator != null)
                        attackAnimator.PlayAttack(WeaponType.Unarmed);
                    // Each enemy is hurt by one shard of the fan: the spread covers more of them,
                    // it doesn't stack on one.
                    var shards = PlayerArrow.NewVolley();
                    foreach (Vector3 direction in HitEffects.Spread(transform.forward, 1 + extraProjectiles, 7f))
                        PlayerArrow.LaunchShard(transform, ShardRange * SkillProjectileRange(skill.Id), damage, skill.Color, 1.5f, direction, volley: shards).WithSkill(tree);
                    Record(skill, level, 0f, 1 + extraProjectiles);
                    break;

                case SkillId.Dash:
                    Vector3 dir = TouchMode.Active || !DashTowardsCursor ? controller.InputDirection() : AimDirection();
                    if (dir.sqrMagnitude < 0.01f)
                        dir = AimDirection();
                    SkillEffects.Shockwave(transform.position, 1.2f, skill.Color, 0.25f, type: SkillEffects.DamagePattern(skill));
                    Record(skill, level);
                    controller.Dash(dir, 7f, 0.18f);
                    break;

                case SkillId.FrostNova:
                    float novaRadius = 5f * area;
                    SkillEffects.Shockwave(transform.position, novaRadius, skill.Color, 0.4f, type: SkillEffects.DamagePattern(skill));
                    Record(skill, level, novaRadius);
                    foreach (EnemyHealth enemy in EnemiesWithin(transform.position, novaRadius))
                    {
                        EnemyController ai = enemy.GetComponent<EnemyController>();
                        if (ai != null)
                            ai.Chill(3f);
                        Hit(enemy, damage, CombatText.ColdColor, attack: false, DamageType.Cold, skillId: skill.Id);
                    }
                    break;

                case SkillId.WarCry:
                    float cryRadius = 4f * area;
                    SkillEffects.Shockwave(transform.position, cryRadius, skill.Color, 0.45f, type: SkillEffects.DamagePattern(skill));
                    foreach (EnemyHealth enemy in EnemiesWithin(transform.position, cryRadius))
                        Hit(enemy, damage, CombatText.PhysicalColor, attack: false, DamageType.Physical, skillId: skill.Id);
                    controller.GrantOnslaught(4f);
                    Record(skill, level, cryRadius);
                    break;

                case SkillId.ChainLightning:
                    ChainLightning(skill, damage, level, asAttack);
                    break;

                case SkillId.Teleport:
                    Teleport(skill, level);
                    break;

                case SkillId.RaiseSkeletons:
                case SkillId.SkeletonMages:
                case SkillId.SpiritWolves:
                case SkillId.BoneGolem:
                case SkillId.SummonViper:
                    if (!asAttack && attackAnimator != null)
                        attackAnimator.PlayAttack(WeaponType.Unarmed);
                    float summonDistance = TouchMode.Active ? 3f : AimDistance() ?? 3f;
                    Minion.Summon(transform, MinionKindOf(skill.Id), level,
                        transform.position + AimDirection() * summonDistance);
                    Record(skill, level, 1.3f);
                    break;

                case SkillId.DeathMark:
                    DeathMark(skill, damage, level, asAttack);
                    break;

                case SkillId.GraveRot:
                    GraveRot(skill, level, area);
                    break;

                case SkillId.VenomSpout:
                {
                    Vector3 direction = AimDirection();
                    float spoutRange = 12f * tree.RangeMultiplier;
                    EnemyHealth aimed = AimedEnemy(spoutRange) ?? (TouchMode.Active ? EnemyInDirection(direction, spoutRange) : null);
                    Vector3 at = aimed != null ? aimed.transform.position : transform.position + direction * Mathf.Clamp(AimDistance() ?? 7f, 0f, spoutRange);
                    float radius = 2.6f * area;
                    SkillEffects.Shockwave(at, radius, skill.Color, 0.45f, type: SkillEffects.DamagePattern(skill));
                    Record(skill, level, radius, 0, new[] { at });
                    foreach (EnemyHealth target in EnemiesWithin(at, radius))
                    {
                        Hit(target, damage, skill.Color, false, DamageType.Poison, skillId: skill.Id);
                        WeaponVenom.Apply(transform, target, damage, 100f, 0f, inventory.Stats, durationMultiplier: tree.DurationMultiplier, radiusMultiplier: tree.AreaMultiplier, tree: tree);
                    }
                    break;
                }
            }
        }

        private System.Collections.IEnumerator FireWheel(SkillDefinition skill, float damage, float area, WeaponSkillBonuses tree, ItemData weapon)
        {
            var volley = PlayerArrow.NewVolley();
            for (int i = 0; i < 8; i++)
            {
                if (stats == null || stats.IsDead || MainWeapon != weapon) yield break;
                Vector3 direction = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
                PlayerArrow.LaunchBolt(transform, BoltRange * SkillProjectileRange(skill.Id), damage * 0.4f, skill.Color, 1.6f * area, direction, volley: volley).WithSkill(tree);
                Record(skill, 1, 1.6f * area, 1, new[] { direction * BoltRange * SkillProjectileRange(skill.Id) });
                PlaySkillSound(skill.Id, transform.position);
                yield return new WaitForSeconds(0.07f);
            }
        }

        private static void PlaySkillSound(SkillId skill, Vector3 at)
        {
            string sound;
            switch (skill)
            {
                case SkillId.Cleave: sound = "skill_cleave"; break;
                case SkillId.Pulverize: sound = "skill_pulverize"; break;
                case SkillId.ReapingArc: sound = "skill_reaping_arc"; break;
                case SkillId.LungingThrust: sound = "skill_lunging_thrust"; break;
                case SkillId.FangStrike:
                case SkillId.VenomSlash: sound = "skill_fang_strike"; break;
                case SkillId.Whirlwind:
                case SkillId.CrossSlash: sound = "skill_cleave"; break;
                case SkillId.Fissure:
                case SkillId.Aftershock: sound = "skill_pulverize"; break;
                default:
                    PoeClone.Audio.AudioManager.Instance?.PlayEffect("skill." + skill, at);
                    return;
            }

            PoeClone.Audio.AudioManager audio = PoeClone.Audio.AudioManager.Instance;
            if (audio != null)
                audio.PlayAtPoint(audio.Sfx(sound), at);
        }

        public static MinionKind MinionKindOf(SkillId id)
        {
            switch (id)
            {
                case SkillId.SkeletonMages: return MinionKind.Mage;
                case SkillId.SpiritWolves: return MinionKind.Wolf;
                case SkillId.BoneGolem: return MinionKind.Golem;
                case SkillId.SummonViper: return MinionKind.Viper;
                default: return MinionKind.Warrior;
            }
        }

        // Where the player aims, up to a dozen paces off: everything within reach of it is cursed.
        // Centred on the enemy aimed at if there is one (on touch: the nearest the aim stick
        // points at), so a quick cast on a crowd lands on it.
        private void GraveRot(SkillDefinition skill, int level, float area)
        {
            Vector3 direction = AimDirection();
            EnemyHealth aimed = AimedEnemy(12f) ?? (TouchMode.Active ? EnemyInDirection(direction, 12f) : null);
            float distance = Mathf.Clamp(AimDistance() ?? 7f, 0f, 12f);
            Vector3 at = aimed != null ? aimed.transform.position : transform.position + direction * distance;
            float radius = 3.5f * area;
            Face(direction);
            if (attackAnimator != null)
                attackAnimator.PlayAttack(WeaponType.Unarmed);
            SkillEffects.Shockwave(at, radius, skill.Color, 0.5f, type: SkillEffects.DamagePattern(skill));

            float seconds = 6f;
            float less = 0.2f;
            float more = 0.15f;
            foreach (EnemyHealth enemy in EnemiesWithin(at, radius))
                Curse.Apply(enemy, seconds, less, more);
            Record(skill, level, radius, 0, new[] { at });
        }

        // How far Death Mark reaches.
        private const float MarkReach = 14f;

        // A bolt of grave-light that marks what it strikes: the aimed enemy, else the nearest one
        // the cast points at, else the nearest one at all. It barely hurts; the mark is the point.
        private void DeathMark(SkillDefinition skill, float damage, int level, bool asAttack)
        {
            Vector3 from = transform.position + Vector3.up * 0.9f;
            EnemyHealth target = AimedEnemy(MarkReach) ?? EnemyInDirection(asAttack ? transform.forward : AimDirection(), MarkReach) ??
                                 Nearest(transform.position, MarkReach * 0.6f, new HashSet<EnemyHealth>());
            if (target == null)
            {
                Vector3 to = from + transform.forward * 4f;
                SkillEffects.Arc(from, to, skill.Color, 0.2f);
                Record(skill, level, 0f, 0, new[] { from, to });
                return;
            }

            Face(target.transform.position - transform.position);
            if (!asAttack && attackAnimator != null)
                attackAnimator.PlayAttack(WeaponType.Unarmed);
            Vector3 hitAt = target.transform.position + Vector3.up * 0.5f * target.transform.localScale.y;
            SkillEffects.Arc(from, hitAt, skill.Color, 0.25f);
            Minion.Mark(target, transform);
            Hit(target, damage, skill.Color, attack: false, skillId: skill.Id);
            Record(skill, level, 0f, 0, new[] { from, hitAt });
        }

        // How far Chain Lightning reaches for its first target, and how far each arc jumps.
        private const float ChainReach = 9f;
        private const float ChainJump = 5f;

        // Rewards aiming: a cast on an enemy under the cursor hits for full damage (more up close,
        // where the caster is in danger too); a blind cast at whatever is nearest hits for less.
        // Each arc is weaker than the one before.
        private void ChainLightning(SkillDefinition skill, float damage, int level, bool asAttack)
        {
            var struck = new HashSet<EnemyHealth>();
            Vector3 from = transform.position + Vector3.up * 0.4f;

            EnemyHealth target = AimedEnemy(ChainReach * BonusesFor(skill.Id).RangeMultiplier);
            if (target == null)
            {
                target = Nearest(transform.position, ChainReach * BonusesFor(skill.Id).RangeMultiplier, struck);
                damage *= 0.7f;
            }
            else
            {
                Vector3 offset = target.transform.position - transform.position;
                offset.y = 0f;
                if (offset.magnitude < 3.5f)
                    damage *= 1.25f;
            }

            if (target == null)
            {
                // Nothing to strike: a short fizzle so the cast doesn't feel swallowed.
                SkillEffects.Arc(from, from + transform.forward * 3f, skill.Color);
                Record(skill, level, 0f, 0, new[] { from, from + transform.forward * 3f });
                return;
            }

            Face(target.transform.position - transform.position);
            if (!asAttack && attackAnimator != null)
                attackAnimator.PlayAttack(WeaponType.Unarmed);

            var tree = BonusesFor(skill.Id);
            int jumps = Mathf.Max(1, 1 + Mathf.RoundToInt(tree.Get(WeaponSkillEffect.Chains)));
            var arc = new List<Vector3> { from };
            float chainMultiplier = 1f;
            for (int jump = 0; jump < jumps && target != null; jump++)
            {
                Vector3 to = target.transform.position + Vector3.up * 0.4f * target.transform.localScale.y;
                SkillEffects.Arc(from, to, skill.Color);
                arc.Add(to);
                Hit(target, damage, CombatText.LightningColor, attack: false, DamageType.Lightning,
                    manaDamageMultiplier: chainMultiplier, skillId: skill.Id);
                struck.Add(target);

                damage *= Mathf.Clamp(0.6f + tree.Get(WeaponSkillEffect.ChainRetention) / 100f, 0.1f, 1f);
                chainMultiplier *= 0.6f;
                from = to;
                target = Nearest(target.transform.position, ChainJump * tree.RangeMultiplier, struck);
            }
            Record(skill, level, 0f, 0, arc.ToArray());
        }

        private void Hit(EnemyHealth enemy, float damage, Color color, bool attack, DamageType type = DamageType.Physical,
            bool melee = false, float manaDamageMultiplier = 1f, SkillId? skillId = null)
        {
            HitEffects.Deal(transform, enemy, damage, attack, color, type, melee: melee, manaDamageMultiplier: manaDamageMultiplier,
                tree: skillId.HasValue ? BonusesFor(skillId.Value) : null);
        }

        // ------------------------------------------------------------------ teleport

        /// <summary>How far Teleport reaches: about half the screen, a little further each level.</summary>
        public static float TeleportRange(int level) => 9f;

        // On mobile, full range straight ahead; on desktop, to the aimed point (or as far as it
        // reaches that way), through anything in between. The
        // landing spot has to be open ground inside the current area: if it isn't, the jump comes
        // up short, back toward the player, until it is.
        private void Teleport(SkillDefinition skill, int level)
        {
            Vector3 start = transform.position;
            Vector3 direction = TouchMode.Active ? Flat(transform.forward) : AimDirection();
            float distance = TeleportRange(level);
            float? aimed = AimDistance();
            if (aimed.HasValue)
                distance = Mathf.Clamp(aimed.Value, 0.05f, distance);

            // Ordinary walls can be crossed, but progression seals stop the jump on this side.
            CharacterController body = GetComponent<CharacterController>();
            float radius = body != null ? body.radius : 0.4f;
            foreach (RaycastHit hit in Physics.SphereCastAll(start, radius, direction, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                var barrier = hit.collider.GetComponentInParent<Quests.QuestBarrier>();
                var passage = hit.collider.GetComponentInParent<World.ActTwoPassage>();
                var sanctuaryDoor = hit.collider.GetComponentInParent<World.ActBossDoor>();
                if ((barrier != null && barrier.BlocksTeleport) || (passage != null && passage.BlocksTeleport) ||
                    (sanctuaryDoor != null && !World.ActBossArena.DoorOpen))
                    distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - 0.03f));
            }

            Vector3? landing = null;
            const float landingStep = 0.1f;
            for (float d = distance; d > 0f; d = Mathf.Max(0f, d - landingStep))
            {
                Vector3? spot = OpenGround(start + direction * d);
                if (spot.HasValue)
                {
                    landing = spot;
                    // Refine the last blocked interval to stop within a centimetre of the wall.
                    float low = d, high = Mathf.Min(distance, d + landingStep);
                    for (int i = 0; i < 4; i++)
                    {
                        float middle = (low + high) * 0.5f;
                        Vector3? closer = OpenGround(start + direction * middle);
                        if (closer.HasValue) { low = middle; landing = closer; }
                        else high = middle;
                    }
                    break;
                }
            }
            if (landing == null)
            {
                SkillEffects.Shockwave(start, 0.8f, skill.Color, 0.2f, type: SkillEffects.DamagePattern(skill));
                Record(skill, level, 0f, 0, new[] { start });
                return;
            }

            SkillEffects.Shockwave(start, 1.4f, skill.Color, 0.3f, type: SkillEffects.DamagePattern(skill));
            Face(direction);
            bool wasEnabled = body != null && body.enabled;
            if (body != null)
                body.enabled = false;
            transform.position = landing.Value;
            if (body != null)
                body.enabled = wasEnabled;
            Physics.SyncTransforms();
            SkillEffects.Shockwave(landing.Value, 1.6f, skill.Color, 0.35f, type: SkillEffects.DamagePattern(skill));
            Record(skill, level, 0f, 0, new[] { landing.Value });
        }

        // How far the mouse points from the player on the ground (null on touch: full range).
        private float? AimDistance()
        {
            if (TouchMode.Active)
                return null;
            Mouse mouse = Mouse.current;
            Camera cam = Camera.main;
            if (mouse == null || cam == null)
                return null;
            Ray ray = cam.ScreenPointToRay(ScreenPointer.Position);
            if (!new Plane(Vector3.up, transform.position).Raycast(ray, out float enter))
                return null;
            Vector3 offset = ray.GetPoint(enter) - transform.position;
            offset.y = 0f;
            return offset.magnitude;
        }

        private static readonly Collider[] landingBuffer = new Collider[16];

        // Ground about level with the player at this spot, with room to stand and inside the area.
        private Vector3? OpenGround(Vector3 at)
        {
            if (World.WorldBuilder.Instance != null && !World.WorldBuilder.Instance.CanMoveTo(at)) return null;
            CharacterController body = GetComponent<CharacterController>();
            float radius = body != null ? body.radius : 0.4f;
            var areas = World.AreaManager.Instance;
            if (areas != null && areas.CurrentAreaIndex >= 0 &&
                !World.WorldBuilder.Shape(areas.CurrentAreaIndex).Contains(at, radius))
                return null;

            float y = transform.position.y;
            float floor = GroundBelowPlayer();
            if (!Physics.Raycast(new Vector3(at.x, floor + 2f, at.z), Vector3.down, out RaycastHit ground, 3.5f, ~0, QueryTriggerInteraction.Ignore))
                return null;
            if (Mathf.Abs(ground.point.y - floor) > 1.2f || ground.normal.y < 0.7f)
                return null;

            float height = body != null ? body.height : 2f;
            Vector3 feet = new Vector3(at.x, ground.point.y, at.z);
            Vector3 bottom = feet + Vector3.up * (radius + 0.15f);
            Vector3 top = feet + Vector3.up * Mathf.Max(radius + 0.2f, height - radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, landingBuffer, ~0, QueryTriggerInteraction.Ignore);
            if (count == landingBuffer.Length) return null;
            for (int k = 0; k < count; k++)
            {
                Collider c = landingBuffer[k];
                if (c == ground.collider || c.transform.IsChildOf(transform))
                    continue;
                return null;
            }
            // The player's pivot stands at the same height over this ground as over its own.
            return new Vector3(at.x, y + (ground.point.y - floor), at.z);
        }

        private float GroundBelowPlayer()
        {
            Vector3 p = transform.position;
            float best = float.MaxValue;
            float y = p.y;
            foreach (RaycastHit hit in Physics.RaycastAll(p + Vector3.up * 1.5f, Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform) || hit.distance >= best)
                    continue;
                best = hit.distance;
                y = hit.point.y;
            }
            return y;
        }

        private float Stat(StatType stat)
        {
            return inventory != null && inventory.Stats != null ? inventory.Stats.Total(stat) : 0f;
        }

        // ------------------------------------------------------------------ helpers

        private WeaponType CurrentWeapon()
        {
            ItemData weapon = inventory != null ? inventory.Equipment.GetActive(EquipSlot.MainHand) : null;
            return weapon != null ? weapon.WeaponType : WeaponType.Unarmed;
        }

        // The same damage a basic attack does (weapon + Strength), see PlayerCombat.
        private float WeaponDamage()
        {
            float physical = inventory != null ? inventory.Stats.Total(StatType.PhysicalDamage) : 2f;
            return physical;
        }

        private List<EnemyHealth> EnemiesWithin(Vector3 center, float radius)
        {
            var found = new List<EnemyHealth>();
            int count = Physics.OverlapSphereNonAlloc(center, radius, buffer);
            for (int k = 0; k < count; k++)
            {
                EnemyHealth enemy = buffer[k].GetComponentInParent<EnemyHealth>();
                if (enemy != null && !enemy.IsDead && !found.Contains(enemy))
                    found.Add(enemy);
            }
            return found;
        }

        private EnemyHealth Nearest(Vector3 center, float radius, HashSet<EnemyHealth> exclude)
        {
            EnemyHealth best = null;
            float bestSq = radius * radius;
            foreach (EnemyHealth enemy in EnemiesWithin(center, radius))
            {
                if (exclude.Contains(enemy))
                    continue;
                Vector3 d = enemy.transform.position - center;
                d.y = 0f;
                if (d.sqrMagnitude <= bestSq)
                {
                    bestSq = d.sqrMagnitude;
                    best = enemy;
                }
            }
            return best;
        }

        // Half-angle, in degrees, of the cone the aim stick's direction has to land an enemy in to
        // count as deliberately aimed at it (as opposed to a blind cast at whatever's nearest).
        private const float AimConeHalfAngle = 20f;

        // The closest living enemy that a world direction (e.g. the aim stick's) currently points
        // at, within reach and AimConeHalfAngle of it. Zero direction never matches anything.
        private EnemyHealth EnemyInDirection(Vector3 direction, float reach)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                return null;

            EnemyHealth best = null;
            float bestSq = float.MaxValue;
            foreach (EnemyHealth enemy in EnemiesWithin(transform.position, reach))
            {
                Vector3 toEnemy = enemy.transform.position - transform.position;
                toEnemy.y = 0f;
                if (Vector3.Angle(direction, toEnemy) > AimConeHalfAngle || !Within(enemy, reach))
                    continue;

                float distSq = toEnemy.sqrMagnitude;
                if (distSq <= bestSq)
                {
                    bestSq = distSq;
                    best = enemy;
                }
            }
            return best;
        }

        // The living enemy drawn under the mouse (or within a short distance of the ground point
        // under it), within reach of the player. On touch: whatever the aim stick points at.
        private EnemyHealth AimedEnemy(float reach)
        {
            if (TouchMode.Active)
                return EnemyInDirection(PlayerController.CameraRelativeDirection(VirtualInput.Aim), reach);

            Mouse mouse = Mouse.current;
            Camera cam = Camera.main;
            if (mouse == null || cam == null)
                return null;

            Ray ray = cam.ScreenPointToRay(ScreenPointer.Position);
            if (Physics.Raycast(ray, out RaycastHit hit, 300f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                EnemyHealth under = hit.collider.GetComponentInParent<EnemyHealth>();
                if (under != null && !under.IsDead && Within(under, reach))
                    return under;
            }

            if (!new Plane(Vector3.up, transform.position).Raycast(ray, out float enter))
                return null;
            EnemyHealth near = Nearest(ray.GetPoint(enter), 1.8f, new HashSet<EnemyHealth>());
            return near != null && Within(near, reach) ? near : null;
        }

        // Within reach of the enemy's body, not just its centre: a big boss is reached at its edge
        // (as by a plain swing, see PlayerCombat.DamageableReach).
        private bool Within(EnemyHealth enemy, float reach)
        {
            Vector3 d = enemy.transform.position - transform.position;
            d.y = 0f;
            float bodyReach = PlayerCombat.DamageableReach(enemy, reach);
            return d.sqrMagnitude <= bodyReach * bodyReach;
        }

        // Where a cast should go: the mouse on desktop; on touch, the aim stick's direction, else
        // the joystick direction, else straight ahead.
        private Vector3 AimDirection()
        {
            if (TouchMode.Active)
            {
                Vector3 aim = PlayerController.CameraRelativeDirection(VirtualInput.Aim);
                if (aim.sqrMagnitude > 0.0001f)
                    return aim;

                Vector3 input = controller.InputDirection();
                return input.sqrMagnitude > 0.01f ? input : transform.forward;
            }

            if (PlayerCombat.CursorAimPoint(transform.position, out Vector3 point))
                return Flat(point - transform.position);
            return transform.forward;
        }

        private void Face(Vector3 direction)
        {
            direction = Flat(direction);
            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(direction);
        }

        private Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : transform.forward;
        }
    }
}
