using System;
using System.Text;
using Godot;

namespace VoidQuest;

/// <summary>
/// Full RPG HUD:
///   – Score, level/XP, coords, enemy count (top-left)
///   – Health / mana bars (bottom-left)
///   – Crosshair (centre)
///   – Spell hotbar (bottom-centre)
///   – Minimap (top-right)
///   – Grid Inventory + Crafting panel (I / Tab)
///   – Pause menu (Esc)
///   – Game-over screen
/// </summary>
public partial class Hud : CanvasLayer
{
    // ── Static HUD labels / bars (from scene) ─────────────────────────
    private Label       _scoreLabel;
    private Label       _levelLabel;
    private Label       _coordsLabel;
    private Label       _enemyLabel;
    private ProgressBar _healthBar;
    private Label       _healthLabel;
    private ProgressBar _manaBar;
    private Label       _manaLabel;

    // ── Minimap (from scene) ──────────────────────────────────────────
    private SubViewport _minimapViewport;
    private TextureRect _minimapTexture;
    private Camera3D    _minimapCamera;

    // ── Overlays (from scene) ─────────────────────────────────────────
    private Control _pauseMenu;
    private Control _gameOverScreen;
    private Label   _gameOverScoreLabel;

    // ── Runtime refs ──────────────────────────────────────────────────
    private Player       _player;
    private ScoreManager _scoreManager;
    private LocaleManager _loc;

    // Pause / game-over button refs for language refresh
    private Button _pauseResumeBtn;
    private Button _pauseMenuBtn;
    private Button _pauseQuitBtn;
    private Button _goRestartBtn;
    private Button _goMenuBtn;

    // ── Inventory panel root (from scene) ─────────────────────────────
    private Control _inventoryUI;

    // ── Grid inventory (built in code) ────────────────────────────────
    private const int GridCols = 8;
    private const int GridRows = 4;
    private const int SlotPx   = 62;

    private Panel[] _invSlots      = new Panel[GridCols * GridRows];
    private Label[] _invIconLabels = new Label[GridCols * GridRows];
    private Label[] _invCntLabels  = new Label[GridCols * GridRows];

    // Equipment slot buttons
    private Button _wepBtn, _armBtn, _shldBtn, _rngBtn;
    private Button _unequipBtn;

    // Info panel
    private Label  _statsLabel;
    private Label  _itemInfoLabel;
    private Button _actionBtn;

    // Crafting
    private VBoxContainer _recipeList;

    // Selection state
    private int    _selectedSlot   = -1;
    private string _selectedItemId = null;
    private bool   _selectedIsEquip = false;

    // ─────────────────────────────────────────────────────────────────
    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _loc = GetNode<LocaleManager>("/root/LocaleManager");

        // Top panel
        _scoreLabel  = GetNode<Label>("TopPanel/VBox/ScoreLabel");
        _levelLabel  = GetNode<Label>("TopPanel/VBox/LevelLabel");
        _coordsLabel = GetNode<Label>("TopPanel/VBox/CoordsLabel");
        _enemyLabel  = GetNode<Label>("TopPanel/VBox/EnemyLabel");

        // Bars
        _healthBar   = GetNode<ProgressBar>("BarsContainer/HealthRow/HealthBar");
        _healthLabel = GetNode<Label>("BarsContainer/HealthRow/HealthLabel");
        _manaBar     = GetNode<ProgressBar>("BarsContainer/ManaRow/ManaBar");
        _manaLabel   = GetNode<Label>("BarsContainer/ManaRow/ManaLabel");

        // Minimap
        _minimapViewport = GetNode<SubViewport>("MinimapContainer/MinimapViewport");
        _minimapTexture  = GetNode<TextureRect>("MinimapContainer/MinimapTexture");
        _minimapCamera   = GetNode<Camera3D>("MinimapContainer/MinimapViewport/MinimapCamera");
        _minimapTexture.Texture = _minimapViewport.GetTexture();

        // Overlays
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

        // Inventory (built in code — panel built inside RefreshLang below)
        _inventoryUI = GetNode<Control>("InventoryUI");
        _inventoryUI.Visible = false;

