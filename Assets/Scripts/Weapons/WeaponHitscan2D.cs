using UnityEngine;

[DisallowMultipleComponent]
public class WeaponHitscan2D : MonoBehaviour
{
    [Header("Refs")]
    public WeaponAnimatorDriver driver;   // Se autoasigna si está en el mismo GO
    public Transform muzzle;              // Se auto-busca si lo dejas vacío
    [Tooltip("Capas que puede golpear (incluye Enemy). No incluyas Player.")]
    public LayerMask hitMask;

    [Header("Comportamiento")]
    [Tooltip("No disparar cuando el arma es melee.")]
    public bool ignoreWhenMelee = true;

    [Header("Alcance & Fuerza")]
    public float range = 24f;
    public float knockback = 4f;

    [Header("Ranged (pistola/rifle)")]
    public float rangedDamage = 12f;
    [Tooltip("Desviación en grados a cada lado")]
    public float rangedSpreadDeg = 1.5f;
    [Tooltip("Rays por disparo (normalmente 1)")]
    public int rangedRays = 1;

    [Header("Shotgun")]
    public int shotgunPellets = 6;
    public float shotgunDamagePerPellet = 3f;
    public float shotgunSpreadDeg = 10f;

    [Header("Penetración")]
    [Tooltip("Cuántos objetivos como máximo puede atravesar cada ray.")]
    public int maxHitsPerRay = 1; // 1 = se detiene en el primero

    [Header("Debug")]
    public bool debugRays = false;

    static readonly int P_Dir = Animator.StringToHash("Dir");

    void Reset()
    {
        driver ??= GetComponent<WeaponAnimatorDriver>();
        AutoAssignMuzzle();

        if (hitMask.value == 0)
        {
            int enemy = LayerMask.NameToLayer("Enemy");
            hitMask = (enemy >= 0) ? (1 << enemy) : ~0; // si no existe la capa, por defecto todo
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

        // 1) Si el driver tiene MuzzleFlash, usa su transform
        if (driver && driver.muzzle) { muzzle = driver.muzzle.transform; if (muzzle) return; }

        // 2) Buscar hijos comunes
        var t = transform.Find("MuzzlePoint");
        if (!t && transform.parent) t = transform.parent.Find("MuzzlePoint");
        if (!t) t = transform.Find("Muzzle");
        if (!t && transform.parent) t = transform.parent.Find("Muzzle");

        // 3) Último recurso: este transform
        muzzle = t ? t : transform;
    }

    // ——— Llamado por Animation Event (AE_Muzzle) ———
    public void AE_Muzzle()
    {
        // El driver ya controló si se podía disparar y ya descontó la bala.
        if (driver && ignoreWhenMelee && driver.isMelee) return; // no pegar en melee
        if (!muzzle) AutoAssignMuzzle();
        if (!muzzle) return;

        FireInternal();
    }

    void FireInternal()
    {
        Vector2 baseDir = GetDirVector();

        bool shotgun = driver && driver.isShotgun;
        int rays = shotgun ? Mathf.Max(1, shotgunPellets) : Mathf.Max(1, rangedRays);
        float spread = shotgun ? shotgunSpreadDeg : rangedSpreadDeg;
        float dmg = shotgun ? shotgunDamagePerPellet : rangedDamage;

        for (int i = 0; i < rays; i++)
        {
            Vector2 dir = ApplySpread(baseDir, spread);
            DoRay(muzzle.position, dir, dmg);
        }
    }

    void DoRay(Vector2 origin, Vector2 dir, float damage)
    {
        if (debugRays) Debug.DrawRay(origin, dir * range, Color.red, 0.15f);

        var hits = Physics2D.RaycastAll(origin, dir, range, hitMask);
        if (hits == null || hits.Length == 0) return;

        int applied = 0;
        foreach (var hit in hits)
        {
            // Ignora al propio player si por error entra en máscara
            if (driver && driver.playerRb && hit.rigidbody == driver.playerRb) continue;

            // Aplica daño a IDamageable (en collider o padre)
            IDamageable dmg = null;
            if (hit.collider.TryGetComponent<IDamageable>(out var d1)) dmg = d1;
            else { var p = hit.collider.GetComponentInParent<IDamageable>(); if (p != null) dmg = p; }

            if (dmg != null)
            {
                var info = new DamageInfo(
                    amount: damage,
                    dir: dir,
                    hitPoint: hit.point,
                    kind: DamageKind.Bullet,
                    knockback: knockback,
                    source: gameObject,
                    owner: transform.root ? transform.root.gameObject : gameObject
                );
                dmg.ApplyDamage(info);
                applied++;
                if (applied >= Mathf.Max(1, maxHitsPerRay)) break;
            }
            else
            {
                // Si quieres que el disparo se detenga al tocar pared, descomenta:
                // break;
            }
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
