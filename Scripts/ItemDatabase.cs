using System.Collections.Generic;

namespace VoidQuest;

public enum ItemCategory { Weapon, Armor, Shield, Ring, Amulet, Consumable, Material }

/// <summary>
/// Static definition of an item.
/// Instances are created by DataLoader from Data/items.json.
/// Add or modify items by editing items.json — no C# changes needed.
/// </summary>
public class ItemDef
{
    public string       Id          { get; init; }
    /// <summary>English name (used as display name when language is EN).</summary>
    public string       Name        { get; init; }
    /// <summary>Russian name. May be null/empty — falls back to Name.</summary>
    public string       NameRu      { get; init; }
    public ItemCategory Category    { get; init; }
    /// <summary>English description shown in item tooltip.</summary>
    public string       Description { get; init; }
    /// <summary>Russian description. Falls back to Description if empty.</summary>
    public string       DescRu      { get; init; }
    public int          AtkBonus    { get; init; }
    public int          DefBonus    { get; init; }
    public int          MagBonus    { get; init; }
    /// <summary>Ability id granted while this item is equipped (amulets only).</summary>
    public string       AbilityId   { get; init; }
    /// <summary>Effect applied when the player uses this item.</summary>
    public System.Action<Player> OnUse { get; init; }
}

/// <summary>
/// Runtime registry of all ItemDef records.
/// Populated exclusively by DataLoader.EnsureLoaded().
/// </summary>
public static class ItemDatabase
{
    public static readonly Dictionary<string, ItemDef> Items = new();

    public static ItemDef Get(string id)
        => id != null && Items.TryGetValue(id, out var def) ? def : null;
}
