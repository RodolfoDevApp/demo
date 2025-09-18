using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class MinimapCamera2D : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public string targetTag = "Player";

    [Header("Follow")]
    public Vector2 offset = Vector2.zero;
    [Min(0f)] public float followLerp = 25f; // mas alto = sigue mas rapido
    public bool lockRotation = true;

    [Header("Bounds (auto si no asignas)")]
    public Collider2D boundsCollider;           // ideal: CompositeCollider2D de Tilemap_Colliders
    public string autoFindPrimary = "Tilemap_Colliders";
    public string autoFindFallback = "CameraBounds";

    Camera _cam;
    float _reacquireTargetAt = 0f;
    float _reacquireBoundsAt = 0f;

    void Reset()
    {
        _cam = GetComponent<Camera>();
        _cam.orthographic = true;
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
            Vector3 p = target.position;
            p.x += offset.x; p.y += offset.y; p.z = transform.position.z;
            transform.position = ClampToBounds(p);
        }

        if (lockRotation) transform.rotation = Quaternion.identity;
    }

    void LateUpdate()
    {
        if (!target && Time.time >= _reacquireTargetAt) TryFindTarget(false);
        if (!boundsCollider && Time.time >= _reacquireBoundsAt) TryFindBounds(false);

        if (!target) return;

        // Seguir con lerp exponencial
        Vector3 desired = target.position;
        desired.x += offset.x;
        desired.y += offset.y;
        desired.z = transform.position.z;

        desired = ClampToBounds(desired);

        float t = 1f - Mathf.Exp(-followLerp * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desired, t);

        if (lockRotation) transform.rotation = Quaternion.identity;
    }

    Vector3 ClampToBounds(Vector3 desired)
    {
        if (!_cam || !boundsCollider || !_cam.orthographic) return desired;

        Bounds b = boundsCollider.bounds;
        float halfH = _cam.orthographicSize;
        float halfW = halfH * _cam.aspect;

        float x = desired.x;
        float y = desired.y;

        if (b.size.x <= halfW * 2f) x = b.center.x;
        else x = Mathf.Clamp(x, b.min.x + halfW, b.max.x - halfW);

        if (b.size.y <= halfH * 2f) y = b.center.y;
        else y = Mathf.Clamp(y, b.min.y + halfH, b.max.y - halfH);

        return new Vector3(x, y, desired.z);
    }

    void TryFindTarget(bool immediate)
    {
        if (!immediate && Time.time < _reacquireTargetAt) return;
        _reacquireTargetAt = Time.time + 0.5f;

        if (!string.IsNullOrEmpty(targetTag))
        {
            var go = GameObject.FindGameObjectWithTag(targetTag);
            if (go) target = go.transform;
        }
    }

    void TryFindBounds(bool immediate)
    {
        if (!immediate && Time.time < _reacquireBoundsAt) return;
        _reacquireBoundsAt = Time.time + 0.75f;

        Collider2D found = null;

        var p = GameObject.Find(autoFindPrimary);
        if (p)
        {
            found = p.GetComponent<CompositeCollider2D>();
            if (!found) found = p.GetComponent<Collider2D>();
        }

        if (!found)
        {
            var f = GameObject.Find(autoFindFallback);
            if (f) found = f.GetComponent<Collider2D>();
        }

        if (!found)
        {
            var any = FindFirstObjectByType<CompositeCollider2D>(FindObjectsInactive.Include);
            if (any) found = any;
        }

        if (found) boundsCollider = found;
    }
}
