using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.UI;
using PoeClone.Visuals;

namespace PoeClone.Skills
{
    /// <summary>
    /// The player's skills (see <see cref="SkillBook"/>). Skills come from worn gear, each at a
    /// level (the highest any worn item grants, plus "+N to level" gear). A staff's first spell is
    /// its attack: <see cref="PlayerCombat"/> casts it on left click / the aim stick instead of
    /// swinging (<see cref="MainSkill"/>, <see cref="TrySpendMain"/>, <see cref="ReleaseMain"/>).
    /// Every other skill goes in a bar slot: Q, E, R, F, then the right, middle, back and forward
    /// mouse buttons (touch has round buttons for the first four). A skill that new gear grants
    /// drops into the first free slot; the skills panel (K) moves them around. Each costs mana and
    /// has a cooldown. Aiming works like attacks: the mouse on desktop, <see cref="VirtualInput.Aim"/>
    /// on touch. Self-added by <see cref="PlayerController"/>.
    /// </summary>
    public class PlayerSkills : MonoBehaviour
    {
        private static readonly string[] SlotLabels = { "Q", "E", "R", "F", "RMB", "MMB", "M4", "M5" };
        private static readonly Key[] SlotKeys = { Key.Q, Key.E, Key.R, Key.F };

        private readonly SkillId?[] slots = new SkillId?[SkillBook.SlotCount];
        private readonly Dictionary<SkillId, float> readyAt = new Dictionary<SkillId, float>();
        private readonly Dictionary<SkillId, float> cooldownOf = new Dictionary<SkillId, float>();
        private readonly Collider[] buffer = new Collider[32];

        private PlayerStats stats;
        private PlayerController controller;
        private PlayerInventory inventory;
        private CharacterAttackAnimator attackAnimator;
        private EquipmentSet boundEquipment;

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

        private void Record(SkillDefinition skill, int level, float size = 0f, int count = 0, Vector3[] points = null)
        {
            CastCount++;
            recentCasts[CastCount % RecentCastCount] = new CastRecord
            {
                Number = CastCount,
                Skill = skill.Id,
                Level = level,
                At = transform.position,
                Facing = transform.forward,
                Size = size,
                Count = count,
                Points = points,
                Time = Time.time
            };
        }

        /// <summary>
        /// Draws a cast's effect without any of its gameplay: harmless bolts and shards, the rings,
        /// the glow, the lightning. For a spectator's copy of the player, whose own skills are off.
        /// </summary>
        public static void PlayVisual(CastRecord cast, Transform caster)
        {
            SkillDefinition skill = SkillBook.Get(cast.Skill);
            Vector3 facing = cast.Facing;
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.0001f)
                facing = caster.forward;
            facing.Normalize();

            switch (cast.Skill)
            {
                case SkillId.FireBolt:
                    foreach (Vector3 direction in HitEffects.Spread(facing, Mathf.Max(1, cast.Count), 12f))
                        PlayerArrow.LaunchBolt(caster, BoltRange, 0f, skill.Color, cast.Size, direction, harmless: true);
                    break;

                case SkillId.IceShard:
                    foreach (Vector3 direction in HitEffects.Spread(facing, Mathf.Max(1, cast.Count), 7f))
                        PlayerArrow.LaunchShard(caster, ShardRange, 0f, skill.Color, 0f, direction, harmless: true);
                    break;

                case SkillId.Dash:
                    SkillEffects.Shockwave(cast.At, 1.2f, skill.Color, 0.25f);
                    break;

                case SkillId.FrostNova:
                case SkillId.Cleave:
                    SkillEffects.Shockwave(cast.At, cast.Size, skill.Color, cast.Skill == SkillId.Cleave ? 0.3f : 0.4f);
                    break;

                case SkillId.Rejuvenate:
                    SkillEffects.Rise(caster, skill.Color);
                    break;

                case SkillId.ChainLightning:
                    if (cast.Points != null)
                    {
                        for (int k = 0; k + 1 < cast.Points.Length; k++)
                            SkillEffects.Arc(cast.Points[k], cast.Points[k + 1], skill.Color);
                    }
                    break;
            }
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
        }

        private void Start()
        {
            if (inventory != null)
            {
                boundEquipment = inventory.Equipment;
                boundEquipment.Changed += OnGearChanged;
                inventory.StatsChanged += OnStatsChanged;
            }
            FillEmptySlots(announce: false);
        }

