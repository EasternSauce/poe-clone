using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using E = PoeClone.Inventory.WeaponSkillEffect;

namespace PoeClone.Inventory
{
    /// <summary>
    /// Skill-local trees, 20 points deep. Each grows upward from the skill origin; a node needs any
    /// one allocated neighbour towards the origin. Cleave, Pulverize and Whirlwind are authored
    /// trees whose mid-tree keystones change how the skill plays and branch on into supporting
    /// nodes. Other skills keep their three routes, with the travel stats spread over more nodes.
    /// </summary>
    public static class WeaponSkillTrees
    {
        private static readonly Dictionary<StatType, List<WeaponSkillNode>> trees = new Dictionary<StatType, List<WeaponSkillNode>>();
        private static readonly Dictionary<StatType, Dictionary<string, WeaponSkillNode>> byId = new Dictionary<StatType, Dictionary<string, WeaponSkillNode>>();
        private static readonly Dictionary<StatType, Dictionary<string, List<string>>> adjacency = new Dictionary<StatType, Dictionary<string, List<string>>>();
        private static readonly List<string> NoNeighbours = new List<string>();

        static WeaponSkillTrees() => Build();

        public static IReadOnlyList<WeaponSkillNode> For(StatType grant) => trees.TryGetValue(grant, out var nodes) ? nodes : null;
        public static WeaponSkillNode Find(StatType grant, string id) =>
            id != null && byId.TryGetValue(grant, out var nodes) && nodes.TryGetValue(id, out var node) ? node : null;
        /// <summary>Linked nodes in both directions; <see cref="WeaponSkillNode.Root"/> lists the first nodes.</summary>
        public static IReadOnlyList<string> Neighbours(StatType grant, string id) =>
            adjacency.TryGetValue(grant, out var links) && links.TryGetValue(id, out var list) ? list : NoNeighbours;

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
                        foreach (string id in PathTo(grant, ends[i].Id))
                            if (!path.Contains(id)) path.Add(id);
                        names.Add(ends[i].Name);
                    }
                var progress = new WeaponSkillProgress { level = WeaponSkillProgress.MaxLevel };
                if (path.Any(id => !progress.Allocate(grant, id))) continue;
                result.Add(new Route { Id = mask.ToString(), Name = string.Join(" + ", names), Nodes = path.ToArray() });
            }
            return result;
        }

        /// <summary>The cheapest allocation order from the origin to a node, the node included.</summary>
        public static List<string> PathTo(StatType grant, string target)
        {
            var from = new Dictionary<string, string> { [WeaponSkillNode.Root] = null };
            var frontier = new Queue<string>();
            frontier.Enqueue(WeaponSkillNode.Root);
            while (frontier.Count > 0 && !from.ContainsKey(target))
            {
                string at = frontier.Dequeue();
                foreach (string next in Neighbours(grant, at))
                    if (!from.ContainsKey(next)) { from[next] = at; frontier.Enqueue(next); }
            }
            var path = new List<string>();
            if (!from.ContainsKey(target)) return path;
            for (string at = target; at != WeaponSkillNode.Root; at = from[at]) path.Add(at);
            path.Reverse();
            return path;
        }

        private static WeaponSkillBonus B(E effect, float value) => new WeaponSkillBonus(effect, value);
        private static WeaponSkillBonus[] M(params WeaponSkillBonus[] bonuses) => bonuses;
        // A route's signature stat plus a little damage. Exotic stats (see Exotic) leave the travel
        // points for the route's notable and keystones when the placeholder tree is built.
        private static WeaponSkillBonus[] U(E effect, float value) => M(B(effect, value), B(E.Damage, 5));
        // Travel points carry basic stats only. Burning Arrow's notables and keystones carry the
        // more damage its travel points used to give: 3.5% compounded per point leading to them.
        private static WeaponSkillBonus BurningMore(int travelPoints) => B(E.MoreDamage, (Mathf.Pow(1.035f, travelPoints) - 1f) * 100f);
        // Twice as many travel points as the old 10-point trees, each worth half.
        private static WeaponSkillBonus[] Half(WeaponSkillBonus[] bonuses) => bonuses.Select(b => B(b.Effect, b.Value / 2f)).ToArray();
        private static readonly string[] Form = { "form" };
        private static readonly string[] Patience = { "patience" };
        private static readonly string[] FormAndPatience = { "form", "patience" };

        private sealed class Path
        {
            public string Name, Notable, End, Side;
            public WeaponSkillBonus[] Small, Major, Final, Alternative;
        }
        private static Path P(string name, WeaponSkillBonus[] small, string notable, WeaponSkillBonus[] major,
            string end, WeaponSkillBonus[] final, string side = null, params WeaponSkillBonus[] alternative) =>
            new Path { Name = name, Small = small, Notable = notable, Major = major, End = end, Final = final, Side = side, Alternative = alternative };

        /// <summary>Collects one tree's nodes; positions grow upward from the origin at (0, 0).</summary>
        private sealed class TreeBuilder
        {
            private readonly StatType grant;
            private readonly List<WeaponSkillNode> nodes = new List<WeaponSkillNode>();
            public TreeBuilder(StatType grant) { this.grant = grant; }

            public WeaponSkillNode Node(string id, string name, Vector2 at, string links, WeaponSkillBonus[] bonuses,
                bool notable = false, bool keystone = false, string[] exclusive = null, string note = null)
            {
                var node = new WeaponSkillNode {
                    Id = id, Name = name, Grant = grant, Position = at, Bonuses = bonuses, Notable = notable, Keystone = keystone,
                    Links = links.Split(','), Exclusive = exclusive ?? System.Array.Empty<string>(), Note = note
                };
                // Full elemental conversions are alternatives, never silently overridden by another school.
                if (bonuses.Any(b => b.Effect == E.Fire || b.Effect == E.Cold || b.Effect == E.Lightning))
                    node.Exclusive = node.Exclusive.Concat(new[] { "element" }).ToArray();
                nodes.Add(node);
                return node;
            }
            public void S(string id, string name, float x, float y, string links, params WeaponSkillBonus[] bonuses) =>
                Node(id, name, new Vector2(x, y), links, bonuses);
            public void N(string id, string name, float x, float y, string links, params WeaponSkillBonus[] bonuses) =>
                Node(id, name, new Vector2(x, y), links, bonuses, notable: true);
            public void K(string id, string name, float x, float y, string links, string[] exclusive, string note, params WeaponSkillBonus[] bonuses) =>
                Node(id, name, new Vector2(x, y), links, bonuses, keystone: true, exclusive: exclusive, note: note);

            public void Register()
            {
                var map = nodes.ToDictionary(n => n.Id);
                var links = new Dictionary<string, List<string>> { [WeaponSkillNode.Root] = new List<string>() };
                foreach (var node in nodes) links[node.Id] = new List<string>();
                foreach (var node in nodes)
                    foreach (string link in node.Links)
                    {
                        links[link].Add(node.Id);
                        links[node.Id].Add(link);
                    }
                trees[grant] = nodes;
                byId[grant] = map;
                adjacency[grant] = links;
                foreach (var node in nodes) node.Depth = PathTo(grant, node.Id).Count;
            }
        }

        private static Vector2 Direction(float degrees) =>
            new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));

        // Routes fan upward, left to right, from the origin.
        private static float LaneAngle(int lane, int lanes)
        {
            float spread = lanes <= 1 ? 0f : lanes == 2 ? 50f : lanes == 3 ? 100f : 120f;
            return lanes <= 1 ? 90f : 90f + spread / 2f - lane * spread / (lanes - 1);
        }

        // Travel points never carry these; a route's exotic stat rides on its notable and keystones instead.
        private static readonly E[] Exotic = { E.Range, E.ProjectileSpeed, E.Duration, E.ArmourPenetration, E.LifeOnHit, E.MinionMoveSpeed };

        // Adds the exotic share of `travelPoints` placeholder travel points (each worth half the route's stat) to a strong node.
        private static WeaponSkillBonus[] WithExotic(WeaponSkillBonus[] bonuses, WeaponSkillBonus[] exotic, int travelPoints)
        {
            var merged = bonuses.ToList();
            foreach (var extra in exotic)
            {
                float value = extra.Value * travelPoints / 2f;
                int i = merged.FindIndex(b => b.Effect == extra.Effect);
                if (i >= 0) merged[i] = B(extra.Effect, merged[i].Value + value);
                else merged.Add(B(extra.Effect, value));
            }
            return merged.ToArray();
        }

        /// <summary>Placeholder layout: four travel points, a notable, four more and the route's keystone;
        /// a side spur of two travel points leads from the notable to the alternative keystone.</summary>
        private static void Add(StatType grant, params Path[] paths)
        {
            var tree = new TreeBuilder(grant);
            bool combo = System.Array.IndexOf(SkillGrants.MeleeSkills, grant) >= 0 && WeaponSkillRules.ComboLength(grant) > 0;
            int lanes = paths.Length + (combo ? 1 : 0);
            for (int lane = 0; lane < paths.Length; lane++)
            {
                Path p = paths[lane];
                var exotic = p.Small.Where(b => System.Array.IndexOf(Exotic, b.Effect) >= 0).ToArray();
                var small = Half(p.Small.Except(exotic).ToArray());
                // A travel point that lost its exotic stat still grants a full 5% increased damage.
                if (exotic.Length > 0)
                    small = small.Where(b => b.Effect != E.Damage).Append(B(E.Damage, Mathf.Max(5f, small.Where(b => b.Effect == E.Damage).Sum(b => b.Value)))).ToArray();
                var major = WithExotic(p.Major, exotic, 4);
                var final = WithExotic(p.Final, exotic, 4);
                float angle = LaneAngle(lane, lanes);
                Vector2 direction = Direction(angle);
                string previous = WeaponSkillNode.Root;
                for (int i = 0; i < 10; i++)
                {
                    bool notable = i == 4, keystone = i == 9;
                    string id = "v3_" + lane + "_" + i;
                    tree.Node(id, notable ? p.Notable : keystone ? p.End : p.Name, direction * (80f + i * 56f), previous,
                        notable ? major : keystone ? final : small, notable, keystone);
                    previous = id;
                }
                if (p.Side == null) continue;
                Vector2 spur = Direction(angle + (angle >= 90f ? 60f : -60f));
                Vector2 origin = direction * (80f + 4 * 56f);
                previous = "v3_" + lane + "_4";
                for (int k = 0; k < 3; k++)
                {
                    bool keystone = k == 2;
                    string id = "v3_" + lane + "_s" + k;
                    tree.Node(id, keystone ? p.Side : p.Name, origin + spur * (56f * (k + 1)), previous,
                        keystone ? WithExotic(p.Alternative, exotic, 2) : small, keystone: keystone);
                    previous = id;
                }
            }
            if (combo) AddComboLane(tree, Direction(LaneAngle(lanes - 1, lanes)));
            tree.Register();
        }

        private static void AddComboLane(TreeBuilder tree, Vector2 direction)
        {
            // Finisher bonuses live on the notable and keystone; travel points stay basic.
            string[] names = { "Decisive Blow", "Decisive Blow", "Sweeping Finish", "Sweeping Finish", "Driving Impact", "Building Finale", "Building Finale", "Grand Finale" };
            WeaponSkillBonus[][] bonuses = {
                M(B(E.Damage, 5)), M(B(E.Damage, 5)), M(B(E.Area, 5)), M(B(E.Area, 5)),
                M(B(E.ComboKnockback, 1.8f), B(E.ComboDamage, 15), B(E.ComboArea, 25)), M(B(E.Damage, 5)), M(B(E.Damage, 5)),
                M(B(E.ComboDamage, 30), B(E.ComboArea, 35))
            };
            string previous = WeaponSkillNode.Root;
            for (int i = 0; i < names.Length; i++)
            {
                string id = "combo_" + i;
                tree.Node(id, names[i], direction * (80f + i * 56f), previous, bonuses[i], notable: i == 4, keystone: i == names.Length - 1);
                previous = id;
            }
        }

        private static void Build()
        {
            Add(StatType.GrantIceShard,
                P("Ice Splinters", U(E.ProjectileSpeed, 12), "Frozen Fan", M(B(E.Projectiles, 2)), "Fracture", M(B(E.Fork, 1)), "Glacial Reach", B(E.Area, 40), B(E.Projectiles, 1)),
                P("Biting Cold", U(E.Duration, 15), "Deep Winter", M(B(E.Chill, 20)), "Winter's Grip", M(B(E.DamageVsChilled, 35)), "Brittle Ice", B(E.Shatter, 20)),
                P("Ice Lance", M(B(E.Damage, 10)), "Through the Heart", M(B(E.Pierce, 2)), "Glacial Spear", M(B(E.NoFork, 1), B(E.ProjectileSpeed, 35), B(E.Pierce, 4)), "Quick Frost", B(E.Speed, 18)));
            Add(StatType.GrantFireBolt,
                P("Fireflight", U(E.ProjectileSpeed, 12), "Burn Through", M(B(E.Pierce, 1)), "Solar Wheel", M(B(E.FireOrbit, 5)), "Twin Flames", B(E.Projectiles, 1)),
                P("Embers", U(E.Duration, 15), "Kindling", M(B(E.Ignite, 15)), "Cremation", M(B(E.Explode, 15)), "Hot Embers", B(E.Ignite, 20), B(E.Damage, 15)),
                P("Flamecraft", U(E.Area, 10), "Firestorm", M(B(E.Area, 30)), "Rolling Flame", M(B(E.Projectiles, 2), B(E.MoreDamage, -15)), "Incantation", B(E.Speed, 20)));
            Add(StatType.GrantChainLightning,
                P("Conductivity", U(E.Range, 10), "Long Circuit", M(B(E.Chains, 1)), "Living Network", M(B(E.Chains, 1), B(E.ChainRetention, 15)), "Branching Current", B(E.Chains, 1)),
                P("Static Charge", U(E.Duration, 12), "Overcharge", M(B(E.Shock, 20)), "Stormwake", M(B(E.Shock, 25), B(E.Duration, 30)), "Heavy Voltage", B(E.Damage, 25)),
                P("Resonance", M(B(E.Speed, 5)), "Efficient Circuit", M(B(E.Cost, -15), B(E.Damage, 10)), "Unbroken Current", M(B(E.ChainRetention, 25)), "Quick Discharge", B(E.Speed, 18)));
            Add(StatType.GrantVenomSpout,
                P("Miasma", U(E.Area, 10), "Poisoned Ground", M(B(E.Cloud, 20)), "Toxic Basin", M(B(E.Cloud, 30), B(E.Duration, 25)), "Broad Eruption", B(E.Area, 35)),
                P("Virulence", M(B(E.Damage, 10)), "Septic Wound", M(B(E.Poison, 25)), "Virulent Core", M(B(E.Poison, 40)), "Lingering Toxins", B(E.Duration, 35)),
                P("Pressure", U(E.Range, 10), "Deep Wells", M(B(E.Cost, -15), B(E.Damage, 10)), "Venom Tap", M(B(E.LifeOnHit, 3)), "Rapid Eruption", B(E.Speed, 18)));
            Add(StatType.GrantSplitShot,
                P("Broadside", U(E.ProjectileSpeed, 12), "Fivefold Shot", M(B(E.Projectiles, 2)), "Splinter Volley", M(B(E.Fork, 1)), "Longshot", B(E.Range, 40)),
                P("Ricochet", U(E.Area, 10), "Rebound", M(B(E.Chains, 1)), "Bouncing Volley", M(B(E.Chains, 1), B(E.MoreDamage, -15)), "Wide Volley", B(E.Projectiles, 1)),
                P("Steady Hands", M(B(E.Damage, 10)), "Through the Ranks", M(B(E.Pierce, 1)), "Rapid Release", M(B(E.Speed, 22), B(E.MoreDamage, 30)), "Hunter's Sustenance", B(E.LifeOnHit, 3)));
            Add(StatType.GrantPiercingShot,
                P("Long Draw", U(E.Range, 12), "Deep Draw", M(B(E.ChargeDamage, 20), B(E.Projectiles, 2)), "Patient Hunter", M(B(E.ChargeDamage, 40), B(E.ChargeTime, 25)), "Far Sight", B(E.Range, 50)),
                P("Heavy Shaft", U(E.ProjectileSpeed, 15), "Armour Breaker", M(B(E.ArmourPenetration, 60), B(E.Damage, 10)), "Heartseeker", M(B(E.FirstStrike, 40)), "Opening Shot", B(E.FirstStrike, 40)),
                P("Breath Control", M(B(E.Damage, 10)), "Quick Draw", M(B(E.ChargeTime, -15)), "Practised Sniper", M(B(E.ChargeTime, -20)), "Sure Aim", B(E.ArmourPenetration, 90), B(E.Damage, 15)));
            Add(StatType.GrantRainOfArrows,
                P("Arrowfall", U(E.Area, 12), "Arrowstorm", M(B(E.Projectiles, 3)), "Cloudburst", M(B(E.Projectiles, 4), B(E.MoreDamage, -35)), "Pinning Rain", B(E.RainSpread, 35), B(E.Projectiles, 2), B(E.Chill, 100)),
                P("Focused Rain", M(B(E.Damage, 10)), "Concentrated Rain", M(B(E.RainSpread, -10)), "Death Zone", M(B(E.RainSpread, -15)), "Overlapping Impacts", B(E.Area, 40)),
                P("Sustained Rain", M(B(E.LifeOnHit, 1), B(E.Damage, 6)), "Second Salvo", M(B(E.RainSalvos, 1), B(E.MoreDamage, -25)), "Rolling Barrage", M(B(E.RainSalvos, 1), B(E.MoreDamage, -33.333333f)), "Blood Rain", B(E.LifeOnHit, 3)));
            Add(StatType.GrantBurningArrow,
                P("Ignition", U(E.Duration, 15), "Firestarter", M(B(E.Ignite, 20), B(E.MoreDamage, 20), BurningMore(4)), "Slow Burn", M(B(E.Duration, 45), B(E.Ignite, 20), B(E.MoreDamage, 20), BurningMore(4)), "Hot Coals", B(E.Damage, 25), B(E.MoreDamage, 10), BurningMore(2)),
                P("Blast Radius", U(E.Area, 10), "Flame Burst", M(B(E.Area, 25), BurningMore(4)), "Scorched Earth", M(B(E.Area, 35), B(E.Pierce, 1), BurningMore(4)), "Burning Volley", B(E.Projectiles, 1), BurningMore(2)),
                P("Hot Draw", U(E.ProjectileSpeed, 12), "Rapid Fire", M(B(E.Speed, 12), BurningMore(4)), "Flashfire", M(B(E.Speed, 20), BurningMore(4)), "Searing Reach", B(E.Range, 40), B(E.Projectiles, 1), BurningMore(2)));
            Add(StatType.GrantVenomArrow,
                P("Venom Glands", U(E.Duration, 15), "Deep Venom", M(B(E.Poison, 55)), "Black Blood", M(B(E.Poison, 35)), "Lingering Venom", B(E.Duration, 40)),
                P("Toxic Air", U(E.Area, 10), "Venom Plume", M(B(E.Cloud, 20)), "Choking Fog", M(B(E.Cloud, 30)), "Wide Infection", B(E.Projectiles, 1)),
                P("Infected Shaft", U(E.ProjectileSpeed, 12), "Piercing Fangs", M(B(E.Pierce, 1)), "Contagious Volley", M(B(E.Projectiles, 2)), "Quick Fangs", B(E.Speed, 20)));
            BuildCleave();
            BuildPulverize();
            BuildWhirlwind();
            Add(StatType.GrantReapingArc,
                P("Long Scythe", U(E.Area, 10), "Harvest", M(B(E.Area, 25)), "Reaper's Horizon", M(B(E.Area, 35)), "Wide Harvest", B(E.Area, 30)),
                P("Heavy Harvest", M(B(E.Damage, 10)), "Sundering Harvest", M(B(E.ArmourPenetration, 40), B(E.Damage, 10)), "Merciless Harvest", M(B(E.MoreDamage, 30)), "Quickening", B(E.Speed, 18)),
                P("First Cut", M(B(E.Damage, 10)), "Reaper's Mark", M(B(E.FirstStrike, 20)), "Death Sentence", M(B(E.FirstStrike, 40)), "Sharp Harvest", B(E.ArmourPenetration, 90), B(E.Damage, 10)));
            Add(StatType.GrantLungingThrust,
                P("Pursuit", U(E.Area, 10), "Long Point", M(B(E.Area, 25)), "Farstrider", M(B(E.Area, 40)), "Swift Entry", B(E.Speed, 18)),
                P("Duelist", M(B(E.Damage, 10)), "Heartseeker", M(B(E.FirstStrike, 25)), "Perfect Thrust", M(B(E.FirstStrike, 35)), "Vital Point", B(E.ArmourPenetration, 100), B(E.Damage, 10)),
                P("Impaler", U(E.ArmourPenetration, 20), "Skewering Point", M(B(E.Splash, 15)), "Through and Through", M(B(E.Splash, 25)), "Decisive Thrust", B(E.MoreDamage, 25)));
            Add(StatType.GrantFissure,
                P("Fault Line", U(E.Area, 10), "Deep Fault", M(B(E.Area, 25)), "Continental Rift", M(B(E.Area, 40)), "Wide Fault", B(E.Area, 30)),
                P("Molten Core", U(E.Duration, 15), "Lava Vein", M(B(E.Fire, 1), B(E.Ignite, 10)), "Volcanic Rupture", M(B(E.Ignite, 20), B(E.Area, 25), B(E.MoreDamage, -15)), "Lingering Heat", B(E.Duration, 40)),
                P("Tectonic Force", U(E.ArmourPenetration, 12), "Deep Pressure", M(B(E.Damage, 35)), "Bedrock Breaker", M(B(E.ArmourPenetration, 52), B(E.Damage, 10)), "Opening Fault", B(E.FirstStrike, 40)));
            Add(StatType.GrantAftershock,
                P("Rolling Thunder", U(E.Area, 10), "Fourth Tremor", M(B(E.ExtraWaves, 1)), "Unending Quake", M(B(E.ExtraWaves, 1), B(E.MoreDamage, -15)), "Long Ripples", B(E.Area, 35)),
                P("Epicentre", M(B(E.Damage, 10)), "Focused Shock", M(B(E.ArmourPenetration, 60), B(E.Damage, 10)), "Ground Zero", M(B(E.FirstStrike, 35)), "Tremor Force", B(E.Damage, 25)),
                P("Shockwave", U(E.Area, 10), "Wider Ripples", M(B(E.Area, 25)), "Tidal Earth", M(B(E.Area, 35)), "Seismic Onset", B(E.FirstStrike, 35)));
            Add(StatType.GrantCrossSlash,
                P("Long Blades", U(E.Area, 10), "Extended Cross", M(B(E.Area, 25)), "Crossroads", M(B(E.Area, 35)), "Far Cuts", B(E.Area, 35)),
                P("Flashing Steel", M(B(E.Speed, 5)), "Swift Cuts", M(B(E.Speed, 12)), "Bladedance", M(B(E.Speed, 20), B(E.MoreDamage, 10)), "Heavy Blades", B(E.MoreDamage, 25)),
                P("Death Cross", U(E.ArmourPenetration, 20), "Opening Cut", M(B(E.FirstStrike, 20)), "Fatal Intersection", M(B(E.FirstStrike, 40)), "Deep Cuts", B(E.ArmourPenetration, 100), B(E.Damage, 10)));
            Add(StatType.GrantFangStrike,
                P("Venom", U(E.Duration, 15), "Deep Fangs", M(B(E.Poison, 20), B(E.MoreDamage, 10)), "Deathbite", M(B(E.Poison, 35)), "Lasting Bite", B(E.Duration, 40)),
                P("Adder", M(B(E.Speed, 5)), "Quick Bite", M(B(E.Speed, 12), B(E.MoreDamage, 10)), "Viper's Dance", M(B(E.Speed, 20)), "Siphoning Fangs", B(E.LifeOnHit, 4)),
                P("Puncture", U(E.ArmourPenetration, 20), "Vital Strike", M(B(E.FirstStrike, 25), B(E.MoreDamage, 10)), "Assassination", M(B(E.Cull, 10)), "Long Fangs", B(E.Area, 35)));
            Add(StatType.GrantVenomSlash,
                P("Poison Fan", U(E.Area, 10), "Wide Infection", M(B(E.Area, 25)), "Pestilent Sweep", M(B(E.Area, 35)), "Spreading Cut", B(E.Area, 35)),
                P("Virulent Edge", U(E.Duration, 15), "Septic Blade", M(B(E.Poison, 25)), "Black Venom", M(B(E.Poison, 45)), "Persistent Rot", B(E.Duration, 40)),
                P("Miasmic Blade", M(B(E.Damage, 10)), "Toxic Wake", M(B(E.Cloud, 20)), "Choking Slash", M(B(E.Cloud, 30)), "Contagious Cuts", B(E.Speed, 20)));
            Add(StatType.GrantRaiseSkeletons,
                P("March of Bones", U(E.MinionMoveSpeed, 12), "Reinforcements", M(B(E.Minions, 1)), "Bone Army", M(B(E.Minions, 2), B(E.MoreDamage, -25)), "More Warriors", B(E.Minions, 1), B(E.MoreDamage, -15)),
                P("Veteran Bones", U(E.MinionLife, 12), "Bone Shields", M(B(E.MinionArmour, 100), B(E.MinionResists, 60)), "Bone Champion", M(B(E.HalfMinions, 1), B(E.MoreDamage, 70), B(E.MinionLife, 50), B(E.MinionSize, 50)), "Unbroken Phalanx", B(E.MinionArmour, 160), B(E.MinionResists, 75)),
                P("Sharpened Blades", M(B(E.Damage, 10)), "Relentless Warriors", M(B(E.Speed, 15)), "Final Service", M(B(E.MinionDeathBurst, 35)), "Drilled Warriors", B(E.Speed, 20)));
            Add(StatType.GrantSkeletonMages,
                P("Winter School", U(E.ProjectileSpeed, 12), "Rallying Volley", M(B(E.Projectiles, 2), B(E.MageCastSpeedAfterSummon, 20)), "Frozen Curriculum", M(B(E.Cold, 1), B(E.Chill, 20), B(E.Duration, 35)), "More Apprentices", B(E.Minions, 1), B(E.MoreDamage, -15)),
                P("Storm School", U(E.MinionMoveSpeed, 12), "Twin Apprentices", M(B(E.Speed, 12), B(E.SummonsPerCast, 2)), "Storm College", M(B(E.Lightning, 1), B(E.Shock, 20), B(E.Speed, 10)), "Arcane Reach", B(E.Area, 100), B(E.Minions, 1), B(E.MoreDamage, -15)),
                P("Fire School", U(E.MinionLife, 12), "Warded Robes", M(B(E.MinionArmour, 150), B(E.MinionResists, 100)), "Fire College", M(B(E.Fire, 1), B(E.Ignite, 20)), "Grand Conclave", B(E.Minions, 1), B(E.MoreDamage, -15)));
            Add(StatType.GrantSpiritWolves,
                P("Hunting Pace", U(E.MinionMoveSpeed, 15), "Packmates", M(B(E.Minions, 1)), "Blood Scent", M(B(E.Cull, 10)), "Running Pack", B(E.Minions, 1), B(E.MoreDamage, -15)),
                P("Spirit Bond", U(E.MinionLife, 12), "Spirit Hide", M(B(E.MinionArmour, 100), B(E.MinionResists, 75)), "Eternal Pack", M(B(E.MinionDuration, 80)), "Wild Recovery", B(E.MinionRegen, 3)),
                P("Pack Instinct", M(B(E.Damage, 10)), "Coordinated Hunt", M(B(E.PackHunt, 5)), "Strength in Numbers", M(B(E.PackHunt, 5), B(E.Minions, 1)), "Relentless Chase", B(E.Speed, 18), B(E.MinionMoveSpeed, 20)));
            Add(StatType.GrantBoneGolem,
                P("Bone Plating", U(E.MinionLife, 12), "Bastion", M(B(E.MinionArmour, 180), B(E.MinionResists, 75)), "Living Fortress", M(B(E.MinionLife, 40), B(E.MinionRegen, 2)), "Ancient Bones", B(E.MinionArmour, 200), B(E.MinionResists, 75)),
                P("Bonecrusher", U(E.Area, 12), "Wide Slam", M(B(E.Area, 25)), "Seismic Golem", M(B(E.GolemAftershock, 1)), "Heavy Fists", B(E.Damage, 30)),
                P("Restless Guardian", U(E.MinionMoveSpeed, 15), "Swift Guardian", M(B(E.Speed, 15)), "Undying Guardian", M(B(E.MinionDuration, 80)), "Colossus", B(E.MinionSize, 30), B(E.Range, 25), B(E.Damage, 15)));
            Add(StatType.GrantSummonViper,
                P("Venom Glands", U(E.Duration, 15), "Potent Venom", M(B(E.Poison, 25)), "Royal Venom", M(B(E.Poison, 40)), "Brood", B(E.Minions, 1), B(E.MoreDamage, -15)),
                P("Serpent Scales", U(E.MinionLife, 12), "Hardened Scales", M(B(E.MinionArmour, 100), B(E.MinionResists, 75)), "Persistent Brood", M(B(E.MinionDuration, 60), B(E.Minions, 1), B(E.MoreDamage, -15)), "Regrowth", B(E.MinionRegen, 3)),
                P("Striking Serpents", U(E.MinionMoveSpeed, 15), "Quick Fangs", M(B(E.Speed, 15)), "Toxic Trail", M(B(E.Cloud, 25)), "Serpent Swarm", B(E.Minions, 1), B(E.MoreDamage, -15)));
        }

        // ------------------------------------------------------------------ authored melee trees
        // Left, centre and right boughs leave a shared two-point trunk. Every notable sits at least
        // three points from the origin, and travel points always separate notables from keystones.
        // Travel points only carry basic stats (increased damage, area, attack speed); cost, conditional,
        // finisher and keystone-specific bonuses belong to notables and keystones.

        private static void BuildCleave()
        {
            var t = new TreeBuilder(StatType.GrantCleave);
            t.S("c1", "Honed Edge", 0, 75, "root", B(E.Damage, 6));
            t.S("c2", "Honed Edge", 0, 145, "c1", B(E.Damage, 6));

            // Left: the finisher. Gathering Force is the toll on the way to Explosive Finale.
            t.S("cl1", "Heavy Swing", -70, 205, "c2", B(E.Damage, 6));
            t.S("cl2", "Heavy Swing", -135, 260, "cl1", B(E.Damage, 6));
            t.N("cl3", "Gathering Force", -190, 325, "cl2", B(E.ComboSetupDamage, -20), B(E.ComboDamage, 50));
            t.S("cl4", "Wide Arc", -225, 395, "cl3", B(E.Area, 6));
            t.S("cl5", "Wide Arc", -245, 465, "cl4", B(E.Area, 6));
            t.K("cl6", "Explosive Finale", -255, 545, "cl5", Form, "Cannot be combined with Twin Rhythm", B(E.CleaveExplosion, 1));
            t.S("cl7", "Wide Arc", -320, 605, "cl6", B(E.Area, 6));
            t.S("cl8", "Wide Arc", -360, 675, "cl7", B(E.Area, 6));
            t.N("cl9", "Driving Finish", -380, 755, "cl8", B(E.ComboKnockback, 2f), B(E.ComboArea, 25));
            t.S("cl10", "Tempered Edge", -195, 615, "cl6", B(E.Damage, 6));
            t.S("cl11", "Tempered Edge", -160, 690, "cl10", B(E.Damage, 6));
            t.N("cl12", "Ringing Blow", -150, 770, "cl11", B(E.FinisherStun, 1));

            // Centre: finishing off the wounded, then Carnage spreads each kill into the pack.
            t.S("cm1", "Keen Edge", 0, 215, "c2", B(E.Damage, 6));
            t.S("cm2", "Keen Edge", 0, 285, "cm1", B(E.Damage, 6));
            t.N("cm3", "Butcher", 0, 360, "cm2", B(E.ExecuteDamage, 50));
            t.S("cm4", "Sweeping Steel", 0, 430, "cm3", B(E.Area, 5));
            t.S("cm5", "Sweeping Steel", 0, 500, "cm4", B(E.Area, 5));
            t.S("cm6", "Sweeping Steel", 0, 570, "cm5", B(E.Area, 5));
            t.K("cm7", "Carnage", 0, 660, "cm6", null,
                "Bursts never chain or trigger on-kill effects", B(E.Overkill, 100));
            t.S("cm8", "Butcher's Strength", 0, 735, "cm7", B(E.Damage, 6));
            t.S("cm9", "Butcher's Strength", 0, 805, "cm8", B(E.Damage, 6));
            t.N("cm10", "Feast of Carnage", 0, 885, "cm9", B(E.OverkillHeal, 1), B(E.Overkill, 25));

            // Right: bleeding. Twin Rhythm branches off midway; Rupture waits at the far end.
            t.S("cr1", "Jagged Steel", 70, 205, "c2", B(E.Damage, 6));
            t.S("cr2", "Jagged Steel", 135, 260, "cr1", B(E.Damage, 6));
            t.N("cr3", "Serrated Edge", 190, 325, "cr2", B(E.CleaveBleed, 20));
            t.S("cr4", "Flowing Cuts", 170, 400, "cr3", B(E.Damage, 6));
            t.S("cr5", "Flowing Cuts", 150, 470, "cr4", B(E.Damage, 6));
            t.K("cr6", "Twin Rhythm", 135, 550, "cr5", Form, "Cannot be combined with Explosive Finale", B(E.CleaveDoubleSlash, 1));
            t.S("cr7", "Steady Rhythm", 175, 625, "cr6", B(E.Damage, 6));
            t.S("cr8", "Steady Rhythm", 195, 700, "cr7", B(E.Damage, 6));
            t.N("cr9", "Iron Rhythm", 205, 780, "cr8", B(E.ComboDamageTaken, -25));
            t.S("cr10", "Deep Cuts", 260, 385, "cr3", B(E.Damage, 6));
            t.S("cr11", "Deep Cuts", 305, 450, "cr10", B(E.Damage, 6));
            t.N("cr12", "Haemorrhage", 335, 525, "cr11", B(E.CleaveBleed, 15));
            t.S("cr13", "Deep Cuts", 350, 605, "cr12", B(E.Damage, 6));
            t.S("cr14", "Deep Cuts", 355, 685, "cr13", B(E.Damage, 6));
            t.K("cr15", "Rupture", 360, 775, "cr14", null, null, B(E.CleaveRupture, 1));
            t.Register();
        }

        private static void BuildPulverize()
        {
            var t = new TreeBuilder(StatType.GrantPulverize);
            t.S("p1", "Crushing Weight", 0, 75, "root", B(E.Damage, 6));
            t.S("p2", "Crushing Weight", 0, 145, "p1", B(E.Damage, 6));

            // Left: patience. Measured Blows rewards resting between combos.
            t.S("pl1", "Steady Blows", -70, 205, "p2", B(E.Damage, 6));
            t.S("pl2", "Steady Blows", -135, 260, "pl1", B(E.Damage, 6));
            t.N("pl3", "Perfect Impact", -190, 325, "pl2", B(E.PerfectImpact, 1));
            t.S("pl4", "Broad Impact", -225, 395, "pl3", B(E.Area, 6));
            t.S("pl5", "Broad Impact", -245, 465, "pl4", B(E.Area, 6));
            t.K("pl6", "Measured Blows", -255, 545, "pl5", Patience,
                "A combo opener is Measured after resting " + WeaponSkillRules.MeasuredRest.ToString("0.#") + " seconds once your next attack is ready\n" +
                "A gold pulse at your feet shows a Measured attack is ready; Measured slams flash gold\nCannot be combined with Cataclysm",
                B(E.MeasuredStrike, 60));
            t.S("pl7", "Patience", -320, 605, "pl6", B(E.Damage, 6));
            t.S("pl8", "Patience", -360, 675, "pl7", B(E.Damage, 6));
            t.N("pl9", "Crushing Patience", -380, 755, "pl8", B(E.MeasuredStun, 1), B(E.MeasuredStrike, 20));
            t.S("pl10", "Calm Breath", -195, 615, "pl6", B(E.Area, 6));
            t.S("pl11", "Calm Breath", -160, 690, "pl10", B(E.Area, 6));
            t.N("pl12", "Unhurried", -235, 770, "pl11", B(E.MeasuredRest, -40));

            // Centre: the combo's finisher, its two alternative forms, and Bonecrusher above them.
            t.S("pm1", "Heavy Head", 0, 215, "p2", B(E.Damage, 6));
            t.S("pm2", "Heavy Head", 0, 285, "pm1", B(E.Damage, 6));
            t.N("pm3", "Seismic Finish", 0, 360, "pm2", B(E.ComboArea, 25), B(E.ComboKnockback, 1.5f), B(E.ComboDamage, 15));
            t.S("pm4", "Rolling Force", -55, 430, "pm3", B(E.Area, 6));
            t.S("pm5", "Rolling Force", -80, 500, "pm4", B(E.Area, 6));
            t.K("pm6", "Worldshaker", -90, 580, "pm5", Form, "Cannot be combined with Twin Hammer or Cataclysm", B(E.PulverizeWorldshaker, 1));
            t.S("pm7", "Driving Rhythm", 55, 430, "pm3", B(E.Damage, 6));
            t.S("pm8", "Driving Rhythm", 80, 500, "pm7", B(E.Damage, 6));
            t.K("pm9", "Twin Hammer", 90, 580, "pm8", Form, "Cannot be combined with Worldshaker or Cataclysm", B(E.PulverizeTwinHammer, 1));
            t.S("pm10", "Bone Splinter", -55, 660, "pm6", B(E.Area, 5));
            t.S("pm11", "Bone Splinter", 55, 660, "pm9", B(E.Area, 5));
            t.S("pm12", "Bone Splinter", 0, 730, "pm10,pm11,pl11", B(E.Damage, 6));
            t.S("pm13", "Bone Splinter", 0, 800, "pm12", B(E.Damage, 6));
            t.K("pm14", "Bonecrusher", 0, 885, "pm13", null, null, B(E.PulverizeBonecrusher, 1));

            // Right: Cataclysm, a channelled slam, then the defences that let it finish charging.
            t.S("pr1", "Heavy Grip", 70, 205, "p2", B(E.Damage, 6));
            t.S("pr2", "Heavy Grip", 135, 260, "pr1", B(E.Damage, 6));
            t.N("pr3", "Concussive Blows", 190, 325, "pr2", B(E.DamageVsStaggered, 40));
            t.S("pr4", "Gathering Strength", 225, 395, "pr3", B(E.Area, 6));
            t.S("pr5", "Gathering Strength", 245, 465, "pr4", B(E.Area, 6));
            t.K("pr6", "Cataclysm", 255, 545, "pr5", FormAndPatience,
                "Cannot be combined with Worldshaker, Twin Hammer or Measured Blows", B(E.PulverizeCataclysm, 1));
            t.S("pr7", "Deep Breath", 320, 605, "pr6", B(E.Area, 6));
            t.S("pr8", "Deep Breath", 360, 675, "pr7", B(E.Area, 6));
            t.N("pr9", "Braced Channel", 380, 755, "pr8", B(E.ChannelDamageTaken, -30));
            t.S("pr10", "Firm Grip", 195, 615, "pr6", B(E.Damage, 6));
            t.S("pr11", "Firm Grip", 160, 690, "pr10", B(E.Damage, 6));
            t.N("pr12", "Unbroken Channel", 150, 770, "pr11", B(E.ChannelUnbroken, 1));
            t.Register();
        }

        private static void BuildWhirlwind()
        {
            var t = new TreeBuilder(StatType.GrantWhirlwind);
            t.S("w1", "Cutting Wind", 0, 75, "root", B(E.Damage, 6));
            t.S("w2", "Cutting Wind", 0, 145, "w1", B(E.Damage, 6));

            // Left: tempo, then Gale Cutter's thrown blades and their reach.
            t.S("wl1", "Keen Gale", -70, 205, "w2", B(E.Damage, 6));
            t.S("wl2", "Keen Gale", -135, 260, "wl1", B(E.Damage, 6));
            t.N("wl3", "Rising Tempo", -190, 325, "wl2", B(E.ChannelRamp, 25));
            t.S("wl4", "Rising Wind", -225, 395, "wl3", B(E.Speed, 3));
            t.S("wl5", "Rising Wind", -245, 465, "wl4", B(E.Speed, 3));
            t.K("wl6", "Gale Cutter", -255, 545, "wl5", null, null, B(E.WhirlGaleCutter, 1));
            t.S("wl7", "Sharp Gale", -320, 605, "wl6", B(E.Damage, 6));
            t.S("wl8", "Sharp Gale", -360, 675, "wl7", B(E.Damage, 6));
            t.N("wl9", "Howling Gale", -380, 755, "wl8", B(E.Range, 50));
            t.S("wl10", "Light Feet", -195, 615, "wl6", B(E.Speed, 3));
            t.S("wl11", "Light Feet", -150, 685, "wl10,wm8", B(E.Speed, 3));
            t.N("wl12", "Fleet Spin", -215, 765, "wl11", B(E.ChannelMoveSpeed, 25));

            // Centre: endurance, Gathering Storm, then Tempest Release far above.
            t.S("wm1", "Honed Steel", 0, 215, "w2", B(E.Damage, 6));
            t.S("wm2", "Honed Steel", 0, 285, "wm1", B(E.Damage, 6));
            t.N("wm3", "Battle Trance", 0, 360, "wm2", B(E.Cost, -20));
            t.S("wm4", "Honed Rhythm", 0, 430, "wm3", B(E.Speed, 3));
            t.S("wm5", "Honed Rhythm", 0, 500, "wm4", B(E.Speed, 3));
            t.K("wm6", "Gathering Storm", 0, 580, "wm5", null, null, B(E.WhirlGatheringStorm, 1));
            t.S("wm7", "Building Pressure", 0, 655, "wm6", B(E.Speed, 3));
            t.S("wm8", "Building Pressure", 0, 725, "wm7", B(E.Speed, 3));
            t.K("wm9", "Tempest Release", 0, 815, "wm8", null,
                "Ending Whirlwind after channelling at least " + WeaponSkillRules.ReleaseMinimumSeconds.ToString("0.#") +
                " second unleashes a shockwave twice its radius\nCounts up to " + WeaponSkillRules.ReleaseMaximumSeconds.ToString("0.#") +
                " seconds of channelling; an interrupted channel releases nothing", B(E.ChannelRelease, 100));
            t.S("wm10", "Gathered Fury", 0, 890, "wm9", B(E.Damage, 6));
            t.S("wm11", "Gathered Fury", 0, 960, "wm10", B(E.Damage, 6));
            t.N("wm12", "Thunderclap", 0, 1045, "wm11", B(E.ReleaseStun, 1), B(E.ChannelRelease, 40));

            // Right: holding ground. Slipstream and Maelstrom pull the spin in opposite directions.
            t.S("wr1", "Iron Edge", 70, 205, "w2", B(E.Damage, 6));
            t.S("wr2", "Iron Edge", 135, 260, "wr1", B(E.Damage, 6));
            t.N("wr3", "Braced Channel", 190, 325, "wr2", B(E.ChannelDamageTaken, -30));
            t.S("wr4", "Wide Spin", 175, 400, "wr3", B(E.Area, 6));
            t.S("wr5", "Wide Spin", 160, 470, "wr4", B(E.Area, 6));
            t.K("wr6", "Slipstream", 150, 550, "wr5", null, null, B(E.WhirlSlipstream, 1));
            t.S("wr14", "Rooted Will", 205, 625, "wr6", B(E.Damage, 6));
            t.S("wr15", "Rooted Will", 225, 700, "wr14", B(E.Damage, 6));
            t.N("wr16", "Unbroken Channel", 235, 780, "wr15", B(E.ChannelUnbroken, 1));
            t.S("wr7", "Pull of the Storm", 260, 385, "wr3", B(E.Speed, 3));
            t.S("wr8", "Pull of the Storm", 305, 450, "wr7", B(E.Speed, 3));
            t.S("wr9", "Pull of the Storm", 330, 525, "wr8", B(E.Speed, 3));
            t.K("wr10", "Maelstrom", 345, 610, "wr9", null, null, B(E.WhirlMaelstrom, 1));
            t.S("wr11", "Tightening Gyre", 380, 685, "wr10", B(E.Area, 6));
            t.S("wr12", "Tightening Gyre", 395, 760, "wr11", B(E.Area, 6));
            t.N("wr13", "Eye of the Storm", 405, 845, "wr12", B(E.PerfectImpact, 1));
            t.Register();
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
                case E.WhirlGaleCutter: return "Melee pulses that land launch 3 straight, piercing blades towards your aim (one volley per pulse)\nMelee deals 40% of normal damage; each projectile deals 20%\nAll three projectiles can hit the same enemy; spectral hits cannot trigger volleys";
                case E.ChannelUnbroken: return "Cannot be stunned or staggered while channelling";
                case E.WhirlGatheringStorm: return "Whirlwind melee hits grant a spectral blade, up to 5; at most one blade gained per second\nEach blade adds a melee hit for 13.33% of Whirlwind's melee damage per revolution (40% of total damage at full stacks without Gale Cutter)\nAfter 3 seconds without a melee hit, lose one blade per second, even after ending Whirlwind\nSpectral hits cannot build blades or trigger Gale Cutter";
                case E.WhirlSlipstream: return "Whirlwind moves at 120% of normal running speed instead of 70%\n50% less Whirlwind area of effect";
                case E.WhirlMaelstrom: return "Each Whirlwind hit drags non-boss enemies within twice its radius " +
                    WeaponSkillRules.MaelstromPullDistance.ToString("0.##") + " metres towards you\n" + More(-(1f - WeaponSkillRules.MaelstromDamage) * 100f, "Whirlwind damage");
                case E.ChannelRelease: return n + "% of a Whirlwind hit's damage per second channelled is released when you stop";
                case E.ReleaseStun: return "Tempest Release stuns non-boss enemies for 2 seconds";
                case E.ChannelDamageTaken: return Percent(b.Value, "damage taken while channelling");
                case E.ChannelMoveSpeed: return Percent(b.Value, "movement speed while channelling");
                case E.ChannelRamp: return "While channelling, Whirlwind spins " + WeaponSkillRules.ChannelRampPerSecond.ToString("0.#") + "% faster each second, up to " + b.Value.ToString("0.#") + "%";
                case E.Damage: return Percent(b.Value, damage);
                case E.MoreDamage: return More(b.Value, damage);
                case E.PulverizeWorldshaker: return "Replaces the third slam with Worldshaker\nDeals 300% of normal hit damage with 35% greater radius and a 40% longer windup\nStuns non-boss enemies hit for 2 seconds";
                case E.PulverizeTwinHammer: return "Replaces Pulverize with two alternating slams, each dealing 130% of normal hit damage\nDeliberate windup before the first slam, followed by a rapid second slam\n50% shorter recovery between pairs";
                case E.PulverizeBonecrusher: return "Enemies killed by Pulverize release 6 fast bone shards\nEach shard deals 10% of the slain enemy's maximum life as Physical damage and hits once\nFour shards scatter towards nearby enemies; two scatter freely\nShard kills cannot release more shards";
                case E.PulverizeCataclysm: return "Replaces the combo with Cataclysm, a channelled slam: hold the attack to raise your weapon, release to slam\n" +
                    "Full charge takes " + WeaponSkillRules.CataclysmChargeIntervals.ToString("0.#") + " attack intervals: " +
                    (WeaponSkillRules.CataclysmTapDamage * 100f).ToString("0") + "% of normal hit damage when tapped, " +
                    (WeaponSkillRules.CataclysmFullDamage * 100f).ToString("0") + "% with " + ((WeaponSkillRules.CataclysmFullRadius - 1f) * 100f).ToString("0") + "% greater radius at full charge\n" +
                    "Costs twice as much; you stand still while channelling, and a stagger interrupts the charge";
                case E.PerfectImpact: return grant == StatType.GrantWhirlwind
                    ? "Whirlwind hits deal 30% more damage to enemies within the inner half of its radius"
                    : "Pulverize hits deal 30% more damage within the inner half of the slam's radius\nThe brighter impact centre marks this zone";
                case E.CleaveExplosion: return "Replaces the fourth slash with a circular explosion at the sword's ground impact\nFirst three slashes each deal " + (WeaponSkillRules.CleaveExplosiveSlashDamage * 100f).ToString("0.#") + "% of normal hit damage\nExplosion deals " + (WeaponSkillRules.CleaveExplosionDamage * 100f).ToString("0.#") + "% of normal hit damage with " + ((WeaponSkillRules.CleaveExplosionRadiusMultiplier - 1f) * 100f).ToString("0.#") + "% greater reach";
                case E.CleaveDoubleSlash: return "Replaces Cleave with a two-part combo: double slash, then double slash\nEach slash deals " + (WeaponSkillRules.CleaveDoubleSlashDamage * 100f).ToString("0.#") + "% of normal hit damage; the second pair deals 10% more\nThe second pair has 35% greater radius and pulls surviving non-boss enemies inward up to 1.2 metres per cut without a guaranteed stagger\nEach cut has its own forward step, alternating swing and hit";
                case E.CleaveBleed: return "Every Cleave hit inflicts bleeding for " + b.Value.ToString("0.#") + "% of its damage over " + WeaponSkillRules.CleaveBleedSeconds.ToString("0.#") + " seconds\nBleeds from separate hits stack";
                case E.CleaveRupture: return "Combo finishers rupture bleeding enemies: they take all remaining bleed damage at once, " +
                    ((WeaponSkillRules.RuptureMultiplier - 1f) * 100f).ToString("0") + "% increased";
                case E.Overkill: return n + "% of a killing blow's excess damage bursts into enemies within " + WeaponSkillRules.OverkillRadius.ToString("0") + " metres of the corpse";
                case E.OverkillHeal: return "Carnage bursts heal you " + b.Value.ToString("0.#") + "% of maximum life per enemy they hit";
                case E.ExecuteDamage: return More(b.Value, damage + " against enemies below " + (WeaponSkillRules.ExecuteThreshold * 100f).ToString("0") + "% life");
                case E.DamageVsStaggered: return More(b.Value, damage + " against staggered or stunned enemies");
                case E.ComboDamageTaken: return Percent(b.Value, "damage taken during combos");
                case E.ComboSetupDamage: return More(b.Value, "damage with combo hits before the finisher");
                case E.FinisherStun: return "Combo finishers stun non-boss enemies for 2 seconds";
                case E.MeasuredStrike: return More(b.Value, "damage with Measured attacks");
                case E.MeasuredRest: return Percent(b.Value, "rest needed for a Measured attack");
                case E.MeasuredStun: return "Measured hits stun non-boss enemies for 2 seconds";
                case E.ComboDamage: return More(b.Value, "damage with combo finishers (every " + WeaponSkillRules.ComboLength(grant) + " attacks)");
                case E.ComboArea: return Percent(b.Value, "area of effect with combo finishers (every " + WeaponSkillRules.ComboLength(grant) + " attacks)");
                case E.ComboKnockback: return "Combo finishers (every " + WeaponSkillRules.ComboLength(grant) + " attacks) knock non-boss enemies back " + b.Value.ToString("0.#") + " metres";
                case E.MageCastSpeedAfterSummon: return More(b.Value, "skeleton mage cast speed if you have summoned a minion in the last 4 seconds");
                case E.Speed: return grant == StatType.GrantWhirlwind ? Percent(b.Value, "Whirlwind spin rate")
                    : Percent(b.Value, grant == StatType.GrantSkeletonMages ? "minion cast speed" : minion ? "minion attack speed" : attack ? "attack speed" : "cast speed");
                case E.Area: return Percent(b.Value, "area of effect" + (grant == StatType.GrantRainOfArrows ? " per arrow impact" : ""));
                case E.Range: return Percent(b.Value, grant == StatType.GrantWhirlwind ? "Gale Cutter range" : "range");
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
                case E.ArmourPenetration: return n + " armour penetration rating";
                case E.PackHunt: return Percent(b.Value, "damage per other Spirit Wolf within 6 metres (up to 4)");
                case E.MinionRegen: return "Minions regenerate " + b.Value.ToString("0.#") + "% of maximum life per second";
                case E.GolemAftershock: return "Slam repeats after 0.6 seconds for 40% damage";
                default: return n + " " + b.Effect;
            }
        }
    }
}
