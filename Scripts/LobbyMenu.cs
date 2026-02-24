using System.Net;
using System.Net.Sockets;
using System.Text;
using Godot;

namespace VoidQuest;

/// <summary>
/// Multiplayer lobby: choose to host or join, see connected players,
/// then start the game (host only triggers scene load for everyone).
/// </summary>
public partial class LobbyMenu : Control
{
    // ── Scene node refs ───────────────────────────────────────────────
    private Control  _modePanel;
    private Control  _joinPanel;
    private Control  _lobbyPanel;
    private LineEdit _ipInput;
    private Label    _statusLabel;
    private Label    _playersList;
    private Label    _ipDisplay;
    private Button   _startBtn;

    // Buttons that need language refresh
    private Button _hostBtn;
    private Button _joinBtn;
    private Button _backBtn;
    private Button _connectBtn;
    private Button _joinBackBtn;
    private Button _disconnectBtn;

    // Labels that need language refresh
    private Label _ipLabel;
    private Label _playersTitle;

    private NetworkManager _net;
    private LocaleManager  _loc;
    private bool _hosting;

    // ── Lifecycle ─────────────────────────────────────────────────────

    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;

        _net = NetworkManager.Instance;
        _loc = GetNode<LocaleManager>("/root/LocaleManager");

        _net.PlayerJoined      += OnPeerListChanged;
        _net.PlayerLeft        += OnPeerListChanged;
        _net.ConnectedToServer += OnConnectedToServer;
        _net.ConnectionFailed  += OnConnectionFailed;
        _loc.LangChanged       += RefreshLang;

        // Panel refs
        _modePanel  = GetNode<Control>("Center/Panel/VBox/ModePanel");
        _joinPanel  = GetNode<Control>("Center/Panel/VBox/JoinPanel");
        _lobbyPanel = GetNode<Control>("Center/Panel/VBox/LobbyPanel");

        // Mode panel
        _hostBtn = GetNode<Button>("Center/Panel/VBox/ModePanel/HostBtn");
        _joinBtn = GetNode<Button>("Center/Panel/VBox/ModePanel/JoinBtn");
        _backBtn = GetNode<Button>("Center/Panel/VBox/ModePanel/BackBtn");

        // Join panel
        _ipInput     = GetNode<LineEdit>("Center/Panel/VBox/JoinPanel/VBox/IpInput");
        _ipLabel     = GetNode<Label>  ("Center/Panel/VBox/JoinPanel/VBox/IpLabel");
        _connectBtn  = GetNode<Button> ("Center/Panel/VBox/JoinPanel/VBox/ConnectBtn");
        _joinBackBtn = GetNode<Button> ("Center/Panel/VBox/JoinPanel/VBox/JoinBackBtn");

        // Lobby panel
        _statusLabel   = GetNode<Label>  ("Center/Panel/VBox/StatusLabel");
        _playersTitle  = GetNode<Label>  ("Center/Panel/VBox/LobbyPanel/VBox/PlayersTitle");
        _playersList   = GetNode<Label>  ("Center/Panel/VBox/LobbyPanel/VBox/PlayersList");
        _ipDisplay     = GetNode<Label>  ("Center/Panel/VBox/LobbyPanel/VBox/IpDisplay");
        _startBtn      = GetNode<Button> ("Center/Panel/VBox/LobbyPanel/VBox/StartBtn");
        _disconnectBtn = GetNode<Button> ("Center/Panel/VBox/LobbyPanel/VBox/DisconnectBtn");