        // Singletons
        _scoreManager = GetNode<ScoreManager>("/root/ScoreManager");
        _scoreManager.ScoreChanged += OnScoreChanged;
        GetNode<GameManager>("/root/GameManager").GameStateChanged += OnGameStateChanged;

        _loc.LangChanged += RefreshLang;
        RefreshLang(); // builds inventory panel + sets all localized texts

        CallDeferred(MethodName.ConnectPlayer);
    }

    public override void _ExitTree()
    {
        if (_scoreManager != null)
            _scoreManager.ScoreChanged -= OnScoreChanged;

        var gm = GetNodeOrNull<GameManager>("/root/GameManager");
        if (gm != null) gm.GameStateChanged -= OnGameStateChanged;

        if (_loc != null)
            _loc.LangChanged -= RefreshLang;

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
        // Pause menu
        if (_pauseResumeBtn != null) _pauseResumeBtn.Text = _loc.T("pause.resume");
        if (_pauseMenuBtn   != null) _pauseMenuBtn.Text   = _loc.T("pause.menu");
        if (_pauseQuitBtn   != null) _pauseQuitBtn.Text   = _loc.T("menu.quit");

        // Game over
        if (_goRestartBtn != null) _goRestartBtn.Text = _loc.T("gameover.restart");
        if (_goMenuBtn    != null) _goMenuBtn.Text    = _loc.T("gameover.menu");

        // Score prefix
        if (_scoreLabel != null && _scoreManager != null)
            _scoreLabel.Text = $"{_loc.T("hud.score")} {_scoreManager.Score}";

        // Rebuild the entire inventory panel so all labels pick up the new language
        if (_inventoryUI != null)
            BuildInventoryPanel();

        // Re-populate if the player is connected
        if (_player != null)
            RefreshInventoryUI();
    }

    private void ConnectPlayer()
    {
        _player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (_player == null)
        {
            // In multiplayer the local player spawns asynchronously via RPC from server.
            // Retry every 0.25 s until the authority player appears in the scene.
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

    // ── Per-frame ─────────────────────────────────────────────────────
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
    }

    // ═════════════════════════════════════════════════════════════════
    // BUILD INVENTORY PANEL
    // ═════════════════════════════════════════════════════════════════

    private void BuildInventoryPanel()
    {
        var invPanel = GetNode<PanelContainer>("InventoryUI/InvPanel");
        foreach (Node c in invPanel.GetChildren()) c.QueueFree();

        var tabs = new TabContainer();
        tabs.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        tabs.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        invPanel.AddChild(tabs);

        // ── Tab 0: Inventory ───────────────────────────────────────
        var invTab = new HBoxContainer();
        invTab.Name = _loc.T("hud.inventory");
        invTab.AddThemeConstantOverride("separation", 10);
        tabs.AddChild(invTab);

        invTab.AddChild(BuildEquipColumn());
        invTab.AddChild(new VSeparator());
        invTab.AddChild(BuildGridColumn());
        invTab.AddChild(new VSeparator());
        invTab.AddChild(BuildInfoColumn());

        // ── Tab 1: Crafting ────────────────────────────────────────
        var craftTab = new VBoxContainer();
        craftTab.Name = _loc.T("hud.crafting");
        craftTab.AddThemeConstantOverride("separation", 6);
        tabs.AddChild(craftTab);

        var craftTitle = MakeLabel(_loc.T("hud.crafting"), 20, new Color(0.96f, 0.78f, 0.12f), HorizontalAlignment.Center);
        craftTab.AddChild(craftTitle);
        var craftHint = MakeLabel("Gather materials from enemies & chests. Each recipe shows how many you have / need.", 11,
            new Color(0.55f, 0.55f, 0.55f), HorizontalAlignment.Center);
        craftHint.AutowrapMode = TextServer.AutowrapMode.Word;
        craftTab.AddChild(craftHint);
        craftTab.AddChild(new HSeparator());

        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        craftTab.AddChild(scroll);

        _recipeList = new VBoxContainer();
        _recipeList.AddThemeConstantOverride("separation", 5);
        _recipeList.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(_recipeList);
    }

    // ── Left column: equipment slots ──────────────────────────────────
    private Control BuildEquipColumn()
    {
        var col = new VBoxContainer();
        col.CustomMinimumSize = new Vector2(152, 0);
        col.AddThemeConstantOverride("separation", 8);

        col.AddChild(MakeLabel(_loc.T("hud.equipment"), 16, new Color(0.40f, 0.60f, 0.45f), HorizontalAlignment.Center));
        col.AddChild(new HSeparator());

        _wepBtn  = MakeEquipBtn(_loc.T("hud.weapon"));
        _armBtn  = MakeEquipBtn(_loc.T("hud.armor"));
        _shldBtn = MakeEquipBtn(_loc.T("hud.shield"));
        _rngBtn  = MakeEquipBtn(_loc.T("hud.ring"));

        _wepBtn .Pressed += () => OnEquipSlotPressed("weapon");
        _armBtn .Pressed += () => OnEquipSlotPressed("armor");
        _shldBtn.Pressed += () => OnEquipSlotPressed("shield");
        _rngBtn .Pressed += () => OnEquipSlotPressed("ring");

        col.AddChild(_wepBtn);
        col.AddChild(_armBtn);
        col.AddChild(_shldBtn);
        col.AddChild(_rngBtn);
        col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 12) });

        _unequipBtn = new Button();
        _unequipBtn.Text = _loc.T("hud.unequip");
        _unequipBtn.Disabled = true;
        _unequipBtn.AddThemeFontSizeOverride("font_size", 12);
        _unequipBtn.Pressed += OnUnequipPressed;
        col.AddChild(_unequipBtn);

        return col;
    }

    private Button MakeEquipBtn(string slot)
    {
        var btn = new Button();
        btn.CustomMinimumSize = new Vector2(148, 58);
        btn.Text = $"{slot}\n─────\nNone";
        btn.AddThemeFontSizeOverride("font_size", 11);
        btn.Modulate = new Color(0.6f, 0.6f, 0.6f);
        return btn;
    }

    // ── Centre column: item grid ──────────────────────────────────────
    private Control BuildGridColumn()
    {
        var col = new VBoxContainer();
        col.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        col.AddThemeConstantOverride("separation", 6);

        col.AddChild(MakeLabel($"{_loc.T("hud.backpack")}  (32)", 16, new Color(0.96f, 0.78f, 0.12f), HorizontalAlignment.Center));
        col.AddChild(MakeLabel("← " + _loc.T("hud.item_info"), 11,
            new Color(0.40f, 0.45f, 0.55f), HorizontalAlignment.Center));

        var grid = new GridContainer();
        grid.Columns = GridCols;
        grid.AddThemeConstantOverride("h_separation", 4);
        grid.AddThemeConstantOverride("v_separation", 4);
        col.AddChild(grid);

        for (int i = 0; i < GridCols * GridRows; i++)
        {
            int idx = i;

            var slot = new Panel();
            slot.CustomMinimumSize = new Vector2(SlotPx, SlotPx);

            // Invisible button covering the slot
            var btn = new Button();
            btn.AnchorRight  = 1f; btn.AnchorBottom = 1f;
            btn.OffsetRight  = 0;  btn.OffsetBottom = 0;
            btn.Flat         = true;
            btn.Pressed += () => OnSlotPressed(idx);

            // Icon label (centred, upper portion)
            var icon = new Label();
            icon.AnchorRight  = 1f; icon.AnchorBottom = 0.65f;
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            icon.VerticalAlignment   = VerticalAlignment.Center;
            icon.AddThemeFontSizeOverride("font_size", 13);

            // Count label (bottom-right)
            var cnt = new Label();
            cnt.AnchorLeft   = 0.5f; cnt.AnchorTop    = 0.6f;
            cnt.AnchorRight  = 1f;   cnt.AnchorBottom = 1f;
            cnt.OffsetRight  = -2;   cnt.OffsetBottom = -2;
            cnt.HorizontalAlignment = HorizontalAlignment.Right;
            cnt.VerticalAlignment   = VerticalAlignment.Bottom;
            cnt.AddThemeFontSizeOverride("font_size", 10);

            slot.AddChild(btn);
            slot.AddChild(icon);
            slot.AddChild(cnt);
            grid.AddChild(slot);

            _invSlots[i]      = slot;
            _invIconLabels[i] = icon;
            _invCntLabels[i]  = cnt;
            SetSlotEmpty(i);
        }

        return col;
    }

    // ── Right column: info + stats ────────────────────────────────────
    private Control BuildInfoColumn()
    {
        var col = new VBoxContainer();
        col.CustomMinimumSize = new Vector2(200, 0);
        col.AddThemeConstantOverride("separation", 8);

        col.AddChild(MakeLabel(_loc.T("hud.info_title"), 16, new Color(0.0f, 0.72f, 0.90f), HorizontalAlignment.Center));
        col.AddChild(new HSeparator());

        _itemInfoLabel = new Label();
        _itemInfoLabel.Text = _loc.T("hud.item_info");
        _itemInfoLabel.AutowrapMode = TextServer.AutowrapMode.Word;
        _itemInfoLabel.AddThemeFontSizeOverride("font_size", 13);
        _itemInfoLabel.AddThemeColorOverride("font_color", new Color(0.88f, 0.88f, 0.88f));
        _itemInfoLabel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        col.AddChild(_itemInfoLabel);

        col.AddChild(new HSeparator());

        _actionBtn = new Button();
        _actionBtn.Text     = "─";
        _actionBtn.Disabled = true;
        _actionBtn.AddThemeFontSizeOverride("font_size", 14);
        _actionBtn.Pressed += OnActionPressed;
        col.AddChild(_actionBtn);

        col.AddChild(new HSeparator());
        col.AddChild(MakeLabel(_loc.T("hud.char"), 13, new Color(0.40f, 0.60f, 0.45f), HorizontalAlignment.Center));

        _statsLabel = new Label();
        _statsLabel.AddThemeFontSizeOverride("font_size", 13);
        _statsLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.85f));
        col.AddChild(_statsLabel);

        return col;
    }

    // ═════════════════════════════════════════════════════════════════
    // REFRESH
    // ═════════════════════════════════════════════════════════════════

    private void RefreshInventoryUI()
    {
        if (_player == null) return;

        for (int i = 0; i < _invSlots.Length; i++)
            SetSlotEmpty(i);

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
        var s = MakeSlotStyle(new Color(0.07f, 0.07f, 0.18f), new Color(0.18f, 0.18f, 0.30f), 1);
        _invSlots[idx].AddThemeStyleboxOverride("panel", s);
        _invIconLabels[idx].Text = "";
        _invCntLabels[idx].Text  = "";
    }

    private void SetSlotItem(int idx, InventoryItem item)
    {
        var def = item.Def;
        if (def == null) { SetSlotEmpty(idx); return; }

        Color cat = CategoryColor(def.Category);
        Color bg  = new Color(cat.R * 0.18f, cat.G * 0.18f, cat.B * 0.18f, 1f);
        _invSlots[idx].AddThemeStyleboxOverride("panel", MakeSlotStyle(bg, cat, 2));

        _invIconLabels[idx].Text = Abbrev(def.Name);
        _invIconLabels[idx].AddThemeColorOverride("font_color", cat);
        _invCntLabels[idx].Text  = item.Count > 1 ? item.Count.ToString() : "";
        _invCntLabels[idx].AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));
    }

    private void HighlightSlot(int idx, bool on)
    {
        if (idx < 0 || idx >= _invSlots.Length) return;
        if (_invSlots[idx].GetThemeStylebox("panel") is not StyleBoxFlat s) return;
        s.BorderColor = on ? new Color(0.96f, 0.78f, 0.12f) : new Color(0.18f, 0.18f, 0.30f);
        s.BorderWidthBottom = s.BorderWidthTop = s.BorderWidthLeft = s.BorderWidthRight = on ? 3 : 2;
    }

    private void RefreshEquipSlots()
    {
        if (_player == null) return;
        var inv = _player.Inventory;
        UpdateEquipBtn(_wepBtn,  _loc.T("hud.weapon"),  inv.EquippedWeapon);
        UpdateEquipBtn(_armBtn,  _loc.T("hud.armor"),   inv.EquippedArmor);
        UpdateEquipBtn(_shldBtn, _loc.T("hud.shield"),  inv.EquippedShield);
        UpdateEquipBtn(_rngBtn,  _loc.T("hud.ring"),    inv.EquippedRing);
    }

    private void UpdateEquipBtn(Button btn, string slot, string itemId)
    {
        string name = itemId != null ? (ItemDatabase.Get(itemId)?.Name ?? itemId) : _loc.T("hud.none");
        btn.Text     = $"{slot}\n─────\n{name}";
        btn.Modulate = itemId != null ? new Color(0.75f, 1f, 0.75f) : new Color(0.55f, 0.55f, 0.55f);
    }

    private void RefreshStatsUI()
    {
        if (_player == null || _statsLabel == null) return;
        _statsLabel.Text =
            $"ATK:  {_player.TotalAttack}\n" +
            $"DEF:  {_player.TotalDefense}\n" +
            $"MAG:  {_player.TotalMagic}\n"  +
            $"──────────\n" +
            $"Lv:   {_player.Level}\n"       +
            $"HP:   {_player.CurrentHealth}/{_player.MaxHealth}\n" +
            $"MP:   {_player.CurrentMana}/{_player.MaxMana}\n"     +
            $"XP:   {_player.Experience}/{_player.XpToNext}";
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
            ps.BgColor = canCraft ? new Color(0.07f, 0.16f, 0.07f) : new Color(0.07f, 0.07f, 0.14f);
            ps.CornerRadiusBottomLeft = ps.CornerRadiusBottomRight =
            ps.CornerRadiusTopLeft   = ps.CornerRadiusTopRight    = 0;
            panel.AddThemeStyleboxOverride("panel", ps);

            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 8);
            panel.AddChild(hbox);

            // Result column
            string resultText = resultDef.Name;
            if (recipe.ResultCount > 1) resultText += $" ×{recipe.ResultCount}";
            var resultLbl = MakeLabel(resultText, 13,
                canCraft ? new Color(0.45f, 1f, 0.45f) : new Color(0.65f, 0.65f, 0.65f));
            resultLbl.CustomMinimumSize = new Vector2(170, 0);
            hbox.AddChild(resultLbl);

            // Ingredients column
            var sb = new StringBuilder("← ");
            foreach (var kvp in recipe.Ingredients)
            {
                var d    = ItemDatabase.Get(kvp.Key);
                int have = _player.Inventory.GetCount(kvp.Key);
                bool ok  = have >= kvp.Value;
                sb.Append($"{d?.Name ?? kvp.Key} {have}/{kvp.Value}  ");
            }
            var ingLbl = MakeLabel(sb.ToString().TrimEnd(), 11, new Color(0.72f, 0.72f, 0.72f));
            ingLbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            ingLbl.AutowrapMode        = TextServer.AutowrapMode.Word;
            hbox.AddChild(ingLbl);

            // Craft button
            var btn = new Button();
            btn.Text     = _loc.T("hud.craft");
            btn.Disabled = !canCraft;
            btn.AddThemeFontSizeOverride("font_size", 12);
            string rid = recipe.ResultId;
            btn.Pressed += () => DoCraft(rid);
            hbox.AddChild(btn);

            _recipeList.AddChild(panel);
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // SLOT & EQUIP INTERACTIONS
    // ═════════════════════════════════════════════════════════════════

    private void OnSlotPressed(int idx)
    {
        if (_selectedSlot >= 0) HighlightSlot(_selectedSlot, false);

        var items = _player?.Inventory?.Items;
        if (items == null || idx >= items.Count)
        {
            _selectedSlot = -1; _selectedItemId = null; _selectedIsEquip = false;
            ShowItemInfo(null);
            return;
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
            "weapon" => inv.EquippedWeapon,
            "armor"  => inv.EquippedArmor,
            "shield" => inv.EquippedShield,
            "ring"   => inv.EquippedRing,
            _        => null,
        };
        if (id == null) return;

        if (_selectedSlot >= 0) HighlightSlot(_selectedSlot, false);
        _selectedSlot    = -1;
        _selectedItemId  = id;
        _selectedIsEquip = true;
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
            _itemInfoLabel.Text = $"Unknown: {item.ItemId}";
            _actionBtn.Text = "─"; _actionBtn.Disabled = true;
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"[{def.Category}]");
        sb.AppendLine(def.Name);
        if (!string.IsNullOrEmpty(def.Description))
        { sb.AppendLine(); sb.AppendLine(def.Description); }
        if (def.AtkBonus != 0) { sb.AppendLine(); sb.Append($"ATK: {(def.AtkBonus > 0 ? "+" : "")}{def.AtkBonus}"); }
        if (def.DefBonus != 0) { sb.AppendLine(); sb.Append($"DEF: {(def.DefBonus > 0 ? "+" : "")}{def.DefBonus}"); }
        if (def.MagBonus != 0) { sb.AppendLine(); sb.Append($"MAG: {(def.MagBonus > 0 ? "+" : "")}{def.MagBonus}"); }
        if (item.Count > 1)    { sb.AppendLine(); sb.AppendLine(); sb.Append($"Amount: ×{item.Count}"); }
        _itemInfoLabel.Text = sb.ToString();

        switch (def.Category)
        {
            case ItemCategory.Consumable:
                _actionBtn.Text = _loc.T("hud.use"); _actionBtn.Disabled = false; break;
            case ItemCategory.Weapon:
            case ItemCategory.Armor:
            case ItemCategory.Shield:
            case ItemCategory.Ring:
                _actionBtn.Text     = _selectedIsEquip ? _loc.T("hud.equipped") : _loc.T("hud.equip");
                _actionBtn.Disabled = _selectedIsEquip; break;
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
            GD.Print($"[Crafting] Made: {ItemDatabase.Get(resultId)?.Name}");
        else
            GD.Print($"[Crafting] Failed: {err}");
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
        _manaBar.Value  = (double)cur / max * 100.0;
        _manaLabel.Text = $"{cur}/{max}";
    }

    private void OnLevelUp(int level)
    {
        if (_player == null) return;
        _levelLabel.Text = $"Lv.{level}  XP: {_player.Experience}/{_player.XpToNext}";
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

    // ═════════════════════════════════════════════════════════════════
    // INPUT
    // ═════════════════════════════════════════════════════════════════

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

    private static StyleBoxFlat MakeSlotStyle(Color bg, Color border, int bw)
    {
        var s = new StyleBoxFlat();
        s.BgColor     = bg;
        s.BorderColor = border;
        s.BorderWidthBottom = s.BorderWidthTop = s.BorderWidthLeft = s.BorderWidthRight = bw;
        // No rounded corners — sharp retro look
        s.CornerRadiusBottomLeft = s.CornerRadiusBottomRight =
        s.CornerRadiusTopLeft   = s.CornerRadiusTopRight    = 0;
        return s;
    }

    private static Label MakeLabel(string text, int fontSize, Color color,
        HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var lbl = new Label();
        lbl.Text = text;
        lbl.HorizontalAlignment = align;
        lbl.AddThemeFontSizeOverride("font_size", fontSize);
        lbl.AddThemeColorOverride("font_color", color);
        return lbl;
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

    // ── Button callbacks ──────────────────────────────────────────────
    public void OnResumePressed()   => GetNode<GameManager>("/root/GameManager").TogglePause();
    public void OnMainMenuPressed() => GetNode<GameManager>("/root/GameManager").ReturnToMenu();
    public void OnRestartPressed()  { GetTree().Paused = false; GetTree().ReloadCurrentScene(); }
    public void OnQuitPressed()     => GetTree().Quit();
}
