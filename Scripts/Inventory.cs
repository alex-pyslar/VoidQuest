using System;
using System.Collections.Generic;

namespace VoidQuest;

// Keep for CollectibleSphere legacy usage
public enum ItemType { Crystal, HealthPotion, Coin }

public class InventoryItem
{
    public string ItemId { get; }
    public int    Count  { get; set; }
    public ItemDef Def  => ItemDatabase.Get(ItemId);

    public InventoryItem(string itemId, int count = 1)
    {
        ItemId = itemId;
        Count  = count;
    }
}

/// <summary>
/// RPG inventory with item stacks and equipment slots.
/// </summary>
public class Inventory
{
    private readonly List<InventoryItem> _items = new();
    public IReadOnlyList<InventoryItem> Items => _items;

    // ── Equipment slots ────────────────────────────────────────────────
    public string EquippedWeapon { get; private set; }
    public string EquippedArmor  { get; private set; }
    public string EquippedShield { get; private set; }
    public string EquippedRing   { get; private set; }

    public event Action InventoryChanged;

    // ── Add / Remove ───────────────────────────────────────────────────

    public void AddItemById(string id, int count = 1)
    {
        if (ItemDatabase.Get(id) == null) return;
        var existing = _items.Find(i => i.ItemId == id);
        if (existing != null) existing.Count += count;
        else _items.Add(new InventoryItem(id, count));
        InventoryChanged?.Invoke();
    }

    /// <summary>Legacy path used by CollectibleSphere.</summary>
    public void AddItem(ItemType type, string name, int count = 1)
        => AddItemById("crystal", count);

    public bool RemoveItem(string id, int count = 1)
    {
        var item = _items.Find(i => i.ItemId == id);
        if (item == null || item.Count < count) return false;
        item.Count -= count;
        if (item.Count <= 0) _items.Remove(item);
        InventoryChanged?.Invoke();
        return true;
    }

    public int GetCount(ItemType type) => GetCount("crystal");
    public int GetCount(string id) => _items.Find(i => i.ItemId == id)?.Count ?? 0;

    // ── Equipment ──────────────────────────────────────────────────────

    /// <summary>Equips item in the correct slot, returns old item id (or null).</summary>
    public string Equip(string id)
    {
        var def = ItemDatabase.Get(id);
        if (def == null) return null;

        string old = null;
        switch (def.Category)
        {
            case ItemCategory.Weapon: old = EquippedWeapon; EquippedWeapon = id; break;
            case ItemCategory.Armor:  old = EquippedArmor;  EquippedArmor  = id; break;
            case ItemCategory.Shield: old = EquippedShield; EquippedShield = id; break;
            case ItemCategory.Ring:   old = EquippedRing;   EquippedRing   = id; break;
            default: return null;
        }
        RemoveItem(id);
        if (old != null) AddItemById(old);
        InventoryChanged?.Invoke();
        return old;
    }

    // ── Stat bonuses from all equipped gear ───────────────────────────

    public int TotalAtkBonus =>
        Bonus(EquippedWeapon, d => d.AtkBonus) + Bonus(EquippedArmor,  d => d.AtkBonus) +
        Bonus(EquippedShield, d => d.AtkBonus) + Bonus(EquippedRing,   d => d.AtkBonus);
    public int TotalDefBonus =>
        Bonus(EquippedWeapon, d => d.DefBonus) + Bonus(EquippedArmor,  d => d.DefBonus) +
        Bonus(EquippedShield, d => d.DefBonus) + Bonus(EquippedRing,   d => d.DefBonus);
    public int TotalMagBonus =>
        Bonus(EquippedWeapon, d => d.MagBonus) + Bonus(EquippedArmor,  d => d.MagBonus) +
        Bonus(EquippedShield, d => d.MagBonus) + Bonus(EquippedRing,   d => d.MagBonus);

    private static int Bonus(string id, Func<ItemDef, int> sel)
        => id != null && ItemDatabase.Get(id) is ItemDef d ? sel(d) : 0;

    // ── Unequip ────────────────────────────────────────────────────────

    /// <summary>Moves the equipped item back into the bag.</summary>
    public void Unequip(string id)
    {
        var def = ItemDatabase.Get(id);
        if (def == null) return;

        bool unequipped = false;
        switch (def.Category)
        {
            case ItemCategory.Weapon: if (EquippedWeapon == id) { EquippedWeapon = null; unequipped = true; } break;
            case ItemCategory.Armor:  if (EquippedArmor  == id) { EquippedArmor  = null; unequipped = true; } break;
            case ItemCategory.Shield: if (EquippedShield == id) { EquippedShield = null; unequipped = true; } break;
            case ItemCategory.Ring:   if (EquippedRing   == id) { EquippedRing   = null; unequipped = true; } break;
        }
        if (unequipped)
        {
            AddItemById(id);
            InventoryChanged?.Invoke();
        }
    }

    // ── Crafting ───────────────────────────────────────────────────────

    /// <summary>Attempts to craft the item with the given result id.
    /// Consumes ingredients and adds the result. Returns false with an error string on failure.</summary>
    public bool TryCraft(string resultId, out string error)
    {
        var recipe = CraftingDatabase.GetRecipe(resultId);
        if (recipe == null) { error = "Unknown recipe"; return false; }
        if (!recipe.CanCraft(this)) { error = "Not enough materials"; return false; }

        foreach (var kvp in recipe.Ingredients)
            RemoveItem(kvp.Key, kvp.Value);

        AddItemById(recipe.ResultId, recipe.ResultCount);
        error = null;
        return true;
    }
}