        private void OnDestroy()
        {
            if (boundEquipment != null)
                boundEquipment.Changed -= OnGearChanged;
            if (inventory != null)
                inventory.StatsChanged -= OnStatsChanged;
        }

        private void OnGearChanged(EquipSlot slot, ItemData item)
        {
            FillEmptySlots(announce: item != null);
        }

        private void OnStatsChanged()
        {
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ levels from gear

        /// <summary>
        /// The skill's level: the highest any worn item grants it at, plus "+N to level" gear for a
        /// spell. 0 if nothing worn grants it.
        /// </summary>
        public int Level(SkillId id)
        {
            if (inventory == null)
                return 0;

            SkillDefinition skill = SkillBook.Get(id);
            int granted = 0;
            foreach (EquipSlot slot in SlotRules.AllSlots)
            {
                ItemData item = inventory.Equipment.Get(slot);
                if (item == null)
                    continue;
                foreach (StatModifier m in item.Modifiers)
                {
                    if (m.Stat == skill.Grant)
                        granted = Mathf.Max(granted, Mathf.RoundToInt(m.Value));
                }
            }
            if (granted <= 0)
                return 0;

            int bonus = 0;
            if (skill.Spell)
            {
                bonus += Mathf.RoundToInt(Stat(StatType.AllSpellLevels));
                if (skill.Element == SkillElement.Fire)
                    bonus += Mathf.RoundToInt(Stat(StatType.FireSpellLevels));
                else if (skill.Element == SkillElement.Cold)
                    bonus += Mathf.RoundToInt(Stat(StatType.ColdSpellLevels));
                else if (skill.Element == SkillElement.Lightning)
                    bonus += Mathf.RoundToInt(Stat(StatType.LightningSpellLevels));
            }
            return Mathf.Max(1, granted + bonus);
        }

        /// <summary>
        /// The staff's attack: the first spell on the weapon that can be one. Null without a staff
        /// (the plain weapon attack is used).
        /// </summary>
        public SkillId? MainSkill
        {
            get
            {
                ItemData weapon = inventory != null ? inventory.Equipment.Get(EquipSlot.MainHand) : null;
                if (weapon == null)
                    return null;
                foreach (StatModifier m in weapon.Modifiers)
                {
                    if (SkillGrants.IsMain(m.Stat))
                    {
                        SkillDefinition skill = SkillBook.ForGrant(m.Stat);
                        if (skill != null)
                            return skill.Id;
                    }
                }
                return null;
            }
        }

        /// <summary>Gear grants it and it isn't the staff's attack: it can go on the bar.</summary>
        public bool IsUnlocked(SkillId id)
        {
            return Level(id) > 0 && MainSkill != id;
        }

        /// <summary>Which worn item grants the skill (the one at the highest level), for the skills panel.</summary>
        public ItemData Source(SkillId id)
        {
            if (inventory == null)
                return null;
            StatType grant = SkillBook.Get(id).Grant;
            ItemData best = null;
            float bestLevel = 0f;
            foreach (EquipSlot slot in SlotRules.AllSlots)
            {
                ItemData item = inventory.Equipment.Get(slot);
                if (item == null)
                    continue;
                foreach (StatModifier m in item.Modifiers)
                {
                    if (m.Stat == grant && m.Value > bestLevel)
                    {
                        bestLevel = m.Value;
                        best = item;
                    }
                }
            }
            return best;
        }

        // ------------------------------------------------------------------ slots

        public SkillId? Slot(int index)
        {
            return index >= 0 && index < slots.Length ? slots[index] : null;
        }

        /// <summary>Empties a slot.</summary>
        public void ClearSlot(int slot)
        {
            if (slot < 0 || slot >= slots.Length || slots[slot] == null)
                return;
            slots[slot] = null;
            Changed?.Invoke();
        }

        /// <summary>
        /// Puts a skill in a slot (taking it out of any other slot). Not the staff's attack. A skill
        /// the gear doesn't grant right now may still be placed (a loaded save, a spectator's copy):
        /// it waits there, greyed out, until gear grants it again.
        /// </summary>
        public void Assign(int slot, SkillId id)
        {
            if (slot < 0 || slot >= slots.Length || MainSkill == id)
                return;

            for (int k = 0; k < slots.Length; k++)
            {
                if (slots[k] == id)
                    slots[k] = null;
            }
            slots[slot] = id;
            Changed?.Invoke();
        }

        /// <summary>Cooldown at the skill's current level, after Cooldown Recovery (or the cast interval for the attack spell).</summary>
        public float Cooldown(SkillId id)
        {
            SkillDefinition skill = SkillBook.Get(id);
            float cooldown = skill.CooldownAt(Mathf.Max(1, Level(id)));
            if (skill.Main && MainSkill == id)
                return cooldown / (1f + Mathf.Max(-50f, Stat(StatType.CastSpeed)) / 100f);
            return cooldown / (1f + Mathf.Max(-50f, Stat(StatType.CooldownRecovery)) / 100f);
        }

        public float CooldownLeft(SkillId id)
        {
            return readyAt.TryGetValue(id, out float at) ? Mathf.Max(0f, at - Time.time) : 0f;
        }

        /// <summary>The full length of the cooldown now running (for the bar's sweep).</summary>
        public float CooldownTotal(SkillId id)
        {
            return cooldownOf.TryGetValue(id, out float total) ? total : Cooldown(id);
        }

        public float ManaCost(SkillId id)
        {
            return SkillBook.Get(id).ManaCostAt(Mathf.Max(1, Level(id)));
        }

        public bool CanAfford(SkillId id)
        {
            return stats != null && stats.CurrentMana >= ManaCost(id);
        }

        // Newly granted skills go into the first free slot (or one holding a skill no longer
        // granted), so nobody has to open the skills panel to get going.
        private void FillEmptySlots(bool announce)
        {
            SkillId? main = MainSkill;
            bool changed = false;
            foreach (SkillDefinition skill in SkillBook.All)
            {
                if (skill.Id == main)
                {
                    // The attack has no bar slot.
                    int at = Array.IndexOf(slots, (SkillId?)skill.Id);
                    if (at >= 0)
                    {
                        slots[at] = null;
                        changed = true;
                    }
                    continue;
                }
                if (!IsUnlocked(skill.Id) || Array.IndexOf(slots, (SkillId?)skill.Id) >= 0)
                    continue;

                int free = Array.IndexOf(slots, null);
                if (free < 0)
                {
                    for (int k = 0; k < slots.Length && free < 0; k++)
                    {
                        if (!IsUnlocked(slots[k].Value))
                            free = k;
                    }
                }
                if (free < 0)
                    continue;

                slots[free] = skill.Id;
                changed = true;
                if (announce)
                    CombatText.Show(transform.position + Vector3.up * 2.2f, skill.Name + " (" + KeyLabel(free) + ")", skill.Color, 1.1f);
            }

            if (changed || announce)
                Changed?.Invoke();
        }

        // ------------------------------------------------------------------ input

        private void Update()
        {
            if (stats == null || stats.IsDead)
            {
                VirtualInput.SkillPressed = -1;
                return;
            }

            int pressed = VirtualInput.SkillPressed;
            VirtualInput.SkillPressed = -1;

            bool free = !UiKit.IsTypingInTextField() && !PlayerController.IsUiFocused();
            Keyboard keyboard = Keyboard.current;
            if (pressed < 0 && keyboard != null && free)
            {
                for (int k = 0; k < SlotKeys.Length; k++)
                {
                    if (keyboard[SlotKeys[k]].wasPressedThisFrame)
                        pressed = k;
                }
            }

            // The spare mouse buttons (the left one attacks). Not over a window, and not on touch,
            // where browsers turn taps into mouse clicks.
            Mouse mouse = Mouse.current;
            if (pressed < 0 && mouse != null && free && !TouchMode.Active && !PlayerController.IsPointerOverUi() && !DialogueUI.IsOpen)
            {
                if (mouse.rightButton.wasPressedThisFrame) pressed = 4;
                else if (mouse.middleButton.wasPressedThisFrame) pressed = 5;
                else if (mouse.backButton.wasPressedThisFrame) pressed = 6;
                else if (mouse.forwardButton.wasPressedThisFrame) pressed = 7;
            }

            if (pressed >= 0)
                TryUse(pressed);
        }

        /// <summary>Uses the skill in a slot if gear grants it and it's ready and affordable.</summary>
        public bool TryUse(int slot)
        {
            SkillId? id = Slot(slot);
            if (id == null)
                return false;

            SkillDefinition skill = SkillBook.Get(id.Value);
            int level = Level(skill.Id);
            if (level <= 0 || MainSkill == skill.Id)
            {
                CombatText.Show(transform.position + Vector3.up * 2f, skill.Name + ": not on your gear", UiKit.DimText, 0.8f);
                return false;
            }
            if (CooldownLeft(skill.Id) > 0f || controller.IsDashing)
                return false;

            // Swinging skills wait for the current swing; Dash and Rejuvenate can cut in.
            bool usesArms = skill.Id != SkillId.Dash && skill.Id != SkillId.Rejuvenate;
            if (usesArms && attackAnimator != null && attackAnimator.IsAttacking)
                return false;

            // Cleave is a melee swing: it can't be done with a bow in hand.
            if (skill.Id == SkillId.Cleave && CharacterAttackAnimator.IsRanged(CurrentWeapon()))
            {
                CombatText.Show(transform.position + Vector3.up * 2f, "Needs a melee weapon", CombatText.PhysicalColor, 0.8f);
                return false;
            }

            if (!stats.TrySpendMana(ManaCost(skill.Id)))
            {
                CombatText.Show(transform.position + Vector3.up * 2f, "Not enough mana", CombatText.ColdColor, 0.8f);
                return false;
            }

            StartCooldown(skill.Id);
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
            if (!stats.TrySpendMana(ManaCost(main.Value)))
                return false;
            StartCooldown(main.Value);
            return true;
        }

        /// <summary>Seconds between casts of the attack spell (its level and Cast Speed shorten it).</summary>
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
            Cast(SkillBook.Get(main.Value), Mathf.Max(1, Level(main.Value)), asAttack: true);
        }

