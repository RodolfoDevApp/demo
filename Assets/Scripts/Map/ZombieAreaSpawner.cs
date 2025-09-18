using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ZombieAreaSpawner : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject zombiePrefab;

    [Header("Spawn Points")]
    public bool useChildrenAsSpawnPoints = true;
    public List<Transform> spawnPoints = new List<Transform>();

    [Header("Counts")]
    [Min(0)] public int startPerPoint = 1;
    [Min(1)] public int maxAlive = 6;

    [Header("Respawn")]
    [Min(0f)] public float respawnCooldown = 5f;

    [Header("Area Activation")]
    public bool activeOnlyIfPlayerInside = true;
    [Min(0f)] public float activationRadius = 16f;
    public string playerTag = "Player";

    // --- internos ---
    readonly List<SpawnedEnemyHandle> _alive = new();
    readonly Queue<(Transform point, float when)> _respawnQueue = new();
    readonly Stack<SpawnedEnemyHandle> _pool = new();

    Transform _player;

    void Awake()
    {
        if (useChildrenAsSpawnPoints)
        {
            spawnPoints.Clear();
            for (int i = 0; i < transform.childCount; i++)
            {
                var t = transform.GetChild(i);
                if (t) spawnPoints.Add(t);
            }
        }

        if (!_player)
        {
            var go = GameObject.FindGameObjectWithTag(playerTag);
            if (go) _player = go.transform;
            else
            {
                var ph = FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
                if (ph) _player = ph.transform;
            }
        }
    }

    void Start()
    {
        if (!zombiePrefab) { Debug.LogWarning($"{name}: Zombie Prefab no asignado"); return; }
        if (spawnPoints.Count == 0) { Debug.LogWarning($"{name}: No hay spawn points"); return; }

        // spawn inicial
        foreach (var p in spawnPoints)
        {
            for (int i = 0; i < startPerPoint; i++)
            {
                TrySpawnAt(p);
            }
        }
    }

    void Update()
    {
        if (!zombiePrefab || spawnPoints.Count == 0) return;

        // si el área es activa solo con jugador dentro
        if (activeOnlyIfPlayerInside && _player)
        {
            if (Vector2.Distance(transform.position, _player.position) > activationRadius)
                return;
        }

        // procesar cola de respawn
        while (_respawnQueue.Count > 0)
        {
            var (p, when) = _respawnQueue.Peek();
            if (Time.time < when) break; // aún no toca
            _respawnQueue.Dequeue();

            if (_alive.Count >= maxAlive) continue;
            if (!p) continue;

            TrySpawnAt(p);
        }
    }

    void TrySpawnAt(Transform point)
    {
        if (_alive.Count >= maxAlive) return;
        if (!point) return;

        SpawnedEnemyHandle handle = null;

        // reusar del pool si tenemos
        while (_pool.Count > 0 && (handle == null || !handle))
            handle = _pool.Pop();

        if (handle == null)
        {
            var go = Instantiate(zombiePrefab, point.position, Quaternion.identity);
            handle = go.GetComponent<SpawnedEnemyHandle>();
            if (!handle) handle = go.AddComponent<SpawnedEnemyHandle>();
        }

        handle.Setup(this, point);
        handle.ReviveAt(point.position);

        if (!_alive.Contains(handle)) _alive.Add(handle);
    }

    // llamado por los enemigos al morir
    public void NotifyDeath(SpawnedEnemyHandle handle, Transform atPoint)
    {
        if (handle)
        {
            _alive.Remove(handle);
            // vuelve al pool cuando se desactive completamente
            _pool.Push(handle);
        }

        // programa respawn en ese punto
        float when = Time.time + Mathf.Max(0f, respawnCooldown);
        _respawnQueue.Enqueue((atPoint, when));
    }

    void OnDrawGizmosSelected()
    {
        if (activeOnlyIfPlayerInside)
        {
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.1f, activationRadius));
        }

        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.8f);
        foreach (var p in spawnPoints)
        {
            if (!p) continue;
            Gizmos.DrawWireCube(p.position, new Vector3(0.35f, 0.35f, 0.35f));
        }
    }
}
