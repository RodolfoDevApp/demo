using UnityEngine;

public class ZombieSpawner : MonoBehaviour
{
    public GameObject zombiePrefab;
    public int count = 3;
    public float radius = 4f;

    [ContextMenu("Spawn")]
    public void Spawn()
    {
        if (!zombiePrefab) return;
        for (int i = 0; i < count; i++)
        {
            var p = (Vector2)transform.position + Random.insideUnitCircle * radius;
            Instantiate(zombiePrefab, p, Quaternion.identity);
        }
    }
}
