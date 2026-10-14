using System.Collections.Generic;
using UnityEngine;
using E = PoeClone.Inventory.WeaponSkillEffect;

namespace PoeClone.Inventory
{
    /// <summary>Each skill has four authored routes, six nodes per route. IDs are save data.</summary>
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
        private static WeaponSkillBonus B(E effect, float value) => new WeaponSkillBonus(effect, value);

        private sealed class Route
        {
            public string Name, Notable, Keystone;
            public WeaponSkillBonus[] Entry, Small, Major, Final;
            public Route(string name, E small, float amount, string notable, WeaponSkillBonus[] major, string keystone, params WeaponSkillBonus[] final)
            {
                Name = name; Small = new[] { B(small, amount) }; Notable = notable; Major = major; Keystone = keystone; Final = final;
                Entry = new[] { B(E.Damage, 24) };
            }
            public Route Start(params WeaponSkillBonus[] entry) { Entry = entry; return this; }
        }
        private static Route R(string name, E small, float amount, string notable, WeaponSkillBonus[] major, string keystone, params WeaponSkillBonus[] final)
            => new Route(name, small, amount, notable, major, keystone, final);
        private static WeaponSkillBonus[] M(params WeaponSkillBonus[] bonuses) => bonuses;

        private static void Add(Dictionary<StatType, List<WeaponSkillNode>> all, StatType grant, params Route[] routes)
        {
            var nodes = new List<WeaponSkillNode>();
            for (int branch = 0; branch < routes.Length; branch++)
            {
                Route r = routes[branch];
                // Different angular offsets and bends give each skill its own readable silhouette.
                float skew = ((int)grant % 5 - 2) * 0.035f;
                for (int depth = 0; depth < 6; depth++)
                {
                    string id = "b" + branch + "_" + depth;
                    float angle = (45f + branch * 90f + skew * depth * 30f) * Mathf.Deg2Rad;
                    float distance = 76f + depth * 53f;
                    nodes.Add(new WeaponSkillNode
                    {
                        Id = id, Parent = depth == 0 ? null : "b" + branch + "_" + (depth - 1),
                        Branch = r.Name, Depth = depth, Notable = depth == 3, Keystone = depth == 5,
                        Name = depth == 0 ? r.Name : depth == 3 ? r.Notable : depth == 5 ? r.Keystone :
                            depth == 2 ? r.Name + " · Power" : r.Name + (depth == 1 ? " · Practice" : " · Mastery"),
                        Position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance,
                        Bonuses = depth == 0 ? r.Entry : depth == 2 ? M(B(E.Damage, 28)) :
                            depth == 3 ? r.Major : depth == 5 ? r.Final : r.Small
                    });
                }
            }
            all.Add(grant, nodes);
        }

