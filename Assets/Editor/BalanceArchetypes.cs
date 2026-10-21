using System;
using System.Collections.Generic;
using System.Linq;
using PoeClone.Inventory;
using PoeClone.Skills;

namespace PoeClone.EditorTools
{
    /// <summary>Catalogue before sampling: every skill, every terminal route, and every legal
    /// combination of terminal routes. Names and prerequisites come from the live weapon trees.</summary>
    public static class BalanceArchetypes
    {
        [Serializable]
        public sealed class Archetype
        {
            public string id, skill, name, family, role, mechanics;
            public int minimumWeaponPoints;
            public string[] requiredNodes, passivePool;
            public SkillId SkillId => (SkillId)Enum.Parse(typeof(SkillId), skill);
        }

        public static List<Archetype> Catalogue()
        {
            var result = new List<Archetype>();
            foreach (var skill in SkillBook.All)
            {
                var tree = WeaponSkillTrees.For(skill.Grant);
                if (!skill.Main || tree == null)
                {
                    result.Add(Create(skill, "support", SupportRole(skill.Id), new List<string>(), "Support"));
                    continue;
                }
                result.Add(Create(skill, "base", "Flexible tree (no destinations)", new List<string>(), "Baseline"));
                var ends = tree.Where(n => n.Keystone).ToArray();
                // Enumerate subsets rather than assuming two destinations fit. Conversion
                // conflicts and the actual ten-point budget are checked by the allocation API.
                for (int mask = 1; mask < (1 << ends.Length); mask++)
                {
                    var required = new List<string>();
                    var names = new List<string>();
                    for (int i = 0; i < ends.Length; i++)
                        if ((mask & (1 << i)) != 0)
                        {
                            AddPath(skill.Grant, ends[i], required);
                            names.Add(ends[i].Name);
                        }
                    var progress = new WeaponSkillProgress { level = 10 };
                    if (required.Any(id => !progress.Allocate(skill.Grant, id))) continue;
                    result.Add(Create(skill, mask.ToString(), string.Join(" + ", names), required,
                        names.Count == 1 ? "Specialization" : "Hybrid"));
                }
            }
            return result;
        }

        private static void AddPath(StatType grant, WeaponSkillNode node, List<string> path)
        {
            if (node.Parent != null) AddPath(grant, WeaponSkillTrees.Find(grant, node.Parent), path);
            if (!path.Contains(node.Id)) path.Add(node.Id);
        }

        private static Archetype Create(SkillDefinition skill, string suffix, string name, List<string> path, string role)
        {
            var effects = path.SelectMany(id => WeaponSkillTrees.Find(skill.Grant, id).Bonuses)
                .Select(b => b.Effect.ToString()).Distinct().OrderBy(s => s);
            var a = new Archetype {
                id = skill.Id + "/" + suffix, skill = skill.Id.ToString(), name = name, role = role,
                family = !skill.Main ? "Support" : skill.Summon ? "Minion" : skill.MeleeAttack ? "Melee" : skill.Bow ? "Bow" : "Spell",
                minimumWeaponPoints = path.Count, requiredNodes = path.ToArray(), mechanics = string.Join(", ", effects)
            };
            a.passivePool = PassiveTree.Nodes.Where(n => n.Id != PassiveTree.OriginId &&
                n.Mods.Any(m => Weight(a, m.Stat) != 0f)).Select(n => n.Id).ToArray();
            return a;
        }

        private static string SupportRole(SkillId id)
        {
            switch (id)
            {
                case SkillId.Dash: return "Mobility and escape";
                case SkillId.Teleport: return "Repeatable mobility";
                case SkillId.FrostNova: return "Cold burst and crowd control";
                case SkillId.WarCry: return "Physical burst and Onslaught";
                case SkillId.DeathMark: return "Recall mobile minions to the caster";
                case SkillId.GraveRot: return "Curse and damage suppression";
                default: throw new InvalidOperationException("Uncatalogued support skill " + id);
            }
        }

