using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ShotgunCone2D : MonoBehaviour
{
    [Header("Refs (si llamas Fire() sin args)")]
    public WeaponAnimatorDriver driver;     // opcional

    [Header("Forma")]
    public float radius = 6.5f;
    [Range(10f, 160f)] public float angle = 75f;
    [Min(3)] public int rays = 18;
    public float startOffset = 0.35f;

    [Header("Daño")]
    public float damage = 3f;
    public float knockback = 3f;
    public DamageKind kind = DamageKind.Bullet;
    public LayerMask hitMask = -1;

    [Header("VFX (opcional)")]
    public TracerPool2D tracerPool;
    public bool drawTracers = true;
    public Color tracerColor = new Color(1f, 1f, 1f, 0.9f);
    public float tracerLife = 0.06f;

    [Header("Ajuste de Y SOLO del disparo del shotgun")]
    public float shotgunYAdjust = 0f;

    public void Fire(Vector2 origin, Vector2 dir, GameObject owner)
    {
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;

        // aplica tu ajuste de Y SOLO al disparo (no al Muzzle)
        origin.y += shotgunYAdjust;

        Vector2 shootOrigin = origin + dir.normalized * Mathf.Max(0f, startOffset);
        float length = Mathf.Max(0.05f, radius - startOffset);

        float half = angle * 0.5f * Mathf.Deg2Rad;
        float step = (angle / Mathf.Max(1, rays - 1)) * Mathf.Deg2Rad;

        var damaged = new HashSet<IDamageable>();

        for (int i = 0; i < rays; i++)
        {
            float a = -half + step * i;
            Vector2 rdir = Rotate(dir.normalized, a);

            var hits = Physics2D.RaycastAll(shootOrigin, rdir, length, hitMask);
            Vector2 end = shootOrigin + rdir * length;

            foreach (var hit in hits)
            {
                IDamageable dmg = hit.collider.GetComponent<IDamageable>() ?? hit.collider.GetComponentInParent<IDamageable>();
                if (dmg != null && damaged.Add(dmg))
                {
                    var info = new DamageInfo(
                        amount: damage, dir: rdir, hitPoint: hit.point, kind: kind,
                        knockback: knockback, source: gameObject, owner: owner ? owner : gameObject
                    );
                    dmg.ApplyDamage(info);
                }
                end = hit.point;
            }

            if (drawTracers && tracerPool)
            {
                var t = tracerPool.Get();
                t.Show(shootOrigin, end, tracerColor, tracerLife);
            }
        }
    }

    public void Fire()
    {
        var d = driver ? driver : GetComponentInParent<WeaponAnimatorDriver>();
        var binder = d ? d.GetComponent<MuzzleAnchorBinder>() : GetComponent<MuzzleAnchorBinder>();

        Vector2 origin = binder ? (Vector2)binder.GetAnchorWorldPos()
                                : (d && d.muzzle ? (Vector2)d.muzzle.transform.position : (Vector2)transform.position);

        int rawDir = d ? d.GetCurrentDirFromAnim() : 1;
        Vector2 dir = rawDir switch
        {
            0 => Vector2.down,
            1 => Vector2.right,
            2 => Vector2.left,
            3 => Vector2.up,
            _ => Vector2.right
        };

        GameObject owner = d ? (d.transform.root ? d.transform.root.gameObject : d.gameObject) : gameObject;
        Fire(origin, dir, owner);
    }

    static Vector2 Rotate(Vector2 v, float rad)
    {
        float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }
}
