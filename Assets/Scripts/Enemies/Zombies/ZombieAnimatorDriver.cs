using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class ZombieAnimatorDriver : MonoBehaviour
{
    public Animator anim;
    public Rigidbody2D rb;

    // Animator params
    static readonly int P_Dir = Animator.StringToHash("Dir");    // 0=Down, 1=Right, 2=Left, 3=Up
    static readonly int P_Speed = Animator.StringToHash("Speed");
    static readonly int P_Dead = Animator.StringToHash("Dead");   // opcional

    // Triggers (opcionales, ya no asumimos que existan)
    static readonly int T_Attack = Animator.StringToHash("Attack");
    static readonly int T_AttackA = Animator.StringToHash("AttackA");
    static readonly int T_AttackB = Animator.StringToHash("AttackB");
    static readonly int T_Die = Animator.StringToHash("Die");

    [Header("Smoothing")]
    public float speedLerp = 14f;
    public float minSpeedForDir = 0.02f;

    // external motion feed (from AI)
    Vector2 _extVel;
    Vector2 _extFacing;
    int _extFrame = -1000;

    // fallback position-based velocity
    Vector2 _lastPos;
    bool _haveLast;

    // thresholds to avoid dirty animator every frame
    float _lastSetSpeed = -999f;
    int _lastSetDir = -999;
    const float SPEED_EPS = 0.001f;

    // bloqueo de animacion al morir
    bool _deadLocked = false;

    void Reset()
    {
        anim = GetComponent<Animator>();
        rb = GetComponentInParent<Rigidbody2D>() ?? GetComponent<Rigidbody2D>();
    }

    void Awake()
    {
        if (!anim) anim = GetComponent<Animator>();
        if (!rb) rb = GetComponentInParent<Rigidbody2D>() ?? GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        _haveLast = false;
        _lastSetSpeed = -999f;
        _lastSetDir = -999;
        // _deadLocked se mantiene; si se "revive", tu flujo debe crear/activar otro prefab o limpiar este flag
    }

    // El AI llama esto por tick
    public void ReportMotion(Vector2 velocityLike, Vector2 facingDir)
    {
        if (_deadLocked) return;
        _extVel = velocityLike;
        _extFacing = facingDir.sqrMagnitude > 0.0001f ? facingDir : Vector2.right;
        _extFrame = Time.frameCount;
    }

    void Update()
    {
        if (_deadLocked)
        {
            if (anim && HasParam(anim, P_Dead)) anim.SetBool(P_Dead, true);
            if (anim) anim.SetFloat(P_Speed, 0f);
            return;
        }

        Vector2 pos = rb ? rb.position : (Vector2)transform.position;
        if (!_haveLast) { _lastPos = pos; _haveLast = true; }

        bool useExt = (_extFrame == Time.frameCount);
        Vector2 vel = useExt ? _extVel : ComputeVelFromDelta(pos);
        Vector2 facing = useExt
            ? (_extVel.sqrMagnitude > 0.0001f ? _extVel : _extFacing)
            : vel;

        float instSpeed = vel.magnitude;
        float displaySpeed = SmoothSpeed(instSpeed);

        if (Mathf.Abs(displaySpeed - _lastSetSpeed) > SPEED_EPS)
        {
            anim.SetFloat(P_Speed, displaySpeed);
            _lastSetSpeed = displaySpeed;
        }

        if (instSpeed > minSpeedForDir)
        {
            int dir = ComputeDir(facing);
            if (dir != _lastSetDir)
            {
                anim.SetInteger(P_Dir, dir);
                _lastSetDir = dir;
            }
        }

        _lastPos = pos;
    }

    Vector2 ComputeVelFromDelta(Vector2 posNow)
    {
        Vector2 delta = posNow - _lastPos;
        float dt = Time.deltaTime;
        return (dt > 0f) ? (delta / dt) : Vector2.zero;
    }

    float SmoothSpeed(float v)
    {
        float k = 1f - Mathf.Exp(-speedLerp * Time.deltaTime);
        return Mathf.Lerp(_lastSetSpeed < -900f ? v : _lastSetSpeed, v, k);
    }

    static int ComputeDir(Vector2 v)
    {
        if (Mathf.Abs(v.x) >= Mathf.Abs(v.y)) return (v.x >= 0f) ? 1 : 2; // right / left
        return (v.y >= 0f) ? 3 : 0; // up / down
    }

    static bool HasParam(Animator a, int hash)
    {
        if (!a) return false;
        var ps = a.parameters;
        for (int i = 0; i < ps.Length; i++)
            if (ps[i].nameHash == hash) return true;
        return false;
    }

    static bool HasTrigger(Animator a, int hash)
    {
        if (!a) return false;
        var ps = a.parameters;
        for (int i = 0; i < ps.Length; i++)
            if (ps[i].nameHash == hash && ps[i].type == AnimatorControllerParameterType.Trigger)
                return true;
        return false;
    }

    static void SafeSetTrigger(Animator a, int hash)
    {
        if (HasTrigger(a, hash)) a.SetTrigger(hash);
    }

    static void SafeResetTrigger(Animator a, int hash)
    {
        if (HasTrigger(a, hash)) a.ResetTrigger(hash);
    }

    // ---- API publica para muerte ----
    public void PlayDie()
    {
        if (!anim) return;
        // Solo dispara si el trigger existe. Si no existe, no hay warning.
        SafeSetTrigger(anim, T_Die);
    }

    /// <summary>
    /// Dispara animacion de muerte y bloquea locomocion.
    /// Si no hay trigger Die, usa Dead=true y Speed=0 como fallback.
    /// </summary>
    public void PlayDieAndLock()
    {
        if (!anim) { _deadLocked = true; return; }

        // Limpia triggers de ataque solo si existen
        SafeResetTrigger(anim, T_Attack);
        SafeResetTrigger(anim, T_AttackA);
        SafeResetTrigger(anim, T_AttackB);

        // Marca Dead si existe
        if (HasParam(anim, P_Dead)) anim.SetBool(P_Dead, true);

        // Intenta trigger Die; si no existe, no genera warning
        SafeSetTrigger(anim, T_Die);

        // Congela speed en cualquier caso
        anim.SetFloat(P_Speed, 0f);

        _deadLocked = true;
    }
}
