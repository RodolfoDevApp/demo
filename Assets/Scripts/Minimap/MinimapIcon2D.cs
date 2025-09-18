using UnityEngine;

[DisallowMultipleComponent]
public class MinimapIcon2D : MonoBehaviour
{
    public Transform target;
    public string targetTag = "Player";
    public bool faceMovement = true;
    public float minSpeedToRotate = 0.001f;

    Vector3 _lastPos;
    float _reacquireAt = 0f;

    void Awake()
    {
        if (!target) TryFindTarget();
        if (target) _lastPos = target.position;
    }

    void LateUpdate()
    {
        if (!target)
        {
            if (Time.time >= _reacquireAt) TryFindTarget();
            return;
        }

        // Seguir posicion XY del target
        transform.position = new Vector3(target.position.x, target.position.y, transform.position.z);

        if (faceMovement)
        {
            Vector3 delta = target.position - _lastPos;
            if (delta.sqrMagnitude > (minSpeedToRotate * minSpeedToRotate))
            {
                float ang = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, 0f, ang);
            }
        }

        _lastPos = target.position;
    }

    void TryFindTarget()
    {
        _reacquireAt = Time.time + 0.5f;
        if (!string.IsNullOrEmpty(targetTag))
        {
            var go = GameObject.FindGameObjectWithTag(targetTag);
            if (go) target = go.transform;
        }
    }
}
