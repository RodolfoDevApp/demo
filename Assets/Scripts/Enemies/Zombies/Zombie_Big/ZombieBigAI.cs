using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class ZombieBigAI : MonoBehaviour
{
    [Header("Config")]
    public ZombieBigConfig config;

    [Header("Componentes")]
    public Rigidbody2D rb;
    public Damageable damageable;
    public ZombieAnimatorDriver animDriver;
    public ContactDamage2D contactDamage;   // Attack A por contacto (activo siempre)

    [Header("Objetivo")]
    public Transform player;

    [Header("Pathfinding (opcional)")]
    public NavAgent2D nav;

    [Header("Return Home")]
    public bool returnHomeOnLost = true;
    public float homeStopRadius = 0.2f;
    public float standStillBand = 0.12f;

    [Header("Colliders a deshabilitar al morir (opcional)")]
    public Collider2D[] collidersToDisable;

    [Header("Override persecucion")]
    public bool alwaysChasePlayer = true;
    public float forceAggroSeconds = 999f;

    [Header("Muerte / Despawn")]
    public float deathDespawnDelay = 2f;
    bool _despawnScheduled = false;
    IEnumerator DespawnAfterDelay()
    {
        _despawnScheduled = true;
        yield return new WaitForSeconds(Mathf.Max(0f, deathDespawnDelay));
        if (this) Destroy(gameObject);
    }

    // movimiento
    Vector2 desiredVel = Vector2.zero;
    Vector2 curVel = Vector2.zero;
    Vector2 lastMoveDir = Vector2.right;
    Vector2 lastPos;
    float stuckCheckAt;

    // home
    bool homeSet;
    Vector2 homePos;

    // aggro
    bool wasInside;
    bool hasAggro;
    float aggroUntil;
    Vector2 lastKnownPlayerPos;
    float reacquireAt;

    // cds
    float cdA, cdB;
    float attackStateTimeoutAt = -1f;

    // slam
    bool slamScheduled;
    float slamAutoAt;
    bool slamDone;
    int lastAttackDir4 = 1; // 0=D,1=R,2=L,3=U

    enum State { Idle, Chase, AttackA, AttackB, ReturnHome, Dead }
    State state = State.Idle;

    static readonly int T_AttackA = Animator.StringToHash("AttackA");
    static readonly int T_AttackB = Animator.StringToHash("AttackB");
    static readonly int T_Die = Animator.StringToHash("Die");

    static readonly Collider2D[] slamHits = new Collider2D[8];

    void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
        damageable = GetComponent<Damageable>();
        animDriver = GetComponentInChildren<ZombieAnimatorDriver>();
        contactDamage = GetComponent<ContactDamage2D>();
        if (!nav) nav = GetComponent<NavAgent2D>();
    }

    void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!damageable) damageable = GetComponent<Damageable>();
        if (!animDriver) animDriver = GetComponentInChildren<ZombieAnimatorDriver>();
        if (!contactDamage) contactDamage = GetComponent<ContactDamage2D>();
        if (!nav) nav = GetComponent<NavAgent2D>();

        EnsurePlayer(true);
        if (damageable) damageable.onDeath.AddListener(OnDeath);
    }

    void OnEnable()
    {
        EnsureRootContactTriggerAndEnable();

        homePos = rb ? rb.position : (Vector2)transform.position;
        homeSet = true;

        state = State.Idle;
        desiredVel = curVel = Vector2.zero;
        lastMoveDir = Vector2.right;
        lastPos = rb ? rb.position : (Vector2)transform.position;
        stuckCheckAt = 0f;

        wasInside = hasAggro = false;
        aggroUntil = -1f;
        attackStateTimeoutAt = -1f;

        cdA = cdB = 0f;

        slamScheduled = slamDone = false;
        slamAutoAt = -1f;
        lastAttackDir4 = 1;
    }

    void Update()
    {
        if (state == State.Dead) return;

        EnsurePlayer(false);

        if (alwaysChasePlayer && player)
        {
            hasAggro = true;
            aggroUntil = Time.time + forceAggroSeconds;
            lastKnownPlayerPos = player.position;
        }

        if (cdA > 0f) cdA -= Time.deltaTime;
        if (cdB > 0f) cdB -= Time.deltaTime;

        if ((state == State.AttackA || state == State.AttackB) &&
            attackStateTimeoutAt > 0f && Time.time >= attackStateTimeoutAt)
        {
            if (state == State.AttackA) OnAnimAttackAEnd();
            else OnAnimAttackBEnd();
        }

        float dist;
        bool inside = HasPlayerInRange(out dist);

        if (!wasInside && inside)
        {
            wasInside = true;
            hasAggro = true;
            aggroUntil = Time.time + config.aggroHoldSeconds;
            if (player) lastKnownPlayerPos = player.position;
        }
        else if (wasInside && !inside)
        {
            wasInside = false;
        }

        if (inside)
        {
            hasAggro = true;
            aggroUntil = Time.time + config.aggroHoldSeconds;
            if (player) lastKnownPlayerPos = player.position;
        }
        else
        {
            hasAggro = (Time.time <= aggroUntil);
        }

        if (state == State.AttackB && slamScheduled && !slamDone && Time.time >= slamAutoAt)
            DoGroundSlam();

        switch (state)
        {
            case State.Dead:
            case State.AttackA:
            case State.AttackB:
                desiredVel = Vector2.zero;
                break;

            case State.ReturnHome:
                {
                    if (hasAggro)
                    {
                        state = State.Chase;
                        desiredVel = Vector2.zero;
                        break;
                    }
                    Vector2 pos = rb ? rb.position : (Vector2)transform.position;
                    Vector2 dirHome = nav ? nav.GetDirection(pos, homePos)
                                          : ((homePos - pos).sqrMagnitude > 0.0001f ? (homePos - pos).normalized : lastMoveDir);
                    float dHome = Vector2.Distance(pos, homePos);
                    if (dHome <= homeStopRadius)
                    {
                        desiredVel = Vector2.zero;
                        state = State.Idle;
                    }
                    else desiredVel = dirHome * config.moveSpeed;
                    break;
                }

            case State.Idle:
                desiredVel = Vector2.zero;
                if (hasAggro) state = State.Chase;
                break;

            default: // Chase
                {
                    if (hasAggro && inside)
                    {
                        bool canA = (cdA <= 0f && dist <= config.attackARange);
                        bool canB = (cdB <= 0f && dist >= config.slamDecisionMin && dist <= config.slamDecisionMax);

                        if (canA)
                        {
                            state = State.AttackA;
                            attackStateTimeoutAt = Time.time + 1.0f;
                            cdA = Mathf.Max(0.05f, config.attackACooldown);
                            if (animDriver && animDriver.anim) animDriver.anim.SetTrigger(T_AttackA);
                            desiredVel = Vector2.zero;
                            break;
                        }
                        if (canB)
                        {
                            state = State.AttackB;
                            attackStateTimeoutAt = Time.time + 1.1f;
                            cdB = Mathf.Max(0.05f, config.slamCooldown);

                            lastAttackDir4 = VectorToDir(DirectionToTarget(lastKnownPlayerPos));

                            slamScheduled = true;
                            slamDone = false;
                            slamAutoAt = Time.time + Mathf.Max(0.02f, config.slamAutoHitDelay);

                            if (animDriver && animDriver.anim) animDriver.anim.SetTrigger(T_AttackB);
                            desiredVel = Vector2.zero;
                            break;
                        }
                    }

                    float stopMin = Mathf.Max(0.05f, config.attackARange - standStillBand);
                    float stopMax = config.attackARange + standStillBand;
                    if (inside && dist >= stopMin && dist <= stopMax && cdA > 0f && cdB > 0f)
                    {
                        desiredVel = Vector2.zero;
                    }
                    else
                    {
                        Vector2 target = hasAggro ? lastKnownPlayerPos : (Vector2)transform.position;
                        Vector2 dirMove = nav ? nav.GetDirection((Vector2)transform.position, target)
                                              : ((target - (Vector2)transform.position).sqrMagnitude > 0.0001f
                                                  ? (target - (Vector2)transform.position).normalized
                                                  : lastMoveDir);
                        desiredVel = dirMove * config.moveSpeed;
                    }

                    if (desiredVel.sqrMagnitude > 0.000001f) lastMoveDir = desiredVel.normalized;

                    if (!alwaysChasePlayer && !hasAggro && returnHomeOnLost && homeSet)
                        state = State.ReturnHome;
                    break;
                }
        }

        if (Time.time >= stuckCheckAt)
        {
            stuckCheckAt = Time.time + 0.5f;
            float moved = Vector2.Distance(rb ? rb.position : (Vector2)transform.position, lastPos);
            lastPos = rb ? rb.position : (Vector2)transform.position;
            if ((state == State.Chase || state == State.ReturnHome) && moved < 0.01f)
            {
                Vector2 target = (state == State.ReturnHome && !alwaysChasePlayer) ? homePos :
                                 (player ? (Vector2)player.position : lastKnownPlayerPos);
                Vector2 dir = nav ? nav.GetDirection((Vector2)transform.position, target)
                                  : ((target - (Vector2)transform.position).sqrMagnitude > 0.0001f
                                      ? (target - (Vector2)transform.position).normalized
                                      : lastMoveDir);
                desiredVel = dir * config.moveSpeed;
            }
        }
    }

    void FixedUpdate()
    {
        if (state == State.Dead) return;

        float dt = Time.fixedDeltaTime;
        Vector2 dv = desiredVel - curVel;
        float a = (desiredVel.sqrMagnitude > 0.000001f) ? config.acceleration : config.deceleration;
        Vector2 step = Vector2.ClampMagnitude(dv, a * dt);
        curVel += step;

        Vector2 pos = rb ? rb.position : (Vector2)transform.position;
        Vector2 next = pos + curVel * dt;

        if (rb) rb.MovePosition(next);
        else transform.position = next;

        if (curVel.sqrMagnitude > 0.000001f) lastMoveDir = curVel.normalized;
    }

    // Attack A: contacto continuo via ContactDamage2D
    public void OnAnimAttackAHit()
    {
        hasAggro = true;
        aggroUntil = Time.time + config.aggroHoldSeconds;
    }

    public void OnAnimAttackAEnd()
    {
        attackStateTimeoutAt = -1f;
        if (state != State.Dead)
            state = hasAggro ? State.Chase : (!alwaysChasePlayer && returnHomeOnLost ? State.ReturnHome : State.Idle);
        hasAggro = true;
        aggroUntil = Time.time + config.aggroHoldSeconds;
    }

    // Attack B: ground slam
    public void OnAnimAttackBHit()
    {
        if (!slamDone) DoGroundSlam();
    }

    public void OnAnimAttackBEnd()
    {
        attackStateTimeoutAt = -1f;
        slamScheduled = false;
        slamDone = false;
        if (state != State.Dead)
            state = hasAggro ? State.Chase : (!alwaysChasePlayer && returnHomeOnLost ? State.ReturnHome : State.Idle);
        hasAggro = true;
        aggroUntil = Time.time + config.aggroHoldSeconds;
    }

    void DoGroundSlam()
    {
        slamDone = true;

        Vector2 center = (Vector2)transform.position + GetSlamOffsetByDir(lastAttackDir4);

        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = true;
        filter.SetLayerMask(config.slamMask);

        int n = Physics2D.OverlapCircle(center, config.slamRadius, filter, slamHits);

        for (int i = 0; i < n; i++)
        {
            var c = slamHits[i];
            if (!c || !c.gameObject.activeInHierarchy) continue;

            var ph = c.GetComponentInParent<PlayerHealth>();
            Vector2 hp = c.bounds.ClosestPoint(center);
            Vector2 pushDir = (hp - center);
            if (pushDir.sqrMagnitude < 0.0001f) pushDir = DirIndexToVector(lastAttackDir4);
            pushDir.Normalize();

            if (ph)
            {
                ph.TakeDamage(Mathf.RoundToInt(Mathf.Max(1f, config.slamDamage)));
                continue;
            }

            var dmg = c.GetComponentInParent<IDamageable>();
            if (dmg != null)
            {
                var info = new DamageInfo(config.slamDamage, pushDir, hp, DamageKind.Explosion, config.slamKnockback, gameObject, gameObject);
                dmg.ApplyDamage(info);
            }
        }
    }

    Vector2 GetSlamOffsetByDir(int dir4)
    {
        switch (dir4)
        {
            case 0: return config.slamOffsetDown;
            case 1: return config.slamOffsetRight;
            case 2: return config.slamOffsetLeft;
            case 3: return config.slamOffsetUp;
            default: return config.slamOffsetRight;
        }
    }

    static int VectorToDir(Vector2 v)
    {
        if (Mathf.Abs(v.x) >= Mathf.Abs(v.y)) return (v.x >= 0f) ? 1 : 2; // R/L
        return (v.y >= 0f) ? 3 : 0; // U/D
    }

    static Vector2 DirIndexToVector(int d)
    {
        switch (d)
        {
            case 0: return Vector2.down;
            case 1: return Vector2.right;
            case 2: return Vector2.left;
            case 3: return Vector2.up;
            default: return Vector2.right;
        }
    }

    Vector2 DirectionToTarget(Vector2 target)
    {
        Vector2 to = target - (Vector2)transform.position;
        if (to.sqrMagnitude < 0.0001f) to = lastMoveDir.sqrMagnitude > 0.0001f ? lastMoveDir : Vector2.right;
        return to.normalized;
    }

    void EnsureRootContactTriggerAndEnable()
    {
        if (!contactDamage) return;

        if (contactDamage.damageTrigger && contactDamage.damageTrigger.gameObject == gameObject)
        {
            contactDamage.damageTrigger.isTrigger = true;
            contactDamage.damageTrigger.enabled = true;
        }
        else
        {
            var rootCol = GetComponent<Collider2D>();
            if (!rootCol)
                rootCol = gameObject.AddComponent<BoxCollider2D>();
            rootCol.isTrigger = true;
            rootCol.enabled = true;
            contactDamage.damageTrigger = rootCol;
        }

        contactDamage.enabled = true;
    }

    void OnDeath()
    {
        if (state == State.Dead) return;
        state = State.Dead;

        desiredVel = Vector2.zero;
        curVel = Vector2.zero;

        if (collidersToDisable != null)
            foreach (var col in collidersToDisable) if (col) col.enabled = false;

        if (animDriver) animDriver.PlayDieAndLock();
        Destroy(gameObject, 2f);
    }

    bool HasPlayerInRange(out float dist)
    {
        dist = Mathf.Infinity;
        if (!player) return false;
        float r = Mathf.Max(0.2f, config.detectionRadius);
        dist = Vector2.Distance(transform.position, player.position);
        if (dist <= r)
        {
            lastKnownPlayerPos = player.position;
            return true;
        }
        return false;
    }

    void EnsurePlayer(bool immediate)
    {
        if (player && player.gameObject.activeInHierarchy) return;
        if (!immediate && Time.time < reacquireAt) return;
        reacquireAt = Time.time + 0.25f;

        var go = GameObject.FindGameObjectWithTag("Player");
        if (go) { player = go.transform; return; }

        var ph = FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
        if (ph) { player = ph.transform; return; }
    }

    void OnDrawGizmosSelected()
    {
        int dir4 = lastAttackDir4;
        if (dir4 < 0 || dir4 > 3)
            dir4 = VectorToDir(lastMoveDir);

        Vector2 center = (Vector2)transform.position + GetSlamOffsetByDir(dir4);
        float r = (config ? config.slamRadius : 1.6f);

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
        Gizmos.DrawWireSphere(center, r);
    }
}
