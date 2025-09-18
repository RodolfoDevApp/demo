using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class ZombieDaggerAI : MonoBehaviour
{
    [Header("Config")]
    public ZombieDaggerConfig config;

    [Header("Componentes")]
    public Rigidbody2D rb;
    public Damageable damageable;
    public ZombieAnimatorDriver animDriver;
    public ContactDamage2D contactDamage; // SIEMPRE activo
    public MeleeHitbox2D meleeHitbox;     // se activa en ventana de AttackA

    [Header("Objetivo")]
    public Transform player;

    [Header("Pathfinding (opcional)")]
    public NavAgent2D nav;

    [Header("Al morir (opcional)")]
    public Collider2D[] collidersToDisable;

    [Header("Return to home al perder vista")]
    public bool returnHomeOnLost = true;
    public float homeStopRadius = 0.15f;

    [Header("Tuning de locomocion")]
    public float standStillBand = 0.12f;

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
    float stuckCheckAt = 0f;

    // home
    bool homeSet = false;
    Vector2 homePos;

    // percepcion / aggro
    bool wasInside = false;
    bool hasAggro = false;
    float aggroUntil = -1f;
    Vector2 lastKnownPlayerPos;
    float reacquireAt = 0f;

    // cooldowns / ataques
    float cdStab = 0f;  // Ataque A
    float cdDash = 0f;  // Ataque B

    bool stabWindowActive = false;
    float stabWindowUntil = -1f;

    bool dashActive = false;
    Vector2 dashDir = Vector2.right;
    float dashUntil = -1f;

    float attackStateTimeoutAt = -1f;

    enum State { Idle, Chase, AttackA, AttackB, ReturnHome, Dead }
    State state = State.Idle;

    static readonly int T_AttackA = Animator.StringToHash("AttackA");
    static readonly int T_AttackB = Animator.StringToHash("AttackB");
    static readonly int T_Die = Animator.StringToHash("Die");

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
        state = State.Idle;

        homePos = rb ? rb.position : (Vector2)transform.position;
        homeSet = true;

        cdStab = 0f;
        cdDash = 0f;

        dashActive = false;
        dashUntil = -1f;

        stabWindowActive = false;
        stabWindowUntil = -1f;

        if (meleeHitbox) meleeHitbox.EndImmediate();

        desiredVel = Vector2.zero;
        curVel = Vector2.zero;
        lastMoveDir = Vector2.right;
        lastPos = rb ? rb.position : (Vector2)transform.position;

        wasInside = false;
        hasAggro = false;
        aggroUntil = -1f;
        attackStateTimeoutAt = -1f;
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

        if (cdStab > 0f) cdStab -= Time.deltaTime;
        if (cdDash > 0f) cdDash -= Time.deltaTime;

        if ((state == State.AttackA || state == State.AttackB) && attackStateTimeoutAt > 0f && Time.time >= attackStateTimeoutAt)
        {
            if (state == State.AttackA) OnAnimAttackAEnd();
            else OnAnimAttackBEnd();
        }

        if (stabWindowActive && Time.time >= stabWindowUntil) EndStabWindow();

        float distToPlayer;
        bool inside = HasPlayerInRange(out distToPlayer);

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

        if (dashActive)
        {
            desiredVel = Vector2.zero;
            return;
        }

        if (!alwaysChasePlayer && !hasAggro && returnHomeOnLost && homeSet)
        {
            float dHome = Vector2.Distance(rb ? rb.position : (Vector2)transform.position, homePos);
            if (dHome > homeStopRadius && state != State.ReturnHome) state = State.ReturnHome;
            else if (dHome <= homeStopRadius && state != State.Idle) state = State.Idle;
        }

        if (hasAggro && inside)
        {
            bool canStab = (cdStab <= 0f && distToPlayer <= config.stabDecisionRange);
            bool canDash = (cdDash <= 0f && distToPlayer >= config.dashDecisionMin && distToPlayer <= config.dashDecisionMax);

            if (canStab)
            {
                state = State.AttackA;
                attackStateTimeoutAt = Time.time + 1.0f;
                StartStabWindow();
                if (animDriver && animDriver.anim) animDriver.anim.SetTrigger(T_AttackA);
            }
            else if (canDash)
            {
                state = State.AttackB;
                attackStateTimeoutAt = Time.time + Mathf.Max(0.26f, config.dashTime + 0.02f);
                StartDashTowardSnapshot();
                if (animDriver && animDriver.anim) animDriver.anim.SetTrigger(T_AttackB);
            }
        }

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
                    else
                    {
                        desiredVel = dirHome * config.moveSpeed;
                    }
                    break;
                }

            case State.Idle:
                desiredVel = Vector2.zero;
                break;

            default: // Chase
                {
                    float mid = Mathf.Min(config.stabDecisionRange, config.dashDecisionMin);
                    float stopMin = Mathf.Max(0.05f, mid - standStillBand);
                    float stopMax = mid + standStillBand;

                    if (inside && distToPlayer >= stopMin && distToPlayer <= stopMax && cdStab > 0f && cdDash > 0f)
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

        if (dashActive)
        {
            float dt = Time.fixedDeltaTime;
            float speed = Mathf.Max(0.1f, config.dashSpeed);
            Vector2 pos = rb ? rb.position : (Vector2)transform.position;
            Vector2 next = pos + dashDir * speed * dt;

            if (rb) rb.MovePosition(next);
            else transform.position = next;

            if (animDriver) animDriver.ReportMotion(dashDir * speed, dashDir);

            if (Time.time >= dashUntil)
            {
                dashActive = false;
                if (state != State.Dead)
                    state = hasAggro ? State.Chase : (!alwaysChasePlayer && returnHomeOnLost ? State.ReturnHome : State.Idle);
            }
            return;
        }

        float dt2 = Time.fixedDeltaTime;
        Vector2 dv = desiredVel - curVel;
        float a = (desiredVel.sqrMagnitude > 0.000001f) ? config.acceleration : config.deceleration;
        Vector2 stepVec = Vector2.ClampMagnitude(dv, a * dt2);
        curVel += stepVec;

        Vector2 pos2 = rb ? rb.position : (Vector2)transform.position;
        Vector2 next2 = pos2 + curVel * dt2;

        if (rb) rb.MovePosition(next2);
        else transform.position = next2;

        if (curVel.sqrMagnitude > 0.000001f) lastMoveDir = curVel.normalized;

        if (animDriver) animDriver.ReportMotion(curVel, lastMoveDir);
    }

    // Attack A (punalada)
    void StartStabWindow()
    {
        if (!meleeHitbox) { cdStab = Mathf.Max(0.05f, config.stabCooldown); return; }

        int d = VectorToDir(DirectionToTarget(lastKnownPlayerPos));
        meleeHitbox.Begin(d);

        stabWindowActive = true;
        stabWindowUntil = Time.time + Mathf.Max(0.05f, config.stabWindowSeconds);
        cdStab = Mathf.Max(0.05f, config.stabCooldown);
    }

    void EndStabWindow()
    {
        if (meleeHitbox) meleeHitbox.End();
        stabWindowActive = false;
        stabWindowUntil = -1f;
    }

    public void OnAnimAttackAHit() { StartStabWindow(); }
    public void OnAnimAttackAEnd()
    {
        attackStateTimeoutAt = -1f;
        EndStabWindow();
        if (state != State.Dead)
            state = hasAggro ? State.Chase : (!alwaysChasePlayer && returnHomeOnLost ? State.ReturnHome : State.Idle);
        hasAggro = true;
        aggroUntil = Time.time + config.aggroHoldSeconds;
    }

    // Attack B (dash)
    void StartDashTowardSnapshot()
    {
        Vector2 pos = rb ? rb.position : (Vector2)transform.position;
        if (player)
        {
            Vector2 to = (Vector2)player.position - pos;
            dashDir = (to.sqrMagnitude > 0.0001f) ? to.normalized :
                      (lastMoveDir.sqrMagnitude > 0.0001f ? lastMoveDir : Vector2.right);
        }
        else
        {
            dashDir = (lastMoveDir.sqrMagnitude > 0.0001f) ? lastMoveDir : Vector2.right;
        }

        dashActive = true;
        dashUntil = Time.time + Mathf.Max(0.05f, config.dashTime);
        cdDash = Mathf.Max(0.05f, config.dashCooldown);
    }

    public void OnAnimAttackBHit() { if (!dashActive) StartDashTowardSnapshot(); }
    public void OnAnimAttackBEnd()
    {
        attackStateTimeoutAt = -1f;
        dashActive = false;
        if (state != State.Dead)
            state = hasAggro ? State.Chase : (!alwaysChasePlayer && returnHomeOnLost ? State.ReturnHome : State.Idle);
        hasAggro = true;
        aggroUntil = Time.time + config.aggroHoldSeconds;
    }

    // muerte
    void OnDeath()
    {
        if (state == State.Dead) return;
        state = State.Dead;

        curVel = Vector2.zero;
        desiredVel = Vector2.zero;
        dashActive = false;
        stabWindowActive = false;

        if (meleeHitbox) meleeHitbox.EndImmediate();
        if (collidersToDisable != null)
            foreach (var c in collidersToDisable) if (c) c.enabled = false;

        if (animDriver) animDriver.PlayDieAndLock();
        Destroy(gameObject, 2f);
    }

    // helpers
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

    Vector2 DirectionToTarget(Vector2 target)
    {
        Vector2 to = target - (Vector2)transform.position;
        if (to.sqrMagnitude < 0.0001f) to = lastMoveDir;
        if (to.sqrMagnitude < 0.0001f) to = Vector2.right;
        return to.normalized;
    }

    static int VectorToDir(Vector2 v)
    {
        if (Mathf.Abs(v.x) > Mathf.Abs(v.y))
            return (v.x >= 0f) ? 1 : 2; // Right / Left
        else
            return (v.y >= 0f) ? 3 : 0; // Up / Down
    }

    void OnDrawGizmosSelected()
    {
        float r = config ? config.detectionRadius : 8f;
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, r);

        if (homeSet)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
            Gizmos.DrawWireSphere(homePos, homeStopRadius);
        }
    }
}
