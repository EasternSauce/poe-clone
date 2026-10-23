using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using E = PoeClone.Inventory.WeaponSkillEffect;

namespace PoeClone.Inventory
{
    /// <summary>Skill-local specialization paths and melee finisher routes.</summary>
    public static class WeaponSkillTrees
    {
        private static readonly Dictionary<StatType, List<WeaponSkillNode>> trees = Build();
        public static IReadOnlyList<WeaponSkillNode> For(StatType grant) => trees.TryGetValue(grant, out var nodes) ? nodes : null;
        public static WeaponSkillNode Find(StatType grant, string id)
        {
            var nodes = For(grant);
            if (nodes != null) foreach (var node in nodes) if (node.Id == id) return node;
            return null;
        }

        public sealed class Route
        {
            public string Id, Name;
            public string[] Nodes;
        }

        /// <summary>All legal destinations and combinations, shared by the picker and survey.</summary>
        public static List<Route> Routes(StatType grant)
        {
            var result = new List<Route> { new Route { Id = "base", Name = "Flexible tree (no destinations)", Nodes = new string[0] } };
            var ends = For(grant)?.Where(n => n.Keystone).ToArray();
            if (ends == null) return result;
            for (int mask = 1; mask < (1 << ends.Length); mask++)
            {
                var path = new List<string>();
                var names = new List<string>();
                for (int i = 0; i < ends.Length; i++)
                    if ((mask & (1 << i)) != 0)
                    {
                        AddRoutePath(grant, ends[i], path);
                        names.Add(ends[i].Name);
                    }
                var progress = new WeaponSkillProgress { level = 10 };
                if (path.Any(id => !progress.Allocate(grant, id))) continue;
                result.Add(new Route { Id = mask.ToString(), Name = string.Join(" + ", names), Nodes = path.ToArray() });
            }
            return result;
        }

        private static void AddRoutePath(StatType grant, WeaponSkillNode node, List<string> path)
        {
            if (node.Parent != null) AddRoutePath(grant, Find(grant, node.Parent), path);
            if (!path.Contains(node.Id)) path.Add(node.Id);
        }
        private static WeaponSkillBonus B(E effect, float value) => new WeaponSkillBonus(effect, value);
        private static WeaponSkillBonus[] M(params WeaponSkillBonus[] bonuses) => bonuses;
        // Utility travel points keep a little offensive progression. Strong offensive stats stand alone.
        private static WeaponSkillBonus[] U(E effect, float value) => M(B(effect, value), B(E.Damage, 5));
        // Burning Arrow earns damage while developing its routes; terminals add the remainder.
        private static WeaponSkillBonus[] BurningTravel(E effect, float value) =>
            M(B(effect, value), B(E.Damage, 5), B(E.MoreDamage, 7));
        private sealed class Path
        {
            public string Name, Notable, End, Side;
            public WeaponSkillBonus[] Small, Major, Final, Alternative;
        }
        private static Path P(string name, WeaponSkillBonus[] small, string notable, WeaponSkillBonus[] major,
            string end, WeaponSkillBonus[] final, string side = null, params WeaponSkillBonus[] alternative) =>
            new Path { Name = name, Small = small, Notable = notable, Major = major, End = end, Final = final, Side = side, Alternative = alternative };

