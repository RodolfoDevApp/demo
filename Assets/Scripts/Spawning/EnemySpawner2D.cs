using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemySpawner2D : MonoBehaviour
{
    [System.Serializable]
    public struct Entry
    {
        public GameObject prefab;
        [Min(0)] public int count;
        [Min(0f)] public float scatterRadius;
    }

    [Header("Entries (grupos por tipo)")]
    public List<Entry> entries = new List<Entry>();

    [Header("Cuándo spawnear")]
    public bool spawnOnStart = true;
    public bool spawnOnPlayerEnter = false;

    [Header("Zona de activación (si spawnOnPlayerEnter)")]
    public float triggerRadius = 6f;
    public LayerMask playerMask;

    [Header("Reutilización (trigger)")]
    public bool oneShot = true;                // solo controla el "enter" del trigger

    [Header("Respawn automático")]
    public bool respawnEnabled = false;        // ACTÍVALO para reaparición
    [Min(0f)] public float respawnDelay = 4f;  // segundos tras eliminar a todos
    public bool respawnOnlyIfPlayerInside = true; // respawn solo si el player está dentro de la zona
    public int respawnCap = 0;                 // 0 = infinito; >0 = máximo número de respawns

    [Header("Organización")]
    public Transform parentForSpawned;

    // ---- internos ----
    readonly List<GameObject> _spawned = new List<GameObject>();
    CircleCollider2D _trigger;
    bool _hasSpawned = false;
    bool _playerInside = false;
    float _nextRespawnAt = -1f;
    int _respawnsDone = 0;

    void Reset()
    {
        if (playerMask.value == 0)
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            playerMask = (playerLayer >= 0) ? (1 << playerLayer) : ~0;
        }
    }

    void OnEnable()
    {
        if (spawnOnPlayerEnter)
            EnsureTrigger();

        if (spawnOnStart && !_hasSpawned)
            SpawnAll();
    }

    void Update()
    {
        // Limpia referencias nulas del listado
        CleanupSpawnedList();

        if (!respawnEnabled) return;

        // ¿Quedan vivos?
        int alive = CountAlive();
        if (alive > 0) { _nextRespawnAt = -1f; return; }

        // Ya no queda nadie y ya hubo al menos un spawn
        if (!_hasSpawned) return;

        // Respawn limitado
        if (respawnCap > 0 && _respawnsDone >= respawnCap) return;

        // Si requiere que el player esté dentro, verifícalo (solo relevante si usamos trigger)
        if (respawnOnlyIfPlayerInside && spawnOnPlayerEnter && !_playerInside) return;

        // Programa el respawn si no está programado
        if (_nextRespawnAt < 0f)
        {
            _nextRespawnAt = Time.time + Mathf.Max(0f, respawnDelay);
        }

        // Ejecuta cuando toca
        if (Time.time >= _nextRespawnAt)
        {
            _nextRespawnAt = -1f;
            SpawnAll();
            _respawnsDone++;
        }
    }

    void EnsureTrigger()
    {
        if (!_trigger) _trigger = gameObject.GetComponent<CircleCollider2D>();
        if (!_trigger) _trigger = gameObject.AddComponent<CircleCollider2D>();
        _trigger.isTrigger = true;
        _trigger.radius = Mathf.Max(0.1f, triggerRadius);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & playerMask.value) == 0) return;
        _playerInside = true;

        if (spawnOnPlayerEnter)
        {
            if (!_hasSpawned) SpawnAll();
            else if (!oneShot && CountAlive() == 0) // reactivar por entrada si así lo quieres
                SpawnAll();
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & playerMask.value) == 0) return;
        _playerInside = false;
    }

    public void SpawnAll()
    {
        _hasSpawned = true;

        foreach (var e in entries)
        {
            if (!e.prefab || e.count <= 0) continue;

            for (int i = 0; i < e.count; i++)
            {
                Vector2 p = (Vector2)transform.position;
                if (e.scatterRadius > 0f)
                {
                    var r = Random.insideUnitCircle * e.scatterRadius;
                    p += r;
                }

                var go = Instantiate(e.prefab, p, Quaternion.identity, parentForSpawned ? parentForSpawned : null);
                _spawned.Add(go);
            }
        }
    }

    int CountAlive()
    {
        int alive = 0;
        for (int i = 0; i < _spawned.Count; i++)
        {
            var go = _spawned[i];
            if (!go) continue;

            // Si tienen Damageable, usa su estado real
            var dmg = go.GetComponent<Damageable>();
            if (dmg != null)
            {
                if (dmg.IsAlive) alive++;
            }
            else
            {
                // fallback: activo en jerarquía
                if (go.activeInHierarchy) alive++;
            }
        }
        return alive;
    }

    void CleanupSpawnedList()
    {
        for (int i = _spawned.Count - 1; i >= 0; i--)
        {
            if (_spawned[i] == null) _spawned.RemoveAt(i);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        if (spawnOnPlayerEnter)
        {
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.1f, triggerRadius));
        }

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
        foreach (var e in entries)
        {
            if (e.count > 0 && e.scatterRadius > 0f)
            {
                Gizmos.DrawWireSphere(transform.position, e.scatterRadius);
            }
        }
    }
}
