using Godot;

namespace VoidQuest;

/// <summary>
/// RPG Player:
///   Movement – WASD + Shift sprint, Space jump, mouse look, Esc pause
///   Combat   – F / LMB: melee attack   Q: Fireball   E: Heal   R: Frost Nova
///   UI       – I / Tab: open/close inventory
/// </summary>
public partial class Player : CharacterBody3D
{
    // ── Movement ──────────────────────────────────────────────────────
    [Export] public float WalkSpeed        = 7.0f;
    [Export] public float SprintSpeed      = 14.0f;
    [Export] public float JumpForce        = 9.0f;
    [Export] public float MouseSensitivity = 0.003f;

    // ── RPG Base Stats ────────────────────────────────────────────────
    [Export] public int MaxHealth      = 100;
    [Export] public int MaxMana        = 100;
    [Export] public int BaseAttack     = 10;
    [Export] public int BaseDefense    = 0;

    // ── XP / Level ────────────────────────────────────────────────────
    public int Level      { get; private set; } = 1;
    public int Experience { get; private set; } = 0;
    public int XpToNext   => Level * 50;

    // ── Current State ─────────────────────────────────────────────────
    public int       CurrentHealth { get; private set; }
    public int       CurrentMana   { get; private set; }
    public Inventory Inventory     { get; private set; }

    // ── Computed stats (base + gear) ──────────────────────────────────
    public int TotalAttack  => BaseAttack  + Inventory.TotalAtkBonus;
    public int TotalDefense => BaseDefense + Inventory.TotalDefBonus;
    public int TotalMagic   => 10          + Inventory.TotalMagBonus;

    // ── Combat config ─────────────────────────────────────────────────
    [Export] public float MeleeRange     = 2.8f;
    [Export] public float MeleeAngle     = 70.0f;   // degrees half-arc
    [Export] public float MeleeCooldown  = 0.55f;

    [Export] public float FireballManaCost  = 20f;
    [Export] public float HealManaCost      = 30f;
    [Export] public float FrostNovaManaCost = 40f;
    [Export] public float FrostNovaCooldown = 8.0f;
    [Export] public float FrostNovaRadius   = 8.0f;
    [Export] public float SpellCooldown     = 0.8f;

    [Export] public PackedScene FireballScene;

    // ── Signals ───────────────────────────────────────────────────────
    [Signal] public delegate void HealthChangedEventHandler(int current, int max);
    [Signal] public delegate void ManaChangedEventHandler(int current, int max);
    [Signal] public delegate void DiedEventHandler();
    [Signal] public delegate void LevelUpEventHandler(int newLevel);
    [Signal] public delegate void InventoryToggledEventHandler(bool open);

    // ── Private state ─────────────────────────────────────────────────
    private float    _gravity     = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
    private float    _cameraPitch = 0.0f;
    private bool     _isDead      = false;
    private bool     _inventoryOpen = false;

    private float    _meleeCooldownTimer = 0f;
    private float    _spellCooldownTimer = 0f;
    private float    _frostNovaCooldownTimer = 0f;
    private float    _manaRegenTimer = 0f;

    // Input flags set in _UnhandledInput, consumed in _PhysicsProcess
    private bool _pendingAttack;
    private bool _pendingFireball;
    private bool _pendingHeal;
    private bool _pendingFrostNova;

    private Node3D   _camPivot;
    private Camera3D _cam;

    // ── Ready ─────────────────────────────────────────────────────────

