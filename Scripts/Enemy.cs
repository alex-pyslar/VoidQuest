using Godot;

namespace VoidQuest;

public enum EnemyState   { Idle, Patrol, Chase, Attack, Frozen, Dead }
public enum EnemyVariant { Normal, Fast, Tank }

/// <summary>
/// Enemy with patrol/chase/attack AI, freeze support, loot drops, and XP grant.
/// </summary>
public partial class Enemy : CharacterBody3D
{
    [Export] public int   MaxHealth      = 50;
    [Export] public float MoveSpeed      = 3.5f;
    [Export] public float ChaseSpeed     = 6.5f;
    [Export] public float DetectRange    = 18.0f;
    [Export] public float AttackRange    = 2.2f;
    [Export] public int   AttackDamage   = 10;
    [Export] public float AttackCooldown = 1.5f;
    [Export] public int   ScoreReward    = 10;
    [Export] public int   XpReward       = 20;

    /// <summary>Loot table: array of item ids to potentially drop.</summary>
    [Export] public string[] LootPool = { "health_potion", "mana_potion", "rusty_sword", "leather_armor" };
    [Export] public float    LootChance = 0.65f;

    [Export] public PackedScene  LootScene;   // assign LootItem.tscn in editor / EnemySpawner
    [Export] public EnemyVariant Variant = EnemyVariant.Normal;

    [Signal] public delegate void HealthChangedEventHandler(int current, int max);
    [Signal] public delegate void DiedEventHandler();

    public int        CurrentHealth => _health;
    public EnemyState State         => _state;

    private int        _health;
    private EnemyState _state        = EnemyState.Idle;
    private float      _attackTimer  = 0f;
    private float      _stateTimer   = 0f;
    private float      _freezeTimer  = 0f;
    private Vector3    _patrolTarget;
    private Player     _player;
    private Label3D    _healthLabel;

    private float _gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

    public override void _Ready()
    {
        _health       = MaxHealth;
        _player       = GetTree().GetFirstNodeInGroup("player") as Player;
        _patrolTarget = GlobalPosition + RandomOffset(8f);
        AddToGroup("enemy");
        _healthLabel  = GetNodeOrNull<Label3D>("HealthLabel");
        ApplyVariant();
        UpdateHealthLabel();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_state == EnemyState.Dead) return;

        float dt = (float)delta;

        // Handle freeze
        if (_state == EnemyState.Frozen)
        {
            _freezeTimer -= dt;
            if (_freezeTimer <= 0f) _state = EnemyState.Idle;
            return;
        }

        _attackTimer -= dt;
        _stateTimer  -= dt;

        UpdateAI();

        var velocity = Velocity;
        if (!IsOnFloor()) velocity.Y -= _gravity * dt;

        switch (_state)
        {
            case EnemyState.Patrol:
                velocity = Patrol(velocity);
                break;
            case EnemyState.Chase:
                velocity = Chase(velocity);
                break;
            case EnemyState.Attack:
                DoAttack();
                velocity.X = Mathf.Lerp(velocity.X, 0f, 0.3f);
                velocity.Z = Mathf.Lerp(velocity.Z, 0f, 0.3f);
                break;
            default:
                velocity.X = Mathf.Lerp(velocity.X, 0f, 0.2f);
                velocity.Z = Mathf.Lerp(velocity.Z, 0f, 0.2f);
                break;
        }

