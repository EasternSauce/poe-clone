using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.Skills;
using E = PoeClone.Inventory.WeaponSkillEffect;
using A = PoeClone.EditorTools.BalanceArchetypes.Archetype;

namespace PoeClone.EditorTools
{
    /// <summary>Boss single-target approximations in Edit mode. No scene, player, save or gameplay is touched.</summary>
    public static class OfflineBalance
    {
        public const string Model = "boss-single-target-v2";
        private static readonly BossStyle[] BossStyles = { BossStyle.Gravelord, BossStyle.Warlord, BossStyle.FrostQueen };
        private static EnemyKind[] benchmarkBosses;
        [Serializable] public sealed class Target
        {
            public string name, bossStyle, attackType;
            public int level;
            public float weight, life, armour, fireResistance, coldResistance, lightningResistance, poisonResistance,
                rawHit, attacksPerSecond;
        }
        public static List<Target> BossTargets()
        {
            var targets = new List<Target>();
            foreach (var boss in EnemyKinds.All.Where(e => e.IsBoss && BossStyles.Contains(e.Boss)))
                for (int level = 1; level <= 34; level++)
                {
                    float hit = boss.Damage * EnemyKinds.HitScale(level, boss, boss.AttackDamageType);
                    float rate = boss.Tempo / boss.AttackCooldown;
                    targets.Add(new Target { name = boss.Name, bossStyle = boss.Boss.ToString(), attackType = boss.AttackDamageType.ToString(),
                        level = level, weight = 1f / BossStyles.Length, life = boss.MaxHealth * EnemyKinds.LifeScale(level, boss),
                        armour = boss.Armour * EnemyKinds.ArmourScale(level), fireResistance = boss.FireResistance,
                        coldResistance = boss.ColdResistance, lightningResistance = boss.LightningResistance, poisonResistance = boss.PoisonResistance,
                        rawHit = hit, attacksPerSecond = rate });
                }
            return targets;
        }
        [Serializable] public sealed class Settings
        {
            public int seed = 20261014, samples = 20, rareRollsPerSlot = 20;
            public bool assumeDeathMarkAvailable = false;
            public float encounterSeconds = 30f, minimumMeleeHits = 4f, minimumMeleeSeconds = 6f;
            public float meleePremium = 1.5f, minionPremium = 1.2f, piercingPremium = 1.5f, outlierThreshold = .25f;
        }
        [Serializable] public sealed class Sample
        {
            public string archetype, passives, weaponNodes, gear, supports, excluded;
            public int level, sample, weaponRank;
            public float rawDps, sustainedDps, hitDps, dotDps, cost, usesPerSecond, manaPerSecond,
                supportManaPerSecond, manaPool, manaRegen, netManaDrain, secondsToEmpty,
                sustainableUseFraction, encounterUseFraction, life, incomingHit, incomingDps,
                hitsToDie, secondsToDie, lifeRecovery, minionLife, minionSecondsToDie,
                minionUptime, armyCost, replacementManaPerSecond, markUptime, unmarkedRawDps, fullSpeedManaPerSecond;
        }
        [Serializable] public sealed class Row
        {
            public string archetype, skill, name, family, status;
            public int level, attempted, viable, topCount;
            public float topDps, normalizedDps, baseline, ratio, viableFraction;
            public Sample representative;
        }
        [Serializable] public sealed class Report
        {
            public string generatedUtc, model = Model, sourceRevision;
            public Settings settings;
            public List<Target> targets;
            public List<A> archetypes;
            public List<Row> rows = new List<Row>();
            public List<Sample> samples = new List<Sample>();
            public string[] assumptions;
        }
        [Serializable] public sealed class RecalculationPool
        {
            public string sourceModel, sourceRevision, sourceGeneratedUtc;
            public Settings sourceSettings;
            public float minimumDpsFraction;
            public int originalSamples, originalViable, retainedSamples;
            public List<Sample> samples;
        }
        private sealed class Kit
        {
            public EquipmentSet equipment = new EquipmentSet { EnforceRequirements = true };
            public List<StatModifier> passives;
            public BaseStats basis;
            public ItemData weapon;
            public A archetype;
            public string[] passiveIds;
        }
        private static Report report;
        private static List<A> scheduled;
        private static int archetypeIndex, levelIndex;
        private static string state = "idle";
        private static readonly Dictionary<string, ItemData> baseItems = new Dictionary<string, ItemData>();
        private static readonly ConcurrentDictionary<string, List<string>> passivePaths = new ConcurrentDictionary<string, List<string>>();
        private static readonly ConcurrentDictionary<string, Func<System.Random, ItemData>> rareRollers = new ConcurrentDictionary<string, Func<System.Random, ItemData>>();

        [MenuItem("PoeClone/Balance/Run offline single-target survey")]
        public static void RunMenu() => Start();

