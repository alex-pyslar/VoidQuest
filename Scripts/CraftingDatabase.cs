using System.Collections.Generic;

namespace VoidQuest;

public class CraftingRecipe
{
    public string ResultId    { get; }
    public int    ResultCount { get; }
    public Dictionary<string, int> Ingredients { get; }

    public CraftingRecipe(string resultId, int resultCount, Dictionary<string, int> ingredients)
    {
        ResultId    = resultId;
        ResultCount = resultCount;
        Ingredients = ingredients;
    }

    public bool CanCraft(Inventory inv)
    {
        foreach (var kvp in Ingredients)
            if (inv.GetCount(kvp.Key) < kvp.Value) return false;
        return true;
    }
}

public static class CraftingDatabase
{
    public static readonly List<CraftingRecipe> Recipes = new List<CraftingRecipe>
    {
        // ── Materials ─────────────────────────────────────────────────────────
        new CraftingRecipe("iron_ingot",       1, new Dictionary<string, int> { ["iron_ore"]      = 3 }),
        new CraftingRecipe("dark_steel_ingot", 1, new Dictionary<string, int> { ["iron_ingot"]    = 2, ["void_essence"]   = 1 }),
        new CraftingRecipe("mithril_ingot",    1, new Dictionary<string, int> { ["mithril_ore"]   = 3 }),
        new CraftingRecipe("essence_crystal",  1, new Dictionary<string, int> { ["crystal"]       = 4, ["void_essence"]   = 1 }),
        new CraftingRecipe("enchanted_thread", 1, new Dictionary<string, int> { ["leather_piece"] = 3, ["crystal"]        = 2 }),
        new CraftingRecipe("void_shard",       2, new Dictionary<string, int> { ["void_essence"]  = 3, ["crystal"]        = 2 }),

        // ── Consumables ───────────────────────────────────────────────────────
        new CraftingRecipe("health_potion",      2, new Dictionary<string, int> { ["crystal"]       = 2, ["leather_piece"]  = 1 }),
        new CraftingRecipe("mana_potion",        2, new Dictionary<string, int> { ["crystal"]       = 2, ["void_essence"]   = 1 }),
        new CraftingRecipe("big_health_potion",  1, new Dictionary<string, int> { ["health_potion"] = 3 }),
        new CraftingRecipe("big_mana_potion",    1, new Dictionary<string, int> { ["mana_potion"]   = 3 }),
        new CraftingRecipe("rejuvenation_potion",1, new Dictionary<string, int> { ["health_potion"] = 1, ["mana_potion"]   = 1, ["crystal"]  = 1 }),
        new CraftingRecipe("elixir",             1, new Dictionary<string, int> { ["health_potion"] = 2, ["mana_potion"]   = 2 }),
        new CraftingRecipe("full_restore",       1, new Dictionary<string, int> { ["elixir"]        = 2, ["essence_crystal"]= 1 }),
        new CraftingRecipe("void_elixir",        1, new Dictionary<string, int> { ["elixir"]        = 1, ["void_shard"]    = 2 }),

        // ── Weapons (Tier 2) ──────────────────────────────────────────────────
        new CraftingRecipe("iron_dagger",  1, new Dictionary<string, int> { ["iron_ingot"]    = 1, ["leather_piece"]  = 1 }),
        new CraftingRecipe("iron_sword",   1, new Dictionary<string, int> { ["iron_ingot"]    = 2, ["leather_piece"]  = 1 }),
        new CraftingRecipe("war_hammer",   1, new Dictionary<string, int> { ["iron_ingot"]    = 3, ["bone_fragment"]  = 2 }),

        // ── Weapons (Tier 3) ──────────────────────────────────────────────────
        new CraftingRecipe("steel_sword",  1, new Dictionary<string, int> { ["dark_steel_ingot"] = 2, ["leather_piece"] = 1 }),
        new CraftingRecipe("battle_axe",   1, new Dictionary<string, int> { ["dark_steel_ingot"] = 2, ["iron_ingot"]   = 1 }),
        new CraftingRecipe("magic_staff",  1, new Dictionary<string, int> { ["iron_ingot"]       = 1, ["crystal"]      = 4, ["enchanted_thread"] = 1 }),
        new CraftingRecipe("arcane_wand",  1, new Dictionary<string, int> { ["crystal"]          = 5, ["enchanted_thread"] = 2 }),

        // ── Weapons (Tier 4-5) ────────────────────────────────────────────────
        new CraftingRecipe("soul_blade",   1, new Dictionary<string, int> { ["void_blade"]       = 1, ["bone_fragment"] = 3, ["void_essence"]     = 2 }),
        new CraftingRecipe("void_sword",   1, new Dictionary<string, int> { ["soul_blade"]       = 1, ["dark_steel_ingot"] = 2, ["essence_crystal"] = 1 }),
        new CraftingRecipe("crystal_spear",1, new Dictionary<string, int> { ["iron_ingot"]       = 2, ["crystal"]      = 6, ["essence_crystal"]  = 1 }),
        new CraftingRecipe("cursed_blade", 1, new Dictionary<string, int> { ["void_blade"]       = 1, ["bone_fragment"] = 5, ["void_shard"]       = 2 }),

        // ── Weapons (Tier 6-7 Mithril/Dragon) ────────────────────────────────
        new CraftingRecipe("mithril_sword",1, new Dictionary<string, int> { ["mithril_ingot"]    = 3, ["leather_piece"] = 1 }),
        new CraftingRecipe("dragon_sword", 1, new Dictionary<string, int> { ["mithril_sword"]    = 1, ["dragon_scale"]  = 1, ["essence_crystal"]  = 1 }),

        // ── Armor (Tier 2-3) ──────────────────────────────────────────────────
        new CraftingRecipe("padded_armor", 1, new Dictionary<string, int> { ["leather_piece"]   = 4, ["crystal"]       = 1 }),
        new CraftingRecipe("chainmail",    1, new Dictionary<string, int> { ["iron_ingot"]      = 3, ["leather_piece"] = 1 }),
        new CraftingRecipe("iron_armor",   1, new Dictionary<string, int> { ["iron_ingot"]      = 4 }),
        new CraftingRecipe("mage_robe",    1, new Dictionary<string, int> { ["leather_piece"]   = 2, ["crystal"]       = 4, ["enchanted_thread"] = 2 }),

        // ── Armor (Tier 4-5) ──────────────────────────────────────────────────
        new CraftingRecipe("crystal_robe", 1, new Dictionary<string, int> { ["mage_robe"]       = 1, ["crystal"]       = 6, ["essence_crystal"]  = 1 }),
        new CraftingRecipe("void_plate",   1, new Dictionary<string, int> { ["iron_armor"]      = 1, ["dark_steel_ingot"] = 2, ["void_essence"]  = 2 }),
        new CraftingRecipe("shadow_armor", 1, new Dictionary<string, int> { ["iron_armor"]      = 1, ["dark_steel_ingot"] = 2, ["void_shard"]    = 2 }),
        new CraftingRecipe("void_robe",    1, new Dictionary<string, int> { ["crystal_robe"]    = 1, ["void_essence"]  = 2, ["enchanted_thread"]  = 1 }),

        // ── Armor (Tier 6-7) ──────────────────────────────────────────────────
        new CraftingRecipe("mithril_armor",1, new Dictionary<string, int> { ["mithril_ingot"]   = 4 }),
        new CraftingRecipe("dragon_plate", 1, new Dictionary<string, int> { ["mithril_armor"]   = 1, ["dragon_scale"]  = 1, ["essence_crystal"]  = 1 }),

        // ── Shields (Tier 2-3) ────────────────────────────────────────────────
        new CraftingRecipe("iron_shield",  1, new Dictionary<string, int> { ["iron_ingot"]      = 3 }),
        new CraftingRecipe("buckler",      1, new Dictionary<string, int> { ["iron_ingot"]      = 2, ["leather_piece"] = 1 }),
        new CraftingRecipe("tower_shield", 1, new Dictionary<string, int> { ["iron_shield"]     = 1, ["iron_ingot"]   = 2 }),

        // ── Shields (Tier 4-5) ────────────────────────────────────────────────
        new CraftingRecipe("crystal_shield",1,new Dictionary<string, int> { ["iron_shield"]     = 1, ["crystal"]      = 6, ["essence_crystal"]  = 1 }),
        new CraftingRecipe("void_aegis",   1, new Dictionary<string, int> { ["tower_shield"]    = 1, ["void_essence"] = 3, ["dark_steel_ingot"] = 1 }),
        new CraftingRecipe("void_barrier", 1, new Dictionary<string, int> { ["void_aegis"]      = 1, ["void_essence"] = 2, ["dark_steel_ingot"] = 1 }),

        // ── Shields (Tier 6-7) ────────────────────────────────────────────────
        new CraftingRecipe("mithril_shield",1,new Dictionary<string, int> { ["mithril_ingot"]   = 3 }),
        new CraftingRecipe("dragon_shield",1, new Dictionary<string, int> { ["mithril_shield"]  = 1, ["dragon_scale"] = 1 }),

        // ── Rings ─────────────────────────────────────────────────────────────
        new CraftingRecipe("power_ring",   1, new Dictionary<string, int> { ["iron_ingot"]      = 2, ["crystal"]      = 2 }),
        new CraftingRecipe("mage_ring",    1, new Dictionary<string, int> { ["crystal"]         = 4, ["enchanted_thread"] = 1 }),
        new CraftingRecipe("guardian_ring",1, new Dictionary<string, int> { ["iron_ingot"]      = 2, ["bone_fragment"] = 2 }),
        new CraftingRecipe("void_ring",    1, new Dictionary<string, int> { ["power_ring"]      = 1, ["void_essence"] = 2 }),
        new CraftingRecipe("void_lord_ring",1,new Dictionary<string, int> { ["void_ring"]       = 1, ["essence_crystal"] = 1, ["dragon_scale"] = 1 }),
    };

    public static CraftingRecipe GetRecipe(string resultId)
        => Recipes.Find(r => r.ResultId == resultId);
}
