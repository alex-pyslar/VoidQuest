using System;
using System.Text;
using Godot;

namespace VoidQuest;

/// <summary>
/// Full RPG HUD — single inventory panel (no tabs):
///   Left  : equipment slots (amulets + ring) + character stats
///   Center: 8×4 item grid  +  crafting recipes below
///   Right : item info + action buttons
/// </summary>
public partial class Hud : CanvasLayer
{
    // ── Static HUD labels / bars ───────────────────────────────────────
    private Label       _scoreLabel;
    private Label       _levelLabel;
    private Label       _coordsLabel;
    private Label       _enemyLabel;
    private ProgressBar _healthBar;
    private Label       _healthLabel;
    private ProgressBar _manaBar;
    private Label       _manaLabel;

    // ── Minimap ────────────────────────────────────────────────────────
    private SubViewport _minimapViewport;
    private TextureRect _minimapTexture;
    private Camera3D    _minimapCamera;

    // ── Overlays ───────────────────────────────────────────────────────
    private Control _pauseMenu;
    private Control _gameOverScreen;
    private Label   _gameOverScoreLabel;

    // ── Runtime refs ───────────────────────────────────────────────────
    private Player        _player;
    private ScoreManager  _scoreManager;
    private LocaleManager _loc;

    private Button _pauseResumeBtn, _pauseMenuBtn, _pauseQuitBtn;
    private Button _goRestartBtn, _goMenuBtn;

    // ── Spell hotbar ───────────────────────────────────────────────────
    private class SpellSlotUI
    {
        public Control        Container;
        public Panel          BgPanel;
        public ColorRect      CdOverlay;
        public Label          KeyLbl;
        public Label          NameLbl;
        public Label          CostLbl;
        public Label          CdTimerLbl;
        public string         SpellId;
        public Color          SpellColor;
        public const float    H = 74f;
        public const float    W = 88f;
    }

    private static readonly (string Id, string Key, Color Color)[] SpellDefs =
    {
        ("fireball",         "Q", new Color(1.0f, 0.45f, 0.10f)),
        ("heal",             "E", new Color(0.20f, 0.90f, 0.30f)),
        ("frost_nova",       "R", new Color(0.40f, 0.80f, 1.00f)),
        ("lightning_strike", "4", new Color(1.0f, 0.90f, 0.15f)),
        ("void_burst",       "5", new Color(0.65f, 0.15f, 0.95f)),
    };

    private HBoxContainer _spellBarNode;
    private SpellSlotUI[] _spellSlots = new SpellSlotUI[5];

    // ── Inventory spell labels ─────────────────────────────────────────
    private Label[] _invSpellLabels = new Label[5];

    // ── Inventory panel ────────────────────────────────────────────────
    private Control _inventoryUI;

    // Grid
    private const int GridCols = 8;
    private const int GridRows = 4;
    private const int SlotPx   = 58;

    private Panel[] _invSlots      = new Panel[GridCols * GridRows];
    private Label[] _invIconLabels = new Label[GridCols * GridRows];
    private Label[] _invCntLabels  = new Label[GridCols * GridRows];

    // Equipment buttons
    private Button _amulet1Btn, _amulet2Btn, _rngBtn, _unequipBtn;

    // Info + actions
    private Label  _itemInfoLabel;
    private Button _actionBtn;
    private Label  _statsLabel;

    // Crafting
    private VBoxContainer _recipeList;

    // Selection
    private int    _selectedSlot    = -1;
    private string _selectedItemId  = null;
    private bool   _selectedIsEquip = false;

    // ──────────────────────────────────────────────────────────────────
    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _loc = GetNode<LocaleManager>("/root/LocaleManager");

        _scoreLabel  = GetNode<Label>("TopPanel/VBox/ScoreLabel");
        _levelLabel  = GetNode<Label>("TopPanel/VBox/LevelLabel");
        _coordsLabel = GetNode<Label>("TopPanel/VBox/CoordsLabel");
        _enemyLabel  = GetNode<Label>("TopPanel/VBox/EnemyLabel");

        _healthBar   = GetNode<ProgressBar>("BarsContainer/HealthRow/HealthBar");
        _healthLabel = GetNode<Label>("BarsContainer/HealthRow/HealthLabel");
        _manaBar     = GetNode<ProgressBar>("BarsContainer/ManaRow/ManaBar");
        _manaLabel   = GetNode<Label>("BarsContainer/ManaRow/ManaLabel");

        _minimapViewport = GetNode<SubViewport>("MinimapContainer/MinimapViewport");
        _minimapTexture  = GetNode<TextureRect>("MinimapContainer/MinimapTexture");
        _minimapCamera   = GetNode<Camera3D>("MinimapContainer/MinimapViewport/MinimapCamera");
        _minimapTexture.Texture = _minimapViewport.GetTexture();

        _pauseMenu          = GetNode<Control>("PauseMenu");
        _gameOverScreen     = GetNode<Control>("GameOverScreen");
        _gameOverScoreLabel = GetNode<Label>("GameOverScreen/CenterContainer/Panel/VBox/ScoreLabel");
        _pauseMenu.Visible      = false;
        _gameOverScreen.Visible = false;

        ConnectBtn("PauseMenu/CenterContainer/Panel/VBox/ResumeBtn",         OnResumePressed);
        ConnectBtn("PauseMenu/CenterContainer/Panel/VBox/MainMenuBtn",        OnMainMenuPressed);
        ConnectBtn("PauseMenu/CenterContainer/Panel/VBox/QuitBtn",            OnQuitPressed);
        ConnectBtn("GameOverScreen/CenterContainer/Panel/VBox/RestartBtn",    OnRestartPressed);
        ConnectBtn("GameOverScreen/CenterContainer/Panel/VBox/GOMainMenuBtn", OnMainMenuPressed);

