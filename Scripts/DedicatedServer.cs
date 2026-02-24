using System.Linq;
using Godot;

namespace VoidQuest;

/// <summary>
/// Autoload singleton: detects headless / dedicated-server mode and auto-starts
/// the game server without showing any UI.
///
/// Activate with either:
///   • the --server command-line flag:  VoidQuest.console.exe --server
///   • the dedicated_server Godot feature (exported server template)
///
/// When active:
///   1. Binds ENet on DefaultPort immediately.
///   2. Loads the World scene (skips MainMenu and LobbyMenu).
///   3. Dedicated server never spawns a local player — players join remotely.
/// </summary>
public partial class DedicatedServer : Node
{
    /// <summary>True when running as a headless dedicated server.</summary>
    public static bool IsActive { get; private set; }

    public override void _Ready()
    {
        IsActive = OS.HasFeature("dedicated_server")
                || OS.GetCmdlineArgs().Contains("--server");

        if (!IsActive) return;

        GD.Print("╔══════════════════════════════════════════╗");
        GD.Print("║    VOID QUEST  –  Dedicated Server       ║");
        GD.Print("╚══════════════════════════════════════════╝");
        GD.Print($"[Server] Port    : {NetworkManager.DefaultPort}");
        GD.Print($"[Server] MaxPeers: {NetworkManager.MaxPlayers}");

        // Minimise window (irrelevant in --headless, but safe on desktop)
        if (DisplayServer.GetName() != "headless")
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Minimized);

        // Bind the ENet server
        var err = NetworkManager.Instance.HostGame();
        if (err != Error.Ok)
        {
            GD.PrintErr($"[Server] FATAL – could not bind port {NetworkManager.DefaultPort}: {err}");
            GetTree().Quit(1);
            return;
        }

        GD.Print($"[Server] Listening on :{NetworkManager.DefaultPort}  –  waiting for players…");
        GD.Print("[Server] Press Ctrl+C to shut down.");

        // Jump straight to the World scene; LobbyMenu / MainMenu are not needed
        GetNode<GameManager>("/root/GameManager").StartGame();
    }
}