        /// <summary>How far the attack spell reaches (for PlayerCombat's aim highlight).</summary>
        public float MainReach()
        {
            SkillId? main = MainSkill;
            if (main == SkillId.ChainLightning)
                return ChainReach;
            return main == SkillId.IceShard ? ShardRange : BoltRange;
        }

        // ------------------------------------------------------------------ the skills

        private const float BoltRange = 16f;
        private const float ShardRange = 12f;

        // asAttack: cast as the staff's attack, already facing where it should go (PlayerCombat
        // turned the player at the moment of release); from the bar it aims itself.
        private void Cast(SkillDefinition skill, int level, bool asAttack = false)
        {
            float spell = SkillBook.SpellMultiplier(stats.Intelligence) * (1f + Stat(StatType.SpellDamage) / 100f);
            float area = DefenceMath.RadiusMultiplier(Stat(StatType.AreaOfEffect));
            float damage = skill.DamageAt(level) * spell;
            int extraProjectiles = Mathf.Max(0, Mathf.RoundToInt(Stat(StatType.AdditionalSpellProjectiles)));

            switch (skill.Id)
            {
                case SkillId.Cleave:
                    StartCoroutine(Spin(skill, level));
                    break;

                case SkillId.FireBolt:
                    if (!asAttack)
                    {
                        Face(AimDirection());
                        if (attackAnimator != null)
                            attackAnimator.PlayAttack(WeaponType.Unarmed);
                    }
                    foreach (Vector3 direction in HitEffects.Spread(transform.forward, 1 + extraProjectiles, 12f))
                        PlayerArrow.LaunchBolt(transform, BoltRange, damage, skill.Color, 2.2f * area, direction);
                    Record(skill, level, 2.2f * area, 1 + extraProjectiles);
                    break;

                case SkillId.IceShard:
                    if (!asAttack)
                    {
                        Face(AimDirection());
                        if (attackAnimator != null)
                            attackAnimator.PlayAttack(WeaponType.Unarmed);
                    }
                    foreach (Vector3 direction in HitEffects.Spread(transform.forward, 3 + extraProjectiles, 7f))
                        PlayerArrow.LaunchShard(transform, ShardRange, damage * 0.45f, skill.Color, 1.5f + 0.1f * level, direction);
                    Record(skill, level, 0f, 3 + extraProjectiles);
                    break;

                case SkillId.Dash:
                    Vector3 dir = controller.InputDirection();
                    if (dir.sqrMagnitude < 0.01f)
                        dir = AimDirection();
                    SkillEffects.Shockwave(transform.position, 1.2f, skill.Color, 0.25f);
                    Record(skill, level);
                    controller.Dash(dir, 7f + 0.35f * (level - 1), 0.18f);
                    break;

                case SkillId.FrostNova:
                    float novaRadius = 5f * (1f + 0.03f * (level - 1)) * area;
                    SkillEffects.Shockwave(transform.position, novaRadius, skill.Color, 0.4f);
                    Record(skill, level, novaRadius);
                    foreach (EnemyHealth enemy in EnemiesWithin(transform.position, novaRadius))
                    {
                        Hit(enemy, damage, CombatText.ColdColor, attack: false);
                        EnemyController ai = enemy.GetComponent<EnemyController>();
                        if (ai != null)
                            ai.Chill(3f + 0.2f * (level - 1));
                    }
                    break;

                case SkillId.Rejuvenate:
                    stats.HealOverTime(stats.MaxHealth * (0.35f + 0.025f * (level - 1)), 3f);
                    SkillEffects.Rise(transform, skill.Color);
                    Record(skill, level);
                    break;

                case SkillId.ChainLightning:
                    ChainLightning(skill, damage, level, asAttack);
                    break;
            }
        }

