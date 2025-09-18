using UnityEngine;

[DisallowMultipleComponent]
public class PlayerDeathAnimatorSync : MonoBehaviour
{
    [Header("Refs")]
    public PlayerHealth health;
    public Animator bodyAnim;    // Player/Visual/Body (isDeath, DeatType, Dir)
    public Animator handsAnim;   // Player/Visual/Hands (isDeath)
    public Animator weaponAnim;  // Player/Visual/Item  (isDeath)

    [Header("Afectar a:")]
    public bool affectBody = false;   // deja false si DeathFlowCoordinator ya controla el body
    public bool affectHands = true;
    public bool affectWeapon = true;

    [Header("Animator params")]
    public string isDeathParam = "isDeath";     // bool
    public string deathTypeParam = "DeatType";  // int solo en body
    public string dirParam = "Dir";             // 0=down,1=right,2=left,3=up
    public int defaultDeathType = 0;

    int _isDeathHash, _deathTypeHash, _dirHash;

    void Reset()
    {
        health ??= GetComponentInParent<PlayerHealth>() ?? GetComponent<PlayerHealth>();

        if (!bodyAnim)
        {
            var t = transform.root ? transform.root.Find("Player/Visual/Body") : null;
            if (t) bodyAnim = t.GetComponent<Animator>();
            if (!bodyAnim) bodyAnim = GetComponentInChildren<Animator>(true);
        }
        if (!handsAnim)
        {
            var t = transform.root ? transform.root.Find("Player/Visual/Hands") : null;
            if (t) handsAnim = t.GetComponent<Animator>();
        }
        if (!weaponAnim)
        {
            var t = transform.root ? transform.root.Find("Player/Visual/Item") : null;
            if (t) weaponAnim = t.GetComponent<Animator>();
        }
    }

    void Awake()
    {
        _isDeathHash = Animator.StringToHash(string.IsNullOrEmpty(isDeathParam) ? "isDeath" : isDeathParam);
        _deathTypeHash = Animator.StringToHash(string.IsNullOrEmpty(deathTypeParam) ? "DeatType" : deathTypeParam);
        _dirHash = Animator.StringToHash(string.IsNullOrEmpty(dirParam) ? "Dir" : dirParam);

        health ??= GetComponentInParent<PlayerHealth>() ?? GetComponent<PlayerHealth>();
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
        // body
        if (affectBody) ApplyDeath_Body(bodyAnim);

        // manos
        if (affectHands) ApplyDeath_Simple(handsAnim);

        // arma
        if (affectWeapon) ApplyDeath_Simple(weaponAnim);
    }

    void HandleRevive()
    {
        if (affectBody) ClearDeath(bodyAnim);
        if (affectHands) ClearDeath(handsAnim);
        if (affectWeapon) ClearDeath(weaponAnim);
    }

    // helpers
    static bool HasParam(Animator a, int hash, AnimatorControllerParameterType type)
    {
        if (!a) return false;
        var ps = a.parameters;
        for (int i = 0; i < ps.Length; i++)
            if (ps[i].nameHash == hash && ps[i].type == type) return true;
        return false;
    }

    void NormalizeDirForDeath(Animator a)
    {
        if (!a || !HasParam(a, _dirHash, AnimatorControllerParameterType.Int)) return;
        int dir = a.GetInteger(_dirHash);
        // colapsa up/down a una lateral para reuse de clips
        int collapsed = (dir == 1 || dir == 2) ? dir : 2; // usa izquierda por defecto
        a.SetInteger(_dirHash, collapsed);
    }

    void ApplyDeath_Body(Animator a)
    {
        if (!a) return;

        NormalizeDirForDeath(a);

        if (HasParam(a, _isDeathHash, AnimatorControllerParameterType.Bool))
            a.SetBool(_isDeathHash, true);

        if (HasParam(a, _deathTypeHash, AnimatorControllerParameterType.Int))
            a.SetInteger(_deathTypeHash, defaultDeathType);

        // no usar Animator.Update
        if (a.isActiveAndEnabled) a.Rebind(); // opcional para reset de layers
    }

    void ApplyDeath_Simple(Animator a)
    {
        if (!a) return;
        if (HasParam(a, _isDeathHash, AnimatorControllerParameterType.Bool))
            a.SetBool(_isDeathHash, true);
        if (a.isActiveAndEnabled) a.Rebind(); // opcional
    }

    void ClearDeath(Animator a)
    {
        if (!a) return;
        if (HasParam(a, _isDeathHash, AnimatorControllerParameterType.Bool))
            a.SetBool(_isDeathHash, false);
        if (a.isActiveAndEnabled) a.Rebind();
    }
}
