using UnityEngine;

public class DebugHitscanShooter : MonoBehaviour
{
    [Header("Refs")]
    public Transform muzzle;           // arrastra aquí tu MuzzlePoint (el hijo que crea tu hotbar)
    public LayerMask enemyMask;        // pon la capa "Enemy"

    [Header("Stats")]
    public float range = 15f;
    public float damage = 10f;
    public float knockback = 4f;

    [Header("Input")]
    public KeyCode key = KeyCode.Mouse0; // click izq o cámbialo a KeyCode.J si prefieres

    void Reset()
    {
        if (enemyMask.value == 0) enemyMask = LayerMask.GetMask("Enemy");
        if (!muzzle)
        {
            // intenta encontrarlo por nombre común
            var t = transform.root.Find("Item/MuzzlePoint");
            if (!t) t = transform.Find("MuzzlePoint");
            muzzle = t;
        }
    }

    void Update()
    {
        if (!muzzle) return;

        bool pressed = (key == KeyCode.Mouse0) ? Input.GetMouseButtonDown(0) : Input.GetKeyDown(key);
        if (!pressed) return;

        Vector2 origin = muzzle.position;
        Vector2 dir = AimDirFromMouse(muzzle.position); // para probar con el mouse

        var hit = Physics2D.Raycast(origin, dir, range, enemyMask);
        Debug.DrawRay(origin, dir * range, Color.red, 0.15f);

        if (hit.collider && hit.collider.TryGetComponent<IDamageable>(out var dmg))
        {
            var info = new DamageInfo(
                amount: damage,
                dir: dir,
                hitPoint: hit.point,
                kind: DamageKind.Bullet,
                knockback: knockback,
                source: null,
                owner: gameObject
            );
            dmg.ApplyDamage(info);
        }
    }

    Vector2 AimDirFromMouse(Vector3 fromWorld)
    {
        var cam = Camera.main;
        if (!cam) return Vector2.right;
        Vector2 mouse = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 d = mouse - (Vector2)fromWorld;
        return d.sqrMagnitude < 0.0001f ? Vector2.right : d.normalized;
    }
}
