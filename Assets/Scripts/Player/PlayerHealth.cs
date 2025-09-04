using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class PlayerHealth : MonoBehaviour
{
    [Header("Stats")]
    [Min(1)] public int maxHP = 6;
    public int startHP = 6;

    [Header("Daño / iFrames")]
    public bool useInvulnerability = true;
    public float invulnTime = 0.6f;

    [Header("Eventos")]
    public UnityEvent<int, int> OnHPChanged; // (hp, max)
    public UnityEvent OnDamaged;
    public UnityEvent OnHealed;
    public UnityEvent OnDeath;
    public UnityEvent OnRevive;

    public int HP { get; private set; }
    public bool IsDead => HP <= 0;

    float _invulnUntil = -999f;

    void Awake()
    {
        HP = Mathf.Clamp(startHP, 1, maxHP);
        OnHPChanged?.Invoke(HP, maxHP);
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || IsDead) return;
        int before = HP;
        HP = Mathf.Min(HP + amount, maxHP);
        if (HP != before)
        {
            OnHealed?.Invoke();
            OnHPChanged?.Invoke(HP, maxHP);
        }
    }

    public void SetMaxHP(int newMax, bool fill = false)
    {
        maxHP = Mathf.Max(1, newMax);
        if (fill) HP = maxHP;
        HP = Mathf.Min(HP, maxHP);
        OnHPChanged?.Invoke(HP, maxHP);
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || IsDead) return;
        if (useInvulnerability && Time.time < _invulnUntil) return;

        HP = Mathf.Max(HP - amount, 0);
        OnDamaged?.Invoke();
        OnHPChanged?.Invoke(HP, maxHP);

        if (useInvulnerability) _invulnUntil = Time.time + invulnTime;
        if (HP <= 0) OnDeath?.Invoke();
    }

    public void Revive(int restoreHP = -1)
    {
        if (!IsDead) return;
        HP = (restoreHP > 0) ? Mathf.Min(restoreHP, maxHP) : maxHP;
        if (useInvulnerability) _invulnUntil = Time.time + invulnTime;
        OnHPChanged?.Invoke(HP, maxHP);
        OnRevive?.Invoke();
    }

    [ContextMenu("Damage 1")] void CM_Dmg1() => TakeDamage(1);
    [ContextMenu("Heal 1")] void CM_Heal1() => Heal(1);
}
