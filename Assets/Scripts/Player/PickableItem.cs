using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PickableItem : MonoBehaviour, IPickable
{
    [Header("ItemDef + cantidad")]
    public ItemDef item;
    [Min(1)] public int amount = 1;

    [Header("Opcional")]
    public bool destroyOnCollect = true;

    void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
        // si existe la capa "Pickup", úsala
        int pickupLayer = LayerMask.NameToLayer("Pickup");
        if (pickupLayer >= 0) gameObject.layer = pickupLayer;
    }

    public void Collect(GameObject collector)
    {
        if (!item) return;

        // 1) primero intenta en el collector (Player)
        InventoryRuntime inv = null;
        if (collector)
        {
            inv = collector.GetComponentInChildren<InventoryRuntime>(true)
               ?? collector.GetComponentInParent<InventoryRuntime>();
        }

        // 2) fallback: buscar en toda la escena (p.ej. tu InventoryData)
        if (!inv)
            inv = FindFirstObjectByType<InventoryRuntime>(FindObjectsInactive.Include);

        if (!inv)
        {
            Debug.LogWarning("[PickableItem] InventoryRuntime no encontrado en la escena.");
            return;
        }

        if (inv.TryPickup(item, amount) && destroyOnCollect)
            Destroy(gameObject);
    }
}