        Velocity = velocity;
        MoveAndSlide();
    }

    // ── AI ────────────────────────────────────────────────────────────

    private void UpdateAI()
    {
        if (_player == null) return;
        float dist = GlobalPosition.DistanceTo(_player.GlobalPosition);

        if (dist <= AttackRange)
            _state = EnemyState.Attack;
        else if (dist <= DetectRange)
            _state = EnemyState.Chase;
        else if (_stateTimer <= 0f)
        {
            _state        = EnemyState.Patrol;
            _stateTimer   = (float)GD.RandRange(3.0, 6.0);
            _patrolTarget = GlobalPosition + RandomOffset(8f);
        }
    }

    private Vector3 Patrol(Vector3 vel)
    {
        Vector3 dir = _patrolTarget - GlobalPosition;
        dir.Y = 0f;
        if (dir.LengthSquared() < 2f) { _stateTimer = 0f; return vel; }
        dir = dir.Normalized();
        FaceDirection(dir);
        vel.X = dir.X * MoveSpeed;
        vel.Z = dir.Z * MoveSpeed;
        return vel;
    }

    private Vector3 Chase(Vector3 vel)
    {
        Vector3 dir = (_player.GlobalPosition - GlobalPosition);
        dir.Y = 0f; dir = dir.Normalized();
        FaceDirection(dir);
        vel.X = dir.X * ChaseSpeed;
        vel.Z = dir.Z * ChaseSpeed;
        return vel;
    }

    private void DoAttack()
    {
        if (_attackTimer > 0f) return;
        _attackTimer = AttackCooldown;
        _player?.TakeDamage(AttackDamage);
    }

    private void FaceDirection(Vector3 dir)
    {
        if (dir.LengthSquared() < 0.001f) return;
        var t = GlobalPosition + dir;
        t.Y = GlobalPosition.Y;
        LookAt(t, Vector3.Up);
    }

    // ── Public API ────────────────────────────────────────────────────

    public void TakeDamage(int amount)
    {
        if (_state == EnemyState.Dead) return;
        _health = Mathf.Max(0, _health - amount);
        EmitSignal(SignalName.HealthChanged, _health, MaxHealth);
        UpdateHealthLabel();
        // Aggro on hit
        if (_state == EnemyState.Patrol || _state == EnemyState.Idle)
            _state = EnemyState.Chase;
        if (_health <= 0) Die();
    }

    /// <summary>Temporarily stops the enemy (Frost Nova etc.).</summary>
    public void ApplyFreeze(float duration)
    {
        if (_state == EnemyState.Dead) return;
        _state       = EnemyState.Frozen;
        _freezeTimer = duration;
    }

    private void Die()
    {
        _state = EnemyState.Dead;
        GetNode<ScoreManager>("/root/ScoreManager")?.Add(ScoreReward);
        _player?.AddExperience(XpReward);
        EmitSignal(SignalName.Died);
        SpawnLoot();
        QueueFree();
    }

    private void SpawnLoot()
    {
        if (LootScene == null || LootPool == null || LootPool.Length == 0) return;
        if (GD.Randf() > LootChance) return;

        string itemId  = LootPool[(int)(GD.Randi() % (uint)LootPool.Length)];
        var loot       = LootScene.Instantiate<LootItem>();
        loot.ItemId    = itemId;
        var dropPos    = GlobalPosition + Vector3.Up * 0.5f;
        GetTree().CurrentScene.AddChild(loot);
        loot.GlobalPosition = dropPos;
    }

    private void ApplyVariant()
    {
        switch (Variant)
        {
            case EnemyVariant.Fast:
                MaxHealth    = 25;  _health = 25;
                MoveSpeed    = 5.5f; ChaseSpeed = 10.0f;
                AttackDamage = 7;
                XpReward     = 15;  ScoreReward = 8;
                LootChance   = 0.50f;
                LootPool     = new[]
                {
                    "mana_potion", "iron_dagger", "crystal", "power_ring",
                    "wooden_shield", "leather_piece", "void_shard", "speed_ring",
                };
                Scale        = new Vector3(0.75f, 0.75f, 0.75f);
                TintBody(new Color(0.08f, 0.18f, 0.85f, 1f));
                TintGlow(new Color(0.3f,  0.5f,  1.0f, 1f));
                break;

            case EnemyVariant.Tank:
                MaxHealth      = 150; _health = 150;
                MoveSpeed      = 2.0f; ChaseSpeed = 3.5f;
                AttackDamage   = 22;  AttackCooldown = 2.2f;
                XpReward       = 50;  ScoreReward = 25;
                LootChance     = 0.85f;
                LootPool       = new[]
                {
                    "void_essence", "iron_armor", "iron_shield", "elixir",
                    "bone_ring",    "iron_sword",  "bone_fragment", "tower_shield",
                    "dark_steel_ingot", "essence_crystal", "dragon_scale",
                };
                Scale          = new Vector3(1.4f, 1.4f, 1.4f);
                TintBody(new Color(0.25f, 0.0f, 0.35f, 1f));
                TintGlow(new Color(0.7f,  0.0f, 0.85f, 1f));
                break;

            default: // Normal
                LootPool = new[]
                {
                    "health_potion", "mana_potion", "rusty_sword", "leather_armor",
                    "crystal", "wooden_shield", "iron_dagger", "iron_ore",
                    "leather_piece", "bone_fragment", "coal",
                };
                break;
        }
    }

    private void TintBody(Color c)
    {
        var body = GetNodeOrNull<MeshInstance3D>("Body");
        if (body == null) return;
        if (body.GetSurfaceOverrideMaterial(0) is StandardMaterial3D mat)
        {
            var dup = (StandardMaterial3D)mat.Duplicate();
            dup.AlbedoColor = c;
            body.SetSurfaceOverrideMaterial(0, dup);
        }
    }

    private void TintGlow(Color c)
    {
        var glow = GetNodeOrNull<OmniLight3D>("GlowLight");
        if (glow != null) glow.LightColor = c;
    }

    private void UpdateHealthLabel()
    {
        if (_healthLabel == null) return;
        _healthLabel.Text = $"HP {_health}/{MaxHealth}";
        float t = (float)_health / MaxHealth;
        _healthLabel.Modulate = new Color(1f, t * 0.7f, t * 0.7f, 1f);
    }

    private static Vector3 RandomOffset(float r)
        => new((float)GD.RandRange(-r, r), 0f, (float)GD.RandRange(-r, r));
}
