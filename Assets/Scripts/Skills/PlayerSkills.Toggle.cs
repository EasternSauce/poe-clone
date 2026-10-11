using UnityEngine;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.UI;

namespace PoeClone.Skills
{
    /// <summary>
    /// Toggled skills (<see cref="SkillDefinition.Toggled"/>): the bow skills (PlayerSkills.Bow.cs).
    /// They sit on the bar like any skill, but pressing one toggles it instead of using it. One can
    /// be on at a time (turning another on swaps them; pressing it again turns it off). While one is
    /// on and a bow is in hand, the bow's attack (left click / the aim stick) is that skill, paid
    /// per use; without the mana for it the plain shot is used until the mana is back. A melee
    /// weapon's skill is never toggled: it is always the weapon's attack (see PlayerSkills.Melee.cs).
    /// </summary>
    public partial class PlayerSkills
    {
        private SkillId? toggled;
        private int toggledSlot = -1;

        public int SavedToggleSlot => toggledSlot + 1;
        public int SavedToggleSkill => toggled.HasValue ? (int)toggled.Value + 1 : 0;

        /// <summary>Restore only after this character's equipment and bindings have loaded.</summary>
        public void RestoreToggle(int savedSlot, int savedSkill)
        {
            toggled = savedSkill > 0 ? (SkillId?)(SkillId)(savedSkill - 1) : null;
            toggledSlot = savedSlot - 1;
            if (toggled.HasValue && !SkillBook.Get(toggled.Value).Toggled)
                toggled = null;
            CheckToggle();
            if (!toggled.HasValue) toggledSlot = -1;
            Changed?.Invoke();
        }

        /// <summary>The bow skill the bow shoots right now (on, granted, and a bow in hand), or null.</summary>
        public SkillId? ActiveBowSkill
        {
            get
            {
                if (toggled == null || toggledSlot < 0 || Slot(toggledSlot) != toggled || LevelAt(toggledSlot) <= 0)
                    return null;
                return CurrentWeapon() == WeaponType.Bow ? toggled : null;
            }
        }

        /// <summary>The skill the weapon attacks with right now: the melee weapon's skill or the bow skill that's on, or null.</summary>
        public SkillId? ActiveAttackSkill => ActiveMeleeSkill ?? ActiveBowSkill;

        /// <summary>Whether this skill is toggled on (for the bar's spinning ring).</summary>
        public bool IsToggledOn(SkillId id) => ActiveBowSkill == id;

        public bool IsToggledOnAt(int slot) => ActiveBowSkill != null && toggledSlot == slot;
        public int ActiveAttackLevel => ActiveMeleeSkill is SkillId melee ? Level(melee) : ActiveBowSkill != null ? LevelAt(toggledSlot) : 0;
        public bool CanAffordAttackSkill => ActiveAttackSkill != null && stats != null &&
            stats.CanAffordSkill(CostOf(ActiveAttackSkill.Value, ActiveAttackLevel));
        public bool TrySpendAttackSkill(SkillId id, int level) => stats != null && stats.TrySpendMana(CostOf(id, level));

        private bool Toggle(SkillDefinition skill, int slot)
        {
            Vector3 above = transform.position + Vector3.up * 2.2f;
            if (toggledSlot == slot && toggled == skill.Id)
            {
                toggled = null;
                toggledSlot = -1;
                CombatText.Show(above, skill.Name + " off", UiKit.DimText, 0.8f);
                Changed?.Invoke();
                SaveSystem.SaveCharacterNow();
                return true;
            }
            if (CurrentWeapon() != WeaponType.Bow)
            {
                CombatText.Show(above, "Needs a bow", CombatText.PhysicalColor, 0.8f);
                return false;
            }

            toggled = skill.Id;
            toggledSlot = slot;
            SkillEffects.Shockwave(transform.position, 1.4f, skill.Color, 0.3f, type: SkillEffects.DamagePattern(skill));
            CombatText.Show(above, skill.Name, skill.Color, 0.9f);
            Changed?.Invoke();
            SaveSystem.SaveCharacterNow();
            return true;
        }

        // Gear changed: a toggled skill that's no longer granted, or no bow in hand, switches off.
        private void CheckToggle()
        {
            if (toggled != null && ActiveBowSkill == null)
            {
                toggled = null;
                toggledSlot = -1;
            }
        }
    }
}
