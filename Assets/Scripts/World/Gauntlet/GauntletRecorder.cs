using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.Skills;

namespace PoeClone.World
{
    /// <summary>
    /// Everything measured in one gauntlet run, written to GauntletResults/ (next to Assets) as a
    /// JSON file with every detail, a readable summary and one row of gauntlet_runs.csv.
    /// Time-to-kill runs from an enemy's first hit to its death; DPS is its life over that time.
    /// </summary>
    public sealed class GauntletRecorder
    {
        // ------------------------------------------------------------------ the report

        [Serializable] public sealed class StatLine { public string stat; public float value; }

        [Serializable]
        public sealed class GearRecord
        {
            public string slot, name, baseId, baseName, rarity, note;
            public int itemLevel, rolls;
            public float score;
            public List<string> mods = new List<string>();
        }

        [Serializable]
        public sealed class SkillRecord
        {
            public string skill, slot;
            public int level, casts;
            public float cost, cooldown, estimatedManaSpent;
        }

        [Serializable]
        public sealed class EnemyRecord
        {
            public string name, category;
            public int level, encounter;
            public float maxHealth, spawnedAt, firstHitAt = -1f, diedAt = -1f, damageTaken, timeToKill, dps;
            public bool oneShot;    // died to its first hit: no time to measure, left out of the DPS averages
        }

        [Serializable]
        public sealed class EncounterRecord
        {
            public int index;
            public string label;
            public List<string> enemies = new List<string>();
            public float spawnedAt, clearedAt = -1f, duration, lifeKilled;
            public int deaths;
            public bool timedOut;
        }

        [Serializable]
        public sealed class ChainRangeSummary
        {
            public int casts, closeCasts;
            public float totalDistance, minimumDistance, maximumDistance;

            public void Add(float distance)
            {
                if (casts == 0) minimumDistance = distance;
                casts++;
                if (distance < 3.5f) closeCasts++;
                totalDistance += distance;
                minimumDistance = Mathf.Min(minimumDistance, distance);
                maximumDistance = Mathf.Max(maximumDistance, distance);
            }
        }

        [Serializable]
        public sealed class BossRecord
        {
            public string name;
            public int level, deaths, healthPotions, manaPotions;
            public float maxHealth, spawnedAt, firstHitAt = -1f, diedAt = -1f, timeToKill, dps, dpsFromSpawn, damageTakenByPlayer, manaSpent;
            public bool timedOut;
            public ChainRangeSummary chainRange = new ChainRangeSummary();
        }

        [Serializable]
        public sealed class Aggregate
        {
            public int count, oneShots;
            // Times average every kill; DPS only kills that took longer than one hit.
            public float averageTimeToKill, medianTimeToKill, averageDps, pooledDps, totalLife;
        }

        [Serializable] public sealed class DeathRecord { public float at; public string during; }
        [Serializable] public sealed class RerollRecord { public float at; public string slot, before, after; }

        [Serializable]
        public sealed class ManaSummary
        {
            public float maxManaAtStart, maxManaAtEnd, manaSpent, lifeSpentOnSkills, manaAbsorbedFromHits;
            public float manaSpentPerCombatSecond, poolPercentPerCombatSecond, poolsSpentTotal, manaSpentPerKill;
            public float averageManaPercent, secondsBelow10Percent, percentOfCombatBelow10Percent;
            public int castsRefusedForCost, manaPotionsUsed, manaPotionRefills;
            public float manaPotionsPerCombatMinute;
        }

