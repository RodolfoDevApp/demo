using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Combat/Contact Damage 2D")]
public class ContactDamage2D : MonoBehaviour
{
    [Header("Trigger (hijo)")]
    [Tooltip("Collider2D con IsTrigger=true. Si lo dejas vacio, busca el primer trigger en hijos.")]
    public Collider2D damageTrigger;

    [Header("Objetivos")]
    public LayerMask targetMask;

    [Header("Daño")]
    [Min(0.01f)] public float damage = 1f;
    public float knockback = 0f;
    [Tooltip("Tiempo entre golpes al MISMO collider/objetivo.")]
    public float hitCooldown = 0.5f;

    [Header("Rendimiento")]
    [Tooltip("Si esta en true, NO usa OnTriggerStay. En su lugar, escanea a tickRateHz con Overlap.")]
    public bool useManualScan = false;
    [Min(1f)] public float tickRateHz = 20f;
    [Min(1)] public int maxHitsPerTick = 16;

    [Header("Debug")]
    public bool debugLog = false;

    private readonly Dictionary<Collider2D, float> _nextHitAllowed = new();
    private DamageTriggerRelay2D _relay;

    // buffer y filtro para el modo manual
    private ContactFilter2D _filter;
    private Collider2D[] _hits;
    private float _nextTickAt = 0f;

    void Reset()
    {
        if (targetMask.value == 0)
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            targetMask = (playerLayer >= 0) ? (1 << playerLayer) : ~0;
        }

        if (!damageTrigger)
        {
            var child = new GameObject("MeleeHitbox");
            child.transform.SetParent(transform, false);
            var box = child.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            damageTrigger = box;
        }
    }

    void OnValidate()
    {
        if (damageTrigger && !damageTrigger.isTrigger)
            damageTrigger.isTrigger = true;

        if (maxHitsPerTick < 1) maxHitsPerTick = 1;
        if (tickRateHz < 1f) tickRateHz = 1f;

        _filter = new ContactFilter2D();
        _filter.useTriggers = true;
        _filter.SetLayerMask(targetMask);
        _filter.useLayerMask = true;

        if (_hits == null || _hits.Length < maxHitsPerTick)
            _hits = new Collider2D[Mathf.Max(4, maxHitsPerTick)];
    }

    void Awake()
    {
        AutoFindTriggerIfNeeded();

        _filter = new ContactFilter2D();
        _filter.useTriggers = true;
        _filter.SetLayerMask(targetMask);
        _filter.useLayerMask = true;

        if (_hits == null || _hits.Length < maxHitsPerTick)
            _hits = new Collider2D[Mathf.Max(4, maxHitsPerTick)];

        HookRelay();
    }

    void OnEnable()
    {
        AutoFindTriggerIfNeeded();
        _nextHitAllowed.Clear();

        if (_hits == null || _hits.Length < maxHitsPerTick)
            _hits = new Collider2D[Mathf.Max(4, maxHitsPerTick)];

        _nextTickAt = Time.time + Random.value * (1f / Mathf.Max(1f, tickRateHz));

        HookRelay();
    }

    void OnDisable()
    {
        _nextHitAllowed.Clear();
        UnhookRelay();
    }

    void Update()
    {
        if (!enabled) return;
        if (!useManualScan) return;
        if (!damageTrigger) return;
        if (!damageTrigger.enabled || !damageTrigger.gameObject.activeInHierarchy) return;

        float now = Time.time;
        if (now < _nextTickAt) return;

        float period = 1f / Mathf.Max(1f, tickRateHz);
        _nextTickAt = now + period;

        int count = 0;
        try
        {
            // Unity 6.2: usar Overlap en lugar de OverlapCollider
            count = damageTrigger.Overlap(_filter, _hits);
        }
        catch { return; }

        for (int i = 0; i < count && i < _hits.Length; i++)
        {
            var other = _hits[i];
            if (other) TryHit(other);
            _hits[i] = null;
        }
    }

    void AutoFindTriggerIfNeeded()
    {
        if (damageTrigger) return;
        var all = GetComponentsInChildren<Collider2D>(true);
        foreach (var c in all)
        {
            if (c && c.isTrigger) { damageTrigger = c; break; }
        }
        if (damageTrigger) damageTrigger.isTrigger = true;
    }

    void HookRelay()
    {
        if (useManualScan) { UnhookRelay(); return; }
        if (!damageTrigger) return;

        _relay = damageTrigger.GetComponent<DamageTriggerRelay2D>();
        if (!_relay) _relay = damageTrigger.gameObject.AddComponent<DamageTriggerRelay2D>();

        _relay.onEnter -= OnRelayEnter; _relay.onEnter += OnRelayEnter;
        _relay.onStay -= OnRelayStay; _relay.onStay += OnRelayStay;
    }

    void UnhookRelay()
    {
        if (_relay)
        {
            _relay.onEnter -= OnRelayEnter;
            _relay.onStay -= OnRelayStay;
        }
    }

    void OnRelayEnter(Collider2D other) { TryHit(other); }
    void OnRelayStay(Collider2D other) { TryHit(other); }

    void TryHit(Collider2D other)
    {
        if (!enabled || other == null) return;

        if (((1 << other.gameObject.layer) & targetMask.value) == 0) return;

        float now = Time.time;
        if (_nextHitAllowed.TryGetValue(other, out float t) && now < t) return;

        bool applied = false;

        var ph = other.GetComponentInParent<PlayerHealth>();
        if (ph != null)
        {
            int intDamage = Mathf.RoundToInt(Mathf.Max(1f, damage));
            ph.TakeDamage(intDamage);
            applied = true;
            if (debugLog) Debug.Log("ContactDamage2D -> PlayerHealth.TakeDamage(" + intDamage + ")", this);
        }
        else
        {
            var idmg = other.GetComponentInParent<IDamageable>();
            if (idmg != null)
            {
                Vector2 src = transform.position;
                Vector2 hp = other.ClosestPoint(src);
                Vector2 dir = hp - src;
                if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
                dir.Normalize();

                var info = new DamageInfo(
                    amount: damage,
                    dir: dir,
                    hitPoint: hp,
                    kind: DamageKind.Melee,
                    knockback: knockback,
                    source: gameObject,
                    owner: gameObject
                );

                idmg.ApplyDamage(info);
                applied = true;
                if (debugLog) Debug.Log("ContactDamage2D -> IDamageable.ApplyDamage(" + damage + ")", this);
            }
        }

        if (applied)
            _nextHitAllowed[other] = now + Mathf.Max(0.05f, hitCooldown);
    }
}
