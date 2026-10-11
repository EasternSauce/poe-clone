using System;
using System.Collections.Generic;
using UnityEngine;
using PoeClone.Inventory;
using PoeClone.Player;

namespace PoeClone.World
{
    /// <summary>
    /// Kits out a gauntlet character: the passive path a build heads for, and its gear: for each
    /// slot a number of rares rolled at the character's level, the best one by the build's
    /// weights kept, then up to two of the build's uniques swapped in. Also rolls the single
    /// random rare behind the inventory's reroll buttons.
    /// </summary>
    public static class GauntletLoadout
    {
        /// <summary>What one gear slot ended up with, for the results file.</summary>
        public sealed class SlotPick
        {
            public EquipSlot Slot;
            public ItemData Item;
            public float Score;
            public int Rolls;
            public bool Unique;
            public string Note = "";
        }

        public sealed class Result
        {
            public readonly List<SlotPick> Picks = new List<SlotPick>();
            public readonly List<string> Notes = new List<string>();
        }

        // ------------------------------------------------------------------ bases

        private static Dictionary<string, List<string>> basesByLine;

        /// <summary>Base ids of a line (e.g. "helm_int"), lowest tier first.</summary>
        private static List<string> Line(string line)
        {
            if (basesByLine == null)
            {
                basesByLine = new Dictionary<string, List<string>>();
                foreach (string id in ItemGenerator.BaseIds)
                {
                    string l = ItemGenerator.LineOf(id);
                    if (l == null) continue;
                    if (!basesByLine.TryGetValue(l, out List<string> list))
                        basesByLine[l] = list = new List<string>();
                    list.Add(id);
                }
                foreach (List<string> list in basesByLine.Values)
                    list.Sort((a, b) => ItemGenerator.RequirementsOf(a, null).Level.CompareTo(ItemGenerator.RequirementsOf(b, null).Level));
            }
            return basesByLine.TryGetValue(line, out List<string> found) ? found : new List<string>();
        }

        /// <summary>The best base of a line a character of this level can wear, <paramref name="stepsDown"/> tiers lower.</summary>
        private static string BestBase(string line, int level, int stepsDown)
        {
            List<string> bases = Line(line);
            int best = -1;
            for (int k = 0; k < bases.Count; k++)
                if (ItemGenerator.RequirementsOf(bases[k], null).Level <= level) best = k;
            return best < 0 ? null : bases[Mathf.Max(0, best - stepsDown)];
        }

        private static string[] LinesFor(GauntletBuild build, EquipSlot slot, ItemData mainHand)
        {
            string a = build.Armour;
            switch (slot)
            {
                case EquipSlot.MainHand: return build.MainHand;
                case EquipSlot.OffHand:
                    if (mainHand == null || !SlotRules.HandsCompatible(mainHand, null)) return new string[0];
                    if (SlotRules.IsTwoHanded(mainHand) && mainHand.WeaponType != WeaponType.Bow) return new string[0];
                    if (mainHand.WeaponType == WeaponType.Bow) return new[] { "quiver" };
                    return build.OffHand;
                case EquipSlot.Helmet: return new[] { "helm_" + a };
                case EquipSlot.BodyArmour: return new[] { "body_" + a };
                case EquipSlot.Gloves: return new[] { "glove_" + a };
                case EquipSlot.Boots: return new[] { "boot_" + a };
                case EquipSlot.Belt: return a == "hyb" ? new[] { "belt_str", "belt_dex" } : new[] { "belt_" + a };
                case EquipSlot.Amulet: return a == "hyb" ? new[] { "amu_str", "amu_dex" } : new[] { "amu_" + a };
                default: return new[] { "ring_phys", "ring_fire", "ring_cold", "ring_light", "ring_mana" };
            }
        }

        // ------------------------------------------------------------------ scoring

