using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

[DefaultExecutionOrder(6000)]
public class MeleeHitbox2D : MonoBehaviour
{
    [Header("Daño")]
    public LayerMask targetLayers = ~0;
    public float damage = 10f;
    public float knockback = 3f;

    [Header("Referencia de tamaño/centro")]
    public SpriteRenderer referenceSR; // si lo dejas vacío, toma driver.itemSRRoot / itemSR / bodySR

    [Header("Tamaños del hitbox (local)")]
    public Vector2 sizeSide = new(0.18f, 0.36f); // L/R (vertical)
    public Vector2 sizeUpDown = new(0.36f, 0.18f); // U/D (horizontal)

    [Header("Separación respecto al borde")]
    public float edgeGap = 0.02f;

    [Header("Ajustes finos")]
    public float sideYBias = 0f;
    public float upXBias = 0f;
    public float downXBias = 0f;

    [Header("Seguimiento")]
    public bool followWhileOpen = true;

    [Header("Autocierre (failsafe)")]
    [Tooltip("Tiempo máximo que puede quedar abierta la ventana si no llega AE_MeleeStop")]
    public float maxWindowSeconds = 0.30f;
    [Tooltip("Si el driver deja de estar en modo melee, se cierra la ventana")]
    public bool autoCloseIfDriverLeavesMelee = true;

    [Header("Mapping Dir (0=D,1=R,2=L,3=U)")]
    public int mapDown = 0, mapRight = 1, mapLeft = 2, mapUp = 3;

    [Header("Debug")]
    public bool drawGizmo = true;
    public Color gizmoColor = new Color(0f, 1f, 0.3f, 0.25f);

    // ---- internals ----
    Collider2D col;
    WeaponAnimatorDriver driver;
    bool windowOpen = false;
    int activeDir = 0;           // 0..3
    float closeAt = -1f;          // Time.time al que se auto-cierra
    readonly HashSet<Collider2D> alreadyHit = new();

    void Reset()
    {
        col = GetComponent<Collider2D>();
        if (!col) col = gameObject.AddComponent<BoxCollider2D>();
        col.isTrigger = true;

        driver = GetComponentInParent<WeaponAnimatorDriver>();
        if (!referenceSR && driver)
            referenceSR = driver.itemSRRoot ? driver.itemSRRoot :
                          driver.itemSR ? driver.itemSR :
                          driver.bodySR;
    }

    void Awake()
    {
        if (!col) col = GetComponent<Collider2D>();
        if (!driver) driver = GetComponentInParent<WeaponAnimatorDriver>();
        if (!referenceSR && driver)
            referenceSR = driver.itemSRRoot ? driver.itemSRRoot :
                          driver.itemSR ? driver.itemSR :
                          driver.bodySR;

        if (col) col.enabled = false;
    }

    void LateUpdate()
    {
        if (!windowOpen) return;

        // Failsafe 1: cierre por tiempo
        if (maxWindowSeconds > 0f && Time.time >= closeAt)
        {
            End();
            return;
        }

        // Failsafe 2: si se cambió a arma de fuego en medio del swing
        if (autoCloseIfDriverLeavesMelee && driver && !driver.isMelee)
        {
            End();
            return;
        }

        if (followWhileOpen) PlaceHitbox();
    }

    // -------- ventana de daño: llamadas desde el Driver --------
    public void Begin(int dirFromDriver)
    {
        activeDir = NormalizeDir(dirFromDriver);
        windowOpen = true;
        alreadyHit.Clear();
        if (col) col.enabled = true;

        closeAt = Time.time + Mathf.Max(0.02f, maxWindowSeconds); // margen mínimo
        PlaceHitbox();
    }

    public void End()
    {
        windowOpen = false;
        if (col) col.enabled = false;
    }

    // alias útil por si quieres forzar desde fuera
    public void EndImmediate() => End();