        // Shared build dimensions are not additional weapon trees: crit/noncrit, resource
        // investment, defensive investment, elemental conversion and on-hit/DoT investment.
        // Score only stats that can affect this skill. Travel to this pool is paid in full.
        public static float Weight(Archetype a, StatType s)
        {
            bool minion = a.family == "Minion", attack = a.family == "Melee" || a.family == "Bow";
            var skill = SkillBook.Get(a.SkillId);
            bool poison = a.skill.Contains("Venom") || a.skill.Contains("Viper") || a.skill == "FangStrike" || a.mechanics.Contains("Poison") || a.mechanics.Contains("Cloud");
            if (s == StatType.MaxLife || s == StatType.IncreasedLife || s == StatType.MoreLife || s == StatType.LifeRegen ||
                s == StatType.PercentLifeRegen || s == StatType.Armour || s == StatType.Evasion || s == StatType.BlockChance ||
                s == StatType.FireResistance || s == StatType.ColdResistance || s == StatType.LightningResistance ||
                s == StatType.PoisonResistance || s == StatType.IronReflexes || s == StatType.AvoidStun) return 1f;
            if (s == StatType.DamageTaken) return -1f;
            if (s == StatType.MaxMana || s == StatType.IncreasedMana || s == StatType.ManaRegen || s == StatType.PercentManaRegen ||
                s == StatType.ManaCostReduction || s == StatType.Intelligence) return 1f;
            if (minion)
            {
                if (s == StatType.MinionDamage || s == StatType.MoreMinionDamage || s == StatType.MinionSpeed || s == StatType.MinionLife ||
                    s == StatType.MinionArmour || s == StatType.MinionResistances || s == StatType.MinionDuration || s == StatType.BoneArmour ||
                    s == StatType.SoulBond || s == StatType.CastSpeed || s == StatType.MarkEffect) return 1f;
                return 0f;
            }
            if (s == StatType.Damage || s == StatType.MoreDamage) return 1f;
            if (attack && (s == StatType.AttackDamage || s == StatType.MoreAttackDamage || s == StatType.Strength ||
                s == StatType.PhysicalDamage || s == StatType.AttackSpeed || s == StatType.CriticalChance || s == StatType.CriticalMultiplier ||
                s == StatType.LifeLeech || s == StatType.LifeOnAttackHit || s == StatType.ArmourPenetration || s == StatType.PoisonOnHit ||
                s == StatType.VenomCloudOnHit)) return 1f;
            if (a.family == "Melee" && s == StatType.MoreMeleeDamage) return 1f;
            if (a.family == "Bow" && (s == StatType.BowDamage || s == StatType.MoreBowDamage || s == StatType.PointBlank)) return 1f;
            if (!attack && (s == StatType.SpellDamage || s == StatType.MoreSpellDamage || s == StatType.CastSpeed || s == StatType.SpellLeech)) return 1f;
            if (s == StatType.AreaOfEffect || s == StatType.ElementalPenetration) return 1f;
            if (poison && (s == StatType.PoisonDamage || s == StatType.MorePoisonDamage || s == StatType.PoisonPenetration || s == StatType.DamageOverTime)) return 1f;
            if ((skill.DamageElement == StatType.FireDamage || a.mechanics.Contains("Fire")) &&
                (s == StatType.FireDamage || s == StatType.MoreFireDamage || s == StatType.FirePenetration || s == StatType.IgniteChance || s == StatType.FireDamageOverTime || s == StatType.DamageOverTime)) return 1f;
            if (skill.DamageElement == StatType.ColdDamage && (s == StatType.ColdDamage || s == StatType.MoreColdDamage || s == StatType.ColdPenetration || s == StatType.DamageVsChilled)) return 1f;
            if (skill.DamageElement == StatType.LightningDamage && (s == StatType.LightningDamage || s == StatType.MoreLightningDamage || s == StatType.LightningPenetration || s == StatType.ShockChance)) return 1f;
            return 0f;
        }
    }
}
