using UnityEngine;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.UI;

namespace PoeClone.Skills
{
    /// <summary>
    /// Toggled skills (<see cref="SkillDefinition.Toggled"/>): bow skills (PlayerSkills.Bow.cs) and
    /// melee skills (PlayerSkills.Melee.cs). They sit on the bar like any skill, but pressing one
    /// toggles it instead of using it. One can be on at a time (turning another on swaps them;
    /// pressing it again turns it off). While one is on and the weapon in hand can use it, the
    /// weapon's attack (left click / the aim stick) is that skill, paid per use; without the mana
    /// for it the plain attack is used until the mana is back.
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

        /// <summary>The toggled skill the weapon attacks with right now (on, granted, and usable with the weapon in hand), or null.</summary>
        public SkillId? ActiveAttackSkill
        {
            get
            {
                if (toggled == null || toggledSlot < 0 || Slot(toggledSlot) != toggled || LevelAt(toggledSlot) <= 0)
                    return null;
                return UsableWithWeapon(SkillBook.Get(toggled.Value)) ? toggled : null;
            }
        }

        /// <summary>The bow skill the bow shoots right now, or null.</summary>
        public SkillId? ActiveBowSkill => ActiveAttackSkill is SkillId id && SkillBook.Get(id).Bow ? id : (SkillId?)null;

        /// <summary>The melee skill the weapon swings right now, or null.</summary>
        public SkillId? ActiveMeleeSkill => ActiveAttackSkill is SkillId id && !SkillBook.Get(id).Bow ? id : (SkillId?)null;

        /// <summary>Whether this skill is toggled on (for the bar's spinning ring).</summary>
        public bool IsToggledOn(SkillId id) => ActiveAttackSkill == id;

        public bool IsToggledOnAt(int slot) => ActiveAttackSkill != null && toggledSlot == slot;
        public int ActiveAttackLevel => ActiveAttackSkill != null ? LevelAt(toggledSlot) : 0;
        public bool CanAffordAttackSkill => ActiveAttackSkill != null && stats != null &&
            stats.CanAffordSkill(CostOf(ActiveAttackSkill.Value, ActiveAttackLevel));
        public bool TrySpendAttackSkill(SkillId id, int level) => stats != null && stats.TrySpendMana(CostOf(id, level));

        private bool UsableWithWeapon(SkillDefinition skill)
        {
            WeaponType weapon = CurrentWeapon();
            return skill.Bow ? weapon == WeaponType.Bow : CanUseWithWeapon(skill.Id, weapon);
        }

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
            if (!UsableWithWeapon(skill))
            {
                CombatText.Show(above, skill.Bow ? "Needs a bow" : RequiredWeaponText(skill.Id), CombatText.PhysicalColor, 0.8f);
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

        // Gear changed: a toggled skill that's no longer granted, or can't be used with the weapon
        // now in hand, switches off.
        private void CheckToggle()
        {
            if (toggled != null && ActiveAttackSkill == null)
            {
                toggled = null;
                toggledSlot = -1;
            }
        }
    }
}
