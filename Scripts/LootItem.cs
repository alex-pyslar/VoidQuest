using Godot;

namespace VoidQuest;

/// <summary>
/// A dropped item in the world. Bobs up-and-down, auto-picks up when player touches it.
/// </summary>
public partial class LootItem : Area3D
{
    [Export] public string ItemId = "health_potion";
    [Export] public int    Count  = 1;

    private float  _bobTimer = 0f;
    private Vector3 _basePos;

    public override void _Ready()
    {
        _basePos      = GlobalPosition;
        BodyEntered  += OnBodyEntered;
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
}
