using System.Collections.Generic;
using System.Linq;
using PoeClone.Inventory;
using PoeClone.Skills;

namespace PoeClone.World
{
    /// <summary>Four authored character trees, shared by every arena skill in its family.</summary>
    public static class ArenaPassiveBuilds
    {
        private static readonly GauntletBuild melee = new GauntletBuild {
            Id = "arena_melee", Name = "Armoured attacker",
            Passives = new[] {
                "m5", "m8", "f_feast", "f_dancer", "m_life_heart", "f_berserk",
                "m_bloodbath", "m_life_guard", "f_precision", "m_crit_master",
                "f_frenzy", "k_stormblade", "z_t3", "z_t4", "f_life_heart"
            },
            Weights = new Dictionary<StatType, float> {
                { StatType.IncreasedLife, 2f }, { StatType.MaxLife, 2f },
                { StatType.DamageTaken, 2f }, { StatType.Armour, 1.5f },
                { StatType.LifeLeech, 1.5f }, { StatType.LifeOnAttackHit, 1.5f },
                { StatType.PhysicalDamage, 1.5f }, { StatType.AttackDamage, 1.5f },
                { StatType.AttackSpeed, 1.5f }, { StatType.Strength, 1f },
                { StatType.FireResistance, 1f }, { StatType.ColdResistance, 1f },
                { StatType.LightningResistance, 1f }, { StatType.PercentLifeRegen, 1f }
            }
        };

        private static readonly GauntletBuild mage = new GauntletBuild {
            Id = "arena_mage", Name = "Glass-cannon mage",
            Passives = new[] {
                "s_eye", "w_l3", "k_twincast", "s_stride", "s_conductor",
                "z_hub", "k_glacial", "k_thunderlord", "z_t1", "z_t2",
                "w_life_outer_heart", "s_life_mid_heart", "w_life_mid", "s_life_outer"
            },
            Weights = new Dictionary<StatType, float> {
                { StatType.CastSpeed, 2f }, { StatType.SpellDamage, 2f },
                { StatType.Damage, 1.5f }, { StatType.Intelligence, 1f },
                { StatType.MaxMana, 1f }, { StatType.IncreasedMana, 1f },
                { StatType.ManaRegen, 1f }, { StatType.PercentManaRegen, 1f },
                { StatType.FireDamage, 1f }, { StatType.ColdDamage, 1f },
                { StatType.LightningDamage, 1f }, { StatType.ElementalPenetration, 1f }
            }
        };

        private static readonly GauntletBuild bow = new GauntletBuild {
            Id = "arena_bow", Name = "Glass-cannon archer",
            Passives = new[] {
                "g8", "g_deadeye", "g_crit_master", "g_longshot", "f_dancer",
                "f_precision", "k_stormblade", "m8", "m_crit_master", "k_volley",
                "f_berserk", "f_frenzy", "k_relentless", "g_life_outer_heart"
            },
            Weights = new Dictionary<StatType, float> {
                { StatType.PhysicalDamage, 2f }, { StatType.BowDamage, 2f },
                { StatType.AttackDamage, 1.5f }, { StatType.AttackSpeed, 2f },
                { StatType.CriticalChance, 1.5f }, { StatType.CriticalMultiplier, 1.5f },
                { StatType.AdditionalArrows, 1.5f }, { StatType.ArmourPenetration, 1f },
                { StatType.Strength, 1f }, { StatType.Dexterity, 0.5f }
            }
        };

        private static readonly GauntletBuild minion = new GauntletBuild {
            Id = "arena_minion", Name = "Necromancer with defensive support",
            // Buy the major minion rewards first, then finish every node in the region
            // before leaving it for life, resistances, regeneration and damage reduction.
            Passives = new[] {
                "n_ward", "n_lord", "k_legion", "k_herald", "k_horde",
                "k_soulbond", "k_spiritpact", "k_command", "k_tether"
            }.Concat(PassiveTree.Nodes.Where(n => n.Branch == PassiveBranch.Necromancy)
                .Select(n => n.Id).OrderBy(id => id, System.StringComparer.Ordinal))
                .Concat(new[] {
                    "w_life_heart", "w_r3", "m5", "m_life_heart", "m_life_guard",
                    "z_t3", "z_t4", "z_life_heart", "g_life_heart", "f_life_heart"
                }).ToArray(),
            Weights = new Dictionary<StatType, float> {
                { StatType.IncreasedLife, 2f }, { StatType.MaxLife, 2f },
                { StatType.DamageTaken, 2f }, { StatType.Armour, 1f },
                { StatType.FireResistance, 1.5f }, { StatType.ColdResistance, 1.5f },
                { StatType.LightningResistance, 1.5f }, { StatType.PercentLifeRegen, 1.5f },
                { StatType.LifeRegen, 1f }, { StatType.BlockChance, 1f }
            }
        };

        private static readonly Dictionary<string, List<string>> orders = new Dictionary<string, List<string>>();

        private static GauntletBuild For(SkillDefinition skill) =>
            skill.Summon ? minion : skill.Bow ? bow : skill.MeleeAttack ? melee : mage;

        public static string[] Plan(SkillDefinition skill, int level)
        {
            GauntletBuild build = For(skill);
            if (!orders.TryGetValue(build.Id, out List<string> order))
            {
                // Plan the complete order once, then take a level-sized prefix. Low-level
                // builds progress toward the same rewards instead of skipping costly paths.
                foreach (string target in build.Passives)
                    if (PassiveTree.Get(target) == null)
                        throw new System.InvalidOperationException("Arena: unknown passive " + target);
                order = GauntletLoadout.PlanPassives(build, PassiveTree.Nodes.Count - 1, null);
                orders.Add(build.Id, order);
            }
            return order.Take(PassiveAllocation.PointsForLevel(level)).ToArray();
        }
    }
}
