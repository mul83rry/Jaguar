using System.Net.WebSockets;
using Jaguar.Core;
using LiteNetLib;

namespace Jaguar;

public class ClientData : IDisposable
{
    // public readonly WebSocketContext Client;
    public readonly NetPeer Peer;
    public User? User;
    public DateTime LastActivateTime { get; set; }

    // internal ClientData(User? user, WebSocketContext client)
    internal ClientData(User? user, NetPeer client)
    {
        User = user;
        Peer = client;
        LastActivateTime = DateTime.UtcNow;
    }

    public void Dispose()
    {
        User?.Dispose();
    }
}