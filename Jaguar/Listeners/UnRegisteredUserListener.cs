using Jaguar.Core.WebSocket;
using LiteNetLib;

namespace Jaguar.Listeners;

public abstract class UnRegisteredUserListener<TRequest>
{
    public string Name { get; set; }
    
    public abstract void Config();
    public abstract Task OnMessageReceived(NetPeer sender, TRequest request);
}