using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using PoeClone.Inventory;
using PoeClone.Player;

namespace PoeClone.World
{
    /// <summary>Actual representative gear and allocations exported from the offline boss survey.</summary>
    public static class BalanceArenaBuilds
    {
        [Serializable] public sealed class Catalogue
        {
            public string source, generatedUtc, model;
            public int rareRollsPerSlot;
            public Entry[] builds;
        }
        [Serializable] public sealed class Entry
        {
            public string id, skill, name, family, passives, weaponNodes, gear;
            public int level, sample, weaponRank;
            public GauntletBuild AsBuild() => new GauntletBuild {
                Id = id, Name = skill + " · " + name, Summary = "Balance survey sample " + sample,
                Melee = family == "Melee", MainHand = new string[0], Armour = "int",
                CoreSkills = new StatType[0], Passives = Split(passives), WeaponNodes = Split(weaponNodes)
            };
        }
        private static Catalogue catalogue;
        public static Catalogue Data => catalogue ?? (catalogue = JsonUtility.FromJson<Catalogue>(Resources.Load<TextAsset>("BalanceArenaBuilds").text));
        private static string[] Split(string text) => string.IsNullOrEmpty(text) ? new string[0] : text.Split(',');

        public static GauntletLoadout.Result Equip(Entry entry, PlayerStats player, PlayerInventory inventory)
        {
            var result = new GauntletLoadout.Result();
            var passives = player.GetComponent<PlayerPassives>();
            passives.Allocation.ResetAll();
            foreach (string id in Split(entry.passives))
                if (!passives.Take(id)) throw new InvalidOperationException("Arena: invalid recorded passive " + id);
            foreach (EquipSlot slot in SlotRules.AllSlots) inventory.Equipment.Unequip(slot);
            result = CreateKit(entry);
            foreach (var pick in result.Picks)
                if (!inventory.Equipment.Restore(pick.Slot, pick.Item, out _))
                    throw new InvalidOperationException("Arena: cannot equip " + pick.Item.Id);
            return result;
        }

        public static GauntletLoadout.Result CreateKit(Entry entry)
        {
            var result = new GauntletLoadout.Result();
            foreach (string saved in entry.gear.Split(';'))
            {
                string[] parts = saved.Trim().Split(new[] { '=' }, 2);
                if (parts.Length != 2 || parts[1] == "empty") continue;
                var slot = (EquipSlot)Enum.Parse(typeof(EquipSlot), parts[0]);
                int start = parts[1].IndexOf(" [", StringComparison.Ordinal);
                string baseId = parts[1].Substring(0, start);
                ItemData basis = ItemGenerator.Display(baseId, null, ItemRarity.Rare);
                var mods = new List<StatModifier>();
                foreach (string encoded in parts[1].Substring(start + 2).TrimEnd(']').Split(','))
                {
                    string[] pair = encoded.Split(':');
                    mods.Add(new StatModifier((StatType)Enum.Parse(typeof(StatType), pair[0]), float.Parse(pair[1], CultureInfo.InvariantCulture)));
                }
                var item = new ItemData(baseId, basis.Name, basis.Type, basis.Width, basis.Height, basis.Tint,
                    mods, basis.HasCape, basis.WeaponType, ItemRarity.Rare) {
                    ItemLevel = entry.level, ArtId = basis.ArtId, ArtTint = basis.ArtTint
                };
                if (slot == EquipSlot.MainHand && item.MainSkillGrant.HasValue)
                {
                    item.SkillProgress = new WeaponSkillProgress { level = entry.weaponRank };
                    foreach (string id in Split(entry.weaponNodes))
                        if (!item.SkillProgress.Allocate(item.MainSkillGrant.Value, id))
                            throw new InvalidOperationException("Arena: invalid recorded weapon passive " + id);
                    item.SynchronizeSkillLevel();
                }
                result.Picks.Add(new GauntletLoadout.SlotPick { Slot = slot, Item = item, Rolls = Data.rareRollsPerSlot, Note = "balance survey sample " + entry.sample });
            }
            return result;
        }
    }
}