        RefreshLang();
        ShowModePanel();
    }

    public override void _ExitTree()
    {
        if (_net != null)
        {
            _net.PlayerJoined      -= OnPeerListChanged;
            _net.PlayerLeft        -= OnPeerListChanged;
            _net.ConnectedToServer -= OnConnectedToServer;
            _net.ConnectionFailed  -= OnConnectionFailed;
        }
        if (_loc != null)
            _loc.LangChanged -= RefreshLang;
    }

    // ── Language refresh ──────────────────────────────────────────────

    private void RefreshLang()
    {
        _hostBtn.Text      = _loc.T("lobby.host");
        _joinBtn.Text      = _loc.T("lobby.join");
        _backBtn.Text      = _loc.T("lobby.back");
        _ipLabel.Text      = _loc.T("lobby.ip");
        _connectBtn.Text   = _loc.T("lobby.connect");
        _joinBackBtn.Text  = _loc.T("lobby.back");
        _startBtn.Text     = _loc.T("lobby.start");
        _disconnectBtn.Text = _loc.T("lobby.back");
        _playersTitle.Text = _loc.T("lobby.players");
    }

    // ── Button callbacks ──────────────────────────────────────────────

    public void OnHostPressed()
    {
        var err = _net.HostGame();
        if (err != Error.Ok)
        {
            _statusLabel.Text = $"Error: {err}";
            return;
        }
        _hosting = true;
        ShowLobbyPanel();
        _startBtn.Visible = true;
        _ipDisplay.Text   = $"{_loc.T("lobby.your_ip")} {GetLocalIp()}:{NetworkManager.DefaultPort}";
        _statusLabel.Text = _loc.T("lobby.waiting");
        RefreshPlayerList();
    }

    public void OnJoinPressed()
    {
        _modePanel.Visible = false;
        _joinPanel.Visible = true;
        _statusLabel.Text  = _loc.T("lobby.connecting");
    }

    public void OnConnectPressed()
    {
        string ip = _ipInput.Text.Trim();
        if (string.IsNullOrEmpty(ip)) ip = "127.0.0.1";
        _statusLabel.Text = $"{_loc.T("lobby.connecting")} {ip}…";

        var err = _net.JoinGame(ip);
        if (err != Error.Ok)
            _statusLabel.Text = $"Error: {err}";
    }

    public void OnJoinBackPressed()
    {
        ShowModePanel();
        _statusLabel.Text = "";
    }

    public void OnBackToMenuPressed()
    {
        _net.CloseConnection();
        _hosting = false;
        GetNode<GameManager>("/root/GameManager").ReturnToMenu();
    }

    public void OnStartGamePressed()
    {
        if (!_hosting) return;
        Rpc(MethodName.RpcStartGame);
        StartGame();
    }

    // ── RPC: called on all clients by the host ────────────────────────

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcStartGame() => StartGame();

    private void StartGame() => GetNode<GameManager>("/root/GameManager").StartGame();

    // ── NetworkManager signal handlers ────────────────────────────────

    private void OnPeerListChanged(long _peerId)
    {
        RefreshPlayerList();
        if (_hosting)
            _statusLabel.Text = $"{_net.Peers.Count}  —  {_loc.T("lobby.waiting")}";
    }

    private void OnConnectedToServer()
    {
        ShowLobbyPanel();
        _startBtn.Visible = false;
        _ipDisplay.Text   = "";
        _statusLabel.Text = _loc.T("lobby.waiting");
        RefreshPlayerList();
    }

    private void OnConnectionFailed(string reason)
    {
        ShowModePanel();
        _statusLabel.Text = $"Error: {reason}";
        _hosting = false;
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private void ShowModePanel()
    {
        _modePanel.Visible  = true;
        _joinPanel.Visible  = false;
        _lobbyPanel.Visible = false;
    }

    private void ShowLobbyPanel()
    {
        _modePanel.Visible  = false;
        _joinPanel.Visible  = false;
        _lobbyPanel.Visible = true;
    }

    private void RefreshPlayerList()
    {
        var sb = new StringBuilder();
        foreach (var kvp in _net.Peers)
            sb.AppendLine($"●  {kvp.Value}  (id {kvp.Key})");
        _playersList.Text = _net.Peers.Count == 0 ? "—" : sb.ToString().TrimEnd();
    }

    /// <summary>Detect the LAN IP by attempting an outbound route.</summary>
    private static string GetLocalIp()
    {
        try
        {
            using var sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            sock.Connect("8.8.8.8", 65530);
            return ((IPEndPoint)sock.LocalEndPoint)?.Address?.ToString() ?? "127.0.0.1";
        }
        catch { return "127.0.0.1"; }
    }
}
