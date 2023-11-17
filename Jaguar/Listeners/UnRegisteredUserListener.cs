using System.Net.WebSockets;
using Jaguar.Core.Socket;

namespace Jaguar.Listeners;

public abstract class UnRegisteredUserListener<TRequest>
{
    public string Name { get; set; }
    
    public abstract void Config();
    public abstract Task OnMessageReceived(WebSocketContextData sender, TRequest request);
}