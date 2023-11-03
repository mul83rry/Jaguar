using System.Net;

namespace Jaguar.Listeners;

public abstract class UnRegisteredUserListener<TData>
{
    public string Name { get; set; }
    
    public abstract void Config();
    public abstract Task OnMessageReceived(IPEndPoint sender, TData data);
}