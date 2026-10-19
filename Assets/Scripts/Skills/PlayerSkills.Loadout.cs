using System.Collections.Generic;
using UnityEngine;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.UI;

namespace PoeClone.Skills
{
    public partial class PlayerSkills
    {
        // ------------------------------------------------------------------ levels from gear

        /// <summary>The granted rank, capped at ten. Rank itself gives no combat bonuses.</summary>
        public int Level(SkillId id)
        {
            if (inventory == null)
                return 0;

            SkillDefinition skill = SkillBook.Get(id);
            if (WeaponSkill == id && MainWeapon?.SkillProgress != null) return MainWeapon.SkillProgress.level;
            int granted = 0;
            foreach (EquipSlot slot in SlotRules.AllSlots)
            {
                ItemData item = inventory.Equipment.GetActive(slot);
                if (item == null)
                    continue;
                foreach (StatModifier m in item.Modifiers)
                {
                    if (m.Stat == skill.Grant)
                        granted = Mathf.Max(granted, Mathf.RoundToInt(m.Value));
                }
            }
            return LevelFromGrant(granted);
        }

        private static int LevelFromGrant(int granted)
        {
            if (granted <= 0) return 0;
            return Mathf.Clamp(granted, 1, 10);
        }

        public struct SkillGrant
        {
            public SkillId Id;
            public EquipSlot Source;
            public int GrantLevel;
            public ItemData Item;
        }

        /// <summary>Every usable grant is its own bindable entry, even for the same skill and level.</summary>
        public List<SkillGrant> AvailableGrants()
        {
            var grants = new List<SkillGrant>();
            if (inventory == null) return grants;
            foreach (EquipSlot source in SlotRules.AllSlots)
            {
                ItemData item = inventory.Equipment.GetActive(source);
                if (item == null) continue;
                foreach (StatModifier mod in item.Modifiers)
                {
                    SkillDefinition skill = SkillBook.ForGrant(mod.Stat);
                    if (skill == null || WeaponSkillRules.IsWeaponSkill(skill.Grant) || skill.MeleeAttack ||
                        (skill.Bow && CurrentWeapon() != WeaponType.Bow) || MainSkill == skill.Id)
                        continue;
                    int grantLevel = Mathf.RoundToInt(mod.Value);
                    if (grantLevel > 0)
                        grants.Add(new SkillGrant { Id = skill.Id, Source = source, GrantLevel = grantLevel, Item = item });
                }
            }
            return grants;
        }

        public int LevelAt(int slot) => slot >= 0 && slot < bindings.Count && bindings[slot].Skill != null &&
            BindingValid(slot) ? LevelFromGrant(bindings[slot].GrantLevel) : 0;
        public int GrantLevelWithBonuses(SkillGrant grant) => LevelFromGrant(grant.GrantLevel);

        public int SourceSlotAt(int slot) => slot >= 0 && slot < bindings.Count && bindings[slot].Skill != null ? (int)bindings[slot].Source : -1;
        public int GrantLevelAt(int slot) => slot >= 0 && slot < bindings.Count && bindings[slot].Skill != null ? bindings[slot].GrantLevel : 0;
        public bool MatchesGrant(int slot, SkillGrant grant) => slot >= 0 && slot < bindings.Count &&
            bindings[slot].Skill == grant.Id && bindings[slot].Item == grant.Item && bindings[slot].GrantLevel == grant.GrantLevel;
        public bool MatchesSource(int slot, SkillId id, EquipSlot source, int grantLevel) =>
            slot >= 0 && slot < bindings.Count && bindings[slot].Skill == id && bindings[slot].Source == source &&
            bindings[slot].GrantLevel == grantLevel && BindingValid(slot);

        private bool BindingValid(int slot)
        {
            if (bindings[slot].Skill == null || bindings[slot].Item == null || inventory == null ||
                inventory.Equipment.GetActive(bindings[slot].Source) != bindings[slot].Item || !CanBind(bindings[slot].Skill.Value))
                return false;
            foreach (StatModifier mod in bindings[slot].Item.Modifiers)
                if (mod.Stat == SkillBook.Get(bindings[slot].Skill.Value).Grant && Mathf.RoundToInt(mod.Value) == bindings[slot].GrantLevel)
                    return true;
            return false;
        }

        /// <summary>
        /// The equipped weapon's spell or summon main skill. Grimoires supply support abilities.
        /// </summary>
        public SkillId? MainSkill
        {
            get
            {
                if (inventory == null)
                    return null;
                return MainOn(inventory.Equipment.GetActive(EquipSlot.MainHand));
            }
        }

        private static SkillId? MainOn(ItemData item)
        {
            if (item == null)
                return null;
            foreach (StatModifier m in item.Modifiers)
            {
                if (WeaponSkillRules.IsWeaponSkill(m.Stat) && !SkillGrants.IsBowSkill(m.Stat) && System.Array.IndexOf(SkillGrants.MeleeSkills, m.Stat) < 0)
                {
                    SkillDefinition skill = SkillBook.ForGrant(m.Stat);
                    if (skill != null)
                        return skill.Id;
                }
            }
            return null;
        }

