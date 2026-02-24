using Godot;
using System.Collections.Generic;

namespace VoidQuest;

/// <summary>
/// Handles player spawning when the World scene loads.
/// - Single-player:  spawns one player directly.
/// - Multiplayer server: spawns itself immediately, then spawns each
///   client when the client calls RequestSpawn (after loading the scene).
/// - Multiplayer client: asks the server to spawn all players for it.
/// </summary>
public partial class WorldSetup : Node3D
{
    [Export] public PackedScene PlayerScene;
    [Export] public PackedScene FireballScene;

    // Spawn positions: first 8 slots around the base spawn point
    private static readonly Vector3 BaseSpawn = new(27.025f, 40f, 30.695f);
    private static readonly Vector3[] SpawnOffsets =
    {
        new(0, 0, 0), new(3, 0, 0), new(-3, 0, 0), new(0, 0, 3),
        new(3, 0, 3), new(-3, 0, 3), new(0, 0, -3), new(3, 0, -3),
    };

    // Server-side: tracks peerId → spawn position (to resend to late joiners)
    private readonly Dictionary<long, Vector3> _spawnedPositions = new();
    private int _nextSlot = 0;

    private Node3D _playersNode;

    // ── Lifecycle ─────────────────────────────────────────────────────

    public override void _Ready()
    {
        _playersNode = GetParent().GetNode<Node3D>("Players");

        var nm = NetworkManager.Instance;

        if (nm == null || !nm.IsOnline)
        {
            // ── Single player: spawn directly ──────────────────────────
            DoSpawnPlayer(1L, BaseSpawn);
            return;
        }

        if (nm.IsServer)
        {
            // ── Server: spawn the host player (skip for dedicated server) ──
            if (!DedicatedServer.IsActive)
            {
                var pos = NextSpawnPos();
                _spawnedPositions[1L] = pos;
                DoSpawnPlayer(1L, pos);
            }
            // Clients will call RpcRequestSpawn after they finish loading
        }
        else
        {
            // ── Client: ask the server to send all spawn info ───────────
            RpcId(1, MethodName.RpcRequestSpawn);
        }
    }

    // ── RPCs ──────────────────────────────────────────────────────────

    /// <summary>
    /// Called on the SERVER by a client that just loaded the World scene.
    /// Server spawns the new client for everyone, and sends all existing
    /// players' positions to the new client only.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcRequestSpawn()
    {
        if (!Multiplayer.IsServer()) return;

        long newPeer = Multiplayer.GetRemoteSenderId();

        // Send existing players to the new client only
        foreach (var kvp in _spawnedPositions)
            RpcId(newPeer, MethodName.RpcDoSpawn, kvp.Key, kvp.Value);

        // Spawn the new client for everyone (including themselves)
        var newPos = NextSpawnPos();
        _spawnedPositions[newPeer] = newPos;
        Rpc(MethodName.RpcDoSpawnForAll, newPeer, newPos);
    }

    /// <summary>Spawn a player only on the receiving peer (no local call on server).</summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcDoSpawn(long peerId, Vector3 pos)
    {
        DoSpawnPlayer(peerId, pos);
    }

    /// <summary>Spawn a player on ALL peers including the server itself.</summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcDoSpawnForAll(long peerId, Vector3 pos)
    {
        DoSpawnPlayer(peerId, pos);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private void DoSpawnPlayer(long peerId, Vector3 spawnPos)
    {
        if (PlayerScene == null) return;

        // Prevent double-spawn
        string nodeName = peerId.ToString();
        if (_playersNode.HasNode(nodeName)) return;

        var player = PlayerScene.Instantiate<Player>();
        player.Name = nodeName;
        if (FireballScene != null)
            player.FireballScene = FireballScene;

        // Authority must be set BEFORE AddChild so Player._Ready() sees the correct value
        player.SetMultiplayerAuthority((int)peerId);
        _playersNode.AddChild(player);
        player.GlobalPosition = spawnPos;
    }

    private Vector3 NextSpawnPos()
    {
        var pos = BaseSpawn + SpawnOffsets[_nextSlot % SpawnOffsets.Length];
        _nextSlot++;
        return pos;
    }
}
