using UnityEngine;

[DisallowMultipleComponent]
public class ZombieLootDropper : MonoBehaviour
{
    public ZombieConfig config;

    public void Drop()
    {
        if (!config) return;

        // Exclusivo: primero intenta ammo, si no cae, intenta medkit, si no, nada.
        if (TryDropAmmo()) return;
        TryDropMedkit();
    }

    bool TryDropAmmo()
    {
        if (Random.value > config.dropAmmoChance) return false;

        // 1) Si hay tabla avanzada, usar pesos
        if (config.ammoTable != null && config.ammoTable.Length > 0)
        {
            float total = 0f;
            for (int i = 0; i < config.ammoTable.Length; i++)
            {
                var w = config.ammoTable[i].weight;
                if (config.ammoTable[i].prefab && w > 0f) total += w;
            }
            if (total > 0f)
            {
                float pick = Random.value * total;
                float accum = 0f;
                for (int i = 0; i < config.ammoTable.Length; i++)
                {
                    var entry = config.ammoTable[i];
                    if (!entry.prefab || entry.weight <= 0f) continue;
                    accum += entry.weight;
                    if (pick <= accum)
                    {
                        Instantiate(entry.prefab, transform.position, Quaternion.identity);
                        return true;
                    }
                }
            }
        }

        // 2) Si no hay tabla o pesos inválidos, usa el modo simple
        if (config.ammoPickupPrefab)
        {
            Instantiate(config.ammoPickupPrefab, transform.position, Quaternion.identity);
            return true;
        }

        return false;
    }

    bool TryDropMedkit()
    {
        if (Random.value > config.dropMedkitChance) return false;
        if (config.medkitPickupPrefab)
        {
            Instantiate(config.medkitPickupPrefab, transform.position, Quaternion.identity);
            return true;
        }
        return false;
    }
}
