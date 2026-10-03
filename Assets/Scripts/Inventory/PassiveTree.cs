using System;
using System.Collections.Generic;

namespace PoeClone.Inventory
{
    public enum PassiveBranch
    {
        Origin,
        Might,     // strength: life, armour, damage
        Grace,     // dexterity: evasion, speed
        Wisdom     // intelligence: mana, spells, resistances
    }

    /// <summary>One passive: its stats, where it's drawn, and which passives it connects to.</summary>
    public sealed class PassiveNode
    {
        public string Id;
        public string Name;
        public PassiveBranch Branch;
        public bool Notable;
        public bool Keystone;   // a rule-changer at the very end of an arm (special stats)
        public float X, Y;   // layout, roughly -1..1
        public StatModifier[] Mods;
        public readonly List<string> Links = new List<string>();
    }

    /// <summary>
    /// A small passive tree, much simpler than PoE's: from the centre, three branches (Might,
    /// Grace, Wisdom), each splitting in two and ending in a notable passive, and past each notable
    /// a keystone that changes how the character plays (extra arrows, leech, Mind over Matter...).
    /// Every level after the first gives one point; a passive can be taken once one next to it is.
    /// </summary>
    public static class PassiveTree
    {
        public const string OriginId = "origin";

        private static readonly Dictionary<string, PassiveNode> byId = new Dictionary<string, PassiveNode>();
        private static readonly List<PassiveNode> nodes = new List<PassiveNode>();

        public static IReadOnlyList<PassiveNode> Nodes => nodes;

        public static PassiveNode Get(string id)
        {
            return id != null && byId.TryGetValue(id, out PassiveNode node) ? node : null;
        }

        static PassiveTree()
        {
            Add(OriginId, "Origin", PassiveBranch.Origin, false, 0f, 0f);

            // Might: down-left.
            Branch(PassiveBranch.Might, 210f,
                Node("m1", "Toughness", Mod(StatType.MaxLife, 12)),
                Node("m2", "Brawn", Mod(StatType.Strength, 6)),
                new[]
                {
                    Node("m3", "Iron Skin", Mod(StatType.Armour, 25)),
                    Node("m4", "Thick Hide", Mod(StatType.MaxLife, 15), Mod(StatType.Armour, 15)),
                    Notable("m5", "Juggernaut", Mod(StatType.MaxLife, 30), Mod(StatType.Armour, 50), Mod(StatType.BlockChance, 5))
                },
                new[]
                {
                    Node("m6", "Heavy Hands", Mod(StatType.PhysicalDamage, 2)),
                    Node("m7", "Power", Mod(StatType.Strength, 8)),
                    Notable("m8", "Brute Force", Mod(StatType.PhysicalDamage, 6), Mod(StatType.Strength, 10), Mod(StatType.MeleeRange, 15))
                });

            // Grace: down-right.
            Branch(PassiveBranch.Grace, 330f,
                Node("g1", "Nimble", Mod(StatType.Evasion, 20)),
                Node("g2", "Agility", Mod(StatType.Dexterity, 6)),
                new[]
                {
                    Node("g3", "Light Step", Mod(StatType.MovementSpeed, 4)),
                    Node("g4", "Dodge", Mod(StatType.Evasion, 30)),
                    Notable("g5", "Wind Dancer", Mod(StatType.MovementSpeed, 8), Mod(StatType.Evasion, 60))
                },
                new[]
                {
                    Node("g6", "Quick Hands", Mod(StatType.AttackSpeed, 5)),
                    Node("g7", "Precision", Mod(StatType.PhysicalDamage, 2), Mod(StatType.Dexterity, 4)),
                    Notable("g8", "Flurry", Mod(StatType.AttackSpeed, 12), Mod(StatType.PhysicalDamage, 3))
                });

            // Wisdom: up.
            Branch(PassiveBranch.Wisdom, 90f,
                Node("w1", "Focus", Mod(StatType.MaxMana, 12)),
                Node("w2", "Insight", Mod(StatType.Intelligence, 6)),
                new[]
                {
                    Node("w3", "Spellcraft", Mod(StatType.Intelligence, 8)),
                    Node("w4", "Deep Well", Mod(StatType.MaxMana, 20)),
                    Notable("w5", "Arcane Mind", Mod(StatType.Intelligence, 15), Mod(StatType.MaxMana, 30), Mod(StatType.AreaOfEffect, 12))
                },
                new[]
                {
                    Node("w6", "Warding", Mod(StatType.FireResistance, 12), Mod(StatType.ColdResistance, 12)),
                    Node("w7", "Grounding", Mod(StatType.LightningResistance, 15), Mod(StatType.MaxLife, 8)),
                    Notable("w8", "Elemental Ward", Mod(StatType.FireResistance, 15), Mod(StatType.ColdResistance, 15), Mod(StatType.LightningResistance, 15))
                });

            // Keystones, one past each notable: the reward for walking an arm to its end.
            Keystone("k_executioner", "Executioner", PassiveBranch.Might, "m5", Mod(StatType.CullingStrike, 1), Mod(StatType.LifeOnKill, 5));
            Keystone("k_bloodthirst", "Bloodthirst", PassiveBranch.Might, "m8", Mod(StatType.LifeLeech, 2));
            Keystone("k_frostbite", "Frostbite", PassiveBranch.Grace, "g5", Mod(StatType.ChillOnHit, 20));
            Keystone("k_volley", "Volley", PassiveBranch.Grace, "g8", Mod(StatType.AdditionalArrows, 1));
            Keystone("k_twincast", "Twin Casting", PassiveBranch.Wisdom, "w5", Mod(StatType.AdditionalSpellProjectiles, 1), Mod(StatType.SpellDamage, 10));
            Keystone("k_mom", "Mind over Matter", PassiveBranch.Wisdom, "w8", Mod(StatType.ManaAbsorb, 30));
        }

