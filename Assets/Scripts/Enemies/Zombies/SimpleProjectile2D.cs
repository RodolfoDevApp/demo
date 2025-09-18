using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class SimpleProjectile2D : MonoBehaviour
{
    [Header("Movimiento / Vida")]
    public float speed = 8f;
    public float lifetime = 4f;

    [Header("Daño")]
    public float damage = 1f;
    public float knockback = 4f;
    public int maxHits = 1;

    [Header("Colisión")]
    public LayerMask hitMask = ~0;

    [Header("Dueño")]
    public GameObject owner;

    Rigidbody2D rb;
    Collider2D col;
    int hits = 0;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        // Reemplaza isKinematic por bodyType y configura para proyectil 2D
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // Asegúrate de que el collider esté en modo trigger
        col.isTrigger = true;
    }

    public void Launch(Vector2 dir)
    {
        // Usa linearVelocity (linearlinearVelocity no es necesario aquí)
        rb.linearVelocity = dir.normalized * speed;
        hits = 0;
        CancelInvoke(nameof(DestroySelf));
        Invoke(nameof(DestroySelf), lifetime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!enabled) return;

        // Ignora al dueño (mismo root)
        if (owner && other.transform.root == owner.transform.root) return;

        // Filtra por máscara
        if (((1 << other.gameObject.layer) & hitMask.value) == 0)
        {
            // Si quieres que atraviese paredes, comenta la siguiente línea:
            DestroySelf();
            return;
        }

        // Daño estándar por contrato IDamageable
        if (other.TryGetComponent<IDamageable>(out var idmg))
        {
            Vector2 hp = other.ClosestPoint(transform.position);
            Vector2 d = rb.linearVelocity.sqrMagnitude > 0.0001f ? (Vector2)rb.linearVelocity.normalized : Vector2.right;

            idmg.ApplyDamage(new DamageInfo(damage, d, hp, DamageKind.Bullet, knockback, gameObject, owner));
            hits++;
            if (hits >= Mathf.Max(1, maxHits)) DestroySelf();
            return;
        }

        // Compat con PlayerHealth directo
        if (other.TryGetComponent<PlayerHealth>(out var ph))
        {
            ph.TakeDamage(Mathf.RoundToInt(damage));
            hits++;
            if (hits >= Mathf.Max(1, maxHits)) DestroySelf();
            return;
        }

        // Obstáculo u otro objeto -> destruir (o comenta si quieres que siga)
        DestroySelf();
    }

    void DestroySelf()
    {
        if (this) Destroy(gameObject);
    }
}
