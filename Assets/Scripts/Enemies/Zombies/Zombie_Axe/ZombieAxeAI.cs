using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class ZombieAxeAI : MonoBehaviour
{
    [Header("Config")]
    public ZombieConfig config;

    [Header("Componentes")]
    public Rigidbody2D rb;
    public Damageable damageable;
    public ZombieAnimatorDriver animDriver;
    public ZombieLootDropper loot;
    public ContactDamage2D contactDamage;

    [Header("Target")]
    public Transform player;

    [Header("Pathfinding (opcional)")]
    public NavAgent2D nav;

    [Header("Estados extra")]
    public bool hasAxe = true;
    public float retrieveSearchRadius = 8f;
    public float takeAxeRange = 0.6f;

    [Header("Dash / Dodge")]
    public float dashDamageLinger = 0.10f;

    [Header("Contact Damage")]
    public bool manageContactDamage = false;

    [Header("Colliders a deshabilitar al morir")]
    public Collider2D[] collidersToDisable;

    [Header("Tuning de quieto")]
    public float standStillBand = 0.12f;

    [Header("Regresar a casa al perder de vista")]
    public bool returnHomeOnLost = true;
    public float homeStopRadius = 0.15f;

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

    AxeProjectile2D myAxe;

    Vector2 desiredVel = Vector2.zero;
    Vector2 curVel = Vector2.zero;
    Vector2 lastMoveDir = Vector2.right;
    Vector2 lastPos;
    float stuckCheckAt = 0f;

    float cdA, cdB;
    float attackStateTimeoutAt = -1f;

    bool hasAggro = false;
    bool sawPlayer = false;
    float aggroUntil = -1f;
    Vector2 lastKnownPlayerPos;
    float reAcquireAt = 0f;

    bool wasInsideDetection = false;

    bool dashActive = false;
    Vector2 dashDir = Vector2.right;
    float dashUntil = -1f;
    float contactDisableAt = -1f;

    bool homeSet = false;
    Vector2 homePos;

    bool attackBArmed = false;
    int attackBShotsInState = 0;

    enum State { Idle, Chase, AttackA, AttackB, NoAxeChase, TakeAxe, ReturnHome, Dead }
    State state = State.Idle;

    static readonly int P_HasAxe = Animator.StringToHash("HasAxe");
    static readonly int T_AttackA = Animator.StringToHash("AttackA");
    static readonly int T_AttackB = Animator.StringToHash("AttackB");
    static readonly int T_TakeAxe = Animator.StringToHash("TakeAxe");
    static readonly int T_Die = Animator.StringToHash("Die");

    void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
        damageable = GetComponent<Damageable>();
        animDriver = GetComponentInChildren<ZombieAnimatorDriver>();
        loot = GetComponent<ZombieLootDropper>();
        contactDamage = GetComponent<ContactDamage2D>();
        if (!nav) nav = GetComponent<NavAgent2D>();
    }

    void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!damageable) damageable = GetComponent<Damageable>();
        if (!animDriver) animDriver = GetComponentInChildren<ZombieAnimatorDriver>();
        if (!loot) loot = GetComponent<ZombieLootDropper>();
        if (!contactDamage) contactDamage = GetComponent<ContactDamage2D>();
        if (!nav) nav = GetComponent<NavAgent2D>();

        EnsurePlayer(true);

        if (damageable) damageable.onDeath.AddListener(OnDeath);
    }

    void OnEnable()
    {
        homePos = rb ? rb.position : (Vector2)transform.position;
        homeSet = true;

        state = State.Idle;
        cdA = cdB = 0f;
        attackStateTimeoutAt = -1f;

        hasAggro = false;
        sawPlayer = false;
        aggroUntil = -1f;

        wasInsideDetection = false;
        desiredVel = Vector2.zero;
        curVel = Vector2.zero;
        lastMoveDir = Vector2.right;
        lastPos = rb ? rb.position : (Vector2)transform.position;
        lastKnownPlayerPos = (Vector2)transform.position;

        dashActive = false;
        dashDir = Vector2.right;
        dashUntil = -1f;
        contactDisableAt = -1f;

        attackBArmed = false;
        attackBShotsInState = 0;

        if (contactDamage && manageContactDamage)
            contactDamage.enabled = false;

        ApplyHasAxeParam();
    }

    void Update()
    {
        if (state == State.Dead) return;

        EnsurePlayer(false);

        // Forzar aggro continuo
        if (alwaysChasePlayer && player)
        {
            hasAggro = true;
            sawPlayer = true;
            aggroUntil = Time.time + forceAggroSeconds;
            lastKnownPlayerPos = player.position;
        }

        if (cdA > 0f) cdA -= Time.deltaTime;
        if (cdB > 0f) cdB -= Time.deltaTime;

        if ((state == State.AttackA || state == State.AttackB || state == State.TakeAxe)
            && attackStateTimeoutAt > 0f && Time.time >= attackStateTimeoutAt)
        {
            if (state == State.AttackA) OnAnimAttackAEnd();
            else if (state == State.AttackB) OnAnimAttackBEnd();
            else OnAnimTakeAxe();
        }

        if (contactDamage && manageContactDamage)
        {
            bool want = dashActive || (contactDisableAt > 0f && Time.time < contactDisableAt);
            if (contactDamage.enabled != want) contactDamage.enabled = want;
        }

        float distToPlayer;
        bool inside = HasPlayerInRange(out distToPlayer);

        if (!wasInsideDetection && inside)
        {
            wasInsideDetection = true;
            sawPlayer = true;
            hasAggro = true;
            aggroUntil = Time.time + config.aggroHoldSeconds;
            if (player) lastKnownPlayerPos = player.position;
            TryImmediateThrowOnEnter(distToPlayer);
        }
        else if (wasInsideDetection && !inside)
        {
            wasInsideDetection = false;
        }

        if (inside)
        {
            hasAggro = true;
            aggroUntil = Time.time + config.aggroHoldSeconds;
            if (player) lastKnownPlayerPos = player.position;
        }
        else
        {
            hasAggro = sawPlayer && (Time.time <= aggroUntil);
        }

        float meleeR = config.attackRangeA;
        float minThrow = Mathf.Max(config.throwMinDistance, meleeR + 0.15f);

        if (dashActive)
        {
            desiredVel = Vector2.zero;
            return;
        }

        if (!hasAggro && !hasAxe)
        {
            if (!myAxe) myAxe = FindMyLandedAxe();
            if (myAxe && myAxe.IsPickup) state = State.NoAxeChase;
        }

        if (!alwaysChasePlayer && !hasAggro && hasAxe && returnHomeOnLost && homeSet)
        {
            float distHome = Vector2.Distance(rb ? rb.position : (Vector2)transform.position, homePos);
            if (distHome > homeStopRadius && state != State.ReturnHome)
            {
                state = State.ReturnHome;
            }
            else if (distHome <= homeStopRadius && state != State.Idle)
            {
                state = State.Idle;
            }
        }

        switch (state)
        {
            case State.Idle:
                {
                    desiredVel = Vector2.zero;
                    if (!hasAggro) break;

                    float d = DistanceToPlayerSafe();

                    bool canThrow = inside && hasAxe && cdB <= 0f && d >= minThrow &&
                                    (config.throwMaxDistance <= 0f || d <= config.throwMaxDistance);
                    bool canDash = inside && cdA <= 0f && d <= (meleeR + 0.25f);

                    if (canThrow)
                    {
                        state = State.AttackB;
                        attackStateTimeoutAt = Time.time + 1.2f;
                        TriggerAttackB();
                    }
                    else if (canDash)
                    {
                        state = State.AttackA;
                        attackStateTimeoutAt = Time.time + 1.0f;
                        TriggerAttackA();
                    }
                    else
                    {
                        state = hasAxe ? State.Chase : State.NoAxeChase;
                    }
                    break;
                }

            case State.Chase:
                {
                    if (!hasAggro)
                    {
                        desiredVel = Vector2.zero;

                        if (!hasAxe)
                        {
                            if (!myAxe) myAxe = FindMyLandedAxe();
                            if (myAxe && myAxe.IsPickup) { state = State.NoAxeChase; break; }
                        }

                        state = (!alwaysChasePlayer && returnHomeOnLost && homeSet && hasAxe)
                                ? State.ReturnHome : State.Idle;
                        break;
                    }

                    float d = inside && player ? distToPlayer
                                               : Vector2.Distance(transform.position, lastKnownPlayerPos);

                    bool shouldThrow = inside && hasAxe && cdB <= 0f && d >= minThrow &&
                                       (config.throwMaxDistance <= 0f || d <= config.throwMaxDistance);
                    bool shouldDash = inside && cdA <= 0f && d <= (meleeR + 0.25f);

                    if (shouldThrow)
                    {
                        desiredVel = Vector2.zero;
                        state = State.AttackB;
                        attackStateTimeoutAt = Time.time + 1.2f;
                        TriggerAttackB();
                    }
                    else if (shouldDash)
                    {
                        desiredVel = Vector2.zero;
                        state = State.AttackA;
                        attackStateTimeoutAt = Time.time + 1.0f;
                        TriggerAttackA();
                    }
                    else
                    {
                        float stopMin = meleeR + standStillBand;
                        float stopMax = Mathf.Max(stopMin, minThrow - standStillBand);

                        if (inside && d >= stopMin && d <= stopMax && cdA > 0f && cdB > 0f)
                        {
                            desiredVel = Vector2.zero;
                        }
                        else
                        {
                            Vector2 target = (inside && player) ? (Vector2)player.position : lastKnownPlayerPos;
                            Vector2 dir = nav ? nav.GetDirection((Vector2)transform.position, target)
                                              : ((target - (Vector2)transform.position).sqrMagnitude > 0.0001f
                                                    ? (target - (Vector2)transform.position).normalized
                                                    : lastMoveDir);
                            desiredVel = dir * config.moveSpeed;
                        }
                    }
                    break;
                }

            case State.NoAxeChase:
                {
                    if (!myAxe) myAxe = FindMyLandedAxe();
                    bool axeValid = myAxe && myAxe.IsPickup;

                    float dPlayer = inside ? distToPlayer : float.PositiveInfinity;
                    float dAxe = axeValid ? Vector2.Distance(transform.position, myAxe.transform.position)
                                          : float.PositiveInfinity;

                    bool preferAttackPlayer = inside && (dPlayer <= dAxe || !axeValid);

                    if (preferAttackPlayer)
                    {
                        if (dPlayer <= (meleeR + 0.25f) && cdA <= 0f)
                        {
                            desiredVel = Vector2.zero;
                            state = State.AttackA;
                            attackStateTimeoutAt = Time.time + 1.0f;
                            TriggerAttackA();
                            break;
                        }
                        else
                        {
                            Vector2 targetP = (player ? (Vector2)player.position : lastKnownPlayerPos);
                            Vector2 dirP = nav ? nav.GetDirection((Vector2)transform.position, targetP)
                                               : ((targetP - (Vector2)transform.position).sqrMagnitude > 0.0001f
                                                    ? (targetP - (Vector2)transform.position).normalized
                                                    : lastMoveDir);
                            desiredVel = dirP * config.moveSpeed;
                            break;
                        }
                    }

                    if (axeValid)
                    {
                        Vector2 axePos = (Vector2)myAxe.transform.position;
                        Vector2 dirA = nav ? nav.GetDirection((Vector2)transform.position, axePos)
                                           : ((axePos - (Vector2)transform.position).sqrMagnitude > 0.0001f
                                                ? (axePos - (Vector2)transform.position).normalized
                                                : lastMoveDir);

                        if (Vector2.Distance(transform.position, axePos) <= takeAxeRange)
                        {
                            desiredVel = Vector2.zero;
                            state = State.TakeAxe;
                            attackStateTimeoutAt = Time.time + 1.0f;
                            if (animDriver && animDriver.anim) animDriver.anim.SetTrigger(T_TakeAxe);
                            break;
                        }

                        desiredVel = dirA * config.moveSpeed;
                        break;
                    }

                    if (inside && dPlayer <= (meleeR + 0.25f) && cdA <= 0f)
                    {
                        desiredVel = Vector2.zero;
                        state = State.AttackA;
                        attackStateTimeoutAt = Time.time + 1.0f;
                        TriggerAttackA();
                    }
                    else
                    {
                        Vector2 target = (inside && player) ? (Vector2)player.position : lastKnownPlayerPos;
                        Vector2 dir = nav ? nav.GetDirection((Vector2)transform.position, target)
                                          : ((target - (Vector2)transform.position).sqrMagnitude > 0.0001f
                                                ? (target - (Vector2)transform.position).normalized
                                                : lastMoveDir);
                        desiredVel = dir * config.moveSpeed;
                    }
                    break;
                }

            case State.ReturnHome:
                {
                    if (hasAggro)
                    {
                        state = hasAxe ? State.Chase : State.NoAxeChase;
                        desiredVel = Vector2.zero;
                        break;
                    }

                    if (!hasAxe)
                    {
                        if (!myAxe) myAxe = FindMyLandedAxe();
                        if (myAxe && myAxe.IsPickup)
                        {
                            state = State.NoAxeChase;
                            desiredVel = Vector2.zero;
                            break;
                        }
                    }

                    if (!homeSet)
                    {
                        state = State.Idle;
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

            case State.AttackA:
            case State.AttackB:
            case State.TakeAxe:
                desiredVel = Vector2.zero; break;

            case State.Dead:
                desiredVel = Vector2.zero; break;
        }

        // anti-atasco suave
        if (Time.time >= stuckCheckAt)
        {
            stuckCheckAt = Time.time + 0.5f;
            float moved = Vector2.Distance(rb ? rb.position : (Vector2)transform.position, lastPos);
            lastPos = rb ? rb.position : (Vector2)transform.position;

            if ((state == State.Chase || state == State.NoAxeChase || state == State.ReturnHome) && moved < 0.01f)
            {
                Vector2 target =
                    (state == State.ReturnHome && !alwaysChasePlayer)
                    ? homePos
                    : (player ? (Vector2)player.position : lastKnownPlayerPos);

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

        // dash move
        if (dashActive)
        {
            float dt = Time.fixedDeltaTime;
            Vector2 pos = rb ? rb.position : (Vector2)transform.position;
            float speed = Mathf.Max(0.1f, config.dashSpeed);
            Vector2 next = pos + dashDir * speed * dt;
            if (rb) rb.MovePosition(next);
            else transform.position = next;

            if (animDriver) animDriver.ReportMotion(dashDir * speed, dashDir);

            if (Time.time >= dashUntil)
            {
                dashActive = false;
                contactDisableAt = Time.time + Mathf.Max(0f, dashDamageLinger);
            }
            return;
        }

        // movimiento normal
        float dt2 = Time.fixedDeltaTime;
        Vector2 dv = desiredVel - curVel;
        float a = (desiredVel.sqrMagnitude > 0.000001f) ? config.acceleration : config.deceleration;
        Vector2 step = Vector2.ClampMagnitude(dv, a * dt2);
        curVel += step;

        if (curVel.sqrMagnitude > 0.000001f) lastMoveDir = curVel.normalized;

        Vector2 pos2 = rb ? rb.position : (Vector2)transform.position;
        Vector2 next2 = pos2 + curVel * dt2;
        if (rb) rb.MovePosition(next2);
        else transform.position = next2;

        if (animDriver) animDriver.ReportMotion(curVel, lastMoveDir);
    }

    void TryImmediateThrowOnEnter(float distNow)
    {
        if (state == State.Dead) return;
        if (!hasAxe) return;
        if (cdB > 0f) return;

        float meleeR = config.attackRangeA;
        float minThrow = Mathf.Max(config.throwMinDistance, meleeR + 0.15f);
        bool inRange = distNow >= minThrow && (config.throwMaxDistance <= 0f || distNow <= config.throwMaxDistance);
        if (!inRange) return;

        state = State.AttackB;
        desiredVel = Vector2.zero;
        attackStateTimeoutAt = Time.time + 1.2f;
        TriggerAttackB();
    }

    bool HasPlayerInRange(out float dist)
    {
        dist = Mathf.Infinity;
        if (!player) return false;
        float r = Mathf.Max(0.2f, config.detectionRadius);
        dist = Vector2.Distance(transform.position, player.position);
        return dist <= r;
    }

    float DistanceToPlayerSafe()
    {
        if (!player) return float.MaxValue;
        return Vector2.Distance(transform.position, player.position);
    }

    void EnsurePlayer(bool immediate)
    {
        if (player && player.gameObject.activeInHierarchy) return;
        if (!immediate && Time.time < reAcquireAt) return;
        reAcquireAt = Time.time + 0.25f;

        var go = GameObject.FindGameObjectWithTag("Player");
        if (go) { player = go.transform; return; }

        var ph = FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
        if (ph) { player = ph.transform; return; }
    }

    AxeProjectile2D FindMyLandedAxe()
    {
        if (!config || !config.axeProjectilePrefab) return null;
        AxeProjectile2D best = null;
        float bestDist = float.MaxValue;
        foreach (var a in AxeProjectile2D.All)
        {
            if (!a || !a.IsPickup) continue;
            if (a.owner != gameObject) continue;
            float d = (a.transform.position - transform.position).sqrMagnitude;
            if (d < bestDist && d <= retrieveSearchRadius * retrieveSearchRadius)
            {
                best = a;
                bestDist = d;
            }
        }
        return best;
    }

    void ApplyHasAxeParam()
    {
        if (animDriver && animDriver.anim && animDriver.anim.isInitialized)
            animDriver.anim.SetBool(P_HasAxe, hasAxe);
    }

    // animation triggers
    void TriggerAttackA() { if (animDriver && animDriver.anim) animDriver.anim.SetTrigger(T_AttackA); }
    void TriggerAttackB()
    {
        if (attackBArmed) return;
        attackBArmed = true;
        attackBShotsInState = 0;
        if (animDriver && animDriver.anim) animDriver.anim.SetTrigger(T_AttackB);
    }

    // animation events
    public void OnAnimAttackAHit()
    {
        if (player)
        {
            Vector2 to = (Vector2)player.position - (Vector2)transform.position;
            if (to.sqrMagnitude > 0.0001f) dashDir = to.normalized;
        }
        else if (lastMoveDir.sqrMagnitude > 0.0001f) dashDir = lastMoveDir;
        else dashDir = Vector2.right;

        dashActive = true;
        dashUntil = Time.time + Mathf.Max(0.05f, config.dashTime);
        contactDisableAt = Time.time + Mathf.Max(0.05f, config.dashTime + dashDamageLinger);
        if (contactDamage && manageContactDamage) contactDamage.enabled = true;

        cdA = Mathf.Max(0.05f, config.attackCooldownA);
    }

    public void OnAnimAttackAEnd()
    {
        attackStateTimeoutAt = -1f;
        dashActive = false;
        hasAggro = true;
        aggroUntil = Time.time + config.aggroHoldSeconds;
        if (state != State.Dead) state = hasAxe ? State.Chase : State.NoAxeChase;
    }

    public void OnAnimAttackBHit()
    {
        if (!attackBArmed) return;
        if (attackBShotsInState > 0) return;

        if (!hasAxe || !config)
        {
            cdB = Mathf.Max(0.05f, config != null ? config.attackCooldownB : 0.5f);
            attackBArmed = false;
            return;
        }

        Vector2 dir = GetFacingDir();
        Vector2 spawn = (Vector2)transform.position + RotateOffset(config.projectileSpawnOffset, dir);

        if (config.axeProjectilePrefab)
        {
            var proj = Instantiate(config.axeProjectilePrefab, spawn, Quaternion.identity);
            proj.speed = Mathf.Max(1f, config.projectileSpeed);
            proj.lifetime = Mathf.Max(0.2f, config.projectileLife);
            proj.maxTravelDistance = Mathf.Max(0f, config.projectileMaxDistance);
            proj.damage = config.attackDamageB;
            proj.knockback = config.attackKnockbackB;
            proj.hitMask = config.projectileHitMask;

            // ?? asignar dueño y suscribir a muerte (el script del hacha se autolimpia)
            if (proj && proj.TryGetComponent<AxeProjectile2D>(out var axe))
            {
                // si tu versión tiene SetOwner:
                axe.SetOwner(gameObject);

                // fallback extra por si usas una versión sin SetOwner:
                if (damageable) damageable.onDeath.AddListener(() => { if (axe) axe.Consume(); });
            }
            else
            {
                // por compatibilidad si el prefab tiene el script exacto ya referenciado
                proj.owner = gameObject;
                if (damageable) damageable.onDeath.AddListener(() => { if (proj) proj.Consume(); });
            }

            proj.Launch(dir);

            myAxe = proj;
            hasAxe = false;
            ApplyHasAxeParam();
        }

        cdB = Mathf.Max(0.05f, config.attackCooldownB);
        attackBShotsInState = 1;
        attackBArmed = false;
    }

    public void OnAnimAttackBEnd()
    {
        attackStateTimeoutAt = -1f;
        attackBArmed = false;
        attackBShotsInState = 0;

        hasAggro = true;
        aggroUntil = Time.time + config.aggroHoldSeconds;
        if (state != State.Dead) state = hasAxe ? State.Chase : State.NoAxeChase;
    }

    public void OnAnimTakeAxe()
    {
        attackStateTimeoutAt = -1f;

        if (myAxe && myAxe.IsPickup) myAxe.Consume();
        myAxe = null;
        hasAxe = true;
        ApplyHasAxeParam();

        float d;
        bool inside = HasPlayerInRange(out d);
        if (!inside && !alwaysChasePlayer && returnHomeOnLost && homeSet)
        {
            hasAggro = false;
            aggroUntil = -1f;
            state = State.ReturnHome;
            return;
        }

        hasAggro = true;
        aggroUntil = Time.time + config.aggroHoldSeconds;
        if (state != State.Dead) state = State.Chase;
    }

    Vector2 GetFacingDir()
    {
        if (player)
        {
            Vector2 to = (Vector2)player.position - (Vector2)transform.position;
            if (to.sqrMagnitude > 0.0001f) return to.normalized;
        }
        if (lastMoveDir.sqrMagnitude > 0.0001f) return lastMoveDir;
        return Vector2.right;
    }

    static Vector2 RotateOffset(Vector2 offset, Vector2 dir)
    {
        float a = Mathf.Atan2(dir.y, dir.x);
        float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
        return new Vector2(offset.x * ca - offset.y * sa, offset.x * sa + offset.y * ca);
    }

    void OnDeath()
    {
        if (state == State.Dead) return;
        state = State.Dead;
        curVel = Vector2.zero;
        desiredVel = Vector2.zero;

        if (collidersToDisable != null)
            foreach (var c in collidersToDisable) if (c) c.enabled = false;

        dashActive = false;
        if (contactDamage) contactDamage.enabled = false;

        // limpiar cualquier hacha que me pertenezca
        if (myAxe) { myAxe.Consume(); myAxe = null; }
        for (int i = AxeProjectile2D.All.Count - 1; i >= 0; i--)
        {
            var a = AxeProjectile2D.All[i];
            if (a && a.owner == gameObject) a.Consume();
        }

        if (loot) loot.Drop();

        if (animDriver) animDriver.PlayDieAndLock();
        Destroy(gameObject, 2f);
    }

    void OnDrawGizmosSelected()
    {
        float r = config ? config.detectionRadius : 6f;
        Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, r);

        if (homeSet)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
            Gizmos.DrawWireSphere(homePos, homeStopRadius);
        }
    }
}
