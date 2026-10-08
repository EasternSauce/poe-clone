using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.Skills;

namespace PoeClone.EditorTools
{
    /// <summary>Repeatable single-target balance fixtures. Editor-only; nothing is saved to a character.</summary>
    public static class DevBalance
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        private static EnemyHealth dummy;
        private static float damage;
        private static int hits;
        private static float startedAt;
        private static float endsAt;
        private static float sampleSeconds;
        private static string defenceName;
        private static string loaded = "none";
        private static int armyStep;
        private static string[] armySkills = new string[0];
        private static double nextArmyAt;
        private static bool benchmarkRunning;
        private static bool benchmarkMark;
        private static string benchmarkBuild;
        private static string benchmarkDefence;
        private static float benchmarkDuration;
        private static string benchmarkState = "idle";

        // Ordered, connected paths. Early / mid / late spend 3 / 8 / 14 points at levels 4 / 9 / 15.
        private static readonly string[] Melee = { "m1", "m2", "m6", "m7", "m8", "m_w2", "m_w1", "m_bloodbath", "m_w3", "m_w4", "m_w5", "f1", "f2", "f_a2" };
        private static readonly string[] Bow = { "g1", "g2", "g6", "g7", "g8", "g10", "g_deadeye", "k_volley", "f1", "f2", "f_a2", "f_dancer", "g_shaft", "g_longshot" };
        private static readonly string[] Caster = { "w1", "w2", "w3", "w_m1", "w_m2", "w_archmage", "k_twincast", "w4", "w_l2", "w_l3", "b_surge", "s_sp5", "s_conductor", "s_sp3" };
        private static readonly string[] Summoner = { "w1", "w2", "w3", "w6", "w7", "w_r3", "b_fireward", "n1", "n2", "n_lord", "n4", "k_legion", "n5", "k_soulbond" };

        /// <summary>Cheap Edit-mode check that all twelve passive paths remain legal.</summary>
        public static string ValidateLoadouts()
        {
            string[][] paths = { Melee, Bow, Caster, Summoner };
            string[] names = { "melee", "bow", "caster", "minion" };
            int[] levels = { 4, 9, 15 };
            int[] points = { 3, 8, 14 };
            for (int b = 0; b < paths.Length; b++)
                for (int stage = 0; stage < levels.Length; stage++)
                {
                    var allocation = new PassiveAllocation();
                    for (int i = 0; i < points[stage]; i++)
                        if (!allocation.Take(paths[b][i], levels[stage]))
                            return names[b] + " L" + levels[stage] + " invalid at " + paths[b][i];
                }
            return "12 connected loadout paths valid";
        }

