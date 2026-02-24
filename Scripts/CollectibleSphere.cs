using Godot;

namespace VoidQuest;

public partial class CollectibleSphere : Area3D
{
	[Export] public int   Points      { get; set; } = 1;
	[Export] public float RotateSpeed { get; set; } = 2.0f;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
	}

	public override void _Process(double delta)
	{
		var rot = Rotation;
		rot.Y  += RotateSpeed * (float)delta;
		Rotation = rot;
	}

	private void OnBodyEntered(Node3D body)
	{
		if (!body.IsInGroup("player")) return;

		GetNode<ScoreManager>("/root/ScoreManager").Add(Points);

		if (body is Player player)
			player.Inventory.AddItem(ItemType.Crystal, "Crystal");

		QueueFree();
	}
}