        // A full turn on the spot, then everything in reach takes the blow.
        private IEnumerator Spin(SkillDefinition skill, int level)
        {
            if (attackAnimator != null)
                attackAnimator.PlayAttack(WeaponType.Axe);

            const float seconds = 0.28f;
            float startYaw = transform.eulerAngles.y;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                transform.rotation = Quaternion.Euler(0f, startYaw + 360f * (t / seconds), 0f);
                yield return null;
            }
            transform.rotation = Quaternion.Euler(0f, startYaw, 0f);

            WeaponType weapon = CurrentWeapon();
            float melee = 1f + Mathf.Max(0f, Stat(StatType.MeleeRange)) / 100f;
            float reach = Mathf.Max(2.6f, CharacterAttackAnimator.IsRanged(weapon) ? 2.6f : (CharacterAttackAnimator.AttackRange(weapon) + 0.6f) * melee) *
                          DefenceMath.RadiusMultiplier(Stat(StatType.AreaOfEffect));
            SkillEffects.Shockwave(transform.position, reach, skill.Color, 0.3f);
            Record(skill, level, reach);

            float damage = WeaponDamage() * (1.4f + 0.1f * (level - 1));
            foreach (EnemyHealth enemy in EnemiesWithin(transform.position, reach))
                Hit(enemy, damage, CombatText.PhysicalColor, attack: true);
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

