using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[AddComponentMenu("Combat/Damageable")]
public class Damageable : MonoBehaviour, IDamageable
{
    [Header("Vida")]
    [SerializeField] float maxHP = 30f;

    [Header("Muerte (opcional)")]
    [Tooltip("Si está activo, el objeto se destruirá inmediatamente al morir (no se verá la animación).")]
    [SerializeField] bool destroyOnDeath = false;

    [Tooltip("Si está activo, el objeto se desactiva inmediatamente al morir (no se verá la animación).")]
    [SerializeField] bool deactivateOnDeath = false;

    [Tooltip("> 0 para autodestruir después de N segundos tras morir. Déjalo en 0 si la muerte la controla otro script (AI).")]
    [SerializeField] float autoDespawnSeconds = 0f;

    [Header("I-Frames (invulnerabilidad tras golpe)")]
    [SerializeField] float iFrameTime = 0f;

    [Header("Knockback (opcional)")]
    [Tooltip("Si hay Rigidbody2D, se aplicará AddForce como impulso.")]
    [SerializeField] float knockbackMultiplier = 1f;

    [Header("Eventos")]
    public UnityEvent<float> onHealthChanged;     // vida actual
    public UnityEvent<DamageInfo> onDamaged;      // hit (para VFX/SFX)
    public UnityEvent onDeath;                    // muerte

    float hp;
    float iTimer;
    Rigidbody2D rb2d;

    public bool IsAlive => hp > 0f;
    public float MaxHP => maxHP;
    public float CurrentHP => hp;

    void Awake()
    {
        hp = Mathf.Max(1f, maxHP);
        rb2d = GetComponent<Rigidbody2D>();
        onHealthChanged?.Invoke(hp);
    }

    void Update()
    {
        if (iTimer > 0f) iTimer -= Time.deltaTime;
    }

    public void ApplyDamage(DamageInfo info)
    {
        if (iTimer > 0f || !IsAlive) return;

        // Daño
        hp -= Mathf.Max(0f, info.amount);
        onHealthChanged?.Invoke(hp);
        onDamaged?.Invoke(info);

        // Knockback
        if (rb2d && info.knockback > 0f)
        {
            var dir = info.dir.sqrMagnitude > 0.0001f ? info.dir.normalized : Vector2.zero;
            rb2d.AddForce(dir * (info.knockback * knockbackMultiplier), ForceMode2D.Impulse);
        }

        // I-frames
        if (iFrameTime > 0f) iTimer = iFrameTime;

        // Muerte
        if (hp <= 0f)
        {
            hp = 0f;
            onHealthChanged?.Invoke(hp);  // notificar 0 por si UI lo necesita
            onDeath?.Invoke();            // los AIs escuchan esto y reproducen anim + despawn

            // Comportamiento opcional (por si NO hay AI controlando la muerte):
            if (autoDespawnSeconds > 0f)
            {
                StartCoroutine(DespawnAfter(autoDespawnSeconds));
            }
            else if (destroyOnDeath)
            {
                Destroy(gameObject);
            }
            else if (deactivateOnDeath)
            {
                gameObject.SetActive(false);
            }
            // Si ninguno está activo, no hacemos nada aquí: la animación se ve y el AI destruye a los 2s.
        }
    }

    IEnumerator DespawnAfter(float seconds)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, seconds));
        if (this) Destroy(gameObject);
    }

    public void Heal(float amount)
    {
        if (!IsAlive || amount <= 0f) return;
        hp = Mathf.Min(maxHP, hp + amount);
        onHealthChanged?.Invoke(hp);
    }

    public void Revive(float newHP = -1f)
    {
        hp = (newHP > 0f) ? Mathf.Min(newHP, maxHP) : maxHP;
        iTimer = 0f;
        gameObject.SetActive(true);
        onHealthChanged?.Invoke(hp);
    }

    void OnValidate()
    {
        if (maxHP < 1f) maxHP = 1f;
        if (hp > maxHP) hp = maxHP;
        if (autoDespawnSeconds < 0f) autoDespawnSeconds = 0f;
    }
}
