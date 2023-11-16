using System.Net.WebSockets;

namespace Jaguar.Listeners;

public abstract class UnRegisteredUserListener<TRequest>
{
    public string Name { get; set; }
    
    public abstract void Config();
    public abstract Task OnMessageReceived(WebSocketContext sender, TRequest request);
}