using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    public enum GridKind { Hotbar, Inventory }

    [Header("Refs")]
    public InventoryRuntime data;
    public MonoBehaviour weaponBridge; // script con Equip(int) o EquipByItemId(string)
    [Tooltip("Script del jugador que tenga un método Heal(int). Opcional.")]
    public MonoBehaviour playerHealth; // opcional

    [Header("UI Roots")]
    public Canvas rootCanvas;
    public GameObject inventoryPanel;
    public Button closeButton;
    public Transform hotbarContainer;
    public Transform inventoryContainer;
    public SlotUI slotPrefab;

    [Header("Visual")]
    public Color selectedHotbarColor = new(1f, 1f, 1f, 0.25f);

    SlotUI[] hotbarSlots;
    SlotUI[] inventorySlots;
    int selectedHotbar = 0;

    void Start()
    {
        if (!rootCanvas) rootCanvas = GetComponentInParent<Canvas>();

        BuildGrid(GridKind.Hotbar, hotbarContainer, data.hotbar.Length, ref hotbarSlots);
        BuildGrid(GridKind.Inventory, inventoryContainer, data.inventory.Length, ref inventorySlots);

        if (data != null) data.Changed += RefreshAll;

        RefreshAll();

        if (closeButton) closeButton.onClick.AddListener(() => ToggleInventory(false));
        ToggleInventory(false);
        HighlightHotbar(selectedHotbar);
    }

    void OnDestroy()
    {
        if (data != null) data.Changed -= RefreshAll;
    }

    void Update()
    {
        // Toggle inventario
        if (Input.GetKeyDown(KeyCode.I))
            ToggleInventory(!inventoryPanel.activeSelf);

        // Hotbar 1..6
        int num = GetPressedNumberKey(1, data.hotbar.Length);
        if (num >= 0)
        {
            selectedHotbar = num;
            HighlightHotbar(selectedHotbar);
            TryEquipSelected();
        }
    }

    // -------- build/refresh --------
    void BuildGrid(GridKind kind, Transform container, int count, ref SlotUI[] cache)
    {
        foreach (Transform c in container) Destroy(c.gameObject);
        cache = new SlotUI[count];
        for (int i = 0; i < count; i++)
        {
            var slot = Instantiate(slotPrefab, container);
            slot.Setup(this, kind, i);
            cache[i] = slot;
        }
    }

    public void RefreshAll()
    {
        for (int i = 0; i < hotbarSlots.Length; i++) hotbarSlots[i].Bind(data.RefHotbar(i));
        for (int i = 0; i < inventorySlots.Length; i++) inventorySlots[i].Bind(data.RefInventory(i));
    }

    public void ToggleInventory(bool show)
    {
        if (inventoryPanel) inventoryPanel.SetActive(show);
    }

    // -------- DnD ops (llamadas desde SlotUI) --------
    public InventoryRuntime.Stack GetStack(GridKind kind, int index)
        => kind == GridKind.Hotbar ? data.hotbar[index] : data.inventory[index];

    public void PutStack(GridKind kind, int index, InventoryRuntime.Stack s)
    {
        if (kind == GridKind.Hotbar) data.hotbar[index] = s;
        else data.inventory[index] = s;
        data.NotifyChanged(); // <- refresca UI
    }

    public void SwapOrMerge(GridKind aKind, int aIndex, GridKind bKind, int bIndex)
    {
        ref var A = ref (aKind == GridKind.Hotbar ? ref data.hotbar[aIndex] : ref data.inventory[aIndex]);
        ref var B = ref (bKind == GridKind.Hotbar ? ref data.hotbar[bIndex] : ref data.inventory[bIndex]);

        if (!InventoryRuntime.TryMerge(ref A, ref B) && !InventoryRuntime.TryMerge(ref B, ref A))
            (A, B) = (B, A);

        data.NotifyChanged(); // <- refresca UI
    }

    // -------- hotbar selection / weapon bridge --------
    void HighlightHotbar(int idx)
    {
        for (int i = 0; i < hotbarSlots.Length; i++)
            hotbarSlots[i].SetHighlight(i == idx, selectedHotbarColor);
    }

    void TryEquipSelected()
    {
        if (!weaponBridge) return;

        var s = data.RefHotbar(selectedHotbar);
        // Slot vacío o no arma -> manos
        if (s.IsEmpty || s.item == null || s.item.kind != ItemKind.Weapon)
        {
            var un = weaponBridge.GetType().GetMethod("Unequip");
            if (un != null) un.Invoke(weaponBridge, null);
            return;
        }

        // Si ya es la equipada -> toggle a manos
        var isEq = weaponBridge.GetType().GetMethod("IsEquippedId");
        if (isEq != null && (bool)isEq.Invoke(weaponBridge, new object[] { s.item.id }))
        {
            var un = weaponBridge.GetType().GetMethod("Unequip");
            if (un != null) un.Invoke(weaponBridge, null);
            return;
        }

        // Equipar por id
        var miId = weaponBridge.GetType().GetMethod("EquipByItemId");
        if (miId != null) { miId.Invoke(weaponBridge, new object[] { s.item.id }); return; }

        // (fallback legacy solo si no hay EquipByItemId)
        var mi = weaponBridge.GetType().GetMethod("Equip");
        if (mi != null) mi.Invoke(weaponBridge, new object[] { selectedHotbar + 1 });
    }


    static int GetPressedNumberKey(int min1, int maxN)
    {
        for (int n = min1; n <= maxN; n++)
        {
            KeyCode key = KeyCode.Alpha0 + n; // Alpha1..Alpha6
            if (Input.GetKeyDown(key)) return n - 1;
        }
        return -1;
    }

    // -------- Doble click para usar/equipar --------
    public void OnSlotDoubleClick(GridKind kind, int index)
    {
        var s = GetStack(kind, index);
        if (s.IsEmpty || !s.item) return;

        switch (s.item.kind)
        {
            case ItemKind.Weapon:
                if (!weaponBridge) return;
                var miId = weaponBridge.GetType().GetMethod("EquipByItemId");
                if (miId != null) miId.Invoke(weaponBridge, new object[] { s.item.id });
                else
                {
                    var mi = weaponBridge.GetType().GetMethod("Equip");
                    if (mi != null && kind == GridKind.Hotbar) mi.Invoke(weaponBridge, new object[] { index + 1 });
                }
                break;

            case ItemKind.Consumable:
                if (data.TryRemove(s.item, 1))
                {
                    if (playerHealth)
                    {
                        var heal = playerHealth.GetType().GetMethod("Heal");
                        if (heal != null) heal.Invoke(playerHealth, new object[] { s.item.healAmount });
                    }
                }
                break;

            case ItemKind.Ammo:
                // Nada (se usan con Reload)
                break;
        }
    }
}