        /// <summary>Gear grants it and it isn't a weapon's attack (a staff spell or a melee skill): it can go on the bar.</summary>
        public bool IsUnlocked(SkillId id)
        {
            return Level(id) > 0 && WeaponSkill != id && !WeaponSkillRules.IsWeaponSkill(SkillBook.Get(id).Grant);
        }

        /// <summary>Whether the current gear both grants this skill and can use it.</summary>
        public bool CanBind(SkillId id)
        {
            return IsUnlocked(id) && (!SkillBook.Get(id).Bow || CurrentWeapon() == WeaponType.Bow);
        }

        /// <summary>Which worn item grants the skill (the one at the highest level), for the skills panel.</summary>
        public ItemData Source(SkillId id)
        {
            if (inventory == null)
                return null;
            if (WeaponSkill == id) return MainWeapon;
            StatType grant = SkillBook.Get(id).Grant;
            ItemData best = null;
            float bestLevel = 0f;
            foreach (EquipSlot slot in SlotRules.AllSlots)
            {
                ItemData item = inventory.Equipment.GetActive(slot);
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
            return index >= 0 && index < bindings.Count ? bindings[index].Skill : null;
        }

        /// <summary>Empties a slot.</summary>
        public void ClearSlot(int slot)
        {
            if (slot < 0 || slot >= bindings.Count || bindings[slot].Skill == null)
                return;
            bindings.Clear(slot);
            Changed?.Invoke();
        }

        /// <summary>
        /// Puts a usable skill in a slot. Disabled spectator replicas may receive remote bindings
        /// before their replicated gear arrives.
        /// </summary>
        public void Assign(int slot, SkillId id)
        {
            SkillGrant? first = null;
            foreach (SkillGrant grant in AvailableGrants())
            {
                if (grant.Id != id) continue;
                if (!first.HasValue) first = grant;
                bool alreadyBound = false;
                for (int k = 0; k < bindings.Count; k++)
                    if (MatchesGrant(k, grant)) alreadyBound = true;
                if (!alreadyBound) { Assign(slot, grant.Id, grant.Source, grant.GrantLevel); return; }
            }
            if (first.HasValue) { Assign(slot, id, first.Value.Source, first.Value.GrantLevel); return; }
            if (!enabled && slot >= 0 && slot < bindings.Count)
            {
                bindings.SetPlaceholder(slot, id);
                Changed?.Invoke();
            }
        }

        public void Assign(int slot, SkillId id, EquipSlot source, int grantLevel)
        {
            if (slot < 0 || slot >= bindings.Count) return;
            foreach (SkillGrant grant in AvailableGrants())
            {
                if (grant.Id != id || grant.Source != source || grant.GrantLevel != grantLevel) continue;
                if (enabled && !TouchMode.Active) PlayerPotions.SetPotionAt(slot, 0);
                bindings.Assign(slot, id, grant.Item, source, grantLevel);
                Changed?.Invoke();
                return;
            }
        }

        /// <summary>Removes every binding no longer usable with the equipped gear.</summary>
        public void ClearUnavailableSlots()
        {
            bool changed = false;
            for (int k = 0; k < bindings.Count; k++)
            {
                if (bindings[k].Skill == null || BindingValid(k))
                    continue;
                bindings.Clear(k);
                changed = true;
            }
            if (changed)
            {
                Changed?.Invoke();
            }
        }

        // Newly granted item skills go into free slots; removing an item clears only its grants.
        private void FillEmptySlots(bool announce)
        {
            ClearUnavailableSlots();
            bool changed = false;
            foreach (SkillGrant grant in AvailableGrants())
            {
                bool assigned = false;
                for (int k = 0; k < bindings.Count; k++)
                    if (bindings[k].Skill == grant.Id)
                        assigned = true;
                if (assigned) continue;

                int free = -1;
                for (int k = 0; k < bindings.Count; k++)
                {
                    // Touch has dedicated potion buttons, so desktop potion bindings reserve no skill slots.
                    if (bindings[k].Skill == null && (TouchMode.Active || PlayerPotions.PotionAt(k) == 0)) { free = k; break; }
                }
                if (free < 0)
                    continue;

                bindings.SetSource(free, grant.Id, grant.Item, grant.Source, grant.GrantLevel);
                changed = true;
                if (announce)
                {
                    SkillDefinition skill = SkillBook.Get(grant.Id);
                    CombatText.Show(transform.position + Vector3.up * 2.2f, skill.Name + " (" + KeyLabel(free) + ")", skill.Color, 1.1f);
                }
            }

            if (changed || announce)
                Changed?.Invoke();
        }
    }
}
