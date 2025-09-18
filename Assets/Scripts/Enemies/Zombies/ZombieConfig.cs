using UnityEngine;

public enum ZombieSpecial
{
    None,
    AxeThrow,
    GroundSlam,
    Dash
}

[System.Serializable]
public struct WeightedPickup
{
    public GameObject prefab;
    [Min(0f)] public float weight;
}

[CreateAssetMenu(fileName = "ZombieConfig", menuName = "Zombies/Zombie Config", order = 0)]
public class ZombieConfig : ScriptableObject
{
    [Header("Movimiento / Detección")]
    [Min(0.5f)] public float moveSpeed = 1.6f;
    [Min(0.2f)] public float acceleration = 12f;
    [Min(0.2f)] public float deceleration = 14f;
    [Min(0.2f)] public float detectionRadius = 8f;
    [Tooltip("0 = ignora máscara y usa solo distancia. Si no es 0, confirma con OverlapCircle.")]
    public LayerMask playerMask = 0;

    [Header("Persistencia de Aggro")]
    [Min(0f)] public float aggroHoldSeconds = 3f;
    [Min(1f)] public float loseSightDistanceFactor = 1.6f;

    [Header("Ataque A (melee)")]
    [Min(0.2f)] public float attackRangeA = 1.2f;     // “muy cerca”
    [Min(0.05f)] public float attackCooldownA = 0.8f;
    [Min(1f)] public float attackDamageA = 1f;
    [Min(0f)] public float attackKnockbackA = 3f;

    [Header("Ataque B (especial)")]
    public ZombieSpecial special = ZombieSpecial.AxeThrow;
    [Min(0.2f)] public float attackRangeB = 1.2f;     // sin uso directo, dejamos por compat
    [Min(0.05f)] public float attackCooldownB = 1.4f;
    [Min(1f)] public float attackDamageB = 1f;
    [Min(0f)] public float attackKnockbackB = 3f;

    [Header("B: AXE (si special = AxeThrow)")]
    public AxeProjectile2D axeProjectilePrefab;
    [Min(1f)] public float projectileSpeed = 8f;
    [Min(0.2f)] public float projectileLife = 0.8f;
    [Min(0f)] public float projectileMaxDistance = 5.5f; // limita recorrido
    public Vector2 projectileSpawnOffset = new Vector2(0.2f, 0.2f);
    public LayerMask projectileHitMask = ~0;

    [Space(6)]
    [Tooltip("No lanzar si está más cerca que esto (debe ser > attackRangeA).")]
    [Min(0f)] public float throwMinDistance = 1.45f;
    [Tooltip("No lanzar si está más lejos que esto (0 = sin tope).")]
    [Min(0f)] public float throwMaxDistance = 5.5f;

    [Tooltip("Si NO tiene hacha y el jugador está a ? esta distancia, decide ir por el hacha.")]
    [Min(0f)] public float pickupDecisionDistance = 3.0f;
    [Tooltip("Tras tomar el hacha, si el jugador está a distancia de lanzamiento, dispara de inmediato.")]
    public bool throwImmediatelyAfterPickup = true;

    [Header("Retrocompat: proyectil genérico")]
    public SimpleProjectile2D genericProjectilePrefab;

    [Header("B: GroundSlam")]
    [Min(0.2f)] public float slamRadius = 1.6f;
    public LayerMask slamMask = ~0;

    [Header("B: Dash")]
    [Min(1f)] public float dashSpeed = 6f;
    [Min(0.05f)] public float dashTime = 0.25f;

    [Header("Loot (simple)")]
    [Range(0f, 1f)] public float dropAmmoChance = 0.45f;
    [Range(0f, 1f)] public float dropMedkitChance = 0.25f;
    public GameObject ammoPickupPrefab;
    public GameObject medkitPickupPrefab;

    [Header("Loot (avanzado)")]
    public WeightedPickup[] ammoTable;

    [Header("VFX / SFX")]
    public GameObject deathVfxPrefab;
    public AudioClip deathSfx;
}
