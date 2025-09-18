using UnityEngine;

[DisallowMultipleComponent]
public class PlayerDamageReceiver : MonoBehaviour, IDamageable
{
    public PlayerHealth playerHealth;

    void Reset()
    {
        if (!playerHealth) playerHealth = GetComponent<PlayerHealth>();
        if (!playerHealth) playerHealth = GetComponentInParent<PlayerHealth>();
    }

    void Awake()
    {
        if (!playerHealth) playerHealth = GetComponent<PlayerHealth>();
        if (!playerHealth) playerHealth = GetComponentInParent<PlayerHealth>();
    }

    public void ApplyDamage(DamageInfo info)
    {
        if (!playerHealth) return;
        int amount = Mathf.Max(1, Mathf.RoundToInt(info.amount));
        playerHealth.TakeDamage(amount);
        // Knockback opcional: si deseas empujar al jugador, puedes agregar aquí AddForce si hay RB2D
    }

    public bool IsAlive => playerHealth && !playerHealth.IsDead;
}
