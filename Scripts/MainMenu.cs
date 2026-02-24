using Godot;

namespace VoidQuest;

/// <summary>
/// Main menu controller. Loads when the game starts.
/// Handles Play / Multiplayer / Settings (language) / Quit.
/// </summary>
public partial class MainMenu : Control
{
    // ── Retro palette constants ────────────────────────────────────────
    private static readonly Color Gold  = new(0.96f, 0.78f, 0.12f);
    private static readonly Color White = new(0.90f, 0.95f, 1.00f);

    // ── Node refs ─────────────────────────────────────────────────────
    private LocaleManager _loc;

    private Control _mainPanel;
    private Control _settingsPanel;

    private Button _playBtn;
    private Button _mpBtn;
    private Button _settBtn;
    private Button _quitBtn;
    private Button _langEnBtn;
    private Button _langRuBtn;
    private Button _backBtn;

    // ── Lifecycle ─────────────────────────────────────────────────────

    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;
        GetNode<GameManager>("/root/GameManager")?.SetMenuState();

        _loc = GetNode<LocaleManager>("/root/LocaleManager");

        _mainPanel     = GetNode<Control>("CenterContainer/Panel/VBox/MainPanel");
        _settingsPanel = GetNode<Control>("CenterContainer/Panel/VBox/SettingsPanel");

        _playBtn = GetNode<Button>("CenterContainer/Panel/VBox/MainPanel/PlayButton");
        _mpBtn   = GetNode<Button>("CenterContainer/Panel/VBox/MainPanel/MultiplayerButton");
        _settBtn = GetNode<Button>("CenterContainer/Panel/VBox/MainPanel/SettingsButton");
        _quitBtn = GetNode<Button>("CenterContainer/Panel/VBox/MainPanel/QuitButton");

        _langEnBtn = GetNode<Button>("CenterContainer/Panel/VBox/SettingsPanel/LangCenter/LangHBox/LangEnBtn");
        _langRuBtn = GetNode<Button>("CenterContainer/Panel/VBox/SettingsPanel/LangCenter/LangHBox/LangRuBtn");
        _backBtn   = GetNode<Button>("CenterContainer/Panel/VBox/SettingsPanel/BackButton");

        _loc.LangChanged += RefreshLang;
        RefreshLang();
    }

    // ── Language refresh ──────────────────────────────────────────────

    private void RefreshLang()
    {
        _playBtn.Text = _loc.T("menu.play");
        _mpBtn.Text   = _loc.T("menu.multiplayer");
        _settBtn.Text = _loc.T("menu.settings");
        _quitBtn.Text = _loc.T("menu.quit");
        _backBtn.Text = _loc.T("menu.back");

        // Highlight the active language button in gold
        bool isRu = _loc.Lang == "ru";
        _langRuBtn.AddThemeColorOverride("font_color", isRu ? Gold : White);
        _langEnBtn.AddThemeColorOverride("font_color", isRu ? White : Gold);
    }

    // ── Button Callbacks ──────────────────────────────────────────────

    public void OnPlayPressed()
    {
        GetNode<GameManager>("/root/GameManager").StartGame();
    }

    public void OnMultiplayerPressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/LobbyMenu.tscn");
    }

    public void OnSettingsPressed()
    {
        _mainPanel.Visible     = false;
        _settingsPanel.Visible = true;
    }

    public void OnBackPressed()
    {
        _settingsPanel.Visible = false;
        _mainPanel.Visible     = true;
    }

    public void OnLangEnPressed() => _loc.SetLang("en");
    public void OnLangRuPressed() => _loc.SetLang("ru");

    public void OnQuitPressed()
    {
        GetTree().Quit();
    }
}
