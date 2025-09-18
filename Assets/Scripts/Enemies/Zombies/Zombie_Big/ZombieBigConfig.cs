using UnityEngine;

[CreateAssetMenu(fileName = "Zombie_Big_Config", menuName = "Configs/Zombie Big Config")]
public class ZombieBigConfig : ScriptableObject
{
    [Header("Deteccion / Persecucion")]
    public float detectionRadius = 10f;
    public float aggroHoldSeconds = 4f;

    [Header("Movimiento")]
    public float moveSpeed = 2.6f;
    public float acceleration = 14f;
    public float deceleration = 18f;

    [Header("Attack A (contacto)")]
    public float attackARange = 1.1f;
    public float attackACooldown = 1.0f;

    [Header("Attack B (Ground Slam)")]
    public float slamRadius = 1.8f;
    public LayerMask slamMask;
    public float slamDamage = 1f;
    public float slamKnockback = 3f;
    public float slamCooldown = 1.6f;
    public float slamAutoHitDelay = 0.18f;

    [Header("Decision Slam")]
    public float slamDecisionMin = 1.2f;
    public float slamDecisionMax = 4.5f;

    [Header("Offsets por direccion (locales al zombie)")]
    public Vector2 slamOffsetDown = new Vector2(0.00f, -0.10f);
    public Vector2 slamOffsetRight = new Vector2(0.70f, -0.05f);
    public Vector2 slamOffsetLeft = new Vector2(-0.70f, -0.05f);
    public Vector2 slamOffsetUp = new Vector2(0.00f, 0.55f);
}