        [Serializable]
        public sealed class Report
        {
            public string mode, buildId, buildName, buildSummary, buildFingerprint, startedAt, finishedAt, unityVersion;
            public bool completed, enemiesFrozen;
            public bool coreSkillsOnly;
            public float requestedDistance;
            public ChainRangeSummary chainRange = new ChainRangeSummary();
            public int level, rollsPerSlot, seed;
            public float durationSeconds, combatSeconds;
            // Life of everything killed in the encounters over their total time, spawn to clear (walking included).
            public float encounterDps;
            public int deaths, kills, healthPotionsUsed, healthPotionRefills;
            public float damageTaken;
            public ManaSummary mana = new ManaSummary();
            public Aggregate normalEnemies = new Aggregate(), eliteEnemies = new Aggregate(), summonedEnemies = new Aggregate(), bosses = new Aggregate();
            public List<BossRecord> bossFights = new List<BossRecord>();
            public List<EncounterRecord> encounters = new List<EncounterRecord>();
            public List<StatLine> characterAtStart = new List<StatLine>(), characterAtEnd = new List<StatLine>();
            public List<string> passivesAtStart = new List<string>(), passivesAtEnd = new List<string>();
            public List<GearRecord> gearAtStart = new List<GearRecord>(), gearAtEnd = new List<GearRecord>();
            public List<SkillRecord> skills = new List<SkillRecord>();
            public List<RerollRecord> rerolls = new List<RerollRecord>();
            public List<DeathRecord> deathLog = new List<DeathRecord>();
            public List<EnemyRecord> enemies = new List<EnemyRecord>();
            public List<string> notes = new List<string>();
        }

        public readonly Report Data = new Report();

        // ------------------------------------------------------------------ live tracking

        private readonly PlayerStats player;
        private readonly PlayerInventory inventory;
        private readonly PlayerSkills skills;
        private readonly float startTime;
        private readonly Dictionary<EnemyHealth, EnemyRecord> tracked = new Dictionary<EnemyHealth, EnemyRecord>();
        private readonly Dictionary<EnemyHealth, string> pendingCategory = new Dictionary<EnemyHealth, string>();
        private readonly Dictionary<SkillId, int> casts = new Dictionary<SkillId, int>();
        private int lastCastNumber;
        private int healthPotions, manaPotions;
        private float manaPercentSum, sampledSeconds;

        /// <summary>The encounter (1-based) or boss fight new enemies belong to, and what the player is doing now.</summary>
        public int CurrentEncounter;
        public BossRecord CurrentBoss;
        public string Phase = "start room";
        public bool InCombat;

        public GauntletRecorder(PlayerStats player, string mode, GauntletBuild build, int level, int rolls, int seed)
        {
            this.player = player;
            inventory = player.GetComponent<PlayerInventory>();
            skills = player.GetComponent<PlayerSkills>();
            startTime = Time.time;
            Data.mode = mode;
            Data.buildId = build.Id;
            Data.buildName = build.Name;
            Data.buildSummary = build.Summary;
            Data.buildFingerprint = build.Fingerprint;
            Data.level = level;
            Data.rollsPerSlot = rolls;
            Data.seed = seed;
            Data.startedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            Data.unityVersion = Application.unityVersion;
            Data.coreSkillsOnly = MinimalCombatMode.Option("coreonly") == "1";
            float.TryParse(MinimalCombatMode.Option("distance"), NumberStyles.Float, CultureInfo.InvariantCulture, out Data.requestedDistance);
            lastCastNumber = skills != null ? skills.CastCount : 0;

            player.Died += OnDied;
            player.DamageTaken += OnDamageTaken;
            player.SkillCostPaid += OnCostPaid;
            player.SkillCostRefused += OnCostRefused;
            player.ManaAbsorbedHit += OnManaAbsorbed;
            inventory.PotionsChanged += OnPotionsChanged;
            healthPotions = inventory.HealthPotions;
            manaPotions = inventory.ManaPotions;
        }

        public void Detach()
        {
            player.Died -= OnDied;
            player.DamageTaken -= OnDamageTaken;
            player.SkillCostPaid -= OnCostPaid;
            player.SkillCostRefused -= OnCostRefused;
            player.ManaAbsorbedHit -= OnManaAbsorbed;
            inventory.PotionsChanged -= OnPotionsChanged;
        }

        // Game time since the kit went on (Play stopping mid-run can't wind it back).
        private float lastNow;
        public float Now => lastNow = Mathf.Max(lastNow, Time.time - startTime);

