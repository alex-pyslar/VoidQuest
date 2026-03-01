using Godot;

namespace VoidQuest;

/// <summary>
/// Continuously spawns enemies around the local player and removes those
/// that have wandered (or been lured) too far away.
///
/// Works in single-player and multiplayer: the group "player" always
/// contains only the local/authority player, so each client independently
/// manages its own set of enemies.
/// </summary>
public partial class EnemySpawner : Node3D
{
    // ── Inspector exports ─────────────────────────────────────────────
    [Export] public PackedScene EnemyScene;
    [Export] public PackedScene LootScene;

    /// <summary>Maximum number of enemies alive at the same time.</summary>
    [Export] public int   MaxEnemies    = 20;

    /// <summary>Maximum distance from the player to spawn a new enemy.</summary>
    [Export] public float SpawnRadius   = 55.0f;

    /// <summary>Minimum distance from player so enemies don't appear on top of you.</summary>
    [Export] public float MinSpawnDist  = 15.0f;

    /// <summary>Enemies beyond this distance get removed to free up resources.</summary>
    [Export] public float DespawnRadius = 85.0f;

    /// <summary>How often (seconds) the spawner tries to fill up to MaxEnemies.</summary>
    [Export] public float SpawnInterval = 5.0f;

    // ── Private state ─────────────────────────────────────────────────
    private Player _player;
    private float  _timer;

    // ── Lifecycle ─────────────────────────────────────────────────────

    public override void _Ready()
    {
        _timer = SpawnInterval * 0.4f; // first spawn a bit sooner
        TryGetPlayer();
    }

    public override void _Process(double delta)
    {
        if (EnemyScene == null || _player == null || !_player.IsInsideTree()) return;
        if (GetNodeOrNull<GameManager>("/root/GameManager")?.State != GameState.Playing) return;

        // 1. Despawn enemies that wandered too far
        foreach (Node node in GetTree().GetNodesInGroup("enemy"))
        {
            if (node is Enemy e && e.IsInsideTree()
                && e.GlobalPosition.DistanceTo(_player.GlobalPosition) > DespawnRadius)
            {
                e.QueueFree();
            }
        }

        // 2. Spawn a batch if below the cap
        _timer -= (float)delta;
        if (_timer > 0f) return;
        _timer = SpawnInterval;

        int current = GetTree().GetNodesInGroup("enemy").Count;
        if (current >= MaxEnemies) return;

        int toSpawn = Mathf.Min(3, MaxEnemies - current);
        for (int i = 0; i < toSpawn; i++)
            TrySpawnOne();
    }

    // ── Helpers ───────────────────────────────────────────────────────

    /// <summary>Find the local player, retry every 0.5 s if not yet spawned.</summary>
    private void TryGetPlayer()
    {
        _player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (_player == null)
            GetTree().CreateTimer(0.5f).Timeout += TryGetPlayer;
    }

    private void TrySpawnOne()
    {
        var pos = FindGroundPos();
        if (pos == null) return;

        var enemy = EnemyScene.Instantiate<Enemy>();

        // 60 % Normal / 25 % Fast / 15 % Tank
        float roll = GD.Randf();
        enemy.Variant = roll < 0.15f ? EnemyVariant.Tank
                      : roll < 0.40f ? EnemyVariant.Fast
                      : EnemyVariant.Normal;

        GetParent().AddChild(enemy);
        enemy.GlobalPosition = pos.Value;
    }

    /// <summary>
    /// Shoot a ray from high above a random point within the spawn arc
    /// and return the first terrain hit, or null if nothing was found.
    /// </summary>
    private Vector3? FindGroundPos()
    {
        // Pick a direction biased toward where the player is facing
        Vector3 fwd = -_player.GlobalTransform.Basis.Z;
        fwd.Y = 0f;
        if (fwd.LengthSquared() < 0.001f) fwd = Vector3.Forward;
        fwd = fwd.Normalized();

        float halfArc = Mathf.DegToRad(130f);
        float rot = (float)GD.RandRange(-halfArc, halfArc);
        float cos = Mathf.Cos(rot), sin = Mathf.Sin(rot);
        var dir = new Vector3(fwd.X * cos - fwd.Z * sin, 0f,
                              fwd.X * sin + fwd.Z * cos);

        float dist = (float)GD.RandRange(MinSpawnDist, SpawnRadius);
        float x = _player.GlobalPosition.X + dir.X * dist;
        float z = _player.GlobalPosition.Z + dir.Z * dist;

        var from = new Vector3(x, _player.GlobalPosition.Y + 120f, z);
        var to   = new Vector3(x, _player.GlobalPosition.Y - 80f,  z);

        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollisionMask = 1; // terrain only
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);

        if (hit.Count > 0)
            return (Vector3)hit["position"] + Vector3.Up * 0.5f;
        return null;
    }
}