        private static Dictionary<StatType, List<WeaponSkillNode>> Build()
        {
            var t = new Dictionary<StatType, List<WeaponSkillNode>>();
            Add(t, StatType.GrantIceShard,
                R("Frozen Fan", E.Range, 12, "Splintering Ice", M(B(E.Projectiles, 2)), "Fracture", B(E.Fork, 1), B(E.Damage, -25)).Start(B(E.Projectiles, 2)),
                R("Ice Lance", E.Range, 12, "Through the Heart", M(B(E.Pierce, 3)), "Glacial Spear", B(E.SingleProjectile, 1), B(E.Damage, 100), B(E.Speed, -15)),
                R("Deep Winter", E.Duration, 15, "Biting Cold", M(B(E.Duration, 50), B(E.DamageVsChilled, 30)), "Winter's Grip", B(E.DamageVsChilled, 60), B(E.Duration, 100), B(E.Speed, -20)),
                R("Shattered Glass", E.DamageVsChilled, 12, "Brittle", M(B(E.Shatter, 35)), "Crystal Funeral", B(E.Shatter, 65), B(E.Area, 60), B(E.Damage, -25)));
            Add(t, StatType.GrantFireBolt,
                R("Wildfire", E.Area, 15, "Firestorm", M(B(E.Projectiles, 2)), "Scatterfire", B(E.Fork, 1), B(E.Damage, -25)),
                R("Searing Lance", E.Range, 15, "Burn Through", M(B(E.Pierce, 3)), "Focused Flame", B(E.SingleProjectile, 1), B(E.Damage, 110), B(E.Area, -50)),
                R("Embers", E.Duration, 15, "Kindling", M(B(E.Ignite, 50)), "Cremation", B(E.Ignite, 50), B(E.Explode, 65), B(E.Speed, -20)),
                R("Incantation", E.Speed, 8, "Double Spark", M(B(E.Repeat, 1), B(E.Damage, -15)), "Echoing Flame", B(E.Repeat, 1), B(E.Damage, -25), B(E.Cost, 35)));
            Add(t, StatType.GrantChainLightning,
                R("Conductivity", E.Range, 12, "Long Circuit", M(B(E.Chains, 2)), "Living Network", B(E.Chains, 3), B(E.ChainRetention, 20), B(E.Damage, -30)),
                R("Thunderhead", E.Damage, 15, "Heavy Voltage", M(B(E.Damage, 45), B(E.Speed, -10)), "Lightning Rod", B(E.Chains, -2), B(E.Damage, 110), B(E.Speed, -20)),
                R("Static", E.Duration, 20, "Overcharge", M(B(E.Shock, 60)), "Stormwake", B(E.Shock, 40), B(E.Explode, 60), B(E.Damage, -20)),
                R("Resonance", E.Speed, 8, "Repeated Discharge", M(B(E.Repeat, 1), B(E.Damage, -15)), "Feedback Loop", B(E.Repeat, 1), B(E.Cost, 50), B(E.Damage, -25)));
            Add(t, StatType.GrantVenomSpout,
                R("Miasma", E.Area, 15, "Poisoned Ground", M(B(E.Cloud, 45)), "Toxic Basin", B(E.Cloud, 100), B(E.Area, 60), B(E.Damage, -30)),
                R("Pressure", E.Damage, 15, "Deep Eruption", M(B(E.Damage, 50), B(E.Area, -20)), "Pinpoint Geyser", B(E.Damage, 110), B(E.Area, -50), B(E.Speed, -20)),
                R("Virulence", E.Poison, 20, "Septic Wound", M(B(E.Poison, 100)), "Virulent Core", B(E.Poison, 180), B(E.Damage, -35), B(E.Cost, 25)),
                R("Contagion", E.Speed, 8, "Second Eruption", M(B(E.Repeat, 1), B(E.Damage, -15)), "Pestilent Wake", B(E.Explode, 100), B(E.Area, 40), B(E.Speed, -25)));
            Add(t, StatType.GrantSplitShot,
                R("Broadside", E.Range, 12, "Fivefold Shot", M(B(E.Projectiles, 2)), "Splinter Volley", B(E.Fork, 1), B(E.Projectiles, 2), B(E.Damage, -30)).Start(B(E.Projectiles, 2)),
                R("Skewer", E.Damage, 15, "Through the Ranks", M(B(E.Pierce, 3)), "Needle Fan", B(E.Pierce, 8), B(E.Range, 50), B(E.Speed, -25)),
                R("Ricochet", E.Speed, 8, "Rebound", M(B(E.Chains, 1)), "Bouncing Volley", B(E.Chains, 2), B(E.Damage, -30)),
                R("Hail", E.Speed, 8, "Rapid Release", M(B(E.Speed, 25), B(E.Cost, 15)), "Endless Quiver", B(E.Speed, 60), B(E.Damage, -40), B(E.Cost, 30)));
            Add(t, StatType.GrantPiercingShot,
                R("Ballista", E.Range, 15, "Deep Draw", M(B(E.Damage, 40), B(E.Speed, -10)), "Siege Arrow", B(E.SingleProjectile, 1), B(E.Damage, 110), B(E.Speed, -25)),
                R("Forked Shaft", E.Range, 15, "Split Tip", M(B(E.Projectiles, 2)), "Shrapnel", B(E.Fork, 1), B(E.Damage, -30)),
                R("Frost-Tipped", E.Duration, 15, "Cold Iron", M(B(E.Cold, 1), B(E.Chill, 50)), "Brittle Line", B(E.Shatter, 100), B(E.Damage, -25)),
                R("Drill", E.Speed, 8, "Quick Draw", M(B(E.Speed, 25)), "Repeating Ballista", B(E.Speed, 55), B(E.Damage, -35), B(E.Cost, 40)));
            Add(t, StatType.GrantRainOfArrows,
                R("Blanket", E.Area, 15, "Arrowstorm", M(B(E.Projectiles, 3)), "Cloudburst", B(E.Projectiles, 5), B(E.Area, 70), B(E.Damage, -35)),
                R("Bullseye", E.Damage, 15, "Concentrated Rain", M(B(E.Area, -30), B(E.Damage, 45)), "Death Zone", B(E.Area, -45), B(E.Damage, 100), B(E.Speed, -25)),
                R("Burning Rain", E.Duration, 15, "Flaming Shafts", M(B(E.Fire, 1), B(E.Ignite, 50)), "Ashfall", B(E.Explode, 100), B(E.Damage, -25)),
                R("Relentless", E.Speed, 8, "Second Wave", M(B(E.ExtraWaves, 3)), "Rolling Barrage", B(E.ExtraWaves, 6), B(E.Damage, -35), B(E.Cost, 40)));
            Add(t, StatType.GrantBurningArrow,
                R("Blast", E.Area, 15, "Flame Burst", M(B(E.Area, 50)), "Powder Keg", B(E.Explode, 100), B(E.Damage, -25)),
                R("Ignition", E.Duration, 20, "Firestarter", M(B(E.Ignite, 60)), "Slow Burn", B(E.Ignite, 40), B(E.Damage, 65), B(E.Speed, -30)),
                R("Fire Fan", E.Range, 12, "Burning Volley", M(B(E.Projectiles, 2)), "Spreading Flames", B(E.Fork, 1), B(E.Damage, -25)),
                R("Rapid Fire", E.Speed, 8, "Hot Draw", M(B(E.Speed, 25)), "Flashfire", B(E.Speed, 60), B(E.Damage, -35), B(E.Cost, 40)));
            Add(t, StatType.GrantVenomArrow,
                R("Fangs", E.Poison, 20, "Deep Venom", M(B(E.Poison, 100)), "Black Blood", B(E.Poison, 180), B(E.Damage, -35)),
                R("Toxic Air", E.Area, 15, "Venom Plume", M(B(E.Cloud, 60)), "Choking Fog", B(E.Cloud, 120), B(E.Area, 60), B(E.Speed, -25)),
                R("Infection", E.Range, 12, "Infected Shaft", M(B(E.Pierce, 3)), "Contagious Volley", B(E.Projectiles, 3), B(E.Fork, 1), B(E.Damage, -35)),
                R("Pestilence", E.Speed, 8, "Septic Burst", M(B(E.Explode, 40)), "Plague Carrier", B(E.Explode, 60), B(E.Area, 50), B(E.Damage, -25)));
            Add(t, StatType.GrantCleave,
                R("Wide Sweep", E.Area, 15, "Broad Blade", M(B(E.Range, 35)), "Horizon Cleaver", B(E.Area, 100), B(E.Damage, -30)),
                R("Execution", E.Damage, 15, "Finishing Edge", M(B(E.Cull, 1)), "Headsman's Blow", B(E.Damage, 100), B(E.Speed, -30), B(E.Area, -30)),
                R("Frenzy", E.Speed, 8, "Fast Follow-through", M(B(E.Speed, 25)), "Blade Tempest", B(E.Speed, 60), B(E.Damage, -35), B(E.Cost, 30)),
                R("Bloodletting", E.Leech, 1, "Ravenous Edge", M(B(E.Leech, 5), B(E.Splash, 25)), "Bloodbath", B(E.Explode, 100), B(E.Damage, -25)));
            Add(t, StatType.GrantPulverize,
                R("Earthbreaker", E.Area, 15, "Seismic Reach", M(B(E.Area, 50)), "Worldshaker", B(E.Area, 100), B(E.Speed, -30)),
                R("Crushing Force", E.Damage, 15, "Weight of Stone", M(B(E.Damage, 45), B(E.Area, -20)), "Anvil", B(E.Damage, 120), B(E.Speed, -35), B(E.Area, -30)),
                R("Shrapnel", E.Splash, 10, "Broken Bones", M(B(E.Splash, 40)), "Detonating Impact", B(E.Explode, 100), B(E.Damage, -25)),
                R("Frosthammer", E.Duration, 20, "Frozen Impact", M(B(E.Cold, 1), B(E.Chill, 60)), "Icebreaker", B(E.Shatter, 100), B(E.Speed, -20)));
            Add(t, StatType.GrantReapingArc,
                R("Harvest", E.Range, 15, "Long Scythe", M(B(E.Area, 45)), "Reaper's Horizon", B(E.Range, 80), B(E.Damage, -25)),
                R("Executioner", E.Damage, 15, "Final Cut", M(B(E.Cull, 1)), "Death Sentence", B(E.Damage, 100), B(E.Area, -45), B(E.Speed, -20)),
                R("Blood Harvest", E.Leech, 1, "Red Harvest", M(B(E.Splash, 45)), "Feast", B(E.Leech, 10), B(E.Explode, 80), B(E.Speed, -25)),
                R("Quickening", E.Speed, 8, "Light Haft", M(B(E.Speed, 25)), "Whistling Steel", B(E.Speed, 65), B(E.Damage, -40)));
            Add(t, StatType.GrantLungingThrust,
                R("Pursuit", E.Range, 15, "Long Point", M(B(E.Range, 40)), "Farstrider", B(E.Range, 100), B(E.Damage, -25)),
                R("Duelist", E.Damage, 15, "Heartseeker", M(B(E.Cull, 1)), "Perfect Thrust", B(E.Damage, 120), B(E.Speed, -30)),
                R("Impaler", E.Splash, 10, "Skewering Point", M(B(E.Splash, 50)), "Explosive Entry", B(E.Explode, 100), B(E.Damage, -25)),
                R("Flurry", E.Speed, 8, "Rapid Thrust", M(B(E.Speed, 25)), "Thousand Needles", B(E.Speed, 70), B(E.Damage, -40), B(E.Cost, 40)));
            Add(t, StatType.GrantWhirlwind,
                R("Cyclone", E.Area, 15, "Wide Orbit", M(B(E.Range, 35)), "Eye of the Storm", B(E.Area, 100), B(E.Damage, -35)),
                R("Razor Vortex", E.Speed, 8, "Rising Tempo", M(B(E.Speed, 25)), "Dervish", B(E.Speed, 65), B(E.Area, -35), B(E.Cost, 40)),
                R("Sustenance", E.Leech, 1, "Blood Funnel", M(B(E.Leech, 5), B(E.Cost, -20)), "Sanguine Storm", B(E.Leech, 10), B(E.Damage, -25), B(E.Splash, 40)),
                R("Winterwind", E.Duration, 15, "Cold Steel", M(B(E.Cold, 1), B(E.Chill, 60)), "Frozen Maelstrom", B(E.Shatter, 100), B(E.Speed, -20)));
            Add(t, StatType.GrantFissure,
                R("Fault Line", E.Range, 15, "Deep Fault", M(B(E.Range, 45)), "Continental Rift", B(E.Range, 100), B(E.Area, -35)),
                R("Fracture", E.Area, 15, "Broad Crack", M(B(E.Area, 55)), "Broken Earth", B(E.Area, 100), B(E.Damage, -30)),
                R("Molten Core", E.Duration, 15, "Lava Vein", M(B(E.Fire, 1), B(E.Ignite, 60)), "Volcanic Rupture", B(E.Explode, 100), B(E.Speed, -25)),
                R("Tectonic Force", E.Damage, 15, "Deep Pressure", M(B(E.Damage, 45)), "Cataclysm", B(E.Damage, 110), B(E.Speed, -35)));
            Add(t, StatType.GrantAftershock,
                R("Rolling Thunder", E.Range, 12, "Fourth Tremor", M(B(E.ExtraWaves, 1)), "Unending Quake", B(E.ExtraWaves, 3), B(E.Damage, -35)),
                R("Epicenter", E.Damage, 15, "Focused Shock", M(B(E.Damage, 45), B(E.Area, -20)), "Ground Zero", B(E.Damage, 110), B(E.Area, -40), B(E.Speed, -20)),
                R("Shockwave", E.Area, 15, "Wider Ripples", M(B(E.Area, 50)), "Tidal Earth", B(E.Area, 100), B(E.Damage, -30)),
                R("Stormquake", E.Duration, 15, "Charged Ground", M(B(E.Lightning, 1), B(E.Shock, 60)), "Thunder Below", B(E.Shock, 40), B(E.Explode, 80), B(E.Speed, -25)));
            Add(t, StatType.GrantCrossSlash,
                R("Long Blades", E.Range, 15, "Extended Cross", M(B(E.Range, 40)), "Crossroads", B(E.Range, 90), B(E.Area, 40), B(E.Speed, -25)),
                R("Flashing Steel", E.Speed, 8, "Swift Cuts", M(B(E.Speed, 25)), "Bladedance", B(E.Speed, 65), B(E.Damage, -35)),
                R("Death Cross", E.Damage, 15, "Finishing Cut", M(B(E.Cull, 1)), "Fatal Intersection", B(E.Damage, 110), B(E.Area, -40), B(E.Speed, -20)),
                R("Shattering Cuts", E.Duration, 15, "Icy Edges", M(B(E.Cold, 1), B(E.Chill, 60)), "Glass Cross", B(E.Shatter, 100), B(E.Damage, -25)));
            Add(t, StatType.GrantFangStrike,
                R("Venom", E.Poison, 20, "Deep Fangs", M(B(E.Poison, 100)), "Deathbite", B(E.Poison, 180), B(E.Speed, -25)),
                R("Adder", E.Speed, 8, "Quick Bite", M(B(E.Speed, 25)), "Viper's Dance", B(E.Speed, 70), B(E.Damage, -40)),
                R("Puncture", E.Damage, 15, "Vital Strike", M(B(E.Cull, 1)), "Assassination", B(E.Damage, 120), B(E.Speed, -30)),
                R("Sepsis", E.Area, 15, "Infected Wound", M(B(E.Cloud, 50)), "Plague Fang", B(E.Explode, 100), B(E.Cloud, 70), B(E.Damage, -30)));
            Add(t, StatType.GrantVenomSlash,
                R("Poison Fan", E.Area, 15, "Wide Infection", M(B(E.Range, 40)), "Pestilent Sweep", B(E.Area, 100), B(E.Damage, -30)),
                R("Virulent Edge", E.Poison, 20, "Septic Blade", M(B(E.Poison, 100)), "Black Venom", B(E.Poison, 180), B(E.Speed, -25)),
                R("Miasmic Blade", E.Area, 15, "Toxic Wake", M(B(E.Cloud, 60)), "Choking Slash", B(E.Cloud, 120), B(E.Damage, -30)),
                R("Contagious Cuts", E.Speed, 8, "Spreading Rot", M(B(E.Explode, 40)), "Plague Reaper", B(E.Explode, 60), B(E.Speed, 35), B(E.Damage, -35)));
            Add(t, StatType.GrantRaiseSkeletons,
                R("Legion", E.MinionLife, 12, "Reinforcements", M(B(E.Minions, 2)), "Bone Army", B(E.Minions, 4), B(E.Damage, -35), B(E.MinionLife, -25)),
                R("Champions", E.Damage, 15, "Veterans", M(B(E.MinionArmour, 180)), "Bone Champion", B(E.Minions, -2), B(E.Damage, 120), B(E.MinionLife, 100)),
                R("Guardians", E.MinionResists, 8, "Bone Shields", M(B(E.MinionLife, 60)), "Unbroken Phalanx", B(E.MinionArmour, 600), B(E.MinionLife, 80), B(E.MinionSpeed, -25)),
                R("Sacrifice", E.MinionSpeed, 10, "Unstable Bones", M(B(E.MinionDeathBurst, 60)), "Final Service", B(E.MinionDeathBurst, 140), B(E.Duration, -50), B(E.MinionLife, -25)));
            Add(t, StatType.GrantSkeletonMages,
                R("Conclave", E.MinionLife, 12, "More Apprentices", M(B(E.Minions, 1)), "Grand Conclave", B(E.Minions, 3), B(E.Damage, -35)),
                R("Archmages", E.Damage, 15, "Focused Studies", M(B(E.Damage, 45)), "Sole Archmage", B(E.Minions, -2), B(E.MinionSpeed, -25), B(E.Damage, 130), B(E.MinionLife, 70)),
                R("Winter School", E.Duration, 15, "Frost Lessons", M(B(E.Cold, 1), B(E.Chill, 60)), "Frozen Curriculum", B(E.Shatter, 100), B(E.MinionSpeed, -20)),
                R("Storm School", E.MinionSpeed, 10, "Lightning Lessons", M(B(E.Lightning, 1), B(E.Shock, 60)), "Storm College", B(E.Splash, 60), B(E.Damage, -25), B(E.MinionSpeed, 30)));
            Add(t, StatType.GrantSpiritWolves,
                R("Pack", E.MinionSpeed, 10, "Packmates", M(B(E.Minions, 2)), "Endless Hunt", B(E.Minions, 3), B(E.Damage, -35), B(E.MinionLife, -20)),
                R("Alpha", E.Damage, 15, "Dominant Hunter", M(B(E.MinionLife, 50)), "Lone Wolf", B(E.Minions, -2), B(E.Damage, 130), B(E.MinionLife, 100)),
                R("Spirit Bond", E.Duration, 20, "Loyal Companions", M(B(E.Duration, 70)), "Eternal Pack", B(E.Duration, 200), B(E.Leech, 8), B(E.Damage, -25)),
                R("Wild Hunt", E.MinionSpeed, 10, "Blood Scent", M(B(E.Cull, 1)), "Feral Spirits", B(E.MinionSpeed, 70), B(E.Damage, 70), B(E.Duration, -55)));
            Add(t, StatType.GrantBoneGolem,
                R("Bastion", E.MinionResists, 8, "Bone Plating", M(B(E.MinionArmour, 400)), "Living Fortress", B(E.MinionLife, 150), B(E.MinionArmour, 600), B(E.MinionSpeed, -30)),
                R("Bonecrusher", E.Damage, 15, "Wide Slam", M(B(E.Area, 60)), "Siege Golem", B(E.Damage, 110), B(E.Area, 60), B(E.MinionSpeed, -30)),
                R("Twins", E.MinionSpeed, 10, "Second Guardian", M(B(E.Minions, 1), B(E.Damage, -15)), "Bone Swarm", B(E.Minions, 2), B(E.Damage, -30), B(E.MinionLife, -25)),
                R("Ossuary", E.Duration, 20, "Unstable Core", M(B(E.MinionDeathBurst, 100)), "Bone Bomb", B(E.MinionDeathBurst, 200), B(E.Duration, -55), B(E.MinionSpeed, 40)));
            Add(t, StatType.GrantSummonViper,
                R("Nest", E.MinionLife, 12, "Brood", M(B(E.Minions, 1)), "Serpent Swarm", B(E.Minions, 3), B(E.Damage, -35)),
                R("Venom Glands", E.Poison, 20, "Potent Venom", M(B(E.Poison, 100)), "Royal Cobra", B(E.Minions, -1), B(E.Cost, 40), B(E.Poison, 180), B(E.Damage, 60), B(E.MinionSpeed, -25)),
                R("Miasma Spirits", E.Area, 15, "Toxic Breath", M(B(E.Cloud, 60)), "Plague Nest", B(E.Cloud, 120), B(E.Explode, 80), B(E.Damage, -30)),
                R("Striking Spirits", E.MinionSpeed, 10, "Quick Fangs", M(B(E.MinionSpeed, 30)), "Fleeting Venom", B(E.MinionSpeed, 70), B(E.Damage, 60), B(E.Duration, -55)));
            return t;
        }

