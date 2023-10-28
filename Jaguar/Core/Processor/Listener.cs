using System.Net;
using Jaguar.Core.Data;

namespace Jaguar.Core.Processor;

internal static class Listener
{
    internal static Action<IPEndPoint?, Packet>? OnMessageReceived;
}