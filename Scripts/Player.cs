using Godot;

namespace VoidQuest;

/// <summary>
/// RPG Player:
///   Movement – WASD + Shift sprint, Space jump, mouse look, Esc pause
///   Combat   – F / LMB: melee attack   Q: Fireball   E: Heal   R: Frost Nova
///   Abilities– [4]: Lightning Strike (requires Amulet of Lightning)
///              [5]: Void Burst       (requires Amulet of the Void)
///   UI       – I / Tab: open/close inventory
///
/// Spell parameters (mana cost, damage, cooldown, radius) are loaded from
/// Data/spells.json via DataLoader — edit that file to tune spells without
/// touching C# code.
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

    // ── Combat config (overridden in _Ready from spells.json) ─────────
    [Export] public float MeleeRange     = 2.8f;
    [Export] public float MeleeAngle     = 70.0f;
    [Export] public float MeleeCooldown  = 0.55f;

    [Export] public float FireballManaCost  = 20f;
    [Export] public float HealManaCost      = 30f;
    [Export] public float FrostNovaManaCost = 40f;
    [Export] public float FrostNovaCooldown = 8.0f;
    [Export] public float FrostNovaRadius   = 8.0f;
    [Export] public float SpellCooldown     = 0.8f;

    // ── Amulet ability costs / ranges (overridden from spells.json) ───
    private float _lightningManaCost   = 35f;
    private float _lightningCooldown   = 6.0f;
    private float _lightningRange      = 20f;
    private int   _lightningDamageBase = 25;

    private float _voidBurstManaCost   = 50f;
    private float _voidBurstCooldown   = 10.0f;
    private float _voidBurstRadius     = 10f;
    private int   _voidBurstDamageBase = 40;

    [Export] public PackedScene FireballScene;

    // ── Signals ───────────────────────────────────────────────────────
    [Signal] public delegate void HealthChangedEventHandler(int current, int max);
    [Signal] public delegate void ManaChangedEventHandler(int current, int max);
    [Signal] public delegate void DiedEventHandler();
    [Signal] public delegate void LevelUpEventHandler(int newLevel);
    [Signal] public delegate void InventoryToggledEventHandler(bool open);

    // ── Private state ─────────────────────────────────────────────────
    private float _gravity     = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
    private float _cameraPitch = 0.0f;
    private bool  _isDead      = false;
    private bool  _inventoryOpen = false;

    private float _meleeCooldownTimer     = 0f;
    private float _spellCooldownTimer     = 0f;
    private float _frostNovaCooldownTimer = 0f;
    private float _lightningCooldownTimer = 0f;
    private float _voidBurstCooldownTimer = 0f;
    private float _manaRegenTimer         = 0f;

    // Input flags set in _UnhandledInput, consumed in _PhysicsProcess
    private bool _pendingAttack;
    private bool _pendingFireball;
    private bool _pendingHeal;
    private bool _pendingFrostNova;
    private bool _pendingLightning;
    private bool _pendingVoidBurst;

    private Node3D   _camPivot;
    private Camera3D _cam;

    // ── Ready ─────────────────────────────────────────────────────────

    public override void _Ready()
    {
        DataLoader.EnsureLoaded();
        ApplySpellParams();

        _camPivot = GetNode<Node3D>("CameraPivot");
        _cam      = GetNode<Camera3D>("CameraPivot/Camera3D");

        CurrentHealth = MaxHealth;
        CurrentMana   = MaxMana;
        Inventory     = new Inventory();

        bool isLocal = IsMultiplayerAuthority();

        if (isLocal)
        {
            Inventory.AddItemById("rusty_sword");
            Inventory.AddItemById("health_potion", 2);
            Inventory.Equip("rusty_sword");
            AddToGroup("player");
            _cam.MakeCurrent();
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        else
        {
            _cam.Current = false;
            SetPhysicsProcess(false);
            SetProcessUnhandledInput(false);
        }
    }

    /// <summary>Reads spell parameters from DataLoader (loaded from spells.json).</summary>
    private void ApplySpellParams()
    {
        if (DataLoader.GetSpell("fireball") is { } fb)
        {
            FireballManaCost = fb.ManaCost;
            SpellCooldown    = fb.Cooldown;
        }
        if (DataLoader.GetSpell("heal") is { } heal)
            HealManaCost = heal.ManaCost;

        if (DataLoader.GetSpell("frost_nova") is { } fn)
        {
            FrostNovaManaCost = fn.ManaCost;
            FrostNovaCooldown = fn.Cooldown;
            if (fn.Radius > 0f) FrostNovaRadius = fn.Radius;
        }
        if (DataLoader.GetSpell("lightning_strike") is { } ls)
        {
            _lightningManaCost   = ls.ManaCost;
            _lightningCooldown   = ls.Cooldown;
            if (ls.Radius > 0f)    _lightningRange      = ls.Radius;
            if (ls.DamageBase > 0) _lightningDamageBase = ls.DamageBase;
        }
        if (DataLoader.GetSpell("void_burst") is { } vb)
        {
            _voidBurstManaCost   = vb.ManaCost;
            _voidBurstCooldown   = vb.Cooldown;
            if (vb.Radius > 0f)    _voidBurstRadius     = vb.Radius;
            if (vb.DamageBase > 0) _voidBurstDamageBase = vb.DamageBase;
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

            // Amulet abilities
            if (key.Keycode == Key.Key4) _pendingLightning = true;
            if (key.Keycode == Key.Key5) _pendingVoidBurst = true;
        }

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
        if (_meleeCooldownTimer     > 0f) _meleeCooldownTimer     -= dt;
        if (_spellCooldownTimer     > 0f) _spellCooldownTimer     -= dt;
        if (_frostNovaCooldownTimer > 0f) _frostNovaCooldownTimer -= dt;
        if (_lightningCooldownTimer > 0f) _lightningCooldownTimer -= dt;
        if (_voidBurstCooldownTimer > 0f) _voidBurstCooldownTimer -= dt;
    }

    private void HandleCombatInput()
    {
        if (_pendingAttack)    { _pendingAttack    = false; TryMeleeAttack();   }
        if (_pendingFireball)  { _pendingFireball  = false; TryCastFireball();  }
        if (_pendingHeal)      { _pendingHeal      = false; TryCastHeal();      }
        if (_pendingFrostNova) { _pendingFrostNova = false; TryCastFrostNova(); }
        if (_pendingLightning) { _pendingLightning = false; TryCastLightning(); }
        if (_pendingVoidBurst) { _pendingVoidBurst = false; TryCastVoidBurst(); }
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

        if (Multiplayer.MultiplayerPeer != null)
            Rpc(MethodName.SyncTransform, GlobalPosition, Rotation);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void SyncTransform(Vector3 pos, Vector3 rot)
    {
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
            // Amulet of Life: passive HP regen
            if (HasAbility("life_regen") && CurrentHealth < MaxHealth)
                Heal(3);
        }
    }

    // ── Melee Attack ──────────────────────────────────────────────────

    private void TryMeleeAttack()
    {
        if (_meleeCooldownTimer > 0f) return;
        _meleeCooldownTimer = MeleeCooldown;

        float halfAngle = Mathf.DegToRad(MeleeAngle);
        int totalDamage = 0;

        foreach (var node in GetTree().GetNodesInGroup("enemy"))
        {
            if (node is not Enemy enemy) continue;
            Vector3 toEnemy = enemy.GlobalPosition - GlobalPosition;
            if (toEnemy.LengthSquared() > MeleeRange * MeleeRange) continue;
            float angle = (-GlobalTransform.Basis.Z).AngleTo(toEnemy.Normalized());
            if (angle <= halfAngle)
            {
                enemy.TakeDamage(TotalAttack);
                totalDamage += TotalAttack;
            }
        }

        // Amulet of Blood: heal 15% of melee damage dealt
        if (totalDamage > 0 && HasAbility("blood_drain"))
            Heal(Mathf.Max(1, totalDamage * 15 / 100));
    }

    // ── Spells ────────────────────────────────────────────────────────

    private void TryCastFireball()
    {
        if (_spellCooldownTimer > 0f || CurrentMana < FireballManaCost) return;
        if (FireballScene == null) return;

        UseMana((int)FireballManaCost);
        _spellCooldownTimer = SpellCooldown;

        var fb = FireballScene.Instantiate<Fireball>();
        var spell = DataLoader.GetSpell("fireball");
        int   damageBase = spell?.DamageBase > 0 ? spell.DamageBase : 30;
        float magScale   = spell?.MagicScale  > 0 ? spell.MagicScale  : 0.5f;
        int   baseDamage = damageBase + (int)(TotalMagic * magScale);
        // Amulet of Fire: +50% fireball damage
        fb.Damage = HasAbility("fireball_boost") ? (int)(baseDamage * 1.5f) : baseDamage;
        fb.GlobalTransform = _cam.GlobalTransform;
        GetTree().CurrentScene.AddChild(fb);
    }

    private void TryCastHeal()
    {
        if (_spellCooldownTimer > 0f || CurrentMana < HealManaCost) return;
        UseMana((int)HealManaCost);
        _spellCooldownTimer = SpellCooldown;
        var spell    = DataLoader.GetSpell("heal");
        int   healBase = spell?.HealBase > 0 ? spell.HealBase : 40;
        float magScale = spell?.HealMagicScale > 0 ? spell.HealMagicScale : 0.5f;
        Heal(healBase + (int)(TotalMagic * magScale));
    }

    private void TryCastFrostNova()
    {
        if (_frostNovaCooldownTimer > 0f || CurrentMana < FrostNovaManaCost) return;
        UseMana((int)FrostNovaManaCost);
        _frostNovaCooldownTimer = FrostNovaCooldown;
        _spellCooldownTimer = SpellCooldown;

        // Amulet of Frost: larger radius and longer freeze duration
        var spell = DataLoader.GetSpell("frost_nova");
        float baseDuration = spell?.Duration > 0 ? spell.Duration : 3.0f;
        float radius   = FrostNovaRadius + (HasAbility("frost_boost") ? 5f : 0f);
        float duration = baseDuration    + (HasAbility("frost_boost") ? 2f : 0f);

        foreach (var node in GetTree().GetNodesInGroup("enemy"))
        {
            if (node is not Enemy enemy) continue;
            if (GlobalPosition.DistanceTo(enemy.GlobalPosition) <= radius)
                enemy.ApplyFreeze(duration);
        }
    }

    // ── Amulet Abilities ──────────────────────────────────────────────

    /// <summary>Lightning Strike [Key 4] — hits all enemies within range.</summary>
    private void TryCastLightning()
    {
        if (!HasAbility("lightning_strike")) return;
        if (_lightningCooldownTimer > 0f || CurrentMana < _lightningManaCost) return;

        UseMana((int)_lightningManaCost);
        _lightningCooldownTimer = _lightningCooldown;

        int damage = _lightningDamageBase + TotalMagic;
        int hit = 0;
        foreach (var node in GetTree().GetNodesInGroup("enemy"))
        {
            if (node is not Enemy enemy) continue;
            if (GlobalPosition.DistanceTo(enemy.GlobalPosition) <= _lightningRange)
            { enemy.TakeDamage(damage); hit++; }
        }
        GD.Print($"[Lightning Strike] Hit {hit} enemies for {damage}.");
    }

    /// <summary>Void Burst [Key 5] — explodes in a void ring around the player.</summary>
    private void TryCastVoidBurst()
    {
        if (!HasAbility("void_burst")) return;
        if (_voidBurstCooldownTimer > 0f || CurrentMana < _voidBurstManaCost) return;

        UseMana((int)_voidBurstManaCost);
        _voidBurstCooldownTimer = _voidBurstCooldown;

        int damage = _voidBurstDamageBase + TotalMagic;
        int hit = 0;
        foreach (var node in GetTree().GetNodesInGroup("enemy"))
        {
            if (node is not Enemy enemy) continue;
            if (GlobalPosition.DistanceTo(enemy.GlobalPosition) <= _voidBurstRadius)
            { enemy.TakeDamage(damage); hit++; }
        }
        GD.Print($"[Void Burst] Hit {hit} enemies for {damage}.");
    }

    // ── Ability helper ────────────────────────────────────────────────

    public bool HasAbility(string abilityId) => Inventory.HasAbility(abilityId);

    /// <summary>
    /// Returns spell state for the HUD hotbar.
    /// ratio   : 0 = ready, 1 = just cast (use for cooldown overlay).
    /// canCast : mana OK + not on cooldown + not locked.
    /// isLocked: requires an amulet ability the player doesn't have.
    /// manaCost: mana cost for display.
    /// </summary>
    public void GetSpellStatus(string spellId,
        out float ratio, out bool canCast, out bool isLocked, out float manaCost)
    {
        ratio = 0f; canCast = false; isLocked = false; manaCost = 0f;
        switch (spellId)
        {
            case "fireball":
                manaCost = FireballManaCost;
                isLocked = FireballScene == null;
                ratio    = Mathf.Clamp(_spellCooldownTimer / Mathf.Max(SpellCooldown, 0.01f), 0f, 1f);
                canCast  = !isLocked && ratio <= 0f && CurrentMana >= FireballManaCost;
                break;
            case "heal":
                manaCost = HealManaCost;
                ratio    = Mathf.Clamp(_spellCooldownTimer / Mathf.Max(SpellCooldown, 0.01f), 0f, 1f);
                canCast  = ratio <= 0f && CurrentMana >= HealManaCost;
                break;
            case "frost_nova":
                manaCost = FrostNovaManaCost;
                ratio    = Mathf.Clamp(_frostNovaCooldownTimer / Mathf.Max(FrostNovaCooldown, 0.01f), 0f, 1f);
                canCast  = ratio <= 0f && CurrentMana >= FrostNovaManaCost;
                break;
            case "lightning_strike":
                manaCost = _lightningManaCost;
                isLocked = !HasAbility("lightning_strike");
                ratio    = Mathf.Clamp(_lightningCooldownTimer / Mathf.Max(_lightningCooldown, 0.01f), 0f, 1f);
                canCast  = !isLocked && ratio <= 0f && CurrentMana >= _lightningManaCost;
                break;
            case "void_burst":
                manaCost = _voidBurstManaCost;
                isLocked = !HasAbility("void_burst");
                ratio    = Mathf.Clamp(_voidBurstCooldownTimer / Mathf.Max(_voidBurstCooldown, 0.01f), 0f, 1f);
                canCast  = !isLocked && ratio <= 0f && CurrentMana >= _voidBurstManaCost;
                break;
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

    public void UseItem(string id)
    {
        if (!Inventory.RemoveItem(id)) return;
        ItemDatabase.Get(id)?.OnUse?.Invoke(this);
    }

    public void EquipItem(string id) => Inventory.Equip(id);

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
        MaxHealth   += 20;
        MaxMana     += 15;
        BaseAttack  += 3;
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
