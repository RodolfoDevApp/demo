using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class NavAgent2D : MonoBehaviour
{
    [Header("Obstaculos")]
    public LayerMask obstacleMask = ~0;

    [Header("Pathfinding")]
    public float repathInterval = 0.35f;
    public float waypointReachRadius = 0.15f;
    public float losRayPadding = 0.2f;

    [Header("Debug")]
    public bool drawPath = false;

    readonly List<Vector2> _path = new List<Vector2>(128);
    int _wpIndex = 0;
    float _repathAt = 0f;

    public Vector2 GetDirection(Vector2 from, Vector2 to)
    {
        // si hay linea de vista libre, ve directo
        Vector2 dir = to - from;
        float dist = dir.magnitude;
        if (dist < 0.0001f) return Vector2.zero;
        dir /= dist;

        if (!Physics2D.Raycast(from, dir, dist - losRayPadding, obstacleMask))
            return dir;

        // recalcular ruta cada cierto tiempo
        if (Time.time >= _repathAt || _wpIndex >= _path.Count)
        {
            _repathAt = Time.time + Mathf.Max(0.05f, repathInterval);
            if (AStarGrid2D.FindPath(from, to, _path))
                _wpIndex = 0;
            else
                _path.Clear();
        }

        if (_path.Count == 0) return dir; // fallback

        // avanzar por waypoints
        if (_wpIndex >= _path.Count) _wpIndex = _path.Count - 1;
        var wp = _path[_wpIndex];
        Vector2 toWp = wp - from;

        if (toWp.magnitude <= waypointReachRadius)
        {
            _wpIndex = Mathf.Min(_wpIndex + 1, _path.Count - 1);
            wp = _path[_wpIndex];
            toWp = wp - from;
        }

        if (toWp.sqrMagnitude < 1e-6f) return Vector2.zero;
        return toWp.normalized;
    }

    void OnDrawGizmos()
    {
        if (!drawPath || _path == null || _path.Count == 0) return;
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.8f);
        for (int i = 0; i < _path.Count - 1; i++)
            Gizmos.DrawLine(_path[i], _path[i + 1]);
    }
}
