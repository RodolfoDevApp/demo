using System;
using UnityEngine;

[DefaultExecutionOrder(-5)]
public class InventoryRuntime : MonoBehaviour
{
    // Evento: avisa a la UI que algo cambió
    public event Action Changed;
    public void NotifyChanged() => Changed?.Invoke();

    [Serializable]
    public struct Stack
    {
        public ItemDef item;
        public int amount;
        public bool IsEmpty => item == null || amount <= 0;
        public static Stack Empty => new Stack { item = null, amount = 0 };
    }

    [Header("Capacidades")]
    [Min(1)] public int hotbarSize = 6;
    [Min(1)] public int inventorySize = 16;

    [Header("Preferencias")]
    [Tooltip("Si está activo, los pickups intentan entrar primero al Inventario y luego a la Hotbar, salvo los de 'autoHotbar'.")]
    public bool pickupToInventoryFirst = true;

    [Header("Auto a Hotbar (armas, medkits, etc.)")]
    [Tooltip("Cualquier ItemDef en esta lista intentará ir primero a la Hotbar, y luego al inventario si no hay espacio.")]
    public ItemDef[] autoHotbar;

    [Header("Estado (runtime)")]
    public Stack[] hotbar;      // 1..6
    public Stack[] inventory;   // p.e. 4x4 (16)

    void Awake()
    {
        if (hotbar == null || hotbar.Length != hotbarSize) hotbar = new Stack[hotbarSize];
        if (inventory == null || inventory.Length != inventorySize) inventory = new Stack[inventorySize];
        NotifyChanged();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying) NotifyChanged();
    }
#endif

    // Accesos por ref (útiles para la UI)
    public ref Stack RefHotbar(int index) => ref hotbar[index];
    public ref Stack RefInventory(int index) => ref inventory[index];

    // Operaciones (siempre notifican)
    public void SwapHotbar(int a, int b)
    {
        (hotbar[a], hotbar[b]) = (hotbar[b], hotbar[a]);
        NotifyChanged();
    }

    public void SwapInventory(int a, int b)
    {
        (inventory[a], inventory[b]) = (inventory[b], inventory[a]);
        NotifyChanged();
    }

    public void MoveHotbarToInventory(int ha, int invb)
    {
        var t = hotbar[ha]; hotbar[ha] = inventory[invb]; inventory[invb] = t;
        NotifyChanged();
    }

    public void MoveInventoryToHotbar(int ia, int hb)
    {
        var t = inventory[ia]; inventory[ia] = hotbar[hb]; hotbar[hb] = t;
        NotifyChanged();
    }

    // Combinar stacks (misma ItemDef). No notifica por sí solo.
    public static bool TryMerge(ref Stack a, ref Stack b)
    {
        if (a.IsEmpty || b.IsEmpty || a.item != b.item) return false;
        int cap = a.item.maxStack;
        int space = Mathf.Max(0, cap - a.amount);
        if (space <= 0) return false;

        int moved = Mathf.Min(space, b.amount);
        a.amount += moved;
        b.amount -= moved;
        if (b.amount <= 0) b = Stack.Empty;
        return moved > 0;
    }

    // ------- Helpers para pickup -------
    public bool TryAddToHotbar(ItemDef item, int amount)
    {
        if (!item || amount <= 0) return false;

        // rellenar stacks existentes
        for (int i = 0; i < hotbar.Length && amount > 0; i++)
        {
            ref var s = ref hotbar[i];
            if (!s.IsEmpty && s.item == item && s.amount < item.maxStack)
            {
                int space = item.maxStack - s.amount;
                int moved = Mathf.Min(space, amount);
                s.amount += moved; amount -= moved;
            }
        }
        // ocupar vacíos
        for (int i = 0; i < hotbar.Length && amount > 0; i++)
        {
            ref var s = ref hotbar[i];
            if (s.IsEmpty)
            {
                int moved = Mathf.Min(item.maxStack, amount);
                s = new Stack { item = item, amount = moved };
                amount -= moved;
            }
        }

        bool added = amount <= 0;
        if (added) NotifyChanged();
        return added;
    }

    public bool TryAddToInventory(ItemDef item, int amount)
    {
        if (!item || amount <= 0) return false;

        // rellenar stacks existentes
        for (int i = 0; i < inventory.Length && amount > 0; i++)
        {
            ref var s = ref inventory[i];
            if (!s.IsEmpty && s.item == item && s.amount < item.maxStack)
            {
                int space = item.maxStack - s.amount;
                int moved = Mathf.Min(space, amount);
                s.amount += moved; amount -= moved;
            }
        }
        // ocupar vacíos
        for (int i = 0; i < inventory.Length && amount > 0; i++)
        {
            ref var s = ref inventory[i];
            if (s.IsEmpty)
            {
                int moved = Mathf.Min(item.maxStack, amount);
                s = new Stack { item = item, amount = moved };
                amount -= moved;
            }
        }

        bool added = amount <= 0;
        if (added) NotifyChanged();
        return added;
    }

    // --- NUEVO: ruta de pickup con preferencia autoHotbar ---
    public bool TryPickup(ItemDef item, int amount)
    {
        if (!item || amount <= 0) return false;

        bool forceToHotbar = IsInAutoHotbar(item);

        if (forceToHotbar)
        {
            if (TryAddToHotbar(item, amount)) return true;
            return TryAddToInventory(item, amount);
        }
        else
        {
            if (pickupToInventoryFirst)
            {
                if (TryAddToInventory(item, amount)) return true;
                return TryAddToHotbar(item, amount);
            }
            else
            {
                if (TryAddToHotbar(item, amount)) return true;
                return TryAddToInventory(item, amount);
            }
        }
    }

    bool IsInAutoHotbar(ItemDef item)
    {
        if (autoHotbar == null) return false;
        for (int i = 0; i < autoHotbar.Length; i++)
            if (autoHotbar[i] == item) return true;
        return false;
    }

    // ---- Remover (usar consumibles o gastar munición) ----
    public bool TryRemove(ItemDef item, int amount)
    {
        if (!item || amount <= 0) return false;

        amount = RemoveFromArray(inventory, amount, item); // prefiero inventario
        if (amount > 0) amount = RemoveFromArray(hotbar, amount, item);

        bool ok = amount <= 0;
        if (ok) NotifyChanged();
        return ok;
    }

    static int RemoveFromArray(Stack[] arr, int amount, ItemDef item)
    {
        for (int i = 0; i < arr.Length && amount > 0; i++)
        {
            ref var s = ref arr[i];
            if (!s.IsEmpty && s.item == item)
            {
                int take = Mathf.Min(s.amount, amount);
                s.amount -= take;
                amount -= take;
                if (s.amount <= 0) s = Stack.Empty;
            }
        }
        return amount;
    }

    // ---- contar cantidad total de un item (para munición) ----
    public int Count(ItemDef item)
    {
        if (!item) return 0;
        int total = 0;
        for (int i = 0; i < inventory.Length; i++)
            if (!inventory[i].IsEmpty && inventory[i].item == item) total += inventory[i].amount;
        for (int i = 0; i < hotbar.Length; i++)
            if (!hotbar[i].IsEmpty && hotbar[i].item == item) total += inventory[i].amount;
        return total;
    }
}