        /// <summary>
        /// Roughly what one point of a stat is worth, so a good roll of anything scores about 1
        /// before the build's weight (40 life, 15% spell damage, 8% cast speed, a level 2 skill...).
        /// </summary>
        public static float UnitValue(StatType stat)
        {
            if (SkillGrants.IsGrant(stat)) return 0.5f;
            switch (stat)
            {
                case StatType.MaxLife: case StatType.MaxMana: return 1f / 30f;
                case StatType.Strength: case StatType.Dexterity: case StatType.Intelligence: return 1f / 12f;
                case StatType.Armour: case StatType.Evasion: case StatType.MinionArmour: return 1f / 60f;
                case StatType.BlockChance: return 1f / 6f;
                case StatType.PhysicalDamage: return 1f / 4f;
                case StatType.AttackSpeed: case StatType.CastSpeed: case StatType.MovementSpeed: return 1f / 8f;
                case StatType.FireResistance: case StatType.ColdResistance: case StatType.LightningResistance:
                case StatType.PoisonResistance: case StatType.MinionResistances: return 1f / 20f;
                case StatType.LifeRegen: case StatType.LifeOnAttackHit: return 1f / 3f;
                case StatType.LifeLeech: case StatType.SpellLeech: return 1f / 1.5f;
                case StatType.PercentLifeRegen: case StatType.PercentManaRegen: return 2f;
                case StatType.DamageTaken: return -1f / 4f;   // less taken is better
                case StatType.CriticalChance: return 1f / 30f;
                case StatType.CriticalMultiplier: return 1f / 20f;
                case StatType.IncreasedLife: return 1f / 6f;
                case StatType.IncreasedMana: case StatType.ManaCostReduction: case StatType.Damage: return 1f / 8f;
                case StatType.HealthPotionRecovery: case StatType.AvoidStun: case StatType.LifeOnKill: case StatType.ManaOnKill: return 1f / 30f;
                case StatType.AllSpellLevels: case StatType.MinionLevels: case StatType.FireSpellLevels: case StatType.ColdSpellLevels:
                case StatType.LightningSpellLevels: case StatType.RaiseSkeletonsLevels: case StatType.SkeletonMagesLevels:
                case StatType.SpiritWolvesLevels: case StatType.BoneGolemLevels:
                case StatType.AdditionalArrows: case StatType.AdditionalSpellProjectiles: case StatType.AdditionalProjectiles:
                case StatType.AdditionalMinions: case StatType.AdditionalSkeletons: case StatType.AdditionalChains: return 1f;
                case StatType.FirePenetration: case StatType.ColdPenetration: case StatType.LightningPenetration:
                case StatType.ElementalPenetration: case StatType.PoisonPenetration: case StatType.ArmourPenetration: return 1f / 8f;
                case StatType.LifePercentOnKill: case StatType.ManaAsDamage: return 1f / 3f;
                default: return 1f / 15f;
            }
        }

        public static float Score(GauntletBuild build, ItemData item)
        {
            if (item == null) return float.MinValue;
            float score = 0f;
            foreach (StatModifier m in item.Modifiers)
            {
                float weight;
                if (SkillGrants.IsGrant(m.Stat))
                {
                    // A skill the build is about outweighs everything else on the item.
                    weight = Array.IndexOf(build.CoreSkills, m.Stat) >= 0 ? 8f : Array.IndexOf(build.SupportSkills, m.Stat) >= 0 ? 2f : 0f;
                    if (m.Stat == StatType.GrantDash) weight = 0.6f;
                }
                else
                    weight = build.Weight(m.Stat);
                score += weight * m.Value * UnitValue(m.Stat);
            }
            return score;
        }

        private static bool HasCoreSkill(GauntletBuild build, IEnumerable<ItemData> items)
        {
            foreach (ItemData item in items)
                if (item != null)
                    foreach (StatModifier m in item.Modifiers)
                        if (Array.IndexOf(build.CoreSkills, m.Stat) >= 0) return true;
            return false;
        }

        // ------------------------------------------------------------------ rolling

        private static ItemData RollRare(System.Random rng, GauntletBuild build, string line, int level, int stepsDown)
        {
            string id = BestBase(line, level, stepsDown);
            if (id == null) return null;
            StatType? spell = line == "staff" ? build.StaffSpell : null;
            return ItemGenerator.Generate(rng, id, Mathf.Max(1, level), ItemRarity.Rare, spell);
        }

        private static SlotPick BestOf(System.Random rng, GauntletBuild build, EquipSlot slot, string[] lines, int level, int rolls, int stepsDown)
        {
            var pick = new SlotPick { Slot = slot, Score = float.MinValue };
            if (lines.Length == 0) return pick;
            for (int k = 0; k < rolls; k++)
            {
                ItemData item = RollRare(rng, build, lines[rng.Next(lines.Length)], level, stepsDown);
                pick.Rolls++;
                float score = Score(build, item);
                if (item != null && score > pick.Score)
                {
                    pick.Item = item;
                    pick.Score = score;
                }
            }
            return pick;
        }

