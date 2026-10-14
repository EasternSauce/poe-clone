using UnityEngine;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.UI;

namespace PoeClone.Skills
{
    public partial class PlayerSkills
    {
        public ItemData MainWeapon
        {
            get
            {
                if (inventory == null) return null;
                ItemData hand = inventory.Equipment.GetActive(EquipSlot.MainHand);
                return hand?.MainSkillGrant != null ? hand : null;
            }
        }
        public SkillId? WeaponSkill => MainWeapon?.MainSkillGrant is StatType grant ? SkillBook.ForGrant(grant)?.Id : null;
        private static readonly WeaponSkillBonuses NoTreeBonuses = new WeaponSkillBonuses(null);
        private ItemData cachedTreeWeapon;
        private int cachedTreeCount = -1;
        private WeaponSkillBonuses cachedTree;
        public WeaponSkillBonuses BonusesFor(SkillId id) => WeaponSkill == id ? WeaponBonuses : NoTreeBonuses;
        public WeaponSkillBonuses WeaponBonuses
        {
            get
            {
                ItemData item = MainWeapon;
                if (item == null) return NoTreeBonuses;
                int count = item.SkillProgress?.nodes.Count ?? 0;
                if (cachedTreeWeapon != item || cachedTreeCount != count || cachedTree == null)
                {
                    cachedTreeWeapon = item; cachedTreeCount = count;
                    cachedTree = new WeaponSkillBonuses(item);
                }
                return cachedTree;
            }
        }
        public int WeaponResetPrice => MainWeapon?.SkillProgress != null ? 75 + 50 * MainWeapon.SkillProgress.level * MainWeapon.SkillProgress.level : 0;

        public bool AllocateWeaponNode(ItemData expectedWeapon, string id)
        {
            if (expectedWeapon == null || MainWeapon != expectedWeapon || !expectedWeapon.MainSkillGrant.HasValue ||
                !expectedWeapon.SkillProgress.Allocate(expectedWeapon.MainSkillGrant.Value, id)) return false;
            cachedTreeCount = -1;
            Minion.Desummon(transform);
            Changed?.Invoke();
            SaveSystem.SaveCharacterNow();
            return true;
        }
        public bool ResetWeaponTree(ItemData expectedWeapon)
        {
            if (PoeClone.World.AreaManager.Instance?.Current?.isTown != true || expectedWeapon == null || expectedWeapon != MainWeapon || expectedWeapon.SkillProgress == null ||
                expectedWeapon.SkillProgress.nodes.Count == 0 || !inventory.TrySpendGold(WeaponResetPrice)) return false;
            expectedWeapon.SkillProgress.nodes.Clear();
            cachedTreeCount = -1;
            Minion.Desummon(transform);
            Changed?.Invoke();
            SaveSystem.SaveCharacterNow();
            return true;
        }
        public void GainWeaponExperience(int amount)
        {
            ItemData weapon = MainWeapon;
            if (weapon?.SkillProgress == null || stats == null || stats.IsDead || !enabled) return;
            int oldLevel = weapon.SkillProgress.level;
            if (weapon.SkillProgress.GainExperience(amount))
            {
                weapon.SynchronizeSkillLevel();
                CombatText.Show(transform.position + Vector3.up * 2.5f,
                    SkillGrants.SkillName(weapon.MainSkillGrant.Value) + " level " + weapon.SkillProgress.level + " · +" + (weapon.SkillProgress.level - oldLevel) + " tree points",
                    UiKit.Gold, 1.2f);
                Changed?.Invoke();
                SaveSystem.SaveCharacterNow();
            }
        }
    }
}
