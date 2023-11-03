using System.Net;

namespace Jaguar.New;

public abstract class UnRegisteredUserListener<TData> where TData : class, new()
{
    public string Name { get; set; }
    
    public abstract Task OnMessageReceived(IPEndPoint endPoint, TData data);
    public abstract void Config();
}