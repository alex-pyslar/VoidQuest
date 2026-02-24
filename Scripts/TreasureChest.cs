using Godot;

namespace VoidQuest;

/// <summary>
/// A treasure chest. Walk into its detection zone to open it.
/// Animates the lid, dims the glow, and scatters loot items on the ground.
/// </summary>
public partial class TreasureChest : Node3D
{
    [Export] public PackedScene LootScene;

    // Default rich loot table — chests contain a mix of common + rare materials
    [Export] public string[] LootPool =
    {
        // Consumables
        "health_potion", "mana_potion", "big_health_potion", "elixir", "rejuvenation_potion",
        // Weapons (common → rare)
        "iron_sword", "steel_sword", "battle_axe", "magic_staff", "void_blade",
        // Armor
        "chainmail", "iron_armor", "mage_robe", "crystal_robe",
        // Shields
        "iron_shield", "tower_shield", "crystal_shield", "void_aegis",
        // Rings
        "power_ring", "mage_ring", "guardian_ring", "bone_ring",
        // Materials (common)
        "crystal", "iron_ore", "leather_piece", "coal",
        // Materials (uncommon)
        "void_essence", "bone_fragment", "dark_steel_ingot", "void_shard", "enchanted_thread",
        // Materials (rare – low weight by rarity, equal chance here but list is small)
        "essence_crystal", "mithril_ore", "dragon_scale",
    };
    [Export] public int   MinItems  = 2;
    [Export] public int   MaxItems  = 4;

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

    private void Open(Player _player)
    {
        if (_opened) return;
        _opened = true;

        // Animate lid opening (tween position up)
        if (_lid != null)
        {
            var tween = CreateTween();
            tween.TweenProperty(_lid, "rotation:x", -Mathf.DegToRad(100f), 0.45f)
                 .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        }

        // Dim the glow
        if (_light != null)
            _light.LightEnergy = 0.2f;

        // Spawn random items
        int count = (int)GD.RandRange(MinItems, MaxItems + 1);
        for (int i = 0; i < count; i++)
            SpawnLoot();
    }

    private void SpawnLoot()
    {
        if (LootScene == null || LootPool == null || LootPool.Length == 0) return;

        string id   = LootPool[(int)(GD.Randi() % (uint)LootPool.Length)];
        var    loot = LootScene.Instantiate<LootItem>();
        loot.ItemId = id;

        float angle = (float)GD.RandRange(0.0, Mathf.Tau);
        float dist  = (float)GD.RandRange(0.6, 1.6);
        GetTree().CurrentScene.AddChild(loot);
        loot.GlobalPosition = GlobalPosition + new Vector3(
            Mathf.Cos(angle) * dist, 0.6f, Mathf.Sin(angle) * dist);
    }
}
