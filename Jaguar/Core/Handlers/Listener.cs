using System.Net.WebSockets;
using Jaguar.Core.Dto;

namespace Jaguar.Core.Handlers;

internal static class Listener
{
    internal static Action<WebSocketContext?, Packet>? OnMessageReceived;
}