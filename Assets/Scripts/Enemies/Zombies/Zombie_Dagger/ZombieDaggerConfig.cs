using UnityEngine;

[CreateAssetMenu(fileName = "Zombie_Dagger_Config", menuName = "Configs/Zombie Dagger Config")]
public class ZombieDaggerConfig : ScriptableObject
{
    [Header("Deteccion / Persecucion")]
    public float detectionRadius = 10f;
    public float loseSightDistanceFactor = 1.25f;
    public float aggroHoldSeconds = 4f;

    [Header("Movimiento")]
    public float moveSpeed = 3.2f;
    public float acceleration = 18f;
    public float deceleration = 24f;

    [Header("Ataque A (punalada con hitbox)")]
    public float stabDecisionRange = 1.35f;
    public float stabCooldown = 1.0f;
    public float stabWindowSeconds = 0.12f; // NUEVO: duracion de ventana de hitbox

    [Header("Ataque B (dash frontal)")]
    public float dashDecisionMin = 1.5f;
    public float dashDecisionMax = 5.5f;
    public float dashSpeed = 7.0f;
    public float dashTime = 0.25f;
    public float dashCooldown = 1.2f;
}
