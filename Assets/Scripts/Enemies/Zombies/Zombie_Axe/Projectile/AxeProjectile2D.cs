using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public enum AxeProjState { Thrown, Landing, Landed }

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class AxeProjectile2D : MonoBehaviour
{
    [Header("Movimiento / Vida")]
    public float speed = 8f;
    public float lifetime = 4f;
    public float landingTime = 0.35f;

    [Header("Daño mientras vuela")]
    public float damage = 1f;
    public float knockback = 4f;
    public int maxHits = 1;
    public LayerMask hitMask = ~0;

    [Header("Refs")]
    public Animator anim;
    public Rigidbody2D rb;
    public Collider2D col;

    [Header("Dueño")]
    public GameObject owner;

    [Tooltip("Si el dueño muere/desaparece, destruir automáticamente este proyectil.")]
    public bool destroyWhenOwnerDies = true;

    [Header("Pickup")]
    public bool isPickupWhenLanded = true;
    public float pickupRadius = 0.5f;

    [Tooltip("Si el hacha queda como pickup, se autodestruye pasado este tiempo. 0 = no despawnear.")]
    public float pickupDespawnAfter = 10f;

    [Header("Límites")]
    [Tooltip("0 = sin límite; si > 0, al superar esta distancia desde el lanzamiento entra en Landing")]
    public float maxTravelDistance = 0f;

    // ---- runtime ----
    int _hits = 0;
    float _dieAt = -1f;
    AxeProjState _state = AxeProjState.Thrown;
    int _dir = 1;
    Vector2 _startPos;
    float _landedAt = -1f;

    public static readonly List<AxeProjectile2D> All = new();

    Damageable _ownerDmg;                // para suscripción a onDeath
    UnityAction _ownerDeathHandler;      // cache para desuscribir

    // -------- lifecycle --------
    void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        if (!anim) anim = GetComponentInChildren<Animator>();
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        col.isTrigger = true;
    }

    void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!col) col = GetComponent<Collider2D>();
        if (!anim) anim = GetComponentInChildren<Animator>();
    }

    void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
        // por si se reusa desde pool
        if (owner) TryBindOwner(owner);
    }

    void OnDisable()
    {
        All.Remove(this);
        UnbindOwner();
    }

    void OnDestroy()
    {
        UnbindOwner();
    }

    // -------- API --------
    public void Launch(Vector2 dir)
    {
        _dir = DirFromVector(dir);
        _state = AxeProjState.Thrown;
        _dieAt = Time.time + Mathf.Max(0.1f, lifetime);
        _hits = 0;
        _startPos = transform.position;
        _landedAt = -1f;

        if (rb) rb.linearVelocity = dir.normalized * Mathf.Max(0.1f, speed);
        PlayThrownAnim(_dir);
    }

    /// <summary> Asigna dueño y queda suscrito a su onDeath para autodestruirse. </summary>
    public void SetOwner(GameObject newOwner)
    {
        owner = newOwner;
        TryBindOwner(owner);
    }

    // -------- update --------
    void Update()
    {
        // 0) si el dueño se destruyó, consume
        if (destroyWhenOwnerDies && (owner == null || owner.Equals(null)))
        {
            Consume();
            return;
        }

        if (_state == AxeProjState.Thrown)
        {
            // lifetime
            if (Time.time >= _dieAt) BeginLanding();

            // distancia máxima
            if (maxTravelDistance > 0f)
            {
                float sq = ((Vector2)transform.position - _startPos).sqrMagnitude;
                if (sq >= maxTravelDistance * maxTravelDistance) BeginLanding();
            }
        }
        else if (_state == AxeProjState.Landed && pickupDespawnAfter > 0f)
        {
            // autodespawn del pickup
            if (_landedAt > 0f && Time.time >= _landedAt + pickupDespawnAfter)
                Consume();
        }
    }

    // -------- colisiones --------
    void OnTriggerEnter2D(Collider2D other)
    {
        if (_state != AxeProjState.Thrown) return;

        // no dañar al dueño (ni a su raíz)
        if (owner && other.transform.root == owner.transform.root) return;

        // Si toca algo fuera de máscara de daño => ambiente -> aterrizar
        if (((1 << other.gameObject.layer) & hitMask.value) == 0)
        {
            BeginLanding();
            return;
        }

        if (other.TryGetComponent<IDamageable>(out var idmg))
        {
            Vector2 hp = other.bounds.ClosestPoint(transform.position);
            Vector2 d = rb && rb.linearVelocity.sqrMagnitude > 0.0001f ? rb.linearVelocity.normalized : Vector2.right;
            idmg.ApplyDamage(new DamageInfo(damage, d, hp, DamageKind.Bullet, knockback, gameObject, owner));
            _hits++;
            if (_hits >= Mathf.Max(1, maxHits)) BeginLanding();
        }
        else if (other.TryGetComponent<PlayerHealth>(out var ph))
        {
            ph.TakeDamage(Mathf.RoundToInt(damage));
            _hits++;
            if (_hits >= Mathf.Max(1, maxHits)) BeginLanding();
        }
        else
        {
            BeginLanding();
        }
    }

    // -------- estados --------
    public bool IsPickup => _state == AxeProjState.Landed;

    public void Consume()
    {
        // aquí podrías disparar VFX/SFX previa destrucción si quieres
        if (this) Destroy(gameObject);
    }

    void BeginLanding()
    {
        if (_state != AxeProjState.Thrown) return;
        _state = AxeProjState.Landing;
        if (rb) rb.linearVelocity = Vector2.zero;
        if (col) col.isTrigger = true;
        PlayLandingAnim(CurrentDir());
        Invoke(nameof(BeLanded), Mathf.Max(0.05f, landingTime));
    }

    void BeLanded()
    {
        _state = AxeProjState.Landed;
        _landedAt = Time.time;
        if (rb) rb.linearVelocity = Vector2.zero;
        if (col) col.isTrigger = true;
        PlayLandedAnim(CurrentDir());
    }

    // -------- helpers anim / dir --------
    int CurrentDir()
    {
        if (rb && rb.linearVelocity.sqrMagnitude > 0.0001f) return DirFromVector(rb.linearVelocity);
        return _dir;
    }

    int DirFromVector(Vector2 v)
    {
        if (Mathf.Abs(v.x) >= Mathf.Abs(v.y)) return v.x >= 0f ? 1 : 2;
        return v.y >= 0f ? 3 : 0;
    }

    void PlayThrownAnim(int d)
    {
        if (!anim) return;
        if (d == 3 || d == 0)
        {
            if (HasState("Vertical_Thrown")) anim.Play("Vertical_Thrown", 0, 0f);
            else anim.Play(d == 3 ? "Up_Thrown" : "Down_Thrown", 0, 0f);
            return;
        }
        anim.Play(d == 1 ? "Right_Thrown" : "Left_Thrown", 0, 0f);
    }

    void PlayLandingAnim(int d)
    {
        if (!anim) return;
        switch (d)
        {
            case 1: anim.Play("Right_Landing", 0, 0f); break;
            case 2: anim.Play("Left_Landing", 0, 0f); break;
            case 3: anim.Play("Up_Landing", 0, 0f); break;
            default: anim.Play("Down_Landing", 0, 0f); break;
        }
    }

    void PlayLandedAnim(int d)
    {
        if (!anim) return;
        switch (d)
        {
            case 1: anim.Play("Right_Landed", 0, 0f); break;
            case 2: anim.Play("Left_Landed", 0, 0f); break;
            case 3: anim.Play("Up_Landed", 0, 0f); break;
            default: anim.Play("Down_Landed", 0, 0f); break;
        }
    }

    bool HasState(string _) { return anim != null; }

    // -------- owner binding --------
    void TryBindOwner(GameObject o)
    {
        if (!destroyWhenOwnerDies || !o) return;

        // si ya está ligado a otro, desuscribe
        UnbindOwner();

        _ownerDmg = o.GetComponent<Damageable>();
        if (_ownerDmg)
        {
            // cachear el handler para poder desuscribir
            _ownerDeathHandler = () => { if (this) Consume(); };
            _ownerDmg.onDeath.AddListener(_ownerDeathHandler);
        }
    }

    void UnbindOwner()
    {
        if (_ownerDmg != null && _ownerDeathHandler != null)
        {
            _ownerDmg.onDeath.RemoveListener(_ownerDeathHandler);
        }
        _ownerDmg = null;
        _ownerDeathHandler = null;
    }
}
