using System.Net;
using Jaguar.Core.Dto;

namespace Jaguar.Core.Handlers;

internal static class Listener
{
    internal static Action<IPEndPoint?, Packet>? OnMessageReceived;
}