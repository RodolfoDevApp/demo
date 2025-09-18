using UnityEngine;

[DisallowMultipleComponent]
public class ChaseNudge2D : MonoBehaviour
{
    public Transform player;
    public LayerMask solidMask;

    public float checkEvery = 0.6f;     // cada cuánto revisa
    public float stuckThreshold = 0.02f;// cuánto debe moverse para NO considerarse atascado
    public float sideProbe = 0.6f;      // raycast lateral para elegir lado
    public float nudgeDistance = 0.20f; // desplazamiento del empujón

    Rigidbody2D rb;
    Vector2 lastPos;
    float nextAt;

    void Awake() { rb = GetComponent<Rigidbody2D>(); }
    void OnEnable()
    {
        lastPos = rb ? rb.position : (Vector2)transform.position;
        nextAt = Time.time + checkEvery;
    }

    void Update()
    {
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
        if (Time.time < nextAt) return;
        nextAt = Time.time + checkEvery;

        Vector2 now = rb ? rb.position : (Vector2)transform.position;
        float moved = (now - lastPos).magnitude;
        lastPos = now;

        if (moved >= stuckThreshold || !player) return;

        Vector2 to = ((Vector2)player.position - now);
        if (to.sqrMagnitude < 0.0001f) return;
        to.Normalize();

        // laterales (perpendicular)
        Vector2 side = new Vector2(-to.y, to.x);
        bool leftBlocked = Physics2D.Raycast(now, -side, sideProbe, solidMask);
        bool rightBlocked = Physics2D.Raycast(now, side, sideProbe, solidMask);

        Vector2 pushDir;
        if (rightBlocked && !leftBlocked) pushDir = -side;
        else if (!rightBlocked && leftBlocked) pushDir = side;
        else pushDir = (Random.value < 0.5f) ? side : -side;

        Vector2 target = now + pushDir * nudgeDistance;
        if (rb) rb.MovePosition(target);
        else transform.position = target;
    }
}
