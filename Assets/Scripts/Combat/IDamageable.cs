namespace PoeClone.Combat
{
    /// <summary>Anything that can be hit by an attack and lose health.</summary>
    public interface IDamageable
    {
        void TakeDamage(float amount);
    }
}