        _pauseResumeBtn = GetNode<Button>("PauseMenu/CenterContainer/Panel/VBox/ResumeBtn");
        _pauseMenuBtn   = GetNode<Button>("PauseMenu/CenterContainer/Panel/VBox/MainMenuBtn");
        _pauseQuitBtn   = GetNode<Button>("PauseMenu/CenterContainer/Panel/VBox/QuitBtn");
        _goRestartBtn   = GetNode<Button>("GameOverScreen/CenterContainer/Panel/VBox/RestartBtn");
        _goMenuBtn      = GetNode<Button>("GameOverScreen/CenterContainer/Panel/VBox/GOMainMenuBtn");

        _inventoryUI = GetNode<Control>("InventoryUI");
        _inventoryUI.Visible = false;

        _scoreManager = GetNode<ScoreManager>("/root/ScoreManager");
        _scoreManager.ScoreChanged += OnScoreChanged;
        GetNode<GameManager>("/root/GameManager").GameStateChanged += OnGameStateChanged;

        _loc.LangChanged += RefreshLang;
        RefreshLang();
        BuildSpellBar();
        CallDeferred(MethodName.ConnectPlayer);
    }

    public override void _ExitTree()
    {
        if (_scoreManager != null) _scoreManager.ScoreChanged -= OnScoreChanged;
        var gm = GetNodeOrNull<GameManager>("/root/GameManager");
        if (gm != null) gm.GameStateChanged -= OnGameStateChanged;
        if (_loc != null) _loc.LangChanged -= RefreshLang;
        if (_player != null)
        {
            _player.HealthChanged    -= OnHealthChanged;
            _player.ManaChanged      -= OnManaChanged;
            _player.Died             -= OnPlayerDied;
            _player.LevelUp          -= OnLevelUp;
            _player.InventoryToggled -= OnInventoryToggled;
            if (_player.Inventory != null)
                _player.Inventory.InventoryChanged -= RefreshInventoryUI;
        }
    }

    private void RefreshLang()
    {
        if (_pauseResumeBtn != null) _pauseResumeBtn.Text = _loc.T("pause.resume");
        if (_pauseMenuBtn   != null) _pauseMenuBtn.Text   = _loc.T("pause.menu");
        if (_pauseQuitBtn   != null) _pauseQuitBtn.Text   = _loc.T("menu.quit");
        if (_goRestartBtn   != null) _goRestartBtn.Text   = _loc.T("gameover.restart");
        if (_goMenuBtn      != null) _goMenuBtn.Text      = _loc.T("gameover.menu");
        if (_scoreLabel != null && _scoreManager != null)
            _scoreLabel.Text = $"{_loc.T("hud.score")} {_scoreManager.Score}";
        BuildSpellBar();
        if (_inventoryUI != null) BuildInventoryPanel();
        if (_player != null) RefreshInventoryUI();
    }

    private void ConnectPlayer()
    {
        _player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (_player == null)
        {
            if (!IsInsideTree()) return;
            GetTree().CreateTimer(0.25f).Timeout += ConnectPlayer;
            return;
        }
        _player.HealthChanged    += OnHealthChanged;
        _player.ManaChanged      += OnManaChanged;
        _player.Died             += OnPlayerDied;
        _player.LevelUp          += OnLevelUp;
        _player.InventoryToggled += OnInventoryToggled;
        _player.Inventory.InventoryChanged += RefreshInventoryUI;
        OnHealthChanged(_player.CurrentHealth, _player.MaxHealth);
        OnManaChanged(_player.CurrentMana, _player.MaxMana);
        OnLevelUp(_player.Level);
    }

    public override void _Process(double delta)
    {
        if (_player == null || !_player.IsInsideTree()) return;
        var pos = _player.GlobalPosition;
        _coordsLabel.Text = $"X:{pos.X:F0}  Y:{pos.Y:F0}  Z:{pos.Z:F0}";
        _enemyLabel.Text  = $"{_loc.T("hud.enemies")} {GetTree().GetNodesInGroup("enemy").Count}";
        if (_minimapCamera != null)
        {
            var c = _minimapCamera.GlobalPosition;
            c.X = pos.X; c.Z = pos.Z;
            _minimapCamera.GlobalPosition = c;
        }
        UpdateSpellBar();
        if (_inventoryUI != null && _inventoryUI.Visible)
            UpdateInventorySpells();
    }

    // ═════════════════════════════════════════════════════════════════
    // SPELL HOTBAR
    // ═════════════════════════════════════════════════════════════════

    private void BuildSpellBar()
    {
        _spellBarNode = GetNodeOrNull<HBoxContainer>("SpellBar");
        if (_spellBarNode == null) return;

        foreach (Node c in _spellBarNode.GetChildren()) c.QueueFree();
        _spellBarNode.AddThemeConstantOverride("separation", 4);

        for (int i = 0; i < SpellDefs.Length; i++)
        {
            var (id, key, color) = SpellDefs[i];
            _spellSlots[i] = CreateSpellSlot(id, key, color);
            _spellBarNode.AddChild(_spellSlots[i].Container);
        }
    }

    private SpellSlotUI CreateSpellSlot(string spellId, string key, Color spellColor)
    {
        var slot = new SpellSlotUI { SpellId = spellId, SpellColor = spellColor };

        // Outer control — fixed size so HBoxContainer sizes it correctly
        var container = new Control();
        container.CustomMinimumSize = new Vector2(SpellSlotUI.W, SpellSlotUI.H);

        // Background panel
        var bgPanel = new Panel();
        bgPanel.AnchorRight  = 1f; bgPanel.AnchorBottom = 1f;
        var ps = new StyleBoxFlat();
        ps.BgColor       = new Color(0.04f, 0.04f, 0.12f, 0.92f);
        ps.BorderColor   = spellColor * 0.55f;
        ps.BorderWidthBottom = ps.BorderWidthTop = ps.BorderWidthLeft = ps.BorderWidthRight = 2;
        ps.CornerRadiusBottomLeft = ps.CornerRadiusBottomRight =
        ps.CornerRadiusTopLeft   = ps.CornerRadiusTopRight    = 5;
        bgPanel.AddThemeStyleboxOverride("panel", ps);
        container.AddChild(bgPanel);

        // Cooldown darkening overlay (top-down fill, height = ratio × H)
        var cdOverlay = new ColorRect();
        cdOverlay.Color        = new Color(0f, 0f, 0f, 0.72f);
        cdOverlay.AnchorLeft   = 0f; cdOverlay.AnchorRight  = 1f;
        cdOverlay.AnchorTop    = 0f; cdOverlay.AnchorBottom = 0f;
        cdOverlay.OffsetBottom = 0f;
        cdOverlay.Visible      = false;
        container.AddChild(cdOverlay);

        // Content VBox (key, name, cost) — fills the slot
        var vbox = new VBoxContainer();
        vbox.AnchorRight  = 1f; vbox.AnchorBottom = 1f;
        vbox.AddThemeConstantOverride("separation", 1);
        // top padding
        var pad = new Control { CustomMinimumSize = new Vector2(0, 4) };
        vbox.AddChild(pad);

        var keyLbl = new Label { Text = $"[{key}]" };
        keyLbl.HorizontalAlignment = HorizontalAlignment.Center;
        keyLbl.AddThemeFontSizeOverride("font_size", 17);
        keyLbl.AddThemeColorOverride("font_color", spellColor);
        vbox.AddChild(keyLbl);

        var nameLbl = new Label { Text = _loc.T($"spell.{spellId}") };
        nameLbl.HorizontalAlignment = HorizontalAlignment.Center;
        nameLbl.AddThemeFontSizeOverride("font_size", 12);
        nameLbl.AddThemeColorOverride("font_color", new Color(0.92f, 0.92f, 0.92f));
        vbox.AddChild(nameLbl);

        float mp = DataLoader.GetSpell(spellId)?.ManaCost ?? 0f;
        var costLbl = new Label { Text = $"{(int)mp} MP" };
        costLbl.HorizontalAlignment = HorizontalAlignment.Center;
        costLbl.AddThemeFontSizeOverride("font_size", 10);
        costLbl.AddThemeColorOverride("font_color", new Color(0.45f, 0.65f, 1.0f));
        vbox.AddChild(costLbl);

        container.AddChild(vbox);

        // Cooldown timer label — centred on top of everything
        var cdTimerLbl = new Label { Text = "" };
        cdTimerLbl.AnchorRight  = 1f; cdTimerLbl.AnchorBottom = 1f;
        cdTimerLbl.HorizontalAlignment = HorizontalAlignment.Center;
        cdTimerLbl.VerticalAlignment   = VerticalAlignment.Center;
        cdTimerLbl.AddThemeFontSizeOverride("font_size", 20);
        cdTimerLbl.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.95f));
        container.AddChild(cdTimerLbl);

        slot.Container  = container;
        slot.BgPanel    = bgPanel;
        slot.CdOverlay  = cdOverlay;
        slot.KeyLbl     = keyLbl;
        slot.NameLbl    = nameLbl;
        slot.CostLbl    = costLbl;
        slot.CdTimerLbl = cdTimerLbl;
        return slot;
    }

    private void UpdateSpellBar()
    {
        if (_spellSlots == null) return;
        for (int i = 0; i < SpellDefs.Length; i++)
        {
            var slot = _spellSlots[i];
            if (slot?.Container == null || !GodotObject.IsInstanceValid(slot.Container)) continue;

            float ratio = 0f; bool canCast = false; bool isLocked = false; float manaCost = 0f;
            if (_player != null)
                _player.GetSpellStatus(slot.SpellId, out ratio, out canCast, out isLocked, out manaCost);

            // Cooldown overlay height
            float overlayH = ratio * SpellSlotUI.H;
            slot.CdOverlay.Visible      = overlayH > 0.5f;
            slot.CdOverlay.OffsetBottom = overlayH;

            // Timer label
            if (ratio > 0.01f)
            {
                float maxCd    = DataLoader.GetSpell(slot.SpellId)?.Cooldown ?? 1f;
                float remaining = ratio * maxCd;
                slot.CdTimerLbl.Text = remaining >= 1f ? $"{remaining:F0}" : $"{remaining:F1}";
            }
            else
            {
                slot.CdTimerLbl.Text = "";
            }

            // Border colour and overall alpha
            Color border;
            float alpha = 1f;
            if (isLocked)
            {
                border = new Color(0.28f, 0.28f, 0.28f);
                alpha  = 0.45f;
            }
            else if (ratio > 0.01f)
            {
                border = new Color(slot.SpellColor.R * 0.35f, slot.SpellColor.G * 0.35f, slot.SpellColor.B * 0.35f, 1f);
            }
            else if (!canCast)   // not enough mana
            {
                border = new Color(0.7f, 0.30f, 0.0f);
            }
            else                 // ready
            {
                border = slot.SpellColor;
            }

            if (slot.BgPanel.GetThemeStylebox("panel") is StyleBoxFlat style)
                style.BorderColor = border;

            slot.Container.Modulate = new Color(1f, 1f, 1f, alpha);
        }
    }

    private void UpdateInventorySpells()
    {
        for (int i = 0; i < SpellDefs.Length; i++)
        {
            var lbl = _invSpellLabels[i];
            if (lbl == null || !GodotObject.IsInstanceValid(lbl)) continue;

            var (id, key, color) = SpellDefs[i];
            float ratio = 0f; bool canCast = false; bool isLocked = false; float manaCost = 0f;
            if (_player != null)
                _player.GetSpellStatus(id, out ratio, out canCast, out isLocked, out manaCost);

            string name = _loc.T($"spell.{id}");
            string status;
            if (isLocked)
                status = _loc.T("hud.spell_locked");
            else if (ratio > 0.01f)
            {
                float maxCd    = DataLoader.GetSpell(id)?.Cooldown ?? 1f;
                float remaining = ratio * maxCd;
                status = $"{remaining:F1}s";
            }
            else
                status = _loc.T("hud.spell_ready");

            lbl.Text = $"[{key}] {name}  {(int)manaCost}mp  {status}";
            lbl.AddThemeColorOverride("font_color", isLocked ? new Color(0.4f, 0.4f, 0.4f) : color);
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // BUILD INVENTORY PANEL  —  single panel, no tabs
    //
    //  ┌── Left (170px) ──┬── Center (expand) ────────────────┬── Right (210px) ──┐
    //  │ СНАРЯЖЕНИЕ       │ РЮКЗАК (32)                       │ О ПРЕДМЕТЕ        │
    //  │ [Амулет 1]       │ □ □ □ □ □ □ □ □                   │                   │
    //  │ [Амулет 2]       │ □ □ □ □ □ □ □ □                   │ [info text]       │
    //  │ [Кольцо  ]       │ □ □ □ □ □ □ □ □                   │                   │
    //  │ [СНЯТЬ   ]       │ □ □ □ □ □ □ □ □                   │ [action btn]      │
    //  │ ─────────────    │ ─── КРАФТ ─────────────────────── │ [unequip btn]     │
    //  │ ПЕРСОНАЖ         │ recipe … [СОЗДАТЬ]                 │ ─────────────     │
    //  │ АТК / ЗАЩ / МАГ │ recipe … [СОЗДАТЬ]                 │ ПЕРСОНАЖ          │
    //  │ ◆ способности    │ …                                  │ ATK / DEF / stats │
    //  └──────────────────┴────────────────────────────────────┴───────────────────┘
    // ═════════════════════════════════════════════════════════════════
    private void BuildInventoryPanel()
    {
        var invPanel = GetNode<PanelContainer>("InventoryUI/InvPanel");
        foreach (Node c in invPanel.GetChildren()) c.QueueFree();

        var root = new HBoxContainer();
        root.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        root.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        root.AddThemeConstantOverride("separation", 0);
        invPanel.AddChild(root);

        root.AddChild(BuildLeftColumn());
        root.AddChild(MakeVSep());
        root.AddChild(BuildCenterColumn());
        root.AddChild(MakeVSep());
        root.AddChild(BuildRightColumn());
    }

    // ── LEFT: equipment + character stats ─────────────────────────────
    private Control BuildLeftColumn()
    {
        var col = new VBoxContainer();
        col.CustomMinimumSize = new Vector2(170, 0);
        col.AddThemeConstantOverride("separation", 6);

        // Equipment header
        col.AddChild(MakeSectionLabel(_loc.T("hud.equipment"), new Color(0.65f, 0.30f, 0.90f)));
        col.AddChild(new HSeparator());

        _amulet1Btn = MakeEquipBtn(_loc.T("hud.amulet") + " 1");
        _amulet2Btn = MakeEquipBtn(_loc.T("hud.amulet") + " 2");
        _rngBtn     = MakeEquipBtn(_loc.T("hud.ring"));

        _amulet1Btn.Pressed += () => OnEquipSlotPressed("amulet1");
        _amulet2Btn.Pressed += () => OnEquipSlotPressed("amulet2");
        _rngBtn.Pressed     += () => OnEquipSlotPressed("ring");

        col.AddChild(_amulet1Btn);
        col.AddChild(_amulet2Btn);
        col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 2) });

        var ringRow = new HBoxContainer();
        ringRow.AddThemeConstantOverride("separation", 4);
        var ringSep = new HSeparator();
        ringSep.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        ringRow.AddChild(ringSep);
        col.AddChild(_rngBtn);
        col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });

        _unequipBtn = new Button();
        _unequipBtn.Text = _loc.T("hud.unequip");
        _unequipBtn.Disabled = true;
        _unequipBtn.AddThemeFontSizeOverride("font_size", 11);
        _unequipBtn.Pressed += OnUnequipPressed;
        col.AddChild(_unequipBtn);

        col.AddChild(new HSeparator());
        col.AddChild(MakeSectionLabel(_loc.T("hud.spells"), new Color(0.55f, 0.35f, 0.90f)));

        for (int i = 0; i < SpellDefs.Length; i++)
        {
            var lbl = MakeLabel("", 10, SpellDefs[i].Color);
            lbl.AutowrapMode = TextServer.AutowrapMode.Off;
            col.AddChild(lbl);
            _invSpellLabels[i] = lbl;
        }

        col.AddChild(new HSeparator());
        col.AddChild(MakeSectionLabel(_loc.T("hud.char"), new Color(0.40f, 0.65f, 0.45f)));

        _statsLabel = new Label();
        _statsLabel.AddThemeFontSizeOverride("font_size", 11);
        _statsLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.82f, 0.82f));
        _statsLabel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        col.AddChild(_statsLabel);

        return col;
    }

    private Button MakeEquipBtn(string label)
    {
        var btn = new Button();
        btn.CustomMinimumSize = new Vector2(166, 52);
        btn.Text = $"{label}\n─────\n{_loc.T("hud.none")}";
        btn.AddThemeFontSizeOverride("font_size", 10);
        btn.Modulate = new Color(0.5f, 0.5f, 0.5f);
        return btn;
    }

    // ── CENTER: inventory grid + crafting below ────────────────────────
    private Control BuildCenterColumn()
    {
        var col = new VBoxContainer();
        col.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        col.AddThemeConstantOverride("separation", 5);

        // ── Backpack ──
        col.AddChild(MakeSectionLabel($"{_loc.T("hud.backpack")}  ({GridCols * GridRows})",
            new Color(0.96f, 0.78f, 0.12f)));

        var grid = new GridContainer();
        grid.Columns = GridCols;
        grid.AddThemeConstantOverride("h_separation", 3);
        grid.AddThemeConstantOverride("v_separation", 3);
        col.AddChild(grid);

        for (int i = 0; i < GridCols * GridRows; i++)
        {
            int idx = i;
            var slot = new Panel();
            slot.CustomMinimumSize = new Vector2(SlotPx, SlotPx);

            var btn = new Button { Flat = true };
            btn.AnchorRight = 1f; btn.AnchorBottom = 1f;
            btn.Pressed += () => OnSlotPressed(idx);

            var icon = new Label();
            icon.AnchorRight = 1f; icon.AnchorBottom = 0.65f;
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            icon.VerticalAlignment   = VerticalAlignment.Center;
            icon.AddThemeFontSizeOverride("font_size", 12);

            var cnt = new Label();
            cnt.AnchorLeft = 0.5f; cnt.AnchorTop = 0.6f;
            cnt.AnchorRight = 1f;  cnt.AnchorBottom = 1f;
            cnt.OffsetRight = -2;  cnt.OffsetBottom = -2;
            cnt.HorizontalAlignment = HorizontalAlignment.Right;
            cnt.VerticalAlignment   = VerticalAlignment.Bottom;
            cnt.AddThemeFontSizeOverride("font_size", 9);

            slot.AddChild(btn); slot.AddChild(icon); slot.AddChild(cnt);
            grid.AddChild(slot);

            _invSlots[i]      = slot;
            _invIconLabels[i] = icon;
            _invCntLabels[i]  = cnt;
            SetSlotEmpty(i);
        }

        // ── Crafting separator ──
        col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
        col.AddChild(MakeDivider(_loc.T("hud.crafting"), new Color(0.96f, 0.78f, 0.12f)));

        var craftHint = MakeLabel(_loc.T("craft.hint"), 9, new Color(0.50f, 0.50f, 0.50f));
        craftHint.AutowrapMode = TextServer.AutowrapMode.Word;
        col.AddChild(craftHint);

        // ── Crafting recipe list ──
        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        scroll.CustomMinimumSize = new Vector2(0, 80);
        col.AddChild(scroll);

        _recipeList = new VBoxContainer();
        _recipeList.AddThemeConstantOverride("separation", 3);
        _recipeList.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(_recipeList);

        return col;
    }

    // ── RIGHT: item info + action buttons ─────────────────────────────
    private Control BuildRightColumn()
    {
        var col = new VBoxContainer();
        col.CustomMinimumSize = new Vector2(210, 0);
        col.AddThemeConstantOverride("separation", 6);

        col.AddChild(MakeSectionLabel(_loc.T("hud.info_title"), new Color(0.0f, 0.72f, 0.90f)));
        col.AddChild(new HSeparator());

        _itemInfoLabel = new Label();
        _itemInfoLabel.Text = _loc.T("hud.item_info");
        _itemInfoLabel.AutowrapMode = TextServer.AutowrapMode.Word;
        _itemInfoLabel.AddThemeFontSizeOverride("font_size", 12);
        _itemInfoLabel.AddThemeColorOverride("font_color", new Color(0.88f, 0.88f, 0.88f));
        _itemInfoLabel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        col.AddChild(_itemInfoLabel);

        col.AddChild(new HSeparator());

        _actionBtn = new Button { Text = "─", Disabled = true };
        _actionBtn.AddThemeFontSizeOverride("font_size", 13);
        _actionBtn.Pressed += OnActionPressed;
        col.AddChild(_actionBtn);

        _unequipBtn = new Button { Text = _loc.T("hud.unequip"), Disabled = true };
        _unequipBtn.AddThemeFontSizeOverride("font_size", 11);
        _unequipBtn.Pressed += OnUnequipPressed;
        col.AddChild(_unequipBtn);

        return col;
    }

    // ═════════════════════════════════════════════════════════════════
    // REFRESH
    // ═════════════════════════════════════════════════════════════════

    private void RefreshInventoryUI()
    {
        if (_player == null) return;

        for (int i = 0; i < _invSlots.Length; i++) SetSlotEmpty(i);

        var items = _player.Inventory.Items;
        for (int i = 0; i < items.Count && i < _invSlots.Length; i++)
            SetSlotItem(i, items[i]);

        if (_selectedSlot >= 0 && _selectedSlot < items.Count)
            HighlightSlot(_selectedSlot, true);

        RefreshEquipSlots();
        RefreshStatsUI();
        RefreshCraftingUI();
    }

    private void SetSlotEmpty(int idx)
    {
        _invSlots[idx].AddThemeStyleboxOverride("panel",
            MakeSlotStyle(new Color(0.07f, 0.07f, 0.18f), new Color(0.17f, 0.17f, 0.28f), 1));
        _invIconLabels[idx].Text = "";
        _invCntLabels[idx].Text  = "";
    }

    private void SetSlotItem(int idx, InventoryItem item)
    {
        var def = item.Def;
        if (def == null) { SetSlotEmpty(idx); return; }

        Color cat = CategoryColor(def.Category);
        Color bg  = new Color(cat.R * 0.16f, cat.G * 0.16f, cat.B * 0.16f, 1f);
        _invSlots[idx].AddThemeStyleboxOverride("panel", MakeSlotStyle(bg, cat, 2));
        _invIconLabels[idx].Text = Abbrev(_loc.ItemName(def.Id));
        _invIconLabels[idx].AddThemeColorOverride("font_color", cat);
        _invCntLabels[idx].Text  = item.Count > 1 ? item.Count.ToString() : "";
        _invCntLabels[idx].AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));
    }

    private void HighlightSlot(int idx, bool on)
    {
        if (idx < 0 || idx >= _invSlots.Length) return;
        if (_invSlots[idx].GetThemeStylebox("panel") is not StyleBoxFlat s) return;
        s.BorderColor = on ? new Color(0.96f, 0.78f, 0.12f) : new Color(0.17f, 0.17f, 0.28f);
        s.BorderWidthBottom = s.BorderWidthTop = s.BorderWidthLeft = s.BorderWidthRight = on ? 3 : 1;
    }

    private void RefreshEquipSlots()
    {
        if (_player == null) return;
        var inv = _player.Inventory;
        UpdateEquipBtn(_amulet1Btn, _loc.T("hud.amulet") + " 1", inv.EquippedAmulet1);
        UpdateEquipBtn(_amulet2Btn, _loc.T("hud.amulet") + " 2", inv.EquippedAmulet2);
        UpdateEquipBtn(_rngBtn,     _loc.T("hud.ring"),           inv.EquippedRing);
    }

    private void UpdateEquipBtn(Button btn, string slot, string itemId)
    {
        string name  = itemId != null ? _loc.ItemName(itemId) : _loc.T("hud.none");
        btn.Text     = $"{slot}\n─────\n{name}";
        btn.Modulate = itemId != null ? new Color(0.75f, 1f, 0.75f) : new Color(0.55f, 0.55f, 0.55f);
    }

    private void RefreshStatsUI()
    {
        if (_player == null || _statsLabel == null) return;
        var sb = new StringBuilder();
        sb.AppendLine($"{_loc.T("stat.atk")}: {_player.TotalAttack}");
        sb.AppendLine($"{_loc.T("stat.def")}: {_player.TotalDefense}");
        sb.AppendLine($"{_loc.T("stat.mag")}: {_player.TotalMagic}");
        sb.AppendLine("──────────");
        sb.AppendLine($"{_loc.T("stat.lv")}: {_player.Level}");
        sb.AppendLine($"{_loc.T("stat.hp")}: {_player.CurrentHealth}/{_player.MaxHealth}");
        sb.AppendLine($"{_loc.T("stat.mp")}: {_player.CurrentMana}/{_player.MaxMana}");
        sb.AppendLine($"{_loc.T("stat.xp")}: {_player.Experience}/{_player.XpToNext}");

        string[] abilities = { "fireball_boost","frost_boost","life_regen","blood_drain","lightning_strike","void_burst" };
        bool hdr = false;
        foreach (var ab in abilities)
        {
            if (!_player.HasAbility(ab)) continue;
            if (!hdr) { sb.AppendLine("──────────"); hdr = true; }
            sb.AppendLine($"◆ {_loc.T($"ability.{ab}.short")}");
        }
        _statsLabel.Text = sb.ToString();
    }

    private void RefreshCraftingUI()
    {
        if (_player == null || _recipeList == null) return;
        foreach (Node c in _recipeList.GetChildren()) c.QueueFree();

        foreach (var recipe in CraftingDatabase.Recipes)
        {
            var resultDef = ItemDatabase.Get(recipe.ResultId);
            if (resultDef == null) continue;

            bool canCraft = recipe.CanCraft(_player.Inventory);

            var panel = new PanelContainer();
            var ps    = new StyleBoxFlat();
            ps.BgColor = canCraft ? new Color(0.05f, 0.13f, 0.05f) : new Color(0.06f, 0.06f, 0.13f);
            panel.AddThemeStyleboxOverride("panel", ps);

            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 6);
            panel.AddChild(hbox);

            // Result name
            var rSb = new StringBuilder(_loc.ItemName(recipe.ResultId));
            if (recipe.ResultCount > 1) rSb.Append($" ×{recipe.ResultCount}");
            if (resultDef.AbilityId != null)
                rSb.Append($"\n  ◆{_loc.T($"ability.{resultDef.AbilityId}.short")}");

            var rLbl = MakeLabel(rSb.ToString(), 11,
                canCraft ? new Color(0.35f, 1f, 0.35f) : new Color(0.60f, 0.60f, 0.60f));
            rLbl.CustomMinimumSize = new Vector2(150, 0);
            rLbl.AutowrapMode = TextServer.AutowrapMode.Word;
            hbox.AddChild(rLbl);

            // Ingredients
            var ingSb = new StringBuilder();
            foreach (var kvp in recipe.Ingredients)
            {
                int have = _player.Inventory.GetCount(kvp.Key);
                ingSb.Append($"{_loc.ItemName(kvp.Key)} {have}/{kvp.Value}  ");
            }
            var ingLbl = MakeLabel(ingSb.ToString().TrimEnd(), 9, new Color(0.65f, 0.65f, 0.65f));
            ingLbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            ingLbl.AutowrapMode        = TextServer.AutowrapMode.Word;
            hbox.AddChild(ingLbl);

            var btn = new Button { Text = _loc.T("hud.craft"), Disabled = !canCraft };
            btn.AddThemeFontSizeOverride("font_size", 11);
            string rid = recipe.ResultId;
            btn.Pressed += () => DoCraft(rid);
            hbox.AddChild(btn);

            _recipeList.AddChild(panel);
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // INTERACTIONS
    // ═════════════════════════════════════════════════════════════════

    private void OnSlotPressed(int idx)
    {
        if (_selectedSlot >= 0) HighlightSlot(_selectedSlot, false);
        var items = _player?.Inventory?.Items;
        if (items == null || idx >= items.Count)
        {
            _selectedSlot = -1; _selectedItemId = null; _selectedIsEquip = false;
            ShowItemInfo(null); return;
        }
        _selectedSlot    = idx;
        _selectedItemId  = items[idx].ItemId;
        _selectedIsEquip = false;
        HighlightSlot(idx, true);
        ShowItemInfo(items[idx]);
    }

    private void OnEquipSlotPressed(string slotType)
    {
        if (_player == null) return;
        var inv = _player.Inventory;
        string id = slotType switch
        {
            "amulet1" => inv.EquippedAmulet1,
            "amulet2" => inv.EquippedAmulet2,
            "ring"    => inv.EquippedRing,
            _         => null,
        };
        if (id == null) return;
        if (_selectedSlot >= 0) HighlightSlot(_selectedSlot, false);
        _selectedSlot = -1; _selectedItemId = id; _selectedIsEquip = true;
        ShowItemInfo(new InventoryItem(id, 1));
        _unequipBtn.Disabled = false;
    }

    private void ShowItemInfo(InventoryItem item)
    {
        _unequipBtn.Disabled = !_selectedIsEquip;
        if (item == null)
        {
            _itemInfoLabel.Text = _loc.T("hud.item_info");
            _actionBtn.Text = "─"; _actionBtn.Disabled = true;
            return;
        }
        var def = item.Def;
        if (def == null)
        {
            _itemInfoLabel.Text = $"? {item.ItemId}";
            _actionBtn.Text = "─"; _actionBtn.Disabled = true;
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"[{LocalisedCategory(def.Category)}]");
        sb.AppendLine(_loc.ItemName(def.Id));
        string desc = _loc.ItemDesc(def.Id);
        if (!string.IsNullOrEmpty(desc)) { sb.AppendLine(); sb.AppendLine(desc); }
        if (def.AtkBonus != 0) { sb.AppendLine(); sb.Append($"ATK {(def.AtkBonus>0?"+":"")}{def.AtkBonus}"); }
        if (def.DefBonus != 0) { sb.AppendLine(); sb.Append($"DEF {(def.DefBonus>0?"+":"")}{def.DefBonus}"); }
        if (def.MagBonus != 0) { sb.AppendLine(); sb.Append($"MAG {(def.MagBonus>0?"+":"")}{def.MagBonus}"); }
        if (def.AbilityId != null) { sb.AppendLine(); sb.AppendLine(); sb.Append($"◆ {_loc.T($"ability.{def.AbilityId}")}"); }
        if (item.Count > 1) { sb.AppendLine(); sb.Append($"×{item.Count}"); }
        _itemInfoLabel.Text = sb.ToString();

        switch (def.Category)
        {
            case ItemCategory.Consumable:
                _actionBtn.Text = _loc.T("hud.use"); _actionBtn.Disabled = false; break;
            case ItemCategory.Amulet:
            case ItemCategory.Ring:
            case ItemCategory.Weapon:
            case ItemCategory.Armor:
            case ItemCategory.Shield:
                _actionBtn.Text     = _selectedIsEquip ? _loc.T("hud.equipped") : _loc.T("hud.equip");
                _actionBtn.Disabled = _selectedIsEquip;
                break;
            default:
                _actionBtn.Text = "─"; _actionBtn.Disabled = true; break;
        }
    }

    private void OnActionPressed()
    {
        if (_player == null || _selectedItemId == null) return;
        var def = ItemDatabase.Get(_selectedItemId);
        if (def == null) return;
        if (def.Category == ItemCategory.Consumable)
            _player.UseItem(_selectedItemId);
        else
            _player.EquipItem(_selectedItemId);
        _selectedSlot = -1; _selectedItemId = null; _selectedIsEquip = false;
        ShowItemInfo(null);
        RefreshInventoryUI();
    }

    private void OnUnequipPressed()
    {
        if (_player == null || _selectedItemId == null || !_selectedIsEquip) return;
        _player.Inventory.Unequip(_selectedItemId);
        _selectedItemId = null; _selectedIsEquip = false;
        ShowItemInfo(null);
        RefreshInventoryUI();
    }

    private void DoCraft(string resultId)
    {
        if (_player == null) return;
        if (_player.Inventory.TryCraft(resultId, out string err))
            GD.Print($"[Craft] {_loc.ItemName(resultId)}");
        else
            GD.Print($"[Craft] Failed: {err}");
        RefreshInventoryUI();
    }

    // ═════════════════════════════════════════════════════════════════
    // SIGNAL HANDLERS
    // ═════════════════════════════════════════════════════════════════

    private void OnScoreChanged(int v)
    {
        if (!GodotObject.IsInstanceValid(_scoreLabel)) return;
        _scoreLabel.Text = $"{_loc.T("hud.score")} {v}";
    }

    private void OnHealthChanged(int cur, int max)
    {
        _healthBar.Value  = (double)cur / max * 100.0;
        _healthLabel.Text = $"{cur}/{max}";
        float t = (float)cur / max;
        _healthBar.Modulate = new Color(1f - t * 0.4f, 0.2f + t * 0.8f, 0.1f, 1f);
    }

    private void OnManaChanged(int cur, int max)
    {
        _manaBar.Value = (double)cur / max * 100.0;
        _manaLabel.Text = $"{cur}/{max}";
    }

    private void OnLevelUp(int level)
    {
        if (_player == null) return;
        _levelLabel.Text = $"{_loc.T("hud.level")}.{level}  {_loc.T("stat.xp")}: {_player.Experience}/{_player.XpToNext}";
        RefreshStatsUI();
    }

    private void OnPlayerDied()
    {
        _gameOverScreen.Visible  = true;
        _gameOverScoreLabel.Text = $"{_loc.T("gameover.score")} {_scoreManager.Score}";
    }

    private void OnGameStateChanged(int stateInt)
        => _pauseMenu.Visible = ((GameState)stateInt == GameState.Paused);

    private void OnInventoryToggled(bool open)
    {
        _inventoryUI.Visible = open;
        if (open) RefreshInventoryUI();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
        {
            var gm = GetNode<GameManager>("/root/GameManager");
            if (gm?.State is GameState.Playing or GameState.Paused)
            {
                gm.TogglePause();
                GetViewport().SetInputAsHandled();
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // HELPERS
    // ═════════════════════════════════════════════════════════════════

    private string LocalisedCategory(ItemCategory cat) => cat switch
    {
        ItemCategory.Amulet     => _loc.T("hud.amulet"),
        ItemCategory.Ring       => _loc.T("hud.ring"),
        ItemCategory.Weapon     => _loc.T("hud.weapon"),
        ItemCategory.Armor      => _loc.T("hud.armor"),
        ItemCategory.Shield     => _loc.T("hud.shield"),
        ItemCategory.Consumable => "Consumable",
        ItemCategory.Material   => "Material",
        _                       => cat.ToString(),
    };

    private static Label MakeSectionLabel(string text, Color color)
    {
        var l = new Label();
        l.Text = text;
        l.HorizontalAlignment = HorizontalAlignment.Center;
        l.AddThemeFontSizeOverride("font_size", 14);
        l.AddThemeColorOverride("font_color", color);
        return l;
    }

    private static Control MakeDivider(string text, Color color)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        var left = new HSeparator(); left.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var lbl  = new Label { Text = text };
        lbl.AddThemeFontSizeOverride("font_size", 13);
        lbl.AddThemeColorOverride("font_color", color);
        var right = new HSeparator(); right.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(left); row.AddChild(lbl); row.AddChild(right);
        return row;
    }

    private static Control MakeVSep()
    {
        var sep = new VSeparator();
        return sep;
    }

    private static StyleBoxFlat MakeSlotStyle(Color bg, Color border, int bw)
    {
        var s = new StyleBoxFlat { BgColor = bg, BorderColor = border };
        s.BorderWidthBottom = s.BorderWidthTop = s.BorderWidthLeft = s.BorderWidthRight = bw;
        s.CornerRadiusBottomLeft = s.CornerRadiusBottomRight =
        s.CornerRadiusTopLeft   = s.CornerRadiusTopRight    = 0;
        return s;
    }

    private static Label MakeLabel(string text, int fontSize, Color color,
        HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var l = new Label { Text = text, HorizontalAlignment = align };
        l.AddThemeFontSizeOverride("font_size", fontSize);
        l.AddThemeColorOverride("font_color", color);
        return l;
    }

    private static string Abbrev(string name)
    {
        if (string.IsNullOrEmpty(name)) return "??";
        var w = name.Split(' ');
        return (w.Length >= 2 ? $"{w[0][0]}{w[1][0]}" : name.Length >= 2 ? name[..2] : name).ToUpper();
    }

    private static Color CategoryColor(ItemCategory cat) => cat switch
    {
        ItemCategory.Weapon     => new Color(0.95f, 0.25f, 0.25f),
        ItemCategory.Armor      => new Color(0.25f, 0.50f, 0.95f),
        ItemCategory.Shield     => new Color(0.15f, 0.85f, 0.90f),
        ItemCategory.Ring       => new Color(0.95f, 0.85f, 0.10f),
        ItemCategory.Amulet     => new Color(0.75f, 0.20f, 0.95f),
        ItemCategory.Consumable => new Color(0.20f, 0.85f, 0.30f),
        ItemCategory.Material   => new Color(0.75f, 0.65f, 0.50f),
        _                       => new Color(0.70f, 0.70f, 0.70f),
    };

    private void ConnectBtn(string path, Action callback)
    {
        var btn = GetNode<Button>(path);
        btn.ProcessMode = ProcessModeEnum.Always;
        btn.Pressed    += callback;
    }

    public void OnResumePressed()   => GetNode<GameManager>("/root/GameManager").TogglePause();
    public void OnMainMenuPressed() => GetNode<GameManager>("/root/GameManager").ReturnToMenu();
    public void OnRestartPressed()  { GetTree().Paused = false; GetTree().ReloadCurrentScene(); }
    public void OnQuitPressed()     => GetTree().Quit();
}
