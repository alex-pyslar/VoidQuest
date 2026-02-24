using Godot;
using System.Collections.Generic;

namespace VoidQuest;

/// <summary>
/// Autoloaded singleton. Wraps ENet multiplayer peer creation,
/// tracks connected players, and fires Godot signals on lobby changes.
/// </summary>
public partial class NetworkManager : Node
{
    public static NetworkManager Instance { get; private set; }

    public const int DefaultPort = 7777;
    public const int MaxPlayers  = 8;

    /// <summary>True once CreateServer or CreateClient has been called successfully.</summary>
    public bool IsOnline => Multiplayer.MultiplayerPeer != null;

    /// <summary>True when we are the host (server).</summary>
    public bool IsServer => IsOnline && Multiplayer.IsServer();

    [Signal] public delegate void PlayerJoinedEventHandler(long peerId);
    [Signal] public delegate void PlayerLeftEventHandler(long peerId);
    [Signal] public delegate void ConnectedToServerEventHandler();
    [Signal] public delegate void ConnectionFailedEventHandler(string reason);

    /// <summary>Maps peer id → display name. Populated on each client.</summary>
    public Dictionary<long, string> Peers { get; } = new();

    // ── Lifecycle ─────────────────────────────────────────────────────

    public override void _Ready()
    {
        Instance    = this;
        ProcessMode = ProcessModeEnum.Always;

        Multiplayer.PeerConnected      += OnPeerConnected;
        Multiplayer.PeerDisconnected   += OnPeerDisconnected;
        Multiplayer.ConnectedToServer  += OnConnectedToServer;
        Multiplayer.ConnectionFailed   += OnConnectionFailed;
        Multiplayer.ServerDisconnected += OnServerDisconnected;
    }

    // ── Public API ────────────────────────────────────────────────────

    /// <summary>Start hosting a game on the given port. Returns Error.Ok on success.</summary>
    public Error HostGame(int port = DefaultPort)
    {
        var peer = new ENetMultiplayerPeer();
        var err  = peer.CreateServer(port, MaxPlayers);
        if (err != Error.Ok)
        {
            GD.PrintErr($"[Net] CreateServer failed: {err}");
            return err;
        }

        Multiplayer.MultiplayerPeer = peer;
        Peers[1L] = "Host";
        GD.Print($"[Net] Server started on port {port}");
        return Error.Ok;
    }

    /// <summary>Connect to a remote host. Returns Error.Ok if the attempt was started.</summary>
    public Error JoinGame(string address, int port = DefaultPort)
    {
        var peer = new ENetMultiplayerPeer();
        var err  = peer.CreateClient(address, port);
        if (err != Error.Ok)
        {
            GD.PrintErr($"[Net] CreateClient failed: {err}");
            return err;
        }

        Multiplayer.MultiplayerPeer = peer;
        GD.Print($"[Net] Connecting to {address}:{port} …");
        return Error.Ok;
    }

    /// <summary>Gracefully close the connection and reset state.</summary>
    public void CloseConnection()
    {
        if (Multiplayer.MultiplayerPeer != null)
        {
            Multiplayer.MultiplayerPeer.Close();
            Multiplayer.MultiplayerPeer = null;
        }
        Peers.Clear();
    }

    // ── ENet callbacks ────────────────────────────────────────────────

    private void OnPeerConnected(long id)
    {
        GD.Print($"[Net] Peer connected: {id}");
        Peers[id] = $"Player_{id}";
        EmitSignal(SignalName.PlayerJoined, id);
    }

    private void OnPeerDisconnected(long id)
    {
        GD.Print($"[Net] Peer disconnected: {id}");
        Peers.Remove(id);
        EmitSignal(SignalName.PlayerLeft, id);
    }

    private void OnConnectedToServer()
    {
        long myId = Multiplayer.GetUniqueId();
        GD.Print($"[Net] Connected to server! My id: {myId}");
        Peers[myId] = $"Player_{myId}";
        EmitSignal(SignalName.ConnectedToServer);
    }

    private void OnConnectionFailed()
    {
        GD.PrintErr("[Net] Connection failed!");
        Peers.Clear();
        EmitSignal(SignalName.ConnectionFailed, "Could not connect to server");
    }

    private void OnServerDisconnected()
    {
        GD.PrintErr("[Net] Server disconnected!");
        Peers.Clear();
        EmitSignal(SignalName.ConnectionFailed, "Server disconnected");
    }
}