        /// <summary>Snapshot of the character as the run starts (after the kit is on).</summary>
        public void CaptureStart(List<GauntletLoadout.SlotPick> picks)
        {
            // Whatever was tried out in the start room doesn't count.
            Data.mana = new ManaSummary();
            Data.chainRange = new ChainRangeSummary();
            Data.damageTaken = 0f;
            Data.deaths = 0;
            Data.deathLog.Clear();
            casts.Clear();
            lastCastNumber = skills != null ? skills.CastCount : 0;
            Data.mana.maxManaAtStart = player.MaxMana;
            Data.characterAtStart = Sheet();
            Data.passivesAtStart = Passives();
            Data.gearAtStart = Gear(picks);
            healthPotions = inventory.HealthPotions;
            manaPotions = inventory.ManaPotions;
        }

        /// <summary>The gauntlet spawned this enemy: it counts as "normal", "elite" or "boss" (anything else that appears is "summoned").</summary>
        public void Expect(EnemyHealth enemy, string category)
        {
            pendingCategory[enemy] = category;
            Track(enemy);
        }

        public void Tick()
        {
            foreach (EnemyHealth e in EnemyHealth.Active)
                if (e != null && !e.IsDead && !e.IsTrainingDummy && !tracked.ContainsKey(e))
                    Track(e);

            if (skills != null && skills.CastCount != lastCastNumber)
            {
                foreach (PlayerSkills.CastRecord c in skills.RecentCasts(30f))
                    if (c.Number > lastCastNumber && c.Skill >= 0)
                    {
                        casts[c.Skill] = (casts.TryGetValue(c.Skill, out int n) ? n : 0) + 1;
                        if (c.Skill == SkillId.ChainLightning && c.Points != null && c.Points.Length > 1)
                        {
                            Vector3 offset = c.Points[1] - c.At;
                            offset.y = 0f;
                            Data.chainRange.Add(offset.magnitude);
                            if (CurrentBoss != null) CurrentBoss.chainRange.Add(offset.magnitude);
                        }
                    }
                lastCastNumber = skills.CastCount;
            }

            if (InCombat && !player.IsDead)
            {
                float dt = Time.deltaTime;
                Data.combatSeconds += dt;
                float pct = player.MaxMana > 0f ? player.CurrentMana / player.MaxMana : 0f;
                manaPercentSum += pct * dt;
                sampledSeconds += dt;
                if (pct < 0.1f) Data.mana.secondsBelow10Percent += dt;
            }
        }

        private void Track(EnemyHealth enemy)
        {
            if (tracked.ContainsKey(enemy)) return;
            string category = pendingCategory.TryGetValue(enemy, out string c) ? c : "summoned";
            var record = new EnemyRecord
            {
                name = enemy.DisplayName, category = category, level = enemy.MonsterLevel, encounter = CurrentEncounter,
                maxHealth = enemy.MaxHealth, spawnedAt = Now
            };
            tracked[enemy] = record;
            Data.enemies.Add(record);
            enemy.DamageApplied += amount =>
            {
                if (record.firstHitAt < 0f) record.firstHitAt = Now;
                record.damageTaken += amount;
                if (CurrentBoss != null && category == "boss" && CurrentBoss.firstHitAt < 0f) CurrentBoss.firstHitAt = Now;
            };
            enemy.Died += () =>
            {
                record.diedAt = Now;
                if (record.firstHitAt >= 0f)
                {
                    record.timeToKill = record.diedAt - record.firstHitAt;
                    record.oneShot = record.timeToKill < 0.05f;
                    record.dps = record.oneShot ? 0f : record.maxHealth / record.timeToKill;
                }
                if (record.encounter > 0 && record.encounter <= Data.encounters.Count)
                    Data.encounters[record.encounter - 1].lifeKilled += record.maxHealth;
                Data.kills++;
            };
        }

        private void OnDied()
        {
            Data.deaths++;
            Data.deathLog.Add(new DeathRecord { at = Now, during = Phase });
            if (CurrentBoss != null) CurrentBoss.deaths++;
            else if (CurrentEncounter > 0 && CurrentEncounter <= Data.encounters.Count) Data.encounters[CurrentEncounter - 1].deaths++;
        }

        private void OnDamageTaken(float amount)
        {
            Data.damageTaken += amount;
            if (CurrentBoss != null) CurrentBoss.damageTakenByPlayer += amount;
        }