        // A keystone further out along the line from the centre through the passive it hangs off.
        private static void Keystone(string id, string name, PassiveBranch branch, string after, params StatModifier[] mods)
        {
            PassiveNode from = byId[after];
            float scale = 1.25f;
            PassiveNode node = Add(id, name, branch, true, from.X * scale, from.Y * scale, mods);
            node.Keystone = true;
            Link(id, after);
        }

        private struct Spec
        {
            public string Id, Name;
            public bool Notable;
            public StatModifier[] Mods;
        }

        private static Spec Node(string id, string name, params StatModifier[] mods) => new Spec { Id = id, Name = name, Mods = mods };
        private static Spec Notable(string id, string name, params StatModifier[] mods) => new Spec { Id = id, Name = name, Mods = mods, Notable = true };
        private static StatModifier Mod(StatType stat, float value) => new StatModifier(stat, value);

        // A branch: two passives out from the centre along the angle, then two arms of three
        // fanning out either side of it.
        private static void Branch(PassiveBranch branch, float angle, Spec first, Spec second, Spec[] armA, Spec[] armB)
        {
            PassiveNode a = Place(first, branch, angle, 0.22f, OriginId);
            PassiveNode b = Place(second, branch, angle, 0.42f, a.Id);
            string previous = b.Id;
            for (int k = 0; k < armA.Length; k++)
                previous = Place(armA[k], branch, angle - 14f - k * 4f, 0.6f + k * 0.17f, previous).Id;
            previous = b.Id;
            for (int k = 0; k < armB.Length; k++)
                previous = Place(armB[k], branch, angle + 14f + k * 4f, 0.6f + k * 0.17f, previous).Id;
        }

        private static PassiveNode Place(Spec spec, PassiveBranch branch, float angle, float radius, string linkTo)
        {
            double rad = angle * Math.PI / 180.0;
            PassiveNode node = Add(spec.Id, spec.Name, branch, spec.Notable,
                (float)(Math.Cos(rad) * radius), (float)(Math.Sin(rad) * radius), spec.Mods);
            Link(node.Id, linkTo);
            return node;
        }

        private static PassiveNode Add(string id, string name, PassiveBranch branch, bool notable, float x, float y, params StatModifier[] mods)
        {
            var node = new PassiveNode { Id = id, Name = name, Branch = branch, Notable = notable, X = x, Y = y, Mods = mods ?? new StatModifier[0] };
            nodes.Add(node);
            byId[id] = node;
            return node;
        }

        private static void Link(string a, string b)
        {
            byId[a].Links.Add(b);
            byId[b].Links.Add(a);
        }
    }

    /// <summary>Which passives a character has taken, and the rules for taking them.</summary>
    public sealed class PassiveAllocation
    {
        private readonly HashSet<string> taken = new HashSet<string> { PassiveTree.OriginId };

        public event Action Changed;

        /// <summary>Passives taken, not counting the free origin.</summary>
        public int Spent => taken.Count - 1;

        public bool Has(string id) => taken.Contains(id);

        /// <summary>Every passive taken, the origin included.</summary>
        public IEnumerable<string> Taken => taken;

        public static int PointsForLevel(int level) => Math.Max(0, level - 1);

        public int Unspent(int level) => PointsForLevel(level) - Spent;

        /// <summary>Not taken yet, next to one that is, and a point to spend.</summary>
        public bool CanTake(string id, int level)
        {
            PassiveNode node = PassiveTree.Get(id);
            if (node == null || taken.Contains(id) || Unspent(level) <= 0)
                return false;
            foreach (string link in node.Links)
            {
                if (taken.Contains(link))
                    return true;
            }
            return false;
        }

        public bool Take(string id, int level)
        {
            if (!CanTake(id, level))
                return false;
            taken.Add(id);
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Gives a passive back, if everything else taken stays connected to the origin without it
        /// (so only the ends of a path can be given back).
        /// </summary>
        public bool CanRefund(string id)
        {
            if (id == PassiveTree.OriginId || !taken.Contains(id))
                return false;

            var reached = new HashSet<string> { PassiveTree.OriginId };
            var open = new Stack<string>();
            open.Push(PassiveTree.OriginId);
            while (open.Count > 0)
            {
                foreach (string link in PassiveTree.Get(open.Pop()).Links)
                {
                    if (link != id && taken.Contains(link) && reached.Add(link))
                        open.Push(link);
                }
            }
            return reached.Count == taken.Count - 1;
        }

        public bool Refund(string id)
        {
            if (!CanRefund(id))
                return false;
            taken.Remove(id);
            Changed?.Invoke();
            return true;
        }

        public void ResetAll()
        {
            if (taken.Count <= 1)
                return;
            taken.Clear();
            taken.Add(PassiveTree.OriginId);
            Changed?.Invoke();
        }

        /// <summary>Every stat line from the passives taken.</summary>
        public List<StatModifier> Modifiers()
        {
            var list = new List<StatModifier>();
            foreach (string id in taken)
            {
                PassiveNode node = PassiveTree.Get(id);
                if (node != null)
                    list.AddRange(node.Mods);
            }
            return list;
        }
    }
}
