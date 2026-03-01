using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace VoidQuest;

// ── Data model classes (usable from JSON or hardcode) ─────────────────────

/// <summary>Parameters for a player spell or active ability, loaded from Data/spells.json.</summary>
public class SpellData
{
    public string Id             { get; set; }
    public string NameEn         { get; set; }
    public string NameRu         { get; set; }
    public float  ManaCost       { get; set; }
    public float  Cooldown       { get; set; }
    public int    DamageBase     { get; set; }
    public float  MagicScale     { get; set; }
    public int    HealBase       { get; set; }
    public float  HealMagicScale { get; set; }
    public float  Radius         { get; set; }
    public float  Duration       { get; set; }
    public string RequiresAbility { get; set; }
    public string Input          { get; set; }
}

/// <summary>Enemy variant statistics, loaded from Data/enemies.json.</summary>
public class EnemyVariantData
{
    public int      MaxHealth      { get; set; }
    public float    MoveSpeed      { get; set; }
    public float    ChaseSpeed     { get; set; }
    public int      AttackDamage   { get; set; }
    public float    AttackCooldown { get; set; }
    public int      XpReward       { get; set; }
    public int      ScoreReward    { get; set; }
    public float    LootChance     { get; set; }
    public string[] LootPool       { get; set; }
    public float    Scale          { get; set; } = 1.0f;
    public float[]  BodyColor      { get; set; }
    public float[]  GlowColor      { get; set; }
}

// ── DataLoader singleton ───────────────────────────────────────────────────

/// <summary>
/// Loads all game data from JSON config files in res://Data/.
/// Call EnsureLoaded() once from GameManager._Ready() before any gameplay starts.
/// The JSON files are the single source of truth — modify them to add items,
/// change spell parameters, tweak enemy stats, or add crafting recipes
/// without touching any C# code.
/// </summary>
public static class DataLoader
{
    private static bool _loaded = false;

    /// <summary>All spell / ability parameter records, keyed by spell id.</summary>
    public static readonly Dictionary<string, SpellData> Spells = new();