        private void OnCostPaid(float amount, bool life)
        {
            if (life) Data.mana.lifeSpentOnSkills += amount;
            else
            {
                Data.mana.manaSpent += amount;
                if (CurrentBoss != null) CurrentBoss.manaSpent += amount;
            }
        }

        private void OnCostRefused() => Data.mana.castsRefusedForCost++;
        private void OnManaAbsorbed(float amount) => Data.mana.manaAbsorbedFromHits += amount;

        private void OnPotionsChanged()
        {
            int h = inventory.HealthPotions, m = inventory.ManaPotions;
            if (h < healthPotions)
            {
                Data.healthPotionsUsed += healthPotions - h;
                if (CurrentBoss != null) CurrentBoss.healthPotions += healthPotions - h;
            }
            if (m < manaPotions)
            {
                Data.mana.manaPotionsUsed += manaPotions - m;
                if (CurrentBoss != null) CurrentBoss.manaPotions += manaPotions - m;
            }
            healthPotions = h;
            manaPotions = m;
        }

        /// <summary>Potions handed out again by the automated run (counted, not as used).</summary>
        public void Refilled(bool health)
        {
            if (health) Data.healthPotionRefills++;
            else Data.mana.manaPotionRefills++;
            healthPotions = inventory.HealthPotions;
            manaPotions = inventory.ManaPotions;
        }

        public void Rerolled(EquipSlot slot, ItemData before, ItemData after)
        {
            Data.rerolls.Add(new RerollRecord { at = Now, slot = slot.ToString(), before = before != null ? before.Name : "(empty)", after = after != null ? after.Name : "(empty)" });
        }

        // ------------------------------------------------------------------ snapshots

        private List<StatLine> Sheet()
        {
            var list = new List<StatLine>
            {
                new StatLine { stat = "Level", value = player.Level },
                new StatLine { stat = "Life", value = player.MaxHealth },
                new StatLine { stat = "Mana", value = player.MaxMana },
                new StatLine { stat = "Strength (total)", value = player.Strength },
                new StatLine { stat = "Dexterity (total)", value = player.Dexterity },
                new StatLine { stat = "Intelligence (total)", value = player.Intelligence }
            };
            StatSheet sheet = inventory.Stats;
            if (sheet != null)
            {
                list.Add(new StatLine { stat = "Attack critical chance %", value = sheet.AttackCriticalChance });
                list.Add(new StatLine { stat = "Cast rate multiplier", value = sheet.CastRateMultiplier });
                foreach (StatType stat in Enum.GetValues(typeof(StatType)))
                {
                    float v = sheet.Total(stat);
                    if (Mathf.Abs(v) > 0.0001f) list.Add(new StatLine { stat = stat.ToString(), value = v });
                }
            }
            return list;
        }

        private List<string> Passives()
        {
            var list = new List<string>();
            PlayerPassives passives = player.GetComponent<PlayerPassives>();
            if (passives == null) return list;
            foreach (string id in passives.Allocation.Taken)
            {
                if (id == PassiveTree.OriginId) continue;
                PassiveNode node = PassiveTree.Get(id);
                var mods = new List<string>();
                foreach (StatModifier m in node.Mods) mods.Add(m.Stat + " " + m.Value.ToString("0.##", CultureInfo.InvariantCulture));
                list.Add(id + (node.Name != null ? " (" + node.Name + (node.Keystone ? ", keystone" : node.Notable ? ", notable" : "") + ")" : "") +
                    ": " + string.Join(", ", mods.ToArray()));
            }
            list.Sort();
            return list;
        }

        private List<GearRecord> Gear(List<GauntletLoadout.SlotPick> picks)
        {
            var list = new List<GearRecord>();
            foreach (EquipSlot slot in SlotRules.AllSlots)
            {
                ItemData item = inventory.Equipment.Get(slot);
                if (item == null) continue;
                GauntletLoadout.SlotPick pick = picks?.Find(p => p.Item == item);
                var g = new GearRecord
                {
                    slot = slot + (inventory.Equipment.IsActive(slot) ? "" : " (INACTIVE: requirements)"),
                    name = item.Name, baseId = item.Id, baseName = item.BaseName, rarity = item.Rarity.ToString(),
                    itemLevel = item.ItemLevel, rolls = pick != null ? pick.Rolls : 0, score = pick != null ? pick.Score : 0f,
                    note = pick != null ? pick.Note : ""
                };
                foreach (StatModifier m in item.Modifiers)
                    g.mods.Add(m.Stat + " " + m.Value.ToString("0.##", CultureInfo.InvariantCulture));
                list.Add(g);
            }
            return list;
        }

