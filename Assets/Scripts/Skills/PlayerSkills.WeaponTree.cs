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
        public int CharacterLevel => stats != null ? stats.Level : 1;

        /// <summary>Allocates a confirmed draft, each node once it connects; all or nothing.</summary>
        public bool AllocateWeaponNodes(ItemData expectedWeapon, System.Collections.Generic.IReadOnlyList<string> ids)
        {
            if (expectedWeapon == null || MainWeapon != expectedWeapon || !expectedWeapon.MainSkillGrant.HasValue || ids.Count == 0) return false;
            var progress = expectedWeapon.SkillProgress.Copy();
            // A refunded draft node can leave later picks connected only through each other.
            var remaining = new System.Collections.Generic.List<string>(ids);
            while (remaining.Count > 0)
                if (remaining.RemoveAll(id => progress.Allocate(expectedWeapon.MainSkillGrant.Value, id)) == 0) return false;
            expectedWeapon.SkillProgress.nodes = progress.nodes;
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
            int points = weapon.SkillProgress.GainExperience(amount, stats.Level);
            if (points <= 0) return;
            weapon.SynchronizeSkillLevel();
            string name = SkillGrants.SkillName(weapon.MainSkillGrant.Value);
            string plus = " · +" + points + " tree point" + (points > 1 ? "s" : "");
            CombatText.Show(transform.position + Vector3.up * 2.5f,
                (weapon.SkillProgress.level > oldLevel ? name + " level " + weapon.SkillProgress.level : name + " halfway to level " + (weapon.SkillProgress.level + 1)) + plus,
                UiKit.Gold, 1.2f);
            Changed?.Invoke();
            SaveSystem.SaveCharacterNow();
        }
    }
}
