using System.Net.WebSockets;

namespace Jaguar.Core.WebSocket;

public class WebSocketContextData
{
    public WebSocketContext SocketContext { get; init; }
    public User? User { get; set; }
    public Dictionary<string, byte> SupportedListeners { get; internal set; }
}