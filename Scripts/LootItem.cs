using Godot;

namespace VoidQuest;

/// <summary>
/// A dropped item in the world. Bobs up-and-down, auto-picks up when player touches it.
/// Can be created from a .tscn scene OR entirely in code via CreateInCode().
/// </summary>
public partial class LootItem : Area3D
{
    [Export] public string ItemId = "health_potion";
    [Export] public int    Count  = 1;

    private float   _bobTimer = 0f;
    private Vector3 _basePos;

    public override void _Ready()
    {
        _basePos     = GlobalPosition;
        BodyEntered += OnBodyEntered;

        // Tint the child mesh (if present) based on item category.
        // Works for both scene-loaded and code-created LootItems.
        var mesh = GetNodeOrNull<MeshInstance3D>("Mesh");
        if (mesh != null)
            ApplyVisualColor(mesh);
    }

    public override void _Process(double delta)
    {
        _bobTimer += (float)delta * 2.0f;
        var pos = _basePos;
        pos.Y += Mathf.Sin(_bobTimer) * 0.25f;
        GlobalPosition = pos;

        var rot = Rotation;
        rot.Y    += (float)delta * 1.2f;
        Rotation  = rot;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body is Player player)
        {
            player.Inventory.AddItemById(ItemId, Count);
            var def = ItemDatabase.Get(ItemId);
            GD.Print($"Picked up: {def?.Name ?? ItemId} x{Count}");
            QueueFree();
        }
    }

    // ── Category-based colour tint ────────────────────────────────────
    private void ApplyVisualColor(MeshInstance3D mesh)
    {
        // Don't override a material that was set by the scene file
        if (mesh.GetSurfaceOverrideMaterial(0) != null) return;

        var def = ItemDatabase.Get(ItemId);
        Color c = def?.Category switch
        {
            ItemCategory.Amulet     => new Color(0.75f, 0.15f, 0.95f),
            ItemCategory.Ring       => new Color(0.95f, 0.85f, 0.10f),
            ItemCategory.Consumable => new Color(0.15f, 0.90f, 0.30f),
            ItemCategory.Material   => new Color(0.65f, 0.55f, 0.35f),
            ItemCategory.Weapon     => new Color(0.95f, 0.25f, 0.25f),
            ItemCategory.Armor      => new Color(0.25f, 0.50f, 0.95f),
            _                       => new Color(0.90f, 0.75f, 0.15f),
        };

        var mat = new StandardMaterial3D();
        mat.AlbedoColor            = c;
        mat.EmissionEnabled        = true;
        mat.Emission               = c * 0.45f;
        mat.EmissionEnergyMultiplier = 1.2f;
        mesh.SetSurfaceOverrideMaterial(0, mat);
    }

    // ── Factory: create a fully-functional LootItem without a .tscn ──
    /// <summary>
    /// Creates a LootItem node with collision shape and mesh built in code.
    /// Use when LootScene PackedScene is not assigned in the editor.
    /// Call this BEFORE AddChild(); set ItemId before or after.
    /// </summary>
    public static LootItem CreateInCode()
    {
        var item = new LootItem();

        // Collision shape required for BodyEntered signal
        var shape = new CollisionShape3D();
        shape.Shape = new SphereShape3D { Radius = 0.42f };
        item.AddChild(shape);

        // Visual sphere — named "Mesh" so _Ready can tint it
        var mesh = new MeshInstance3D();
        mesh.Name = "Mesh";
        mesh.Mesh = new SphereMesh { Radius = 0.22f, Height = 0.44f };
        item.AddChild(mesh);

        return item;
    }
}
