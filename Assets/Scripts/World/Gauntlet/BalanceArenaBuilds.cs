using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.Skills;

namespace PoeClone.World
{
    /// <summary>Live weapon routes with authored family passives and repeatable best-of-pool loot.</summary>
    public static class BalanceArenaBuilds
    {
        public static readonly int[] PickerLevels = { 1, 10, 16, 24, 30, 34 };
        private static readonly Dictionary<string, Entry[]> pickerChoices = new Dictionary<string, Entry[]>();
        private static readonly Dictionary<Entry, (string label, bool highest)> pickerRows = new Dictionary<Entry, (string, bool)>();
        public static string[] PickerSkills { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            catalogue = null;
            PickerSkills = null;
            pickerChoices.Clear();
            pickerRows.Clear();
            damageEstimates.Clear();
        }

        /// <summary>Finish every comparison under the loading cover, yielding between estimates.</summary>
        public static IEnumerator PreparePicker()
        {
            if (PickerSkills != null) yield break;
            UI.LoadingStatus.Set("Preparing arena builds");
            yield return null;
            var skills = Data.builds.Select(e => e.skill).Distinct().ToArray();
            for (int s = 0; s < skills.Length; s++)
            {
                string skill = skills[s];
                UI.LoadingStatus.Set("Comparing " + skill + " (" + (s + 1) + "/" + skills.Length + ")");
                yield return null;
                var choices = new List<Entry>();
                foreach (var group in Data.builds.Where(e => e.skill == skill && PickerLevels.Contains(e.level))
                    .GroupBy(e => e.level).OrderBy(g => g.Key))
                {
                    var candidates = group.GroupBy(e => string.Join(",", Split(e.weaponNodes).OrderBy(id => id, StringComparer.Ordinal)))
                        .Select(allocations => allocations.First()).ToArray();
                    foreach (var entry in candidates)
                    {
                        EstimatedDps(entry);
                        yield return null;
                    }
                    var ranked = candidates.OrderByDescending(e => EstimatedDps(e))
                        .ThenBy(e => e.name, StringComparer.Ordinal).ThenBy(e => e.id, StringComparer.Ordinal).Take(4).ToArray();
                    foreach (var entry in ranked)
                    {
                        float dps = EstimatedDps(entry), bossDps = EstimatedDps(entry, boss: true);
                        bool highest = entry == ranked[0];
                        string label = entry.name + " · " + RouteEffects(entry) +
                            "\n≈ " + dps.ToString("N0") + " effective DPS" +
                            (Mathf.Approximately(dps, bossDps) ? "" : " · " + bossDps.ToString("N0") + " boss DPS") +
                            " at Lv " + entry.level + (highest ? " · BEST AT THIS LEVEL" : "");
                        pickerRows[entry] = (label, highest);
                    }
                    choices.AddRange(ranked);
                }
                pickerChoices[skill] = choices.ToArray();
            }
            // Publish readiness only once every skill page is complete. Scene reloads reuse it.
            PickerSkills = skills;
        }

        public static (string label, bool highest) PickerRow(Entry entry) => pickerRows[entry];