        /// <summary>Apply one of melee, bow, caster, minion at early (4), mid (9), or late (15).
        /// Use tiers in increasing order within one Play session; levels cannot be reduced.</summary>
        public static string Loadout(string build, string tier)
        {
            PlayerStats player = UnityEngine.Object.FindAnyObjectByType<PlayerStats>();
            if (player == null) return "no player";
            build = (build ?? "").ToLowerInvariant();
            tier = (tier ?? "").ToLowerInvariant();
            int stage = tier == "early" ? 0 : tier == "mid" ? 1 : tier == "late" ? 2 : -1;
            string[] path = build == "melee" ? Melee : build == "bow" ? Bow : build == "caster" ? Caster : build == "minion" ? Summoner : null;
            if (stage < 0 || path == null) return "use build melee/bow/caster/minion and tier early/mid/late";
            int level = new[] { 4, 9, 15 }[stage];
            if (player.Level > level) return "current level " + player.Level + " exceeds " + tier + "; start fresh Play";

            EditorApplication.update -= BuildArmy;
            EditorApplication.update -= EndWindow;
            startedAt = 0f;
            foreach (Minion minion in new List<Minion>(Minion.All)) if (minion != null) minion.Crumble();
            PlayerInventory inventory = player.GetComponent<PlayerInventory>();
            foreach (EquipSlot slot in SlotRules.AllSlots) inventory.Equipment.Unequip(slot);
            PlayerPassives passives = player.GetComponent<PlayerPassives>();
            passives.Allocation.ResetAll();
            PlayerSkills skills = player.GetComponent<PlayerSkills>();
            if (skills != null)
                for (int slot = 0; slot < SkillBook.SlotCount; slot++) skills.ClearSlot(slot);
            player.RestoreProgress(level, 0);
            int points = new[] { 3, 8, 14 }[stage];
            for (int i = 0; i < points; i++)
                if (!passives.Take(path[i])) return "passive path failed at " + path[i];

            string weapon;
            string gear;
            int skillLevel = new[] { 2, 5, 9 }[stage];
            int bonus = new[] { 4, 10, 18 }[stage];
            if (build == "melee")
            {
                weapon = new[] { "bastard_sword", "zweihander", "colossus_blade" }[stage];
                gear = DevTest.Equip(weapon, "AttackSpeed=" + bonus + ", GrantCleave=" + skillLevel, level);
            }
            else if (build == "bow")
            {
                weapon = new[] { "short_bow", "long_bow", "imperial_bow" }[stage];
                gear = DevTest.Equip(weapon, "AttackSpeed=" + bonus, level) + "; " +
                    DevTest.Equip(new[] { "leather_quiver", "serrated_quiver", "barbed_quiver" }[stage], "AttackDamage=" + bonus, level);
            }
            else if (build == "caster")
            {
                weapon = new[] { "gnarled_staff", "archmage_staff", "eldritch_staff" }[stage];
                gear = DevTest.Equip(weapon, "GrantFireBolt=" + skillLevel + ", SpellDamage=" + bonus + ", CastSpeed=" + bonus, level);
            }
            else
            {
                weapon = new[] { "bone_sceptre", "lich_sceptre", "deathlord_sceptre" }[stage];
                gear = DevTest.Equip(weapon, "GrantRaiseSkeletons=" + skillLevel + ", MinionDamage=" + bonus +
                    ", AdditionalMinions=" + (stage > 0 ? 1 : 0) + ", MinionLevels=" + stage, level) + "; " +
                    DevTest.Equip(new[] { "grimoire", "lich_codex", "necronomicon" }[stage],
                    "GrantSkeletonMages=" + skillLevel + ", MinionDamage=" + bonus +
                    ", AdditionalMinions=" + (stage == 2 ? 1 : 0), level);
            }
            string dash = DevTest.EnsureDash();
            DevTest.God();
            loaded = build + "/" + tier;
            return loaded + " L" + level + " passives=" + passives.Allocation.Spent + " " + gear + "; " + dash;
        }

        /// <summary>Stationary immortal dummy with neutral (Zombie) or current Shepherd defences.
        /// Distance 2.5 works for melee; 6 works for ranged. Damage is counted after defences.</summary>
        public static string Dummy(string defence = "boss", float distance = 6f)
        {
            if (defence != "neutral" && defence != "boss") return "use defence neutral/boss";
            PlayerStats player = UnityEngine.Object.FindAnyObjectByType<PlayerStats>();
            EnemySpawner spawner = UnityEngine.Object.FindAnyObjectByType<EnemySpawner>();
            if (player == null || spawner == null) return "no player/spawner";
            if (dummy != null) UnityEngine.Object.DestroyImmediate(dummy.gameObject);
            int kind = EnemyKinds.IndexOf(defence == "neutral" ? "Zombie" : "The Shepherd");
            if (kind < 0) return "no enemy kind";
            Vector3 forward = player.transform.forward;
            forward.y = 0;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            Vector3 at = player.transform.position + forward.normalized * distance;
            at.y = player.transform.position.y + 0.5f;
            GameObject go = UnityEngine.Object.Instantiate(spawner.EnemyPrefab, at, Quaternion.LookRotation(-forward));
            go.name = "DPS Dummy (" + defence + ")";
            EnemyKinds.Apply(go, kind, 12);
            foreach (MonoBehaviour behaviour in go.GetComponents<MonoBehaviour>())
                if (behaviour != go.GetComponent<EnemyHealth>()) behaviour.enabled = false;
            dummy = go.GetComponent<EnemyHealth>();
            typeof(EnemyHealth).GetField("maxHealth", Fields).SetValue(dummy, 1000000f);
            typeof(EnemyHealth).GetField("currentHealth", Fields).SetValue(dummy, 1000000f);
            typeof(EnemyHealth).GetField("noFlinch", Fields).SetValue(dummy, true);
            dummy.DamageApplied += Count;
            defenceName = defence;
            damage = 0f;
            hits = 0;
            EditorApplication.update -= EndWindow;
            startedAt = 0f;
            Physics.SyncTransforms();
            return go.name + " at " + distance + "m, armour=" + EnemyKinds.Get(kind).Armour +
                " fireRes=" + EnemyKinds.Get(kind).FireResistance;
        }

