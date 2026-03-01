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

/// <summary>
/// Registry of crafting recipes.
/// Populated exclusively by DataLoader from Data/crafting.json.
/// Add or modify recipes by editing crafting.json — no C# changes needed.
/// </summary>
public static class CraftingDatabase
{
    public static readonly List<CraftingRecipe> Recipes = new();

    public static CraftingRecipe GetRecipe(string resultId)
        => Recipes.Find(r => r.ResultId == resultId);
}
