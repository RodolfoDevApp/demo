using UnityEngine;

public enum DamageKind { Melee, Bullet, Shotgun, Explosion, Other }

/// <summary>
/// Paquete de datos para cualquier daño del juego.
/// </summary>
public struct DamageInfo
{
    public float amount;          // Daño bruto
    public Vector2 dir;           // Dirección del golpe (normalizada si puedes)
    public Vector2 hitPoint;      // Punto de impacto (mundo)
    public DamageKind kind;       // Tipo de daño (para resistencias/VFX)
    public float knockback;       // Intensidad del empuje
    public GameObject source;     // Qué generó el daño (bala, hitbox…)
    public GameObject owner;      // Dueño (player/enemigo que lo causó)

    public DamageInfo(
        float amount,
        Vector2 dir,
        Vector2 hitPoint,
        DamageKind kind = DamageKind.Other,
        float knockback = 0f,
        GameObject source = null,
        GameObject owner = null)
    {
        this.amount = amount;
        this.dir = dir;
        this.hitPoint = hitPoint;
        this.kind = kind;
        this.knockback = knockback;
        this.source = source;
        this.owner = owner;
    }
}
