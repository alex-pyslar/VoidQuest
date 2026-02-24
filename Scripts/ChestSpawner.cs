using Godot;
using System.Collections.Generic;

namespace VoidQuest;

/// <summary>
/// Continuously maintains a pool of treasure chests around the local player.
/// Chests that drift beyond DespawnRadius are removed; new ones are placed
/// whenever the pool falls below MaxChests.
///
/// Works in single-player and multiplayer: "player" group always holds the
/// local/authority player, so each client runs its own independent chest world.
/// </summary>
public partial class ChestSpawner : Node3D
{
    // ── Inspector exports ─────────────────────────────────────────────
    [Export] public PackedScene ChestScene;
    [Export] public PackedScene LootScene;

    /// <summary>Target number of chests alive at once.</summary>
    [Export] public int   MaxChests     = 15;

    /// <summary>Max spawn radius around the player.</summary>
    [Export] public float SpawnRadius   = 100.0f;

    /// <summary>Don't spawn within this range (avoid spawning on top of player).</summary>
    [Export] public float MinSpawnDist  = 22.0f;

    /// <summary>Remove chests beyond this distance.</summary>
    [Export] public float DespawnRadius = 140.0f;

    /// <summary>Minimum distance between two chests so they don't cluster.</summary>
    [Export] public float ChestGap      = 14.0f;

    /// <summary>How often (seconds) to run the spawn/despawn pass.</summary>
    [Export] public float CheckInterval = 8.0f;

    // ── Private state ─────────────────────────────────────────────────
    private Player _player;
    private float  _timer;
    private readonly List<TreasureChest> _chests = new();

    // ── Lifecycle ─────────────────────────────────────────────────────

    public override void _Ready()
    {
        // Initial delay: give terrain physics time to settle before raycasting
        _timer = 3.0f;
        TryGetPlayer();
    }

    public override void _Process(double delta)
    {
        if (ChestScene == null || _player == null || !_player.IsInsideTree()) return;

        _timer -= (float)delta;
        if (_timer > 0f) return;
        _timer = CheckInterval;

        DespawnFar();
        SpawnNearby();
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private void TryGetPlayer()
    {
        _player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (_player == null)
            GetTree().CreateTimer(0.5f).Timeout += TryGetPlayer;
    }

    /// <summary>Remove chests that are too far away or have already been freed.</summary>
    private void DespawnFar()
    {
        for (int i = _chests.Count - 1; i >= 0; i--)
        {
            var chest = _chests[i];

            // Node was freed externally — clean up stale reference
            if (!GodotObject.IsInstanceValid(chest) || !chest.IsInsideTree())
            {
                _chests.RemoveAt(i);
                continue;
            }

            if (chest.GlobalPosition.DistanceTo(_player.GlobalPosition) > DespawnRadius)
            {
                chest.QueueFree();
                _chests.RemoveAt(i);
            }
        }
    }

    /// <summary>Spawn new chests until the pool reaches MaxChests.</summary>
    private void SpawnNearby()
    {
        int needed = MaxChests - _chests.Count;
        if (needed <= 0) return;

        int spawned  = 0;
        int attempts = 0;

        while (spawned < needed && attempts < needed * 20)
        {
            attempts++;

            var pos = FindGroundPos();
            if (pos == null) continue;
            if (pos.Value.DistanceTo(_player.GlobalPosition) < MinSpawnDist) continue;

            // Keep chests spread apart from each other
            bool tooClose = false;
            foreach (var c in _chests)
            {
                if (GodotObject.IsInstanceValid(c) && c.IsInsideTree()
                    && c.GlobalPosition.DistanceTo(pos.Value) < ChestGap)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;

            var chest = ChestScene.Instantiate<TreasureChest>();
            chest.LootScene = LootScene;
            GetParent().AddChild(chest);
            chest.GlobalPosition = pos.Value;
            _chests.Add(chest);
            spawned++;
        }

        if (spawned > 0)
            GD.Print($"[ChestSpawner] +{spawned} chests  (pool {_chests.Count}/{MaxChests})");
    }

    private Vector3? FindGroundPos()
    {
        float angle = (float)GD.RandRange(0.0, Mathf.Tau);
        float dist  = (float)GD.RandRange(MinSpawnDist + 5.0, SpawnRadius);
        float x     = _player.GlobalPosition.X + Mathf.Cos(angle) * dist;
        float z     = _player.GlobalPosition.Z + Mathf.Sin(angle) * dist;

        var from = new Vector3(x, _player.GlobalPosition.Y + 120f, z);
        var to   = new Vector3(x, _player.GlobalPosition.Y - 80f,  z);

        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollisionMask = 1; // terrain only
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);

        if (hit.Count > 0)
            return (Vector3)hit["position"] + Vector3.Up * 0.4f;
        return null;
    }
}