        public static string Start(int samples = 20, int seed = 20261014)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Offline survey requires Edit mode.");
            if (report != null) throw new InvalidOperationException("Survey already running.");
            if (samples < 5) throw new ArgumentOutOfRangeException(nameof(samples));
            WarmCaches();
            report = new Report { generatedUtc = DateTime.UtcNow.ToString("o"), settings = new Settings { samples = samples, seed = seed },
                archetypes = BalanceArchetypes.Catalogue(), targets = BossTargets(), sourceRevision = ReadRevision(), assumptions = Assumptions };
            scheduled = report.archetypes;
            archetypeIndex = 0; levelIndex = 1;
            EditorApplication.update += Advance;
            return state = "started " + report.archetypes.Count + " archetypes";
        }
        /// <summary>Retest changed skills against an existing survey without rerolling every
        /// unrelated skill. The same seeds/settings apply, and every level baseline is rebuilt.</summary>
        public static string RefreshSkills(string skills)
        {
            if (report != null || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Requires an idle Editor in Edit mode.");
            var selected = new HashSet<string>(skills.Split(',').Select(s => s.Trim()));
            var data = JsonUtility.FromJson<Report>(File.ReadAllText("BalanceResults/survey.json"));
            if (data.model != Model) throw new InvalidOperationException("A fresh full boss survey is required before refreshing skills.");
            var catalogue = BalanceArchetypes.Catalogue();
            if (selected.Any(skill => !catalogue.Any(a => a.skill == skill))) throw new ArgumentException("Unknown skill");
            WarmCaches();
            data.targets = BossTargets(); data.archetypes = catalogue; data.assumptions = Assumptions; data.model = Model;
            data.generatedUtc = DateTime.UtcNow.ToString("o"); data.sourceRevision = ReadRevision();
            var ids = new HashSet<string>(catalogue.Where(a => selected.Contains(a.skill)).Select(a => a.id));
            data.rows.RemoveAll(r => ids.Contains(r.archetype)); data.samples.RemoveAll(s => ids.Contains(s.archetype));
            foreach (var row in data.rows)
            {
                row.name = catalogue.First(a => a.id == row.archetype).name;
                if (row.representative != null) FixEmptyTime(row.representative);
            }
            foreach (var sample in data.samples) FixEmptyTime(sample);
            report = data; scheduled = catalogue.Where(a => ids.Contains(a.id)).ToList();
            archetypeIndex = 0; levelIndex = 1; EditorApplication.update += Advance;
            return state = "refreshing " + scheduled.Count + " archetypes";
        }
        private static void FixEmptyTime(Sample sample) => sample.secondsToEmpty = TimeToEmpty(Mathf.Max(0, sample.manaPool - sample.armyCost),
            sample.manaPerSecond + sample.supportManaPerSecond, sample.manaRegen);
        public static string Status() => state;
        // Compare only within an archetype AND level. A weak skill or early level must
        // never disappear merely because a different skill or later level is stronger.
        public static List<Sample> RecalculationCandidates(List<Sample> samples, float minimumDpsFraction = .75f)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            if (float.IsNaN(minimumDpsFraction) || minimumDpsFraction <= 0 || minimumDpsFraction > 1)
                throw new ArgumentOutOfRangeException(nameof(minimumDpsFraction));
            return samples.Where(s => string.IsNullOrEmpty(s.excluded) && s.sustainedDps > 0 &&
                    !float.IsNaN(s.sustainedDps) && !float.IsInfinity(s.sustainedDps))
                .GroupBy(s => new { s.archetype, s.level })
                .SelectMany(g =>
                {
                    float cutoff = g.Max(build => build.sustainedDps) * minimumDpsFraction;
                    return g.Where(s => s.sustainedDps >= cutoff);
                })
                .OrderBy(s => s.archetype, StringComparer.Ordinal).ThenBy(s => s.level).ThenBy(s => s.sample).ToList();
        }
        public static string ExportRecalculationPool(float minimumDpsFraction = .75f)
        {
            if (report != null) throw new InvalidOperationException("Wait for the current survey to finish.");
            var data = JsonUtility.FromJson<Report>(File.ReadAllText("BalanceResults/survey.json"));
            return WriteRecalculationPool(data, minimumDpsFraction);
        }
        private static string WriteRecalculationPool(Report data, float minimumDpsFraction = .75f)
        {
            var samples = RecalculationCandidates(data.samples, minimumDpsFraction);
            var pool = new RecalculationPool { sourceModel = data.model, sourceRevision = data.sourceRevision,
                sourceGeneratedUtc = data.generatedUtc, sourceSettings = data.settings, minimumDpsFraction = minimumDpsFraction,
                originalSamples = data.samples.Count, originalViable = data.samples.Count(s => string.IsNullOrEmpty(s.excluded)),
                retainedSamples = samples.Count, samples = samples };
            const string path = "BalanceResults/recalculation-pool.json";
            File.WriteAllText(path, JsonUtility.ToJson(pool, true));
            return samples.Count + "/" + data.samples.Count + " builds retained; " + path;
        }
        public static string ValidateReportFile()
        {
            var data = JsonUtility.FromJson<Report>(File.ReadAllText("BalanceResults/survey.json"));
            if (data.model != Model || data.targets == null || data.targets.Count != BossStyles.Length * 34) return "Missing boss target metadata";
            if (data.targets.Any(t => t.weight <= 0 || t.armour < 0 || t.rawHit <= 0 || t.attacksPerSecond <= 0)) return "Invalid boss target metadata";
            if (data.rows.Count != data.archetypes.Count * 34) return "Missing archetype/level rows";
            if (data.rows.Select(r => r.archetype + "/" + r.level).Distinct().Count() != data.rows.Count) return "Duplicate rows";
            var byRow = data.samples.GroupBy(s => s.archetype + "/" + s.level).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var row in data.rows)
            {
                if (row.level < 1 || row.level > 34 || float.IsNaN(row.topDps) || float.IsInfinity(row.topDps)) return "Invalid row " + row.archetype;
                if (row.attempted > 0 && row.attempted != data.settings.samples) return "Missing samples";
                if (row.topCount != (int)Math.Ceiling(row.viable * .2)) return "Wrong top-fifth count";
                if (!byRow.TryGetValue(row.archetype + "/" + row.level, out var builds)) builds = new List<Sample>();
                if (builds.Count != row.attempted) return "Sample count mismatch";
                var valid = builds.Where(s => string.IsNullOrEmpty(s.excluded)).ToList();
                if (valid.Count != row.viable) return "Viability count mismatch";
                if (row.topCount > 0 && Mathf.Abs(TopFifth(valid).Average(s => s.sustainedDps) - row.topDps) > .001) return "Wrong cohort mean";
            }
            foreach (var s in data.samples)
            {
                if (s.encounterUseFraction < 0 || s.encounterUseFraction > 1 || s.minionUptime < 0 || s.minionUptime > 1 ||
                    float.IsNaN(s.sustainedDps) || float.IsInfinity(s.sustainedDps) || s.sustainedDps < 0 || s.sustainedDps > s.rawDps + .001) return "Invalid damage/sustain";
                if (Mathf.Abs(s.netManaDrain - (s.manaPerSecond + s.supportManaPerSecond - s.manaRegen)) > .001) return "Resource reconciliation";
                float emptyTime = TimeToEmpty(Mathf.Max(0, s.manaPool - s.armyCost), s.manaPerSecond + s.supportManaPerSecond, s.manaRegen);
                if (Mathf.Abs(s.secondsToEmpty - emptyTime) > .001) return "Initial army mana not reserved";
            }
            return "valid";
        }
        public static void Cancel()
        {
            EditorApplication.update -= Advance;
            report = null; state = "cancelled";
        }
        private static string ReadRevision()
        {
            string head = File.ReadAllText(".git/HEAD").Trim();
            return head.StartsWith("ref: ") && File.Exists(".git/" + head.Substring(5)) ? File.ReadAllText(".git/" + head.Substring(5)).Trim() : head;
        }
        private static void Advance()
        {
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) { Cancel(); return; }
                var a = scheduled[archetypeIndex];
                var row = Measure(a, levelIndex, report.settings);
                report.rows.Add(row);
                state = (archetypeIndex + 1) + "/" + scheduled.Count + " " + a.id + " L" + levelIndex;
                if (++levelIndex > 34) { levelIndex = 1; archetypeIndex++; }
                if (archetypeIndex < scheduled.Count) return;
                Normalize(report);
                Directory.CreateDirectory("BalanceResults");
                File.WriteAllText("BalanceResults/survey.json", JsonUtility.ToJson(report, true));
                WriteRecalculationPool(report);
                state = "complete: " + report.samples.Count + " builds, " + report.rows.Count + " rows; BalanceResults/survey.json";
                report = null;
                EditorApplication.update -= Advance;
                Debug.Log("OfflineBalance: " + state);
            }
            catch (Exception error)
            {
                Cancel(); state = "failed: " + error; Debug.LogException(error);
            }
        }
        // A trained weapon scenario, independent of random drop rank. Weapon XP can raise rank;
        // this is an explicit progression assumption, not a character-level equip restriction.
        public static int WeaponRank(int level) => 1 + (Mathf.Clamp(level, 1, 34) - 1) * 9 / 33;

        private static Row Measure(A a, int level, Settings cfg)
        {
            var row = new Row { archetype = a.id, skill = a.skill, name = a.name, family = a.family, level = level };
            if (a.family == "Support") { row.status = "Support only"; row.representative = SupportReference(a, level, cfg); return row; }
            if (a.minimumWeaponPoints > WeaponRank(level)) { row.status = "Weapon rank unavailable"; return row; }
            var results = new Sample[cfg.samples];
            // All assets and native-backed type initializers are loaded on the Editor thread.
            // Workers only construct ordinary C# equipment/stat data and calculate numbers.
            Parallel.For(0, cfg.samples, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Math.Min(4, Environment.ProcessorCount - 2)) }, i =>
            {
                // Stable across .NET versions and catalogue ordering; never string.GetHashCode.
                int seed = StableSeed(cfg.seed, a.id, level, i);
                var rng = new System.Random(seed);
                Kit kit = CreateKit(a, level, rng, cfg);
                var sample = Evaluate(kit, cfg);
                sample.archetype = a.id; sample.level = level; sample.sample = i; sample.weaponRank = WeaponRank(level);
                sample.passives = string.Join(",", kit.passiveIds);
                sample.weaponNodes = string.Join(",", kit.weapon.SkillProgress.nodes);
                sample.gear = string.Join("; ", SlotRules.AllSlots.Select(slot => slot + "=" + Describe(kit.equipment.GetActive(slot))));
                if (a.family == "Melee" && (sample.hitsToDie < cfg.minimumMeleeHits || sample.secondsToDie < cfg.minimumMeleeSeconds))
                    sample.excluded = "Melee survival";
                results[i] = sample;
            });
            var all = results.ToList();
            var viable = all.Where(s => string.IsNullOrEmpty(s.excluded)).ToList();
            report?.samples.AddRange(all);
            row.attempted = cfg.samples; row.viable = viable.Count; row.viableFraction = (float)viable.Count / cfg.samples;
            var top = TopFifth(viable);
            row.topCount = top.Count;
            if (top.Count == 0) { row.status = "No viable builds"; row.representative = all.OrderByDescending(s => s.sustainedDps).First(); return row; }
            row.topDps = top.Average(s => s.sustainedDps);
            row.normalizedDps = row.topDps / DpsTargetFactor(a.SkillId, a.family, cfg);
            // The representative is an actual sampled build nearest the cohort's mean, not
            // a collection of unrelated medians masquerading as one build.
            row.representative = top.OrderBy(s => Mathf.Abs(s.sustainedDps - row.topDps)).First();
            row.status = a.mechanics.Contains("Area") || a.mechanics.Contains("Projectiles") || a.mechanics.Contains("Range") ||
                a.mechanics.Contains("Explode") || a.mechanics.Contains("Shatter") ? "Coverage or utility route" : "Measured";
            return row;
        }
        private static string Describe(ItemData item) => item == null ? "empty" : item.Id + " [" +
            string.Join(",", item.Modifiers.Select(m => m.Stat + ":" + m.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))) + "]";
        public static List<Sample> TopFifth(List<Sample> samples) => samples.OrderByDescending(s => s.sustainedDps)
            .ThenBy(s => s.sample).Take((int)Math.Ceiling(samples.Count * .2)).ToList();
        public static int StableSeed(int seed, string id, int level, int sample)
        {
            unchecked { foreach (char c in id) seed = seed * 31 + c; return (seed * 31 + level) * 31 + sample; }
        }
        private static void Normalize(Report data)
        {
            foreach (var group in data.rows.GroupBy(r => r.level))
            {
                // One vote per skill, not per branch. Use the strongest available archetype
                // per skill so six coverage destinations do not drag down the baseline.
                var skills = group.Where(r => r.topCount > 0).GroupBy(r => r.skill).Select(g => g.Max(r => r.normalizedDps)).OrderBy(x => x).ToArray();
                float baseline = skills.Length == 0 ? 0 : (skills[(skills.Length - 1) / 2] + skills[skills.Length / 2]) / 2f;
                foreach (var row in group)
                {
                    row.baseline = baseline;
                    if (row.topCount == 0 || baseline <= 0) continue;
                    row.ratio = row.normalizedDps / baseline;
                    bool utility = data.archetypes.First(a => a.id == row.archetype).role != "Baseline" &&
                        IsUtilityDestination(data.archetypes.First(a => a.id == row.archetype));
                    row.status = row.ratio > 1f + data.settings.outlierThreshold ? "High DPS: review" :
                        row.ratio < 1f - data.settings.outlierThreshold ? utility ? "Low DPS: utility route" : "Low DPS: review" : "Within band";
                    if (row.viable < Math.Max(5, data.settings.samples / 5)) row.status = "Few viable builds: review survival first";
                }
            }
        }
        private static bool IsUtilityDestination(A a)
        {
            var ends = a.requiredNodes.Select(id => WeaponSkillTrees.Find(SkillBook.Get(a.SkillId).Grant, id)).Where(n => n.Keystone);
            return ends.Any() && ends.All(n => n.Bonuses.All(b => b.Effect == E.Area || b.Effect == E.Range || b.Effect == E.Projectiles ||
                b.Effect == E.Pierce || b.Effect == E.Fork || b.Effect == E.Chains || b.Effect == E.Explode || b.Effect == E.Shatter ||
                b.Effect == E.ProjectileSpeed || b.Effect == E.NoFork || b.Effect == E.MoreDamage || b.Effect == E.MinionMoveSpeed || b.Effect == E.RainSpread));
        }

        private static bool UnsupportedNode(PassiveNode node) => node.Mods.Any(m => m.Stat == StatType.BloodMagic || m.Stat == StatType.ManaAbsorb);
        private static List<StatModifier> Plan(A a, int level, System.Random rng, out string[] passiveIds)
        {
            var owned = new HashSet<string> { PassiveTree.OriginId };
            var plan = new List<string>();
            var targets = a.passivePool.Select(id => new { id, priority = rng.NextDouble() }).OrderBy(x => x.priority);
            int budget = PassiveAllocation.PointsForLevel(level);
            foreach (var target in targets)
            {
                // Shortest path from origin cached once, then pay only untaken nodes. The
                // origin paths guarantee connectivity even if different targets share travel.
                var path = PathTo(target.id);
                if (path.Any(id => UnsupportedNode(PassiveTree.Get(id)))) continue;
                var extra = path.Where(id => !owned.Contains(id)).ToArray();
                if (extra.Length > budget - plan.Count) continue;
                foreach (var id in extra) if (owned.Add(id)) plan.Add(id);
            }
            while (plan.Count < budget)
            {
                var frontier = owned.SelectMany(id => PassiveTree.Get(id).Links).Distinct().Where(id => !owned.Contains(id) && !UnsupportedNode(PassiveTree.Get(id))).ToArray();
                if (frontier.Length == 0) break;
                string next = frontier[rng.Next(frontier.Length)]; owned.Add(next); plan.Add(next);
            }
            passiveIds = plan.ToArray();
            var allocation = new PassiveAllocation();
            foreach (string id in plan) if (!allocation.Take(id, level)) throw new InvalidOperationException("Illegal passive path " + id);
            return plan.SelectMany(id => PassiveTree.Get(id).Mods).ToList();
        }
        private static List<string> PathTo(string target)
        {
            if (passivePaths.TryGetValue(target, out var path)) return path;
            var from = new Dictionary<string, string> { [PassiveTree.OriginId] = null };
            var queue = new Queue<string>(); queue.Enqueue(PassiveTree.OriginId);
            while (queue.Count > 0 && !from.ContainsKey(target))
            {
                string id = queue.Dequeue();
                foreach (var next in PassiveTree.Get(id).Links) if (!from.ContainsKey(next)) { from[next] = id; queue.Enqueue(next); }
            }
            path = new List<string>();
            for (string at = target; at != PassiveTree.OriginId; at = from[at]) path.Add(at);
            path.Reverse(); passivePaths[target] = path; return path;
        }

        private static Kit CreateKit(A a, int level, System.Random rng, Settings cfg)
        {
            var kit = new Kit { archetype = a, basis = Base(level) };
            kit.passives = Plan(a, level, rng, out kit.passiveIds);
            var sheet = StatSheet.Build(kit.basis, kit.equipment, kit.passives);
            // Weapon identity is part of the random build. Only compatible families are eligible.
            var bases = BasesFor(a, EquipSlot.MainHand, level, sheet, null);
            string baseId = bases[rng.Next(bases.Count)];
            kit.weapon = ItemGenerator.Generate(rng, baseId, level, ItemRarity.Normal, SkillBook.Get(a.SkillId).Grant);
            AllocateTree(kit.weapon, a, level, rng);
            kit.equipment.Restore(EquipSlot.MainHand, kit.weapon, out _);
            foreach (var slot in SlotRules.AllSlots)
            {
                sheet = StatSheet.Build(kit.basis, kit.equipment, kit.passives);
                bases = BasesFor(a, slot, level, sheet, kit.weapon);
                if (bases.Count == 0) continue;
                ItemData original = kit.equipment.Get(slot), best = null;
                float bestDps = -1f;
                for (int roll = 0; roll < cfg.rareRollsPerSlot; roll++)
                {
                    string id = bases[rng.Next(bases.Count)];
                    StatType? grant = slot == EquipSlot.MainHand ? (StatType?)SkillBook.Get(a.SkillId).Grant : null;
                    string rollerKey = id + "/" + level + "/" + grant;
                    if (!rareRollers.TryGetValue(rollerKey, out var roller)) rareRollers[rollerKey] = roller = ItemGenerator.PrepareRareRoller(id, level, grant);
                    var item = roller(rng);
                    if (slot == EquipSlot.MainHand) item.SkillProgress = kit.weapon.SkillProgress.Copy();
                    if (!kit.equipment.Restore(slot, item, out _)) continue;
                    var candidateSheet = StatSheet.Build(kit.basis, kit.equipment, kit.passives);
                    if (!kit.equipment.IsActive(slot) || !AllWornActive(kit.equipment)) continue;
                    var previousWeapon = kit.weapon;
                    if (slot == EquipSlot.MainHand) kit.weapon = item;
                    float dps = GearScore(kit, cfg, candidateSheet);
                    kit.weapon = previousWeapon;
                    if (dps > bestDps) { bestDps = dps; best = item; }
                }
                kit.equipment.Unequip(slot);
                if (best != null) kit.equipment.Restore(slot, best, out _);
                else if (original != null) kit.equipment.Restore(slot, original, out _);
                if (slot == EquipSlot.MainHand && best != null) kit.weapon = best;
            }
            StatSheet.Build(kit.basis, kit.equipment, kit.passives);
            return kit;
        }
        private static void WarmCaches()
        {
            benchmarkBosses = EnemyKinds.All.Where(e => e.IsBoss && BossStyles.Contains(e.Boss)).ToArray();
            if (benchmarkBosses.Length != BossStyles.Length) throw new InvalidOperationException("Missing reference boss definitions.");
            baseItems.Clear(); rareRollers.Clear();
            foreach (var id in ItemGenerator.BaseIds) baseItems[id] = ItemGenerator.Display(id, null, ItemRarity.Normal);
            ItemGenerator.Generate(new System.Random(0), "gnarled_staff", 1, ItemRarity.Normal);
            StatSheet.Build(Base(1), null);
            new WeaponSkillBonuses(null);
            SkillEffects.DamagePattern(SkillBook.All[0]);
            PlayerSkills.MeleeSpeed(SkillBook.All[0].Id);
            Minion.DamageAt(MinionKind.Warrior, 1);
            _ = EnemyKinds.All.Length;
        }
        private static bool AllWornActive(EquipmentSet equipment) => SlotRules.AllSlots.All(s => equipment.Get(s) == null || equipment.IsActive(s));
        private static List<string> BasesFor(A a, EquipSlot slot, int level, StatSheet sheet, ItemData main)
        {
            if (baseItems.Count == 0) foreach (var id in ItemGenerator.BaseIds) baseItems[id] = ItemGenerator.Display(id, null, ItemRarity.Normal);
            var candidates = baseItems.Values.Where(item => item != null && item.Requirements.Level <= level &&
                item.Requirements.MetBy(sheet) && SlotRules.Accepts(slot, item));
            if (slot == EquipSlot.MainHand) candidates = candidates.Where(item => WeaponSkillRules.Pool(item.Type, item.WeaponType).Contains(SkillBook.Get(a.SkillId).Grant));
            if (slot == EquipSlot.OffHand) candidates = candidates.Where(item => SlotRules.HandsCompatible(main, item) && item.Type != ItemType.Weapon && (item.Type != ItemType.Grimoire || a.family == "Minion"));
            // Latest available base in each line; choosing an old base would add loot noise
            // without representing a different build. The 20 rare rolls still vary affixes.
            return candidates.GroupBy(item => ItemGenerator.LineOf(item.Id)).Select(g => g.OrderByDescending(item => item.Requirements.Level).First().Id).OrderBy(id => id).ToList();
        }
        private static void AllocateTree(ItemData weapon, A a, int level, System.Random rng)
        {
            weapon.SkillProgress = new WeaponSkillProgress { level = WeaponRank(level) };
            foreach (string id in a.requiredNodes) if (!weapon.SkillProgress.Allocate(SkillBook.Get(a.SkillId).Grant, id)) throw new InvalidOperationException("Illegal archetype " + a.id);
            // Baseline deliberately has no destinations; other routes cannot silently acquire
            // an additional destination and be reported under the wrong archetype.
            while (weapon.SkillProgress.Available > 0)
            {
                var choices = WeaponSkillTrees.For(SkillBook.Get(a.SkillId).Grant).Where(n => !n.Keystone && weapon.SkillProgress.CanAllocate(n.Grant, n.Id)).ToArray();
                if (choices.Length == 0) break;
                var next = choices[rng.Next(choices.Length)]; weapon.SkillProgress.Allocate(next.Grant, next.Id);
            }
            weapon.SynchronizeSkillLevel();
        }
        private static BaseStats Base(int level)
        {
            return new BaseStats { Level = level }.Set(StatType.Strength, 9 + level).Set(StatType.Dexterity, 9 + level)
                .Set(StatType.Intelligence, 9 + level).Set(StatType.MaxLife, 90 + level * 10).Set(StatType.MaxMana, 45 + level * 5)
                .Set(StatType.PhysicalDamage, PlayerStatsLink.BasePhysicalDamage);
        }

        public static float ResourceCost(SkillDefinition skill, int level, WeaponType weapon, WeaponSkillBonuses tree, StatSheet sheet)
        {
            float cost = skill.ManaCostAt(1) * PlayerSkills.CharacterCostMultiplier(skill, level) + PlayerSkills.DamageSpellManaSurcharge(skill, level);
            if (skill.Spell) cost *= PlayerSkills.SpellManaCostMultiplier(level);
            if (skill.StaffSkill) cost *= Mathf.Lerp(.5f, 1, Mathf.SmoothStep(0, 1, (level - 1f) / 33f));
            if (skill.MeleeAttack && !skill.Channelled) cost *= SlotRules.BaseAttacksPerSecond(WeaponType.Sword) / Mathf.Max(.1f, SlotRules.BaseAttacksPerSecond(weapon));
            return cost * Mathf.Max(.1f, 1 + (tree?.Get(E.Cost) ?? 0) / 100) * Mathf.Clamp01(1 - sheet.Total(StatType.ManaCostReduction) / 100);
        }
        public static float TimeToEmpty(float pool, float spend, float regen) => spend > regen ? pool / (spend - regen) : -1;
        public static float DpsTargetFactor(SkillId skill, string family, Settings cfg) =>
            skill == SkillId.PiercingShot ? cfg.piercingPremium : family == "Melee" ? cfg.meleePremium : family == "Minion" ? cfg.minionPremium : 1f;
        public static float PiercingInterval(float attackRate, float chargeTimePercent) =>
            Mathf.Max(.65f, 1.6f * (1 + chargeTimePercent / 100) / attackRate) + .35f / attackRate;
        public static DamageType BaseElement(SkillDefinition skill) => skill.MeleeAttack || skill.Bow ?
            skill.Id == SkillId.BurningArrow ? DamageType.Fire : DamageType.Physical : SkillEffects.DamagePattern(skill);
        public static float UseFraction(float pool, float regen, float spend, float seconds) => spend <= 0 ? 1 : Mathf.Clamp01((pool / seconds + regen) / spend);
        private static Sample SupportReference(A a, int level, Settings cfg)
        {
            var sheet = StatSheet.Build(Base(level), null);
            var skill = SkillBook.Get(a.SkillId);
            float capacity = 1f / Mathf.Max(.01f, skill.UseInterval(sheet.CastRateMultiplier, sheet.CooldownRecoveryMultiplier));
            var s = new Sample { archetype = a.id, level = level, sample = -1, supports = "Support reference: no equipment or passives",
                cost = ResourceCost(skill, level, WeaponType.Sword, null, sheet), usesPerSecond = capacity,
                manaPool = sheet.Total(StatType.MaxMana), life = sheet.Total(StatType.MaxLife) };
            s.fullSpeedManaPerSecond = s.cost * capacity; s.manaPerSecond = s.fullSpeedManaPerSecond;
            s.manaRegen = DefenceMath.ManaRegenPerSecond(s.manaPool, sheet.Total(StatType.Intelligence));
            s.netManaDrain = s.manaPerSecond - s.manaRegen; FixEmptyTime(s);
            s.sustainableUseFraction = s.manaPerSecond <= 0 ? 1 : Mathf.Clamp01(s.manaRegen / s.manaPerSecond);
            s.encounterUseFraction = UseFraction(s.manaPool, s.manaRegen, s.manaPerSecond, cfg.encounterSeconds);
            return s;
        }
        private static float GearScore(Kit kit, Settings cfg, StatSheet prepared = null)
        {
            var s = Evaluate(kit, cfg, prepared);
            if (!string.IsNullOrEmpty(s.excluded)) return -1;
            float survival = kit.archetype.family == "Melee" ? Mathf.Min(1, s.hitsToDie / cfg.minimumMeleeHits) * Mathf.Min(1, s.secondsToDie / cfg.minimumMeleeSeconds) : 1;
            return s.sustainedDps * survival;
        }
        private static Sample Evaluate(Kit kit, Settings cfg, StatSheet prepared = null)
        {
            var sheet = prepared ?? StatSheet.Build(kit.basis, kit.equipment, kit.passives);
            var tree = new WeaponSkillBonuses(kit.weapon);
            var skill = SkillBook.Get(kit.archetype.SkillId);
            var s = new Sample { life = sheet.Total(StatType.MaxLife), manaPool = sheet.Total(StatType.MaxMana), minionUptime = 1 };
            s.rawDps = Damage(kit, sheet, tree, out s.hitDps, out s.dotDps, out s.usesPerSecond);
            s.cost = ResourceCost(skill, kit.basis.Level, kit.weapon.WeaponType, tree, sheet);
            float payments = skill.Channelled ? 1 / PlayerSkills.ChannelTick : s.usesPerSecond;
            s.manaPerSecond = s.cost * payments;
            s.fullSpeedManaPerSecond = s.manaPerSecond;
            s.manaRegen = DefenceMath.ManaRegenPerSecond(s.manaPool, sheet.Total(StatType.Intelligence), sheet.Total(StatType.ManaRegen), sheet.Total(StatType.PercentManaRegen));
            bool bloodMagic = sheet.Total(StatType.BloodMagic) > 0;
            if (bloodMagic) s.excluded = "Blood Magic not modelled";
            if (skill.Summon)
            {
                var kind = PlayerSkills.MinionKindOf(skill.Id);
                int cap = Minion.CapFor(kind, tree, sheet), batch = kind == MinionKind.Warrior || kind == MinionKind.Wolf ? 2 : 1;
                s.armyCost = Mathf.Ceil((float)cap / batch) * s.cost;
                s.minionLife = Minion.LifeFor(kind, 1, sheet, tree);
                Incoming(kit.basis.Level, sheet, tree, true, out var hit, out var dps);
                float recovery = s.minionLife * tree.Get(E.MinionRegen) / 100;
                s.minionSecondsToDie = MinionLifetime(s.minionLife, dps, cap, recovery, kind == MinionKind.Wolf);
                float duration = Minion.Duration(kind, 1, sheet) * tree.MinionDurationMultiplier;
                float lifetime = Math.Min(s.minionSecondsToDie < 0 ? 1e9f : s.minionSecondsToDie, duration <= 0 ? 1e9f : duration);
                float replacementUses = cap / Mathf.Max(.01f, lifetime) / batch;
                s.replacementManaPerSecond = replacementUses * s.cost;
                s.minionUptime = Mathf.Min(1, s.usesPerSecond / Mathf.Max(.000001f, replacementUses));
                s.manaPerSecond = Mathf.Min(replacementUses, s.usesPerSecond) * s.cost;
                // Recall Minions no longer supplies a mark; model sceptre marks only when explicitly assumed.
                bool marked = cfg.assumeDeathMarkAvailable;
                s.unmarkedRawDps = s.rawDps;
                s.supports = marked ? "Sceptre mark (assumed)" : "No Death Mark";
                if (marked)
                {
                    s.markUptime = 1f;
                    float mark = 1 + .3f * (1 + Mathf.Max(-100, sheet.Total(StatType.MarkEffect)) / 100) * s.markUptime;
                    s.hitDps *= mark; s.dotDps *= mark;
                    s.rawDps = s.hitDps + s.dotDps;
                }
            }
            float spend = s.manaPerSecond + s.supportManaPerSecond;
            s.netManaDrain = spend - s.manaRegen;
            s.secondsToEmpty = TimeToEmpty(Mathf.Max(0, s.manaPool - s.armyCost), spend, s.manaRegen);
            s.sustainableUseFraction = spend <= 0 ? 1 : Mathf.Clamp01(s.manaRegen / spend);
            s.encounterUseFraction = UseFraction(Mathf.Max(0, s.manaPool - s.armyCost), s.manaRegen, spend, cfg.encounterSeconds);
            if (s.cost > s.manaPool || s.armyCost > s.manaPool + s.manaRegen * cfg.encounterSeconds) s.excluded = "Cannot afford setup or one use";
            s.sustainedDps = s.rawDps * s.encounterUseFraction * s.minionUptime;
            Incoming(kit.basis.Level, sheet, tree, false, out s.incomingHit, out s.incomingDps);
            s.lifeRecovery = sheet.Total(StatType.NoLifeRegen) > 0 ? 0 : DefenceMath.LifeRegenPerSecond(s.life, sheet.Total(StatType.LifeRegen), sheet.Total(StatType.PercentLifeRegen));
            float leech = skill.Summon ? sheet.Total(StatType.SoulBond) : skill.MeleeAttack || skill.Bow ? sheet.Total(StatType.LifeLeech) : sheet.Total(StatType.SpellLeech);
            s.lifeRecovery += s.hitDps * s.encounterUseFraction * leech / 100;
            if (!skill.Summon) s.lifeRecovery += (tree.Get(E.LifeOnHit) + (skill.MeleeAttack || skill.Bow ? sheet.Total(StatType.LifeOnAttackHit) : 0)) * s.usesPerSecond * s.encounterUseFraction;
            s.hitsToDie = s.life / Mathf.Max(.01f, s.incomingHit);
            s.secondsToDie = s.incomingDps <= s.lifeRecovery ? -1 : s.life / (s.incomingDps - s.lifeRecovery);
            if (s.secondsToDie < 0) s.secondsToDie = 1e6f;
            return s;
        }

        private static float Damage(Kit kit, StatSheet sheet, WeaponSkillBonuses tree, out float hitDps, out float dotDps, out float uses) =>
            PoeClone.World.SingleTargetDamage.Estimate(SkillBook.Get(kit.archetype.SkillId), kit.weapon, sheet, tree, out hitDps, out dotDps, out uses);
        public static float ChanceUptime(float chancePercent, float rate, float duration) =>
            PoeClone.World.SingleTargetDamage.ChanceUptime(chancePercent, rate, duration);
        /// <summary>One boss's basic attacks are shared evenly across the targetable army; no splash is assumed.</summary>
        public static float MinionLifetime(float life, float bossDps, int cap, float recovery, bool untargetable) =>
            untargetable || bossDps / Mathf.Max(1, cap) <= recovery ? -1 : life / (bossDps / Mathf.Max(1, cap) - recovery);

        public static float BossMitigatedHit(float damage, float armour, float resistance, float penetration, bool physical, int level = 1) =>
            PoeClone.World.SingleTargetDamage.BossMitigatedHit(damage, armour, resistance, penetration, physical, level);
        private static void Incoming(int level, StatSheet sheet, WeaponSkillBonuses tree, bool minion, out float hit, out float dps)
        {
            hit = dps = 0; float weights = 0;
            foreach (var enemy in benchmarkBosses)
            {
                var type = enemy.AttackDamageType;
                float amount = enemy.Damage * EnemyKinds.HitScale(level, enemy, type);
                if (minion)
                    amount = Minion.DamageTaken(amount, type, sheet, tree);
                else
                {
                    if (type == DamageType.Physical) amount *= 1 - DefenceMath.ArmourReduction(sheet.Total(StatType.Armour), amount);
                    else amount = DefenceMath.AfterResistance(amount,
                        sheet.Total(type == DamageType.Fire ? StatType.FireResistance : type == DamageType.Cold ? StatType.ColdResistance : type == DamageType.Lightning ? StatType.LightningResistance : StatType.PoisonResistance));
                    amount *= Mathf.Max(0, 1 + sheet.Total(StatType.DamageTaken) / 100);
                }
                float avoided = minion ? 1 : (1 - DefenceMath.BlockChance(sheet.Total(StatType.BlockChance))) * (1 - DefenceMath.EvadeChance(sheet.Total(StatType.Evasion)));
                hit += amount;
                dps += amount * avoided * enemy.Tempo / enemy.AttackCooldown;
                weights += 1;
            }
            hit /= Mathf.Max(.01f, weights); dps /= Mathf.Max(.01f, weights);
        }
        private static readonly string[] Assumptions = {
            "Main spell base damage grows 6% of its level-one base per character level; minions grow 3.5% (golems 4.5%, vipers 1.22%). Weapon rank still grants allocation points only.",
            "Single stationary target at 6 units for ranged attacks. No clearing, boss phases, movement, kills, potions or unique items.",
            "Every slot rolls 20 rares. Greedy selection maximizes 30-second single-target DPS, including mana sustain. Melee scores multiply DPS by the fraction of each survival threshold met, capped at one. Equal scores keep the first roll. Requirements and hand compatibility are enforced.",
            "20 seeded builds per available archetype and level by default; the sample count is configurable. Random connected character passives, random compatible weapon family and nonterminal weapon nodes. Travel costs count. Top fifth means arithmetic mean of the best ceil(viable count / 5) builds by 30-second DPS.",
            "Weapon rank progresses from 1 at character level 1 to 10 at 34. This is a trained-weapon scenario, not an actual equip gate. Destinations are unavailable until this scenario has enough weapon points.",
            "Reference bosses: Gravelord Mortis, Ashen Warlord and Rimeheart, each weighted one-third and scaled to each character level 1-34. Targets export actual scaled life, armour, resistances and basic attacks. This continuous benchmark is not their campaign appearance schedule.",
            "Solo encounter: all skills face the same boss defences and basic attacks. Boss-capable executes apply; flinching and first-strike benefits are excluded.",
            "One reference boss attacks freely with its scaled basic hit and actual Tempo/AttackCooldown. No special attacks, phases or downtime. This is an explicit pressure test, not a full boss-fight prediction.",
            "Melee requires at least 4 average unavoided hits and 6 seconds survival with expected block/evasion and attack recovery. Leech is instantaneous in the game. No life/mana gain on kill is assumed.",
            "Minion pressure is one reference boss's basic attacks shared evenly across targetable minions (expected army attrition; no boss splash). Wolves cannot be targeted. Replacement demand includes death and expiration, batches and the main-skill 0.65s cooldown. Army setup cost is reserved from starting mana; no pre-summoning assumed.",
            "Mana use is gross demand at full speed, before shortages. 30-second and regeneration-only sustain are reported separately. Resource limits scale damage by affordable use fraction; no free basic-attack fallback is credited.",
            "Death Mark is used if rolled on the equipped grimoire. Refresh every 6 seconds; include its mana cost and Mark Effect scaling. No free support grant is assumed. Sceptres alone supply summon main skills.",
            "Support skills are not ranked for DPS. Their separate mana rows use an unequipped character with no passives as a reference. Main-build optional Grave Rot/War Cry/Frost Nova rotations are not estimated. Death Mark is included in minion builds when granted.",
            "Extra projectiles never shotgun. Rain uses impact-area/spread-area overlap expectation; Cross Slash assumes both cuts connect; Aftershock assumes one wave hits a stationary target. First-strike/cull/on-kill/Fire Orbit clear benefits are not credited.",
            "Poison stacks, clouds are limited to one creation per second, and ignite does not stack. Ailments use expected uptime and steady-state damage; secondary scaling, short-fight ramp and proc/crit variance are approximate. Enemy special skills, phase-specific wards and movement are excluded.",
            "DPS baseline is the median of each skill's best available normalized archetype at that level. Melee and Piercing Shot divide by 1.5; minions by 1.2. A 25% band flags review, not automatic tuning. Utility destinations get a separate low-DPS label.",
            "Blood Magic and Mind over Matter coupling require a resource time simulation, so paths containing those passives are excluded from the sampled pool. Shared stat investment is sampled; this is not an exhaustive search of every rare or passive combination."
        };
    }
}