        private static void Add(Dictionary<StatType, List<WeaponSkillNode>> all, StatType grant, params Path[] paths)
        {
            var nodes = new List<WeaponSkillNode>();
            // Bends, staggered forks and skill-specific rotations keep routes readable without four spokes.
            float rotation = ((int)grant % 7 - 3) * 5f;
            for (int lane = 0; lane < paths.Length; lane++)
            {
                Path p = paths[lane];
                Vector2[] shape = {
                    new Vector2(65, -12), new Vector2(115, 14), new Vector2(166, 24),
                    new Vector2(207, 68), new Vector2(247, 103), new Vector2(292, 117),
                    new Vector2(212, -30), new Vector2(263, -65)
                };
                float angle = (rotation + lane * 120f) * Mathf.Deg2Rad;
                for (int i = 0; i < shape.Length; i++)
                {
                    Vector2 at = shape[i];
                    Vector2 pos = new Vector2(at.x * Mathf.Cos(angle) - at.y * Mathf.Sin(angle), at.x * Mathf.Sin(angle) + at.y * Mathf.Cos(angle));
                    int parent = i == 6 ? 2 : i == 7 ? 6 : i - 1;
                    nodes.Add(new WeaponSkillNode {
                        Id = "v2_" + lane + "_" + i, Parent = i == 0 ? null : "v2_" + lane + "_" + parent,
                        Grant = grant, Depth = i == 6 ? 3 : i == 7 ? 4 : i,
                        Notable = i == 2, Keystone = i == 5 || i == 7,
                        Name = i == 2 ? p.Notable : i == 5 ? p.End : i == 7 ? p.Side : p.Name,
                        Position = pos, Bonuses = i == 2 ? p.Major : i == 5 ? p.Final : i == 7 ? p.Alternative : p.Small
                    });
                }
            }
            all.Add(grant, nodes);
        }
        private static void AddFourBranches(Dictionary<StatType, List<WeaponSkillNode>> all, StatType grant, Path[] paths)
        {
            var nodes = new List<WeaponSkillNode>();
            for (int lane = 0; lane < paths.Length; lane++)
            {
                Path path = paths[lane];
                int length = lane < 2 ? 6 : 4;
                float angle = (45f + lane * 90f) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                for (int i = 0; i < length; i++)
                {
                    bool end = i == length - 1;
                    nodes.Add(new WeaponSkillNode {
                        Id = "v2_" + lane + "_" + i, Parent = i == 0 ? null : "v2_" + lane + "_" + (i - 1),
                        Grant = grant, Depth = i, Notable = i == 2, Keystone = end,
                        Name = end ? path.End : i == 2 ? path.Notable : path.Name,
                        Bonuses = end ? path.Final : i == 2 ? path.Major : path.Small,
                        Position = direction * (70f + i * 65f)
                    });
                }
            }
            all.Add(grant, nodes);
        }

