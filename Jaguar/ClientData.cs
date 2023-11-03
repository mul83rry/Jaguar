using System.Net;
using Jaguar.Core;
using Jaguar.Core.Handlers;
using Jaguar.Extensions;

namespace Jaguar;

public class ClientData : IDisposable
{
    public readonly IPEndPoint Client;
    public User? User;
    internal readonly PacketSender PacketSender;
    internal readonly PacketReceiver PacketReceiver;
    public DateTime LastActivateTime { get; set; }

    internal ClientData(User? user, IPEndPoint client)
    {
        User = user;
        Client = client;
        LastActivateTime = DateTime.UtcNow;

        PacketSender = new PacketSender(client, this);
        PacketReceiver = new PacketReceiver(this);
    }

    public void Dispose()
    {
        Console.WriteLine($"ClientData {Client.ConvertToKey()} And User UniqueId {User?.UniqueId ?? null} Disposed");
        User?.Dispose();
    }
}