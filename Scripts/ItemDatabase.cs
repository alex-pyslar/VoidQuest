using System.Collections.Generic;

namespace VoidQuest;

public enum ItemCategory { Weapon, Armor, Shield, Ring, Consumable, Material }

/// <summary>Static definition of an item.</summary>
public class ItemDef
{
    public string       Id          { get; }
    public string       Name        { get; }
    public ItemCategory Category    { get; }
    public string       Description { get; }
    public int          AtkBonus    { get; }
    public int          DefBonus    { get; }
    public int          MagBonus    { get; }
    public System.Action<Player> OnUse { get; }

    public ItemDef(string id, string name, ItemCategory cat, string desc = "",
                   int atk = 0, int def = 0, int mag = 0,
                   System.Action<Player> onUse = null)
    {
        Id = id; Name = name; Category = cat; Description = desc;
        AtkBonus = atk; DefBonus = def; MagBonus = mag; OnUse = onUse;
    }
}

public static class ItemDatabase
{
    public static readonly Dictionary<string, ItemDef> Items = new Dictionary<string, ItemDef>
    {
        // ══════════════════════════════════════════════════════════════════════
        // CONSUMABLES
        // ══════════════════════════════════════════════════════════════════════
        ["health_potion"] = new ItemDef("health_potion", "Health Potion", ItemCategory.Consumable,
            "Restores 40 HP.", onUse: p => p.Heal(40)),

        ["big_health_potion"] = new ItemDef("big_health_potion", "Big Health Potion", ItemCategory.Consumable,
            "Restores 80 HP.", onUse: p => p.Heal(80)),

        ["mana_potion"] = new ItemDef("mana_potion", "Mana Potion", ItemCategory.Consumable,
            "Restores 40 mana.", onUse: p => p.RestoreMana(40)),

        ["big_mana_potion"] = new ItemDef("big_mana_potion", "Big Mana Potion", ItemCategory.Consumable,
            "Restores 80 mana.", onUse: p => p.RestoreMana(80)),

        ["rejuvenation_potion"] = new ItemDef("rejuvenation_potion", "Rejuvenation Potion", ItemCategory.Consumable,
            "Restores 60 HP and 60 mana.", onUse: p => { p.Heal(60); p.RestoreMana(60); }),

        ["elixir"] = new ItemDef("elixir", "Elixir", ItemCategory.Consumable,
            "Restores 80 HP and 80 mana.", onUse: p => { p.Heal(80); p.RestoreMana(80); }),

        ["full_restore"] = new ItemDef("full_restore", "Full Restore", ItemCategory.Consumable,
            "Fully restores HP and mana.", onUse: p => { p.Heal(9999); p.RestoreMana(9999); }),

        ["void_elixir"] = new ItemDef("void_elixir", "Void Elixir", ItemCategory.Consumable,
            "Restores 150 HP and 150 mana.", onUse: p => { p.Heal(150); p.RestoreMana(150); }),

        // ══════════════════════════════════════════════════════════════════════
        // WEAPONS
        // ══════════════════════════════════════════════════════════════════════
        // Tier 1
        ["rusty_sword"]  = new ItemDef("rusty_sword",  "Rusty Sword",  ItemCategory.Weapon, "A worn blade. +5 ATK.",         atk: 5),
        ["bone_dagger"]  = new ItemDef("bone_dagger",  "Bone Dagger",  ItemCategory.Weapon, "Carved from bone. +7 ATK.",     atk: 7),
        // Tier 2
        ["iron_dagger"]  = new ItemDef("iron_dagger",  "Iron Dagger",  ItemCategory.Weapon, "Fast and sharp. +10 ATK.",      atk: 10),
        ["stone_axe"]    = new ItemDef("stone_axe",    "Stone Axe",    ItemCategory.Weapon, "Crude but heavy. +12 ATK.",     atk: 12),
        ["iron_sword"]   = new ItemDef("iron_sword",   "Iron Sword",   ItemCategory.Weapon, "Sturdy iron blade. +15 ATK.",   atk: 15),
        // Tier 3
        ["war_hammer"]   = new ItemDef("war_hammer",   "War Hammer",   ItemCategory.Weapon, "Crushes armour. +18 ATK, +3 DEF.",  atk: 18, def: 3),
        ["battle_axe"]   = new ItemDef("battle_axe",   "Battle Axe",   ItemCategory.Weapon, "Heavy chopper. +20 ATK.",       atk: 20),
        ["steel_sword"]  = new ItemDef("steel_sword",  "Steel Sword",  ItemCategory.Weapon, "Refined dark steel. +22 ATK.",  atk: 22),
        // Tier 4
        ["crystal_spear"]= new ItemDef("crystal_spear","Crystal Spear",ItemCategory.Weapon, "Crystalline lance. +22 ATK, +5 MAG.", atk: 22, mag: 5),
        ["magic_staff"]  = new ItemDef("magic_staff",  "Magic Staff",  ItemCategory.Weapon, "Arcane focus. +8 ATK, +12 MAG.",    atk: 8,  mag: 12),
        ["arcane_wand"]  = new ItemDef("arcane_wand",  "Arcane Wand",  ItemCategory.Weapon, "Crackles with power. +5 ATK, +20 MAG.", atk: 5, mag: 20),
        ["void_blade"]   = new ItemDef("void_blade",   "Void Blade",   ItemCategory.Weapon, "Void-forged. +25 ATK, +8 MAG.", atk: 25, mag: 8),
        ["cursed_blade"] = new ItemDef("cursed_blade", "Cursed Blade", ItemCategory.Weapon, "Enormous power, huge cost. +30 ATK, -5 DEF.", atk: 30, def: -5),
        // Tier 5 (crafted)
        ["soul_blade"]   = new ItemDef("soul_blade",   "Soul Blade",   ItemCategory.Weapon, "Drinks the life of the slain. +30 ATK, +15 MAG.", atk: 30, mag: 15),
        ["void_sword"]   = new ItemDef("void_sword",   "Void Sword",   ItemCategory.Weapon, "Mastery of void. +35 ATK, +10 MAG.", atk: 35, mag: 10),
        // Tier 6-7 (crafted, endgame)
        ["mithril_sword"]= new ItemDef("mithril_sword","Mithril Sword",ItemCategory.Weapon, "Light as air. +40 ATK.",        atk: 40),
        ["dragon_sword"] = new ItemDef("dragon_sword", "Dragon Sword", ItemCategory.Weapon, "Dragon-fire tempered. +45 ATK, +12 MAG.", atk: 45, mag: 12),

        // ══════════════════════════════════════════════════════════════════════
        // ARMOR
        // ══════════════════════════════════════════════════════════════════════
        // Tier 1
        ["cloth_robe"]   = new ItemDef("cloth_robe",   "Cloth Robe",   ItemCategory.Armor, "Barely any protection. +5 DEF.",  def: 5),
        ["leather_armor"]= new ItemDef("leather_armor","Leather Armor",ItemCategory.Armor, "Light protection. +10 DEF.",      def: 10),
        // Tier 2
        ["padded_armor"] = new ItemDef("padded_armor", "Padded Armor", ItemCategory.Armor, "Quilted layers. +15 DEF.",        def: 15),
        ["chainmail"]    = new ItemDef("chainmail",    "Chainmail",    ItemCategory.Armor, "Linked iron rings. +18 DEF.",     def: 18),
        // Tier 3
        ["iron_armor"]   = new ItemDef("iron_armor",   "Iron Armor",   ItemCategory.Armor, "Heavy iron plates. +25 DEF.",    def: 25),
        ["mage_robe"]    = new ItemDef("mage_robe",    "Mage Robe",    ItemCategory.Armor, "Arcane threads. +6 DEF, +12 MAG.", def: 6, mag: 12),
        // Tier 4
        ["crystal_robe"] = new ItemDef("crystal_robe", "Crystal Robe", ItemCategory.Armor, "Shimmers with magic. +8 DEF, +10 MAG.", def: 8, mag: 10),
        ["void_plate"]   = new ItemDef("void_plate",   "Void Plate",   ItemCategory.Armor, "Hardened void metal. +35 DEF.",   def: 35),
        // Tier 5 (crafted)
        ["void_robe"]    = new ItemDef("void_robe",    "Void Robe",    ItemCategory.Armor, "Woven void silk. +15 DEF, +25 MAG.", def: 15, mag: 25),
        ["shadow_armor"] = new ItemDef("shadow_armor", "Shadow Armor", ItemCategory.Armor, "Born in darkness. +30 DEF, +15 ATK.", def: 30, atk: 15),
        // Tier 6-7 (crafted)
        ["mithril_armor"]= new ItemDef("mithril_armor","Mithril Armor",ItemCategory.Armor, "Legendary light mail. +45 DEF.", def: 45),
        ["dragon_plate"] = new ItemDef("dragon_plate", "Dragon Plate", ItemCategory.Armor, "Impenetrable. +40 DEF, +10 MAG.", def: 40, mag: 10),

        // ══════════════════════════════════════════════════════════════════════
        // SHIELDS
        // ══════════════════════════════════════════════════════════════════════
        // Tier 1
        ["wooden_shield"]= new ItemDef("wooden_shield","Wooden Shield",ItemCategory.Shield, "Basic protection. +5 DEF.",     def: 5),
        ["bone_shield"]  = new ItemDef("bone_shield",  "Bone Shield",  ItemCategory.Shield, "Stitched bones. +8 DEF.",       def: 8),
        // Tier 2
        ["buckler"]      = new ItemDef("buckler",      "Buckler",      ItemCategory.Shield, "Parrying shield. +12 DEF, +3 ATK.", def: 12, atk: 3),
        ["iron_shield"]  = new ItemDef("iron_shield",  "Iron Shield",  ItemCategory.Shield, "Reliable iron buckler. +18 DEF.", def: 18),
        // Tier 3
        ["kite_shield"]  = new ItemDef("kite_shield",  "Kite Shield",  ItemCategory.Shield, "Long kite shape. +22 DEF, +5 ATK.", def: 22, atk: 5),
        ["tower_shield"] = new ItemDef("tower_shield", "Tower Shield", ItemCategory.Shield, "Covers the whole body. +28 DEF.", def: 28),
        // Tier 4
        ["crystal_shield"]= new ItemDef("crystal_shield","Crystal Shield",ItemCategory.Shield,"Refracts spells. +15 DEF, +15 MAG.", def: 15, mag: 15),
        ["void_aegis"]   = new ItemDef("void_aegis",   "Void Aegis",   ItemCategory.Shield, "Void-pulsing. +22 DEF, +8 MAG.", def: 22, mag: 8),
        // Tier 5-7 (crafted)
        ["void_barrier"] = new ItemDef("void_barrier", "Void Barrier", ItemCategory.Shield, "Wall of pure void. +30 DEF, +15 MAG.", def: 30, mag: 15),
        ["mithril_shield"]= new ItemDef("mithril_shield","Mithril Shield",ItemCategory.Shield,"Featherlight fortress. +38 DEF.", def: 38),
        ["dragon_shield"]= new ItemDef("dragon_shield","Dragon Shield",ItemCategory.Shield, "Dragon heart-scale. +45 DEF, +10 MAG.", def: 45, mag: 10),

        // ══════════════════════════════════════════════════════════════════════
        // RINGS
        // ══════════════════════════════════════════════════════════════════════
        ["power_ring"]   = new ItemDef("power_ring",   "Power Ring",   ItemCategory.Ring, "Boosts strength. +8 ATK.",       atk: 8),
        ["mage_ring"]    = new ItemDef("mage_ring",    "Mage Ring",    ItemCategory.Ring, "Amplifies spells. +15 MAG.",     mag: 15),
        ["bone_ring"]    = new ItemDef("bone_ring",    "Bone Ring",    ItemCategory.Ring, "Carved from bone. +10 DEF.",     def: 10),
        ["guardian_ring"]= new ItemDef("guardian_ring","Guardian Ring",ItemCategory.Ring, "Steadfast defence. +12 DEF.",   def: 12),
        ["speed_ring"]   = new ItemDef("speed_ring",   "Speed Ring",   ItemCategory.Ring, "Agile bonus. +5 ATK, +3 DEF.",  atk: 5, def: 3),
        ["arcane_ring"]  = new ItemDef("arcane_ring",  "Arcane Ring",  ItemCategory.Ring, "Wild power. +20 MAG, -5 DEF.",  mag: 20, def: -5),
        ["void_ring"]    = new ItemDef("void_ring",    "Void Ring",    ItemCategory.Ring, "Resonates with darkness. +8 ATK, +8 MAG.", atk: 8, mag: 8),
        ["void_lord_ring"]= new ItemDef("void_lord_ring","Void Lord Ring",ItemCategory.Ring,"Ring of the Void Lord. +15 ATK, +15 MAG.", atk: 15, mag: 15),

        // ══════════════════════════════════════════════════════════════════════
        // MATERIALS — Common
        // ══════════════════════════════════════════════════════════════════════
        ["crystal"]        = new ItemDef("crystal",        "Crystal Shard",     ItemCategory.Material, "A glowing void crystal."),
        ["void_essence"]   = new ItemDef("void_essence",   "Void Essence",      ItemCategory.Material, "Dense dark energy."),
        ["bone_fragment"]  = new ItemDef("bone_fragment",  "Bone Fragment",     ItemCategory.Material, "Dropped by armoured foes."),
        ["leather_piece"]  = new ItemDef("leather_piece",  "Leather Piece",     ItemCategory.Material, "Scraps of cured hide."),
        ["iron_ore"]       = new ItemDef("iron_ore",       "Iron Ore",          ItemCategory.Material, "Raw iron. Smelt 3 into 1 ingot."),
        ["iron_ingot"]     = new ItemDef("iron_ingot",     "Iron Ingot",        ItemCategory.Material, "Smelted iron bar."),
        ["coal"]           = new ItemDef("coal",           "Coal",              ItemCategory.Material, "Black fuel rock."),
        ["wood_plank"]     = new ItemDef("wood_plank",     "Wood Plank",        ItemCategory.Material, "Cut timber plank."),
        // Uncommon materials
        ["dark_steel_ingot"]= new ItemDef("dark_steel_ingot","Dark Steel Ingot",ItemCategory.Material, "Iron fused with void energy."),
        ["void_shard"]     = new ItemDef("void_shard",     "Void Shard",        ItemCategory.Material, "Condensed void fragment."),
        ["essence_crystal"]= new ItemDef("essence_crystal","Essence Crystal",   ItemCategory.Material, "Crystallised magic essence."),
        ["enchanted_thread"]= new ItemDef("enchanted_thread","Enchanted Thread",ItemCategory.Material, "Magically woven silk."),
        // Rare materials
        ["mithril_ore"]    = new ItemDef("mithril_ore",    "Mithril Ore",       ItemCategory.Material, "Mythic silver ore. Rare chest drop."),
        ["mithril_ingot"]  = new ItemDef("mithril_ingot",  "Mithril Ingot",     ItemCategory.Material, "Refined mithril. Endgame crafting base."),
        ["dragon_scale"]   = new ItemDef("dragon_scale",   "Dragon Scale",      ItemCategory.Material, "Drops only from Tank elites."),
    };

    public static ItemDef Get(string id)
        => Items.TryGetValue(id, out var def) ? def : null;
}
