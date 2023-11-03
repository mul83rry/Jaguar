using System.Net;

namespace Jaguar.New;

public abstract class ByteListener
{
    public abstract Task OnMessageReceived(IPEndPoint endPoint, byte[] data);
}