using System.Net;

namespace Jaguar.Listeners;

public abstract class ByteListener
{
    public abstract Task OnMessageReceived(IPEndPoint endPoint, byte[] data);
}