        private static Dictionary<StatType, List<WeaponSkillNode>> Build()
        {
            var t = new Dictionary<StatType, List<WeaponSkillNode>>();
            Add(t, StatType.GrantIceShard,
                P("Ice Splinters", U(E.ProjectileSpeed, 12), "Frozen Fan", M(B(E.Projectiles, 2)), "Fracture", M(B(E.Fork, 1)), "Glacial Reach", B(E.Area, 40), B(E.Projectiles, 1)),
                P("Biting Cold", U(E.Duration, 15), "Deep Winter", M(B(E.Chill, 20)), "Winter's Grip", M(B(E.DamageVsChilled, 35)), "Brittle Ice", B(E.Shatter, 20)),
                P("Ice Lance", M(B(E.Damage, 10)), "Through the Heart", M(B(E.Pierce, 2)), "Glacial Spear", M(B(E.NoFork, 1), B(E.ProjectileSpeed, 35), B(E.Pierce, 4)), "Quick Frost", B(E.Speed, 18)));
            Add(t, StatType.GrantFireBolt,
                P("Fireflight", U(E.ProjectileSpeed, 12), "Burn Through", M(B(E.Pierce, 1)), "Solar Wheel", M(B(E.FireOrbit, 5)), "Twin Flames", B(E.Projectiles, 1)),
                P("Embers", U(E.Duration, 15), "Kindling", M(B(E.Ignite, 15)), "Cremation", M(B(E.Explode, 15)), "Hot Embers", B(E.Ignite, 20), B(E.Damage, 15)),
                P("Flamecraft", U(E.Area, 10), "Firestorm", M(B(E.Area, 30)), "Rolling Flame", M(B(E.Projectiles, 2), B(E.MoreDamage, -15)), "Incantation", B(E.Speed, 20)));
            Add(t, StatType.GrantChainLightning,
                P("Conductivity", U(E.Range, 10), "Long Circuit", M(B(E.Chains, 1)), "Living Network", M(B(E.Chains, 1), B(E.ChainRetention, 15)), "Branching Current", B(E.Chains, 1)),
                P("Static Charge", U(E.Duration, 12), "Overcharge", M(B(E.Shock, 20)), "Stormwake", M(B(E.Shock, 25), B(E.Duration, 30)), "Heavy Voltage", B(E.Damage, 25)),
                P("Resonance", M(B(E.Speed, 5)), "Efficient Circuit", M(B(E.Cost, -15), B(E.Damage, 10)), "Unbroken Current", M(B(E.ChainRetention, 25)), "Quick Discharge", B(E.Speed, 18)));
            Add(t, StatType.GrantVenomSpout,
                P("Miasma", U(E.Area, 10), "Poisoned Ground", M(B(E.Cloud, 20)), "Toxic Basin", M(B(E.Cloud, 30), B(E.Duration, 25)), "Broad Eruption", B(E.Area, 35)),
                P("Virulence", M(B(E.Damage, 10)), "Septic Wound", M(B(E.Poison, 25)), "Virulent Core", M(B(E.Poison, 40)), "Lingering Toxins", B(E.Duration, 35)),
                P("Pressure", U(E.Range, 10), "Deep Wells", M(B(E.Cost, -15), B(E.Damage, 10)), "Venom Tap", M(B(E.LifeOnHit, 3)), "Rapid Eruption", B(E.Speed, 18)));
            Add(t, StatType.GrantSplitShot,
                P("Broadside", U(E.ProjectileSpeed, 12), "Fivefold Shot", M(B(E.Projectiles, 2)), "Splinter Volley", M(B(E.Fork, 1)), "Longshot", B(E.Range, 40)),
                P("Ricochet", U(E.Area, 10), "Rebound", M(B(E.Chains, 1)), "Bouncing Volley", M(B(E.Chains, 1), B(E.MoreDamage, -15)), "Wide Volley", B(E.Projectiles, 1)),
                P("Steady Hands", M(B(E.Damage, 10)), "Through the Ranks", M(B(E.Pierce, 1)), "Rapid Release", M(B(E.Speed, 22), B(E.MoreDamage, 30)), "Hunter's Sustenance", B(E.LifeOnHit, 3)));
            Add(t, StatType.GrantPiercingShot,
                P("Long Draw", U(E.Range, 12), "Deep Draw", M(B(E.ChargeDamage, 20), B(E.Projectiles, 2)), "Patient Hunter", M(B(E.ChargeDamage, 40), B(E.ChargeTime, 25)), "Far Sight", B(E.Range, 50)),
                P("Heavy Shaft", U(E.ProjectileSpeed, 15), "Armour Breaker", M(B(E.ArmourPenetration, 60), B(E.Damage, 10)), "Heartseeker", M(B(E.FirstStrike, 40)), "Opening Shot", B(E.FirstStrike, 40)),
                P("Breath Control", M(B(E.Damage, 10)), "Quick Draw", M(B(E.ChargeTime, -15)), "Practised Sniper", M(B(E.ChargeTime, -20)), "Sure Aim", B(E.ArmourPenetration, 90), B(E.Damage, 15)));
            Add(t, StatType.GrantRainOfArrows,
                P("Arrowfall", U(E.Area, 12), "Arrowstorm", M(B(E.Projectiles, 3)), "Cloudburst", M(B(E.Projectiles, 4), B(E.MoreDamage, -35)), "Pinning Rain", B(E.RainSpread, 35), B(E.Projectiles, 2), B(E.Chill, 100)),
                P("Focused Rain", M(B(E.Damage, 10)), "Concentrated Rain", M(B(E.RainSpread, -10)), "Death Zone", M(B(E.RainSpread, -15)), "Overlapping Impacts", B(E.Area, 40)),
                P("Sustained Rain", M(B(E.LifeOnHit, 1), B(E.Damage, 6)), "Second Salvo", M(B(E.RainSalvos, 1), B(E.MoreDamage, -25)), "Rolling Barrage", M(B(E.RainSalvos, 1), B(E.MoreDamage, -33.333333f)), "Blood Rain", B(E.LifeOnHit, 3)));
            Add(t, StatType.GrantBurningArrow,
                P("Ignition", BurningTravel(E.Duration, 15), "Firestarter", M(B(E.Ignite, 20), B(E.MoreDamage, 20)), "Slow Burn", M(B(E.Duration, 45), B(E.Ignite, 20), B(E.MoreDamage, 20)), "Hot Coals", B(E.Damage, 25), B(E.MoreDamage, 10)),
                P("Blast Radius", BurningTravel(E.Area, 10), "Flame Burst", M(B(E.Area, 25)), "Scorched Earth", M(B(E.Area, 35), B(E.Pierce, 1)), "Burning Volley", B(E.Projectiles, 1)),
                P("Hot Draw", BurningTravel(E.ProjectileSpeed, 12), "Rapid Fire", M(B(E.Speed, 12)), "Flashfire", M(B(E.Speed, 20)), "Searing Reach", B(E.Range, 40), B(E.Projectiles, 1)));
            Add(t, StatType.GrantVenomArrow,
                P("Venom Glands", U(E.Duration, 15), "Deep Venom", M(B(E.Poison, 55)), "Black Blood", M(B(E.Poison, 35)), "Lingering Venom", B(E.Duration, 40)),
                P("Toxic Air", U(E.Area, 10), "Venom Plume", M(B(E.Cloud, 20)), "Choking Fog", M(B(E.Cloud, 30)), "Wide Infection", B(E.Projectiles, 1)),
                P("Infected Shaft", U(E.ProjectileSpeed, 12), "Piercing Fangs", M(B(E.Pierce, 1)), "Contagious Volley", M(B(E.Projectiles, 2)), "Quick Fangs", B(E.Speed, 20)));
            AddFourBranches(t, StatType.GrantCleave, new[] {
                P("Heavy Steel", M(B(E.Damage, 10)), "Sundering Edge", M(B(E.ArmourPenetration, 40), B(E.Damage, 10)), "Explosive Finale", M(B(E.CleaveExplosion, 1))),
                P("Flowing Steel", M(B(E.Damage, 10)), "Tempered Steel", M(B(E.Speed, 8), B(E.ArmourPenetration, 40)), "Twin Rhythm", M(B(E.CleaveDoubleSlash, 1))),
                P("Executioner", M(B(E.Damage, 10)), "Opening Edge", M(B(E.FirstStrike, 20)), "Headsman's Blow", M(B(E.BossCull, 15))),
                P("Serrated Steel", M(B(E.Damage, 10)), "Opening Wound", M(B(E.FirstStrike, 20)), "Serrated Edge", M(B(E.CleaveBleed, 10)))
            });
            AddFourBranches(t, StatType.GrantPulverize, new[] {
                P("Earthbreaker", M(B(E.Damage, 10)), "Seismic Reach", M(B(E.Area, 20)), "Worldshaker", M(B(E.PulverizeWorldshaker, 1))),
                P("Crushing Rhythm", M(B(E.Damage, 10)), "Steady Blows", M(B(E.Speed, 8), B(E.ArmourPenetration, 40)), "Twin Hammer", M(B(E.PulverizeTwinHammer, 1))),
                P("Splintering Blows", M(B(E.Damage, 10)), "Splintering Force", M(B(E.Damage, 10), B(E.ProjectileSpeed, 20)), "Bonecrusher", M(B(E.PulverizeBonecrusher, 1))),
                P("Crushing Precision", M(B(E.Damage, 10)), "Heavy Head", M(B(E.Damage, 15)), "Perfect Impact", M(B(E.PulverizePerfectImpact, 1)))
            });
            Add(t, StatType.GrantReapingArc,
                P("Long Scythe", U(E.Area, 10), "Harvest", M(B(E.Area, 25)), "Reaper's Horizon", M(B(E.Area, 35)), "Wide Harvest", B(E.Area, 30)),
                P("Heavy Harvest", M(B(E.Damage, 10)), "Sundering Harvest", M(B(E.ArmourPenetration, 40), B(E.Damage, 10)), "Merciless Harvest", M(B(E.MoreDamage, 30)), "Quickening", B(E.Speed, 18)),
                P("First Cut", M(B(E.Damage, 10)), "Reaper's Mark", M(B(E.FirstStrike, 20)), "Death Sentence", M(B(E.FirstStrike, 40)), "Sharp Harvest", B(E.ArmourPenetration, 90), B(E.Damage, 10)));
            Add(t, StatType.GrantLungingThrust,
                P("Pursuit", U(E.Area, 10), "Long Point", M(B(E.Area, 25)), "Farstrider", M(B(E.Area, 40)), "Swift Entry", B(E.Speed, 18)),
                P("Duelist", M(B(E.Damage, 10)), "Heartseeker", M(B(E.FirstStrike, 25)), "Perfect Thrust", M(B(E.FirstStrike, 35)), "Vital Point", B(E.ArmourPenetration, 100), B(E.Damage, 10)),
                P("Impaler", U(E.ArmourPenetration, 20), "Skewering Point", M(B(E.Splash, 15)), "Through and Through", M(B(E.Splash, 25)), "Decisive Thrust", B(E.MoreDamage, 25)));
            Add(t, StatType.GrantWhirlwind,
                P("Wide Orbit", U(E.Area, 10), "Cyclone", M(B(E.Area, 25)), "Eye of the Storm", M(B(E.Area, 35)), "Long Blades", B(E.Area, 35)),
                P("Rising Tempo", M(B(E.Speed, 5)), "Razor Vortex", M(B(E.Speed, 12)), "Dervish", M(B(E.Speed, 20)), "Unbroken Spin", B(E.Cost, -25), B(E.Damage, 15)),
                P("Sustenance", M(B(E.LifeOnHit, 1), B(E.Damage, 6)), "Blood Funnel", M(B(E.LifeOnHit, 2)), "Sanguine Storm", M(B(E.LifeOnHit, 3)), "Reaching Steel", B(E.Area, 30)));
            Add(t, StatType.GrantFissure,
                P("Fault Line", U(E.Area, 10), "Deep Fault", M(B(E.Area, 25)), "Continental Rift", M(B(E.Area, 40)), "Wide Fault", B(E.Area, 30)),
                P("Molten Core", U(E.Duration, 15), "Lava Vein", M(B(E.Fire, 1), B(E.Ignite, 10)), "Volcanic Rupture", M(B(E.Ignite, 20), B(E.Area, 25), B(E.MoreDamage, -15)), "Lingering Heat", B(E.Duration, 40)),
                P("Tectonic Force", U(E.ArmourPenetration, 12), "Deep Pressure", M(B(E.Damage, 35)), "Bedrock Breaker", M(B(E.ArmourPenetration, 52), B(E.Damage, 10)), "Opening Fault", B(E.FirstStrike, 40)));
            Add(t, StatType.GrantAftershock,
                P("Rolling Thunder", U(E.Area, 10), "Fourth Tremor", M(B(E.ExtraWaves, 1)), "Unending Quake", M(B(E.ExtraWaves, 1), B(E.MoreDamage, -15)), "Long Ripples", B(E.Area, 35)),
                P("Epicentre", M(B(E.Damage, 10)), "Focused Shock", M(B(E.ArmourPenetration, 60), B(E.Damage, 10)), "Ground Zero", M(B(E.FirstStrike, 35)), "Tremor Force", B(E.Damage, 25)),
                P("Shockwave", U(E.Area, 10), "Wider Ripples", M(B(E.Area, 25)), "Tidal Earth", M(B(E.Area, 35)), "Seismic Onset", B(E.FirstStrike, 35)));
            Add(t, StatType.GrantCrossSlash,
                P("Long Blades", U(E.Area, 10), "Extended Cross", M(B(E.Area, 25)), "Crossroads", M(B(E.Area, 35)), "Far Cuts", B(E.Area, 35)),
                P("Flashing Steel", M(B(E.Speed, 5)), "Swift Cuts", M(B(E.Speed, 12)), "Bladedance", M(B(E.Speed, 20), B(E.MoreDamage, 10)), "Heavy Blades", B(E.MoreDamage, 25)),
                P("Death Cross", U(E.ArmourPenetration, 20), "Opening Cut", M(B(E.FirstStrike, 20)), "Fatal Intersection", M(B(E.FirstStrike, 40)), "Deep Cuts", B(E.ArmourPenetration, 100), B(E.Damage, 10)));
            Add(t, StatType.GrantFangStrike,
                P("Venom", U(E.Duration, 15), "Deep Fangs", M(B(E.Poison, 20), B(E.MoreDamage, 10)), "Deathbite", M(B(E.Poison, 35)), "Lasting Bite", B(E.Duration, 40)),
                P("Adder", M(B(E.Speed, 5)), "Quick Bite", M(B(E.Speed, 12), B(E.MoreDamage, 10)), "Viper's Dance", M(B(E.Speed, 20)), "Siphoning Fangs", B(E.LifeOnHit, 4)),
                P("Puncture", U(E.ArmourPenetration, 20), "Vital Strike", M(B(E.FirstStrike, 25), B(E.MoreDamage, 10)), "Assassination", M(B(E.Cull, 10)), "Long Fangs", B(E.Area, 35)));
            Add(t, StatType.GrantVenomSlash,
                P("Poison Fan", U(E.Area, 10), "Wide Infection", M(B(E.Area, 25)), "Pestilent Sweep", M(B(E.Area, 35)), "Spreading Cut", B(E.Area, 35)),
                P("Virulent Edge", U(E.Duration, 15), "Septic Blade", M(B(E.Poison, 25)), "Black Venom", M(B(E.Poison, 45)), "Persistent Rot", B(E.Duration, 40)),
                P("Miasmic Blade", M(B(E.Damage, 10)), "Toxic Wake", M(B(E.Cloud, 20)), "Choking Slash", M(B(E.Cloud, 30)), "Contagious Cuts", B(E.Speed, 20)));
            Add(t, StatType.GrantRaiseSkeletons,
                P("March of Bones", U(E.MinionMoveSpeed, 12), "Reinforcements", M(B(E.Minions, 1)), "Bone Army", M(B(E.Minions, 2), B(E.MoreDamage, -25)), "More Warriors", B(E.Minions, 1), B(E.MoreDamage, -15)),
                P("Veteran Bones", U(E.MinionLife, 12), "Bone Shields", M(B(E.MinionArmour, 100), B(E.MinionResists, 60)), "Bone Champion", M(B(E.HalfMinions, 1), B(E.MoreDamage, 70), B(E.MinionLife, 50), B(E.MinionSize, 50)), "Unbroken Phalanx", B(E.MinionArmour, 160), B(E.MinionResists, 75)),
                P("Sharpened Blades", M(B(E.Damage, 10)), "Relentless Warriors", M(B(E.Speed, 15)), "Final Service", M(B(E.MinionDeathBurst, 35)), "Drilled Warriors", B(E.Speed, 20)));
            Add(t, StatType.GrantSkeletonMages,
                P("Winter School", U(E.ProjectileSpeed, 12), "Rallying Volley", M(B(E.Projectiles, 2), B(E.MageCastSpeedAfterSummon, 20)), "Frozen Curriculum", M(B(E.Cold, 1), B(E.Chill, 20), B(E.Duration, 35)), "More Apprentices", B(E.Minions, 1), B(E.MoreDamage, -15)),
                P("Storm School", U(E.MinionMoveSpeed, 12), "Twin Apprentices", M(B(E.Speed, 12), B(E.SummonsPerCast, 2)), "Storm College", M(B(E.Lightning, 1), B(E.Shock, 20), B(E.Speed, 10)), "Arcane Reach", B(E.Area, 100), B(E.Minions, 1), B(E.MoreDamage, -15)),
                P("Fire School", U(E.MinionLife, 12), "Warded Robes", M(B(E.MinionArmour, 150), B(E.MinionResists, 100)), "Fire College", M(B(E.Fire, 1), B(E.Ignite, 20)), "Grand Conclave", B(E.Minions, 1), B(E.MoreDamage, -15)));
            Add(t, StatType.GrantSpiritWolves,
                P("Hunting Pace", U(E.MinionMoveSpeed, 15), "Packmates", M(B(E.Minions, 1)), "Blood Scent", M(B(E.Cull, 10)), "Running Pack", B(E.Minions, 1), B(E.MoreDamage, -15)),
                P("Spirit Bond", U(E.MinionLife, 12), "Spirit Hide", M(B(E.MinionArmour, 100), B(E.MinionResists, 75)), "Eternal Pack", M(B(E.MinionDuration, 80)), "Wild Recovery", B(E.MinionRegen, 3)),
                P("Pack Instinct", M(B(E.Damage, 10)), "Coordinated Hunt", M(B(E.PackHunt, 5)), "Strength in Numbers", M(B(E.PackHunt, 5), B(E.Minions, 1)), "Relentless Chase", B(E.Speed, 18), B(E.MinionMoveSpeed, 20)));
            Add(t, StatType.GrantBoneGolem,
                P("Bone Plating", U(E.MinionLife, 12), "Bastion", M(B(E.MinionArmour, 180), B(E.MinionResists, 75)), "Living Fortress", M(B(E.MinionLife, 40), B(E.MinionRegen, 2)), "Ancient Bones", B(E.MinionArmour, 200), B(E.MinionResists, 75)),
                P("Bonecrusher", U(E.Area, 12), "Wide Slam", M(B(E.Area, 25)), "Seismic Golem", M(B(E.GolemAftershock, 1)), "Heavy Fists", B(E.Damage, 30)),
                P("Restless Guardian", U(E.MinionMoveSpeed, 15), "Swift Guardian", M(B(E.Speed, 15)), "Undying Guardian", M(B(E.MinionDuration, 80)), "Colossus", B(E.MinionSize, 30), B(E.Range, 25), B(E.Damage, 15)));
            Add(t, StatType.GrantSummonViper,
                P("Venom Glands", U(E.Duration, 15), "Potent Venom", M(B(E.Poison, 25)), "Royal Venom", M(B(E.Poison, 40)), "Brood", B(E.Minions, 1), B(E.MoreDamage, -15)),
                P("Serpent Scales", U(E.MinionLife, 12), "Hardened Scales", M(B(E.MinionArmour, 100), B(E.MinionResists, 75)), "Persistent Brood", M(B(E.MinionDuration, 60), B(E.Minions, 1), B(E.MoreDamage, -15)), "Regrowth", B(E.MinionRegen, 3)),
                P("Striking Serpents", U(E.MinionMoveSpeed, 15), "Quick Fangs", M(B(E.Speed, 15)), "Toxic Trail", M(B(E.Cloud, 25)), "Serpent Swarm", B(E.Minions, 1), B(E.MoreDamage, -15)));
            foreach (StatType grant in SkillGrants.MeleeSkills)
                if (grant != StatType.GrantCleave && grant != StatType.GrantPulverize && WeaponSkillRules.ComboLength(grant) > 0 && t.TryGetValue(grant, out var meleeNodes))
                    AddComboBranch(meleeNodes, grant);
            return t;
        }