        public static string Describe(WeaponSkillBonus b)
        {
            string n = (b.Value > 0 ? "+" : "") + b.Value.ToString("0.#");
            switch (b.Effect)
            {
                case E.Damage: return n + "% skill damage (additive within this tree)";
                case E.Speed: return n + "% skill speed";
                case E.Area: return n + "% area of effect";
                case E.Range: return n + "% skill range";
                case E.Projectiles: return n + " projectiles";
                case E.Chains: return n + " chains";
                case E.Pierce: return "Pierces " + b.Value + " additional enemies";
                case E.Fork: return "Projectiles split into two on their first enemy impact";
                case E.Repeat: return "Repeat this spell " + b.Value + " additional time(s); repeats cannot repeat";
                case E.Chill: return n + "% chance to chill";
                case E.Shock: return n + "% chance to shock";
                case E.Ignite: return n + "% chance to ignite";
                case E.Shatter: return n + "% chance to shatter chilled enemies on kill";
                case E.Explode: return n + "% chance to explode enemies on kill";
                case E.Poison: return "Apply an additional " + b.Value + "% of hit damage as poison";
                case E.Cloud: return "Leave venom clouds for " + b.Value + "% of hit damage per second";
                case E.Duration: return n + "% ailment and temporary minion duration";
                case E.Minions: return n + " maximum minions (minimum one)";
                case E.MinionLife: return n + "% minion life";
                case E.MinionSpeed: return n + "% minion attack and movement speed";
                case E.MinionArmour: return n + " minion armour";
                case E.MinionResists: return n + "% minion elemental and poison resistances";
                case E.Leech: return "Recover " + b.Value + "% of damage dealt as life";
                case E.Cull: return "Hits execute non-boss enemies below 10% life";
                case E.Splash: return "Hits splash for " + b.Value + "% damage around their target";
                case E.Fire: return "Skill hits deal fire damage";
                case E.Cold: return "Skill hits deal cold damage";
                case E.Lightning: return "Skill hits deal lightning damage";
                case E.DamageVsChilled: return n + "% damage against chilled enemies";
                case E.Cost: return n + "% mana or life cost";
                case E.SingleProjectile: return "Fire exactly one projectile; overrides all extra projectiles and forks";
                case E.ChainRetention: return n + " percentage points of damage retained per lightning jump";
                case E.ExtraWaves: return n + " additional tremors or rain arrows";
                case E.MinionDeathBurst: return "Combat death or expiry explodes for " + b.Value + "% minion maximum life";
                default: return n + " " + b.Effect;
            }
        }
    }
}