        /// <summary>One random rare for a slot, of the same kind of item as what's there (the inventory's reroll button).</summary>
        public static ItemData RandomRare(System.Random rng, ItemData current, EquipSlot slot, int level, GauntletBuild build, ItemData mainHand)
        {
            string line = current != null ? ItemGenerator.LineOf(current.Id) : null;
            if (line == null)
            {
                string[] lines = LinesFor(build, slot, mainHand);
                if (lines.Length == 0) return null;
                line = lines[rng.Next(lines.Length)];
            }
            StatType? spell = null;
            if (current != null && ItemGenerator.AlwaysHasMainSkill(current))
                foreach (StatModifier m in current.Modifiers)
                    if (SkillGrants.IsMain(m.Stat) || Array.IndexOf(SkillGrants.MeleeSkills, m.Stat) >= 0) { spell = m.Stat; break; }
            string id = BestBase(line, level, 0) ?? current?.Id;
            return id == null ? null : ItemGenerator.Generate(rng, id, Mathf.Max(1, level), ItemRarity.Rare, spell);
        }

        // ------------------------------------------------------------------ uniques

        private static int UniqueIndex(string name)
        {
            for (int k = 0; k < UniqueItems.Count; k++)
                if (UniqueItems.NameOf(k) == name) return k;
            return -1;
        }

        /// <summary>The uniques a build may wear at this level, by priority, each with whether the build is made around it.</summary>
        private static List<KeyValuePair<ItemData, bool>> UniquesFor(System.Random rng, GauntletBuild build, int level, Result result)
        {
            var list = new List<KeyValuePair<ItemData, bool>>();
            foreach (string listed in build.Uniques)
            {
                bool essential = listed.StartsWith("!");
                string entry = listed.TrimStart('!');
                string name = entry;
                int from = 0;
                int at = entry.IndexOf('@');
                if (at >= 0)
                {
                    name = entry.Substring(0, at);
                    int.TryParse(entry.Substring(at + 1), out from);
                }
                int index = UniqueIndex(name);
                if (index < 0)
                {
                    result.Notes.Add("unknown unique '" + name + "' in build " + build.Id);
                    continue;
                }
                int needs = Mathf.Max(from, ItemGenerator.RequirementsOf(UniqueItems.BaseIdOf(index), name).Level);
                if (level < needs) continue;
                list.Add(new KeyValuePair<ItemData, bool>(UniqueItems.Create(index, rng), essential));
            }
            return list;
        }

        /// <summary>
        /// Swaps in up to two uniques: one the build is made around always, any other only where
        /// it scores at least as well as the best rare rolled for its slot.
        /// </summary>
        private static void ChooseUniques(System.Random rng, GauntletBuild build, int level, int rolls, Result result,
            Dictionary<EquipSlot, SlotPick> picks, HashSet<EquipSlot> uniqueSlots)
        {
            foreach (KeyValuePair<ItemData, bool> pair in UniquesFor(rng, build, level, result))
            {
                if (uniqueSlots.Count >= 2) break;
                ItemData unique = pair.Key;
                EquipSlot slot = SlotOf(unique, uniqueSlots);
                // A ring competes with the weaker of the two rare rings.
                if (unique.Type == ItemType.Ring && !uniqueSlots.Contains(EquipSlot.Ring1) && !uniqueSlots.Contains(EquipSlot.Ring2) &&
                    picks.TryGetValue(EquipSlot.Ring1, out SlotPick r1) && picks.TryGetValue(EquipSlot.Ring2, out SlotPick r2) && r2.Score < r1.Score)
                    slot = EquipSlot.Ring2;
                float uniqueScore = Score(build, unique);
                float rareScore = picks.TryGetValue(slot, out SlotPick rare) && rare.Item != null ? rare.Score : float.MinValue;
                if (!pair.Value && uniqueScore < rareScore)
                {
                    result.Notes.Add("kept a rare over " + unique.Name + " (score " + uniqueScore.ToString("0.#") + " vs " + rareScore.ToString("0.#") + ")");
                    continue;
                }
                uniqueSlots.Add(slot);
                picks[slot] = new SlotPick { Slot = slot, Item = unique, Score = uniqueScore, Unique = true,
                    Note = pair.Value ? "the build's unique" : "unique beat the best rare (" + rareScore.ToString("0.#") + ")" };
                if (slot == EquipSlot.MainHand && !uniqueSlots.Contains(EquipSlot.OffHand))
                {
                    // The off hand has to suit the new weapon (none at all for a two-hander).
                    string[] lines = LinesFor(build, EquipSlot.OffHand, unique);
                    ItemData off = picks.TryGetValue(EquipSlot.OffHand, out SlotPick o) ? o.Item : null;
                    if (lines.Length == 0) picks.Remove(EquipSlot.OffHand);
                    else if (off == null || !SlotRules.HandsCompatible(unique, off))
                        picks[EquipSlot.OffHand] = BestOf(rng, build, EquipSlot.OffHand, lines, level, rolls, 0);
                }
            }
        }

