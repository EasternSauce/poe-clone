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
        /// <summary>Stable skill code names for selection; resolve SkillBook names for display.</summary>
        public static string[] PickerSkills { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            catalogue = null;
            PickerSkills = null;
            pickerChoices.Clear();
            pickerRows.Clear();
            damageEstimates.Clear();
            equipmentPools.Clear();
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
                UI.LoadingStatus.Set("Comparing " + SkillBook.GetByCodeName(skill).Name + " (" + (s + 1) + "/" + skills.Length + ")");
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
                            "\n≈ " + dps.ToString("N0") + " mitigated DPS" +
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
            public string SkillName => SkillBook.GetByCodeName(skill).Name;
            public GauntletBuild AsBuild() => new GauntletBuild {
                Id = id, Name = SkillName + " · " + name, Summary = "Weapon-tree route: " + name,
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
                var skill = SkillBook.GetByCodeName(group.Key);
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
            var grant = SkillBook.GetByCodeName(entry.skill).Grant;
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

        private sealed class EquipmentPool
        {
            public ItemData[] Bases;
            public string SharedGear;
            public readonly Dictionary<(EquipSlot slot, string baseId, int sample), GauntletLoadout.SlotPick> Picks =
                new Dictionary<(EquipSlot, string, int), GauntletLoadout.SlotPick>();
        }

        private static readonly Dictionary<(string family, int level), EquipmentPool> equipmentPools =
            new Dictionary<(string, int), EquipmentPool>();

        private static int EquipmentSeed(string family, int level, EquipSlot slot, string baseId, int sample)
        {
            int seed = unchecked(level + sample * 7919);
            foreach (char c in family) seed = unchecked(seed * 31 + c);
            seed = unchecked(seed * 31 + (int)slot);
            foreach (char c in baseId) seed = unchecked(seed * 31 + c);
            return seed;
        }

        /// <summary>Estimate the same repeatable kit that Equip assigns, without touching player state or RNG.</summary>
        public static float EstimatedDps(Entry entry, bool boss = false)
        {
            if (damageEstimates.TryGetValue((entry, boss), out float cached)) return cached;
            var skill = SkillBook.GetByCodeName(entry.skill);
            var kit = CreateKit(entry);
            var equipment = new EquipmentSet { EnforceRequirements = true };
            foreach (var pick in kit.Picks) equipment.Restore(pick.Slot, pick.Item, out _);
            var basis = new BaseStats { Level = entry.level }
                .Set(StatType.Strength, 9 + entry.level).Set(StatType.Dexterity, 9 + entry.level)
                .Set(StatType.Intelligence, 9 + entry.level).Set(StatType.MaxLife, 90 + entry.level * 10)
                .Set(StatType.MaxMana, 45 + entry.level * 5).Set(StatType.PhysicalDamage, PlayerStatsLink.BasePhysicalDamage);
            var sheet = StatSheet.Build(basis, equipment, Split(entry.passives).SelectMany(id => PassiveTree.Get(id).Mods));
            var weapon = equipment.GetActive(EquipSlot.MainHand);
            if (weapon == null) throw new InvalidOperationException("Arena: missing weapon for " + entry.skill);
            var bonuses = new WeaponSkillBonuses(weapon);
            damageEstimates[(entry, false)] = SingleTargetDamage.Estimate(skill, weapon, sheet, bonuses,
                out _, out _, out _, includeNonBossCull: true);
            damageEstimates[(entry, true)] = SingleTargetDamage.Estimate(skill, weapon, sheet, bonuses,
                out _, out _, out _, includeNonBossCull: false);
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

        /// <summary>Reuse family loot pools; return independent items with this skill's grant and route.</summary>
        public static GauntletLoadout.Result CreateKit(Entry entry, int sample = 0)
        {
            var result = new GauntletLoadout.Result();
            var skill = SkillBook.GetByCodeName(entry.skill);
            string family = ArenaPassiveBuilds.Family(skill);
            if (!equipmentPools.TryGetValue((family, entry.level), out var pool))
            {
                var basis = new BaseStats { Level = entry.level }
                    .Set(StatType.Strength, 9 + entry.level).Set(StatType.Dexterity, 9 + entry.level)
                    .Set(StatType.Intelligence, 9 + entry.level);
                var passiveMods = Split(entry.passives).SelectMany(id => PassiveTree.Get(id).Mods);
                var sheet = StatSheet.Build(basis, null, passiveMods);
                // Pick a stable family template, independent of which skill is evaluated first.
                var template = Data.builds.Where(e => e.level == entry.level &&
                        ArenaPassiveBuilds.Family(SkillBook.GetByCodeName(e.skill)) == family)
                    .OrderBy(e => e.skill, StringComparer.Ordinal).First();
                pool = new EquipmentPool {
                    Bases = ItemGenerator.BaseIds.Select(id => ItemGenerator.Display(id, null, ItemRarity.Normal))
                        .Where(item => item.Requirements.Level <= entry.level && item.Requirements.MetBy(sheet)).ToArray(),
                    SharedGear = template.gear
                };
                equipmentPools[(family, entry.level)] = pool;
            }
            int rolls = Math.Max(1, Data.rareRollsPerSlot);
            // Keep skill-compatible hands; share all armour and accessories within the family.
            var slots = new Dictionary<EquipSlot, string>();
            foreach (string saved in pool.SharedGear.Split(';').Concat(entry.gear.Split(';')))
            {
                string[] parts = saved.Trim().Split(new[] { '=' }, 2);
                if (parts.Length != 2) continue;
                var slot = (EquipSlot)Enum.Parse(typeof(EquipSlot), parts[0]);
                if (!slots.ContainsKey(slot) || slot == EquipSlot.MainHand || slot == EquipSlot.OffHand)
                    slots[slot] = parts[1];
            }
            foreach (var saved in slots.OrderBy(p => p.Key == EquipSlot.MainHand ? 0 : p.Key == EquipSlot.OffHand ? 1 : 2))
            {
                var slot = saved.Key;
                if (saved.Value == "empty") continue;
                string recordedBase = saved.Value.Substring(0, saved.Value.IndexOf(" [", StringComparison.Ordinal));
                // Retain the build's item family, stepping down to a base that needs no
                // lucky attribute affixes on other pieces to stay active.
                string line = ItemGenerator.LineOf(recordedBase);
                var available = pool.Bases.Where(item => ItemGenerator.LineOf(item.Id) == line)
                    .OrderByDescending(item => item.Requirements.Level).FirstOrDefault();
                // Some hybrid armour families have no low-requirement tier. Fall back
                // to wearable armour in this slot rather than aborting the whole build.
                if (available == null && slot != EquipSlot.MainHand && slot != EquipSlot.OffHand)
                    available = pool.Bases.Where(item => SlotRules.Accepts(slot, item))
                        .OrderByDescending(item => item.Requirements.Level).ThenBy(item => item.Id).FirstOrDefault();
                if (available == null) throw new InvalidOperationException("Arena: no wearable base for " + recordedBase);
                if (!pool.Picks.TryGetValue((slot, available.Id, sample), out var cached))
                {
                    var rng = new System.Random(EquipmentSeed(family, entry.level, slot, available.Id, sample));
                    var grants = WeaponSkillRules.Pool(available.Type, available.WeaponType);
                    StatType? grant = grants.Length > 0 ? (StatType?)grants[0] : null;
                    for (int i = 0; i < rolls; i++)
                    {
                        var item = ItemGenerator.Generate(rng, available.Id, entry.level, ItemGenerator.RollRarity(rng), grant);
                        // Compare normalized totals so large armour/life numbers do not dominate.
                        float score = item.Modifiers.Sum(mod => mod.Value * GauntletLoadout.UnitValue(mod.Stat));
                        if (cached == null || score > cached.Score)
                            cached = new GauntletLoadout.SlotPick { Slot = slot, Item = item, Score = score, Rolls = rolls,
                                Note = "best of shared " + family + " ground-loot rolls" };
                    }
                    pool.Picks[(slot, available.Id, sample)] = cached;
                }
                var source = cached.Item;
                if (slot == EquipSlot.MainHand && !WeaponSkillRules.Pool(source.Type, source.WeaponType).Contains(skill.Grant))
                    throw new InvalidOperationException("Arena: incompatible weapon for " + entry.skill);
                var copy = new ItemData(source.Id, source.Name, source.Type, source.Width, source.Height, source.Tint,
                    source.Modifiers.Select(mod => slot == EquipSlot.MainHand && mod.Stat == source.MainSkillGrant
                        ? new StatModifier(skill.Grant, mod.Value, mod.Tier) : mod),
                    source.HasCape, source.WeaponType, source.Rarity) {
                    ItemLevel = source.ItemLevel, ArtId = source.ArtId, ArtTint = source.ArtTint
                };
                var pick = new GauntletLoadout.SlotPick { Slot = slot, Item = copy, Score = cached.Score,
                    Rolls = cached.Rolls, Note = cached.Note };
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