            EnemyHealth target = AimedEnemy(ChainReach);
            if (target == null)
            {
                target = Nearest(transform.position, ChainReach, struck);
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

            int jumps = 3 + (level >= 5 ? 1 : 0) + (level >= 9 ? 1 : 0) + Mathf.Max(0, Mathf.RoundToInt(Stat(StatType.AdditionalChains)));
            var arc = new List<Vector3> { from };
            for (int jump = 0; jump < jumps && target != null; jump++)
            {
                Vector3 to = target.transform.position + Vector3.up * 0.4f * target.transform.localScale.y;
                SkillEffects.Arc(from, to, skill.Color);
                arc.Add(to);
                Hit(target, damage, CombatText.LightningColor, attack: false);
                struck.Add(target);

                damage *= 0.7f;
                from = to;
                target = Nearest(target.transform.position, ChainJump, struck);
            }
            Record(skill, level, 0f, 0, arc.ToArray());
        }

        private void Hit(EnemyHealth enemy, float damage, Color color, bool attack)
        {
            HitEffects.Deal(transform, enemy, damage, attack, color);
        }

        private float Stat(StatType stat)
        {
            return inventory != null && inventory.Stats != null ? inventory.Stats.Total(stat) : 0f;
        }

        // ------------------------------------------------------------------ helpers

        private WeaponType CurrentWeapon()
        {
            ItemData weapon = inventory != null ? inventory.Equipment.Get(EquipSlot.MainHand) : null;
            return weapon != null ? weapon.WeaponType : WeaponType.Unarmed;
        }

        // The same damage a basic attack does (weapon + Strength), see PlayerCombat.
        private float WeaponDamage()
        {
            float physical = inventory != null ? inventory.Stats.Total(StatType.PhysicalDamage) : 2f;
            return physical * (1f + stats.Strength / 100f);
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
            float bestSq = reach * reach;
            foreach (EnemyHealth enemy in EnemiesWithin(transform.position, reach))
            {
                Vector3 toEnemy = enemy.transform.position - transform.position;
                toEnemy.y = 0f;
                if (Vector3.Angle(direction, toEnemy) > AimConeHalfAngle)
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

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
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

        private bool Within(EnemyHealth enemy, float reach)
        {
            Vector3 d = enemy.transform.position - transform.position;
            d.y = 0f;
            return d.sqrMagnitude <= reach * reach;
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

            Mouse mouse = Mouse.current;
            Camera cam = Camera.main;
            if (mouse != null && cam != null)
            {
                Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
                if (new Plane(Vector3.up, transform.position).Raycast(ray, out float enter))
                    return Flat(ray.GetPoint(enter) - transform.position);
            }
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
