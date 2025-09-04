public interface IDamageable
{
    /// <summary>Aplica daño al objeto.</summary>
    void ApplyDamage(DamageInfo info);

    /// <summary>Si el objeto sigue vivo.</summary>
    bool IsAlive { get; }
}