        public static Entry[] PickerChoices(string skill)
        {
            if (PickerSkills == null) throw new InvalidOperationException("Arena picker has not finished loading.");
            return pickerChoices[skill];
        }

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
                Id = id, Name = skill + " · " + name, Summary = "Weapon-tree route: " + name,
                Melee = family == "Melee", MainHand = new string[0], Armour = "int",
                CoreSkills = new StatType[0], Passives = Split(passives), WeaponNodes = Split(weaponNodes)
            };
        }
        private static Catalogue catalogue;
        public static Catalogue Data => catalogue ?? (catalogue = Generate(
            JsonUtility.FromJson<Catalogue>(Resources.Load<TextAsset>("BalanceArenaBuilds").text)));
        private static string[] Split(string text) => string.IsNullOrEmpty(text) ? new string[0] : text.Split(',');

        /// <summary>The saved survey supplies equipment templates; character and weapon trees are live.</summary>
        public static Catalogue Generate(Catalogue samples)
        {
            var entries = new List<Entry>();
            foreach (var group in samples.builds.GroupBy(e => e.skill))
            {
                var skill = SkillBook.Get((SkillId)Enum.Parse(typeof(SkillId), group.Key));
                var tree = WeaponSkillTrees.For(skill.Grant);
                foreach (var route in WeaponSkillTrees.Routes(skill.Grant))
                    foreach (var level in group.GroupBy(e => e.level).OrderBy(g => g.Key))
                    {
                        var baseline = level.FirstOrDefault(e => e.id.EndsWith("/base", StringComparison.Ordinal)) ?? level.First();
                        var progress = new WeaponSkillProgress { level = baseline.weaponRank };
                        // Preview a legal developing route before its destination unlocks.
                        foreach (string id in route.Nodes.Take(baseline.weaponRank))
                            if (!progress.Allocate(skill.Grant, id)) throw new InvalidOperationException("Arena: illegal live route " + route.Name);
                        // Preserve useful sampled travel preferences, then fill from the live tree.
                        // Never add an unselected keystone or silently change the advertised route.
                        foreach (string id in Split(baseline.weaponNodes).Concat(tree?.Select(n => n.Id) ?? Enumerable.Empty<string>()))
                        {
                            var node = WeaponSkillTrees.Find(skill.Grant, id);
                            if (node != null && !node.Keystone) progress.Allocate(skill.Grant, id);
                        }
                        entries.Add(new Entry {
                            id = group.Key + "/" + route.Id, skill = group.Key,
                            name = route.Nodes.Length > baseline.weaponRank ? "Toward " + route.Name : route.Name,
                            family = baseline.family, level = level.Key, sample = baseline.sample,
                            weaponRank = baseline.weaponRank, passives = string.Join(",", ArenaPassiveBuilds.Plan(skill, level.Key)), gear = baseline.gear,
                            weaponNodes = string.Join(",", progress.nodes)
                        });
                    }
            }
            return new Catalogue { source = samples.source, generatedUtc = samples.generatedUtc, model = samples.model,
                rareRollsPerSlot = samples.rareRollsPerSlot, builds = entries.ToArray() };
        }

        public static string RouteEffects(Entry entry)
        {
            var grant = SkillBook.Get((SkillId)Enum.Parse(typeof(SkillId), entry.skill)).Grant;
            var destinations = Split(entry.weaponNodes).Select(id => WeaponSkillTrees.Find(grant, id))
                .Where(node => node != null && node.Keystone).ToArray();
            if (destinations.Length > 0)
                return string.Join("; ", destinations.Select(node => node.Description.Replace("\n", "; ")));
            var allocated = Split(entry.weaponNodes).Select(id => WeaponSkillTrees.Find(grant, id)).Where(node => node != null);
            return string.Join("; ", allocated.SelectMany(node => node.Bonuses)
                .GroupBy(bonus => bonus.Effect)
                .Select(group => WeaponSkillTrees.Describe(new WeaponSkillBonus(group.Key, group.Sum(bonus => bonus.Value)), grant)));
        }

        private static readonly Dictionary<(Entry entry, bool boss), float> damageEstimates = new Dictionary<(Entry, bool), float>();

        // Route choices must not change the item pools used to compare the same skill and level.
        private static int EquipmentSeed(Entry entry)
        {
            int seed = entry.level;
            foreach (char c in entry.skill) seed = unchecked(seed * 31 + c);
            return seed;
        }

        /// <summary>Median of three repeatable best-of-pool kits, without touching player state or RNG.</summary>
        public static float EstimatedDps(Entry entry, bool boss = false)
        {
            if (damageEstimates.TryGetValue((entry, boss), out float cached)) return cached;
            int seed = EquipmentSeed(entry);
            var rolls = new float[3];
            var bossRolls = new float[3];
            var skill = SkillBook.Get((SkillId)Enum.Parse(typeof(SkillId), entry.skill));
            for (int i = 0; i < rolls.Length; i++)
            {
                var kit = CreateKit(entry, new System.Random(unchecked(seed + i * 7919)));
                var equipment = new EquipmentSet { EnforceRequirements = true };
                foreach (var pick in kit.Picks) equipment.Restore(pick.Slot, pick.Item, out _);
                var basis = new BaseStats { Level = entry.level }
                    .Set(StatType.Strength, 9 + entry.level).Set(StatType.Dexterity, 9 + entry.level)
                    .Set(StatType.Intelligence, 9 + entry.level).Set(StatType.MaxLife, 90 + entry.level * 10)
                    .Set(StatType.MaxMana, 45 + entry.level * 5).Set(StatType.PhysicalDamage, PlayerStatsLink.BasePhysicalDamage);
                var sheet = StatSheet.Build(basis, equipment, Split(entry.passives).SelectMany(id => PassiveTree.Get(id).Mods));
                var weapon = equipment.GetActive(EquipSlot.MainHand);
                if (weapon == null) continue;
                var bonuses = new WeaponSkillBonuses(weapon);
                rolls[i] = SingleTargetDamage.Estimate(skill, weapon, sheet, bonuses,
                    out _, out _, out _, includeNonBossCull: true);
                bossRolls[i] = SingleTargetDamage.Estimate(skill, weapon, sheet, bonuses,
                    out _, out _, out _, includeNonBossCull: false);
            }
            Array.Sort(rolls);
            Array.Sort(bossRolls);
            damageEstimates[(entry, false)] = rolls[1];
            damageEstimates[(entry, true)] = bossRolls[1];
            return damageEstimates[(entry, boss)];
        }

        public static GauntletLoadout.Result Equip(Entry entry, PlayerStats player, PlayerInventory inventory)
        {
            var passives = player.GetComponent<PlayerPassives>();
            passives.Allocation.ResetAll();
            foreach (string id in Split(entry.passives))
                if (!passives.Take(id)) throw new InvalidOperationException("Arena: invalid recorded passive " + id);
            foreach (EquipSlot slot in SlotRules.AllSlots) inventory.Equipment.Unequip(slot);
            var result = CreateKit(entry);
            foreach (var pick in result.Picks)
                if (!inventory.Equipment.Restore(pick.Slot, pick.Item, out _))
                    throw new InvalidOperationException("Arena: cannot equip " + pick.Item.Id);
            return result;
        }

        public static GauntletLoadout.Result CreateKit(Entry entry, System.Random rng = null)
        {
            rng = rng ?? new System.Random(EquipmentSeed(entry));
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
                // Some hybrid armour families have no low-requirement tier. Fall back
                // to wearable armour in this slot rather than aborting the whole build.
                if (available == null && slot != EquipSlot.MainHand && slot != EquipSlot.OffHand)
                    available = bases.Where(item => SlotRules.Accepts(slot, item))
                        .OrderByDescending(item => item.Requirements.Level).ThenBy(item => item.Id).FirstOrDefault();
                if (available == null) throw new InvalidOperationException("Arena: no wearable base for " + recordedBase);
                StatType? grant = slot == EquipSlot.MainHand
                    ? (StatType?)SkillBook.Get((SkillId)Enum.Parse(typeof(SkillId), entry.skill)).Grant : null;
                GauntletLoadout.SlotPick pick = null;
                for (int i = 0; i < rolls; i++)
                {
                    var item = ItemGenerator.Generate(rng, available.Id, entry.level, ItemGenerator.RollRarity(rng), grant);
                    // Compare normalized stat totals so large armour/life numbers do not
                    // automatically outweigh smaller speed, resistance or skill rolls.
                    float score = item.Modifiers.Sum(mod => mod.Value * GauntletLoadout.UnitValue(mod.Stat));
                    if (pick == null || score > pick.Score)
                        pick = new GauntletLoadout.SlotPick { Slot = slot, Item = item, Score = score, Rolls = rolls,
                            Note = "best of ground-loot rolls; survey route sample " + entry.sample };
                }
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
