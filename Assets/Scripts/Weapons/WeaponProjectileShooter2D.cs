using UnityEngine;

[DisallowMultipleComponent]
public class WeaponProjectileShooter2D : MonoBehaviour
{
    [Header("Refs")]
    public WeaponAnimatorDriver driver;              // para dir, hotbar, SR, etc.
    public Transform projectilesParent;              // contenedor opcional

    [Header("Prefabs")]
    public GameObject pistolProjectilePrefab;
    public GameObject rifleProjectilePrefab;

    [Header("Pistol")]
    public float pistolSpeed = 14f;
    public float pistolLife = 1.1f;
    public float pistolDamage = 10f;
    public float pistolKnock = 2f;
    public float pistolMaxDist = 18f;

    [Header("Rifle")]
    public float rifleSpeed = 18f;
    public float rifleLife = 1.2f;
    public float rifleDamage = 16f;
    public float rifleKnock = 3f;
    public float rifleMaxDist = 26f;

    [Header("Ajuste de Y al CREAR la bala (sobre el anchor)")]
    public float pistolYAdjust = 0f;   // ej: 0.91f - anchor ya da la base
    public float rifleYAdjust = 0f;   // ej: 1.45f

    int lastShotFrame = -1;

    void Reset()
    {
        if (!driver) driver = GetComponent<WeaponAnimatorDriver>();
    }

    MuzzleAnchorBinder FindBinder()
    {
        var b = GetComponent<MuzzleAnchorBinder>();
        if (b) return b;

        if (driver)
        {
            b = driver.GetComponent<MuzzleAnchorBinder>();
            if (b) return b;
        }

        return GetComponentInParent<MuzzleAnchorBinder>();
    }

    public void Fire()
    {
        // evita doble AE_Muzzle el mismo frame
        if (Time.frameCount == lastShotFrame) return;
        lastShotFrame = Time.frameCount;

        if (!driver) return;

        // origen desde los ANCHORS del arma actual (sin mover el Muzzle)
        var binder = FindBinder();
        Vector3 origin = binder ? binder.GetAnchorWorldPos()
                                : (driver.muzzle ? driver.muzzle.transform.position : driver.transform.position);

        // arma actual
        bool isPistol = false, isRifle = false;
        var hb = driver.hotbar;
        if (hb != null)
        {
            string id = hb.CurrentItemId;
            isPistol = id == hb.idPistol;
            isRifle = id == hb.idGun;
        }
        else isRifle = true;

        // aplica SOLO a la bala el ajuste Y que tú configures
        origin.y += isPistol ? pistolYAdjust : rifleYAdjust;

        // dirección por anim
        int d = driver.GetCurrentDirFromAnim();
        Vector2 dir = d switch
        {
            0 => Vector2.down,
            1 => Vector2.right,
            2 => Vector2.left,
            3 => Vector2.up,
            _ => Vector2.right
        };

        // elige prefab
        GameObject prefab = isPistol ? pistolProjectilePrefab : rifleProjectilePrefab;
        if (!prefab) return;

        // instancia en el punto exacto
        var go = Instantiate(prefab, origin, Quaternion.identity, projectilesParent);

        // evita colisión inicial con el owner
        var projCol = go.GetComponent<Collider2D>();
        if (projCol && driver)
        {
            var ownerCols = driver.transform.root.GetComponentsInChildren<Collider2D>(true);
            foreach (var oc in ownerCols)
                if (oc && oc.enabled) Physics2D.IgnoreCollision(projCol, oc, true);
        }

        // sincroniza y fuerza posición exacta
        Physics2D.SyncTransforms();
        var rb = go.GetComponent<Rigidbody2D>();
        if (rb)
        {
#if UNITY_2022_1_OR_NEWER
            rb.position = origin;
#else
            rb.MovePosition(origin);
#endif
            rb.linearVelocity = Vector2.zero;
        }
        else
        {
            go.transform.position = origin;
        }

        // configura stats
        var p = go.GetComponent<WeaponProjectile2D>();
        if (!p) return;

        if (isPistol)
        {
            p.speed = pistolSpeed; p.lifeTime = pistolLife; p.damage = pistolDamage;
            p.knockback = pistolKnock; p.maxDistance = pistolMaxDist; p.pierce = 0;
        }
        else
        {
            p.speed = rifleSpeed; p.lifeTime = rifleLife; p.damage = rifleDamage;
            p.knockback = rifleKnock; p.maxDistance = rifleMaxDist; p.pierce = 999;
        }

        p.Init(dir, driver.gameObject, driver.itemSR, projectilesParent);
    }
}
