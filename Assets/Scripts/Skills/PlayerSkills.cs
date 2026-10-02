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
    /// The player's skills (see <see cref="SkillBook"/>): four bar slots used with Q, E, R and F
    /// (or the round touch buttons). Skills unlock as the character levels up and drop into the
    /// first free slot; the skills panel (K) moves them around. Each costs mana and has a cooldown.
    /// Aiming works like attacks: the mouse on desktop, the nearest enemy on touch.
    /// Self-added by <see cref="PlayerController"/>.
    /// </summary>
    public class PlayerSkills : MonoBehaviour
    {
        private static readonly Key[] SlotKeys = { Key.Q, Key.E, Key.R, Key.F };

        private readonly SkillId?[] slots = new SkillId?[SkillBook.SlotCount];
        private readonly Dictionary<SkillId, float> readyAt = new Dictionary<SkillId, float>();
        private readonly Collider[] buffer = new Collider[32];

        private PlayerStats stats;
        private PlayerController controller;
        private PlayerInventory inventory;
        private CharacterAttackAnimator attackAnimator;

        /// <summary>Slots or unlocks changed (the bars redraw).</summary>
        public event Action Changed;

        public static string KeyLabel(int slot)
        {
            return SlotKeys[slot].ToString();
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
            if (stats != null)
                stats.LeveledUp += OnLeveledUp;
            FillEmptySlots(announce: false);
        }

        private void OnDestroy()
        {
            if (stats != null)
                stats.LeveledUp -= OnLeveledUp;
        }

        // ------------------------------------------------------------------ slots

        public SkillId? Slot(int index)
        {
            return index >= 0 && index < slots.Length ? slots[index] : null;
        }

        public bool IsUnlocked(SkillId id)
        {
            return stats != null && stats.Level >= SkillBook.Get(id).UnlockLevel;
        }

        /// <summary>Empties a slot.</summary>
        public void ClearSlot(int slot)
        {
            if (slot < 0 || slot >= slots.Length || slots[slot] == null)
                return;
            slots[slot] = null;
            Changed?.Invoke();
        }

        /// <summary>Puts an unlocked skill in a slot (taking it out of any other slot).</summary>
        public void Assign(int slot, SkillId id)
        {
            if (slot < 0 || slot >= slots.Length || !IsUnlocked(id))
                return;

            for (int k = 0; k < slots.Length; k++)
            {
                if (slots[k] == id)
                    slots[k] = null;
            }
            slots[slot] = id;
            Changed?.Invoke();
        }

        public float CooldownLeft(SkillId id)
        {
            return readyAt.TryGetValue(id, out float at) ? Mathf.Max(0f, at - Time.time) : 0f;
        }

        public bool CanAfford(SkillId id)
        {
            return stats != null && stats.CurrentMana >= SkillBook.Get(id).ManaCost;
        }

        private void OnLeveledUp(int level)
        {
            FillEmptySlots(announce: true);
        }

        // Newly unlocked skills go into the first free slot, so a new player never has to open
        // the skills panel to get going.
        private void FillEmptySlots(bool announce)
        {
            bool changed = false;
            foreach (SkillDefinition skill in SkillBook.All)
            {
                if (!IsUnlocked(skill.Id) || Array.IndexOf(slots, (SkillId?)skill.Id) >= 0)
                    continue;

                bool justUnlocked = stats.Level == skill.UnlockLevel;
                if (announce && justUnlocked)
                    CombatText.Show(transform.position + Vector3.up * 2.2f, "New skill: " + skill.Name, skill.Color, 1.1f);

                int free = Array.IndexOf(slots, null);
                if (free < 0)
                    continue;
                slots[free] = skill.Id;
                changed = true;
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

            Keyboard keyboard = Keyboard.current;
            if (pressed < 0 && keyboard != null && !UiKit.IsTypingInTextField() && !PlayerController.IsUiFocused())
            {
                for (int k = 0; k < SlotKeys.Length; k++)
                {
                    if (keyboard[SlotKeys[k]].wasPressedThisFrame)
                        pressed = k;
                }
            }

            if (pressed >= 0)
                TryUse(pressed);
        }

        /// <summary>Uses the skill in a slot if it's ready and affordable.</summary>
        public bool TryUse(int slot)
        {
            SkillId? id = Slot(slot);
            if (id == null)
                return false;

            SkillDefinition skill = SkillBook.Get(id.Value);
            if (CooldownLeft(skill.Id) > 0f || controller.IsDashing)
                return false;

            // Swinging skills wait for the current swing; Dash and Rejuvenate can cut in.
            bool usesArms = skill.Id == SkillId.Cleave || skill.Id == SkillId.FireBolt || skill.Id == SkillId.ChainLightning;
            if (usesArms && attackAnimator != null && attackAnimator.IsAttacking)
                return false;

            if (!stats.TrySpendMana(skill.ManaCost))
            {
                CombatText.Show(transform.position + Vector3.up * 2f, "Not enough mana", CombatText.ColdColor, 0.8f);
                return false;
            }

            readyAt[skill.Id] = Time.time + skill.Cooldown;
            controller.CancelWalk();
            Cast(skill);
            return true;
        }

        // ------------------------------------------------------------------ the skills

        private void Cast(SkillDefinition skill)
        {
            float spell = SkillBook.SpellMultiplier(stats.Intelligence);
            int level = stats.Level;

            switch (skill.Id)
            {
                case SkillId.Cleave:
                    StartCoroutine(Spin(skill));
                    break;

                case SkillId.FireBolt:
                    Face(AimDirection(16f));
                    if (attackAnimator != null)
                        attackAnimator.PlayAttack(WeaponType.Unarmed);
                    PlayerArrow.LaunchBolt(transform, 16f, (10f + 4f * level) * spell, skill.Color, 2.2f);
                    break;

                case SkillId.Dash:
                    Vector3 dir = controller.InputDirection();
                    if (dir.sqrMagnitude < 0.01f)
                        dir = AimDirection(10f);
                    SkillEffects.Shockwave(transform.position, 1.2f, skill.Color, 0.25f);
                    controller.Dash(dir, 7f, 0.18f);
                    break;

                case SkillId.FrostNova:
                    const float novaRadius = 5f;
                    SkillEffects.Shockwave(transform.position, novaRadius, skill.Color, 0.4f);
                    foreach (EnemyHealth enemy in EnemiesWithin(transform.position, novaRadius))
                    {
                        Hit(enemy, (8f + 3f * level) * spell, CombatText.ColdColor);
                        EnemyController ai = enemy.GetComponent<EnemyController>();
                        if (ai != null)
                            ai.Chill(3f);
                    }
                    break;

                case SkillId.Rejuvenate:
                    stats.HealOverTime(stats.MaxHealth * 0.35f, 3f);
                    SkillEffects.Rise(transform, skill.Color);
                    break;

                case SkillId.ChainLightning:
                    ChainLightning(skill, (12f + 4f * level) * spell);
                    break;
            }
        }

        // A full turn on the spot, then everything in reach takes the blow.
        private IEnumerator Spin(SkillDefinition skill)
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
            float reach = Mathf.Max(2.6f, CharacterAttackAnimator.IsRanged(weapon) ? 2.6f : CharacterAttackAnimator.AttackRange(weapon) + 0.6f);
            SkillEffects.Shockwave(transform.position, reach, skill.Color, 0.3f);

            float damage = WeaponDamage() * 1.4f;
            foreach (EnemyHealth enemy in EnemiesWithin(transform.position, reach))
                Hit(enemy, damage, CombatText.PhysicalColor);
        }

        // How far Chain Lightning reaches for its first target, and how far each arc jumps.
        private const float ChainReach = 9f;
        private const float ChainJump = 5f;

        // Rewards aiming: a cast on an enemy under the cursor hits for full damage (more up close,
        // where the caster is in danger too); a blind cast at whatever is nearest hits for less.
        // Each arc is weaker than the one before.
        private void ChainLightning(SkillDefinition skill, float damage)
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
                return;
            }

            Face(target.transform.position - transform.position);
            if (attackAnimator != null)
                attackAnimator.PlayAttack(WeaponType.Unarmed);

            for (int jump = 0; jump < 3 && target != null; jump++)
            {
                Vector3 to = target.transform.position + Vector3.up * 0.4f * target.transform.localScale.y;
                SkillEffects.Arc(from, to, skill.Color);
                Hit(target, damage, CombatText.LightningColor);
                struck.Add(target);

                damage *= 0.7f;
                from = to;
                target = Nearest(target.transform.position, ChainJump, struck);
            }
        }

        private void Hit(EnemyHealth enemy, float damage, Color color)
        {
            enemy.TakeDamage(damage);
            CombatText.Show(enemy.transform.position + Vector3.up * 1.6f * enemy.transform.localScale.y,
                Mathf.Max(1, Mathf.RoundToInt(damage)).ToString(), color);
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

        // The living enemy drawn under the mouse (or within a short distance of the ground point
        // under it), within reach of the player. On touch: the nearest enemy, as aimed casts go.
        private EnemyHealth AimedEnemy(float reach)
        {
            if (TouchMode.Active)
                return Nearest(transform.position, reach, new HashSet<EnemyHealth>());

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

        // Where a cast should go: the mouse on desktop; on touch the nearest enemy in range, else
        // the joystick direction, else straight ahead.
        private Vector3 AimDirection(float range)
        {
            if (TouchMode.Active)
            {
                EnemyHealth nearest = Nearest(transform.position, range, new HashSet<EnemyHealth>());
                if (nearest != null)
                    return Flat(nearest.transform.position - transform.position);

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