        // ------------------------------------------------------------------ finishing

        private static Aggregate Summarise(List<EnemyRecord> kills)
        {
            var a = new Aggregate { count = kills.Count };
            if (kills.Count == 0) return a;
            float sumT = 0f, sumL = 0f, sumDps = 0f, timedT = 0f, timedL = 0f;
            var sorted = new List<float>();
            foreach (EnemyRecord e in kills)
            {
                sumT += e.timeToKill;
                sumL += e.maxHealth;
                sorted.Add(e.timeToKill);
                if (e.oneShot) { a.oneShots++; continue; }
                sumDps += e.dps;
                timedT += e.timeToKill;
                timedL += e.maxHealth;
            }
            sorted.Sort();
            int timed = kills.Count - a.oneShots;
            a.averageTimeToKill = sumT / kills.Count;
            a.medianTimeToKill = sorted[sorted.Count / 2];
            a.averageDps = timed > 0 ? sumDps / timed : 0f;
            a.pooledDps = timed > 0 ? timedL / Mathf.Max(0.01f, timedT) : 0f;
            a.totalLife = sumL;
            return a;
        }

        public void Finish(bool completed, List<GauntletLoadout.SlotPick> startPicks)
        {
            Data.completed = completed;
            Data.finishedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            Data.durationSeconds = Now;
            Data.characterAtEnd = Sheet();
            Data.passivesAtEnd = Passives();
            Data.gearAtEnd = Gear(startPicks);

            var byCategory = new Dictionary<string, List<EnemyRecord>>();
            foreach (EnemyRecord e in Data.enemies)
            {
                if (e.diedAt < 0f || e.firstHitAt < 0f) continue;
                if (!byCategory.TryGetValue(e.category, out var list))
                    byCategory[e.category] = list = new List<EnemyRecord>();
                list.Add(e);
            }
            Aggregate Of(string c) => byCategory.TryGetValue(c, out var l) ? Summarise(l) : new Aggregate();
            Data.normalEnemies = Of("normal");
            Data.eliteEnemies = Of("elite");
            Data.summonedEnemies = Of("summoned");
            Data.bosses = Of("boss");
            float encounterTime = 0f, encounterLife = 0f;
            foreach (EncounterRecord enc in Data.encounters)
                if (enc.clearedAt >= 0f)
                {
                    enc.duration = enc.clearedAt - enc.spawnedAt;
                    encounterTime += enc.duration;
                    encounterLife += enc.lifeKilled;
                }
            Data.encounterDps = encounterTime > 0f ? encounterLife / encounterTime : 0f;

            ManaSummary m = Data.mana;
            m.maxManaAtEnd = player.MaxMana;
            float combat = Mathf.Max(0.01f, Data.combatSeconds);
            m.manaSpentPerCombatSecond = m.manaSpent / combat;
            m.poolPercentPerCombatSecond = m.maxManaAtStart > 0f ? 100f * m.manaSpentPerCombatSecond / m.maxManaAtStart : 0f;
            m.poolsSpentTotal = m.maxManaAtStart > 0f ? m.manaSpent / m.maxManaAtStart : 0f;
            m.manaSpentPerKill = Data.kills > 0 ? m.manaSpent / Data.kills : 0f;
            m.averageManaPercent = sampledSeconds > 0f ? 100f * manaPercentSum / sampledSeconds : 0f;
            m.percentOfCombatBelow10Percent = 100f * m.secondsBelow10Percent / combat;
            m.manaPotionsPerCombatMinute = m.manaPotionsUsed / (combat / 60f);

            Data.skills.Clear();
            if (skills != null)
            {
                var seen = new HashSet<SkillId>();
                SkillId? main = skills.MainSkill;
                if (main != null) AddSkill(main.Value, "attack", seen);
                for (int k = 0; k < SkillBook.SlotCount; k++)
                {
                    SkillId? id = skills.Slot(k);
                    if (id != null) AddSkill(id.Value, PlayerSkills.KeyLabel(k), seen);
                }
                foreach (var pair in casts)
                    if (!seen.Contains(pair.Key)) AddSkill(pair.Key, "(no longer worn)", seen);
            }
        }

