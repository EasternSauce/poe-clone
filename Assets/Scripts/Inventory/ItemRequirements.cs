namespace PoeClone.Inventory
{
    public readonly struct ItemRequirements
    {
        public readonly int Level, Strength, Dexterity, Intelligence;

        public ItemRequirements(int level, int strength, int dexterity, int intelligence)
        {
            Level = level;
            Strength = strength;
            Dexterity = dexterity;
            Intelligence = intelligence;
        }

        public bool MetBy(StatSheet stats) => stats != null && stats.Level >= Level &&
            stats.Total(StatType.Strength) >= Strength && stats.Total(StatType.Dexterity) >= Dexterity &&
            stats.Total(StatType.Intelligence) >= Intelligence;

        /// <summary>The requirement line; given the character's stats, every requirement they don't meet is red.</summary>
        public string Describe(StatSheet stats)
        {
            string text = Level > 0 ? Mark("Requires level " + Level, stats != null && stats.Level < Level) : "No level requirement";
            if (Strength > 0) text += ", " + Mark(Strength + " STR", stats != null && stats.Total(StatType.Strength) < Strength);
            if (Dexterity > 0) text += ", " + Mark(Dexterity + " DEX", stats != null && stats.Total(StatType.Dexterity) < Dexterity);
            if (Intelligence > 0) text += ", " + Mark(Intelligence + " INT", stats != null && stats.Total(StatType.Intelligence) < Intelligence);
            return text;
        }

        private static string Mark(string part, bool unmet) => unmet ? "<color=#FF5040>" + part + "</color>" : part;
    }
}