    /// <summary>Enemy variant records, keyed by variant name (Normal / Fast / Tank).</summary>
    public static readonly Dictionary<string, EnemyVariantData> EnemyVariants = new();

    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
    };

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Loads all config files if not already loaded.
    /// Safe to call multiple times — subsequent calls are no-ops.
    /// </summary>
    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        LoadItems();
        LoadCrafting();
        LoadEnemies();
        LoadSpells();
    }

    public static SpellData GetSpell(string id)
        => Spells.TryGetValue(id, out var s) ? s : null;

    public static EnemyVariantData GetVariant(string name)
        => EnemyVariants.TryGetValue(name, out var v) ? v : null;

    // ── Loaders ───────────────────────────────────────────────────────────

    private static void LoadItems()
    {
        var json = ReadFile("res://Data/items.json");
        if (json == null) return;

        try
        {
            var list = JsonSerializer.Deserialize<ItemJson[]>(json, _opts);
            if (list == null) return;

            foreach (var j in list)
            {
                if (string.IsNullOrEmpty(j.Id)) continue;

                var def = new ItemDef
                {
                    Id          = j.Id,
                    Name        = j.NameEn ?? j.Id,
                    NameRu      = j.NameRu,
                    Category    = ParseCategory(j.Category),
                    Description = j.DescEn ?? "",
                    DescRu      = j.DescRu ?? "",
                    AtkBonus    = j.Atk,
                    DefBonus    = j.Def,
                    MagBonus    = j.Mag,
                    AbilityId   = j.AbilityId,
                    OnUse       = MakeOnUse(j.OnUse, j.OnUseAmount, j.OnUseAmount2),
                };
                ItemDatabase.Items[j.Id] = def;
            }
            GD.Print($"[DataLoader] Loaded {ItemDatabase.Items.Count} items.");
        }
        catch (Exception e)
        {
            GD.PrintErr($"[DataLoader] items.json parse error: {e.Message}");
        }
    }

    private static void LoadCrafting()
    {
        var json = ReadFile("res://Data/crafting.json");
        if (json == null) return;

        try
        {
            var list = JsonSerializer.Deserialize<RecipeJson[]>(json, _opts);
            if (list == null) return;

            CraftingDatabase.Recipes.Clear();
            foreach (var j in list)
            {
                if (string.IsNullOrEmpty(j.ResultId) || j.Ingredients == null) continue;
                CraftingDatabase.Recipes.Add(
                    new CraftingRecipe(j.ResultId, j.ResultCount > 0 ? j.ResultCount : 1, j.Ingredients));
            }
            GD.Print($"[DataLoader] Loaded {CraftingDatabase.Recipes.Count} recipes.");
        }
        catch (Exception e)
        {
            GD.PrintErr($"[DataLoader] crafting.json parse error: {e.Message}");
        }
    }

    private static void LoadEnemies()
    {
        var json = ReadFile("res://Data/enemies.json");
        if (json == null) return;

        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, EnemyVariantData>>(json, _opts);
            if (dict == null) return;

            EnemyVariants.Clear();
            foreach (var kvp in dict)
                EnemyVariants[kvp.Key] = kvp.Value;

            GD.Print($"[DataLoader] Loaded {EnemyVariants.Count} enemy variants.");
        }
        catch (Exception e)
        {
            GD.PrintErr($"[DataLoader] enemies.json parse error: {e.Message}");
        }
    }

    private static void LoadSpells()
    {
        var json = ReadFile("res://Data/spells.json");
        if (json == null) return;

        try
        {
            var list = JsonSerializer.Deserialize<SpellData[]>(json, _opts);
            if (list == null) return;

            Spells.Clear();
            foreach (var s in list)
            {
                if (!string.IsNullOrEmpty(s.Id))
                    Spells[s.Id] = s;
            }
            GD.Print($"[DataLoader] Loaded {Spells.Count} spells.");
        }
        catch (Exception e)
        {
            GD.PrintErr($"[DataLoader] spells.json parse error: {e.Message}");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string ReadFile(string resPath)
    {
        if (!FileAccess.FileExists(resPath))
        {
            GD.PrintErr($"[DataLoader] File not found: {resPath}");
            return null;
        }
        using var f = FileAccess.Open(resPath, FileAccess.ModeFlags.Read);
        return f?.GetAsText();
    }

    private static ItemCategory ParseCategory(string s) => s?.ToLowerInvariant() switch
    {
        "weapon"     => ItemCategory.Weapon,
        "armor"      => ItemCategory.Armor,
        "shield"     => ItemCategory.Shield,
        "ring"       => ItemCategory.Ring,
        "amulet"     => ItemCategory.Amulet,
        "consumable" => ItemCategory.Consumable,
        "material"   => ItemCategory.Material,
        _            => ItemCategory.Material,
    };

    private static Action<Player> MakeOnUse(string type, int amount, int amount2) => type switch
    {
        "heal"          => p => p.Heal(amount),
        "restore_mana"  => p => p.RestoreMana(amount),
        "heal_and_mana" => p => { p.Heal(amount); p.RestoreMana(amount2 > 0 ? amount2 : amount); },
        "full_restore"  => p => { p.Heal(9999); p.RestoreMana(9999); },
        _               => null,
    };

    // ── JSON DTO models (private — only used during loading) ──────────────

    private class ItemJson
    {
        public string Id           { get; set; }
        public string NameEn       { get; set; }
        public string NameRu       { get; set; }
        public string Category     { get; set; }
        public string DescEn       { get; set; }
        public string DescRu       { get; set; }
        public int    Atk          { get; set; }
        public int    Def          { get; set; }
        public int    Mag          { get; set; }
        public string AbilityId    { get; set; }
        public string OnUse        { get; set; }
        public int    OnUseAmount  { get; set; }
        public int    OnUseAmount2 { get; set; }
    }

    private class RecipeJson
    {
        public string                 ResultId    { get; set; }
        public int                    ResultCount { get; set; } = 1;
        public Dictionary<string, int> Ingredients { get; set; }
    }
}
