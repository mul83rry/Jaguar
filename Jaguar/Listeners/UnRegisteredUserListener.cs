using Jaguar.Core.WebSocket;

namespace Jaguar.Listeners;

public abstract class UnRegisteredUserListener<TRequest>
{
    public string Name { get; set; } = null!;

    public abstract void Config();
    public abstract Task OnMessageReceived(WebSocketContextData sender, TRequest request);
}