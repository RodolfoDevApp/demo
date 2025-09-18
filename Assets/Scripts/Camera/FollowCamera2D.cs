using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class FollowCamera2D : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public string targetTag = "Player";

    [Header("Offset (Z negativo en 2D)")]
    public Vector3 offset = new Vector3(0f, 0f, -10f);

    [Header("Suavizado")]
    [Min(0f)] public float smooth = 10f; // mayor = mas rapido

    [Header("Bounds (si no asignas, se auto-detecta)")]
    public Collider2D boundsCollider;      // ideal: CompositeCollider2D del Tilemap_Colliders
    public string autoFindPrimary = "Tilemap_Colliders"; // nombre esperado de tu tilemap de colision
    public string autoFindFallback = "CameraBounds";     // objeto alterno con Box/PolygonCollider2D

    private Camera _cam;
    private float _reacquireTargetAt = 0f;
    private float _reacquireBoundsAt = 0f;

    void Reset()
    {
        _cam = GetComponent<Camera>();
        _cam.orthographic = true;
        if (offset.z == 0f) offset.z = -10f;
    }

    void Awake()
    {
        _cam = GetComponent<Camera>();
        _cam.orthographic = true;

        if (!target) TryFindTarget(true);
        if (!boundsCollider) TryFindBounds(true);

        // Snap inicial para evitar salto
        if (target)
        {
            Vector3 desired = target.position + offset;
            desired = ClampToBounds(desired);
            transform.position = desired;
        }
    }

    void LateUpdate()
    {
        // Reintentos suaves
        if (!target && Time.time >= _reacquireTargetAt) TryFindTarget(false);
        if (!boundsCollider && Time.time >= _reacquireBoundsAt) TryFindBounds(false);

        if (!target) return;

        Vector3 desired = target.position + offset;
        desired = ClampToBounds(desired);

        Vector3 cur = transform.position;
        float t = 1f - Mathf.Exp(-smooth * Time.deltaTime);
        transform.position = Vector3.Lerp(cur, desired, t);
    }

    private void TryFindTarget(bool immediate)
    {
        if (!immediate && Time.time < _reacquireTargetAt) return;
        _reacquireTargetAt = Time.time + 0.5f;

        if (!string.IsNullOrEmpty(targetTag))
        {
            var go = GameObject.FindGameObjectWithTag(targetTag);
            if (go) target = go.transform;
        }
    }

    private void TryFindBounds(bool immediate)
    {
        if (!immediate && Time.time < _reacquireBoundsAt) return;
        _reacquireBoundsAt = Time.time + 0.75f;

        Collider2D found = null;

        // 1) Prioriza Tilemap_Colliders con CompositeCollider2D
        if (!string.IsNullOrEmpty(autoFindPrimary))
        {
            var go = GameObject.Find(autoFindPrimary);
            if (go)
            {
                found = go.GetComponent<CompositeCollider2D>();
                if (!found) found = go.GetComponent<Collider2D>();
            }
        }

        // 2) Fallback: objeto "CameraBounds" con Box/PolygonCollider2D
        if (!found && !string.IsNullOrEmpty(autoFindFallback))
        {
            var go2 = GameObject.Find(autoFindFallback);
            if (go2) found = go2.GetComponent<Collider2D>();
        }

        // 3) Ultimo recurso: cualquier CompositeCollider2D de la escena
        if (!found)
        {
            var anyComposite = FindFirstObjectByType<CompositeCollider2D>(FindObjectsInactive.Include);
            if (anyComposite) found = anyComposite;
        }

        // 4) Acepta si es valido
        if (found)
        {
            boundsCollider = found;
        }
        else
        {
            // Warning una sola vez por intento
            Debug.LogWarning("[FollowCamera2D] No se encontraron bounds. Asigna 'boundsCollider' o crea 'Tilemap_Colliders' con CompositeCollider2D, o un objeto 'CameraBounds' con Box/PolygonCollider2D.");
        }
    }

    private Vector3 ClampToBounds(Vector3 desired)
    {
        if (!_cam || !boundsCollider || !_cam.orthographic)
            return desired;

        Bounds b = boundsCollider.bounds;

        float halfH = _cam.orthographicSize;
        float halfW = halfH * _cam.aspect;

        float x = desired.x;
        float y = desired.y;

        // Si el area de bounds es menor que la vista, centra
        if (b.size.x <= halfW * 2f) x = b.center.x;
        else x = Mathf.Clamp(x, b.min.x + halfW, b.max.x - halfW);

        if (b.size.y <= halfH * 2f) y = b.center.y;
        else y = Mathf.Clamp(y, b.min.y + halfH, b.max.y - halfH);

        return new Vector3(x, y, desired.z);
    }
}
