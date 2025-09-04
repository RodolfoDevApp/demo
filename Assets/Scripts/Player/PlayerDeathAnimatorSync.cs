using UnityEngine;

[DisallowMultipleComponent]
public class PlayerDeathAnimatorSync : MonoBehaviour
{
    [Header("Refs")]
    public PlayerHealth health;
    public Animator bodyAnim;    // Body (tiene isDeath + DeatType + Dir)
    public Animator handsAnim;   // Hands (solo isDeath)
    public Animator weaponAnim;  // Item/Weapon (solo isDeath)

    [Header("Animator Params")]
    public string isDeathParam = "isDeath";   // bool
    public string deathTypeParam = "DeatType";  // int (0..2) — SOLO en Body
    public string dirParam = "Dir";       // int: 0=down,1=right,2=left,3=up (según tu setup)
    public int defaultDeathType = 0;

    int _isDeathHash, _deathTypeHash, _dirHash;
    bool _applied;

    void Reset()
    {
        if (!health) health = GetComponentInParent<PlayerHealth>() ?? GetComponent<PlayerHealth>();

        if (!bodyAnim)
        {
            var t = transform.root.Find("Player/Visual/Body");
            if (t) bodyAnim = t.GetComponent<Animator>();
            if (!bodyAnim) bodyAnim = GetComponentInChildren<Animator>();
        }
        if (!handsAnim)
        {
            var t = transform.root.Find("Player/Visual/Hands");
            if (t) handsAnim = t.GetComponent<Animator>();
        }
        if (!weaponAnim)
        {
            var t = transform.root.Find("Player/Visual/Item");
            if (t) weaponAnim = t.GetComponent<Animator>();
        }
    }

    void Awake()
    {
        _isDeathHash = Animator.StringToHash(isDeathParam);
        _deathTypeHash = Animator.StringToHash(deathTypeParam);
        _dirHash = Animator.StringToHash(dirParam);
        if (!health) health = GetComponentInParent<PlayerHealth>() ?? GetComponent<PlayerHealth>();
    }

    void OnEnable()
    {
        if (health)
        {
            health.OnDeath.AddListener(HandleDeath);
            health.OnRevive.AddListener(HandleRevive);
        }
    }

    void OnDisable()
    {
        if (health)
        {
            health.OnDeath.RemoveListener(HandleDeath);
            health.OnRevive.RemoveListener(HandleRevive);
        }
    }

    void HandleDeath()
    {
        if (_applied) return;
        _applied = true;

        // --- FIX: normalizar Dir antes de activar isDeath ---
        NormalizeDirForDeath(bodyAnim);

        ApplyDeath_Body(bodyAnim);       // Body: isDeath + DeatType (si existe)
        ApplyDeath_Simple(handsAnim);    // Hands: solo isDeath
        ApplyDeath_Simple(weaponAnim);   // Item:  solo isDeath
    }

    void HandleRevive()
    {
        _applied = false;

        ClearDeath(bodyAnim);
        ClearDeath(handsAnim);
        ClearDeath(weaponAnim);
    }

    // ---------- helpers ----------
    static bool HasParam(Animator a, int hash)
    {
        if (!a) return false;
        foreach (var p in a.parameters)
            if (p.nameHash == hash) return true;
        return false;
    }

    void NormalizeDirForDeath(Animator a)
    {
        if (!a) return;
        if (!HasParam(a, _dirHash)) return;

        // Leemos el Dir actual y lo colapsamos a Right(1) o Left(2)
        int dir = a.GetInteger(_dirHash);
        int collapsed = (dir <= 1) ? 1 : 2; // 0/1 -> 1 (right), 2/3 -> 2 (left)
        a.SetInteger(_dirHash, collapsed);
        if (a.isActiveAndEnabled) { a.Update(0f); }
    }

    void ApplyDeath_Body(Animator a)
    {
        if (!a) return;

        if (HasParam(a, _isDeathHash)) a.SetBool(_isDeathHash, true);
        if (HasParam(a, _deathTypeHash)) a.SetInteger(_deathTypeHash, defaultDeathType);

        if (a.isActiveAndEnabled) { a.Rebind(); a.Update(0f); }
    }

    void ApplyDeath_Simple(Animator a)
    {
        if (!a) return;

        if (HasParam(a, _isDeathHash)) a.SetBool(_isDeathHash, true);

        if (a.isActiveAndEnabled) { a.Rebind(); a.Update(0f); }
    }

    void ClearDeath(Animator a)
    {
        if (!a) return;

        if (HasParam(a, _isDeathHash)) a.SetBool(_isDeathHash, false);

        if (a.isActiveAndEnabled) { a.Rebind(); a.Update(0f); }
    }
}