        private void AddSkill(SkillId id, string slot, HashSet<SkillId> seen)
        {
            if (!seen.Add(id)) return;
            int n = casts.TryGetValue(id, out int c) ? c : 0;
            float cost = skills.ManaCost(id);
            Data.skills.Add(new SkillRecord
            {
                skill = SkillBook.Get(id).Name, slot = slot, level = skills.Level(id), casts = n,
                cost = cost, cooldown = skills.Cooldown(id), estimatedManaSpent = n * cost
            });
        }

        // ------------------------------------------------------------------ files

        public static string ResultsFolder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "GauntletResults"));

        /// <summary>Writes the JSON, the summary and the CSV row; returns the summary's path.</summary>
        public string Write()
        {
#if UNITY_EDITOR
            Directory.CreateDirectory(ResultsFolder);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string fileName = stamp + "_" + Data.mode + "_" + Data.buildId + "_L" + Data.level;
            foreach (char invalid in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(invalid, '_');
            string stem = Path.Combine(ResultsFolder, fileName);
            File.WriteAllText(stem + ".json", JsonUtility.ToJson(Data, true));
            File.WriteAllText(stem + ".txt", Summary());
            string csv = Path.Combine(ResultsFolder, "gauntlet_runs.csv");
            bool fresh = !File.Exists(csv);
            File.AppendAllText(csv, (fresh ? CsvHeader + "\n" : "") + CsvRow() + "\n");
            return stem + ".txt";
#else
            return null;
#endif
        }

        private const string CsvHeader =
            "finishedAt,mode,build,fingerprint,level,completed,durationS,combatS,deaths,kills,encounterDPS," +
            "normalCount,normalOneShots,normalAvgTTK,normalAvgDPS,normalPooledDPS,eliteCount,eliteAvgTTK,eliteAvgDPS," +
            "bossCount,bossAvgTTK,bossAvgDPS,bossDeaths,boss1,boss1TTK,boss1DPS,boss2,boss2TTK,boss2DPS,boss3,boss3TTK,boss3DPS," +
            "life,maxMana,manaSpent,manaPerCombatS,poolPctPerCombatS,manaPerKill,avgManaPct,pctCombatBelow10PctMana,refusedCasts," +
            "manaPotions,manaPotionRefills,manaPotionsPerMin,healthPotions,lifeSpentOnSkills,damageTaken,rerolls";

        private static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        private static string Q(string s) => "\"" + (s ?? "").Replace("\"", "'") + "\"";

        private string CsvRow()
        {
            Report d = Data;
            ManaSummary m = d.mana;
            int bossDeaths = 0;
            foreach (BossRecord b in d.bossFights) bossDeaths += b.deaths;
            var sb = new StringBuilder();
            sb.Append(Q(d.finishedAt)).Append(',').Append(d.mode).Append(',').Append(d.buildId).Append(',').Append(d.buildFingerprint).Append(',')
              .Append(d.level).Append(',').Append(d.completed).Append(',').Append(F(d.durationSeconds)).Append(',').Append(F(d.combatSeconds)).Append(',')
              .Append(d.deaths).Append(',').Append(d.kills).Append(',').Append(F(d.encounterDps)).Append(',')
              .Append(d.normalEnemies.count).Append(',').Append(d.normalEnemies.oneShots).Append(',').Append(F(d.normalEnemies.averageTimeToKill)).Append(',').Append(F(d.normalEnemies.averageDps)).Append(',').Append(F(d.normalEnemies.pooledDps)).Append(',')
              .Append(d.eliteEnemies.count).Append(',').Append(F(d.eliteEnemies.averageTimeToKill)).Append(',').Append(F(d.eliteEnemies.averageDps)).Append(',')
              .Append(d.bosses.count).Append(',').Append(F(d.bosses.averageTimeToKill)).Append(',').Append(F(d.bosses.averageDps)).Append(',').Append(bossDeaths);
            for (int k = 0; k < 3; k++)
            {
                BossRecord b = k < d.bossFights.Count ? d.bossFights[k] : null;
                sb.Append(',').Append(Q(b?.name)).Append(',').Append(b != null ? F(b.timeToKill) : "").Append(',').Append(b != null ? F(b.dps) : "");
            }
            float life = 0f;
            foreach (StatLine s in d.characterAtStart) if (s.stat == "Life") life = s.value;
            sb.Append(',').Append(F(life)).Append(',').Append(F(m.maxManaAtStart)).Append(',').Append(F(m.manaSpent)).Append(',')
              .Append(F(m.manaSpentPerCombatSecond)).Append(',').Append(F(m.poolPercentPerCombatSecond)).Append(',').Append(F(m.manaSpentPerKill)).Append(',')
              .Append(F(m.averageManaPercent)).Append(',').Append(F(m.percentOfCombatBelow10Percent)).Append(',').Append(m.castsRefusedForCost).Append(',')
              .Append(m.manaPotionsUsed).Append(',').Append(m.manaPotionRefills).Append(',').Append(F(m.manaPotionsPerCombatMinute)).Append(',')
              .Append(d.healthPotionsUsed).Append(',').Append(F(m.lifeSpentOnSkills)).Append(',').Append(F(d.damageTaken)).Append(',').Append(d.rerolls.Count);
            return sb.ToString();
        }

        public string Summary()
        {
            Report d = Data;
            ManaSummary m = d.mana;
            var sb = new StringBuilder();
            sb.AppendLine("CLASS GAUNTLET - " + d.buildName + " (" + d.buildId + ", build " + d.buildFingerprint + "), level " + d.level);
            sb.AppendLine(d.mode + (d.enemiesFrozen ? " run, enemies frozen" : " run") + ", " + (d.completed ? "completed" : "NOT completed") +
                ", " + d.startedAt + " to " + d.finishedAt + ", " + d.rollsPerSlot + " rolls per slot, seed " + d.seed);
            sb.AppendLine(d.buildSummary);
            sb.AppendLine();
            sb.AppendLine("Time " + F(d.durationSeconds) + "s (in combat " + F(d.combatSeconds) + "s). Deaths " + d.deaths + ". Kills " + d.kills +
                ". Life lost " + F(d.damageTaken) + ". Health potions " + d.healthPotionsUsed + (d.healthPotionRefills > 0 ? " (refilled " + d.healthPotionRefills + "x)" : "") + ".");
            sb.AppendLine();
            sb.AppendLine("KILL SPEED (first hit to death; DPS = enemy life / that time, one-hit kills left out of DPS)");
            sb.AppendLine("  encounters: " + F(d.encounterDps) + " life killed per second, spawn to clear (walking and targeting included)");
            void Agg(string label, Aggregate a)
            {
                if (a.count == 0) return;
                sb.AppendLine("  " + label.PadRight(9) + a.count + " killed (" + a.oneShots + " in one hit), average " + F(a.averageTimeToKill) + "s (median " + F(a.medianTimeToKill) +
                    "s), average DPS " + F(a.averageDps) + ", pooled DPS " + F(a.pooledDps));
            }
            Agg("normal", d.normalEnemies);
            Agg("elite", d.eliteEnemies);
            Agg("summoned", d.summonedEnemies);
            Agg("bosses", d.bosses);
            foreach (BossRecord b in d.bossFights)
                sb.AppendLine("  boss     " + b.name + " L" + b.level + ": " + (b.timedOut ? "TIMED OUT" : F(b.timeToKill) + "s, DPS " + F(b.dps) + " (" + F(b.dpsFromSpawn) + " from spawn)") +
                    ", life " + F(b.maxHealth) + ", deaths " + b.deaths + ", potions " + b.healthPotions + " health / " + b.manaPotions + " mana, mana spent " + F(b.manaSpent));
            sb.AppendLine();
            sb.AppendLine("MANA (pool " + F(m.maxManaAtStart) + ")");
            sb.AppendLine("  spent " + F(m.manaSpent) + " = " + F(m.poolsSpentTotal) + " pools; " + F(m.manaSpentPerCombatSecond) + "/s in combat = " +
                F(m.poolPercentPerCombatSecond) + "% of the pool per second; " + F(m.manaSpentPerKill) + " per kill");
            sb.AppendLine("  average mana " + F(m.averageManaPercent) + "%, under 10% for " + F(m.secondsBelow10Percent) + "s (" + F(m.percentOfCombatBelow10Percent) +
                "% of combat), " + m.castsRefusedForCost + " casts refused for cost");
            sb.AppendLine("  mana potions " + m.manaPotionsUsed + " (" + F(m.manaPotionsPerCombatMinute) + "/min in combat)" + (m.manaPotionRefills > 0 ? ", refilled " + m.manaPotionRefills + "x" : ""));
            if (m.lifeSpentOnSkills > 0f) sb.AppendLine("  life spent on skills (Blood Magic) " + F(m.lifeSpentOnSkills));
            if (m.manaAbsorbedFromHits > 0f) sb.AppendLine("  mana lost soaking hits (Mind over Matter) " + F(m.manaAbsorbedFromHits));
            sb.AppendLine();
            sb.AppendLine("SKILLS");
            foreach (SkillRecord s in d.skills)
                sb.AppendLine("  " + s.skill + " L" + s.level + " [" + s.slot + "]: " + s.casts + " casts, cost " + F(s.cost) + ", cooldown " + F(s.cooldown) + "s, ~" + F(s.estimatedManaSpent) + " mana");
            sb.AppendLine();
            sb.AppendLine("GEAR AT START");
            foreach (GearRecord g in d.gearAtStart)
                sb.AppendLine("  " + g.slot + ": " + g.name + " (" + g.rarity + " " + g.baseName + ", ilvl " + g.itemLevel + (g.rolls > 0 ? ", rolls " + g.rolls + ", score " + F(g.score) : "") +
                    (string.IsNullOrEmpty(g.note) ? "" : ", " + g.note) + ")\n      " + string.Join("; ", g.mods.ToArray()));
            if (d.rerolls.Count > 0)
            {
                sb.AppendLine("REROLLS");
                foreach (RerollRecord r in d.rerolls) sb.AppendLine("  " + F(r.at) + "s " + r.slot + ": " + r.before + " -> " + r.after);
                sb.AppendLine("GEAR AT END");
                foreach (GearRecord g in d.gearAtEnd) sb.AppendLine("  " + g.slot + ": " + g.name + "\n      " + string.Join("; ", g.mods.ToArray()));
            }
            sb.AppendLine();
            sb.AppendLine("CHARACTER AT START");
            foreach (StatLine s in d.characterAtStart) sb.AppendLine("  " + s.stat + " " + F(s.value));
            sb.AppendLine();
            sb.AppendLine("PASSIVES (" + d.passivesAtStart.Count + ")");
            foreach (string p in d.passivesAtStart) sb.AppendLine("  " + p);
            sb.AppendLine();
            sb.AppendLine("ENCOUNTERS");
            foreach (EncounterRecord e in d.encounters)
                sb.AppendLine("  " + e.index.ToString().PadLeft(2) + ". " + e.label + ": " + string.Join(", ", e.enemies.ToArray()) + " - " +
                    (e.timedOut ? "TIMED OUT" : e.clearedAt >= 0f ? F(e.duration) + "s" : "not cleared") + (e.deaths > 0 ? ", " + e.deaths + " death(s)" : ""));
            if (d.deathLog.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("DEATHS");
                foreach (DeathRecord r in d.deathLog) sb.AppendLine("  " + F(r.at) + "s during " + r.during);
            }
            if (d.notes.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("NOTES");
                foreach (string n in d.notes) sb.AppendLine("  " + n);
            }
            return sb.ToString();
        }
    }
}
