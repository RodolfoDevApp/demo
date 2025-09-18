using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Tilemaps;

[AddComponentMenu("Spawning/Wave Director 2D (Safe AnyCollider + Tilemap)")]
public class WaveDirector2D : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;

    [Header("Prefabs por tipo")]
    public GameObject zombieAxePrefab;
    public GameObject zombieDaggerPrefab;
    public GameObject zombieBigPrefab;

    [Header("Spawn alrededor del player")]
    public float minRadius = 8f;
    public float maxRadius = 12f;
    public Transform parentForSpawned;

    [Header("Chequeo fisico (fallback)")]
    public float spawnClearRadius = 0.28f;
    public Vector2 spawnClearBox = new(0.50f, 0.50f);
    public int maxTriesPerEnemy = 30;

    [Header("Tilemap de colision")]
    public Tilemap collidersTilemap;
    public int searchFreeCellsRadius = 8;

    [Header("Capas solidas (OBLIGATORIO)")]
    [Tooltip("Todo lo que NO se debe atravesar: muros, props, bordes, etc.")]
    public LayerMask solidMask = ~0;

    [Header("Oleadas")]
    public float timeBetweenWaves = 10f;
    public int baseCount = 5;
    public int addPerWave = 3;
    public int maxAliveCap = 50;

    [Header("Escalado 5+")]
    public float hpScalePerWave = 0.20f;
    public float dmgScalePerWave = 0.10f;

    [Header("Velocidad extra")]
    public float baseSpeedMultiplier = 1.20f;
    public float speedScalePerWave = 0.05f;

    [Header("Forzar persecucion")]
    public bool forceAggroOnSpawn = true;
    public float aggroHoldSecondsOverride = 999f;

    [Header("Debug")]
    public bool drawGizmos = true;
    public Color gizmoColor = new(0.3f, 1f, 0.6f, 0.35f);

    // internos
    int wave = 0;
    readonly List<GameObject> alive = new();
    static readonly Collider2D[] _buf = new Collider2D[32];

    void Awake()
    {
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
        if (solidMask == 0) solidMask = DefaultSolidMask();
    }

    void OnEnable() { StartCoroutine(WaveLoop()); }

    IEnumerator WaveLoop()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, timeBetweenWaves));
        while (true)
        {
            wave++;
            SpawnWave(wave);

            while (true)
            {
                int a = CountAlive();
                if (a == 0 || a > maxAliveCap) break;
                yield return null;
            }

            yield return new WaitForSeconds(Mathf.Max(0f, timeBetweenWaves));
        }
    }

    void SpawnWave(int waveIndex)
    {
        int total = Mathf.Max(1, baseCount + (waveIndex - 1) * Mathf.Max(0, addPerWave));

        int nAxe = 0, nDagger = 0, nBig = 0;
        if (waveIndex == 1) nAxe = total;
        else if (waveIndex == 2) nDagger = total;
        else if (waveIndex == 3) nBig = total;
        else if (waveIndex == 4) { nAxe = Mathf.CeilToInt(total * 0.5f); nDagger = total - nAxe; }
        else { nBig = Mathf.CeilToInt(total * 0.4f); nAxe = Mathf.CeilToInt(total * 0.3f); nDagger = total - nBig - nAxe; }

        float hpMult = 1f, dmgMult = 1f, spdMult = baseSpeedMultiplier;
        if (waveIndex >= 5)
        {
            int extra = waveIndex - 4;
            hpMult = 1f + hpScalePerWave * extra;
            dmgMult = 1f + dmgScalePerWave * extra;
            spdMult *= (1f + speedScalePerWave * extra);
        }

        SpawnType(zombieAxePrefab, nAxe, hpMult, dmgMult, spdMult);
        SpawnType(zombieDaggerPrefab, nDagger, hpMult, dmgMult, spdMult);
        SpawnType(zombieBigPrefab, nBig, hpMult, dmgMult, spdMult);
    }

    void SpawnType(GameObject prefab, int count, float hpMult, float dmgMult, float spdMult)
    {
        if (!prefab || count <= 0 || !player) return;

        Vector2 checkBox; float checkRad;
        GetPrefabCheckSize(prefab, out checkBox, out checkRad);

        for (int i = 0; i < count; i++)
        {
            if (!TryFindValidSpawn(checkBox, checkRad, out var pos))
                continue;

            var go = Instantiate(prefab, pos, Quaternion.identity, parentForSpawned ? parentForSpawned : null);
            alive.Add(go);
            HookDeath(go);

            AssignTargetIfPossible(go, player);
            if (forceAggroOnSpawn) ForceAggro(go, player);
            ForceChaseState(go);
            ApplyWaveTuning(go, hpMult, dmgMult, spdMult, aggroHoldSecondsOverride);

            // agente de pathfinding (opcional, si ya existe no duplica)
            if (!go.GetComponent<NavAgent2D>())
            {
                var agent = go.AddComponent<NavAgent2D>();
                agent.obstacleMask = solidMask;
            }

            ResolvePenetrations(go, 12, 0.02f);
        }
    }

    LayerMask DefaultSolidMask()
    {
        int m = ~0;
        int enemy = LayerMask.NameToLayer("Enemy");
        int playerL = LayerMask.NameToLayer("Player");
        int pickup = LayerMask.NameToLayer("Pickup");
        if (enemy >= 0) m &= ~(1 << enemy);
        if (playerL >= 0) m &= ~(1 << playerL);
        if (pickup >= 0) m &= ~(1 << pickup);
        return m;
    }

    void GetPrefabCheckSize(GameObject prefab, out Vector2 box, out float rad)
    {
        box = spawnClearBox; rad = spawnClearRadius;
        var col = prefab.GetComponentInChildren<Collider2D>();
        if (!col) return;
        var s3 = col.bounds.size;
        var s = new Vector2(s3.x, s3.y);
        if (s.x > 0.001f && s.y > 0.001f)
        {
            box = s + new Vector2(0.02f, 0.02f);
            rad = Mathf.Max(s.x, s.y) * 0.5f + 0.02f;
        }
    }

    bool TryFindValidSpawn(Vector2 box, float rad, out Vector2 pos)
    {
        pos = Vector2.zero;
        if (!player) return false;

        for (int t = 0; t < Mathf.Max(1, maxTriesPerEnemy); t++)
        {
            Vector2 candidate = RandomPointAroundPlayer();

            // si hay muro entre player y candidato, recoloca justo antes del muro
            Vector2 dir = candidate - (Vector2)player.position;
            float dist = dir.magnitude;
            if (dist > 0.001f)
            {
                var hit = Physics2D.Raycast((Vector2)player.position, dir.normalized, dist, solidMask);
                if (hit.collider)
                    candidate = hit.point - dir.normalized * (rad + 0.06f);
            }

            // si la celda de tilemap esta bloqueada, busca celda libre cercana
            if (IsBlockedByTile(candidate) && TryFindNearestFreeCell(candidate, out var freed))
                candidate = freed;

            // checar punto, circulo y caja usando SOLO solidMask
            if (Physics2D.OverlapPoint(candidate, solidMask)) continue;
            if (Physics2D.OverlapCircle(candidate, rad, solidMask)) continue;
            if (Physics2D.OverlapBox(candidate, box, 0f, solidMask)) continue;

            pos = candidate;
            return true;
        }
        return false;
    }

    Vector2 RandomPointAroundPlayer()
    {
        float r = Random.Range(minRadius, maxRadius);
        float ang = Random.Range(0f, Mathf.PI * 2f);
        Vector2 center = (Vector2)player.position;
        return center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
    }

    bool IsBlockedByTile(Vector2 world)
    {
        if (!collidersTilemap) return false;
        var cell = collidersTilemap.WorldToCell(world);
        return collidersTilemap.HasTile(cell);
    }

    bool TryFindNearestFreeCell(Vector2 fromWorld, out Vector2 freeWorld)
    {
        freeWorld = fromWorld;
        if (!collidersTilemap) return false;

        Vector3Int c0 = collidersTilemap.WorldToCell(fromWorld);
        if (!collidersTilemap.HasTile(c0))
        {
            freeWorld = (Vector2)collidersTilemap.GetCellCenterWorld(c0);
            // validar sin solidos fisicos
            if (!Physics2D.OverlapBox(freeWorld, spawnClearBox, 0f, solidMask) &&
                !Physics2D.OverlapCircle(freeWorld, spawnClearRadius, solidMask))
                return true;
        }

        int R = Mathf.Max(1, searchFreeCellsRadius);
        for (int r = 1; r <= R; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                int dy = r;
                if (TryCell(c0 + new Vector3Int(dx, dy, 0), out freeWorld)) return true;
                if (TryCell(c0 + new Vector3Int(dx, -dy, 0), out freeWorld)) return true;
            }
            for (int dy = -r + 1; dy <= r - 1; dy++)
            {
                int dx = r;
                if (TryCell(c0 + new Vector3Int(dx, dy, 0), out freeWorld)) return true;
                if (TryCell(c0 + new Vector3Int(-dx, dy, 0), out freeWorld)) return true;
            }
        }
        return false;

        bool TryCell(Vector3Int cell, out Vector2 worldOut)
        {
            worldOut = default;
            if (collidersTilemap.HasTile(cell)) return false;
            Vector2 w = (Vector2)collidersTilemap.GetCellCenterWorld(cell);

            if (Physics2D.OverlapCircle(w, spawnClearRadius, solidMask)) return false;
            if (Physics2D.OverlapBox(w, spawnClearBox, 0f, solidMask)) return false;

            worldOut = w;
            return true;
        }
    }

    void ResolvePenetrations(GameObject go, int iterations, float pad)
    {
        var self = go ? go.GetComponentInChildren<Collider2D>() : null;
        if (!self) return;

        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = solidMask };

        for (int it = 0; it < iterations; it++)
        {
            int n = self.Overlap(filter, _buf);
            if (n == 0) break;

            Vector2 total = Vector2.zero; int cnt = 0;
            for (int i = 0; i < n; i++)
            {
                var other = _buf[i];
                if (!other) continue;

                var dist = Physics2D.Distance(self, other);
                if (!dist.isOverlapped) continue;

                float push = -dist.distance + pad;
                total += dist.normal * push;
                cnt++;
            }
            if (cnt == 0) break;
            go.transform.position = (Vector2)go.transform.position + (total / cnt);
        }
    }

    int CountAlive()
    {
        for (int i = alive.Count - 1; i >= 0; i--)
            if (!alive[i]) alive.RemoveAt(i);
        return alive.Count;
    }

    void HookDeath(GameObject go)
    {
        var dmg = go.GetComponent<Damageable>();
        if (dmg != null) dmg.onDeath.AddListener(() => { if (go) alive.Remove(go); });
        else StartCoroutine(RemoveWhenGone(go));
    }

    IEnumerator RemoveWhenGone(GameObject go) { while (go) yield return null; alive.Remove(go); }

    static void AssignTargetIfPossible(GameObject go, Transform target)
    {
        if (!go || !target) return;
        var comps = go.GetComponentsInChildren<Component>(true);
        foreach (var c in comps)
        {
            if (!c) continue;
            var f = c.GetType().GetField("player", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(Transform))
                f.SetValue(c, target);
        }
    }

    static void ForceAggro(GameObject go, Transform player)
    {
        if (!go || !player) return;
        var comps = go.GetComponentsInChildren<Component>(true);
        foreach (var c in comps)
        {
            var t = c.GetType();
            var fAggro = t.GetField("hasAggro", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var fSaw = t.GetField("sawPlayer", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var fAggroUntil = t.GetField("aggroUntil", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var fLastPos = t.GetField("lastKnownPlayerPos", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            if (fAggro != null) fAggro.SetValue(c, true);
            if (fSaw != null) fSaw.SetValue(c, true);
            if (fAggroUntil != null) fAggroUntil.SetValue(c, Time.time + 999f);
            if (fLastPos != null) fLastPos.SetValue(c, (Vector2)player.position);
        }
    }

    static void ForceChaseState(GameObject go)
    {
        var comps = go.GetComponentsInChildren<Component>(true);
        foreach (var c in comps)
        {
            var fState = c.GetType().GetField("state", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (fState == null) continue;
            var enumType = fState.FieldType;
            if (!enumType.IsEnum) continue;
            try
            {
                var chaseVal = System.Enum.Parse(enumType, "Chase");
                fState.SetValue(c, chaseVal);
            }
            catch { }
        }
    }

    static void ApplyWaveTuning(GameObject go, float hpMult, float dmgMult, float spdMult, float aggroHoldOverride)
    {
        if (!go) return;

        var dmgbl = go.GetComponent<Damageable>();
        if (dmgbl != null && hpMult > 1f) TryScaleDamageableHP(dmgbl, hpMult);

        var contact = go.GetComponent<ContactDamage2D>();
        if (contact && dmgMult > 1f) contact.damage = Mathf.Max(0.01f, contact.damage * dmgMult);

        var anyComps = go.GetComponentsInChildren<Component>(true);
        foreach (var c in anyComps)
        {
            var cfgField = c.GetType().GetField("config", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (cfgField != null)
            {
                var cfg = cfgField.GetValue(c) as ScriptableObject;
                if (cfg)
                {
                    var clone = ScriptableObject.Instantiate(cfg);
                    ScaleFieldIfExists(clone, "attackDamageA", dmgMult);
                    ScaleFieldIfExists(clone, "attackDamageB", dmgMult);
                    ScaleFieldIfExists(clone, "hpBase", hpMult);
                    ScaleFieldIfExists(clone, "moveSpeed", spdMult);
                    ScaleFieldIfExists(clone, "dashSpeed", spdMult);
                    SetFieldIfExists(clone, "aggroHoldSeconds", aggroHoldOverride);
                    cfgField.SetValue(c, clone);
                }
            }
        }
    }

    static void TryScaleDamageableHP(object damageable, float mult)
    {
        string[] maxNames = { "maxHP", "maxHealth", "MaxHP", "HPMax", "hpMax" };
        string[] curNames = { "hp", "currentHP", "currentHealth", "HP", "health" };

        var t = damageable.GetType();
        FieldInfo fMax = null, fCur = null;

        foreach (var n in maxNames) { fMax = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (fMax != null) break; }
        foreach (var n in curNames) { fCur = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (fCur != null) break; }

        if (fMax != null)
        {
            if (fMax.FieldType == typeof(int))
            {
                int old = (int)fMax.GetValue(damageable);
                int nw = Mathf.Max(1, Mathf.RoundToInt(old * mult));
                fMax.SetValue(damageable, nw);
                if (fCur != null && fCur.FieldType == typeof(int))
                    fCur.SetValue(damageable, Mathf.Min(nw, (int)fCur.GetValue(damageable)));
            }
            else if (fMax.FieldType == typeof(float))
            {
                float old = (float)fMax.GetValue(damageable);
                float nw = Mathf.Max(1f, old * mult);
                fMax.SetValue(damageable, nw);
                if (fCur != null && fCur.FieldType == typeof(float))
                    fCur.SetValue(damageable, Mathf.Min(nw, (float)fCur.GetValue(damageable)));
            }
        }
    }

    static void ScaleFieldIfExists(object obj, string field, float mult)
    {
        var f = obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f == null) return;

        if (f.FieldType == typeof(int))
        {
            int v = (int)f.GetValue(obj);
            f.SetValue(obj, Mathf.RoundToInt(Mathf.Max(0, v * mult)));
        }
        else if (f.FieldType == typeof(float))
        {
            float v = (float)f.GetValue(obj);
            f.SetValue(obj, Mathf.Max(0f, v * mult));
        }
    }

    static void SetFieldIfExists(object obj, string field, float value)
    {
        var f = obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f == null) return;
        if (f.FieldType == typeof(float)) f.SetValue(obj, value);
        else if (f.FieldType == typeof(int)) f.SetValue(obj, Mathf.RoundToInt(value));
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos || !player) return;
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(player.position, minRadius);
        Gizmos.DrawWireSphere(player.position, maxRadius);
    }
}