        private static EquipSlot SlotOf(ItemData item, ICollection<EquipSlot> taken)
        {
            foreach (EquipSlot slot in SlotRules.AllSlots)
                if (SlotRules.Accepts(slot, item) && !taken.Contains(slot)) return slot;
            return EquipSlot.Ring2;
        }

        // ------------------------------------------------------------------ the whole kit

        /// <summary>
        /// Rolls and equips a full kit (replacing whatever is worn), then swaps in uniques (see
        /// <see cref="ChooseUniques"/>). Bases a character can't meet
        /// the attributes for drop a tier and are rolled again; if no rolled piece carries one of
        /// the build's core skills, the main hand is rolled again in further batches.
        /// </summary>
        public static Result Equip(PlayerInventory inventory, GauntletBuild build, int level, int rolls, System.Random rng)
        {
            var result = new Result();
            var picks = new Dictionary<EquipSlot, SlotPick>();
            var stepsDown = new Dictionary<EquipSlot, int>();
            var lowered = new HashSet<EquipSlot>();
            foreach (EquipSlot slot in SlotRules.AllSlots)
                stepsDown[slot] = 0;

            var uniqueSlots = new HashSet<EquipSlot>();

            for (int attempt = 0; attempt < 4; attempt++)
            {
                foreach (EquipSlot slot in SlotRules.AllSlots)
                {
                    if (uniqueSlots.Contains(slot) || (picks.ContainsKey(slot) && attempt > 0 && stepsDown[slot] == 0))
                        continue;
                    if (slot == EquipSlot.OffHand) continue; // after the main hand is settled
                    picks[slot] = BestOf(rng, build, slot, LinesFor(build, slot, null), level, rolls, stepsDown[slot]);
                }
                if (!uniqueSlots.Contains(EquipSlot.OffHand) &&
                    (attempt == 0 || stepsDown[EquipSlot.OffHand] > 0 || stepsDown[EquipSlot.MainHand] > 0))
                {
                    ItemData main = picks.TryGetValue(EquipSlot.MainHand, out SlotPick m) ? m.Item : null;
                    picks[EquipSlot.OffHand] = BestOf(rng, build, EquipSlot.OffHand, LinesFor(build, EquipSlot.OffHand, main), level, rolls, stepsDown[EquipSlot.OffHand]);
                }

                // No core skill anywhere: keep rolling the main hand (counted in its Rolls).
                if (attempt == 0 && !uniqueSlots.Contains(EquipSlot.MainHand))
                {
                    var all = new List<ItemData>();
                    foreach (SlotPick p in picks.Values) all.Add(p.Item);
                    int batches = 0;
                    while (!HasCoreSkill(build, all) && batches < 40)
                    {
                        batches++;
                        SlotPick extra = BestOf(rng, build, EquipSlot.MainHand, LinesFor(build, EquipSlot.MainHand, null), level, rolls, 0);
                        if (!HasCoreSkill(build, new[] { extra.Item })) { picks[EquipSlot.MainHand].Rolls += extra.Rolls; continue; }
                        extra.Rolls += picks[EquipSlot.MainHand].Rolls;
                        extra.Note = "rolled " + batches + " extra batch(es) for a core skill";
                        all.Remove(picks[EquipSlot.MainHand].Item);
                        all.Add(extra.Item);
                        picks[EquipSlot.MainHand] = extra;
                    }
                    if (!HasCoreSkill(build, all))
                        result.Notes.Add("no core skill (" + string.Join(", ", build.CoreSkills) + ") rolled after " + batches + " extra batches");
                }
                if (attempt == 0)
                    ChooseUniques(rng, build, level, rolls, result, picks, uniqueSlots);

                Wear(inventory, picks);
                bool allActive = true;
                foreach (EquipSlot slot in SlotRules.AllSlots)
                {
                    stepsDown[slot] = 0;
                    if (inventory.Equipment.Get(slot) != null && !inventory.Equipment.IsActive(slot))
                    {
                        allActive = false;
                        if (uniqueSlots.Contains(slot))
                        {
                            // A unique the attributes can't carry: a rare goes there instead.
                            uniqueSlots.Remove(slot);
                            result.Notes.Add("dropped unique " + picks[slot].Item.Name + ": attribute requirements not met");
                        }
                        stepsDown[slot] = attempt + 1;
                        lowered.Add(slot);
                    }
                }
                if (allActive) break;
                if (attempt == 3) result.Notes.Add("some gear is still inactive (attribute requirements)");
            }

            foreach (EquipSlot slot in SlotRules.AllSlots)
                if (picks.TryGetValue(slot, out SlotPick p) && p.Item != null)
                {
                    if (lowered.Contains(slot) && !p.Unique) p.Note += (p.Note.Length > 0 ? "; " : "") + "lower tier base for attributes";
                    result.Picks.Add(p);
                }
            return result;
        }

