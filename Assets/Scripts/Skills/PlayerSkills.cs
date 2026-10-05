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
    /// The player's skills (see <see cref="SkillBook"/>). Skills come from worn gear, each at a
    /// level (the highest any worn item grants, plus "+N to level" gear). A staff's first spell is
    /// its attack: <see cref="PlayerCombat"/> casts it on left click / the aim stick instead of
    /// swinging (<see cref="MainSkill"/>, <see cref="TrySpendMain"/>, <see cref="ReleaseMain"/>).
    /// Every other skill goes in a bar slot: Q, E, R, F, then the right, middle, back and forward
    /// mouse buttons (touch has round buttons for the first four). A skill that new gear grants
    /// drops into the first free slot; the skills panel (K) moves them around. Each costs mana and
    /// has a cooldown, except bow skills, which are toggled (see PlayerSkills.Bow.cs). Aiming works like attacks: the mouse on desktop, <see cref="VirtualInput.Aim"/>
    /// on touch. Self-added by <see cref="PlayerController"/>.
    /// </summary>
    public partial class PlayerSkills : MonoBehaviour
    {
        private static readonly string[] SlotLabels = { "Q", "E", "R", "F", "RMB", "MMB", "M4", "M5", "1", "2", "3", "4" };
        private static readonly Key[] SlotKeys = { Key.Q, Key.E, Key.R, Key.F, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4 };

        private readonly SkillId?[] slots = new SkillId?[SkillBook.SlotCount];
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
            if (cast.Skill == CorpseExplosionCast || cast.Skill == ShatterCast)
            {
                HitEffects.PlayBurstVisual(cast.At, cast.Size, cast.Skill == ShatterCast);
                return;
            }

            SkillDefinition skill = SkillBook.Get(cast.Skill);
            PlaySkillSound(cast.Skill, caster.position);
            Vector3 facing = cast.Facing;
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.0001f)
                facing = caster.forward;
            facing.Normalize();

            if (skill.Bow)
            {
                PlayBowVisual(cast, skill, caster, facing);
                return;
            }

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

                case SkillId.Teleport:
                    SkillEffects.Shockwave(cast.At, 1.4f, skill.Color, 0.3f);
                    if (cast.Points != null && cast.Points.Length > 0)
                        SkillEffects.Shockwave(cast.Points[0], 1.6f, skill.Color, 0.35f);
                    break;

                case SkillId.FrostNova:
                case SkillId.Cleave:
                case SkillId.Pulverize:
                    SkillEffects.Shockwave(cast.At, cast.Size, skill.Color, cast.Skill == SkillId.Cleave ? 0.3f : 0.4f);
                    break;

                case SkillId.ReapingArc:
                    SkillEffects.Shockwave(cast.At + facing * 1.8f, 2.8f, skill.Color, 0.34f);
                    break;

                case SkillId.LungingThrust:
                    SkillEffects.Arc(cast.At + Vector3.up, cast.At + Vector3.up + facing * cast.Size, skill.Color, 0.25f);
                    break;

                case SkillId.FangStrike:
                    SkillEffects.Arc(cast.At + Vector3.up * 0.8f, cast.At + Vector3.up * 0.8f + facing * cast.Size, skill.Color, 0.18f);
                    break;

                case SkillId.Rejuvenate:
                    SkillEffects.Rise(caster, skill.Color);
                    break;

                case SkillId.RaiseSkeletons:
                case SkillId.SkeletonMages:
                case SkillId.SpiritWolves:
                case SkillId.BoneGolem:
                case SkillId.SummonViper:
                    SkillEffects.Shockwave(cast.At, 1.6f, skill.Color, 0.4f);
                    break;

                case SkillId.GraveRot:
                    if (cast.Points != null && cast.Points.Length > 0)
                        SkillEffects.Shockwave(cast.Points[0], cast.Size, skill.Color, 0.5f);
                    break;

                case SkillId.DeathMark:
                    if (cast.Points != null && cast.Points.Length > 1)
                    {
                        SkillEffects.Arc(cast.Points[0], cast.Points[1], skill.Color, 0.25f);
                        SkillEffects.Shockwave(cast.Points[1], 1.1f, skill.Color, 0.3f);
                    }
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

        private void OnPlayerDied()
        {
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
                // Summons are built for their current summoner loadout. A weapon swap must not
                // let the player keep those minions while changing to a different damage setup.
                if (boundEquipment != null && (slot == EquipSlot.OffHand || boundMainHand != item))
                    Minion.Desummon(transform);
            }
            if (slot == EquipSlot.MainHand)
            {
                boundMainHand = item;
            }
            CheckBowToggle();
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
            if (skill.Summon)
            {
                // Summons only grow with summon levels (never "+N to level of all Spells").
                bonus += Mathf.RoundToInt(Stat(StatType.MinionLevels));
                StatType? own = SkillGrants.SummonLevelStat(skill.Grant);
                if (own.HasValue)
                    bonus += Mathf.RoundToInt(Stat(own.Value));
            }
            else if (skill.Spell)
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
        /// The attack spell: the first spell on the staff that can be one, or a grimoire's Death
        /// Mark (in the off hand, with a one-handed weapon or none). Null without either (the
        /// plain weapon attack is used).
        /// </summary>
        public SkillId? MainSkill
        {
            get
            {
                if (inventory == null)
                    return null;
                SkillId? main = MainOn(inventory.Equipment.Get(EquipSlot.MainHand));
                if (main != null)
                    return main;
                ItemData offHand = inventory.Equipment.Get(EquipSlot.OffHand);
                return offHand != null && offHand.Type == ItemType.Grimoire ? MainOn(offHand) : null;
            }
        }

        private static SkillId? MainOn(ItemData item)
        {
            if (item == null)
                return null;
            foreach (StatModifier m in item.Modifiers)
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

            int binding = slot < 4 ? slot + 4 : slot < 8 ? slot + 4 : slot - 8;
            if (enabled) PlayerPotions.ClearBindingAt(binding);

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
            if (skill.Bow)
                return 0f;
            float cooldown = skill.CooldownAt(Mathf.Max(1, Level(id)));
            float onslaught = controller != null && controller.HasOnslaught ? PlayerController.OnslaughtMore : 1f;
            if (skill.Main && MainSkill == id)
                return cooldown / ((1f + Mathf.Max(-50f, Stat(StatType.CastSpeed)) / 100f) * onslaught);
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

                int free = -1;
                for (int k = 0; k < slots.Length; k++)
                {
                    int binding = k < 4 ? k + 4 : k < 8 ? k + 4 : k - 8;
                    if (slots[k] == null && !PlayerPotions.IsBoundTo(binding)) { free = k; break; }
                }
                if (free < 0)
                {
                    for (int k = 0; k < slots.Length && free < 0; k++)
                    {
                        int binding = k < 4 ? k + 4 : k < 8 ? k + 4 : k - 8;
                        if (!PlayerPotions.IsBoundTo(binding) && slots[k] != null && !IsUnlocked(slots[k].Value))
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
                    int slot = k < 4 ? k : k + 4;
                    int binding = k < 4 ? k + 4 : k - 4;
                    if (!PlayerPotions.IsBoundTo(binding) && keyboard[SlotKeys[k]].wasPressedThisFrame)
                        pressed = slot;
                }
            }

            // The spare mouse buttons (the left one attacks). Not over a window, and not on touch,
            // where browsers turn taps into mouse clicks.
            Mouse mouse = Mouse.current;
            if (pressed < 0 && mouse != null && free && !TouchMode.Active && !PlayerController.IsPointerOverUi() && !DialogueUI.IsOpen)
            {
                if (!PlayerPotions.IsBoundTo(8) && mouse.rightButton.wasPressedThisFrame) pressed = 4;
                else if (!PlayerPotions.IsBoundTo(9) && mouse.middleButton.wasPressedThisFrame) pressed = 5;
                else if (!PlayerPotions.IsBoundTo(10) && mouse.backButton.wasPressedThisFrame) pressed = 6;
                else if (!PlayerPotions.IsBoundTo(11) && mouse.forwardButton.wasPressedThisFrame) pressed = 7;
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
            if (skill.Bow)
                return ToggleBow(skill);
            if (CooldownLeft(skill.Id) > 0f || controller.IsDashing || controller.IsSkillCommitted)
                return false;

            // Swinging skills wait for the current swing; Dash and Rejuvenate can cut in.
            bool usesArms = skill.Id != SkillId.Dash && skill.Id != SkillId.Rejuvenate;
            if (usesArms && attackAnimator != null && attackAnimator.IsAttacking)
                return false;

            // Melee skills belong to a weapon family; never let a bow or another weapon use them.
            if (!CanUseWithWeapon(skill.Id, CurrentWeapon()))
            {
                CombatText.Show(transform.position + Vector3.up * 2f, RequiredWeaponText(skill.Id), CombatText.PhysicalColor, 0.8f);
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
            if (main == SkillId.DeathMark)
                return MarkReach;
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
                case SkillId.Pulverize:
                    StartCoroutine(CommittedStrike(skill.Id, 0.72f, CurrentWeapon(), () => Pulverize(skill, level)));
                    break;
                case SkillId.ReapingArc:
                    StartCoroutine(CommittedStrike(skill.Id, 0.58f, CurrentWeapon(), () => ReapingArc(skill, level)));
                    break;
                case SkillId.LungingThrust:
                    StartCoroutine(Lunge(skill, level));
                    break;

                case SkillId.FireBolt:
                    if (!asAttack)
                    {
                        Face(AimDirection());
                        if (attackAnimator != null)
                            attackAnimator.PlayAttack(WeaponType.Unarmed);
                    }
                    var bolts = PlayerArrow.NewVolley();
                    foreach (Vector3 direction in HitEffects.Spread(transform.forward, 1 + extraProjectiles, 12f))
                        PlayerArrow.LaunchBolt(transform, BoltRange, damage, skill.Color, 1.6f * area, direction, volley: bolts);
                    Record(skill, level, 1.6f * area, 1 + extraProjectiles);
                    break;

                case SkillId.IceShard:
                    if (!asAttack)
                    {
                        Face(AimDirection());
                        if (attackAnimator != null)
                            attackAnimator.PlayAttack(WeaponType.Unarmed);
                    }
                    // Each enemy is hurt by one shard of the fan: the spread covers more of them,
                    // it doesn't stack on one.
                    var shards = PlayerArrow.NewVolley();
                    foreach (Vector3 direction in HitEffects.Spread(transform.forward, 3 + extraProjectiles, 7f))
                        PlayerArrow.LaunchShard(transform, ShardRange, damage * 0.8f, skill.Color, 1.5f + 0.1f * level, direction, volley: shards);
                    Record(skill, level, 0f, 3 + extraProjectiles);
                    break;

                case SkillId.Dash:
                    Vector3 dir = TouchMode.Active ? controller.InputDirection() : AimDirection();
                    if (dir.sqrMagnitude < 0.01f)
                        dir = AimDirection();
                    SkillEffects.Shockwave(transform.position, 1.2f, skill.Color, 0.25f);
                    Record(skill, level);
                    controller.Dash(dir, 7f + 0.35f * (level - 1), 0.18f);
                    if (Stat(StatType.GlacialStep) > 0f)
                        StartCoroutine(GlacialStep(0.18f, level, spell, area));
                    break;

                case SkillId.FrostNova:
                    float novaRadius = 5f * (1f + 0.03f * (level - 1)) * area;
                    SkillEffects.Shockwave(transform.position, novaRadius, skill.Color, 0.4f);
                    Record(skill, level, novaRadius);
                    foreach (EnemyHealth enemy in EnemiesWithin(transform.position, novaRadius))
                    {
                        EnemyController ai = enemy.GetComponent<EnemyController>();
                        if (ai != null)
                            ai.Chill(3f + 0.2f * (level - 1));
                        Hit(enemy, damage, CombatText.ColdColor, attack: false, DamageType.Cold);
                    }
                    break;

                case SkillId.Rejuvenate:
                    stats.HealOverTime(stats.MaxHealth * (0.35f + 0.025f * (level - 1)), 3f);
                    SkillEffects.Rise(transform, skill.Color);
                    if (Stat(StatType.SecondWind) > 0f)
                    {
                        stats.RestoreMana(stats.MaxMana / 3f);
                        controller.GrantOnslaught(HitEffects.OnslaughtSeconds + 2f);
                    }
                    Record(skill, level);
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
                    if (attackAnimator != null)
                        attackAnimator.PlayAttack(WeaponType.Unarmed);
                    Minion.Summon(transform, MinionKindOf(skill.Id), level);
                    Record(skill, level, 1.3f);
                    break;

                case SkillId.DeathMark:
                    DeathMark(skill, damage, level, asAttack);
                    break;

                case SkillId.GraveRot:
                    GraveRot(skill, level, area);
                    break;

                case SkillId.FangStrike:
                    StartCoroutine(CommittedStrike(skill.Id, 0.12f, WeaponType.Dagger, () => FangStrike(skill, level)));
                    break;
                case SkillId.VenomSpout:
                {
                    Vector3 direction = AimDirection();
                    EnemyHealth aimed = AimedEnemy(12f) ?? (TouchMode.Active ? EnemyInDirection(direction, 12f) : null);
                    Vector3 at = aimed != null ? aimed.transform.position : transform.position + direction * Mathf.Clamp(AimDistance() ?? 7f, 0f, 12f);
                    float radius = 2.6f * area;
                    SkillEffects.Shockwave(at, radius, skill.Color, 0.45f);
                    Record(skill, level, radius, 0, new[] { at });
                    foreach (EnemyHealth target in EnemiesWithin(at, radius))
                    {
                        Hit(target, damage * (1f + 0.12f * (level - 1)), skill.Color, false, DamageType.Poison);
                        WeaponVenom.Apply(transform, target, damage, 100f + 10f * (level - 1), 0f, inventory.Stats);
                    }
                    break;
                }
            }
        }

        // A full turn on the spot, then everything in reach takes the blow.
        private IEnumerator Spin(SkillDefinition skill, int level)
        {
            controller.SetSkillCommit(true);
            Face(AimDirection());
            PlaySkillAnimation(skill.Id, CurrentWeapon());
            yield return new WaitForSeconds(0.65f);

            const float seconds = 0.26f;
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

            float damage = WeaponDamage() * (1.68f + 0.12f * (level - 1));
            PlaySkillSound(skill.Id, transform.position);
            foreach (EnemyHealth enemy in EnemiesWithin(transform.position, reach))
                Hit(enemy, damage, CombatText.PhysicalColor, attack: true);
            controller.SetSkillCommit(false);
        }

        private IEnumerator CommittedStrike(SkillId skill, float windup, WeaponType animationWeapon, Action release)
        {
            controller.SetSkillCommit(true);
            Face(AimDirection());
            PlaySkillAnimation(skill, animationWeapon);
            yield return new WaitForSeconds(windup);
            PlaySkillSound(skill, transform.position);
            release();
            yield return new WaitForSeconds(0.18f);
            controller.SetSkillCommit(false);
        }

        private IEnumerator Lunge(SkillDefinition skill, int level)
        {
            controller.SetSkillCommit(true);
            Face(AimDirection());
            PlaySkillAnimation(skill.Id, CurrentWeapon());
            yield return new WaitForSeconds(0.34f);
            controller.Dash(transform.forward, 2.4f, 0.16f);
            yield return new WaitForSeconds(0.16f);
            PlaySkillSound(skill.Id, transform.position);
            LungingThrust(skill, level);
            yield return new WaitForSeconds(0.16f);
            controller.SetSkillCommit(false);
        }

        private void PlaySkillAnimation(SkillId skill, WeaponType weapon)
        {
            if (attackAnimator != null)
                attackAnimator.PlaySkillAttack(skill, weapon);
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
                case SkillId.FangStrike: sound = "skill_fang_strike"; break;
                default: return;
            }

            PoeClone.Audio.AudioManager audio = PoeClone.Audio.AudioManager.Instance;
            if (audio != null)
                audio.PlayAtPoint(audio.Sfx(sound), at, 0.75f);
        }

        private static bool CanUseWithWeapon(SkillId id, WeaponType weapon)
        {
            switch (id)
            {
                case SkillId.Cleave: return !CharacterAttackAnimator.IsRanged(weapon);
                case SkillId.Pulverize: return weapon == WeaponType.Mace || weapon == WeaponType.Maul;
                case SkillId.ReapingArc: return weapon == WeaponType.Axe || weapon == WeaponType.Greataxe;
                case SkillId.LungingThrust: return weapon == WeaponType.Sword || weapon == WeaponType.Greatsword;
                case SkillId.FangStrike: return weapon == WeaponType.Dagger;
                default: return true;
            }
        }

        private static string RequiredWeaponText(SkillId id)
        {
            switch (id)
            {
                case SkillId.Pulverize: return "Needs a mace or maul";
                case SkillId.ReapingArc: return "Needs an axe";
                case SkillId.LungingThrust: return "Needs a sword";
                case SkillId.FangStrike: return "Needs a dagger";
                default: return "Needs a melee weapon";
            }
        }

        private void Pulverize(SkillDefinition skill, int level)
        {
            float radius = 3.6f * DefenceMath.RadiusMultiplier(Stat(StatType.AreaOfEffect));
            SkillEffects.Shockwave(transform.position, radius, skill.Color, 0.42f);
            // Match the impact treatment of a basic maul slam: ground burst plus a brief camera jolt.
            PoeClone.CameraSystem.CameraFollow.Shake(0.12f, 0.18f);
            Record(skill, level, radius);
            float damage = WeaponDamage() * (3.12f + 0.216f * (level - 1));
            foreach (EnemyHealth enemy in EnemiesWithin(transform.position, radius))
                Hit(enemy, damage, CombatText.PhysicalColor, attack: true);
        }

        private void ReapingArc(SkillDefinition skill, int level)
        {
            float reach = 4.6f * (1f + Mathf.Max(0f, Stat(StatType.MeleeRange)) / 100f);
            float damage = WeaponDamage() * (2.64f + 0.18f * (level - 1));
            foreach (EnemyHealth enemy in EnemiesWithin(transform.position, reach))
            {
                Vector3 to = enemy.transform.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f && Vector3.Angle(transform.forward, to) <= 72f)
                    Hit(enemy, damage, CombatText.PhysicalColor, attack: true);
            }
            SkillEffects.Shockwave(transform.position + transform.forward * 1.8f, 2.8f, skill.Color, 0.34f);
            Record(skill, level, reach);
        }

        private void LungingThrust(SkillDefinition skill, int level)
        {
            float reach = 3f * (1f + Mathf.Max(0f, Stat(StatType.MeleeRange)) / 100f);
            float damage = WeaponDamage() * (2.88f + 0.192f * (level - 1));
            EnemyHealth target = AimedEnemy(reach) ?? EnemyInDirection(transform.forward, reach);
            SkillEffects.Arc(transform.position + Vector3.up, transform.position + Vector3.up + transform.forward * reach, skill.Color, 0.25f);
            if (target != null)
                Hit(target, damage, CombatText.PhysicalColor, attack: true);
            Record(skill, level, reach);
        }

        private void FangStrike(SkillDefinition skill, int level)
        {
            float reach = 2.4f * (1f + Mathf.Max(0f, Stat(StatType.MeleeRange)) / 100f);
            EnemyHealth target = AimedEnemy(reach) ?? EnemyInDirection(transform.forward, reach);
            Record(skill, level, reach);
            if (target != null)
            {
                Hit(target, WeaponDamage() * (1.56f + 0.096f * (level - 1)), skill.Color, true);
                WeaponVenom.Apply(transform, target, WeaponDamage() * 1.2f, 150f + 15f * (level - 1), 0f, inventory.Stats);
            }
        }

        /// <summary>
        /// A summon's minions as they'd come out now ("max 2 · 97 life · 19 per hit"), so the
        /// skills panel shows what summon levels and minion stats buy. Null for other skills.
        /// </summary>
        public string MinionSummary(SkillId id)
        {
            SkillDefinition skill = SkillBook.Get(id);
            int level = Level(id);
            if (!skill.Summon || level <= 0)
                return null;
            MinionKind kind = MinionKindOf(id);
            StatSheet sheet = inventory != null ? inventory.Stats : null;
            string summary = "army max " + Minion.GlobalCap(transform) + " · this skill max " + Minion.KindCap(kind, sheet) + " · " +
                             Mathf.RoundToInt(Minion.LifeFor(kind, level, sheet)) + " life · " +
                             Mathf.RoundToInt(Minion.ArmourFor(sheet)) + " armour · " +
                             Mathf.RoundToInt(Minion.ResistanceFor(sheet)) + "% elemental/poison res · " +
                             Mathf.RoundToInt(Minion.DamageFor(kind, level, sheet)) + " per hit";
            float duration = Minion.Duration(kind, level, sheet);
            if (duration > 0f)
                summary += " · " + Mathf.RoundToInt(duration) + "s";
            return summary;
        }

        private static MinionKind MinionKindOf(SkillId id)
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
            SkillEffects.Shockwave(at, radius, skill.Color, 0.5f);

            float seconds = 6f + 0.3f * (level - 1);
            float less = Mathf.Min(0.4f, 0.2f + 0.01f * (level - 1));
            float more = 0.15f + 0.015f * (level - 1);
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
            Hit(target, damage, skill.Color, attack: false);
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
                Hit(target, damage, CombatText.LightningColor, attack: false, DamageType.Lightning);
                struck.Add(target);

                damage *= 0.6f;
                from = to;
                target = Nearest(target.transform.position, ChainJump, struck);
            }
            Record(skill, level, 0f, 0, arc.ToArray());
        }

        private void Hit(EnemyHealth enemy, float damage, Color color, bool attack, DamageType type = DamageType.Physical)
        {
            HitEffects.Deal(transform, enemy, damage, attack, color, type);
        }

        // ------------------------------------------------------------------ teleport

        /// <summary>How far Teleport reaches: about half the screen, a little further each level.</summary>
        public static float TeleportRange(int level) => 9f + 0.3f * (Mathf.Max(1, level) - 1);

        // To the aimed point (or as far as it reaches that way), through anything in between. The
        // landing spot has to be open ground inside the current area: if it isn't, the jump comes
        // up short, back toward the player, until it is.
        private void Teleport(SkillDefinition skill, int level)
        {
            Vector3 start = transform.position;
            Vector3 direction = AimDirection();
            float distance = TeleportRange(level);
            float? aimed = AimDistance();
            if (aimed.HasValue)
                distance = Mathf.Clamp(aimed.Value, 1.5f, distance);

            Vector3? landing = null;
            for (float d = distance; d >= 1f; d -= 0.5f)
            {
                Vector3? spot = OpenGround(start + direction * d);
                if (spot.HasValue)
                {
                    landing = spot;
                    break;
                }
            }
            if (landing == null)
            {
                SkillEffects.Shockwave(start, 0.8f, skill.Color, 0.2f);
                Record(skill, level, 0f, 0, new[] { start });
                return;
            }

            SkillEffects.Shockwave(start, 1.4f, skill.Color, 0.3f);
            Face(direction);
            CharacterController body = GetComponent<CharacterController>();
            bool wasEnabled = body != null && body.enabled;
            if (body != null)
                body.enabled = false;
            transform.position = landing.Value;
            if (body != null)
                body.enabled = wasEnabled;
            Physics.SyncTransforms();
            SkillEffects.Shockwave(landing.Value, 1.6f, skill.Color, 0.35f);
            Record(skill, level, 0f, 0, new[] { landing.Value });
            if (Audio.AudioManager.Instance != null)
                Audio.AudioManager.Instance.PlayUI(Audio.AudioManager.Instance.uiItemPlace, 0.25f);
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
            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
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
            var areas = World.AreaManager.Instance;
            if (areas != null && areas.CurrentAreaIndex >= 0 &&
                !World.WorldBuilder.Shape(areas.CurrentAreaIndex).Contains(at, 2.5f))
                return null;

            float y = transform.position.y;
            float floor = GroundBelowPlayer();
            if (!Physics.Raycast(new Vector3(at.x, floor + 2f, at.z), Vector3.down, out RaycastHit ground, 3.5f, ~0, QueryTriggerInteraction.Ignore))
                return null;
            if (Mathf.Abs(ground.point.y - floor) > 1.2f || ground.normal.y < 0.7f)
                return null;

            CharacterController body = GetComponent<CharacterController>();
            float radius = body != null ? body.radius : 0.4f;
            float height = body != null ? body.height : 2f;
            Vector3 feet = new Vector3(at.x, ground.point.y, at.z);
            Vector3 bottom = feet + Vector3.up * (radius + 0.15f);
            Vector3 top = feet + Vector3.up * Mathf.Max(radius + 0.2f, height - radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, landingBuffer, ~0, QueryTriggerInteraction.Ignore);
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

        // Glacial Step: where the Dash ends, a Frost Nova (a smaller one, scaled by the Dash's level).
        private IEnumerator GlacialStep(float after, int level, float spell, float area)
        {
            yield return new WaitForSeconds(after);
            float radius = 3.5f * area;
            Color cold = CombatText.ColdColor;
            SkillEffects.Shockwave(transform.position, radius, cold, 0.4f);
            float damage = (4f + 1.2f * (level - 1)) * spell;
            foreach (EnemyHealth enemy in EnemiesWithin(transform.position, radius))
            {
                EnemyController ai = enemy.GetComponent<EnemyController>();
                if (ai != null)
                    ai.Chill(2.5f);
                Hit(enemy, damage, cold, attack: false, DamageType.Cold);
            }
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
