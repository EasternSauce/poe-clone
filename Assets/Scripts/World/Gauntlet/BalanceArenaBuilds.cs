using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.Skills;

namespace PoeClone.World
{
    /// <summary>Survey builds with fresh ground-loot rolls selected from the weakest 30% per slot.</summary>
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

        public static GauntletLoadout.Result Equip(Entry entry, PlayerStats player, PlayerInventory inventory, System.Random rng)
        {
            var passives = player.GetComponent<PlayerPassives>();
            passives.Allocation.ResetAll();
            foreach (string id in Split(entry.passives))
                if (!passives.Take(id)) throw new InvalidOperationException("Arena: invalid recorded passive " + id);
            foreach (EquipSlot slot in SlotRules.AllSlots) inventory.Equipment.Unequip(slot);
            var result = CreateKit(entry, rng);
            foreach (var pick in result.Picks)
                if (!inventory.Equipment.Restore(pick.Slot, pick.Item, out _))
                    throw new InvalidOperationException("Arena: cannot equip " + pick.Item.Id);
            return result;
        }

        public static GauntletLoadout.Result CreateKit(Entry entry, System.Random rng = null)
        {
            rng = rng ?? new System.Random();
            var result = new GauntletLoadout.Result();
            var basis = new BaseStats { Level = entry.level }
                .Set(StatType.Strength, 9 + entry.level).Set(StatType.Dexterity, 9 + entry.level)
                .Set(StatType.Intelligence, 9 + entry.level);
            var passiveMods = Split(entry.passives).SelectMany(id => PassiveTree.Get(id).Mods);
            var sheet = StatSheet.Build(basis, null, passiveMods);
            var bases = ItemGenerator.BaseIds.Select(id => ItemGenerator.Display(id, null, ItemRarity.Normal))
                .Where(item => item.Requirements.Level <= entry.level && item.Requirements.MetBy(sheet)).ToArray();
            int rolls = Math.Max(1, Data.rareRollsPerSlot);
            foreach (string saved in entry.gear.Split(';'))
            {
                string[] parts = saved.Trim().Split(new[] { '=' }, 2);
                if (parts.Length != 2 || parts[1] == "empty") continue;
                var slot = (EquipSlot)Enum.Parse(typeof(EquipSlot), parts[0]);
                string recordedBase = parts[1].Substring(0, parts[1].IndexOf(" [", StringComparison.Ordinal));
                // Retain the build's item family, stepping down to a base that needs no
                // lucky attribute affixes on other pieces to stay active.
                string line = ItemGenerator.LineOf(recordedBase);
                var available = bases.Where(item => ItemGenerator.LineOf(item.Id) == line)
                    .OrderByDescending(item => item.Requirements.Level).FirstOrDefault();
                if (available == null) throw new InvalidOperationException("Arena: no wearable base for " + recordedBase);
                StatType? grant = slot == EquipSlot.MainHand
                    ? (StatType?)SkillBook.Get((SkillId)Enum.Parse(typeof(SkillId), entry.skill)).Grant : null;
                var candidates = new List<GauntletLoadout.SlotPick>();
                for (int i = 0; i < rolls; i++)
                {
                    var item = ItemGenerator.Generate(rng, available.Id, entry.level, ItemGenerator.RollRarity(rng), grant);
                    // Compare normalized stat totals so large armour/life numbers do not
                    // automatically outweigh smaller speed, resistance or skill rolls.
                    float score = item.Modifiers.Sum(mod => mod.Value * GauntletLoadout.UnitValue(mod.Stat));
                    candidates.Add(new GauntletLoadout.SlotPick { Slot = slot, Item = item, Score = score, Rolls = rolls,
                        Note = "bottom 30% of ground-loot rolls; survey route sample " + entry.sample });
                }
                candidates.Sort((left, right) => left.Score.CompareTo(right.Score));
                int bottomCount = Math.Max(1, Mathf.FloorToInt(candidates.Count * .3f));
                var pick = candidates[rng.Next(bottomCount)];
                if (slot == EquipSlot.MainHand && pick.Item.MainSkillGrant.HasValue)
                {
                    pick.Item.SkillProgress = new WeaponSkillProgress { level = entry.weaponRank };
                    foreach (string id in Split(entry.weaponNodes))
                        if (!pick.Item.SkillProgress.Allocate(pick.Item.MainSkillGrant.Value, id))
                            throw new InvalidOperationException("Arena: invalid recorded weapon passive " + id);
                    pick.Item.SynchronizeSkillLevel();
                }
                result.Picks.Add(pick);
            }
            return result;
        }
    }
}