    public override void _Ready()
    {
        _camPivot = GetNode<Node3D>("CameraPivot");
        _cam      = GetNode<Camera3D>("CameraPivot/Camera3D");

        CurrentHealth = MaxHealth;
        CurrentMana   = MaxMana;
        Inventory     = new Inventory();

        bool isLocal = IsMultiplayerAuthority();

        if (isLocal)
        {
            // Full local player setup
            Inventory.AddItemById("rusty_sword");
            Inventory.AddItemById("health_potion", 2);
            Inventory.Equip("rusty_sword");
            AddToGroup("player");
            _cam.MakeCurrent();
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        else
        {
            // Remote player: disable camera and all local processing
            _cam.Current = false;
            SetPhysicsProcess(false);
            SetProcessUnhandledInput(false);
        }
    }

    // ── Input ─────────────────────────────────────────────────────────

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion
            && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            var rot = Rotation;
            rot.Y        -= motion.Relative.X * MouseSensitivity;
            Rotation      = rot;
            _cameraPitch -= motion.Relative.Y * MouseSensitivity;
            _cameraPitch  = Mathf.Clamp(_cameraPitch, -1.45f, 0.9f);
            var camRot    = _cam.Rotation;
            camRot.X      = _cameraPitch;
            _cam.Rotation = camRot;
        }

        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.Keycode == Key.I || key.Keycode == Key.Tab)
                ToggleInventory();
        }

        // Combat – set flags here so _PhysicsProcess never misses a press
        if (@event.IsActionPressed("attack"))  _pendingAttack    = true;
        if (@event.IsActionPressed("spell_1")) _pendingFireball  = true;
        if (@event.IsActionPressed("spell_2")) _pendingHeal      = true;
        if (@event.IsActionPressed("spell_3")) _pendingFrostNova = true;
    }

    // ── Physics / Combat tick ─────────────────────────────────────────

    public override void _PhysicsProcess(double delta)
    {
        if (_isDead) return;
        float dt = (float)delta;

        TickCooldowns(dt);
        HandleCombatInput();
        HandleMovement(dt);
        HandleManaRegen(dt);
    }

    private void TickCooldowns(float dt)
    {
        if (_meleeCooldownTimer  > 0f) _meleeCooldownTimer  -= dt;
        if (_spellCooldownTimer  > 0f) _spellCooldownTimer  -= dt;
        if (_frostNovaCooldownTimer > 0f) _frostNovaCooldownTimer -= dt;
    }

    private void HandleCombatInput()
    {
        if (_pendingAttack)    { _pendingAttack    = false; TryMeleeAttack(); }
        if (_pendingFireball)  { _pendingFireball  = false; TryCastFireball(); }
        if (_pendingHeal)      { _pendingHeal      = false; TryCastHeal(); }
        if (_pendingFrostNova) { _pendingFrostNova = false; TryCastFrostNova(); }
    }

    private void HandleMovement(float dt)
    {
        var velocity = Velocity;
        bool    sprinting = Input.IsActionPressed("sprint");
        float   speed     = sprinting ? SprintSpeed : WalkSpeed;
        Vector2 inputDir  = Input.GetVector("move_left", "move_right", "move_backward", "move_forward");

        bool moving = inputDir.LengthSquared() > 0f;
        if (moving)
        {
            Vector3 moveDir = (Transform.Basis * new Vector3(inputDir.X, 0f, -inputDir.Y)).Normalized();
            velocity.X = moveDir.X * speed;
            velocity.Z = moveDir.Z * speed;
        }
        else
        {
            velocity.X = Mathf.Lerp(velocity.X, 0f, 0.18f);
            velocity.Z = Mathf.Lerp(velocity.Z, 0f, 0.18f);
        }

        // FOV pulse when sprinting
        float targetFov = (sprinting && moving) ? 85.0f : 75.0f;
        _cam.Fov = Mathf.Lerp(_cam.Fov, targetFov, 0.1f);

        if (IsOnFloor())
        {
            if (Input.IsActionJustPressed("jump"))
                velocity.Y = JumpForce;
        }
        else
        {
            velocity.Y -= _gravity * dt;
        }

        Velocity = velocity;
        MoveAndSlide();

        // Broadcast position to all other peers every physics frame
        if (Multiplayer.MultiplayerPeer != null)
            Rpc(MethodName.SyncTransform, GlobalPosition, Rotation);
    }

    // ── Multiplayer position sync ──────────────────────────────────────

    /// <summary>
    /// Received on all non-authority peers to update this remote player's transform.
    /// Sent every physics frame by the authority (local player on their machine).
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void SyncTransform(Vector3 pos, Vector3 rot)
    {
        // Only update if we are NOT the owner of this player
        if (!IsMultiplayerAuthority())
        {
            GlobalPosition = pos;
            Rotation       = rot;
        }
    }

    private void HandleManaRegen(float dt)
    {
        _manaRegenTimer += dt;
        if (_manaRegenTimer >= 2.0f)
        {
            _manaRegenTimer = 0f;
            if (CurrentMana < MaxMana) RestoreMana(5);
        }
    }

    // ── Melee Attack ──────────────────────────────────────────────────

    private void TryMeleeAttack()
    {
        if (_meleeCooldownTimer > 0f) return;
        _meleeCooldownTimer = MeleeCooldown;

        float halfAngle = Mathf.DegToRad(MeleeAngle);
        foreach (var node in GetTree().GetNodesInGroup("enemy"))
        {
            if (node is not Enemy enemy) continue;
            Vector3 toEnemy = enemy.GlobalPosition - GlobalPosition;
            if (toEnemy.LengthSquared() > MeleeRange * MeleeRange) continue;
            float angle = (-GlobalTransform.Basis.Z).AngleTo(toEnemy.Normalized());
            if (angle <= halfAngle)
                enemy.TakeDamage(TotalAttack);
        }
    }

    // ── Spells ────────────────────────────────────────────────────────

    private void TryCastFireball()
    {
        if (_spellCooldownTimer > 0f || CurrentMana < FireballManaCost) return;
        if (FireballScene == null) return;

        UseMana((int)FireballManaCost);
        _spellCooldownTimer = SpellCooldown;

        var fb = FireballScene.Instantiate<Fireball>();
        fb.Damage = 30 + TotalMagic / 2;
        fb.GlobalTransform = _cam.GlobalTransform;
        GetTree().CurrentScene.AddChild(fb);
    }

    private void TryCastHeal()
    {
        if (_spellCooldownTimer > 0f || CurrentMana < HealManaCost) return;
        UseMana((int)HealManaCost);
        _spellCooldownTimer = SpellCooldown;
        Heal(40 + TotalMagic / 2);
    }

    private void TryCastFrostNova()
    {
        if (_frostNovaCooldownTimer > 0f || CurrentMana < FrostNovaManaCost) return;
        UseMana((int)FrostNovaManaCost);
        _frostNovaCooldownTimer = FrostNovaCooldown;
        _spellCooldownTimer = SpellCooldown;

        // Slow / freeze all nearby enemies
        foreach (var node in GetTree().GetNodesInGroup("enemy"))
        {
            if (node is not Enemy enemy) continue;
            if (GlobalPosition.DistanceTo(enemy.GlobalPosition) <= FrostNovaRadius)
                enemy.ApplyFreeze(3.0f);
        }
    }

    // ── Inventory ─────────────────────────────────────────────────────

    private void ToggleInventory()
    {
        _inventoryOpen = !_inventoryOpen;
        Input.MouseMode = _inventoryOpen
            ? Input.MouseModeEnum.Visible
            : Input.MouseModeEnum.Captured;
        EmitSignal(SignalName.InventoryToggled, _inventoryOpen);
    }

    /// <summary>Use a consumable item by id. Called from inventory UI.</summary>
    public void UseItem(string id)
    {
        if (!Inventory.RemoveItem(id)) return;
        ItemDatabase.Get(id)?.OnUse?.Invoke(this);
    }

    /// <summary>Equip gear item. Called from inventory UI.</summary>
    public void EquipItem(string id)
    {
        Inventory.Equip(id);
    }

    // ── Public Health / Mana API ──────────────────────────────────────

    public void TakeDamage(int rawAmount)
    {
        if (_isDead) return;
        int damage = Mathf.Max(1, rawAmount - TotalDefense);
        CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
        if (CurrentHealth <= 0) Die();
    }

    public void Heal(int amount)
    {
        if (_isDead) return;
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
    }

    private void UseMana(int amount)
    {
        CurrentMana = Mathf.Max(0, CurrentMana - amount);
        EmitSignal(SignalName.ManaChanged, CurrentMana, MaxMana);
    }

    public void RestoreMana(int amount)
    {
        CurrentMana = Mathf.Min(MaxMana, CurrentMana + amount);
        EmitSignal(SignalName.ManaChanged, CurrentMana, MaxMana);
    }

    // ── XP / Leveling ─────────────────────────────────────────────────

    public void AddExperience(int xp)
    {
        Experience += xp;
        while (Experience >= XpToNext)
        {
            Experience -= XpToNext;
            Level++;
            OnLevelUp();
        }
    }

    private void OnLevelUp()
    {
        MaxHealth  += 20;
        MaxMana    += 15;
        BaseAttack += 3;
        BaseDefense += 1;
        Heal(MaxHealth);
        RestoreMana(MaxMana);
        EmitSignal(SignalName.LevelUp, Level);
    }

    private void Die()
    {
        _isDead = true;
        EmitSignal(SignalName.Died);
        GetNode<GameManager>("/root/GameManager")?.OnPlayerDied();
    }
}
