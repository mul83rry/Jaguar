using System.Net;
using System.Net.WebSockets;
using Jaguar.Core.Dto;

namespace Jaguar.Core.Handlers;

internal static class Listener
{
    internal static Dictionary<string, byte> ListenersNameToId = new();
    
    internal static Action<WebSocketContext?, Packet>? OnMessageReceived;
}