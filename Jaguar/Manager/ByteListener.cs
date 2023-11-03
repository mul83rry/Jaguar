using System.Net;

namespace Jaguar.Manager;

public abstract class ByteListener
{
    public static ByteListener Instance { get; protected set; }

    public abstract void Config();
    public abstract Task OnMessageReceived(IPEndPoint endPoint, byte[] data);
}

public interface IByteListener
{
    public static IByteListener Instance { get; protected set; }

    public void Config();
    public Task OnMessageReceived(IPEndPoint endPoint, byte[] data);
}