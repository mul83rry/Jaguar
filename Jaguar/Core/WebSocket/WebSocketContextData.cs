using System.Net.WebSockets;
using Jaguar.Core.Entity;

namespace Jaguar.Core.WebSocket;

public class WebSocketContextData
{
    public WebSocketContext SocketContext { get; init; }
    public User? User { get; set; }
    public Dictionary<string, byte> SupportedListeners { get; internal set; }
}