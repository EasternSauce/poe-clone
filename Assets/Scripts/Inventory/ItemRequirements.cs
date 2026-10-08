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

        public override string ToString()
        {
            string text = Level > 0 ? "Requires level " + Level : "No level requirement";
            if (Strength > 0) text += ", " + Strength + " STR";
            if (Dexterity > 0) text += ", " + Dexterity + " DEX";
            if (Intelligence > 0) text += ", " + Intelligence + " INT";
            return text;
        }
    }
}
