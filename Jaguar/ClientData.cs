using System.Net.WebSockets;
using Jaguar.Core;

namespace Jaguar;

public class ClientData : IDisposable
{
    public readonly WebSocketContext Client;
    public User? User;
    public DateTime LastActivateTime { get; set; }

    internal ClientData(User? user, WebSocketContext client)
    {
        User = user;
        Client = client;
        LastActivateTime = DateTime.UtcNow;
    }

    public void Dispose()
    {
        User?.Dispose();
    }
}