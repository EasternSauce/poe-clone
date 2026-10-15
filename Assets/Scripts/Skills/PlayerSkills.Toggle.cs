using PoeClone.Inventory;

namespace PoeClone.Skills
{
    public partial class PlayerSkills
    {
        // Legacy save fields are read but no longer activate toggles.
        public int SavedToggleSlot => 0;
        public int SavedToggleSkill => 0;
        public void RestoreToggle(int savedSlot, int savedSkill) { }
        public bool IsToggledOn(SkillId id) => false;
        public bool IsToggledOnAt(int slot) => false;
        public SkillId? ActiveBowSkill => CurrentWeapon() == WeaponType.Bow && MainWeapon?.MainSkillGrant is StatType grant &&
            SkillGrants.IsBowSkill(grant) ? SkillBook.ForGrant(grant)?.Id : null;
        public SkillId? ActiveAttackSkill => ActiveMeleeSkill ?? ActiveBowSkill;
        public int ActiveAttackLevel => ActiveAttackSkill.HasValue ? Level(ActiveAttackSkill.Value) : 0;
        public bool CanAffordAttackSkill => ActiveAttackSkill != null && stats != null &&
            stats.CanAffordSkill(CostOf(ActiveAttackSkill.Value, ActiveAttackLevel));
        public bool TrySpendAttackSkill(SkillId id, int level) => stats != null && stats.TrySpendMana(CostOf(id, level));
    }
}
