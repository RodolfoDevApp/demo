public interface IDamageable
{
    /// <summary>Aplica da�o al objeto.</summary>
    void ApplyDamage(DamageInfo info);

    /// <summary>Si el objeto sigue vivo.</summary>
    bool IsAlive { get; }
}