        private static void Wear(PlayerInventory inventory, Dictionary<EquipSlot, SlotPick> picks)
        {
            foreach (EquipSlot slot in SlotRules.AllSlots)
                inventory.Equipment.Unequip(slot);
            foreach (EquipSlot slot in SlotRules.AllSlots)
                if (picks.TryGetValue(slot, out SlotPick p) && p.Item != null)
                    inventory.Equipment.Restore(slot, p.Item, out _);
        }

        /// <summary>The level 1 kit of the automated run: the starter gear a build would pick up.</summary>
        public static Result EquipStarter(PlayerInventory inventory, GauntletBuild build)
        {
            var result = new Result();
            string weapon = build.MainHand[0] == "staff" ? "gnarled_staff" : build.MainHand[0] == "bow" ? "short_bow"
                : build.MainHand[0] == "sceptre" ? "bone_sceptre" : "rusty_sword";
            foreach (ItemData item in ItemCatalog.CreateStarterItems())
            {
                if (item.Type == ItemType.Weapon && item.Id != weapon) continue;
                EquipSlot slot = SlotOf(item, new HashSet<EquipSlot>());
                inventory.Equipment.Restore(slot, item, out _);
                result.Picks.Add(new SlotPick { Slot = slot, Item = item, Score = Score(build, item), Note = "starter item" });
            }
            return result;
        }

        // ------------------------------------------------------------------ passives

        /// <summary>
        /// The passives a build takes with this many points: each wanted passive in turn by the
        /// shortest path from what's taken (skipped if it costs more than is left), then the
        /// remaining points one at a time on the neighbour its weights value most.
        /// </summary>
        public static List<string> PlanPassives(GauntletBuild build, int points, List<string> notes)
        {
            var taken = new HashSet<string> { PassiveTree.OriginId };
            var order = new List<string>();
            foreach (string target in build.Passives)
            {
                if (PassiveTree.Get(target) == null)
                {
                    notes?.Add("unknown passive '" + target + "' in build " + build.Id);
                    continue;
                }
                List<string> path = ShortestPath(taken, target);
                if (path == null || path.Count > points - order.Count) continue;
                foreach (string id in path)
                    if (taken.Add(id)) order.Add(id);
            }
            while (order.Count < points)
            {
                string best = null;
                float bestScore = float.MinValue;
                foreach (string id in taken)
                    foreach (string link in PassiveTree.Get(id).Links)
                    {
                        if (taken.Contains(link)) continue;
                        float score = NodeScore(build, PassiveTree.Get(link));
                        if (score > bestScore) { bestScore = score; best = link; }
                    }
                if (best == null) break;
                taken.Add(best);
                order.Add(best);
            }
            return order;
        }

        private static float NodeScore(GauntletBuild build, PassiveNode node)
        {
            float score = 0f;
            foreach (StatModifier m in node.Mods)
                score += build.Weight(m.Stat) * m.Value * UnitValue(m.Stat);
            return score;
        }

        // The untaken passives from the taken set to the target, nearest first.
        private static List<string> ShortestPath(HashSet<string> taken, string target)
        {
            if (taken.Contains(target)) return new List<string>();
            var from = new Dictionary<string, string>();
            var open = new Queue<string>();
            foreach (string id in taken)
            {
                open.Enqueue(id);
                from[id] = null;
            }
            while (open.Count > 0)
            {
                string at = open.Dequeue();
                if (at == target) break;
                foreach (string link in PassiveTree.Get(at).Links)
                {
                    if (from.ContainsKey(link)) continue;
                    from[link] = at;
                    open.Enqueue(link);
                }
            }
            if (!from.ContainsKey(target)) return null;
            var path = new List<string>();
            for (string at = target; at != null && !taken.Contains(at); at = from[at])
                path.Add(at);
            path.Reverse();
            return path;
        }

        /// <summary>Clears the tree and takes the build's plan with every point this level gives.</summary>
        public static List<string> AllocatePassives(PlayerPassives passives, GauntletBuild build, int level, List<string> notes)
        {
            passives.Allocation.ResetAll();
            List<string> plan = PlanPassives(build, PassiveAllocation.PointsForLevel(level), notes);
            foreach (string id in plan)
                if (!passives.Take(id))
                    notes?.Add("could not take passive " + id);
            return plan;
        }
    }
}
