namespace PoeClone.Combat
{
    /// <summary>
    /// What a hit is made of. Armour only stops Physical; each element has its own resistance.
    /// </summary>
    public enum DamageType
    {
        Physical,
        Fire,
        Cold,
        Lightning
    }
}
