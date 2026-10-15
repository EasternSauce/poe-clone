using PoeClone.Inventory;

namespace PoeClone.Skills
{
    public partial class PlayerSkills
    {
        public SkillId? ActiveBowSkill => CurrentWeapon() == WeaponType.Bow && MainWeapon?.MainSkillGrant is StatType grant &&
            SkillGrants.IsBowSkill(grant) ? SkillBook.ForGrant(grant)?.Id : null;
        public SkillId? ActiveAttackSkill => ActiveMeleeSkill ?? ActiveBowSkill;
        public int ActiveAttackLevel => ActiveAttackSkill.HasValue ? Level(ActiveAttackSkill.Value) : 0;
        public bool CanAffordAttackSkill => ActiveAttackSkill != null && stats != null &&
            stats.CanAffordSkill(CostOf(ActiveAttackSkill.Value, ActiveAttackLevel));
        public bool TrySpendAttackSkill(SkillId id, int level) => stats != null && stats.TrySpendMana(CostOf(id, level));
    }
}
