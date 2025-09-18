using UnityEngine;

[DisallowMultipleComponent]
public class WeaponHitscan2D : MonoBehaviour
{
    [Header("Refs")]
    public WeaponAnimatorDriver driver;
    public Transform muzzle;
    public LayerMask hitMask;

    [Header("Comportamiento")]
    public bool ignoreWhenMelee = true;

    [Header("Alcance & Fuerza (GENERIC RANGED fallback)")]
    public float range = 24f;
    public float knockback = 4f;

    [Header("Ranged GENERIC (pistola/rifle si no hay override)")]
    public float rangedDamage = 12f;
    public float rangedSpreadDeg = 1.5f;
    public int rangedRays = 1;

    [Header("Shotgun")]
    public int shotgunPellets = 6;
    public float shotgunDamagePerPellet = 3f;
    public float shotgunSpreadDeg = 10f;

    [Header("Penetracion")]
    public int maxHitsPerRay = 1;

    [Header("Debug")]
    public bool debugRays = false;

    // ----------------- OVERRIDES OPCIONALES POR ARMA -----------------
    [Header("Hotbar IDs (opcional)")]
    public string pistolId = "pistol";
    public string rifleId = "gun";

    [Header("Pistol Override (si CurrentItemId == pistolId)")]
    public bool pistolOverride = true;
    public float pistolDamage = 10f;
    public float pistolSpreadDeg = 1.5f;
    public int pistolRays = 1;
    public float pistolRange = 20f;
    public float pistolKnockback = 2f;
    public int pistolMaxHitsPerRay = 1;

    [Header("Rifle Override (si CurrentItemId == rifleId)")]
    public bool rifleOverride = true;
    public float rifleDamage = 16f;
    public float rifleSpreadDeg = 1.0f;
    public int rifleRays = 1;
    public float rifleRange = 26f;
    public float rifleKnockback = 3f;
    public int rifleMaxHitsPerRay = 1;

    // ----------------- TRACER -----------------------------
    [Header("Tracer (visual)")]
    public bool spawnTracer = true;
    public TracerPool2D tracerPool;        // arrastra el GO del pool
    public Color tracerColor = Color.white;
    public Color tracerColorShotgun = new Color(1, 1, 1, 0.9f);
    public float tracerLife = 0.08f;
    // ------------------------------------------------------

    static readonly int P_Dir = Animator.StringToHash("Dir");

    void Reset()
    {
        driver ??= GetComponent<WeaponAnimatorDriver>();
        AutoAssignMuzzle();

        if (hitMask.value == 0)
        {
            int enemy = LayerMask.NameToLayer("Enemy");
            hitMask = (enemy >= 0) ? (1 << enemy) : ~0;
        }
    }

    void Awake()
    {
        if (!driver) driver = GetComponent<WeaponAnimatorDriver>();
        AutoAssignMuzzle();
    }

    void AutoAssignMuzzle()
    {
        if (muzzle) return;
        if (driver && driver.muzzle) { muzzle = driver.muzzle.transform; if (muzzle) return; }
        var t = transform.Find("MuzzlePoint");
        if (!t && transform.parent) t = transform.parent.Find("MuzzlePoint");
        if (!t) t = transform.Find("Muzzle");
        if (!t && transform.parent) t = transform.parent.Find("Muzzle");
        muzzle = t ? t : transform;
    }

    // Animation Event
    public void AE_Muzzle()
    {
        if (driver && ignoreWhenMelee && driver.isMelee) return;
        if (!muzzle) AutoAssignMuzzle();
        if (!muzzle) return;
        FireInternal();
    }