        private static void Count(float amount) { if (startedAt > 0f && Time.time <= endsAt) { damage += amount; hits++; } }

        /// <summary>Count exactly this many game seconds. Use DevTest.Attack(seconds) for direct builds.</summary>
        public static string Start(float seconds = 8f)
        {
            if (dummy == null) return "make a dummy first";
            damage = 0f;
            hits = 0;
            sampleSeconds = 0f;
            startedAt = Time.time;
            endsAt = startedAt + Mathf.Max(1f, seconds);
            EditorApplication.update -= EndWindow;
            EditorApplication.update += EndWindow;
            return loaded + " measuring for " + seconds + " game seconds";
        }

        private static void EndWindow()
        {
            if (!EditorApplication.isPlaying || startedAt <= 0f || Time.time >= endsAt)
            {
                if (startedAt > 0f) sampleSeconds = Mathf.Min(Time.time, endsAt) - startedAt;
                startedAt = 0f;
                EditorApplication.update -= EndWindow;
            }
        }

        private static string F(float value) => value.ToString("0.0", CultureInfo.InvariantCulture);

        /// <summary>End a sample; reports post-defence DPS and optimistic Shepherd phase times.</summary>
        public static string Report()
        {
            if (startedAt > 0f) EndWindow();
            if (sampleSeconds <= 0f) return "measurement still running (or no sample)";
            float dps = damage / sampleSeconds;
            float bossLife = EnemyKinds.Get(EnemyKinds.IndexOf("The Shepherd")).MaxHealth * EnemyKinds.LifeScale(PoeClone.World.WorldBuilder.MonsterLevels[PoeClone.World.WorldBuilder.ActArena], EnemyKinds.Get(EnemyKinds.IndexOf("The Shepherd")));
            string phase = defenceName == "boss" ? "; Shepherd P1/P2/P3 free-DPS seconds " +
                F(bossLife / 3f / Mathf.Max(.01f, dps)) + "/" + F(bossLife * 2f / 3f / Mathf.Max(.01f, dps)) +
                "/" + F(bossLife / Mathf.Max(.01f, dps)) : "";
            return loaded + " " + (dummy != null ? dummy.name : "no dummy") + " " + hits + " hits " + F(damage) +
                " damage / " + F(sampleSeconds) + "s = " + F(dps) + " DPS" + phase + "; minions=" + Minion.All.Count;
        }

