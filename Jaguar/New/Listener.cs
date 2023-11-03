using System.Net;

namespace Jaguar.New;

public abstract class UnRegisteredUserListener<TData>
{
    public string Name { get; set; }
    
    public abstract void Config();
    public abstract Task OnMessageReceived(IPEndPoint sender, TData data);
}