    void FireInternal()
    {
        // Resolver modo de arma actual
        bool isShotgunNow = (driver && driver.isShotgun);
        bool isPistolNow = false;
        bool isRifleNow = false;

        if (!isShotgunNow && driver && driver.hotbar)
        {
            string id = driver.hotbar.CurrentItemId;
            if (!string.IsNullOrEmpty(id))
            {
                isPistolNow = pistolOverride && id == pistolId;
                isRifleNow = rifleOverride && id == rifleId;
            }
        }

        // Seleccionar stats efectivos
        float useRange, useKnock, useSpread, useDamage;
        int useRays, useMaxHits;

        if (isShotgunNow)
        {
            useRange = range; // rango para raycast; si quieres un rango distinto por shotgun, crea otro campo
            useKnock = knockback; // knockback puede ser común; si quieres uno propio, añade shotgunKnockbackOverride
            useSpread = shotgunSpreadDeg;
            useDamage = shotgunDamagePerPellet;
            useRays = Mathf.Max(1, shotgunPellets);
            useMaxHits = Mathf.Max(1, maxHitsPerRay);
        }
        else if (isPistolNow)
        {
            useRange = pistolRange;
            useKnock = pistolKnockback;
            useSpread = pistolSpreadDeg;
            useDamage = pistolDamage;
            useRays = Mathf.Max(1, pistolRays);
            useMaxHits = Mathf.Max(1, pistolMaxHitsPerRay);
        }
        else if (isRifleNow)
        {
            useRange = rifleRange;
            useKnock = rifleKnockback;
            useSpread = rifleSpreadDeg;
            useDamage = rifleDamage;
            useRays = Mathf.Max(1, rifleRays);
            useMaxHits = Mathf.Max(1, rifleMaxHitsPerRay);
        }
        else
        {
            // Fallback genérico (tu comportamiento previo)
            useRange = range;
            useKnock = knockback;
            useSpread = rangedSpreadDeg;
            useDamage = rangedDamage;
            useRays = Mathf.Max(1, rangedRays);
            useMaxHits = Mathf.Max(1, maxHitsPerRay);
        }

        Vector2 baseDir = GetDirVector();

        for (int i = 0; i < useRays; i++)
        {
            Vector2 dir = ApplySpread(baseDir, useSpread);
            DoRay(muzzle.position, dir, useDamage, useRange, useKnock, isShotgunNow, useMaxHits);
        }
    }

    void DoRay(Vector2 origin, Vector2 dir, float damage, float rangeUsed, float knockbackUsed, bool isShotgun, int maxHitsThisRay)
    {
        if (debugRays) Debug.DrawRay(origin, dir * rangeUsed, Color.red, 0.15f);

        var hits = Physics2D.RaycastAll(origin, dir, rangeUsed, hitMask);
        Vector2 end = origin + dir * rangeUsed;

        int applied = 0;
        foreach (var hit in hits)
        {
            // ignora al propio player
            if (driver && driver.playerRb && hit.rigidbody == driver.playerRb) continue;

            end = hit.point; // punto visual para el tracer

            IDamageable dmg = null;
            if (hit.collider.TryGetComponent<IDamageable>(out var d1)) dmg = d1;
            else
            {
                var p = hit.collider.GetComponentInParent<IDamageable>();
                if (p != null) dmg = p;
            }

            if (dmg != null)
            {
                var info = new DamageInfo(
                    amount: damage,
                    dir: dir,
                    hitPoint: hit.point,
                    kind: DamageKind.Bullet,
                    knockback: knockbackUsed,
                    source: gameObject,
                    owner: transform.root ? transform.root.gameObject : gameObject
                );
                dmg.ApplyDamage(info);
                applied++;
                if (applied >= Mathf.Max(1, maxHitsThisRay)) break;
            }
            else
            {
                // Si deseas que el rayo muera al tocar pared, descomenta:
                // break;
            }
        }

        // TRACER visual
        if (spawnTracer && tracerPool)
        {
            var t = tracerPool.Get();
            t.Show(origin, end, isShotgun ? tracerColorShotgun : tracerColor, tracerLife);
        }
    }

    Vector2 ApplySpread(Vector2 v, float spreadDeg)
    {
        if (spreadDeg <= 0.001f) return v;
        float ang = Random.Range(-spreadDeg, spreadDeg) * Mathf.Deg2Rad;
        float cos = Mathf.Cos(ang), sin = Mathf.Sin(ang);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos).normalized;
    }

    Vector2 GetDirVector()
    {
        int d = 1;
        if (driver && driver.anim && HasParam(driver.anim, P_Dir)) d = driver.anim.GetInteger(P_Dir);
        return d switch
        {
            0 => Vector2.down,
            1 => Vector2.right,
            2 => Vector2.left,
            3 => Vector2.up,
            _ => Vector2.right
        };
    }

    static bool HasParam(Animator a, int hash)
    {
        if (!a) return false;
        foreach (var p in a.parameters) if (p.nameHash == hash) return true;
        return false;
    }
}
