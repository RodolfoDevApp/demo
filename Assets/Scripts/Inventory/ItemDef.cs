using UnityEngine;

public enum ItemKind { Weapon, Ammo, Consumable }

[CreateAssetMenu(fileName = "Item_", menuName = "Inventory/ItemDef")]
public class ItemDef : ScriptableObject
{
    [Header("Identidad")]
    public string id;                 // ej: "pistol", "gun", "shotgun", "ammo_9mm"
    public string displayName;

    [Header("UI")]
    public Sprite icon;               // icono para el inventario

    [Header("Stacking")]
    [Min(1)] public int maxStack = 99;

    [Header("Tipo")]
    public ItemKind kind = ItemKind.Weapon;

    [Header("Consumible (si aplica)")]
    [Tooltip("Cantidad a curar al usar este ítem (solo para 'Consumable').")]
    public int healAmount = 0;

    [Header("Ammo (si aplica)")]
    [Tooltip("Identificador de calibre/munición. Ej: 'ammo_9mm'")]
    public string ammoCaliberId = "";
}
