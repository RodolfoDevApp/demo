using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    public enum GridKind { Hotbar, Inventory }

    [Header("Refs")]
    public InventoryRuntime data;
    public MonoBehaviour weaponBridge;
    public MonoBehaviour playerHealth;

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

        ResolvePlayerHealth();
    }

    void OnDestroy()
    {
        if (data != null) data.Changed -= RefreshAll;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
            ToggleInventory(!inventoryPanel.activeSelf);

        int num = GetPressedNumberKey(1, data.hotbar.Length);
        if (num >= 0)
        {
            selectedHotbar = num;
            HighlightHotbar(selectedHotbar);
            TryEquipOrUseSelected();
        }
    }

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

    public InventoryRuntime.Stack GetStack(GridKind kind, int index)
        => kind == GridKind.Hotbar ? data.hotbar[index] : data.inventory[index];

    public void PutStack(GridKind kind, int index, InventoryRuntime.Stack s)
    {
        if (kind == GridKind.Hotbar) data.hotbar[index] = s;
        else data.inventory[index] = s;
        data.NotifyChanged();
    }

    public void SwapOrMerge(GridKind aKind, int aIndex, GridKind bKind, int bIndex)
    {
        ref var A = ref (aKind == GridKind.Hotbar ? ref data.hotbar[aIndex] : ref data.inventory[aIndex]);
        ref var B = ref (bKind == GridKind.Hotbar ? ref data.hotbar[bIndex] : ref data.inventory[bIndex]);

        if (!InventoryRuntime.TryMerge(ref A, ref B) && !InventoryRuntime.TryMerge(ref B, ref A))
            (A, B) = (B, A);

        data.NotifyChanged();
    }

    void HighlightHotbar(int idx)
    {
        for (int i = 0; i < hotbarSlots.Length; i++)
            hotbarSlots[i].SetHighlight(i == idx, selectedHotbarColor);
    }

    void TryEquipOrUseSelected()
    {
        var s = data.RefHotbar(selectedHotbar);
        if (s.IsEmpty || s.item == null)
        {
            if (weaponBridge)
            {
                var un0 = weaponBridge.GetType().GetMethod("Unequip");
                if (un0 != null) un0.Invoke(weaponBridge, null);
            }
            return;
        }

        if (s.item.kind == ItemKind.Consumable && IsHealConsumable(s))
        {
            UseMedkitFromHotbar(ref s);
            data.hotbar[selectedHotbar] = s;
            data.NotifyChanged();
            return;
        }

        if (!weaponBridge) return;

        if (s.item.kind != ItemKind.Weapon)
        {
            var un = weaponBridge.GetType().GetMethod("Unequip");
            if (un != null) un.Invoke(weaponBridge, null);
            return;
        }

        var isEq = weaponBridge.GetType().GetMethod("IsEquippedId");
        if (isEq != null && (bool)isEq.Invoke(weaponBridge, new object[] { s.item.id }))
        {
            var un = weaponBridge.GetType().GetMethod("Unequip");
            if (un != null) un.Invoke(weaponBridge, null);
            return;
        }

        var miId = weaponBridge.GetType().GetMethod("EquipByItemId");
        if (miId != null) { miId.Invoke(weaponBridge, new object[] { s.item.id }); return; }

        var mi = weaponBridge.GetType().GetMethod("Equip");
        if (mi != null) mi.Invoke(weaponBridge, new object[] { selectedHotbar + 1 });
    }

    static int GetPressedNumberKey(int min1, int maxN)
    {
        for (int n = min1; n <= maxN; n++)
        {
            KeyCode key = KeyCode.Alpha0 + n;
            if (Input.GetKeyDown(key)) return n - 1;
        }
        return -1;
    }

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
                if (IsHealConsumable(s))
                {
                    UseMedkitGeneric();
                    if (data.TryRemove(s.item, 1)) data.NotifyChanged();
                }
                break;

            case ItemKind.Ammo:
                break;
        }
    }

    bool IsHealConsumable(InventoryRuntime.Stack s)
    {
        if (s.item.kind != ItemKind.Consumable) return false;
        if (s.item.healAmount > 0) return true;
        if (!string.IsNullOrEmpty(s.item.id))
        {
            var id = s.item.id.Trim().ToLowerInvariant();
            if (id.Contains("medkit") || id.Contains("botiquin") || id.Contains("kit")) return true;
        }
        return false;
    }

    void UseMedkitFromHotbar(ref InventoryRuntime.Stack s)
    {
        UseMedkitGeneric();
        s.amount -= 1;
        if (s.amount <= 0) s = InventoryRuntime.Stack.Empty;
    }

    void UseMedkitGeneric()
    {
        if (!ResolvePlayerHealth()) return;

        var ph = playerHealth as PlayerHealth;
        if (ph != null) { ph.Heal(ph.maxHP); return; }

        var t = playerHealth.GetType();
        var mInt = t.GetMethod("Heal", new System.Type[] { typeof(int) });
        if (mInt != null) { mInt.Invoke(playerHealth, new object[] { int.MaxValue }); return; }

        var mFloat = t.GetMethod("Heal", new System.Type[] { typeof(float) });
        if (mFloat != null) { mFloat.Invoke(playerHealth, new object[] { float.MaxValue }); return; }
    }

    bool ResolvePlayerHealth()
    {
        if (playerHealth) return true;

        var ph = FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
        if (ph) { playerHealth = ph; return true; }

        var go = GameObject.FindGameObjectWithTag("Player");
        if (go)
        {
            ph = go.GetComponent<PlayerHealth>();
            if (ph) { playerHealth = ph; return true; }
        }

        return false;
    }
}
