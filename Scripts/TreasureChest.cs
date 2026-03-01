using Godot;

namespace VoidQuest;

/// <summary>
/// A treasure chest. Walk into its detection zone to open it.
/// Animates the lid, dims the glow, and adds loot DIRECTLY into the player's inventory.
/// </summary>
public partial class TreasureChest : Node3D
{
    // LootScene kept for optional physical drops, but not used by default
    [Export] public PackedScene LootScene;

    [Export] public string[] LootPool =
    {
        // Consumables (common)
        "health_potion", "mana_potion", "big_health_potion",
        "elixir", "rejuvenation_potion",
        // Consumables (rare)
        "full_restore", "void_elixir",
        // Rings
        "power_ring", "mage_ring", "void_ring",
        // Amulets (common)
        "amulet_warrior", "amulet_fire", "amulet_frost",
        "amulet_life",    "amulet_blood",
        // Amulets (rare)
        "amulet_lightning", "amulet_void",
        // Materials (common)
        "crystal", "void_essence", "bone_fragment",
        "iron_ore", "leather_piece", "coal",
        // Materials (uncommon)
        "void_shard", "essence_crystal", "enchanted_thread", "iron_ingot",
        // Materials (rare)
        "dragon_scale",
    };

    [Export] public int MinItems = 2;
    [Export] public int MaxItems = 5;

    private bool           _opened = false;
    private MeshInstance3D _lid;
    private OmniLight3D    _light;

    public override void _Ready()
    {
        _lid   = GetNodeOrNull<MeshInstance3D>("Lid");
        _light = GetNodeOrNull<OmniLight3D>("GlowLight");

        var area = GetNodeOrNull<Area3D>("OpenArea");
        if (area != null)
            area.BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body is Player player)
            Open(player);
    }

    private void Open(Player player)
    {
        if (_opened) return;
        _opened = true;

        // Animate lid
        if (_lid != null)
        {
            var tween = CreateTween();
            tween.TweenProperty(_lid, "rotation:x", -Mathf.DegToRad(100f), 0.45f)
                 .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        }

        if (_light != null)
            _light.LightEnergy = 0.2f;

        // Add loot directly to player inventory
        if (LootPool == null || LootPool.Length == 0) return;
        int count = (int)GD.RandRange(MinItems, MaxItems + 1);
        for (int i = 0; i < count; i++)
        {
            string id = LootPool[(int)(GD.Randi() % (uint)LootPool.Length)];
            if (ItemDatabase.Get(id) == null) continue;
            player.Inventory.AddItemById(id, 1);
            GD.Print($"[Chest] +{ItemDatabase.Get(id)?.Name}");
        }
    }
}