        private static void AddComboBranch(List<WeaponSkillNode> nodes, StatType grant)
        {
            string[] names = { "Decisive Blow", "Sweeping Finish", "Driving Impact", "Grand Finale" };
            WeaponSkillBonus[][] bonuses = {
                M(B(E.ComboDamage, 15)), M(B(E.ComboArea, 25)),
                M(B(E.ComboKnockback, 1.8f)), M(B(E.ComboDamage, 30), B(E.ComboArea, 35))
            };
            for (int i = 0; i < names.Length; i++)
                nodes.Add(new WeaponSkillNode {
                    Id = "combo_" + i, Parent = i == 0 ? null : "combo_" + (i - 1),
                    Grant = grant, Depth = i, Name = names[i], Bonuses = bonuses[i],
                    Notable = i == 2, Keystone = i == 3,
                    Position = new Vector2(75f + i * 38f, -110f - i * 53f)
                });
        }

        private static string Percent(float value, string stat) => Mathf.Abs(value).ToString("0.#") + "% " + (value < 0 ? "reduced " : "increased ") + stat;
        private static string More(float value, string stat) => Mathf.Abs(value).ToString("0.#") + "% " + (value < 0 ? "less " : "more ") + stat;
        public static string Describe(WeaponSkillBonus b, StatType grant = StatType.GrantIceShard)
        {
            string n = (b.Value > 0 ? "+" : "") + b.Value.ToString("0.#");
            bool minion = SkillGrants.IsSummon(grant);
            bool attack = minion || SkillGrants.IsBowSkill(grant) || System.Array.IndexOf(SkillGrants.MeleeSkills, grant) >= 0;
            string damage = minion ? "minion damage" : "damage";
            switch (b.Effect)
            {
                case E.Damage: return Percent(b.Value, damage);
                case E.MoreDamage: return More(b.Value, damage);
                case E.PulverizeWorldshaker: return "Replaces the third slam with Worldshaker\nDeals 300% of normal hit damage with 35% greater radius and a 40% longer windup\nStuns non-boss enemies hit for 2 seconds";
                case E.PulverizeTwinHammer: return "Replaces Pulverize with two alternating slams, each dealing 130% of normal hit damage\n50% faster combo links and 25% shorter recovery between combos";
                case E.PulverizeBonecrusher: return "Enemies killed by Pulverize release 6 fast bone shards\nEach shard deals 10% of the slain enemy's maximum life as Physical damage and hits once\nFour shards scatter towards nearby enemies; two scatter freely\nShard kills cannot release more shards";
                case E.PulverizePerfectImpact: return "Pulverize hits deal 30% more damage within the inner half of the slam's radius\nThe brighter impact centre marks this zone";
                case E.CleaveExplosion: return "Replaces the fourth slash with a circular explosion at the sword's ground impact\nDeals " + (WeaponSkillRules.CleaveExplosionDamage * 100f).ToString("0.#") + "% of normal hit damage with 35% greater reach\nStuns non-boss enemies hit for 2 seconds";
                case E.CleaveDoubleSlash: return "Replaces Cleave with a two-part combo: double slash, then double slash\nEach slash deals " + (WeaponSkillRules.CleaveDoubleSlashDamage * 100f).ToString("0.#") + "% of normal hit damage; the second pair deals 10% more\nEach cut has its own alternating swing and hit";
                case E.BossCull: return "Hits execute enemies below " + b.Value.ToString("0.#") + "% life";
                case E.CleaveBleed: return "Every Cleave hit inflicts bleeding for " + b.Value.ToString("0.#") + "% of its damage over " + WeaponSkillRules.CleaveBleedSeconds.ToString("0.#") + " seconds\nBleeds from separate hits stack";
                case E.ComboDamage: return More(b.Value, "damage with combo finishers (every " + WeaponSkillRules.ComboLength(grant) + " attacks)");
                case E.ComboArea: return Percent(b.Value, "area of effect with combo finishers (every " + WeaponSkillRules.ComboLength(grant) + " attacks)");
                case E.ComboKnockback: return "Combo finishers (every " + WeaponSkillRules.ComboLength(grant) + " attacks) knock non-boss enemies back " + b.Value.ToString("0.#") + " metres";
                case E.MageCastSpeedAfterSummon: return More(b.Value, "skeleton mage cast speed if you have summoned a minion in the last 4 seconds");
                case E.Speed: return Percent(b.Value, grant == StatType.GrantSkeletonMages ? "minion cast speed" : minion ? "minion attack speed" : attack ? "attack speed" : "cast speed");
                case E.Area: return Percent(b.Value, "area of effect" + (grant == StatType.GrantRainOfArrows ? " per arrow impact" : ""));
                case E.Range: return Percent(b.Value, "range");
                case E.Projectiles: return n + (grant == StatType.GrantRainOfArrows ? " arrows per salvo" : minion ? " minion projectiles" : " projectiles");
                case E.Chains: return n + " chains";
                case E.Pierce: return "Pierces " + b.Value + " additional enemies";
                case E.Fork: return "Projectiles fork on their first enemy impact";
                case E.Chill: return "Hits grant a " + b.Value.ToString("0.#") + "% chance to chill\nChill slows movement by 50% for a base duration of 2 seconds";
                case E.Shock: return "Hits grant a " + b.Value.ToString("0.#") + "% chance to shock";
                case E.Ignite: return "Hits grant a " + b.Value.ToString("0.#") + "% chance to ignite";
                case E.Shatter: return "This skill has a " + b.Value.ToString("0.#") + "% chance to shatter chilled enemies on kill";
                case E.Explode: return "This skill has a " + b.Value.ToString("0.#") + "% chance to explode enemies on kill";
                case E.Poison: return "Inflicts poison dealing " + b.Value.ToString("0.#") + "% of hit damage";
                case E.Cloud: return "Hits leave venom clouds dealing " + b.Value.ToString("0.#") + "% of hit damage per second";
                case E.Duration: return Percent(b.Value, "ailment and venom cloud duration");
                case E.MinionDuration: return Percent(b.Value, "summoned minion duration");
                case E.Minions: return n + " maximum summoned minions";
                case E.SummonsPerCast: return "Summon up to " + b.Value.ToString("0") + " skeletons per cast (summon limit still applies)";
                case E.MinionLife: return Percent(b.Value, "minion maximum life");
                case E.MinionMoveSpeed: return Percent(b.Value, "minion movement speed");
                case E.MinionArmour: return n + " minion armour";
                case E.MinionResists: return Percent(b.Value, "minion elemental and poison resistances");
                case E.LifeOnHit: return n + " life gained per enemy hit";
                case E.Cull: return "Hits execute non-boss enemies below " + b.Value.ToString("0.#") + "% life";
                case E.Splash: return "Hits splash for " + b.Value.ToString("0.#") + "% damage around their target";
                case E.Fire: return "Converts all damage to fire damage";
                case E.Cold: return "Converts all damage to cold damage";
                case E.Lightning: return "Converts all damage to lightning damage";
                case E.DamageVsChilled: return Percent(b.Value, damage + " against chilled enemies");
                case E.FirstStrike: return More(b.Value, damage + " against enemies at full life");
                case E.Cost: return Percent(b.Value, "resource cost");
                case E.NoFork: return "Projectiles cannot fork";
                case E.ChainRetention: return n + " percentage points of damage retained per lightning jump";
                case E.ExtraWaves: return n + " additional tremors";
                case E.MinionDeathBurst: return "Skeletons explode on combat death for " + b.Value.ToString("0.#") + "% of maximum life";
                case E.ProjectileSpeed: return Percent(b.Value, "projectile speed");
                case E.HalfMinions: return "Maximum number of summoned skeletons is halved";
                case E.MinionSize: return Percent(b.Value, "minion size");
                case E.RainSpread: return Percent(b.Value, "arrow landing spread");
                case E.RainSalvos: return n + " delayed salvo per attack";
                case E.FireOrbit: return "Every " + b.Value + " uses, fire 8 fireballs in sequence around you at 40% of normal damage";
                case E.ChargeDamage: return Percent(b.Value, "damage at full charge (scales with charge)");
                case E.ChargeTime: return Percent(b.Value, "time required to fully charge");
                case E.ArmourPenetration: return n + " armour penetration rating (diminishing returns; cannot fully ignore armour)";
                case E.PackHunt: return Percent(b.Value, "damage per other Spirit Wolf within 6 metres (up to 4)");
                case E.MinionRegen: return "Minions regenerate " + b.Value.ToString("0.#") + "% of maximum life per second";
                case E.GolemAftershock: return "Slam repeats after 0.6 seconds for 40% damage";
                default: return n + " " + b.Effect;
            }
        }
    }
}