    // -------- Colocación del hitbox --------
    void PlaceHitbox()
    {
        var sr = referenceSR;
        if (!sr || !sr.sprite) return;

        var b = sr.sprite.bounds; // local al SR
        Vector2 center = b.center;
        Vector2 ext = b.extents;

        Vector2 hbSize;
        Vector2 hbCenter;

        switch (activeDir)
        {
            // RIGHT
            case 1:
                hbSize = sizeSide;
                hbCenter = new Vector2(center.x + ext.x + edgeGap + hbSize.x * 0.5f,
                                       center.y + sideYBias);
                break;

            // LEFT
            case 2:
                hbSize = sizeSide;
                hbCenter = new Vector2(center.x - ext.x - edgeGap - hbSize.x * 0.5f,
                                       center.y + sideYBias);
                break;

            // UP
            case 3:
                hbSize = sizeUpDown;
                hbCenter = new Vector2(center.x + upXBias,
                                       center.y + ext.y + edgeGap + hbSize.y * 0.5f);
                break;

            // DOWN
            default:
                hbSize = sizeUpDown;
                hbCenter = new Vector2(center.x + downXBias,
                                       center.y - ext.y - edgeGap - hbSize.y * 0.5f);
                break;
        }

        // posicionamos por mundo para evitar errores con escalas
        Vector3 worldPos = sr.transform.TransformPoint(hbCenter);
        transform.position = worldPos;

        if (col is BoxCollider2D box)
        {
            box.offset = Vector2.zero;
            box.size = hbSize;
        }
    }

    int NormalizeDir(int dir)
    {
        if (dir == mapDown) return 0;
        if (dir == mapRight) return 1;
        if (dir == mapLeft) return 2;
        if (dir == mapUp) return 3;
        return 0;
    }

    // -------- daño --------
    void OnDisable()
    {
        windowOpen = false;
        if (col) col.enabled = false;
        alreadyHit.Clear();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!windowOpen) return;
        if ((targetLayers.value & (1 << other.gameObject.layer)) == 0) return;
        if (alreadyHit.Contains(other)) return;

        alreadyHit.Add(other);

        Vector2 hitPoint = other.bounds.ClosestPoint(transform.position);
        Vector2 dir = (Vector2)(other.bounds.center - transform.position);
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        dir.Normalize();
        Vector2 impulse = dir * knockback;

        TryApplyDamage(other.gameObject, damage, hitPoint, impulse);
    }

    // --- compat con DamageInfo / IDamageable ---
    static void TryApplyDamage(GameObject target, float dmg, Vector2 point, Vector2 impulse)
    {
        var comps = target.GetComponents<Component>();
        var dmgInfoType = FindTypeByName("DamageInfo");
        if (dmgInfoType != null)
        {
            foreach (var c in comps)
            {
                if (c == null) continue;
                var m = c.GetType().GetMethod("ApplyDamage", new[] { dmgInfoType });
                if (m != null)
                {
                    object info = Activator.CreateInstance(dmgInfoType);
                    SetFieldIfExists(dmgInfoType, info, "amount", dmg);
                    SetFieldIfExists(dmgInfoType, info, "damage", dmg);
                    SetFieldIfExists(dmgInfoType, info, "point", point);
                    SetFieldIfExists(dmgInfoType, info, "hitPoint", point);
                    SetFieldIfExists(dmgInfoType, info, "impulse", impulse);
                    SetFieldIfExists(dmgInfoType, info, "knockback", impulse.magnitude);
                    m.Invoke(c, new[] { info });
                    return;
                }
            }
        }

        foreach (var c in comps)
        {
            if (c == null) continue;
            var m = c.GetType().GetMethod("ApplyDamage", new[] { typeof(float) });
            if (m != null) { m.Invoke(c, new object[] { dmg }); return; }
        }

        target.SendMessage("ApplyDamage", dmg, SendMessageOptions.DontRequireReceiver);
    }

    static Type FindTypeByName(string name)
    {
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = a.GetTypes().FirstOrDefault(x => x.Name == name);
            if (t != null) return t;
        }
        return null;
    }

    static void SetFieldIfExists(Type t, object obj, string field, object val)
    {
        var f = t.GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null && (val == null || f.FieldType.IsAssignableFrom(val.GetType())))
            f.SetValue(obj, val);
    }

    // -------- Gizmos --------
    void OnDrawGizmos()
    {
        if (!drawGizmo) return;
        if (col is BoxCollider2D box)
        {
            Gizmos.color = gizmoColor;
            var center = transform.position;
            Gizmos.DrawCube(center, (Vector3)box.size);
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
            Gizmos.DrawWireCube(center, (Vector3)box.size);
        }
    }
}