        /// <summary>Use the real summon skill path several times, spaced for cast animations.</summary>
        public static string Army()
        {
            if (!loaded.StartsWith("minion/")) return "load a minion build first";
            // Respect both the shared cap and the per-kind caps: two warriors early,
            // two warriors plus two mages mid, four warriors plus three mages late.
            armySkills = loaded.EndsWith("/early") ? new[] { "RaiseSkeletons" } :
                loaded.EndsWith("/mid") ? new[] { "RaiseSkeletons", "SkeletonMages", "SkeletonMages" } :
                new[] { "RaiseSkeletons", "RaiseSkeletons", "SkeletonMages", "SkeletonMages", "SkeletonMages" };
            armyStep = 0;
            nextArmyAt = 0;
            EditorApplication.update -= BuildArmy;
            EditorApplication.update += BuildArmy;
            return "summoning army; check ArmyStatus after about " + (armySkills.Length + 1) + "s";
        }

        private static void BuildArmy()
        {
            if (!EditorApplication.isPlaying || !loaded.StartsWith("minion/")) { EditorApplication.update -= BuildArmy; return; }
            if (EditorApplication.timeSinceStartup < nextArmyAt) return;
            if (armyStep >= armySkills.Length) { EditorApplication.update -= BuildArmy; return; }
            string skill = armySkills[armyStep];
            string result = DevTest.Use(skill, false);
            if (result.StartsWith("used ")) armyStep++;
            nextArmyAt = EditorApplication.timeSinceStartup + 1;
        }

        public static string ArmyStatus() => "casts=" + armyStep + "/" + armySkills.Length + " minions=" + Minion.All.Count + " cap=" +
            (UnityEngine.Object.FindAnyObjectByType<PlayerStats>() != null ? Minion.GlobalCap(UnityEngine.Object.FindAnyObjectByType<PlayerStats>().transform) : 0);

        /// <summary>One-call loadout, army, dummy, and timed damage sample. Defaults to a marked
        /// summoner, since Death Mark is its main attack. Returns immediately; poll BenchmarkStatus.</summary>
        public static string Benchmark(string build, string tier, string defence = "boss", bool mark = true, float seconds = 8f)
        {
            if (benchmarkRunning) return "benchmark already running: " + benchmarkState;
            string result = Loadout(build, tier);
            if (!result.Contains(" passives=")) return result;
            if (defence != "neutral" && defence != "boss") return "use defence neutral/boss";
            if (dummy != null) UnityEngine.Object.DestroyImmediate(dummy.gameObject);
            dummy = null;
            benchmarkBuild = build.ToLowerInvariant();
            benchmarkDefence = defence;
            benchmarkMark = mark;
            benchmarkDuration = seconds;
            benchmarkRunning = true;
            benchmarkState = result;
            if (benchmarkBuild == "minion")
                Army();
            else
                StartBenchmarkWindow();
            EditorApplication.update -= AdvanceBenchmark;
            EditorApplication.update += AdvanceBenchmark;
            return "benchmark started: " + loaded + " vs " + defence + "; poll BenchmarkStatus";
        }

        private static void StartBenchmarkWindow()
        {
            float distance = benchmarkBuild == "melee" ? 2.5f : benchmarkBuild == "minion" ? 4f : 6f;
            string target = Dummy(benchmarkDefence, distance);
            string window = Start(benchmarkDuration);
            string attack = benchmarkBuild != "minion" || benchmarkMark ? DevTest.Attack(benchmarkDuration) : "minions only";
            benchmarkState = target + " || " + window + " || " + attack;
        }

        private static void AdvanceBenchmark()
        {
            if (!EditorApplication.isPlaying)
            {
                benchmarkState = "Play stopped before benchmark finished";
                benchmarkRunning = false;
                EditorApplication.update -= AdvanceBenchmark;
                return;
            }
            if (benchmarkBuild == "minion" && dummy == null)
            {
                if (armyStep >= armySkills.Length) StartBenchmarkWindow();
                return;
            }
            if (sampleSeconds <= 0f) return;
            benchmarkState = Report();
            benchmarkRunning = false;
            EditorApplication.update -= AdvanceBenchmark;
            Debug.Log("DevBalance: " + benchmarkState);
        }

        public static string BenchmarkStatus() => benchmarkState;
    }
}
