using UnityEngine;

[DisallowMultipleComponent]
public class SpawnedEnemyHandle : MonoBehaviour
{
    public ZombieAreaSpawner owner { get; private set; }
    public Transform spawnPoint { get; private set; }

    Damageable dmg;
    bool subscribed;

    void Awake()
    {
        if (!dmg) dmg = GetComponent<Damageable>();
    }

    public void Setup(ZombieAreaSpawner spawner, Transform point)
    {
        owner = spawner;
        spawnPoint = point;

        if (!dmg) dmg = GetComponent<Damageable>();
        if (dmg != null && !subscribed)
        {
            dmg.onDeath.AddListener(OnEnemyDeath);
            subscribed = true;
        }
    }

    // llamado por el spawner cuando toca revivir
    public void ReviveAt(Vector3 pos)
    {
        // forzar activar y re-habilitar colisionadores
        var cols = GetComponentsInChildren<Collider2D>(true);
        foreach (var c in cols) if (c) c.enabled = true;

        transform.position = pos;

        if (!gameObject.activeSelf) gameObject.SetActive(true);

        if (!dmg) dmg = GetComponent<Damageable>();
        if (dmg != null)
            dmg.Revive(); // resetea vida y activa GO

        // si tu AI necesita algo extra, OnEnable de tu AI ya lo resetea
    }

    void OnEnemyDeath()
    {
        // aquí el Damageable normalmente hará SetActive(false) (si destroyOnDeath=false)
        // Notificamos al spawner para programar respawn.
        if (owner) owner.NotifyDeath(this, spawnPoint ? spawnPoint : transform);
    }
}
