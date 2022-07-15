using System.Net;
using Jaguar.Core.Data;

namespace Jaguar.Core.Processor
{
    public static class Listener
    {
        public static Action<IPEndPoint?, Packet>? OnNewMessageReceived;
        public static Action<IPEndPoint, byte[]>? OnNewBytesMessageReceived;
    }
}
