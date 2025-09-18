using UnityEngine;

[AddComponentMenu("Inventory/Quickbar Item User")]
public class QuickbarItemUser : MonoBehaviour
{
    [Header("Refs")]
    public InventoryRuntime inventory;      // arrastra tu InventoryRuntime
    public Damageable playerHealth;         // arrastra el Damageable del player

    [Header("ItemDefs")]
    public ItemDef medkitDef;               // arrastra el ItemDef del medkit

    [Header("Comportamiento")]
    [Tooltip("Si la vida ya está llena, no gasta el medkit.")]
    public bool dontConsumeIfFullHP = true;

    void Reset()
    {
        if (!inventory) inventory = FindFirstObjectByType<InventoryRuntime>();
        if (!playerHealth)
        {
            var ph = FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            if (ph) playerHealth = ph.GetComponent<Damageable>();
        }
    }

    // Llama esto desde tu UI de hotbar (botón del slot i) o desde input
    public void UseHotbarSlot(int index)
    {
        if (!inventory || index < 0 || index >= inventory.hotbar.Length) return;

        ref var s = ref inventory.hotbar[index];
        if (s.IsEmpty || s.amount <= 0 || !s.item) return;

        // ¿Es Medkit?
        if (s.item == medkitDef)
        {
            TryUseMedkit(ref s);
            inventory.NotifyChanged();
            return;
        }

        // Aquí puedes rutear otros consumibles si quieres.
    }

    public void UseFirstMedkitOnHotbar()
    {
        if (!inventory) return;
        for (int i = 0; i < inventory.hotbar.Length; i++)
        {
            ref var s = ref inventory.hotbar[i];
            if (!s.IsEmpty && s.item == medkitDef)
            {
                TryUseMedkit(ref s);
                inventory.NotifyChanged();
                return;
            }
        }
    }

    void TryUseMedkit(ref InventoryRuntime.Stack stack)
    {
        if (!playerHealth || !playerHealth.IsAlive) return;

        float cur = playerHealth.CurrentHP;
        float max = playerHealth.MaxHP;

        if (dontConsumeIfFullHP && cur >= max) return;

        float need = Mathf.Max(0f, max - cur);
        playerHealth.Heal(need > 0f ? need : max); // full heal

        stack.amount -= 1;
        if (stack.amount <= 0) stack = InventoryRuntime.Stack.Empty;
    }
}
