using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WeaponProjectile2D : MonoBehaviour
{
    [Header("Movimiento")]
    public float speed = 18f;
    public float lifeTime = 1.2f;
    public float maxDistance = 26f;
    public bool alignToVelocity = true;

    [Header("Daño")]
    public float damage = 10f;
    public float knockback = 2f;
    public int pierce = 0;                     // 0 = se destruye al primer enemigo; 999 = atraviesa todos
    public DamageKind kind = DamageKind.Bullet;

    [Header("Colisión")]
    public LayerMask hitMask = ~0;
    public bool destroyOnNonDamageable = true;

    [Header("Refs opcionales")]
    public SpriteRenderer ownerSR;             // copia sorting layer/order
    public Transform parentOnSpawn;            // para tener limpio el hierarchy

    // runtime
    GameObject owner;
    Vector2 dir;
    Vector2 startPos;
    float life;
    SpriteRenderer sr;
    Rigidbody2D rb;
    readonly HashSet<Collider2D> hitSet = new();

    public void Init(Vector2 direction, GameObject ownerGO, SpriteRenderer ownerSprite = null, Transform parent = null)
    {
        owner = ownerGO;
        dir = direction.normalized;
        startPos = transform.position;
        life = lifeTime;
        parentOnSpawn = parent;

        if (parentOnSpawn) transform.SetParent(parentOnSpawn, true);

        sr = GetComponent<SpriteRenderer>();
        if (!sr) sr = GetComponentInChildren<SpriteRenderer>(true);
        if (ownerSprite)
        {
            ownerSR = ownerSprite;
            sr.sortingLayerID = ownerSR.sortingLayerID;
            sr.sortingOrder = ownerSR.sortingOrder + 1;
        }

        rb = GetComponent<Rigidbody2D>();

        if (alignToVelocity && sr)
        {
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, ang);
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        Vector3 delta = (Vector3)(dir * speed * dt);

        if (rb) rb.MovePosition(rb.position + (Vector2)delta);
        else transform.position += delta;

        // lifetime / distancia
        life -= dt;
        if (life <= 0f || (maxDistance > 0f && Vector2.Distance(startPos, transform.position) >= maxDistance))
        {
            Destroy(gameObject);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Ignorar cosas que no están en máscara
        if (((1 << other.gameObject.layer) & hitMask.value) == 0) return;

        // Ignorar al dueño
        if (owner && other.transform.root == owner.transform.root) return;

        // Evitar múltiples impactos sobre el mismo collider
        if (hitSet.Contains(other)) return;
        hitSet.Add(other);

        // Buscar IDamageable
        IDamageable dmg = other.GetComponentInParent<IDamageable>();
        if (dmg != null)
        {
            var p = (Vector2)transform.position;
            var info = new DamageInfo(
                amount: damage,
                dir: dir,
                hitPoint: p,
                kind: kind,
                knockback: knockback,
                source: gameObject,
                owner: owner ? owner : gameObject
            );
            dmg.ApplyDamage(info);

            if (pierce <= 0) { Destroy(gameObject); }
            else { pierce--; }
        }
        else if (destroyOnNonDamageable)
        {
            Destroy(gameObject);
        }
    }
}
