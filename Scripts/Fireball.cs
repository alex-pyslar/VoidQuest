using Godot;

namespace VoidQuest;

/// <summary>
/// Fireball projectile. Moves forward, damages the first enemy it hits.
/// Spawned by Player when casting the Fireball spell.
/// </summary>
public partial class Fireball : Area3D
{
    [Export] public float Speed      = 22.0f;
    [Export] public float MaxLifetime = 3.5f;

    public int Damage = 30;   // set by Player before adding to scene

    private float _timer = 0f;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        // Light pulse for visual effect
    }

    public override void _Process(double delta)
    {
        _timer += (float)delta;
        if (_timer >= MaxLifetime)
        {
            QueueFree();
            return;
        }
        // Move forward along local -Z axis
        GlobalPosition += -GlobalTransform.Basis.Z * Speed * (float)delta;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body is Enemy enemy)
        {
            enemy.TakeDamage(Damage);
            QueueFree();
        }
        else if (body is StaticBody3D)
        {
            // Hit terrain
            QueueFree();
        }
    }
}
