using UnityEngine;

public enum DamageKind { Melee, Bullet, Shotgun, Explosion, Other }

/// <summary>
/// Paquete de datos para cualquier da�o del juego.
/// </summary>
public struct DamageInfo
{
    public float amount;          // Da�o bruto
    public Vector2 dir;           // Direcci�n del golpe (normalizada si puedes)
    public Vector2 hitPoint;      // Punto de impacto (mundo)
    public DamageKind kind;       // Tipo de da�o (para resistencias/VFX)
    public float knockback;       // Intensidad del empuje
    public GameObject source;     // Qu� gener� el da�o (bala, hitbox�)
    public GameObject owner;      // Due�o (player/enemigo que lo caus�)

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
