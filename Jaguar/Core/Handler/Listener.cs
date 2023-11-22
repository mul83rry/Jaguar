using System.Net.WebSockets;
using Jaguar.Core.Dto;

namespace Jaguar.Core.Handler;

internal static class Listener
{
    internal static Action<WebSocketContext?, Packet>? OnMessageReceived;
}